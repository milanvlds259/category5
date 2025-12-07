**Projectile System**

What it is
- A data-driven ranged projectile system. Create `Projectile Data` assets to define arrows/bolts and assign them to the player `PlayerCombat` for firing.

How it works
- Each `Projectile Data` asset tells the game which prefab to spawn, how fast and how hard it hits, what VFX/SFX it uses, and how charging affects damage and speed.

Make a projectile (3 steps)
1. Right-click in Project -> Create > Category5 > Projectile Data.
2. Fill the fields (see list below). Save under `Assets/Data/Projectiles/` and name it whatever you want.
3. Assign the asset to the player's `PlayerCombat` `arrowData` slot or to a ranged enemy/spawner.

Serialized fields (what to fill)
- `projectilePrefab`: the networked projectile prefab (must have `NetworkObject` and `NetworkedProjectile`).
- `speed`: base travel speed (units/sec).
- `damage`: base integer damage on hit.
- `lifetime`: seconds before the projectile auto-despawns.
- `trailVfxPrefab`: optional trail particle or object attached to the projectile.
- `impactVfxPrefab`: optional prefab spawned on hit.
- `fireSound`: sound played when firing.
- `impactSound`: sound played on impact.
- `maxChargeTime`: seconds to reach full charge when holding fire.
- `maxDamageMultiplier`: damage multiplier at full charge (1 = no change).
- `maxSpeedMultiplier`: speed multiplier at full charge (1 = no change).
- `chargeMovementSpeedMultiplier`: how much the player is slowed while charging (0.5 = half speed).

Put it in the scene / use it
- Ensure the `projectilePrefab` is registered in `NetworkManager` prefab list.
- Assign the `Projectile Data` asset to the `PlayerCombat` `arrowData` slot (or enemy spawner) in the inspector.

Quick test (editor)
1. Play as Host in Editor.
2. Switch the player's `combatClass` to `Ranged` and press fire to shoot. Hold fire to test charging behavior (watch `ChargeIndicatorUI`).
3. Observe trail and impact VFX and listen for sounds.

If it doesn't work
- Make sure the `projectilePrefab` has `NetworkObject` and `NetworkedProjectile` components and is listed in `NetworkManager` prefabs.
- Check `speed`, `lifetime`, and `damage` values.
- If charge feels wrong, tweak `maxChargeTime` and `maxDamageMultiplier`.
