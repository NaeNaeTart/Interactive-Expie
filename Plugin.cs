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
            if (ExpiePettingController.Instance == null) return;

            // Fetch the specific body this MoodleManager is rendering for
            Body? body = null;
            FieldInfo bodyField = typeof(MoodleManager).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic);
            if (bodyField != null)
            {
                body = bodyField.GetValue(__instance) as Body;
            }
            if (body == null && PlayerCamera.main != null)
            {
                body = PlayerCamera.main.body;
            }

            if (body == null) return;

            bool isPermanent = ExpiePettingController.Instance.IsPermanentlyIndifferent(body);
            float sat = ExpiePettingController.Instance.GetPettingSaturation(body);
            bool pettingRecently = ExpiePettingController.Instance.IsPettingRecentlyFor(body);
            bool pettingHealthy = ExpiePettingController.Instance.IsPettingHealthyFor(body);

            if (pettingRecently)
            {
                if (!pettingHealthy)
                {
                    // Show "Irritated Wounds" if petting unhealthy, even if indifferent
                    __instance.AddMoodle(3, "pain", "Irritated Wounds", "This expie's raw skin wounds are being touched or rubbed, causing intense physical distress!");
                }
                else
                {
                    if (isPermanent)
                    {
                        __instance.AddMoodle(2, "happy", "Petting Satiety", "This expie has been petted so much that they are now permanently indifferent. Petting grants no mood boost.");
                    }
                    else if (sat >= 50f)
                    {
                        __instance.AddMoodle(4, "happy", "Satiating Petting", "This expie is starting to get tired of being petted, reducing the comforting effect.");
                    }
                    else
                    {
                        __instance.AddMoodle(5, "happy", "Being Petted", "This expie is actively receiving gentle, comforting physical contact, lowering stress and reducing pain.");
                    }
                }
            }
            else
            {
                // Not actively petting recently, but display persistent/decaying moodles
                if (isPermanent)
                {
                    __instance.AddMoodle(2, "happy", "Petting Satiety", "This expie has been petted so much that they are now permanently indifferent. Petting grants no mood boost.");
                }
                else if (sat >= 50f)
                {
                    __instance.AddMoodle(4, "happy", "Satiating Petting", "This expie is starting to get tired of being petted, reducing the comforting effect.");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Body), "HandleVisuals", new System.Type[] { typeof(Painkillers) })]
    public static class HandleVisualsPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Body __instance, out Limb[]? __state)
        {
            __state = null;
            if (__instance.standing && __instance.limbs != null)
            {
                // Check if any limb is currently simulated (dragged)
                bool hasSimulatedLimbs = false;
                foreach (Limb limb in __instance.limbs)
                {
                    if (limb != null && limb.rb != null && limb.rb.simulated)
                    {
                        hasSimulatedLimbs = true;
                        break;
                    }
                }

                if (hasSimulatedLimbs)
                {
                    // Save the original limbs array so we can restore it in the Postfix
                    __state = __instance.limbs;

                    // Filter out any simulated (dragged) limbs so HandleVisuals doesn't reset their transforms
                    System.Collections.Generic.List<Limb> filteredLimbs = new System.Collections.Generic.List<Limb>();
                    foreach (Limb limb in __instance.limbs)
                    {
                        if (limb == null)
                        {
                            continue;
                        }
                        if (limb.rb != null && limb.rb.simulated)
                        {
                            continue;
                        }
                        filteredLimbs.Add(limb);
                    }

                    __instance.limbs = filteredLimbs.ToArray();
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Body __instance, Limb[]? __state)
        {
            // Restore the original limbs array if it was modified
            if (__state != null)
            {
                __instance.limbs = __state;
            }

            // Sync the joint anchors to the newly updated parent visual positions
            if (ExpiePettingController.Instance != null)
            {
                ExpiePettingController.Instance.UpdateJointAnchors();
            }

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

