using System.Collections.Generic;
using UnityEngine;
using DecisionEngine.Model.Environment;

// Samples the scripted characters' positions over the whole drill by scrubbing the very
// same CharComponent.SetAnimationTime the visuals use, then restores them.
// Because it reads what the headset actually renders, the engine can never disagree with
// the animation; the cost is one ~50 ms hitch at drill activation.
// Swap this for an offline bake by producing the same AgentTrajectorySet.
public static class LiveTrajectoryCapture {
    public static AgentTrajectorySet Capture(IReadOnlyList<CharComponent> chars, float maxTime, float dt, CalibratedCourtFrame frame, float restoreTime) {
        var frames = Mathf.Max(2, Mathf.CeilToInt(maxTime / dt) + 1);
        var set = new AgentTrajectorySet { dt = dt, frameCount = frames, agents = new AgentTrajectorySet.Agent[chars.Count] };
        var bones = new Transform[chars.Count];
        var savedPos = new Vector3[chars.Count];
        var savedRot = new Quaternion[chars.Count];

        for (var i = 0; i < chars.Count; i++) {
            var c = chars[i];
            bones[i] = BoneUsedForPosition(c);
            savedPos[i] = c.transform.localPosition;
            savedRot[i] = c.transform.localRotation;
            set.agents[i] = new AgentTrajectorySet.Agent {
                name = c.name,
                isOpponent = c.Data != null && !c.Data.IsFriendly,
                positions = new Vector2[frames],
            };
        }

        for (var f = 0; f < frames; f++) {
            var t = f * dt;
            for (var i = 0; i < chars.Count; i++) {
                chars[i].SetAnimationTime(t);
                set.agents[i].positions[f] = frame.WorldToCourt(bones[i].position);
            }
        }

        // Put the characters back exactly where playback expects them.
        for (var i = 0; i < chars.Count; i++) {
            chars[i].SetAnimationTime(restoreTime);
            chars[i].transform.SetLocalPositionAndRotation(savedPos[i], savedRot[i]);
        }
        return set;
    }

    // Hips when the rig is humanoid (≈ center of mass on the floor), else the head.
    public static Transform BoneUsedForPosition(CharComponent c) {
        var skin = c.ActiveSkin;
        var animator = skin?.Animator;
        if (animator != null && animator.isHuman) {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) return hips;
        }
        return skin?.HeadTf != null ? skin.HeadTf : c.transform;
    }
}
