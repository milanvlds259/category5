**Enemy System**

What it is
- Data-driven enemy system. Create `Enemy Data` assets to define enemy stats and VFX, use enemy prefabs with `EnemyBase` (or `BasicEnemy`) for behavior, and use `EnemySpawner` to control waves.

How it works
- `EnemyData` holds all tunable values (HP, speed, damage, VFX, audio). An enemy prefab uses `EnemyBase` (or a subclass) which reads `EnemyData` at spawn. `EnemySpawner` spawns networked enemy prefabs on the server and tracks waves.

Make an enemy type (3 steps)
1. Right-click in Project -> Create > Category5 > Enemy Data.
2. Fill the `Enemy Data` fields (see list below). Save under `Assets/Data/Enemies/` and name like `IceWolf` or something.
3. Make a new prefab that contains an enemy component (e.g., `BasicEnemy`) and assign the `Enemy Data` asset on the prefab.

EnemyData fields (what to fill)
- `enemyName`: short display name.
- `elementType`: elemental tag (None/Thunder/etc) used for effects.
- `maxHealth`: starting HP.
- `moveSpeed`: movement speed (units/sec).
- `rotationSpeed`: turning speed (deg/sec).
- `damage`: damage per attack.
- `attackRange`: melee or ranged check distance.
- `attackCooldown`: seconds between attacks.
- `staggerDuration`: seconds stunned after hit.
- `detectionRange`: how far the enemy notices players.
- `leashRange`: distance at which the enemy gives up chasing.
- `enemyPrefab`: fallback prefab for spawners (assign the prefab you use).
- `enemyColor`: tint applied to the enemy mesh.
- `scaleMultiplier`: scale applied at spawn.
- `deathVfxPrefab` / `spawnVfxPrefab` / `attackVfxPrefab`: optional VFX prefabs.
- `spawnSound` / `attackSound` / `hurtSound` / `deathSound`: optional audio clips.
- `experienceReward`: exp on death (future use).
- `gizmoColor`: editor color for gizmos.

EnemyBase / prefab fields (what to check on the prefab)
- `enemyData` (EnemyData): assign the data asset here.
- `healthBar` (EnemyHealthBar): optional world-space health bar component.
- `targetUpdateInterval`: how often the enemy refreshes its nearest player target (seconds).
- Ground/physics: `groundCheckRadius`, `groundCheckOffset`, `groundLayers`, `gravity`, `terminalVelocity` can be tuned on prefab if ground collision is being weird.

BasicEnemy-specific fields
- `attackDuration`: how long the attack animation/state lasts.
- `damageDelay`: delay before damage is applied during the attack.
- `meshRenderer`: optional renderer used for hit flash and tinting.

EnemySpawner fields (what to set)
- `enemyData` / `enemyPrefab`: enemy type or prefab to spawn. `enemyPrefab` wins if set.
- `spawnPoints`: array of Transforms used as spawn locations.
- `useRandomSpawnPoints`: true to pick random points, false to cycle.
- `enemiesPerWave`, `totalWaves`: wave sizing.
- `spawnInterval`: seconds between individual spawns.
- `waveCooldown`: seconds between waves.
- `autoStartOnSpawn`: start spawning automatically when the server spawner spawns.
- `spawnOccupancyRadius`: radius to check to avoid spawning inside colliders.

Put enemies in the scene
- Add enemy prefab to the scene OR let `EnemySpawner` instantiate it. If using a spawner, place spawn point Transforms as children or reference existing points.

Quick test (editor)
1. Play as Host in the Editor (server-authoritative spawning runs on host).
2. If using a spawner, ensure `autoStartOnSpawn` is enabled or call `StartSpawning()` on the spawner.
3. Watch enemies spawn, chase the player, telegraph/attack, take damage, and die. Tune `attackRange`, `detectionRange`, and timing values on the `EnemyData` asset.

If it doesn't work
- Make sure the prefab has a `NetworkObject` and the enemy prefab is registered in `NetworkManager` prefabs if spawned at runtime.
- Ensure `EnemyData.enemyPrefab` or `EnemySpawner.enemyPrefab` is assigned and not null.
- If enemies never target players, verify `targetUpdateInterval`, `detectionRange`, and that player `PlayerController` instances are connected and not marked dead.
- If attacks don't deal damage, check the concrete enemy's `damage` value (from `EnemyData`) and `attackDuration`/`damageDelay` on the prefab component.
