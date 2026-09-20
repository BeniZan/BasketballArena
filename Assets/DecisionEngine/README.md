# DecisionEngine — Decision Quality Index (HoopEval MVP1)

Sport-agnostic evaluation of a player's spatial decisions against pre-scripted opponents,
after *HoopEval* (MIT Sloan): the possession is a finite-horizon MDP on a hexagonal grid at
5 Hz; the reward of a move is the change in shot quality (qSQ); the reward of a shot is
`qSQ − 0.40`. Because the opponents are scripted, the optimal policy is solved **exactly**
by backward induction instead of a learned Q-function.

## Layout

| Folder | Assembly | Depends on | Purpose |
|---|---|---|---|
| `Assets/Field/Court/` | `Field` | UnityEngine math only | `CourtBounds` — the rectangle: dimensions, on-court test, nearest point on court |
| `Assets/Field/Hexagons/` | `Field` | | `HexagonCell`, `HexagonGrid`, `HexagonMath` |
| `Model/ScoreRules/` | `DecisionEngine` (references `Field`) | | `ScoringRules` (target position, distance bonus), `BasketballScoringRules` (FIBA numbers) |
| `Model/QsqModel/` | `DecisionEngine` (references `Field`) | | `IShotQualityModel`, `QsqModel` |
| `Model/Environment/` | | | `AgentTrajectorySet`, `MovementModel` |
| `Model/OptimalPath/` | | | `ValueTable`, `BlockedTable`, `PrecomputationEngine`, `DecisionPlan` |
| `Model/Rating/` | | | `ScoringSession`, `ScoringReport`, `FeedbackGenerator` |
| `Model/ActionRecognition/` | | | `ShotReleaseClassifier` |
| `Resources/` | — | — | `HexagonGrid.mat` (overlay material), `Shot Release.asset` (XR hand pose) |
| `Assets/Scripts/DecisionEngine/` | Assembly-CSharp | Core + ARena app | Adapters: `DecisionEngineRunner`, `CalibratedCourtFrame`, `LiveTrajectoryCapture`, `HexagonGridVisualizer`, `KinematicShotDetector` (hand tracking → classifier), `KeyboardShotDetector` (editor), gizmos |
| `Assets/Scripts/Shared/UI/` | Assembly-CSharp | UI Toolkit | `RingGauge` (Painter2D radial gauge) and `DecisionCardView` (rating + shot-quality rings, toggles) used by the coach dashboard |

`Field` is its own assembly — the physical world (court + hex discretization) with no dependencies, usable by anything in the app. `DecisionEngine` is the HoopEval machinery built on it. Neither references the app. Adapters translate app concepts (calibration, drill
clock, headset, hand tracking, Netcode) into the core's inputs.

## Contract for another sport

1. Provide `CourtBounds` (width/length) and `ScoringRules` — target position and the
   distance bonus (e.g. 1.5× beyond the arc). See `BasketballScoringRules`.
2. Provide an `IShotQualityModel` — `Qsq(position, opponents, count, court)` on an
   expected-value scale. `QsqModel` is the tunable MVP1 default.
3. Produce an `AgentTrajectorySet` — opponent positions per 0.2 s frame in court meters.
   `LiveTrajectoryCapture` reads them from the running animation; an offline bake can
   replace it as long as it yields the same structure.
4. Drive a `ScoringSession`: `Begin()`, `OnFrame(frame, cell)` at every tick of the
   scenario clock, `OnShot(frame, cell)` or `End()`. Read `ScoringReport`.

## Runtime flow (ARena)

```
drill activated ─► wait 2 frames for characters ─► CalibratedCourtFrame (adaptive size)
   ─► HexagonGrid (20 hexes across the calibrated width ≈ HoopEval's 454 cells per half court) ─► LiveTrajectoryCapture (≈50 ms)
   ─► qSQ table ─► drill clock starts ─► BackwardInduction from the player's cell
   ─► 5 Hz OnFrame from HeadCam ─► shot recognized (wrist kinematics) / Space / Force Shot ─► ScoringReport
   ─► CustomLogger + NetSpawnedXRData.SubmitScoringReport ─► coach dashboard panel
```

Court frame conventions (derived from how `XRDrillActivator` places drills, so they hold
regardless of the order the court was calibrated in): the drill origin "CourtCenter" is the
half-court line center = court `(0, 0)`; `y` grows toward the basket up to `Length`; `x`
is across the width, positive to the right of a player facing the basket; basket at
`(0, Length − 1.575·Length/14)`. `CalibratedCourtFrame.Validate` checks this at every drill
activation and logs an error if the convention ever drifts.

## Flags (on `DecisionEngineRunner`, Local XR Device prefab)

- **Show Hexagons** — render the grid on the court inside the headset. The coach's
  *HEX GRID* button flips the same overlay for every connected headset.
- **Color By Qsq** — tint cells by current shot quality; white = player, green = optimal.
- **Drift Check** — warn when captured trajectories diverge from the live animation.
- Scene view gizmos (`DecisionEngineGizmos`): grid, optimal path (green), actual path (red),
  opponents (magenta), basket (yellow).

## Math note

With delta rewards the per-frame terms telescope:
`total = 2·qSQ[t_shot][c_shot] − qSQ[t_0][c_0] − 0.40`. The DP still matters for
reachability (19 moves per 0.2 s) and for the argmax backpointers that define the optimal
path used by the feedback ("critical error frame").

## Shot recognition

`ShotReleaseClassifier` works on wrist and palm joints only (fingers are occluded by a real
ball), in a head-relative frame so walking and head pitch cancel out. It recognizes a jump
shot in three phases: **gather** (both wrists high and close, within the last 0.7 s),
**release** (a wrist faster than 2 m/s along up+forward ending at/above head height — mandatory),
and **follow-through** (palm facing forward-and-down). Thresholds live on
`KinematicShotDetector` in the Inspector; every detection logs its phase breakdown so they can
be tuned on the device. Layups and other drive shots are not recognized yet.
Editor fallback: Space (`KeyboardShotDetector`) or the runner's **Force Shot** button.
