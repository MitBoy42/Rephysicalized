using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUNING;

namespace Rephysicalized.Content.Building_patches
{
    [HarmonyPatch(typeof(RefrigeratorConfig), nameof(RefrigeratorConfig.CreateBuildingDef))]
    public static class RefrigeratorConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.MaterialCategory = TUNING.MATERIALS.RAW_MINERALS_OR_METALS;
        }
    }
  
    [HarmonyPatch(typeof(RationBoxConfig), nameof(RationBoxConfig.CreateBuildingDef))]
    public static class RationBoxConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.MaterialCategory = TUNING.MATERIALS.RAW_MINERALS_OR_METALS;
        }
    }

    [HarmonyPatch(typeof(BottleEmptierConfig), nameof(BottleEmptierConfig.CreateBuildingDef))]
    public static class BottleEmptierConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.MaterialCategory = TUNING.MATERIALS.RAW_MINERALS_OR_METALS;

        }
    }



    [HarmonyPatch(typeof(MiniFridgeConfig), nameof(MiniFridgeConfig.CreateBuildingDef))]
    public static class MiniFridgeConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            // MiniFridge has two material slots: { BuildableRaw, BuildingGasket }.
            // Replace only slot 0 with RAW_MINERALS_OR_METALS and keep BuildingGasket unchanged.
            if (__result == null || __result.MaterialCategory == null)
                return;

            if (__result.MaterialCategory.Length >= 1)
                __result.MaterialCategory[0] = "BuildableRaw&Metal";
        }
    }


    // Patch ClusterTelescopeEnclosedConfig.CreateBuildingDef to require both metal (same mass) and 100kg of Glass
    [HarmonyPatch(typeof(ClusterTelescopeEnclosedConfig), nameof(ClusterTelescopeEnclosedConfig.CreateBuildingDef))]
    public static class ClusterTelescopeEnclosed_MaterialsPatch
    {
        private const float GlassMassKg = 100f;


        // We’ll wrap the original call by intercepting the BuildingDef after it’s created and overwrite its mass/materials.
        public static void Postfix(ref BuildingDef __result)
        {
 
            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

      

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = TUNING.MATERIALS.GLASSES[0];
            newMass[oldMass.Length] = GlassMassKg;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }

    [HarmonyPatch(typeof(DataMinerConfig), nameof(DataMinerConfig.CreateBuildingDef))]
    public static class DataMiner_MaterialsPatch
    {
    


        public static void Postfix(ref BuildingDef __result)
        {

            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = ModElementRegistration.Silicon.Tag.ToString();
            newMass[oldMass.Length] = 50f;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
    [HarmonyPatch(typeof(GeneticAnalysisStationConfig), nameof(GeneticAnalysisStationConfig.CreateBuildingDef))]
    public static class GeneticAnalysisStation_MaterialsPatch
    {



        public static void Postfix(ref BuildingDef __result)
        {

            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = ModElementRegistration.Silicon.Tag.ToString();
            newMass[oldMass.Length] = 50f;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
    [HarmonyPatch(typeof(MissionControlConfig), nameof(MissionControlConfig.CreateBuildingDef))]
    public static class MissionControl_MaterialsPatch
    {
        public static void Postfix(ref BuildingDef __result)
        {

            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = ModElementRegistration.Silicon.Tag.ToString();
            newMass[oldMass.Length] = 50f;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
    [HarmonyPatch(typeof(MissionControlClusterConfig), nameof(MissionControlClusterConfig.CreateBuildingDef))]
    public static class MissionControlCluster_MaterialsPatch
    {
        public static void Postfix(ref BuildingDef __result)
        {

            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = ModElementRegistration.Silicon.Tag.ToString();
            newMass[oldMass.Length] = 50f;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
    [HarmonyPatch(typeof(ArcadeMachineConfig), nameof(ArcadeMachineConfig.CreateBuildingDef))]
    public static class ArcadeMachine_MaterialsPatch
    {
        public static void Postfix(ref BuildingDef __result)
        {

            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = ModElementRegistration.Silicon.Tag.ToString();
            newMass[oldMass.Length] = 50f;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
}
