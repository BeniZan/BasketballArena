using UnityEngine;

namespace DecisionEngine.Model.ScoreRules {
    // What the sport's rules say a shot from a given spot is worth: where the shot is aimed
    // and the "farther shots are worth more" bonus (the three-point line in basketball).
    // Positions are court meters (see Field.Court.CourtBounds).
    public sealed class ScoringRules {
        // Where a shot is aimed (the basket), in court meters.
        public Vector2 Target { get; }
        // Shots released at least this far from the target are worth BonusMultiplier times more.
        // Zero disables the bonus.
        public float BonusRadius { get; }
        public float BonusMultiplier { get; }

        public ScoringRules(Vector2 target, float bonusRadius = 0f, float bonusMultiplier = 1f) {
            Target = target;
            BonusRadius = bonusRadius;
            BonusMultiplier = bonusMultiplier;
        }

        public float DistanceToTarget(Vector2 p) {
            return Vector2.Distance(p, Target);
        }

        // Multiplies P(make) into an expected-value scale, e.g. 1.5 for a basketball
        // three-pointer so that qSQ is comparable to effective field-goal percentage.
        public float ShotValueMultiplier(Vector2 p) {
            if (BonusRadius <= 0f) {
                return 1f;
            }
            return DistanceToTarget(p) >= BonusRadius ? BonusMultiplier : 1f;
        }
    }
}
