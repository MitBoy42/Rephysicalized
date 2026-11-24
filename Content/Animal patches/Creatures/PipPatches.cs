using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TUNING;
using Klei.AI;

namespace Rephysicalized
{

    [HarmonyPatch(typeof(EntityConfigManager), "LoadGeneratedEntities")]
    public static class Pips_AddToDescription
    {
        public static void Prefix()
        {
            const string key = "STRINGS.CODEX.PIP.BODY.CONTAINER3";
            // Append gas input functionality to the description
            Strings.Add(key, Strings.Get(key) + "\n\n<b>REPHYSICALIZED: Note that Pips, as any other creatures, require consuming real mass, so they can't feed off of wild trees.</b>");
        }
    }
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
        private static readonly Tag CaviarTag = "Caviar".ToTag();

        private static Diet BuildDiet(float kgConsumedPerDay, float kgPoopPerDay, float caloriesPerDay)
        {
            // Ratios
            float caloriesPerKg = caloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);

            var plantTags = new HashSet<Tag>
            {
                ForestTreeBranch,
                SpaceTreeBranch,
                BasicFabricPlant,
                DewPalmConfig.ID.ToTag(),
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
            bool isMerm = tag == new Tag(SquirrelAquaticConfig.ID) || tag == new Tag(BabySquirrelAquaticConfig.ID);

            if (isMerm)
            {
                ApplyAquaticDiet(prefab);
                return;
            }

            // Per-requested tuning
            float kgConsumedPerDay = isHug ? 11f : 21f;
            float kgPoopPerDay = isHug ? 10f : 20f;

            // Keep plant ingestion on CreatureCalorieMonitor and Caviar on SolidConsumer.
            var cals = prefab.AddOrGetDef<CreatureCalorieMonitor.Def>();
            var caviarInfos = BuildNoPoopCaviarInfos(cals.diet);
            var dietInfos = new List<Diet.Info>(BuildDiet(kgConsumedPerDay, kgPoopPerDay, CaloriesPerDay).infos);
            dietInfos.AddRange(caviarInfos);
            cals.diet = new Diet(dietInfos.ToArray());

            // Minimum calories before pooping scales with produced mass ratio
            float caloriesPerKg = CaloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            cals.minConsumedCaloriesBeforePooping = (MinPoopKg / Mathf.Max(producedPerKg, 0.0001f)) * caloriesPerKg;

            // Critical: SolidConsumer should NOT share the plant diet (prevents double-eating)
            var solid = prefab.GetDef<SolidConsumerMonitor.Def>();
            if (solid != null)
                solid.diet = new Diet(caviarInfos);
        }

        private static Diet.Info[] BuildNoPoopCaviarInfos(Diet vanillaDiet)
        {
            if (vanillaDiet?.infos == null)
                return Array.Empty<Diet.Info>();

            var caviarInfos = new List<Diet.Info>();
            foreach (var info in vanillaDiet.infos)
            {
                if (info.consumedTags.Contains(CaviarTag))
                    caviarInfos.Add(CopyDietInfo(info, Tag.Invalid, 0f));
            }

            return caviarInfos.ToArray();
        }

        private static Diet.Info CopyDietInfo(Diet.Info info, Tag producedElement, float producedConversionRate)
        {
            return new Diet.Info(
                new HashSet<Tag>(info.consumedTags),
                producedElement,
                info.caloriesPerKg,
                producedConversionRate,
                disease_per_kg_produced: info.diseasePerKgProduced,
                produce_solid_tile: info.produceSolidTile,
                food_type: info.foodType,
                emmit_disease_on_cell: info.emmitDiseaseOnCell,
                eat_anims: info.eatAnims
            );
        }

        private static void ApplyAquaticDiet(GameObject prefab)
        {
            var calorieMonitor = prefab.GetDef<CreatureCalorieMonitor.Def>();
            var vanillaDiet = calorieMonitor?.diet;
            if (vanillaDiet?.infos == null)
                return;

            var infos = new Diet.Info[vanillaDiet.infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                var info = vanillaDiet.infos[i];
                bool isCaviarDiet = info.consumedTags.Contains(CaviarTag);
                Tag producedElement = isCaviarDiet ? Tag.Invalid : info.producedElement;
                float producedConversionRate = isCaviarDiet
                    ? 0f
                    : info.producedConversionRate * 19f / 20f;

                infos[i] = CopyDietInfo(info, producedElement, producedConversionRate);
            }

            var diet = new Diet(infos);
            calorieMonitor.diet = diet;

            var solidConsumer = prefab.GetDef<SolidConsumerMonitor.Def>();
            if (solidConsumer != null)
                solidConsumer.diet = diet;
        }
    }

    // Apply to all squirrel variants (adult, baby, hug) at the single, common setup point.
    [HarmonyPatch(typeof(BaseSquirrelConfig), nameof(BaseSquirrelConfig.SetupDiet))]
    internal static class BaseSquirrelConfig_SetupDiet_Postfix
    {
        public static void Postfix(GameObject prefab)
        {

                SquirrelDietRebalance.ApplyCreatureDiet(prefab);
       
        }
    }



    // Pip (Squirrel) Hug pen size
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_SquirrelPenSize
    {
        public static void Postfix()
        {
          
                SquirrelTuning.PEN_SIZE_PER_CREATURE_HUG = CREATURES.SPACE_REQUIREMENTS.TIER2;
      
        }
    }
}
