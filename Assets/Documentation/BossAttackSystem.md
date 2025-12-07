**Boss Attack System**

What it is
- A simple data-driven system where each boss attack is a `BossAttackData` asset you create in the editor. Bosses pick attacks from a list, show a telegraph, then execute the attack.

How it works
- `BossAttackData` holds timings, ranges, damage, and VFX/SFX for one attack. The boss (e.g., `TestBoss`/`BossBase`) uses those assets to telegraph and run the attack without code changes.

Make an attack (3 steps)
1. Right-click in Project -> Create > Category5 > Boss Attack.
2. Fill the fields (see list below). Keep names unique and clear.
3. Add the new asset to your boss's attack list in the boss inspector (often called `availableAttacks`).

Serialized fields (what to fill)
- `attackName`: short display name for the attack.
- `attackType`: pick a type for VFX/SFX hooks (Slam, Swipe, Projectile, etc.).
- `selectionWeight`: higher = more likely this attack is chosen. use 0-100.
- `healthThreshold`: only available when boss HP <= this percent (0-1). use `1` to always allow.
- `minRange` / `maxRange`: distance from target where this attack can be used.
- `telegraphDuration`: how long the warning lasts before the attack.
- `attackDuration`: how long the attack action lasts.
- `cooldownDuration`: extra wait after the attack before boss acts again.
- `damage`: base integer damage dealt.
- `damageRadius`: area radius for the damage check.
- `damageOffset`: local-space offset where the damage is checked (move forward to hit in front of boss).
- `hasLunge`: tick if the boss should lunge forward during the attack.
- `lungeSpeed` / `lungeDistance`: how fast and how far the lunge goes.
- `isSweep`: tick for sweeping/arc attacks.
- `sweepOffset`: local offset where the sweep starts (lower y to align with player height).
- `sweepAngle`: sweep arc in degrees (e.g., 180).
- `sweepLength` / `sweepWidth`: size of the sweep beam.
- `customFeedback`: optional hit feedback settings (leave empty to use defaults).
- `isHeavyAttack`: tick for stronger feedback (screen shake/hit freeze).
- `gizmoColor`: editor-only color for scene gizmos.
- `telegraphPrefab`: prefab spawned during telegraph (ground indicator, ring, etc.).
- `telegraphColor`: tint applied to the telegraph prefab.
- `attackVfxPrefab`: prefab spawned when the attack executes (explosion, beam, etc.).
- `telegraphSound` / `attackSound`: audio clips for telegraph and execute phases.

Put it in the scene
- Add the asset to the boss's attack list (`availableAttacks`) in the boss inspector.

Quick test (editor)
1. Play as Host in Editor.
2. Make the boss target the player and reduce boss HP to enable any health-locked attacks.
3. Watch the telegraph appear, then the attack execute. Use `telegraphDuration` to tune timing.

If it doesn't work
- Ensure the boss has the attack list populated and the boss script is enabled.
- Make sure `telegraphPrefab` and `attackVfxPrefab` are set if you expect visuals.
- Check ranges (`minRange`/`maxRange`) and `healthThreshold` so the attack can be selected.

