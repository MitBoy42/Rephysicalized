using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using STRINGS;

namespace Rephysicalized.Content.Building_patches.Fabricators
{

    [HarmonyPatch(typeof(OrbitalResearchCenterConfig), nameof(OrbitalResearchCenterConfig.ConfigureBuildingTemplate))]
    public static class OrbitalResearchCenterConfig_SiliconPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var inputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(ModElementRegistration.Silicon.Tag, 2f)
            };
            var outputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement("OrbitalResearchDatabank".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID(OrbitalResearchCenterConfig.ID, inputs, outputs);
            var recipe = new ComplexRecipe(recipeID, inputs, outputs, 0, 0)
            {
                time = 33f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = (string) ITEMS.INDUSTRIAL_PRODUCTS.ORBITAL_RESEARCH_DATABANK.RECIPE_DESC,
                fabricators = new List<Tag> { TagManager.Create(OrbitalResearchCenterConfig.ID) }
            };
        }
    }
    [HarmonyPatch(typeof(DataMinerConfig), nameof(DataMinerConfig.ConfigureBuildingTemplate))]
    public static class DataMinerConfig_SiliconPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var inputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(ModElementRegistration.Silicon.Tag, 2f)
            };
            var outputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement("OrbitalResearchDatabank".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID(OrbitalResearchCenterConfig.ID, inputs, outputs);
            var recipe = new ComplexRecipe(recipeID, inputs, outputs, 0, 0)
            {
                time = 200f,
                description = string.Format((string)BUILDINGS.PREFABS.EGGCRACKER.RECIPE_DESCRIPTION, (object)ElementLoader.FindElementByHash(ModElementRegistration.Silicon).name, (object)DatabankHelper.NAME),
                fabricators = new List<Tag>()
      {
        TagManager.Create("DataMiner")
      },
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                sortOrder = 300

            };
        }
    }
}
