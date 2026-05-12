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
  

            foreach (var cfg in __result)
            {
                var gt = cfg.geyserType;
       

                if (!WaterSteamIds.Contains(gt.id))
                    continue;

                gt.minRatePerCycle *= WaterGeyserOutput;
                gt.maxRatePerCycle *= WaterGeyserOutput;


            }
        }
    }

//    [HarmonyPatch(typeof(GeyserGenericConfig), nameof(GeyserGenericConfig.GenerateConfigs))]
//    internal class Geyser_Metal_RateScalePatch
//    {

//        public static float MetalVolcanoOutput => Config.Instance.MetalVolcanoOutput;


//        private static readonly HashSet<string> MetalIds = new HashSet<string>
//{
//    GeyserGenericConfig.MoltenGold,
//    GeyserGenericConfig.MoltenCopper,
//    GeyserGenericConfig.MoltenAluminum,
//    GeyserGenericConfig.MoltenCobalt,
//    GeyserGenericConfig.MoltenIron,
//    GeyserGenericConfig.MoltenTungsten,
//    GeyserGenericConfig.MoltenNiobium,


//};

//        [HarmonyPostfix]
//        private static void Postfix(List<GeyserGenericConfig.GeyserPrefabParams> __result)
//        {

//            foreach (var cfg in __result)
//            {
//                var gt = cfg.geyserType;
            
//                if (!MetalIds.Contains(gt.id))
//                    continue;

//                gt.minRatePerCycle *= MetalVolcanoOutput;
//                gt.maxRatePerCycle *= MetalVolcanoOutput;


//            }
//        }
//    }
}


