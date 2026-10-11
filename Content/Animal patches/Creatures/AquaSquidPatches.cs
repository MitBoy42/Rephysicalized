using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;
using Rephysicalized;
using Rephysicalized.Content.Animal_patches.Aqua_Creatures;

namespace Rephysicalized.Content.Animal_patches.Creatures
{
    internal class AquaSquidPatches
    {
        private const float CaloriesPerDay = 100_000f;
        private const float MinPoopKg = 1.5f;

        private static readonly Tag TubeWormTag = "TubeWorm".ToTag();

        private static Diet BuildDiet(HashSet<Tag> plantTags, float kgConsumedPerDay, float kgPoopPerDay, float caloriesPerDay)
        {
            float caloriesPerKg = caloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);

            var info = new Diet.Info(
                consumed_tags: plantTags,
                produced_element: "Katairite".ToTag(),
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

        private static void ApplyCreatureDiet_Squid(GameObject prefab)
        {
            if (prefab == null) return;

            float kgConsumedPerDay = 100f;
            float kgPoopPerDay = 20f;

            var plantTags = new HashSet<Tag> { TubeWormTag };

            var cals = prefab.AddOrGetDef<CreatureCalorieMonitor.Def>();
            cals.diet = BuildDiet(plantTags, kgConsumedPerDay, kgPoopPerDay, CaloriesPerDay);

            float caloriesPerKg = CaloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            cals.minConsumedCaloriesBeforePooping = (MinPoopKg / Mathf.Max(producedPerKg, 0.0001f)) * caloriesPerKg;

            var solid = prefab.GetDef<SolidConsumerMonitor.Def>();
            if (solid != null)
                solid.diet = new Diet(Array.Empty<Diet.Info>());
        }

        [HarmonyPatch(typeof(EntityConfigManager), nameof(EntityConfigManager.LoadGeneratedEntities))]
        private static class EntityConfigManager_LoadGeneratedEntities_Postfix_Squid
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    var go = Assets.GetPrefab("Squid");
                    if (go != null)
                        ApplyCreatureDiet_Squid(go);
                }
                catch (Exception e)
                {
                }
            }
        }

     


            }
        }


