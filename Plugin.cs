using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ExpiePettingMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    [BepInProcess("CasualtiesUnknown.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        private Harmony _harmony = null!;
        internal static ModConfig Cfg { get; private set; } = null!;

        private void Awake()
        {
            Instance = this;
            Logger   = base.Logger;

            // Bind configuration settings
            Cfg = new ModConfig(Config);

            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Add the main interaction controller to this persistent GameObject
            gameObject.AddComponent<ExpiePettingController>();

            Logger.LogInfo($"[{PluginInfo.NAME} v{PluginInfo.VERSION}] Loaded successfully! Hover and interact with your subject.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(Body), nameof(Body.UseItemInHand))]
    public static class BlockAttackPatch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (ExpiePettingController.Instance != null && ExpiePettingController.Instance.IsInteracting)
            {
                // Suppress clawing/punching/item use while holding drag button or actively dragging
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MoodleManager), "AddAllMoodles")]
    public static class MoodleManagerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MoodleManager __instance)
        {
            if (ExpiePettingController.Instance != null && ExpiePettingController.Instance.IsPettingRecently)
            {
                if (ExpiePettingController.Instance.IsPettingHealthy)
                {
                    // intensity 5 gives a positive green/happy background, "happy" is the built-in cute smiley icon
                    __instance.AddMoodle(5, "happy", "Being Petted", "This expie is actively receiving gentle, comforting physical contact, lowering stress and reducing pain.");
                }
                else
                {
                    // intensity 3 gives a critical/warning red background, "pain" or "shock" icon
                    __instance.AddMoodle(3, "pain", "Irritated Wounds", "This expie's raw skin wounds are being touched or rubbed, causing intense physical distress!");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Body), "HandleVisuals", new System.Type[] { typeof(Painkillers) })]
    public static class RestoreStandingSimulatedLimbsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Body __instance)
        {
            if (__instance.standing && __instance.limbs != null)
            {
                foreach (Limb limb in __instance.limbs)
                {
                    if (limb != null && !limb.dismembered && limb.rb != null && limb.rb.simulated)
                    {
                        if (limb.rb.bodyType == RigidbodyType2D.Kinematic)
                        {
                            // Kinematic anchors (e.g. torso) follow the animator's visual position/rotation
                            limb.rb.position = limb.transform.position;
                            limb.rb.rotation = limb.transform.eulerAngles.z;
                        }
                        else
                        {
                            // Dynamic simulated limbs follow the physical Rigidbody2D simulation state
                            limb.transform.position = limb.rb.position;
                            limb.transform.eulerAngles = new Vector3(0f, 0f, limb.rb.rotation);
                        }
                    }
                }
            }
        }
    }
}

