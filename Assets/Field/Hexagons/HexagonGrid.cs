using System;
using System.Collections.Generic;
using UnityEngine;
using Field.Court;

namespace Field.Hexagons {
    
    // A Hex grid we are using to divide the court to hexagons
    public sealed class HexagonGrid {
        // According to MIT Sloan paper:
        // 15 Hexagons, each with a radius of 0.6 meters (Normal person's step length)
        public const int DefaultHexagonsCount = 15;

        // Number of movement actions:
        // 1. Stay
        // 2. 6 hexagons to move into the 1st degree ring.
        // 3. 12 hexagons to move to the 2nd degree ring.
        public const int ActionCount = 19;

        public const int StayAction = 0;

        // 19 movement actions array. index 0 is "stay".
        public static readonly HexagonCell[] MovementActions = BuildMovementActions();

        // The court this grid tiles; all "is it on the court" questions go through it.
        private CourtBounds Bounds { get; }

        // Hexagon radius (center to corner) in meters.
        public float Radius { get; }
        public int CellCount => _cells.Count;

        private readonly List<HexagonCell> _cells = new();
        private readonly Dictionary<HexagonCell, int> _indexOf = new();

        public HexagonGrid(CourtBounds bounds, int hexesAcross = DefaultHexagonsCount) {
            if (hexesAcross < 1) throw new ArgumentOutOfRangeException(nameof(hexesAcross));
            Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
            Radius = HexagonMath.RadiusForHexagonsAcross(bounds.Width, hexesAcross);
            EnumerateCells();
        }

        public HexagonGrid(float width, float length, int hexesAcross = DefaultHexagonsCount)
            : this(new CourtBounds(width, length), hexesAcross) { }

        // ---- lookup ------------------------------------------------------------------

        // Court-meter center of a cell.
        public Vector2 CellCenter(HexagonCell cell) {
            return HexagonMath.CellCenterInMeters(cell, Radius);
        }

        // The i-th corner (0..5) of a cell's hexagon, in court meters.
        public Vector2 CellCorner(HexagonCell cell, int i, float scale = 1f) {
            return HexagonMath.CellCornerInMeters(cell, i, Radius, scale);
        }

        // Grid cell containing a court-meter position. A position outside the court is first
        // moved to the nearest point inside it, so a player a step out of bounds still maps to
        // the cell on the edge rather than to nothing.
        public HexagonCell CellContainingPoint(Vector2 courtMeters) {
            var p = Bounds.ClosestPointOnCourt(courtMeters);
            var cell = HexagonMath.CellContainingPoint(p, Radius);
            return _indexOf.ContainsKey(cell) ? cell :
                // The clamped point can round to a center just outside the rectangle; snap to
                // the nearest enumerated cell in that rare case.
                Nearest(p);
        }


        public bool IsCellInGrid(HexagonCell cell) {
            return _indexOf.ContainsKey(cell);
        }

        public int IndexOfCell(HexagonCell cell) {
            return _indexOf.TryGetValue(cell, out var i) ? i : throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside the grid.");
        }

        public HexagonCell CellAtIndex(int index) {
            return _cells[index];
        }

        public IReadOnlyList<HexagonCell> Cells => _cells;

        // ---- actions ---------------------------------------------------------------

        // Target cell of an action, or null when it leaves the court.
        public HexagonCell? CellAfterAction(HexagonCell from, int action) {
            var to = from + MovementActions[action];
            return _indexOf.ContainsKey(to) ? to : null;
        }

        // Action index that moves from → to, or -1 if not reachable in one step.

        // In-bounds cells reachable from a cell in one step (including itself).

        // ---- internals -------------------------------------------------------------

        private void EnumerateCells() {
            // Generous axial bounds, then keep the cells whose center is inside the rect.
            var rMax = Mathf.CeilToInt(Bounds.Length / HexagonMath.DistanceBetweenRows(Radius)) + 1;
            var qMax = Mathf.CeilToInt(Bounds.Width / HexagonMath.DistanceBetweenNeighborCenters(Radius)) + rMax + 1;
            for (var r = -1; r <= rMax; r++) {
                for (var q = -qMax; q <= qMax; q++) {
                    var cell = new HexagonCell(q, r);
                    if (!Bounds.IsPointOnCourt(CellCenter(cell))) continue;
                    _indexOf[cell] = _cells.Count;
                    _cells.Add(cell);
                }
            }
        }

        private HexagonCell Nearest(Vector2 p) {
            var best = 0;
            var bestD = float.MaxValue;
            for (var i = 0; i < _cells.Count; i++) {
                var d = (CellCenter(_cells[i]) - p).sqrMagnitude;

                if (d < bestD) {
                    bestD = d;
                    best = i;
                }
            }
            return _cells[best];
        }


        private static HexagonCell[] BuildMovementActions() {
            var list = new List<HexagonCell> { HexagonCell.Zero };

            // Ring 1 then ring 2, in a stable order so action indices are reproducible.
            for (var radius = 1; radius <= 2; radius++)
                for (var q = -radius; q <= radius; q++)
                    for (var r = -radius; r <= radius; r++) {
                        var c = new HexagonCell(q, r);
                        if (HexagonCell.DistanceInCells(c, HexagonCell.Zero) == radius) list.Add(c);
                    }

            return list.Count != ActionCount ? throw new InvalidOperationException("Expected 19 movement actions.") : list.ToArray();
        }
    }
}
