using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    // Early hook: patch either SuitFabricatorConfig.ConfigureRecipes or ExosuitForgeConfig.ConfigureRecipes (whichever exists)
    [HarmonyPatch]
    public static class SuitRecipe_ConfigRecipes_Patch
    {
        public static MethodBase TargetMethod()
        {
            var t1 = AccessTools.TypeByName("SuitFabricatorConfig");
            var m1 = t1 != null ? AccessTools.Method(t1, "ConfigureRecipes") : null;
            if (m1 != null) return m1;

            var t2 = AccessTools.TypeByName("ExosuitForgeConfig");
            var m2 = t2 != null ? AccessTools.Method(t2, "ConfigureRecipes") : null;
            return m2;
        }

        public static void Postfix()
        {
           SuitRecipeTweaker.Apply("Early"); 

        }
    }

    // Safety net: apply again when the Db is ready
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class SuitRecipe_DbInit_Patch
    {
        public static void Postfix()
        {
           SuitRecipeTweaker.Apply("Db.Initialize"); 
      //      catch (Exception e) { Debug.LogWarning($"[Rephysicalized] SuitRecipe_DbInit_Patch.Postfix failed: {e}"); }
        }
    }

    // Robust hook: catch recipe creation at construction time
    [HarmonyPatch]
    public static class ComplexRecipe_Ctors_Postfix
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(ComplexRecipe);
            return t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        }

        public static void Postfix(ComplexRecipe __instance)
        {
            try
            {
                if (__instance != null && SuitRecipeTweaker.TweakRecipe(__instance))
            //        Debug.Log($"[Rephysicalized] Patched recipe during ctor: {__instance.id ?? "(no id)"}")
           ;
            }
            catch (Exception e)
            {
         //       Debug.LogWarning($"[Rephysicalized] ComplexRecipe_Ctors_Postfix failed: {e}")
         ;
            }
        }
    }

    internal static class SuitRecipeTweaker
    {
        // Target results
        private static readonly Tag AtmoSuitTag = "Atmo_Suit".ToTag();
        private static readonly Tag JetSuitTag = "Jet_Suit".ToTag();
        private static readonly Tag LeadSuitTag = "Lead_Suit".ToTag();

        // Worn suit inputs (repair recipes are skipped)
        private static readonly Tag WornAtmoSuitTag = "Worn_Atmo_Suit".ToTag();
        private static readonly Tag WornJetSuitTag = "Worn_Jet_Suit".ToTag();
        private static readonly Tag WornLeadSuitTag = "Worn_Lead_Suit".ToTag();

        // Explicit set of fabric-like tags to exclude from Atmo Suit input slot 2
        private static readonly HashSet<Tag> FabricLikeTags = new HashSet<Tag>
        {
            "Fabrics".ToTag(),        // your build's fabric tag
            "BasicFabric".ToTag(),    // reed fiber prefab id
            "ReedFiber".ToTag(),      // alias in some forks
            "FeatherFiber".ToTag(),   // mod variant
            "Feather_Fiber".ToTag()   // mod variant
        };

        private static readonly Tag WarmVestTag = "Warm_Vest".ToTag();
        private static readonly Tag GlassTag = SimHashes.Glass.CreateTag();
        private static readonly Tag PetroleumTag = SimHashes.Petroleum.CreateTag();

        // Prevent spamming Apply repeatedly in the same frame
        private static int _lastApplyFrame = -1;

        public static void Apply(string source)
        {
            if (Time.renderedFrameCount == _lastApplyFrame) return;
            _lastApplyFrame = Time.renderedFrameCount;

            var mgr = ComplexRecipeManager.Get();
            var recipes = mgr?.recipes;
            if (recipes == null || recipes.Count == 0)
            {
          //      Debug.Log($"[Rephysicalized] SuitRecipeTweaker[{source}]: no recipes available yet.");
                return;
            }

            int scanned = 0, modified = 0;
            foreach (var r in recipes)
            {
                if (r == null) continue;

                bool touched = TweakRecipe(r, out bool wasTarget);
                if (wasTarget) scanned++;
                if (touched) modified++;
            }

         //   Debug.Log($"[Rephysicalized] SuitRecipeTweaker[{source}] scanned {scanned}, modified {modified} recipe(s).");
        }

        public static bool TweakRecipe(ComplexRecipe r) => TweakRecipe(r, out _);

        private static bool TweakRecipe(ComplexRecipe r, out bool wasTarget)
        {
            wasTarget = false;
            if (r == null) return false;

            bool producesAtmo = r.results != null && r.results.Any(e => e.material == AtmoSuitTag);
            bool producesJet = r.results != null && r.results.Any(e => e.material == JetSuitTag);
            bool producesLead = r.results != null && r.results.Any(e => e.material == LeadSuitTag);
            if (!(producesAtmo || producesJet || producesLead))
                return false;

            wasTarget = true;

            var ing = r.ingredients ?? Array.Empty<ComplexRecipe.RecipeElement>();
            // Skip repair recipes (those consuming Worn_* suits)
            bool isRepair =
                ing.Any(e => e.material == WornAtmoSuitTag || e.material == WornJetSuitTag || e.material == WornLeadSuitTag);
            if (isRepair)
                return false;

            var ingredients = new List<ComplexRecipe.RecipeElement>(ing);
            bool touched = false;

            if (producesAtmo)
            {
                // Remove all fabric-like, Warm_Vest, and Glass to avoid duplication or wrong positions
                ingredients.RemoveAll(e => IsFabricLike(e.material) || e.material == WarmVestTag || e.material == GlassTag);

                // Identify the primary non-fabric ingredient to keep at slot 1 (usually refined metal)
                var firstOther = ingredients.FirstOrDefault();
                var restOthers = (firstOther.material == default ? ingredients
                                                                  : ingredients.Skip(1)).ToList();

                // Rebuild ensuring WV is slot 2 and Glass is slot 3
                var rebuilt = new List<ComplexRecipe.RecipeElement>(capacity: 3 + restOthers.Count);
                if (firstOther.material != default)
                    rebuilt.Add(firstOther); // slot 1

                rebuilt.Add(new ComplexRecipe.RecipeElement(WarmVestTag, 1f)); // slot 2
                rebuilt.Add(new ComplexRecipe.RecipeElement(GlassTag, 1f));    // slot 3

                // Preserve any remaining non-fabric inputs after slot 3 (if any exist from other mods)
                if (restOthers.Count > 0)
                    rebuilt.AddRange(restOthers);

                ingredients = rebuilt;
                touched = true;
            }

            if (producesLead && DlcManager.FeatureRadiationEnabled())
            {
                if (!ingredients.Any(e => e.material == WarmVestTag))
                {
                    ingredients.Add(new ComplexRecipe.RecipeElement(WarmVestTag, 1f));
                    touched = true;
                }
            }

            if (producesJet)
            {
                if (!ingredients.Any(e => e.material == WarmVestTag))
                {
                    ingredients.Add(new ComplexRecipe.RecipeElement(WarmVestTag, 1f));
                    touched = true;
                }
                if (!ingredients.Any(e => e.material == GlassTag))
                {
                    ingredients.Add(new ComplexRecipe.RecipeElement(GlassTag, 1f));
                    touched = true;
                }
                // Remove Petroleum
                for (int i = ingredients.Count - 1; i >= 0; i--)
                {
                    if (ingredients[i].material == PetroleumTag)
                    {
                        ingredients.RemoveAt(i);
                        touched = true;
                    }
                }
            }

            if (touched)
                r.ingredients = ingredients.ToArray();

            return touched;
        }

        private static bool IsFabricLike(Tag tag) => FabricLikeTags.Contains(tag);
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
            try
            {
                var recipe = ElectrobankConfig.recipe;
                if (recipe == null)
                {
                    Debug.LogWarning("[AdvancedCraftingTweaks] Electrobank recipe not found; cannot adjust Katairite amount.");
                    return;
                }

                // In ONI, ingredients is an array, not a List
                var ingredients = recipe.ingredients; // ComplexRecipe.RecipeElement[]
                if (ingredients == null || ingredients.Length == 0)
                {
                    Debug.LogWarning("[AdvancedCraftingTweaks] Electrobank recipe has no ingredients.");
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
            catch (Exception ex)
            {
                Debug.LogError($"[AdvancedCraftingTweaks] Failed to adjust Electrobank recipe Katairite amount: {ex}");
            }
        }
    }
 
}
