using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.ModElements;
using Rephysicalized.Content.Animal_patches.Aqua_Creatures;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches.Aqua_Creatures
{
    internal static class AquaPufferFishPatches
    {
   

        // Ensures we don't spam logs if Harmony postfix is invoked multiple times.
        private static bool _logged;

        private static readonly object _lock = new object();

        private const float EPS_KG = 0.0001f;

        private const float CaloriesPerDay = 100_000f;
        private const float MinPoopKg = 1.5f;



        private static void ApplyCreatureDiet(GameObject prefab)
        {
            if (prefab == null) return;

            float kgConsumedPerDay = 34f;
            float kgPoopPerDay = 30f;
            // Separate pickupable (phosphorite) vs plant-growth (plankton coral)
   

            float caloriesPerKg = CaloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);

            var itemInfo = new Diet.Info(
                consumed_tags: new HashSet<Tag> { "Lettuce" },
                produced_element: SimHashes.Oxygen.CreateTag(),
                calories_per_kg: caloriesPerKg*68,
                produced_conversion_rate: 0.5f,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,

                emmit_disease_on_cell: false,
                eat_anims: null
            );

            var plantInfo = new Diet.Info(
                consumed_tags: new HashSet<Tag> { "SeaLettuce" },
                produced_element: SimHashes.Oxygen.CreateTag(),
                calories_per_kg: caloriesPerKg*8,
                produced_conversion_rate: producedPerKg,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,
                food_type: Diet.Info.FoodType.EatPlantDirectly,
                emmit_disease_on_cell: false,
                eat_anims: null
            );

            // Build combined diet like vanilla: both pickupable and plant-growth infos
            var diet = new Diet(new Diet.Info[] { itemInfo, plantInfo });

            var cals = prefab.AddOrGetDef<CreatureCalorieMonitor.Def>();
            cals.diet = diet;
            cals.minConsumedCaloriesBeforePooping = (MinPoopKg / Mathf.Max(producedPerKg, 0.0001f)) * caloriesPerKg;

            // Also assign to SolidConsumer so pickupable consumption is available
            var solid = prefab.AddOrGetDef<SolidConsumerMonitor.Def>();
            if (solid != null)
                solid.diet = diet;


        }


        // Apply diet after entities are generated so we can find the ParrotFish prefab
        [HarmonyPatch(typeof(EntityConfigManager), nameof(EntityConfigManager.LoadGeneratedEntities))]
        private static class EntityConfigManager_LoadGeneratedEntities_Postfix
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
            
                    var go = Assets.GetPrefab("PufferFish");
                    if (go != null)
                        ApplyCreatureDiet(go);
           
            }
        }

        // Also patch the original BasePufferFish.CreatePrefab to ensure our diet is applied to the prefab the game uses.
        [HarmonyPatch(typeof(BasePufferFish), nameof(BasePufferFish.CreatePrefab))]
        private static class BasePufferFish_CreatePrefab_Postfix
        {
            private static void Postfix(ref GameObject __result)
            {
               
                    if (__result == null) return;
                    ApplyCreatureDiet(__result);

            }
        }
    }
}
