using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using STRINGS;
namespace Rephysicalized
{

    // Diet tuning
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.SetupDiet))]
    public static class Belly_Diet_Tuning_Patch
    {

        private const float CaloriesPerCycle = 1777777f;

        public static void Prefix(
            GameObject prefab,
            List<Diet.Info> diet_infos,
            float referenceCaloriesPerKg,
            float minPoopSizeInKg)
        {


            for (int i = 0; i < diet_infos.Count; i++)
            {
                var info = diet_infos[i];



                bool isCarrotPlant = info.consumedTags.Contains("CarrotPlant");
                bool isBeanPlant = info.consumedTags.Contains("BeanPlant");
                bool isCarrot = info.consumedTags.Contains(CarrotConfig.ID);
                bool isBean = info.consumedTags.Contains("BeanPlantSeed");
                bool isFries = info.consumedTags.Contains("FriesCarrot");

                if (isCarrotPlant)
                {
                    float originalCaloriesPerKg = info.caloriesPerKg > 0f ? info.caloriesPerKg : referenceCaloriesPerKg > 0f ? referenceCaloriesPerKg : CaloriesPerCycle;
                    float originalProducedConversion = info.producedConversionRate;

                    float targetKgPerCycle = 30f;

                    // Re-tune calories per kg to hit the target mass consumption per cycle
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;

                    info.producedConversionRate = 0.9667f;

                    diet_infos[i] = info;
                    continue;
                }

                if (isBeanPlant)
                {
                    float targetKgPerCycle = 30f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0.9667f;
                    diet_infos[i] = info;
                    continue;
                }

                if (isBean || isFries)
                {
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
                if (isCarrot)
                {
                    float targetKgPerCycle = 1.778f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
            }
        }
    }



    public sealed class BellyHeatOffset : KMonoBehaviour, ISim4000ms
    {
        [SerializeField] public float PowerWatts = -1274f;

        private const float SpecificHeat_kJ_per_kg_K = 3.47f;

        private const float SlopeWattsPerKg = -325f;

        private PrimaryElement pe;
        private float baselineMass; // not serialized; resets each load/spawn

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            pe = GetComponent<PrimaryElement>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (pe == null)
                pe = GetComponent<PrimaryElement>();

            baselineMass = (pe != null && pe.Mass > 0f) ? pe.Mass : 4f; // fallback 4 if something odd
        }

        public void Sim4000ms(float dt)
        {
            float m = pe.Mass;
            if (m <= 0f) return;

            float powerScaledWatts = SlopeWattsPerKg * (m - baselineMass);

            float energy_kJ = (powerScaledWatts * dt) / 1000f;
            float dT = energy_kJ / (m * SpecificHeat_kJ_per_kg_K);

            if (dT == 0f || float.IsNaN(dT) || float.IsInfinity(dT)) return;

            float newT = pe.Temperature + dT;
            if (newT > 0f && newT < 10000f)
                pe.Temperature = newT;

        }

        [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
        public static class Patch_BaseBellyConfig_AddNegativeHeat
        {
            public static void Postfix(ref GameObject __result)
            {
 __result.AddOrGet<BellyHeatOffset>();
            }
        }
    }



    // Diet tuning
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
    internal static class GoldBelly_SolidFuelChoreInjection
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
            var inject = AccessTools.Method(typeof(GoldBelly_SolidFuelChoreInjection), nameof(Inject));

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
    [HarmonyPatch(typeof(GoldBellyConfig), nameof(GoldBellyConfig.CreateGoldBelly))]
    internal static class GoldBelly_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {

            __result.AddOrGet<KSelectable>();



            var diet = BuildFueledDiet();
            var controller = __result.AddOrGet<FueledDietController>();
            ConfigureFueledDiet(controller, diet);
            controller.RefillThreshold = 250f; // adjust as desired (kg)

            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();

            monitor.navigatorSize = new Vector2(2f, 2f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };

            var crownFuel = __result.AddOrGet<GoldBellyCrownFuelController>();

            var shearable = __result.GetDef<WellFedShearable.Def>();
            shearable.growthDurationCycles = 5f;

            var kpid = __result.GetComponent<KPrefabID>();
         FueledDietRegistry.Register(kpid.PrefabTag, diet); 
              
        }

        private static FueledDiet BuildFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Katairite.CreateTag()),

            };

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: null,
                totalFuelCapacityKg: 500f,
                allowBlendedInputByOutput: true
               
            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
            controller.Configure(diet);
        }
    }
    public sealed class GoldBellyCrownFuelController : KMonoBehaviour, ISim200ms
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
        private const float FULL_ANTLER_MASS_KG = 250f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            _shearSMI = gameObject.GetSMI<WellFedShearable.Instance>();
            if (_shearSMI != null && _shearSMI.def != null && !string.IsNullOrEmpty(_shearSMI.def.effectId))
                _wellFedEffectId = _shearSMI.def.effectId;

            if (_effects == null)
                _effects = GetComponent<Effects>();

            _scaleGrowth = Db.Get().Amounts.ScaleGrowth.Lookup(gameObject);

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


    [HarmonyPatch(typeof(ModifierSet), "CreateCritteEffects")]
    public static class GoldBelly_FasterScaleGrowth_Patch
    {
        public static void Postfix(ModifierSet __instance)
        {
            var effect = __instance.effects.Get("GoldBellyWellFed");
            if (effect != null)
            {
               
                effect.Add(new AttributeModifier(Db.Get().Amounts.ScaleGrowth.deltaAttribute.Id, 0.01666667f, "Rephysicalized Addition"));
            }
        }
    }
}