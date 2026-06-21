// README_MessageGeneration.cs
// ============================
// Documents which ROS2 messages must be generated before scripts compile.
//
// In Unity: Robotics -> Generate ROS Messages
// Point at the folder on Windows that contains the msg definitions.
//
// Required — generate these from the Ubuntu workspace:
//
//   doosan_teleop_msgs   (our custom package)
//     Source : ~/ros2_ws/src/doosan_teleop_msgs/msg/   <- copy from Ubuntu
//     Output : Assets/RosMessages/DoosanTeleopMsgs/msg/
//       VRControllerInputMsg.cs
//       VRHapticFeedbackMsg.cs
//       TeleopStatusMsg.cs
//
//   geometry_msgs / std_msgs / sensor_msgs  (standard — generate too)
//     Output : Assets/RosMessages/Geometry/ etc.
//
// C# namespace for doosan_teleop_msgs:
//   RosMessageTypes.DoosanTeleopMsgs
//
// Used by:
//   ButtonPublisher.cs         -> VRControllerInputMsg
//   HapticSubscriber.cs        -> VRHapticFeedbackMsg
//   TeleopStatusSubscriber.cs  -> TeleopStatusMsg
//   ControllerPublisher.cs     -> PoseStampedMsg    (geometry_msgs)
//   JointStateSubscriber.cs    -> JointStateMsg     (sensor_msgs)
