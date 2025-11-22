using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized
{
    //RUST DEOXIDIZER
    // The Assumption is that Oni Rust is Fe2O3+H2O. Only a quater of salt actually breaks down into chlorine, other 3/4s just get morphed into another version of salt that we see as sand
    [HarmonyPatch(typeof(RustDeoxidizerConfig), "ConfigureBuildingTemplate")]
    public static class RustDeoxidizerConfig_Patch
    {
        // The method to run after the original (Postfix)
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            ElementConverter elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter != null)
            {
                // Adjust inputs
                elementConverter.consumedElements = new ElementConverter.ConsumedElement[]
                {
                new ElementConverter.ConsumedElement(new Tag("Rust"), 0.750f),
                new ElementConverter.ConsumedElement(new Tag("Salt"), 0.250f)
                };

                // Adjust outputs
                elementConverter.outputElements = new ElementConverter.OutputElement[]
                {
                new ElementConverter.OutputElement(0.300f, SimHashes.Oxygen, 348.15f, outputElementOffsety: 1f),
                new ElementConverter.OutputElement(0.03f, SimHashes.ChlorineGas, 348.15f, outputElementOffsety: 1f),
                new ElementConverter.OutputElement(0.220f, SimHashes.Sand, 348.15f, storeOutput: true, outputElementOffsety: 1f),
                   new ElementConverter.OutputElement(0.450f, SimHashes.IronOre, 348.15f, storeOutput: true, outputElementOffsety: 1f)
                };
                ElementDropper elementDropper = go.AddComponent<ElementDropper>();
                elementDropper.emitMass = 24f;
                elementDropper.emitTag = SimHashes.Sand.CreateTag();
                elementDropper.emitOffset = new Vector3(1.0f, 1f, 0.0f);
            }
        }
    }


    // Electrolyzer

    [HarmonyPatch(typeof(ElectrolyzerConfig), nameof(ElectrolyzerConfig.CreateBuildingDef))]
    public static class ElectrolyzerConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            __result.EnergyConsumptionWhenActive *= 10f;
        }
    }


    [HarmonyPatch(typeof(MineralDeoxidizerConfig), "ConfigureBuildingTemplate")]
    public static class MineralDeoxidizerConfig_SandOutputPatch
    {
        private const float Sand_PER_LOAD = 10f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure ElementConverter exists
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter != null)
            {
                var outputs = elementConverter.outputElements?.ToList() ?? new System.Collections.Generic.List<ElementConverter.OutputElement>();
                // Remove any existing Sand output
                outputs.RemoveAll(o => o.elementHash == SimHashes.Sand);
                outputs.Add(new ElementConverter.OutputElement(
                    0.05f,      //  (AirFilter)
                    SimHashes.Sand,  //  
                    0.0f,            // 
                    storeOutput: true,
                    diseaseWeight: 0.25f
                ));
                elementConverter.outputElements = outputs.ToArray();
            }
            // Ensure Storage uses StandardSealedStorage modifiers (like AirFilter)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            }
            // Ensure ElementDropper exists and is set up to drop Sand in 10kg loads, like AirFilter
            var elementDropper = go.GetComponent<ElementDropper>();
            if (elementDropper == null)
            {
                elementDropper = go.AddComponent<ElementDropper>();
            }
            elementDropper.emitTag = new Tag("Sand");
            elementDropper.emitMass = 20f;
            elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
        }
    }
    [HarmonyPatch(typeof(SublimationStationConfig), "ConfigureBuildingTemplate")]
    //Sublimation
    public static class SublimationStationConfig_SandOutputPatch
    {
        private const float Sand_PER_LOAD = 30f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure ElementConverter exists, or add if missing
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null)
            {
                elementConverter = go.AddComponent<ElementConverter>();
                elementConverter.consumedElements = new ElementConverter.ConsumedElement[0];
            }
            var outputs = elementConverter.outputElements?.ToList() ?? new System.Collections.Generic.List<ElementConverter.OutputElement>();
            // Remove any existing Sand output
            outputs.RemoveAll(o => o.elementHash == SimHashes.Sand);
            // Always add/overwrite with a Sand output (AirFilter values)
            outputs.Add(new ElementConverter.OutputElement(
                0.34f,      // massGenerationRate (AirFilter)
                SimHashes.Sand,  // output element
                0.0f,            // temperatureOperation
                storeOutput: true,
                diseaseWeight: 0.25f
            ));
            elementConverter.outputElements = outputs.ToArray();

            // Ensure Storage uses StandardSealedStorage modifiers (like AirFilter)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            }
            // Ensure ElementDropper exists and is set up to drop Sand in 30kg loads
            var elementDropper = go.AddComponent<ElementDropper>();

            elementDropper.emitTag = new Tag("Sand");
            elementDropper.emitMass = 40f;
            elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
        }
    }
}
