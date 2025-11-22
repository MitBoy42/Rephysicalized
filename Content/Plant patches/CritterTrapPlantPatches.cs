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
    // Primary: rewrite the CROPS static initializer so that
    // new Crop.CropVal("PlantMeat", 18000f, 10) becomes ... , 2)
    [HarmonyPatch(typeof(CROPS), MethodType.StaticConstructor)]
    internal static class Crops_PlantMeatAmount_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < list.Count; i++)
            {
                var ins = list[i];

                if (ins.opcode == OpCodes.Ldstr && ins.operand is string s && s == "PlantMeat")
                {
                    // Pattern:
                    // ldstr "PlantMeat"
                    // ldc.r4 18000f
                    // ldc.i4.s 10 (or ldc.i4 10, or ldc.r4 10f)
                    // newobj instance void Crop/CropVal::.ctor(string, float32, int32)
                    int amountIndex = -1;

                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var c = list[j];

                        if (c.opcode == OpCodes.Newobj)
                        {
                            var ctor = c.operand as ConstructorInfo;
                            if (ctor != null &&
                                ctor.DeclaringType != null &&
                                ctor.DeclaringType.FullName != null &&
                                ctor.DeclaringType.FullName.Contains("Crop+CropVal"))
                            {
                                if (amountIndex >= 0)
                                {
                                    var num = list[amountIndex];

                                    if (num.opcode == OpCodes.Ldc_I4 || num.opcode == OpCodes.Ldc_I4_S ||
                                        num.opcode == OpCodes.Ldc_I4_0 || num.opcode == OpCodes.Ldc_I4_1 ||
                                        num.opcode == OpCodes.Ldc_I4_2 || num.opcode == OpCodes.Ldc_I4_3 ||
                                        num.opcode == OpCodes.Ldc_I4_4 || num.opcode == OpCodes.Ldc_I4_5 ||
                                        num.opcode == OpCodes.Ldc_I4_6 || num.opcode == OpCodes.Ldc_I4_7 ||
                                        num.opcode == OpCodes.Ldc_I4_8)
                                    {
                                        // Force int 2
                                        num.opcode = OpCodes.Ldc_I4_2;
                                        num.operand = null;
                                    }
                                    else if (num.opcode == OpCodes.Ldc_R4)
                                    {
                                        num.operand = 1f;
                                    }
                                    else if (num.opcode == OpCodes.Ldc_R8)
                                    {
                                        num.operand = 1.0;
                                    }
                                    else
                                    {
                                        // Fallback: replace with ldc.i4.2 regardless
                                        num.opcode = OpCodes.Ldc_I4_2;
                                        num.operand = null;
                                    }

                                    list[amountIndex] = num;
                                    patched = true;
                                }
                                break; // done scanning this PlantMeat occurrence
                            }
                            break; // hit a different constructor; stop scanning this occurrence
                        }

                        // Track the last numeric literal after "PlantMeat" and before newobj;
                        // in this initializer it's the '10' amount.
                        if (IsNumericLiteral(c))
                            amountIndex = j;
                    }

                    if (patched) break; // only one PlantMeat entry expected
                }
            }



            return list;
        }

        private static bool IsNumericLiteral(CodeInstruction c)
        {
            if (c == null) return false;
            var op = c.opcode;

            if (op == OpCodes.Ldc_R4 || op == OpCodes.Ldc_R8 ||
                op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S ||
                op == OpCodes.Ldc_I4_M1 || op == OpCodes.Ldc_I4_0 ||
                op == OpCodes.Ldc_I4_1 || op == OpCodes.Ldc_I4_2 ||
                op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 ||
                op == OpCodes.Ldc_I4_5 || op == OpCodes.Ldc_I4_6 ||
                op == OpCodes.Ldc_I4_7 || op == OpCodes.Ldc_I4_8)
            {
                return true;
            }

            return false;
        }
    }

    // Fallback and guarantee: even if CROPS was initialized before Harmony applied the transpiler,
    // make sure the entry in CROP_TYPES is corrected before prefabs are created in Db.Initialize.
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class Db_Initialize_FixPlantMeatAmount_Prefix
    {
        public static void Prefix()
        {
            try
            {
                var list = CROPS.CROP_TYPES;
                if (list == null) return;

                for (int i = 0; i < list.Count; i++)
                {
                    var cv = list[i];
                    if (cv.cropId == "PlantMeat")
                    {
                        // Preserve duration (18000f) and set amount to 1
                        list[i] = new Crop.CropVal(cv.cropId, cv.cropDuration, 1);
                        //      Debug.Log("[Rephysicalized] Db.Initialize: corrected CROPS.CROP_TYPES[PlantMeat] amount to 1.");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                //          Debug.LogWarning("[Rephysicalized] Db.Initialize prefix: failed to correct PlantMeat amount: " + e);
            }
        }
    }

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


    // Register CritterTrapPlant with PlantMassTracker:
    // - realHarvestSubtractKg = 0f (do not subtract fixed mass; tracker will reset to 1 kg)
    // - massNeededToReach100GrowthKg = 1f (target mass after harvest)
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class CritterTrapPlant_PMT_Registration
    {
        public static void Postfix()
        {
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: "CritterTrapPlant",
                yields: new List<MaterialYield>
                {
                    new MaterialYield("ToxicMud", 1f),
                },
                realHarvestSubtractKg: 2f

            );
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