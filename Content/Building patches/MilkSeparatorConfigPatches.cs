using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;


namespace Rephysicalized.Content.Building_patches
{
    // Keep vanilla element converter, but store outputs and adjust rates; add a naphtha converter.
    [HarmonyPatch(typeof(MilkFatSeparatorConfig), nameof(MilkFatSeparatorConfig.ConfigureBuildingTemplate))]
    public static class MilkFatSeparator_Config_Postfix
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Store MilkFat + Brine outputs and normalize rates
            var milkConv = go.GetComponent<ElementConverter>();

            if (milkConv?.outputElements != null)
            {
                var outs = milkConv.outputElements;
                for (int i = 0; i < outs.Length; i++)
                {
                    if (outs[i].elementHash == SimHashes.MilkFat)
                    {
                        outs[i].storeOutput = true;
                        outs[i].massGenerationRate = 0.10f; // 0.1 kg/s MilkFat
                    }
                    else if (outs[i].elementHash == SimHashes.Brine)
                    {
                        outs[i].storeOutput = true;
                        outs[i].massGenerationRate = 0.80f; // 0.8 kg/s Brine
                    }
                }
                milkConv.outputElements = outs;

                // Initial UI: show Milk converter in Milk mode by default
                milkConv.ShowInUI = true;
            }

            // Add a dedicated Naphtha-mode ElementConverter if not present
            var existing = go.GetComponents<ElementConverter>() ?? Array.Empty<ElementConverter>();
            bool hasNaphthaConverter = existing.Any(ec =>
                ec != null && ec.consumedElements != null && ec.consumedElements.Any(c => c.Tag == SimHashes.Naphtha.CreateTag()));

            if (!hasNaphthaConverter)
            {
                var napConv = go.AddComponent<ElementConverter>();

                napConv.consumedElements = new[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.Naphtha.CreateTag(), 1.0f)
                };
                napConv.outputElements = new[]
                {
                    // Solid Sulfur (stored) to trigger empty chore
                    new ElementConverter.OutputElement(0.10f, SimHashes.Sulfur, 373.15f, storeOutput : true),
                    // Liquid Gunk (stored, but dispenser will send to pipe)
                    new ElementConverter.OutputElement(0.80f, SimHashes.LiquidGunk, 473.15f, storeOutput : true),
                    // Hydrogen to world
                    new ElementConverter.OutputElement(0.10f, SimHashes.Hydrogen, 473.15f, storeOutput: false),
                };

