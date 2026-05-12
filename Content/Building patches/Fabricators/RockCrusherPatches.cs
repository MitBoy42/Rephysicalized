using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using STRINGS;
using Rephysicalized.ModElements;

namespace Rephysicalized.Content.Building_patches
{

    [HarmonyPatch]
    public static class RockCrusherElementDropperPatch
    {
        [HarmonyPatch(typeof(RockCrusherConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            if (!Config.Instance.RephysicalizedMetalOre) return;
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.storeProduced = true;

            var dropper = go.AddComponent<WorldElementDropper>();

            dropper.DropSolids = true;
            dropper.DropLiquids = true;
            dropper.DropGases = true;
            dropper.TargetStorage = fabricator.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            cmp.storages = [fabricator.outStorage];

        }
        [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
        [HarmonyPostfix]
        public static void DropAfterRecipe(ComplexFabricator __instance)
        {
            if (__instance.PrefabID().Name == RockCrusherConfig.ID)
            {
                if (!Config.Instance.RephysicalizedMetalOre) return;
                var dropper = __instance.GetComponent<WorldElementDropper>();
                var storage = __instance.outStorage;
                if (dropper != null && storage != null)
                {
                    storage.DropAll();
                }
            }
        }

    }

    // Patch the method that creates the RockCrusher recipes
    [HarmonyPatch(typeof(RockCrusherConfig), nameof(RockCrusherConfig.ConfigureBuildingTemplate))]
    internal static class RockCrusherRecipeAdjustments
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var crm = ComplexRecipeManager.Get();
            if (crm == null) return;

            // Common tags
            Tag katairiteTag = ElementLoader.FindElementByHash(SimHashes.Katairite).tag;
            Tag sandTag = SimHashes.Sand.CreateTag();
            Tag saltTag = SimHashes.Salt.CreateTag();
            Tag tableSaltTag = TableSaltConfig.ID.ToTag();
            Tag garbageElectrobankTag = TagManager.Create("GarbageElectrobank");

            // 1) GarbageElectrobank -> Katairite recipe adjustment (kept close to original)
            var garbInputs = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(garbageElectrobankTag, 1f) };
            var garbOutputsOriginal = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(katairiteTag, 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature) };
            string garbRecipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, garbInputs, garbOutputsOriginal);
            var garbRecipe = crm.GetRecipe(garbRecipeId);
            if (garbRecipe != null)
            {
                var results = garbRecipe.results;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].material == katairiteTag)
                        results[i].amount = 10f;
                }

                if (!results.Any(r => r.material == sandTag))
                {
                    var newResults = new ComplexRecipe.RecipeElement[results.Length + 1];
                    Array.Copy(results, newResults, results.Length);
                    newResults[newResults.Length - 1] = new ComplexRecipe.RecipeElement(sandTag, 10f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature);
                    garbRecipe.results = newResults;
                }
            }

            // 2) Salt -> TableSalt recipe: multiply TableSalt output by 4 (kept close to original)
            float num2 = 5e-05f; // as in RockCrusherConfig
            var saltInputs = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(saltTag, 100f) };
            var saltOutputsOriginal = new ComplexRecipe.RecipeElement[] {
                new ComplexRecipe.RecipeElement(tableSaltTag, 100f * num2, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                new ComplexRecipe.RecipeElement(sandTag, 100f * (1f - num2), ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
            string saltRecipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, saltInputs, saltOutputsOriginal);
            var saltRecipe = crm.GetRecipe(saltRecipeId);
            if (saltRecipe != null)
            {
                var results = saltRecipe.results;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].material == tableSaltTag)
                    {
                        results[i].amount *= 4f;
                        break;
                    }
                }
            }
            if (!Config.Instance.RephysicalizedMetalOre) return;
            // 3) Metal ore rewrites using RockCrusher's recipe id scheme
            var metalRewrites = new (SimHashes ingredient, Tag[] tags, float[] amts)[]
            {
               (SimHashes.Cinnabar,     new[]{ SimHashes.Mercury.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 42.5f, 50f, 12.5f }),
                (SimHashes.IronOre,      new[]{ SimHashes.Iron.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 33f, 67f }),
                (SimHashes.Cuprite,      new[]{ SimHashes.Copper.CreateTag(), SimHashes.ContaminatedOxygen.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 40f, 10f, 50f }),
                (SimHashes.Cobaltite,    new[]{ SimHashes.Cobalt.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 35f, 50f, 15f }),
                (SimHashes.NickelOre,    new[]{ SimHashes.Nickel.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 37.5f, 50f , 12.5f}),
                (SimHashes.Wolframite,   new[]{ SimHashes.Tungsten.CreateTag(), SimHashes.Rust.CreateTag() , SimHashes.Sand.CreateTag() }, new[]{ 30f, 20f, 50f }),
                (SimHashes.AluminumOre,  new[]{ SimHashes.Aluminum.CreateTag(), SimHashes.ContaminatedOxygen.CreateTag(), SimHashes.DirtyWater.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 30f, 10f, 10f, 50f }),
                (SimHashes.GoldAmalgam,  new[]{ SimHashes.Gold.CreateTag(), SimHashes.Mercury.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 35f, 15f, 50f })
            };

            foreach (var rw in metalRewrites)
            {
                var elem = ElementLoader.FindElementByHash(rw.ingredient);
                if (elem == null) continue;
                var low = elem.highTempTransition?.lowTempTransition;
                if (low == null || low == elem) continue;

                var inputsMeta = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(elem.tag, 100f) };
                var outputsMeta = new ComplexRecipe.RecipeElement[] {
                    new ComplexRecipe.RecipeElement(low.tag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                    new ComplexRecipe.RecipeElement(sandTag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                };
                string recipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, inputsMeta, outputsMeta);
                var recipe = crm.GetRecipe(recipeId);
                if (recipe == null) continue;

                var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
                if (recipe.results != null && recipe.results.Length > 0) tempOp = recipe.results[0].temperatureOperation;

                var newResults = new ComplexRecipe.RecipeElement[rw.tags.Length];
                for (int i = 0; i < rw.tags.Length; i++)
                    newResults[i] = new ComplexRecipe.RecipeElement(rw.tags[i], rw.amts[i], tempOp, true);

                recipe.results = newResults;
            }
        }
    }

    [HarmonyPatch(typeof(RockCrusherConfig), nameof(RockCrusherConfig.ConfigureBuildingTemplate))]
    public static class RockCrusher_IceToSnowRecipe_Patch
    {
        private static bool _recipeAdded;
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (_recipeAdded) return;
            var mgr = ComplexRecipeManager.Get();
            var rockCrusherTag = TagManager.Create(RockCrusherConfig.ID);
            var inputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Ice.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature) };
            var outputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Snow.CreateTag(), 100f) };
            string recipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, inputs, outputs);
            if (mgr.recipes != null && mgr.recipes.Any(r => r.id == recipeId)) { _recipeAdded = true; return; }
            var recipe = new ComplexRecipe(recipeId, inputs, outputs)
            {
                time = 40f,
                description = string.Format(BUILDINGS.PREFABS.ROCKCRUSHER.RECIPE_DESCRIPTION, SimHashes.Ice.CreateTag().ProperName(), SimHashes.Snow.CreateTag().ProperName()),
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                fabricators = new List<Tag> { rockCrusherTag },
                sortOrder = 0
            };
            _recipeAdded = true;
        }
    }
}
