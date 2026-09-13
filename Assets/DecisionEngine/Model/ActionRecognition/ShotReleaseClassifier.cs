using System;
using UnityEngine;

namespace DecisionEngine.Model.ActionRecognition {
    // Recognizes the release of a jump shot from wrist and palm motion.
    //
    // Positions are expressed in a head-relative body frame (x = right, y = up, z = forward,
    // meters, head at the origin), so walking and head pitch do not pollute the signal.
    // Only the wrist and palm joints are used: they stay tracked while the fingers wrap a
    // real ball, whereas finger shape does not.
    //
    // A shot is recognized in three phases, each with its own thresholds in Params:
    //   1. Gather        — both wrists high and close together (the ball at the set point)
    //   2. Release       — the shooting wrist moves fast up-and-forward and ends above the head
    //   3. Follow-through — the palm faces forward-and-down after the release
    // Release is mandatory; gather and follow-through add confidence.
    // Pure C#: feed it samples with AddSample, then poll TryDetectShot.
    public sealed class ShotReleaseClassifier {
        [Serializable]
        public class Params {
            [Header("Gather")]
            [Tooltip("Wrist height (m, relative to the head) above which a hand counts as gathered at the set point.")]
            public float gatherWristHeight = -0.35f;
            [Tooltip("Max distance between the two wrists (m) for the gather to count.")]
            public float gatherHandsDistance = 0.35f;
            [Tooltip("How far back in time (s) to look for a gather before the release.")]
            public float gatherLookbackSeconds = 0.7f;

            [Header("Release")]
            [Tooltip("Wrist speed (m/s, head-relative, along up+forward) that counts as a release.")]
            public float releaseWristSpeed = 2.0f;
            [Tooltip("Wrist must end at least this high (m, relative to the head) at release.")]
            public float releaseWristHeight = 0.0f;

            [Header("Follow-through")]
            [Tooltip("How closely the palm must face forward-and-down (cosine of the angle) to count as a follow-through.")]
            public float followThroughPalmAlignment = 0.3f;

            [Header("Detection")]
            [Tooltip("Window (s) over which wrist velocity is measured.")]
            public float velocityWindowSeconds = 0.06f;
            [Tooltip("Confidence needed to report a shot.")]
            public float minConfidence = 0.6f;
            [Tooltip("Seconds after a detection during which no new shot can be reported.")]
            public float refractorySeconds = 2f;
        }

        public enum Hand { Left = 0, Right = 1 }

        public struct Sample {
            public float Time;
            public Vector3 Wrist;       // head-relative
            public Vector3 PalmNormal;  // head-relative, unit, direction the palm faces
            public bool Tracked;
        }

        public struct Detection {
            public float Time;
            public Hand Hand;
            public float Confidence;
            public bool Gather;
            public bool Release;
            public bool FollowThrough;
            public override string ToString() {
                return $"shot ({Hand}) confidence {Confidence:0.00} [gather={Gather} release={Release} followThrough={FollowThrough}]";
            }
        }

        private const int Capacity = 128;
        private const float ConfidenceRelease = 0.45f;
        private const float ConfidenceGather = 0.35f;
        private const float ConfidenceFollowThrough = 0.20f;
        private static readonly Vector3 UpAndForward = new Vector3(0f, 1f, 1f).normalized;
        private static readonly Vector3 ForwardAndDown = new Vector3(0f, -0.6f, 0.8f).normalized;

        private readonly Params _params;
        private readonly Sample[][] _samples = { new Sample[Capacity], new Sample[Capacity] };
        private readonly int[] _count = { 0, 0 };
        private readonly int[] _next = { 0, 0 };
        private float _lastDetectionTime = float.NegativeInfinity;

        public ShotReleaseClassifier(Params p = null) {
            _params = p ?? new Params();
        }

        public void AddSample(Hand hand, Sample s) {
            var h = (int)hand;
            _samples[h][_next[h]] = s;
            _next[h] = (_next[h] + 1) % Capacity;
            if (_count[h] < Capacity) {
                _count[h]++;
            }
        }

