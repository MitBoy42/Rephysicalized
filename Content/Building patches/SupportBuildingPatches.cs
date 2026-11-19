using HarmonyLib;
using Rephysicalized.Content.System_Patches;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{
    public static class SupportBuildingBoostMaterialScaling
    {
        private static float GetMaterialMultiplier(SupportBuilding sb)
        {
            if (sb == null) return 1f;
            float m = StorageMaterialCapacity.GetMultiplier(sb.gameObject);
            if (m <= 0f) m = 1f;
            return m;
        }

        [HarmonyPatch(typeof(SupportBuildingBoost_FabricatorSpeed), "ApplyBoost")]
        public static class FabricatorSpeed_ApplyBoost_Patch
        {
            public static bool Prefix(SupportBuildingBoost_FabricatorSpeed __instance, SupportBuilding supportBuilding, SupportBuildingReceptor receptor)
            {
                if (supportBuilding == null || receptor == null) return true;
                ComplexFabricator component = receptor.GetComponent<ComplexFabricator>();
                if (component == null) return true;

                float mult = GetMaterialMultiplier(supportBuilding);
                float boostAmount = (1f - __instance.SpeedModifier);
                float effectiveBoost = boostAmount * mult;
                float newModifier = 1f - effectiveBoost;
                if (newModifier < 0.01f) newModifier = 0.01f;

                int instanceId = supportBuilding.gameObject.GetInstanceID();
                component.AddSpeedModifier(instanceId, supportBuilding.GetProperName(), newModifier);
                return false;
            }
        }

        [HarmonyPatch(typeof(SupportBuildingBoost_ForgeSpeed), "ApplyBoost")]
        public static class ForgeSpeed_ApplyBoost_Patch
        {
            public static bool Prefix(SupportBuildingBoost_ForgeSpeed __instance, SupportBuilding supportBuilding, SupportBuildingReceptor receptor)
            {
                if (supportBuilding == null || receptor == null) return true;

                ComplexFabricator cf = receptor.GetComponent<ComplexFabricator>();
                if (cf != null)
                {
                    float mult = GetMaterialMultiplier(supportBuilding);
                    float boostAmount = 1f - 0.85f;
                    float newModifier = 1f - boostAmount * mult;
                    if (newModifier < 0.01f) newModifier = 0.01f;
                    int instanceId = supportBuilding.gameObject.GetInstanceID();
                    cf.AddSpeedModifier(instanceId, supportBuilding.GetProperName(), newModifier);
                }

                ElementConverter ec = receptor.GetComponent<ElementConverter>();
                if (ec != null)
                {
                    float mult = GetMaterialMultiplier(supportBuilding);
                    float rateMultiplier = 1f + 0.15f * mult;
                    int instanceId = supportBuilding.gameObject.GetInstanceID();
                    ec.AddRateBoost(instanceId, supportBuilding.GetProperName(), rateMultiplier);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(SupportBuildingBoost_ResearchSpeed), "ApplyBoost")]
        public static class ResearchSpeed_ApplyBoost_Patch
        {
            public static bool Prefix(SupportBuildingBoost_ResearchSpeed __instance, SupportBuilding supportBuilding, SupportBuildingReceptor receptor)
            {
                if (supportBuilding == null || receptor == null) return true;
                float mult = GetMaterialMultiplier(supportBuilding);
                float efficiency = 0.05f * mult;

                ResearchCenter rc = receptor.GetComponent<ResearchCenter>();
                int instanceId = supportBuilding.gameObject.GetInstanceID();
                if (rc != null)
                {
                    rc.AddEfficiencyBoost(instanceId, supportBuilding.GetProperName(), efficiency);
                }
                else
                {
                    NuclearResearchCenterWorkable nrc = receptor.GetComponent<NuclearResearchCenterWorkable>();
                    if (nrc != null)
                        nrc.AddEfficiencyBoost(instanceId, supportBuilding.GetProperName(), efficiency);
                }
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(SupportBuildingReceptor), "ApplyBoost")]
    public static class SupportBuildingReceptor_ApplyBoost_PriorityPatch
    {
        public static bool Prefix(SupportBuildingReceptor __instance, SupportBuilding supportBuilding)
        {
            if (supportBuilding == null) return true;
            SupportBuildingBoost boostData = supportBuilding.GetBoostData();
            if (boostData == null || boostData.CanStack) return true;

            var dict = Traverse.Create(__instance).Field("supportBuildingsBoosts")
                .GetValue<Dictionary<SupportBuilding, SupportBuildingBoost>>();
            if (dict == null) return true;

            SupportBuilding existing = null;
            foreach (var kvp in dict)
            {
                if (kvp.Value != null && kvp.Value.Id == boostData.Id)
                {
                    existing = kvp.Key;
                    break;
                }
            }
            if (existing == null) return true;

            float newMult = StorageMaterialCapacity.GetMultiplier(supportBuilding.gameObject);
            float oldMult = StorageMaterialCapacity.GetMultiplier(existing.gameObject);
            if (newMult > oldMult + 0.0001f)
            {
                __instance.RemoveBoost(existing);
                return true;
            }
            return false;
        }
    }

  
}
