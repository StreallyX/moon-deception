# Moon Deception - Development Tracker

## Current Phase: 4.1 - Multiplayer Setup - IN PROGRESS
## Current Branch: phase1-fps
## Status: Waiting for user to install Netcode package

---

## Project Vision
- Asymmetric multiplayer survival game on Steam
- 1 Astronaut (FPS) vs up to 5 Aliens (TPS) among ~30 NPCs
- Quality target: Steam-ready aesthetic (no placeholder feel)

---

## Phase 1 Checklist: Solo Astronaut

### Core Systems
- [x] PlayerMovement.cs - FPS controls (WASD, mouse look, jump)
- [x] PlayerShooting.cs - Raycast shooting with debug visuals
- [x] NPCBehavior.cs - AI with states (Idle/Walking/Working/Panicking/Dead)
- [x] GameUIManager.cs - Dynamic UI for Astronaut/Alien
- [x] StressSystem.cs - Stress bar (kill innocent +stress, kill alien -stress, passive recovery)
- [x] GameManager.cs - Game phases (Lobby/Starting/Playing/Chaos/Ended), win conditions
- [x] SimpleCrosshair.cs - Basic crosshair UI (OnGUI)
- [x] GameController.cs - Switch Player/Alien with TAB (for testing)

### Audio (CODE DONE - needs audio files)
- [x] AudioManager.cs - Central audio system with pooling
- [x] Gunshot sounds - Integrated in PlayerShooting
- [x] Footstep sounds - Integrated in PlayerMovement
- [x] Impact sounds - Flesh/Metal/Concrete
- [x] UI sounds - Click, Hover, Back
- [ ] **NEED**: Actual .wav/.mp3 audio files

### Visual Polish (DONE)
- [x] Muzzle flash particles - Auto-created
- [x] Hit markers on screen - X pattern, red for kills
- [x] Blood splatter particles - Auto-created
- [x] Bullet impact sparks - Auto-created
- [x] Camera shake on shoot - CameraShake.cs
- [x] Screen vignette when stressed - PostProcessController.cs
- [x] Post-processing (URP) - Bloom, vignette, chromatic aberration, film grain

### UI Polish - Steam Quality (DONE)
- [x] Main Menu - MOON DECEPTION title, Play, Settings, Quit
- [x] Pause Menu - Resume, Settings, Main Menu
- [x] Win/Lose Screen - Stats (time, kills), Play Again, Main Menu
- [x] Settings Menu - Master/SFX/Music volume, Mouse sensitivity

### Gameplay Feel (PARTIAL)
- [x] Camera shake on shoot
- [ ] Weapon recoil animation (visual gun model needed)
- [ ] NPC ragdoll on death (Rigidbody setup needed)
- [x] Dynamic post-process effects based on stress

---

## Phase 2 Checklist: Solo Alien (TPS) - COMPLETE
- [x] AlienController.cs - TPS movement with orbital camera
- [x] HungerSystem.cs - Hunger decay, coffee boost, eat to restore
- [x] AlienAbilities.cs - 4 chaos powers (collision, glitch, sound, wind) with cooldowns
- [x] AlienTransformation.cs - Transform during chaos, speed boost, attack ability
- [x] Wall-hack vision - See astronaut through walls when transformed
- [x] ChaosLightingController.cs - Lights off, emergency red lights, alien night vision
- [x] DefenseZone.cs - Zone where astronaut gets machine gun upgrade
- [x] AstronautHealth.cs - Health system for astronaut (alien attacks work)

---

## Phase 3 Checklist: Map & Gameplay Loop - COMPLETE
- [x] MapZone.cs - Zone definition with boundaries, spawn points, patrol waypoints
- [x] MapManager.cs - Central zone management, distance-based spawn validation
- [x] SpawnManager.cs - Random alien assignment, defense zone spawning with rules
- [x] Interactable.cs - Base class for all interactables
- [x] CoffeeMachine.cs - Alien drinks to blend in, but INCREASES hunger decay (stacks!)
- [x] AlarmTerminal.cs - Alien uses to stress astronaut (+10, panic NPCs)
- [x] WinConditions - In GameManager (astronaut wins / aliens win)
- [x] GameTimer - In GameManager (10 min default)
- [x] 4 Map sections setup in Unity (Habitat, Research, Industrial, Command)
- [x] Scene setup with zones and spawn points
- [x] Audio integration - 22 audio files connected to all game systems

---

## Phase 4 Checklist: Multiplayer (Netcode for GameObjects)

### Phase 4.1: Setup & Base - TODO
- [ ] Install Netcode for GameObjects package
- [ ] Create NetworkManager prefab
- [ ] Create basic connection UI (Host/Join)
- [ ] Test: 2 Unity instances can connect

### Phase 4.2: Player Sync - TODO
- [ ] NetworkObject on Player & Alien prefabs
- [ ] Sync position/rotation (ClientNetworkTransform)
- [ ] Sync shooting (ServerRpc)
- [ ] Sync animations
- [ ] Test: See other player move

