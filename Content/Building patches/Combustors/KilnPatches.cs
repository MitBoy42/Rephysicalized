using HarmonyLib;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Fabricators
{

    [HarmonyPatch(typeof(KilnConfig), "ConfigureBuildingTemplate")]
    public static class KilnConfig_OxygeninputPatch
    {
        public const float OXYGEN_INPUT = 0.2f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Dedicated sealed storage for oxidizer gas used by the kiln
            var oxidizerStorage = go.AddComponent<Storage>();
            oxidizerStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            // Ambient gas consumer that fills the oxidizer storage
            var dualGasConsumer = go.AddComponent<DualGasElementConsumer>();
            dualGasConsumer.storage = oxidizerStorage;

            // Fuel consumption setup; uses same oxidizer storage/tag
            var fueledFabricator = go.AddComponent<FueledFabricator>();
            fueledFabricator.fuelTag = ModTags.OxidizerGas;
            fueledFabricator.START_FUEL_MASS = 0.1f;
            fueledFabricator.storage = oxidizerStorage;

            // Element converter configuration for byproduct output
            var elementConverter = go.AddOrGet<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[1]
            {
            new ElementConverter.ConsumedElement(ModTags.OxidizerGas, 0.045f)
            };
            elementConverter.outputElements = new ElementConverter.OutputElement[1]
            {
            new ElementConverter.OutputElement(0.060f, SimHashes.CarbonDioxide, 303.15f, outputElementOffsety: 1f)
            };
            elementConverter.SetStorage(oxidizerStorage);

            // Status component: explicitly point to the oxidizer storage and tag
            var status = go.AddOrGet<OxidizerLowStatus>();
            status.explicitStorage = oxidizerStorage;
            status.oxidizerTag = ModTags.OxidizerGas;

        }
    }

    public static class FabricatorHelpers
    {

        public static float GetSumIngredients(ComplexFabricator fabricator)
        {
            var recipe = fabricator.CurrentWorkingOrder;
            if (recipe != null && recipe.ingredients != null)
                return recipe.ingredients.Sum(i => i.amount);
            return 0f;
        }


        public static float GetSumResults(ComplexFabricator fabricator)
        {
            var recipe = fabricator.CurrentWorkingOrder;
            if (recipe != null && recipe.results != null)
                return recipe.results.Sum(r => r.amount);
            return 0f;
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "CompleteWorkingOrder")]
    public static class Kiln_SpawnAsh_Patch
    {
        public static void Prefix(ComplexFabricator __instance)
        {
            // Target Kiln only
            if (__instance.PrefabID().ToString() == KilnConfig.ID)
            {
                float sumIngredients = FabricatorHelpers.GetSumIngredients(__instance);
                float sumResults = FabricatorHelpers.GetSumResults(__instance);
                float amount = sumIngredients - sumResults - 0.6f; //Flat amount, simply calculated from Kiln CO2 - O2 conversion over 40 seconds
                float temp = 353.15f;

                // Best practice: spawn at output cell
                int cell = Grid.PosToCell(__instance.transform.position);
                int spawnCell = Grid.OffsetCell(cell, 1, 0);
                Vector3 pos = Grid.CellToPosCCC(spawnCell, Grid.SceneLayer.Ore);

                var elem = ElementLoader.FindElementByTag(ModElementRegistration.AshByproduct.Tag);
                if (elem != null)
                {
                    var go = elem.substance.SpawnResource(pos, amount, temp, byte.MaxValue, 0);
                    if (go != null) go.SetActive(true);
                }
            }
        }

    }

}
