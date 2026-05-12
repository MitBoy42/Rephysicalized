using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace Rephysicalized.Content.Buildings
{
    [HarmonyPatch(typeof(ChemicalRefineryConfig),
        nameof(ChemicalRefineryConfig.ConfigureBuildingTemplate))]
    internal static class ChemicalRefinery_SuperCoolant_AdjustPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var crm = ComplexRecipeManager.Get();
            var vanillaInputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Fullerene.CreateTag(), 1f), 
                new ComplexRecipe.RecipeElement(SimHashes.Gold.CreateTag(), 49.5f), new ComplexRecipe.RecipeElement(SimHashes.Petroleum.CreateTag(), 49.5f), }; var vanillaOutputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.SuperCoolant.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, true) };
            string vanillaId = ComplexRecipeManager.MakeRecipeID(ChemicalRefineryConfig.ID, vanillaInputs, vanillaOutputs);
            var recipe = crm.GetRecipe(vanillaId);


            recipe.ingredients = new[]
            {
            new ComplexRecipe.RecipeElement(SimHashes.Fullerene.CreateTag(), 10f),
            new ComplexRecipe.RecipeElement(SimHashes.Gold.CreateTag(), 40f),
            new ComplexRecipe.RecipeElement(SimHashes.Petroleum.CreateTag(), 50f),
        };
        }
    }
    [HarmonyPatch(typeof(ChemicalRefineryConfig),
    nameof(ChemicalRefineryConfig.ConfigureBuildingTemplate))]
    public static class ChemicalRefineryIsosaptoSap
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {

            // Inputs/outputs
            var inputs = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(SimHashes.Isoresin.CreateTag(), 25f),
                        new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 75f),
            };

            var outputs = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(
                        SimHashes.Resin.CreateTag(),
                        100f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
                    ),

            };

            // Deterministic recipe ID
            string recipeID = ComplexRecipeManager.MakeRecipeID(ChemicalRefineryConfig.ID, inputs, outputs);

            // Avoid duplicate registration if another mod or reload already added it
            var crm = ComplexRecipeManager.Get();
            if (crm != null && crm.GetRecipe(recipeID) != null)
                return;

            // Create and register the recipe (mirror style from base config)
            var recipe = new ComplexRecipe(recipeID, inputs, outputs)
            {
                time = 80f,
                description = STRINGS.BUILDINGS.CHEMICALREFINERY.ISOSAP_TO_SAP,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { TagManager.Create(ChemicalRefineryConfig.ID) },
                requiredTech = Db.Get().TechItems.superLiquids.parentTechId
            };



            // Inputs/outputs
            var inputs2 = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(SimHashes.SaltWater.CreateTag(), 75f),
                        new ComplexRecipe.RecipeElement(SimHashes.Salt.CreateTag(), 25),
            };

            var outputs2 = new ComplexRecipe.RecipeElement[]
            {
                    new ComplexRecipe.RecipeElement(
                        SimHashes.Brine.CreateTag(),
                        100f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
                    ),

            };

            // Deterministic recipe ID
            string recipeID2 = ComplexRecipeManager.MakeRecipeID(ChemicalRefineryConfig.ID, inputs2, outputs2);



            // Create and register the recipe (mirror style from base config)
            var recipe2 = new ComplexRecipe(recipeID2, inputs2, outputs2)
            {
                time = 80f,
                description = STRINGS.BUILDINGS.CHEMICALREFINERY.SALT_TO_BRINE,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { TagManager.Create(ChemicalRefineryConfig.ID) },

            };
        }
    }
}



