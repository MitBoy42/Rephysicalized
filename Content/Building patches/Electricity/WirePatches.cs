using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUNING;

namespace Rephysicalized.Content.Building_patches
{

    [HarmonyPatch(typeof(WireRefinedConfig), nameof(WireRefinedConfig.CreateBuildingDef))]
    public static class WireRefinedConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            if (!Config.Instance.RephysicalizedMetalOre) return;
            __result.Mass = [15f];
        }
    }


    [HarmonyPatch(typeof(WireRefinedHighWattageConfig), nameof(WireRefinedHighWattageConfig.CreateBuildingDef))]
    public static class WireRefinedHighWattageConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {

            if (!Config.Instance.RephysicalizedMetalOre) return;
            __result.Mass = [60f];
        }
    }

    [HarmonyPatch(typeof(WireRubberConfig), nameof(WireRubberConfig.CreateBuildingDef))]
    public static class WireRubberConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            if (!Config.Instance.RephysicalizedMetalOre) return;

            // WireRubber uses 2 materials (RefinedMetal, Rubber). Only adjust the first one.
            __result.Mass = [15f, __result.Mass.Length > 1 ? __result.Mass[1] : 0f];
        }
    }
}
