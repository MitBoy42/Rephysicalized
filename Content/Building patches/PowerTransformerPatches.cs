using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Patches
{
    // Central map of multipliers
    internal static class TransformerMaterialTuning
    {
        internal static readonly Dictionary<SimHashes, float> Map = new Dictionary<SimHashes, float>
        {
            { SimHashes.Copper, 1.5f },
            { SimHashes.Cuprite, 1.2f },
            { SimHashes.Gold, 1.2f },
            { SimHashes.GoldAmalgam, 1.1f },
            { SimHashes.Aluminum, 1.2f },
            { SimHashes.AluminumOre, 1.1f },
            { SimHashes.Iridium, 1.2f },
            { SimHashes.TempConductorSolid, 1.2f },
            { SimHashes.Lead, 0.8f },
            { SimHashes.DepletedUranium, 0.7f },
            { SimHashes.UraniumOre, 0.8f },
            { SimHashes.Cinnabar, 0.8f },
            { SimHashes.SolidMercury, 2.5f },
        };

        internal static float GetMultiplier(GameObject go)
        {
            if (go == null) return 1f;
            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null && Map.TryGetValue(pe.ElementID, out var mul))
                return mul;
            return 1f;
        }
    }

    // Per-instance cache of computed multipliers without adding new components
    internal static class TransformerMaterialState
    {
        private static readonly ConditionalWeakTable<PowerTransformer, Holder> store = new ConditionalWeakTable<PowerTransformer, Holder>();

        private sealed class Holder
        {
            public float Mul;
        }

        internal static void Set(PowerTransformer pt, float mul)
        {
            if (pt == null) return;
            store.Remove(pt);
            store.Add(pt, new Holder { Mul = mul });
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

    // 2) Compute material multiplier once per instance on spawn, and scale Battery to avoid battery bottleneck.
    [HarmonyPatch(typeof(PowerTransformer), nameof(PowerTransformer.OnSpawn))]
    public static class PowerTransformer_OnSpawn_MaterialMult_Patch
    {
        public static void Postfix(PowerTransformer __instance)
        {
            try
            {
                if (__instance == null) return;

                var go = __instance.gameObject;
                var building = go.GetComponent<Building>();
                var battery = go.GetComponent<Battery>();
                if (building == null || building.Def == null || battery == null)
                    return;

                float baseWattage = building.Def.GeneratorWattageRating; // shared def
                float mul = TransformerMaterialTuning.GetMultiplier(go);
                TransformerMaterialState.Set(__instance, mul);

                // Avoid double-scaling on load: only scale if battery still equals base values
                if (Mathf.Approximately(battery.capacity, baseWattage))
                {
                    float scaled = baseWattage * mul;
                    battery.capacity = scaled;
                }

                if (Mathf.Approximately(battery.chargeWattage, baseWattage))
                {
                    float scaled = baseWattage * mul;
                    battery.chargeWattage = scaled;
                }
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
                if (__instance == null) return;
                if (!__instance.operational.IsOperational) return;

                var battery = __instance.GetComponent<Battery>();
                if (battery == null) return;

                float mul = 1f;
                if (!TransformerMaterialState.TryGet(__instance, out mul) || Mathf.Approximately(mul, 1f))
                    return;

                // Original did: AssignJoulesAvailable(min(battery.JoulesAvailable, WattageRating * dt))
                float cap = Mathf.Min(battery.JoulesAvailable, __instance.WattageRating * mul * dt);
                __instance.AssignJoulesAvailable(cap);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransformerMaterialMult] EnergySim200ms Postfix failed: {ex}");
            }
        }
    }
}