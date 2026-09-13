using System;
using UnityEngine;
using DecisionEngine.Model.ScoreRules;
using DecisionEngine.Model.ActionRecognition;

namespace DecisionEngine.Model.QsqModel {
    // Tunable coefficients for QsqModel. MVP2 archetypes swap these.
    [Serializable]
    public class QsqParams {
        [Tooltip("Logit intercept: shot quality at the rim with no defender nearby contribution.")]
        public float intercept = 0.6f;
        [Tooltip("Logit decrease per meter of distance to the target.")]
        public float distancePenalty = 0.425f;
        [Tooltip("Logit increase per meter of separation from the closest opponent.")]
        public float defenderBonus = 0.5f;
        [Tooltip("Separation beyond this many meters counts as fully open.")]
        public float defenderDistanceCap = 3f;

    }

    // Quantified Shot Quality (Chang et al., MIT Sloan 2014): expected value of a shot from a
    // position given the defense, on an effective-field-goal scale.
    // MVP1 form: logistic in distance-to-target and closest-opponent separation, times the
    // court's shot-value multiplier (three-point bonus). Coefficients live in QsqParams so a
    // fitted model can replace them without changing callers.
    public sealed class QsqModel : IShotQualityModel {
        private QsqParams Params { get; }

        public QsqModel(QsqParams p = null) {
            Params = p ?? new QsqParams();
        }

        private float ProbabilityOfMake(float distanceToTarget, float closestOpponentDistance) {
            var sep = Mathf.Min(closestOpponentDistance, Params.defenderDistanceCap);
            var logit = Params.intercept - Params.distancePenalty * distanceToTarget + Params.defenderBonus * sep;
            return 1f / (1f + Mathf.Exp(-logit));
        }

        public float ShotQualityAt(Vector2 position, Vector2[] opponents, int opponentCount, ScoringRules rules) {
            var closest = Params.defenderDistanceCap;
            for (var i = 0; i < opponentCount; i++) {
                var d = Vector2.Distance(position, opponents[i]);
                if (d < closest) closest = d;
            }
            var p = ProbabilityOfMake(rules.DistanceToTarget(position), closest);
            return p * rules.ShotValueMultiplier(position);
        }
    }
}
