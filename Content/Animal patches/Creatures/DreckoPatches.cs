using Epic.OnlineServices;
using HarmonyLib;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs.UIcmp;
using static DeserializeWarnings;
using static ProcGen.Room;
using static STRINGS.BUILDING.STATUSITEMS.MEGABRAINTANK;
using static STRINGS.DUPLICANTS.PERSONALITIES;
using static STRINGS.RESEARCH.TECHS;

namespace Rephysicalized.Content.Animal_patches
{


    internal static class DreckoDietRebalance
    {
        private const float CaloriesPerDay = 2_000_000f;
        private const float KgConsumedPerDay = 11f;
        private const float KgPoopPerDay = 10f;

        private static float CaloriesPerKg => CaloriesPerDay / KgConsumedPerDay; // ~181_818.18
        private static float ProducedPerKg => KgPoopPerDay / KgConsumedPerDay;   // ~0.9090909

        // For min poop gate: calories needed to produce MinPoopKg
        private const float MinPoopKg = 1.5f; // vanilla Drecko
        private static float MinCaloriesBeforePoop => (MinPoopKg / ProducedPerKg) * CaloriesPerKg; // ~300_000

        // Direct plant tags (strings used to avoid hard dependency on *Config types at compile-time)
        private static readonly Tag SwampLilyTag = "SwampLily".ToTag();
        private static readonly Tag SpiceVineTag = "SpiceVine".ToTag();
        private static readonly Tag BasicSingleHarvestPlantTag = "BasicSingleHarvestPlant".ToTag();

        // Poop element (vanilla Drecko = Phosphorite)
        private static readonly Tag PoopTag = DreckoConfig.POOP_ELEMENT;

        private static Diet BuildDiet()
        {
            var plantTags = new HashSet<Tag>
            {
                SwampLilyTag,
                SpiceVineTag,
                BasicSingleHarvestPlantTag
            };

            // Eat plants directly; per-kg values must encode our daily targets
            var info = new Diet.Info(
                consumed_tags: plantTags,
                produced_element: PoopTag,
                calories_per_kg: CaloriesPerKg,
                produced_conversion_rate: ProducedPerKg,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,
                food_type: Diet.Info.FoodType.EatPlantDirectly,
                emmit_disease_on_cell: false,
                eat_anims: null
            );

            return new Diet(info);
        }

        private static void ApplyDiet(GameObject creature)
        {
            if (creature == null) return;

            var diet = BuildDiet();

            // Apply same Diet to both monitors
            var cal = creature.AddOrGetDef<CreatureCalorieMonitor.Def>();
            cal.diet = diet;
            cal.minConsumedCaloriesBeforePooping = MinCaloriesBeforePoop;

            var solid = creature.AddOrGetDef<SolidConsumerMonitor.Def>();
            solid.diet = diet;
        }

        // Patch for regular Drecko
        [HarmonyPatch(typeof(DreckoConfig), nameof(DreckoConfig.CreateDrecko))]
        private static class DreckoConfig_CreateDrecko_Postfix
        {
            // Do NOT add parameters that don't exist on the original method; we only need __result.
            public static void Postfix(ref GameObject __result)
            {
                ApplyDiet(__result);

            }
        }


    }

    // Part 1 (diet):
    // - Eats plant growth directly from BasicSingleHarvestPlant and PrickleFlower only.
    // - Must eat 30 kg growth per cycle, and produce 9 kg poop per cycle.
    //   => calories_per_kg = dailyCalories / 30
    //   => produced_conversion_rate = 9 / 30 = 0.3
    //
    // Part 2 (scales):
    // - 50 kg scale growth per cycle
    //
    // Part 3 (mass drain):
    // - As scales grow, effective mass decreases proportionally (no refund on shear).
    // - Drain rate is linked to configured "kg per cycle" derived from the Def:
    //   kgPerCycle = def.dropMass * def.defaultGrowthRate * 600.
    // - Only drains while progress increases; stops at mass floor; runs every 1000 ms.
    [HarmonyPatch(typeof(DreckoPlasticConfig), nameof(DreckoPlasticConfig.CreateDrecko))]
    internal static class DreckoPlasticConfig_CreateDrecko_Postfix
    {
        private const float TargetKgPlantPerCycle = 30f;   // 30 kg plant growth per cycle
        private const float TargetKgPoopPerCycle = 9f;     // 9 kg poop per cycle
        private const float MinMassFloorKg = 1f;           // effective mass floor
        private const float ScaleKgPerCycle = 60f;         // scale growth product mass per cycle
        private const float ManualDailyCalories = 2_000_000f;

        public static void Postfix(ref GameObject __result)
        {
            // -------- Part 1: Diet (plants only, per-kg tuning) --------
            float caloriesPerKg = ManualDailyCalories / TargetKgPlantPerCycle; // kcal per kg of plant growth
            float producedPerKg = TargetKgPoopPerCycle / TargetKgPlantPerCycle;

            var plantTags = new HashSet<Tag>
            {
                "BasicSingleHarvestPlant".ToTag(),
                "PrickleFlower".ToTag()
            };

            var dietInfo = new Diet.Info(
                consumed_tags: plantTags,
                produced_element: DreckoPlasticConfig.POOP_ELEMENT,
                calories_per_kg: caloriesPerKg,
                produced_conversion_rate: producedPerKg,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,
                food_type: Diet.Info.FoodType.EatPlantDirectly,
                emmit_disease_on_cell: false,
                eat_anims: null
            );

            var diet = new Diet(dietInfo);

            var calDef = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
            calDef.diet = diet;
            // Minimum calories before pooping to gate 1.5 kg minimum poop (vanilla)
            float minPoopKg = 1.5f;
            calDef.minConsumedCaloriesBeforePooping = (minPoopKg / producedPerKg) * caloriesPerKg;

            var solidDef = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
            solidDef.diet = diet;

            // -------- Part 2: Scales (ScaleKgPerCycle per cycle; full-scale mass) --------
            var scaleDef = __result.AddOrGetDef<ScaleGrowthMonitor.Def>();
            // defaultGrowthRate is per second; 600 seconds per cycle -> cycles to full = 1 / (rate * 600)
            float growthTimeInCycles = 1f / (scaleDef.defaultGrowthRate * 600f);
            // Full mass scales with growth time
            scaleDef.dropMass = ScaleKgPerCycle * growthTimeInCycles;

        }
    }


 
}