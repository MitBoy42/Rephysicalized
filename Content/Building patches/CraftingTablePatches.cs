using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using STRINGS;

namespace Rephysicalized
{
    // Replace the raw metal electrobank recipe:
    // - Iron Ore input -> Electrobank + 180 kg Rust
    // - Other metal ores -> Electrobank + 180 kg Sand
    [HarmonyPatch(typeof(CraftingTableConfig), "CreateMetalMiniVoltRecipe")]
    public static class CraftingTableConfig_CreateMetalMiniVoltRecipe_Patch
    {
        public static bool Prefix(CraftingTableConfig __instance, Tag[] inputMetals)
        {
            // Build tags and inputs for two separate recipes
            Tag ironOreTag = SimHashes.IronOre.CreateTag();
            Tag[] otherMetalTags = inputMetals?.Where(t => t != ironOreTag).ToArray() ?? new Tag[0];

            // Common result: the disposable raw-metal electrobank item
            var electrobankResult = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement("DisposableElectrobank_RawMetal".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
            };

            // 1) Iron-only recipe: 200 kg IronOre -> Electrobank + 180 kg Rust
            {
                var ironInput = new ComplexRecipe.RecipeElement[]
                {
                    // Require exactly Iron Ore
                    new ComplexRecipe.RecipeElement(ironOreTag, 200f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                };

                var ironOutputs = new List<ComplexRecipe.RecipeElement>(electrobankResult)
                {
                    new ComplexRecipe.RecipeElement(SimHashes.Rust.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                     new ComplexRecipe.RecipeElement(SimHashes.Sand.CreateTag(), 80f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                };

                var recipeId = ComplexRecipeManager.MakeRecipeID(
                    CraftingTableConfig.ID,
                    ironInput,
                    ironOutputs.ToArray()
                );

                var r = new ComplexRecipe(recipeId, ironInput, ironOutputs.ToArray(), DlcManager.DLC3)
                {
                    time = TUNING.INDUSTRIAL.RECIPES.STANDARD_FABRICATION_TIME * 2f,
                    description = string.Format(BUILDINGS.PREFABS.CRAFTINGTABLE.RECIPE_DESCRIPTION, MISC.TAGS.METAL, ITEMS.INDUSTRIAL_PRODUCTS.ELECTROBANK_METAL_ORE.NAME),
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.ResultWithIngredient,
                    fabricators = new List<Tag> { CraftingTableConfig.ID.ToTag() },
                    sortOrder = 0
                };
            }

            // 2) Other-metal recipe: 200 kg of any allowed metal (excluding Iron) -> Electrobank + 180 kg Sand
            if (otherMetalTags.Length > 0)
            {
                var otherInput = new ComplexRecipe.RecipeElement[]
                {
                    // Accept any of the provided metal ore tags (excluding Iron), keep vanilla 'inheritElement' behavior
                    new ComplexRecipe.RecipeElement(otherMetalTags, 200f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, "", inheritElement: true)
                };

                var otherOutputs = new List<ComplexRecipe.RecipeElement>(electrobankResult)
                {
                    new ComplexRecipe.RecipeElement(SimHashes.Sand.CreateTag(), 180f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                };

                var recipeId = ComplexRecipeManager.MakeRecipeID(
                    CraftingTableConfig.ID,
                    otherInput,
                    otherOutputs.ToArray()
                );

                var r = new ComplexRecipe(recipeId, otherInput, otherOutputs.ToArray(), DlcManager.DLC3)
                {
                    time = TUNING.INDUSTRIAL.RECIPES.STANDARD_FABRICATION_TIME * 2f,
                    description = string.Format(BUILDINGS.PREFABS.CRAFTINGTABLE.RECIPE_DESCRIPTION, MISC.TAGS.METAL, ITEMS.INDUSTRIAL_PRODUCTS.ELECTROBANK_METAL_ORE.NAME),
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                    fabricators = new List<Tag> { CraftingTableConfig.ID.ToTag() },
                    sortOrder = 0
                };
            }

            // Skip the original CreateMetalMiniVoltRecipe implementation
            return false;
        }
    }
}