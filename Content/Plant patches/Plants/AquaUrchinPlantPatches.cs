using System;
using System.Collections.Generic;
using System.Text;


using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches.AquaPlants
{
    internal static class AquaUrchinPlantPatches
    {
        // Vanilla: 0.008333334f. Target: 0.01f
    

        [HarmonyPatch(typeof(UrchinPlantConfig), nameof(UrchinPlantConfig.CreatePrefab))]
        private static class UrchinPlantConfig_CreatePrefab_Patch
        {
            private static void Postfix(ref GameObject __result)
            {
             
                EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = UrchinPlantConfig.FertilizerElement.CreateTag(),
                        massConsumptionRate = 0.01f
                    }
                });
            }
        }
    }
}

