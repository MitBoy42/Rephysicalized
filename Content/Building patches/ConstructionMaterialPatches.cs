using HarmonyLib;
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

            // Safety guards
            if (oldMats == null || oldMass == null || oldMats.Length != oldMass.Length || oldMats.Length == 0)
            {
                __result.MaterialCategory = new[] { MATERIALS.ALL_METALS[0], MATERIALS.GLASS };
                __result.Mass = new[] { BUILDINGS.CONSTRUCTION_MASS_KG.TIER4[0], GlassMassKg };
                return;
            }

            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            newMats[oldMats.Length] = MATERIALS.GLASS;
            newMass[oldMass.Length] = GlassMassKg;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }
}
