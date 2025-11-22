using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.AnimalPatches
{
    // Molt-only handling:
    // - Hooks MoltDropperMonitor.Instance drop methods (not predicate checks).
    // - Dynamically targets any instance method whose name starts with "Drop" EXCEPT "ShouldDropElement".
    // - After vanilla executes, subtract body mass equal to the molted mass, clamped to keep at least 1 kg.
    [HarmonyPatch]
    internal static class MoltDropper_SubtractBodyMass_OnActualDrop
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var instType = typeof(MoltDropperMonitor.Instance);
            var methods = AccessTools.GetDeclaredMethods(instType);
            foreach (var m in methods)
            {
                if (m == null || m.IsStatic || m.IsAbstract || m.IsConstructor)
                    continue;

                string name = m.Name ?? string.Empty;
                if (name.StartsWith("Drop", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("ShouldDropElement", StringComparison.OrdinalIgnoreCase))
                {
                    yield return m;
                }
            }
        }

        // Postfix subtracts mass based on def.massToDrop (clamped so mass never goes below 1 kg)
        private static void Postfix(MoltDropperMonitor.Instance __instance)
        {
            if (__instance == null)
                return;

            var go = __instance.gameObject; // monitor sits on the creature root
            var pe = go != null ? go.GetComponent<PrimaryElement>() : null;
            var def = __instance.def;

            if (go == null || pe == null || def == null)
                return;

            float vanillaDrop = Mathf.Max(0f, def.massToDrop);
            float before = Mathf.Max(0f, pe.Mass);
            float available = Mathf.Max(0f, before - 1f);
            float subtractKg = Mathf.Min(vanillaDrop, available);

            if (subtractKg > 0f)
            {
                pe.Mass = Mathf.Max(1f, before - subtractKg);
            }
        }
    }


    // Patch CrabConfig.CreateCrab to replace TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL with 0.857142f

    [HarmonyPatch(typeof(CrabConfig))]
    public static class CrabConfig_ConversionEfficiency_Transpiler
    {
        // Support both CreatePrefab (current) and CreateCrab (older) to be safe
        static IEnumerable<MethodBase> TargetMethods()
        {
            var names = new[] { "CreatePrefab", "CreateCrab" };
            foreach (var name in names)
            {
                var m = AccessTools.Method(typeof(CrabConfig), name);
                if (m != null)
                    yield return m;
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve TUNING.CREATURES+CONVERSION_EFFICIENCY::NORMAL (TUNING is a namespace, CREATURES is a type, CONVERSION_EFFICIENCY is a nested type)
            FieldInfo normalField = null;
            var convEffType = AccessTools.TypeByName("TUNING.CREATURES+CONVERSION_EFFICIENCY");
            normalField = AccessTools.Field(convEffType, "NORMAL");

            int replacements = 0;

            foreach (var ins in instructions)
            {
                if (ins.opcode == OpCodes.Ldsfld && ins.operand is FieldInfo fi)
                {
                    bool match =
                        (normalField != null && fi == normalField)
                        ||
                        (normalField == null
                         && fi.FieldType == typeof(float)
                         && fi.Name == "NORMAL"
                         && fi.DeclaringType != null
                         && fi.DeclaringType.FullName == "TUNING.CREATURES+CONVERSION_EFFICIENCY");

                    if (match)
                    {
                        replacements++;
                        var replacement = new CodeInstruction(OpCodes.Ldc_R4, 0.857142f);
#if HARMONYX
                        // Preserve labels/blocks if HarmonyX block support is used
                        replacement.labels.AddRange(ins.labels);
                        replacement.blocks.AddRange(ins.blocks);
#endif
                        yield return replacement;
                        continue;
                    }
                }
                yield return ins;
            }

        }
    }

    // 1) Ensure KG_ORE_EATEN_PER_CYCLE is 140 and CALORIES_PER_KG_OF_ORE is adjusted before diet is built
    [HarmonyPatch(typeof(CrabWoodConfig), nameof(CrabWoodConfig.CreateCrabWood))]
    public static class CrabWoodConfig_CreateCrabWood_Prefix
    {
        private static readonly FieldInfo f_KgPerCycle = AccessTools.Field(typeof(CrabWoodConfig), "KG_ORE_EATEN_PER_CYCLE");
        private static readonly FieldInfo f_CalsPerKg = AccessTools.Field(typeof(CrabWoodConfig), "CALORIES_PER_KG_OF_ORE");

        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                if (f_KgPerCycle != null)
                {
                    f_KgPerCycle.SetValue(null, 140f);
                }
                else
                {
                }

                if (f_CalsPerKg != null)
                {
                    // Recompute with the new KG_ORE_EATEN_PER_CYCLE (140)
                    float newCalsPerKg = CrabTuning.STANDARD_CALORIES_PER_CYCLE / 140f;
                    f_CalsPerKg.SetValue(null, newCalsPerKg);
                }
                else
                {
                    //   Debug.LogWarning("[Rephysicalized] CrabWoodConfig: Could not find field CALORIES_PER_KG_OF_ORE");
                }
            }
            catch (Exception e)
            {
                // Debug.LogWarning($"[Rephysicalized] CrabWoodConfig: Failed to adjust ore/cycle and calories/kg: {e.Message}");
            }
        }
    }



}