                // Initial UI: hide Naphtha converter in Milk default mode
                napConv.ShowInUI = false;
            }
        }

    
    }

    // Swap vanilla consumer -> RCC and configure dispenser; attach mode controller.
    [HarmonyPatch(typeof(MilkFatSeparatorConfig), nameof(MilkFatSeparatorConfig.ConfigureBuildingTemplate))]
    public static class MilkFatSeparatorConfig_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var vanilla = go.GetComponent<ConduitConsumer>();
            float cap = 4f, rate = 10f;
            bool forceSatisfied = true;
            var wrong = ConduitConsumer.WrongElementResult.Store;

            if (vanilla)
            {
                cap = vanilla.capacityKG;
                rate = vanilla.consumptionRate;
                forceSatisfied = vanilla.forceAlwaysSatisfied;
                wrong = vanilla.wrongElementResult;
                UnityEngine.Object.DestroyImmediate(vanilla);
            }

            var rcc = go.AddOrGet<RephysicalizedConduitConsumer>();
            rcc.conduitType = ConduitType.Liquid;
            rcc.capacityKG = (float.IsInfinity(cap) || cap <= 0f) ? 4f : cap;
            rcc.consumptionRate = (float.IsInfinity(rate) || rate <= 0f) ? 10f : rate;
            rcc.forceAlwaysSatisfied = forceSatisfied;
            rcc.wrongElementResult = wrong;

            // Accept Milk via tag; allow Naphtha explicitly
            var milkTag = ElementLoader.FindElementByHash(SimHashes.Milk).tag;
            rcc.acceptedTags = new[] { milkTag };
            rcc.acceptedElements = new[] { SimHashes.Naphtha };

            // Dispenser keeps current mode's liquid in storage (invert filter)
            var disp = go.AddOrGet<ConduitDispenser>();
            disp.conduitType = ConduitType.Liquid;
            disp.invertElementFilter = true;
            disp.elementFilter = new[] { SimHashes.Milk };

            // Attach controller
            go.AddOrGet<MilkSeparatorModeController>();
        }
    }

    [HarmonyPatch(typeof(MilkFatSeparatorConfig), nameof(MilkFatSeparatorConfig.DoPostConfigureComplete))]
    public static class MilkFatSeparatorConfig_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            // RequireInputs can conflict with RCC in some setups
            var reqs = go.GetComponents<RequireInputs>();
            foreach (var r in reqs)
                if (r) UnityEngine.Object.DestroyImmediate(r);

            // Ensure no vanilla consumer remains
            var ccs = go.GetComponents<ConduitConsumer>();
            foreach (var cc in ccs)
                if (cc) UnityEngine.Object.DestroyImmediate(cc);

            go.AddOrGet<RephysicalizedConduitConsumer>();
        }
    }

    // Mode-aware conversion gating with epsilon to ignore tiny crumbs.
    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.CanConvertAtAll))]
    public static class MilkSeparator_ElementConverter_CanConvertAtAll_Patch
    {
        private const float SOLID_EPS = 0.1f; // crumbs shouldn't block conversion

        public static bool Prefix(ElementConverter __instance, ref bool __result)
        {
            if (__instance == null) return true;

            var go = __instance.gameObject;
            var ctrl = go != null ? go.GetComponent<MilkSeparatorModeController>() : null;
            if (ctrl == null) return true;

            bool isMilkConv = __instance.consumedElements != null &&
                              __instance.consumedElements.Any(c => c.Tag == SimHashes.Milk.CreateTag());
            bool isNaphthaConv = __instance.consumedElements != null &&
                                 __instance.consumedElements.Any(c => c.Tag == SimHashes.Naphtha.CreateTag());

            if (!isMilkConv && !isNaphthaConv) return true;

            var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            var storage = go.GetComponent<Storage>();
            float fat = storage != null ? storage.GetAmountAvailable(fatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;

            if (isMilkConv)
            {
                bool hasMilk = storage != null && storage.GetAmountAvailable(SimHashes.Milk.CreateTag()) > 0.0001f;
                __result = (ctrl != null && ctrl.GetSelectedOption() == SimHashes.Milk.CreateTag()) && hasMilk && sulfur <= SOLID_EPS && fat < cap;
                return false;
            }

            if (isNaphthaConv)
            {
                bool hasNaphtha = storage != null && storage.GetAmountAvailable(SimHashes.Naphtha.CreateTag()) > 0.0001f;
                __result = (ctrl != null && ctrl.GetSelectedOption() == SimHashes.Naphtha.CreateTag()) && hasNaphtha && fat <= SOLID_EPS && sulfur < cap;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ElementConverter), "HasEnoughMassToStartConverting")]
    public static class MilkSeparator_ElementConverter_HasEnoughMassToStartConverting_Patch
    {
        private const float SOLID_EPS = 0.1f; // crumbs shouldn't block conversion

        public static bool Prefix(ElementConverter __instance, ref bool __result)
        {
            if (__instance == null) return true;

            var go = __instance.gameObject;
            var ctrl = go != null ? go.GetComponent<MilkSeparatorModeController>() : null;
            if (ctrl == null) return true;

            bool isMilkConv = __instance.consumedElements != null &&
                              __instance.consumedElements.Any(c => c.Tag == SimHashes.Milk.CreateTag());
            bool isNaphthaConv = __instance.consumedElements != null &&
                                 __instance.consumedElements.Any(c => c.Tag == SimHashes.Naphtha.CreateTag());

            if (!isMilkConv && !isNaphthaConv) return true;

            var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            var storage = go.GetComponent<Storage>();
            float fat = storage != null ? storage.GetAmountAvailable(fatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;

            if (isMilkConv)
            {
                bool hasMilk = storage != null && storage.GetAmountAvailable(SimHashes.Milk.CreateTag()) > 0.0001f;
                __result = (ctrl != null && ctrl.GetSelectedOption() == SimHashes.Milk.CreateTag()) && hasMilk && sulfur <= SOLID_EPS && fat < cap;
                return false;
            }

            if (isNaphthaConv)
            {
                bool hasNaphtha = storage != null && storage.GetAmountAvailable(SimHashes.Naphtha.CreateTag()) > 0.0001f;
                __result = (ctrl != null && ctrl.GetSelectedOption() == SimHashes.Naphtha.CreateTag()) && hasNaphtha && fat <= SOLID_EPS && sulfur < cap;
                return false;
            }

            return true;
        }
    }

    // Ensure mode switch disables the wrong converter and purges tiny crumbs of the wrong solid,
    // then re-triggers the SM to reevaluate and create an emptying chore if needed.
    [HarmonyPatch(typeof(MilkSeparatorModeController), nameof(MilkSeparatorModeController.OnOptionSelected))]
    public static class MilkSeparatorModeController_OnOptionSelected_Postfix
    {
        private const float PURGE_MAX = 0.2f; // drop wrong solid automatically if <= 20 g

        public static void Postfix(MilkSeparatorModeController __instance)
        {
            var go = __instance != null ? __instance.gameObject : null;
            if (go == null) return;

            var selected = __instance.GetSelectedOption();

            // 1) Enable only the converter that matches the selected mode,
            //    and set ShowInUI accordingly (Milk visible in Milk, Naphtha visible in Naphtha).
            var converters = go.GetComponents<ElementConverter>() ?? Array.Empty<ElementConverter>();
            foreach (var ec in converters)
            {
                if (ec == null || ec.consumedElements == null) continue;
                bool isMilkConv = ec.consumedElements.Any(c => c.Tag == SimHashes.Milk.CreateTag());
                bool isNaphthaConv = ec.consumedElements.Any(c => c.Tag == SimHashes.Naphtha.CreateTag());

                if (isMilkConv)
                {
                    bool enable = (selected == SimHashes.Milk.CreateTag());
                    ec.enabled = enable;
                    ec.ShowInUI = enable;
                }
                if (isNaphthaConv)
                {
                    bool enable = (selected == SimHashes.Naphtha.CreateTag());
                    ec.enabled = enable;
                    ec.ShowInUI = enable;
                }
            }

            // 2) Purge tiny crumbs of the "wrong" solid so they never stall the start in the new mode
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
                Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;
                Tag wrongSolid = (selected == SimHashes.Milk.CreateTag()) ? SimHashes.Sulfur.CreateTag() : fatTag;

                float wrongMass = storage.GetAmountAvailable(wrongSolid);
                if (wrongMass > 0f && wrongMass <= PURGE_MAX && storage.items != null)
                {
                    // Drop only the wrong solid items (small leftovers) to unblock startup
                    var snapshot = storage.items.ToArray();
                    foreach (var item in snapshot)
                    {
                        if (item == null) continue;
                        var pe = item.GetComponent<PrimaryElement>();
                        if (pe == null) continue;
                        if ((selected == SimHashes.Milk.CreateTag() && pe.ElementID == SimHashes.Sulfur) ||
                            (selected == SimHashes.Naphtha.CreateTag() && pe.ElementID == (ElementLoader.FindElementByHash(SimHashes.MilkFat)?.id ?? SimHashes.MilkFat)))
                        {
                            storage.Drop(item);
                        }
                    }
                }
            }

            // 3) Ask the SM to re-evaluate now that converters/tiny crumbs are resolved
            go.Trigger((int)GameHashes.OnStorageChange, null);
        }

     
    }
}