using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.AnimalPatches
{


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
                }
            }
           
      }
    



}