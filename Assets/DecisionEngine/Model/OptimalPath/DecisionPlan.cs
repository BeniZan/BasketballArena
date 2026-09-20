using System;
using System.Collections.Generic;
using UnityEngine;
using Field.Hexagons;

namespace DecisionEngine.Model.OptimalPath {
    // Output of backward induction: the value of every (frame, cell) state, the optimal
    // action from it, and the optimal trajectory from the session's start cell.
    public sealed class DecisionPlan {
        public const int ShootAction = -1;

        public HexagonGrid Grid { get; }
        public ValueTable Qsq { get; }
        public float ShotBaseline { get; }
        public int FrameCount {
            get { return Qsq.FrameCount; }
        }

        // V[t, cell]: best achievable cumulative reward from that state.
        public ValueTable Value { get; }
        // Optimal action index per (frame, cell); ShootAction means shoot now.
        private readonly int[] _bestAction;

        public HexagonCell StartCell { get; }
        // Frame at which the evaluated possession starts (usually 0).
        public int StartFrame { get; }
        // Last frame the plan may act on (decision window); the plan shoots here at the latest.
        public int HorizonFrame { get; internal set; }
        // Optimal cells for frames StartFrame..shotFrame inclusive, starting at StartCell.
        public IReadOnlyList<HexagonCell> BestPath {
            get { return _bestPath; }
        }
        private readonly List<HexagonCell> _bestPath = new List<HexagonCell>();
        public int ShotFrame {
            get { return StartFrame + _bestPath.Count - 1; }
        }
        public float MaxPossibleScore {
            get { return Value[StartFrame, Grid.IndexOfCell(StartCell)]; }
        }

        internal DecisionPlan(HexagonGrid grid, ValueTable qsq, float shotBaseline, HexagonCell startCell, int startFrame) {
            Grid = grid;
            Qsq = qsq;
            ShotBaseline = shotBaseline;
            StartCell = startCell;
            StartFrame = startFrame;
            Value = new ValueTable(qsq.FrameCount, qsq.CellCount);
            _bestAction = new int[qsq.FrameCount * qsq.CellCount];
        }

        private int BestActionAt(int frame, int cellIndex) {
            return _bestAction[frame * Qsq.CellCount + cellIndex];
        }
        internal void SetBestAction(int frame, int cellIndex, int action) {
            _bestAction[frame * Qsq.CellCount + cellIndex] = action;
        }

        // Best cell to be in at frame along the optimal path (last cell after the optimal shot).
        public HexagonCell BestCellAt(int frame) {
            return _bestPath[Mathf.Clamp(frame - StartFrame, 0, _bestPath.Count - 1)];
        }

        internal void TracePath() {
            _bestPath.Clear();
            var cell = StartCell;
            for (var t = StartFrame; t <= HorizonFrame; t++) {
                _bestPath.Add(cell);
                var action = BestActionAt(t, Grid.IndexOfCell(cell));
                if (action == ShootAction) return;
                var next = Grid.CellAfterAction(cell, action);
                if (!next.HasValue) throw new InvalidOperationException("Best action left the grid; plan is corrupt.");
                cell = next.Value;
            }
        }
    }
}
