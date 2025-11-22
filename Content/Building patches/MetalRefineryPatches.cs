using System;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    // Patch ComplexRecipe constructors to catch the recipe creation regardless of when it happens.
    // We identify the MetalRefinery's Cinnabar(100 kg) recipe and rewrite its outputs to Mercury(86 kg) + Sulfur(14 kg).
    internal static class MetalRefinery_CinnabarSplit_RecipeCtorPatches
    {
        private const float InputKg = 100f;
        private const float MercuryOutKg = 86f;
        private const float SulfurOutKg = 14f;
        private const float Epsilon = 0.0001f;

        private static readonly Tag CinnabarTag = SimHashes.Cinnabar.CreateTag();
        private static readonly Tag MercuryTag = SimHashes.Mercury.CreateTag();
        private static readonly Tag SulfurTag = SimHashes.Sulfur.CreateTag();

        // Constructor: ComplexRecipe(string id, RecipeElement[] ingredients, RecipeElement[] results)
        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] {
            typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[])
        })]
        private static class CtorPatch_NoDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
            {
                TryRewriteCinnabarRecipe(__instance, id, ingredients, results);
            }
        }

        // Constructor: ComplexRecipe(string id, RecipeElement[] ingredients, RecipeElement[] results, string[] requiredDlcIds)
        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] {
            typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[])
        })]
        private static class CtorPatch_WithDlc
        {
            // IMPORTANT: parameter name must match the original ("requiredDlcIds") for Harmony to bind it
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, string[] requiredDlcIds)
            {
                TryRewriteCinnabarRecipe(__instance, id, ingredients, results);
            }
        }

        private static void TryRewriteCinnabarRecipe(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
        {
            try
            {
                // Only MetalRefinery recipes: MakeRecipeID starts with the fabricator ID passed by the config.
                if (string.IsNullOrEmpty(id) || !id.StartsWith(MetalRefineryConfig.ID, StringComparison.Ordinal))
                    return;

                // Match exactly one ingredient: 100 kg Cinnabar
                if (ingredients == null || ingredients.Length != 1)
                    return;

                var ing = ingredients[0];
                if (ing.material != CinnabarTag)
                    return;

                if (Mathf.Abs(ing.amount - InputKg) > Epsilon)
                    return;

                // Preserve original temperature operation if present, else AverageTemperature
                var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
                if (results != null && results.Length > 0)
                    tempOp = results[0].temperatureOperation;

                // Rewrite outputs
                var newResults = new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(MercuryTag, MercuryOutKg, tempOp),
                    new ComplexRecipe.RecipeElement(SulfurTag,  SulfurOutKg,  tempOp)
                };

                recipe.results = newResults;
            }
            catch
            {
                // Be safe: never break recipe creation if something goes wrong
            }
        }
    }
}