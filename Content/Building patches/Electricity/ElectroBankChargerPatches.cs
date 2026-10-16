using System;
using Database;
using HarmonyLib;
using Rephysicalized;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Electricity
{
    [HarmonyPatch(typeof(BuildingStatusItems), MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
    internal static class ElectroBankCharger_StatusItem_Patch
    {
        private static void Postfix(BuildingStatusItems __instance)
        {
            if (!Config.Instance.ChargerSpeed)
                return;
            if (__instance == null || __instance.PowerBankChargerInProgress == null)
            {
                return;
            }

            __instance.PowerBankChargerInProgress.resolveStringCallback = ResolveString;
        }

        private static string ResolveString(string text, object data)
        {
            if (!(data is ElectrobankCharger.Instance charger) || charger.targetElectrobank == null)
            {
                return text;
            }

            PrimaryElement element = charger.master.GetComponent<PrimaryElement>();
            float multiplier = element != null ? WireCapacitySettings.GetMultiplier(element.ElementID) : 1f;
            return string.Format(text, GameUtil.GetFormattedWattage(400f * multiplier));
        }
    }

    [HarmonyPatch(typeof(EnergyConsumer), "OnSpawn")]
    internal static class ElectroBankCharger_EnergyConsumer_OnSpawn_Patch
    {
        private static void Postfix(EnergyConsumer __instance)
        {
            if (!Config.Instance.ChargerSpeed)
                return;
            if (__instance == null)
            {
                return;
            }

            GameObject go = __instance.gameObject;
            KPrefabID prefabId = go.GetComponent<KPrefabID>();
            if (prefabId == null || prefabId.PrefabTag.Name != ElectrobankChargerConfig.ID)
            {
                return;
            }

            Building building = go.GetComponent<Building>();
            PrimaryElement element = go.GetComponent<PrimaryElement>();
            if (building == null || building.Def == null || element == null)
            {
                return;
            }

            __instance.BaseWattageRating = building.Def.EnergyConsumptionWhenActive *
                WireCapacitySettings.GetMultiplier(element.ElementID);
        }
    }

    [HarmonyPatch(typeof(ElectrobankCharger.Instance), nameof(ElectrobankCharger.Instance.ChargeInternal))]
    internal static class ElectroBankCharger_ChargeInternal_Patch
    {
        private static void Prefix(ElectrobankCharger.Instance smi, ref float dt)
        {
            if (!Config.Instance.ChargerSpeed)
                return;
            if (smi == null || smi.master == null)
            {
                return;
            }

            PrimaryElement element = smi.master.GetComponent<PrimaryElement>();
            if (element != null)
            {
                dt *= WireCapacitySettings.GetMultiplier(element.ElementID);
            }
        }
    }

    internal static class ElectroBankDischargerMaterialMultiplier
    {
        internal static bool TryGetMultiplier(ElectrobankDischarger discharger, out float multiplier)
        {
          
            multiplier = 1f;
            if (discharger == null)
            {
                return false;
            }

            KPrefabID prefabId = discharger.GetComponent<KPrefabID>();
            if (prefabId == null ||
                (prefabId.PrefabTag.Name != SmallElectrobankDischargerConfig.ID &&
                 prefabId.PrefabTag.Name != LargeElectrobankDischargerConfig.ID))
            {
                return false;
            }

            PrimaryElement element = discharger.GetComponent<PrimaryElement>();
            if (element == null)
            {
                return false;
            }

            multiplier = WireCapacitySettings.GetMultiplier(element.ElementID);
            return true;
        }
    }

    [HarmonyPatch(typeof(ElectrobankDischarger), nameof(ElectrobankDischarger.OnSpawn))]
    internal static class ElectroBankDischarger_OnSpawn_Wattage_Patch
    {
        private static void Postfix(ElectrobankDischarger __instance)
        {
            if (__instance == null || !ElectroBankDischargerMaterialMultiplier.TryGetMultiplier(__instance, out float multiplier))
            {
                return;
            }
            if (!Config.Instance.ChargerSpeed)
                return;
            Building building = __instance.GetComponent<Building>();
            if (building != null && building.Def != null)
            {
                __instance.wattageRating = building.Def.GeneratorWattageRating * multiplier;
            }
        }
    }

    [HarmonyPatch(typeof(Generator), "get_WattageRating")]
    internal static class ElectroBankDischarger_WattageRating_Patch
    {
        private static void Postfix(Generator __instance, ref float __result)
        {
            if (!Config.Instance.ChargerSpeed)
                return;
            if (__instance is ElectrobankDischarger discharger &&
                ElectroBankDischargerMaterialMultiplier.TryGetMultiplier(discharger, out float multiplier))
            {
                __result *= multiplier;
            }
        }
    }
}
