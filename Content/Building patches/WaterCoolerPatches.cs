using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using STRINGS;
namespace Rephysicalized.Content.Building_patches
{
    [HarmonyPatch(typeof(WaterCooler.StatesInstance), nameof(WaterCooler.StatesInstance.Drink))]
    public static class WaterCooler_Drink_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                // Replace the hardcoded 1f consumption with 0.25f
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float f && f == 1f)
                {
                    instruction.operand = 0.5f;
                }
                yield return instruction;
            }
        }
    }


    [HarmonyPatch(typeof(WaterCoolerConfig), nameof(WaterCoolerConfig.ConfigureBuildingTemplate))]
    public static class WaterCoolerRefillMassPatch
    {
        // Signature matches: void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        [HarmonyPostfix]
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null || go.Equals(null)) return;

            var delivery = go.GetComponent<ManualDeliveryKG>();
            if (delivery != null)
            {
                delivery.refillMass = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(WaterCooler), "AddRequirementDesc")]
    public static class WaterCooler_UI_Amount_Patch
    {
        static void Prefix(ref float mass)
        {
            mass = 0.5f;
        }
    }

  // Adds SugarWater drinking effect to WaterCooler.
    [HarmonyPatch]
    internal static class WaterCoolerPatches
    {
        private const string HadSugarwaterEffectId = "DuplicantDrankSugarWater";

        // Patch WaterCoolerConfig options to include SugarWater -> DuplicantDrankSugarWater
        [HarmonyPatch(typeof(WaterCoolerConfig), nameof(WaterCoolerConfig.ConfigureBuildingTemplate))]
        private static class WaterCoolerConfig_ConfigureBuildingTemplate_Patch
        {
            private static void Postfix(GameObject go)
            {
                    EnsureSugarwaterOption();
            }
        }

        private static void EnsureSugarwaterOption()
        {
            var arr = WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS;
            if (arr == null)
                return;

            // If already present, do nothing.
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].second == HadSugarwaterEffectId)
                    return;
            }

            var newArr = new Tuple<Tag, string>[arr.Length + 1];
            Array.Copy(arr, newArr, arr.Length);
            newArr[newArr.Length - 1] = new Tuple<Tag, string>(SimHashes.SugarWater.CreateTag(), HadSugarwaterEffectId);

        
                typeof(WaterCoolerConfig).GetField(nameof(WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.SetValue(null, newArr);
                       
        }

        // Add the modifier effect to ModifierSet.
        [HarmonyPatch(typeof(ModifierSet), "CreateCritteEffects")]
        public static void Postfix(ModifierSet __instance)
        {

            Effect effect = new Effect(
                    HadSugarwaterEffectId,
                    STRINGS.DUPLICANTS.DUPLICANTDRANKSUGARWATER.NAME,
                    STRINGS.DUPLICANTS.DUPLICANTDRANKSUGARWATER.TOOLTIP,
                    600f,
                    true,
                    true,
                    false);

                effect.Add(new AttributeModifier(Db.Get().Amounts.Stress.deltaAttribute.Id, -0.01666667f, STRINGS.DUPLICANTS.DUPLICANTDRANKSUGARWATER.NAME));
                effect.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, 1f, STRINGS.DUPLICANTS.DUPLICANTDRANKSUGARWATER.NAME));
                effect.Add(new AttributeModifier(Db.Get().Attributes.Athletics.Id, -1f, STRINGS.DUPLICANTS.DUPLICANTDRANKSUGARWATER.NAME));
            __instance.effects.Add(effect);
            }
        }
    }

