using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TUNING;
using Klei.AI;

namespace Rephysicalized
{
    // Squirrel diet rebalance:
    // - Normal (adult/baby): consumes 21 kg/day, poops 20 kg/day
    // - Hug (adult/baby): consumes 11 kg/day, poops 10 kg/day
    // - Uses EatPlantDirectly only via CreatureCalorieMonitor; SolidConsumer diet is cleared to prevent double-eating
    internal static class SquirrelDietRebalance
    {
        // Shared across all variants
        private const float CaloriesPerDay = 100_000f; // keep 100k cal/day as given
        private const float MinPoopKg = 1.5f;

        // Diet: plants eaten directly (prefab tags)
        private static readonly Tag ForestTreeBranch = "ForestTreeBranch".ToTag();
        private static readonly Tag SpaceTreeBranch = "SpaceTreeBranch".ToTag();
        private static readonly Tag BasicFabricPlant = "BasicFabricPlant".ToTag(); // Thimble Reed

        private static readonly Tag PoopTag = SimHashes.Dirt.CreateTag();

        private static Diet BuildDiet(float kgConsumedPerDay, float kgPoopPerDay, float caloriesPerDay)
        {
            // Ratios
            float caloriesPerKg = caloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);

            var plantTags = new HashSet<Tag>
            {
                ForestTreeBranch,
                SpaceTreeBranch,
                BasicFabricPlant
            };

            var info = new Diet.Info(
                consumed_tags: plantTags,
                produced_element: PoopTag,
                calories_per_kg: caloriesPerKg,
                produced_conversion_rate: producedPerKg,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,
                food_type: Diet.Info.FoodType.EatPlantDirectly,
                emmit_disease_on_cell: false,
                eat_anims: null
            );

            return new Diet(info);
        }

        public static void ApplyCreatureDiet(GameObject prefab)
        {
            if (prefab == null) return;

            // Identify Hug vs Normal variant from prefab tag
            var kpid = prefab.GetComponent<KPrefabID>();
            var tag = kpid != null ? kpid.PrefabTag : Tag.Invalid;

            bool isHug = tag == new Tag(SquirrelHugConfig.ID) || tag == new Tag(BabySquirrelHugConfig.ID);

            // Per-requested tuning
            float kgConsumedPerDay = isHug ? 11f : 21f;
            float kgPoopPerDay = isHug ? 10f : 20f;

            // Build and assign diet to CreatureCalorieMonitor only
            var cals = prefab.AddOrGetDef<CreatureCalorieMonitor.Def>();
            cals.diet = BuildDiet(kgConsumedPerDay, kgPoopPerDay, CaloriesPerDay);

            // Minimum calories before pooping scales with produced mass ratio
            float caloriesPerKg = CaloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            cals.minConsumedCaloriesBeforePooping = (MinPoopKg / Mathf.Max(producedPerKg, 0.0001f)) * caloriesPerKg;

            // Critical: SolidConsumer should NOT share the plant diet (prevents double-eating)
            var solid = prefab.GetDef<SolidConsumerMonitor.Def>();
            if (solid != null)
                solid.diet = new Diet(Array.Empty<Diet.Info>());
        }
    }

    // Apply to all squirrel variants (adult, baby, hug) at the single, common setup point.
    [HarmonyPatch(typeof(BaseSquirrelConfig), nameof(BaseSquirrelConfig.SetupDiet))]
    internal static class BaseSquirrelConfig_SetupDiet_Postfix
    {
        public static void Postfix(GameObject prefab)
        {
            try
            {
                SquirrelDietRebalance.ApplyCreatureDiet(prefab);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] SetupDiet patch failed for {prefab?.name}: {e}");
            }
        }
    }

    // Squirrel Hug: lower meat drop
    [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction")]
    public static class EntityTemplates_DeathDropFunction_SquirrelHug
    {
        public static void Prefix(GameObject inst, ref float onDeathDropCount, ref string onDeathDropID)
        {
            try
            {
                var kpid = inst.GetComponent<KPrefabID>();
                if (kpid == null) return;

                var pt = kpid.PrefabTag;
                if (pt == new Tag(SquirrelHugConfig.ID) || pt == new Tag(BabySquirrelHugConfig.ID))
                {
                    onDeathDropID = "Meat";
                    onDeathDropCount = 0.5f;
                }
            }
            catch { /* best-effort */ }
        }
    }

    // Pip (Squirrel) Hug pen size
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_SquirrelPenSize
    {
        public static void Postfix()
        {
            try
            {
                SquirrelTuning.PEN_SIZE_PER_CREATURE_HUG = CREATURES.SPACE_REQUIREMENTS.TIER2;
            }
            catch { /* best-effort */ }
        }
    }
}