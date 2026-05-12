using HarmonyLib;
using Klei;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TUNING;

namespace Rephysicalized.Content.System_Patches
{


internal static class StorageCapacitySettings
    {
        public static Dictionary<SimHashes, float> Multipliers = new();

        public static void InitializeBaseMultipliers()
        {
            Multipliers = new Dictionary<SimHashes, float>
            {
              { SimHashes.Cinnabar, 0.4f },
           { SimHashes.SolidMercury, 0.4f },

            { SimHashes.Aluminum, 0.6f },
            { SimHashes.AluminumOre, 0.6f },
            { SimHashes.Lead, 0.6f },

            { SimHashes.Gold, 0.8f },
            { SimHashes.GoldAmalgam, 0.8f },

            { SimHashes.Cobalt, 1.2f },
            { SimHashes.Cobaltite, 1.2f },
          { SimHashes.Niobium, 1.2f },
           { SimHashes.Steel, 1.2f },

           { SimHashes.Nickel, 1.4f },
           { SimHashes.NickelOre, 1.4f },
           { SimHashes.Wolframite, 1.4f },
           { SimHashes.Tungsten, 1.4f },
          { SimHashes.TempConductorSolid, 1.4f },

       { SimHashes.Iridium, 1.6f },
            };
        }

        public static float GetMultiplier(SimHashes id)
        {
            return Multipliers.TryGetValue(id, out var mult) ? Mathf.Max(0f, mult) : 1f;
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class StorageCapacitySettingsInitializer
    {
        public static void Postfix()
        {
            StorageCapacitySettings.InitializeBaseMultipliers();
        }
    }

    public static class StorageMaterialCapacity
    {
        public static float GetMultiplier(GameObject go)
        {
            if (go == null)
                return 1f;
            var pe = go.GetComponent<PrimaryElement>();
            if (pe == null)
                return 1f;
            return StorageCapacitySettings.GetMultiplier(pe.ElementID);

        }
        public static float GetMultiplier(Element element)
        {
            if (element == null)
                return 1f;
            return StorageCapacitySettings.GetMultiplier(element.id);
        }
    }

        // Helper component to cache base storage capacity per instance
        public sealed class StorageCapacityCache : KMonoBehaviour
    {
        public float BaseCapacityKg;
        public bool Initialized;
    }

    internal static class StorageCapacityUtil
    {
        public static void ApplyCapacityMultiplier(GameObject go)
        {
            if (go == null) return;

            var storage = go.GetComponent<Storage>();
            if (storage == null) return;

            var cache = go.GetComponent<StorageCapacityCache>() ?? go.AddOrGet<StorageCapacityCache>();
            if (!cache.Initialized)
            {
                cache.BaseCapacityKg = storage.capacityKg;
                cache.Initialized = true;
            }

            float baseCap = cache.BaseCapacityKg;
            if (baseCap <= 0f) baseCap = storage.capacityKg;

            var pe = go.GetComponent<PrimaryElement>();
            var elementId = pe != null ? pe.ElementID : SimHashes.Vacuum;

            float mult = StorageCapacitySettings.GetMultiplier(elementId);
            if (mult <= 0f) mult = 1f;

            storage.capacityKg = baseCap * mult;
          //  Debug.Log($"[StorageCapacity] Storage capacity modified by {mult}x (element: {elementId}), new capacity: {storage.capacityKg}kg");

            // Also update ConduitConsumer capacity if it exists
            var conduitConsumer = go.GetComponent<ConduitConsumer>();
            if (conduitConsumer != null)
            {
                conduitConsumer.capacityKG = storage.capacityKg;
            }

            // Force meter update to show correct fill level for Reservoir buildings
            var reservoir = go.GetComponent<Reservoir>();
            if (reservoir != null)
            {
                // Skip trigger if Ronivans SmartReservoir present to avoid crash
                if (go.GetComponent("SmartReservoir") == null)
                {
                    reservoir.Trigger(-1697596308); // GameHashes.OnStorageChange
                }
            }
        }
    }

    // Individual patches for each building type to avoid scanning all buildings

    //Reservoir
    [HarmonyPatch(typeof(Reservoir), "OnSpawn")]
    internal static class Reservoir_OnSpawn_ElementCapacity
    {
        public static void Postfix(Reservoir __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }



    // SolidConduitInbox
    [HarmonyPatch(typeof(SolidConduitInbox), "OnSpawn")]
    internal static class SolidConduitInbox_OnSpawn_ElementCapacity
    {
        private static void Postfix(SolidConduitInbox __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }

    // BottleEmptier
    [HarmonyPatch(typeof(BottleEmptier), "OnSpawn")]
    internal static class BottleEmptier_OnSpawn_ElementCapacity
    {
        private static void Postfix(BottleEmptier __instance)
        {

            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }


    // Bottle
    [HarmonyPatch(typeof(Bottler), "OnSpawn")]
    internal static class BottlerElementCapacity
    {
        private static void Postfix(Bottler __instance)
        {

            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }




    // StorageLocker
    [HarmonyPatch(typeof(StorageLocker), "OnSpawn")]
    internal static class StorageLocker_OnSpawn_ElementCapacity
    {
        public static void Postfix(StorageLocker __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }

    // StorageLockerSmart
    [HarmonyPatch(typeof(StorageLockerSmart), "OnSpawn")]
    internal static class StorageLockerSmart_OnSpawn_ElementCapacity
    {
        public static void Postfix(StorageLockerSmart __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }

    // ObjectDispenser
    [HarmonyPatch(typeof(ObjectDispenser), "OnSpawn")]
    internal static class ObjectDispenser_OnSpawn_ElementCapacity
    {
        private static void Postfix(ObjectDispenser __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }

    // CreatureFeeder
    [HarmonyPatch(typeof(CreatureFeeder), "OnSpawn")]
    internal static class CreatureFeeder_OnSpawn_ElementCapacity
    {
        private static void Postfix(CreatureFeeder __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }
    // Refrigerator
    [HarmonyPatch(typeof(Refrigerator), "OnSpawn")]
    internal static class Refrigerator_OnSpawn_ElementCapacity
    {
        public static void Postfix(Refrigerator __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }
    // RationBox
    [HarmonyPatch(typeof(RationBox), "OnSpawn")]
    internal static class RationBox_OnSpawn_ElementCapacity
    {
        public static void Postfix(RationBox __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }


    // StorageTile - use the Instance class (which is a KMonoBehaviour)
    [HarmonyPatch(typeof(StorageTile.Instance), "StartSM")]
    internal static class StorageTile_OnSpawn_ElementCapacity
    {
        private static void Postfix(StorageTile.Instance __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }



    // MilkFeeder
    [HarmonyPatch(typeof(MilkFeeder.Instance), "StartSM")]
    internal static class MilkFeeder_OnSpawn_ElementCapacity
    {
        private static void Postfix(MilkFeeder.Instance __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }

    // SolidConduitOutbox
    [HarmonyPatch(typeof(SolidConduitOutbox), "OnSpawn")]
    internal static class SolidConduitOutbox_OnSpawn_ElementCapacity
    {
        private static void Postfix(SolidConduitOutbox __instance)
        {
            if (__instance == null) return;
            StorageCapacityUtil.ApplyCapacityMultiplier(__instance.gameObject);
        }
    }




    internal static class StorageCapacityDescriptorHelper
    {
        public static void AppendDescriptor(Element element, ref List<Descriptor> descriptors)
        {
            if (element == null || descriptors == null)
                return;

            float mult = StorageMaterialCapacity.GetMultiplier(element);
            if (Mathf.Approximately(mult, 1f))
                return;

            float deltaPct = (mult - 1f) * 100f;
            string sign = deltaPct >= 0f ? "+" : "";
            string value = sign + deltaPct.ToString("0.#") + "%";

            var d = new Descriptor();
            d.SetupDescriptor(string.Format((string)STRINGS.MATERIAL_MODIFIERS.CAPACITY, value),
                              string.Format((string)STRINGS.MATERIAL_MODIFIERS.TOOLTIP.CAPACITY, value));
            d.IncreaseIndent();
            descriptors.Add(d);
        }
    }



    // Patch GameUtil material descriptor builders to include capacity modifier lines
    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Tag) })]
    internal static class GameUtil_GetStorageCapacityDescriptors_Tag_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Tag tag, ref List<Descriptor> __result)
        {

            var elem = ElementLoader.GetElement(tag);
            if (elem == null) return;
            StorageCapacityDescriptorHelper.AppendDescriptor(elem, ref __result);

        }
    }

    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Element) })]
    internal static class GameUtil_GetStorageCapacityDescriptors_Element_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Element element, ref List<Descriptor> __result)
        {

            StorageCapacityDescriptorHelper.AppendDescriptor(element, ref __result);


        }
    }

    [HarmonyPatch(typeof(MaterialSelector), nameof(MaterialSelector.ConfigureMaterialTooltips))]
    internal static class MaterialSelector_ConfigureMaterialCapacityTooltips_Patch
    {
        private static void Postfix(MaterialSelector __instance)
        {
            if (__instance == null || __instance.ElementToggles == null)
                return;

            foreach (var kvp in __instance.ElementToggles)
            {
                var tag = kvp.Key;
                var toggle = kvp.Value;
                if (toggle == null || toggle.gameObject == null)
                    continue;

                var tt = toggle.gameObject.GetComponent<ToolTip>();
                if (tt == null)
                    continue;

                var elem = ElementLoader.GetElement(tag);
                if (elem == null)
                    continue;

                var list = new List<Descriptor>(1);
                StorageCapacityDescriptorHelper.AppendDescriptor(elem, ref list);
                if (list.Count == 0)
                    continue;

                string extraLine = list[0].text;
                if (string.IsNullOrEmpty(extraLine))
                    continue;

                // Avoid adding the same line multiple times if tooltips are rebuilt
                if (!tt.multiStringToolTips.Contains(extraLine))
                {
                    tt.AddMultiStringTooltip(extraLine, PluginAssets.Instance.defaultTextStyleSetting);
                }
            }
        }

    }
}

