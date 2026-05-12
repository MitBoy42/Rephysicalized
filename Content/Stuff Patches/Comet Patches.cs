using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

using PLibOptions = PeterHan.PLib.Options;
using AccessTools = HarmonyLib.AccessTools;

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
                var pe = __instance.GetComponent<PrimaryElement>();
                if (pe != null)
                    pe.Mass *= mult;
                AccessTools.Field(typeof(Comet), "explosionMass").SetValue(__instance, (float)AccessTools.Field(typeof(Comet), "explosionMass").GetValue(__instance) * mult);
                AccessTools.Field(typeof(Comet), "addTileMass").SetValue(__instance, (float)AccessTools.Field(typeof(Comet), "addTileMass").GetValue(__instance) * mult);
            }
        }
    }
}
