# Category 5 — Project Reference

> **Purpose**: This file gives any agent the essential context to work efficiently on
> Category 5 without re-researching the game each session. Read this first.

---

## Game at a Glance

**Category 5** is a third-person **cooperative (1–5 player) PVE action platformer**.
A team of stormchasers rides wind tunnels between floating island arenas ("storm eyes"),
fights elemental enemies, collects tiered items, and defeats boss storms across multiple
rounds. MOBA-style class combat + wind-riding traversal are equally core.

| Field | Value |
|-------|-------|
| **Genre** | Third-Person Co-op PVE Action Platformer |
| **View** | Third-person, over-the-shoulder |
| **Team Size** | 1–5 players (co-op) |
| **Session Length** | 20–45 min per storm (3 rounds default) |
| **Engine** | Unity 6 (6000.3.0f1), URP |
| **Language** | C# |
| **Networking** | Unity Netcode for GameObjects (NGO) |
| **Audio** | Wwise (AK.Wwise) |
| **Input** | Keyboard/Mouse + Gamepad (Input System / `InputSystem_Actions`) |
| **Art Style** | High-contrast, vivid colors, ink-shadow aesthetic |
| **Platform** | PC (primary), Console (future) |

### Game Pillars
1. **Team Synergy** — coordinated class combos beat solo play
2. **Flow State** — always in motion; no standing still
3. **Build Variety** — classes + tiered items create meaningful builds
4. **Unpredictable Sessions** — procedural storm layouts, "one more game"
5. **Visual Impact** — high-contrast, thrilling, not peaceful

### Anti-Pillars
- NOT competitive PVP (co-op PVE only)
- NOT stand-and-aim combat (movement/platforming core)
- NOT slow/methodical pacing (fast and action-packed)

---

## Core Loop & Session Flow

```
Homebase (Van) ──glide──▶ Procedural Map (arenas + wind tunnels)
   │                              │
   │              ┌───────────────┘
   │              ▼
   │     Player triggers arena → EnemySpawner activates (waves)
   │              │
   │              ▼  all spawners cleared
   │     Boss entrance (delay) → Boss fight
   │              │
   │              ▼  boss dies
   │     Item selection (synchronous, all players, blocks round)
   │              │
   │              ▼  all selections done
   │     Next round: new map generated, spawners reset, dead revived
   │              │
   │              ▼  final round boss dies
   └────────── Victory ◀──────────┘
```

**Round structure** (managed by `GameFlowManager`):
- 3 rounds default (`totalRounds`), enemy count scales `[1.0x, 1.5x, 2.0x]` per round
- Boss HP scales with round progress (AnimationCurve) **and** player count (`playerCountMultipliers`)
- `GamePhase` enum: `Fighting → PowerUpSelection → (next round Fighting) | Victory | GameOver`
- All players dead → `GameOver`. Players respawn at round transitions.

**Two item-drop paths:**
- **Boss drop** (synchronous): all players get selection UI, round **blocks** until all done
- **Island drop** (async): `ItemDrop` prefab spawns at cleared spawner, player collides → individual selection, **no round block**, can skip, 60s timeout, one per spawner per round

---

## Systems Map

Every system → code location, key types, and essential facts.

### Foundation

#### Networking
- **Location**: `Assets/Scripts/Core/` (`NetworkManagerBootstrap`, `NetworkSessionManager`, `RelayHelper`, `LobbyManager`, `LobbyChatManager`, `PlayerSpawnPoint`, `PlayerNameManager`)
- **Transport**: UnityTransport (NGO)
- **Authority model**:
  - **Server-authoritative**: damage, deaths, spawns, item selection, game phase, round, map seed
  - **Owner-authoritative**: movement, abilities, wind riding (synced via `OwnerPlayerNetworkAnimator`)
