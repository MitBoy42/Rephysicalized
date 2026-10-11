using HarmonyLib;
using Klei.AI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using System.Reflection.Emit;

using UnityEngine;
using static STRINGS.ITEMS;


namespace Rephysicalized
{

    [HarmonyPatch(typeof(GasketConfig), nameof(GasketConfig.CreatePrefab))]
    internal static class GasketConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {

            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();


            pe.SetElement(SimHashes.Polypropylene);
            pe.Mass = 50f;

     
        }
    }
    [HarmonyPatch(typeof(RubberGasketConfig), nameof(RubberGasketConfig.CreatePrefab))]
    internal static class RubberGasketConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {
            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();

            pe.SetElement(SimHashes.Rubber);
            pe.Mass = 50f;

        }
    }
    [HarmonyPatch(typeof(UrchinConfig), nameof(UrchinConfig.CreatePrefab))]
    internal static class UrchinConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {
            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();

            pe.Mass = 1f;

        }
    }
    [HarmonyPatch(typeof(FishFoodConfig), nameof(FishFoodConfig.CreatePrefab))]
    internal static class FishFoodConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {
            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();

            pe.Mass = 6f;

        }
    }

    [HarmonyPatch(typeof(EntityTemplates))]
    internal static class EntityTemplates_CreateLooseEntity_GasketOrRubberGasket
    {
        // Match the commonly used CreateLooseEntity overload
        public static MethodBase TargetMethod()
        {
            var types = new Type[] {
                typeof(string), typeof(string), typeof(string), typeof(float), typeof(bool),
                typeof(KAnimFile), typeof(string), typeof(Grid.SceneLayer),
                typeof(EntityTemplates.CollisionShape), typeof(float), typeof(float),
                typeof(bool), typeof(int), typeof(SimHashes), typeof(List<Tag>)
            };
            return AccessTools.Method(typeof(EntityTemplates), "CreateLooseEntity", types);
        }

        public static void Prefix(string id, ref float mass)
        {
            if (string.Equals(id, "PlasticGasket", StringComparison.Ordinal) ||
                string.Equals(id, "RubberGasket", StringComparison.Ordinal))
            {
                mass = 50f;
            }
            if (string.Equals(id, "PinkRockCarved", StringComparison.Ordinal))
            {
                mass = 25f;
            }

            if (
                string.Equals(id, "Urchin", StringComparison.Ordinal))
            {
                mass = 1f;
            }
            if (
           string.Equals(id, "FishFood", StringComparison.Ordinal))
            {
                mass = 6f;
            }
        }
    }

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
    [HarmonyPatch(typeof(NoriConfig), nameof(NoriConfig.CreatePrefab))]
    public static class NoriConfig_CreatePrefab_Patch
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



} 