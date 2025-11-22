using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(GeyserGenericConfig), nameof(GeyserGenericConfig.GenerateConfigs))]
    internal class Geyser_WaterSteam_RateScalePatch
    {
        public static float WaterGeyserOutput => Config.Instance.WaterGeyserOutput;

        private static readonly HashSet<string> WaterSteamIds = new HashSet<string>
{
    GeyserGenericConfig.Steam,
    GeyserGenericConfig.HotSteam,
    GeyserGenericConfig.HotWater,
    GeyserGenericConfig.SlushWater,
    GeyserGenericConfig.FilthyWater,
    GeyserGenericConfig.SlushSaltWater,
    GeyserGenericConfig.SaltWater
};

        [HarmonyPostfix]
        private static void Postfix(List<GeyserGenericConfig.GeyserPrefabParams> __result)
        {
            if (__result == null || __result.Count == 0)
                return;

            foreach (var cfg in __result)
            {
                var gt = cfg.geyserType;
                if (gt == null)
                    continue;

                // Match on the string id declared in GenerateConfigs
                if (!WaterSteamIds.Contains(gt.id))
                    continue;

                gt.minRatePerCycle *= WaterGeyserOutput;
                gt.maxRatePerCycle *= WaterGeyserOutput;


            }
        }
    }
}