- **Networked state** (`NetworkVariable<T>`): `CurrentHealth`, `CurrentMana`, `IsDead`, ability cooldowns, `enchanterCharges`, `CurrentPhase`, `CurrentRound`, map `Seed`, `PlayerName`
- **Scenes**: `MainMenu` → `Homebase` (hub) → game scene (`DebugMap` is active dev scene; `SampleScene` is legacy default)
- **Singletons**: `GameFlowManager.Instance`, `ItemManager.Instance`, `ItemRegistry.Instance`, `ClassRegistry.Instance`, `UIManager.Instance`, `HomebaseManager.Instance`
- **Gaps**: no lag compensation, no rollback, no matchmaker, reconnect unclear, spectator basic

#### Data-Driven Design
- All gameplay content is **ScriptableObject-driven**: `PlayerClass`, `EnemyData`, `EnemyAttackData`, `BossData`, `BossAttackData`, `AbilityData`, `ItemData`, `SoundData`
- Designers create/edit content via `CreateAssetMenu` without touching code
- Gameplay values must remain data-driven (external config), never hardcoded

#### Stat Access Pattern
- `PlayerStats` owns all stat reads; `PlayerStats.CalculateDamage(coefficient)` is the single damage path
- `IDamageable` interface implemented by `PlayerController`, `EnemyBase`, `BossBase`

### Core

#### Player System — `Assets/Scripts/Player/`
- `PlayerController` (NetworkBehaviour, CharacterController): health/mana/death, movement (7 u/s, sprint 1.5x, jump, dodge 2s cd), cloud-surface detection, external velocity (launches/boosts)
- `PlayerStats`: LoL-style armor `dmg * 100/(100+armor)`, crit (5% base, 1.5x), item bonuses, dynamic bonuses, temp multipliers
- `PlayerClass` (SO): `classId`, base stats, ability prefabs (Q/E/R), `combatClass` (Melee/Ranged)
- `PlayerClassManager`: loads class data from `ClassRegistry`
- `PlayerCombat`: basic attacks, melee combos (light 0.8x / heavy 1.5x), ranged projectiles
- `PlayerModelManager`, `OwnerPlayerNetworkAnimator`, `PlayerAnimationEventRelay`
- **Damage formula**: `CalculateDamage(coefficient)` = `AttackDamage × coefficient × DamageMultiplier + FlatDamageBonus`, crit roll, min 1 damage
- **Gaps**: no death consequences/respawn rewards in-player, no leveling, no CC system

#### Classes (5 implemented) — `Assets/Scripts/Player/Abilities/{ClassName}/`

| Class | Role | Combat | Q | E | R (ult) | Special |
|-------|------|--------|---|---|---------|---------|
| **Ranger** | ADC/DPS | Ranged | Projectile | Arrow + Zone | Multi-arrow skillshot | — |
| **Fighter** | Tank/Bruiser | Melee | Hook (pull) | Taunt Aura | Damage buff | Taunt redirects enemies |
| **Enchanter** | Healer/Support | Ranged | Heal beacon | Heal projectile | Area heal | **Charge system**: 5 charges, 15s decay |
| **Elementalist** | Mage/Burst | Ranged | Projectile | Fire/Ice/Thunder dispatcher | Black hole + burn | E cycles 3 variants |
| **Assassin** | Burst/Mobility | Melee | Dash + dmg | Spin AOE | Crit buff | — |

- **Abilities** (`AbilityBase` abstract): `Initialize → CanUse → Execute → ConsumeCost → StartCooldown`
- `AbilityData` (SO): `cooldownDuration`, `damageCoefficient`, `castTime`, `manaCost`, `consumesAllMana` (ult = full mana), `vfxPrefab`, `sfxClip`
- `PlayerAbilityManager`: Q/E/R slots, networked cooldowns (`NetworkVariable<float>`), mana costs
- **Damage**: `playerStats.CalculateDamage(abilityData.damageCoefficient)` — coefficient-based, class-agnostic

### Feature

