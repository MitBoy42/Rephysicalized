using System;
using System.Collections.Generic;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    // Consolidated recipe rewrite logic for Metal Refinery outputs.
    internal static class MetalRefinery_CinnabarSplit_RecipeCtorPatches
    {
        private const float InputKg = 100f;
        private const float Epsilon = 0.001f;

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
        private static class CtorPatch_NoDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) })]
        private static class CtorPatch_WithDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, string[] requiredDlcIds)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        private static void ApplyAllRewrites(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
        {
            if (recipe == null) return;
            if (!Config.Instance.RephysicalizedMetalOre) return;
            if (ingredients == null || ingredients.Length != 1)
                return;

            var ing = ingredients[0];
            if (!TryGetMetalDictionaryOutputs(ing.material, out var outputs))
                return;

            TryRewriteRecipe(recipe, id, ingredients, results, ing.material, outputs);

        }

        private static bool TryGetMetalDictionaryOutputs(Tag ingredientTag, out (Tag tag, float amount)[] outputs)
        {
            outputs = null;
            if (ingredientTag == null) return false;

            if (!Rephysicalized.Content.System_Patches.Helper_Components.RephysicalizedMetalDictionary.MetalOresAdditionalOutputs.TryGetValue(ingredientTag.ToString(), out var dictOutputs))
                return false;

            outputs = new (Tag tag, float amount)[dictOutputs.Count];
            for (int i = 0; i < dictOutputs.Count; i++)
            {
                var output = dictOutputs[i];
                var outputTagString = output.first;
                var ratio = output.second;

                var outputTag = TagManager.Create(outputTagString);
                outputs[i] = (outputTag, ratio * 100f);
            }



            return outputs.Length > 0;
        }

        private static void TryRewriteRecipe(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, Tag targetIngredientTag, (Tag tag, float amount)[] outputs)
        {

            if (string.IsNullOrEmpty(id) || !id.StartsWith(MetalRefineryConfig.ID, StringComparison.Ordinal)) return;
            if (ingredients == null || ingredients.Length != 1) return;
            var ing = ingredients[0];
            if (ing.material != targetIngredientTag) return;
            if (Mathf.Abs(ing.amount - InputKg) > Epsilon) return;

            var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
            if (results != null && results.Length > 0) tempOp = results[0].temperatureOperation;

            var newResults = new ComplexRecipe.RecipeElement[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
            {
                newResults[i] = new ComplexRecipe.RecipeElement(outputs[i].tag, outputs[i].amount, tempOp);
            }

            recipe.results = newResults;
        }
    }
}