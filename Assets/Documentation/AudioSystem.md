**Audio System**

What it is
- Simple configurable audio assets (`Sound Data`) you use for SFX and music. The `AudioManager` reads these and plays clips with pooling and optional 3D attenuation.

How it works
- Create `Sound Data` assets that hold one or more clips and tuning (volume, pitch, 3D settings). Call `AudioManager` or let game events trigger sounds; the manager handles pooling and playback.

Make a sound asset (3 steps)
1. Right-click in Project -> Create > Category5 > Sound Data.
2. Fill the fields (list below). Save under `Assets/Data/Audio/` and name like `AttackSlash` or something
3. Assign the `Sound Data` asset to the appropriate slot in `AudioManager` or call `AudioManager.Instance.PlaySound(soundData, position)` from a script.

Serialized fields (what to fill)
- `clips` (AudioClip[]): add one or more clips. one is chosen at random when played.
- `volume` (0.0-1.0): base loudness.
- `volumeVariation` (0.0-0.5): random +/- variation applied each play.
- `pitch` (0.1-3.0): base pitch.
- `pitchVariation` (0.0-0.5): random +/- pitch variation.
- `is3D` (bool): true = 3D spatialized sound; false = 2D UI-style sound.
- `minDistance` / `maxDistance`: 3D attenuation distances (min where full volume starts, max where it fades out).
- `loop` (bool): loop the clip when played by the audio manager.
- `priority` (int 0-256): audio source priority (0 = highest priority).
- `mixerGroup` (AudioMixerGroup): optional route to SFX/Music/UI mixer group.

Quick test (editor)
1. Play as Host in Editor.
2. Trigger an event that plays the sound (attack, telegraph, UI click). Confirm you hear it and variations apply.
3. For 3D sounds (if we make any), move the camera or player to test attenuation and spatialization.

If it doesn't work
- Ensure `AudioManager` exists in the scene and that the `Sound Data` asset is assigned or referenced.
- If no sound plays, check that `clips` contains a valid AudioClip and that volume isn't zero.
- If 3D sound seems wrong, verify `is3D`, `minDistance`, and `maxDistance` values, and that the AudioSource is using the correct spatial blend.