#### Wind Riding / Traversal — `Assets/Scripts/Player/WindRiding/`
- `WindRiderController` (MonoBehaviour, **not** NetworkBehaviour — syncs via `IsWindRiding` animator param, owner-authoritative)
- **Riding modes**: `None, Tunnel, Cloud, Gliding`
  - **Tunnel**: Unity Spline paths, W/S lean (0.7x–1.3x speed), A/D sway (3.5m max), 28 m/s base, `WindLaunchPad` entry
  - **Cloud**: surfing on `CloudSurface` layer (8), steering via lateral velocity
  - **Gliding**: air control from van exit / launch pads (boost + upward force)
- `WindTunnel` (wraps Spline), `WindRideSettings` (tunable), `WindDraftZone`/`WindDraftAccumulator` (drafting speed boosts), `WindTunnelVisualizer`
- `Glider.cs` is a **stub** (empty) — gliding logic lives in `WindRiderController`

#### Item System — `Assets/Scripts/Items/`
- `ItemData` (SO): `effects[]` (stat modifiers), optional `behaviourPrefab`, tiers T1–T5
- **Tier scaling**: `value × (1 + 0.25 × (tier−1))` → T1=100%, T2=125%, T3=150%, T4=175%, T5=200%. MaxTier=5.
- `ItemRegistry` (singleton): holds all items, `GetRandomItems(count, inventory)` for selection
- `PlayerInventory` (networked): 5 slots, duplicate → tier upgrade, `IsFull`, `ReplaceItem`, `UpgradeItemTier`
- `ItemManager` (singleton): selection flow — `StartItemSelection()` (boss, sync) / `StartItemSelectionForPlayer(clientId)` (island, async)
- `ItemDrop` (NetworkBehaviour): spawned at cleared spawner, trigger collider, 60s timeout, `SpawnerCollectionTracker` (one per spawner per round)
- `ItemBehaviour` (abstract) + 12 behaviours in `Behaviours/`: WeatherBalloon, VantagePoint, StrongSupplements, StormSuppressor, SpiritualWell, SecretSensation, RechargingShield, ReapersQuota, MarkOfTheAlpha, KineticCleats, ForcefulImpact, BackupPlan
- **GDD**: `design/gdd/item-system.md`, quick-spec: `design/quick-design-item-drop.md`

#### Enemy System — `Assets/Scripts/Enemies/` (no GDD yet)
- `EnemyBase` (abstract, NetworkBehaviour, IDamageable): state machine `Idle→Chase→Attack→Stagger→Dead`, `NavMeshAgent` (server-only, clients use NetworkTransform), nearest-alive-player targeting
- `EnemyData` (SO): stats, `attacks[]` (weighted selection), `ElementType`, `EnemyPhysicsData`, group spawn (`minGroupSize`/`maxGroupSize`), navmesh avoidance settings, wander behavior
- `EnemyAttackData`: `damageMultiplier`, `attackRangeOverride`, `damageDelay`, `attackDuration`, `selectionWeight`
- **Concrete enemies**: `BasicEnemy` (melee, tauntable via `ICanBeTaunted`), `SwarmEnemy` (surrounds player in slots), `RangedEnemy` (keeps distance, projectiles, tauntable)
- **Combat features**: movement modifiers (slow), `ApplyStun`, `ApplyKnockback`, `ApplyLaunch` (knocks off navmesh, re-enables on land), `StartGrapple`/`StopGrapple` (Fighter Q hook), kill attribution (`OnEnemyKilledBy`)
- `EnemySpawner` (NetworkBehaviour): weighted `enemyPool`, waves (`enemiesPerWave`/`totalWaves`/`waveCooldown`), spawn bounds, trigger-activated (`TriggerVolume`), `ItemDrop` spawn on clear, `ResetSpawner` for new rounds, `ResetAllSpawners` static helper
- `EnemyVisuals`: hit flash, spawn/death VFX, color tint; `EnemyProjectile`, `EnemyHealthBar`

