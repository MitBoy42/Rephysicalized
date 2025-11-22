using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;
namespace Rephysicalized
{
    [HarmonyPatch(typeof(MechanicalSurfboardConfig), "ConfigureBuildingTemplate")]
    public static class MechanicalSurfboardConfig_MassPreservation
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            MechanicalSurfboard mechanicalSurfboard = go.AddOrGet<MechanicalSurfboard>();
            mechanicalSurfboard.waterSpillRateKG = 2f;
        }
    }


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
                    instruction.operand = 0.25f;
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(WaterCooler), "AddRequirementDesc")]
    public static class WaterCooler_UI_Amount_Patch
    {
        static void Prefix(ref float mass)
        {
            // Ensure UI lists 0.25 kg consumed per use instead of 1 kg
            mass = 0.25f;
        }
    }


    // Patch the type initializer (.cctor) of TableSaltTuning so we can override defaults safely.
    [HarmonyPatch(typeof(TableSaltTuning))]
    internal static class TableSaltTuningPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.StaticConstructor)]
        private static void Postfix()
        {
            try
            {
                // Multiply the storage mass by 3
                TableSaltTuning.SALTSHAKERSTORAGEMASS *= 3f;

                // Set consumable rate to storage mass divided by 15
                TableSaltTuning.CONSUMABLE_RATE = TableSaltTuning.SALTSHAKERSTORAGEMASS / 15f;

                Debug.Log($"[AdvancedCraftingTweaks] TableSaltTuning adjusted: storage={TableSaltTuning.SALTSHAKERSTORAGEMASS}, rate={TableSaltTuning.CONSUMABLE_RATE}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdvancedCraftingTweaks] Failed to adjust TableSaltTuning: {ex}");
            }
        }
    }

    // Minimal patch: set SunLamp power draw to 480W
    [HarmonyPatch(typeof(SunLampConfig), nameof(SunLampConfig.CreateBuildingDef))]
    internal static class SunLamp_Energy_Patch
    {
        private static void Postfix(ref BuildingDef __result)
        {
            if (__result != null)
            {
                __result.EnergyConsumptionWhenActive = 480f;
            }
        }
    }

}



    

