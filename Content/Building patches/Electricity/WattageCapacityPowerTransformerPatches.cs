using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Rephysicalized;
using UnityEngine;

namespace Rephysicalized.Patches
{


    [HarmonyPatch(typeof(PowerTransformer), "OnSpawn")]
    public static class PowerTransformer_OnSpawn_MaterialMult_Patch
    {
        public static void Postfix(PowerTransformer __instance)
        {
            if (!Config.Instance.WireCapacity)
                return;

            var battery = __instance.GetComponent<Battery>();
                var primaryElement = __instance.GetComponent<PrimaryElement>();
                if (battery == null || primaryElement == null)
                    return;

                float mul = WireCapacitySettings.GetMultiplier(primaryElement.ElementID);
                if (!(mul > 0f) || Mathf.Approximately(mul, 1f))
                    return;

                battery.capacity *= mul;
                battery.chargeWattage *= mul;
        
        }
    }

    // Vanilla caps throughput at WattageRating * dt, read from the shared BuildingDef.
    // Re-cap from the per-instance battery capacity instead (vanilla capacity == base rating),
    // keeping the generator-output Efficiency factor vanilla applies.
    [HarmonyPatch(typeof(PowerTransformer), nameof(PowerTransformer.EnergySim200ms))]
    public static class PowerTransformer_EnergySim200ms_MaterialMult_Patch
    {
        public static void Postfix(PowerTransformer __instance, float dt)
        {
            if (!Config.Instance.WireCapacity)
                return;
            if (!__instance.operational.IsOperational)
                return;

            var battery = __instance.GetComponent<Battery>();
            float baseRating = __instance.BaseWattageRating;
            if (battery == null || !(baseRating > 0f) || Mathf.Approximately(battery.capacity, baseRating))
                return;

            float efficiency = __instance.WattageRating / baseRating;
            __instance.AssignJoulesAvailable(Mathf.Min(battery.JoulesAvailable, battery.capacity * efficiency * dt));
        }
    }
}
