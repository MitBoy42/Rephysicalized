﻿using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches.Aqua_Creatures
{
 
    // Harmony patch for snail lubricant production.
    // Uses CreatureMassTracker to throttle/block lubricant generation based on creature mass.
    [HarmonyPatch(typeof(MoistureMonitor.Instance), nameof(MoistureMonitor.Instance.ProduceLubricant))]
    internal static class SnailPatches_MoistureMonitor_ProduceLubricant
    {
        private static bool Prefix(MoistureMonitor.Instance __instance)
        {
            if (__instance == null)
                return true;

            var tracker = __instance.GetComponent<CreatureMassTracker>();
  
        

            float producedLubricantKg = (__instance.mucusAmount != null) ? __instance.mucusAmount.value : 0f;
            if (producedLubricantKg <= 0f)
                return true; // nothing to do

            float creatureMassKg = tracker.GetCurrentMass();

            // 1) If produced amount is more than the creature's mass, block lubricant creation.
            if (producedLubricantKg > creatureMassKg)
            {
                if (__instance.mucusAmount != null)
                    __instance.mucusAmount.value = 0f;
                return false;
            }

            // 2) If produced amount is less than (creature's mass + 10f), subtract produced amount from creature mass.
            if (producedLubricantKg < (creatureMassKg + 10f))
            {
                tracker.AddExternalMass(-producedLubricantKg);
            }
            return true;
        }
    }

    // Replaces the SnailIron diet's consumed Sulfur with a set of rock types
    // (SedimentaryRock, IgneousRock, Granite, Basalt), keeping the rest of the
    // vanilla diet entry (Obsidian output, calories, conversion rate) intact.
    [HarmonyPatch(typeof(SnailIronConfig), nameof(SnailIronConfig.CreatePrefab))]
    internal static class SnailIronConfig_CreatePrefab_RockDiet
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject __result)
        {
            if (__result == null) return;


            var calDef = __result.GetDef<CreatureCalorieMonitor.Def>();
            var solidDef = __result.GetDef<SolidConsumerMonitor.Def>();

            if (calDef?.diet?.infos == null || calDef.diet.infos.Length == 0)
                return;

            var rockTags = new HashSet<Tag>
            {
                SimHashes.SedimentaryRock.CreateTag(),
                SimHashes.IgneousRock.CreateTag(),
                SimHashes.Granite.CreateTag(),
                SimHashes.Basalt.CreateTag(),
            };

            var oldInfos = calDef.diet.infos;
            var newInfos = new List<Diet.Info>(oldInfos.Length);

            foreach (var info in oldInfos)
            {
                // Clone the tags per entry so we don't share one mutable set across infos.
                var consumed = new HashSet<Tag>(rockTags);

                // Rebuild Diet.Info with only the consumed tags swapped out.
                var newInfo = new Diet.Info(
                    consumed,
                    info.producedElement,
                    info.caloriesPerKg,
                    info.producedConversionRate,
                    disease_per_kg_produced: info.diseasePerKgProduced,
                    produce_solid_tile: info.produceSolidTile,
                    food_type: info.foodType,
                    emmit_disease_on_cell: info.emmitDiseaseOnCell,
                    eat_anims: info.eatAnims
                );

                newInfos.Add(newInfo);
            }

            var newDiet = new Diet(newInfos.ToArray());
            calDef.diet = newDiet;

            if (solidDef != null)
                solidDef.diet = newDiet;
        }
    }
}

