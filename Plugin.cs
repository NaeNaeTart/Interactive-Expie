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
}

