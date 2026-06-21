using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

/// <summary>
/// Reads the Meta Quest 2 right controller pose and publishes relative
/// delta poses to /vr/right/pose at 30 Hz.
///
/// Delta = change since last anchor. Press lower button to pause and
/// reset anchor — reposition your hand without moving the robot.
///
/// Inspector setup:
///   - Right Position : RobotTeleop / RightController / RightPosition
///   - Right Rotation : RobotTeleop / RightController / RightRotation
/// </summary>
public class ControllerPublisher : MonoBehaviour
{
    [Header("ROS Settings")]
    public string poseTopic = "/vr/right/pose";
    public string frameId   = "world";
    [Range(10, 100)]
    public float publishHz  = 1.0f;

    [Header("Input Actions — assign from RobotTeleop asset")]
    public InputActionReference rightPosition;
    public InputActionReference rightRotation;

    [Header("Gain / Safety")]
    [Tooltip("Scales translation delta into m/s for Servo")]
    public float linearGain  = 1.0f;
    [Tooltip("Scales rotation delta into rad/s for Servo")]
    public float angularGain = 1.0f;
    [Tooltip("Minimum translation magnitude to publish (eliminates hand tremor)")]
    public float deadband    = 0.002f;

    private ROSConnection _ros;
    private float         _publishInterval;
    private float         _timer;
    private Vector3       _anchorPos;
    private Quaternion    _anchorRot;
    private bool          _paused = false;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<PoseStampedMsg>(poseTopic);
        _publishInterval = 1f / publishHz;
        rightPosition?.action.Enable();
        rightRotation?.action.Enable();
        ResetAnchor();
        Debug.Log($"[ControllerPublisher] Ready — publishing {poseTopic} at {publishHz} Hz");
    }

    void Update()
    {
        if (_paused) return;

        _timer += Time.deltaTime;
        if (_timer < _publishInterval) return;
        _timer = 0f;

        if (rightPosition == null || rightRotation == null) return;

        Vector3    currentPos = rightPosition.action.ReadValue<Vector3>();
        Quaternion currentRot = rightRotation.action.ReadValue<Quaternion>();

        Vector3    deltaPos = currentPos - _anchorPos;
        Quaternion deltaRot = currentRot * Quaternion.Inverse(_anchorRot);

        if (deltaPos.magnitude < deadband) deltaPos = Vector3.zero;
        deltaPos *= linearGain;

        float t = Time.realtimeSinceStartup;
        var msg = new PoseStampedMsg
        {
            header = new HeaderMsg
            {
                frame_id = frameId,
                stamp    = new TimeMsg
                {
                    sec     = (int)t,
                    nanosec = (uint)((t % 1f) * 1e9f)
                }
            },
            pose = new PoseMsg
            {
                position    = deltaPos.To<FLU>(),
                orientation = deltaRot.To<FLU>()
            }
        };

        _ros.Publish(poseTopic, msg);
    }

    public void ResetAnchor()
    {
        if (rightPosition != null)
            _anchorPos = rightPosition.action.ReadValue<Vector3>();
        if (rightRotation != null)
            _anchorRot = rightRotation.action.ReadValue<Quaternion>();
        Debug.Log("[ControllerPublisher] Anchor reset");
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        Debug.Log(paused ? "[ControllerPublisher] PAUSED" : "[ControllerPublisher] RESUMED");
    }
}
