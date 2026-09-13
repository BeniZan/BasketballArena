using System;
using UnityEngine;
using Field.Hexagons;

namespace DecisionEngine.Model.Environment {
    // Physical constraints on the evaluated player. HoopEval's raw action space (any of the
    // 19 nearest hexes per 0.2 s) allows ~10 m/s and running through defenders; with scripted,
    // non-reacting opponents that makes "sprint to the rim" trivially optimal. These knobs
    // keep the optimal path human and consistent with the test bot.
    [Serializable]
    public class MovementModel {
        [Tooltip("How many hex rings the player may move per frame: 1 ≈ 5 m/s bursts (realistic), 2 = the paper's full 19-cell action space (~10 m/s).")]
        [Range(1, 2)] public int maxRing = 1;

        [Tooltip("Cells whose center is within this many meters of an opponent are impassable (you cannot run through a defender).")]
        [Min(0f)] public float blockedRadius = 0.6f;

        [Tooltip("Seconds after the session starts by which the plan must have shot; 0 = the whole drill. Keeps the optimal path a decision, not a marathon.")]
        [Min(0f)] public float decisionWindowSeconds = 3f;

        public bool IsActionAllowed(int action) {
            return HexagonCell.DistanceInCells(HexagonGrid.MovementActions[action], HexagonCell.Zero) <= maxRing;
        }

        // Last frame index the plan may still act on, given the start frame.
        public int LastFrameToActOn(int startFrame, int frameCount, float dt) {
            var last = frameCount - 1;

            return decisionWindowSeconds <= 0f ? last : Mathf.Min(last, startFrame + Mathf.RoundToInt(decisionWindowSeconds / dt));
        }
    }
}
