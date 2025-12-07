**Power-Up System**

What it is
- A simple system that shows players 1 or more power-up cards after a boss dies. Players pick a card and the chosen power-up is applied to their character for the next round.

How it works
- All power-ups are `PowerUpData` assets. The `PowerUpRegistry` lists the assets present in the scene. `PowerUpManager` picks choices from the registry, sends them to each client, and tells `PlayerStats` to apply the selected power-up.

How to Make a power-up
1. Right-click in Project -> Create > Category5 > Power-Up Data.
2. Fill the fields: `displayName`, `effectType` (DamageMultiplier / MaxHealthBonus / DodgeCooldownReduction / FlatDamageBonus / Lifesteal), `value`, and an `icon`.
3. Name it whatever you want and save it under `Assets/Data/PowerUps/`.

Serialized fields (what to fill)
- `powerUpName`: the name shown on the card. keep it short.
- `description`: one-line explanation shown in tooltips.
- `icon`: card image sprite (recommended 256x256).
- `effectType`: pick one: `DamageMultiplier`(multiplies player damage), `MaxHealthBonus`(adds flat HP), `DodgeCooldownReduction`(reduces dodge cooldown in seconds), `FlatDamageBonus`(adds flat damage per hit), `Lifesteal`(fraction of damage returned as health).
- `effectValue`: the numeric value for the chosen effect (eg. 1.10 for a 10% damage multiplier, 50 for +50 HP, 0.15 for 0.15s dodge reduction, 3 for +3 damage, 0.05 for 5% lifesteal).
- `glowColor`: tint used on the card and selection glow.
- `visualEffectPrefab`: optional particle or VFX prefab to spawn on the player when the power-up is applied.

Put it in the scene
- Open the `PowerUpRegistry` GameObject in the scene and drag the new power up asset into the list.

How to quick test (editor)
1. Play as Host in the Editor.
2. Defeat the boss —> the selection UI should show your card.
3. Pick the card and then test to see if the changes applied on your player.

If it doesn't work
- Make sure `PowerUpRegistry` contains the asset in the scene (not just Project).
- Make sure `PowerUpManager` exists and has a `NetworkObject` component and is spawned.
- Check the Inspector on your player `PlayerStats` to see if the power-up was added.
