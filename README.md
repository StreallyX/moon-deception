# 🌙 Moon Deception

🎮 Asymmetrical multiplayer FPS/TPS game made with Unity — 1 Astronaut vs up to 5 disguised Aliens among 30 NPCs.

## Phase 1: Astronaute FPS - Fondations

### Scripts créés

#### PlayerMovement.cs
Contrôle FPS complet de l'astronaute:
- **WASD** : Déplacement (avant/arrière/gauche/droite)
- **Souris** : Rotation caméra (look around)
- **Space** : Saut avec gravité réaliste
- Nécessite un `CharacterController` sur le GameObject

#### PlayerShooting.cs
Système de tir raycast:
- **Clic gauche** : Tir
- Raycast depuis le centre de l'écran
- Debug visuel (rayons colorés dans Scene view)
- Interface `IDamageable` pour les cibles

### Setup Unity

**Dans Unity, cliquez sur Moon Deception > Setup Phase 1 pour configurer la scène automatiquement.**

Ou manuellement:

1. Créer un GameObject "Player" avec:
   - `CharacterController` component
   - `PlayerMovement` script
   - `PlayerShooting` script

2. Attacher la caméra principale comme enfant du Player

3. Configurer les layers pour `hitLayers` dans PlayerShooting

### Prochaines phases
- Phase 2: Système d'aliens et NPCs
- Phase 3: Multijoueur avec Netcode
- Phase 4: Intégration Steam
