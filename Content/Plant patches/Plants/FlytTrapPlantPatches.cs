using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{


    internal static class FlyTrapPatchDebug
    {
        public static bool Enabled = false;
        public static void Log(string msg)
        {
            if (Enabled) Debug.Log($"[Rephysicalized][FlyTrap] {msg}");
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class FlyTrap_Db_Initialize_Patches
    {
        private static void Postfix()
        {
            // PMT registry: keep dig as Sand, subtract 1 kg on real harvest semantics (symbolic).
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: FlyTrapPlantConfig.ID,
                yields: new List<MaterialYield> { new MaterialYield("Sand", 1f) },
                realHarvestSubtractKg: 50f
          
            );

            CROPS.CROP_TYPES[28] = new Crop.CropVal(SimHashes.Amber.ToString(), 7200f, 50);
        }
    }

    // 2) Add sulfur
    [HarmonyPatch(typeof(FlyTrapPlantConfig), nameof(FlyTrapPlantConfig.CreatePrefab))]
    internal static class FlyTrap_CreatePrefab_Patch
    {
        private const float FertilizerRateKgPerSecond = 0.03333f;

        private static void Postfix(ref GameObject __result)
        {
            EntityTemplates.ExtendPlantToFertilizable(  __result,
                new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Sulfur.CreateTag(),
                        massConsumptionRate = FertilizerRateKgPerSecond
                    }
                }
            );
      
        }
    }

    // 3) Ensure the yield modifier sits on the SAME PMT instance used by the plant, with no hierarchy crawling.
    //    We attach the component to the PMT in its OnSpawn (FlyTrap only).
    [HarmonyPatch(typeof(PlantMassTrackerComponent), "OnSpawn")]
    internal static class PMT_OnSpawn_AttachModifier_For_FlyTrap
    {
        private static void Postfix(PlantMassTrackerComponent __instance)
        {
            var myId = __instance.GetComponentInParent<KPrefabID>();
            if (myId == null) return;

            // Only for Flytrap plants
            if (!string.Equals(myId.PrefabID().Name, FlyTrapPlantConfig.ID, System.StringComparison.Ordinal))
                return;

            var mod = __instance.gameObject.AddOrGet<PlantMassTrackerYieldModifier>();
            mod.overrideHarvestYields = true;
            if (mod.harvestYields == null) mod.harvestYields = new List<MaterialYield>(); else mod.harvestYields.Clear();
            mod.harvestYields.Add(new MaterialYield(SimHashes.Amber.ToString(), 1f));

            mod.overrideDigYields = true;
            if (mod.digYields == null) mod.digYields = new List<MaterialYield>(); else mod.digYields.Clear();
            mod.digYields.Add(new MaterialYield(SimHashes.Sand.ToString(), 1f));

        }
    }

    // 4) When Flytrap consumes a prey, add that prey's mass to the SAME PMT instance before the prey is deleted.
    //   . We hook Instance.OnPickupableLayerObjectDetected.
    [HarmonyPatch(typeof(FlytrapConsumptionMonitor.Instance), nameof(FlytrapConsumptionMonitor.Instance.OnPickupableLayerObjectDetected))]
    internal static class FlytrapConsumption_AddPreyMass_To_PMT
    {
        private static void Prefix(FlytrapConsumptionMonitor.Instance __instance, object obj)
        {
            // Validate prey
            var pickup = obj as Pickupable;
            if (pickup == null) return;

            var preyGo = pickup.gameObject;

            // Only add if edible (mirrors original check)
            var master = __instance.master;
            if (master == null || !master.IsEntityEdible(preyGo))
                return;

            // Get prey mass
            var pe = preyGo.GetComponent<PrimaryElement>();
            float preyMass = pe != null ? pe.Mass : 0f;
            if (preyMass <= 0f) return;

            // Add to the SAME PMT instance on this plant (no hierarchy crawling).
            var pmt = __instance.GetComponent<PlantMassTrackerComponent>();
            if (pmt == null)
            {
                return;
            }

            pmt.AddPreyMass(preyMass);
       
        }
    }
}