using HarmonyLib;
using Klei.AI;
using System;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
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
                    name: "Not enough body mass",
                    tooltip: "Milk production paused: not enough body mass to convert into milk.",
                    category: Db.Get().StatusItemCategories.Stored)
                .Update((smi, dt) =>
                {
                    if (!smi.isPaused())
                        smi.GoTo(ok);
                }, UpdateRate.SIM_1000ms);
        }
    }

    public sealed class ScaleMassTransferDriver : KMonoBehaviour, ISim200ms
    {
        // Adjust tags if your prefab IDs differ
        internal static readonly Tag WoodDeerTag = new Tag("WoodDeer");
        internal static readonly Tag BabyWoodDeerTag = new Tag("WoodDeerBaby");
        internal static readonly Tag GoldBellyTag = new Tag("GoldBelly");
        internal static readonly Tag BabyGoldBellyTag = new Tag("GoldBellyBaby");


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
            prevScaleKg = ComputeScaleMassKgFromMonitors(gameObject);
            prevMilkKg = ComputeMilkMassKgFromMonitor(gameObject);

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

        public void Sim200ms(float dt)
        {
            // No-op for exempt creatures even if attached
            if (IsExempt())
            {
                pausedScales = false;
                pausedMilk = false;
                return;
            }

            // 1) Handle scale growth: transfer from body to scales
            float currentScaleKg = ComputeScaleMassKgFromMonitors(gameObject);
            float deltaScaleKg = currentScaleKg - prevScaleKg;

            if (deltaScaleKg > 0f)
            {
                float body = tracker.GetBodyMass();
                float minBody = Mathf.Max(0.001f, tracker.STARTING_MASS);
                float headroom = Mathf.Max(0f, body - minBody);

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
            float currentMilkKg = ComputeMilkMassKgFromMonitor(gameObject);
            float deltaMilkKg = currentMilkKg - prevMilkKg;

            if (deltaMilkKg > 0f)
            {
                float body = tracker.GetBodyMass();
                float minBody = Mathf.Max(0.001f, tracker.STARTING_MASS);
                float headroom = Mathf.Max(0f, body - minBody);

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
            return kpid != null && (kpid.HasTag(WoodDeerTag) || kpid.HasTag(BabyWoodDeerTag ) || kpid.HasTag(GoldBellyTag) || kpid.HasTag(BabyGoldBellyTag));
        }

        // Pause flags read by SMs
        private void SetScalePaused(bool value) => pausedScales = value;
        private bool IsScalePaused() => pausedScales;

        private void SetMilkPaused(bool value) => pausedMilk = value;
        private bool IsMilkPaused() => pausedMilk;

        // ===== Scales helpers =====
        private static float ComputeScaleMassKgFromMonitors(GameObject go)
        {
            if (TryGetScaleDefAndPercent01(go, out _, out var dropMass, out var pct01))
                return pct01 * dropMass;
            return 0f;
        }

        private static bool TryGetScaleDefAndPercent01(GameObject go, out Tag dropTag, out float dropMass, out float percent01)
        {
            dropTag = Tag.Invalid;
            dropMass = 0f;
            percent01 = 0f;
            if (go == null) return false;

            float pct01 = 0f;
            var db = Db.Get();
            var scaleAmt = db?.Amounts?.ScaleGrowth?.Lookup(go);
            if (scaleAmt != null)
                pct01 = Mathf.Clamp01(scaleAmt.value / 100f);
            else
            {
                var elemAmt = db?.Amounts?.ElementGrowth?.Lookup(go);
                if (elemAmt != null)
                    pct01 = Mathf.Clamp01(elemAmt.value / 100f);
            }
            if (pct01 <= 0f) return false;

            Tag tag = Tag.Invalid;
            float fullMass = 0f;

            var sgm = go.GetSMI<ScaleGrowthMonitor.Instance>();
            if (sgm?.def != null)
            {
                tag = sgm.def.itemDroppedOnShear;
                fullMass = Mathf.Max(0f, sgm.def.dropMass);
            }
            else
            {
                var wfs = go.GetSMI<WellFedShearable.Instance>();
                if (wfs?.def != null)
                {
                    tag = wfs.def.itemDroppedOnShear;
                    fullMass = Mathf.Max(0f, wfs.def.dropMass);
                }
            }
            if (!tag.IsValid || fullMass <= 0f) return false;

            dropTag = tag;
            dropMass = fullMass;
            percent01 = pct01;
            return true;
        }

        private void ClampGrowthToScaleKg(float desiredScaleKg)
        {
            var db = Db.Get();
            var amt = db?.Amounts?.ScaleGrowth?.Lookup(gameObject);
            if (amt == null) return;

            if (!TryGetScaleDefAndPercent01(gameObject, out _, out var dropMass, out _))
                return;

            float pct01 = (dropMass > 0f) ? Mathf.Clamp01(desiredScaleKg / dropMass) : 0f;
            amt.value = pct01 * 100f;
        }

        // ===== Milk helpers =====
        private static float ComputeMilkMassKgFromMonitor(GameObject go)
        {
            var milkSmi = go != null ? go.GetSMI<MilkProductionMonitor.Instance>() : null;
            if (milkSmi == null) return 0f;
            // MilkAmount is in kg
            return Mathf.Max(0f, milkSmi.MilkAmount);
        }

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
            try
            {
                var go = __instance.gameObject;
                var kpid = __instance;

                // Skip attachment on exempt prefabs entirely
                if (kpid != null && (kpid.HasTag(ScaleMassTransferDriver.WoodDeerTag) || kpid.HasTag(ScaleMassTransferDriver.BabyWoodDeerTag) || kpid.HasTag(ScaleMassTransferDriver.GoldBellyTag) || kpid.HasTag(ScaleMassTransferDriver.BabyGoldBellyTag)))
                    return;

                if (go.GetComponent<CreatureMassTracker>() != null && go.GetComponent<ScaleMassTransferDriver>() == null)
                {
                    go.AddOrGet<ScaleMassTransferDriver>();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] Failed to attach ScaleMassTransferDriver: {e}");
            }
        }
    }
}