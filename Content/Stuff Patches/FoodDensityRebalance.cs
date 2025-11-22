using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
    // Central config: add more foods here later
    // 1. Increase amount in crops
    // 2. Decrease calorie amount
    // 3. Traverse recipes.
    // 4. Check animal diet
    // 5. Check rephysicalized cooking
    public static class FoodDensityRebalance
    {
        // Multipliers
        public const float PricklefruitMultiplier = 5f;
        public const float SwampfruitMultiplier = 5f;
        public const float CarrotMultiplier = 4f;
        public const float FriesCarrotMultiplier = 6f;

        // Ingredient tags
        internal static readonly Tag PrickleTag = new Tag(PrickleFruitConfig.ID);
        internal static readonly Tag SwampTag = new Tag(SwampFruitConfig.ID);
        internal static readonly Tag CarrotTag = new Tag(CarrotConfig.ID);
        internal static readonly Tag FriesCarrotTag = new Tag(FriesCarrotConfig.ID);

        // Map ingredient/result tag -> multiplier
        internal static readonly Dictionary<Tag, float> IngredientMultipliers = new Dictionary<Tag, float>
        {
            { PrickleTag, PricklefruitMultiplier },
            { SwampTag,   SwampfruitMultiplier   },
            { CarrotTag,  CarrotMultiplier       },
            { FriesCarrotTag,  FriesCarrotMultiplier  },
        };

        // Fabricator IDs (Tags) to scale; names must match building IDs
        internal static readonly Tag MicrobeMusher = TagManager.Create("MicrobeMusher");
        internal static readonly Tag CookingStation = TagManager.Create("CookingStation");
        internal static readonly Tag GourmetCookingStation = TagManager.Create("GourmetCookingStation");
        internal static readonly Tag Smoker = TagManager.Create("Smoker");
        internal static readonly Tag Deepfryer = TagManager.Create("Deepfryer"); // note lowercase f in ID
    }

    internal static class CropValAccess
    {
        private static readonly Type CropValType = typeof(Crop.CropVal);

        private static FieldInfo FindField(params string[] names)
        {
            foreach (var n in names)
            {
                var fi = AccessTools.Field(CropValType, n);
                if (fi != null) return fi;
            }
            return null;
        }

        private static readonly FieldInfo FI_Id =
            FindField("cropId", "crop_id");
        private static readonly FieldInfo FI_Duration =
            FindField("cropDuration", "time");
        private static readonly FieldInfo FI_Amount =
            FindField("cropAmount", "numProduced");

        public static bool TryRead(Crop.CropVal val, out string id, out float duration, out int amount)
        {
            id = null;
            duration = 0f;
            amount = 0;

            object boxed = val;

            if (FI_Id == null || FI_Duration == null || FI_Amount == null)
                return false;

            id = (string)FI_Id.GetValue(boxed);
            duration = (float)FI_Duration.GetValue(boxed);
            amount = (int)FI_Amount.GetValue(boxed);
            return true;
        }
    }

    // Run crop and food changes after the database has initialized to ensure types are ready
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Db_Initialize_CropsAndFood_Patch
    {
        private static bool applied;

        public static void Postfix()
        {
            if (applied) return;
            applied = true;

            try
            {
                // CROPS: increase crop amounts by multipliers (affects second number; default 1 when missing)
                var list = CROPS.CROP_TYPES;
                if (list != null)
                {
                    // Helper to rewrite a crop entry
                    void RewriteCropAmount(string cropId, float multiplier)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var cv = list[i];
                            if (!CropValAccess.TryRead(cv, out var id, out var duration, out var amount))
                                continue;

                            if (!string.Equals(id, cropId, StringComparison.Ordinal))
                                continue;

                            int baseAmt = amount <= 0 ? 1 : amount;
                            int newAmount = Mathf.RoundToInt(baseAmt * multiplier);
                            list[i] = new Crop.CropVal(id, duration, newAmount);
                        //    Debug.Log($"[PricklefruitRebalance] CROPS: '{id}' amount {amount} -> {newAmount} (duration {duration:0.#}s).");
                            return;
                        }
                 //       Debug.LogWarning($"[PricklefruitRebalance] CROPS: '{cropId}' not found; no crop rewrite performed.");
                    }

                    RewriteCropAmount(PrickleFruitConfig.ID, FoodDensityRebalance.PricklefruitMultiplier);
                    RewriteCropAmount(SwampFruitConfig.ID, FoodDensityRebalance.SwampfruitMultiplier);
                    RewriteCropAmount(CarrotConfig.ID, FoodDensityRebalance.CarrotMultiplier);
                }
                else
                {
             //       Debug.LogWarning("[PricklefruitRebalance] CROPS.CROP_TYPES is null; skipping crop patch.");
                }

                // FOOD: divide raw food calories by multiplier
                // Pricklefruit raw
                AdjustFoodCalories(FOOD.FOOD_TYPES.PRICKLEFRUIT, FoodDensityRebalance.PricklefruitMultiplier, "PRICKLEFRUIT");
                // Swampfruit raw
                AdjustFoodCalories(FOOD.FOOD_TYPES.SWAMPFRUIT, FoodDensityRebalance.SwampfruitMultiplier, "SWAMPFRUIT");
                // Carrot raw
                AdjustFoodCalories(FOOD.FOOD_TYPES.CARROT, FoodDensityRebalance.CarrotMultiplier, "CARROT");
                // FriesCarrot 
                AdjustFoodCalories(FOOD.FOOD_TYPES.FRIES_CARROT, FoodDensityRebalance.FriesCarrotMultiplier, "FRIES_CARROT");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PricklefruitRebalance] Db.Initialize crop/food patch failed: {ex}");
            }
        }

        private static void AdjustFoodCalories(EdiblesManager.FoodInfo foodInfo, float divider, string nameForLog)
        {
            

            try
            {
                float before = foodInfo.CaloriesPerUnit;
                float after = before / divider;
                foodInfo.CaloriesPerUnit = after;
                Debug.Log($"[PricklefruitRebalance] FOOD: {nameForLog} calories {before} -> {after}.");
            }
            catch
            {
                var caloriesField = AccessTools.Field(foodInfo.GetType(), "caloriesPerUnit");
                if (caloriesField != null)
                {
                    float before = (float)caloriesField.GetValue(foodInfo);
                    float after = before / divider;
                    caloriesField.SetValue(foodInfo, after);
                    Debug.Log($"[PricklefruitRebalance] FOOD: {nameForLog} calories {before} -> {after} (via reflection).");
                }
            
            }
        }
    }

    // Recipe scaler with robust timing and diagnostics
    internal static class MultiFoodRecipeScaler
    {
        private static readonly HashSet<string> ProcessedRecipeIds = new HashSet<string>(StringComparer.Ordinal);
        private static FieldInfo _fiId;

        private static string GetRecipeId(ComplexRecipe r)
        {
            if (r == null) return null;
            try
            {
                var pid = r.id; // public in many builds
                if (!string.IsNullOrEmpty(pid)) return pid;
            }
            catch { /* ignore */ }

            _fiId ??= AccessTools.Field(typeof(ComplexRecipe), "id")
                 ?? AccessTools.Field(typeof(ComplexRecipe), "_id");
            if (_fiId != null)
                return _fiId.GetValue(r) as string;

            return null;
        }

        public static void ScaleForFabricator(Tag fabricatorTag, string fabricatorNameForLog)
        {
            var mgr = ComplexRecipeManager.Get();
            if (mgr == null)
            {
                Debug.LogWarning($"[PricklefruitRebalance] Manager null; skip {fabricatorNameForLog}");
                return;
            }

            var recipes = GetRecipesForFabricator(mgr, fabricatorTag) ?? GetAllRecipes(mgr);
            if (recipes == null)
            {
                Debug.LogWarning("[PricklefruitRebalance] No recipes found; aborting for " + fabricatorNameForLog);
                return;
            }

            int seen = 0, matched = 0, changedCount = 0, changedIngredients = 0, changedResults = 0;

            foreach (var recipe in recipes)
            {
                if (recipe == null)
                    continue;

                seen++;

                var fabs = recipe.fabricators;
                if (fabs == null || fabs.Count == 0 || !fabs.Contains(fabricatorTag))
                    continue;

                matched++;

                var rid = GetRecipeId(recipe) ?? "<unknown>";
                if (ProcessedRecipeIds.Contains(rid))
                    continue;

                bool changed = false;

                // Scale ingredients
                var ings = recipe.ingredients;
                if (ings != null && ings.Length > 0)
                {
                    for (int i = 0; i < ings.Length; i++)
                    {
                        var ing = ings[i];

                        // Exact match on material
                        if (FoodDensityRebalance.IngredientMultipliers.TryGetValue(ing.material, out var mul) && mul > 0f)
                        {
                            float old = ing.amount;
                            ing.amount = old * mul;
                            ings[i] = ing;
                            changed = true;
                            changedIngredients++;
                            continue;
                        }

                        // Or any option list that includes supported ingredients
                        if (ing.possibleMaterials != null && ing.possibleMaterials.Length > 0 && ing.amount > 0f)
                        {
                            float maxMul = 0f;
                            foreach (var t in ing.possibleMaterials)
                            {
                                if (FoodDensityRebalance.IngredientMultipliers.TryGetValue(t, out var m) && m > maxMul)
                                    maxMul = m;
                            }
                            if (maxMul > 0f)
                            {
                                float old = ing.amount;
                                ing.amount = old * maxMul;
                                ings[i] = ing;
                                changed = true;
                                changedIngredients++;
                            }
                        }
                    }
                }

                // Scale results (e.g., FriesCarrot as an output)
                var res = recipe.results;
                if (res != null && res.Length > 0)
                {
                    for (int i = 0; i < res.Length; i++)
                    {
                        var r = res[i];
                        if (FoodDensityRebalance.IngredientMultipliers.TryGetValue(r.material, out var mul) && mul > 0f)
                        {
                            float old = r.amount;
                            r.amount = old * mul;
                            res[i] = r;
                            changed = true;
                            changedResults++;
                        }
                    }
                }

                // Apply modifications if any
                if (changed)
                {
                    if (ings != null) recipe.ingredients = ings;
                    if (res != null) recipe.results = res;

                    ProcessedRecipeIds.Add(rid);
                    changedCount++;
              //      Debug.Log($"[PricklefruitRebalance] Scaled recipe '{rid}' on {fabricatorNameForLog}: ingredientsChanged={changedIngredients}, resultsChanged={changedResults}.");
                }
            }

       //     Debug.Log($"[PricklefruitRebalance] {fabricatorNameForLog}: scanned={seen}, fabricatorMatched={matched}, modified={changedCount}, ingChanged={changedIngredients}, resChanged={changedResults}.");
        }

        // Prefer manager API if present: GetRecipesForFabricator(Tag)
        private static IEnumerable<ComplexRecipe> GetRecipesForFabricator(ComplexRecipeManager mgr, Tag fabricatorTag)
        {
            try
            {
                var mi = AccessTools.Method(mgr.GetType(), "GetRecipesForFabricator", new[] { typeof(Tag) });
                if (mi != null)
                {
                    var val = mi.Invoke(mgr, new object[] { fabricatorTag });
                    if (val is IEnumerable<ComplexRecipe> enumerable)
                        return enumerable;
                    if (val is List<ComplexRecipe> list)
                        return list;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        // Robust reflection to handle different backing types (Dictionary, List, IEnumerable)
        private static IEnumerable<ComplexRecipe> GetAllRecipes(ComplexRecipeManager mgr)
        {
            if (mgr == null) return null;
            var t = mgr.GetType();

            foreach (var fname in new[] { "recipes", "_recipes", "recipeList", "allRecipes" })
            {
                var fi = AccessTools.Field(t, fname);
                if (fi == null) continue;
                var val = fi.GetValue(mgr);
                if (val is Dictionary<string, ComplexRecipe> dict)
                    return dict.Values;
                if (val is List<ComplexRecipe> list)
                    return list;
                if (val is IEnumerable<ComplexRecipe> enumerable)
                    return enumerable;
            }

            foreach (var pname in new[] { "Recipes", "AllRecipes" })
            {
                var pi = AccessTools.Property(t, pname);
                if (pi == null) continue;
                var val = pi.GetValue(mgr, null);
                if (val is Dictionary<string, ComplexRecipe> dict)
                    return dict.Values;
                if (val is List<ComplexRecipe> list)
                    return list;
                if (val is IEnumerable<ComplexRecipe> enumerable)
                    return enumerable;
            }

            return null;
        }
    }

    // Postfix patches that run after each station populates its recipes (fabricators already set)
    [HarmonyPatch(typeof(CookingStationConfig), "ConfigureRecipes")]
    public static class CookingStationConfig_ConfigureRecipes_MultiFoodPatch
    {
        public static void Postfix()
        {
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.CookingStation, "CookingStation");
        }
    }

    [HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureRecipes")]
    public static class MicrobeMusherConfig_ConfigureRecipes_MultiFoodPatch
    {
        public static void Postfix()
        {
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.MicrobeMusher, "MicrobeMusher");
        }
    }

    [HarmonyPatch(typeof(GourmetCookingStationConfig), "ConfigureRecipes")]
    public static class GourmetCookingStationConfig_ConfigureRecipes_MultiFoodPatch
    {
        public static void Postfix()
        {
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.GourmetCookingStation, "GourmetCookingStation");
        }
    }

    [HarmonyPatch(typeof(SmokerConfig), "ConfigureRecipes")]
    public static class SmokerConfig_ConfigureRecipes_MultiFoodPatch
    {
        public static void Postfix()
        {
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Smoker, "Smoker");
        }
    }

    // Note: Building ID tag is 'Deepfryer' (not 'DeepFryer')
    [HarmonyPatch(typeof(DeepfryerConfig), "ConfigureRecipes")]
    public static class DeepfryerConfig_ConfigureRecipes_MultiFoodPatch
    {
        public static void Postfix()
        {
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Deepfryer, "Deepfryer");
        }
    }

    // Final safety net: after all prefabs are initialized, run one more pass per fabricator
    [HarmonyPatch(typeof(Assets), "OnPrefabInit")]
    public static class Assets_OnPrefabInit_MultiFoodRecipe_FinalPass_Patch
    {
        public static void Postfix()
        {
         //   Debug.Log("[PricklefruitRebalance] Final recipe scaling pass after Assets.OnPrefabInit.");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.CookingStation, "CookingStation (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.MicrobeMusher, "MicrobeMusher (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.GourmetCookingStation, "GourmetCookingStation (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Smoker, "Smoker (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Deepfryer, "Deepfryer (final)");
        }
    }
}