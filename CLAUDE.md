# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Moon Deception is an asymmetric multiplayer survival game built with Unity 2022.3+ (URP). 1 Astronaut (FPS) vs up to 5 Aliens (TPS) among ~30 NPCs on a lunar base.

## Development Setup

1. Open project in Unity 2022.3+ with Universal Render Pipeline
2. Open `Assets/Scenes/SampleScene.unity`
3. Menu: **Moon Deception > Setup Phase 1** (if available)
4. Press Play

## Architecture

### Core Systems (Singleton Pattern)
All managers use singleton pattern with `Instance` static property:
- **GameManager** - Game state, phases (Lobby/Starting/Playing/Chaos/Ended), win conditions, timers
- **AudioManager** - Centralized audio with object pooling for SFX
- **GameUIManager** - Dynamic HUD that switches between Astronaut/Alien views
- **StressSystem** - Astronaut stress bar (triggers Chaos phase when maxed)
- **CameraShake** - Screen shake effects (shoot, impact, stress)
- **PostProcessController** - Dynamic URP post-processing tied to stress

### Game Phases
1. **Search Phase** - Aliens blend in with NPCs, astronaut hunts
2. **Chaos Phase** - Triggered when astronaut stress maxes out; lights go out, aliens transform, can attack openly

### Player Systems
- **Astronaut (FPS)**: `PlayerMovement.cs`, `PlayerShooting.cs`, `StressSystem.cs`, `AstronautHealth.cs`
- **Alien (TPS)**: `AlienController.cs`, `HungerSystem.cs`, `AlienAbilities.cs`, `AlienTransformation.cs`, `AlienHealth.cs`
- Switch between Astronaut/Alien with **TAB** key (testing only via `GameController.cs`)

### Key Static Properties
- `AlienController.IsAlienControlled` - Check if player is controlling alien
- `GameManager.Instance.CurrentPhase` - Current game phase
- `DefenseZone.NearestActiveZone` - Active defense zone during chaos

### Damage System
Classes implementing `IDamageable` interface (defined in `PlayerShooting.cs`):
- `NPCBehavior` - NPCs take damage and die
- `AlienHealth` - Alien player takes damage
- `AstronautHealth` - Astronaut takes damage from alien attacks

### Event System
GameManager uses UnityEvents for phase transitions:
- `OnGameStart`, `OnChaosPhase`, `OnGameEnd`, `OnNPCKilled`, `OnAlienKilled`

### Auto-Initialization
`GameBootstrap.cs` (attach to empty GameObject) auto-creates all manager instances at runtime if they don't exist.

## Controls

### Astronaut (FPS)
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| Left Click | Shoot |
| E | Interact (defense zone) |
| Escape | Pause menu |

### Alien (TPS)
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Camera orbit |
| E | Interact/Eat |
| 1,2,3,4 | Chaos abilities (cooldowns) |
| Left Click | Attack (Chaos phase only) |

## Development Phases

Track progress in `DEV_TRACKER.md`:
- **Phase 1**: Solo Astronaut FPS ✅
- **Phase 2**: Solo Alien TPS ✅
- **Phase 3**: Map & Gameplay Loop (4 sections, spawn system)
- **Phase 4**: Multiplayer (Unity Netcode for GameObjects)
- **Phase 5**: Steam Integration (Steamworks.NET)

## Audio

AudioManager expects audio clips assigned in inspector or loaded from Resources. Currently needs actual .wav/.mp3 files added to:
- `AudioManager.gunshot`, `footstep`, `bulletImpactMetal`, `bulletImpactFlesh`, etc.
