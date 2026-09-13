using System;
using UnityEngine;

namespace DecisionEngine.Model.Environment {
    // positions of every scripted agent sampled at a fixed rate. Coordinates are
    // whatever frame the producer chose (drill-local when baked, court meters once the
    // runtime adapter has mapped them) — the engine only requires that all agents and
    // the player share one frame.
    [Serializable]
    public class AgentTrajectorySet {
        [Serializable]
        public class Agent {
            public string name;
            public bool isOpponent;
            public Vector2[] positions = Array.Empty<Vector2>();
        }

        public float dt = 0.2f;
        public int frameCount;
        public Agent[] agents = Array.Empty<Agent>();

        public int OpponentCount {
            get { int n = 0; foreach (var a in agents) if (a.isOpponent) n++; return n; }
        }

        // Copies the opponent positions at a frame into buffer and returns the count.
        public int CopyOpponentPositionsAt(int frame, Vector2[] buffer) {
            var n = 0;
            foreach (var a in agents) {
                if (!a.isOpponent) continue;
                buffer[n++] = a.positions[Mathf.Clamp(frame, 0, a.positions.Length - 1)];
            }
            return n;
        }

        public void Validate() {
            if (dt <= 0f) throw new InvalidOperationException("dt must be positive.");
            if (frameCount <= 0) throw new InvalidOperationException("frameCount must be positive.");
            foreach (var a in agents)
                if (a.positions == null || a.positions.Length != frameCount)
                    throw new InvalidOperationException($"Agent '{a.name}' has {a.positions?.Length ?? 0} samples, expected {frameCount}.");
        }
    }
}
