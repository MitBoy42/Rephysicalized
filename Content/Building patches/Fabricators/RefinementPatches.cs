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
                4.91f,      // massGenerationRate
                SimHashes.Naphtha,  // output element
                348.15f,            // temperatureOperation
                storeOutput: false,
                outputElementOffsety: 1f
            ));
            elementConverter.outputElements = outputs.ToArray();


        }
    }

    //Plant Pulverizer

    [HarmonyPatch(typeof(MilkPressConfig), "AddRecipes")]

    public static class MilkPressConfig_AddRecipes_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            // Markers for matching
            var dewIdField = AccessTools.Field(AccessTools.TypeByName("DewDripConfig"), "ID");
            var tagCtor = AccessTools.Constructor(typeof(Tag), new[] { typeof(string) });
            var tagCreate = AccessTools.Method(typeof(TagManager), nameof(TagManager.Create), new[] { typeof(string) });
            var tagOpImplicit = AccessTools.Method(typeof(Tag), "op_Implicit", new[] { typeof(string) });
            var recipeElemCtor2 = AccessTools.Constructor(typeof(ComplexRecipe.RecipeElement), new[] { typeof(Tag), typeof(float) });

            const int SeekingDewId = 0;
            const int SeekingTagFromDew = 1;
            const int SeekingAmountThenCtor = 2;

            int state = SeekingDewId;
            for (int i = 0; i < code.Count; i++)
            {
                var instr = code[i];

                if (state == SeekingDewId)
                {
                    if (instr.opcode == OpCodes.Ldsfld && Equals(instr.operand as FieldInfo, dewIdField))
                    {
                        state = SeekingTagFromDew;
                    }
                }
                else if (state == SeekingTagFromDew)
                {
                    bool tagBuiltFromDew =
                        (instr.opcode == OpCodes.Newobj && Equals(instr.operand as ConstructorInfo, tagCtor)) ||
                        (instr.opcode == OpCodes.Call && (Equals(instr.operand as MethodInfo, tagCreate) || Equals(instr.operand as MethodInfo, tagOpImplicit)));

                    if (tagBuiltFromDew)
                    {
                        state = SeekingAmountThenCtor;
                    }
                    else
                    {
                        // If something unexpected, reset to seek next DewDrip ID
                        state = SeekingDewId;
                    }
                }
                else if (state == SeekingAmountThenCtor)
                {
                    // Expect: ldc.r4 2 -> newobj ComplexRecipe.RecipeElement(Tag,float)
                    if (i + 1 < code.Count &&
                        code[i].opcode == OpCodes.Ldc_R4 &&
                        code[i].operand is float f &&
                        f == 2f &&
                        code[i + 1].opcode == OpCodes.Newobj &&
                        Equals(code[i + 1].operand as ConstructorInfo, recipeElemCtor2))
                    {
                        code[i].operand = 20f;
                        break;
                    }
                    state = SeekingDewId;
                }
            }

            return code;
        }
    }




    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class MilkPress_AddBalmLilyPhytoOilRecipe_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var fabricatorTag = TagManager.Create("MilkPress");

            var input = new ComplexRecipe.RecipeElement[]
            {
            new ComplexRecipe.RecipeElement((Tag)"SwampLilyFlower", 20f),
            new ComplexRecipe.RecipeElement(SimHashes.Mud.CreateTag(), 80f),
            };

            var output = new ComplexRecipe.RecipeElement[]
            {

            new ComplexRecipe.RecipeElement(
                SimHashes.PhytoOil.CreateTag(),
                60f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                true ),
               new ComplexRecipe.RecipeElement(
                SimHashes.Dirt.CreateTag(),
                40f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature
            )
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID("MilkPress", input, output);

            var recipe = new ComplexRecipe(recipeId, input, output)
            {
                time = 40f,
                description = STRINGS.BUILDINGS.MILKPRESS.BALMLILY_PHYTOOIL,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                fabricators = new List<Tag> { fabricatorTag },
                sortOrder = 21,
       
            };
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
            0.003f,      // massGenerationRate
           SimHashes.GoldAmalgam,  // output element
            1.0f,            // temperatureOperation
            storeOutput: true, // store the output
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
    ComplexRecipe.RecipeElement[] recipeElementArray2 = new ComplexRecipe.RecipeElement[3]  // <-- now size 3
    {
        new ComplexRecipe.RecipeElement(ChlorinatorConfig.BLEACH_STONE_TAG, 10f),
        new ComplexRecipe.RecipeElement(ChlorinatorConfig.SAND_TAG, 20f),
        new ComplexRecipe.RecipeElement(SimHashes.GoldAmalgam.CreateTag(), 0.5f) // <-- ADD HERE
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
}


 
    // MilkPress: switch Water to Brine for specified recipes and adjust ColdWheatSeed amount
    [HarmonyPatch(typeof(MilkPressConfig), nameof(MilkPressConfig.AddRecipes))]
    public static class MilkPressConfig_AddRecipes_Postfix
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {
            // Helper to swap water -> brine and optionally adjust amount
            void SwapWaterToBrine(ComplexRecipe recipe, float? overrideBrineAmount = null)
            {
                if (recipe == null || recipe.ingredients == null)
                    return;

                var ingredients = recipe.ingredients.ToArray();
                for (int i = 0; i < ingredients.Length; i++)
                {
                    var ing = ingredients[i];
                    if (ing.material == SimHashes.Water.CreateTag())
                    {
                        float amount = overrideBrineAmount.HasValue ? overrideBrineAmount.Value : ing.amount;
                        ingredients[i] = new ComplexRecipe.RecipeElement(SimHashes.Brine.CreateTag(), amount);
                    }
                }
                recipe.ingredients = ingredients;
            }

            // Reuse common output definition used by MilkPress
            var outputMilk = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Milk.CreateTag(), 20f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, true)
            };

            // 1) ColdWheatSeed(10) + Water(15) -> Milk(20)
            var input1 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement((Tag)"ColdWheatSeed", 10f),
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 15f)
            };
            string id1 = ComplexRecipeManager.MakeRecipeID("MilkPress", input1, outputMilk);
            var r1 = ComplexRecipeManager.Get().GetRecipe(id1);
            SwapWaterToBrine(r1, 10f); // set Brine to 10

            // 2) SpiceNut(3) + Water(17) -> Milk(20)
            var input2 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement((Tag)SpiceNutConfig.ID, 3f),
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 17f)
            };
            string id2 = ComplexRecipeManager.MakeRecipeID("MilkPress", input2, outputMilk);
            var r2 = ComplexRecipeManager.Get().GetRecipe(id2);
            SwapWaterToBrine(r2); // keep amount 17

            // 3) BeanPlantSeed(2) + Water(18) -> Milk(20)
            var input3 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement((Tag)"BeanPlantSeed", 2f),
                new ComplexRecipe.RecipeElement(SimHashes.Water.CreateTag(), 18f)
            };
            string id3 = ComplexRecipeManager.MakeRecipeID("MilkPress", input3, outputMilk);
            var r3 = ComplexRecipeManager.Get().GetRecipe(id3);
            SwapWaterToBrine(r3); // keep amount 18
        }
    }

}