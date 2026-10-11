using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace Rephysicalized.Content.Animal_patches.Creatures
{
    // Harmony patch to change Pacu seed diet producedConversionRate from 3f to 1f
    [HarmonyPatch(typeof(BasePacuConfig), "SeedDiet", new System.Type[] { typeof(Tag), typeof(float), typeof(float) })]
    public static class PacuSeedDietPatch
    {
        // Prefix runs before BasePacuConfig.SeedDiet and forces the producedConversionRate to 1f
        static void Prefix(ref float producedConversionRate)
        {
            producedConversionRate = 0f;
        }
    }


}

