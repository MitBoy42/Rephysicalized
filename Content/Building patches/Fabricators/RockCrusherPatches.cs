using HarmonyLib;
using PeterHan.PLib.Options;
using Rephysicalized.Content.System_Patches.Helper_Components;
using Rephysicalized.ModElements;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{

    [HarmonyPatch]
    public static class RockCrusherElementDropperPatch
    {
        [HarmonyPatch(typeof(RockCrusherConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            if (!Config.Instance.RephysicalizedMetalOre) return;
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.storeProduced = true;

            var dropper = go.AddComponent<WorldElementDropper>();

            dropper.DropSolids = true;
            dropper.DropLiquids = true;
            dropper.DropGases = true;
            dropper.TargetStorage = fabricator.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            cmp.storages = [fabricator.outStorage];

        }
        [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
        [HarmonyPostfix]
        public static void DropAfterRecipe(ComplexFabricator __instance)
        {
            if (__instance.PrefabID().Name == RockCrusherConfig.ID)
            {
                if (!Config.Instance.RephysicalizedMetalOre) return;
                var dropper = __instance.GetComponent<WorldElementDropper>();
                var storage = __instance.outStorage;
                if (dropper != null && storage != null)
                {
                    storage.DropAll();
                }
            }
        }

    }
 [HarmonyPatch(typeof(RockCrusherConfig), "ConfigureBuildingTemplate")]
    public static class RockCrusher_UrchinRecipe_Amount100Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var mgr = ComplexRecipeManager.Get();
            if (mgr == null || mgr.recipes == null) return;
        
            var inputs = new[] { new ComplexRecipe.RecipeElement(UrchinConfig.ID.ToTag(), 1f) };
            var outputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Diamond.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature) };
            var recipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, inputs, outputs);

            var recipe = mgr.GetRecipe(recipeId);
            if (recipe == null || recipe.results == null) return;
            recipe.results = new[]
         {
            new ComplexRecipe.RecipeElement(SimHashes.Diamond.CreateTag(), 99f),
            new ComplexRecipe.RecipeElement(RotPileConfig.ID.ToTag(), 1f),
   
        };
            if (recipe.ingredients != null)
            {
                foreach (var ing in recipe.ingredients)
                {
                    if (ing.material == UrchinConfig.ID.ToTag())
                    {
                        ing.amount = 100f;
                        break;
                    }
                }
            }
        }
    }
    [HarmonyPatch(typeof(RockCrusherConfig), "ConfigureBuildingTemplate")]
    internal static class RockCrusherRecipeAdjustments
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            ComplexRecipeManager complexRecipeManager = ComplexRecipeManager.Get();
            if (complexRecipeManager == null)
                return;
            Tag tag1 = ElementLoader.FindElementByHash(SimHashes.Katairite).tag;
            Tag sandTag = SimHashes.Sand.CreateTag();
            Tag tag2 = SimHashes.Salt.CreateTag();
            Tag tag3 = TableSaltConfig.ID.ToTag();
            string id1 = ComplexRecipeManager.MakeRecipeID("RockCrusher", (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[1]
            {
        new ComplexRecipe.RecipeElement(TagManager.Create("GarbageElectrobank"), 1f)
            }, (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[1]
            {
        new ComplexRecipe.RecipeElement(tag1, 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            });
            ComplexRecipe recipe1 = complexRecipeManager.GetRecipe(id1);
            if (recipe1 != null)
            {
                ComplexRecipe.RecipeElement[] results = recipe1.results;
                for (int index = 0; index < results.Length; ++index)
                {
                    if (results[index].material == tag1)
                        results[index].amount = 10f;
                }
                if (!((IEnumerable<ComplexRecipe.RecipeElement>)results).Any<ComplexRecipe.RecipeElement>((Func<ComplexRecipe.RecipeElement, bool>)(r => r.material == sandTag)))
                {
                    ComplexRecipe.RecipeElement[] destinationArray = new ComplexRecipe.RecipeElement[results.Length + 1];
                    Array.Copy((Array)results, (Array)destinationArray, results.Length);
                    destinationArray[destinationArray.Length - 1] = new ComplexRecipe.RecipeElement(sandTag, 10f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature);
                    recipe1.results = destinationArray;
                }
            }
            float num = 5E-05f;
            string id2 = ComplexRecipeManager.MakeRecipeID("RockCrusher", (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[1]
            {
        new ComplexRecipe.RecipeElement(tag2, 100f)
            }, (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[2]
            {
        new ComplexRecipe.RecipeElement(tag3, 100f * num, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
        new ComplexRecipe.RecipeElement(sandTag, (float) (100.0 * (1.0 - (double) num)), ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            });
            ComplexRecipe recipe2 = complexRecipeManager.GetRecipe(id2);
            if (recipe2 != null)
            {
                ComplexRecipe.RecipeElement[] results = recipe2.results;
                for (int index = 0; index < results.Length; ++index)
                {
                    if (results[index].material == tag3)
                    {
                        results[index].amount *= 4f;
                        break;
                    }
                }
            }
            if (!SingletonOptions<Config>.Instance.RephysicalizedMetalOre)
                return;
            foreach (KeyValuePair<string, List<Tuple<string, float>>> additionalOutput in RephysicalizedMetalDictionary.MetalOresAdditionalOutputs)
            {
                string key = additionalOutput.Key;
                if (!string.IsNullOrEmpty(key))
                {
                    Element elementByTag = ElementLoader.FindElementByTag(TagManager.Create(key));
                    if (elementByTag != null)
                    {
                        Element lowTempTransition = elementByTag.highTempTransition?.lowTempTransition;
                        if (lowTempTransition != null && lowTempTransition != elementByTag)
                        {
                            string id3 = ComplexRecipeManager.MakeRecipeID("RockCrusher", (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[1]
                            {
                new ComplexRecipe.RecipeElement(elementByTag.tag, 100f)
                            }, (IList<ComplexRecipe.RecipeElement>)new ComplexRecipe.RecipeElement[2]
                            {
                new ComplexRecipe.RecipeElement(lowTempTransition.tag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                new ComplexRecipe.RecipeElement(sandTag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                            });
                            ComplexRecipe recipe3 = complexRecipeManager.GetRecipe(id3);
                            if (recipe3 != null)
                            {
                                ComplexRecipe.RecipeElement.TemperatureOperation temperatureOperation = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
                                if (recipe3.results != null && (uint)recipe3.results.Length > 0U)
                                    temperatureOperation = recipe3.results[0].temperatureOperation;
                                List<Tuple<string, float>> tupleList = additionalOutput.Value;
                                if (tupleList != null && tupleList.Count != 0)
                                {
                                    ComplexRecipe.RecipeElement[] recipeElementArray = new ComplexRecipe.RecipeElement[tupleList.Count + 1];
                                    for (int index = 0; index < tupleList.Count; ++index)
                                    {
                                        string first = tupleList[index].first;
                                        float second = tupleList[index].second;
                                        Tag material = TagManager.Create(first);
                                        recipeElementArray[index] = new ComplexRecipe.RecipeElement(material, second * 50f, temperatureOperation);
                                    }
                                    recipeElementArray[recipeElementArray.Length - 1] = new ComplexRecipe.RecipeElement(sandTag, 50f, temperatureOperation);
                                    recipe3.results = recipeElementArray;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    [HarmonyPatch(typeof(RockCrusherConfig), nameof(RockCrusherConfig.ConfigureBuildingTemplate))]
    public static class RockCrusher_IceToSnowRecipe_Patch
    {
        private static bool _recipeAdded;
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (_recipeAdded) return;
            var mgr = ComplexRecipeManager.Get();
            var rockCrusherTag = TagManager.Create(RockCrusherConfig.ID);
            var inputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Ice.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature) };
            var outputs = new[] { new ComplexRecipe.RecipeElement(SimHashes.Snow.CreateTag(), 100f) };
            string recipeId = ComplexRecipeManager.MakeRecipeID(RockCrusherConfig.ID, inputs, outputs);
            if (mgr.recipes != null && mgr.recipes.Any(r => r.id == recipeId)) { _recipeAdded = true; return; }
            var recipe = new ComplexRecipe(recipeId, inputs, outputs)
            {
                time = 40f,
                description = string.Format(BUILDINGS.PREFABS.ROCKCRUSHER.RECIPE_DESCRIPTION, SimHashes.Ice.CreateTag().ProperName(), SimHashes.Snow.CreateTag().ProperName()),
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                fabricators = new List<Tag> { rockCrusherTag },
                sortOrder = 0
            };
            _recipeAdded = true;
        }
    }
}
