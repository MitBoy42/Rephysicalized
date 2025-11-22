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

    // Drecko diet rebalance:
    // - Eats plants directly: SwampLily, SpiceVine, BasicSingleHarvestPlant
    // - No fruits/items in the diet at all
    // - Daily targets: 2,000,000 kcal eaten, 11 kg growth consumed, 10 kg feces (Phosphorite) produced
    //   => caloriesPerKg = 2_000_000 / 11
    //   => producedConversionRate = 10 / 11
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

            // -------- Part 3: Proportional mass drain derived from Def --------
            __result.AddOrGet<PlasticDreckoScaleMassDrain>()
                    .Initialize(floorKg: MinMassFloorKg);
        }
    }


    // Keep Drecko scale drain in the same file for now (null-safe and additive with other sources)
    public sealed class PlasticDreckoScaleMassDrain : KMonoBehaviour, ISim1000ms
    {
        [MyCmpReq] private PrimaryElement primaryElement;

        [Serialize] private float massFloorKg = 1f;        // do not drain below this effective mass
        [Serialize] private float drainedMassApplied;       // retained for save compatibility (no runtime use)
        [Serialize] private float lastScaleProgress = -1f;  // last observed scale progress [0..1]

        private Rephysicalized.CreatureMassTracker tracker;

        private static bool IsFiniteFloat(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public void Initialize(float floorKg)
        {
            massFloorKg = (IsFiniteFloat(floorKg) && floorKg >= 0f) ? floorKg : 1f;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            tracker = gameObject.GetComponent<Rephysicalized.CreatureMassTracker>();

            // Initialize progress baseline
            var smi = gameObject.GetSMI<ScaleGrowthMonitor.Instance>();
            if (smi != null && smi.scaleGrowth != null)
            {
                float max = smi.scaleGrowth.GetMax();
                if (max > 0f)
                {
                    float progress = Mathf.Clamp01(smi.scaleGrowth.value / max);
                    lastScaleProgress = progress;
                }
            }
        }

        public void Sim1000ms(float dt)
        {
            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            if (primaryElement == null)
                return;

            var smi = gameObject.GetSMI<ScaleGrowthMonitor.Instance>();
            if (smi == null || smi.scaleGrowth == null)
                return;

            float max = smi.scaleGrowth.GetMax();
            if (max <= 0f) return;

            float progress = Mathf.Clamp01(smi.scaleGrowth.value / max);
            const float EPS = 1e-6f;

            // First tick bootstrap
            if (lastScaleProgress < 0f)
                lastScaleProgress = progress;

            // If scales regressed (sheared or reduced), accept new baseline
            if (progress + EPS < lastScaleProgress)
            {
                lastScaleProgress = progress;
                return;
            }

            // No change -> nothing to do
            if (progress <= lastScaleProgress + EPS)
                return;

            // Calculate how much progress actually occurred since last tick
            float deltaProgress = progress - lastScaleProgress; // [0..1]
            float dropMass = GetDropMassConfigured();           // kg per full growth (progress 0 -> 1)
            float requestedDrainKg = Mathf.Max(0f, deltaProgress) * Mathf.Max(0f, dropMass);

            // Enforce mass floor using current mass
            float currentMass = GetCurrentMassSafe();
            float allowedByFloor = Mathf.Max(0f, currentMass - massFloorKg);

            // Determine how much mass we can drain and clamp progress accordingly
            float drainKg = Mathf.Min(requestedDrainKg, allowedByFloor);

            if (drainKg > EPS)
            {
                float newMass = Mathf.Max(massFloorKg, (primaryElement.Mass - drainKg));
                primaryElement.Mass = newMass;
                drainedMassApplied += drainKg;
            }

            // If we couldn't drain enough mass for the full growth that occurred, clamp progress
            if (drainKg + EPS < requestedDrainKg)
            {
                float allowedProgressDelta = (dropMass > 0f) ? (drainKg / dropMass) : 0f;
                float clampedProgress = Mathf.Clamp01(lastScaleProgress + allowedProgressDelta);
                smi.scaleGrowth.value = clampedProgress * max;
                lastScaleProgress = clampedProgress;
                return;
            }

            // Full drain matched the growth; accept the progressed state
            lastScaleProgress = progress;
        }

        private float GetDropMassConfigured()
        {
            var def = gameObject.GetDef<ScaleGrowthMonitor.Def>();
            return def != null ? def.dropMass : 0f;
        }

        private float GetCurrentMassSafe()
        {
            // Prefer the tracker’s current PE mass accessor; fallback to PE directly
            if (tracker != null)
            {
                float m = tracker.GetCurrentMass();
                return IsFiniteFloat(m) ? m : 0f;
            }
            return (primaryElement != null && IsFiniteFloat(primaryElement.Mass)) ? primaryElement.Mass : 0f;
        }
    }
}