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

using Klei;
using Klei.AI;
using System.Collections.Generic;
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

    [HarmonyPatch(typeof(EspressoMachineWorkable), nameof(EspressoMachineWorkable.OnCompleteWork))]
    public static class EspressoMachineWarmTouchFoodPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EspressoMachineWorkable __instance, WorkerBase worker)
        {
            var effects = worker.GetComponent<Effects>();

            var effect = effects.Add("WarmTouchFood", true);
            effect.timeRemaining = 600f;
        }
    }
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class EspressoMachine_ResourcePerUse_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try { EspressoMachine.INGREDIENT_MASS_PER_USE = 0.5f; } catch { }
            try { EspressoMachine.WATER_MASS_PER_USE = 0.5f; } catch { }
        }
    }


    [HarmonyPatch(typeof(JuicerWorkable), nameof(JuicerWorkable.OnCompleteWork))]
    public static class JuicerRefreshingTouchPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EspressoMachineWorkable __instance, WorkerBase worker)
        {
            var effects = worker.GetComponent<Effects>();

            var effect = effects.Add("RefreshingTouch", true);
            effect.timeRemaining = 600f;
        }
    }
    [HarmonyPatch(typeof(SodaFountainWorkable), nameof(SodaFountainWorkable.OnCompleteWork))]
    public static class SodaFountainRefreshingTouchPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EspressoMachineWorkable __instance, WorkerBase worker)
        {
            var effects = worker.GetComponent<Effects>();

            var effect = effects.Add("RefreshingTouch", true);
            effect.timeRemaining = 300f;
        }
    }


    [HarmonyPatch(typeof(JuicerConfig), nameof(JuicerConfig.ConfigureBuildingTemplate))]
    public static class JuicerConfig_ConfigureBuildingTemplate_Use300kPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var juicer = go.GetComponent<Juicer>();
            // Order matches vanilla: [Mushroom, PrickleFruit (Berry), BasicPlantFood (Lice)]
            var fMush = EdiblesManager.GetFoodInfo(MushroomConfig.ID);
            var fBerry = EdiblesManager.GetFoodInfo(PrickleFruitConfig.ID);
            var fLice = EdiblesManager.GetFoodInfo("BasicPlantFood");



            // Desired calories per drink
            const float MUSHROOM_CALS = 300000f; // vanilla unchanged
            const float BERRY_CALS = 300000f;    // changed from 600000f -> 300000f
            const float LICE_CALS = 300000f;     // changed from 500000f -> 300000f

            // Recompute per-use masses based on new calorie targets
            var masses = new float[3];
            masses[0] = MUSHROOM_CALS / fMush.CaloriesPerUnit;
            masses[1] = BERRY_CALS / fBerry.CaloriesPerUnit;
            masses[2] = LICE_CALS / fLice.CaloriesPerUnit;

            juicer.ingredientMassesPerUse = masses;


        }


        [HarmonyPatch(typeof(JuicerConfig), nameof(JuicerConfig.CreateBuildingDef))]
        public static class JuicerConfig_CreateBuildingDef_ConstAlignPatch
        {
            public static void Postfix()
            {

                var t = typeof(JuicerConfig);
                var fBerry = AccessTools.Field(t, "BERRY_CALS");
                var fLice = AccessTools.Field(t, "LICE_CALS");
                if (fBerry != null && !fBerry.IsLiteral && !fBerry.IsInitOnly) fBerry.SetValue(null, 300000f);
                if (fLice != null && !fLice.IsLiteral && !fLice.IsInitOnly) fLice.SetValue(null, 300000f);

            }
        }
    }
}



