using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which animation clip each character is playing at a given drill time.
/// Ported from the exporter's DrillAnimator, with one difference: a gate's start time
/// comes from when it actually opened at runtime instead of always from
/// <see cref="DrillTrigger.NominalTime"/>, so a player reaching a trigger early advances
/// the drill early.
///
/// A null <paramref name="gateOpenTimes"/> means "no runtime gate state", and every gate
/// falls back to its nominal time. That keeps playback identical to the exporter preview.
/// </summary>
public static class DrillSegmentResolver {
    /// <summary>Open time value meaning the gate has not fired yet.</summary>
    public const float GATE_CLOSED = -1f;

    /// <summary>
    /// Picks the segment a character is in at <paramref name="time"/>: its base animation
    /// until a gate opens, then the clip of the last gate whose start time has passed.
    /// </summary>
    public static void ResolveSegment(DrillData drill, CharData data, float time,
                                      IReadOnlyList<float> gateOpenTimes,
                                      out AnimationClip clip, out float localTime) {
        clip = data.Animation;
        localTime = time + data.AnimationTimeOffset;

        foreach (var trigger in data.AnimationTriggers) {
            if (!trigger.TriggeredClip)
                continue;
            if (!TryGetSegmentStart(drill, trigger, gateOpenTimes, out var start) || start > time)
                continue;
            clip = trigger.TriggeredClip;
            localTime = time - start;
        }
    }

    /// <summary>
    /// When the clip gated behind <paramref name="trigger"/> starts playing.
    /// Returns false for an out of range trigger index, or for a gate that is still shut.
    /// </summary>
    public static bool TryGetSegmentStart(DrillData drill, CharData.CharAnimationTrigger trigger,
                                          IReadOnlyList<float> gateOpenTimes, out float start) {
        start = 0f;
        if (!drill)
            return false;

        var idx = trigger.TriggerIndex;
        if (idx < 0 || idx >= drill.Triggers.Count)
            return false;

        var openTime = GetOpenTime(drill, idx, gateOpenTimes);
        if (openTime < 0f)
            return false;

        start = openTime + trigger.DelayAfterTrigger;
        return true;
    }

    /// <summary>
    /// Longest the drill can run. Gates that have not fired are assumed to fire at their
    /// nominal time, which is the latest they can fire because the server force opens them
    /// there, so this stays an upper bound and the drill cannot be declared finished early.
    /// </summary>
    public static float CalculateMaxAnimationTime(DrillData drill, IReadOnlyList<float> gateOpenTimes) {
        if (!drill)
            return 0f;

        var max = 0f;
        foreach (var data in drill.CharsData) {
            if (data == null)
                continue;

            var baseLength = data.Animation ? data.Animation.length : 0f;
            max = Mathf.Max(max, baseLength + Mathf.Abs(data.AnimationTimeOffset));

            foreach (var trigger in data.AnimationTriggers) {
                if (!trigger.TriggeredClip)
                    continue;

                var idx = trigger.TriggerIndex;
                if (idx < 0 || idx >= drill.Triggers.Count)
                    continue;

                var openTime = GetOpenTime(drill, idx, gateOpenTimes);
                if (openTime < 0f)
                    openTime = drill.Triggers[idx].NominalTime;

                max = Mathf.Max(max, openTime + trigger.DelayAfterTrigger + trigger.TriggeredClip.length);
            }
        }
        return max;
    }

    static float GetOpenTime(DrillData drill, int idx, IReadOnlyList<float> gateOpenTimes) {
        if (gateOpenTimes == null)
            return drill.Triggers[idx].NominalTime;
        if (idx >= gateOpenTimes.Count)
            return GATE_CLOSED;
        return gateOpenTimes[idx];
    }
}
