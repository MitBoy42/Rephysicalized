using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

using PLibOptions = PeterHan.PLib.Options;

namespace Rephysicalized.Content.Stuff_Patches
{
internal class Comet_Patches
    {
        [HarmonyPatch(typeof(Comet), nameof(Comet.RandomizeMassAndTemperature))]
        public static class Comet_RandomizeMassAndTemperature_Patch
        {
            public static void Postfix(Comet __instance)
            {
                float mult = Config.Instance.CometMassMult;

                // Oxyrock comets use a separate multiplier when RealBreathing is enabled (see Comet_OxyrockMassMult_Patch)
                if (Config.Instance.RealBreathing)
                {
                    var pe = __instance.GetComponent<PrimaryElement>();
                    if (pe != null && pe.ElementID == SimHashes.OxyRock)
                        return;
                }

                var pe2 = __instance.GetComponent<PrimaryElement>();
                if (pe2 != null)
                    pe2.Mass *= mult;
                __instance.explosionMass *= mult;
                __instance.addTileMass *= mult;
            }
        }

        [HarmonyPatch(typeof(Comet), nameof(Comet.RandomizeMassAndTemperature))]
        public static class Comet_OxyrockMassMult_Patch
        {
            public static void Postfix(Comet __instance)
            {
                if (!Config.Instance.RealBreathing)
                    return;

                var pe = __instance.GetComponent<PrimaryElement>();
                if (pe != null && pe.ElementID == SimHashes.OxyRock)
                {
                    float mult = 0.04f;
                    pe.Mass *= mult;
                    __instance.explosionMass *= mult;
                    __instance.addTileMass *= mult;
                }
            }
        }
    }
}
