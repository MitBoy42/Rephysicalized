using HarmonyLib;
using Rephysicalized.ModElements;
using STRINGS;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Rephysicalized.Content.BuildingPatches
{



    [HarmonyPatch(typeof(ComplexRecipe))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
    public static class ComplexRecipe_GlassForgeResult_Postfix
    {
        static void Postfix(ComplexRecipe __instance)
        {

            var inputs = __instance.ingredients ?? System.Array.Empty<ComplexRecipe.RecipeElement>();
            var results = __instance.results ?? System.Array.Empty<ComplexRecipe.RecipeElement>();

            if (inputs.Length == 1 && inputs[0].material == SimHashes.Sand.CreateTag() && inputs[0].amount == 100f &&
                results.Length >= 1 && results.Any(r => r.material == SimHashes.MoltenGlass.CreateTag()))
            {
            
                if (!results.Any(r => r.material == ModElementRegistration.CrudByproduct.Tag))
                {
                    __instance.results = results
                        .Concat(new[]
                        {
                        new ComplexRecipe.RecipeElement(
                            ModElementRegistration.CrudByproduct.Tag,
                            75f,
                            ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                        })
                        .ToArray();
                }

                for (int i = 0; i < __instance.results.Length; i++)
                {
                    ref var re = ref __instance.results[i];
                    if (re.material == SimHashes.MoltenGlass.CreateTag())
                    {
                        re.temperatureOperation = ComplexRecipe.RecipeElement.TemperatureOperation.Melted;
                        re.storeElement = true; // store in outStorage as liquid
                    }
                }

            }
        }
    }
 
    [HarmonyPatch(typeof(GlassForgeConfig), nameof(GlassForgeConfig.ConfigureBuildingTemplate))]
    public static class GlassForge_Silicon
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.heatedTemperature = 273.15f + 80f;
            fabricator.storeProduced = false;

            var inputs = new ComplexRecipe.RecipeElement[2]
            {
                new ComplexRecipe.RecipeElement( new Tag[3] { SimHashes.Granite.CreateTag(), SimHashes.Basalt.CreateTag(), SimHashes.IgneousRock.CreateTag() }, 100f),
               new ComplexRecipe.RecipeElement( new Tag[3] { SimHashes.Carbon.CreateTag(), SimHashes.WoodLog.CreateTag(), SimHashes.FabricatedWood.CreateTag() }, 50f)
            };
            var outputs = new ComplexRecipe.RecipeElement[3]
            {
                new ComplexRecipe.RecipeElement(ModElementRegistration.MoltenSilicon.Tag, 25f, ComplexRecipe.RecipeElement.TemperatureOperation.Melted),
                    new ComplexRecipe.RecipeElement(SimHashes.CarbonDioxide.CreateTag(), 50f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, true),
                      new ComplexRecipe.RecipeElement(ModElementRegistration.CrudByproduct.Tag, 75f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID(GlassForgeConfig.ID, inputs, outputs);
            var recipe = new ComplexRecipe(recipeID, inputs, outputs)
            {
                time = 60f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = STRINGS.BUILDINGS.GLASSFORGE.SILICON,
                fabricators = new List<Tag> { TagManager.Create(GlassForgeConfig.ID) }
            };
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
    public static class GlassForge_CarbonDioxideTemperature
    {
        public static void Postfix(ComplexFabricator __instance, List<GameObject> __result)
        {
            if (__instance.PrefabID().Name != GlassForgeConfig.ID)
                return;

            bool hasCarbonDioxide = false;
            foreach (var product in __result)
            {
                var primaryElement = product.GetComponent<PrimaryElement>();
                if (primaryElement != null && primaryElement.Element.id == SimHashes.CarbonDioxide)
                {
                    primaryElement.Temperature = 1873f;
                    hasCarbonDioxide = true;
                }
            }

            if (hasCarbonDioxide && __instance.outStorage != null)
            {
                var carbonDioxideTag = SimHashes.CarbonDioxide.CreateTag();
                var amount = __instance.outStorage.GetAmountAvailable(carbonDioxideTag);
                if (amount > 0f)
                    __instance.outStorage.DropSome(carbonDioxideTag, amount, ventGas: true);
            }
        }
    }
}