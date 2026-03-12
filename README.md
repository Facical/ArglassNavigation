# Second Screen, Second Opinion

**Supporting Smart Glass Navigation with a Handheld Companion Display Under Uncertainty**

A research prototype for studying hybrid AR navigation using OST smart glasses paired with a handheld companion display. Built for a within-subjects experiment (N=24) targeting **ISMAR 2026**.

## Research Context

Optical see-through (OST) smart glasses offer head-up AR navigation but suffer from limited field of view and low display resolution — making it hard to convey rich contextual information. We investigate whether a **handheld companion display** (the "second screen") can serve as a complementary information source that improves navigation decision-making under uncertainty.

**Core idea:** The glass provides *WHERE to go* (AR directional arrows), while the handheld provides *WHAT to know* (interactive map, POI cards, mission references). The only difference between conditions is access to this companion display.

### Experiment Design

| | |
|---|---|
| **Design** | 2 (Glass-Only vs. Hybrid) x Within-subjects |
| **Participants** | 24 (4 counterbalancing groups x 6) |
| **Route** | Single route (Route B), 9 waypoints, ~55m indoor corridor |
| **Mission sets** | 2 sets (Set1 / Set2) with 5 missions each (A1→B1→A2→B2→C1) |
| **Uncertainty triggers** | 4 types: degraded guidance, info mismatch, low resolution, absent guidance |
| **Counterbalancing** | Condition order (2) x Mission set order (2) |
| **Environment** | University building basement floor (KIT Digital Building B1F) |

### Research Questions

- **RQ-a:** How do participants use the Beam Pro information hub (frequency, timing, duration, content type)?
- **RQ-b:** How does the device condition affect trust calibration at uncertainty points?
- **RQ-c:** How does the device condition affect navigation efficiency (time, stops)?

## Features

- **AR arrow navigation** on XREAL Air2 Ultra smart glasses
- **3-tab information hub** on XREAL Beam Pro: interactive map, POI detail cards, mission reference panel
- **Spatial Anchor localization** with automatic fallback mode (zero-anchor calibration, image tracking alignment)
- **Mission-based wayfinding** with 3 mission types: Direction & Verify, Ambiguous Decision, Information Integration
- **4 uncertainty triggers** that induce cross-referencing behavior
- **35+ event types** logged to CSV (head tracking, device switching, mission responses, spatial events)
- **Experimenter control panel** with real-time state management and manual overrides
- **Python analysis pipeline** for device switching, trust calibration, verification accuracy, and trigger analysis

## System Architecture

### Hardware

| Device | Role |
|--------|------|
| **XREAL Air2 Ultra** | OST smart glasses — AR arrow overlay, mission briefing/verification UI |
| **XREAL Beam Pro** | Android handheld — 3-tab information hub, experimenter controls |

### Software Stack

| Component | Version |
|-----------|---------|
| Unity | 2022.3.62f2 (LTS) |
| XREAL SDK | 3.1.0 |
| AR Foundation | 5.1.5 |
| XR Hands | 1.4.1 |
| Build target | Android (ARM64, IL2CPP) |

### DDD Architecture (Onion)

The Unity project follows Domain-Driven Design with 4 assembly definitions:

```
ARNav.Domain    (pure C#, no engine references)
    ↑
ARNav.Runtime   (Application, Core, Navigation, Mission, Presentation, Logging)
    ↑
ARNav.Debug     (editor testing/debug tools)
    ↑
ARNav.Editor    (scene setup, build validation)
```

Cross-layer communication uses a **Domain Event Bus** (publish/subscribe) with ~20 domain event types. All logging flows through `ObservationService`, which subscribes to domain events and delegates to `EventLogger`.

### 3-Canvas Architecture

| Canvas | Render Mode | Purpose |
|--------|-------------|---------|
| ExperimentCanvas | WorldSpace (head-locked) | Glass-side UI (AR arrows, mission panels) |
| BeamProCanvas | ScreenSpaceOverlay | 3-tab information hub (map, cards, reference) |
| ExperimenterCanvas | ScreenSpaceOverlay | Experimenter HUD and flow controls |

## Repository Structure

