using HarmonyLib;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
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
        private static void Prefix(object obj, PlantFiberProducer __instance)
        {
            var harvestable = obj as Harvestable;
            if (harvestable == null) return;

            // During cleanup this may be null
            var worker = harvestable.completed_by;
            if (worker == null) return;

            var resume = worker.GetComponent<MinionResume>();
            if (resume == null) return;

            if (!resume.HasPerk(Db.Get().SkillPerks.CanSalvagePlantFiber)) return;

            PlantFiberHarvestTracker.MarkFiberHarvested(harvestable);
        }
    }

    // Extra fiber loss: subtract 6 kg from mother when fiber also spawned during this harvest.
    [HarmonyPatch(typeof(VineBranch.Instance), nameof(VineBranch.Instance.SpawnHarvestedFruit))]
    internal static class VineBranch_Instance_SpawnHarvestedFruit_ExtraFiberLoss_Patch
    {
        private const float ExtraFiberFlatLossKg = 6f;

        private static void Postfix(VineBranch.Instance __instance)
        {
            if (__instance == null)
                return;

            var go = __instance.gameObject;
            if (go == null || !go)
                return;

            // Mother can already be null in uproot cascade
            GameObject mother = __instance.Mother;
            if (mother == null || !mother)
                return;

            // Confirm this harvest was a fiber-producing one
            var harvestable = go.GetComponent<Harvestable>();
            if (!PlantFiberHarvestTracker.ConsumeFiberFlag(harvestable))
                return;

            // Prefer PlantMassTrackerComponent if present
            var pmt = mother.GetComponent<PlantMassTrackerComponent>();
            var pe = pmt == null ? mother.GetComponent<PrimaryElement>() : null;

            if (pmt == null && pe == null)
                return;

            float currentMotherMass = 1f;
            if (pmt != null)
                currentMotherMass = Mathf.Max(1f, pmt.TrackedMassKg);
            else
                currentMotherMass = Mathf.Max(1f, pe.Mass);

            float maxRemovable = Mathf.Max(0f, currentMotherMass - 1f);
            float actualRemoved = Mathf.Min(ExtraFiberFlatLossKg, maxRemovable);

            if (actualRemoved > 0f)
            {
                if (pmt != null)
                    pmt.AddExternalMass(-actualRemoved);
                else if (pe != null && mother) // double-check still valid
                    pe.Mass = Mathf.Max(1f, pe.Mass - actualRemoved);
            }
        }
    }

    // Decrease VineMother mass on any connected VineBranch harvest:
    // - Spawn 20% of current mother mass as Mud (at mother's temperature)
    // - Additionally subtract 1 kg from the mother (not spawned)
    // Clamp to 1 kg minimum remaining mass.
    [HarmonyPatch(typeof(VineBranch.Instance), nameof(VineBranch.Instance.SpawnHarvestedFruit))]
    internal static class VineBranch_Instance_SpawnHarvestedFruit_Patch
    {
        private const float ExtraFlatLossKg = 1f;
        private const float PercentageLoss = 0.20f;

        private static void Postfix(VineBranch.Instance __instance)
        {
            if (__instance == null)
                return;

            var branchGO = __instance.gameObject;
            if (branchGO == null || !branchGO)
                return;

            GameObject mother = __instance.Mother;
            if (mother == null || !mother)
                return;

            // Prefer PlantMassTrackerComponent if present
            var pmt = mother.GetComponent<PlantMassTrackerComponent>();
            var pe = pmt == null ? mother.GetComponent<PrimaryElement>() : mother.GetComponent<PrimaryElement>(); // we may still need PE for temperature

            // Determine current mass (prefer PMT's tracked mass if available)
            float currentMotherMass = 1f;
            if (pmt != null)
                currentMotherMass = Mathf.Max(1f, pmt.TrackedMassKg);
            else if (pe != null)
                currentMotherMass = Mathf.Max(1f, pe.Mass);
            else
                return; // nothing to adjust against

            // Calculate desired removals
            float percentLoss = currentMotherMass * PercentageLoss; // mass to spawn as mud
            float desiredTotalRemoval = percentLoss + ExtraFlatLossKg;

            // Do not drop below the 1 kg baseline
            float maxRemovable = Mathf.Max(0f, currentMotherMass - 1f);
            float actualRemoved = Mathf.Min(desiredTotalRemoval, maxRemovable);

            // Mud spawned is the portion from the percentage loss, capped by actualRemoved
            float mudToSpawn = Mathf.Min(percentLoss, actualRemoved);
            float extraLossApplied = Mathf.Max(0f, actualRemoved - mudToSpawn); // remainder toward the flat loss

            // Spawn mud at mother's temperature



            if (mudToSpawn > 0f)

            {
                float tempK = pe != null ? pe.Temperature : 293.15f;
                var mudElement = ElementLoader.FindElementByHash(SimHashes.Mud);

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
            if (actualRemoved > 0f && mother)
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
            if (__instance == null || __instance.gameObject == null || !__instance.gameObject)
                return;

            // Ensure this tinkerable applies FarmTinker
            if (!string.Equals(__instance.addedEffect, "FarmTinker"))
                return;

            // Is this tinkerable on a vine? Locate VineBranch.Instance in self or parents.
            var smc = __instance.gameObject.GetComponent<StateMachineController>();
            var branch = smc != null ? smc.GetSMI<VineBranch.Instance>() : null;
            if (branch == null)
                branch = __instance.gameObject.GetSMI<VineBranch.Instance>();
            if (branch == null)
                branch = __instance.GetComponentInParent<StateMachineController>()?.GetSMI<VineBranch.Instance>();
            if (branch == null)
                return;

            var mother = branch.Mother;
            if (mother == null || !mother)
                return;

            var pmt = mother.GetComponent<PlantMassTrackerComponent>();
            var pe = pmt == null ? mother.GetComponent<PrimaryElement>() : null;

            if (pmt == null && pe == null)
                return;

            if (pmt != null)
                pmt.AddExternalMass(FarmTinkerMotherBonusKg);
            else if (pe != null && mother)
                pe.Mass += FarmTinkerMotherBonusKg;
        }
    }
}