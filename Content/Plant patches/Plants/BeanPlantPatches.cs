﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;


namespace Rephysicalized
{
    [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
    public static class BeanPlantConfig_CreatePrefab_Patch
    {
        private const string BeanPlantCropId = "BeanPlantSeed";

        [HarmonyPostfix]
        public static void Postfix(GameObject __result)
        {
            if (__result == null)
                return;

            if (!__result.TryGetComponent<PlantElementAbsorber>(out var absorber))
                return;

            bool isBeanPlant = false;
            if (__result.name != null && __result.name.Contains("BeanPlant", StringComparison.OrdinalIgnoreCase))
                isBeanPlant = true;

            if (!isBeanPlant)
                return;

            EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
            {
                new PlantElementAbsorber.ConsumeInfo
                {
                    tag = ModTags.RichSoil,
                    massConsumptionRate = BeanPlantConfig.FERTILIZATION_RATE
                }
            });
        }

        // Replace IL transpiler for safe_elements: append Methane for BeanPlant only.
        [HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.ExtendEntityToBasicPlant))]
        internal static class EntityTemplates_ExtendEntityToBasicPlant_AppendMethane_Patch
        {
            private static readonly string CropId = BeanPlantCropId;

            private static bool IsBeanPlant(GameObject template, string crop_id)
            {
          
                return template != null && template.name != null && template.name.Contains("BeanPlant", StringComparison.OrdinalIgnoreCase);
            }

            public static void Prefix(GameObject template, ref SimHashes[] safe_elements, string crop_id)
            {
                if (!IsBeanPlant(template, crop_id))
                    return;
   
                    safe_elements = safe_elements.Append(SimHashes.Methane).ToArray();
            }
        }
    }
}