```
ARglasses/
├── ARNavExperiment/           # Unity project
│   └── Assets/
│       ├── Scripts/
│       │   ├── Domain/        # Events, interfaces, value objects
│       │   ├── Application/   # Event bus, orchestrators (5 services)
│       │   ├── Core/          # State machines, session, spatial anchors
│       │   ├── Navigation/    # Waypoints, AR arrows, triggers
│       │   ├── Mission/       # Mission FSM, ScriptableObject data
│       │   ├── Presentation/  # Glass / BeamPro / Experimenter / Mapping / Shared
│       │   ├── Logging/       # CSV event logger, head tracker
│       │   ├── Debug/         # Editor movement, glass capture
│       │   └── Editor/        # Scene auto-setup (13 tools)
│       └── Data/              # ScriptableObject assets, floor plans
├── analysis/                  # Python analysis scripts (4 scripts → output/)
├── data/
│   ├── raw/                   # Experiment CSV logs
│   └── surveys/               # Post-experiment survey data
├── docs/                      # Experiment design, protocol, questionnaires
├── tools/                     # adb utilities, floor plan extraction
└── CLAUDE.md                  # Detailed project documentation
```

## Getting Started

### Prerequisites

- **Unity 2022.3.62f2** (LTS) — install via Unity Hub
- **Git LFS** — required for `.aar` and binary files

```bash
git lfs install
git clone https://github.com/Facical/ArglassNavigation.git
cd ArglassNavigation
git lfs pull
```

### Initial Setup (mandatory order)

> Skipping or reordering these steps will break hand tracking and touch input.

1. Open the project in Unity Hub (2022.3.62f2)
2. **Import XRI Starter Assets** — Package Manager → XR Interaction Toolkit → Samples → Starter Assets
3. **Run `XREAL > Setup Hand Tracking`** — creates Hand Ray prefabs and InputAction bindings
4. **Run `ARNav > Master Setup > Full Setup`** — 10-step automatic scene configuration
5. **Run `ARNav > Master Setup > Build & Validate`** — verifies Android build settings

For full setup details, see [`CLAUDE.md`](CLAUDE.md).

### Building

1. File → Build Settings → select Android platform
2. Ensure the following settings:
   - Graphics API: **OpenGLES3 only** (no Vulkan)
   - Architecture: **ARM64**
   - Minimum API Level: **31+**
   - Scripting Backend: **IL2CPP**
3. Build and deploy to XREAL Beam Pro

### Editor Test Mode

Activate via `ARNav > Master Setup > Editor Test Mode`. Keyboard controls:

| Key | Action |
|-----|--------|
| WASD | Move |
| Shift | Sprint |
| Right-click drag | Look around |
| N | Advance experiment state |
| M | Start mission |
| J | Teleport to waypoint |
| B | Toggle BeamPro view |
| R | Toggle recording |

## Experiment Protocol

1. **Pre-survey** — demographics, spatial ability, tech familiarity
2. **Calibration** — spatial anchor relocalization (~30s)
3. **Condition 1** — 5 missions along Route B (Set1 or Set2)
4. **NASA-TLX + Trust Scale** — post-condition questionnaire
5. **Condition 2** — 5 missions with alternate set
6. **NASA-TLX + Trust Scale** — post-condition questionnaire
7. **Post-survey** — preference, open-ended feedback

Total duration: ~50 minutes per participant.

## Data & Analysis

### Data Collection

Event logs are saved on-device at:
```
/storage/emulated/0/Android/data/com.KIT_HCI.ARNavExperiment/files/data/raw/
```

Pull to local machine:
```bash
./tools/pull_diagnostics.sh     # → data/diagnostics/ + data/raw/
./tools/pull_glass_captures.sh  # → debug_captures/ (glass-view video)
```

CSV format: 15 columns including timestamp, participant ID, condition, event type, head rotation, device state, mission responses, and more. See [`docs/데이터_포맷_명세.md`](docs/데이터_포맷_명세.md) for the full schema.

### Analysis Scripts

```bash
pip3 install numpy pandas scipy matplotlib pingouin

python3 analysis/analyze_device_switching.py     # Device switching patterns
python3 analysis/analyze_trust_performance.py    # Trust calibration analysis
python3 analysis/analyze_verification.py         # Mission accuracy analysis
python3 analysis/analyze_triggers.py             # Uncertainty trigger analysis
```

Results are saved to `analysis/output/`. Scripts auto-generate demo data if `data/raw/` is empty.

> **Note:** Analysis scripts use `AppleGothic` font for Korean labels (macOS). On other platforms, change to `NanumGothic` or another Korean font in the matplotlib config.

## Documentation

Detailed documentation is available in [`docs/`](docs/) (Korean):

- [Experiment Design](docs/실험_설계_v2.md)
- [Research Questions & Hypotheses](docs/RQ_정제.md)
- [Experiment Protocol](docs/실험_프로토콜.md)
- [Data Format Specification](docs/데이터_포맷_명세.md)
- [Waypoint Mapping Guide](docs/웨이포인트_매핑_가이드.md)
- [Implementation Roadmap](docs/구현_로드맵.md)

## License

This project is part of ongoing academic research. All rights reserved.

## Contact

HCI Research Group, Kumoh National Institute of Technology (KIT)
