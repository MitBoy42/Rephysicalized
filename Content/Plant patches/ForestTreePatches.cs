using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    
    // Minimal patch: for ForestTree only, increase high-pressure thresholds 10x
    [HarmonyPatch(typeof(ForestTreeConfig), nameof(ForestTreeConfig.CreatePrefab))]
    internal static class ForestTreeConfig_CreatePrefab_PressurePatch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            var pv = __result.GetComponent<PressureVulnerable>();
            if (pv == null) return;

            // Ensure pressure sensitivity remains enabled (vanilla defaults to true)
            pv.pressure_sensitive = true;

            // Multiply high-pressure thresholds by 10
            // Vanilla defaults are ~10f (warning) and ~30f (lethal), make them 100 and 300
            pv.pressureWarning_High *= 10f;
            pv.pressureLethal_High *= 10f;
        }
    }


[HarmonyPatch(typeof(Db), "Initialize")]
    internal static class ForestPlant_Registration
    {
        public static void Postfix()
        {
            PlantMassTrackerRegistry.ApplyToCrop(
                   plantPrefabId: "ForestTree",
                   yields: new List<MaterialYield>
                   {
                        new MaterialYield("ToxicMud", 0.6f),
             new MaterialYield("WoodLog", 0.4f),
                   },
                   realHarvestSubtractKg: 0f

               );
        }
    }

    [HarmonyPatch(typeof(ForestTreeConfig), nameof(ForestTreeConfig.CreatePrefab))]
    public static class ForestTreeConfig_CreatePrefab_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;
            __result.AddOrGet<ForestTreeTrunkDrainController>();
         
        }

        // New desired rates (per second)
        private const float NewDirtyWaterRate = 0.225f;     // 135 kg/cycle
        private const float NewDirtRate = 0.05277778f;      // ~31.66668 kg/cycle

        // Original inlined rates in the stock method (per second)
        private const float OldDirtyWaterRate = 0.1166667f;
        private const float OldDirtRate = 0.01666667f;

        private const float Epsilon = 0.000001f;
        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= Epsilon;

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f)
                {
                    if (Approximately(f, OldDirtyWaterRate))
                        instr.operand = NewDirtyWaterRate;
                    else if (Approximately(f, OldDirtRate))
                        instr.operand = NewDirtRate;
                }

                yield return instr;
            }
        }

        [HarmonyPostfix]
        public static void SwapDirtToRichSoil(ref GameObject __result)
        {
                bool changed = false;

                // Update FertilizationMonitor.Def consumed elements (source of fertilizer requirement)
                var fertDef = __result.GetDef<FertilizationMonitor.Def>();
                if (fertDef != null && fertDef.consumedElements != null)
                {
                    var arr = fertDef.consumedElements;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i].tag == GameTags.Dirt)
                        {
                            arr[i].tag = ModTags.RichSoil;
                            changed = true;
                        }
                    }
                    fertDef.consumedElements = arr;
                }

                // Update delivery requests so farmers bring RichSoil instead of Dirt
                var deliveries = __result.GetComponents<ManualDeliveryKG>();
                if (deliveries != null)
                {
                    foreach (var md in deliveries)
                    {
                        if (md != null && md.RequestedItemTag == GameTags.Dirt)
                        {
                            md.RequestedItemTag = ModTags.RichSoil;
                            changed = true;
                        }
                    }
                }

                if (changed) Debug.Log("[ForestTreeFertilizerPatch] Replaced Dirt with RichSoil");
            }
         
        }
    

    [HarmonyLib.HarmonyPatch(typeof(MinionVitalsPanel), nameof(MinionVitalsPanel.GetFertilizationLabel))]
    internal static class MinionVitalsPanel_GetFertilizationLabel_Safe_Patch
    {
        private static bool Prefix(UnityEngine.GameObject go, ref string __result)
        {
            try
            {
                string label = Db.Get().Amounts.Fertilization.Name;

                if (go == null)
                {
                    __result = label;
                    return false;
                }

                var smi = go.GetSMI<FertilizationMonitor.Instance>();
                var consumed = smi?.def?.consumedElements;
                if (consumed == null || consumed.Length == 0)
                {
                    __result = label;
                    return false;
                }

                // Fertilizer usage multiplier (null-safe)
                float usageMult = 1f;
                var attrs = go.GetAttributes();
                if (attrs != null)
                {
                    var amt = attrs.Get(Db.Get().PlantAttributes.FertilizerUsageMod);
                    if (amt != null)
                        usageMult = amt.GetTotalValue();
                }

                for (int i = 0; i < consumed.Length; i++)
                {
                    var entry = consumed[i];

                    // Try resolve as element; fall back to tag name if it's a custom Tag
                    var elem = ElementLoader.GetElement(entry.tag);
                    string name = elem != null ? elem.name : entry.tag.ProperName();

                    // ONI formats per-cycle mass from per-second rates via GameUtil
                    float perSec = entry.massConsumptionRate * usageMult;

                    label += "\n    • " + name + " " + GameUtil.GetFormattedMass(perSec, GameUtil.TimeSlice.PerCycle);
                }

                __result = label;
                return false; // skip original to avoid its null assumptions
            }
            catch
            {
                // Fail-safe: return just the section header
                __result = Db.Get().Amounts.Fertilization.Name;
                return false;
            }
        }
    }



      

        // Ensure PMT component is present on branch prefab
        [HarmonyPatch(typeof(ForestTreeBranchConfig), nameof(ForestTreeBranchConfig.CreatePrefab))]
        internal static class ForestTreeBranchConfig_CreatePrefab_Patch
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result == null) return;
                __result.AddOrGet<PlantMassTrackerComponent>();
            }
        }

        // Register PMT configs and zero vanilla wood crop so only PMT handles branch drops
        [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
        internal static class ForestTree_PMT_Registration
        {
            private static void Postfix()
            {
                // Register branch to drop WoodLog via PMT (100% of mass-to-drop)
                PlantMassTrackerRegistry.ApplyToCrop(
                    plantPrefabId: "ForestTreeBranch",
                    yields: new List<MaterialYield> { new MaterialYield("WoodLog", 1f) },
                    realHarvestSubtractKg: 30f
                );

              
              
                // Zero vanilla WoodLog crop (prevents vanilla wood spawn on harvest)
            
                    var crops = TUNING.CROPS.CROP_TYPES;
                    string woodId = SimHashes.WoodLog.ToString(); // "WoodLog"
                    for (int i = 0; i < crops.Count; i++)
                    {
                        if (crops[i].cropId == woodId)
                        {
                            var c = crops[i];
                            crops[i] = new Crop.CropVal(c.cropId, c.cropDuration, 30);
                            break;
                        }
                    }

            }
        }

        // Initialize PMT for ForestTreeBranch instances when the SMI starts
        // PlantBranch has no OnSpawn; StartSM is the correct hook.
        [HarmonyPatch(typeof(PlantBranch.Instance), nameof(PlantBranch.Instance.StartSM))]
        internal static class PlantBranch_Instance_StartSM_ForestTreeBranch_Init_Patch
        {
            private static void Postfix(PlantBranch.Instance __instance)
            {

                var go = __instance.gameObject;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null || kpid.PrefabTag != (Tag)"ForestTreeBranch")
                    return;

                var pmt = go.AddOrGet<PlantMassTrackerComponent>();
                if (PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
                    pmt.InitializeFromConfig(cfg);
            }
        }

        // Trunk-to-branch mass transfer while branches are growing (and not wilting).
        // Each growing branch drains 50 kg per cycle from the trunk (cycle = 600 s => ~0.083333 kg/s per branch).
        // The trunk is clamped to a minimum mass of 1 kg; branches stop draining if trunk would drop below 1 kg.
        public sealed class ForestTreeTrunkDrainController : KMonoBehaviour, ISim1000ms
        {
            private const float DrainPerCycleKg = 25f; //For some reason is double counted idk why
            private const float SecondsPerCycle = 600f;
            private const float PerBranchRateKgPerSec = DrainPerCycleKg / SecondsPerCycle; // ~0.083333 kg/s

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
                if (_trunkWilt == null) _trunkWilt = GetComponent<WiltCondition>();
                if (_trunkWilt != null && _trunkWilt.IsWilting())
                    return; // No drain while trunk is wilting

                if (_grower == null)
                    _grower = gameObject.GetSMI<PlantBranchGrower.Instance>();
                if (_grower == null)
                    return;

                // Collect eligible branches: not fully grown AND not wilting
                var branches = ListPool<GameObject, ForestTreeTrunkDrainController>.Allocate();
                var eligible = ListPool<GameObject, ForestTreeTrunkDrainController>.Allocate();
                try
                {
                    _grower.ActionPerBranch(branch =>
                    {
                        if (branch != null)
                            branches.Add(branch);
                    });

                    if (branches.Count == 0)
                        return;

                    for (int i = 0; i < branches.Count; i++)
                    {
                        var b = branches[i];
                        if (b == null) continue;

                        var growing = b.GetComponent<Growing>();
                        if (growing != null && growing.IsGrown())
                            continue; // fully grown: no drain

                        var bwilt = b.GetComponent<WiltCondition>();
                        if (bwilt != null && bwilt.IsWilting())
                            continue; // wilted branch: no drain

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

                    // Transfer mass to each branch, scaled if trunk is near baseline
                    for (int i = 0; i < eligible.Count; i++)
                    {
                        float applied = perBranchDesired * scale;
                        if (applied <= 0f) continue;

                        var b = eligible[i];
                        AddMassToBranch(b, applied);
                        totalDrainApplied += applied;
                    }

                    // Subtract from trunk once
                    if (totalDrainApplied > 0f)
                        ApplyDrainToTrunk(totalDrainApplied);
                }
                finally
                {
                    branches.Recycle();
                    eligible.Recycle();
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
