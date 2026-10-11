using HarmonyLib;
using Klei.AI;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace Rephysicalized.Content.ModDb
{
    // Ronivans integrations - BasicOilRefinery dynamic patch + Chemical_AdvancedKiln CrudByproduct spawn (all recipes, like Kiln ash)
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationMetallurgy
    {
        private static void Postfix()
        {
            // call shared apply so patching can be triggered either via Harmony or manually
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            Debug.Log("[Rephysicalized] RonivansIntegration.ApplyPatches running");
            // BasicOilRefinery ConfigureBuildingTemplate postfix
            try
            {
                var targetType = Type.GetType("Metallurgy.Buildings.Metallurgy_BasicOilRefineryConfig, RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("Metallurgy.Buildings.Metallurgy_BasicOilRefineryConfig");
                if (targetType == null)
                {
                    Debug.Log("[Rephysicalized] BasicOilRefinery type not found; skipping patch.");
                }
                else
                {
                    var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                              ?? AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                    if (method == null)
                        Debug.Log("[Rephysicalized] BasicOilRefinery.ConfigureBuildingTemplate method not found; skipping.");
                    else
                    {
                        var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.BasicOilRefinery");
                        var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationMetallurgy), nameof(BasicOilRefinery_NaphthaOutputPatch)));
                        harmony.Patch(method, postfix: postfix);
                        Debug.Log("[Rephysicalized] Patched BasicOilRefinery ConfigureBuildingTemplate.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Rephysicalized] Exception patching BasicOilRefinery: {e.Message}");
            }

            try
            {
                var targetType = Type.GetType("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_GlassFoundryConfig, RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_GlassFoundryConfig");
                if (targetType == null)
                {
                    Debug.Log("[Rephysicalized] Glass Foundry type not found; skipping patch.");
                }
                else
                {
                    var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                              ?? AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                    if (method == null)
                        Debug.Log("[Rephysicalized] GlassFoundry.ConfigureBuildingTemplate method not found; skipping patch.");
                    else
                    {
                        var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.GlassFoundry");
                        var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationMetallurgy), nameof(GlassFoundry_ConfigureBuildingTemplate_Patch)));
                        harmony.Patch(method, postfix: postfix);
                        Debug.Log("[Rephysicalized] Patched Glass Foundry ConfigureBuildingTemplate.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Rephysicalized] Exception patching Glass Foundry: {e.Message}");
            }
 }



        private static void GlassFoundry_ConfigureBuildingTemplate_Patch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            var fabricator = go.GetComponent<ComplexFabricator>();
            if (fabricator == null) return;

            var dropper = go.AddOrGet<WorldElementDropper>();
            dropper.DropSolids = true;
            dropper.DropLiquids = false;
            dropper.DropGases = false;
            dropper.TargetStorage = fabricator.outStorage;

            GlassFoundry_AddSiliconExhaust(go);
            GlassFoundry_AddSiliconRecipe();
        }

        private static void GlassFoundry_AddSiliconExhaust(GameObject go)
        {
            var exhaustType = AccessTools.TypeByName("RonivansLegacy_ChemicalProcessing.Content.Scripts.PipedOptionalExhaust");
            var dispenserType = AccessTools.TypeByName("PipedConduitDispenser");
            var dispenser = dispenserType == null ? null : go.GetComponent(dispenserType);
            if (exhaustType == null || dispenser == null)
                return;

            var exhaust = go.AddComponent(exhaustType);
            AccessTools.Field(exhaustType, "dispenser")?.SetValue(exhaust, dispenser);
            AccessTools.Field(exhaustType, "elementTag")?.SetValue(exhaust, ModElementRegistration.MoltenSilicon.Tag);
            AccessTools.Field(exhaustType, "capacity")?.SetValue(exhaust, 100f);
            AccessTools.Field(exhaustType, "emissionRate")?.SetValue(exhaust, 50f);
        }

        private static void GlassFoundry_AddSiliconRecipe()
        {
            var inputs = new ComplexRecipe.RecipeElement[2]
            {
                new ComplexRecipe.RecipeElement(new Tag[4] { SimHashes.Granite.CreateTag(), SimHashes.Basalt.CreateTag(), SimHashes.IgneousRock.CreateTag(), SimHashes.CrushedRock.CreateTag() }, 100f),
                new ComplexRecipe.RecipeElement(new Tag[3] { SimHashes.Carbon.CreateTag(), SimHashes.WoodLog.CreateTag(), SimHashes.FabricatedWood.CreateTag() }, 50f)
            };
            var outputs = new ComplexRecipe.RecipeElement[2]
            {
                new ComplexRecipe.RecipeElement(ModElementRegistration.MoltenSilicon.Tag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.Melted),
                new ComplexRecipe.RecipeElement(SimHashes.CarbonDioxide.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, true),
          
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID("Chemical_GlassFoundry", inputs, outputs);
            if (ComplexRecipeManager.Get().GetRecipe(recipeID) != null)
                return;

            new ComplexRecipe(recipeID, inputs, outputs)
            {
                time = 60f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = STRINGS.BUILDINGS.GLASSFORGE.SILICON,
                fabricators = new List<Tag> { TagManager.Create("Chemical_GlassFoundry") }
            };
        }

        private static void BasicOilRefinery_NaphthaOutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            outputs.Add(new ElementConverter.OutputElement(
                0.085f,
                ModElementRegistration.AshByproduct,
                348.15f,
                storeOutput: true,
                outputElementOffsety: 1f
            ));

            outputs.Add(new ElementConverter.OutputElement(
                2.41f,
                SimHashes.Naphtha,
                348.15f,
                storeOutput: false,
                outputElementOffsety: 1f,
                outputElementOffsetx: 1f
            ));
            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.035f
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

        private static SimHashes? TagToSimHash(Tag t)
        {
            try
            {
                var name = t.ToString() ?? string.Empty;
                foreach (SimHashes sh in Enum.GetValues(typeof(SimHashes)))
                {
                    if (sh.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                        return sh;
                }
            }
            catch { }
            return null;
        }


    }

    [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
    public static class RonivansGlassFoundry_CarbonDioxideTemperature
    {
        public static void Postfix(ComplexFabricator __instance, List<GameObject> __result)
        {
            if (__instance.PrefabID().Name != "Chemical_GlassFoundry")
                return;

            bool hasCarbonDioxide = false;
            foreach (var product in __result)
            {
                var primaryElement = product.GetComponent<PrimaryElement>();
                if (primaryElement != null && primaryElement.Element.id == SimHashes.CarbonDioxide)
                {
                    primaryElement.Temperature = 1873f;
                    hasCarbonDioxide = true;
                }
            }

            if (hasCarbonDioxide && __instance.outStorage != null)
            {
                var carbonDioxideTag = SimHashes.CarbonDioxide.CreateTag();
                var amount = __instance.outStorage.GetAmountAvailable(carbonDioxideTag);
                if (amount > 0f)
                    __instance.outStorage.DropSome(carbonDioxideTag, amount, ventGas: true);
            }
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
    public static class ChemicalAdvancedKiln_SpawnCrud_Patch
    {
        public static void Prefix(ComplexFabricator __instance)
        {
            // Target Chemical_AdvancedKiln only (all recipes)
            if (__instance.PrefabID().ToString() != "Chemical_AdvancedKiln")
                return;

            float sumIngredients = FabricatorHelpers.GetSumIngredients(__instance);
            float sumResults = FabricatorHelpers.GetSumResults(__instance);
            float amount = sumIngredients - sumResults; // no extra subtraction

            if (amount <= 0) return;

            float temp = 353.15f;

            // Sample from input
            var storage = __instance.inStorage;
            var sampleItem = storage.items.FirstOrDefault();
            var pe = sampleItem?.GetComponent<PrimaryElement>();
            if (pe != null)
                temp = pe.Temperature;

            // Spawn pos like Kiln ash
            int cell = Grid.PosToCell(__instance.transform.position);
            int spawnCell = Grid.OffsetCell(cell, 1, 0);
            Vector3 pos = Grid.CellToPosCCC(spawnCell, Grid.SceneLayer.Ore);

            var elem = ElementLoader.FindElementByTag(ModElementRegistration.CrudByproduct.Tag);
            if (elem != null)
            {
                var go = elem.substance.SpawnResource(pos, amount, temp, byte.MaxValue, 0);
                if (go != null) go.SetActive(true);
            }
        }
    }



    [HarmonyPatch(typeof(ComplexRecipe))]

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
    public static class ComplexRecipe_GlassFoundryResult_Postfix
    {
        static void Postfix(ComplexRecipe __instance)
        {
            var inputs = __instance.ingredients ?? System.Array.Empty<ComplexRecipe.RecipeElement>();
            var results = __instance.results ?? System.Array.Empty<ComplexRecipe.RecipeElement>();

            if (inputs.Length == 1 && inputs[0].material == SimHashes.Sand.CreateTag() && inputs[0].amount == 300f &&
                results.Length >= 1 && results.Any(r => r.material == SimHashes.MoltenGlass.CreateTag()))
            {
                // Add Crud (solid, dropped)
                if (!results.Any(r => r.material == ModElementRegistration.CrudByproduct.Tag))
                {
                    __instance.results = results
                        .Concat(new[]
                        {
                        new ComplexRecipe.RecipeElement(
                            ModElementRegistration.CrudByproduct.Tag,
                            200f,
                            ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                        })
                        .ToArray();
                }
            }
        }


    }

  
}






