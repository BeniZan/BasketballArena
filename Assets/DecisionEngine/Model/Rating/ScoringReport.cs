using System;
using UnityEngine;
using Field.Hexagons;

namespace DecisionEngine.Model.Rating {
    // One 5 Hz observation of the player against the optimal plan.
    [Serializable]
    public class FrameRecord {
        public int frame;
        public float time;
        public int CellQ, CellR;
        public int BestCellQ, BestCellR;
        public float reward;
        public float qsq;
        public float bestQsq;
        public int hexDistanceToBest;

        public HexagonCell Cell {
            get { return new HexagonCell(CellQ, CellR); }
        }
        public HexagonCell BestCell {
            get { return new HexagonCell(BestCellQ, BestCellR); }
        }
    }

    // Final evaluation of one possession. Plain serializable data (JsonUtility-friendly).
    [Serializable]
    public class ScoringReport {
        public string scenarioName;
        public float totalScore;
        public float maxPossibleScore;
        // totalScore / maxPossibleScore in [0, 100].
        public float percent;
        // Decision Quality Index on the 0–10 scale shown to coaches (percent / 10).
        public float Score {
            get { return percent / 10f; }
        }
        public bool shotTaken;
        public int shotFrame = -1;
        public float shotReward;
        // qSQ at the release cell (expected-value scale, ~0–1.5); -1 when no shot was taken.
        public float shotQuality = -1f;
        // Shot quality on the 0–10 gauge scale (qSQ × 10, capped).
        public float ShotQualityScore {
            get { return shotTaken ? Mathf.Clamp(shotQuality * 10f, 0f, 10f) : -1f; }
        }
        public int optimalShotFrame = -1;
        // frame with the largest divergence from the optimal path (earliest on ties), or -1.
        public int criticalErrorFrame = -1;
        // One-line summary (logs).
        public string message;
        // Coach-facing feedback, one point per line.
        public string[] bullets = Array.Empty<string>();
        public FrameRecord[] frames = Array.Empty<FrameRecord>();

        public string ToJson(bool pretty = false) {
            return JsonUtility.ToJson(this, pretty);
        }
        public static ScoringReport FromJson(string json) {
            return JsonUtility.FromJson<ScoringReport>(json);
        }

        public override string ToString() {
            return $"{Score:0.0}/10 ({totalScore:0.00}/{maxPossibleScore:0.00}) — {message}";
        }
    }
}
