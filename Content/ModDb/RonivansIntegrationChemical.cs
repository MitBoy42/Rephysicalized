using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationChemical
    {
        private static void Postfix()
        {
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            // RonivansIntegrationChemical patches loaded

            var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.RonivansChemical");

            // Existing ExpellerPress patches...
            var ctor1 = AccessTools.Constructor(typeof(ComplexRecipe), new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) });
            if (ctor1 != null)
            {
                harmony.Patch(ctor1, postfix: new HarmonyMethod(typeof(RonivansIntegrationChemical), nameof(ExpellerPressRecipePatch)));
                harmony.Patch(ctor1, postfix: new HarmonyMethod(typeof(RonivansIntegrationChemical), nameof(AnaerobicDigesterRecipePatch)));
            }
            var ctor2 = AccessTools.Constructor(typeof(ComplexRecipe), new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) });
            if (ctor2 != null)
            {
                harmony.Patch(ctor2, postfix: new HarmonyMethod(typeof(RonivansIntegrationChemical), nameof(ExpellerPressRecipePatch)));
                harmony.Patch(ctor2, postfix: new HarmonyMethod(typeof(RonivansIntegrationChemical), nameof(AnaerobicDigesterRecipePatch)));
            }

            // BiodieselGenerator patch
            try
            {
                var biodieselType = Type.GetType("Biochemistry.Buildings.Biochemistry_BiodieselGeneratorConfig, RonivansLegacy_ChemicalProcessing");
                if (biodieselType != null)
                {
                    var method = AccessTools.Method(biodieselType, "DoPostConfigureComplete", new[] { typeof(GameObject) });
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RonivansIntegrationChemical), nameof(BiodieselGeneratorConfigPatch));
                        harmony.Patch(method, postfix: postfix);
                    }
                }
            }
            catch (System.Exception)
            {
            }
        }

        public static void ExpellerPressRecipePatch(ComplexRecipe __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.id) || !__instance.id.StartsWith("Biochemistry_ExpellerPress"))
                return;

            float inputSum = __instance.ingredients.Sum(i => i.amount);
            float outputSum = __instance.results.Sum(r => r.amount);

            if (inputSum + 0.01f >= outputSum)
                return;

            Tag plantFiberTag = new Tag("PlantFiber");
            float fiberAmount = outputSum;

            var newIngredients = new List<ComplexRecipe.RecipeElement>(__instance.ingredients);
            newIngredients.Add(new ComplexRecipe.RecipeElement(plantFiberTag, fiberAmount, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature));
            __instance.ingredients = newIngredients.ToArray();
        }

        public static void AnaerobicDigesterRecipePatch(ComplexRecipe __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.id) || !__instance.id.StartsWith("Biochemistry_AnaerobicDigester"))
                return;

            float inputSum = __instance.ingredients.Sum(i => i.amount);
            float outputSum = __instance.results.Sum(r => r.amount);

            if (inputSum + 0.01f >= outputSum)
                return;

            Tag waterTag = SimHashes.ToxicSand.CreateTag();
            float waterAmount = outputSum - inputSum;

            var newIngredients = new List<ComplexRecipe.RecipeElement>(__instance.ingredients);
            newIngredients.Add(new ComplexRecipe.RecipeElement(waterTag, waterAmount, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature));
            __instance.ingredients = newIngredients.ToArray();
        }

        public static void BiodieselGeneratorConfigPatch(GameObject go)
        {
            var biodieselType = Type.GetType("RonivansLegacy_ChemicalProcessing.Content.Scripts.BiodieselEnergyGenerator, RonivansLegacy_ChemicalProcessing");
            var energyGenType = Type.GetType("RonivansLegacy_ChemicalProcessing.Content.Scripts.BiodieselEnergyGenerator, RonivansLegacy_ChemicalProcessing");
            var energyGen = (go.GetComponent(energyGenType) as EnergyGenerator);
            var genObj = energyGen;

            const float dirtyWaterTemp = 313.15f;
            var crudTag = ModElementRegistration.CrudByproduct;

            // Mod formula
            var modFormulaField = AccessTools.Field(biodieselType, "modDieselFormula");
            var modFormula = (EnergyGenerator.Formula)modFormulaField?.GetValue(genObj);
            var newModOutputs = new List<EnergyGenerator.OutputItem>(modFormula.outputs);
            newModOutputs.Add(new EnergyGenerator.OutputItem(crudTag, 0.15572f, true, new CellOffset(0, 0), dirtyWaterTemp));
            modFormula.outputs = newModOutputs.ToArray();
            modFormulaField.SetValue(genObj, modFormula);

            // Vanilla formula
            var vanillaFormulaField = AccessTools.Field(biodieselType, "vanillaDieselFormula");
            var vanillaFormula = (EnergyGenerator.Formula)vanillaFormulaField?.GetValue(genObj);
            var newVanillaOutputs = new List<EnergyGenerator.OutputItem>(vanillaFormula.outputs);
            newVanillaOutputs.Add(new EnergyGenerator.OutputItem(crudTag, 1.92f, true, new CellOffset(0, 0), dirtyWaterTemp));
            vanillaFormula.outputs = newVanillaOutputs.ToArray();
            vanillaFormulaField.SetValue(genObj, vanillaFormula);

            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = new Tag("CrudByproduct");
            tilemaker.emitMass = 100f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.MakeTiles = false;


        }
    }
}
