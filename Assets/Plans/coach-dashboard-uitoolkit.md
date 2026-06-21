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
