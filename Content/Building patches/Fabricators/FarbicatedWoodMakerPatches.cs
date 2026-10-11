using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VYaml.Serialization;

namespace Rephysicalized.Content.Building_patches.Fabricators
{


    [HarmonyPatch]
    public static class FabricatedWoodMakerElementDropperPatch
    {
        [HarmonyPatch(typeof(FabricatedWoodMakerConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            if (fabricator == null)
                return; // nothing to patch safely

            fabricator.storeProduced = true;

            var dropper = go.GetComponent<WorldElementDropper>() ?? go.AddComponent<WorldElementDropper>();
            dropper.DropSolids = true;
            dropper.DropLiquids = true;
            dropper.DropGases = false;
            dropper.TargetStorage = fabricator.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            if (cmp != null)
                cmp.storages = new Storage[] { fabricator.outStorage };


        }
        [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
        [HarmonyPostfix]
        public static void DropAfterRecipe(ComplexFabricator __instance)
        {
            // Only for FabricatedWoodMaker
            if (__instance.PrefabID().Name == FabricatedWoodMakerConfig.ID)
            {
                var dropper = __instance.GetComponent<WorldElementDropper>();
                var storage = __instance.outStorage;
                if (dropper != null && storage != null)
                {
                    storage.DropAll();
                }
            }
        }

    }



    // Gum wood to latex recipe. 
    [HarmonyPatch(typeof(FabricatedWoodMakerConfig), nameof(FabricatedWoodMakerConfig.ConfigureBuildingTemplate))]
        public static class FabrocatedWoodMaker_Latex
        {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
                var inputs = new ComplexRecipe.RecipeElement[2]
                {
                new ComplexRecipe.RecipeElement(SimHashes.PalmWood.CreateTag(), 200f),
               new ComplexRecipe.RecipeElement(SimHashes.NaturalResin.CreateTag(), 10f)
                };
                var outputs = new ComplexRecipe.RecipeElement[2]
                {
                new ComplexRecipe.RecipeElement(SimHashes.Latex.CreateTag(), 100f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                    new ComplexRecipe.RecipeElement(SimHashes.FabricatedWood.CreateTag(), 110f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                };

                string recipeID = ComplexRecipeManager.MakeRecipeID(FabricatedWoodMakerConfig.ID, inputs, outputs);
                var recipe = new ComplexRecipe(recipeID, inputs, outputs)
                {
                    time = 40f,
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                    description = STRINGS.BUILDINGS.WOODMAKER.LATEX,
                    fabricators = new List<Tag> { TagManager.Create(FabricatedWoodMakerConfig.ID) }
                };
            }
        }
    }

