﻿using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;


namespace Rephysicalized.Content.Building_patches.Fabricators
{


    [HarmonyPatch(typeof(RubberMakerConfig), nameof(RubberMakerConfig.ConfigureBuildingTemplate))]
    internal static class RubberMaker_LatexInput_AdjustPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var buildingElementEmitter = go.GetComponent<BuildingElementEmitter>();
            if (buildingElementEmitter != null)
                UnityEngine.Object.DestroyImmediate(buildingElementEmitter);

            var fabricator = go.GetComponent<ComplexFabricator>();
            if (fabricator != null)
            {
                fabricator.storeProduced = true;

                var dropper = go.GetComponent<WorldElementDropper>() ?? go.AddComponent<WorldElementDropper>();
                dropper.DropSolids = false;
                dropper.DropLiquids = false;
                dropper.DropGases = true;
                dropper.TargetStorage = fabricator.outStorage;

                var dropAllWorkable = go.GetComponent<DropAllWorkable>();
                if (dropAllWorkable != null)
                    dropAllWorkable.storages = new Storage[] { fabricator.outStorage };
            }

            var crm = ComplexRecipeManager.Get();

            var inputs = new[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Latex.CreateTag(), 100f)
            };
            var outputs = new[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Rubber.CreateTag(), 100f)
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(RubberMakerConfig.ID, inputs, outputs);
            var recipe = crm?.GetRecipe(recipeId);
            if (recipe == null)
                return;

            recipe.ingredients = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(new Tag[2] { SimHashes.Resin.CreateTag(), SimHashes.Latex.CreateTag() } , 110f) 
            };

            recipe.results = new ComplexRecipe.RecipeElement[]
       {
                new ComplexRecipe.RecipeElement(SimHashes.Rubber.CreateTag(), 100f),
         new ComplexRecipe.RecipeElement(SimHashes.CarbonDioxide.CreateTag(), 10f)
       };
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
    internal static class RubberMaker_DropCarbonDioxidePatch
    {
        public static void Postfix(ComplexFabricator __instance)
        {
            if (__instance.PrefabID().Name != RubberMakerConfig.ID)
                return;

            var dropper = __instance.GetComponent<WorldElementDropper>();
            var storage = __instance.outStorage;
            if (dropper != null && storage != null)
                storage.DropAll();
        }
    }
}

