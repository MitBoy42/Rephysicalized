using HarmonyLib;
using Rephysicalized.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized
{

    [HarmonyPatch]
    public static class DiamondPressElementDropperPatch
    {
        // Patch the prefab to set storeProduced = false and add WorldElementDropper
        [HarmonyPatch(typeof(DiamondPressConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.storeProduced = true;



            //Ensure WorldElementDropper is present and configured

            var dropper = go.AddComponent<WorldElementDropper>();

            dropper.DropSolids = true;
            dropper.DropLiquids = true;
            dropper.DropGases = true;
            dropper.TargetStorage = fabricator?.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            cmp.storages = [fabricator.outStorage];


        }
        [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
        [HarmonyPostfix]
        public static void DropAfterRecipe(ComplexFabricator __instance)
        {
        
            if (__instance.PrefabID().Name == DiamondPressConfig.ID)
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

    // Diamond Press: add Depleted Uranium -> Enriched Uranium recipe
    [HarmonyPatch(typeof(DiamondPressConfig), nameof(DiamondPressConfig.ConfigureBuildingTemplate))]
    public static class DiamondPressConfig_AddUraniumRecipe_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var inputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(SimHashes.DepletedUranium.CreateTag(), 2f)
            };
            var outputs = new ComplexRecipe.RecipeElement[1]
            {
                new ComplexRecipe.RecipeElement(SimHashes.EnrichedUranium.CreateTag(), 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
            };

            string recipeID = ComplexRecipeManager.MakeRecipeID(DiamondPressConfig.ID, inputs, outputs);
            var recipe = new ComplexRecipe(recipeID, inputs, outputs, 1000, 0)
            {
                time = 80f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = STRINGS.BUILDINGS.DIAMONDPRESS.DEPLETEDURANIUM_ENRICHEDURANIUM,
                fabricators = new List<Tag> { TagManager.Create(DiamondPressConfig.ID) }
            };
        }
    }

    [HarmonyPatch(typeof(DiamondPressConfig), nameof(DiamondPressConfig.ConfigureBuildingTemplate))]
   
    
    public static class DiamondPressConfig_AddPinkRockRecipe_Patch
    {
        private static bool Prepare() => Dlc2Gate.Enabled;
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Inputs
            var inputs = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(SimHashes.Granite.CreateTag(), 18f),
                new ComplexRecipe.RecipeElement(SimHashes.SaltWater.CreateTag(), 100f)
            };

            // Outputs
            var outputs = new ComplexRecipe.RecipeElement[]
            {
                // PinkRock item (prefab id/tag) at 1 kg
                new ComplexRecipe.RecipeElement(TagManager.Create("PinkRock"), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated),
                // Return the 100 kg of SaltWater, heated by the operation
                new ComplexRecipe.RecipeElement(SimHashes.Steam.CreateTag(), 93f, ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
            };

            // Deterministic recipe ID bound to DiamondPress
            string recipeID = ComplexRecipeManager.MakeRecipeID(DiamondPressConfig.ID, inputs, outputs);

            var recipe = new ComplexRecipe(recipeID, inputs, outputs, 1000, 0)
            {
                time = 80f,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.IngredientToResult,
                description = STRINGS.BUILDINGS.DIAMONDPRESS.LUMENQUARTZ,
                fabricators = new List<Tag> { TagManager.Create(DiamondPressConfig.ID) }
            };
        }
    }

    // Sets the Diamond Press fabricator heatedTemperature to 320°C (593.15 K)
    [HarmonyPatch(typeof(DiamondPressConfig), nameof(DiamondPressConfig.ConfigureBuildingTemplate))]
    internal static class DiamondPressHeatedTempPatch
    {
        private const float CelsiusToKelvin = 273.15f;
        private const float HeatedTempC = 320f;
        private const float HeatedTempK = HeatedTempC + CelsiusToKelvin; // 593.15 K

        private static void Postfix(GameObject go, Tag prefab_tag)
        {
            try
            {
                var fabricator = go != null ? go.GetComponent<ComplexFabricator>() : null;
               
                    fabricator.heatedTemperature = HeatedTempK;
               
            }
            catch (Exception e)
            {
            }
        }
    }

    // Add and configure the root RadiationEmitter on the Diamond Press prefab.
    [HarmonyPatch(typeof(DiamondPressConfig), nameof(DiamondPressConfig.ConfigureBuildingTemplate))]
    internal static class DiamondPress_AddRootEmitter_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            var emitter = go.AddOrGet<RadiationEmitter>();
            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRads = 1000f;
            emitter.emitRate = 0f; 
            emitter.emissionOffset = new Vector3(0f, 2f);
            emitter.SetEmitting(false);
            emitter.Refresh();
        }
    }

    // Ensure the emitter is OFF when the Diamond Press spawns.
    [HarmonyPatch(typeof(ComplexFabricator), nameof(ComplexFabricator.OnSpawn))]
    internal static class DiamondPress_Fabricator_OnSpawn_EnsureEmitterOff
    {
        public static void Postfix(ComplexFabricator __instance)
        {
            var kpid = __instance != null ? __instance.GetComponent<KPrefabID>() : null;
            if (kpid == null || kpid.PrefabID().Name != DiamondPressConfig.ID) return;

            var emitter = __instance.GetComponent<RadiationEmitter>();
 

            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRads = 1000f;
            emitter.emitRate = 0f;
            emitter.emissionOffset = new Vector3(0f, 2f);
            emitter.SetEmitting(false);
            emitter.Refresh();
        }
    }

    // Toggle emission via the fabricator state machine (no polling).
    // Emission turns on only while a recipe that outputs EnrichedUranium is being worked.
    [HarmonyPatch(typeof(ComplexFabricatorSM.States), "InitializeStates")]
    internal static class DiamondPress_FabricatorSM_RadsPatch
    {
        public static void Postfix(ComplexFabricatorSM.States __instance, ref StateMachine.BaseState default_state)
        {
            __instance.operating.working_pre.Enter(smi => ToggleForDiamondPress(smi, turnOnIfTarget: true));
            __instance.operating.working_pst.Enter(smi => ToggleForDiamondPress(smi, turnOnIfTarget: false));
            __instance.operating.working_pst_complete.Enter(smi => ToggleForDiamondPress(smi, turnOnIfTarget: false));
            try { __instance.idle.Enter(smi => ToggleForDiamondPress(smi, turnOnIfTarget: false)); } catch { /* idle may not exist on some branches */ }
        }

        private static void ToggleForDiamondPress(ComplexFabricatorSM.StatesInstance smi, bool turnOnIfTarget)
        {
            if (smi == null || smi.master == null) return;

            var kpid = smi.master.GetComponent<KPrefabID>();
            if (kpid == null || kpid.PrefabID().Name != DiamondPressConfig.ID) return;

            var emitter = kpid.gameObject.GetComponent<RadiationEmitter>();
            if (emitter == null) return;

      

            bool shouldEmit = false;
            if (turnOnIfTarget)
            {
                var fab = smi.master.GetComponent<ComplexFabricator>();
                var order = fab != null ? fab.CurrentWorkingOrder : null;
                if (order != null && !string.IsNullOrEmpty(order.id))
                {
                    var recipe = ComplexRecipeManager.Get()?.GetRecipe(order.id);
                    if (recipe != null && RecipeOutputsContainEnrichedUranium(recipe))
                        shouldEmit = true;
                }
            }

            emitter.SetEmitting(shouldEmit);
            emitter.Refresh();
        }

        // Outputs-only detection: EnrichedUranium present in results.
        private static bool RecipeOutputsContainEnrichedUranium(ComplexRecipe recipe)
        {
            var results = GetResultsCompat(recipe);
            if (results == null || results.Length == 0) return false;

            Tag enriched = SimHashes.EnrichedUranium.CreateTag();
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].material == enriched)
                    return true;
            }
            return false;
        }

        // Access results across branches (field or property)
        private static ComplexRecipe.RecipeElement[] GetResultsCompat(ComplexRecipe recipe)
        {
            if (recipe == null) return null;
            var t = typeof(ComplexRecipe);

            var f = AccessTools.Field(t, "results") ?? AccessTools.Field(t, "Results");
            if (f != null) return f.GetValue(recipe) as ComplexRecipe.RecipeElement[];

            var p = AccessTools.Property(t, "results") ?? AccessTools.Property(t, "Results");
            if (p != null) return p.GetValue(recipe, null) as ComplexRecipe.RecipeElement[];

            return null;
        }
    }


}
