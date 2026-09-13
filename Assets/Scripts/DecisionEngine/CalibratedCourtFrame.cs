using UnityEngine;
using DecisionEngine.Model.ScoreRules;
using Field.Court;

// Maps world space onto the engine's court meters using the calibrated half-court surface.
// Conventions come from how the app places drills, not from the calibration order (which
// can leave the surface's X axis pointing either way): XRDrillActivator puts
// the drill origin ("CourtCenter" = the half-court line center) at the scaling transform's
// local X = -0.5 and characters extend toward local +X, i.e. toward the basket. So:
// court y = distance from the half-court line toward the basket = (localX + 0.5) * Length
// court x = across the width, positive to the right of a player facing the basket.
// With forward = +localX and up = +Y, right = up x forward = -localZ.
public sealed class CalibratedCourtFrame {
    private readonly Transform _scaling;

    public float Width { get; }
    public float Length { get; }
    public CourtBounds Bounds { get; }
    public ScoringRules Rules { get; }
    // The transform the court is attached to; overlays should parent under it.
    public Transform Root {
        get { return _scaling.parent != null ? _scaling.parent : _scaling; }
    }

    public CalibratedCourtFrame(SurfaceHandler surface) {
        _scaling = surface.ScalingTransform;
        var size = surface.Surface.Size;
        Width = Mathf.Max(0.01f, size[Calibration.WIDTH_AXIS]);
        Length = Mathf.Max(0.01f, size[Calibration.LENGTH_AXIS]);
        Bounds = new CourtBounds(Width, Length);
        Rules = BasketballScoringRules.ForHalfCourt(Length);
    }

    public Vector2 WorldToCourt(Vector3 world) {
        var l = _scaling.InverseTransformPoint(world);
        return new Vector2(-l.z * Width, (l.x + 0.5f) * Length);
    }

    public Vector3 CourtToWorld(Vector2 court, float heightAboveFloor = 0f) {
        var l = new Vector3(court.y / Length - 0.5f, heightAboveFloor, -court.x / Width);
        return _scaling.TransformPoint(l);
    }

    // Sanity check against the app's drill placement: the drill origin's parent must sit at
    // court (0, 0) and the drill's characters inside the rectangle. Returns null when fine.
    public string DescribeConventionProblem(Transform drillOrigin, System.Collections.Generic.IReadOnlyList<CharComponent> chars) {
        if (drillOrigin == null || drillOrigin.parent == null) return null;
        var center = WorldToCourt(drillOrigin.parent.position);
        if (center.magnitude > 0.5f)
            return $"drill origin maps to court {center} instead of (0,0) — court frame convention is off";
        var outside = 0;
        foreach (var c in chars) {
            var p = WorldToCourt(c.transform.position);
            if (p.x < -Width * 0.5f - 1f || p.x > Width * 0.5f + 1f || p.y < -1f || p.y > Length + 1f) outside++;
        }
        return outside > 0 ? $"{outside}/{chars.Count} characters map outside the court rectangle" : null;
    }

}
