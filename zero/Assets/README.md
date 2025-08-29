# Dice Demo (SampleScene)

This Unity project spawns six dice in SampleScene and provides a stage-based interaction flow driven by the spacebar. It supports a 3D physically-plausible visual rotation (quad-based cube) and an alternative 2D/cartoon mode.

## Files and Responsibilities

- Assets/Scirpte/DiceManager.cs
  - Attach this component manually to a GameObject in your scene (e.g., an empty `DiceController`).
  - No longer auto-bootstraps on load.
  - Spawns six dice in a row (3D by default; 2D alternative available).
  - Creates lightweight UI: a top status text and, for the total stage, a fullscreen semi-transparent overlay + centered total text.
  - Stage machine driven by spacebar: Ready → Shaking → Revealed → TotalShown → Ready.
  - Handles energy model (per-press acceleration, decay, and non-linear speed mapping) and transitions.
  - In 3D mode, randomizes each die’s rotation axis while avoiding axes nearly parallel to the camera forward vector.
  - Stops and reveals per-die result; in 3D, aligns each die smoothly to make the chosen face front.

- Assets/Scirpte/Dice3DRotator.cs
  - Rotates a die around a specified 3D axis with smoothed angular speed.
  - `SetTargetFace(face)` computes a target rotation (1..6 mapping) and Slerps to it when stopping.
  - Exposes `CurrentSpeedAbs` for “natural stop” detection.

- Assets/Scirpte/DiceRotator.cs (2D optional)
  - 2D Z-rotation with smoothing and optional squash/stretch and faux-3D flip.

- Assets/Scirpte/GhostTrail.cs (2D optional)
  - Spawns short-lived, fading sprite ghosts for motion trails.

## Scene Lifecycle

- On scene start (with `DiceManager` attached), six dice are spawned horizontally centered at y=0.
- Status text shows the current stage and the next action.

Setup steps:
- Open `SampleScene`.
- Create an empty GameObject (e.g., `DiceController`) and add `DiceManager`.
- Optional: set `defaultSkin` or assign a `DiceSkin` asset to `skinAsset`.

## Interaction and Stages

- Ready
  - UI: “按空格开始摇动”.
  - Space → enters Shaking and grants a small initial energy boost.

- Shaking
  - Each space press adds energy (like giving an impulse). Energy decays over time.
  - Actual angular speed derives from non-linear mapping of energy.
  - Natural stop logic: when energy is exhausted and (in 3D) all dice angular speeds fall below a small threshold, the system transitions automatically to Revealed.
  - UI: “摇动中（按空格加速，松开自然停）”.

- Revealed
  - Each die is assigned a random face 1..6.
  - In 3D, each die Slerps to align that face to the camera.
  - Space → TotalShown.

- TotalShown
  - Shows a fullscreen dark overlay (fade-in) with the list of face results and their sum in a centered text.
  - Space → resets to Ready, hides overlay, resets dice to face 1, rotation stopped.

## 3D Dice Construction

- Each die is a root object with six child `Quad` primitives forming a cube:
  - Face mapping: 1 front +Z, 2 right +X, 3 back -Z, 4 left -X, 5 top +Y, 6 bottom -Y.
  - Each quad uses a simple `Universal Render Pipeline/Unlit` material whose main texture is a procedurally generated face texture.

- Axis randomness with camera-avoidance:
  - DiceManager picks a random axis per die with |dot(axis, camera.forward)| <= `cameraDotLimit` to avoid “spinning into the screen”.

## 2D Dice (optional)

- Uses `SpriteRenderer` with generated face sprites.
- Optional exaggerated squash/stretch and faux-3D flip during spin.
- Optional `GhostTrail` can be enabled for motion trails.
- Switch via `DiceManager.use3D` (default true = 3D).

## Energy and Speed Model

- On press (space) during Shaking: `energy += energyPerPress` (clamped to `maxEnergy`).
- Non-linear speed factor: `factor = nonLinearA * (1 - exp(-nonLinearB * energy))`.
  - Effective angular speed used by rotators: `rotationSpeed * factor`.
