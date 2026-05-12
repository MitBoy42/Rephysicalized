using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TUNING;

namespace Rephysicalized
{

    [HarmonyPatch(typeof(SuitFabricatorConfig),
    nameof(SuitFabricatorConfig.ConfigureBuildingTemplate))]
    internal static class SuitFabricatorAtmoSuitRecipePatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var crm = ComplexRecipeManager.Get();
            var vanillaInputs = new[] {   new ComplexRecipe.RecipeElement(GameTags.BasicRefinedMetals, 300f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", inheritElement: true),
               new ComplexRecipe.RecipeElement(GameTags.Fabrics, 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "")  };
            var vanillaOutputs = new[] { new ComplexRecipe.RecipeElement("Atmo_Suit".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaId = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputs, vanillaOutputs);
            var recipe = crm.GetRecipe(vanillaId);


            recipe.ingredients = new[]
            {
            new ComplexRecipe.RecipeElement(GameTags.BasicRefinedMetals, 200f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", inheritElement: true),
            new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 1f),
            new ComplexRecipe.RecipeElement("Warm_Vest".ToTag(), 1f),
        };

            var crmjet = ComplexRecipeManager.Get();
            var vanillaInputsjet = new[] {   new ComplexRecipe.RecipeElement((Tag) SimHashes.Steel.ToString(), 200f),
               new ComplexRecipe.RecipeElement(GameTags.Fabrics, 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "")  };
            var vanillaOutputsjet = new[] { new ComplexRecipe.RecipeElement("Jet_Suit".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaIdjet = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputsjet, vanillaOutputsjet);
            var recipejet = crmjet.GetRecipe(vanillaIdjet);


            recipejet.ingredients = new[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Steel.CreateTag(), 200f),
                new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 1f),
                new ComplexRecipe.RecipeElement("Warm_Vest".ToTag(), 1f),
            };




            var crmlead = ComplexRecipeManager.Get();
            var vanillaInputslead = new[] {          new ComplexRecipe.RecipeElement((Tag) SimHashes.Lead.ToString(), 200f),
        new ComplexRecipe.RecipeElement((Tag) SimHashes.Glass.ToString(), 10f)};
            var vanillaOutputslead = new[] { new ComplexRecipe.RecipeElement("Lead_Suit".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaIdlead = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputslead, vanillaOutputslead);
            var recipelead = crmlead.GetRecipe(vanillaIdlead);
            if (recipelead == null) return;


            recipelead.ingredients = new[]
            {
            new ComplexRecipe.RecipeElement(SimHashes.Lead.ToString(), 200f),
            new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 10f),
            new ComplexRecipe.RecipeElement("Warm_Vest".ToTag(), 1f),
        };
        }
    }

  

    // Ensure this class is in the same namespace as the rest of your patches in this file
    [HarmonyPatch(typeof(AdvancedCraftingTableConfig), nameof(AdvancedCraftingTableConfig.ConfigureRecipes))]
    internal static class AdvancedCraftingTableKatairitePatch
    {
        // After the game defines the recipes for Advanced Crafting Table,
        // adjust the Electrobank recipe's Katairite ingredient from 200f to 20f.
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var recipe = ElectrobankConfig.recipe;
                if (recipe == null)
                {
                    Debug.LogWarning("[AdvancedCraftingTweaks] Electrobank recipe not found; cannot adjust Katairite amount.");
                    return;
                }

                // In ONI, ingredients is an array, not a List
                var ingredients = recipe.ingredients; // ComplexRecipe.RecipeElement[]
                if (ingredients == null || ingredients.Length == 0)
                {
                    Debug.LogWarning("[AdvancedCraftingTweaks] Electrobank recipe has no ingredients.");
                    return;
                }

                var katTag = SimHashes.Katairite.CreateTag();
                bool changed = false;

                // Use index-based loop; RecipeElement may be a struct and foreach would modify a copy
                for (int i = 0; i < ingredients.Length; i++)
                {
                    if (ingredients[i].material == katTag && Math.Abs(ingredients[i].amount - 20f) > 0.0001f)
                    {
                        ingredients[i].amount = 20f;
                        changed = true;
                    }
                }

                if (changed)
                {
                   
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdvancedCraftingTweaks] Failed to adjust Electrobank recipe Katairite amount: {ex}");
            }
        }
    }



}
