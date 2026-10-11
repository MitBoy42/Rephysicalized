using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{
    [HarmonyPatch]
    internal static class MilkSeparatorPatches
    {
        internal static T[] GetComponentsOfType<T>(GameObject go) where T : Component
        {
            if (go == null)
                return new T[0];

            // Try to call non-generic GetComponents(Type) via reflection first (available on GameObject)
            try
            {
                MethodInfo mi = typeof(GameObject).GetMethod("GetComponents", new Type[] { typeof(Type) });
                if (mi != null)
                {
                    var arr = mi.Invoke(go, new object[] { typeof(T) }) as Array;
                    if (arr != null)
                    {
                        var list = new List<T>();
                        foreach (var o in arr)
                        {
                            if (o is T t) list.Add(t);
                        }
                        return list.ToArray();
                    }
                }
            }
            catch { }

            try
            {
                var single = go.GetComponent<T>();
                if (single != null)
                    return new T[] { single };
            }
            catch { }

            return new T[0];
        }

        // Tint meter and liquid reservoir symbols to reflect Naphtha and Sulfur when present
        [HarmonyPatch(typeof(MilkSeparator.Instance), "RefreshMeters")]
        [HarmonyPostfix]
        public static void MilkSeparator_RefreshMeters_Postfix(MilkSeparator.Instance __instance)
        {
          
                var storage = __instance.storage;
                var anim = __instance.animController;
                var fatMeter = __instance.fatMeter;

                if ((UnityEngine.Object)storage == (UnityEngine.Object)null || (UnityEngine.Object)anim == (UnityEngine.Object)null)
                    return;

                // If sulfur exists in storage, tint fat meter to sulfur color
                float sulfurMass = storage.GetMassAvailable(SimHashes.Sulfur.CreateTag());
                if (sulfurMass > 0f)
                {
                    var sulfurColor = ElementLoader.FindElementByHash(SimHashes.Sulfur).substance.colour;
                 
                        if (fatMeter != null && fatMeter.meterController != null)
                            fatMeter.meterController.SetSymbolTint(new KAnimHashedString("meter_fat"), sulfurColor);
                
                    try { anim.SetSymbolTint((KAnimHashedString)"fat", sulfurColor); } catch { }
                }

                // If Naphtha exists in storage, tint liquid reservoir to Naphtha color
                float naphthaMass = storage.GetMassAvailable(SimHashes.Naphtha.CreateTag());
                if (naphthaMass > 0f)
                {
                    var naphthaElement = ElementLoader.FindElementByHash(SimHashes.Naphtha);
                
                            GameUtil.TintLiquidSymbolOnBuilding("liquid_reservoir", anim, naphthaElement);

                }      
        }


        // Patch MilkFatSeparatorConfig.ConfigureBuildingTemplate to adjust converters and add an extra conversion
        [HarmonyPatch(typeof(MilkFatSeparatorConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void ConfigureBuildingTemplate_Postfix(GameObject go, Tag prefab_tag)
        {
            var converters = GetComponentsOfType<ElementConverter>(go);

            bool hasNaphtha = false;
            foreach (var c in converters)
            {
                if (c.consumedElements != null)
                {
                    foreach (var ce in c.consumedElements)
                    {
                        if (ce.Tag == SimHashes.Naphtha.CreateTag())
                        {
                            hasNaphtha = true;
                            break;
                        }
                    }
                }
                if (hasNaphtha) break;
            }

            // Ensure vanilla converters have desired rates
            if (converters.Length > 0)
            {
                var milkConv = converters[0];
                milkConv.consumedElements = new ElementConverter.ConsumedElement[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.Milk.CreateTag(), 1f)
                };

                milkConv.outputElements = new ElementConverter.OutputElement[]
                {
                    new ElementConverter.OutputElement(0.1f, SimHashes.MilkFat, 0.0f, storeOutput: true),
                    new ElementConverter.OutputElement(0.8f, SimHashes.SaltWater, 0.0f, storeOutput: true, diseaseWeight: 0.0f),
                    new ElementConverter.OutputElement(0.1f, SimHashes.CarbonDioxide, 348.15f, outputElementOffsetx: 1f, outputElementOffsety: 3f, diseaseWeight: 0.0f)
                };
            }

            if (converters.Length > 1)
            {
                var fishConv = converters[1];
                fishConv.consumedElements = new ElementConverter.ConsumedElement[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.FishMilk.CreateTag(), 1f)
                };

                fishConv.outputElements = new ElementConverter.OutputElement[]
                {
                    new ElementConverter.OutputElement(0.81f, SimHashes.Mucus, 0.0f, storeOutput: true, diseaseWeight: 0.0f),
                    new ElementConverter.OutputElement(0.1f, SimHashes.CarbonDioxide, 348.15f, outputElementOffsetx: 1f, outputElementOffsety: 3f, diseaseWeight: 0.0f)
                };
            }

            if (!hasNaphtha)
            {
                var napConv = go.AddComponent<ElementConverter>();
                napConv.consumedElements = new ElementConverter.ConsumedElement[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.Naphtha.CreateTag(), 1.0f)
                };

                napConv.outputElements = new ElementConverter.OutputElement[]
                {
                    new ElementConverter.OutputElement(0.10f, SimHashes.Sulfur, 373.15f, storeOutput: true),
                    new ElementConverter.OutputElement(0.80f, SimHashes.LiquidGunk, 473.15f, storeOutput: true),
                    new ElementConverter.OutputElement(0.10f, SimHashes.Hydrogen, 473.15f, storeOutput: false)
                };

                // Ensure the ConduitDispenser does not immediately dispense Naphtha to the output pipe.
            
                    var dispenser = go.GetComponent<ConduitDispenser>();
               
                        var existing = dispenser.elementFilter ?? new SimHashes[0];
                        bool hass = false;
                        foreach (var h in existing)
                        {
                            if (h == SimHashes.Naphtha) { hass = true; break; }
                        }
                        if (!hass)
                        {
                            var list = new List<SimHashes>(existing);
                            list.Add(SimHashes.Naphtha);
                            dispenser.elementFilter = list.ToArray();
                        }
            }
        }
    }

    // Runtime patches must be outside the postfix method/class
    [HarmonyPatch]
    internal static class MilkSeparatorRuntimePatches
    {
        [HarmonyPatch(typeof(MilkSeparator.Instance), "StartSM")]
        [HarmonyPostfix]
        public static void MilkSeparator_StartSM_Postfix(MilkSeparator.Instance __instance)
        {
           
                var converterList = new List<ElementConverter>();
                // converters on the instance
                converterList.AddRange(MilkSeparatorPatches.GetComponentsOfType<ElementConverter>(__instance.gameObject));
                // converters on the master prefab
                var masterComp = __instance.master as Component;
                if ((UnityEngine.Object)masterComp != (UnityEngine.Object)null)
                    converterList.AddRange(MilkSeparatorPatches.GetComponentsOfType<ElementConverter>(masterComp.gameObject));

                __instance.elementConverters = converterList.ToArray();
        }

        [HarmonyPatch(typeof(MilkSeparator.Instance), "get_MilkFatStored")]
        [HarmonyPostfix]
        public static void MilkSeparator_MilkFatStored_Postfix(MilkSeparator.Instance __instance, ref float __result)
        {
           
                var storage = __instance.storage;
                if ((UnityEngine.Object)storage != (UnityEngine.Object)null)
                {
                    __result += storage.GetMassAvailable(SimHashes.Sulfur.CreateTag());
                }

        }
        [HarmonyPatch(typeof(MilkSeparator.Instance), "DropMilkFat")]
        [HarmonyPostfix]
        public static void MilkSeparator_DropMilkFat_Postfix(MilkSeparator.Instance __instance)
        {  
                var storage = __instance.storage;
                if ((UnityEngine.Object)storage != (UnityEngine.Object)null)
                {
                    var items = new List<GameObject>(storage.items);
                    foreach (var go in items)
                    {
                        if (go == null) continue;
                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe == null) continue;

                        var id = pe.ElementID;
                        if (id == SimHashes.Sulfur || id == SimHashes.LiquidGunk)
                        {
                                storage.Drop(go); 
                        }
                    }
                }
          
        }
    }
}
