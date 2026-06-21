using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

/// <summary>
/// Subscribes to /joint_states from ROS2 and drives the Doosan M1509
/// ArticulationBody joints in Unity to match the real robot in real time.
///
/// Inspector setup:
///   1. Attach to the root GameObject of the imported M1509 URDF.
///   2. Expand the URDF hierarchy, find joint_1 through joint_6 GameObjects.
///   3. Drag them into the Joints array in order (joint_1 at index 0).
///   4. Leave Topic as /joint_states.
/// </summary>
public class JointStateSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    public string topic = "/joint_states";

    [Header("Robot Joints — drag joint_1 to joint_6 in order")]
    public ArticulationBody[] joints;

    private ROSConnection _ros;
    private float[]       _targetPositions;
    private bool          _hasNewData = false;
    private readonly object _lock = new object();

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.Subscribe<JointStateMsg>(topic, OnJointState);
        _targetPositions = new float[joints.Length];
        Debug.Log($"[JointStateSubscriber] Subscribed to {topic} — {joints.Length} joints");
    }

    void OnJointState(JointStateMsg msg)
    {
        lock (_lock)
        {
            int count = Mathf.Min(msg.position.Length, joints.Length);
            for (int i = 0; i < count; i++)
                _targetPositions[i] = (float)msg.position[i] * Mathf.Rad2Deg;
            _hasNewData = true;
        }
    }

    void Update()
    {
        if (!_hasNewData) return;

        float[] targets;
        lock (_lock)
        {
            targets     = (float[])_targetPositions.Clone();
            _hasNewData = false;
        }

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;
            var drive    = joints[i].xDrive;
            drive.target = targets[i];
            joints[i].xDrive = drive;
        }
    }
}
