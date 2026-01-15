# Moon Deception - Development Tracker

## Current Phase: 1 - Solo Astronaut (FPS)
## Current Branch: phase1-fps

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

## Phase 2 Checklist: Solo Alien (TPS)
- [x] AlienController.cs - TPS movement with orbital camera
- [x] HungerSystem.cs - Hunger decay, coffee boost, eat to restore
- [ ] AlienAbilities.cs - Chaos powers (collision, bugs, sounds, wind)
- [ ] AlienDisguise.cs - Blend with NPCs behavior
- [x] StressOverload event - Basic in GameManager (needs visual effects)

---

## Phase 3 Checklist: Map & Gameplay Loop
- [ ] 4 Map sections (Habitat, Research, Industrial, Command)
- [ ] SpawnSystem - Random alien spawn among NPCs
- [ ] DefensePoints - Strategic positions with machine gun
- [x] WinConditions - In GameManager (astronaut wins / aliens win)
- [x] GameTimer - In GameManager (10 min default)
- [ ] Interactables - Coffee machines, terminals

---

## Phase 4 Checklist: Multiplayer
- [ ] Unity Netcode for GameObjects setup
- [ ] NetworkManager - Host/Client connection
- [ ] Role assignment (1 astronaut, rest aliens)
- [ ] Synchronized movement, shooting, NPCs
- [ ] Lobby system

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

---

## Known Issues
- SimpleCrosshair uses OnGUI (old system) - consider switching to UI Canvas
- GameUIManager health bars created but UpdateAstronautHealthBar not connected

---

## Files Structure (Complete)
```
Assets/Scripts/
├── Astronaut/
│   ├── PlayerMovement.cs    ✅ + Footsteps
│   ├── PlayerShooting.cs    ✅ + Muzzle flash, hit markers, sounds
│   └── StressSystem.cs      ✅ Complete
├── Alien/
│   ├── AlienController.cs   ✅ Complete
│   ├── HungerSystem.cs      ✅ Complete
│   └── AlienEatSystem.cs    ⚠️ Exists (not reviewed)
├── NPC/
│   ├── NPCBehavior.cs       ✅ Complete
│   └── NPCInteraction.cs    ⚠️ Exists (not reviewed)
├── Core/
│   ├── GameManager.cs       ✅ + Game Over integration
│   ├── GameController.cs    ✅ TAB switch
│   ├── GameBootstrap.cs     ✅ NEW - Auto-init all systems
│   ├── AudioManager.cs      ✅ NEW - Central audio with pooling
│   ├── CameraShake.cs       ✅ NEW - Shoot/impact/stress shake
│   ├── PostProcessController.cs ✅ NEW - Dynamic URP effects
│   └── NetworkManager.cs    📋 Phase 4
├── UI/
│   ├── GameUIManager.cs     ✅ HUD bars
│   ├── MenuManager.cs       ✅ NEW - All menus (Main, Pause, Settings, GameOver)
│   ├── SimpleCrosshair.cs   ✅ Basic crosshair
│   └── EatPromptUI.cs       ⚠️ Exists (not reviewed)
└── Utils/
    └── TargetHighlight.cs   ⚠️ Exists (not reviewed)
```
