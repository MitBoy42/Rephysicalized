using HarmonyLib;
using Klei.AI;
using Rephysicalized.Chores;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static STRINGS.CREATURES.SPECIES;

namespace Rephysicalized
{

    // Inject SolidFuelStates.Def into ChoreTable build chain, before the last PopInterruptGroup.
    [HarmonyPatch(typeof(BaseOilFloaterConfig), nameof(BaseOilFloaterConfig.BaseOilFloater))]
    internal static class BaseOilFloaterConfig_SolidFuelChoreInjection
    {
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SolidFuelStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            var pop = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.PopInterruptGroup));
            var inject = AccessTools.Method(typeof(BaseOilFloaterConfig_SolidFuelChoreInjection), nameof(Inject));

            if (pop != null && inject != null)
            {
                int lastPopIdx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Calls(pop))
                        lastPopIdx = i;
                }

                if (lastPopIdx >= 0)
                    list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
            }
            return list;
        }
    }

    [HarmonyPatch(typeof(BaseOilFloaterConfig), nameof(BaseOilFloaterConfig.BaseOilFloater))]
    internal static class OilFloater_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
            if (__result == null) return;

            __result.AddOrGet<KSelectable>();

            // Keep vanilla edible diet
            var cal = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
            if (cal != null && cal.minConsumedCaloriesBeforePooping < 0f)
                cal.minConsumedCaloriesBeforePooping = 0f;

            // Ensure SolidConsumer diet is not overridden here
            var solidConsumer = __result.GetDef<SolidConsumerMonitor.Def>();
            if (solidConsumer != null)
                solidConsumer.diet = null;

            // Decide which FueledDiet to attach based on prefab id/tag
            var kpid = __result.GetComponent<KPrefabID>();
            Tag prefabTag = kpid != null ? kpid.PrefabTag : Tag.Invalid;
            var isDecor = prefabTag == TagManager.Create("OilFloaterDecor"); if (isDecor) return;
            bool isOilFLoaterHighTemp = prefabTag == TagManager.Create("OilFloaterHighTemp");

            FueledDiet diet = isOilFLoaterHighTemp
                ? BuildOilFloaterHighTempFueledDiet()
                : BuildOilFloaterFueledDiet();

            var controller = __result.AddOrGet<FueledDietController>();
            controller.RefillThreshold = 40f;
            ConfigureFueledDiet(controller, diet);

            // Solid fuel monitor drives the SOLIDFUEL chore/state
            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
            monitor.navigatorSize = new Vector2(1f, 1f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };

            if (kpid != null)
            {
                FueledDietRegistry.Register(kpid.PrefabTag, diet);
            }
        }
       
        private static FueledDiet BuildOilFloaterFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
           new FuelInput("PlantFiber"),
   new FuelInput(SimHashes.WoodLog.CreateTag()),
                new FuelInput(SimHashes.ToxicMud.CreateTag()),
                new FuelInput(SimHashes.Peat.CreateTag()),
                new FuelInput(SimHashes.Carbon.CreateTag()),
            };


            const float kgOutputPerKgInput = 1.0f;
            var output = SimHashes.CrudeOil.CreateTag();

            var conversions = new List<FuelConversion>(inputs.Count);
            foreach (var fi in inputs)
            {
                conversions.Add(new FuelConversion(
                    inputTag: fi.ElementTag,
                    outputTag: output,
                    kgInputPerKgMain: 1f,
                    kgOutputPerKgInput: kgOutputPerKgInput,
                    outputTemperatureOverrideKelvin: 0f));
            }

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: conversions,
                totalFuelCapacityKg: 60f,
                allowBlendedInputByOutput: true

            );
        }

        private static FueledDiet BuildOilFloaterHighTempFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                  new FuelInput("PlantFiber"),
                    new FuelInput(SimHashes.WoodLog.CreateTag()),

                new FuelInput(SimHashes.ToxicMud.CreateTag()),
                new FuelInput(SimHashes.Peat.CreateTag()),
                new FuelInput(SimHashes.Carbon.CreateTag()),
              
            };

            const float baseKgInputPerKgMain = 1f;
            const float kgOutputPerKgInput = 1.0f;
            var output = SimHashes.Petroleum.CreateTag();

            var conversions = new List<FuelConversion>(inputs.Count);
            foreach (var fi in inputs)
            {
                conversions.Add(new FuelConversion(
                    inputTag: fi.ElementTag,
                    outputTag: output,
                    kgInputPerKgMain: baseKgInputPerKgMain,
                    kgOutputPerKgInput: kgOutputPerKgInput,
                    outputTemperatureOverrideKelvin: 0f));
            }

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: conversions,
                totalFuelCapacityKg: 60f,
                allowBlendedInputByOutput: true

            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
            try { controller.Configure(diet); } catch { }
        }
    }


    [HarmonyPatch(typeof(OilFloaterConfig), nameof(OilFloaterConfig.CreateOilFloater))]
    internal static class OilFloater_CreateOilFloater_Postfix
    {
        // Postfix signature matches the decompiled method: last statement assigns 'go' from SetupDiet and returns it.
        // We replace the result by calling SetupDiet again with our desired parameters.
        private const float EfficiencyMul = 1.9f;
        private const float NewMinPoopKg = 10f;

        [HarmonyPostfix]
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            // Re-run SetupDiet with adjusted args
            // Original: SetupDiet(prefab, CO2, CrudeOil, CALORIES_PER_KG_OF_ORE, TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL, null, 0f, MIN_POOP_SIZE_IN_KG)
            // Changes:
            // - Emit: ModElementRegistration.CrudByproduct
            // - Efficiency *= 1.9
            // - Min poop = 10f
            var prefab = __result;
            var consume = SimHashes.CarbonDioxide.CreateTag();
            var emit = ModElementRegistration.CrudByproduct.CreateTag(); ;
            float caloriesPerKg = AccessTools.StaticFieldRefAccess<float>(typeof(OilFloaterConfig), "CALORIES_PER_KG_OF_ORE");
            float efficiency = TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL * EfficiencyMul;
            string diseaseId = null;
            float diseasePerKg = 0f;
            float minPoop = NewMinPoopKg;

            __result = BaseOilFloaterConfig.SetupDiet(prefab, consume, emit, caloriesPerKg, efficiency, diseaseId, diseasePerKg, minPoop);
        }
    }

    [HarmonyPatch(typeof(OilFloaterHighTempConfig), nameof(OilFloaterHighTempConfig.CreateOilFloater))]
    internal static class OilFloaterHighTemp_CreateOilFloater_Postfix
    {
        private const float EfficiencyMul = 1.9f;
        private const float NewMinPoopKg = 10f;

        [HarmonyPostfix]
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            var prefab = __result;
            var consume = SimHashes.CarbonDioxide.CreateTag();
            var emit = ModElementRegistration.AshByproduct.CreateTag(); ;
            float caloriesPerKg = AccessTools.StaticFieldRefAccess<float>(typeof(OilFloaterConfig), "CALORIES_PER_KG_OF_ORE"); // same tuning base
            float efficiency = TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL * EfficiencyMul;
            string diseaseId = null;
            float diseasePerKg = 0f;
            float minPoop = NewMinPoopKg;

            __result = BaseOilFloaterConfig.SetupDiet(prefab, consume, emit, caloriesPerKg, efficiency, diseaseId, diseasePerKg, minPoop);
        }
    }

}