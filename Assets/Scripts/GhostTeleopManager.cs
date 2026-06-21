using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Trajectory;       // Nuevo: Para enviar directo a los motores
using RosMessageTypes.BuiltinInterfaces; // Nuevo: Para el formato de tiempo en ROS2
using RosMessageTypes.Assets;         // Ajusta según tu namespace de Ikin

public class GhostTeleopManager : MonoBehaviour
{
    private enum TeleopState { IDLE, PLANNING, EXECUTING }
    private TeleopState currentState = TeleopState.IDLE;

    [Header("ROS 2 Config")]
    public string jointStateTopic = "/joint_states";
    public string ikinServiceName = "/dsr01/motion/ikin"; 
    // Tópico directo al controlador de bajo nivel de Doosan
    public string jointTrajectoryTopic = "/dsr_controller2/joint_trajectory"; 
    private ROSConnection ros;

    [Header("Inputs")]
    public InputActionReference upperTrigger; // Planear
    public InputActionReference lowerTrigger; // Ejecutar
    public Transform rightHandTransform;

    [Header("Ghost Robot (Holograma)")]
    public ArticulationBody[] ghostJoints; 
    private float[] targetJointAngles = new float[6]; 
    private float[] currentVisualAngles = new float[6]; 
    private float[] jointVelocities = new float[6]; 

    [Header("Configuración Híbrida")]
    public float serviceCallRate = 15f; // Hz
    public float visualSmoothTime = 0.05f; 
    public float executionTime = 2.0f; // Segundos: Tiempo estricto que tardará el robot real en moverse
    
    private float nextServiceCallTime = 0f;
    private bool isWaitingForService = false;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        
        // Registramos el publicador de trayectoria articular directa
        ros.RegisterPublisher<JointTrajectoryMsg>(jointTrajectoryTopic);
        ros.Subscribe<JointStateMsg>(jointStateTopic, JointStateCallback);
        ros.RegisterRosService<IkinRequest, IkinResponse>(ikinServiceName);

        upperTrigger.action.Enable();
        lowerTrigger.action.Enable();

