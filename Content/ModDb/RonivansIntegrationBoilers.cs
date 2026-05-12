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

namespace Rephysicalized.Content.ModDb
{
    // Ronivans integrations - BasicOilRefinery dynamic patch + Chemical_AdvancedKiln CrudByproduct spawn (all recipes, like Kiln ash)
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationBoilers
    {
        private static void Postfix()
        {
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            PatchBoiler("Chemical_Coal_BoilerConfig", nameof(Chemical_Coal_BoilerConfig_OutputPatch));
            PatchBoiler("Chemical_Wooden_BoilerConfig", nameof(Chemical_Wooden_BoilerConfig_OutputPatch));
            PatchBoiler("Chemical_Liquid_BoilerConfig", nameof(Chemical_Liquid_BoilerConfig_OutputPatch));
            PatchBoiler("Chemical_Gas_BoilerConfig", nameof(Chemical_Gas_BoilerConfig_OutputPatch));
        }

        private static void PatchBoiler(string className, string postfixMethod)
        {
            try
            {
                var targetType = Type.GetType("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings." + className + ", RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings." + className);
                if (targetType == null)
                {
                    Debug.Log("[Rephysicalized] " + className + " type not found; skipping patch.");
                    return;
                }
                var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                          ?? AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                if (method == null)
                {
                    Debug.Log("[Rephysicalized] " + className + ".ConfigureBuildingTemplate method not found; skipping.");
                    return;
                }
                var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans." + className);
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationBoilers), postfixMethod));
                harmony.Patch(method, postfix: postfix);
            }
            catch (Exception e)
            {
            }
        }



        private static void Chemical_Coal_BoilerConfig_OutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            outputs.Add(new ElementConverter.OutputElement(
                1.7888f,
                ModElementRegistration.AshByproduct,
                348.15f,
                storeOutput: true,
                outputElementOffsety: 1f

            ));
            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.1f
            ));
            elementConverter.outputElements = outputs.ToArray();
            elementConverter.consumedElements = inputs.ToArray();

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

 

            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = new Tag("AshByproduct");
            tilemaker.emitMass = 120f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }

        private static void Chemical_Wooden_BoilerConfig_OutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            outputs.Add(new ElementConverter.OutputElement(
                1.3f,
                ModElementRegistration.AshByproduct,
                348.15f,
                storeOutput: true,
                outputElementOffsety: 1f

            ));
            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.1f
            ));
            elementConverter.outputElements = outputs.ToArray();
            elementConverter.consumedElements = inputs.ToArray();

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

    
            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = new Tag("AshByproduct");
            tilemaker.emitMass = 120f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }

        private static void Chemical_Gas_BoilerConfig_OutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.1f
            ));
            elementConverter.outputElements = outputs.ToArray();
            elementConverter.consumedElements = inputs.ToArray();

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

    
        }

        private static void Chemical_Liquid_BoilerConfig_OutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            outputs.Add(new ElementConverter.OutputElement(
                0.47083f,
                ModElementRegistration.CrudByproduct,
                348.15f,
                storeOutput: true,
                outputElementOffsety: 1f

            ));
            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.1f
            ));
            elementConverter.outputElements = outputs.ToArray();
            elementConverter.consumedElements = inputs.ToArray();

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;



            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = new Tag("CrudByproduct");
            tilemaker.emitMass = 120f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }


    }
}
