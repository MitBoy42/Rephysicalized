using Database;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(SpiceGrinderWorkable))]
    public static class SpiceGrinderWorkTimePatch
    {
        // Target OnStartWork(WorkerBase worker) and force constant 4s work time
        [HarmonyPatch(nameof(SpiceGrinderWorkable.OnStartWork), new Type[] { typeof(WorkerBase) })]
        [HarmonyPostfix]
        private static void Postfix(SpiceGrinderWorkable __instance)
        {
            if (__instance != null && __instance.Grinder != null && __instance.Grinder.CurrentFood != null)
            {
                __instance.SetWorkTime(4f);
            }
        }
    }

    // Avoid referencing SpiceGrinderConfig (its type initializer can throw early).
    // Instead, wait until all buildings are generated and then modify the Spice Grinder prefab.
    [HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.LoadGeneratedBuildings))]
    public static class SpiceGrinderStorageCapacityPatch
    {
        public static void Postfix()
        {
            try
            {
                // Building ID is the config's ID constant; "SpiceGrinder" in vanilla
                var def = Assets.GetBuildingDef("SpiceGrinder");
                if (def?.BuildingComplete == null)
                    return;

                var storages = def.BuildingComplete.GetComponents<Storage>();
                if (storages == null)
                    return;

                foreach (var s in storages)
                {
                    if (s != null && s.storageFilters != null && s.storageFilters.Contains(GameTags.Edible))
                    {
                        s.capacityKg = 5f;
                        return; // done
                    }
                }

            //    Debug.LogWarning("[Rephysicalized] SpiceGrinder edible storage not found to adjust capacity.");
            }
            catch (Exception e)
            {
          //      Debug.LogWarning($"[Rephysicalized] Failed to adjust SpiceGrinder edible storage capacity: {e}");
            }
        }
    }
    [HarmonyPatch(typeof(Spices))]
    [HarmonyPatch(MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
    public static class SpicesCtorTranspiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int changes = 0;

            // Target new AttributeModifier("SpaceNavigation"/"Strength"/"Machinery", 3f, "Spices", ...)
            // Replace the 3f with 10f for those three calls only.
            for (int i = 0; i < code.Count; i++)
            {
                var instr = code[i];

                if (instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci &&
                    ci.DeclaringType == typeof(AttributeModifier))
                {
                    // Look backward a short window for the attribute id ("SpaceNavigation"/"Strength"/"Machinery")
                    // and the nearest preceding float literal (the value argument).
                    bool isTargetAttribute = false;
                    int floatIdx = -1;
                    string attrId = null;

                    for (int j = Math.Max(0, i - 10); j < i; j++)
                    {
                        var prev = code[j];

                        if (prev.opcode == OpCodes.Ldstr && prev.operand is string s)
                        {
                            if (s == "SpaceNavigation" || s == "Strength" || s == "Machinery")
                            {
                                isTargetAttribute = true;
                                attrId = s;
                            }
                        }

                        if (prev.opcode == OpCodes.Ldc_R4)
                        {
                            floatIdx = j; // keep the last float before newobj (expected to be 3f)
                        }
                    }

                    if (isTargetAttribute && floatIdx >= 0 && code[floatIdx].operand is float)
                    {
                        float old = (float)code[floatIdx].operand;
                        // Only change the canonical 3f to 10f to avoid accidental edits
                        if (Math.Abs(old - 3f) < 0.0001f)
                        {
                            code[floatIdx].operand = 10f;
                            changes++;
                     //       Debug.Log($"[Rephysicalized] Spices ctor: changed {attrId} bonus from {old} to 10f");
                        }
                    }
                }
            }

            if (changes == 0)
            {
            }

            return code;
        }
    }

    // 2) Postfix adjusts ingredient arrays:
    //    - Seeds from 0.1f to 0.05f
    //    - Secondary ingredient to 0.1f
    //    - Strength secondary ingredient tag Iron -> EggShell
    [HarmonyPatch(typeof(Spices))]
    public static class SpicesCtorPostfix
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
        public static void Postfix(Spices __instance)
        {
            try
            {
                // Each spice has exactly two ingredients in this build.
                // Index 0: seed (reduce to 0.05 kg)
                // Index 1: secondary material (set to 0.1 kg)
                SetRecipe(__instance?.PreservingSpice, secondaryTagOverride: null, seedKg: 0.05f, secondaryKg: 0.02f);
                SetRecipe(__instance?.PilotingSpice, secondaryTagOverride: null, seedKg: 0.05f, secondaryKg: 0.02f);
                SetRecipe(__instance?.StrengthSpice, secondaryTagOverride: null, seedKg: 0.05f, secondaryKg: 0.02f);
                SetRecipe(__instance?.MachinerySpice, secondaryTagOverride: null, seedKg: 0.05f, secondaryKg: 0.02f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] Spices Postfix failed: {e}");
            }
        }

        private static void SetRecipe(Spice spice, Tag? secondaryTagOverride, float seedKg, float secondaryKg)
        {
            if (spice == null) return;

            var ingredients = GetIngredientsArray(spice);
            if (ingredients == null || ingredients.Length < 2) return;

            // Seed at index 0
            var seed = ingredients[0];
            seed.AmountKG = seedKg;
            ingredients[0] = seed;

            // Secondary at index 1
            var sec = ingredients[1];
            sec.AmountKG = secondaryKg;
            if (secondaryTagOverride.HasValue)
            {
                sec.IngredientSet = new Tag[] { secondaryTagOverride.Value };
            }
            ingredients[1] = sec;
        }

        private static Spice.Ingredient[] GetIngredientsArray(Spice spice)
        {
            var t = Traverse.Create(spice);
            var arr = t.Field("ingredients").GetValue<Spice.Ingredient[]>();
            if (arr != null) return arr;

            arr = t.Field("Ingredients").GetValue<Spice.Ingredient[]>();
            if (arr != null) return arr;

            try
            {
                arr = t.Property("Ingredients")?.GetValue<Spice.Ingredient[]>();
            }
            catch { /* ignore */ }

            if (arr == null)
                Debug.LogWarning($"[Rephysicalized] Unable to access ingredients for spice {spice?.Id}; build may differ.");

            return arr;
        }
    }
}