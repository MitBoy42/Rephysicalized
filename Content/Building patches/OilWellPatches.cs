using System;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Patches
{
    // Minimal patch: only change the mass value of the crude oil OutputElement to 2f
    [HarmonyPatch(typeof(OilWellCapConfig), nameof(OilWellCapConfig.ConfigureBuildingTemplate))]
    public static class OilWellCapConfig_ConfigureBuildingTemplate_MinimalPatch
    {
        // Signature: (GameObject go, Tag prefab_tag)
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
         
                var converter = go.GetComponent<ElementConverter>();
                if (converter?.outputElements == null)
                    return;

                var outputs = converter.outputElements;

                for (int i = 0; i < outputs.Length; i++)
                {
                    var e = outputs[i];
                    if (e.elementHash == SimHashes.CrudeOil)
                    {
                        e.massGenerationRate = 2f;

                        outputs[i] = e;
                    }
                }

                converter.outputElements = outputs;
            }
           
        }
    }
