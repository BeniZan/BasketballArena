# Project Overview
- **Game Title:** BasketballArena — Coach Dashboard (VR basketball training control panel)
- **High-Level Concept:** A coach-side dashboard to manage VR headsets/players, assemble a sequence of basketball drills ("Training Flow"), and run/monitor a live training session streamed from the players' POV.
- **Players:** Coach operates the dashboard; players are in VR headsets (networked via Unity Netcode + WebRTC streaming).
- **Inspiration / Reference Games:** N/A (training/simulation tool)
- **Tone / Art Direction:** Dark UI, orange (`rgb(255,107,0)`) accent, Barlow Condensed / Inter fonts.
- **Target Platform:** StandaloneWindows64 (PC)
- **Screen Orientation / Resolution:** Landscape (desktop)
- **Render Pipeline:** URP (PC_RPAsset)

> **Note on Figma:** The provided Figma links could not be read programmatically (the `/make/` URL exposes no static design data; the `/design/` and `/design/?m=dev` Dev Mode URLs render client-side behind authentication and return no extractable design data via fetch). This plan is therefore built from the written spec and the actual project code, which are authoritative for behavior. For the one new visual element (the LIVE SESSION tag), styling uses the same green `rgb(16,185,129)` already used by `statusText` in its "RUNNING" state. If you want the tag to match Figma pixel-for-pixel, paste a screenshot or the exact color/size/spacing values and it will be adjusted.

# Game Mechanics
## Core Gameplay Loop
The coach: (1) assigns headsets/positions and Offense/Defense states, (2) browses the Exercise Library and taps drills to add them to the Training Flow, (3) presses START to begin a live session, then uses NEXT / PAUSE / STOP / FORCE to drive drills while watching the live player stream.

## Controls and Input Methods
Mouse/pointer interaction with UI Toolkit buttons. No changes to input backend required (this is UI-only logic).

# UI
Existing UI Toolkit layout (unchanged structurally except one added element):
- **Left sidebar** (`left-sidebar`): logo, H1/H2 headset cards with Offense/Defense buttons (`h1OffenseBtn`, `h1DefenseBtn`, `h2OffenseBtn`, `h2DefenseBtn`).
- **Center pane** (`playerStreaming`): stream mode toggles (`realisticBtn`/`hologramBtn`), the stream viewport (`castingView`), and the bottom control bar with status + playback buttons (`startBtn`, `nextBtn`, `pauseBtn`, `stopBtn`, `forceBtn`) and `statusText` / `timerText` / `repText`.
- **Right sidebar** (`exercise`): Exercise Library foldouts (tap a drill to add) + Training Flow (`drillListContainer` ListView + `buildSessionPlaceholder`) + Analytics.

**New UI element:** a green **"LIVE SESSION"** tag overlaid on the stream viewport (`castingView`), hidden by default, shown while a session is active.

```
 ┌── castingView (stream-viewport) ───────────────┐
 │  ● LIVE SESSION   ← new tag (top-left, green)   │
 │                                                 │
 │        [ placeholder / live POV ]               │
 └─────────────────────────────────────────────────┘
```

# Key Asset & Context
Files to modify (all confirmed to exist):
- **`Assets/Scripts/Coach/CoachDashboardUIToolkitController.cs`** — main controller. Key existing members:
  - Buttons: `_startBtn`, `_nextBtn`, `_pauseBtn`, `_stopBtn`, `_forceBtn`
  - Model: `TrainingSession _session` (`_session.Drills`, `_session.ActiveIndex`)
  - Handlers: `StartTimer()`, `PauseTimer()`, `StopTimer()`, `NextRep()`, `ForceSession()`
  - Events wired: `HandleDrillsChanged()` (fires when drills added/removed), `UpdateDrillsDisplay()` (toggles placeholder vs list)
  - ListView setup: `SetupDrillListView()`, `MakeDrillItem()`, `BindDrillItem()`
- **`Assets/UI/CoachDashboard/CoachDashboard.uxml`** — add the LIVE SESSION tag inside `castingView` (lines ~84–90).
- **`Assets/UI/CoachDashboard/CoachDashboard.uss`** — add `.live-session-tag` styles; add ListView `:hover`/`--selected` overrides near the `.drill-item-*` block (lines ~636–680).

Unchanged / out of scope:
- `TrainingSession.cs` (model already exposes everything needed).
- `WebRTCVideoReceiver.cs` — the WebRTC connection already auto-starts on server start; `InitializeTexture()` is a stub (video display not yet wired). "Trigger the live stream" is therefore implemented at the **UI level** (activate LIVE SESSION visual state / swap viewport out of placeholder). No WebRTC changes.

