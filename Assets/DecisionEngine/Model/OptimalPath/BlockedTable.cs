namespace DecisionEngine.Model.OptimalPath {
    // [frame, cell] → true when an opponent occupies the cell.
    public sealed class BlockedTable {
        private readonly bool[] _blocked;
        public int FrameCount { get; }
        public int CellCount { get; }

        public BlockedTable(int frameCount, int cellCount) {
            FrameCount = frameCount; CellCount = cellCount;
            _blocked = new bool[frameCount * cellCount];
        }

        public bool this[int frame, int cell] {
            get { return _blocked[frame * CellCount + cell]; }
            set { _blocked[frame * CellCount + cell] = value; }
        }

    }
}
