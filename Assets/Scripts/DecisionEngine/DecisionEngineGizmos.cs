using UnityEngine;

// Scene-view debugging for the engine: grid, optimal path (green), actual path (red), opponents (magenta).
[RequireComponent(typeof(DecisionEngineRunner))]
public class DecisionEngineGizmos : MonoBehaviour {
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private bool drawPaths = true;
    [SerializeField] private bool drawOpponents = true;

    private void OnDrawGizmos() {
        var runner = GetComponent<DecisionEngineRunner>();
        if (runner == null || runner.Grid == null || runner.Frame == null) return;
        var grid = runner.Grid;
        var frame = runner.Frame;

        if (drawGrid) {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
            foreach (var cell in grid.Cells) {
                for (var i = 0; i < 6; i++) {
                    var p0 = grid.CellCorner(cell, i);
                    var p1 = grid.CellCorner(cell, (i + 1) % 6);
                    Gizmos.DrawLine(frame.CourtToWorld(p0, 0.01f), frame.CourtToWorld(p1, 0.01f));
                }
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(frame.CourtToWorld(frame.Rules.Target, 0.05f), 0.25f);
        }

        var plan = runner.Plan;
        if (drawPaths && plan != null) {
            Gizmos.color = Color.green;
            for (var i = 1; i < plan.BestPath.Count; i++)
                Gizmos.DrawLine(frame.CourtToWorld(grid.CellCenter(plan.BestPath[i - 1]), 0.05f), frame.CourtToWorld(grid.CellCenter(plan.BestPath[i]), 0.05f));
            var shot = frame.CourtToWorld(grid.CellCenter(plan.BestPath[plan.BestPath.Count - 1]), 0.05f);
            Gizmos.DrawWireSphere(shot, 0.3f);

            var session = runner.Session;
            if (session != null) {
                Gizmos.color = Color.red;
                var frames = session.Frames;
                for (var i = 1; i < frames.Count; i++)
                    Gizmos.DrawLine(frame.CourtToWorld(grid.CellCenter(frames[i - 1].Cell), 0.08f), frame.CourtToWorld(grid.CellCenter(frames[i].Cell), 0.08f));
            }
        }

        var tracks = runner.Tracks;
        if (drawOpponents && tracks != null && runner.CurrentFrame >= 0) {
            var f = Mathf.Clamp(runner.CurrentFrame, 0, tracks.frameCount - 1);
            foreach (var a in tracks.agents) {
                Gizmos.color = a.isOpponent ? Color.magenta : Color.white;
                Gizmos.DrawWireSphere(frame.CourtToWorld(a.positions[f], 0.1f), 0.2f);
            }
        }
    }
}
