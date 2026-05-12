using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;
using Rephysicalized;
using TUNING;
using KSerialization;
using Rephysicalized.ModElements;

namespace Rephysicalized
{
    internal static class BatteryCapacityUtil
    {
        // Attach a tiny component to cache base capacity per instance so we can recompute if needed.
        private sealed class Cache : KMonoBehaviour
        {
            public float BaseCapacity;
            public bool Initialized;
        }

        public static void ApplyCapacityMultiplier(GameObject go)
        {
            if (go == null) return;

            var battery = go.GetComponent<Battery>();
            if (battery == null) return;

            var cache = go.GetComponent<Cache>() ?? go.AddOrGet<Cache>();
            if (!cache.Initialized)
            {
                cache.BaseCapacity = battery.capacity; // vanilla/post-configured capacity
                cache.Initialized = true;
            }

            float baseCap = cache.BaseCapacity;
            if (baseCap <= 0f) baseCap = battery.capacity;

            var pe = go.GetComponent<PrimaryElement>();
            var elementId = pe != null ? pe.ElementID : SimHashes.Vacuum;

            // Use the same element multipliers as wires
            float mult = WireCapacitySettings.GetMultiplier(elementId);
            if (mult <= 0f) mult = 1f;

            battery.capacity = baseCap * mult;
        }

        // Helper to gate only specific battery prefabs
        public static bool IsSupportedBatteryPrefab(GameObject go)
        {
            var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
            if (kpid == null) return false;

            // Standard vanilla IDs
            // BatteryConfig.ID, BatterySmartConfig.ID, BatteryMediumConfig.ID
            var tag = kpid.PrefabTag;
            return tag == BatteryConfig.ID || (AccessTools.TypeByName("BatterySmartConfig") != null && tag == TagManager.Create("BatterySmart"))
   || (AccessTools.TypeByName("BatteryMediumConfig") != null && tag == TagManager.Create("BatteryMedium"));
        }
    }

    // Apply on spawn, but only for the three intended prefabs.
    [HarmonyPatch(typeof(Battery), "OnSpawn")]
    internal static class Battery_OnSpawn_ElementCapacity_ForSupportedPrefabs
    {
        private static void Postfix(Battery __instance)
        {
            if (__instance == null) return;

            var go = __instance.gameObject;
            if (!BatteryCapacityUtil.IsSupportedBatteryPrefab(go))
                return;

            BatteryCapacityUtil.ApplyCapacityMultiplier(go);
        }
    }

    [HarmonyPatch(typeof(BatteryConfig), "ConfigureBuildingTemplate")]
      public static class BatteryConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (!Config.Instance.BatteryWaterDamage) return;

            var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
            }

      }
      [HarmonyPatch(typeof(BatteryMediumConfig), "ConfigureBuildingTemplate")]
      public static class BatteryMediumConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (!Config.Instance.BatteryWaterDamage) return;

            var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
            }

      }
      [HarmonyPatch(typeof(BatterySmartConfig), "ConfigureBuildingTemplate")]
      public static class BatterySmartConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (!Config.Instance.BatteryWaterDamage) return;

            var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
                  var Resistance = BatteryWaterDamagePatch.WATER_DAMAGE_CHANCE;
                  Resistance = 12;
            }

      }
      [HarmonyPatch(typeof(PowerTransformerConfig), "ConfigureBuildingTemplate")]
      public static class PowerTransformerConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
            if (!Config.Instance.BatteryWaterDamage) return;

            var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
                  var Resistance = BatteryWaterDamagePatch.WATER_DAMAGE_CHANCE;
                  Resistance = 12;
            }

      }
       [HarmonyPatch(typeof(PowerTransformerSmallConfig), "ConfigureBuildingTemplate")]
      public static class PowerTransformerSmallConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
            if (!Config.Instance.BatteryWaterDamage) return;

            var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
          
            }

      }
}