using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{

    [HarmonyPatch]
    public static class SupermaterialRefineryElementDropperPatch
    {
        // Patch the prefab to set storeProduced = false and add WorldElementDropper
        [HarmonyPatch(typeof(SupermaterialRefineryConfig), "ConfigureBuildingTemplate")]
        [HarmonyPostfix]
        public static void PatchPrefab(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            fabricator.storeProduced = true;



            //Ensure WorldElementDropper is present and configured

            var dropper = go.AddComponent<WorldElementDropper>();

            dropper.DropSolids = true;
            dropper.DropLiquids = false;
            dropper.DropGases = true;
            dropper.TargetStorage = fabricator?.outStorage;

            var cmp = go.GetComponent<DropAllWorkable>();
            cmp.storages = [fabricator.outStorage];


        }
        [HarmonyPatch(typeof(ComplexFabricator), "SpawnOrderProduct")]
        [HarmonyPostfix]
        public static void DropAfterRecipe(ComplexFabricator __instance)
        {
            // Only for Microbe Musher
            if (__instance.PrefabID().Name == SupermaterialRefineryConfig.ID)
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

    // Supermaterial Refinery: add 116 kg Depleted Uranium -> 100 kg Lead + 16 kg Hydrogen recipe.
    [HarmonyPatch(typeof(SupermaterialRefineryConfig), nameof(SupermaterialRefineryConfig.ConfigureBuildingTemplate))]
    public static class SupermaterialRefineryConfig_AddDUtoLeadRecipe_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            try
            {
                // Inputs/outputs
                var inputs = new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(SimHashes.DepletedUranium.CreateTag(), 116f)
                };

                var outputs = new ComplexRecipe.RecipeElement[]
                {
                    new ComplexRecipe.RecipeElement(
                        SimHashes.Lead.CreateTag(),
                        100f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.Heated
                    ),
                    new ComplexRecipe.RecipeElement(
                        SimHashes.Hydrogen.CreateTag(),
                        16f,
                        ComplexRecipe.RecipeElement.TemperatureOperation.Heated
                    )
                };

                // Deterministic recipe ID
                string recipeID = ComplexRecipeManager.MakeRecipeID(SupermaterialRefineryConfig.ID, inputs, outputs);

                // Avoid duplicate registration if another mod or reload already added it
                var crm = ComplexRecipeManager.Get();
                if (crm != null && crm.GetRecipe(recipeID) != null)
                    return;

                // Create and register the recipe (mirror style from base config)
                var recipe = new ComplexRecipe(recipeID, inputs, outputs)
                {
                    time = 80f, // same as other refinery recipes
                    description = STRINGS.BUILDINGS.SUPERMATERIALREFINERY.DU_TO_LEAD,
                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                    fabricators = new List<Tag> { TagManager.Create(SupermaterialRefineryConfig.ID) }
                    // requiredTech = null; // add tech gate if needed
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Rephysicalized] Failed to add DU->Lead recipe: {ex}");
            }
        }
    }



    // Configure the root RadiationEmitter on the Supermaterial Refinery prefab.
    [HarmonyPatch(typeof(SupermaterialRefineryConfig), nameof(SupermaterialRefineryConfig.ConfigureBuildingTemplate))]
    internal static class SupermaterialRefinery_AddRootEmitter_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            var emitter = go.AddOrGet<RadiationEmitter>();
            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRads = 1000f;
            emitter.emitRate = 0f;                 // steady emission (no pulses)
            emitter.emissionOffset = new Vector3(1f, 3f);
            emitter.SetEmitting(false);
            emitter.Refresh();
        }
    }

    // Assert the emitter is OFF when the fabricator spawns (covers any default-on edge cases).
    [HarmonyPatch(typeof(ComplexFabricator), nameof(ComplexFabricator.OnSpawn))]
    internal static class ComplexFabricator_OnSpawn_EnsureEmitterOff
    {
        public static void Postfix(ComplexFabricator __instance)
        {
            var kpid = __instance != null ? __instance.GetComponent<KPrefabID>() : null;
            if (kpid == null || kpid.PrefabID().Name != SupermaterialRefineryConfig.ID) return;

            var emitter = __instance.GetComponent<RadiationEmitter>();
            if (emitter == null) return;

            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRads = 1000f;
            emitter.emitRate = 0f;
            emitter.emissionOffset = new Vector3(1f, 3f);
            emitter.SetEmitting(false);
            emitter.Refresh();
        }
    }

    // Inject toggles into ComplexFabricatorSM states so emission follows working states without polling.
    [HarmonyPatch(typeof(ComplexFabricatorSM.States), "InitializeStates")]
    internal static class ComplexFabricatorSM_InitializeStates_RadsPatch
    {
        public static void Postfix(ComplexFabricatorSM.States __instance, ref StateMachine.BaseState default_state)
        {
            __instance.operating.working_pre.Enter(smi => ToggleForSuperRefinery(smi, turnOnIfTarget: true));
            __instance.operating.working_pst.Enter(smi => ToggleForSuperRefinery(smi, turnOnIfTarget: false));
            __instance.operating.working_pst_complete.Enter(smi => ToggleForSuperRefinery(smi, turnOnIfTarget: false));
            try { __instance.idle.Enter(smi => ToggleForSuperRefinery(smi, turnOnIfTarget: false)); } catch { /* optional on some branches */ }
        }

        private static void ToggleForSuperRefinery(ComplexFabricatorSM.StatesInstance smi, bool turnOnIfTarget)
        {
            if (smi == null || smi.master == null) return;

            var kpid = smi.master.GetComponent<KPrefabID>();
            if (kpid == null || kpid.PrefabID().Name != SupermaterialRefineryConfig.ID) return;

            var emitter = kpid.gameObject.GetComponent<RadiationEmitter>();
            if (emitter == null) return;

            // Ensure emitter parameters are consistent
            emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            emitter.radiusProportionalToRads = false;
            emitter.emitRads = 1000f;
            emitter.emitRate = 0f;
            emitter.emissionOffset = new Vector3(1f, 3f);

            bool shouldEmit = false;
            if (turnOnIfTarget)
            {
                var fab = smi.master.GetComponent<ComplexFabricator>();
                var order = fab != null ? fab.CurrentWorkingOrder : null;
                if (order != null && !string.IsNullOrEmpty(order.id))
                {
                    var recipe = ComplexRecipeManager.Get()?.GetRecipe(order.id);
                    if (recipe != null && RecipeOutputsMatchTargets(recipe))
                        shouldEmit = true;
                }
            }

            emitter.SetEmitting(shouldEmit);
            emitter.Refresh();
        }

        // Outputs-only detection: Lead or SelfChargingElectrobank present in results.
        private static bool RecipeOutputsMatchTargets(ComplexRecipe recipe)
        {
            var results = GetResultsCompat(recipe);
            if (results == null || results.Length == 0) return false;

            Tag lead = SimHashes.Lead.CreateTag();
            Tag powerbank = new Tag("SelfChargingElectrobank");

            for (int i = 0; i < results.Length; i++)
            {
                var mat = results[i].material;
                if (mat == lead || mat == powerbank)
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