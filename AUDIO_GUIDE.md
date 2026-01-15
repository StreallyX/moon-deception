# Audio Guide - Moon Deception

This file explains all audio files needed for the game, where to put them, and what type of sounds they should be.

---

## Folder Structure

Create this folder structure in your Unity project:
```
Assets/
└── Audio/
    ├── SFX/
    │   ├── Weapons/
    │   ├── Impacts/
    │   ├── Footsteps/
    │   ├── Alien/
    │   ├── Environment/
    │   └── UI/
    └── Music/
        ├── Ambient/
        └── Stingers/
```

---

## Required Audio Files

### 1. WEAPONS (`Assets/Audio/SFX/Weapons/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `gunshot_pistol.wav` | Pistol shot sound | Sharp, metallic sci-fi pistol | 0.2-0.5s |
| `gunshot_minigun.wav` | Minigun/machine gun burst | Heavy automatic weapon | 0.1-0.3s |
| `reload_pistol.wav` | Magazine reload sound | Click, slide, chamber | 1.0-1.5s |
| `empty_clip.wav` | Dry fire / no ammo click | Metallic click | 0.1s |

**Where to assign:** AudioManager Inspector → `gunshot` field

---

### 2. IMPACTS (`Assets/Audio/SFX/Impacts/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `impact_flesh.wav` | Bullet hits flesh/body | Wet, meaty thud | 0.2-0.4s |
| `impact_metal.wav` | Bullet hits metal surface | Metallic ping/ricochet | 0.2-0.4s |
| `impact_concrete.wav` | Bullet hits concrete/wall | Dull thud with debris | 0.2-0.4s |

**Where to assign:** AudioManager Inspector → `bulletImpactFlesh`, `bulletImpactMetal`, `bulletImpactConcrete`

---

### 3. FOOTSTEPS (`Assets/Audio/SFX/Footsteps/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `footstep_metal_01.wav` | Walking on metal floor | Metallic step | 0.2-0.3s |
| `footstep_metal_02.wav` | Variation 2 | Slightly different | 0.2-0.3s |
| `footstep_metal_03.wav` | Variation 3 | Slightly different | 0.2-0.3s |
| `footstep_run_01.wav` | Running on metal | Faster, heavier | 0.15-0.25s |

**Where to assign:** AudioManager Inspector → `footstep` (pick one, or use array for variety)

---

### 4. ALIEN (`Assets/Audio/SFX/Alien/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `alien_reveal.wav` | Alien transformation sound | Dramatic, monstrous growl with bass | 1.5-2.5s |
| `alien_growl.wav` | Hungry alien sound (starving) | Low growl, gurgling stomach | 1.0-2.0s |
| `alien_attack.wav` | Alien melee attack swoosh | Fast slash/claw sound | 0.3-0.5s |
| `alien_death.wav` | Alien dying | Screech fading out | 1.0-2.0s |

**Where to assign:** AudioManager Inspector → `alienReveal`, (custom clips)

---

### 5. NPC (`Assets/Audio/SFX/NPC/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `npc_death.wav` | NPC dying sound | Human scream/gasp | 0.5-1.0s |
| `npc_panic.wav` | NPC panicking | Scared yelling | 1.0-2.0s |

**Where to assign:** AudioManager Inspector → `npcDeath`

---

### 6. ENVIRONMENT (`Assets/Audio/SFX/Environment/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `alarm.wav` | Station alarm (chaos phase) | Loud siren, repeating | 2.0-5.0s (loop) |
| `power_down.wav` | Lights going off | Electrical hum dying | 1.0-2.0s |
| `emergency_light.wav` | Emergency lights activating | Click + hum | 0.5-1.0s |
| `door_open.wav` | Automatic door opening | Sci-fi whoosh | 0.5-1.0s |
| `coffee_machine.wav` | Coffee pouring | Liquid sounds | 1.0-2.0s |

**Where to assign:** AudioManager Inspector → `alarm`, custom clips

---

### 7. UI (`Assets/Audio/SFX/UI/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `ui_click.wav` | Button click | Clean click/beep | 0.1-0.2s |
| `ui_hover.wav` | Button hover | Soft tick | 0.05-0.1s |
| `ui_back.wav` | Menu back/cancel | Lower pitched click | 0.1-0.2s |
| `ui_start.wav` | Game start | Dramatic whoosh | 0.5-1.0s |

**Where to assign:** AudioManager Inspector → `uiClick`, `uiHover`, `uiBack`

---

### 8. MUSIC (`Assets/Audio/Music/`)

| File Name | Description | Type | Duration |
|-----------|-------------|------|----------|
| `ambient_station.wav` | Normal gameplay ambient | Low hum, distant machinery, quiet | 60s+ (loop) |
| `ambient_tension.wav` | Rising stress ambient | Heartbeat, distortion | 60s+ (loop) |
| `music_chaos.wav` | Chaos phase music | Intense, aggressive | 60s+ (loop) |
| `stinger_alien_kill.wav` | Alien killed stinger | Short victory sting | 2-3s |
| `stinger_game_over.wav` | Game over | Dramatic ending | 3-5s |

**Where to assign:** AudioManager Inspector → `musicNormal`, `musicChaos`

---

## AudioManager Setup in Unity

1. Select the **AudioManager** GameObject in your scene
2. In the Inspector, you'll see fields for each audio clip
3. Drag your audio files from the Project window to the corresponding fields:

```
AudioManager (Script)
├── Gunshot: gunshot_pistol.wav
├── Footstep: footstep_metal_01.wav
├── Bullet Impact Metal: impact_metal.wav
├── Bullet Impact Flesh: impact_flesh.wav
├── Bullet Impact Concrete: impact_concrete.wav
├── NPC Death: npc_death.wav
├── Alien Reveal: alien_reveal.wav
├── Alarm: alarm.wav
├── UI Click: ui_click.wav
├── UI Hover: ui_hover.wav
├── UI Back: ui_back.wav
├── Music Normal: ambient_station.wav
└── Music Chaos: music_chaos.wav
```

---

## Import Settings Recommendations

For all audio files, use these Unity import settings:

### SFX (short sounds)
- **Load Type:** Decompress On Load
- **Compression Format:** PCM (for short clips) or Vorbis (for longer)
- **Sample Rate:** Original or 44100 Hz

### Music (long loops)
- **Load Type:** Streaming
- **Compression Format:** Vorbis
- **Quality:** 70-100%

---

## Free Sound Resources

You can find free game audio at:
- [Freesound.org](https://freesound.org) - Creative Commons sounds
- [OpenGameArt.org](https://opengameart.org) - Game-specific assets
- [Pixabay](https://pixabay.com/sound-effects/) - Royalty-free
- [Zapsplat](https://www.zapsplat.com) - Free with attribution

**Keywords to search:**
- Weapons: "sci-fi pistol", "laser gun", "futuristic weapon"
- Impacts: "bullet impact", "flesh hit", "metal ricochet"
- Alien: "monster growl", "creature roar", "alien sound"
- Ambient: "space station ambience", "sci-fi hum"

---

## Quick Test Without Audio

The game will work without audio files - AudioManager has null checks. You'll see warnings in the console like:
```
[AudioManager] Gunshot clip not assigned!
```

This is normal during development until you add audio files.
