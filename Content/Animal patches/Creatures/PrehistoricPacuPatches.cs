
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
    // Tracks pre-eat calorie snapshots so Postfix can compute overflow using pre-award capacity
    internal static class PrehistoricPacuPreyEatContext { internal static readonly Dictionary<GameObject, float> PreEatCalories = new Dictionary<GameObject, float>(); }

    internal static class PrehistoricPacuUtil
    {
        internal static bool IsPrehistoricPacu(GameObject go)
        {
            var kpid = go.GetComponent<KPrefabID>();
            return kpid.HasTag(GameTags.Creatures.Species.PrehistoricPacuSpecies)
                || kpid.HasTag((Tag)"PrehistoricPacu")
                || kpid.HasTag((Tag)"PrehistoricPacuBaby");
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


        // Adjust PrehistoricPacu diet: disable poop from Meat/DinosaurMeat and prey calories.
        [HarmonyPatch(typeof(BasePrehistoricPacuConfig), nameof(BasePrehistoricPacuConfig.CreatePrefab))]
        internal static class PrehistoricPacuDietAdjustPatch
     {
     private const float PreyCaloriesPerKg = 100000f;


     static void Postfix(GameObject __result)
     {
         var poopElem = ElementLoader.GetElement(PrehistoricPacuTuning.POOP_ELEMENT);
         Tag poopTag = poopElem.tag;

                var preyTags = new HashSet<Tag>();
         {
           preyTags.Add((Tag) "Pacu");
                    preyTags.Add((Tag) "PacuCleaner");
                    preyTags.Add((Tag) "PacuTropical");
    if (DlcManager.IsContentSubscribed("DLC5_ID"))
                {
                        preyTags.Add((Tag)"ParrotFish");
                        preyTags.Add((Tag)"PufferFish");
                }


            };

         var preyInfo = new Diet.Info(
             consumed_tags: preyTags,
             produced_element: poopTag,
             calories_per_kg: PreyCaloriesPerKg,
             produced_conversion_rate: 0f,
             food_type: Diet.Info.FoodType.EatPrey
         );

         var fishMeatInfo = new Diet.Info(
             consumed_tags: new HashSet<Tag> { (Tag)"FishMeat", (Tag)"CookedFish" },
             produced_element: poopTag,
             calories_per_kg: GetCaloriesPerKgPacuMeat(),
             produced_conversion_rate: 0f
         );

 

         var newDiet = new Diet(new[] { preyInfo, fishMeatInfo });

         var calDef = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
         calDef.diet = newDiet;

         var solidDef = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
         solidDef.diet = newDiet;
     }

     private static float GetCaloriesPerKgPacuMeat()
     {
         return PrehistoricPacuTuning.STANDARD_CALORIES_PER_CYCLE / 1f;
     }
 }


        // On prey consumption by a PrehistoricPacu: compute extra poop = max(preyActualMass - preyStartingMass, 0)
        [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
        internal static class PrehistoricPacuPreyExtraPoopPatch
        {
            [HarmonyPriority(Priority.First)]
            static void Prefix(SolidConsumerMonitor.Instance __instance, object data)
            {
                var eater = __instance.smi?.gameObject;
                if (!PrehistoricPacuUtil.IsPrehistoricPacu(eater))
                    return;

                var prey = PrehistoricPacuUtil.ResolveKPrefabID(data);
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
                    PrehistoricPacuPreyEatContext.PreEatCalories[eater] = amount.value;

                // Compute masses (kept for debugging/possible future use)
                float actualMass = 0f;
                var pe = prey.GetComponent<PrimaryElement>();
                if (pe != null) actualMass = pe.Mass;

            }
        }

        // Buffer component to hold pending extra poop mass for PrehistoricPacus
        public sealed class PrehistoricPacuExtraPoopBuffer : MonoBehaviour
        {
            [NonSerialized] public Tag poopElement = Tag.Invalid; // Meat or DinosaurMeat
            [NonSerialized] public float pendingKg = 0f;
        }

        internal static class PrehistoricPacuPoopUtil
        {
            internal static float FlushBufferedPoopAndReturnKg(GameObject owner, PrehistoricPacuExtraPoopBuffer buffer)
            {
                if (buffer == null) return 0f;
                float kg = buffer.pendingKg;
                if (kg <= 0f) return 0f;
                buffer.pendingKg = 0f;
                return kg;
            }

            // Detect if a creature is a PrehistoricPacu (reusing tag logic)
            internal static bool IsPrehistoricPacu(KPrefabID kpid)
            {
                if (kpid == null) return false;
                return kpid.HasTag(GameTags.Creatures.Species.PrehistoricPacuSpecies)
                    || kpid.HasTag((Tag)"PrehistoricPacu")
                    || kpid.HasTag((Tag)"PrehistoricPacuBaby");
            }

            internal static float GetOwnerTemperature(GameObject owner)
            {
                return owner.GetComponent<PrimaryElement>()?.Temperature ?? 243.15f;
            }
        }


        // When vanilla Stomach.Poop runs for a PrehistoricPacu, emit the buffered brine-ice using the same cell/temperature.
        [HarmonyPatch(typeof(CreatureCalorieMonitor.Stomach), "PoopInStorage")]
        internal static class PrehistoricPacuBufferedPoopOnVanillaPoop_Patch
        {
            // Signature in builds: PoopInStorage(Storage storage, bool skipPoopSpawn = false, Sprite customPopupImage = null, string customPopupMessage = null)
            static void Postfix(CreatureCalorieMonitor.Stomach __instance)
            {
                var owner = __instance.owner;
                if (owner == null) return;

                var kpid = owner.GetComponent<KPrefabID>();
                if (!PrehistoricPacuPoopUtil.IsPrehistoricPacu(kpid)) return;

                var buffer = owner.GetComponent<PrehistoricPacuExtraPoopBuffer>();
                if (buffer == null) return;

                // Flush first so we don't ever touch buffer.poopElement if it wasn't set.
                float kg = PrehistoricPacuPoopUtil.FlushBufferedPoopAndReturnKg(owner, buffer);
                if (kg <= 0f) return;

                var elem = ElementLoader.GetElement(buffer.poopElement);
                if (elem == null) return;
                {
                    elem.substance.SpawnResource(Grid.CellToPosCCC(Grid.PosToCell(owner.transform.GetPosition()), Grid.SceneLayer.Ore), kg, owner.GetComponent<PrimaryElement>()?.Temperature ?? 243.15f, byte.MaxValue, 0);
                    //     Debug.Log($"PrehistoricPacu buffered poop: Spawned {kg} kg of {elem.name} at cell {Grid.PosToCell(owner.transform.GetPosition())}");
                }
                PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Resource, elem.name, owner.transform);
            }
        }

        // On prey consumption: for PrehistoricPacus, buffer extra poop mass equal to max(preyMass - 1f, 0)

        // We hook into the same method as existing PrehistoricPacu patches but only accumulate buffer.
        [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
        internal static class PrehistoricPacuExtraPoopOnEatPatch
        {
            [HarmonyPriority(Priority.First)]
            static void Prefix(SolidConsumerMonitor.Instance __instance, object data)
            {

                var eaterGO = __instance?.smi?.gameObject;
                if (eaterGO == null) return;

                var eaterKpid = eaterGO.GetComponent<KPrefabID>();
                if (!PrehistoricPacuPoopUtil.IsPrehistoricPacu(eaterKpid)) return;

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
                float extraPoopKg = Mathf.Max(preyMass - PrehistoricPacuUtil.GetPreyStartingMass(prey), 0f);
                if (extraPoopKg <= 0f) return;

                // Buffer on the eater
                var buffer = eaterGO.GetComponent<PrehistoricPacuExtraPoopBuffer>() ?? eaterGO.AddComponent<PrehistoricPacuExtraPoopBuffer>();
                // PrehistoricPacus poop is Brine Ice in this mod.
                buffer.poopElement = SimHashes.Rust.CreateTag();
                buffer.pendingKg += extraPoopKg;

            }
        }


    }
}
