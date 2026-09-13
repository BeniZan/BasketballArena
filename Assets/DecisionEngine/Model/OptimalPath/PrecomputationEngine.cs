using System;
using UnityEngine;
using Field.Hexagons;
using DecisionEngine.Model.Environment;
using DecisionEngine.Model.QsqModel;
using DecisionEngine.Model.ScoreRules;

namespace DecisionEngine.Model.OptimalPath {
    // Builds the qSQ table for a scripted scenario and solves the finite-horizon MDP
    // exactly by backward induction. Pure C#; safe to run off the main thread.
    public static class PrecomputationEngine {
        // HoopEval's fixed shot baseline: shot reward = qSQ − 0.40.
        public const float DefaultShotBaseline = 0.40f;

        public static ValueTable BuildQsqTable(HexagonGrid grid, AgentTrajectorySet tracks, ScoringRules rules, IShotQualityModel model) {
            tracks.Validate();
            var table = new ValueTable(tracks.frameCount, grid.CellCount);
            var opponents = new Vector2[tracks.OpponentCount];
            for (var t = 0; t < tracks.frameCount; t++) {
                var n = tracks.CopyOpponentPositionsAt(t, opponents);
                for (var c = 0; c < grid.CellCount; c++)
                    table[t, c] = model.ShotQualityAt(grid.CellCenter(grid.CellAtIndex(c)), opponents, n, rules);
            }
            return table;
        }

        // Marks every cell within radius of an opponent, per frame.
        public static BlockedTable BuildBlockedTable(HexagonGrid grid, AgentTrajectorySet tracks, float radius) {
            var table = new BlockedTable(tracks.frameCount, grid.CellCount);
            if (radius <= 0f) return table;
            var opponents = new Vector2[tracks.OpponentCount];
            var r2 = radius * radius;
            for (var t = 0; t < tracks.frameCount; t++) {
                var n = tracks.CopyOpponentPositionsAt(t, opponents);
                for (var c = 0; c < grid.CellCount; c++) {
                    var center = grid.CellCenter(grid.CellAtIndex(c));
                    for (var i = 0; i < n; i++)
                        if ((opponents[i] - center).sqrMagnitude <= r2) { table[t, c] = true; break; }
                }
            }
            return table;
        }

        // V[T][c] = qSQ[T][c] − baseline (must shoot at the horizon)
        // V[t][c] = max( qSQ[t][c] − baseline,
        // max over reachable c' of (qSQ[t+1][c'] − qSQ[t][c]) + V[t+1][c'] )
        public static DecisionPlan SolveOptimalPath(HexagonGrid grid, ValueTable qsq, HexagonCell startCell, float shotBaseline = DefaultShotBaseline, int startFrame = 0,
                                                     MovementModel movement = null, BlockedTable blocked = null, float dt = 0.2f) {
            if (!grid.IsCellInGrid(startCell)) throw new ArgumentOutOfRangeException(nameof(startCell));
            if (startFrame < 0 || startFrame >= qsq.FrameCount) throw new ArgumentOutOfRangeException(nameof(startFrame));
            movement ??= new MovementModel { maxRing = 2, blockedRadius = 0f, decisionWindowSeconds = 0f };
            var plan = new DecisionPlan(grid, qsq, shotBaseline, startCell, startFrame);
            var T = movement.LastFrameToActOn(startFrame, qsq.FrameCount, dt);
            plan.HorizonFrame = T;

            var actions = new System.Collections.Generic.List<int>();
            for (var a = 0; a < HexagonGrid.ActionCount; a++) if (movement.IsActionAllowed(a)) actions.Add(a);

            for (var c = 0; c < grid.CellCount; c++) {
                plan.Value[T, c] = qsq[T, c] - shotBaseline;
                plan.SetBestAction(T, c, DecisionPlan.ShootAction);
            }

            for (var t = T - 1; t >= 0; t--) {
                for (var c = 0; c < grid.CellCount; c++) {
                    var cell = grid.CellAtIndex(c);
                    var best = qsq[t, c] - shotBaseline;
                    var bestAction = DecisionPlan.ShootAction;
                    foreach (int a in actions) {
                        var next = grid.CellAfterAction(cell, a);
                        if (!next.HasValue) continue;
                        var cn = grid.IndexOfCell(next.Value);
                        if (blocked != null && a != HexagonGrid.StayAction && blocked[t + 1, cn]) continue;
                        var v = qsq.QsqChange(t, c, cn) + plan.Value[t + 1, cn];
                        // Strict '>' keeps "shoot" on ties so the plan never wanders without gain.
                        if (v > best) { best = v; bestAction = a; }
                    }
                    plan.Value[t, c] = best;
                    plan.SetBestAction(t, c, bestAction);
                }
            }

            plan.TracePath();
            return plan;
        }
    }
}
