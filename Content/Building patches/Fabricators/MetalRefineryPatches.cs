using HarmonyLib;
using PeterHan.PLib.Options;
using Rephysicalized.Content.System_Patches.Helper_Components;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    internal static class MetalRefinery_CinnabarSplit_RecipeCtorPatches
    {
        private const float InputKg = 100f;
        private const float Epsilon = 0.001f;

        private static void ApplyAllRewrites(
          ComplexRecipe recipe,
          string id,
          ComplexRecipe.RecipeElement[] ingredients,
          ComplexRecipe.RecipeElement[] results)
        {
            if (recipe == null || !SingletonOptions<Config>.Instance.RephysicalizedMetalOre || ingredients == null || ingredients.Length != 1)
                return;
            ComplexRecipe.RecipeElement ingredient = ingredients[0];
            (Tag, float)[] outputs;
            if (!MetalRefinery_CinnabarSplit_RecipeCtorPatches.TryGetMetalDictionaryOutputs(ingredient.material, out outputs))
                return;
            MetalRefinery_CinnabarSplit_RecipeCtorPatches.TryRewriteRecipe(recipe, id, ingredients, results, ingredient.material, outputs);
        }

        private static bool TryGetMetalDictionaryOutputs(
          Tag ingredientTag,
          out (Tag tag, float amount)[] outputs)
        {
            outputs = ((Tag, float)[])null;
            List<Tuple<string, float>> tupleList;
            if (ingredientTag == (Tag)(string)null || !RephysicalizedMetalDictionary.MetalOresAdditionalOutputs.TryGetValue(ingredientTag.ToString(), out tupleList))
                return false;
            outputs = new (Tag, float)[tupleList.Count];
            for (int index = 0; index < tupleList.Count; ++index)
            {
                Tuple<string, float> tuple = tupleList[index];
                string first = tuple.first;
                float second = tuple.second;
                Tag tag = TagManager.Create(first);
                outputs[index] = (tag, second * 100f);
            }
            return (uint)outputs.Length > 0U;
        }

        private static void TryRewriteRecipe(
          ComplexRecipe recipe,
          string id,
          ComplexRecipe.RecipeElement[] ingredients,
          ComplexRecipe.RecipeElement[] results,
          Tag targetIngredientTag,
          (Tag tag, float amount)[] outputs)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("MetalRefinery", StringComparison.Ordinal) || ingredients == null || ingredients.Length != 1)
                return;
            ComplexRecipe.RecipeElement ingredient = ingredients[0];
            if (ingredient.material != targetIngredientTag || (double)Mathf.Abs(ingredient.amount - 100f) > 1.0 / 1000.0)
                return;
            ComplexRecipe.RecipeElement.TemperatureOperation temperatureOperation = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
            if (results != null && (uint)results.Length > 0U)
                temperatureOperation = results[0].temperatureOperation;
            ComplexRecipe.RecipeElement[] recipeElementArray = new ComplexRecipe.RecipeElement[outputs.Length];
            for (int index = 0; index < outputs.Length; ++index)
                recipeElementArray[index] = new ComplexRecipe.RecipeElement(outputs[index].tag, outputs[index].amount, temperatureOperation);
            recipe.results = recipeElementArray;
        }

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new System.Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
        private static class CtorPatch_NoDlc
        {
            public static void Postfix(
              ComplexRecipe __instance,
              string id,
              ComplexRecipe.RecipeElement[] ingredients,
              ComplexRecipe.RecipeElement[] results)
            {
                MetalRefinery_CinnabarSplit_RecipeCtorPatches.ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new System.Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) })]
        private static class CtorPatch_WithDlc
        {
            public static void Postfix(
              ComplexRecipe __instance,
              string id,
              ComplexRecipe.RecipeElement[] ingredients,
              ComplexRecipe.RecipeElement[] results,
              string[] requiredDlcIds)
            {
                MetalRefinery_CinnabarSplit_RecipeCtorPatches.ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }
    }
}