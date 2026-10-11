using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;
using Rephysicalized.Content.Animal_patches;

namespace Rephysicalized
{

    // Diet tuning
    [HarmonyPatch(typeof(BaseDeerConfig), nameof(BaseDeerConfig.SetupDiet))]
    public static class WoodDeer_Diet_Tuning_Patch
    {

        private const float CaloriesPerCycle = 100_000f;

        public static void Prefix(GameObject prefab, Diet.Info[] diet_infos, float minPoopSizeInKg)
        {

            for (int i = 0; i < diet_infos.Length; i++)
            {
                var info = diet_infos[i];
                if (info.consumedTags == null || info.consumedTags.Count == 0)
                    continue;

                bool isHardSkinPlant = info.consumedTags.Contains("HardSkinBerryPlant");
                bool isPricklePlant = info.consumedTags.Contains("PrickleFlower");
                bool isHardSkinItem = info.consumedTags.Contains("HardSkinBerry");
                bool isPrickleItem = info.consumedTags.Contains("PrickleFruit");
                bool isKatairiteItem = info.consumedTags.Contains("Katairite");

                if (isHardSkinPlant)
                {
                    float originalCaloriesPerKg = info.caloriesPerKg > 0f ? info.caloriesPerKg : CaloriesPerCycle;
                    float originalProducedConversion = info.producedConversionRate;

                    float originalKgPerCycle = CaloriesPerCycle / originalCaloriesPerKg;
                    float targetKgPerCycle = 3f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;

                    float newProducedConversion = originalProducedConversion * 0.4f * (originalKgPerCycle / targetKgPerCycle);
                    info.producedConversionRate = newProducedConversion;

                    diet_infos[i] = info;
                    continue;
                }

                if (isPricklePlant)
                {
                    float targetKgPerCycle = 12f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 11f / 12f;
                    diet_infos[i] = info;
                    continue;
                }

                if (isHardSkinItem)
                {
                    info.producedConversionRate = 0f;
                    diet_infos[i] = info;
                    continue;

                }
                if (isPrickleItem)
                {
                    float targetKgPerCycle = 0.5f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0f;
                    diet_infos[i] = info;
                    continue;

                }

            }
        }
    }

    
    [HarmonyPatch(typeof(BaseDeerConfig), nameof(BaseDeerConfig.BaseDeer))]
    internal static class WoodDeerConfig_CreateWoodDeer_SolidFuelChoreInjection
    {
        // Helper called by injected IL to add our state to the builder.
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SolidFuelStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);

            var popInterrupt = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.PopInterruptGroup));
            var inject = AccessTools.Method(typeof(WoodDeerConfig_CreateWoodDeer_SolidFuelChoreInjection), nameof(Inject));

            if (popInterrupt == null || inject == null)
                return list;

            // Insert our Inject call immediately before the last PopInterruptGroup() call.
            // This mirrors prior behavior used on other creatures.
            int lastPopIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(popInterrupt))
                    lastPopIdx = i;
            }

            if (lastPopIdx >= 0)
            {
                list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
            }

            return list;
        }
    }
    // Inject SolidFuelStates.Def into Deer's ChoreTable build chain 
    [HarmonyPatch(typeof(WoodDeerConfig), nameof(WoodDeerConfig.CreateWoodDeer))]
    internal static class Deer_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {

            __result.AddOrGet<KSelectable>();

            var diet = BuildDeerFueledDiet();
            var controller = __result.AddOrGet<FueledDietController>();
            ConfigureFueledDiet(controller, diet);
            controller.RefillThreshold = 180f; // adjust as desired (kg)


            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
            monitor.scanWindowCells = 16;
            monitor.minPickupKg = 0.01f;
            monitor.navigatorSize = new Vector2(2f, 2f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };

        __result.AddOrGet<WoodDeerAntlerFuelController>();

            var kpid = __result.GetComponent<KPrefabID>();
           
         FueledDietRegistry.Register(kpid.PrefabTag, diet); }
           
        private static FueledDiet BuildDeerFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Ice.CreateTag()),
                new FuelInput(SimHashes.DirtyIce.CreateTag()),
                new FuelInput(SimHashes.BrineIce.CreateTag()),
                new FuelInput(SimHashes.CrushedIce.CreateTag()),
                new FuelInput(SimHashes.MurkyBrineIce.CreateTag()),
            };


            return new FueledDiet(
                fuelInputs: inputs,
                conversions: null,
                totalFuelCapacityKg: 240f,
                allowBlendedInputByOutput: true
            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
           controller.Configure(diet); 
        }
    }
    // WoodDeer: antler growth is paid for with fuel; antler mass at 100% is 360kg.
    public sealed class WoodDeerAntlerFuelController : ScaleFuelGrowthController
    {
        protected override float FullScaleMassKg => 360f;
    }
}