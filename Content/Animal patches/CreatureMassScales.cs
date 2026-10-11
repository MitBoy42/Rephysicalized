using HarmonyLib;
using Klei.AI;
using System;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    /// <summary>
    /// Shared mechanic for creatures whose scale/coat/antler growth must be paid for with fuel
    /// instead of body mass. Each tick the creature's growth is measured; the equivalent kg is
    /// burned from its fuel storage, and any growth that could not be paid for is rolled back.
    /// Used by GoldBelly, WoodDeer and SeaTurtle, which only differ in the mass of their grown part.
    /// </summary>
    public abstract class ScaleFuelGrowthController : KMonoBehaviour, ISim1000ms
    {
        [MyCmpGet] private Effects _effects;

        private WellFedShearable.Instance _shearSMI;
        private AmountInstance _scaleGrowth;

        // Single storage assumption (first Storage found)
        private Storage _fuelStorage;

        [SerializeField] private string _wellFedEffectId = "WellFed";

        private float _lastScaleValue;
        private float _scaleMax;

        protected abstract float FullScaleMassKg { get; }

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

        public void Sim1000ms(float dt)
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
            float kgNeeded = Mathf.Max(0f, pct01Delta * FullScaleMassKg);

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
                float affordedPct01 = (FullScaleMassKg > 1e-6f) ? (consumed / FullScaleMassKg) : 0f;
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

    // Small SM to drive a ToggleStatusItem when mass transfer is paused
    public sealed class ScaleMassPauseSM : GameStateMachine<ScaleMassPauseSM, ScaleMassPauseSM.Instance, IStateMachineTarget>
    {
        public sealed class Instance : GameInstance
        {
            public Instance(IStateMachineTarget master, System.Func<bool> isPausedProvider) : base(master)
            {
                this.isPaused = isPausedProvider ?? (() => false);
            }
            public readonly System.Func<bool> isPaused;
        }
        private State ok;
        private State paused;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = ok;

            ok.Update((smi, dt) =>
            {
                if (smi.isPaused())
                    smi.GoTo(paused);
            }, UpdateRate.SIM_1000ms);

            paused
                .ToggleStatusItem(
                    name: STRINGS.STATUSITEMS.NOTENOUGHBODYMASS.NAME,
                    tooltip: STRINGS.STATUSITEMS.NOTENOUGHBODYMASS.TOOLTIP,
                    category: Db.Get().StatusItemCategories.Stored)
                .Update((smi, dt) =>
                {
                    if (!smi.isPaused())
                        smi.GoTo(ok);
                }, UpdateRate.SIM_1000ms);
        }
    }

    // SM for milk pause (toggle status item)
    public sealed class MilkMassPauseSM : GameStateMachine<MilkMassPauseSM, MilkMassPauseSM.Instance, IStateMachineTarget>
    {
        public sealed class Instance : GameInstance
        {
            public Instance(IStateMachineTarget master, Func<bool> isPausedProvider) : base(master)
            {
                this.isPaused = isPausedProvider ?? (() => false);
            }
            public readonly Func<bool> isPaused;
        }

        private State ok;
        private State paused;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = ok;

            ok.Update((smi, dt) =>
            {
                if (smi.isPaused())
                    smi.GoTo(paused);
            }, UpdateRate.SIM_1000ms);

            paused
                .ToggleStatusItem(
                    name: STRINGS.STATUSITEMS.NOTENOUGHBODYMASSMILK.NAME,
                    tooltip: STRINGS.STATUSITEMS.NOTENOUGHBODYMASSMILK.TOOLTIP,
                    category: Db.Get().StatusItemCategories.Stored)
                .Update((smi, dt) =>
                {
                    if (!smi.isPaused())
                        smi.GoTo(ok);
                }, UpdateRate.SIM_1000ms);
        }
    }

    public sealed class ScaleMassTransferDriver : KMonoBehaviour, ISim1000ms
    {
        // Adjust tags if your prefab IDs differ
        internal static readonly Tag WoodDeerTag = new Tag("WoodDeer");
        internal static readonly Tag BabyWoodDeerTag = new Tag("WoodDeerBaby");
        internal static readonly Tag GoldBellyTag = new Tag("GoldBelly");
        internal static readonly Tag BabyGoldBellyTag = new Tag("GoldBellyBaby");
        internal static readonly Tag SeaTurtleTag = new Tag("SeaTurtle");
        internal static readonly Tag BabySeaTurtleTag = new Tag("BabySeaTurtle");

        [MyCmpReq] private CreatureMassTracker tracker;
        [MyCmpGet] private KPrefabID kpid;

        // Scales tracking
        private float prevScaleKg;
        private bool pausedScales;

        // Milk tracking
        private float prevMilkKg;
        private bool pausedMilk;

        public override void OnSpawn()
        {
            base.OnSpawn();

            // Fully exempt WD/BWD from any init
            if (IsExempt())
                return;

            // Initialize caches to current derived masses so no immediate transfer on load
            prevScaleKg = CreatureMassTracker.ComputeScaleMassKgFromMonitors(gameObject);
            prevMilkKg = CreatureMassTracker.ComputeMilkMassKgFromMilkMonitor(gameObject);

            // Start ToggleStatusItem SMs (one for scales, one for milk)
            if (gameObject.GetSMI<ScaleMassPauseSM.Instance>() == null)
            {
                var smiScale = new ScaleMassPauseSM.Instance(this, IsScalePaused);
                smiScale.StartSM();
            }
            if (gameObject.GetSMI<MilkMassPauseSM.Instance>() == null)
            {
                var smiMilk = new MilkMassPauseSM.Instance(this, IsMilkPaused);
                smiMilk.StartSM();
            }
        }

        public void Sim1000ms(float dt)
        {
            // No-op for exempt creatures even if attached
            if (IsExempt())
            {
                pausedScales = false;
                pausedMilk = false;
                return;
            }

            // 1) Handle scale growth: transfer from body to scales
            float currentScaleKg = CreatureMassTracker.ComputeScaleMassKgFromMonitors(gameObject);
            float deltaScaleKg = currentScaleKg - prevScaleKg;

            if (deltaScaleKg > 0f)
            {
                float headroom = BodyHeadroom();

                if (headroom <= 0f)
                {
                    ClampGrowthToScaleKg(prevScaleKg);
                    SetScalePaused(true);
                    currentScaleKg = prevScaleKg;
                    deltaScaleKg = 0f;
                }
                else
                {
                    float transfer = Mathf.Min(deltaScaleKg, headroom);
                    tracker.AddExternalMass(-transfer);
                    SetScalePaused(false);

                    if (deltaScaleKg > headroom)
                    {
                        float allowedScaleKg = prevScaleKg + headroom;
                        ClampGrowthToScaleKg(allowedScaleKg);
                        currentScaleKg = allowedScaleKg;
                    }
                }
            }
            else
            {
                // Shrink (shear/reset); do not refund to body
                SetScalePaused(false);
            }

            prevScaleKg = currentScaleKg;

            // 2) Handle milk production: transfer from body to milk
            float currentMilkKg = CreatureMassTracker.ComputeMilkMassKgFromMilkMonitor(gameObject);
            float deltaMilkKg = currentMilkKg - prevMilkKg;

            if (deltaMilkKg > 0f)
            {
                float headroom = BodyHeadroom();

                if (headroom <= 0f)
                {
                    ClampMilkToKg(prevMilkKg);
                    SetMilkPaused(true);
                    currentMilkKg = prevMilkKg;
                    deltaMilkKg = 0f;
                }
                else
                {
                    float transfer = Mathf.Min(deltaMilkKg, headroom);
                    tracker.AddExternalMass(-transfer);
                    SetMilkPaused(false);

                    if (deltaMilkKg > headroom)
                    {
                        float allowedMilkKg = prevMilkKg + headroom;
                        ClampMilkToKg(allowedMilkKg);
                        currentMilkKg = allowedMilkKg;
                    }
                }
            }
            else
            {
                // Milk reduced (milked/consumed); do not refund to body
                SetMilkPaused(false);
            }

            prevMilkKg = currentMilkKg;
        }

        private bool IsExempt()
        {

            return kpid != null && (kpid.HasTag(WoodDeerTag) || kpid.HasTag(BabyWoodDeerTag) || kpid.HasTag(GoldBellyTag) ||
                kpid.HasTag(BabyGoldBellyTag) || kpid.HasTag(SeaTurtleTag) || kpid.HasTag(BabySeaTurtleTag));
        }

        // Body mass available above the reserved minimum: how much can still be
        // converted into scales/milk this tick.
        private float BodyHeadroom() =>
            Mathf.Max(0f, tracker.GetBodyMass() - Mathf.Max(0.001f, tracker.STARTING_MASS));

        // Pause flags read by SMs
        private void SetScalePaused(bool value) => pausedScales = value;
        private bool IsScalePaused() => pausedScales;

        private void SetMilkPaused(bool value) => pausedMilk = value;
        private bool IsMilkPaused() => pausedMilk;

        // ===== Scales helpers =====
        // Scale/mass lookups live on CreatureMassTracker so this file has a single source of truth.

        private void ClampGrowthToScaleKg(float desiredScaleKg)
        {
            var db = Db.Get();
            var amt = db?.Amounts?.ScaleGrowth?.Lookup(gameObject);
            if (amt == null) return;

            if (!CreatureMassTracker.TryGetScaleDefAndPercent01(gameObject, out _, out var dropMass, out _))
                return;

            float pct01 = (dropMass > 0f) ? Mathf.Clamp01(desiredScaleKg / dropMass) : 0f;
            amt.value = pct01 * 100f;
        }

        // ===== Milk helpers =====
        // Clamp milk such that MilkAmount == desiredMilkKg
        private void ClampMilkToKg(float desiredMilkKg)
        {
            var milkSmi = gameObject.GetSMI<MilkProductionMonitor.Instance>();
            if (milkSmi == null) return;

            // Capacity in kg comes from the definition
            float capacity = milkSmi.def != null ? Mathf.Max(0f, milkSmi.def.Capacity) : 0f;
            if (capacity <= 0f) return;

            // Convert desired kg to percent [0..100]
            float pct = Mathf.Clamp01(desiredMilkKg / capacity) * 100f;

            // Set the AmountInstance value (percent), clamped to min/max supported by the amount
            var amt = milkSmi.milkAmountInstance;
            if (amt != null)
            {
                float min = amt.GetMin(); // typically 0
                float max = amt.GetMax(); // typically 100
                amt.SetValue(Mathf.Clamp(pct, min, max));
            }
        }
    }

    [HarmonyPatch(typeof(KPrefabID), "OnSpawn")]
    public static class Attach_ScaleMassTransferDriver_OnSpawn
    {
        public static void Postfix(KPrefabID __instance)
        {
           
                // Skip attachment on exempt prefabs entirely
                var kpid = __instance;
                if (kpid != null && (kpid.HasTag(ScaleMassTransferDriver.WoodDeerTag) ||
                    kpid.HasTag(ScaleMassTransferDriver.BabyWoodDeerTag) || kpid.HasTag(ScaleMassTransferDriver.GoldBellyTag) ||
                    kpid.HasTag(ScaleMassTransferDriver.BabyGoldBellyTag) || kpid.HasTag(ScaleMassTransferDriver.SeaTurtleTag) ||
                    kpid.HasTag(ScaleMassTransferDriver.BabySeaTurtleTag)))
                    return;

                var go = __instance.gameObject;
                if (go.GetComponent<CreatureMassTracker>() != null && go.GetComponent<ScaleMassTransferDriver>() == null)
                {
                    go.AddOrGet<ScaleMassTransferDriver>();
                }
            
          
        }
    }
}