# Current Status (verified against project code)

A prior implementation pass already completed most of this plan. Verified by reading `CoachDashboardUIToolkitController.cs`, `CoachDashboard.uxml`, and `CoachDashboard.uss`:

- ✅ **Step 1 (DONE):** `_isSessionActive` flag + `UpdateControlInteractability()` present (controller lines ~74, ~271–279).
- ✅ **Step 2 (DONE):** `UpdateControlInteractability()` called from `InitializeUI()` (line ~204) and `HandleDrillsChanged()` (line ~518). Start gated on `!_isSessionActive && Drills.Count > 0`.
- ✅ **Step 3 C#/UXML (DONE):** `liveTag` element exists in UXML (lines 85–88); `StartTimer()`/`StopTimer()` toggle `_liveTag` + `_streamPlaceholder`; hidden by default in `InitializeUI()` (line ~203).
- ✅ **Step 4 C# (DONE):** `selectionType = SelectionType.None` removes the cyan click-selection (controller line ~457).
- ❌ **REMAINING (USS only) — see Steps 3b & 4b below:**
  1. `.live-session-tag` / `.live-session-dot` / `.live-session-text` styles are **absent** from `CoachDashboard.uss` — the tag element renders unstyled (no green badge look).
  2. The white **hover** override on drill rows is **absent** — Unity's default ListView theme still paints a white hover background on `.drill-item-uss` rows.

Only `Assets/UI/CoachDashboard/CoachDashboard.uss` needs edits to finish the tasks.

# Implementation Steps

