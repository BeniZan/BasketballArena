# Project Overview

- **Game Title:** Basketball Arena
- **High-Level Concept:** An AR basketball training and coaching dashboard designed to view player POV streams, construct coaching sessions/drills, toggle stream modes, and manage training flows.
- **Players:** Single-player / Coach viewer.
- **Tone / Art Direction:** Dark, technical, sleek AR HUD theme with cyan and orange neon accents.
- **Target Platform:** PC / AR Standalone
- **Screen Orientation / Resolution:** Landscape (e.g., 1920x1080)
- **Render Pipeline:** Universal Render Pipeline (URP)

# Game Mechanics

## Core Gameplay Loop
The Coach uses this dashboard to monitor a live player stream in real-time, construct dynamic drills (Pick & Roll, Shooting, Post Plays, etc.), manage active training reps, and track performance analytics.

## Controls and Input Methods
- **Mouse clicks / Touch gestures:** Click UI buttons to toggle offense/defense states, switch realistic vs hologram stream views, add exercises, start/pause/stop the session timer, and view analytics.

# UI Toolkit Design & Architecture

Rather than stretching Canvas-based uGUI components which can distort on different aspect ratios, we will implement the **Coach Dashboard** using Unity's modern **UI Toolkit** (UXML/USS) framework. 

## 1. Visual Layout Hierarchy (UXML)
The entire dashboard layout is structured as a three-column CSS-like Flexbox container:
```
VisualElement (.dashboard-root)
├── VisualElement (.left-sidebar) [width: 279px, background: #0A0A0A]
│   ├── Image (.logo-image) [ARena logo - Container.png]
│   ├── VisualElement (.separator)
│   ├── VisualElement (.cards-section)
│   │   ├── VisualElement (.team-card H1)
│   │   ├── VisualElement (.team-card H2)
│   │   └── VisualElement (.coach-view-card)
│   └── VisualElement (.bottom-status) [0/3 Active]
├── VisualElement (.center-pane) [flex-grow: 1, background: #000]
│   ├── VisualElement (.center-header)
│   │   ├── Label (.stream-title) [PLAYER STREAM]
│   │   └── VisualElement (.stream-mode-toggles) [REALISTIC | HOLOGRAM]
│   ├── VisualElement (.stream-viewport) [Black background, rounded 14px, bordered]
│   │   └── VisualElement (.viewport-placeholder) [Awaiting Player Stream...]
│   └── VisualElement (.bottom-control-bar)
│       └── VisualElement (.control-bar-inner)
│           ├── VisualElement (.status-group) [READY · 00:00 · REP 0/0]
│           └── VisualElement (.controls-group) [START | NEXT | PAUSE | STOP | FORCE]
└── VisualElement (.right-sidebar) [width: 419px, background: #0A0A0A]
    ├── VisualElement (.exercise-library)
    │   ├── VisualElement (.section-header) [EXERCISE LIBRARY | Tap to add →]
    │   └── ScrollView (.exercise-list) [Pick & Roll, Shooting, Post Plays]
    ├── VisualElement (.separator)
    ├── VisualElement (.training-flow)
    │   ├── VisualElement (.section-header) [TRAINING FLOW | 0 drills]
    │   ├── ScrollView (.drill-list-container)
    │   │   └── VisualElement (.build-session-placeholder) [Icon, "Build Your Session", subtext]
    │   └── Button (.random-drill-btn) [RANDOM DRILL]
    ├── VisualElement (.separator)
    └── VisualElement (.analytics-section) [ANALYTICS | Upgrade / Subtext]
```

## 2. Style Sheet Specifications (USS)
- **Colors:**
  - Background Dark: `#0A0A0A`
  - Body Black: `#000000`
  - Cyan Accent: `#00D4FF` (alpha 0.15 for card indicators, 1.0 for labels)
  - Orange Accent: `#FF6B00` (alpha 0.08 for Coach View background, 1.0 for active titles)
  - Red Accent: `#EF4444` (alpha 0.12 for Stop button background)
  - Text Gray: `#64748B`
