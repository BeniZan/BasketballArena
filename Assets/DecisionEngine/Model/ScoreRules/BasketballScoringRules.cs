using UnityEngine;

namespace DecisionEngine.Model.ScoreRules {
    // FIBA basketball numbers, scaled to whatever half-court was calibrated.
    public static class BasketballScoringRules {
        private const float FibaHalfLength = 14f;
        // Distance from the baseline to the center of the hoop.
        private const float FibaBasketFromBaseline = 1.575f;
        private const float FibaThreePointRadius = 6.75f;
        private const float ThreePointValueMultiplier = 1.5f;

        // Rules for a half-court of the given length. Reference distances scale with the
        // length ratio so a shorter practice court keeps its proportions.
        public static ScoringRules ForHalfCourt(float courtLength) {
            var scale = courtLength / FibaHalfLength;
            var basket = new Vector2(0f, courtLength - FibaBasketFromBaseline * scale);
            return new ScoringRules(basket, FibaThreePointRadius * scale, ThreePointValueMultiplier);
        }
    }
}
