using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Rephysicalized.Content.Building_patches.Boosted_Buildings;

namespace Rephysicalized.Content.Building_patches
{
    internal static class MissionControlBoostedCrafts
    {
        public static readonly HashSet<Clustercraft> EnhancedBoosts = new HashSet<Clustercraft>();
    }

    [HarmonyPatch(typeof(MissionControlClusterConfig), nameof(MissionControlClusterConfig.ConfigureBuildingTemplate))]
    internal static class MissionControlClusterConfig_BoostedBuilding_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            BoostedBuildings.Configure(go, new BoostedBuildings.Configuration
            {
                ItemTag = (Tag)DatabankHelper.ID,
                RequiredAmount = 2f,
                StorageCapacity = 100f,
                RefillMass = 5f,
                BoostedAnimations = new[] { Assets.GetAnim("mission_control_station_boosted_kanim") },
                EnableButtonText = Rephysicalized.STRINGS.BUILDINGS.MISSIONCONTROLSTASTION.BOOSTBUTTON,
                EnableButtonTooltip = Rephysicalized.STRINGS.BUILDINGS.MISSIONCONTROLSTASTION.BOOSTBUTTONDESC,
                DisableButtonText = Rephysicalized.STRINGS.BUILDINGS.MISSIONCONTROLSTASTION.BOOSTBUTTONOFF,
                DisableButtonTooltip = Rephysicalized.STRINGS.BUILDINGS.MISSIONCONTROLSTASTION.BOOSTBUTTONDESCOFF
            });
        }
    }

    [HarmonyPatch(typeof(MissionControlCluster.Instance), nameof(MissionControlCluster.Instance.UpdateWorkableRocketsInRange))]
    internal static class MissionControlCluster_UpdateWorkableRocketsInRange_Patch
    {
        private static readonly System.Reflection.FieldInfo BoostableClustercraftField = AccessTools.Field(typeof(MissionControlCluster.Instance), "boostableClustercraft");

        [HarmonyPostfix]
        private static void Postfix(MissionControlCluster.Instance __instance)
        {
            BoostedBuildings booster = __instance.gameObject.GetComponent<BoostedBuildings>();
            if (booster == null || !booster.HasRequiredItems)
                return;

            List<Clustercraft> boostableClustercraft = (List<Clustercraft>)BoostableClustercraftField.GetValue(__instance);
            AxialI stationLocation = __instance.gameObject.GetMyWorldLocation();
            bool foundAdditionalRocket = false;

            for (int i = 0; i < Components.Clustercrafts.Count; i++)
            {
                Clustercraft clustercraft = Components.Clustercrafts[i];
                if (AxialUtil.GetDistance(stationLocation, clustercraft.Location) > 3 ||
                    clustercraft.controlStationBuffTimeRemaining > 0f ||
                    !clustercraft.HasResourcesToMove() ||
                    !clustercraft.IsFlightInProgress() ||
                    IsOwnWorld(__instance, clustercraft))
                    continue;

                bool alreadyAssigned = false;
                foreach (MissionControlClusterWorkable workable in Components.MissionControlClusterWorkables)
                {
                    if (workable.gameObject != __instance.gameObject && workable.TargetClustercraft == clustercraft)
                    {
                        alreadyAssigned = true;
                        break;
                    }
                }

                if (!alreadyAssigned && !boostableClustercraft.Contains(clustercraft))
                {
                    boostableClustercraft.Add(clustercraft);
                    foundAdditionalRocket = true;
                }
            }

            if (foundAdditionalRocket)
                __instance.sm.WorkableRocketsAreInRange.Set(true, __instance);
        }

        private static bool IsOwnWorld(MissionControlCluster.Instance station, Clustercraft clustercraft)
        {
            WorldContainer interiorWorld = clustercraft.ModuleInterface.GetInteriorWorld();
            return interiorWorld != null && station.gameObject.GetMyWorldId() == interiorWorld.id;
        }
    }

    [HarmonyPatch(typeof(MissionControlClusterWorkable), nameof(MissionControlClusterWorkable.IsRocketInRange))]
    internal static class MissionControlClusterWorkable_IsRocketInRange_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result, AxialI worldLocation, AxialI rocketLocation)
        {
            if (__result)
                return;

            foreach (MissionControlClusterWorkable workable in Components.MissionControlClusterWorkables)
            {
                if (workable.gameObject.GetMyWorldLocation() != worldLocation)
                    continue;

                BoostedBuildings booster = workable.GetComponent<BoostedBuildings>();
                if (booster != null && booster.HasRequiredItems && AxialUtil.GetDistance(worldLocation, rocketLocation) <= 3)
                {
                    __result = true;
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(MissionControlClusterWorkable), "OnCompleteWork")]
    internal static class MissionControlClusterWorkable_OnCompleteWork_BoostedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MissionControlClusterWorkable __instance, out Clustercraft __state)
        {
            __state = __instance.TargetClustercraft;
        }

        [HarmonyPostfix]
        private static void Postfix(MissionControlClusterWorkable __instance, Clustercraft __state)
        {
            BoostedBuildings booster = __instance.GetComponent<BoostedBuildings>();
            if (__state != null && booster != null && booster.TryConsumeRequiredItems())
                MissionControlBoostedCrafts.EnhancedBoosts.Add(__state);
        }
    }

    [HarmonyPatch(typeof(Clustercraft), nameof(Clustercraft.Speed), MethodType.Getter)]
    internal static class Clustercraft_Speed_MissionControlBoost_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Clustercraft __instance, ref float __result)
        {
            if (__instance.controlStationBuffTimeRemaining <= 0f)
            {
                MissionControlBoostedCrafts.EnhancedBoosts.Remove(__instance);
                return;
            }

            if (MissionControlBoostedCrafts.EnhancedBoosts.Contains(__instance))
                __result += __instance.EnginePower / __instance.TotalBurden * 0.1f;
        }
    }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class MissionControlBoostedStatusItem_Patch
    {
        private static bool callbackPatched;

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (callbackPatched)
                return;

            callbackPatched = true;
            StatusItem statusItem = Db.Get().BuildingStatusItems.MissionControlBoosted;
            System.Func<string, object, string> originalCallback = statusItem.resolveStringCallback;
            statusItem.resolveStringCallback = (text, data) =>
            {
                string resolvedText = originalCallback(text, data);
                if (data is Clustercraft clustercraft && MissionControlBoostedCrafts.EnhancedBoosts.Contains(clustercraft))
                {
                    resolvedText = resolvedText.Replace(
                        GameUtil.GetFormattedPercent(20f),
                        GameUtil.GetFormattedPercent(30f));
                }

                return resolvedText;
            };
        }
    }
}