        // Encendemos los motores virtuales del holograma
        foreach (var joint in ghostJoints)
        {
            var drive = joint.xDrive;
            drive.stiffness = 100000f;
            drive.damping = 10000f;
            joint.xDrive = drive;
        }
    }

    void Update()
    {
        bool isUpperPressed = upperTrigger.action.ReadValue<float>() > 0.5f;
        bool isLowerPressed = lowerTrigger.action.ReadValue<float>() > 0.5f;

        switch (currentState)
        {
            case TeleopState.IDLE:
                if (isUpperPressed)
                {
                    currentState = TeleopState.PLANNING;
                    Debug.Log("<color=green>[Ghost] MODO PLANNING INICIADO.</color>");
                }
                break;

            case TeleopState.PLANNING:
                // Consultamos la IK a Doosan
                if (Time.time >= nextServiceCallTime && !isWaitingForService)
                {
                    nextServiceCallTime = Time.time + (1f / serviceCallRate);
                    RequestDoosanIK(rightHandTransform.localPosition, rightHandTransform.localRotation);
                }

                if (!isUpperPressed)
                {
                    currentState = TeleopState.IDLE;
                    Debug.Log("<color=yellow>[Ghost] PLANNING ABORTADO.</color>");
                }
                else if (isLowerPressed)
                {
                    currentState = TeleopState.EXECUTING;
                    Debug.Log("<color=cyan>[Ghost] EJECUTANDO: Control Articular Directo.</color>");
                    StartCoroutine(ExecuteDirectJointTrajectory());
                }
                break;

            case TeleopState.EXECUTING:
                // Bloqueado hasta que termine la orden a los motores
                break;
        }

        // Siempre suavizamos visualmente el holograma
        UpdateVisualHologram();
    }

    private void RequestDoosanIK(Vector3 unityPos, Quaternion unityRot)
    {
        isWaitingForService = true;
        var rosPos = unityPos.To<FLU>();
        
        Vector3 unityEuler = unityRot.eulerAngles;
        IkinRequest req = new IkinRequest();
        req.pos = new double[] { rosPos.x * 1000.0, rosPos.y * 1000.0, rosPos.z * 1000.0, unityEuler.z, -unityEuler.x, unityEuler.y };
        req.@ref = 0; 

        ros.SendServiceMessage<IkinResponse>(ikinServiceName, req, OnIKResponseReceived);
    }

    private void OnIKResponseReceived(IkinResponse response)
    {
        isWaitingForService = false;
        if (response.success)
        {
            Debug.Log($"<color=green>[Ghost] IK Aceptado. Ángulos: {response.conv_posj[0]:F1}, {response.conv_posj[1]:F1}, {response.conv_posj[2]:F1}...</color>");
            for (int i = 0; i < 6; i++)
            {
                targetJointAngles[i] = (float)response.conv_posj[i];
            }
        }
        else
        {
            // Si Doosan rechaza la pose, esto nos dirá qué coordenada estamos pidiendo mal.
            Vector3 handPos = rightHandTransform.localPosition;
            Debug.LogWarning($"<color=orange>[Ghost] IK RECHAZADO por Doosan. Pose inalcanzable. XYZ (Unity): {handPos.x:F2}, {handPos.y:F2}, {handPos.z:F2}</color>");
        }
    }

    private void UpdateVisualHologram()
    {
        for (int i = 0; i < ghostJoints.Length; i++)
        {
            currentVisualAngles[i] = ghostJoints[i].jointPosition[0] * Mathf.Rad2Deg;
            float smoothedAngle = Mathf.SmoothDampAngle(currentVisualAngles[i], targetJointAngles[i], ref jointVelocities[i], visualSmoothTime);
            
            var drive = ghostJoints[i].xDrive;
            drive.target = smoothedAngle;
            ghostJoints[i].xDrive = drive;
        }
    }

    // ==========================================
    // EL BYPASS: CONTROL ARTICULAR DIRECTO
    // ==========================================
    private IEnumerator ExecuteDirectJointTrajectory()
    {
        // 1. Creamos el mensaje de trayectoria
        var msg = new JointTrajectoryMsg();
        msg.joint_names = new string[] { "joint1", "joint2", "joint3", "joint4", "joint5", "joint6" };

        // 2. Creamos el único punto de destino (la meta)
        var point = new JointTrajectoryPointMsg();
        point.positions = new double[6];

        for (int i = 0; i < 6; i++)
        {
            // CRÍTICO: ROS2 exige RADIANES, pero Doosan IK nos dio grados. Convertimos:
            point.positions[i] = targetJointAngles[i] * Mathf.Deg2Rad;
        }

        // 3. Le decimos al controlador cuánto tiempo tiene para llegar suavemente
        point.time_from_start = new DurationMsg 
        { 
            sec = (int)Mathf.Floor(executionTime), 
            nanosec = (uint)((executionTime - Mathf.Floor(executionTime)) * 1e9) 
        };

        msg.points = new JointTrajectoryPointMsg[] { point };

        // 4. Disparamos la orden directa a los motores (Adiós MoveIt Servo)
        ros.Publish(jointTrajectoryTopic, msg);

        // Esperamos exactamente el tiempo que le dijimos al robot que tardaría
        yield return new WaitForSeconds(executionTime + 0.5f);

        currentState = TeleopState.IDLE;
        Debug.Log("<color=green>[Ghost] Trayectoria completada.</color>");
    }

    void JointStateCallback(JointStateMsg msg)
    {
        if (currentState == TeleopState.IDLE)
        {
            for (int i = 0; i < ghostJoints.Length; i++)
            {
                // ROS nos envía radianes en el joint_states, los pasamos a grados para Unity
                targetJointAngles[i] = (float)msg.position[i] * Mathf.Rad2Deg; 
            }
        }
    }
}