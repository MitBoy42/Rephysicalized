using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{

    // Diet tuning
    [HarmonyPatch(typeof(BaseDeerConfig), nameof(BaseDeerConfig.SetupDiet))]
    public static class WoodDeer_Diet_Tuning_Patch
    {


        private const float CaloriesPerCycle = 100_000f;

        public static void Prefix(GameObject prefab, Diet.Info[] diet_infos, float minPoopSizeInKg)
        {

            for (int i = 0; i < diet_infos.Length; i++)
            {
                var info = diet_infos[i];
                if (info.consumedTags == null || info.consumedTags.Count == 0)
                    continue;

                bool isHardSkinPlant = info.consumedTags.Contains("HardSkinBerryPlant");
                bool isPricklePlant = info.consumedTags.Contains("PrickleFlower");
                bool isHardSkinItem = info.consumedTags.Contains("HardSkinBerry");
                bool isPrickleItem = info.consumedTags.Contains("PrickleFruit");
                bool isKatairiteItem = info.consumedTags.Contains("Katairite");

                if (isHardSkinPlant)
                {
                    float originalCaloriesPerKg = info.caloriesPerKg > 0f ? info.caloriesPerKg : CaloriesPerCycle;
                    float originalProducedConversion = info.producedConversionRate;

                    float originalKgPerCycle = CaloriesPerCycle / originalCaloriesPerKg;
                    float targetKgPerCycle = 3f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;

                    float newProducedConversion = originalProducedConversion * 0.4f * (originalKgPerCycle / targetKgPerCycle);
                    info.producedConversionRate = newProducedConversion;

                    diet_infos[i] = info;
                    continue;
                }

                if (isPricklePlant)
                {
                    float targetKgPerCycle = 12f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 11f / 12f;
                    diet_infos[i] = info;
                    continue;
                }

                if (isHardSkinItem)
                {
                    info.producedConversionRate = 0f;
                    diet_infos[i] = info;
                    continue;

                }
                if (isPrickleItem)
                {
                    float targetKgPerCycle = 0.5f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0f;
                    diet_infos[i] = info;
                    continue;

                }

            }
        }
    }

    
    // Diet tuning
    [HarmonyPatch(typeof(BaseDeerConfig), nameof(BaseDeerConfig.BaseDeer))]
    internal static class WoodDeerConfig_CreateWoodDeer_SolidFuelChoreInjection
    {
        // Helper called by injected IL to add our state to the builder.
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SolidFuelStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);

            var popInterrupt = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.PopInterruptGroup));
            var inject = AccessTools.Method(typeof(WoodDeerConfig_CreateWoodDeer_SolidFuelChoreInjection), nameof(Inject));

            if (popInterrupt == null || inject == null)
                return list;

            // Insert our Inject call immediately before the last PopInterruptGroup() call.
            // This mirrors prior behavior used on other creatures.
            int lastPopIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(popInterrupt))
                    lastPopIdx = i;
            }

            if (lastPopIdx >= 0)
            {

                list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
            }

            return list;
        }
    }
    // Inject SolidFuelStates.Def into Deer's ChoreTable build chain 
    [HarmonyPatch(typeof(WoodDeerConfig), nameof(WoodDeerConfig.CreateWoodDeer))]
    internal static class Deer_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {

            __result.AddOrGet<KSelectable>();



            var diet = BuildDeerFueledDiet();
            var controller = __result.AddOrGet<FueledDietController>();
            ConfigureFueledDiet(controller, diet);
            controller.RefillThreshold = 180f; // adjust as desired (kg)


            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
            monitor.scanWindowCells = 16;
            monitor.minPickupKg = 0.01f;
            monitor.navigatorSize = new Vector2(2f, 2f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };

        __result.AddOrGet<WoodDeerAntlerFuelController>();
         


            var kpid = __result.GetComponent<KPrefabID>();
           
         FueledDietRegistry.Register(kpid.PrefabTag, diet); }
           
        

        private static FueledDiet BuildDeerFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Ice.CreateTag()),
                new FuelInput(SimHashes.DirtyIce.CreateTag()),
                new FuelInput(SimHashes.BrineIce.CreateTag()),
                new FuelInput(SimHashes.CrushedIce.CreateTag()),
            };


            return new FueledDiet(
                fuelInputs: inputs,
                conversions: null,
                totalFuelCapacityKg: 240f,
                allowBlendedInputByOutput: true
            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
           controller.Configure(diet); 
        }
    }
    public sealed class WoodDeerAntlerFuelController : KMonoBehaviour, ISim200ms
    {
        [MyCmpGet] private Effects _effects;

        // WellFedShearable SMI and its ScaleGrowth amount (0..max)
        private WellFedShearable.Instance _shearSMI;
        private AmountInstance _scaleGrowth;

        // Single storage assumption (first Storage found)
        private Storage _fuelStorage;

        // Effect id used by WellFedShearable to permit growth
        [SerializeField] private string _wellFedEffectId = "WellFed";

        private float _lastScaleValue;
        private float _scaleMax;

        // Antler mass at 100% growth (kg)
        private const float FULL_ANTLER_MASS_KG = 360f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            _shearSMI = gameObject.GetSMI<WellFedShearable.Instance>();
            if (_shearSMI != null && _shearSMI.def != null && !string.IsNullOrEmpty(_shearSMI.def.effectId))
                _wellFedEffectId = _shearSMI.def.effectId;

            if (_effects == null)
                _effects = GetComponent<Effects>();

            _scaleGrowth = Db.Get().Amounts.ScaleGrowth.Lookup(gameObject);

            // Assume at least one storage exists and use the first found (self or children)
            _fuelStorage = GetComponent<Storage>();
          

            // Initialize growth tracking
            if (_scaleGrowth != null)
            {
                _lastScaleValue = _scaleGrowth.value;
                _scaleMax = _scaleGrowth.GetMax();
                if (_scaleMax <= 0f) _scaleMax = 100f; // default safety
            }
        }

        public void Sim200ms(float dt)
        {
           

            if (_scaleMax <= 0f) _scaleMax = _scaleGrowth.GetMax();
            if (_scaleMax <= 0f) _scaleMax = 100f;

            bool gateEffectActive = !string.IsNullOrEmpty(_wellFedEffectId) && _effects.HasEffect(_wellFedEffectId);
            bool fullyGrown = IsFullyGrown();

            // If not well-fed or already fully grown: do nothing special
            if (fullyGrown || !gateEffectActive)
            {
                _lastScaleValue = _scaleGrowth.value;
                return;
            }

            float current = _scaleGrowth.value;
            float desiredDelta = Mathf.Max(0f, current - _lastScaleValue);

            // No attempted growth since last tick
            if (desiredDelta <= 0f)
            {
                // If no fuel, clamp to last known value (prevent free growth)
                if (TotalFuelKg() <= 0.0001f)
                {
                    if (Mathf.Abs(_scaleGrowth.value - _lastScaleValue) > 0.0001f)
                        _scaleGrowth.value = _lastScaleValue;
                }
                _lastScaleValue = _scaleGrowth.value;
                return;
            }

            // Compute required kg for the desired growth amount
            float pct01Delta = desiredDelta / _scaleMax;
            float kgNeeded = Mathf.Max(0f, pct01Delta * FULL_ANTLER_MASS_KG);

            if (kgNeeded <= 0f)
            {
                _lastScaleValue = current;
                return;
            }

            float consumed = ConsumeFuelUpTo(kgNeeded);

            if (consumed + 1e-6f >= kgNeeded)
            {
                // Fully paid, accept growth
                _lastScaleValue = _scaleGrowth.value;
            }
            else
            {
                // Partial payment: clamp growth to afforded amount
                float affordedPct01 = (FULL_ANTLER_MASS_KG > 1e-6f) ? (consumed / FULL_ANTLER_MASS_KG) : 0f;
                float affordedDelta = affordedPct01 * _scaleMax;
                float clampedValue = _lastScaleValue + affordedDelta;

                if (clampedValue < _scaleGrowth.value - 1e-5f)
                    _scaleGrowth.value = clampedValue;

                _lastScaleValue = _scaleGrowth.value;
            }
        }

        private bool IsFullyGrown()
        {
            float max = _scaleGrowth?.GetMax() ?? 0f;
            return max > 0f && _scaleGrowth.value >= max - 0.0001f;
        }

        private float TotalFuelKg()
        {
            float total = 0f;
            var items = _fuelStorage.items;
            for (int j = items.Count - 1; j >= 0; j--)
            {
                var go = items[j];
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null) total += pe.Mass;
            }
            return total;
        }

        // Consume up to target kg; returns how much was actually consumed.
        private float ConsumeFuelUpTo(float kgNeeded)
        {
            float remaining = Mathf.Max(0f, kgNeeded);
            float consumed = 0f;

            var items = _fuelStorage.items;
            for (int j = items.Count - 1; j >= 0 && remaining > 0f; j--)
            {
                var go = items[j];
                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f) continue;

                float take = Mathf.Min(remaining, pe.Mass);
                pe.Mass -= take;
                remaining -= take;
                consumed += take;

                if (pe.Mass <= 0.0001f)
                {
                    Util.KDestroyGameObject(go);
                }
            }

            return consumed;
        }
    }
}