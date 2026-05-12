using HarmonyLib;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Runtime.CompilerServices; 

namespace Rephysicalized.Content.ModDb
{
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationGenerators
    {
        private static void Postfix()
        {
            Debug.Log("[Rephysicalized] RonivansIntegrationGenerators Postfix called");
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            Debug.Log("[Rephysicalized] RonivansIntegrationGenerators ApplyPatches called");
            PatchGenerator("CustomGasGeneratorConfig", nameof(CustomGasGeneratorConfig_OutputPatch));
            PatchGenerator("CustomSolidGeneratorConfig", nameof(CustomSolidGeneratorConfig_OutputPatch));
            PatchGenerator("CustomDieselGeneratorConfig", nameof(CustomDieselGeneratorConfig_OutputPatch));
        }

        private static void PatchGenerator(string className, string postfixMethod)
        {
            try
            {
             
                var targetType = Type.GetType("RonivansLegacy_ChemicalProcessing.Content.Defs.Buildings.CustomGenerators." + className + ", RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("RonivansLegacy_ChemicalProcessing.Content.Defs.Buildings.CustomGenerators." + className);
                Debug.Log("[Rephysicalized] TargetType for " + className + ": " + (targetType?.FullName ?? "null"));
                if (targetType == null)
                {
                    return;
                }
                var method = AccessTools.Method(targetType, "DoPostConfigureComplete", new Type[] { typeof(GameObject) });
              
                if (method == null)
                {   
                    return;
                }
                var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans." + className);
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationGenerators), postfixMethod));
                harmony.Patch(method, postfix: postfix);
  
            }
            catch (Exception e)
            { 
            }
        }

        private static void CustomGasGeneratorConfig_OutputPatch(GameObject go)
        {
            Debug.Log("[Rephysicalized] CustomGasGeneratorConfig_OutputPatch called");
            if (go == null) return;
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen == null) return;

            var formula = energyGen.formula;
            var newInputs = new List<EnergyGenerator.InputItem>(formula.inputs);
            newInputs.Add(new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.02f, 1f));
            formula.inputs = newInputs.ToArray();
            
                  var newOutputs = new List<EnergyGenerator.OutputItem>(formula.outputs);
            newOutputs.Add(new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.02f, true, new CellOffset(0, 0), 348.15f));
            formula.outputs = newOutputs.ToArray();
            
            energyGen.formula = formula;

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                storage.storageFilters.Add(ModTags.OxidizerGas);
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.storage = storage;

        
            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = ModElementRegistration.AshByproduct.Tag;
            tilemaker.emitMass = 1000f;
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }

        private static void CustomSolidGeneratorConfig_OutputPatch(GameObject go)
        {
            Debug.Log("[Rephysicalized] CustomSolidGeneratorConfig_OutputPatch called");
            if (go == null) return;
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen == null) return;

            var formula = energyGen.formula;
            var newInputs = new List<EnergyGenerator.InputItem>(formula.inputs);
            newInputs.Add(new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.05f, 1f));
            formula.inputs = newInputs.ToArray();

            var newOutputs = new List<EnergyGenerator.OutputItem>(formula.outputs);
            newOutputs.Add(new EnergyGenerator.OutputItem(ModElementRegistration.AshByproduct, 0.79f, true, new CellOffset(0, 0), 348.15f));
            formula.outputs = newOutputs.ToArray();

            energyGen.formula = formula;

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                storage.storageFilters.Add(ModTags.OxidizerGas);
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.storage = storage;



            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = ModElementRegistration.AshByproduct.Tag;
            tilemaker.emitMass = 1000f;
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }

        private static void CustomDieselGeneratorConfig_OutputPatch(GameObject go)
        {
            Debug.Log("[Rephysicalized] CustomDieselGeneratorConfig_OutputPatch called");
            if (go == null) return;
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen == null) return;

            var formula = energyGen.formula;
            var newInputs = new List<EnergyGenerator.InputItem>(formula.inputs);
            newInputs.Add(new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.1f, 1f));
            formula.inputs = newInputs.ToArray();

            var newOutputs = new List<EnergyGenerator.OutputItem>(formula.outputs);
            newOutputs.Add(new EnergyGenerator.OutputItem(ModElementRegistration.CrudByproduct, 0.85f/3f, true, new CellOffset(0, 0), 348.15f));
            formula.outputs = newOutputs.ToArray();

            energyGen.formula = formula;

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                storage.storageFilters.Add(ModTags.OxidizerGas);
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.storage = storage;

            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = ModElementRegistration.CrudByproduct.Tag;
            tilemaker.emitMass = 1000f;
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }
    }
}
