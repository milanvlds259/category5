# Copilot Instructions for STORM CHASERS (Category5)

## Project Overview
"STORM CHASERS" is a Co-op Boss Rush game built in Unity 6.2.
- **Core Loop:** Fight boss → Select power-up → Fight harder boss.
- **Multiplayer:** 2-4 player online co-op using **Netcode for GameObjects (NGO)**.
- **Design Reference:** **CRITICAL:** Before implementing ANY new feature, always consult `design-doc.md` first to ensure alignment with specific gameplay values, mechanics, and the intended player experience.

## Architecture & Patterns

### Networking (Netcode for GameObjects)
- **Base Class:** Inherit from `NetworkBehaviour` instead of `MonoBehaviour` for networked entities.
- **State Sync:** Use `NetworkVariable<T>` for syncing properties like Health or BossState.
    - Example: `public NetworkVariable<int> Health = new NetworkVariable<int>();`
- **RPCs:**
    - `[ServerRpc]`: Clients requesting actions (e.g., `TryAttackServerRpc`).
    - `[ClientRpc]`: Server notifying clients (e.g., `PlayHitVfxClientRpc`).
- **Spawning:** Networked objects (Projectiles, Enemies) must be instantiated on the Server and spawned via `GetComponent<NetworkObject>().Spawn()`.

### Code Organization
- **State Machines:** Use explicit State Machine patterns for Boss AI (e.g., `BossBaseState`, `BossAttackState`).
- **Managers:** Singleton pattern for `GameManager` (Round logic) and `LobbyManager`.
- **Input:** Use the **New Input System**. Reference `InputSystem_Actions.inputactions`.
    - Subscribe to events in `OnEnable`/`OnDisable`.

### Unity Best Practices
- **Serialization:** Use `[SerializeField] private` for Inspector variables. Avoid `public` fields unless necessary.
- **Dependencies:** Use `RequireComponent(typeof(T))` to ensure required components exist.
- **Performance:**
    - Cache `GetComponent` calls in `Awake`.
    - Use `TryGetComponent` for collision handling.
    - Avoid `FindObjectOfType` in `Update` loops.

### Coding Style
- **Comments:** ALL comments in the code must be fully written in lowercase and no punctuation unless extremely necessary for internal readability purposes.

## Implementation Specifics

### Player Controller
- **Movement:** Kinematic or CharacterController based.
- **Dodge:** Must implement 0.5s invincibility frame (i-frame).
- **Combat:** 3-hit combo system with distinct timing windows.

### Boss AI (Tempest Titan)
- **Telegraphing:** Every attack MUST have a visual telegraph state before execution.
- **Logic:** `Idle` (2s) -> `Telegraph` (1.5s) -> `Attack` -> `Cooldown`.

### Power-Ups
- Implement as a modular system (e.g., ScriptableObjects or Decorators) to allow stacking effects.
