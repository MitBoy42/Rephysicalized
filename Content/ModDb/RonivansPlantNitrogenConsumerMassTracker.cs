using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{

    internal static class RonivansPlantNitrogenConsumerMassTracker
    {
        private const string ConsumerTypeName =
            "RonivansLegacy_ChemicalProcessing.Content.Scripts.PlantNitrogenConsumer";

        private static FieldInfo _consumerField;
        private static bool _installAttempted;

        /// <summary>
        /// Patches the nitrogen consumer if RonivansLegacy_ChemicalProcessing is loaded. Safe to call
        /// more than once; only the first call actually tries.
        /// </summary>
        public static void TryInstall()
        {
            if (_installAttempted)
                return;

            _installAttempted = true;

            try
            {
                var consumerType = AccessTools.TypeByName(ConsumerTypeName);
                if (consumerType == null)
                    return; // Ronivans' mod is not installed - nothing to do.

                var sim = AccessTools.Method(consumerType, "Sim200ms", new[] { typeof(float) });
                if (sim == null)
                    return;

                _consumerField = AccessTools.Field(consumerType, "nitrogenConsumer");
                if (_consumerField == null)
                    return;

                var harmony = Mod.HarmonyInstance ?? new Harmony("Rephysicalized.RonivansPlantNitrogen");
                harmony.Patch(sim, prefix: new HarmonyMethod(
                    typeof(RonivansPlantNitrogenConsumerMassTracker), nameof(Sim200msPrefix)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlantMassTracker] Nitrogen mass integration not applied: {e.Message}");
            }
        }


        private static void Sim200msPrefix(Component __instance)
        {
            var elementConsumer = _consumerField?.GetValue(__instance) as ElementConsumer;
            if (elementConsumer == null)
                return;

            float consumedKg = elementConsumer.consumedMass;
            if (consumedKg <= 0f)
                return;


            PlantMassTrackerRegistry.EnsureTracker(__instance.gameObject)?.AddMassDelta(consumedKg);
        }
    }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class RonivansPlantNitrogenRegistration
    {
        private static void Postfix()
        {
            RonivansPlantNitrogenConsumerMassTracker.TryInstall();
        }
    }
}