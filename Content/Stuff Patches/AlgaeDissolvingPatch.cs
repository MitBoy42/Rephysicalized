using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Stuff_Patches
{
    [HarmonyPatch(typeof(DissolvingElementDiseaseEmitter), nameof(DissolvingElementDiseaseEmitter.OnSpawn))]
    internal static class AlgaeDissolvingPatch
    {
        private static void Postfix(DissolvingElementDiseaseEmitter __instance)
        {
            if (__instance == null)
                return;

            __instance.massConversionRatio = 1f;
        }
    }

    [HarmonyPatch(typeof(DissolvingElementDiseaseEmitter), "Update")]
    internal static class StoredAlgaeDissolvingPatch
    {
        private static bool Prefix(DissolvingElementDiseaseEmitter __instance)
        {
            return __instance != null && !__instance.gameObject.HasTag(GameTags.Stored);
        }
    }
}

