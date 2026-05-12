using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;
using UtilLibs;
using static STRINGS.ITEMS.INDUSTRIAL_PRODUCTS;

namespace Rephysicalized
{
[HarmonyPatch(typeof(BaseHatchConfig), nameof(BaseHatchConfig.VeggieDiet))]
public static class HatchVeggie_AshByproductPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref List<Diet.Info> __result)
    {
        __result.Add(new Diet.Info(
            new HashSet<Tag> { ModElementRegistration.AshByproduct.Tag, TagManager.Create("PlantFiber") },  // Fix tag
            SimHashes.Carbon.CreateTag(),  // or desired poop
            HatchVeggieConfig.CALORIES_PER_KG_OF_ORE,
            TUNING.CREATURES.CONVERSION_EFFICIENCY.GOOD_3,  // adjust as needed
            null, 0f
        ));
    }
}

    // Postfix: scale HatchVeggie conversion efficiency without referencing TUNING.
    [HarmonyPatch(typeof(HatchVeggieConfig), nameof(HatchVeggieConfig.CreatePrefab))]
    public static class HatchVeggie_AdjustConversionEfficiency_Postfix
    {
        private static bool _applied;

        [HarmonyPostfix]
        public static void Postfix(GameObject __result)
        {
            if (_applied || __result == null) return;


            const float factor = 0.5f;

            var calDef = __result.GetDef<CreatureCalorieMonitor.Def>();
            var solidDef = __result.GetDef<SolidConsumerMonitor.Def>();

            if (calDef?.diet?.infos == null || calDef.diet.infos.Length == 0)
                return;

            var oldInfos = calDef.diet.infos;
            var newInfos = new List<Diet.Info>(oldInfos.Length);

            foreach (var info in oldInfos)
            {
                // Clone consumed tags so we don't mutate shared sets
                var consumed = new HashSet<Tag>(info.consumedTags);

                // Rebuild Diet.Info with producedConversionRate scaled by factor
                var newInfo = new Diet.Info(
                    consumed,
                    info.producedElement,
                    info.caloriesPerKg,
                    info.producedConversionRate * factor,
                    disease_per_kg_produced: info.diseasePerKgProduced,
                    produce_solid_tile: info.produceSolidTile,
                    food_type: info.foodType,
                    emmit_disease_on_cell: info.emmitDiseaseOnCell,
                    eat_anims: info.eatAnims
                );

                newInfos.Add(newInfo);
            }

            var newDiet = new Diet(newInfos.ToArray());
            calDef.diet = newDiet;

            if (solidDef != null)
                solidDef.diet = newDiet;

            if (calDef.minConsumedCaloriesBeforePooping > 0f)
                calDef.minConsumedCaloriesBeforePooping /= factor;

            _applied = true;

        }
    }

    [HarmonyPatch(typeof(HatchHardConfig), nameof(HatchHardConfig.CreateHatch))]
    internal static class HatchHardConfig_CreateHatch_MetalEfficiency_Normal_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var badField = AccessTools.Field(typeof(CREATURES.CONVERSION_EFFICIENCY), nameof(CREATURES.CONVERSION_EFFICIENCY.BAD_1));
            var normalField = AccessTools.Field(typeof(CREATURES.CONVERSION_EFFICIENCY), nameof(CREATURES.CONVERSION_EFFICIENCY.NORMAL));

            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Ldsfld && ci.operand != null && ci.operand.Equals(badField))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, normalField);
                    continue;
                }

                yield return ci;
            }
        }

    }



     
    }
