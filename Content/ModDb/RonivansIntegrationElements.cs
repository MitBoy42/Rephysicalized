using HarmonyLib;
using Klei;
using Klei.AI;
using Rephysicalized.Content.System_Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationElements
    {
        private static void Postfix()
        {
            RegisterRonivansElements();
        }

        private static void RegisterRonivansElements()
        {
            // Check if Ronivans mod is present
            var modElementsType = AccessTools.TypeByName("RonivansLegacy_ChemicalProcessing.Content.ModDb.ModElements");
            if (modElementsType == null)
            {
           
                return;
            }


            var ronivanElementNames = new Dictionary<string, (float storageMult, float wireMult)>
            {
                ["SolidZinc"] = (1f, 1.2f),
                ["SolidSilver"] = (1.1f, 1.4f),
                   ["AurichalciteOre"] = (1f, 1.1f),
                ["ArgentiteOre"] = (1f, 1.2f),
                ["SolidBrass"] = (1f, 1f),
                ["PhosphorBronze"] = (1f, 1f),
                ["Plasteel"] = (1.4f, 0.2f),
                ["AIO_Permendur_Solid"] = (1.2f, 1f),
                ["AIO_Invar_Solid"] = (1.2f, 1f),
                  ["AIO_StainlessSteel_Solid"] = (1.3f, 1f),
                    ["AIO_FerroChrome_Solid"] = (0.6f, 0.2f),
                      ["AIO_Chromium_Solid"] = (0.6f, 0.8f),
                ["SolidFiberGlass"] = (1.2f, 1f),
                ["CarbonFiber"] = (2f, 0.2f),

             
            };

            int registered = 0;
            foreach (var kvp in ronivanElementNames)
            {
                string elemName = kvp.Key;
                var (storageMult, wireMult) = kvp.Value;
                var elem = ElementLoader.GetElement(new Tag(elemName));
                if (elem == null) 
                {
                    continue;
                }

                var hash = elem.id;

                if (!StorageCapacitySettings.Multipliers.ContainsKey(hash))
                {
                    StorageCapacitySettings.Multipliers[hash] = storageMult;
                  //  UnityEngine.Debug.Log($"[Rephysicalized] Added StorageCapacity for {elemName} ({hash}): {storageMult}x");
                }

                if (!WireCapacitySettings.Multipliers.ContainsKey(hash))
                {
                    WireCapacitySettings.Multipliers[hash] = wireMult;
                  //  UnityEngine.Debug.Log($"[Rephysicalized] Added WireCapacity for {elemName} ({hash}): {wireMult}x");
                }

                registered++;
            }

        }
    }
}
