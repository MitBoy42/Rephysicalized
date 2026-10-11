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

            BuildingElementEmitter buildingElementEmitter = go.AddOrGet<BuildingElementEmitter>();
            buildingElementEmitter.emitRate = 0.25f;
        }
    }
}