#### Boss System — `Assets/Scripts/Boss/` (no GDD yet)
- `BossBase` (abstract, NetworkBehaviour, IDamageable): state machine `Idle→Telegraph→Attack→Cooldown`, `BossMovementStyle` (Direct/Strafe/ChargeAndRetreat), ground check + gravity, targeting nearest alive player
- `BossData` (SO): `baseHealth`, `hpScalingCurve` (round progress), `playerCountMultipliers[]`, `availableAttacks[]`, `bossPrefab`, intro card (`introSubtitle`/`introPortrait`/`introDuration`), audio
- `BossAttackData`, `BossProjectile`, `BossVisuals`, `BossTelegraphIndicator`
- `TestBoss`: concrete implementation
- **Lifecycle**: `HideBoss` (pre-fight) → `SpawnOrRevealBoss` (scaled HP) → intro card dormant → fight → `Die` → `HideBoss` during item selection → `ResetBoss` next round
- **HP**: `GetHealthForRound(roundIndex, totalRounds, playerCount)` = `baseHealth × curve(t) × playerCountMultiplier`

#### Map Generation — `Assets/Scripts/Map/` (no GDD yet)
- `MapGenerator` (NetworkBehaviour): **procedural**, seed-synced (`NetworkVariable<int> Seed`)
- Generates: boss arena (center) + N arenas (random positions, no overlap) + paths (Splines between nearest-neighbor arenas) + cloud boundaries (walls for eyes, spheres for others) + NavMesh + wind tunnels + launch pads + enemy spawners (on non-boss arenas, trigger-activated)
- `numberOfArenas`, `numberOfEyes` (eyes = drop-in arenas), `minBounds`/`maxBounds`
- `StartRound()`: server picks seed → `DeleteMap` → `GenerateMap(seed)` → `AddEnemySpawnersToArenas`; clients generate from synced seed
- `TriggerVolume` (`Assets/Scripts/Map/TriggerVolume.cs`): generic trigger event component
- **Known issues** (noted in code): entrance reposition, path spacing, paths over arenas

#### Game Flow — `Assets/Scripts/Core/GameFlowManager.cs`
- `GameFlowManager` (NetworkBehaviour, singleton): round progression, boss lifecycle, spawner tracking, player death/respawn, victory/gameover, Wwise state switching
- Flow: `TryInitializeServerFlow` → spawners activate on trigger → `NotifySpawnerCompleted` → all complete → `OnAllWavesCleared` → `BossEntranceSequence` (delay) → `SpawnOrRevealBoss` → boss `Die` → `OnBossDied` → `ItemManager.StartItemSelection` (or `Victory` if final round) → `OnAllItemSelectionsComplete` → `StartNextRound` (`mapGenerator.StartRound`, spawners reset, `RespawnAllPlayers`)
- Disconnect handling via `HandlePlayerDisconnected`

#### Van / Homebase — `Assets/Scripts/Player/Van/` + `Assets/Scripts/Core/HomebaseManager.cs`
- `HomebaseManager`: spawns offline player, destroys on network start
- `VanExitController`: F to exit van → teleport to exit position → boost → `StartGliding()`
- `VanHealingZone`: heal while in van
- `RecallController`: hold Recall (input) → 5s channel → teleport to van; interrupted by movement/wind-riding/death/pause
- Hub interactables (`Assets/Scripts/Interactions/`): `DepartureGate` (party mgmt), `ClassSelectionStation`, `NetworkTerminal`, `HubInteractable` (base), `IInteractable`

### Presentation

