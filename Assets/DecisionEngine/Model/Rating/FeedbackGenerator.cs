using System.Collections.Generic;
using UnityEngine;
using Field.Hexagons;
using DecisionEngine.Model.OptimalPath;

namespace DecisionEngine.Model.Rating {
    // Turns a finished report into a critical-error frame and coach-readable bullet points.
    public static class FeedbackGenerator {
        public static void AddFeedbackTo(ScoringReport report, DecisionPlan plan) {
            report.criticalErrorFrame = FindCriticalErrorFrame(report);
            report.bullets = BuildBullets(report, plan).ToArray();
            report.message = string.Join(" ", report.bullets);
        }

        // Frame with the largest hex distance to the optimal cell; earliest wins ties; -1 if never off-path.
        private static int FindCriticalErrorFrame(ScoringReport report) {
            var worst = -1;
            var worstDist = 0;
            foreach (var f in report.frames)
                if (f.hexDistanceToBest > worstDist) { worstDist = f.hexDistanceToBest; worst = f.frame; }
            return worst;
        }

        // Short, time-free notes for logs and coach review. The player-facing card shows gauges instead.
        private static List<string> BuildBullets(ScoringReport r, DecisionPlan plan) {
            var b = new List<string>();
            var grid = plan.Grid;

            if (!r.shotTaken) b.Add("No shot was taken.");
            else {
                var look = r.shotQuality >= 0.55f ? "an open look" : r.shotQuality >= 0.4f ? "a fair look" : "a contested look";
                b.Add($"Shot from {look}.");
            }

            if (r.criticalErrorFrame < 0) {
                b.Add("You followed the optimal path.");
            } else {
                var err = Find(r, r.criticalErrorFrame);
                if (err != null) {
                    var didWhat = DescribeMovement(r, err, grid);
                    var better = DirectionWord(grid.CellCenter(err.BestCell) - grid.CellCenter(err.Cell));
                    b.Add($"Instead of {didWhat}, cutting {better} would have been better.");
                }
            }
            return b;
        }

        private static FrameRecord Find(ScoringReport r, int frame) {
            foreach (var f in r.frames) if (f.frame == frame) return f;
            return null;
        }

        private static string DescribeMovement(ScoringReport r, FrameRecord err, HexagonGrid grid) {
            var prev = Find(r, err.frame - 1);
            if (prev == null || prev.Cell == err.Cell) return "holding your position";
            return "drifting " + DirectionWord(grid.CellCenter(err.Cell) - grid.CellCenter(prev.Cell));
        }

        // Coarse direction of a court-meter vector: toward/away from the basket, left/right.
        private static string DirectionWord(Vector2 v) {
            if (v.sqrMagnitude < 1e-6f) return "nowhere";
            var f = Vector2.Dot(v.normalized, Vector2.up);
            var s = Vector2.Dot(v.normalized, Vector2.right);
            var fw = f > 0.38f ? "toward the basket" : f < -0.38f ? "away from the basket" : "";
            var sw = s > 0.38f ? "right" : s < -0.38f ? "left" : "";
            if (fw.Length > 0 && sw.Length > 0) return $"{sw} and {fw}";
            return fw.Length > 0 ? fw : sw;
        }
    }
}
