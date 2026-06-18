# 🐾 Expie Petting and Interaction Mod

An interactive, high-fidelity physical and psychological BepInEx mod for **Casualties: Unknown** that lets you interact with your experiment subject ("expie") like a beloved, physical pet.

This mod introduces custom physics dragging/tugging forces, petting gestures, comforting neurological benefits, a satiety-fatigue simulation, and highly reactive sensory feedback depending on the subject's physical state.

---

## ✨ Features

### 1. 🧲 Physical Mouse Grabbing & Tugging
*   **Grab and Drag**: Hold the configured modifier key (default: `Left Alt`) and **Left-Click** on any physical limb in the world to grab and drag it.
*   **Spring Physics**: The grabbed limb is realistically pulled towards your mouse cursor using a fully physical 2D spring-damper calculation.
*   **Tug-to-Flop**: Grabbing and dragging a limb while the subject is standing up temporarily makes *only that limb* dynamic and floppy, allowing you to stretch or position it while the rest of the body maintains its standing animation.
*   **Strain Discomfort**: Dragging limbs too far beyond their range or pulling on dislocated/broken parts causes immediate localized pain spikes, tension stretch sound effects, and distressed vocalizations.
*   **Anti-Flying Physics Ceiling**: While the subject is ragdolled, dragging targets are dynamically clamped via a downward raycast checking the `"Ground"` layer. You can drag and slide them horizontally across any platforms, ramps, or sloped surfaces, but lifting them high into the air is blocked, neutralizing the flying physics exploit.

### 2. 🫳 Cozy Petting & Stroking
*   **Stroking Gesture Verification**: Petting requires active hand-stroking movement above a customizable velocity threshold (`MinPettingSpeed`). Simply hovering or holding your cursor stationary over a limb will not trigger petting.
*   **Soothing Neurological Benefits**: Petting healthy limbs slowly heals active limb pain, increases overall character `happiness`, and reduces emotional `trauma/stress`.
*   **Micro-movements & Nuzzling**: When petted on the head, the expie will physically lift and tilt their head into your cursor to simulate nuzzling.
*   **Happy Cooing**: Healthy subjects express deep contentment through soft sighs, purrs, or high-pitched cozy whimpers.

### 3. 🥱 Petting Saturation & Satiety
*   **Per-Body Satiety Tracking**: Satiety levels are tracked independently for each specific Expie body instance, allowing you to interact with multiple subjects naturally.
*   **Comfort Fatigue**: Over-petting causes the expie to gradually grow tired of physical contact. Satiety accumulates over continuous stroking.
*   **Diminishing Comfort Efficiency**: 
    *   **0% to 50% Satiety**: Full comfort and maximum mood boost efficiency.
    *   **50% to 100% Satiety**: Satiety sets in, smoothly scaling comfort and healing efficiency down to `0%`.
    *   **100% Satiety (Over-stimulation)**: The expie is completely indifferent. No mood upgrades, pain relief, or happy vocalizations are triggered.
*   **Persistent Moodles & Decay**:
    *   **Satiating Petting Moodle**: Once satiety reaches `50%`, the light-green `Satiating Petting` moodle remains continuously visible on the HUD, even when you stop petting, until it slowly decays back below the `50%` threshold.
    *   **Permanent Indifference Moodle**: Once satiety reaches `100%` (indifference is triggered), it is permanently locked at `100%` for that specific Expie body and never decays. The orange `Petting Satiety` moodle remains visible permanently on the HUD for that subject body.

### 🩹 Sensory Pain & Wounded Reactions
*   **Hypersensitivity**: Petting a body part with skin damage (active wounds or burns) triggers intense localized pain spikes, adrenaline shock surges, and a severe drop in happiness.
*   **Agonizing Vocalizations**: Petting wounded parts triggers intense vocal screams and high-volume distress lines.
*   **Unconscious Spasms**: Touching/petting active wounds on an **unconscious** subject causes physical reflexes (sudden spasms/convulsive twitches) accompanied by low, painful groans.

---

## 🎮 Controls & Interaction

| Interaction | Action | Default Controls | Reactions / Effects |
|---|---|---|---|
| **Limb Tugging** | Pull & drag body parts | Hold `Left Alt` + Click & Drag | Physical stretching, joint strain, floppy standing limb dynamics, auto-clamped ground ceiling. |
| **Cozy Petting** | Stroke healthy parts | Hold `Left Alt` + Gentle strokes | Heals limb pain, increases happiness, reduces trauma, triggers cozy sighing and happy whimpers. |
| **Painful Petting** | Stroke wounded parts | Hold `Left Alt` + Stroke wounds | Intense pain spikes, adrenaline shock, immediate drop in happiness, screams. |
| **Nuzzling** | Stroke the head | Hold `Left Alt` + Stroke head | Head physically lifts into your cursor, cozy purrs/coos. |
| **Unconscious reflex** | Touch wounds when asleep | Hold `Left Alt` + Touch wound | Subject physically twitches/groans in pain. |

---

## ⚙️ Configuration

A standard BepInEx config file is generated automatically at `BepInEx/config/expiepettingmod.casualtiesunknown.mod.cfg` after launching the game once. You can customize the following settings:

| Setting | Type | Default Value | Description |
|---|---|---|---|
| **ModifierKey** | KeyCode | `LeftAlt` | The key that must be held down to drag or pet. Set to `None` to use Left-Click directly without a modifier. |
| **PullStrength** | float | `120.0` | How hard the mouse physically pulls/tugs body parts. |
| **AutoReleaseDistance** | float | `1.25` | The distance a limb can be pulled before your grip automatically slips and releases. |
| **HappinessGainRate** | float | `0.3` | How fast petting a healthy limb increases subject happiness per second. |
| **PainIncreaseRate** | float | `25.0` | How fast petting a damaged limb spikes pain per second. |
| **StressDecreaseRate** | float | `0.5` | How fast petting decreases trauma/stress per second. |
| **EnableHappyWhimpers** | bool | `true` | Should the subject make soft coos, sighs, and happy noises when petted? |
| **MinPettingSpeed** | float | `120.0` | The minimum mouse speed (in screen pixels per second) required to register petting gestures. Prevents petting while stationary. |
| **PettingSaturationRate** | float | `5.0` | How fast petting saturation/satiety increases per second of continuous healthy petting (from 0 to 100). |
| **PettingDecayRate** | float | `2.0` | How fast petting saturation decays per second when not being petted. |

---

## 🛠️ Installation

1.  Ensure you have **BepInEx 5** installed for *Casualties: Unknown*.
2.  Drop `ExpiePettingMod.dll` into your `Casualties Unknown Demo/BepInEx/plugins/` directory.
3.  Launch the game and enjoy interacting with your expie!

---

## ⚖️ License & Disclaimer

Distributed under the **MIT License**. See `LICENSE` for details.

*Disclaimer: This is an unofficial, non-commercial fan-made mod for Casualties: Unknown. It is not affiliated with or endorsed by Orsoniks.*
