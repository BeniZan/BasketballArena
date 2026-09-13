using UnityEngine;
using DecisionEngine.Model.ScoreRules;

namespace DecisionEngine.Model.QsqModel {
    // Quantified Shot Quality: expected value of a shot taken from position
    // given the opponents' positions, on an effective-field-goal-like scale in [0, ~1.5].
    public interface IShotQualityModel {
        // opponents: Buffer of opponent positions; only the first opponentCount are read.
        float ShotQualityAt(Vector2 position, Vector2[] opponents, int opponentCount, ScoringRules rules);
    }
}
