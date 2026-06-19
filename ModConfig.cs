using BepInEx.Configuration;
using UnityEngine;

namespace ExpiePettingMod
{
    public class ModConfig
    {
        public ConfigEntry<KeyCode> ModifierKey { get; }
        public ConfigEntry<float> PullStrength { get; }
        public ConfigEntry<float> AutoReleaseDistance { get; }
        public ConfigEntry<float> HappinessGainRate { get; }
        public ConfigEntry<float> PainIncreaseRate { get; }
        public ConfigEntry<float> StressDecreaseRate { get; }
        public ConfigEntry<bool> EnableHappyWhimpers { get; }
        public ConfigEntry<float> MinPettingSpeed { get; }
        public ConfigEntry<float> PettingSaturationRate { get; }
        public ConfigEntry<float> PettingDecayRate { get; }

        public ModConfig(ConfigFile config)
        {
            ModifierKey = config.Bind("Interaction", "ModifierKey", KeyCode.LeftAlt, "The key that must be held down to drag or pet the subject. Set to None to use Left Click only.");
            PullStrength = config.Bind("Interaction", "PullStrength", 120f, "How hard the mouse gently pulls/tugs body parts. Lower values make it gentler.");
            AutoReleaseDistance = config.Bind("Interaction", "AutoReleaseDistance", 1.25f, "The distance the limb can be pulled before your grip automatically slips and releases.");
            HappinessGainRate = config.Bind("Interaction", "HappinessGainRate", 0.3f, "How fast petting a healthy limb increases subject happiness per second.");
            PainIncreaseRate = config.Bind("Interaction", "PainIncreaseRate", 25f, "How fast petting a damaged limb spikes pain per second.");
            StressDecreaseRate = config.Bind("Interaction", "StressDecreaseRate", 0.5f, "How fast petting decreases trauma/stress per second.");
            EnableHappyWhimpers = config.Bind("Interaction", "EnableHappyWhimpers", true, "Should the expie coo, sigh, or make happy noises when petted?");
            MinPettingSpeed = config.Bind("Interaction", "MinPettingSpeed", 120f, "The minimum mouse speed (in screen pixels per second) required to register petting gestures. Prevents petting while the mouse is stationary.");
            PettingSaturationRate = config.Bind("Interaction", "PettingSaturationRate", 2.5f, "How fast petting saturation/satiety increases per second of continuous healthy petting (from 0 to 100). Default: 2.5 (40 seconds to full saturation).");
            PettingDecayRate = config.Bind("Interaction", "PettingDecayRate", 2.0f, "How fast petting saturation decays per second when not being petted. Default: 2.0 (50 seconds to fully cool down from 100).");
        }
    }
}
