using System;

namespace DecisionEngine.Model.OptimalPath {
    // Dense [frame, cell] table of shot quality.
    public sealed class ValueTable {
        public int FrameCount { get; }
        public int CellCount { get; }
        private readonly float[] _values;

        public ValueTable(int frameCount, int cellCount) {
            if (frameCount <= 0 || cellCount <= 0) throw new ArgumentOutOfRangeException();
            FrameCount = frameCount;
            CellCount = cellCount;
            _values = new float[frameCount * cellCount];
        }

        public float this[int frame, int cell] {
            get { return _values[frame * CellCount + cell]; }
            set { _values[frame * CellCount + cell] = value; }
        }

        // Reward of moving from cellFrom at t to cellTo at t+1.
        public float QsqChange(int frame, int cellFrom, int cellTo) {
            return this[frame + 1, cellTo] - this[frame, cellFrom];
        }

    }
}
