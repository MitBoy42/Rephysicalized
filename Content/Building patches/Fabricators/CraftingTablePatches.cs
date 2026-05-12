using HarmonyLib;
using Rephysicalized.ModElements;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized
{
    // Replace the raw metal electrobank recipe:
    // - Iron Ore input -> Electrobank + 180 kg Rust
    // - Other metal ores -> Electrobank + 180 kg Sand
    [HarmonyPatch(typeof(CraftingTableConfig), "CreateMetalMiniVoltRecipe")]
    public static class CraftingTableConfig_CreateMetalMiniVoltRecipe_Patch
    {
        public static bool Prefix(CraftingTableConfig __instance, Tag[] inputMetals)
        {
            // Build tags for unified recipe (all metal ores incl. Iron -> Electrobank + 180 Crud)
            Tag[] allMetalTags = inputMetals ?? new Tag[0];

            // Common result: the disposable raw-metal electrobank item
            var electrobankResult = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement("DisposableElectrobank_RawMetal".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
            };

            // Unified recipe: 200 kg any metal ore -> Electrobank + 180 kg Crud
            if (allMetalTags.Length > 0)
            {
                var unifiedInput = new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(allMetalTags, 200f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, "", inheritElement: false)
                };

                var unifiedOutputs = new List<ComplexRecipe.RecipeElement>(electrobankResult)
                {
                    new ComplexRecipe.RecipeElement(ModElementRegistration.CrudByproduct.Tag, 180f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, storeElement: false)
                };

                var recipeId = ComplexRecipeManager.MakeRecipeID(
                    CraftingTableConfig.ID,
                    unifiedInput,
                    unifiedOutputs.ToArray()
                );

                var r = new ComplexRecipe(recipeId, unifiedInput, unifiedOutputs.ToArray(), DlcManager.DLC3)
                {
                    time = TUNING.INDUSTRIAL.RECIPES.STANDARD_FABRICATION_TIME * 2f,
                    description = string.Format(BUILDINGS.PREFABS.CRAFTINGTABLE.RECIPE_DESCRIPTION, MISC.TAGS.METAL, ITEMS.INDUSTRIAL_PRODUCTS.ELECTROBANK_METAL_ORE.NAME),
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                    fabricators = new List<Tag> { CraftingTableConfig.ID.ToTag() },
                    sortOrder = 0
                };
            }

            // Skip the original CreateMetalMiniVoltRecipe implementation
            return false;
        }
    }



    internal static class BionicDecraftGroupedRecipes
    {
        private static bool Prepare() => Dlc3Gate.Enabled;
        private const string CraftingTableId = CraftingTableConfig.ID; // "CraftingTable"
        private static readonly Tag PowerStationToolsTag = "PowerStationTools".ToTag();

        private static bool registered;

        // Runs after all booster prefabs for this config have been created
        [HarmonyPatch(typeof(BionicUpgradeComponentConfig), nameof(BionicUpgradeComponentConfig.CreatePrefabs))]
        private static class BionicUpgradeComponentConfig_CreatePrefabs_Postfix
        {
            // __result is not used; the static UpgradesData contains all boosters created
            public static void Postfix()
            {
                TryRegisterGroupedRecipes();
            }
        }

        private static void TryRegisterGroupedRecipes()
        {
            try
            {
                if (registered)
                    return;

                var upgrades = BionicUpgradeComponentConfig.UpgradesData;
                if (upgrades == null || upgrades.Count == 0)
                {
                    Debug.LogWarning("[BionicDecraftRecipe] UpgradesData is empty after CreatePrefabs; skipping grouped decraft registration.");
                    return;
                }

                var basicIds = BionicUpgradeComponentConfig.BASIC_BOOSTERS ?? new List<string>();

                // Group: Basic boosters (list membership + BoosterType.Basic) => 2 tools
                var basicGroup = new List<Tag>();
                var nonBasicGroup = new List<Tag>();

                foreach (var kv in upgrades)
                {
                    string id = kv.Key.ToString();
                    var data = kv.Value;
                    bool isBasicId = basicIds.Contains(id);
                    if (isBasicId && data.Booster == BionicUpgradeComponentConfig.BoosterType.Basic)
                        basicGroup.Add(kv.Key);
                    else
                        nonBasicGroup.Add(kv.Key);
                }

                // Register the two recipes
                int registeredCount = 0;
                registeredCount += RegisterCombinedRecipeForGroup(
                    permittedInputs: basicGroup.ToArray(),
                    powerToolsOut: 2,
                    recipeKeySuffix: "BasicBoosters",
                    sortOrder: 950,
                    description: STRINGS.BUILDINGS.CRAFTINGTABLE.BOOSTERDECRAFTBASIC
                ) ? 1 : 0;

                registeredCount += RegisterCombinedRecipeForGroup(
                    permittedInputs: nonBasicGroup.ToArray(),
                    powerToolsOut: 8,
                    recipeKeySuffix: "NonBasicBoosters",
                    sortOrder: 951,
                    description: STRINGS.BUILDINGS.CRAFTINGTABLE.BOOSTERDECRAFTADVANCED
                ) ? 1 : 0;

                // Mark done if at least one recipe was created
                if (registeredCount > 0)
                {
                    registered = true;
                    //     Debug.Log($"[BionicDecraftRecipe] Grouped decraft recipes registered. Basic={basicGroup.Count}, NonBasic={nonBasicGroup.Count}");
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
            }
        }

        private static bool RegisterCombinedRecipeForGroup(
            Tag[] permittedInputs,
            int powerToolsOut,
            string recipeKeySuffix,
            int sortOrder,
            string description)
        {
            if (permittedInputs == null || permittedInputs.Length == 0)
                return false;

            var input = new ComplexRecipe.RecipeElement[]
            {
                // Takes any one of the permitted boosters
                new ComplexRecipe.RecipeElement(permittedInputs, 1f)
            };

            var output = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(PowerStationToolsTag, powerToolsOut, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            string recipeId = ComplexRecipeManager.MakeRecipeID(
                CraftingTableId + "_Decraft_" + recipeKeySuffix,
                input,
                output
            );

            if (ComplexRecipeManager.Get().GetRecipe(recipeId) != null)
                return false;

            var recipe = new ComplexRecipe(recipeId, input, output)
            {
                time = TUNING.INDUSTRIAL.RECIPES.STANDARD_FABRICATION_TIME,
                description = description,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag> { CraftingTableId.ToTag() },
                requiredTech = null,
                sortOrder = sortOrder
            };

            //     Debug.Log($"[BionicDecraftRecipe] Added grouped decraft recipe '{recipeKeySuffix}': any({permittedInputs.Length}) -> {powerToolsOut}x PowerStationTools");
            return true;
        }
    }
}