### Phase 4.3: NPC Sync - TODO
- [ ] NPCs spawned by server only
- [ ] NPC state synced (NetworkVariable)
- [ ] NPC damage synced (ServerRpc)
- [ ] Test: Both players see same NPCs

### Phase 4.4: Game State Sync - TODO
- [ ] GameManager → NetworkBehaviour
- [ ] Sync game phase (NetworkVariable)
- [ ] Sync timers, stress, scores
- [ ] Sync win/lose conditions
- [ ] Test: Chaos triggers for everyone

### Phase 4.5: Role Assignment - TODO
- [ ] Server assigns roles on connect
- [ ] 1 Astronaut (first player or random)
- [ ] Others = Aliens among NPCs
- [ ] Spawn at correct positions
- [ ] Test: 2+ players get different roles

### Phase 4.6: Lobby System - TODO
- [ ] Lobby UI (Create/Join/Ready)
- [ ] Player list display
- [ ] Ready check before start
- [ ] Disconnect handling
- [ ] Test: Full game flow with 2+ players

---

## Phase 5 Checklist: Steam Integration
- [ ] Steamworks.NET SDK
- [ ] Steam Lobby - Create/Join
- [ ] Steam Invites - Friend invitations
- [ ] Steam Relay - P2P connection
- [ ] Steam Achievements (optional)

---

## Session Notes

### Session 1 (2026-01-15)
- Full codebase review completed
- Phase 1 CORE is 100% done
- Phase 1 POLISH needed: Audio, VFX, Menus, Post-processing
- Created: AudioManager, CameraShake, PostProcessController
- Created: MenuManager (Main Menu, Pause, Settings, Game Over)
- Updated: PlayerShooting (muzzle flash, hit markers, sounds)
- Updated: PlayerMovement (footsteps)
- Updated: GameManager (game over screen integration)
- Created: GameBootstrap (auto-initializes all systems)
- **Phase 1 CODE COMPLETE** - needs audio files and testing

### Session 2 (2026-01-15)
- Created AlienAbilities.cs - 4 chaos powers (1,2,3,4 keys)
- Created AlienTransformation.cs - Transform + wall-hack + attack
- Created ChaosLightingController.cs - Blackout + emergency lights
- Created DefenseZone.cs - Weapon upgrade zone for astronaut
- Created AstronautHealth.cs - Health system for astronaut
- Updated GameBootstrap.cs - Auto-creates all new systems
- **Phase 2 CODE COMPLETE** - ready for testing

### Session 3 (2026-01-15)
- Fixed CameraShake inactive camera error (added activeInHierarchy check)
- Fixed DefenseZone collider error (auto-creates BoxCollider)
- Created AlienHealth.cs - Alien takes damage and loses HP
- Updated PlayerShooting.cs - Hitting alien reduces stress by 10
- Rewrote AlienTransformation.cs - Better visuals, fluorescent wall-hack with direction line
- Rewrote HungerSystem.cs - Hunger=0 reveals alien (glitch effects, sounds) instead of death
- Alien attack now damages astronaut via AstronautHealth
- Created CLAUDE.md for future Claude Code sessions
- **All Phase 2 bugs fixed** - ready for testing

### Session 4 (2026-01-15)
- **Fixed alien not taking damage**: Raycast was hitting child mesh, added GetComponentInParent<>() fallback in PlayerShooting.cs
- **Added magazine reload system**: 8 rounds per magazine, 1.5s reload time, visual reload indicator
- **Added minigun upgrade**: DefenseZone gives infinite ammo, no reload, faster fire rate
- **Fixed chaos mode UI**:
  - HungerSystem OnGUI now checks `if (!enabled) return;` to properly hide
  - AlienTransformation now disables HungerSystem and calls SetChaosMode(true)
  - HP boost from 100 to 200 during chaos phase
- **Fixed chaos phase not triggering**:
  - GameManager now subscribes to StressSystem.OnStressMaxed AFTER StartGame()
  - AlienTransformation uses LateSubscribe() coroutine for timing issues
- **Fixed health bar not updating**: AlienHealth.UpdateUI() now always updates regardless of control state
- **Fixed CharacterController collision**: Set center to (0,1,0) for proper raycast detection
- **Added auto-loading audio**: AudioManager.LoadAudioFromResources() loads from Assets/Resources/Audio/
- Created AUDIO_GUIDE.md with sound requirements and folder structure
- Updated CLAUDE.md with comprehensive project documentation
- **All bugs fixed and verified working**

### Session 5 (2026-01-15) - PHASE 3 START
- **Created Map System**:
  - MapZone.cs - Zone definition with boundaries (BoxCollider), spawn points arrays, patrol waypoints
  - MapManager.cs - Singleton managing all zones, distance-based spawn validation
- **Created Spawn System**:
  - SpawnManager.cs - Random alien assignment (Fisher-Yates), defense zone spawning with min distance rules (20m from astronaut)
- **Created Interactables**:
  - Interactable.cs - Base class with proximity detection, cooldowns, role filtering (alien/astronaut)
  - CoffeeMachine.cs - Alien-only, +40 hunger, 10s cooldown, visual feedback
  - AlarmTerminal.cs - Alien-only, +10 stress to astronaut in range, panics NPCs, 30s cooldown
