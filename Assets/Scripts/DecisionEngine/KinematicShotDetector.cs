using System;
using DecisionEngine.Model.ActionRecognition;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.XR.Hands;

// Shot recognition from hand tracking: converts the wrist and palm joints of both hands into
// a head-relative body frame on every tracking update and hands them to ShotReleaseClassifier.
public class KinematicShotDetector : MonoBehaviour, IShotDetector {
    [SerializeField, InlineProperty, HideLabel] private ShotReleaseClassifier.Params parameters = new ShotReleaseClassifier.Params();
    [SerializeField, Tooltip("Log every detection with its hand and phase breakdown.")] private bool verbose = true;

    [ShowInInspector, ReadOnly] private string _lastDetection = "";

    public event Action Shot;

    private ShotReleaseClassifier _classifier;
    private XRDeviceInstance _device;
    private CustomLogger _logger;

    private void Awake() {
        _classifier = new ShotReleaseClassifier(parameters);
        _logger = new CustomLogger(this, Color.yellow);
    }

    private void OnEnable() {
        _device = GetComponentInParent<XRDeviceInstance>() ?? XRDeviceInstance.Instance;
        if (_device == null) {
            return;
        }
        if (_device.LeftTracking) {
            _device.LeftTracking.jointsUpdated.AddListener(OnLeftJoints);
        }
        if (_device.RightTracking) {
            _device.RightTracking.jointsUpdated.AddListener(OnRightJoints);
        }
        _classifier.Reset();
    }

    private void OnDisable() {
        if (_device == null) {
            return;
        }
        if (_device.LeftTracking) {
            _device.LeftTracking.jointsUpdated.RemoveListener(OnLeftJoints);
        }
        if (_device.RightTracking) {
            _device.RightTracking.jointsUpdated.RemoveListener(OnRightJoints);
        }
    }

    private void OnLeftJoints(XRHandJointsUpdatedEventArgs args) {
        OnJoints(ShotReleaseClassifier.Hand.Left, args);
    }

    private void OnRightJoints(XRHandJointsUpdatedEventArgs args) {
        OnJoints(ShotReleaseClassifier.Hand.Right, args);
    }

    private void OnJoints(ShotReleaseClassifier.Hand hand, XRHandJointsUpdatedEventArgs args) {
        var head = _device && _device.HeadCam ? _device.HeadCam.transform : null;
        if (head == null) {
            return;
        }
        var sample = new ShotReleaseClassifier.Sample { Time = Time.time };

        if (args.hand.isTracked
            && args.hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose)
            && args.hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose)) {
            // Joint poses are in XR Origin space; bring them to world, then into the head's
            // yaw-only frame (x right, y up, z forward) so body motion and head pitch cancel out.
            var originTransform = OriginTransform();
            var wristWorld = originTransform ? originTransform.TransformPoint(wristPose.position) : wristPose.position;
            var palmRotationWorld = originTransform ? originTransform.rotation * palmPose.rotation : palmPose.rotation;
            var inverseYaw = Quaternion.Inverse(HeadYawRotation(head));
            sample.Wrist = inverseYaw * (wristWorld - head.position);
            // XR Hands: a joint's +Y points out of the back of the hand, so the palm faces -Y.
            sample.PalmNormal = inverseYaw * (palmRotationWorld * Vector3.down);
            sample.Tracked = true;
        }

        _classifier.AddSample(hand, sample);
        if (_classifier.TryDetectShot(sample.Time, out var detection)) {
            _lastDetection = detection.ToString();
            if (verbose) {
                _logger.Log($"Shot recognized: {detection}");
            }
            Shot?.Invoke();
        }
    }

    private Transform OriginTransform() {
        var origin = _device ? _device.Origin : null;
        if (origin == null) {
            return null;
        }
        return origin.Origin != null ? origin.Origin.transform : origin.transform;
    }

    private static Quaternion HeadYawRotation(Transform head) {
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) {
            forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        }
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
