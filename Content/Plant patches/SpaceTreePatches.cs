using HarmonyLib;
using Klei.AI;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Rephysicalized
{
    // Adjust SpaceTree prefab: storage capacity and attach trunk controller
    [HarmonyPatch(typeof(SpaceTreeConfig), nameof(SpaceTreeConfig.CreatePrefab))]
    public static class SpaceTreeConfig_CreatePrefab_Patch
    {
        private const float NewStorageCapacity = 100f; // 20 -> 100

        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
    
            __result.AddOrGet<SpaceTreeTrunkDrainController>();

          
                var storage = __result.GetComponent<Storage>();
                if (storage != null)
                    storage.capacityKg = NewStorageCapacity;

                var edible = __result.GetComponent<DirectlyEdiblePlant_StorageElement>();
                if (edible != null)
                    edible.storageCapacity = NewStorageCapacity;
            
          
        }


        // Register PMT for SpaceTreeBranch (drops WoodLog via PMT)
        [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
        internal static class SpaceTreeBranch_PMT_Registration
        {
            private static void Postfix()
            {
                PlantMassTrackerRegistry.ApplyToCrop(
                    plantPrefabId: "SpaceTreeBranch",
                    yields: new List<MaterialYield>
                    {
                    new MaterialYield("WoodLog", 1f)
                    },
                    realHarvestSubtractKg: 0f
                );
            }
        }


        [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
        internal static class SpaceTree_PMT_Registration
        {
            private static void Postfix()
            {
                PlantMassTrackerRegistry.ApplyToCrop(
                    plantPrefabId: "SpaceTree",
                    yields: new List<MaterialYield>
                    {
                    new MaterialYield("Mud", 0.6f),
                           new MaterialYield("WoodLog", 0.4f)
                    },
                    realHarvestSubtractKg: 0f
                );
            }
        }

        // Initialize PMT on SpaceTreeBranch instances when their SMI starts
        [HarmonyPatch(typeof(PlantBranch.Instance), nameof(PlantBranch.Instance.StartSM))]
        internal static class PlantBranch_Instance_StartSM_SpaceTreeBranch_Init_Patch
        {
            private static void Postfix(PlantBranch.Instance __instance)
            {
                if (__instance == null) return;

                var go = __instance.gameObject;
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null || kpid.PrefabTag != (Tag)"SpaceTreeBranch")
                    return;

                var pmt = go.AddOrGet<PlantMassTrackerComponent>();
                if (PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
                    pmt.InitializeFromConfig(cfg);
            }
        }

        // Suppress vanilla wood spawn from SpaceTree branches (harvest and on death)
        [HarmonyPatch(typeof(SpaceTreeBranch), nameof(SpaceTreeBranch.SpawnWoodOnHarvest))]
        internal static class SpaceTreeBranch_SpawnWoodOnHarvest_Patch
        {
            private static bool Prefix(SpaceTreeBranch.Instance smi) => false;
        }

        [HarmonyPatch(typeof(SpaceTreeBranch), nameof(SpaceTreeBranch.SpawnWoodOnDeath))]
        internal static class SpaceTreeBranch_SpawnWoodOnDeath_Patch
        {
            private static bool Prefix(SpaceTreeBranch.Instance smi) => false;
        }

        // IMPORTANT: We disable extra production multipliers here to avoid changing current domesticated tuning and to keep wild vanilla.
        // This patch becomes a no-op for both wild and domesticated to prevent accidental double scaling.
        [HarmonyPatch(typeof(SpaceTreePlant.Instance), nameof(SpaceTreePlant.Instance.GetProductionSpeed))]
        internal static class SpaceTreePlant_Instance_GetProductionSpeed_Scale_Patch
        {
            private const float BaseFactor = 1f;

            private static void Postfix(SpaceTreePlant.Instance __instance, ref float __result)
            {
                // Intentionally left as a no-op to preserve existing domesticated tuning and vanilla wild behavior.
                __result *= BaseFactor;
            }
        }

        // Disable extra lux scaling patch to avoid increasing rates beyond current expectations.
        [HarmonyPatch(typeof(SpaceTreePlant.Instance), nameof(SpaceTreePlant.Instance.GetProductionSpeed))]
        internal static class SpaceTreePlant_Instance_GetProductionSpeed_ExtraLux_Patch
        {
            private static void Postfix(SpaceTreePlant.Instance __instance, ref float __result)
            {
                // No-op: we do not change speed here. Domesticated output is determined by the custom ProduceUpdate.
                // Wild stays vanilla and will be scaled post-production by WildScale (below).
                return;
            }
        }

        // Trunk-to-branch mass transfer while branches are growing (and not wilting).
        // Each growing branch drains 10 kg per cycle (0.0166667 kg/s) from the trunk; mass is transferred to the branch.
        // WILD GATED: wild trees do not drain at all.
        public sealed class SpaceTreeTrunkDrainController : KMonoBehaviour, ISim1000ms
        {
            private const float DrainPerCycleKg = 10f;
            private const float SecondsPerCycle = 600f;
            private const float PerBranchRateKgPerSec = DrainPerCycleKg / SecondsPerCycle; // 0.0166667 kg/s

            [MyCmpGet] private PrimaryElement _trunkPE;
            [MyCmpGet] private WiltCondition _trunkWilt;
            private PlantMassTrackerComponent _trunkPMT;
            private PlantBranchGrower.Instance _grower;

            public override void OnSpawn()
            {
                base.OnSpawn();
                _trunkPMT = GetComponent<PlantMassTrackerComponent>();
                _grower = gameObject.GetSMI<PlantBranchGrower.Instance>();
            }

            public void Sim1000ms(float dt)
            {
                // Wild plants never drain
                var plant = gameObject.GetSMI<SpaceTreePlant.Instance>();
                if (plant != null && plant.IsWildPlanted)
                    return;

                if (_trunkWilt == null) _trunkWilt = GetComponent<WiltCondition>();
                if (_trunkWilt != null && _trunkWilt.IsWilting())
                    return; // no drain while trunk is wilting

                if (_grower == null)
                    _grower = gameObject.GetSMI<PlantBranchGrower.Instance>();
                if (_grower == null)
                    return;

                // Collect unique branches
                var unique = HashSetPool<GameObject, SpaceTreeTrunkDrainController>.Allocate();
                try
                {
                    _grower.ActionPerBranch(branch =>
                    {
                        if (branch != null)
                            unique.Add(branch);
                    });

                    if (unique.Count == 0)
                        return;

                    // Filter eligible branches: not fully grown AND not wilting
                    var eligible = ListPool<GameObject, SpaceTreeTrunkDrainController>.Allocate();
                    try
                    {
                        foreach (var b in unique)
                        {
                            if (b == null) continue;

                            var smi = b.GetSMI<SpaceTreeBranch.Instance>();
                            if (smi == null) continue;
                            if (smi.IsBranchFullyGrown) continue;
                            if (smi.wiltCondition != null && smi.wiltCondition.IsWilting()) continue;

                            eligible.Add(b);
                        }

                        if (eligible.Count == 0)
                            return;

                        float trunkMass = GetTrunkMass();
                        float availableAboveBaseline = trunkMass - 1f;
                        if (availableAboveBaseline <= 0f)
                            return;

                        float perBranchDesired = Mathf.Max(0.001f, dt) * PerBranchRateKgPerSec;
                        float totalDesired = perBranchDesired * eligible.Count;

                        float scale = totalDesired > 0f ? Mathf.Min(1f, availableAboveBaseline / totalDesired) : 0f;
                        if (scale <= 0f)
                            return;

                        float totalDrainApplied = 0f;

                        foreach (var b in eligible)
                        {
                            float applied = perBranchDesired * scale;
                            if (applied <= 0f) continue;

                            AddMassToBranch(b, applied);
                            totalDrainApplied += applied;
                        }

                        if (totalDrainApplied > 0f)
                            ApplyDrainToTrunk(totalDrainApplied);
                    }
                    finally
                    {
                        eligible.Recycle();
                    }
                }
                finally
                {
                    unique.Recycle();
                }
            }

            private float GetTrunkMass()
            {
                if (_trunkPMT != null)
                    return Mathf.Max(0f, _trunkPMT.TrackedMassKg);
                if (_trunkPE != null)
                    return Mathf.Max(0f, _trunkPE.Mass);
                return 0f;
            }

            private void ApplyDrainToTrunk(float kg)
            {
                if (kg <= 0f) return;

                if (_trunkPMT != null)
                {
                    _trunkPMT.AddExternalMass(-kg);
                    if (_trunkPE != null)
                        _trunkPE.Mass = Mathf.Max(1f, _trunkPE.Mass - kg);
                }
                else if (_trunkPE != null)
                {
                    _trunkPE.Mass = Mathf.Max(1f, _trunkPE.Mass - kg);
                }
            }

            private static void AddMassToBranch(GameObject branch, float kg)
            {
                if (branch == null || kg <= 0f) return;

                var pmt = branch.GetComponent<PlantMassTrackerComponent>();
                if (pmt != null)
                {
                    pmt.AddExternalMass(+kg);
                    var pe = branch.GetComponent<PrimaryElement>();
                    if (pe != null) pe.Mass = Mathf.Max(1f, pe.Mass + kg);
                    return;
                }

                var bpe = branch.GetComponent<PrimaryElement>();
                if (bpe != null)
                    bpe.Mass = Mathf.Max(1f, bpe.Mass + kg);
            }
        }
    }

    // Domesticated: run custom override; Wild: let vanilla run, then scale down to 0.4x of vanilla result.
    [HarmonyPatch(typeof(SpaceTreePlant.Instance), nameof(SpaceTreePlant.Instance.ProduceUpdate))]
    internal static class SpaceTreePlant_Instance_ProduceUpdate_GatedAddLiquid_Patch
    {
        private static bool Prefix(SpaceTreePlant.Instance __instance, float dt)
        {
         
                if (__instance == null) return true;

                // Wild trees: do NOT override; let vanilla handle ProduceUpdate
                if (__instance.IsWildPlanted)
                    return true;

                var go = __instance.gameObject;
                if (go == null) return true;

                var storage = go.GetComponent<Storage>();
                var pe = go.GetComponent<PrimaryElement>();
                if (storage == null || pe == null) return true;

                // Vanilla desired mass for this tick:
                // desired = dt / OptimalProductionDuration * GetProductionSpeed() * storage.capacityKg
                float speed = __instance.GetProductionSpeed();
                float duration = __instance.OptimalProductionDuration > 0f ? __instance.OptimalProductionDuration : 150f;
                float capacity = storage.capacityKg;

                float desired = Mathf.Max(0f, dt / duration * speed * capacity / 5f);

                // Clamp by storage remaining and trunk PE mass above 1 kg baseline.
                float remaining = storage.RemainingCapacity();
                float trunkAvailable = Mathf.Max(0f, pe.Mass - 1f);

                float produced = Mathf.Min(desired, remaining, trunkAvailable);
                if (produced <= 1e-09f)
                    return false; // skip vanilla too (nothing to add)

                // Vanilla temperature rule: at least element.lowTemp + 8 K
                var sugarElem = ElementLoader.FindElementByHash(SimHashes.SugarWater);
                if (sugarElem == null)
                    return false;

                float minTemp = sugarElem.lowTemp + 8f;
                float tempK = Mathf.Max(pe.Temperature, minTemp);

                // Use Storage.AddLiquid to add sugarwater
                storage.AddLiquid(SimHashes.SugarWater, produced, tempK, byte.MaxValue, 0);

                // Subtract identical mass from trunk PrimaryElement
                pe.Mass = Mathf.Max(1f, pe.Mass - produced);

                // PMT sync
                var pmt = go.GetComponent<PlantMassTrackerComponent>();
                if (pmt != null)
                    pmt.AddExternalMass(-produced);

                // Domesticated handled here
                return false;
      
        }
    }

    // Wild production scaler: reduce vanilla net sugarwater added this tick to 40% for wild trees.
    [HarmonyPatch(typeof(SpaceTreePlant.Instance), nameof(SpaceTreePlant.Instance.ProduceUpdate))]
    internal static class SpaceTreePlant_Instance_ProduceUpdate_WildScale_Pre
    {
        private sealed class BoxF { public float V; public BoxF(float v) { V = v; } }
        private static readonly ConditionalWeakTable<SpaceTreePlant.Instance, BoxF> PreMass = new();

        private static void Prefix(SpaceTreePlant.Instance __instance)
        {
           if (__instance == null || !__instance.IsWildPlanted) return;
                var storage = __instance.GetComponent<Storage>();
                float pre = storage != null ? storage.MassStored() : 0f;
                PreMass.Remove(__instance);
                PreMass.Add(__instance, new BoxF(pre));
          
        }

        public static bool TryGet(SpaceTreePlant.Instance i, out float v)
        {
            if (i != null && PreMass.TryGetValue(i, out var b)) { v = b.V; return true; }
            v = 0f; return false;
        }

        public static void Clear(SpaceTreePlant.Instance i)
        {
            if (i != null) PreMass.Remove(i);
        }
    }

    [HarmonyPatch(typeof(SpaceTreePlant.Instance), nameof(SpaceTreePlant.Instance.ProduceUpdate))]
    internal static class SpaceTreePlant_Instance_ProduceUpdate_WildScale_Post
    {
        private const float WildScale = 0.02f; 

        private static void Postfix(SpaceTreePlant.Instance __instance, float dt)
        {
            try
            {
                if (__instance == null || !__instance.IsWildPlanted) return;

                var storage = __instance.GetComponent<Storage>();
                if (storage == null) return;

                if (!SpaceTreePlant_Instance_ProduceUpdate_WildScale_Pre.TryGet(__instance, out var pre))
                    return;

                float post = storage.MassStored();
                float delta = post - pre;
                if (delta <= 1e-06f)
                    return;

                float desired = delta * Mathf.Clamp01(WildScale);
                float toRemove = delta - desired;
                if (toRemove <= 1e-06f)
                    return;

                float remaining = toRemove;
                var items = storage.items;
                if (items == null) return;

                for (int i = items.Count - 1; i >= 0 && remaining > 1e-06f; i--)
                {
                    var go = items[i];
                    if (go == null) continue;
                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe == null || pe.ElementID != SimHashes.SugarWater) continue;

                    float take = Mathf.Min(pe.Mass, remaining);
                    if (take >= pe.Mass - 1e-06f)
                    {
                        remaining -= pe.Mass;
                        storage.Remove(go, true);
                        Util.KDestroyGameObject(go);
                    }
                    else
                    {
                        pe.Mass -= take;
                        remaining -= take;
                    }
                }
            }
            finally
            {
                SpaceTreePlant_Instance_ProduceUpdate_WildScale_Pre.Clear(__instance);
            }
        }
    }

    // Show >100% productivity on branch UI by extending Productivity up to 2.0 at 100k lux.
    [HarmonyPatch(typeof(SpaceTreeBranch.Instance), "get_Productivity")]
    internal static class SpaceTreeBranch_Instance_GetProductivity_Scale_Patch
    {
        private const float MaxLuxForScaling = 100000f;

        public static void Postfix(SpaceTreeBranch.Instance __instance, ref float __result)
        {
            // Keep 0 for not fully grown, same as vanilla
            if (__instance == null || !__instance.IsBranchFullyGrown)
                return;

            // Extended productivity for UI only
            int lux = __instance.CurrentAmountOfLux;
            int optimal = Mathf.Max(1, __instance.def.OPTIMAL_LUX_LEVELS);

            float value;
            if (lux <= optimal)
            {
                value = (float)lux / optimal;
            }
            else
            {
                float t = Mathf.Clamp01((lux - (float)optimal) / (MaxLuxForScaling - (float)optimal));
                value = 1f + t; // 1..2
            }

            __result = Mathf.Clamp(value, 0f, 2f);
        }
    }
}
