using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.ModDb
{
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationIndustrialOverhaul
    {
        private static void Postfix()
        {
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.RonivansIndustrialOverhaul");

            // RayonLoom Recipe patches (like ExpellerPress)
            var ctor1 = AccessTools.Constructor(typeof(ComplexRecipe), new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) });
            if (ctor1 != null)
            {
                harmony.Patch(ctor1, postfix: new HarmonyMethod(typeof(RonivansIntegrationIndustrialOverhaul), nameof(RayonLoomRecipePatch)));
            }
var ctor2 = AccessTools.Constructor(typeof(ComplexRecipe), new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) });
            if (ctor2 != null)
            {
                harmony.Patch(ctor2, postfix: new HarmonyMethod(typeof(RonivansIntegrationIndustrialOverhaul), nameof(RayonLoomRecipePatch)));
            }

            // RayonLoom ElementConverter patch
            try
            {
                var rayonLoomType = Type.GetType("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_RayonLoomConfig, RonivansLegacy_ChemicalProcessing");
                if (rayonLoomType != null)
                {
                    var method = AccessTools.Method(rayonLoomType, "ConfigureBuildingTemplate", new[] { typeof(GameObject), typeof(Tag) });
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RonivansIntegrationIndustrialOverhaul), nameof(RayonLoomConfigPatch));
                        harmony.Patch(method, postfix: postfix);
                    }
                }
            }
            catch (System.Exception e)
            {
            }
        }

        public static void RayonLoomRecipePatch(ComplexRecipe __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.id) || !__instance.id.StartsWith("Chemical_RayonLoom"))
                return;

            float inputSum = __instance.ingredients.Sum(i => i.amount);
            float outputSum = __instance.results.Sum(r => r.amount);

            if (outputSum + 0.01f >= inputSum)
                return;

            Tag toxicsandTag = SimHashes.ToxicSand.CreateTag();
            float toxicsandAmount = inputSum - outputSum;

            var newResults = new List<ComplexRecipe.RecipeElement>(__instance.results);
            newResults.Add(new ComplexRecipe.RecipeElement(toxicsandTag, toxicsandAmount));
            __instance.results = newResults.ToArray();
        }

        public static void RayonLoomConfigPatch(GameObject go, Tag prefab_tag)
        {
            var converter = go.GetComponent<ElementConverter>();
            if (converter == null || converter.outputElements == null || converter.outputElements.Length == 0)
                return;

            var newOutputs = new List<ElementConverter.OutputElement>(converter.outputElements);
            newOutputs.Add(new ElementConverter.OutputElement(0.075f, SimHashes.CarbonDioxide, 273.15f + 100f));
            converter.outputElements = newOutputs.ToArray();
        }
    }
}

