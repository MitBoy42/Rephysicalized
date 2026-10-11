using HarmonyLib;
using Rephysicalized.Content.Plant_patches;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    internal static class BlueGrassTuning
    {
        // Base CO2 consumption (kg/s) at 0 lux, replanted vs wild. Scaled up by light below.
        public const float BASE_DOMESTICATED = 1f / 500f; // 0.002 kg/s
        public const float BASE_WILD = 0.0005f;

        public const float LUX_SATURATION = 100000f;
        public const float MAX_FACTOR = 100f; // 1x at 0 lux -> 100x at 200k lux

        public const float BUFFER_CAPACITY_KG = 1f;

        public static float RateForLux(int lux, bool replanted)
        {
            float baseRate = replanted ? BASE_DOMESTICATED : BASE_WILD;
            float t = Mathf.Clamp01(lux / LUX_SATURATION);
            return baseRate * Mathf.Lerp(1f, MAX_FACTOR, t);
        }

        public static int LuxAt(GameObject go)
        {
            int cell = Grid.PosToCell(go.transform.GetPosition());
            return Grid.IsValidCell(cell) ? Grid.LightIntensity[cell] : 0;
        }
    }

    [HarmonyPatch(typeof(EntityConfigManager), "LoadGeneratedEntities")]
    public static class Bluegrass_AddToDescription
    {
        public static void Postfix()
        {
            const string key = "STRINGS.CODEX.BLUE_GRASS.BODY.CONTAINER1";
            Strings.Add(key, Strings.Get(key) + "\n\n<b>REPHYSICALIZED: Massively increases CO2 consumption under light. At 100 000 Lux or higher, it will consume 100g/s of CO2 and turn it into plant mass.</b>");
        }
    }

    /// <summary>
    /// Blue Grass is fed by the shared gas-consumer mechanic: an internal CO2 buffer that an
    /// ElementConsumer refills from the environment, which the plant then eats. Consumption is driven by
    /// BlueGrassLightScaler, and an empty buffer only stops growth - it does not wilt the plant.
    /// </summary>
    [HarmonyPatch(typeof(BlueGrassConfig), nameof(BlueGrassConfig.CreatePrefab))]
    internal static class BlueGrassConfig_CreatePrefab_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null)
                return;

            ExtendPlantToGasConsumer.Extend(
                __result,
                new PlantGasConsumerInfo(SimHashes.CarbonDioxide.CreateTag(), BlueGrassTuning.BASE_DOMESTICATED)
                {
                    storageCapacityKg = BlueGrassTuning.BUFFER_CAPACITY_KG,
                    consumptionRadius = 3,
                    consumerSampleOffset = Vector3.zero,
                    showStorageInUI = true,
                
                    wiltsWhenGasDepleted = true,
                    requiredMassKg = 0.1f
                });

            __result.AddOrGet<BlueGrassLightScaler>();
        }
    }

    [HarmonyPatch(typeof(BlueGrass), nameof(BlueGrass.OnSpawn))]
    internal static class BlueGrass_OnSpawn_Patch
    {
        private static void Postfix(BlueGrass __instance)
        {
            if (__instance == null)
                return;

            // The PlantGasConsumer registers the CO2 buffer with the Plant Mass Tracker itself, so the
            // gas it eats is credited to the plant without any extra bookkeeping here.
            var scaler = __instance.gameObject.AddOrGet<BlueGrassLightScaler>();
            scaler.ApplyLightScaling();
        }
    }

    /// <summary>Scales the plant's CO2 consumption with the light falling on it.</summary>
    public sealed class BlueGrassLightScaler : KMonoBehaviour, ISim1000ms
    {
        [MyCmpGet] private ReceptacleMonitor _receptacle;
        [MyCmpGet] private PlantGasConsumer _gasConsumer;

        private float _lastAppliedRate = -1f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplyLightScaling();
        }

        public void Sim1000ms(float dt) => ApplyLightScaling();

        /// <summary>Applies the lux-scaled rate to the gas consumer, if it has changed.</summary>
        public void ApplyLightScaling()
        {
            if (_gasConsumer == null)
                return;

            float rate = BlueGrassTuning.RateForLux(
                BlueGrassTuning.LuxAt(gameObject),
                _receptacle != null && _receptacle.Replanted);

            if (Mathf.Approximately(rate, _lastAppliedRate))
                return;

            _lastAppliedRate = rate;
            _gasConsumer.SetConsumptionRate(rate);
        }

        /// <summary>
        /// Forces the rate to be reapplied. Needed after vanilla resets the consumer, otherwise the
        /// unchanged-rate guard would skip the correction.
        /// </summary>
        public void ForceResync() => _lastAppliedRate = -1f;
    }

    /// <summary>
    /// Vanilla resets the CO2 consumption rate whenever the plant is replanted. Re-apply our lux-scaled
    /// value straight afterwards so the plant does not snap back to the vanilla rate.
    /// </summary>
    [HarmonyPatch(typeof(BlueGrass), nameof(BlueGrass.SetConsumptionRate))]
    internal static class BlueGrass_SetConsumptionRate_Patch
    {
        private static void Postfix(BlueGrass __instance)
        {
            if (__instance == null)
                return;

            var scaler = __instance.GetComponent<BlueGrassLightScaler>();
            if (scaler == null)
                return;

            scaler.ForceResync();
            scaler.ApplyLightScaling();
        }
    }

    [HarmonyPatch(typeof(BlueGrassConfig), nameof(BlueGrassConfig.CreatePrefab))]
    public static class BlueGrassConfig_SafeElements_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PlantSafeAtmosphereTranspiler.CollapseSafeAtmospheres(instructions);
    }
}
