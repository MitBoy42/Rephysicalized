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
        // Patch the prefab to set storeProduced = false and add WorldElementDropper
        [HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.storeProduced = true;



            //Ensure WorldElementDropper is present and configured

            var dropper = go.AddComponent<WorldElementDropper>();

            dropper.DropSolids = true;
            dropper.DropLiquids = true;
            dropper.DropGases = false;
            dropper.TargetStorage = fabricator?.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            cmp.storages = [fabricator.outStorage];


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
      new ComplexRecipe.RecipeElement("Water".ToTag(), 90f) };

                var newResults = new List<ComplexRecipe.RecipeElement>
            {
                new ComplexRecipe.RecipeElement("MushBar".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                new ComplexRecipe.RecipeElement("Mud".ToTag(), 149f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),

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
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 49f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
                Traverse.Create(recipe2).Field("results").SetValue(newResults.ToArray());
            }

            // PATCH THIRD RECIPE (Tofu)
            var recipe3 = TofuConfig.recipe;
            if (recipe3 != null)
            {
                var newResults = new List<ComplexRecipe.RecipeElement>(recipe3.results)
            {
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 49f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };
                Traverse.Create(recipe3).Field("results").SetValue(newResults.ToArray());

               
            }
        }
    }
}
