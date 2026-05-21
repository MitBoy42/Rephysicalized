using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ElectricityPatches
{


    // Per-instance cache of computed multipliers without adding new components
    internal static class TransformerMaterialState
    {
        private static readonly Dictionary<PowerTransformer, Holder> store = new Dictionary<PowerTransformer, Holder>();

        private sealed class Holder
        {
            public float Mul;
            public Battery BatteryRef;
            public float ScaledWattage;
            public bool HasMul;
        }

        internal static void Set(PowerTransformer pt, float mul, Battery batteryRef, float scaledWattage)
        {
            if (pt == null) return;
            store[pt] = new Holder { Mul = mul, BatteryRef = batteryRef, ScaledWattage = scaledWattage, HasMul = !Mathf.Approximately(mul, 1f) };
        }

        internal static bool TryGet(PowerTransformer pt, out float mul)
        {
            mul = 1f;
            if (pt == null) return false;
            if (store.TryGetValue(pt, out var h))
            {
                mul = h.Mul;
                return true;
            }
            return false;
        }

        internal static bool TryGetHolderData(PowerTransformer pt, out float mul, out Battery batteryRef, out float scaledWattage)
        {
            mul = 1f; batteryRef = null; scaledWattage = 0f;
            if (pt == null) return false;
            if (store.TryGetValue(pt, out var h))
            {
                mul = h.Mul;
                batteryRef = h.BatteryRef;
                scaledWattage = h.ScaledWattage;
                return true;
            }
            return false;
        }

        internal static void Remove(PowerTransformer pt)
        {
            if (pt == null) return;
            store.Remove(pt);
        }
    }

    // 1) Minimal base tweak: set normal transformer base rating/capacity to 2000 W at build-time.
    [HarmonyPatch(typeof(PowerTransformerConfig), nameof(PowerTransformerConfig.CreateBuildingDef))]
    public static class PowerTransformerConfig_CreateBuildingDef_Base2000_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            if (__result == null) return;
            __result.GeneratorWattageRating = 2000f;
            __result.GeneratorBaseCapacity = 2000f;
        }
    }

    // Remove cached entries when a transformer is destroyed to avoid memory growth.
    // Patch KMonoBehaviour.OnCleanUp and remove cache if the instance is a PowerTransformer.
    [HarmonyPatch(typeof(KMonoBehaviour), "OnCleanUp")]
    public static class KMonoBehaviour_OnCleanUp_RemoveTransformerCache_Patch
    {
        public static void Postfix(KMonoBehaviour __instance)
        {
            try
            {
                if (!Config.Instance.WattageCapacity) return;
                if (__instance is PowerTransformer pt) TransformerMaterialState.Remove(pt);
            }
            catch { }
        }
    }

    // 2) Compute material multiplier once per instance on spawn, and scale Battery to avoid battery bottleneck.
    [HarmonyPatch(typeof(PowerTransformer), nameof(PowerTransformer.OnSpawn))]
    public static class PowerTransformer_OnSpawn_MaterialMult_Patch
    {
        public static void Postfix(PowerTransformer __instance)
        {
            try
            {
                if (!Config.Instance.WattageCapacity) return;
                if (__instance == null) return;
                var go = __instance.gameObject;
                var building = go.GetComponent<Building>();
                var battery = go.GetComponent<Battery>();
                if (building == null || building.Def == null || battery == null) return;
                float baseWattage = building.Def.GeneratorWattageRating;
                var pe = go.GetComponent<PrimaryElement>();
                float mul = pe != null ? WireCapacitySettings.GetMultiplier(pe.ElementID) : 1f;
                float scaled = baseWattage * mul;
                TransformerMaterialState.Set(__instance, mul, battery, scaled);
                if (Mathf.Approximately(battery.capacity, baseWattage)) battery.capacity = scaled;
                if (Mathf.Approximately(battery.chargeWattage, baseWattage)) battery.chargeWattage = scaled;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransformerMaterialMult] OnSpawn failed: {ex}");
            }
        }
    }

    // 3) Apply the multiplier to the effective throughput cap each tick.
    // This affects BOTH small and normal transformers (both use PowerTransformer).
    [HarmonyPatch(typeof(PowerTransformer), nameof(PowerTransformer.EnergySim200ms))]
    public static class PowerTransformer_EnergySim200ms_MaterialMult_Patch
    {
        public static void Postfix(PowerTransformer __instance, float dt)
        {
            // Use the same logic as the original but scale WattageRating by our per-instance multiplier
            try
            {
                if (!Config.Instance.WattageCapacity) return;
                if (__instance == null) return;
                if (!__instance.operational.IsOperational) return;
                if (TransformerMaterialState.TryGetHolderData(__instance, out var mul, out var cachedBattery, out var scaledWattage))
                {
                    if (cachedBattery == null || Mathf.Approximately(mul, 1f)) return;
                    float cap = Mathf.Min(cachedBattery.JoulesAvailable, scaledWattage * dt);
                    __instance.AssignJoulesAvailable(cap);
                    return;
                }
                var battery = __instance.GetComponent<Battery>(); if (battery == null) return;
                float fallbackMul = 1f;
                if (!TransformerMaterialState.TryGet(__instance, out fallbackMul) || Mathf.Approximately(fallbackMul, 1f)) return;
                float capFallback = Mathf.Min(battery.JoulesAvailable, __instance.WattageRating * fallbackMul * dt);
                __instance.AssignJoulesAvailable(capFallback);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransformerMaterialMult] EnergySim200ms Postfix failed: {ex}");
            }
        }
    }
}