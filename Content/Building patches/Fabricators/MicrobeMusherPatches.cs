using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static STRINGS.BUILDING.STATUSITEMS;




namespace Rephysicalized
{


    [HarmonyPatch]
    public static class MicrobeMusherElementDropperPatch
    {
        [HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            if (fabricator == null)
                return;

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
            // Only for Microbe Musher
            if (__instance.PrefabID().Name == MicrobeMusherConfig.ID)
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

    [HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureRecipes")]
    public static class MicrobeMusherConfig_ConfigureRecipes_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // PATCH FIRST RECIPE (MushBar)
            var recipe = MushBarConfig.recipe;
            if (recipe != null)
            {
                var newIngredients = new List<ComplexRecipe.RecipeElement>
                {   new ComplexRecipe.RecipeElement("Dirt".ToTag(), 60f),
      new ComplexRecipe.RecipeElement(new Tag[2]
      {
        SimHashes.Water.CreateTag(),
        SimHashes.Mucus.CreateTag()
      }, 90f) };

                var newResults = new List<ComplexRecipe.RecipeElement>(recipe.results)
            {
                new ComplexRecipe.RecipeElement("Mud".ToTag(), 145f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),

            };
                Traverse.Create(recipe).Field("results").SetValue(newResults.ToArray());
                Traverse.Create(recipe).Field("ingredients").SetValue(newIngredients.ToArray());
            }

            // PATCH SECOND RECIPE (BasicPlantBar / Liceloaf)
            var recipe2 = BasicPlantBarConfig.recipe;
            if (recipe2 != null)
            {
                // Copy the results and add 50f of liquid Water
                var newResults = new List<ComplexRecipe.RecipeElement>(recipe2.results)
            {
                new ComplexRecipe.RecipeElement(SimHashes.DirtyWater.CreateTag(), 45f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
                Traverse.Create(recipe2).Field("results").SetValue(newResults.ToArray());
            }

            // PATCH THIRD RECIPE (Tofu)
            var recipe3 = TofuConfig.recipe;
            if (recipe3 != null)
            {
                var newResults = new List<ComplexRecipe.RecipeElement>(recipe3.results)
            {
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 45f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
                Traverse.Create(recipe3).Field("results").SetValue(newResults.ToArray());


            }
        }
    }





    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class MicrobeMusherNutrientBar
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var fabricatorTag = TagManager.Create("CookingStation");

            var inputNutrient = new ComplexRecipe.RecipeElement[2]
            {

            new ComplexRecipe.RecipeElement(SimHashes.Sucrose.CreateTag(), 4f),
            new ComplexRecipe.RecipeElement(SimHashes.Tallow.CreateTag(), 1f),
            };

            var outputNutrient = new ComplexRecipe.RecipeElement[1]
            {
            new ComplexRecipe.RecipeElement( "FieldRation".ToTag(),
                2f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            ),
         
            };

            string recipeIdNutrient = ComplexRecipeManager.MakeRecipeID("CookingStation", inputNutrient, outputNutrient);

            var recipeNutrient = new ComplexRecipe(recipeIdNutrient, inputNutrient, outputNutrient)
            {
                time = 40f,
                description = STRINGS.BUILDINGS.MICROBEMUSHER.NUTRIENTBAR,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 6,

            }; 
        } 
    }
      
}