- **Typography:**
  - Text elements will use `-unity-font-definition: url('project://database/Assets/Figma/Fonts/Barlow Condensed_700_SDF.asset');` or `Inter_400_SDF.asset` to achieve exact designer typography.
- **Button Styling:**
  - Smooth hover and active transitions using USS transitions (`transition: background-color 0.2s;`).
  - No Canvas scaling artifacts: all buttons stay crisp and keep their relative dimensions perfectly using exact flex properties.

# Key Asset & Context

- **UXML File:** `Assets/UI/FigmaImport/CoachDashboard/CoachDashboard.uxml`
- **USS File:** `Assets/UI/FigmaImport/CoachDashboard/CoachDashboard.uss`
- **Controller Script:** `Assets/Scripts/Coach/CoachDashboardUIToolkitController.cs` (A clean script to manage UI Toolkit events, mimicking uGUI logical actions).

# Implementation Steps

### Step 1: Write the USS Stylesheet (.uss)
- Create `CoachDashboard.uss`.
- Define classes for all components: `.dashboard-root`, `.left-sidebar`, `.center-pane`, `.right-sidebar`, `.team-card`, `.viewport-placeholder`, `.playback-btn`, etc.
- Configure exact borders, paddings, backgrounds, text-colors, and custom SDF Font definitions.
- **Role:** Developer | **Dependencies:** None | **Parallelizable:** Yes

### Step 2: Write the UXML Document (.uxml)
- Create `CoachDashboard.uxml`.
- Construct the structured hierarchical tree matching the Figma layout column-by-column.
- Assign appropriate CSS class names to all elements.
- Link `CoachDashboard.uss` directly as the stylesheet reference.
- **Role:** Developer | **Dependencies:** Step 1 | **Parallelizable:** No

### Step 3: Implement the UI Toolkit Controller Script
- Create `CoachDashboardUIToolkitController.cs`.
- Query all interactive elements by class name or `#id` name (e.g. `rootVisualElement.Q<Button>("startButton")`).
- Wire up the exact same dashboard logic as the original script:
  - **Timer ticking:** ticking up time when running, formatted as `00:00`.
  - **Rep counter:** clicking NEXT increments rep, updating `REP count/total`.
  - **Offense/Defense toggle coloring:** updating background-colors on click.
  - **Dynamic list additions:** instantiating drill items inside the scrollview on exercise button click, hide placeholder if count > 0.
- **Role:** Developer | **Dependencies:** Step 2 | **Parallelizable:** No

### Step 4: Scene Setup and Integration
- Disable the old uGUI `Canvas/ScreenParentTransform/CoachDashboardCanvas` inside `Assets/Dashboard/Dash.unity` to prevent visual overlapping.
- Add a new root GameObject `UIDocument_CoachDashboard` with a `UIDocument` component to `Assets/Dashboard/Dash.unity`.
- Assign `CoachDashboard.uxml` to the `UIDocument` source asset.
- Attach the `CoachDashboardUIToolkitController` to the GameObject and save the scene.
- **Role:** Developer | **Dependencies:** Step 3 | **Parallelizable:** No

# Verification & Testing

- **Layout Fit & Stretching:** Scale the game window to various aspect ratios (16:9, 16:10, 21:9) and verify that the layout scales flawlessly, buttons maintain correct shapes, and columns remain sharp.
- **Button Interactions:** Click START, NEXT, PAUSE, STOP, and FORCE to verify the status label, timer formatting, and rep numbers respond dynamically.
- **Exercise Library Additions:** Click "PICK & ROLL", "SHOOTING", "POST PLAYS", and "RANDOM DRILL" and verify they dynamically spawn elegant list items in the Training Flow list and hide the placeholder.
- **Stream View Toggling:** Click "REALISTIC" and "HOLOGRAM" to verify background and text highlights toggle perfectly.

---

# Amendment A — Convert Exercise Library Items into Foldouts

