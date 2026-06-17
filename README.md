# 🐾 Expie Petting and Interaction Mod

An interactive, high-fidelity physical and psychological BepInEx mod for **Casualties: Unknown** that lets you interact with your experiment subject ("expie") like a beloved, physical pet.

This mod introduces custom physics dragging/tugging forces, petting gestures, comforting neurological benefits, and highly reactive sensory feedback depending on the subject's physical state.

---

## ✨ Features

### 1. 🧲 Physical Mouse Grabbing & Tugging
*   **Grab and Drag**: Hold the configured modifier key (default: `Left Alt`) and **Left-Click** on any physical limb in the world to grab it.
*   **Spring Physics**: The grabbed limb is realistically pulled towards your mouse cursor using a fully physical 2D spring-damper calculation.
*   **Stretch Feedback**: Pulling a body part further than its normal physical range triggers subtle `"stretch"` tension sound effects.
*   **Strain Discomfort**: Dragging limbs too far or pulling on dislocated/broken parts causes immediate localized pain spikes and distressed vocalizations.

### 2. 🫳 Cozy Petting & Stroking
*   **Gesture Recognition**: Dragging the cursor gently back and forth over a limb collider mimics petting.
*   **Soothing neurological benefits**: Petting healthy limbs (`skinHealth >= 95%`) slowly heals active limb pain, increases overall character `happiness` (+1.5/sec), and reduces emotional `trauma/stress` (-0.5/sec).
*   **Micro-movements & Nuzzling**: When petted on the head, the expie will physically lift and tilt their head into your cursor to simulate nuzzling.
*   **Happy Cooing**: Fully petted subjects express deep contentment through soft sighs, purrs, or high-pitched cozy whimpers.

### 3. 🩹 Sensory Pain & Wounded Reactions
*   **Hypersensitivity**: Petting a body part with skin damage (active wounds or burns, i.e., `skinHealth < 95%`) triggers intense localized pain spikes (+25.0/sec), adrenaline/shock surges, and a severe drop in happiness.
*   **Agonizing Vocalizations**: Petting wounded parts triggers intense vocal screams and high-volume distress sounds ("Please stop!", "It hurts!").
*   **Unconscious Spasms**: Touching/petting active wounds on an **unconscious** subject causes physical reflexes (sudden spasms/convulsive twitches) accompanied by low, painful groans.

---

## 🎮 Controls & Interaction

| Interaction | Action | Default Controls | Reactions / Effects |
|---|---|---|---|
| **Limb Tugging** | Pull & drag body parts | Hold `Left Alt` + Click & Drag | Physical stretching, joint strain, minor pain if pulled too far. |
| **Cozy Petting** | Stroke healthy parts | Hold `Left Alt` + Gentle strokes | Heals limb pain, increases happiness, reduces trauma, triggers cozy sighing. |
| **Painful Petting** | Stroke wounded parts | Hold `Left Alt` + Stroke wounds | Intense pain spikes, adrenaline shock, immediate drop in happiness, screams. |
| **Nuzzling** | Stroke the head | Hold `Left Alt` + Stroke head | Head physically lifts into your cursor, cozy purrs/coos. |
| **Unconscious reflex** | Touch wounds when asleep | Hold `Left Alt` + Touch wound | Subject physically twitches/groans in pain. |

---

## ⚙️ Configuration

A standard BepInEx config file is generated automatically at `BepInEx/config/expiepettingmod.casualtiesunknown.mod.cfg` after launching the game once. You can customize the following settings:

| Setting | Type | Default Value | Description |
|---|---|---|---|
| **ModifierKey** | KeyCode | `LeftAlt` | The key that must be held down to drag or pet. Set to `None` to use Left-Click directly without a modifier. |
| **PullStrength** | float | `350.0` | How hard the mouse physically pulls/tugs body parts. |
| **HappinessGainRate** | float | `1.5` | How fast petting a healthy limb increases subject happiness per second. |
| **PainIncreaseRate** | float | `25.0` | How fast petting a damaged limb spikes pain per second. |
| **StressDecreaseRate** | float | `0.5` | How fast petting decreases trauma/stress per second. |
| **EnableHappyWhimpers** | bool | `true` | Should the subject make soft coos, sighs, and happy noises when petted? |

---

## 🛠️ Installation

1.  Ensure you have **BepInEx 5** installed for *Casualties: Unknown*.
2.  Drop `ExpiePettingMod.dll` into your `Casualties Unknown Demo/BepInEx/plugins/` directory.
3.  Launch the game and enjoy interacting with your expie!

---

## ⚖️ License & Disclaimer

Distributed under the **MIT License**. See `LICENSE` for details.

*Disclaimer: This is an unofficial, non-commercial fan-made mod for Casualties: Unknown. It is not affiliated with or endorsed by Orsoniks.*
