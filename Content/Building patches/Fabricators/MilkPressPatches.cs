using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Fabricators
{


    [HarmonyPatch(typeof(MilkPressConfig), nameof(MilkPressConfig.ConfigureBuildingTemplate))]
    internal static class MilkPress_LatexInput_AdjustPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (DlcManager.IsContentSubscribed("DLC5_ID"))
            {
                var crm = ComplexRecipeManager.Get();
                var inputs1 = new[]
                {
                new ComplexRecipe.RecipeElement(SimHashes.PalmWood.CreateTag(), 100f)
            };
                var outputs1 = new[]
                {
                new ComplexRecipe.RecipeElement(SimHashes.Latex.CreateTag(), 50f)
            };
                string recipeIdLatex = ComplexRecipeManager.MakeRecipeID(MilkPressConfig.ID, inputs1, outputs1);
                var recipeLatex = crm?.GetRecipe(recipeIdLatex);
                recipeLatex.results = new ComplexRecipe.RecipeElement[2]
                   {
                 new ComplexRecipe.RecipeElement(SimHashes.Latex.CreateTag(), 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, true),
                new ComplexRecipe.RecipeElement(ModElementRegistration.CrudByproduct.CreateTag(), 50f)
                   };

                var inputs2 = new[]
              {
                new ComplexRecipe.RecipeElement( DewDripConfig.ID, 2f)
            };
                var outputs2 = new[]
                {
                new ComplexRecipe.RecipeElement(SimHashes.Milk.CreateTag(), 20f)
            };
                string recipeIdDew = ComplexRecipeManager.MakeRecipeID(MilkPressConfig.ID, inputs2, outputs2);
                var recipeDew = crm?.GetRecipe(recipeIdDew);
                recipeDew.ingredients = new ComplexRecipe.RecipeElement[]
                   {
                 new ComplexRecipe.RecipeElement(DewDripConfig.ID, 20f),

                   };
            }
        }

    }

    // MilkPress: switch Water to Brine for specified recipes and adjust ColdWheatSeed amount
    [HarmonyPatch(typeof(MilkPressConfig), nameof(MilkPressConfig.AddRecipes))]
    public static class MilkPressConfig_AddRecipes_Postfix
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {
            // Increase milk output for ColdWheatSeed + Water/Mucus from 20f -> 25f.
            var crm = ComplexRecipeManager.Get();
            if (crm == null)
                return;

            // Ingredients for the recipe (as defined in MilkPressConfig.AddRecipes)
            var coldWheatInputs = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement((Tag)"ColdWheatSeed", 10f),
                new ComplexRecipe.RecipeElement(new Tag[2]
                {
                    SimHashes.Water.CreateTag(),
                    SimHashes.Mucus.CreateTag()
                }, 15f)
            };

            var coldWheatOutput = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Milk.CreateTag(), 20f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, true)
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(MilkPressConfig.ID, coldWheatInputs, coldWheatOutput);
            var recipe = crm.GetRecipe(recipeId);
            if (recipe == null)
                return;

            // Update the Milk result amount to 25f (keep the rest of the element settings intact)
            var results = recipe.results;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].material == SimHashes.Milk.CreateTag())
                {
                    results[i] = new ComplexRecipe.RecipeElement(
                        SimHashes.Milk.CreateTag(),
                        25f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                        true);
                }
            }
            recipe.results = results;
        }
    }



    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class MilkPress_AddBalmLilyPhytoOilRecipe_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var fabricatorTag = TagManager.Create("MilkPress");

            var input = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement((Tag)"SwampLilyFlower", 20f),
            new ComplexRecipe.RecipeElement(SimHashes.Mud.CreateTag(), 80f),
            };

            var output = new ComplexRecipe.RecipeElement[]
            {

            new ComplexRecipe.RecipeElement(
                SimHashes.PhytoOil.CreateTag(),
                60f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),
               new ComplexRecipe.RecipeElement(
                SimHashes.Dirt.CreateTag(),
                40f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            )
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID("MilkPress", input, output);

            var recipe = new ComplexRecipe(recipeId, input, output)
            {
                time = 40f,
                description = STRINGS.BUILDINGS.MILKPRESS.BALMLILY_PHYTOOIL,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 21,

            };
        }
    }


    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class MilkPress_AddTableSaltRecipe_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (DlcManager.IsContentSubscribed("DLC5_ID"))
            {
                var fabricatorTag = TagManager.Create("MilkPress");

                var input = new ComplexRecipe.RecipeElement[]
                {
            new ComplexRecipe.RecipeElement((Tag)"SaltySticksFood", 1f),

                };

                var output = new ComplexRecipe.RecipeElement[]
                {

            new ComplexRecipe.RecipeElement(
             "TableSalt",
                0.01f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),
               new ComplexRecipe.RecipeElement(
              "PlantFiber",
                1f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            )
                };

                string recipeId = ComplexRecipeManager.MakeRecipeID("MilkPress", input, output);

                var recipe = new ComplexRecipe(recipeId, input, output)
                {
                    time = 20f,
                    description = STRINGS.BUILDINGS.MILKPRESS.TABLESALT,
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                    fabricators = new List<Tag> { fabricatorTag },
                    sortOrder = 21,

                };
            }
        }
    }
}
