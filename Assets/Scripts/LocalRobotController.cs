using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Dsr; 

public class LocalRobotController : MonoBehaviour 
{
    [Header("Configuración del Robot")]
    public Transform[] robotJoints; 
    public TextMeshProUGUI uiText; 
    public float sensitivity = 40f; 

    [Header("Inputs VR (Cinemática Directa)")]
    public InputActionReference nextButton; 
    public InputActionReference prevButton; 
    public InputActionReference moveStick; 
    public InputActionReference executeTrigger; // Gatillo para enviar a ROS2

    [Header("Configuración ROS 2")]
    public string moveJointServiceName = "/dsr01/motion/move_joint"; 
    public double robotVelocity = 10.0;
    public double robotAcceleration = 20.0;

    private int currentJoint = 0;
    private ROSConnection ros;
    private bool isSending = false;

    void Start()
    {
        // Conectamos con el puente ROS2 y registramos únicamente el servicio
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterRosService<MoveJointRequest, MoveJointResponse>(moveJointServiceName);

        executeTrigger?.action.Enable();
    }

    void Update() 
    {
        // 1. Selección de Joint
        if (moveStick.action.ReadValue<Vector2>().sqrMagnitude > 0.01f)
            Debug.Log($"Reading from: {moveStick.action.activeControl?.path}");
            
        if (nextButton.action.WasPressedThisFrame()) 
            currentJoint = (currentJoint + 1) % robotJoints.Length;
            
        if (prevButton.action.WasPressedThisFrame()) 
            currentJoint = (currentJoint - 1 + robotJoints.Length) % robotJoints.Length;

        // 2. Movimiento Local con Joystick
        float inputY = moveStick.action.ReadValue<Vector2>().y;

        if (Mathf.Abs(inputY) > 0.1f) 
        {
            MoveJoint(robotJoints[currentJoint], inputY);
        }

        UpdateUI();

        // 3. Ejecución: Enviar pose final al robot real
        if (executeTrigger != null && executeTrigger.action.WasPressedThisFrame() && !isSending)
        {
            SendPoseToRealRobot();
        }
    }

    void MoveJoint(Transform joint, float input)
    {
        ArticulationBody ab = joint.GetComponent<ArticulationBody>();

        if (ab != null) 
        {
            var drive = ab.xDrive;
            drive.target += input * sensitivity * Time.deltaTime;
            ab.xDrive = drive;
        }
        else 
        {
            Vector3 axis = (currentJoint == 0 || currentJoint == 3 || currentJoint == 5) ? Vector3.up : Vector3.right;
            joint.Rotate(axis, input * sensitivity * Time.deltaTime);
        }
    }

    void UpdateUI() 
    {
        if (uiText == null) return;
        
        float angle = 0;
        ArticulationBody ab = robotJoints[currentJoint].GetComponent<ArticulationBody>();
        
        angle = (ab != null) ? ab.jointPosition[0] * Mathf.Rad2Deg : robotJoints[currentJoint].localEulerAngles.x;
        if (angle > 180) angle -= 360;

        uiText.text = $"JOINT: {currentJoint + 1}\nÁNGULO: {angle:F1}°";
    }

    // ==========================================
    // COMUNICACIÓN CON ROS 2 (Servicio MoveJoint)
    // ==========================================
    void SendPoseToRealRobot()
    {
        isSending = true;
        Debug.Log("<color=cyan>[LocalRobotController] Empaquetando ángulos. Enviando al robot físico...</color>");

        double[] currentAngles = new double[6];

        for (int i = 0; i < 6; i++)
        {
            ArticulationBody ab = robotJoints[i].GetComponent<ArticulationBody>();
            if (ab != null)
            {
                currentAngles[i] = (double)(ab.jointPosition[0] * Mathf.Rad2Deg);
            }
            else
            {
                float angle = robotJoints[i].localEulerAngles.x;
                if (angle > 180) angle -= 360;
                currentAngles[i] = (double)angle;
            }
        }

        MoveJointRequest request = new MoveJointRequest
        {
            pos = currentAngles,
            vel = robotVelocity,
            acc = robotAcceleration,
            time = 0.0,
            radius = 0.0,
            mode = 0,
            blend_type = 0, 
            sync_type = 1   
        };

        ros.SendServiceMessage<MoveJointResponse>(moveJointServiceName, request, OnMoveJointResponseReceived);
    }

    void OnMoveJointResponseReceived(MoveJointResponse response)
    {
        isSending = false;
        
        if (response.success) 
        {
            Debug.Log("<color=green>[LocalRobotController] ¡Robot real aceptó el movimiento!</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>[LocalRobotController] Comando rechazado por el controlador físico.</color>");
        }
    }
}