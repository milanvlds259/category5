**Hit Feedback**

What it is
- Centralized system for screen shake, hit freeze, and vfx/sfx hooks. Use it to tune how hits and boss attacks feel.

How it works
- `HitFeedbackManager` holds presets (light, heavy, boss slam, player damaged). Combat code calls the manager to trigger shake/freeze and fires events you can subscribe to for VFX/SFX.

Serialized fields (what to set)
- `lightHitFeedback` / `heavyHitFeedback` / `bossSlamFeedback` / `playerDamagedFeedback`:
  - Each is a `HitFeedbackData` block. edit these to tune intensity and timings for different hit grades.

- `enableScreenShake` (bool): master on/off for screen shake. turn off to disable all shake.

- `enableHitFreeze` (bool): master on/off for hit freeze. turn off to skip animator/time freezes.

- `globalIntensityMultiplier` (0-2): multiplies shake and freeze amounts. use <1 to tone down all effects quickly.

- `freezeMethod` (AnimatorPause / TimeScale / Both): how freeze is applied. `AnimatorPause` is safest for networking.

- `debugHitFreeze` (bool): enable to log freeze start/end for debugging.

HitFeedbackData fields (what to enter)
- `shakeIntensity` (0.0 - 1.0): 0 = none, 0.1 = light, 0.5 = strong. start small and increase until it feels punchy.
- `shakeDuration` (seconds): how long the shake lasts. 0.05–0.3 typical.
- `shakeFrequency`: how fast the shake oscillates. higher = choppier. 20–40 typical.
- `freezeDuration` (seconds): freeze length. 0.03–0.15 typical.
- `freezeTimeScale` (0.0 - 1.0): time scale during freeze. 0.0 = full stop, 0.05 = very slow.

How to tune quickly
1. Edit `lightHitFeedback` for regular hits and `heavyHitFeedback` for strong hits.
2. Use `globalIntensityMultiplier` to preview stronger/weaker overall effects without changing presets.
3. Toggle `enableScreenShake` / `enableHitFreeze` to isolate one system while tuning the other.

Quick test (editor)
1. Play as Host in Editor.
2. Hit an enemy to trigger `TriggerLightHit`/`TriggerHeavyHit` or take damage to trigger `TriggerPlayerDamaged`.
3. Watch camera shake and animator freeze. Use `debugHitFreeze` to verify freeze timing in Console.

VFX/SFX hooks (events)
- `OnPlayerHitEnemy(Vector3 pos, int damage, bool isCritical)` — fired when you hit an enemy.
- `OnPlayerTakeDamage(Vector3 pos, int damage)` — fired when you take damage.
- `OnBossAttackTelegraph(BossAttackType, Vector3)` — fired at telegraph start.
- `OnBossAttackExecute(BossAttackType, Vector3)` — fired when the boss executes.
- `OnHitFeedback(Vector3 pos, HitFeedbackData data)` — fired for any hit with the resolved feedback data.

If it doesn't behave as expected
- Make sure the `HitFeedbackManager` GameObject exists in the scene and is enabled.
- If shakes are missing, confirm `ThirdPersonCamera` is present (manager finds camera at Start).
- If freeze seems to affect networking, switch `freezeMethod` to `AnimatorPause`.