- **Updated Existing Systems**:
  - GameBootstrap.cs - Auto-creates MapManager and SpawnManager
  - GameManager.cs - Integrates SpawnManager.SpawnAllEntities() in StartGame()
  - NPCBehavior.cs - Added zone assignment and zone-based patrol
- **Phase 3 CODE COMPLETE** - ready for Unity scene setup

### Session 6 (2026-01-16) - PHASE 3 COMPLETE
- **Audio Integration** (22 files):
  - Connected all audio files to game systems
  - AlienTransformation.TryAttack() now uses PlayAlienAttack()
  - AlienHealth.Die() now uses PlayAlienKilled()
  - NPCBehavior.Panic() now uses PlayNPCPanic()
  - NPCBehavior.Die() now uses PlayNPCDeath()
- **Coffee Mechanic Rework**:
  - Coffee restores +25 hunger immediately
  - BUT increases hunger decay rate (stacks!)
  - Each coffee adds +0.5x to decay multiplier (max 5x)
  - Stacks expire after 15s each
  - UI shows coffee stacks and decay multiplier
  - Double-edged sword: gain now, lose faster later
- **Unity Scene Setup** - User completed:
  - 4 zones created with MapZone component and BoxCollider
  - Spawn points created in each zone
  - All interactables tested and working
- **NPC Bug Fix**:
  - Fixed NPCs sinking into ground
  - Added Y-position lock in ApplyGravity()
- **PHASE 3 COMPLETE** - Ready for Phase 4 Multiplayer

### Session 7 (2026-01-16) - PHASE 4 START
- **Phase 4.1: Multiplayer Setup** - IN PROGRESS
  - Created NetworkManagerSetup.cs - Configures NetworkManager
  - Created NetworkConnectionUI.cs - Host/Join UI (H/J keys, F1 toggle)
  - Updated GameBootstrap.cs - Auto-creates NetworkManager
  - **WAITING**: User needs to install Netcode for GameObjects package
- **Next Steps**:
  1. User installs package: com.unity.netcode.gameobjects
  2. Test Host/Join with 2 Unity instances
  3. Then Phase 4.2: Player Sync

---

## Known Issues
- SimpleCrosshair uses OnGUI (old system) - consider switching to UI Canvas
- ~~GameUIManager health bars created but UpdateAstronautHealthBar not connected~~ **FIXED in Session 4**

---

## Files Structure (Complete)
```
Assets/Scripts/
├── Astronaut/
│   ├── PlayerMovement.cs    ✅ + Footsteps
│   ├── PlayerShooting.cs    ✅ + Muzzle flash, hit markers, sounds
│   ├── StressSystem.cs      ✅ Complete
│   └── AstronautHealth.cs   ✅ Health + damage
├── Alien/
│   ├── AlienController.cs   ✅ Complete
│   ├── HungerSystem.cs      ✅ Hunger=0 reveals alien
│   ├── AlienAbilities.cs    ✅ 4 chaos powers (1,2,3,4)
│   ├── AlienTransformation.cs ✅ Transform + wall-hack + attack
│   ├── AlienHealth.cs       ✅ Alien HP system
│   └── AlienEatSystem.cs    ⚠️ Exists (not reviewed)
├── NPC/
│   ├── NPCBehavior.cs       ✅ + Zone assignment
│   └── NPCInteraction.cs    ⚠️ Exists (not reviewed)
├── Map/
│   └── MapZone.cs           ✅ NEW - Zone boundaries, spawn points
├── Interactables/
│   ├── Interactable.cs      ✅ NEW - Base class for interactables
│   ├── CoffeeMachine.cs     ✅ NEW - Alien hunger restore
│   └── AlarmTerminal.cs     ✅ NEW - Stress astronaut
├── Core/
│   ├── GameManager.cs       ✅ + SpawnManager integration
│   ├── GameController.cs    ✅ TAB switch
│   ├── GameBootstrap.cs     ✅ + NetworkManager auto-create
│   ├── MapManager.cs        ✅ Zone management
│   ├── SpawnManager.cs      ✅ Spawning with rules
│   ├── AudioManager.cs      ✅ Central audio with pooling
│   ├── CameraShake.cs       ✅ Shoot/impact/stress shake
│   ├── PostProcessController.cs ✅ Dynamic URP effects
│   ├── ChaosLightingController.cs ✅ Blackout + emergency lights
│   └── DefenseZone.cs       ✅ Weapon upgrade zones
├── Network/
│   ├── NetworkManagerSetup.cs  ✅ NEW - NetworkManager config
│   └── NetworkConnectionUI.cs  ✅ NEW - Host/Join UI (H/J/F1)
├── UI/
│   ├── GameUIManager.cs     ✅ HUD bars
│   ├── MenuManager.cs       ✅ All menus (Main, Pause, Settings, GameOver)
│   ├── SimpleCrosshair.cs   ✅ Basic crosshair
│   └── EatPromptUI.cs       ⚠️ Exists (not reviewed)
└── Utils/
    └── TargetHighlight.cs   ⚠️ Exists (not reviewed)
```
