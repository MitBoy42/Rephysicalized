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
      [HarmonyPatch(typeof(BatteryConfig), "ConfigureBuildingTemplate")]
      public static class BatteryConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
                  var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
            }

      }
      [HarmonyPatch(typeof(BatteryMediumConfig), "ConfigureBuildingTemplate")]
      public static class BatteryMediumConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
                  var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
            }

      }
      [HarmonyPatch(typeof(BatterySmartConfig), "ConfigureBuildingTemplate")]
      public static class BatterySmartConfig_WaterDamagePatch
      {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
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
                  var WaterDamage = go.AddComponent<BatteryWaterDamagePatch>();
          
            }

      }
}