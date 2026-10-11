using HarmonyLib;
using HarmonyLib;
using Rephysicalized;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using YamlDotNet.Core;

namespace Rephysicalized
{




    // Patch FertilizerMaker to request and consume the shared tag instead of hard-coded Dirt
    [HarmonyPatch(typeof(FertilizerMakerConfig), nameof(FertilizerMakerConfig.ConfigureBuildingTemplate))]
    public static class FertilizerMakerConfig_SolidInput_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var dirtTag = SimHashes.Dirt.CreateTag();

            // Update manual delivery to accept the shared tag (either Dirt or Ash)
            var deliveries = go.GetComponents<ManualDeliveryKG>();
            if (deliveries != null)
            {
                foreach (var md in deliveries)
                {
                    if (md != null && md.RequestedItemTag == dirtTag)
                    {
                        md.RequestedItemTag = ModTags.RichSoil;
                    }
                }
            }

            // Update ElementConverter to consume the shared tag instead of specifically Dirt
            var converter = go.GetComponent<ElementConverter>();
            if (converter != null && converter.consumedElements != null)
            {
                var consumed = converter.consumedElements.ToList();
                for (int i = 0; i < consumed.Count; i++)
                {
                    if (consumed[i].Tag == dirtTag)
                    {
                        consumed[i] = new ElementConverter.ConsumedElement(
                            ModTags.RichSoil,
                            consumed[i].MassConsumptionRate);
                    }
                }
                converter.consumedElements = consumed.ToArray();
            }
        }
    }




    [HarmonyPatch(typeof(WaterPurifierConfig), nameof(WaterPurifierConfig.ConfigureBuildingTemplate))]
    public static class WaterPurifierConfig_ConfigureBuildingTemplate_Postfix
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var converter = go.GetComponent<ElementConverter>();
            if (converter != null)
            {
        
       
                converter.consumedElements = new ElementConverter.ConsumedElement[]
                {
                    new ElementConverter.ConsumedElement(new Tag("Filter"), 1f),
                    new ElementConverter.ConsumedElement(new Tag("DirtyWater"), 5f)
                };

                converter.outputElements = new ElementConverter.OutputElement[]
                {
                    new ElementConverter.OutputElement(4.95f, SimHashes.Water, 0.0f, storeOutput: true, diseaseWeight: 0.75f),
                    new ElementConverter.OutputElement(0.20f, SimHashes.ToxicSand, 0.0f, storeOutput: true, diseaseWeight: 0.25f),
                    new ElementConverter.OutputElement(0.8f, ModElementRegistration.CrudByproduct, 0.0f, storeOutput: true)
                };
            }

            // Add an ElementDropper for CrudByproduct identical to the ToxicSand dropper
            // Avoid duplicates if another patch or reload adds it again.
            Tag crudTag = new Tag("CrudByproduct");
            bool hasCrudDropper = go.GetComponents<ElementDropper>()
                                    .Any(d => d != null && d.emitTag == crudTag);

            if (!hasCrudDropper)
            {
                var crudDropper = go.AddComponent<ElementDropper>();
                crudDropper.emitMass = 50f;
                crudDropper.emitTag = crudTag;
                crudDropper.emitOffset = new Vector3(1.0f, 1f, 0.0f);
            }
        }
    }




    //Oil Refinery        
    [HarmonyPatch(typeof(OilRefineryConfig), "ConfigureBuildingTemplate")]
    public static class OilRefineryConfig_NaphaOutputPatch
    {


        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure ElementConverter exists, or add if missing
            var elementConverter = go.GetComponent<ElementConverter>();
            var outputs = elementConverter.outputElements?.ToList() ?? new System.Collections.Generic.List<ElementConverter.OutputElement>();

            // Always add/overwrite
            outputs.Add(new ElementConverter.OutputElement(
                4.91f,     
                SimHashes.Naphtha, 
                348.15f,      
                storeOutput: false,
                outputElementOffsety: 1f
            ));
            elementConverter.outputElements = outputs.ToArray();


        }
    }

    // OxyliteRefineryConfig: Patch to add additional output of GoldAmalgam
    [HarmonyPatch(typeof(OxyliteRefineryConfig), "ConfigureBuildingTemplate")]
public static class OxyliteRefineryConfig_GoldAmalgamOutputPatch
{
    public static void Postfix(GameObject go, Tag prefab_tag)
    {
        // Ensure ElementConverter exists, or add if missing
        var elementConverter = go.GetComponent<ElementConverter>();
        if (elementConverter == null)
        {
            elementConverter = go.AddComponent<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[0];
        }

        var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();

        // Add GoldAmalgam output
        outputs.Add(new ElementConverter.OutputElement(
            0.003f,    
           SimHashes.GoldAmalgam, 
            1.0f,       
            storeOutput: true, 
            outputElementOffsety: 1f
        ));

        elementConverter.outputElements = outputs.ToArray();

        // Ensure ElementDropper exists and is set up to drop GoldAmalgam
        var elementDropper = go.GetComponent<ElementDropper>();
        if (elementDropper == null)
        {
            elementDropper = go.AddComponent<ElementDropper>();
        }
        elementDropper.emitTag = new Tag("GoldAmalgam");
        elementDropper.emitMass = 5f;
        elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
    }
}

//Bleach Stone Hopper 
[HarmonyPatch(typeof(ChlorinatorConfig), nameof(ChlorinatorConfig.ConfigureRecipes))]
public class ChlorinatorConfig_ConfigureRecipes_Patch
{


    ComplexRecipe.RecipeElement[] recipeElementArray1 = new ComplexRecipe.RecipeElement[2]
    {
        new ComplexRecipe.RecipeElement(SimHashes.Salt.CreateTag(), 30f),
        new ComplexRecipe.RecipeElement(SimHashes.Gold.CreateTag(), 0.5f)
    };
    ComplexRecipe.RecipeElement[] recipeElementArray2 = new ComplexRecipe.RecipeElement[3]  
    {
        new ComplexRecipe.RecipeElement(ChlorinatorConfig.BLEACH_STONE_TAG, 10f),
        new ComplexRecipe.RecipeElement(ChlorinatorConfig.SAND_TAG, 20f),
        new ComplexRecipe.RecipeElement(SimHashes.GoldAmalgam.CreateTag(), 0.5f) 
    };

}
[HarmonyPatch(typeof(Chlorinator.StatesInstance), "TryEmit", new System.Type[0])]
public static class Chlorinator_TryEmit_GoldAmalgamPatch
{
    static void Postfix(Chlorinator.StatesInstance __instance)
    {
        // Find a PE from the input storage to get temperature/disease
        // We'll use the primaryOreTag as representative
        Tag sourceTag = __instance.def.primaryOreTag;

        GameObject sample = __instance.storage.FindFirst(sourceTag);
        if (sample == null) return;
        PrimaryElement samplePE = sample.GetComponent<PrimaryElement>();
        if (samplePE == null) return;

        // Prepare properties
        Substance goldAmalgamSub = ElementLoader.FindElementByHash(SimHashes.GoldAmalgam).substance;
        float mass = 0.125f;
        float temperature = samplePE.Temperature;
        byte diseaseIdx = samplePE.DiseaseIdx;
        int diseaseCount = samplePE.DiseaseCount;

        // Drop at the same position as usual (center of building + offset)
        Vector3 pos = __instance.smi.gameObject.transform.position + __instance.def.offset;

        // No velocity/position config required, just drop at position
        goldAmalgamSub.SpawnResource(pos, mass, temperature, diseaseIdx, diseaseCount);
    }
}}