## A.0 Goal & Rationale
The three items inside `#exerciseLibrary` → `.exercise-list` (PICK & ROLL, SHOOTING, POST PLAYS) are currently plain `Button`s (`exercise-item-btn`) that each call `AddDrill(category)` directly. This is semantically wrong: each of these is a **category / type of training**, not the training itself. They should become **collapsible foldouts** (matching the attached screenshots): a header row with a left chevron, the category icon, the title, and a right-aligned count badge. Expanding a header reveals the individual drills (the real trainings) underneath, each with a small triangle marker. Clicking an individual drill adds it to the Training Flow.

## A.1 Confirmed Decisions (from user)
- **Interaction model:** Clicking a category **header expands/collapses** the foldout. Clicking an **individual sub-drill row adds** that drill to the Training Flow. The header itself never adds a drill.
- **Implementation:** **Custom foldout** (not Unity's built-in `<Foldout>`) — a header `Button` + a collapsible content `VisualElement`, toggled via a `.expanded` USS class with an animated chevron rotation. This gives an exact match to the screenshot (chevron-left, badge-right) with full styling control.
- **Count badge:** **Auto-derived** from the number of child drills in each category (no longer the hard-coded 12 / 12 / 8).

## A.2 Sub-Drill Data (placeholders — user skipped naming, freely editable in Inspector)
Defined as serialized `string[]` arrays on the controller so they can be edited without touching code. POST PLAYS uses the names visible in the screenshot.
- **PICK & ROLL:** High Screen, Side P&R, Horns, Spain P&R, Step-Up, Drag Screen
- **SHOOTING:** Catch & Shoot, Off The Dribble, Spot-Up Corner, Pull-Up Mid, Transition 3, Free Throws
- **POST PLAYS:** Drop Step, Up & Under, Seal & Feed, Face Up

## A.3 UXML Changes — `Assets/UI/FigmaImport/CoachDashboard/CoachDashboard.uxml`
Replace each of the three `<ui:Button class="exercise-item-btn">` blocks (lines ~132–158) inside `<ui:ScrollView class="exercise-list">` with a foldout structure. The content container is left **empty** in UXML and is populated at runtime by the controller from the A.2 arrays. Pattern per category (Pick & Roll shown):
```xml
<ui:VisualElement name="pickAndRollFoldout" class="exercise-foldout">
    <ui:Button name="pickAndRollHeader" class="exercise-foldout-header">
        <ui:VisualElement class="exercise-left-group">
            <ui:Image name="pickAndRollChevron" class="exercise-chevron"/>
            <ui:Image name="pickAndRollIcon" class="exercise-icon"/>
            <ui:Label text="PICK &amp; ROLL" class="exercise-text"/>
        </ui:VisualElement>
        <ui:VisualElement class="exercise-badge">
            <ui:Label name="pickAndRollBadge" text="0" class="exercise-badge-text"/>
        </ui:VisualElement>
    </ui:Button>
    <ui:VisualElement name="pickAndRollContent" class="exercise-foldout-content"/>
</ui:VisualElement>
```
Repeat with names `shootingFoldout / shootingHeader / shootingChevron / shootingIcon / shootingBadge / shootingContent` and `postPlaysFoldout / postPlaysHeader / postPlaysChevron / postPlaysIcon / postPlaysBadge / postPlaysContent`. Keep the existing `pickAndRollIcon` / `shootingIcon` / `postPlaysIcon` names so `ApplySprites()` still binds the category icons. The `.small` badge variant is dropped (size is now uniform).

## A.4 USS Changes — `Assets/UI/FigmaImport/CoachDashboard/CoachDashboard.uss`
Replace the `.exercise-item-btn` rule (and `.exercise-badge.small`) and add foldout styling. Reuse existing `.exercise-left-group`, `.exercise-icon`, `.exercise-text`, `.exercise-badge`, `.exercise-badge-text` as-is.
```css
.exercise-foldout {
    flex-direction: column;
}

.exercise-foldout-header {       /* was .exercise-item-btn */
    height: 44px;
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    background-color: transparent;
    border-width: 0;
    padding-left: 0;
    padding-right: 0;
}

.exercise-chevron {
    width: 10px;
    height: 10px;
    margin-right: 8px;
    rotate: 0deg;                 /* collapsed = points right (>) */
    transition: rotate 0.15s ease-out;
    -unity-background-scale-mode: scale-to-fit;
}

.exercise-foldout.expanded .exercise-chevron {
    rotate: 90deg;               /* expanded = points down (v) */
}

.exercise-foldout-content {
    flex-direction: column;
    display: none;               /* collapsed by default */
    padding-left: 6px;
}

.exercise-foldout.expanded .exercise-foldout-content {
    display: flex;
}

.exercise-sub-item {
    height: 34px;
    flex-direction: row;
    align-items: center;
    background-color: transparent;
    border-width: 0;
    padding-left: 0;
}

.exercise-sub-icon {             /* small triangle marker */
    width: 10px;
    height: 10px;
    margin-right: 12px;
    -unity-background-scale-mode: scale-to-fit;
}

.exercise-sub-text {
    font-size: 12px;
    color: #94A3B8;
}

.exercise-sub-item:hover .exercise-sub-text {
    color: #E5E7EB;
}
```
Note: `display` is not animatable in USS; the chevron rotation provides the visible transition. If a height-based slide animation is desired later it can be added, but `display` toggling is the simplest robust approach.

## A.5 Controller Changes — `Assets/Scripts/Coach/CoachDashboardUIToolkitController.cs`
1. **Remove** the three category `Button` fields `_pickAndRollBtn`, `_shootingBtn`, `_postPlaysBtn` (lines 53–55) and their old `clicked += () => AddDrill(...)` wiring (lines 169–171) and queries (lines 127–129).
2. **Add serialized data + icon fields** under the existing `[Header("Sprites")]`/new header:
```csharp
[Header("Exercise Foldouts")]
[SerializeField] private Sprite _iconChevron;       // small chevron (rotates via USS)
[SerializeField] private Sprite _iconSubDrill;      // triangle marker for sub-items
[SerializeField] private string[] _pickAndRollDrills = { "High Screen", "Side P&R", "Horns", "Spain P&R", "Step-Up", "Drag Screen" };
[SerializeField] private string[] _shootingDrills    = { "Catch & Shoot", "Off The Dribble", "Spot-Up Corner", "Pull-Up Mid", "Transition 3", "Free Throws" };
[SerializeField] private string[] _postPlaysDrills    = { "Drop Step", "Up & Under", "Seal & Feed", "Face Up" };
```
3. **Add a small helper** to build each foldout. Call it three times during setup (works in both EditMode preview and PlayMode; header click only toggles, so it is safe to wire unconditionally):
```csharp
private void SetupExerciseFoldout(string baseName, string[] drills)
{
    var foldout = _root.Q<VisualElement>(baseName + "Foldout");
    var header  = _root.Q<Button>(baseName + "Header");
    var content = _root.Q<VisualElement>(baseName + "Content");
    var badge   = _root.Q<Label>(baseName + "Badge");
    if (foldout == null || header == null || content == null) return;

    // Badge auto-count
    if (badge != null) badge.text = drills.Length.ToString();

    // Populate sub-drill rows
    content.Clear();
    foreach (var drillName in drills)
    {
        var row = new Button { name = baseName + "_" + drillName };
        row.AddToClassList("exercise-sub-item");
        var icon = new Image(); icon.AddToClassList("exercise-sub-icon");
        if (_iconSubDrill != null) icon.sprite = _iconSubDrill;
        var lbl = new Label(drillName); lbl.AddToClassList("exercise-sub-text");
        if (_barlow700 != null) lbl.style.unityFontDefinition = new StyleFontDefinition(_barlow700);
        row.Add(icon); row.Add(lbl);
        string captured = drillName;
        if (Application.isPlaying) row.clicked += () => AddDrill(captured);
        content.Add(row);
    }

    // Apply chevron sprite
    var chevron = _root.Q<Image>(baseName + "Chevron");
    if (chevron != null && _iconChevron != null) chevron.sprite = _iconChevron;

    // Header toggles expand/collapse
    header.clicked -= () => { };               // (use a named method to allow unsubscribe — see note)
    header.clicked += () => foldout.ToggleInClassList("expanded");
}
```
   - **Wiring note:** to avoid double-subscription across EditMode→PlayMode reloads (the existing code already guards other buttons this way), call `SetupExerciseFoldout` from the same place `ApplySprites()` is called, and guard the whole block so foldouts are rebuilt cleanly each `OnEnable` (the `content.Clear()` already prevents duplicate rows; for the header use a stored delegate or rebuild pattern consistent with the existing controller style).
4. **Call** `SetupExerciseFoldout("pickAndRoll", _pickAndRollDrills);`, `SetupExerciseFoldout("shooting", _shootingDrills);`, `SetupExerciseFoldout("postPlays", _postPlaysDrills);` right after `ApplySprites(); ApplyTypography();` (around line 138).
5. **`ApplyTypography()`** — add `exercise-sub-text` to the `barlow700` (or `inter400`) class branch so sub-item labels get the correct font.
6. **`ApplySprites()`** — category icons (`pickAndRollIcon`, etc.) remain bound as before; chevron/sub-drill sprites are applied inside `SetupExerciseFoldout`.

## A.6 Required Assets
- A **chevron sprite** (`_iconChevron`) — small right-pointing triangle/caret; rotated 90° via USS when expanded. If none exists in `Assets/Figma/`, reuse an existing arrow/triangle sprite or it gracefully no-ops (null-guarded).
- A **sub-drill triangle marker** (`_iconSubDrill`) — the small ▷ shown beside each drill in the screenshot. Optional; null-guarded.
- Both are assigned in the Inspector on the `CoachDashboardUIToolkitController` component in `Assets/Dashboard/Dash.unity`.

## A.7 Implementation Steps

### Step A1: Update USS
- **Description:** Edit `CoachDashboard.uss` per A.4 — rename `.exercise-item-btn` → `.exercise-foldout-header`, remove `.exercise-badge.small`, add `.exercise-foldout`, `.exercise-chevron`, `.exercise-foldout-content`, `.exercise-sub-item`, `.exercise-sub-icon`, `.exercise-sub-text` and the `.expanded` state rules.
- **Assigned role:** developer | **Dependencies:** None | **Parallelizable:** Yes

### Step A2: Update UXML
- **Description:** Edit `CoachDashboard.uxml` per A.3 — replace the three `exercise-item-btn` buttons with `exercise-foldout` blocks (header + empty content), keeping the existing icon names.
- **Assigned role:** developer | **Dependencies:** Step A1 | **Parallelizable:** No

### Step A3: Update Controller
- **Description:** Edit `CoachDashboardUIToolkitController.cs` per A.5 — remove old category-button fields/queries/wiring, add serialized drill arrays + chevron/sub-drill sprite fields, add `SetupExerciseFoldout(...)` and call it for the three categories, update `ApplyTypography()` for `exercise-sub-text`. Keep `AddDrill` / Training Flow logic unchanged.
- **Assigned role:** developer | **Dependencies:** Step A2 | **Parallelizable:** No

### Step A4: Assign Assets in Scene
- **Description:** In `Assets/Dashboard/Dash.unity`, assign the chevron and sub-drill sprites on the controller; verify drill arrays show in the Inspector and are editable.
- **Assigned role:** developer | **Dependencies:** Step A3 | **Parallelizable:** No

## A.8 Verification & Testing (Amendment)
- **Foldout collapse/expand:** All three start collapsed showing only header + badge. Clicking a header expands it (chevron rotates from `>` to `v`) and reveals the sub-drills; clicking again collapses it.
- **Auto badge count:** PICK & ROLL shows `6`, SHOOTING shows `6`, POST PLAYS shows `4` (matching array lengths); editing an array in the Inspector updates the badge.
- **Sub-drill add:** Clicking an individual sub-drill (e.g., "Drop Step") adds it by name to the Training Flow list, hides the "Build Your Session" placeholder, and increments the `N drills` counter. Clicking a header never adds a drill.
- **EditMode preview:** With `[ExecuteAlways]`, headers/rows render and toggle in the Scene/Game view without entering Play Mode; no duplicate rows after domain reload.
- **No console errors** on enter/exit Play Mode (null-guarded sprite/element lookups).
