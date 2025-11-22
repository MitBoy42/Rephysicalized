using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Rephysicalized
{ // Patch goals:  - Moo: ~100 kg/cycle GasGrass + 100 kg/cycle PlantFiber; Milk 90 kg/cycle -> capacity 360 over 4 cycles // - DieselMoo: ~200 kg/cycle GasGrass + 200 kg/cycle PlantFiber; Diesel 190 kg/cycle -> capacity 760 over 4 cycles // - Keep gas output stable per entry by capping producedConversionRate so each entry alone yields 10 kg/cycle // // Implementation: // - On Moo/DieselMoo prefab creation: set MilkProductionMonitor.Def capacity and element. // - On BaseMooConfig.SetupBaseDiet: rebuild diet per species so mass/cycle matches targets by tuning caloriesPerKg, // and set producedConversionRate so each entry yields 10 kg/cycle when eaten to target.
    internal static class MooAndDieselMooPatch
    {
        private const float Moo_GasGrassPerCycle = 100f;
        private const float Diesel_GasGrassPerCycle = 200f; // kg per cycle target

        // PlantFiber targets (Option A = mirror GasGrass targets)
        private const float Moo_PlantFiberPerCycle = 100f;   // kg per cycle target
        private const float Diesel_PlantFiberPerCycle = 200f;

        private const float Moo_MilkPerCycle = 90f;          // kg per cycle -> 360 over 4 cycles
        private const float Diesel_PerCycle = 190f;          // kg per cycle -> 760 over 4 cycles

        private const float PoopPerCycleTarget = 9f;        // kg/cycle per diet entry when eaten to target

        // 1) Moo prefab: set MilkProductionMonitor capacity and element
        [HarmonyPatch(typeof(MooConfig), nameof(MooConfig.CreateMoo))]
        private static class MooConfig_CreateMoo_CapacityPatch
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result == null) return;

                var milk = __result.GetDef<MilkProductionMonitor.Def>();
                if (milk != null)
                {
                    float cycles = MooTuning.CYCLES_UNTIL_MILKING;
                    if (cycles <= 0f) cycles = 4f;

                    milk.element = SimHashes.Milk;
                    milk.Capacity = Moo_MilkPerCycle * cycles; // 90*4 = 360
                                                               // Keep CaloriesPerCycle as MooTuning.WELLFED_CALORIES_PER_CYCLE
                }
            }
        }

        // 2) DieselMoo prefab: set MilkProductionMonitor capacity and element
        [HarmonyPatch(typeof(DieselMooConfig), nameof(DieselMooConfig.CreateMoo))]
        private static class DieselMooConfig_CreateMoo_CapacityPatch
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result == null) return;

                var milk = __result.GetDef<MilkProductionMonitor.Def>();
                if (milk != null)
                {
                    float cycles = MooTuning.CYCLES_UNTIL_MILKING;
                    if (cycles <= 0f) cycles = 4f;

                    milk.element = DieselMooConfig.MILK_ELEMENT; // RefinedLipid
                    milk.Capacity = Diesel_PerCycle * cycles;     // 190*4 = 760
                }
            }
        }

        // 3) Rebuild diets per species to deliver target kg/cycle for GasGrass and PlantFiber,
        //    and set producedConversionRate so each entry by itself produces 10 kg/cycle of gas.
        [HarmonyPatch(typeof(BaseMooConfig), nameof(BaseMooConfig.SetupBaseDiet))]
        private static class BaseMooConfig_SetupBaseDiet_Rebuild
        {
            private static void Postfix(GameObject prefab, Tag producedTag)
            {
                try
                {
                    if (prefab == null) return;

                    var id = prefab.GetComponent<KPrefabID>();
                    if (id == null) return;

                    bool isDiesel = id.PrefabID().Name == DieselMooConfig.ID;

                    float stdCals = MooTuning.STANDARD_CALORIES_PER_CYCLE;

                    // Targets per species
                    float targetGasGrassKg = isDiesel ? Diesel_GasGrassPerCycle : Moo_GasGrassPerCycle;
                    float targetPlantFiberKg = isDiesel ? Diesel_PlantFiberPerCycle : Moo_PlantFiberPerCycle;

                    // Calories per kg tuned to meet targets: calPerKg = stdCals / targetKgPerCycle
                    float gasGrassCaloriesPerKg = SafeCalPerKg(stdCals, targetGasGrassKg);
                    float plantFiberCaloriesPerKg = SafeCalPerKg(stdCals, targetPlantFiberKg);

                    // Poop conversion tuned so each entry yields PoopPerCycleTarget when eaten to target
                    float gasGrassPoopConv = PoopPerCycleTarget / Math.Max(1f, targetGasGrassKg);
                    float plantFiberPoopConv = PoopPerCycleTarget / Math.Max(1f, targetPlantFiberKg);

                    Diet diet = null;

                    // GasGrass (EatPlantDirectly)
                    diet = ExpandDietCompat(
                        diet,
                        prefab,
                        "GasGrass".ToTag(),
                        producedTag,
                        gasGrassCaloriesPerKg,
                        gasGrassPoopConv,
                        Diet.Info.FoodType.EatPlantDirectly,
                        MooTuning.MIN_POOP_SIZE_IN_KG
                    );

                    // PlantFiber (EatSolid)
                    diet = ExpandDietCompat(
                        diet,
                        prefab,
                        "PlantFiber".ToTag(),
                        producedTag,
                        plantFiberCaloriesPerKg,
                        plantFiberPoopConv,
                        Diet.Info.FoodType.EatSolid,
                        MooTuning.MIN_POOP_SIZE_IN_KG
                    );

                    var calMon = prefab.AddOrGetDef<CreatureCalorieMonitor.Def>();
                    calMon.diet = diet;
                    calMon.minConsumedCaloriesBeforePooping = MooTuning.MIN_POOP_SIZE_IN_CALORIES;

                    var solidMon = prefab.AddOrGetDef<SolidConsumerMonitor.Def>();
                    solidMon.diet = diet;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Rephysicalized][MooDiet] Failed to rebuild diet: {e}");
                }
            }

            private static float SafeCalPerKg(float stdCals, float targetKgPerCycle)
            {
                return Mathf.Max(0.001f, stdCals / Mathf.Max(1f, targetKgPerCycle));
            }

            // Copied-compatible helper (avoids needing internal context)
            private static Diet ExpandDietCompat(
                Diet diet,
                GameObject prefab,
                Tag consumed_tag,
                Tag producedTag,
                float caloriesPerKg,
                float producedConversionRate,
                Diet.Info.FoodType foodType,
                float minPoopSizeInKg)
            {
                var consumed_tags = new HashSet<Tag> { consumed_tag };
                var infos = diet != null ? new Diet.Info[diet.infos.Length + 1] : new Diet.Info[1];
                if (diet != null)
                {
                    for (int i = 0; i < diet.infos.Length; i++)
                        infos[i] = diet.infos[i];
                }
                infos[infos.Length - 1] = new Diet.Info(consumed_tags, producedTag, caloriesPerKg, producedConversionRate, food_type: foodType);
                return new Diet(infos);
            }
        }
    }
}