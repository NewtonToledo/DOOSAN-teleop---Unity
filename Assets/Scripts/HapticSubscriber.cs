using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.DoosanTeleop;
using System.Linq;

/// <summary>
/// Subscribes to /vr/right/haptic (doosan_teleop_msgs/VRHapticFeedback)
/// and sends impulses to the Quest 2 right controller.
///
/// Sent by haptic_node.py on the ROS side when MoveIt Servo reports
/// a warning (near singularity, joint bound, collision, etc).
///
/// No inspector setup needed — auto-finds the right controller device.
/// </summary>
public class HapticSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    public string hapticTopic = "/vr/right/haptic";

    private ROSConnection _ros;
    private InputDevice   _rightController;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.Subscribe<VRHapticFeedbackMsg>(hapticTopic, OnHaptic);
        FindRightController();
        Debug.Log($"[HapticSubscriber] Subscribed to {hapticTopic}");
    }

    void FindRightController()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand))
            {
                _rightController = device;
                Debug.Log($"[HapticSubscriber] Found right controller: {_rightController.name}");
                return;
            }
        }
        Debug.LogWarning("[HapticSubscriber] Right controller not found — will retry on first haptic event.");
    }

    void OnHaptic(VRHapticFeedbackMsg msg)
    {
        if (_rightController == null) FindRightController();
        if (_rightController == null) return;

        if (_rightController is XRControllerWithRumble rumble)
        {
            float duration = msg.seconds > 0f ? msg.seconds
                           : (msg.frequency > 0f ? 1f / msg.frequency : 0.1f);
            rumble.SendImpulse(msg.amplitude, duration);
        }
    }
}
