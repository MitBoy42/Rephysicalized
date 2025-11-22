using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    // Requirements:
    // 1) Irrigation chlorine: 10/600 kg/s
    // 2) Fertilizer: tag ModTags.RichSoil instead of Dirt, rate 40/600 kg/s
    // 3) Harvest plant fiber amount: 50 (down from 400)
    //
    // We patch:
    // - GasGrassConfig.CreatePrefab (IL) to replace inlined constants and tags at runtime.
    // - Db.Initialize Postfix to update GasGrassConfig const fields (some systems read those for UI).
    //
    // Notes:
    // - Some C# const floats get inlined; our transpiler ensures CreatePrefab uses our values regardless.

    internal static class GasGrassTuning
    {
        public const float NewChlorineRate = 10f / 600f; // ~0.0166666667
        public const float NewFertilizerRate = 40f / 600f; // ~0.0666666667
        public const float NewPlantFiberPerHarvest = 50f;

        // Originals in game code (decompiled constants)
        public const float OrigChlorineRate = 0.0008333334f;
        public const float OrigFertilizerRate = 0.04166667f; // 2.5/60
        public const float OrigPlantFiberPerHarvest = 400f;
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class GasGrass_DbInitialize_Postfix_UpdateConsts
    {
        private static void Postfix()
        {

            // Replace GasGrassConfig const-like fields if present (not always writeable; ignore failures)
            var t = typeof(GasGrassConfig);
            // public const float CHLORINE_FERTILIZATION_RATE
            ReplaceConstLike(t, "CHLORINE_FERTILIZATION_RATE", GasGrassTuning.NewChlorineRate);
            // public const float DIRT_FERTILIZATION_RATE
            ReplaceConstLike(t, "DIRT_FERTILIZATION_RATE", GasGrassTuning.NewFertilizerRate);
            // public const int PLANT_FIBER_KG_PER_HARVEST
            ReplaceConstLike(t, "PLANT_FIBER_KG_PER_HARVEST", (int)GasGrassTuning.NewPlantFiberPerHarvest);


        }

        private static void ReplaceConstLike(Type t, string name, float value)
        {
            var f = AccessTools.Field(t, name);
            if (f == null) return;
            try { f.SetValue(null, value); } catch { }
        }

        private static void ReplaceConstLike(Type t, string name, int value)
        {
            var f = AccessTools.Field(t, name);
            if (f == null) return;
            try { f.SetValue(null, value); } catch { }
        }
    }

    [HarmonyPatch(typeof(GasGrassConfig), nameof(GasGrassConfig.CreatePrefab))]
    internal static class GasGrassConfig_CreatePrefab_Transpiler
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Targets to replace in IL
            float origChlorine = GasGrassTuning.OrigChlorineRate;
            float origFert = GasGrassTuning.OrigFertilizerRate;
            float origHarvest = GasGrassTuning.OrigPlantFiberPerHarvest;

            int dirtInt = (int)SimHashes.Dirt;
            var fi_ModTags_RichSoil = AccessTools.Field(typeof(ModTags), "RichSoil");

            // helpers
            CodeInstruction CloneLike(CodeInstruction src, OpCode op, object operand = null)
            {
                var ni = new CodeInstruction(op, operand);
                if (src != null)
                {
                    if (src.labels != null && src.labels.Count > 0) ni.labels.AddRange(src.labels);

                    ni.blocks = src.blocks;

                }
                return ni;
            }

            bool IsLoadIntOf(int value, CodeInstruction ci)
            {
                if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int vi) return vi == value;
                if (ci.opcode == OpCodes.Ldc_I4_S && ci.operand is sbyte sb) return sb == value;
                if (value == -1 && ci.opcode == OpCodes.Ldc_I4_M1) return true;
                if (value == 0 && ci.opcode == OpCodes.Ldc_I4_0) return true;
                if (value == 1 && ci.opcode == OpCodes.Ldc_I4_1) return true;
                if (value == 2 && ci.opcode == OpCodes.Ldc_I4_2) return true;
                if (value == 3 && ci.opcode == OpCodes.Ldc_I4_3) return true;
                if (value == 4 && ci.opcode == OpCodes.Ldc_I4_4) return true;
                if (value == 5 && ci.opcode == OpCodes.Ldc_I4_5) return true;
                if (value == 6 && ci.opcode == OpCodes.Ldc_I4_6) return true;
                if (value == 7 && ci.opcode == OpCodes.Ldc_I4_7) return true;
                if (value == 8 && ci.opcode == OpCodes.Ldc_I4_8) return true;
                return false;
            }

            bool IsAnyCallReturningTag(CodeInstruction ins)
            {
                if (!(ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)) return false;
                if (ins.operand is MethodInfo mi)
                    return mi.ReturnType == typeof(Tag);
                return false;
            }

            for (int i = 0; i < codes.Count; i++)
            {
                var ci = codes[i];

                // Replace fertilizer tag: SimHashes.Dirt.CreateTag() -> ModTags.RichSoil
                if (fi_ModTags_RichSoil != null && i + 1 < codes.Count && IsLoadIntOf(dirtInt, ci) && IsAnyCallReturningTag(codes[i + 1]))
                {
                    // Load static field ModTags.RichSoil and skip the CreateTag-like call
                    yield return CloneLike(ci, OpCodes.Ldsfld, fi_ModTags_RichSoil);
                    i += 1; // skip the call returning Tag
                    continue;
                }

                // Replace chlorine irrigation rate constant
                if (ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f1 && Mathf.Approximately(f1, origChlorine))
                {
                    yield return CloneLike(ci, OpCodes.Ldc_R4, GasGrassTuning.NewChlorineRate);
                    continue;
                }

                // Replace fertilizer mass rate constant
                if (ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f2 && Mathf.Approximately(f2, origFert))
                {
                    yield return CloneLike(ci, OpCodes.Ldc_R4, GasGrassTuning.NewFertilizerRate);
                    continue;
                }

                // Replace harvest amount (if referenced inside CreatePrefab)
                if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int intv && intv == (int)origHarvest)
                {
                    yield return CloneLike(ci, OpCodes.Ldc_I4, (int)GasGrassTuning.NewPlantFiberPerHarvest);
                    continue;
                }
                if (ci.opcode == OpCodes.Ldc_R4 && ci.operand is float fh && Mathf.Approximately(fh, origHarvest))
                {
                    yield return CloneLike(ci, OpCodes.Ldc_R4, GasGrassTuning.NewPlantFiberPerHarvest);
                    continue;
                }

                // pass-through
                yield return ci;
            }
        }
    }
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Crops_PlantFiber_Amount_Patch
    {
        private static void Postfix()
        {
            var list = TUNING.CROPS.CROP_TYPES; if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var cv = list[i];
                if (cv.cropId == "PlantFiber")
                {
                    // Keep original duration 2400f, change amount to 50
                    list[i] = new Crop.CropVal("PlantFiber", 2400f, 50);
                    break;
                }
            }
        }
    }
}