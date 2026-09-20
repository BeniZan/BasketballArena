using System;

namespace Field.Hexagons {
    // One hexagon on the grid, addressed with "axial coordinates" (the standard scheme for hex grids)
    //
    // A square grid uses (column, row). Hexagons can't, because every other row is shifted by
    // half a cell, so instead we use two axes 60 degrees apart:
    //
    //        (0,-1)  (1,-1)
    //    (-1,0)  (0,0)  (1,0)        Q grows to the right along a row
    //        (-1,1)  (0,1)           R grows down-and-to-the-right along a diagonal
    //
    // The six neighbors of (Q, R) are reached by adding (+1,0) (-1,0) (0,+1) (0,-1) (+1,-1) (-1,+1).
    // A third, implied coordinate S = -Q - R makes the triple sum to zero ("cube coordinates");
    // it is only needed to measure distances and to round a fractional position to a cell.
    // HexagonMath converts between (Q, R) and meters on the court.
    public readonly struct HexagonCell : IEquatable<HexagonCell> {
        // Steps to the right along a row.
        public int Q { get; }
        // Steps down-and-to-the-right along a diagonal.
        public int R { get; }

        public HexagonCell(int q, int r) { Q = q; R = r; }

        private int S => -Q - R;

        public static readonly HexagonCell Zero = new HexagonCell(0, 0);

        public static HexagonCell operator +(HexagonCell a, HexagonCell b) {
            return new HexagonCell(a.Q + b.Q, a.R + b.R);
        }
        public static HexagonCell operator -(HexagonCell a, HexagonCell b) {
            return new HexagonCell(a.Q - b.Q, a.R - b.R);
        }
        public static bool operator ==(HexagonCell a, HexagonCell b) {
            return a.Q == b.Q && a.R == b.R;
        }
        public static bool operator !=(HexagonCell a, HexagonCell b) {
            return !(a == b);
        }

        // Number of cell-to-cell steps between two hexagons (the hex-grid equivalent of Manhattan
        // distance): half the sum of the absolute cube-coordinate differences.
        public static int DistanceInCells(HexagonCell a, HexagonCell b) {
            var d = a - b;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        public bool Equals(HexagonCell other) {
            return this == other;
        }
        public override bool Equals(object obj) {
            return obj is HexagonCell other && this == other;
        }
        public override int GetHashCode() {
            return unchecked(Q * 73856093 ^ R * 19349663);
        }
        public override string ToString() {
            return $"({Q},{R})";
        }
    }
}
