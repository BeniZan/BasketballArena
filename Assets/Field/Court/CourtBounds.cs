using System;
using UnityEngine;

namespace Field.Court {
    public sealed class CourtBounds {
        public float Width { get; }
        public float Length { get; }
        private float HalfWidth => Width * 0.5f;

        public CourtBounds(float width, float length) {
            if (width <= 0f || length <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(width), "Court dimensions must be positive.");
            }

            Width = width;
            Length = length;
        }

        public bool IsPointOnCourt(Vector2 p) {
            return p.x >= -HalfWidth && p.x <= HalfWidth && p.y >= 0f && p.y <= Length;
        }

        // If the point is inside the court it is returned unchanged; otherwise each coordinate is
        // limited to the court's range, which gives the nearest point on the boundary.
        public Vector2 ClosestPointOnCourt(Vector2 p) {
            return new Vector2( Mathf.Clamp(p.x, -HalfWidth, HalfWidth), Mathf.Clamp(p.y, 0f, Length));
        }
    }
}
