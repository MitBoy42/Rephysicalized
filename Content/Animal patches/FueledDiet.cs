using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System; // added for [Serializable]
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Rephysicalized
{
    internal static class FuelStatusItems
    {
        public static StatusItem SeekingFuel;
        public static StatusItem EatingSecondary;
    }

    [HarmonyPatch(typeof(Db), "Initialize")]

    // Makes DietManager's collectors robust against null/partially-initialized prefabs during load.
    [HarmonyPatch]
    internal static class DietManagerSafePatch
    {
        [HarmonyPatch(typeof(DietManager), nameof(DietManager.CollectSaveDiets))]
        [HarmonyPrefix]
        private static bool CollectSaveDiets_Safe(Tag[] target_species, ref Dictionary<Tag, Diet> __result)
        {
            try
            {
                var dict = new Dictionary<Tag, Diet>();
                foreach (var prefab in Assets.Prefabs)
                {
                    if (prefab == null) continue;
                    var go = prefab.gameObject;
                    if (go == null) continue;

                    // Guard GetDef calls
                    CreatureCalorieMonitor.Def ccDef = null;
                    BeehiveCalorieMonitor.Def bhDef = null;
                    try
                    {
                        ccDef = prefab.GetDef<CreatureCalorieMonitor.Def>();
                    }
                    catch { /* ignore */ }
                    if (ccDef == null)
                    {
                        try { bhDef = prefab.GetDef<BeehiveCalorieMonitor.Def>(); }
                        catch { /* ignore */ }
                    }

                    Diet diet = null;
                    if (ccDef != null) diet = ccDef.diet;
                    else if (bhDef != null) diet = bhDef.diet;
                    if (diet == null) continue;

                    if (target_species != null)
                    {
                        var brain = go.GetComponent<CreatureBrain>();
                        if (brain == null) continue;
                        if (Array.IndexOf(target_species, brain.species) < 0) continue;
                    }

                    var copy = new Diet(diet);
                    copy.FilterDLC();
                    dict[prefab.PrefabTag] = copy;
                }

                __result = dict;
                return false; // skip original
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] CollectSaveDiets_Safe failed, falling back to original: {e}");
                return true; // run original as last resort
            }
        }

        [HarmonyPatch(typeof(DietManager), nameof(DietManager.CollectDiets))]
        [HarmonyPrefix]
        private static bool CollectDiets_Safe(Tag[] target_species, ref Dictionary<Tag, Diet> __result)
        {
            try
            {
                var dict = new Dictionary<Tag, Diet>();
                foreach (var prefab in Assets.Prefabs)
                {
                    if (prefab == null) continue;
                    var go = prefab.gameObject;
                    if (go == null) continue;

                    CreatureCalorieMonitor.Def ccDef = null;
                    BeehiveCalorieMonitor.Def bhDef = null;
                    try { ccDef = prefab.GetDef<CreatureCalorieMonitor.Def>(); } catch { }
                    if (ccDef == null) { try { bhDef = prefab.GetDef<BeehiveCalorieMonitor.Def>(); } catch { } }

                    Diet diet = null;
                    if (ccDef != null) diet = ccDef.diet;
                    else if (bhDef != null) diet = bhDef.diet;
                    if (diet == null) continue;

                    if (target_species != null)
                    {
                        var brain = go.GetComponent<CreatureBrain>();
                        if (brain == null) continue;
                        if (Array.IndexOf(target_species, brain.species) < 0) continue;
                    }

                    dict[prefab.PrefabTag] = diet;
                }

                __result = dict;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] CollectDiets_Safe failed, falling back to original: {e}");
                return true;
            }
        }
    }

    // Simple registry to rebuild FueledDiet on spawned instances
    internal static class FueledDietRegistry
    {
        private static readonly Dictionary<Tag, FueledDiet> map = new Dictionary<Tag, FueledDiet>();

        internal static void Register(Tag prefabTag, FueledDiet diet)
        {
            if (!prefabTag.IsValid || diet == null) return;
            map[prefabTag] = diet;
        }

        internal static bool TryGet(Tag prefabTag, out FueledDiet diet) => map.TryGetValue(prefabTag, out diet);
    }

    // FueledDiet
    [Serializable]
    public class FueledDiet
    {
        public IReadOnlyList<FuelInput> FuelInputs { get; private set; }
        public IReadOnlyList<FuelConversion> Conversions { get; private set; }

        public bool AllowBlendedInputByOutput { get; private set; }
        public float TotalFuelCapacityKg { get; private set; }

      
        public IReadOnlyDictionary<Tag, float> MainFoodMultipliers => _mainFoodMultipliers;
        public IReadOnlyDictionary<Tag, float> MainFoodKgInputPerKgMainOverrides => _mainFoodOverrides;

        private readonly Dictionary<Tag, float> _mainFoodMultipliers;
        private readonly Dictionary<Tag, float> _mainFoodOverrides;

        public FueledDiet(IEnumerable<FuelInput> fuelInputs,
                          IEnumerable<FuelConversion> conversions,
                          float totalFuelCapacityKg = 0f,
                          bool allowBlendedInputByOutput = false
                        )
            : this(fuelInputs, conversions, totalFuelCapacityKg, allowBlendedInputByOutput, null, null)
        {
        }

        public FueledDiet(IEnumerable<FuelInput> fuelInputs,
                          IEnumerable<FuelConversion> conversions,
                          float totalFuelCapacityKg,
                          bool allowBlendedInputByOutput,
                          IDictionary<Tag, float> mainFoodMultipliers,
                          IDictionary<Tag, float> mainFoodKgInputPerKgMainOverrides
)
        {
            FuelInputs = (fuelInputs ?? Enumerable.Empty<FuelInput>()).ToList().AsReadOnly();
            Conversions = (conversions ?? Enumerable.Empty<FuelConversion>()).ToList().AsReadOnly();
            TotalFuelCapacityKg = Mathf.Max(0f, totalFuelCapacityKg);
            AllowBlendedInputByOutput = allowBlendedInputByOutput;

            _mainFoodMultipliers = mainFoodMultipliers != null
                ? new Dictionary<Tag, float>(mainFoodMultipliers)
                : new Dictionary<Tag, float>();

            _mainFoodOverrides = mainFoodKgInputPerKgMainOverrides != null
                ? new Dictionary<Tag, float>(mainFoodKgInputPerKgMainOverrides)
                : new Dictionary<Tag, float>();

           
        }

        public float ResolveKgInputPerKgMain(Tag outputTag, Tag mainFoodTag, float baseKgInputPerKgMain)
        {
            float effective = Mathf.Max(0f, baseKgInputPerKgMain);

            if (mainFoodTag.IsValid && _mainFoodMultipliers.TryGetValue(mainFoodTag, out var mul))
                effective *= Mathf.Max(0f, mul);

            if (mainFoodTag.IsValid && _mainFoodOverrides.TryGetValue(mainFoodTag, out var abs))
                effective = Mathf.Max(0f, abs);

            return effective;
        }

        public float ComputeRequiredInputKg(Tag outputTag, Tag mainFoodTag, float kgMainEaten, float baseKgInputPerKgMain)
        {
            if (kgMainEaten <= 0f) return 0f;
            var resolved = ResolveKgInputPerKgMain(outputTag, mainFoodTag, baseKgInputPerKgMain);
            return kgMainEaten * resolved;
        }

        public bool TryBuildInputPlanForOutput(
            Tag outputTag,
            float requiredInputKg,
            IReadOnlyDictionary<Tag, float> availableKgByTag,
            out Dictionary<Tag, float> plan)
        {
            plan = new Dictionary<Tag, float>();
            if (requiredInputKg <= 0f) return true;

            var matching = Conversions.Where(c => c.OutputTag == outputTag).ToList();
            if (matching.Count == 0) return false;

            if (!AllowBlendedInputByOutput)
            {
                var conv = matching[0];
                float available = GetAvailable(availableKgByTag, conv.InputTag);
                float take = Mathf.Min(available, requiredInputKg);
                if (take > 0f) plan[conv.InputTag] = take;
                return true;
            }

            float remaining = requiredInputKg;
            foreach (var conv in matching.OrderByDescending(c => GetAvailable(availableKgByTag, c.InputTag)))
            {
                float available = GetAvailable(availableKgByTag, conv.InputTag);
                if (available <= 0f) continue;

                float take = Mathf.Min(available, remaining);
                if (take > 0f)
                {
                    if (plan.TryGetValue(conv.InputTag, out var already))
                        plan[conv.InputTag] = already + take;
                    else
                        plan[conv.InputTag] = take;

                    remaining -= take;
                    if (remaining <= 0f) break;
                }
            }
            return true;
        }

        private static float GetAvailable(IReadOnlyDictionary<Tag, float> dict, Tag tag)
            => dict != null && dict.TryGetValue(tag, out var v) ? v : 0f;
    }


    [Serializable]
    public class FuelInput
    {
        public Tag ElementTag;
        public float CapacityKg;
        public float ConsumptionRateKgPerSecond;
        public byte ConsumptionRadius = 1;
        public bool IsGas = false;
        public bool IsLiquid = false;

        public FuelInput(Tag elementTag, float capacityKg = 20000f, float rateKgPerSecond = 20000f, bool isGas = false, bool isLiquid = false, byte consumptionRadius = 0)
        {
            ElementTag = elementTag;
            CapacityKg = Mathf.Max(0f, capacityKg);
            ConsumptionRateKgPerSecond = Mathf.Max(0f, rateKgPerSecond);
            ConsumptionRadius = consumptionRadius;
            IsGas = isGas;
            IsLiquid = isLiquid;
        }
    }

    [Serializable]
    public class FuelConversion
    {
        public Tag InputTag;
        public Tag OutputTag;

        // Base requirement: kg of this input per 1 kg of "main" eaten.
        public float KgInputPerKgMain;

        // After consuming the input, how much output is generated (kg per kg input).
        public float KgOutputPerKgInput;

        // Optionally override the temperature of produced output (0 means inherit/skip).
        public float OutputTemperatureOverrideKelvin;

        public FuelConversion(Tag inputTag,
                              Tag outputTag,
                              float kgInputPerKgMain,
                              float kgOutputPerKgInput = 1.0f,
                              float outputTemperatureOverrideKelvin = 0f)
        {
            InputTag = inputTag;
            OutputTag = outputTag;
            KgInputPerKgMain = Mathf.Max(0f, kgInputPerKgMain);
            KgOutputPerKgInput = Mathf.Max(0f, kgOutputPerKgInput);
            OutputTemperatureOverrideKelvin = Mathf.Max(0f, outputTemperatureOverrideKelvin);
        }
    }

    public static class ModInit
    {
        public static void OnLoad()
        {
            var harmony = new Harmony("com.yourname.oni.fueleddiet");
            harmony.PatchAll();
        }
    }

    internal static class FueledDietDispatch
    {
        internal static void TryDispatch(GameObject creatureGO, Tag consumedTag, float consumedMassKg)
        {
            try
            {
                if (creatureGO == null || consumedMassKg <= 0f) return;

                var diet = creatureGO.GetComponent<Diet>();
                var controller = creatureGO.GetComponent<FueledDietController>();
                if (diet == null || controller == null) return;

                var info = diet.GetDietInfo(consumedTag);
                if (info == null) return;

     
            }
            catch (Exception e)
            {
                Debug.LogError($"[FueledDiet] Dispatch failed: {e}");
            }
        }
    }



    // Central hook: for any creature with FueledDietController, convert CaloriesConsumed -> kg of main diet
    // and credit it to the controller. No per-species binders needed.
    [HarmonyPatch(typeof(CreatureCalorieMonitor.Instance), nameof(CreatureCalorieMonitor.Instance.OnCaloriesConsumed))]
    internal static class FueledDiet_CaloriesConsumed_Postfix
    {
        [HarmonyPostfix]
        private static void Postfix(CreatureCalorieMonitor.Instance __instance, object data)
        {
     
            if (data is not Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent> boxed)
                return;

            var ev = boxed.value;
            if (!ev.tag.IsValid || ev.calories <= 0f)
                return;

            var go = __instance.gameObject;
            if (go == null)
                return;

            var controller = go.GetComponent<FueledDietController>();
            if (controller == null)
                return;

            // Use the live diet from the stomach/monitor (not defs)
            var diet = __instance.stomach?.diet;
            if (diet == null)
                return;

            var info = diet.GetDietInfo(ev.tag);
            if (info == null || info.caloriesPerKg <= 0f)
                return;

            float kg = ev.calories / info.caloriesPerKg;
            if (kg <= 0f)
                return;

            controller.CreditMainDietKg(kg, ev.tag);
        }
    }
}
