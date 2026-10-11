using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TUNING;
using STRINGS;

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
            var vanillaOutputs = new[] { new ComplexRecipe.RecipeElement(new Tag("Atmo_Suit"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaId = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputs, vanillaOutputs);
            var recipe = crm.GetRecipe(vanillaId);


            recipe.ingredients = new[]
            {
            new ComplexRecipe.RecipeElement(GameTags.BasicRefinedMetals, 200f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", inheritElement: true),
            new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 1f),
            new ComplexRecipe.RecipeElement(new Tag("Warm_Vest"), 1f),
        };

            var crmjet = ComplexRecipeManager.Get();
            var vanillaInputsjet = new[] {   new ComplexRecipe.RecipeElement((Tag) SimHashes.Steel.ToString(), 200f),
               new ComplexRecipe.RecipeElement(GameTags.Fabrics, 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "")  };
            var vanillaOutputsjet = new[] { new ComplexRecipe.RecipeElement(new Tag("Jet_Suit"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaIdjet = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputsjet, vanillaOutputsjet);
            var recipejet = crmjet.GetRecipe(vanillaIdjet);


            recipejet.ingredients = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Steel.CreateTag(), 200f),
                new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 1f),
                new ComplexRecipe.RecipeElement(new Tag("Warm_Vest"), 1f),
            };




            var crmlead = ComplexRecipeManager.Get();
            var vanillaInputslead = new[] {          new ComplexRecipe.RecipeElement((Tag) SimHashes.Lead.ToString(), 200f),
        new ComplexRecipe.RecipeElement((Tag) SimHashes.Glass.ToString(), 10f)};
            var vanillaOutputslead = new[] { new ComplexRecipe.RecipeElement(new Tag("Lead_Suit"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated) };
            string vanillaIdlead = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, vanillaInputslead, vanillaOutputslead);
            var recipelead = crmlead.GetRecipe(vanillaIdlead);
            if (recipelead == null) return;


            recipelead.ingredients = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement(SimHashes.Lead.ToString(), 200f),
            new ComplexRecipe.RecipeElement(SimHashes.Glass.CreateTag(), 10f),
            new ComplexRecipe.RecipeElement(new Tag("Warm_Vest"), 1f),
        };

            // --- Worn suit repair recipes: add BuildingGasket as alternative to Fabrics ---
            var wornAtmoInputs = new[]
            {
                new ComplexRecipe.RecipeElement(new Tag("Worn_Atmo_Suit"), 1f),
                new ComplexRecipe.RecipeElement(GameTags.Fabrics, 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "")
            };
            var atmoOutputs = new[]
            {
                new ComplexRecipe.RecipeElement(new Tag("Atmo_Suit"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };
            string wornAtmoId = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, wornAtmoInputs, atmoOutputs);
            var wornAtmoRecipe = crm.GetRecipe(wornAtmoId);
            if (wornAtmoRecipe != null)
            {
                var atmoAltTags = new Tag[] { GasketConfig.ID, RubberGasketConfig.ID, GameTags.Fabrics[0], GameTags.Fabrics[1] };
                var newIngredients = new List<ComplexRecipe.RecipeElement>
                {
                    new ComplexRecipe.RecipeElement("Worn_Atmo_Suit".ToTag(), 1f),
                    new ComplexRecipe.RecipeElement(atmoAltTags, 1f)
                };
                Traverse.Create(wornAtmoRecipe).Field("ingredients").SetValue(newIngredients.ToArray());
            }

            var wornJetInputs = new[]
            {
                new ComplexRecipe.RecipeElement("Worn_Jet_Suit".ToTag(), 1f),
                new ComplexRecipe.RecipeElement(GameTags.Fabrics, 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "")
            };
            var jetOutputs = new[]
            {
                new ComplexRecipe.RecipeElement(new Tag("Jet_Suit"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };
            string wornJetId = ComplexRecipeManager.MakeRecipeID(SuitFabricatorConfig.ID, wornJetInputs, jetOutputs);
            var wornJetRecipe = crm.GetRecipe(wornJetId);
            if (wornJetRecipe != null)
            {
                var jetAltTags = new Tag[] { GasketConfig.ID, RubberGasketConfig.ID, GameTags.Fabrics[0], GameTags.Fabrics[1] };
                var newIngredients = new List<ComplexRecipe.RecipeElement>
                {
                    new ComplexRecipe.RecipeElement("Worn_Jet_Suit".ToTag(), 1f),
                    new ComplexRecipe.RecipeElement(jetAltTags, 1f)
                };
                Traverse.Create(wornJetRecipe).Field("ingredients").SetValue(newIngredients.ToArray());
            }
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
          
                var recipe = ElectrobankConfig.recipe;
         

                // In ONI, ingredients is an array, not a List
                var ingredients = recipe.ingredients; // ComplexRecipe.RecipeElement[]
                if (ingredients == null || ingredients.Length == 0)
                {
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
    }



}
