using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
    public static class BeanPlantConfig_CreatePrefab_Patch
    {
        /// <summary>
        /// IL transpiler to expand safe_elements [CO2] -> [CO2, Methane] and Dirt tag -> RichSoil.
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);

          
                int co2 = (int)SimHashes.CarbonDioxide;
                int methane = (int)SimHashes.Methane;
                int dirt = (int)SimHashes.Dirt;

                // 1. Add Methane to safe_elements array
                for (int i = 0; i < list.Count - 5; i++)
                {
                    if (IsLdcI4(list[i], 1) &&
                        list[i + 1].opcode == OpCodes.Newarr && Equals(list[i + 1].operand, typeof(SimHashes)) &&
                        list[i + 2].opcode == OpCodes.Dup &&
                        IsLdcI4(list[i + 3], 0) &&
                        IsLdcI4(list[i + 4], co2) &&
                        list[i + 5].opcode == OpCodes.Stelem_I4)
                    {
                        list[i] = new CodeInstruction(OpCodes.Ldc_I4_2);
                        list.Insert(i + 6, new CodeInstruction(OpCodes.Dup));
                        list.Insert(i + 7, new CodeInstruction(OpCodes.Ldc_I4_1));
                        list.Insert(i + 8, new CodeInstruction(OpCodes.Ldc_I4, methane));
                        list.Insert(i + 9, new CodeInstruction(OpCodes.Stelem_I4));
                        break;
                    }
                }

                // 2. Replace Dirt.CreateTag() with ModTags.RichSoil
                var tagField = AccessTools.Field(typeof(PlantElementAbsorber.ConsumeInfo), "tag");
                var richSoilField = AccessTools.Field(typeof(ModTags), "RichSoil");
                for (int i = 2; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Stfld && Equals(list[i].operand, tagField))
                    {
                        var prev1 = list[i - 1];
                        var prev2 = list[i - 2];
                        if (IsLdcI4(prev2, dirt) && (prev1.opcode == OpCodes.Call || prev1.opcode == OpCodes.Callvirt))
                        {
                            list[i - 2] = new CodeInstruction(OpCodes.Ldsfld, richSoilField);
                            list[i - 1] = new CodeInstruction(OpCodes.Nop);
                            break;
                        }
                    }
                }
       

            return list;
        }
        

        private static bool IsLdcI4(CodeInstruction ci, int value)
        {
            if (ci.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (ci.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (ci.opcode == OpCodes.Ldc_I4_2) return value == 2;
            if (ci.opcode == OpCodes.Ldc_I4_S || ci.opcode == OpCodes.Ldc_I4) 
                return ci.operand is int v && v == value;
            return false;
        }
    }
}
