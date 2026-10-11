using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtilLibs;
using static ModUtil;
using static UtilLibs.GameStrings;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.LoadGeneratedBuildings))]
    public static class LightsIntoUtilities
    {
        [HarmonyPostfix] private static void AfterLoadGeneratedBuildings() { MoveVanilla(); }
        internal static void MoveVanilla()
        {

            if (!Config.Instance.LightsInUtility) return;
           
            string[] ids =
            {
            FloorLampConfig.ID,
            CeilingLightConfig.ID,
            SunLampConfig.ID,
            DevLightGeneratorConfig.ID,

            MercuryCeilingLightConfig.ID,
             GlassCeilingLightConfig.ID
        };

       

            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                // DLC5-only: fully skip these two lights unless DLC5 is subscribed.
                if (!DlcManager.IsContentSubscribed("DLC5_ID") && (id == GlassCeilingLightConfig.ID))
                    continue;
                if (!DlcManager.IsContentSubscribed("DLC3_ID") && (id == MercuryCeilingLightConfig.ID))
                    continue;

                InjectionMethods.MoveExistingBuildingToNewCategory(
                    category: PlanMenuCategory.Utilities,
                    building_id: id,
                    relativeBuildingId: "SpaceHeater"
                );
            }
        }
    }
}
