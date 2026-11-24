using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using TUNING;

namespace Rephysicalized.Content.System_Patches.RealBreathing
{
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class BreathingTweaks_ContaminatedLungs_Patch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Config.Instance.RealBreathing)
                return;

            Effect effect = null;
            try { effect = Db.Get().effects.Get("ContaminatedLungs"); } catch { }
        
            foreach (var modifier in effect.SelfModifiers)
            {
                if (modifier != null && modifier.Value == 0.03f)
                    modifier.SetValue(0.002f);
            }
        }
    }



    [HarmonyPatch(typeof(CoughMonitor), "OnBreatheDirtyAir", new[] { typeof(CoughMonitor.Instance), typeof(object) })]
    internal static class BreathingTweaks_CoughMonitor_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(ref object data)
        {
            if (!Config.Instance.RealBreathing)
                return;

            if (data is Boxed<float> boxed)
                boxed.value *= 50f;
        }
    }

    // Redirect Sim200ms to use the ConsumptionRate property (which we can patch)
    // instead of calling airConsumptionRate.GetTotalValue() directly.
    [HarmonyPatch(typeof(OxygenBreather), nameof(OxygenBreather.Sim200ms))]
    internal static class BreathingTweaks_Sim200ms_Transpiler
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var consumptionRateGetter = AccessTools.Property(typeof(OxygenBreather), nameof(OxygenBreather.ConsumptionRate)).GetMethod;

            for (int i = 0; i < codes.Count; i++)
            {
                // Pattern: ldfld airConsumptionRate ; callvirt GetTotalValue
                if (i > 0 &&
                    codes[i - 1].opcode == OpCodes.Ldfld &&
                    codes[i - 1].operand is FieldInfo field && field.Name == "airConsumptionRate" &&
                    codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand is MethodInfo method && method.Name == "GetTotalValue")
                {
                    // Remove the ldfld airConsumptionRate
                    codes.RemoveAt(i - 1);
                    i--;

                    // Replace callvirt GetTotalValue with callvirt get_ConsumptionRate
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, consumptionRateGetter);
                }
            }

            return codes;
        }
    }

    // Instead, ConsumptionRate is boosted proportionally to the oxygen debt, so a minion
    // repays the oxygen they should have breathed while suffocating. This makes oxygen
    // consumption roughly constant across a suffocate→recover cycle.
    //
    // During the Recover Breath chore, breath refills faster than passive recovery
    // (RECOVER_BREATH_DELTA > BREATH_RATE), so debt repayment is scaled by that same
    // ratio — oxygen consumption can exceed 2x while the chore is active.
    [HarmonyPatch(typeof(OxygenBreather), nameof(OxygenBreather.ConsumptionRate), MethodType.Getter)]
    internal static class BreathingTweaks_ConsumptionRate_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result, OxygenBreather __instance)
        {
            if (!Config.Instance.RealBreathing) return;

            if (OxygenDebtTracker.TryGetDebt(__instance, out float debt) && debt > 0f)
            {
                // Base repayment rate: up to 1x normal metabolic rate.
                float repaymentRate = __result;

                // If actively recovering breath via the chore, scale repayment by how much
                // faster breath recovers compared to passive breathing.
                if (__instance.prefabID != null && __instance.prefabID.HasTag(GameTags.RecoveringBreath))
                {
                    float recoverBreathDelta = DUPLICANTSTATS.STANDARD.BaseStats.RECOVER_BREATH_DELTA;
                    float breathRate = DUPLICANTSTATS.STANDARD.Breath.BREATH_RATE;
                    if (breathRate > 0f)
                        repaymentRate *= (recoverBreathDelta / breathRate);
                }

                // Repay debt at up to repaymentRate.
                __result += Mathf.Min(debt, repaymentRate);
            }
        }
    }

    // Track oxygen debt: accumulate when oxygen is unavailable, reduce when breathing.
    [HarmonyPatch(typeof(OxygenBreather), nameof(OxygenBreather.Sim200ms))]
    internal static class BreathingTweaks_Sim200ms_DebtTracker
    {
        [HarmonyPostfix]
        private static void Postfix(OxygenBreather __instance, float dt)
        {
            if (!Config.Instance.RealBreathing) return;
            if (__instance.prefabID.HasTag(GameTags.Dead)) return;

            float normalRatePerSecond = OxygenDebtTracker.GetNormalConsumptionRate(__instance);
            float normalConsumption = normalRatePerSecond * dt;

            var provider = __instance.GetCurrentGasProvider();
            bool oxygenAvailable = provider != null && provider.HasOxygen();

            if (oxygenAvailable)
            {
                // Able to breathe - reduce debt by what was repaid this tick.
                OxygenDebtTracker.ReduceDebtByRepayment(__instance, normalRatePerSecond, dt);
            }
            else
            {
                // Could not breathe - accumulate oxygen debt.
                OxygenDebtTracker.AddDebt(__instance, normalConsumption);
            }
        }
    }

    // Clean up debt tracking when the breather is destroyed.
    [HarmonyPatch(typeof(OxygenBreather), nameof(OxygenBreather.OnCleanUp))]
    internal static class BreathingTweaks_OnCleanUp
    {
        [HarmonyPostfix]
        private static void Postfix(OxygenBreather __instance)
        {
            OxygenDebtTracker.Remove(__instance);
        }
    }

    // Static per-breather oxygen debt tracker.
    internal static class OxygenDebtTracker
    {
        private static readonly Dictionary<OxygenBreather, float> debt = new();
        private const float MaxOxygenDebt = 10f; // kg of O2, prevents runaway inhale after long suffocation

        public static bool TryGetDebt(OxygenBreather breather, out float value)
        {
            return debt.TryGetValue(breather, out value);
        }

        public static void AddDebt(OxygenBreather breather, float amount)
        {
            if (!debt.TryGetValue(breather, out float current))
                current = 0f;
            debt[breather] = Mathf.Min(current + amount, MaxOxygenDebt);
        }

        public static void ReduceDebtByRepayment(OxygenBreather breather, float normalRatePerSecond, float dt)
        {
            if (!debt.TryGetValue(breather, out float current) || current <= 0f) return;

            // Repayment rate matches ConsumptionRate_Patch: base 1x normal, scaled during Recover Breath chore.
            float repaymentRate = normalRatePerSecond;
            if (breather.prefabID != null && breather.prefabID.HasTag(GameTags.RecoveringBreath))
            {
                float recoverBreathDelta = DUPLICANTSTATS.STANDARD.BaseStats.RECOVER_BREATH_DELTA;
                float breathRate = DUPLICANTSTATS.STANDARD.Breath.BREATH_RATE;
                if (breathRate > 0f)
                    repaymentRate *= (recoverBreathDelta / breathRate);
            }

            // Repay debt at up to repaymentRate, matching the excess oxygen consumed this tick.
            float repaid = Mathf.Min(current, repaymentRate) * dt;
            float remaining = current - repaid;
            if (remaining <= 0f)
                debt.Remove(breather);
            else
                debt[breather] = remaining;
        }

        public static float GetNormalConsumptionRate(OxygenBreather breather)
        {
            var field = AccessTools.Field(typeof(OxygenBreather), "airConsumptionRate");
            var attr = field.GetValue(breather) as AttributeInstance;
            return attr?.GetTotalValue() ?? 0f;
        }

        public static void Remove(OxygenBreather breather)
        {
            debt.Remove(breather);
        }
    }
}
