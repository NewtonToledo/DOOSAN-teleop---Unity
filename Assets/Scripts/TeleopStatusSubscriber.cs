using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.DoosanTeleop;
using TMPro;

/// <summary>
/// Subscribes to /teleop/status (doosan_teleop_msgs/TeleopStatus)
/// published by vr_bridge_node and displays the system state in VR.
///
/// Inspector setup:
///   1. Attach to any GameObject.
///   2. Optionally assign TextMeshProUGUI labels and indicator objects.
///      All fields are optional — script works with none assigned.
/// </summary>
public class TeleopStatusSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    public string statusTopic = "/teleop/status";

    [Header("VR Status Labels (TextMeshPro — all optional)")]
    public TextMeshProUGUI fullStatusLabel;
    public TextMeshProUGUI vrConnectedLabel;
    public TextMeshProUGUI teleopActiveLabel;
    public TextMeshProUGUI servoStatusLabel;

    [Header("Indicator Objects (optional)")]
    [Tooltip("Shown/hidden based on VR connection state")]
    public GameObject vrIndicator;
    [Tooltip("Shown/hidden based on teleop active state")]
    public GameObject teleopIndicator;

    private static readonly Color ColOK      = Color.green;
    private static readonly Color ColWarning = Color.yellow;
    private static readonly Color ColError   = new Color(1f, 0.3f, 0.3f);

    private ROSConnection    _ros;
    private TeleopStatusMsg  _lastStatus;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.Subscribe<TeleopStatusMsg>(statusTopic, OnStatus);
        Debug.Log($"[TeleopStatusSubscriber] Subscribed to {statusTopic}");
    }

    void OnStatus(TeleopStatusMsg msg)
    {
        _lastStatus = msg;
    }

    void Update()
    {
        if (_lastStatus == null) return;
        var msg = _lastStatus;

        if (fullStatusLabel != null)
            fullStatusLabel.text = msg.status_message;

        if (vrConnectedLabel != null)
        {
            vrConnectedLabel.text  = msg.vr_connected ? "VR: Connected" : "VR: Disconnected";
            vrConnectedLabel.color = msg.vr_connected ? ColOK : ColError;
        }

        if (teleopActiveLabel != null)
        {
            teleopActiveLabel.text  = msg.teleop_active ? "Teleop: ACTIVE" : "Teleop: PAUSED";
            teleopActiveLabel.color = msg.teleop_active ? ColOK : ColWarning;
        }

        if (servoStatusLabel != null)
        {
            string name  = ServoStatusName(msg.servo_status);
            Color  color = msg.servo_status <= 1 ? ColOK
                         : msg.servo_status <= 2 ? ColWarning : ColError;
            servoStatusLabel.text  = $"Servo: {name}";
            servoStatusLabel.color = color;
        }

        if (vrIndicator    != null) vrIndicator.SetActive(msg.vr_connected);
        if (teleopIndicator != null) teleopIndicator.SetActive(msg.teleop_active);
    }

    static string ServoStatusName(byte code)
    {
        switch (code)
        {
            case 0:  return "INVALID";
            case 1:  return "OK";
            case 2:  return "NEAR SINGULARITY";
            case 3:  return "SINGULARITY HALT";
            case 4:  return "NEAR COLLISION";
            case 5:  return "COLLISION HALT";
            case 6:  return "JOINT BOUND";
            default: return $"UNKNOWN({code})";
        }
    }
}
