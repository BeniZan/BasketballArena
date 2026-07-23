# Project Overview
- **Game Title:** BasketballArena
- **High-Level Concept:** A basketball training/coaching app where a coach drives live drill sessions and monitors player streams from a Coach Dashboard built with UI Toolkit.
- **Players:** Networked multiplayer (Netcode for GameObjects present); coach + players.
- **Inspiration / Reference Games:** N/A (tool/coaching UI feature).
- **Tone / Art Direction:** Dark, high-contrast HUD; cyan (`rgb(0,212,255)`) + orange (`rgb(255,107,0)`) accents on near-black panels.
- **Target Platform:** StandaloneWindows64 (+ XR / Meta OpenXR present).
- **Screen Orientation / Resolution:** Landscape.
- **Render Pipeline:** URP (`PC_RPAsset`).

# Feature Summary
Add the **"other view" of the Coach Dashboard** — the **Coach View card** — plus the **missing exercise-category icons**, by wiring already-imported Figma assets into the dashboard `.uxml`. No new asset download is required (see note below).

### Figma / asset-download status
- The provided Figma link (`figma.com/design/S29HLaRMZwMnQNMpW72ifi`) returns **HTTP 403** to direct fetch, and the interactive Figma-import tooling is not reachable in this session.
- **However, all sprites the current design references already exist on disk** at `Assets/UI/CoachDashboard/` (Sprite-type, verified) and are already assigned in the `CoachDashboardUIToolkitController` inspector on the Dash scene.
- The gap is **markup-only**: four `<ui:Image>` elements the controller expects do not exist in the `.uxml`, so their (already-assigned) sprites never render.
- If the target Figma frame contains **brand-new** assets beyond the four below, the user must export those PNGs into `Assets/UI/CoachDashboard/`; they would then be wired identically (out of scope until provided).

# Game Mechanics
## Core Gameplay Loop
Unchanged. Coach builds a training session (drills), starts/pauses/steps the session, and watches the player stream. This feature only adds the missing **Coach View** toggle card and category iconography to the existing dashboard.

## Controls and Input Methods
Unchanged (New Input System). The Coach View card is a visual/toggle element; no new input bindings. Interactivity for the toggle is optional (see Step 3, optional).

# UI
Target file: `Assets/UI/CoachDashboard/CoachDashboard.uxml` (StyleSheet: `Assets/UI/CoachDashboard/CoachDashboard.uss`).

### 1) Coach View card — left sidebar
Placed inside `.cards-section`, immediately after the second team card (`H2`), matching the existing `.coach-view-card` style (orange tint, `margin-top: 10px`, `height: 58px`).

```
left-sidebar
 └─ cards-section
     ├─ playerViewCard (H1 team card)      [existing]
     ├─ team-card (H2)                      [existing]
     └─ coachViewCard (.coach-view-card)    [NEW]
          ├─ .coach-view-left
          │    ├─ Image  #coachViewEyeIcon (.coach-view-icon)   ← Container_1_53
          │    └─ Label  "COACH VIEW" (.coach-view-text)
          └─ .coach-view-toggle .active
               └─ .toggle-knob
```

### 2) Exercise category icons — right sidebar
Add one `<ui:Image class="exercise-icon">` into each of the three foldout headers' `.exercise-left-group`, between the chevron and the label:

