using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    // Helper logic to detect target recipes and append water
    internal static class EggCrackerWaterRecipeLogic
    {
        private static readonly Tag EggCrackerTag = (Tag)"EggCracker";
        private static readonly Tag WaterTag = SimHashes.Water.CreateTag();
        private const float WaterKgRequired = 5f;

        private static HashSet<Tag> _lightbugEggTags;

        private static HashSet<Tag> LightbugEggTags
        {
            get
            {
                if (_lightbugEggTags == null)
                {
                    try
                    {
                        var speciesKey = GameTags.Creatures.Species.LightBugSpecies;
                        if (EggCrackerConfig.EggsBySpecies.TryGetValue(speciesKey, out var eggData) &&
                            eggData != null && eggData.Count > 0)
                        {
                            _lightbugEggTags = new HashSet<Tag>(eggData.Select(ed => ed.id));
                        }
                        else
                        {
                            _lightbugEggTags = new HashSet<Tag>();
                        }
                    }
                    catch
                    {
                        _lightbugEggTags = new HashSet<Tag>();
                    }
                }
                return _lightbugEggTags;
            }
        }

        internal static bool IsEggCrackerRecipe(ComplexRecipe recipe)
        {
            if (recipe == null) return false;

            if (recipe.fabricators != null && recipe.fabricators.Contains(EggCrackerTag))
                return true;

            return !string.IsNullOrEmpty(recipe.id) &&
                   recipe.id.IndexOf("EggCracker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsLightBugEggRecipe(ComplexRecipe recipe)
        {
            if (recipe == null || recipe.ingredients == null) return false;

            foreach (var ing in recipe.ingredients)
            {
                if (ing.possibleMaterials != null && ing.possibleMaterials.Length > 0)
                {
                    if (ing.possibleMaterials.Any(pm => LightbugEggTags.Contains(pm)))
                        return true;
                }
                else
                {
                    if (LightbugEggTags.Contains(ing.material))
                        return true;
                }
            }
            return false;
        }

        internal static bool HasWaterIngredient(ComplexRecipe recipe)
        {
            if (recipe?.ingredients == null) return false;
            return recipe.ingredients.Any(i => i.material == WaterTag ||
                                               (i.possibleMaterials != null && i.possibleMaterials.Contains(WaterTag)));
        }

        internal static void AppendWater(ComplexRecipe recipe)
        {
            if (recipe == null || recipe.ingredients == null) return;

            var newIngredients = new List<ComplexRecipe.RecipeElement>(recipe.ingredients)
            {
                new ComplexRecipe.RecipeElement(
                    WaterTag,
                    WaterKgRequired,
                    ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
            recipe.ingredients = newIngredients.ToArray();
        }
    }

    // Patch ComplexRecipe constructor so every newly created recipe can be adjusted deterministically.
    [HarmonyPatch(typeof(ComplexRecipe))]
    public static class ComplexRecipeCtorWaterPatch
    {
        // Common ONI constructor: (string id, RecipeElement[] inputs, RecipeElement[] outputs, string[] requiredDlcIds, string[] forbiddenDlcIds)
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, new Type[] {
            typeof(string),
            typeof(ComplexRecipe.RecipeElement[]),
            typeof(ComplexRecipe.RecipeElement[]),
            typeof(string[]),
            typeof(string[])
        })]
        public static void PostfixCtor(ComplexRecipe __instance)
        {
            TryAppendWater(__instance);
        }

        private static void TryAppendWater(ComplexRecipe recipe)
        {
            try
            {
                if (recipe == null) return;
                if (!EggCrackerWaterRecipeLogic.IsEggCrackerRecipe(recipe)) return;
                if (!EggCrackerWaterRecipeLogic.IsLightBugEggRecipe(recipe)) return;
                if (EggCrackerWaterRecipeLogic.HasWaterIngredient(recipe)) return;

                EggCrackerWaterRecipeLogic.AppendWater(recipe);
            }
            catch
            {
                // Silent fail to avoid impacting other mods or game load if something unexpected occurs.
            }
        }
    }
}