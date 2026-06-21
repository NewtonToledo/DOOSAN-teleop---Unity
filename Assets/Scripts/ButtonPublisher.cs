using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.DoosanTeleop;

/// <summary>
/// Reads Meta Quest 2 right controller buttons and publishes
/// doosan_teleop_msgs/VRControllerInput to /vr/right/inputs.
///
/// Also drives ControllerPublisher pause/anchor-reset on lower
/// button rising edge.
///
/// Inspector setup:
///   - Lower Button      : RobotTeleop / RightController / LowerButton
///   - Upper Button      : RobotTeleop / RightController / UpperButton
///   - Trigger Axis      : RobotTeleop / RightController / TriggerAxis
///   - Thumbstick        : RobotTeleop / RightController / Thumbstick
///   - ControllerPublisher : drag the ControllerPublisher component here
/// </summary>
public class ButtonPublisher : MonoBehaviour
{
    [Header("ROS Settings")]
    public string inputsTopic = "/vr/right/inputs";
    [Range(10, 100)]
    public float publishHz = 30f;

    [Header("Input Actions — assign from RobotTeleop asset")]
    public InputActionReference lowerButton;
    public InputActionReference upperButton;
    public InputActionReference triggerAxis;
    public InputActionReference thumbstick;

    [Header("References")]
    public ControllerPublisher controllerPublisher;

    private ROSConnection _ros;
    private float _publishInterval;
    private float _timer;
    private bool  _prevLower = false;
    private bool  _isPaused  = false;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<VRControllerInputMsg>(inputsTopic);
        _publishInterval = 1f / publishHz;
        lowerButton?.action.Enable();
        upperButton?.action.Enable();
        triggerAxis?.action.Enable();
        thumbstick?.action.Enable();
        Debug.Log($"[ButtonPublisher] Ready — publishing {inputsTopic} at {publishHz} Hz");
    }

    void Update()
    {
        _timer += Time.deltaTime;

        bool lower = lowerButton != null && lowerButton.action.ReadValue<float>() > 0.5f;
        if (lower && !_prevLower)
        {
            _isPaused = !_isPaused;
            if (controllerPublisher != null)
            {
                controllerPublisher.SetPaused(_isPaused);
                if (!_isPaused) controllerPublisher.ResetAnchor();
            }
            Debug.Log(_isPaused ? "[ButtonPublisher] Teleop PAUSED" : "[ButtonPublisher] Teleop RESUMED");
        }
        _prevLower = lower;

        if (_timer < _publishInterval) return;
        _timer = 0f;

        bool    upper = upperButton != null && upperButton.action.ReadValue<float>() > 0.5f;
        float   trig  = triggerAxis != null ? triggerAxis.action.ReadValue<float>() : 0f;
        Vector2 stick = thumbstick  != null ? thumbstick.action.ReadValue<Vector2>() : Vector2.zero;

        _ros.Publish(inputsTopic, new VRControllerInputMsg
        {
            button_lower   = lower,
            button_upper   = upper,
            trigger_index  = trig,
            trigger_middle = 0f,
            thumbstick_x   = stick.x,
            thumbstick_y   = stick.y,
        });
    }
}
