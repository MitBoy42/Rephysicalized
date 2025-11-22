using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Tracks pre-eat calorie snapshots so Postfix can compute overflow using pre-award capacity
    internal static class RaptorPreyEatContext { internal static readonly Dictionary<GameObject, float> PreEatCalories = new Dictionary<GameObject, float>(); }

    internal static class RaptorUtil
    {
        internal static bool IsRaptor(GameObject go)
        {
            if (go == null) return false;
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return false;
            return kpid.HasTag(GameTags.Creatures.Species.RaptorSpecies)
                || kpid.HasTag((Tag)"Raptor")
                || kpid.HasTag((Tag)"RaptorBaby");
        }

        internal static KPrefabID ResolveKPrefabID(object data)
        {
            if (data is KPrefabID kpid && kpid != null) return kpid;
            if (data is Component comp && comp != null) return comp.GetComponent<KPrefabID>();
            if (data is GameObject go && go != null) return go.GetComponent<KPrefabID>();
            return null;
        }

        // Robustly extract the consumed tag from various payload forms used by CaloriesConsumed.
        internal static bool TryGetConsumedTag(object data, out Tag tag)
        {
            // Preferred/canonical in current builds: Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent>
            if (data is Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent> boxed && boxed != null)
            {
                tag = boxed.value.tag;
                return true;
            }
            // Some paths may pass the struct directly (unlikely in current build, but tolerate)
            if (data is CreatureCalorieMonitor.CaloriesConsumedEvent evt)
            {
                tag = evt.tag;
                return true;
            }
      
            tag = Tag.Invalid;
            return false;
        }

        internal static float GetPreyStartingMass(KPrefabID prey)
        {
            float startingMass = 0f;
            if (prey == null) return 0f;

            var preyTracker = prey.GetComponent<CreatureMassTracker>();
            if (preyTracker != null)
            {
                startingMass = Mathf.Max(0f, preyTracker.STARTING_MASS);
            }
            else
            {
        
                    var prefab = Assets.GetPrefab(prey.PrefabTag);
                    var pe = prefab != null ? prefab.GetComponent<PrimaryElement>() : null;
                    if (pe != null)
                        startingMass = Mathf.Max(0f, pe.Mass);
              
            }
            return startingMass;
        }

        internal static Tag ResolveOverfeedDropTag(Tag preyTag)
        {
            // Defaults
            Tag dinosaurMeat = (Tag)"DinosaurMeat";
            Tag meat = (Tag)"Meat";

    
                // Check the prey prefab's butcher drops
                var preyPrefab = Assets.GetPrefab(preyTag);
                if (preyPrefab != null)
                {
                    var butch = preyPrefab.GetComponent<Butcherable>();
                    if (butch?.drops != null && butch.drops.Count > 0)
                    {
                        // drops keys are prefab IDs (strings)
                        bool hasDino = butch.drops.Keys.Any(k => string.Equals(k, "DinosaurMeat", StringComparison.Ordinal));
                        bool hasMeat = butch.drops.Keys.Any(k => string.Equals(k, "Meat", StringComparison.Ordinal));

                        if (hasDino) return dinosaurMeat;
                        if (hasMeat) return meat;
                    }
                }

            return meat;
        }
    }

    // Adjust Raptor diet: disable poop from Meat/DinosaurMeat and prey calories.
    [HarmonyPatch(typeof(BaseRaptorConfig), nameof(BaseRaptorConfig.StandardDiets))]
    internal static class RaptorDietAdjustPatch
    {
        static void Postfix(List<Diet.Info> __result)
        {
   
            for (int i = 0; i < __result.Count; i++)
            {
                var info = __result[i];
                if (info == null) continue;

                bool isMeatSolid = info.consumedTags.Contains((Tag)"Meat") || info.consumedTags.Contains((Tag)"DinosaurMeat");
                bool isPrey = info.foodType == Diet.Info.FoodType.EatPrey || info.foodType == Diet.Info.FoodType.EatButcheredPrey;

                if (isMeatSolid || isPrey)
                {
                    var newInfo = new Diet.Info(
                        consumed_tags: new HashSet<Tag>(info.consumedTags),
                        produced_element: info.producedElement,
                        calories_per_kg: info.caloriesPerKg,
                        produced_conversion_rate: 0f, // no poop from calories for these entries
                        disease_id: null,
                        disease_per_kg_produced: 0f,
                        produce_solid_tile: info.produceSolidTile,
                        food_type: info.foodType,
                        emmit_disease_on_cell: info.emmitDiseaseOnCell,
                        eat_anims: info.eatAnims
                    );
                    __result[i] = newInfo;
                }
            }
        }
    }

    // Attach CreatureMassTracker and link on adult raptor prefab
    [HarmonyPatch(typeof(RaptorConfig), nameof(RaptorConfig.CreatePrefab))]
    internal static class RaptorMassTrackerAttach_Adult
    {
        static void Postfix(GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.CALORIE_RATIO = 80000f;
            __result.AddOrGet<CreatureMassTrackerLink>();
        }
    }

    // Attach CreatureMassTracker and link on baby raptor prefab
    [HarmonyPatch(typeof(BabyRaptorConfig), nameof(BabyRaptorConfig.CreatePrefab))]
    internal static class RaptorMassTrackerAttach_Baby
    {
        static void Postfix(GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.CALORIE_RATIO = 80000f;
            __result.AddOrGet<CreatureMassTrackerLink>();

        }
    }

    // On prey consumption by a Raptor: compute and buffer extra poop = max(preyActualMass - preyStartingMass, 0)
    //  Also remove Butcherable so consumed prey cannot create drops.
    [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
    internal static class RaptorPreyExtraPoopPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(SolidConsumerMonitor.Instance __instance, object data)
        {
          
     

                var eater = __instance.smi?.gameObject;
                if (!RaptorUtil.IsRaptor(eater))
                    return;

                var prey = RaptorUtil.ResolveKPrefabID(data);
                if (prey == null)
                    return;

                var diet = __instance.diet;
                if (diet == null)
                    return;

                var dietInfo = diet.GetDietInfo(prey.PrefabTag);
                if (dietInfo == null)
                    return;

                if (dietInfo.foodType != Diet.Info.FoodType.EatPrey &&
                    dietInfo.foodType != Diet.Info.FoodType.EatButcheredPrey)
                    return;

                // Remove Butcherable so eating never spawns drops
                var butcher = prey.GetComponent<Butcherable>();
                if (butcher != null)
                    UnityEngine.Object.Destroy(butcher);

                // Snapshot pre-eat calories
                var amount = Db.Get().Amounts.Calories.Lookup(eater);
                if (amount != null)
                    RaptorPreyEatContext.PreEatCalories[eater] = amount.value;

                // Compute masses
                float actualMass = 0f;
                var pe = prey.GetComponent<PrimaryElement>();
                if (pe != null) actualMass = pe.Mass;

                float startingMass = RaptorUtil.GetPreyStartingMass(prey);

            

        }
    }

    // Correct prey calorie attribution and spawn an overfeed meat drop (if any overflow).
    [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
    internal static class SolidConsumerRaptorPreyCaloriesPatch
    {
        // Raw kcal per kg constant (matches basic Meat kcal/kg), used explicitly for clarity/debugging
       private const float RAW_KCAL_PER_KG = 1600000f;

        [HarmonyPostfix]
        static void Postfix(SolidConsumerMonitor.Instance __instance, object data)
        {
            GameObject eater = __instance?.smi?.gameObject;
          
                var preyKPID = RaptorUtil.ResolveKPrefabID(data);
                if (preyKPID == null)
                    return;

                if (!RaptorUtil.IsRaptor(eater))
                    return;

                var diet = __instance.diet;
                if (diet == null)
                    return;

                var info = diet.GetDietInfo(preyKPID.PrefabTag);
                if (info == null ||
                    (info.foodType != Diet.Info.FoodType.EatPrey && info.foodType != Diet.Info.FoodType.EatButcheredPrey))
                    return;

                var amount = Db.Get().Amounts.Calories.Lookup(eater);
                if (amount == null)
                    return;

                float stomachMax = amount.GetMax();

                // Use pre-eat snapshot to compute overflow correctly
                float calSnapshot = amount.value;
                if (RaptorPreyEatContext.PreEatCalories.TryGetValue(eater, out var snap))
                    calSnapshot = snap;

                // Resolve prey starting mass: prefer tracker, fallback to prefab base mass
                float startingMass = RaptorUtil.GetPreyStartingMass(preyKPID);

                // Total calories from prey (explicit raw constant)
                float awardedCalories = startingMass * Mathf.Max(0f, RAW_KCAL_PER_KG);

                // Calculate overflow before applying to avoid dropping full prey mass erroneously
                float overfedCalories = Mathf.Max(0f, (calSnapshot + awardedCalories) - stomachMax);

                // Apply calories
                amount.value = Mathf.Min(stomachMax, calSnapshot + awardedCalories);

                // Update stomach journal/time similar to vanilla
                var cmi = eater.GetSMI<CreatureCalorieMonitor.Instance>();
                if (cmi != null && cmi.stomach != null)
                {
                    cmi.stomach.Consume(preyKPID.PrefabTag, awardedCalories);
                    cmi.lastMealOrPoopTime = Time.time;
                }

                // Extra safety: clamp next frame and a bit later
                var target = amount.value;
                GameScheduler.Instance.ScheduleNextFrame("RaptorCalorieApply", _ =>
                {
                    var a = Db.Get().Amounts.Calories.Lookup(eater);
                    if (a != null) a.value = target;
                });
                GameScheduler.Instance.Schedule("RaptorCalorieApplyLate", 0.1f, _ =>
                {
                    var a = Db.Get().Amounts.Calories.Lookup(eater);
                    if (a != null) a.value = target;
                });

                // Spawn meat drop from overflow using prey’s butcher drops to determine tag
                if (overfedCalories > 0f)
                    SpawnOverfeedMeatDrop(eater, overfedCalories, preyKPID.PrefabTag);
            
          
        }

        private static void SpawnOverfeedMeatDrop(GameObject eater, float overfedCalories, Tag preyTag)
        {
          
                float kg = overfedCalories / Mathf.Max(1f, RAW_KCAL_PER_KG);
                if (kg <= 0.0005f)
                    return;

                // Determine drop tag based on prey’s butcher drops
                Tag dropTag = RaptorUtil.ResolveOverfeedDropTag(preyTag);

                GameObject prefab = Assets.GetPrefab(dropTag);
                if (prefab == null)
                    return;

                int cell = Grid.PosToCell(eater);
                Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);

                GameObject drop = Util.KInstantiate(prefab, pos);
                drop.SetActive(true);

                var pe = drop.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    var eaterPe = eater.GetComponent<PrimaryElement>();
                    pe.Temperature = eaterPe != null ? eaterPe.Temperature : 300f;
                    pe.Mass = kg;
                }

                drop.AddOrGet<Pickupable>();
         
        }
    }

}