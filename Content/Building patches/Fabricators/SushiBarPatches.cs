using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Fabricators
{


    // Adds extra output to Sushi Bar Urchin meat recipe: 99f of Coquina.
    // UrchinMeatConfig does not implement ConfigureRecipes; SushiBarConfig creates UrchinMeatConfig.recipe.
    // Patch the recipe field after all configs are set up.
    [HarmonyPatch(typeof(SushiBarConfig), "ConfigureRecipes")]
    public static class SushiBarConfig_ConfigureRecipes_UrchinCoquinaPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var recipe = UrchinMeatConfig.recipe;
            
            if (recipe == null)
                return;
            var urchinTag = (Tag)"Urchin";


            foreach (var ing in recipe.ingredients)
            {
                if (ing.material == urchinTag)
                {
                    ing.amount = 100f;
                    break;
                }
            }
            var newResults = new List<ComplexRecipe.RecipeElement>(recipe.results)
            {
                new ComplexRecipe.RecipeElement(
                    SimHashes.Coquina.CreateTag(),
                    99f,
                    ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            Traverse.Create(recipe).Field("results").SetValue(newResults.ToArray());
        }
    }

}

