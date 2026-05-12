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
        private const float Epsilon = 0.0001f;

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
            // list of target ingredient tag -> outputs (tag, amount)
            var rewrites = new (Tag ingredientTag, (Tag tag, float amount)[] outputs)[]
            {
                (SimHashes.Cinnabar.CreateTag(), new[]{ (SimHashes.Mercury.CreateTag(), 86f), (SimHashes.Sulfur.CreateTag(), 14f) }),
                (SimHashes.IronOre.CreateTag(), new[]{ (SimHashes.Iron.CreateTag(), 67f), (SimHashes.IgneousRock.CreateTag(), 33f) }),
                (SimHashes.Cuprite.CreateTag(), new[]{ (SimHashes.Copper.CreateTag(), 80f), (SimHashes.Oxygen.CreateTag(), 20f) }),
                (SimHashes.Cobaltite.CreateTag(), new[]{ (SimHashes.Cobalt.CreateTag(), 71f), (SimHashes.Sulfur.CreateTag(), 29f) }),
                (SimHashes.NickelOre.CreateTag(), new[]{ (SimHashes.Nickel.CreateTag(), 73f), (SimHashes.Sulfur.CreateTag(), 27f) }),
                (SimHashes.Wolframite.CreateTag(), new[]{ (SimHashes.Tungsten.CreateTag(), 60f), (SimHashes.Rust.CreateTag(), 40f) }),
                (SimHashes.AluminumOre.CreateTag(), new[]{ (SimHashes.Aluminum.CreateTag(), 60f), (SimHashes.Oxygen.CreateTag(), 20f), (SimHashes.Water.CreateTag(), 20f) }),
                (SimHashes.GoldAmalgam.CreateTag(), new[]{ (SimHashes.Gold.CreateTag(), 67f), (SimHashes.Mercury.CreateTag(), 33f) })
            };

            foreach (var rw in rewrites)
                TryRewriteRecipe(recipe, id, ingredients, results, rw.ingredientTag, rw.outputs);
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