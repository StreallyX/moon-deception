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
| `MapManager` | Zone management, spawn point validation |
| `SpawnManager` | Random spawning with distance rules |

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
- Creates `MapManager`, `SpawnManager` singletons

### Map & Zone System
- `MapZone.cs` - Define zones with BoxCollider boundaries
- Each zone has: npcSpawnPoints[], defenseZoneSpawnPoints[], interactableSpawnPoints[], patrolWaypoints[]
- `MapManager.Instance.GetValidDefenseZoneSpawnPoints(astronautPos)` - respects min distance

### Interactables
Base class: `Interactable.cs` - proximity detection, cooldowns, role filtering
- `CoffeeMachine.cs` - Alien-only, INCREASES hunger decay (stacks!), 10s cooldown
  - Each coffee adds +0.5x to decay multiplier (max 5x)
  - Stacks expire after 15s each
  - Strategic risk: blend in by drinking coffee, but starve faster
- `AlarmTerminal.cs` - Alien-only, +10 stress (if astronaut in 30m), panics NPCs

### Spawn System
`SpawnManager.cs` called by `GameManager.StartGame()`:
- `AssignAliensToNPCs(count)` - Fisher-Yates shuffle, random assignment
- Defense zones spawn min 20m from astronaut
- Interactables spawn per zone (2 coffee, 1 alarm per zone)

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

AudioManager auto-loads from `Resources/Audio/` folder (22 files):
```
Assets/Resources/Audio/
├── gunshot.wav, minigun.wav, reload.wav     # Weapons
├── impact_metal.wav, impact_concrete.wav    # Impacts
├── npc_death.wav, npc_panic.wav             # NPCs
├── alien_reveal.wav, alien_growl.wav        # Alien
├── alien_attack.wav, alien_killed.wav
├── ambient_station.wav, ambient_chaos.wav   # Ambient loops
├── alarm.wav, heartbeat.wav
├── power_down.wav, lights_emergency.wav     # Chaos events
├── victory_stinger.wav, defeat_stinger.wav  # Game end
├── coffee_machine.wav, alarm_trigger.wav    # Interactables
└── terminal_beep.wav
```

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
├── Map/
│   └── MapZone.cs
├── Interactables/
│   ├── Interactable.cs (base class)
│   ├── CoffeeMachine.cs
│   └── AlarmTerminal.cs
├── Core/
│   ├── GameManager.cs
│   ├── GameController.cs (TAB switch)
│   ├── GameBootstrap.cs (auto-init)
│   ├── MapManager.cs
│   ├── SpawnManager.cs
│   ├── AudioManager.cs
│   ├── CameraShake.cs
│   ├── PostProcessController.cs
│   ├── ChaosLightingController.cs
│   └── DefenseZone.cs
├── Network/
│   ├── NetworkManagerSetup.cs
│   └── NetworkConnectionUI.cs
└── UI/
    ├── GameUIManager.cs
    ├── MenuManager.cs
    └── SimpleCrosshair.cs
```

## Development Progress

Track in `DEV_TRACKER.md`:
- **Phase 1**: Solo Astronaut FPS ✅
- **Phase 2**: Solo Alien TPS ✅
- **Phase 3**: Map & Gameplay Loop ✅
- **Phase 4**: Multiplayer - Netcode for GameObjects (IN PROGRESS)
  - 4.1: Setup & Base - Scripts ready, needs package install
  - 4.2-4.6: TODO
- **Phase 5**: Steam Integration - Steamworks.NET (TODO)

## Network System (Phase 4)

### Setup
Package: `com.unity.netcode.gameobjects`

### Debug Keys
| Key | Action |
|-----|--------|
| H | Host game (quick) |
| J | Join game (quick) |
| F1 | Toggle connection UI |

### Network Scripts
- `NetworkManagerSetup.cs` - Configures NetworkManager and transport
- `NetworkConnectionUI.cs` - OnGUI for Host/Join, IP input

## Known Patterns

- Managers use Singleton with `DontDestroyOnLoad`
- UI uses OnGUI for quick prototyping (SimpleCrosshair, HungerSystem warnings)
- Components auto-create if missing (CharacterController, Colliders)
- Debug logs prefixed with `[ClassName]` for filtering
