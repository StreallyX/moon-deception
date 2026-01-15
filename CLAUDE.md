# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Moon Deception** - Asymmetric multiplayer survival game built with Unity 2022.3+ (URP).
- 1 Astronaut (FPS) vs up to 5 Aliens (TPS) among ~30 NPCs on a lunar base
- Two phases: Search (aliens hide) → Chaos (aliens hunt)
- Target: Steam release with multiplayer

## Quick Start

1. Open project in Unity 2022.3+ with Universal Render Pipeline
2. Open `Assets/Scenes/SampleScene.unity`
3. Press Play
4. **TAB** = Switch between Astronaut/Alien (testing)
5. **F5** = Manually activate DefenseZone (testing)

## Architecture

### Singleton Managers (use `.Instance`)
| Manager | Purpose |
|---------|---------|
| `GameManager` | Game phases, win conditions, timers |
| `AudioManager` | Sound effects with object pooling |
| `GameUIManager` | Dynamic HUD (Astronaut/Alien views) |
| `StressSystem` | Astronaut stress (triggers Chaos at max) |
| `CameraShake` | Screen shake effects |
| `PostProcessController` | URP post-processing tied to stress |
| `MenuManager` | All menus (Main, Pause, Settings, GameOver) |

### Game Phases
```
Playing → (stress maxed) → Chaos → Ended
```
- **Playing**: Aliens blend with NPCs, astronaut hunts
- **Chaos**: Lights out, aliens transform (200 HP, wall-hack, can attack)

### Player Systems

**Astronaut (FPS)**:
- `PlayerMovement.cs` - WASD, mouse look, jump
- `PlayerShooting.cs` - Raycast shooting, reload system (R key), magazine
- `StressSystem.cs` - Stress bar (kill innocent +stress, kill alien -stress)
- `AstronautHealth.cs` - Takes damage from alien attacks

**Alien (TPS)**:
- `AlienController.cs` - Third-person movement, camera orbit
- `HungerSystem.cs` - Hunger decay, coffee boost, starving reveals alien
- `AlienAbilities.cs` - 4 chaos powers (keys 1,2,3,4) with cooldowns
- `AlienTransformation.cs` - Transform in chaos (speed, wall-hack, attack)
- `AlienHealth.cs` - Takes damage from astronaut

### Damage System
`IDamageable` interface (in `PlayerShooting.cs`):
- Use `GetComponentInParent<IDamageable>()` for child mesh hits
- Implementers: `NPCBehavior`, `AlienHealth`, `AstronautHealth`, `StressSystem`

### Event System
GameManager UnityEvents:
- `OnGameStart`, `OnChaosPhase`, `OnGameEnd`
- `OnNPCKilled`, `OnAlienKilled`

Subscribe in Start() with null check + LateSubscribe coroutine fallback.

### Auto-Initialization
`GameBootstrap.cs` auto-creates managers and adds components:
- Adds `AlienHealth`, `AlienAbilities`, `AlienTransformation` to alien
- Adds `AstronautHealth`, `CameraShake` to astronaut

## Controls

### Astronaut
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| Left Click | Shoot |
| R | Reload |
| E | Interact (DefenseZone = minigun) |
| Escape | Pause |

### Alien
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Camera orbit |
| E | Interact/Eat |
| 1,2,3,4 | Chaos abilities |
| Left Click | Attack (Chaos only) |

### Debug
| Key | Action |
|-----|--------|
| TAB | Switch Astronaut/Alien |
| F5 | Activate DefenseZone manually |

## Important Implementation Details

### Raycast Hitting Child Objects
Always use `GetComponentInParent<T>()` as fallback:
```csharp
var health = hit.collider.GetComponent<AlienHealth>();
if (health == null)
    health = hit.collider.GetComponentInParent<AlienHealth>();
```

### CharacterController Setup
Both player and alien use CharacterController with:
- `center = (0, 1, 0)` for proper collision
- `height = 2f`, `radius = 0.5f`

### Late Event Subscription
When subscribing to GameManager events, always add fallback:
```csharp
if (GameManager.Instance != null) {
    GameManager.Instance.OnChaosPhase.AddListener(MyMethod);
} else {
    StartCoroutine(LateSubscribe());
}
```

### UI Updates When Switching Characters
`AlienController.OnEnable()` and `GameController.SwitchToAlien()` check for chaos mode and update UI.

## Audio Setup

AudioManager loads from `Resources/Audio/` folder:
```
Assets/Resources/Audio/
├── gunshot.wav
├── impact_flesh.wav
├── impact_metal.wav
├── ui_click.wav
├── alarm.wav
└── alien_reveal.wav
```
See `AUDIO_GUIDE.md` for full list.

## File Structure

```
Assets/Scripts/
├── Astronaut/
│   ├── PlayerMovement.cs
│   ├── PlayerShooting.cs (+ IDamageable interface)
│   ├── StressSystem.cs
│   └── AstronautHealth.cs
├── Alien/
│   ├── AlienController.cs
│   ├── AlienHealth.cs
│   ├── AlienAbilities.cs
│   ├── AlienTransformation.cs
│   └── HungerSystem.cs
├── NPC/
│   └── NPCBehavior.cs
├── Core/
│   ├── GameManager.cs
│   ├── GameController.cs (TAB switch)
│   ├── GameBootstrap.cs (auto-init)
│   ├── AudioManager.cs
│   ├── CameraShake.cs
│   ├── PostProcessController.cs
│   ├── ChaosLightingController.cs
│   └── DefenseZone.cs
└── UI/
    ├── GameUIManager.cs
    ├── MenuManager.cs
    └── SimpleCrosshair.cs
```

## Development Progress

Track in `DEV_TRACKER.md`:
- **Phase 1**: Solo Astronaut FPS ✅
- **Phase 2**: Solo Alien TPS ✅
- **Phase 3**: Map & Gameplay Loop (TODO)
- **Phase 4**: Multiplayer - Netcode for GameObjects (TODO)
- **Phase 5**: Steam Integration - Steamworks.NET (TODO)

## Known Patterns

- Managers use Singleton with `DontDestroyOnLoad`
- UI uses OnGUI for quick prototyping (SimpleCrosshair, HungerSystem warnings)
- Components auto-create if missing (CharacterController, Colliders)
- Debug logs prefixed with `[ClassName]` for filtering
