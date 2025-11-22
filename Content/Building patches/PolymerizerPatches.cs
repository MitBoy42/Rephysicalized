using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    // Keep vanilla plastics converter values (do not merge with resin)
    [HarmonyPatch(typeof(PolymerizerConfig), "ConfigureBuildingTemplate")]
    public static class PolymerizerPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter != null)
            {
                elementConverter.consumedElements = new ElementConverter.ConsumedElement[1]
                {
                    new ElementConverter.ConsumedElement(PolymerizerConfig.INPUT_ELEMENT_TAG, 0.8f)
                };

                elementConverter.outputElements = new ElementConverter.OutputElement[3]
                {
                    new ElementConverter.OutputElement(0.5f, SimHashes.Polypropylene, 348.15f, storeOutput: true),
                    new ElementConverter.OutputElement(0.15f, SimHashes.Steam, 383.15f, storeOutput: true),
                    new ElementConverter.OutputElement(0.15f, SimHashes.CarbonDioxide, 423.15f, storeOutput: true)
                };
            }
        }
    }

    // Replace ConduitConsumer with RephysicalizedConduitConsumer and add a separate Resin converter.
    [HarmonyPatch(typeof(PolymerizerConfig), nameof(PolymerizerConfig.ConfigureBuildingTemplate))]
    public static class PolymerizerConfig_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Replace vanilla ConduitConsumer with RephysicalizedConduitConsumer
            var original = go.GetComponent<ConduitConsumer>();
            float capacityKG = 1.666667f;
            float consumptionRate = 1.666667f;
            bool forceAlwaysSatisfied = true;
            ConduitConsumer.WrongElementResult wrongElementResult = ConduitConsumer.WrongElementResult.Dump;

            if (original != null)
            {
                capacityKG = original.capacityKG;
                consumptionRate = original.consumptionRate;
                forceAlwaysSatisfied = original.forceAlwaysSatisfied;
                wrongElementResult = original.wrongElementResult;
                UnityEngine.Object.DestroyImmediate(original);
            }

   


            var rcc = go.AddOrGet<RephysicalizedConduitConsumer>();
            rcc.conduitType = ConduitType.Liquid;
            rcc.consumptionRate = consumptionRate;
            rcc.capacityKG = capacityKG; // limits only accepted inputs (plastifiable + resin)
            rcc.forceAlwaysSatisfied = forceAlwaysSatisfied;
            rcc.wrongElementResult = wrongElementResult;
            rcc.consumptionRate = 1.666667f;
            rcc.capacityKG = 1.666667f;

            rcc.acceptedTags = new[] { PolymerizerConfig.INPUT_ELEMENT_TAG }; // PlastifiableLiquid
            rcc.acceptedElements = new[] { SimHashes.Resin };                 // Resin

            // Allow both CO2 and Hydrogen to be dispensed without overwriting existing filters
            var dispenser = go.AddOrGet<ConduitDispenser>();
            dispenser.conduitType = ConduitType.Gas;
            dispenser.invertElementFilter = false;

            var filter = dispenser.elementFilter != null
                ? new List<SimHashes>(dispenser.elementFilter)
                : new List<SimHashes>();
            if (!filter.Contains(SimHashes.CarbonDioxide)) filter.Add(SimHashes.CarbonDioxide);
            if (!filter.Contains(SimHashes.Hydrogen)) filter.Add(SimHashes.Hydrogen);
            dispenser.elementFilter = filter.ToArray();

            // Separate Resin converter (unchanged, keep tuned numbers)
            var resinConverter = go.AddComponent<ElementConverter>();
            resinConverter.inputIsCategory = false;
            resinConverter.showDescriptors = false;
            resinConverter.ShowInUI = false;
            resinConverter.consumedElements = new ElementConverter.ConsumedElement[]
            {
                new ElementConverter.ConsumedElement(GameTagExtensions.Create(SimHashes.Resin), 0.8f)
            };
            resinConverter.outputElements = new ElementConverter.OutputElement[]
            {
                new ElementConverter.OutputElement(0.5f, SimHashes.Polypropylene, 348.15f, storeOutput: true),
                new ElementConverter.OutputElement(0.15f, SimHashes.EthanolGas, 383.15f, storeOutput: true),
                new ElementConverter.OutputElement(0.15f, SimHashes.Hydrogen, 423.15f, storeOutput: true)
            };
        }
    }

  

    // Intercept the vanilla consumer tick: if a ConduitConsumer is attached to a Polymerizer, skip its update.
    [HarmonyPatch(typeof(ConduitConsumer), "ConduitUpdate")]
    public static class ConduitConsumer_ConduitUpdate_BlockOnPolymerizer
    {
        public static bool Prefix(ConduitConsumer __instance, float dt)
        {
            try
            {
                if (__instance != null && __instance.GetComponent<Polymerizer>() != null)
                {
                    return false; // skip original
                }
            }
            catch
            {
                // best-effort
            }
            return true;
        }
    }

    // If any RequireInputs component survives, skip its logic on Polymerizer to prevent NREs.
    [HarmonyPatch(typeof(RequireInputs), nameof(RequireInputs.OnSpawn))]
    public static class RequireInputs_OnSpawn_SkipOnPolymerizer
    {
        public static bool Prefix(RequireInputs __instance)
        {
            try
            {
                if (__instance != null && __instance.GetComponent<Polymerizer>() != null)
                {
                    return false; // skip original
                }
            }
            catch
            {
                // best-effort
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RequireInputs), "CheckRequirements")]
    public static class RequireInputs_CheckRequirements_SkipOnPolymerizer
    {
        public static bool Prefix(RequireInputs __instance, bool forceEvent)
        {
            try
            {
                if (__instance != null && __instance.GetComponent<Polymerizer>() != null)
                {
              
                    return false;
                }
            }
            catch
            {
                // best-effort
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.CanConvertAtAll))]
    public static class ElementConverter_CanConvertAtAll_Prefix
    {
        public static bool Prefix(ElementConverter __instance, ref bool __result)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<Polymerizer>() == null) return true; // not our building

            __result = Polymerizer_SM_Utils.AnyConverterCanConvertOnPoly(__instance);
            return false; // skip original for Polymerizer
        }
    }

    [HarmonyPatch(typeof(ElementConverter), "HasEnoughMassToStartConverting")]
    public static class ElementConverter_HasEnoughMassToStartConverting_Prefix
    {
        public static bool Prefix(ElementConverter __instance, ref bool __result)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<Polymerizer>() == null) return true; // not our building

            __result = Polymerizer_SM_Utils.AnyConverterHasEnoughToStartOnPoly(__instance);
            return false; // skip original for Polymerizer
        }
    }

    // Make the oil meter work without referencing the removed vanilla ConduitConsumer
    [HarmonyPatch(typeof(Polymerizer), "UpdateOilMeter")]
    public static class Polymerizer_UpdateOilMeter_Patch
    {
        public static bool Prefix(Polymerizer __instance)
        {
            try
            {
                var storage = AccessTools.Field(__instance.GetType(), "storage")?.GetValue(__instance) as Storage;
                var oilMeter = AccessTools.Field(__instance.GetType(), "oilMeter")?.GetValue(__instance) as MeterController;

                float total = 0f;
                if (storage != null)
                {
                    var plastifiable = PolymerizerConfig.INPUT_ELEMENT_TAG;
                    var resinTag = GameTagExtensions.Create(SimHashes.Resin);

                    foreach (var go in storage.items)
                    {
                        if (go == null) continue;
                        if (go.HasTag(plastifiable) || go.HasTag(resinTag))
                        {
                            var pe = go.GetComponent<PrimaryElement>();
                            if (pe != null) total += pe.Mass;
                        }
                    }
                }

                float capacity = 1.666667f;
                var rcc = __instance.GetComponent<RephysicalizedConduitConsumer>();
                if (rcc != null)
                    capacity = rcc.capacityKG;

                oilMeter?.SetPositionPercent(Mathf.Clamp01(total / Mathf.Max(capacity, 0.0001f)));
            }
            catch
            {
                // best-effort
            }
            return false;
        }
    }

    public static class Polymerizer_SM_Utils
    {
        public static bool AnyConverterCanConvertOnPoly(ElementConverter instance)
        {
            if (instance == null) return false;
            var go = instance.gameObject;
            if (go == null) return false;
            if (go.GetComponent<Polymerizer>() == null) return false;

            var storage = go.GetComponent<Storage>();
            if (storage == null) return false;

            var converters = go.GetComponents<ElementConverter>();
            foreach (var c in converters)
            {
                if (c == null || c.consumedElements == null) continue;
                foreach (var ce in c.consumedElements)
                {
                    if (HasAnyMassWithTag(storage, ce.Tag))
                        return true;
                }
            }
            return false;
        }

        public static bool AnyConverterHasEnoughToStartOnPoly(ElementConverter instance)
        {
            if (instance == null) return false;
            var go = instance.gameObject;
            if (go == null) return false;
            if (go.GetComponent<Polymerizer>() == null) return false;

            var storage = go.GetComponent<Storage>();
            if (storage == null) return false;

            var converters = go.GetComponents<ElementConverter>();
            foreach (var c in converters)
            {
                if (c == null || c.consumedElements == null || c.consumedElements.Length == 0) continue;

                bool allPresent = true;
                foreach (var ce in c.consumedElements)
                {
                    if (!HasAnyMassWithTag(storage, ce.Tag))
                    {
                        allPresent = false;
                        break;
                    }
                }
                if (allPresent)
                    return true;
            }
            return false;
        }

        private static bool HasAnyMassWithTag(Storage storage, Tag tag)
        {
            var items = storage.items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = items[i];
                if (go == null) continue;
                if (!go.HasTag(tag)) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null && pe.Mass > 0f)
                    return true;
            }
            return false;
        }
    }
}