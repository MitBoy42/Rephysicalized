﻿﻿using HarmonyLib;
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
    internal static class AquaParrotFishPatches
    {
        private const string PlanktonCoralConsumedTagLiteral = "PlanktonCoral";

        // Ensures we don't spam logs if Harmony postfix is invoked multiple times.
        private static bool _logged;

        private static readonly object _lock = new object();

        private const float EPS_KG = 0.0001f;

        private const float CaloriesPerDay = 100_000f;
        private const float MinPoopKg = 1.5f;

        private static readonly Tag PlanktonCoralTag = "PlanktonCoral".ToTag();
        private static readonly Tag PhosphoriteTag = SimHashes.Phosphorite.CreateTag();



        private static void ApplyCreatureDiet(GameObject prefab)
        {
            if (prefab == null) return;

            float kgConsumedPerDay = 10f;
            float kgPoopPerDay = 5f;
            // Separate pickupable (phosphorite) vs plant-growth (plankton coral)
            var phosphoriteTag = PhosphoriteTag;
            var planktonTag = PlanktonCoralTag;

            float caloriesPerKg = CaloriesPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);
            float producedPerKg = kgPoopPerDay / Mathf.Max(kgConsumedPerDay, 0.0001f);

            var itemInfo = new Diet.Info(
                consumed_tags: new HashSet<Tag> { phosphoriteTag },
                produced_element: SimHashes.Lime.CreateTag(),
                calories_per_kg: caloriesPerKg,
                produced_conversion_rate: producedPerKg,
                disease_id: null,
                disease_per_kg_produced: 0f,
                produce_solid_tile: false,
        
                emmit_disease_on_cell: false,
                eat_anims: null
            );

            var plantInfo = new Diet.Info(
                consumed_tags: new HashSet<Tag> { planktonTag },
                produced_element: SimHashes.Lime.CreateTag(),
                calories_per_kg: caloriesPerKg,
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
             
                    var go = Assets.GetPrefab("ParrotFish");
                    if (go != null)
                        ApplyCreatureDiet(go);
            }
        }

        // Also patch the original BaseParrotFish.CreatePrefab to ensure our diet is applied to the prefab the game uses.
        [HarmonyPatch(typeof(BaseParrotFish), nameof(BaseParrotFish.CreatePrefab))]
        private static class BaseParrotFish_CreatePrefab_Postfix
        {
            private static void Postfix(ref GameObject __result)
            {
                    if (__result == null) return;
                    ApplyCreatureDiet(__result);
            }
        }

        //
        [HarmonyPatch(typeof(SurfaceAirConsumerStates.Instance), nameof(SurfaceAirConsumerStates.Instance.ConsumeOxygen))]
        private static class SurfaceAirConsumerStates_Instance_ConsumeOxygen_MassGainPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(SurfaceAirConsumerStates.Instance __instance, float dt)
            {
                    if (__instance == null)
                        return true;
                    var monitor = __instance.monitor;
                    if (monitor == null)
                        return true;
                    var owner = monitor.gameObject;
                    if (owner == null)
                        return true;
                    var massTracker = owner.GetComponent<CreatureMassTracker>();
                    SimHashes consumedElement = monitor.def.element;
                    float amountConsumed = monitor.def != null ? __instance.def.consumptionRate * dt : 0f;
                    float kgGain = amountConsumed;
                    massTracker.AddExternalMass(kgGain);
                return true;
            }
        }
    }
}


