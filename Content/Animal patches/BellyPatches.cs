using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{

    // Diet tuning
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.SetupDiet))]
    public static class Belly_Diet_Tuning_Patch
    {
        private static readonly Tag CarrotPlantTag = new Tag("CarrotPlant");
        private static readonly Tag BeanPlantTag = new Tag("BeanPlant");

        // Prefer IDs/constants over literals if available. CarrotConfig.ID should be the prefab/tag ID.
        private static readonly Tag CarrotTag = new Tag(CarrotConfig.ID);
        private static readonly Tag BeanTag = new Tag("BeanPlantSeed");
        private static readonly Tag FriesTag = new Tag("FriesCarrot");

        // Total calories we target per cycle for the animal
        private const float CaloriesPerCycle = 1777777f;

        // IMPORTANT: Signature must match the original exactly for Harmony's default binding:
        // (GameObject prefab, List<Diet.Info> diet_infos, float referenceCaloriesPerKg, float minPoopSizeInKg)
        // Names matter here due to two floats; Harmony binds by name unless you use [HarmonyArgument].
        public static void Prefix(
            GameObject prefab,
            List<Diet.Info> diet_infos,
            float referenceCaloriesPerKg,
            float minPoopSizeInKg)
        {
            if (diet_infos == null || diet_infos.Count == 0)
                return;

            for (int i = 0; i < diet_infos.Count; i++)
            {
                var info = diet_infos[i];
                if (info == null)
                    continue;

                // consumedTags is often a collection (HashSet<Tag>) – guard before using
                if (info.consumedTags == null || info.consumedTags.Count == 0)
                    continue;

                bool isCarrotPlant = info.consumedTags.Contains(CarrotPlantTag);
                bool isBeanPlant = info.consumedTags.Contains(BeanPlantTag);
                bool isCarrot = info.consumedTags.Contains(CarrotTag);
                bool isBean = info.consumedTags.Contains(BeanTag);
                bool isFries = info.consumedTags.Contains(FriesTag);

                if (isCarrotPlant)
                {
                    float originalCaloriesPerKg = info.caloriesPerKg > 0f ? info.caloriesPerKg : referenceCaloriesPerKg > 0f ? referenceCaloriesPerKg : CaloriesPerCycle;
                    float originalProducedConversion = info.producedConversionRate;

                    float targetKgPerCycle = 30f;

                    // Re-tune calories per kg to hit the target mass consumption per cycle
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;

                    info.producedConversionRate = 0.9667f;

                    diet_infos[i] = info;
                    continue;
                }

                if (isBeanPlant)
                {
                    float targetKgPerCycle = 30f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0.9667f;
                    diet_infos[i] = info;
                    continue;
                }

                if (isBean || isFries)
                {
                    // Prevent seed/produce-eating from generating outputs if undesired
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
                if ( isCarrot)
                {
                    float targetKgPerCycle = 1.778f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    // Prevent seed/produce-eating from generating outputs if undesired
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
            }
        }
    }



    // Changes the onDeathDropCount: 14f inside BaseBelly(...) to 4f
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
    public static class BaseBellyMeatDropTranspiler
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - 14f) < 0.0001f)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 4f);
                    continue;
                }

                yield return instr;
            }
        }
    }

  
}
