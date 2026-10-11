using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Rephysicalized
{
    // Patch 1: After the SwampLily prefab is created:
    // - add salt fertilizer requirement
    [HarmonyPatch(typeof(SwampLilyConfig), nameof(SwampLilyConfig.CreatePrefab))]
    public static class SwampLily_CreatePrefab_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            if (OrganicOverhaulIntegration.IsPresent())
                return; // let OrganicOverhaul handle it


            // Re-add fertilizer: consume Salt at 10 kg/cycle == 0.01666667 kg/s
            EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Salt.CreateTag(),
                        massConsumptionRate = 0.01666667f
                    }
                });

               
                      
        }

    }

    
 
}