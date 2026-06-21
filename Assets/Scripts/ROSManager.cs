using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using TMPro;

/// <summary>
/// Central ROS connection manager.
/// Initialises ROSConnection and shows status as a floating VR label.
///
/// Inspector setup:
///   1. Attach to an empty GameObject named "ROSManager".
///   2. Set ROS IP to your Ubuntu PC's WiFi IP (e.g. 192.168.x.x).
///      Leave empty to use Robotics -> ROS Settings value instead.
///   3. Optionally drag a world-space TextMeshProUGUI for status display.
/// </summary>
public class ROSManager : MonoBehaviour
{
    [Header("ROS Connection")]
    [Tooltip("Ubuntu PC WiFi IP. Leave empty to use Robotics -> ROS Settings.")]
    public string rosIP   = "";
    public int    rosPort = 10000;

    [Header("VR Status Display (optional)")]
    [Tooltip("World-space TextMeshPro label visible inside the headset")]
    public TextMeshProUGUI statusText;

    private ROSConnection _ros;
    private float         _checkTimer    = 0f;
    private const float   CHECK_INTERVAL = 2f;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();

        if (!string.IsNullOrEmpty(rosIP))
        {
            _ros.RosIPAddress = rosIP;
            _ros.RosPort      = rosPort;
        }

        SetStatus("Connecting to ROS...");
        Debug.Log($"[ROSManager] Connecting to {_ros.RosIPAddress}:{_ros.RosPort}");
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < CHECK_INTERVAL) return;
        _checkTimer = 0f;

        SetStatus(_ros != null
            ? $"ROS Connected\n{_ros.RosIPAddress}:{_ros.RosPort}"
            : "ROS Disconnected");
    }

    void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}