#### UI System — `Assets/Scripts/UI/`
- **Lobby** (`UI/Lobby/`): `LobbyManager`, `LobbyTabController`, `LobbySettingsPanel`, `LobbyPartyPanel`, `LobbyChatUI`, `LobbyClassSelectionUI`, `CharacterSelectPanel`, `CharacterViewPanel`, `LobbyClassCard`
- **HUD**: `HealthBar`, `ManaBar`, `AbilityCooldownUI` (Q/E/R), `MinimapUI`/`MinimapTrackable` (enemies=red, boss=orange), `TeamHealthUI`/`TeamHealthBarEntry`, `ChargeIndicatorUI`, `InventoryHUD`, `ItemSlotUI`
- **Feedback**: `DamageNumber`, `EnemyHealthBar`, `BossIntroUI`, `PingIndicatorUI`, `FpsIndicatorUI`, `ChargeIndicatorUI`
- **Menus**: `PauseMenu`, `VictoryUI`, `GameOverUI`, `DisconnectNotificationUI`, `LoadingScreenUI`, `SpectatorUI`, `NetworkMenu`, `InteractionUI`
- **Items**: `ItemSelectionUI`, `ItemCard`, `ItemSlotUI`
- `UIManager` (singleton): registers boss, shows/hides boss health bar
- Events: `OnMaxHealthChanged`, `OnManaChanged`, `OnCooldownChanged`, `OnEnchanterChargesChanged`, `OnShowItemSelection`/`OnHideItemSelection`
- TextMeshPro for text; prefabs in `Assets/Prefabs/UI/`

#### Camera — `Assets/Scripts/Camera/ThirdPersonCamera.cs`
- Over-the-shoulder third-person

#### Audio — `Assets/Scripts/Audio/`
- Wwise integration (`AK.Wwise.State`: Combat, Explore, Win, Lose, Menu)
- Event hubs: `GameEvents`, `PlayerEvents`, `EnemyEvents`, `BossEvents`, `WindRideEvents`
- `SoundData` (SO)

---

## Key Conventions & Patterns

### Namespaces
- `Category5.Core`, `Category5.Player`, `Category5.Player.WindRiding`, `Category5.Player.Van`, `Category5.Enemies`, `Category5.Boss`, `Category5.Items`, `Category5.UI`, `Category5.Audio`, `Category5.Interactions`
- `MapGenerator` and `Glider` are **root namespace** (no `Category5.` prefix)

### ScriptableObject Pattern
- Every data-driven type uses `[CreateAssetMenu(menuName = "Category5/...")]`
- Runtime stats are copied from the SO into protected fields in `InitializeFromData()` (called in `OnNetworkSpawn`)
- Designers tune via inspector; code reads from the SO instance

### Networking Patterns
- NetworkBehaviour + `[RequireComponent(typeof(NetworkObject))]` for networked entities
- `NetworkVariable<T>` for synced state; `ClientRpc` for server→client, `ServerRpc`/`[Rpc(SendTo.Server)]` for client→server
- Server guards: `if (!IsServer) return;` / `if (!IsServerAuthority) return;` at top of logic methods
- Owner guards: `if (!IsOwner && !IsOffline()) return;` for input handling
- Targeted RPCs use `ClientRpcParams.Send.TargetClientIds`

### State Machines
- Enemies/Bosses use enum state + `HandleStateMachine()` + `TransitionToX()` methods
- States run server-side only (`if (!IsServer) return;` in Update)

### Movement
- `CharacterController` for players; `NavMeshAgent` (server) + `NetworkTransform` (client sync) for enemies; `Rigidbody.MovePosition` for bosses
- Manual gravity + ground check with hysteresis (confirm/loss frames) for enemies and bosses

### Elements
- `ElementType` enum: `None, Fire, Thunder, Ice, Rain, Void`

### Layers
- 0=Default, 1=TransparentFX, 2=Ignore Raycast, 3=Player, 4=Water, 5=UI, 6=Enemy, 7=Projectile, 8=CloudSurface, 9=PlayerInTunnel

### Tags
- Untagged, Respawn, Finish, EditorOnly, MainCamera, Player, GameController, Path

---

## Current State

