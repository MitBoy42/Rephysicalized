using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(EntityConfigManager), "LoadGeneratedEntities")]
    public static class Oxyfern_AddToDescription
    {
        public static void Prefix()
        {
            const string key = "STRINGS.CODEX.OXYFERN.BODY.CONTAINER1";
            Strings.Add(key, Strings.Get(key) + "\n\n<b>REPHYSICALIZED: Massively increases CO2 consumption and Oxygen production under light. At 50 000 Lux or higher, it will consume 50g/s of CO2 and make 80 g/s of Oxygen.</b>");
        }
    }

    internal static class OxyfernTuning
    {
        // Rates at 0 lux, for replanted (domesticated) and wild plants respectively.
        public const float DOMESTIC_WATER_RATE = 0.015f;
        public const float DOMESTIC_DIRT_RATE = 0.015f;
        public const float DOMESTIC_CO2_RATE = 0.005f;
        public const float DOMESTIC_O2_RATE = 0.035f;
        public const float WILD_CO2_RATE = 0.002f;
        public const float WILD_O2_RATE = 0.002f;

        // Rates reached once the plant is fully lit.
        public const float LUX_SATURATION = 50000f;
        public const float TARGET_CO2_RATE = 0.05f;
        public const float TARGET_O2_RATE = 0.08f;

        // Vanilla Oxygen output mass, used as the multiplier baseline.
        public const float VANILLA_O2_OUTPUT_ELEMENT = 0.03125f;

        // Raw CO2 pull from the environment into the plant's own buffer. Deliberately far above the
        // plant's own consumption so the buffer stays topped up regardless of lux.
        public const float CONST_ENV_CO2_PULL = 0.1f;

        // Size of the plant's internal CO2 buffer, in kg. Also caps the ElementConsumer's capacityKG so
        // the plant cannot hoard an unbounded amount of gas.
        public const float BUFFER_CAPACITY_KG = 1f;
    }

    /// <summary>
    /// Scales the Oxyfern's CO2 consumption and oxygen output with the light falling on it.
    ///
    /// Water and dirt stay fixed: only the gas side reacts to lux. The ElementConverter is retuned each
    /// tick (CO2 in, O2 out) while the ElementConsumer that refills the internal buffer is left on a
    /// constant rate, so the buffer behaves like PlantGasConsumer's: it fills at a fixed rate and the
    /// plant eats from it at the lux-scaled rate. Net gas is roughly mass neutral.

    /// </summary>
    [AddComponentMenu("KMonoBehaviour/Rephysicalized/OxyfernLightScaler")]
    public sealed class OxyfernLightScaler : KMonoBehaviour, ISim1000ms
    {
        private static readonly Tag Co2Tag = SimHashes.CarbonDioxide.CreateTag();
        private static readonly Tag WaterTag = SimHashes.Water.CreateTag();
        private static readonly Tag DirtTag = GameTags.Dirt;

        private ReceptacleMonitor receptacle;
        private ElementConsumer gasConsumer;
        private ElementConverter converter;
        private float baseOxygenElementRate = OxyfernTuning.VANILLA_O2_OUTPUT_ELEMENT;
        private float lastCo2Rate = -1f;
        private float lastO2Rate = -1f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            receptacle = GetComponent<ReceptacleMonitor>();
            converter = GetComponent<ElementConverter>();
            gasConsumer = GetComponents<ElementConsumer>().FirstOrDefault(c => c != null && c.elementToConsume == SimHashes.CarbonDioxide);

            if (converter?.outputElements != null)
            {
                var oxygen = converter.outputElements.FirstOrDefault(oe => oe.elementHash == SimHashes.Oxygen);
                if (oxygen.elementHash == SimHashes.Oxygen)
                    baseOxygenElementRate = oxygen.massGenerationRate;
            }

            ApplyConstantBufferRefill();
            ApplyDomesticatedRates();
            lastCo2Rate = lastO2Rate = -1f;
            Sim1000ms(0f);
        }

        /// <summary>
        /// Forces the converter rates to be rewritten on the next tick.
        /// Vanilla <c>SetConsumptionRate</c> overwrites the CO2 consumer's rate whenever the plant is
        /// replanted, so the cached "already applied" values have to be invalidated or the lux scaling
        /// would never be re-applied after a replant.
        /// </summary>
        public void ResyncAfterVanillaReset()
        {
            lastCo2Rate = -1f;
            lastO2Rate = -1f;
        }

        public void Sim1000ms(float dt)
        {
            if (converter == null)
                return;

            int cell = Grid.PosToCell(transform.GetPosition());
            float lux = Grid.IsValidCell(cell) ? Grid.LightIntensity[cell] : 0f;
            float t = Mathf.Clamp01(lux / OxyfernTuning.LUX_SATURATION);

            bool domesticated = receptacle != null && receptacle.Replanted;
            float co2 = Mathf.Lerp(domesticated ? OxyfernTuning.DOMESTIC_CO2_RATE : OxyfernTuning.WILD_CO2_RATE, OxyfernTuning.TARGET_CO2_RATE, t);
            float o2 = Mathf.Lerp(domesticated ? OxyfernTuning.DOMESTIC_O2_RATE : OxyfernTuning.WILD_O2_RATE, OxyfernTuning.TARGET_O2_RATE, t);

            ApplyConstantBufferRefill();

            if (Mathf.Approximately(co2, lastCo2Rate) && Mathf.Approximately(o2, lastO2Rate))
                return;

            // ElementConverter multiplies OUTPUTS by OutputMultiplier but never its inputs, so the CO2
            // input rate is set directly and the multiplier only tunes how much oxygen comes out.
            converter.outputMultiplier = o2 / Mathf.Max(baseOxygenElementRate, 1e-6f);

            var consumed = converter.consumedElements;
            for (int i = 0; i < consumed.Length; i++)
            {
                if (consumed[i].Tag == Co2Tag)
                {
                    consumed[i].MassConsumptionRate = co2;
                    break;
                }
            }

            lastCo2Rate = co2;
            lastO2Rate = o2;
        }

        /// <summary>
        /// Keeps the CO2 consumer pulling a constant amount into the plant's internal buffer, regardless
        /// of lux or of vanilla <c>SetConsumptionRate</c> (which runs on replant). Also hides its UI,
        /// since the buffered gas is an implementation detail and the real rate is on the converter.
        /// capacityKG is pinned to the buffer size so the consumer stops once the plant's buffer is
        /// full and resumes as the plant eats into it. Left unset it defaults to infinity, which would
        /// let the plant accumulate an unbounded amount of CO2.
        /// </summary>
        private void ApplyConstantBufferRefill()
        {
            if (gasConsumer == null)
                return;

            if (!Mathf.Approximately(gasConsumer.consumptionRate, OxyfernTuning.CONST_ENV_CO2_PULL))
                gasConsumer.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;

            gasConsumer.capacityKG = OxyfernTuning.BUFFER_CAPACITY_KG;

            gasConsumer.showInStatusPanel = false;
            gasConsumer.showDescriptor = false;
        }

        /// <summary>
        /// Applies the replanted water/dirt rates. Vanilla changes these on replant too, but only to its
        /// own values; this re-applies ours afterwards so the UI and the sim agree.
        /// </summary>
        public void ApplyDomesticatedRates()
        {
            if (receptacle == null || !receptacle.Replanted)
                return;

            // Read through the Defs rather than reflecting on the live monitor components: the Def is
            // what both the UI descriptors and the Codex read, so this keeps them consistent.
            var irrigDef = gameObject.GetDef<IrrigationMonitor.Def>();
            if (irrigDef?.consumedElements != null)
            {
                for (int i = 0; i < irrigDef.consumedElements.Length; i++)
                {
                    if (irrigDef.consumedElements[i].tag == WaterTag)
                        irrigDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_WATER_RATE;
                }
            }

            var dirtElementTag = SimHashes.Dirt.CreateTag();
            var fertDef = gameObject.GetDef<FertilizationMonitor.Def>();
            if (fertDef?.consumedElements != null)
            {
                for (int i = 0; i < fertDef.consumedElements.Length; i++)
                {
                    var tag = fertDef.consumedElements[i].tag;
                    if (tag == DirtTag || tag == dirtElementTag)
                        fertDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_DIRT_RATE;
                }
            }
        }
    }

    [HarmonyPatch(typeof(OxyfernConfig), nameof(OxyfernConfig.CreatePrefab))]
    public static class OxyfernConfig_CreatePrefab_Patch
    {
        /// <summary>Survive in any atmosphere. See PlantSafeAtmosphereTranspiler.</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PlantSafeAtmosphereTranspiler.CollapseSafeAtmospheres(instructions);

        public static void Postfix(GameObject __result)
        {
            if (__result == null)
                return;

            // Show the internal gas buffer and the converter so the lux-scaled rates are visible.
            var storage = __result.GetComponent<Storage>();
            if (storage != null)
                storage.showInUI = true;

            // Cap the gas buffer so the consumer stops filling a full buffer and resumes as the plant
            // eats into it, instead of accumulating an unbounded amount of CO2.
            var gasConsumers = __result.GetComponents<ElementConsumer>();
            foreach (var c in gasConsumers)
            {
                if (c != null && c.elementToConsume == SimHashes.CarbonDioxide)
                    c.capacityKG = OxyfernTuning.BUFFER_CAPACITY_KG;
            }

            // Prefab descriptors reflect the replanted rates, matching what the scaler applies.
            var irrigDef = __result.GetDef<IrrigationMonitor.Def>();
            if (irrigDef?.consumedElements != null)
            {
                var waterTag = SimHashes.Water.CreateTag();
                for (int i = 0; i < irrigDef.consumedElements.Length; i++)
                {
                    if (irrigDef.consumedElements[i].tag == waterTag)
                        irrigDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_WATER_RATE;
                }
            }

            var fertDef = __result.GetDef<FertilizationMonitor.Def>();
            if (fertDef?.consumedElements != null)
            {
                var dirtElementTag = SimHashes.Dirt.CreateTag();
                for (int i = 0; i < fertDef.consumedElements.Length; i++)
                {
                    var tag = fertDef.consumedElements[i].tag;
                    if (tag == dirtElementTag || tag == GameTags.Dirt)
                        fertDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_DIRT_RATE;
                }
            }

  
            var consumers = __result.GetComponents<ElementConsumer>();
            foreach (var consumer in consumers)
            {
                if (consumer != null && consumer.elementToConsume == SimHashes.CarbonDioxide)
                {
                    consumer.showInStatusPanel = false;
                    consumer.showDescriptor = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.OnSpawn))]
    public static class Oxyfern_OnSpawn_Patch
    {
        public static void Postfix(Oxyfern __instance)
        {
            if (__instance == null)
                return;

            var go = __instance.gameObject;
            go.AddOrGet<OxyfernLightScaler>();

            // Covers plants already placed in older saves.
            var storage = go.GetComponent<Storage>();
            if (storage != null)
                storage.showInUI = true;
        }
    }


    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.OnReplanted))]
    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.SetConsumptionRate))]
    public static class Oxyfern_OnReplanted_Patch
    {
        public static void Postfix(Oxyfern __instance)
        {
            if (__instance == null)
                return;

            var scaler = __instance.GetComponent<OxyfernLightScaler>();
            if (scaler == null)
                return;

            scaler.ApplyDomesticatedRates();
            scaler.ResyncAfterVanillaReset();
        }
    }
}
