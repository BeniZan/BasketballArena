using System;
using UnityEngine;
using Field.Hexagons;

namespace DecisionEngine.Model.Environment {
    // Physical constraints on the evaluated player. HoopEval's action space (any of the 19
    // nearest hexes per 0.2 s) says nothing about running through defenders, and with scripted,
    // non-reacting opponents an unbounded horizon makes "sprint to the rim" trivially optimal.
    // These knobs keep the optimal path human.
    [Serializable]
    public class MovementModel {
        [Tooltip("How many hex rings the player may move per 0.2 s frame. With the paper's cell size (20 across): 1 = one cell ≈ 3.8 m/s (jog), 2 = the paper's full 19-cell action space, up to ≈ 7.5 m/s (sprint).")]
        [Range(1, 2)] public int maxRing = 2;

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
