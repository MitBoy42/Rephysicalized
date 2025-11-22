using HarmonyLib;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
   

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class VineMother_Registration
    {
        private static void Postfix()
        {
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: "VineMother",
                yields: new List<MaterialYield>
                {
                    new MaterialYield("Mud", 1f),
                },
                realHarvestSubtractKg: 0f
            );
        }
    }
    // Tracks which Harvestable triggered PlantFiber so we can apply extra loss on the same harvest.
    internal static class PlantFiberHarvestTracker
    {
        // Using a HashSet of instance IDs to avoid holding strong refs
        private static readonly HashSet<int> fiberHarvested = new HashSet<int>();

        public static void MarkFiberHarvested(Harvestable harvestable)
        {
            if (harvestable != null)
                fiberHarvested.Add(harvestable.GetInstanceID());
        }

        public static bool ConsumeFiberFlag(Harvestable harvestable)
        {
            if (harvestable == null) return false;
            int id = harvestable.GetInstanceID();
            if (fiberHarvested.Contains(id))
            {
                fiberHarvested.Remove(id);
                return true;
            }
            return false;
        }
    }

    // Patch PlantFiberProducer.OnHarvest to record when plant fiber is created for a harvest event.
    [HarmonyPatch(typeof(PlantFiberProducer), "OnHarvest")]
    internal static class PlantFiberProducer_OnHarvest_Patch
    {
        // Signature is: private void OnHarvest(object obj)
        // We can't easily know if fiber actually spawned post method without copying logic.
        // But we can peek the same conditions pre-spawn to decide if it will spawn; then we mark it.
        private static void Prefix(object obj, PlantFiberProducer __instance)
        {
            // Mirror the original checks:
            var harvestable = obj as Harvestable;
            if (harvestable == null) return;
            if (harvestable.completed_by == null) return;

            var resume = harvestable.completed_by.GetComponent<MinionResume>();
            if (resume == null) return;

            if (!resume.HasPerk(Db.Get().SkillPerks.CanSalvagePlantFiber)) return;

            // This harvest would spawn plant fiber -> mark it.
            PlantFiberHarvestTracker.MarkFiberHarvested(harvestable);
        }
    }

    // Extend your existing logic on VineBranch.Instance.SpawnHarvestedFruit to subtract 6kg if fiber harvested.
    // If you already have a patch on SpawnHarvestedFruit, this postfix can coexist — place both in the same project.
    [HarmonyPatch(typeof(VineBranch.Instance), nameof(VineBranch.Instance.SpawnHarvestedFruit))]
    internal static class VineBranch_Instance_SpawnHarvestedFruit_ExtraFiberLoss_Patch
    {
        private const float ExtraFiberFlatLossKg = 6f;

        private static void Postfix(VineBranch.Instance __instance)
        {
            // We need to find the Harvestable involved in this harvest action.
            // SpawnHarvestedFruit is triggered by Harvestable, so the closest reliable link is to query
            // the Harvestable on this vine. If present and we flagged it, apply extra loss.
            var go = __instance.gameObject;
            var harvestable = go != null ? go.GetComponent<Harvestable>() : null;

            bool hadFiber = PlantFiberHarvestTracker.ConsumeFiberFlag(harvestable);
            if (!hadFiber)
                return;

            GameObject mother = __instance.Mother;
            if (mother == null) return;

            var pmt = mother.GetComponent<PlantMassTrackerComponent>();
            var pe = mother.GetComponent<PrimaryElement>();

            if (pmt == null && pe == null) return;

            float currentMotherMass = 1f;
            if (pmt != null)
                currentMotherMass = Mathf.Max(1f, pmt.TrackedMassKg);
            else
                currentMotherMass = Mathf.Max(1f, pe.Mass);

            // Do not go below 1 kg baseline
            float maxRemovable = Mathf.Max(0f, currentMotherMass - 1f);
            float actualRemoved = Mathf.Min(ExtraFiberFlatLossKg, maxRemovable);

            if (actualRemoved > 0f)
            {
                if (pmt != null)
                    pmt.AddExternalMass(-actualRemoved);
                else
                    pe.Mass = Mathf.Max(1f, pe.Mass - actualRemoved);
            }
        }
    }

    // Decrease VineMother mass on any connected VineBranch harvest:
    // - Spawn 10% of current mother mass as Mud (at mother's temperature)
    // - Additionally subtract 1 kg from the mother (not spawned)
    // Both changes clamp to a 1 kg minimum mother mass baseline.
    [HarmonyPatch(typeof(VineBranch.Instance), nameof(VineBranch.Instance.SpawnHarvestedFruit))]
    internal static class VineBranch_Instance_SpawnHarvestedFruit_Patch
    {
        private const float ExtraFlatLossKg = 1f;
        private const float PercentageLoss = 0.10f;

        private static void Postfix(VineBranch.Instance __instance)
        {
 

            GameObject mother = __instance.Mother;
   

                var pmt = mother.GetComponent<PlantMassTrackerComponent>();
                var pe = mother.GetComponent<PrimaryElement>();

                // Determine current mass (prefer PMT's tracked mass if available)
                float currentMotherMass = 1f;
                if (pmt != null)
                    currentMotherMass = Mathf.Max(1f, pmt.TrackedMassKg);
                else if (pe != null)
                    currentMotherMass = Mathf.Max(1f, pe.Mass);

                // Calculate desired removals
                float tenPercent = currentMotherMass * PercentageLoss; // mass to spawn as mud
                float desiredTotalRemoval = tenPercent + ExtraFlatLossKg;

                // Do not drop below the 1 kg baseline
                float maxRemovable = Mathf.Max(0f, currentMotherMass - 1f);
                float actualRemoved = Mathf.Min(desiredTotalRemoval, maxRemovable);

                // Mud spawned is the portion from the percentage loss, capped by actualRemoved
                float mudToSpawn = Mathf.Min(tenPercent, actualRemoved);
                float extraLossApplied = Mathf.Max(0f, actualRemoved - mudToSpawn); // the remainder toward the 1 kg flat loss

            // Spawn mud at mother's temperature
            if (mudToSpawn > 0f)
            {
                float tempK = pe != null ? pe.Temperature : 293.15f;
                var mudElement = ElementLoader.FindElementByHash(SimHashes.Mud);
                if (mudElement != null)
                {
                    // Spawn as solid resource at mother's position
                    var pos = mother.transform.GetPosition();
                    mudElement.substance.SpawnResource(
                        pos,
                        mudToSpawn,
                        tempK,
                        byte.MaxValue,
                        0,
                        prevent_merge: false,
                        forceTemperature: true,
                        manual_activation: false
                    );
                }
            }

                // Apply the total removal to the mother
                if (actualRemoved > 0f)
                {
                    if (pmt != null)
                    {
                        pmt.AddExternalMass(-actualRemoved);
                    }
                    else if (pe != null)
                    {
                        pe.Mass = Mathf.Max(1f, pe.Mass - actualRemoved);
                    }
                }
            }
         
        }

    // Patch Tinkerable.OnCompleteWork to detect FarmTinker application.
    // When a vine is tinkered with FarmTinker, add +5 kg to the mother.
    [HarmonyPatch(typeof(Tinkerable), nameof(Tinkerable.OnCompleteWork))]
    internal static class Tinkerable_OnCompleteWork_FarmTinker_Patch
    {
        private const float FarmTinkerMotherBonusKg = 5f;

        private static void Postfix(Tinkerable __instance)
        {

            // Ensure this tinkerable applies FarmTinker
            if (!string.Equals(__instance.addedEffect, "FarmTinker"))
                return;

            // Is this tinkerable on a vine? Check for VineBranch.Instance on the tinkerable's GO.
            var branch = __instance.gameObject.GetComponent<StateMachineController>()?.GetSMI<VineBranch.Instance>();
            if (branch == null)
            {
                // Some vine prefabs may have VineBranch on the same GO without SMI helper
                branch = __instance.gameObject.GetSMI<VineBranch.Instance>();
            }
            if (branch == null)
            {
                // Fallback: check parent chain for a VineBranch.Instance
                branch = __instance.GetComponentInParent<StateMachineController>()?.GetSMI<VineBranch.Instance>();
            }
            if (branch == null) return;

            var mother = branch.Mother;
            if (mother == null) return;

            var pmt = mother.GetComponent<PlantMassTrackerComponent>();
            var pe = mother.GetComponent<PrimaryElement>();

            if (pmt == null && pe == null) return;

            if (pmt != null)
            {
                pmt.AddExternalMass(FarmTinkerMotherBonusKg);
            }
            else
            {
                pe.Mass += FarmTinkerMotherBonusKg;
            }
        }
    }
}

