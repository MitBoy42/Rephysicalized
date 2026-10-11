using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rephysicalized.Content.Building_patches.Fabricators
{
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class SludgePressRecipePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var fabricatorTag = TagManager.Create("SludgePress");

            var inputMucus = new ComplexRecipe.RecipeElement[]
            {

            new ComplexRecipe.RecipeElement(SimHashes.SolidMucus.CreateTag(), 150f),
            };

            var outputMucus = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement(
                SimHashes.SlimeMold.CreateTag(),
                45f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),
            new ComplexRecipe.RecipeElement(
                SimHashes.DirtyWater.CreateTag(),
                105f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),
           
            };

            string recipeIdMucus = ComplexRecipeManager.MakeRecipeID("SludgePress", inputMucus, outputMucus);

            var recipeMucus = new ComplexRecipe(recipeIdMucus, inputMucus, outputMucus)
            {
                time = 20f,
                description = STRINGS.BUILDINGS.SLUDGEPRESS.MUCUS,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Composite,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 21,

            };
            // Squid Ink recipe
            var inputInk = new ComplexRecipe.RecipeElement[]
            {

            new ComplexRecipe.RecipeElement(SimHashes.FrozenInk.CreateTag(), 150f),
            };

            var outputInk = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement(
                SimHashes.RefinedCarbon.CreateTag(),
                15f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),
                     
            
            new ComplexRecipe.RecipeElement(
                SimHashes.SlimeMold.CreateTag(),
                40.5f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),
            new ComplexRecipe.RecipeElement(
                SimHashes.DirtyWater.CreateTag(),
                94.5f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),

            };
           
            string recipeIdInk = ComplexRecipeManager.MakeRecipeID("SludgePress", inputInk, outputInk);

            var recipeInk = new ComplexRecipe(recipeIdInk, inputInk, outputInk)
            {
                time = 20f,
                description = STRINGS.BUILDINGS.SLUDGEPRESS.SQUIDINK,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Composite,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 21,

            };

            //Ovolene
            var inputOvolene = new ComplexRecipe.RecipeElement[]
{

            new ComplexRecipe.RecipeElement(SimHashes.FishMilkIce.CreateTag(), 150f),
};

            var outputOvolene = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement(
                SimHashes.MilkFat.CreateTag(),
                15f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),


            new ComplexRecipe.RecipeElement(
                SimHashes.SlimeMold.CreateTag(),
                40.5f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),
            new ComplexRecipe.RecipeElement(
                SimHashes.DirtyWater.CreateTag(),
                94.5f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),

            };

            string recipeIdOvolene = ComplexRecipeManager.MakeRecipeID("SludgePress", inputOvolene, outputOvolene);

            var recipeOvolene = new ComplexRecipe(recipeIdOvolene, inputOvolene, outputOvolene)
            {
                time = 20f,
                description = STRINGS.BUILDINGS.SLUDGEPRESS.FISHMILK,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Composite,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 21,

            };
        }
    }

}
