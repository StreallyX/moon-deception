# 🌙 Moon Deception

**Asymmetric Multiplayer Survival Game for Steam** — Built with Unity

> One Astronaut. Five Aliens. Thirty NPCs. One Moon. Trust no one.

---

## 🎮 Game Concept

Moon Deception is an asymmetric multiplayer game set on a lunar base divided into **4 interconnected sections** populated by ~30 human NPCs.

- **1 Astronaut** (FPS) must identify and eliminate alien infiltrators hiding among the NPCs
- **Up to 5 Aliens** (TPS) must blend in, survive, and hunt the astronaut

---

## 👨‍🚀 Astronaut (FPS Perspective)

### Gameplay
- First-person shooter controls with pistol
- Must observe NPC behavior to identify aliens
- Can kill NPCs but at a cost...

### Stress System 📊
| Action | Effect |
|--------|--------|
| Kill innocent NPC | **+Stress** (panic increases) |
| Kill alien | **-Stress** (relief) |
| Witness chaos events | **+Stress** |
| Stay calm/idle | **-Stress** (slow recovery) |

### ⚠️ Critical Event: Stress Overload
When stress bar maxes out:
1. **Alarm triggers** across the station
2. **Lights go out**
3. **Aliens transform** and attack openly
4. Astronaut must reach a **strategic defense point** with machine gun to survive

---

## 👽 Aliens (TPS Perspective)

### Gameplay
- Third-person view while disguised as human NPC
- Must blend in with NPC behavior patterns
- Can attack when opportunity arises

### Hunger System 🍖
| Action | Effect |
|--------|--------|
| Do nothing | Hunger slowly increases |
| Drink coffee ☕ | Partially satisfies hunger BUT accelerates hunger decay |
| Eat astronaut 🩸 | Full satisfaction BUT leaves blood trace evidence |

### Chaos Abilities
Aliens can create disturbances to raise astronaut stress:
- **Collisions** — Bump into NPCs/objects
- **Bugs** — Trigger visual glitches
- **Sounds** — Create suspicious noises
- **Wind** — Disturb environment

---

## 🗺️ Map Structure

```
┌─────────────┬─────────────┐
│  Section A  │  Section B  │
│  (Habitat)  │  (Research) │
├─────────────┼─────────────┤
│  Section C  │  Section D  │
│  (Industrial)│  (Command) │
└─────────────┴─────────────┘
```

Each section contains:
- ~7-8 NPCs performing routine tasks
- Interactive objects (coffee machines, terminals, etc.)
- Strategic defense points

---

## 🏆 Win Conditions

| Player | Victory Condition |
|--------|------------------|
| **Astronaut** | Eliminate all aliens |
| **Aliens** | Kill the astronaut OR survive until timer expires |

---

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── Astronaut/
│   │   ├── PlayerMovement.cs      # FPS movement controls
│   │   ├── PlayerShooting.cs      # Raycast shooting system
│   │   └── StressSystem.cs        # Stress bar management
│   ├── Alien/
│   │   ├── AlienController.cs     # TPS movement & disguise
│   │   └── HungerSystem.cs        # Hunger mechanics
│   ├── NPC/
│   │   └── NPCBehavior.cs         # AI behavior patterns
│   ├── Core/
│   │   ├── GameManager.cs         # Game state & phases
│   │   └── NetworkManager.cs      # Multiplayer (placeholder)
│   └── Net/
├── Scenes/
├── Resources/
├── Art/
├── Audio/
└── UI/
```

---

## 🚀 Development Phases

- [x] **Phase 1**: Astronaut FPS foundations
- [ ] **Phase 2**: Alien TPS & NPC systems
- [ ] **Phase 3**: Multiplayer with Netcode for GameObjects
- [ ] **Phase 4**: Steam integration & polish

---

## 🔧 Quick Setup

1. Open project in Unity 2022.3+ (URP)
2. Open `Assets/Scenes/SampleScene.unity`
3. Menu: **Moon Deception > Setup Phase 1**
4. Press Play

See [SETUP_GUIDE.md](SETUP_GUIDE.md) for detailed instructions.

---

## 🎯 Controls

### Astronaut (FPS)
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| Left Click | Shoot |

### Alien (TPS) — *Coming Phase 2*
| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Camera |
| E | Interact/Disguise |
| Q | Use ability |

---

## 📝 License

© 2026 StreallyX. All rights reserved.
