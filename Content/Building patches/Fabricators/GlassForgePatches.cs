using HarmonyLib;
using Rephysicalized.ModElements;
using System.Linq;
using UnityEngine;
namespace Rephysicalized.Content.BuildingPatches
{



    [HarmonyPatch(typeof(GlassForgeConfig), nameof(GlassForgeConfig.ConfigureBuildingTemplate))]
    public static class GlassForgeConfigPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            ComplexFabricator fabricator = go.AddOrGet<ComplexFabricator>();
            fabricator.heatedTemperature = 273.15f + 80f;
            //fabricator.storeProduced = false;

          
            var dropper = go.AddComponent<ElementTileMaker>();
            dropper.storage = fabricator.outStorage;
            dropper.emitTag = ModElementRegistration.CrudByproduct.Tag;
            dropper.emitMass = 75f;
            dropper.emitOffset = new Vector3(0f, 0f);
            dropper.MakeTiles = false;


        }
    }
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
                // Add Crud (solid, dropped)
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
}