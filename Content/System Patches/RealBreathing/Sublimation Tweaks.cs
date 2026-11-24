using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches.RealBreathing
{
    internal class Sublimation_Tweaks
    {
        [HarmonyPatch(typeof(Sublimates), "OnSpawn")]
        internal static class BreathingTweaks_Sublimates_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(Sublimates __instance)
            {
                if (!Config.Instance.RealBreathing)
                    return;

                var info = __instance.info;
                info.sublimationRate *= 0.02f;
                __instance.info = info;
            }
        }

        [HarmonyPatch(typeof(SlimeMoldConfig), "CreatePrefab")]
        internal static class SlimeMoldConfig_RemoveSublimation_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(GameObject __result)
            {
                if (!Config.Instance.RealBreathing)
                    return;

                var sublimates = __result.GetComponent<Sublimates>();
                if (sublimates != null)
                    UnityEngine.Object.Destroy(sublimates);
            }
        }

        [HarmonyPatch(typeof(ToxicSandConfig), "CreatePrefab")]
        internal static class ToxicSandConfig_RemoveSublimation_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(GameObject __result)
            {
                if (!Config.Instance.RealBreathing)
                    return;

                var sublimates = __result.GetComponent<Sublimates>();
                if (sublimates != null)
                    UnityEngine.Object.Destroy(sublimates);
            }
        }
    }
}
