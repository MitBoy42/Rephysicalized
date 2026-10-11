using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static STRINGS.ITEMS.INDUSTRIAL_PRODUCTS;

namespace Rephysicalized.Content.Building_patches.Fabricators
{
    [HarmonyPatch(typeof(ClothingFabricatorConfig), "ConfigureRecipes")]
    internal static class ClothingFabricatorConfig_AddBasicFabricFromFeathers
    {
        public static void Postfix()
        {

            var ingredients = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement("FeatherFabric", 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
         new ComplexRecipe.RecipeElement(new Tag[3] { SimHashes.NaturalResin.CreateTag(), SimHashes.Latex.CreateTag(), SimHashes.Resin.CreateTag() } , 1f),
            new ComplexRecipe.RecipeElement("PlantFiber", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
        };

            var results = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement(BasicFabricConfig.ID, 4f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
            };

            string fabId = ClothingFabricatorConfig.ID; // "ClothingFabricator"
            string recipeId = ComplexRecipeManager.MakeRecipeID(fabId, ingredients, results);

            // Avoid duplicating if something already registered same recipe
            if (ComplexRecipeManager.Get().GetRecipe(recipeId) != null)
                return;

            var recipe = new ComplexRecipe(recipeId, ingredients, results)
            {
                time = 20f,
                description = STRINGS.BUILDINGS.ClOTHING_FABRICATOR.FIBER,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { fabId },
                sortOrder = 100
            };


        }
    }
}
