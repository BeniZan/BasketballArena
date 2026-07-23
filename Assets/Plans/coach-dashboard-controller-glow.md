# Project Overview
- **Game Title:** BasketballArena
- **High-Level Concept:** Basketball coaching app; the coach drives live drill sessions from a UI Toolkit Coach Dashboard.
- **Players:** Networked multiplayer (Netcode for GameObjects).
- **Tone / Art Direction:** Dark neon HUD — cyan `rgb(0,212,255)` + orange `rgb(255,107,0)` accents on near-black panels.
- **Target Platform:** StandaloneWindows64 (+ Meta OpenXR).
- **Render Pipeline:** URP (`PC_RPAsset`).

# Feature Summary
Make the playback buttons inside the `#controller` bar (`.control-bar-inner`, in the center pane's `.bottom-control-bar`) **glow** to signal they are usable, matching the neon button samples in the referenced Figma files.

Buttons affected (`name` → variant class): `startBtn` (.start), `nextBtn` (.next), `pauseBtn` (.pause), `stopBtn` (.stop), `forceBtn` (.force).

### Important technical constraint (glow in UI Toolkit)
- USS has **no `box-shadow`, no `filter`, no blur** — a literal soft outer bloom cannot be rendered with pure USS.
- **Chosen approach (recommended): "neon outline glow."** Give each button a resting accent-colored border, then intensify the border + background and apply a subtle scale on `:hover`. This reads as a glow/light-up and requires only USS. This is the approach detailed below.
- **Alternative (only if a true soft bloom is required):** author a 9-sliced radial-glow PNG per accent color and set it as `background-image` on a wrapper element behind each button. Heavier (new art assets + markup); out of scope unless requested.

### Figma access note
Both Figma links (`figma.com/design/S29HLaRMZwMnQNMpW72ifi` and `.../qqRX4BVajg0czlH6uVz58K`) return **HTTP 403** to direct fetch and the interactive Figma tooling is unreachable in this session, so exact glow radius/opacity values could not be read. Values below are chosen to match the existing dashboard's neon palette; easy to tune after a first look.

# Game Mechanics
## Core Gameplay Loop
Unchanged. This is a visual/feedback polish on the existing session controls (start/next/pause/stop/force).

## Controls and Input Methods
Unchanged (New Input System). No new bindings. Glow is a CSS-like `:hover` visual state; no C# changes required.

# UI
Target file: `Assets/UI/CoachDashboard/CoachDashboard.uss` (only file changed). The `#controller` structure already exists:

```
[VisualElement] name="controller" class="control-bar-inner"
  └─ [VisualElement] class="controls-group"
       ├─ [Button] name="startBtn" class="playback-btn start"
       ├─ [Button] name="nextBtn"  class="playback-btn next"
       ├─ [Button] name="pauseBtn" class="playback-btn pause"
       ├─ [Button] name="stopBtn"  class="playback-btn stop"
       └─ [Button] name="forceBtn" class="playback-btn force"
```

### Glow design (per accent)
- Base `.playback-btn`: add `border-width: 1px`, transparent resting border, and a `transition` on border-color/background-color/scale so the light-up animates.
- `.playback-btn:hover`: `scale: 1.04 1.04` (subtle lift for all buttons).
- Per-variant **resting** border (soft glow) + **hover** border/background (bright glow):
  - `.start` (orange): resting `rgba(255,107,0,0.55)` → hover border `rgb(255,107,0)`, bg `rgba(255,107,0,0.28)`.
  - `.next` (cyan): resting `rgba(0,212,255,0.55)` → hover border `rgb(0,212,255)`, bg `rgba(0,212,255,0.28)`, text brightens to white.
  - `.pause` (white/subtle): no resting border → hover border `rgba(255,255,255,0.55)`, bg `rgba(255,255,255,0.12)`, text white.
  - `.stop` (red): resting `rgba(239,68,68,0.55)` → hover border `rgb(239,68,68)`, bg `rgba(239,68,68,0.3)`.
  - `.force` (white/subtle): no resting border → hover border `rgba(255,255,255,0.55)`, bg `rgba(255,255,255,0.12)`, text white.

Note: `.start` and `.next` already carry an accent tint, so they get a persistent resting glow (they're the primary usable actions). `.pause`/`.force` stay neutral until hovered, keeping the bar calm.

# Key Asset & Context
- **Edit only:** `Assets/UI/CoachDashboard/CoachDashboard.uss` — modify `.playback-btn` base and add `:hover` + resting-glow rules for each variant (`.start/.next/.pause/.stop/.force`).
- **No UXML change** — element names/classes already exist.
- **No C# change** — `CoachDashboardUIToolkitController` wires clicks; glow is pure USS `:hover`.
- **Precedent in project:** `transition: rotate 0.15s ease-out;` is already used on `.exercise-chevron`, and `:hover` selectors already exist (`.exercise-sub-item:hover`, `.drill-item-close:hover`), so both patterns are known-good here.

# Implementation Steps

### Step 1 — Base playback button: border + transition + hover lift
- **Description:** In `.playback-btn`, change `border-width: 0` → `border-width: 1px`, add `border-color: rgba(0,0,0,0)` and `transition: border-color 0.15s ease-out, background-color 0.15s ease-out, scale 0.15s ease-out;`. Add new rule `.playback-btn:hover { scale: 1.04 1.04; }`.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** No (same file/region as Step 2)

### Step 2 — Per-variant resting + hover glow
- **Description:** Add resting `border-color` to `.playback-btn.start/.next/.stop`, and add `:hover` rules for all five variants with the border/background/text values listed in the UI section above.
- **Assigned role:** developer
- **Dependencies:** Depends on Step 1
- **Parallelizable:** No

### Step 3 (Optional) — Persistent "usable" glow driven by state
- **Description:** If you want the glow to indicate *enabled/usable* rather than only hover (e.g., START pulses while READY), add an `.is-usable` class toggled by the controller and give it the bright border. Requires small C# additions in `CoachDashboardUIToolkitController` (add/remove the class in `StartTimer/StopTimer/etc.`). Skipped unless requested.
- **Assigned role:** developer
- **Dependencies:** Depends on Step 2
- **Parallelizable:** No

# Verification & Testing
1. **Save + validate USS** parses with no errors.
2. **Editor preview** the Dash / `CoachDashboardUI` prefab: the START (orange) and NEXT (cyan) buttons show a subtle resting outline glow; PAUSE/FORCE stay neutral.
3. **Hover check** (Play mode or UI Builder preview): hovering any playback button brightens its border/background, lifts it slightly (scale), and brightens dim text to white.
4. **Console:** no new UI Toolkit warnings/errors.
5. **Regression:** existing click behavior (start/pause/stop/next/force) unchanged; layout width/height of the control bar unchanged (border is 1px, absorbed by existing sizing).
