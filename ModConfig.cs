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

        public ModConfig(ConfigFile config)
        {
            ModifierKey = config.Bind("Interaction", "ModifierKey", KeyCode.LeftAlt, "The key that must be held down to drag or pet the subject. Set to None to use Left Click only.");
            PullStrength = config.Bind("Interaction", "PullStrength", 120f, "How hard the mouse gently pulls/tugs body parts. Lower values make it gentler.");
            AutoReleaseDistance = config.Bind("Interaction", "AutoReleaseDistance", 1.25f, "The distance the limb can be pulled before your grip automatically slips and releases.");
            HappinessGainRate = config.Bind("Interaction", "HappinessGainRate", 1.5f, "How fast petting increases happiness per second.");
            PainIncreaseRate = config.Bind("Interaction", "PainIncreaseRate", 25f, "How fast petting a damaged limb spikes pain per second.");
            StressDecreaseRate = config.Bind("Interaction", "StressDecreaseRate", 0.5f, "How fast petting decreases trauma/stress per second.");
            EnableHappyWhimpers = config.Bind("Interaction", "EnableHappyWhimpers", true, "Should the expie coo, sigh, or make happy noises when petted?");
        }
    }
}
