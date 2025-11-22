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
            try
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
                        // Minimal change: set only the mass value
                        e.massGenerationRate = 2f;

                        // If OutputElement is a struct, write back the modified copy
                        outputs[i] = e;
                        // If there are multiple crude outputs (unlikely), update them all
                    }
                }

                // Assign back (harmless; ensures updated array is visible)
                converter.outputElements = outputs;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Rephysicalized] OilWellCap crude oil output patch failed: {ex}");
            }
        }
    }
}