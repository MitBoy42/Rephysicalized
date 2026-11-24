using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
    internal sealed class IngredientMultiplierInfo
    {
        public float Multiplier { get; }
        public string PlantId { get; }

        public IngredientMultiplierInfo(float multiplier, string plantId = null)
        {
            Multiplier = multiplier;
            PlantId = plantId;
        }
    }

    // Central config: add more foods here later
    // 1. Increase amount in crops
    // 2. Decrease calorie amount
    // 3. Traverse recipes.
    // 4. Check animal diet (Not being set up here)
    // 5. Check rephysicalized cooking (Not being set up here)
    public static class FoodDensityRebalance
    {
        // SINGLE central place for multipliers: { "CropTag", multiplier, optional plant ID }
        // Adding new food: add entry here. Auto-generates crop/calories/stego/recipes.
        internal static readonly Dictionary<Tag, IngredientMultiplierInfo> IngredientMultiplier = new Dictionary<Tag, IngredientMultiplierInfo>
        {
            { PrickleFruitConfig.ID.ToTag(), new IngredientMultiplierInfo(5f, PrickleFlowerConfig.ID) },
            { SwampFruitConfig.ID.ToTag(), new IngredientMultiplierInfo(5f, SwampHarvestPlantConfig.ID) },
            { CarrotConfig.ID.ToTag(), new IngredientMultiplierInfo(4f, CarrotPlantConfig.ID) },
            { "FriesCarrot".ToTag(), new IngredientMultiplierInfo(6f) },
            { "MushBar".ToTag(), new IngredientMultiplierInfo(5f) },
            { "FriedMushBar".ToTag(), new IngredientMultiplierInfo(5f) },
            { "JawboFillet".ToTag(), new IngredientMultiplierInfo(0.5f) },
            { "PrehistoricPacuFillet".ToTag(), new IngredientMultiplierInfo(0.5f) },
       //One only affects  food and the other affects recipes

        };


        // Fabricator IDs (Tags) to scale; names must match building IDs
        internal static readonly Tag MicrobeMusher = TagManager.Create("MicrobeMusher");
        internal static readonly Tag CookingStation = TagManager.Create("CookingStation");
        internal static readonly Tag GourmetCookingStation = TagManager.Create("GourmetCookingStation");
        internal static readonly Tag Smoker = TagManager.Create("Smoker");
        internal static readonly Tag Deepfryer = TagManager.Create("Deepfryer");
        internal static readonly Tag SushiBar = TagManager.Create("SushiBar");
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
                // Scale linked plant harvests directly so their prefab configs use the updated yield.
                foreach (var kv in FoodDensityRebalance.IngredientMultiplier)
                {
                    if (string.IsNullOrEmpty(kv.Value.PlantId))
                        continue;

                    ScaleHarvest(GetPlantHarvest(kv.Value.PlantId), kv.Value.Multiplier);
                }

                //// Keep product matching for multiplier entries without a plant link.
                //var crops = TUNING.CROPS.CROP_TYPES;
                //if (crops != null)
                //{
                //    for (int i = 0; i < crops.Count; i++)
                //    {
                //        var crop = crops[i];
                //        if (crop?.products == null)
                //            continue;

                //        for (int productIndex = 0; productIndex < crop.products.Count; productIndex++)
                //        {
                //            var product = crop.products[productIndex];
                //            if (!FoodDensityRebalance.IngredientMultiplier.TryGetValue(product.cropId.ToTag(), out var multiplierInfo)
                //                || !string.IsNullOrEmpty(multiplierInfo.PlantId))
                //                continue;

                //            product.quantity *= multiplierInfo.Multiplier;
                //            crop.products[productIndex] = product;
                //        }
                //    }
                //}

                // FOOD: AUTO divide calories by multiplier
                foreach (var kv in FoodDensityRebalance.IngredientMultiplier)
                {
                    // Map tag to FOOD.FOOD_TYPES (handle UPPERCASE)
                    string foodKey = kv.Key.ToString().ToUpperInvariant();
                    
                    // Special case: FriesCarrot is stored as FRIES_CARROT in FOOD.FOOD_TYPES
                    if (foodKey == "FRIESCARROT")
                        foodKey = "FRIES_CARROT";
                    
                    var foodTypeField = AccessTools.Field(typeof(FOOD.FOOD_TYPES), foodKey);
                    if (foodTypeField?.GetValue(null) is EdiblesManager.FoodInfo foodInfo)
                    {
                        AdjustFoodCalories(foodInfo, kv.Value.Multiplier, foodKey);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PricklefruitRebalance] Db.Initialize crop/food patch failed: {ex}");
            }
        }

        private static Crop.CropVal GetPlantHarvest(string plantId)
        {
            if (plantId == PrickleFlowerConfig.ID)
                return PrickleFlowerConfig.HARVEST;
            if (plantId == SwampHarvestPlantConfig.ID)
                return SwampHarvestPlantConfig.HARVEST;
            if (plantId == CarrotPlantConfig.ID)
                return CarrotPlantConfig.HARVEST;

            return null;
        }

        private static void ScaleHarvest(Crop.CropVal crop, float multiplier)
        {
            if (crop?.products == null)
                return;

            for (int i = 0; i < crop.products.Count; i++)
            {
                var product = crop.products[i];
                product.quantity *= multiplier;
                crop.products[i] = product;
            }
        }

        private static void AdjustFoodCalories(EdiblesManager.FoodInfo foodInfo, float divider, string nameForLog)
        {
            

            try
            {
                float before = foodInfo.CaloriesPerUnit;
                float after = before / divider;
                foodInfo.CaloriesPerUnit = after;
            }
            catch
            {
                var caloriesField = AccessTools.Field(foodInfo.GetType(), "caloriesPerUnit");
                if (caloriesField != null)
                {
                    float before = (float)caloriesField.GetValue(foodInfo);
                    float after = before / divider;
                    caloriesField.SetValue(foodInfo, after);
                }
            
            }
        }
    }

    // Recipe scaler with robust timing and diagnostics
    internal static class MultiFoodRecipeScaler
    {
        // Include fabricator in the key so multi-pass initialization doesn't re-apply for the same recipe.
        private static readonly HashSet<string> ProcessedRecipeIds = new HashSet<string>(StringComparer.Ordinal);

        private static readonly HashSet<string> ProcessedResultSignatures = new HashSet<string>(StringComparer.Ordinal);
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
            var recipes = GetRecipesForFabricator(mgr, fabricatorTag) ?? GetAllRecipes(mgr);

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

                // Key includes fabricator to avoid cross-fabricator collisions and ensure multi-pass init doesn't re-apply for same fabricator.
                // Always use the recipe object identity to guarantee a stable, per-object key across initialization passes and
                // avoid double-scaling caused by inconsistent reflected ids.
                var processedKey = fabricatorTag + ":" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(recipe).ToString();
                if (ProcessedRecipeIds.Contains(processedKey))
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
                        if (FoodDensityRebalance.IngredientMultiplier.TryGetValue(ing.material, out var multiplierInfo) && multiplierInfo.Multiplier > 0f)
                        {
                            float old = ing.amount;
                            ing.amount = old * multiplierInfo.Multiplier;
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
                                if (FoodDensityRebalance.IngredientMultiplier.TryGetValue(t, out var possibleMultiplierInfo) && possibleMultiplierInfo.Multiplier > maxMul)
                                    maxMul = possibleMultiplierInfo.Multiplier;
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
                    // Build a signature for current result materials so variants that produce the same outputs
                    // are recognized and result-scaling is only applied once. Use material-only signature because
                    // some builds share result object instances or amounts may already be mutated by earlier passes.
                    string resultSignature = fabricatorTag + ":" + string.Join("|", res.Select(rr => rr.material.ToString().ToUpperInvariant()));
                 
                    bool alreadyProcessedResults = ProcessedResultSignatures.Contains(resultSignature);
                  

                    bool resultsChanged = false;
                    for (int i = 0; i < res.Length; i++)
                    {
                        var r = res[i];
                        if (!alreadyProcessedResults && FoodDensityRebalance.IngredientMultiplier.TryGetValue(r.material, out var multiplierInfo) && multiplierInfo.Multiplier > 0f)
                        {
                            float old = r.amount;
                            r.amount = old * multiplierInfo.Multiplier;
                            res[i] = r;
                            changed = true;
                            resultsChanged = true;
                            changedResults++;
                          
                        }
                    }

                    if (resultsChanged)
                    {
                        ProcessedResultSignatures.Add(resultSignature);
                    
                    }
                }

                if (changed)
                {
                    if (ings != null) recipe.ingredients = ings;
                    if (res != null) recipe.results = res;

                    ProcessedRecipeIds.Add(processedKey);
                    changedCount++;
              

                }
            }
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

    //[HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureRecipes")]
    //public static class MicrobeMusherConfig_ConfigureRecipes_MultiFoodPatch
    //{
    //    public static void Postfix()
    //    {
    //        MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.MicrobeMusher, "MicrobeMusher");
    //    }
    //}

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
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.CookingStation, "CookingStation (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.MicrobeMusher, "MicrobeMusher (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.GourmetCookingStation, "GourmetCookingStation (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Smoker, "Smoker (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.Deepfryer, "Deepfryer (final)");
            MultiFoodRecipeScaler.ScaleForFabricator(FoodDensityRebalance.SushiBar, "SushiBar (final)");
        }
    }
}
