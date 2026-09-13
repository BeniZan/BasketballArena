using System;
using System.Collections.Generic;
using UnityEngine;
using Field.Hexagons;
using DecisionEngine.Model.OptimalPath;

namespace DecisionEngine.Model.Rating {
    // Live evaluation of one possession. Feed it the player's cell at every frame of the
    // scenario clock and the shot event; it accumulates HoopEval rewards and produces a
    // ScoringReport. No Unity lifecycle — drive it from anywhere.
    public sealed class ScoringSession {
        public DecisionPlan Plan { get; }
        public string ScenarioName { get; }
        public bool IsActive { get; private set; }
        private bool IsFinished { get; set; }
        public float TotalScore { get; private set; }
        public int LastFrame {
            get { return _frames.Count > 0 ? _frames[_frames.Count - 1].frame : -1; }
        }
        public IReadOnlyList<FrameRecord> Frames {
            get { return _frames; }
        }

        private readonly List<FrameRecord> _frames = new List<FrameRecord>();
        private HexagonGrid Grid {
            get { return Plan.Grid; }
        }
        private ValueTable Qsq {
            get { return Plan.Qsq; }
        }

        public event Action<ScoringReport> Finished;

        public ScoringSession(DecisionPlan plan, string scenarioName = null) {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            ScenarioName = scenarioName ?? "";
        }

        // Records the plan's start frame at its start cell.
        public void StartPossession() {
            if (IsActive || IsFinished) throw new InvalidOperationException("Session already started.");
            IsActive = true;
            Record(Plan.StartFrame, Plan.StartCell, reward: 0f);
        }

        // Observe the player's cell at frame. Frames must be non-decreasing;
        // skipped frames are filled by holding the previous cell so rewards still telescope.
        public void RecordPlayerCell(int frame, HexagonCell cell) {
            if (!IsActive) return;
            if (frame >= Plan.FrameCount) { EndWithoutShot(); return; }
            if (frame <= LastFrame) return;
            var prev = _frames[_frames.Count - 1];
            for (var t = prev.frame + 1; t < frame; t++)
                Record(t, prev.Cell, Qsq.QsqChange(t - 1, Grid.IndexOfCell(prev.Cell), Grid.IndexOfCell(prev.Cell)));
            var last = _frames[_frames.Count - 1];
            Record(frame, cell, Qsq.QsqChange(frame - 1, Grid.IndexOfCell(last.Cell), Grid.IndexOfCell(cell)));
        }

        // Player released a shot at frame from cell.
        public void RecordShot(int frame, HexagonCell cell) {
            if (!IsActive) return;
            frame = Mathf.Clamp(frame, LastFrame, Plan.FrameCount - 1);
            RecordPlayerCell(frame, cell);
            if (!IsActive) return;
            var shotQsq = Qsq[frame, Grid.IndexOfCell(cell)];
            var shotReward = shotQsq - Plan.ShotBaseline;
            TotalScore += shotReward;
            Finish(shotTaken: true, shotFrame: frame, shotReward: shotReward, shotQsq: shotQsq);
        }

        // Scenario ended without a shot: possession lost, no shot reward.
        public void EndWithoutShot() {
            if (!IsActive) return;
            Finish(shotTaken: false, shotFrame: -1, shotReward: 0f, shotQsq: -1f);
        }

        private void Record(int frame, HexagonCell cell, float reward) {
            var best = Plan.BestCellAt(frame);
            TotalScore += reward;
            _frames.Add(new FrameRecord {
                frame = frame,
                time = frame * 0.2f,
                CellQ = cell.Q, CellR = cell.R,
                BestCellQ = best.Q, BestCellR = best.R,
                reward = reward,
                qsq = Qsq[frame, Grid.IndexOfCell(cell)],
                bestQsq = Qsq[frame, Grid.IndexOfCell(best)],
                hexDistanceToBest = HexagonCell.DistanceInCells(cell, best),
            });
        }

        private void Finish(bool shotTaken, int shotFrame, float shotReward, float shotQsq) {
            IsActive = false;
            IsFinished = true;
            var max = Plan.MaxPossibleScore;
            var percent = max > 1e-4f
                ? Mathf.Clamp(TotalScore / max * 100f, 0f, 100f)
                : (TotalScore >= max ? 100f : 0f);
            var report = new ScoringReport {
                scenarioName = ScenarioName,
                totalScore = TotalScore,
                maxPossibleScore = max,
                percent = percent,
                shotTaken = shotTaken,
                shotFrame = shotFrame,
                shotReward = shotReward,
                shotQuality = shotQsq,
                optimalShotFrame = Plan.ShotFrame,
                frames = _frames.ToArray(),
            };
            FeedbackGenerator.AddFeedbackTo(report, Plan);
            Finished?.Invoke(report);
        }
    }
}