> Steps 1, 2, 3(C#/UXML), and 4(C#) are already complete (see Current Status). The remaining work is Steps 3b and 4b, both in `CoachDashboard.uss`.

### Step 3b — Add USS styling for the LIVE SESSION tag  *(REMAINING)*
- **Description:** In `Assets/UI/CoachDashboard/CoachDashboard.uss`, add styles matching the existing UXML element (`name="liveTag" class="live-session-tag"` containing `.live-session-dot` and `.live-session-text`). Position it absolutely at the top-left of `castingView` (`.stream-viewport`), with a green theme consistent with the "RUNNING" status color already used in code (`rgb(16,185,129)`):
  - `.live-session-tag`: `position: absolute; top: 12px; left: 12px; flex-direction: row; align-items: center; padding: 4px 10px; border-radius: 6px; background-color: rgba(16,185,129,0.18); border-width: 1px; border-color: rgb(16,185,129);` (default visibility is controlled inline by C#, which hides it on load and shows it on START — no `display` needed here, but `display: none;` may be added as a safety default).
  - `.live-session-dot`: `width: 8px; height: 8px; border-radius: 4px; background-color: rgb(16,185,129); margin-right: 6px;`
  - `.live-session-text`: `color: rgb(16,185,129); font-size: 11px;` (Barlow700 font is applied via C# typography if the label carries a recognized class; otherwise default font is acceptable).
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 4b — Remove the white hover (and any residual selection) styling on drill rows  *(REMAINING)*
- **Description:** In `CoachDashboard.uss`, near the `.drill-item-*` block (lines ~636–680), add overrides so no background/border appears on hover, focus, or selection of ListView items. The `.drill-list-view` class is applied to the ListView in C# (`AddToClassList("drill-list-view")`), so scope the overrides under it:
  ```
  .drill-list-view .unity-collection-view__item:hover,
  .drill-list-view .unity-collection-view__item--selected,
  .drill-list-view .unity-collection-view__item:focus,
  .drill-list-view .unity-list-view__item:hover,
  .drill-list-view .unity-list-view__item--selected {
      background-color: rgba(0, 0, 0, 0);
      border-width: 0;
  }
  ```
  This removes the white hover highlight entirely. The custom orange `.drill-item-active` highlight (driven by `_session.ActiveIndex`) is unaffected because it targets a different, higher-specificity class on the item content element.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

---

## Original Steps (for reference — already implemented)

### Step 1 — Add a session-active state flag and a central interactability updater
- **Description:** In `CoachDashboardUIToolkitController.cs`, add `private bool _isSessionActive = false;`. Add a new method `UpdateControlInteractability()` that sets:
  - `_startBtn.SetEnabled(!_isSessionActive && _session.Drills.Count > 0)` — Start is only usable when NOT already running AND at least one drill exists (Requirement 1).
  - `_nextBtn.SetEnabled(_isSessionActive)`, `_pauseBtn.SetEnabled(_isSessionActive)`, `_stopBtn.SetEnabled(_isSessionActive)`, `_forceBtn.SetEnabled(_isSessionActive)` — session controls only usable while active (Requirement 3). *(FORCE included as one of the "etc." session controls; can be excluded on request.)*
  - Guard each with a null check.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** No (other steps build on this)

### Step 2 — Gate the Start button on Training Flow contents
- **Description:** Call `UpdateControlInteractability()` from: (a) the end of `InitializeUI()` initial-setup block (so Start starts disabled — the flow is empty on load), and (b) inside `HandleDrillsChanged()` (so adding/removing drills re-evaluates Start). This satisfies Requirement 1 (Start disabled by default, enabled once ≥1 exercise is in the flow).
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 3 — Enter/exit active session state + green LIVE SESSION tag + stream trigger
- **Description:**
  - Add the LIVE SESSION tag to **UXML** inside `castingView`: `<ui:VisualElement name="liveTag" class="live-session-tag"> <ui:VisualElement class="live-session-dot"/> <ui:Label text="LIVE SESSION" class="live-session-text"/> </ui:VisualElement>`.
  - Add **USS** `.live-session-tag` (absolute-positioned top-left, green background e.g. `rgba(16,185,129,0.18)` with `rgb(16,185,129)` border/text, `display: none` by default), `.live-session-dot` (small green circle), `.live-session-text` (green, bold).
  - In controller: query `_liveTag = _root.Q<VisualElement>("liveTag")` in `InitializeUI()`.
  - In `StartTimer()`: set `_isSessionActive = true`, show the tag (`_liveTag.style.display = DisplayStyle.Flex`), hide the viewport placeholder to reflect the live stream being triggered, then call `UpdateControlInteractability()`. (Existing status→"RUNNING"/green and `_session.Start()` logic stays.)
  - In `StopTimer()`: set `_isSessionActive = false`, hide the tag (`DisplayStyle.None`), restore the placeholder, then call `UpdateControlInteractability()`.
  - `PauseTimer()` keeps `_isSessionActive = true` (paused is a sub-state of an active session, so NEXT/STOP/FORCE stay usable and the coach can resume) — leave the LIVE tag visible.
  - This satisfies Requirement 2.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 4 — Remove the white-hover / cyan-click styling from Training Flow items
- **Description:** The white hover + sticky cyan selection are Unity's built-in `ListView` item states. Remove them two ways for robustness:
  1. In `SetupDrillListView()`, change `_drillListView.selectionType = SelectionType.Single;` to `SelectionType.None;` (eliminates the cyan click-selection and the "stuck" selected state). Reorder-by-drag still works.
  2. In **USS**, add overrides so no background/border appears on hover or selection:
     ```
     .drill-list-view .unity-collection-view__item:hover,
     .drill-list-view .unity-collection-view__item--selected,
     .drill-list-view .unity-collection-view__item:focus,
     .drill-list-view .unity-list-view__item:hover,
     .drill-list-view .unity-list-view__item--selected {
         background-color: rgba(0, 0, 0, 0);
         border-width: 0;
     }
     ```
  - This fully removes the white/cyan visual state changes (Requirement 3 / Bug Fix), while the custom `.drill-item-active` orange highlight (driven by `_session.ActiveIndex`) is preserved.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (independent of Steps 1–3)

# Verification & Testing
1. **Start gating (Req 1):** Enter Play mode with an empty Training Flow → START is greyed/disabled. Tap one exercise in the Exercise Library → START becomes enabled. Remove the last drill (✕) → START disables again.
2. **Active state + LIVE tag (Req 2):** Press START → status reads "RUNNING" (green), the green **LIVE SESSION** tag appears on the viewport, placeholder hidden. Press STOP → tag disappears, status "READY", placeholder restored.
3. **Session controls (Req 3):** Before START → NEXT / PAUSE / STOP / FORCE are disabled. After START → all enabled. PAUSE → controls remain enabled (can resume/next/stop), LIVE tag stays. STOP → they disable again and START re-enables (if drills remain).
4. **Training Flow styling (Bug Fix):** Hover over a drill row → no white highlight. Click a drill row → no cyan highlight, nothing gets "stuck". Drag-reorder still works. The active drill still shows the orange `.drill-item-active` highlight during a running session.
5. **No regressions / compile check:** Confirm no console errors on entering Play mode; verify EditMode preview still renders the dashboard.
