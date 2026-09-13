using UnityEngine;

namespace Field.Hexagons {
    // Geometry of a pointy-top hexagon lattice: conversions between axial hex coordinates
    // (q, r) and meters, hexagon corners, and rounding. Stateless; every function takes the
    // hexagon radius explicitly. Formulas follow Red Blob Games' hex grid reference.
    public static class HexagonMath {
        // Pointy-top hexagons tile horizontally every sqrt(3) * R and vertically every 1.5 * R;
        // sqrt(3) shows up in every conversion between hex coordinates and meters, so compute it once.
        private static readonly float Sqrt3 = Mathf.Sqrt(3f);

        // Radius (center to corner) such that hexesAcross hexagons span a given width.
        public static float RadiusForHexagonsAcross(float width, int hexesAcross) {
            return width / (hexesAcross * Sqrt3);
        }

        // Distance between the centers of two adjacent hexagons.
        public static float DistanceBetweenNeighborCenters(float radius) {
            return Sqrt3 * radius;
        }

        // Vertical distance between consecutive rows.
        public static float DistanceBetweenRows(float radius) {
            return 1.5f * radius;
        }

        // Center of a cell in meters.
        public static Vector2 CellCenterInMeters(HexagonCell cell, float radius) {
            var x = radius * (Sqrt3 * cell.Q + Sqrt3 * 0.5f * cell.R);
            var y = radius * (1.5f * cell.R);
            return new Vector2(x, y);
        }

        // Cell whose hexagon contains a point, on the infinite lattice.
        public static HexagonCell CellContainingPoint(Vector2 meters, float radius) {
            var q = (Sqrt3 / 3f * meters.x - 1f / 3f * meters.y) / radius;
            var r = (2f / 3f * meters.y) / radius;
            return RoundToNearestCell(q, r);
        }

        // The i-th corner (0..5) of a cell's hexagon in meters; corner 0 sits at 30 degrees.
        // scale < 1 shrinks the hexagon around its center (useful for drawing gaps between cells).
        public static Vector2 CellCornerInMeters(HexagonCell cell, int i, float radius, float scale = 1f) {
            var a = Mathf.Deg2Rad * (60f * i + 30f);
            return CellCenterInMeters(cell, radius) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius * scale);
        }

        // Rounds fractional axial coordinates to the nearest cell by rounding in cube space
        // and fixing the axis with the largest rounding error so q + r + s stays zero.
        private static HexagonCell RoundToNearestCell(float fq, float fr) {
            var fs = -fq - fr;
            var q = Mathf.RoundToInt(fq);
            var r = Mathf.RoundToInt(fr);
            var s = Mathf.RoundToInt(fs);
            var dq = Mathf.Abs(q - fq);
            var dr = Mathf.Abs(r - fr);
            var ds = Mathf.Abs(s - fs);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return new HexagonCell(q, r);
        }
    }
}