- Dynamic decay each frame during Shaking:
  - Base decay ramps up during idle: lerp from `decayBase` to `decayHigh` after `idleThreshold` seconds without presses over `rampDuration`.
  - Energy-dependent decay: `+ decayEnergyK * max(0, energy - energyThresholdE0)`.
  - Speed friction: `+ speedFrictionK * avgAngularSpeed`.
  - Hard cap on stop time: if `energy / decay > maxStopTime`, decay is increased so that `energy / decay == maxStopTime`.

- Natural stop:
  - 3D: energy nearly zero and every die’s `CurrentSpeedAbs <= stopSpeedThresholdDeg` → transition to Revealed.
  - 2D: approximated via a short buffer timer once energy is zero.

## UI

- Status Text (top center): shows the stage and prompts.
- Overlay (TotalShown): a fullscreen semi-transparent black panel (fade-in) with centered total text.
- Fonts use `LegacyRuntime.ttf` to avoid modern Unity’s Arial.ttf removal.

## Audio (SFX)

- Fields on `DiceManager`:
  - Enable flags: `enableSfx`, `enableLoop`, `enableTick`, `enablePressSfx`.
  - Clips: `clipStart`, `clipLoop`, `clipTick[]`, `clipStop`, `clipReveal`, `clipPress`.
  - Mixer: `sfxMixer` (optional AudioMixerGroup for unified routing/limiting).
  - Volumes/pitch: `volumeMaster`, `volumeLoop`, `volumeTick`, `volumeOneShot`, `loopPitchMin/Max`.
  - Tick timing: `tickBaseInterval`, `tickMinInterval`, `tickSpeedScale`, `tickPitchJitter`.
  - `spatialBlend` for 2D/3D sound, and `loopFadeOutTime`.

- Lifecycle:
  - StartShaking: plays `clipStart` (one-shot), starts `clipLoop` (looped, volume/pitch follow speed).
  - Shaking: emits `clipTick` by speed-driven cadence; optional `clipPress` on space.
  - StopAndReveal: fades out loop, plays `clipStop`.
  - ShowTotal: plays `clipReveal`.

- Audio Mixer setup (recommended):
  1. Create an AudioMixer (Project: right-click → Create → Audio Mixer), add a group named `SFX`.
  2. (Optional) Add a Limiter/Compressor on `SFX` to avoid clipping; adjust bus volume.
  3. In Unity menu `Tools → Dice → Audio Setup...`, drag the `SFX` group into the window and click “Assign To Scene”.
  4. Alternatively, set `DiceManager.sfxMixer` manually and ensure per-die `AudioSource` route to that group.

## Configuration (Inspector)

- Dice Settings
  - `diceCount`, `spacing`, `diceSize`, `rotationSpeed`, `use3D`.
  - `defaultSkin` (enum): Classic, Dark, Candy, Neon.
  - `skinAsset` (optional): ScriptableObject to procedurally generate a custom skin.

- 3D Axis Settings
  - `cameraDotLimit`: exclude axes near camera forward (smaller = more planar spin).

- Spacebar Control
  - `energyPerPress`, `maxEnergy`.

- Energy Decay Advanced
  - `nonLinearA`, `nonLinearB` – non-linear speed mapping parameters.
  - `decayBase`, `decayHigh` – base and idle-ramped decay.
  - `idleThreshold`, `rampDuration` – idle ramp settings.
  - `energyThresholdE0`, `decayEnergyK` – high-energy extra decay.
  - `speedFrictionK` – speed-proportional decay.
  - `maxStopTime` – ensures stopping no slower than this.
  - `stopSpeedThresholdDeg` – threshold to consider a die stopped (3D).

- FX Settings (2D only)
  - `enableGhostTrail`, `enableFlip`, `exaggerationCartoon`.

- Overlay
  - `overlayTargetAlpha`, `overlayFadeDuration`.

## Defaults tuned for “fast acceleration, ≤1s stop after last press”

- DiceManager defaults (as code values) were tuned to:
  - Quickly accelerate with presses: `rotationSpeed` increased and `energyPerPress` at 1.0.
  - Short stopping tail: high `decayHigh`, non-linear mapping, friction, and `maxStopTime` = 1.0.

If you want different behavior, adjust these values in the Inspector. For stronger acceleration increase `energyPerPress` or `rotationSpeed`; for faster stopping increase `decayHigh` and/or `speedFrictionK`, or reduce `nonLinearA`.