### Implemented & Functional
- 5 classes with full Q/E/R abilities + projectiles/zones/buffs
- Wind riding (tunnel + cloud + gliding), launch pads, drafting
- Procedural map generation (arenas + spline paths + wind tunnels + spawners), per-round regen
- Enemy system: 3 enemy types (Basic/Swarm/Ranged), spawner waves, taunt, knockback, launch, grapple
- Boss system: state machine, HP scaling, intro card, round reset
- Item system: 12 items with behaviours, tier upgrades, boss (sync) + island (async) drops
- Full game flow: 3 rounds, victory/game-over, respawn at round transition
- Networking: lobby, class selection, scene transitions, disconnect handling, Relay support
- UI: lobby, HUD, minimap, team health, cooldowns, item selection, menus, loading screens
- Van/Homebase: exit/gliding, healing, recall, hub interactables, departure gate
- Wwise audio integration

### Known Gaps / Future Work
- `Glider.cs` is an empty stub (gliding handled in `WindRiderController`)
- No death/respawn reward system; no leveling/progression; no XP
- No crowd-control (CC) framework (stuns exist ad-hoc on enemies)
- No lag compensation / rollback / matchmaker; reconnect flow unclear
- No voice chat (text only); no settings persistence; no accessibility options
- Procedural map gen has known issues (entrance reposition, path spacing, paths over arenas)
- No GDDs yet for: Enemy System, Boss System, Map Generation (existing GDDs are reverse-documented)

### GDDs (`design/gdd/`)
- `game-concept.md` — core vision, pillars, scope tiers
- `player-system.md` — stats, classes, combat, movement (reverse-documented)
- `abilities-system.md` — Q/E/R, cooldowns, mana, class loadouts (reverse-documented)
- `wind-riding-system.md` — tunnel riding, sway, physics (reverse-documented)
- `item-system.md` — items, tiers, boss/island drops (in design)
- `networking-system.md` — NGO architecture, sync, scenes (reverse-documented)
- `ui-system.md` — lobby, HUD, menus, feedback (reverse-documented)
- `systems-index.md` — system layer index
- `design/quick-design-item-drop.md` — island drop quick spec

---

## Technology Stack

- **Engine**: Unity 6 (6000.3.0f1), URP
- **Language**: C#
- **Networking**: Unity Netcode for GameObjects + Unity Relay (`RelayHelper`)
- **Splines**: Unity Splines package (wind tunnel paths)
- **Navigation**: Unity AI Navigation (`NavMeshSurface`/`NavMeshAgent`)
- **Audio**: Wwise (`AK.Wwise`)
- **UI Text**: TextMeshPro
- **Input**: Input System (`InputSystem_Actions`)
- **Version Control**: Git, trunk-based development (feature branches off `main`)
- **Asset packages**: Kevin Iglesias Human Animations, Polysplit LowPolyMedievalFantasyHeroes

---

## Project Structure

```text
Assets/
├── Scripts/
│   ├── Core/          # GameFlowManager, GamePhase, networking, spawn, IDamageable, ClassRegistry, ElementType
│   ├── Player/        # PlayerController, PlayerStats, PlayerCombat, PlayerClass, PlayerClassManager
│   │   ├── Abilities/ # AbilityBase, AbilityData, PlayerAbilityManager + per-class folders (Ranger/Fighter/Enchanter/Elementalist/Assassin)
│   │   ├── WindRiding/# WindRiderController, WindTunnel, WindLaunchPad, WindDraft*, WindRideSettings
│   │   ├── Van/       # VanExitController, VanHealingZone, RecallController
│   │   └── Movement/  # Glider (stub)
│   ├── Enemies/       # EnemyBase, EnemyData, EnemySpawner, BasicEnemy, SwarmEnemy, RangedEnemy, EnemyVisuals
│   ├── Boss/          # BossBase, BossData, BossAttackData, TestBoss, BossVisuals, BossProjectile
│   ├── Items/         # ItemData, ItemRegistry, ItemManager, PlayerInventory, ItemDrop, ItemBehaviour + Behaviours/
│   ├── UI/            # All UI (Lobby/, HUD, menus, feedback, items)
│   ├── Audio/         # Wwise events, SoundData
│   ├── Camera/        # ThirdPersonCamera
│   ├── Map/           # MapGenerator, TriggerVolume
│   ├── Interactions/  # Hub interactables, IInteractable
│   └── Utils/         # AutoStartHost
├── Scenes/            # MainMenu, Homebase, DebugMap (active), SampleScene (legacy), NiccoTestScene
├── Prefabs/           # (UI/, etc.)
└── [imported asset packages]
design/gdd/            # Game design documents (see Current State)
opencode.json          # OpenCode config
.opencode/             # Framework: agents/, skills/, commands/, plugins/, rules/
docs/                  # Architecture, engine reference, workflow docs
```