```
exercise-foldout-header
 └─ exercise-left-group
     ├─ Image #<cat>Chevron (.exercise-chevron)   [existing]
     ├─ Image #<cat>Icon    (.exercise-icon)       [NEW]   ← Icon_1_148/156/164
     └─ Label  "<CATEGORY>" (.exercise-text)        [existing]
```
Where `<cat>` = `pickAndRoll`, `shooting`, `postPlays` → image names `pickAndRollIcon`, `shootingIcon`, `postPlaysIcon` (exact names the controller's `ApplySprites()` already targets).

# Key Asset & Context
- **Edit:** `Assets/UI/CoachDashboard/CoachDashboard.uxml` — add 1 Coach View card block + 3 header icon `<Image>` elements.
- **Reuse (already present):** `Assets/UI/CoachDashboard/CoachDashboard.uss` classes `.coach-view-card`, `.coach-view-left`, `.coach-view-icon`, `.coach-view-text`, `.coach-view-toggle`, `.toggle-knob`, `.exercise-icon`. No USS changes required.
- **Sprites (already on disk + assigned in controller):** `Container_1_53.png` (coach eye), `Icon_1_148.png`, `Icon_1_156.png`, `Icon_1_164.png`.
- **Controller (already wired):** `Assets/Scripts/Coach/CoachDashboardUIToolkitController.cs` — `ApplySprites()` already calls `SetImageSprite("coachViewEyeIcon"/"pickAndRollIcon"/"shootingIcon"/"postPlaysIcon", ...)`; these become no-ops today because the elements are absent. Once the markup exists, sprites render automatically. `SetImageSprite` null-guards, so no code change is strictly needed.
- **Scene:** Dash scene contains the controller with all sprites assigned (verified).

# Implementation Steps

### Step 1 — Add the Coach View card to the left sidebar
- **Description:** In `CoachDashboard.uxml`, inside `<ui:VisualElement ... class="cards-section">`, after the closing `</ui:VisualElement>` of the H2 team card (line ~53) and before the `cards-section` closing tag (line ~54), insert the Coach View card block (markup shown in the UI section). Use `name="coachViewCard"` and `name="coachViewEyeIcon"` on the image.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (independent region of the file from Step 2)

### Step 2 — Add category icons to the three exercise foldout headers
- **Description:** In `CoachDashboard.uxml`, inside each `.exercise-left-group` (pickAndRoll ~line 125, shooting ~line 137, postPlays ~line 149), insert `<ui:Image name="pickAndRollIcon" class="exercise-icon"/>` / `shootingIcon` / `postPlaysIcon` immediately after the existing chevron `<ui:Image>` and before the `<ui:Label>`.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 3 (Optional) — Make the Coach View toggle interactive + fix stale Reset() paths
- **Description:** Optional polish. (a) In the controller, query `coachViewCard`/`.coach-view-toggle` and add a click handler that toggles the `active` class and, if desired, calls a network setter — only if you want the card to be functional rather than a static indicator. (b) Update the `#if UNITY_EDITOR Reset()` sprite/font paths (currently point to non-existent `Assets/UI/FigmaImport/CoachDashboard` and `Assets/Figma/Fonts`) to the real paths `Assets/UI/CoachDashboard` and `Assets/Data/Fonts` so future `Reset` re-links correctly. Inspector assignments already work, so this is housekeeping only.
- **Assigned role:** developer
- **Dependencies:** Depends on Step 1 (for the card element to exist)
- **Parallelizable:** No

### Step 4 (Conditional) — Import genuinely new Figma assets
- **Description:** Only if the target Figma frame contains elements beyond the four already-imported assets. User exports the new PNGs into `Assets/UI/CoachDashboard/`, sets Texture Type = Sprite, assigns them on the controller (or via `Reset`), and adds matching `<ui:Image>` markup. Skipped unless new assets are supplied.
- **Assigned role:** developer
- **Dependencies:** External (user-provided assets)
- **Parallelizable:** No

# Verification & Testing
1. **Editor preview:** Open the Dash scene. The controller runs `InitializeUI` in edit mode; confirm the Coach View card renders in the left sidebar with the eye icon + "COACH VIEW" text + orange toggle, and the three exercise headers now show their category icons.
2. **Blank-image check:** Confirm no `<Image>` in the dashboard renders empty; the four previously-blank targets now show `Container_1_53` / `Icon_1_148` / `Icon_1_156` / `Icon_1_164`.
3. **Console:** Confirm no new UI Toolkit or `Q<Image>` null warnings/errors after the change.
4. **Play mode:** Enter play mode; verify layout is intact, existing controls (start/pause/stop/next/force, foldouts, drill list) still work, and (if Step 3a done) the Coach View toggle flips its `active` state on click.
5. **UXML validity:** Ensure the file still parses (no import errors on `CoachDashboard.uxml`).
