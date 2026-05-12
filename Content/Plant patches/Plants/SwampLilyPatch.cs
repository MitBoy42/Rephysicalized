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
    // - set Crop amount to 10 (Codex)
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

        private static bool TrySetCropComponentAmount(Crop crop, int desired)
        {
           
            var cropValField = AccessTools.Field(typeof(Crop), "cropVal");
            var boxed = cropValField.GetValue(crop);
            var cvt = boxed.GetType();

            var countField =
                AccessTools.Field(cvt, "numProduced") ??
                AccessTools.Field(cvt, "amount") ??
                AccessTools.Field(cvt, "count") ??
                AccessTools.Field(cvt, "quantity");

            object valueToSet =
                countField.FieldType == typeof(int) ? desired
                : (countField.FieldType == typeof(float) || countField.FieldType == typeof(double))
                    ? Convert.ChangeType((double)desired, countField.FieldType)
                    : null;

            countField.SetValue(boxed, valueToSet);
            cropValField.SetValue(crop, boxed); // write back boxed struct
            return true;
        }
    }

    
 
}