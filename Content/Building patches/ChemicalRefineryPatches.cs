using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Buildings
{
    // Adjust the Super Coolant recipe on the Chemical Refinery:
    // Inputs -> 10 kg Fullerene, 40 kg Gold, 50 kg Petroleum (output remains 100 kg SuperCoolant).
    [HarmonyPatch(typeof(ChemicalRefineryConfig), nameof(ChemicalRefineryConfig.ConfigureBuildingTemplate))]
    internal static class ChemicalRefinery_SuperCoolant_AdjustPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            try
            {
                var crm = ComplexRecipeManager.Get();
         

                // In your decompiled ChemicalRefineryConfig, vanilla inputs are:
                // num1 = 0.01 -> 1 kg Fullerene
                // num2 = (1 - num1) * 0.5 -> 49.5 kg Gold, 49.5 kg Petroleum
                // Build the deterministic vanilla ID first
                string vanillaId = BuildSCRecipeId(1f, 49.5f, 49.5f, 100f);

                

                ComplexRecipe target = crm.GetRecipe(vanillaId) ?? FindSCRecipeOnChemicalRefinery(crm);

              

                var newIngredients = new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(SimHashes.Fullerene.CreateTag(), 10f),
                    new ComplexRecipe.RecipeElement(SimHashes.Gold.CreateTag(), 40f),
                    new ComplexRecipe.RecipeElement(SimHashes.Petroleum.CreateTag(), 50f)
                };

                if (!TrySetIngredients(target, newIngredients))
                {
                    Debug.LogWarning("[SuperCoolantAdjust] Failed to update Super Coolant recipe ingredients.");
                    return;
                }

          //      Debug.Log("[SuperCoolantAdjust] Updated Super Coolant inputs to: 10 kg Fullerene, 40 kg Gold, 50 kg Petroleum.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SuperCoolantAdjust] Exception while adjusting Super Coolant recipe: {ex}");
            }
        }

        private static string BuildSCRecipeId(float fullereneKg, float goldKg, float petroleumKg, float outputKg)
        {
            var inputs = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Fullerene.CreateTag(), fullereneKg),
                new ComplexRecipe.RecipeElement(SimHashes.Gold.CreateTag(), goldKg),
                new ComplexRecipe.RecipeElement(SimHashes.Petroleum.CreateTag(), petroleumKg)
            };
            var outputs = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.SuperCoolant.CreateTag(), outputKg, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, true)
            };
            return ComplexRecipeManager.MakeRecipeID(ChemicalRefineryConfig.ID, inputs, outputs);
        }

        private static ComplexRecipe FindSCRecipeOnChemicalRefinery(ComplexRecipeManager crm)
        {
            try
            {
                Tag building = TagManager.Create(ChemicalRefineryConfig.ID);
                Tag scTag = SimHashes.SuperCoolant.CreateTag();

                // Find a dictionary-like field holding recipes: IDictionary<string, ComplexRecipe>
                var recipesField = typeof(ComplexRecipeManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(f => typeof(IDictionary<string, ComplexRecipe>).IsAssignableFrom(f.FieldType));

                if (recipesField?.GetValue(crm) is IDictionary<string, ComplexRecipe> dict)
                {
                    foreach (var kv in dict)
                    {
                        var r = kv.Value;
                        if (r == null || r.fabricators == null || !r.fabricators.Contains(building)) continue;

                        var results = GetResults(r);
                        if (results == null || results.Length != 1) continue;

                        var res = results[0];
                        if (!res.material.Equals(scTag)) continue;
                        if (Mathf.Abs(res.amount - 100f) > 1e-3f) continue;

                        return r;
                    }
                    return null;
                }

                // Fallback: list-like field
                var listField = typeof(ComplexRecipeManager)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(f => typeof(IEnumerable<ComplexRecipe>).IsAssignableFrom(f.FieldType));

                if (listField?.GetValue(crm) is IEnumerable<ComplexRecipe> list)
                {
                    foreach (var r in list)
                    {
                        if (r == null || r.fabricators == null || !r.fabricators.Contains(building)) continue;

                        var results = GetResults(r);
                        if (results == null || results.Length != 1) continue;

                        var res = results[0];
                        if (!res.material.Equals(scTag)) continue;
                        if (Mathf.Abs(res.amount - 100f) > 1e-3f) continue;

                        return r;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SuperCoolantAdjust] Fallback scan failed: {ex}");
            }
            return null;
        }

        private static ComplexRecipe.RecipeElement[] GetResults(ComplexRecipe recipe)
        {
            var t = typeof(ComplexRecipe);
            var f = AccessTools.Field(t, "results") ?? AccessTools.Field(t, "Results");
            if (f != null) return f.GetValue(recipe) as ComplexRecipe.RecipeElement[];

            var p = AccessTools.Property(t, "results") ?? AccessTools.Property(t, "Results");
            if (p != null) return p.GetValue(recipe, null) as ComplexRecipe.RecipeElement[];

            var lf = AccessTools.Field(t, "resultsList");
            if (lf != null && lf.GetValue(recipe) is List<ComplexRecipe.RecipeElement> list)
                return list.ToArray();

            return null;
        }

        private static bool TrySetIngredients(ComplexRecipe recipe, ComplexRecipe.RecipeElement[] newIngredients)
        {
            var t = typeof(ComplexRecipe);

            var f = AccessTools.Field(t, "ingredients") ?? AccessTools.Field(t, "Ingredients");
            if (f != null) { f.SetValue(recipe, newIngredients); return true; }

            var p = AccessTools.Property(t, "ingredients") ?? AccessTools.Property(t, "Ingredients");
            if (p != null && p.CanWrite) { p.SetValue(recipe, newIngredients, null); return true; }

            var lf = AccessTools.Field(t, "ingredientsList");
            if (lf != null && lf.GetValue(recipe) is IList<ComplexRecipe.RecipeElement> list)
            {
                list.Clear();
                foreach (var el in newIngredients) list.Add(el);
                return true;
            }

            return false;
        }
    }
}