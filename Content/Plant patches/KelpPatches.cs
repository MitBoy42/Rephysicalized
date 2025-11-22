using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized.Content.Plant_patches
{
    // Set vanilla Salt crop yield to 0 so only PMT governs harvest output
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Kelp_ZeroYieldPatch
    {
        private static void Postfix()
        {
            var crops = TUNING.CROPS.CROP_TYPES;
            string KelpId = "Kelp"; 
            for (int i = 0; i < crops.Count; i++)
            {
                if (crops[i].cropId == KelpId)
                {
                    var c = crops[i];
                    crops[i] = new Crop.CropVal(c.cropId, c.cropDuration, 10);
                    break;
                }
            }
        }
    }
}
