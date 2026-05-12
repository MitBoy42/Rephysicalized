using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Klei.AI;
using KSerialization;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
   


    internal static class CritterTrapUtil
    {
        private static Tag _critterTrapTag;
        private static bool _init;

        private static void EnsureInit()
        {
            if (_init) return;
            _critterTrapTag = new Tag(CritterTrapPlantConfig.ID);
            _init = true;
        }

        internal static bool IsCritterTrap(GameObject go)
        {
            if (go == null) return false;
            EnsureInit();
            var kpid = go.GetComponent<KPrefabID>();
            return kpid != null && kpid.HasTag(_critterTrapTag);
        }
    }

    // Harvest-time: Only trigger HarvestComplete so PlantMassTracker resets to 1 kg.
    // Signature in your build: void Crop.SpawnConfiguredFruit(object callbackParam)
    [HarmonyPatch(typeof(Crop), nameof(Crop.SpawnConfiguredFruit))]
    internal static class Crop_SpawnConfiguredFruit_CritterTrap_Postfix
    {
        private static void Postfix(Crop __instance, object callbackParam)
        {
            if (__instance == null) return;

            var go = __instance.gameObject;
            if (!CritterTrapUtil.IsCritterTrap(go))
                return;

            // Ensure trackers (e.g., PlantMassTracker) are notified of a completed harvest
            try
            {
                go.Trigger((int)GameHashes.HarvestComplete);
            }
            catch
            {
                // Swallow to avoid interfering with harvest flow
            }
        }
    }




    // Keep your CreatePrefab transpiler if you need the other float tweaks (mass/water rate).
    [HarmonyPatch(typeof(CritterTrapPlantConfig), nameof(CritterTrapPlantConfig.CreatePrefab))]
    public static class CritterTrapPlant_CreatePrefab_Transpiler
    {
        private const float OldIrrigationRate = 0.01666667f;
        private const float NewIrrigationRate = 0.00833333f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool massReplaced = false;
            bool waterRateReplaced = false;

            foreach (var instr in instructions)
            {
                if (!massReplaced && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f1 && Math.Abs(f1 - 4f) < 0.0001f)
                {
                    massReplaced = true;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                    continue;
                }

                if (!waterRateReplaced && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2 - OldIrrigationRate) < 1e-6f)
                {
                    waterRateReplaced = true;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, NewIrrigationRate);
                    continue;
                }

                yield return instr;
            }
        }

        // Ensure our controller is added at prefab-time so it serializes and survives reloads
        public static void Postfix(GameObject __result)
        {
            if (__result == null) return;

            // Digest/emit controller
            __result.AddOrGet<CritterTrapPlantRework>();

            // IMPORTANT: do NOT add any HarvestMassReset component; PlantMassTracker will handle resetting to 1 kg.
        }
    }

    // On trap: capture prey mass
    [HarmonyPatch(typeof(CritterTrapPlant.StatesInstance), nameof(CritterTrapPlant.StatesInstance.OnTrapTriggered))]
    public static class CritterTrapPlant_OnTrapTriggered_Patch
    {
        public static void Postfix(CritterTrapPlant.StatesInstance __instance, object data)
        {
            if (__instance?.master == null) return;
            var rework = __instance.master.gameObject.AddOrGet<CritterTrapPlantRework>();
            rework.OnTrapTriggered(data);
        }
    }

    // Prevent vanilla emission when our rework is active
    [HarmonyPatch(typeof(CritterTrapPlant.StatesInstance), nameof(CritterTrapPlant.StatesInstance.AddGas))]
    public static class CritterTrapPlant_AddGas_Override
    {
        public static bool Prefix(CritterTrapPlant.StatesInstance __instance, float dt)
        {
            var master = __instance?.master;
            if (master == null) return true;

            var rework = master.gameObject.GetComponent<CritterTrapPlantRework>();
            if (rework != null && rework.IsDigesting)
                return false; // skip vanilla; we emit in Sim1000ms

            return true;
        }
    }

    // UI growth percent: override the method your build actually uses (PercentOfCurrentHarvest).
    [HarmonyPatch(typeof(Growing), nameof(Growing.PercentOfCurrentHarvest))]
    public static class Growing_PercentOfCurrentHarvest_Patch
    {
        public static bool Prefix(Growing __instance, ref float __result)
        {
            if (__instance == null) return true;

            var rework = __instance.GetComponent<CritterTrapPlantRework>();
            if (rework == null || !rework.IsDigesting) return true;

            __result = rework.CurrentProgressPercent();
            return false; // override while digesting
        }
    }

    // Tooltip "Growing" line override: patch StatusItem.ResolveString for the "Growing" status item only.
    [HarmonyPatch(typeof(StatusItem))]
    public static class StatusItem_ResolveString_GrowthOverride
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var m in typeof(StatusItem).GetMethods(flags))
            {
                if (m.Name == "ResolveString" && m.ReturnType == typeof(string))
                    yield return m;
            }
        }

        static void Postfix(StatusItem __instance, MethodBase __originalMethod, object[] __args, ref string __result)
        {
            try
            {
                // Only touch the specific "Growing" status item
                if (__instance == null || string.IsNullOrEmpty(__instance.Id) ||
                    !string.Equals(__instance.Id, "Growing", StringComparison.OrdinalIgnoreCase))
                    return;

                // Try to extract Growing component from the arguments
                Growing growing = null;
                if (__args != null)
                {
                    for (int i = 0; i < __args.Length; i++)
                    {
                        if (__args[i] is Growing g) { growing = g; break; }
                        if (__args[i] is KMonoBehaviour km && km != null)
                        {
                            var gg = km.GetComponent<Growing>();
                            if (gg != null) { growing = gg; break; }
                        }
                    }
                }
                if (growing == null) return;

                var rework = growing.GetComponent<CritterTrapPlantRework>();
                if (rework == null || !rework.IsDigesting) return;

                float p01 = rework.CurrentProgressPercent();
                float pct = Mathf.Round(p01 * 1000f) * 0.1f; // one decimal
                __result = $"Growth: {pct:0.0}%";
            }
            catch
            {
                // swallow to avoid breaking status item rendering
            }
        }
    }

    // Controller: Sim1000ms emission, save persistence, percent computation
    // Mass gain/loss is integrated via PlantMassTracker (if present). We avoid writing PMT internals.
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class CritterTrapPlantRework : KMonoBehaviour, ISim1000ms
    {
        // Duration mapping (seconds)
        private const float MinDurationSeconds = 600f;   // at 1 kg (or smaller)
        private const float MaxDurationSeconds = 12000f; // at >= 100 kg (cap can be tuned)

        // Mass reference points
        private const float MinReferenceMass = 1f;
        private const float MaxReferenceMass = 500f;

        // Serialized state (persists through save/load)
        [Serialize] private bool isDigesting;
        [Serialize] private float totalHydrogen;        // kg, equals prey mass
        [Serialize] private float hydrogenRemaining;    // kg, decremented as gas is emitted
        [Serialize] private float duration;             // seconds
        [Serialize] private float elapsed;              // seconds

        // Cached components
        [MyCmpGet] private CritterTrapPlant plant;
        [MyCmpGet] private Growing growing;
        [MyCmpGet] private PrimaryElement primaryElement;
        [MyCmpGet] private Storage storage;

        public bool IsDigesting => isDigesting;

        // Trap event provides the prey via data (GO or KPrefabID)
        public void OnTrapTriggered(object data)
        {
            float preyMass = ExtractMass(data);

            // Fallback: try to read mass from a creature already in storage, if any
            if (preyMass <= 0f && storage != null)
            {
                var critter = storage.FindFirst(GameTags.Creature);
                if (critter != null)
                {
                    var pe = critter.GetComponent<PrimaryElement>();
                    if (pe != null) preyMass = pe.Mass;
                }
            }
            if (preyMass <= 0f) preyMass = MinReferenceMass;

            totalHydrogen = Mathf.Max(0f, preyMass);
            hydrogenRemaining = totalHydrogen;
            duration = ComputeDuration(preyMass);
            elapsed = 0f;
            isDigesting = true;

            // Apply mass gain through PMT if available (visual will update via PMT); otherwise fall back to PE.
            ApplyMassDelta(+preyMass);
        }

        public void Sim1000ms(float dt)
        {
            if (!isDigesting)
                return;

            elapsed += dt;

            // Emit hydrogen at a deterministic rate
            if (totalHydrogen > 0f && duration > 0f && storage != null && hydrogenRemaining > 0f)
            {
                float rate = totalHydrogen / duration; // kg/s
                float toEmit = Mathf.Min(rate * dt, hydrogenRemaining);
                if (toEmit > 0f)
                {
                    float temperature = (primaryElement != null ? primaryElement.Temperature : 293.15f) + 10f;

                    storage.AddGasChunk(SimHashes.Hydrogen, toEmit, temperature, byte.MaxValue, 0, keep_zero_mass: false);

                    // Subtract mass through PMT if available (visual will update via PMT); else fall back to PE.
                    ApplyMassDelta(-toEmit);

                    hydrogenRemaining -= toEmit;

                    // Vent when storage chunk exceeds the plant's threshold
                    if (plant != null)
                    {
                        var h2 = storage.FindPrimaryElement(SimHashes.Hydrogen);
                        if (h2 != null && h2.Mass >= plant.gasVentThreshold)
                        {
                            int cell = Grid.PosToCell(transform.GetPosition());
                            SimMessages.AddRemoveSubstance(cell, h2.ElementID, CellEventLogger.Instance.Dumpable, h2.Mass, h2.Temperature, h2.DiseaseIdx, h2.DiseaseCount);
                            storage.ConsumeIgnoringDisease(h2.gameObject);
                        }
                    }
                }
            }

            // Keep state machine/tooltip refresh going
            try { gameObject.Trigger((int)GameHashes.Grow, null); } catch { /* ignore */ }

            // Stop if uprooted/harvested
            if (plant == null || growing == null)
                isDigesting = false;
        }

        public bool HasCompletedDigest()
        {
            // Done when all hydrogen has been generated or elapsed >= duration
            return isDigesting && (hydrogenRemaining <= 0.0001f || elapsed >= (duration - 0.0001f));
        }

        // UI progress based on time (matches dynamic duration)
        public float CurrentProgressPercent()
        {
            if (isDigesting && duration > 0f)
                return Mathf.Clamp01(elapsed / duration);

            return 0f;
        }

        private static float ComputeDuration(float mass)
        {
            float clamped = Mathf.Clamp(mass, MinReferenceMass, MaxReferenceMass);
            float t = Mathf.InverseLerp(MinReferenceMass, MaxReferenceMass, clamped);
            return Mathf.Lerp(MinDurationSeconds, MaxDurationSeconds, t);
        }

        private static float ExtractMass(object data)
        {
            try
            {
                if (data is GameObject go)
                {
                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe != null) return pe.Mass;
                }
                if (data is KPrefabID kpid && kpid != null)
                {
                    var pe = kpid.GetComponent<PrimaryElement>();
                    if (pe != null) return pe.Mass;
                }
            }
            catch { /* ignore */ }
            return 0f;
        }

        // Apply mass delta and integrate with PlantMassTracker when present.
        private void ApplyMassDelta(float deltaKg)
        {
            var pmt = gameObject.GetComponent<PlantMassTrackerComponent>();
            if (pmt != null)
            {
                // Let PMT own the visual PrimaryElement mass changes. We only move tracked mass here.
                // This avoids baseline resets that hide irrigation gains during digestion.
                if (deltaKg > 0f) pmt.AddPreyMass(deltaKg);
                else if (deltaKg < 0f) pmt.AddPreyMass(deltaKg); // negative reduces tracked mass
            }

        }
    }
}