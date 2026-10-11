
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;

namespace Rephysicalized.Content.Animal_patches
{


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
                        produced_conversion_rate: 0.00f, // no poop from calories for these entries
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

    // Tracks pre-eat calorie snapshots so Postfix can compute overflow using pre-award capacity
    internal static class RaptorPreyEatContext { internal static readonly Dictionary<GameObject, float> PreEatCalories = new Dictionary<GameObject, float>(); }

    internal static class RaptorUtil
    {
        internal static bool IsRaptor(GameObject go)
        {
            var kpid = go.GetComponent<KPrefabID>();
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



    // On prey consumption by a Raptor: compute extra poop = max(preyActualMass - preyStartingMass, 0)
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

            var dietInfo = diet.GetDietInfo(prey.PrefabTag);

            if (dietInfo.foodType != Diet.Info.FoodType.EatPrey &&
                dietInfo.foodType != Diet.Info.FoodType.EatButcheredPrey)
                return;

            // Snapshot pre-eat calories
            var amount = Db.Get().Amounts.Calories.Lookup(eater);
            if (amount != null)
                RaptorPreyEatContext.PreEatCalories[eater] = amount.value;

        // Compute masses (kept for debugging/possible future use)
            float actualMass = 0f;
            var pe = prey.GetComponent<PrimaryElement>();
            if (pe != null) actualMass = pe.Mass;

            float startingMass = RaptorUtil.GetPreyStartingMass(prey);
            }
    }

        // Buffer component to hold pending extra poop mass for Raptors
        public sealed class RaptorExtraPoopBuffer : MonoBehaviour
        {
            [NonSerialized] public Tag poopElement = Tag.Invalid; // Meat or DinosaurMeat
            [NonSerialized] public float pendingKg = 0f;
        }

        internal static class RaptorPoopUtil
        {
            internal static float FlushBufferedPoopAndReturnKg(GameObject owner, RaptorExtraPoopBuffer buffer)
            {
                if (buffer == null) return 0f;
                float kg = buffer.pendingKg;
                if (kg <= 0f) return 0f;
                buffer.pendingKg = 0f;
                return kg;
            }

            // Detect if a creature is a Raptor (reusing tag logic)
            internal static bool IsRaptor(KPrefabID kpid)
            {
                if (kpid == null) return false;
                return kpid.HasTag(GameTags.Creatures.Species.RaptorSpecies)
                    || kpid.HasTag((Tag)"Raptor")
                    || kpid.HasTag((Tag)"RaptorBaby");
            }

            internal static float GetOwnerTemperature(GameObject owner)
            {
                return owner.GetComponent<PrimaryElement>()?.Temperature ?? 243.15f;
            }
        }


        // When vanilla Stomach.Poop runs for a raptor, emit the buffered brine-ice using the same cell/temperature.
        [HarmonyPatch(typeof(CreatureCalorieMonitor.Stomach), "PoopInStorage")]
        internal static class RaptorBufferedPoopOnVanillaPoop_Patch
        {
            // Signature in builds: PoopInStorage(Storage storage, bool skipPoopSpawn = false, Sprite customPopupImage = null, string customPopupMessage = null)
            static void Postfix(CreatureCalorieMonitor.Stomach __instance)
            {
                var owner = __instance.owner;
                if (owner == null) return;
          
                var kpid = owner.GetComponent<KPrefabID>();
                if (!RaptorPoopUtil.IsRaptor(kpid)) return;

                var buffer = owner.GetComponent<RaptorExtraPoopBuffer>();
                if (buffer == null) return;

                // Flush first so we don't ever touch buffer.poopElement if it wasn't set.
                float kg = RaptorPoopUtil.FlushBufferedPoopAndReturnKg(owner, buffer);
                if (kg <= 0f) return;

                var elem = ElementLoader.GetElement(buffer.poopElement);
                if (elem == null) return;
                {
                    elem.substance.SpawnResource(Grid.CellToPosCCC( Grid.PosToCell(owner.transform.GetPosition()), Grid.SceneLayer.Ore), kg, owner.GetComponent<PrimaryElement>()?.Temperature ?? 243.15f, byte.MaxValue, 0);
          //     Debug.Log($"Raptor buffered poop: Spawned {kg} kg of {elem.name} at cell {Grid.PosToCell(owner.transform.GetPosition())}");
                }
                PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Resource, elem.name, owner.transform);
            }
        }

   

        // We hook into the same method as existing raptor patches but only accumulate buffer.
        [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
        internal static class RaptorExtraPoopOnEatPatch
        {
            [HarmonyPriority(Priority.First)]
            static void Prefix(SolidConsumerMonitor.Instance __instance, object data)
            {
              
                    var eaterGO = __instance?.smi?.gameObject;
                    if (eaterGO == null) return;

                    var eaterKpid = eaterGO.GetComponent<KPrefabID>();
                    if (!RaptorPoopUtil.IsRaptor(eaterKpid)) return;

                    // Resolve prey
                    KPrefabID prey = null;
                    if (data is KPrefabID kpid) prey = kpid;
                    else if (data is Component comp && comp != null) prey = comp.GetComponent<KPrefabID>();
                    else if (data is GameObject go && go != null) prey = go.GetComponent<KPrefabID>();
                    if (prey == null) return;
                    var diet = __instance.diet;

                    var dietInfo = diet.GetDietInfo(prey.PrefabTag);

                    if (dietInfo.foodType != Diet.Info.FoodType.EatPrey &&
                        dietInfo.foodType != Diet.Info.FoodType.EatButcheredPrey)
                        return;

                    // Determine prey current mass
                    float preyMass = 0f;
                    var pe = prey.GetComponent<PrimaryElement>();
                    if (pe != null) preyMass = pe.Mass;

                    // Compute extra poop mass: preyMass - 1f
                    float extraPoopKg = Mathf.Max(preyMass - 1f, 0f);
                    if (extraPoopKg <= 0f) return;

                    // Buffer on the eater
                    var buffer = eaterGO.GetComponent<RaptorExtraPoopBuffer>() ?? eaterGO.AddComponent<RaptorExtraPoopBuffer>();
                    // Raptors poop is Brine Ice in this mod.
                    buffer.poopElement = SimHashes.BrineIce.CreateTag();
                    buffer.pendingKg += extraPoopKg;
   
            }
        }


    }
