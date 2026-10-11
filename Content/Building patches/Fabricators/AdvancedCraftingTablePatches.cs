using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Fabricators
{
    [HarmonyPatch(typeof(AdvancedCraftingTableConfig), nameof(AdvancedCraftingTableConfig.ConfigureBuildingTemplate))]
    public static class AdvancedCraftingTableConfig_AddUraniumRecipe_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var inputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(ModElementRegistration.Silicon.Tag, 5f)
            };
            var outputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement( "PowerStationTools".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID(AdvancedCraftingTableConfig.ID, inputs, outputs);
            var recipe = new ComplexRecipe(recipeID, inputs, outputs, 0, 0)
            {
                time = 30f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = STRINGS.BUILDINGS.ADVANCEDCRAFTINGTABLE.MICROCHIP,
                fabricators = new List<Tag> { TagManager.Create(AdvancedCraftingTableConfig.ID) }
            };
        }
    }
}