---

## Development Standards

- All game code must include doc comments on public APIs
- Gameplay values must be data-driven (ScriptableObjects / external config), never hardcoded
- All public methods must be unit-testable (dependency injection over singletons where feasible)
- **Verification-driven development**: write tests first for gameplay systems; verify UI with screenshots; compare expected vs actual before marking complete
- Commits must reference the relevant design document or task ID
- No comments in code unless explicitly requested

### Networking Rules
- Never modify `NetworkVariable` values on clients (server-only writes for game state)
- Guard all server logic with `if (!IsServer) return;`
- Guard all owner input with `if (!IsOwner && !IsOffline()) return;`
- Use targeted `ClientRpcParams` when sending to a specific client

---

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question → Options → Decision → Draft → Approval**

- Ask "May I write this to [filepath]?" before using Write/Edit tools
- Show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

## Context Management

**The file is the memory, not the conversation.** Maintain
`production/session-state/active.md` as a living checkpoint after each milestone
(design approved, architecture decision, implementation milestone, test results).

---

## Workflow & Commands

This project uses the OpenCode Game Studios framework. Type `/` to see all commands.
All 50 commands route to skills in `.opencode/skills/`.

Key commands: `/brainstorm`, `/design-system`, `/quick-design`, `/create-architecture`,
`/dev-story`, `/code-review`, `/qa-plan`, `/smoke-check`, `/prototype`, `/sprint-plan`.

### Hybrid Workflow (this project)
- **Discovery**: rapid prototyping in `prototypes/`, low process overhead
- **Production**: full discipline — GDDs, ADRs, tests, quality gates

### Quality Gates
- Agent validation (`.github/workflows/agent-validation.yml`)
- Plugin tests (`node .opencode/plugins/tests/test-*.mjs`)

### Studio Hierarchy
Tier 1 Directors: `creative-director`, `technical-director`, `producer`
Tier 2 Leads: `game-designer`, `lead-programmer`, `art-director`, `audio-director`, `narrative-director`, `qa-lead`, `release-manager`, `localization-lead`
Tier 3 Specialists: gameplay/engine/ai/network/tools/ui programmers, systems/level/economy designers, technical artist, sound designer, writer, world-builder, ux-designer, prototyper, performance analyst, devops, analytics, security, qa-tester, accessibility, live-ops, community

**Engine specialist (Unity)**: `unity-specialist` + `unity-dots-specialist`, `unity-shader-specialist`, `unity-addressables-specialist`, `unity-ui-specialist`

---

## Notes

Framework: port of [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) to OpenCode.
Agents in `.opencode/agents/`, skills in `.opencode/skills/`, plugins in `.opencode/plugins/`.
Framework contribution guide: `docs/CONTRIBUTING.md`.
<!-- UNITY CODE ASSIST INSTRUCTIONS START -->
- Project name: Category5
- Unity version: Unity 6000.3.0f1
- Active scene:
  - Name: SoldierEnemy
  - Tags:
    - Untagged, Respawn, Finish, EditorOnly, MainCamera, Player, GameController, Path
  - Layers:
    - Default, TransparentFX, Ignore Raycast, Player, Water, UI, Enemy, Projectile, CloudSurface, PlayerInTunnel
- Active game object:
  - Name: WeakPoint_Flank
  - Tag: Untagged
  - Layer: Enemy
<!-- UNITY CODE ASSIST INSTRUCTIONS END -->