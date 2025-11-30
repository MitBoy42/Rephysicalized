using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using STRINGS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;


namespace Rephysicalized
{

    // Patch Kelp: add ModTags.Distillable on prefab creation
    [HarmonyPatch(typeof(KelpConfig), nameof(KelpConfig.CreatePrefab))]
    public static class KelpConfig_CreatePrefab_Patch
    {
        public static void Postfix(GameObject __result)
        {


            var kpid = __result.GetComponent<KPrefabID>();
            if (kpid != null && ModTags.Distillable.IsValid)
            {
                kpid.AddTag(ModTags.Distillable, false);
            }
        }
    }

    // Makes the existing Tallow Lubrication Stick recipe accept either 10f Tallow OR 10f Graphite (same recipe entry).
    [HarmonyPatch(typeof(TallowLubricationStickConfig), nameof(TallowLubricationStickConfig.CreatePrefab))]
    public static class TallowLubricationStick_GraphiteAlt_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            var recipe = TallowLubricationStickConfig.recipe;

            Tag tallow = SimHashes.Tallow.CreateTag();
            Tag graphite;

            graphite = SimHashes.Graphite.CreateTag();



            // Find the Tallow ingredient and replace it with an alternative [Tallow | Graphite], both 10f
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                var ing = recipe.ingredients[i];

                if (ing.material == tallow)
                {
                    // Use the alt-materials constructor, same mass (10f) for both options.
                    recipe.ingredients[i] = new ComplexRecipe.RecipeElement(
                        new Tag[] { tallow, graphite },
                        10f
                    );
                    break;
                }
            }
        }
    }


    [HarmonyPatch(typeof(FeatherFabricConfig))]
    [HarmonyPatch(MethodType.Constructor)]
    public static class FeatherFabricCtorTranspiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int changes = 0;
            for (int i = 0; i < code.Count; i++)
            {
                var instr = code[i];
                if (instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci &&
                    ci.DeclaringType == typeof(AttributeModifier))
                {
                    bool decorFound = false;
                    int valueIndex = -1;
                    for (int j = Math.Max(0, i - 8); j < i; j++)
                    {
                        var prev = code[j];
                        if (!decorFound && prev.opcode == OpCodes.Ldstr && prev.operand is string s && s == "Decor")
                        {
                            decorFound = true;
                        }
                        if (prev.opcode == OpCodes.Ldc_R4)
                        {
                            valueIndex = j; // keep last one, IL pushes args in order
                        }
                    }
                    if (decorFound && valueIndex >= 0 && code[valueIndex].operand is float)
                    {
                        var old = (float)code[valueIndex].operand;
                        code[valueIndex].operand = 0.5f;
                        changes++;
                    }
                }
            }
            if (changes == 0)
            {
            }
            return code;
        }
    }

    // Patch the prefab creation to override the primary element to CLay
    [HarmonyPatch(typeof(IceBellyPoopConfig), nameof(IceBellyPoopConfig.CreatePrefab))]
    public static class IceBellyPoopConfig_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {


            var primary = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();


            primary.SetElement(SimHashes.Clay);

        }
    }

    [HarmonyPatch(typeof(CrabWoodShellConfig), nameof(CrabWoodShellConfig.CreatePrefab))]
    public static class CrabWoodShell_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromentCookablePatch>();

            comp.temperature = 0f;
            comp.ID = "WoodLog";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 4000f;
        }
    }
    [HarmonyPatch(typeof(CrabShellConfig), nameof(CrabShellConfig.CreatePrefab))]
    public static class CrabShell_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromentCookablePatch>();

            comp.temperature = 0f;
            comp.ID = "Lime";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 4000f;
        }

    }

    [HarmonyPatch(typeof(DewDripConfig), nameof(DewDripConfig.CreatePrefab))]
    public static class DewDripPatch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromentCookablePatch>();
            comp.temperature = 80f + 273.15f;
            comp.ID = "Milk";
            comp.massConversionRatio = 1.0f;
            var compF = __result.AddComponent<EnviromentCookablePatch>();
            compF.enableFreezing = true;
            compF.temperature = -40f + 273.15f;
            compF.ID = "MilkIce";
            compF.massConversionRatio = 1.0f;

        }
    }
    [HarmonyPatch(typeof(KelpConfig), nameof(KelpConfig.CreatePrefab))]
    public static class Kelp_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromentCookablePatch>();

            comp.temperature = 75f + 273.15f;
            comp.ID = "PhytoOil";
            comp.massConversionRatio = 4f;
            comp.pressureThreshold = 1000f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.Water);
            comp.elementConsumedRatio = 0.75f;
        }

    }



} 