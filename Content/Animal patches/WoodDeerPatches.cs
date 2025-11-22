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
        private static readonly Tag HardSkinBerryPlantTag = new Tag("HardSkinBerryPlant");
        private static readonly Tag PrickleFlowerPlantTagA = new Tag("PrickleFlower");
        private static readonly Tag HardSkinBerryItemTag = new Tag("HardSkinBerry");
        private static readonly Tag PrickleFruitTag = new Tag("PrickleFruit");

        private const float CaloriesPerCycle = 100_000f;

        public static void Prefix(GameObject prefab, Diet.Info[] diet_infos, float minPoopSizeInKg)
        {
  
            for (int i = 0; i < diet_infos.Length; i++)
            {
                var info = diet_infos[i];
                if (info.consumedTags == null || info.consumedTags.Count == 0)
                    continue;

                bool isHardSkinPlant = info.consumedTags.Contains(HardSkinBerryPlantTag);
                bool isPricklePlant = info.consumedTags.Contains(PrickleFlowerPlantTagA);
                bool isHardSkinItem = info.consumedTags.Contains(HardSkinBerryItemTag);
                bool isPrickleItem = info.consumedTags.Contains(PrickleFruitTag);

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

                if (isHardSkinItem )
                {
                    info.producedConversionRate = 0f;
                    diet_infos[i] = info;
                    continue;

                }
                if ( isPrickleItem)
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

    // Inject SolidFuelStates.Def into Deer's ChoreTable build chain (unchanged).
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
                // The ChoreTable.Builder instance should be on the stack for PopInterruptGroup();
                // calling Inject(builder) will consume and return the same builder with SolidFuelStates added.
                list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
            }

            return list;
        }
    }
    // Inject SolidFuelStates.Def into Deer's ChoreTable build chain (unchanged).
    [HarmonyPatch(typeof(WoodDeerConfig), nameof(WoodDeerConfig.CreateWoodDeer))]
    internal static class Deer_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
     
            __result.AddOrGet<KSelectable>();

            var cal = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();

            var solidConsumer = __result.GetDef<SolidConsumerMonitor.Def>();
            if (solidConsumer != null)
                solidConsumer.diet = null;

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

            // IMPORTANT: Attach the antler fuel controller so it actually runs
            var antlerFuel = __result.AddOrGet<WoodDeerAntlerFuelController>();
            antlerFuel.fuelConsumeRateKgPerSecond = 0.1f;
        

            var kpid = __result.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                try { FueledDietRegistry.Register(kpid.PrefabTag, diet); }
                catch { }
            }
        }

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
            if (controller == null || diet == null) return;
            try { controller.Configure(diet); } catch { }
        }
    }

    public sealed class WoodDeerAntlerFuelController : KMonoBehaviour, ISim1000ms
    {
        [MyCmpGet] private Effects _effects;

        // WellFedShearable SMI and its ScaleGrowth amount (0..max)
        private WellFedShearable.Instance _shearSMI;
        private AmountInstance _scaleGrowth;

        // Fuel storages discovered on spawn (e.g., the Fueled Diet chore fills one of these)
        private readonly List<Storage> _fuelStorages = new List<Storage>();

        [SerializeField] public float fuelConsumeRateKgPerSecond = 0.1f;

        // Reduce consumption while wild to match wild growth penalty
        [SerializeField] public float wildConsumptionMultiplier = 0.25f;

        // Effect id used by WellFedShearable to permit growth
        [SerializeField] private string _wellFedEffectId = "WellFed";

        // Freeze state: when fuel is missing but effect is active, keep scale growth fixed
        private bool _freezeActive;
        private float _frozenScaleValue;

        public override void OnSpawn()
        {
            base.OnSpawn();

            _shearSMI = gameObject.GetSMI<WellFedShearable.Instance>();
            if (_shearSMI != null && _shearSMI.def != null && !string.IsNullOrEmpty(_shearSMI.def.effectId))
                _wellFedEffectId = _shearSMI.def.effectId;

            if (_effects == null)
                _effects = GetComponent<Effects>();

            _scaleGrowth = Db.Get().Amounts.ScaleGrowth.Lookup(gameObject);

            DiscoverFuelStorages();

            if (_scaleGrowth != null)
                _frozenScaleValue = _scaleGrowth.value;

            UpdateFreezeState(initial: true);
        }

        public void Sim1000ms(float dt)
        {
            if (_fuelStorages.Count == 0)
                DiscoverFuelStorages();

            if (_effects == null || _scaleGrowth == null)
                return;

            bool gateEffectActive = !string.IsNullOrEmpty(_wellFedEffectId) && _effects.HasEffect(_wellFedEffectId);
            bool hasFuel = TotalFuelKg() > 0.0001f;
            bool fullyGrown = IsFullyGrown();

            // If not wellfed or already fully grown: do nothing extra (vanilla behavior applies)
            if (fullyGrown || !gateEffectActive)
            {
                _freezeActive = false;
                _frozenScaleValue = _scaleGrowth.value;
                return;
            }

            if (hasFuel)
            {
                _freezeActive = false;
                _frozenScaleValue = _scaleGrowth.value;

                // Scale consumption if wild (directly via WildnessMonitor)
                float mult = IsWild() ? Mathf.Clamp01(wildConsumptionMultiplier) : 1f;
                float toConsume = Mathf.Max(0f, fuelConsumeRateKgPerSecond) * mult * dt;
                if (toConsume > 0f)
                    ConsumeFuel(toConsume);
            }
            else
            {
                // No fuel: pause growth without resetting progress
                if (!_freezeActive)
                {
                    _freezeActive = true;
                    _frozenScaleValue = _scaleGrowth.value;
                }
                else
                {
                    _frozenScaleValue = Mathf.Min(_frozenScaleValue, _scaleGrowth.value);
                }

                if (Mathf.Abs(_scaleGrowth.value - _frozenScaleValue) > 0.0001f)
                    _scaleGrowth.value = _frozenScaleValue;
            }
        }

        // Direct wildness check using WildnessMonitor.Instance (no reflection/heuristics)
        private bool IsWild()
        {
            var wildSMI = gameObject.GetSMI<WildnessMonitor.Instance>();
            return wildSMI != null && wildSMI.IsWild();
        }

        private bool IsFullyGrown()
        {
            float max = _scaleGrowth?.GetMax() ?? 0f;
            return max > 0f && _scaleGrowth.value >= max - 0.0001f;
        }

        private void UpdateFreezeState(bool initial)
        {
            if (_effects == null || _scaleGrowth == null)
                return;

            bool gateEffectActive = !string.IsNullOrEmpty(_wellFedEffectId) && _effects.HasEffect(_wellFedEffectId);
            bool hasFuel = TotalFuelKg() > 0.0001f;
            bool fullyGrown = IsFullyGrown();

            if (fullyGrown || !gateEffectActive || hasFuel)
            {
                _freezeActive = false;
                _frozenScaleValue = _scaleGrowth.value;
            }
            else
            {
                _freezeActive = true;
                _frozenScaleValue = _scaleGrowth.value;
            }
        }

        private void DiscoverFuelStorages()
        {
            _fuelStorages.Clear();

            var storages = GetComponentsInChildren<Storage>(includeInactive: true);
            foreach (var s in storages)
            {
                if (s == null) continue;

                bool acceptsFuel = false;
                if (s.storageFilters != null)
                {
                    foreach (var t in s.storageFilters)
                    {
                        if (IsFuelTag(t))
                        {
                            acceptsFuel = true;
                            break;
                        }
                    }
                }

                if (acceptsFuel || s.capacityKg > 0f)
                {
                    if (!_fuelStorages.Contains(s))
                        _fuelStorages.Add(s);
                }
            }
        }

        private float TotalFuelKg()
        {
            float total = 0f;
            for (int i = 0; i < _fuelStorages.Count; i++)
            {
                var s = _fuelStorages[i];
                if (s == null) continue;

                var items = s.items;
                for (int j = items.Count - 1; j >= 0; j--)
                {
                    var go = items[j];
                    if (go == null) continue;
                    if (!IsFuelItem(go)) continue;

                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe != null) total += pe.Mass;
                }
            }
            return total;
        }

        private void ConsumeFuel(float kgNeeded)
        {
            float remaining = kgNeeded;

            for (int i = 0; i < _fuelStorages.Count && remaining > 0f; i++)
            {
                var s = _fuelStorages[i];
                if (s == null) continue;

                var items = s.items;
                for (int j = items.Count - 1; j >= 0 && remaining > 0f; j--)
                {
                    var go = items[j];
                    if (go == null) continue;
                    if (!IsFuelItem(go)) continue;

                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe == null || pe.Mass <= 0f) continue;

                    float take = Mathf.Min(remaining, pe.Mass);
                    pe.Mass -= take;
                    remaining -= take;

                    if (pe.Mass <= 0.0001f)
                    {
                        Util.KDestroyGameObject(go);
                    }
                }
            }
        }

        private static readonly Tag Ice = SimHashes.Ice.CreateTag();
        private static readonly Tag DirtyIce = SimHashes.DirtyIce.CreateTag();
        private static readonly Tag BrineIce = SimHashes.BrineIce.CreateTag();
        private static readonly Tag CrushedIce = new Tag("CrushedIce");

        private static bool IsFuelItem(GameObject go)
        {
            if (go == null) return false;
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return false;

            if (IsFuelTag(kpid.PrefabTag))
                return true;

            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                var elem = pe.Element;
                if (elem != null && elem.IsSolid)
                {
                    var id = elem.id;
                    if (id == SimHashes.Ice) return true;
                    string name = id.ToString();
                    if (name.IndexOf("Ice", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Snow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        private static bool IsFuelTag(Tag t)
        {
           
            return t == Ice || t == DirtyIce || t == BrineIce || t == CrushedIce;
        }
    }
}

