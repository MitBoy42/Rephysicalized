using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using STRINGS;

namespace Rephysicalized.Content.Building_patches
{
    // Patch the method that creates the RockCrusher recipes
    [HarmonyPatch(typeof(RockCrusherConfig), nameof(RockCrusherConfig.ConfigureBuildingTemplate))]
    internal static class RockCrusherRecipeAdjustments
    {
        // Postfix runs after recipes are created
        public static void Postfix(GameObject go, Tag prefab_tag)
        {

            var crm = ComplexRecipeManager.Get();

            // Common tags
            Tag katairiteTag = ElementLoader.FindElementByHash(SimHashes.Katairite).tag;
            Tag sandTag = SimHashes.Sand.CreateTag();
            Tag saltTag = SimHashes.Salt.CreateTag();
            Tag tableSaltTag = TableSaltConfig.ID.ToTag();
            Tag garbageElectrobankTag = TagManager.Create("GarbageElectrobank");

            // 1) GarbageElectrobank -> Katairite recipe adjustment
            // Build the same ID RockCrusher uses during creation (see decompiled RockCrusherConfig)
            var garbInputs = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(garbageElectrobankTag, 1f)
            };
            var garbOutputsOriginal = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(katairiteTag, 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
            string garbRecipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, garbInputs, garbOutputsOriginal);

            var garbRecipe = crm.GetRecipe(garbRecipeId);
            if (garbRecipe != null)
            {
                bool changed = false;

                // Adjust Katairite result amount to 10f
                var results = garbRecipe.results; // array
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].material == katairiteTag && Math.Abs(results[i].amount - 10f) > 0.0001f)
                    {
                        results[i].amount = 10f;
                        changed = true;
                    }
                }

                // Ensure Sand result is present with 10f
                int sandIndex = -1;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].material == sandTag)
                    {
                        sandIndex = i;
                        break;
                    }
                }

                if (sandIndex >= 0)
                {
                    if (Math.Abs(results[sandIndex].amount - 10f) > 0.0001f)
                    {
                        results[sandIndex].amount = 10f;
                        changed = true;
                    }
                }
                else
                {
                    // Add Sand by creating a new array
                    var newResults = new ComplexRecipe.RecipeElement[results.Length + 1];
                    Array.Copy(results, newResults, results.Length);
                    newResults[newResults.Length - 1] = new ComplexRecipe.RecipeElement(
                        sandTag,
                        10f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
                    );
                    garbRecipe.results = newResults;
                    changed = true;
                }


            }

            // 2) Salt -> TableSalt recipe: multiply TableSalt output by 4
            float num2 = 5e-05f; // as in RockCrusherConfig
            var saltInputs = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(saltTag, 100f)
            };
            var saltOutputsOriginal = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(tableSaltTag, 100f * num2, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                    new ComplexRecipe.RecipeElement(sandTag, 100f * (1f - num2), ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
            string saltRecipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, saltInputs, saltOutputsOriginal);

            var saltRecipe = crm.GetRecipe(saltRecipeId);
            if (saltRecipe != null)
            {
                var results = saltRecipe.results; // array
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].material == tableSaltTag)
                    {
                        float newAmt = results[i].amount * 4f;
                        if (Math.Abs(newAmt - results[i].amount) > 0.0001f)
                        {
                            results[i].amount = newAmt;
                        }
                        break;
                    }
                }
            }

        }

    }


    [HarmonyPatch(typeof(RockCrusherConfig), nameof(RockCrusherConfig.ConfigureBuildingTemplate))]
    public static class RockCrusher_IceToSnowRecipe_Patch
    {
        private static bool _recipeAdded;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure we only add once (ConfigureBuildingTemplate can be invoked multiple times).
            if (_recipeAdded)
                return;

            // Locate the existing Sand recipe on the RockCrusher to clone common properties.
            ComplexRecipe sandRecipe = FindRockCrusherSandRecipe();

            // Build our input/output
            var inputs = new[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Ice.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
            var outputs = new[]
            {
                // Match Sand recipe style: a single result of 100f; do not force a temperature unless needed
                new ComplexRecipe.RecipeElement(SimHashes.Snow.CreateTag(), 100f)
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, inputs, outputs);

            // If by any chance it's already registered, do nothing.
            if (ComplexRecipeManager.Get().recipes != null && ComplexRecipeManager.Get().recipes.Any(r => r.id == recipeId))
            {
                _recipeAdded = true;
                return;
            }

            var recipe = new ComplexRecipe(recipeId, inputs, outputs);

       
                // Fallbacks if Sand recipe isn't found for some reason
                recipe.time = 40f;
                recipe.description = string.Format(BUILDINGS.PREFABS.ROCKCRUSHER.RECIPE_DESCRIPTION, SimHashes.Ice.CreateTag().ProperName(), SimHashes.Snow.CreateTag().ProperName());
                recipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult;
                recipe.fabricators = new List<Tag> { TagManager.Create(RockCrusherConfig.ID) };
                recipe.sortOrder = 0;
            

            _recipeAdded = true;
        }

        private static ComplexRecipe FindRockCrusherSandRecipe()
        {
            var mgr = ComplexRecipeManager.Get();
            if (mgr?.recipes == null)
                return null;

            Tag rockCrusherTag = TagManager.Create(RockCrusherConfig.ID);
            Tag sandTag = SimHashes.Sand.CreateTag();

            // Find the recipe added in RockCrusherConfig that produces exactly 100f Sand
            // and is assigned to the RockCrusher fabricator.
            return mgr.recipes.FirstOrDefault(r =>
                r != null
                && r.fabricators != null
                && r.fabricators.Contains(rockCrusherTag)
                && r.results != null
                && r.results.Length == 1
                && r.results[0].material == sandTag
                && Mathf.Approximately(r.results[0].amount, 100f));
        }
    }
}
