using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{

    // Optional component: attach to a plant prefab to customize what the PlantMassTracker
    // spawns on harvest or uproot. If not present on a plant, PMT uses its base config yields.
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PlantMassTrackerYieldModifier : KMonoBehaviour
    {
        // If true, replace the base yields for harvest with 'harvestYields'.
        // If false, 'harvestYields' are added to the base yields.
        [Serialize] public bool overrideHarvestYields = false;

        // If true, replace the base yields for dig with 'digYields'.
        // If false, 'digYields' are added to the base yields.
        [Serialize] public bool overrideDigYields = false;

        // Extra/override yields when the plant is harvested.
        [Serialize] public List<MaterialYield> harvestYields = new List<MaterialYield>();

        // Extra/override yields when the plant is uprooted.
        [Serialize] public List<MaterialYield> digYields = new List<MaterialYield>();
    }
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
            // PMT registry: keep dig as Mud, subtract 1 kg on real harvest semantics (symbolic).
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: FlyTrapPlantConfig.ID,
                yields: new List<MaterialYield> { new MaterialYield("Mud", 1f) },
                realHarvestSubtractKg: 50f
          
            );

            // Set Amber crop to 1
            var list = CROPS.CROP_TYPES;
            string amberId = SimHashes.Amber.ToString();

            int idx = -1;
            float duration = 7200f;

            for (int i = 0; i < list.Count; i++)
            {
                object cv = list[i];
                var t = cv.GetType();
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                string id = null;
                foreach (var n in new[] { "cropId", "id", "productId", "CropId", "Id" })
                {
                    var f = t.GetField(n, BF);
                    if (f != null && f.FieldType == typeof(string)) { id = f.GetValue(cv) as string; break; }
                    var p = t.GetProperty(n, BF);
                    if (p != null && p.PropertyType == typeof(string) && p.CanRead) { id = p.GetValue(cv, null) as string; break; }
                }
                if (string.Equals(id, amberId, System.StringComparison.Ordinal))
                {
                    float? dur = null;
                    foreach (var n in new[] { "cropDuration", "time", "duration", "growthTime", "harvestTime", "growingTime" })
                    {
                        var f = t.GetField(n, BF);
                        if (f != null && f.FieldType == typeof(float)) { dur = (float)f.GetValue(cv); break; }
                        var p = t.GetProperty(n, BF);
                        if (p != null && p.PropertyType == typeof(float) && p.CanRead) { dur = (float)p.GetValue(cv, null); break; }
                    }
                    if (dur.HasValue) duration = dur.Value;
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                list[idx] = new Crop.CropVal(amberId, duration, 50);
     
            }
        }
    }

    // 2) Add DirtyWater irrigation requirement to Flytrap plant prefab.
    [HarmonyPatch(typeof(FlyTrapPlantConfig), nameof(FlyTrapPlantConfig.CreatePrefab))]
    internal static class FlyTrap_CreatePrefab_Patch
    {
        private const float IrrigationRateKgPerSecond = 0.03333f;

        private static void Postfix(ref GameObject __result)
        {
            EntityTemplates.ExtendPlantToIrrigated(
                __result,
                new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.DirtyWater.CreateTag(),
                        massConsumptionRate = IrrigationRateKgPerSecond
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

            // Attach modifier to THIS PMT's GO (no child/parent scans).
            var mod = __instance.gameObject.AddOrGet<PlantMassTrackerYieldModifier>();
            mod.overrideHarvestYields = true;
            if (mod.harvestYields == null) mod.harvestYields = new List<MaterialYield>(); else mod.harvestYields.Clear();
            mod.harvestYields.Add(new MaterialYield(SimHashes.Amber.ToString(), 1f));

            mod.overrideDigYields = true;
            if (mod.digYields == null) mod.digYields = new List<MaterialYield>(); else mod.digYields.Clear();
            mod.digYields.Add(new MaterialYield(SimHashes.Mud.ToString(), 1f));

          //  FlyTrapPatchDebug.Log($"PMT.OnSpawn: attached yield modifier to PMT GO '{__instance.gameObject.name}'.");
        }
    }

    // 4) When Flytrap consumes a prey, add that prey's mass to the SAME PMT instance before the prey is deleted.
    //    This uses the exact monitor you focused. We hook Instance.OnPickupableLayerObjectDetected.
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