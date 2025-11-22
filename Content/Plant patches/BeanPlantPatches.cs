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
        private const string LogPrefix = "[Rephysicalized/BeanPlant]";
        private static bool sAddedMethaneIL;
        private static bool sReplacedFertIL;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            bool addedMethanePattern = false;
            bool replacedFertPattern = false;

            try
            {
                // IL uses constants for enums: ldc.i4 (int)SimHashes.X
                int co2 = (int)SimHashes.CarbonDioxide;
                int methane = (int)SimHashes.Methane;
                int dirt = (int)SimHashes.Dirt;

                // 1) Expand safe_elements array (from [CO2] to [CO2, Methane])
                for (int i = 0; i < list.Count - 5; i++)
                {
                    if (IsLdcI4(list[i], 1) && // array length
                        list[i + 1].opcode == OpCodes.Newarr && Equals(list[i + 1].operand, typeof(SimHashes)) &&
                        list[i + 2].opcode == OpCodes.Dup &&
                        IsLdcI4(list[i + 3], 0) &&
                        IsLdcI4(list[i + 4], co2) &&
                        list[i + 5].opcode == OpCodes.Stelem_I4)
                    {
                        // Change array length 1 -> 2
                        list[i] = new CodeInstruction(OpCodes.Ldc_I4_2);

                        // Append Methane: dup, ldc.i4.1, ldc.i4 (methane), stelem.i4
                        list.Insert(i + 6, new CodeInstruction(OpCodes.Dup));
                        list.Insert(i + 7, new CodeInstruction(OpCodes.Ldc_I4_1));
                        list.Insert(i + 8, new CodeInstruction(OpCodes.Ldc_I4, methane));
                        list.Insert(i + 9, new CodeInstruction(OpCodes.Stelem_I4));

                        addedMethanePattern = true;
                      
                        break;
                    }
                }

                // 2) Replace Dirt.CreateTag() with ModTags.RichSoil when setting ConsumeInfo.tag
                var consumeTagField = AccessTools.Field(typeof(PlantElementAbsorber.ConsumeInfo), "tag");
                var richSoilField = AccessTools.Field(typeof(ModTags), "RichSoil");

                for (int i = 2; i < list.Count; i++)
                {
                    if (list[i].opcode == OpCodes.Stfld && Equals(list[i].operand, consumeTagField))
                    {
                        var prev1 = list[i - 1]; // call CreateTag
                        var prev2 = list[i - 2]; // ldc.i4 <SimHashes enum value>
                        if (IsLdcI4(prev2, (int)SimHashes.Dirt) &&
                            (prev1.opcode == OpCodes.Call || prev1.opcode == OpCodes.Callvirt))
                        {
                            // Replace with ModTags.RichSoil Tag on the stack and NOP the call
                            list[i - 2] = new CodeInstruction(OpCodes.Ldsfld, richSoilField);
                            list[i - 1] = new CodeInstruction(OpCodes.Nop);

                            replacedFertPattern = true;
                           
                            break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogPrefix} Transpiler failed: {e}");
            }
            finally
            {
                sAddedMethaneIL = addedMethanePattern;
                sReplacedFertIL = replacedFertPattern;

              
            }

            return list;
        }

        public static void Postfix(GameObject __result)
        {
            try
            {
                if (__result == null)
                    return;

                // 1) Ensure Methane is a safe atmosphere (PressureVulnerable)
                try
                {
                    var pv = __result.GetComponent<PressureVulnerable>();
                    if ((object)pv != null) // reference comparison avoids operator overload issues
                    {
                        var field = AccessTools.Field(typeof(PressureVulnerable), "safe_atmospheres");
                        if (field != null)
                        {
                            var val = field.GetValue(pv);
                            if (val is HashSet<SimHashes> set)
                            {
                               
                            }
                            else if (val is SimHashes[] arr)
                            {
                                if (!arr.Contains(SimHashes.Methane))
                                {
                                    var newArr = new SimHashes[arr.Length + 1];
                                    System.Array.Copy(arr, newArr, arr.Length);
                                    newArr[newArr.Length - 1] = SimHashes.Methane;
                                    field.SetValue(pv, newArr);
                                  
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                   
                }

                // 2) Replace Dirt fertilizer tag with ModTags.RichSoil (PlantElementAbsorber / FertilizerConsumer)
                try
                {
                    int changes = 0;
                    var dirtTag = SimHashes.Dirt.CreateTag();
                    var richTag = ModTags.RichSoil;

                    var absorber = __result.GetComponent<PlantElementAbsorber>();
                    if ((object)absorber != null) // reference comparison avoids operator overload issues (fixes CS0019)
                    {
                        var fields = absorber.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var f in fields)
                        {
                            if (f.FieldType == typeof(PlantElementAbsorber.ConsumeInfo[]))
                            {
                                var arr = f.GetValue(absorber) as PlantElementAbsorber.ConsumeInfo[];
                                if (arr == null) continue;

                                for (int i = 0; i < arr.Length; i++)
                                {
                                    var ci = arr[i];
                                    if (ci.tag == dirtTag)
                                    {
                                        ci.tag = richTag;
                                        arr[i] = ci;
                                        changes++;
                                    }
                                }
                                f.SetValue(absorber, arr);
                            }
                        }
                     
                    }

                    // Also scan for a FertilizerConsumer-like component by name (if present)
                    if (changes == 0)
                    {
                        foreach (var comp in __result.GetComponents<Component>())
                        {
                            var t = comp.GetType();
                            if (t.Name == "FertilizerConsumer")
                            {
                                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                                foreach (var f in t.GetFields(flags))
                                {
                                    if (f.FieldType == typeof(Tag))
                                    {
                                        var current = (Tag)f.GetValue(comp);
                                        if (current == dirtTag)
                                        {
                                            f.SetValue(comp, richTag);
                                            changes++;
                                        }
                                    }
                                }
                            }
                        }
                      
                    }

                 
                }
                catch (System.Exception e)
                {
                }
            }
            catch (System.Exception e)
            {
            }
        }

        private static bool IsLdcI4(CodeInstruction ci, int value)
        {
            if (ci.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (ci.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (ci.opcode == OpCodes.Ldc_I4_2) return value == 2;
            if (ci.opcode == OpCodes.Ldc_I4_3) return value == 3;
            if (ci.opcode == OpCodes.Ldc_I4_4) return value == 4;
            if (ci.opcode == OpCodes.Ldc_I4_5) return value == 5;
            if (ci.opcode == OpCodes.Ldc_I4_6) return value == 6;
            if (ci.opcode == OpCodes.Ldc_I4_7) return value == 7;
            if (ci.opcode == OpCodes.Ldc_I4_8) return value == 8;
            if (ci.opcode == OpCodes.Ldc_I4) return ci.operand is int i && i == value;
            if (ci.opcode == OpCodes.Ldc_I4_S) return ci.operand is sbyte sb && sb == value;
            return false;
        }
    }
}