        public void Reset() {
            _count[0] = 0;
            _count[1] = 0;
            _lastDetectionTime = float.NegativeInfinity;
        }

        // Checks both hands at the given time. Returns true at most once per refractory period.
        public bool TryDetectShot(float time, out Detection best) {
            best = default;
            if (time - _lastDetectionTime < _params.refractorySeconds) {
                return false;
            }
            var bestConfidence = 0f;
            for (var h = 0; h < 2; h++) {
                if (_count[h] < 3) {
                    continue;
                }
                if (!TryLatestSample(h, out var now) || !now.Tracked || Mathf.Abs(now.Time - time) > 0.1f) {
                    continue;
                }
                if (!TryWristVelocity(h, now, out var velocity)) {
                    continue;
                }

                var release = Vector3.Dot(velocity, UpAndForward) > _params.releaseWristSpeed
                              && now.Wrist.y > _params.releaseWristHeight;
                if (!release) {
                    continue;
                }
                var gather = HadGather(now.Time);
                var followThrough = Vector3.Dot(now.PalmNormal, ForwardAndDown) > _params.followThroughPalmAlignment;
                var confidence = ConfidenceRelease
                                 + (gather ? ConfidenceGather : 0f)
                                 + (followThrough ? ConfidenceFollowThrough : 0f);
                if (confidence <= bestConfidence) {
                    continue;
                }
                bestConfidence = confidence;
                best = new Detection {
                    Time = now.Time,
                    Hand = (Hand)h,
                    Confidence = confidence,
                    Gather = gather,
                    Release = true,
                    FollowThrough = followThrough,
                };
            }
            if (bestConfidence < _params.minConfidence) {
                return false;
            }
            _lastDetectionTime = time;
            return true;
        }

        private Sample SampleFromEnd(int h, int stepsBack) {
            return _samples[h][(_next[h] - stepsBack + Capacity) % Capacity];
        }

        private bool TryLatestSample(int h, out Sample s) {
            s = default;
            if (_count[h] == 0) {
                return false;
            }
            s = SampleFromEnd(h, 1);
            return true;
        }

        // Finite-difference wrist velocity over roughly velocityWindowSeconds.
        private bool TryWristVelocity(int h, Sample now, out Vector3 velocity) {
            velocity = Vector3.zero;
            for (var i = 2; i <= _count[h]; i++) {
                var s = SampleFromEnd(h, i);
                if (!s.Tracked) {
                    return false;
                }
                var dt = now.Time - s.Time;
                if (dt >= _params.velocityWindowSeconds) {
                    velocity = (now.Wrist - s.Wrist) / dt;
                    return true;
                }
            }
            return false;
        }

        // Both wrists high and close together at some moment in the look-back window before now.
        private bool HadGather(float now) {
            for (var i = 1; i <= _count[0]; i++) {
                var left = SampleFromEnd(0, i);
                if (now - left.Time > _params.gatherLookbackSeconds) {
                    break;
                }
                if (now - left.Time < 0.1f || !left.Tracked || left.Wrist.y < _params.gatherWristHeight) {
                    continue;
                }
                if (TryNearestSample(1, left.Time, out var right)
                    && right.Tracked
                    && right.Wrist.y >= _params.gatherWristHeight
                    && Vector3.Distance(left.Wrist, right.Wrist) <= _params.gatherHandsDistance) {
                    return true;
                }
            }
            return false;
        }

        private bool TryNearestSample(int h, float time, out Sample best) {
            best = default;
            var bestDt = 0.05f;
            var found = false;
            for (var i = 1; i <= _count[h]; i++) {
                var s = SampleFromEnd(h, i);
                var dt = Mathf.Abs(s.Time - time);
                if (dt < bestDt) {
                    bestDt = dt;
                    best = s;
                    found = true;
                }
                if (s.Time < time - 0.2f) {
                    break;
                }
            }
            return found;
        }
    }
}
