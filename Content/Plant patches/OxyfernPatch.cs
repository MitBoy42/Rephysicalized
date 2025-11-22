using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
    internal static class OxyfernTuning
    {
        // Domesticated (replanted) base rates @ 0 lux
        public const float DOMESTIC_WATER_RATE = 0.015f;
        public const float DOMESTIC_DIRT_RATE = 0.015f;
        public const float DOMESTIC_CO2_RATE = 0.005f;
        public const float DOMESTIC_O2_RATE = 0.035f;

        // Wild (not replanted) base rates @ 0 lux (keep vanilla)
        public const float WILD_CO2_RATE = 0.00015625f; // from Oxyfern.SetConsumptionRate()
        public const float WILD_O2_RATE = 0.03125f;     // from OxyfernConfig

        // Lux scaling
        public const float LUX_SATURATION = 50000f;     // 50k lux saturation point
        public const float TARGET_CO2_RATE = 0.05f;     // >= 50k lux
        public const float TARGET_O2_RATE = 0.08f;      // >= 50k lux

        // Vanilla OxyfernConfig oxygen base output element mass
        public const float VANILLA_O2_OUTPUT_ELEMENT = 0.03125f;

        // Constant raw pull from the environment via ElementConsumer
        public const float CONST_ENV_CO2_PULL = 0.1f;
    }

    // Applies domesticated (replanted) base consumption changes and
    // dynamically scales CO2 conversion and O2 production with light (lux) from 0..50k.
    // Note: CO2 pull from environment is forced to a constant 0.1f elsewhere in this file.
    [AddComponentMenu("KMonoBehaviour/Rephysicalized/OxyfernLightScaler")]
    public sealed class OxyfernLightScaler : KMonoBehaviour, ISim1000ms
    {
        private ReceptacleMonitor receptacle;
        private ElementConsumer gasConsumer; // CO2 consumer
        private ElementConverter converter;

        // Optional convenience refs (exist on plants) used to adjust irrigation/fertilization rates
        private Component irrigation;    // "Irrigation" MonoBehaviour
        private Component fertilization; // "Fertilization" MonoBehaviour

        private float baseOxygenElementRate = OxyfernTuning.VANILLA_O2_OUTPUT_ELEMENT;

        private readonly Tag waterTag = SimHashes.Water.CreateTag();
        private readonly Tag dirtTag = GameTags.Dirt;
        private readonly Tag co2Tag = SimHashes.CarbonDioxide.CreateTag();

        // Cache last-applied values to avoid unnecessary writes every tick
        private float lastDesiredCO2 = -1f;
        private float lastDesiredO2 = -1f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            receptacle = GetComponent<ReceptacleMonitor>();

            // Pick the ElementConsumer that consumes CO2
            var consumers = GetComponents<ElementConsumer>();
            if (consumers != null && consumers.Length > 0)
                gasConsumer = consumers.FirstOrDefault(c => c != null && c.elementToConsume == SimHashes.CarbonDioxide);

            converter = GetComponent<ElementConverter>();

            if (converter != null && converter.outputElements != null && converter.outputElements.Length > 0)
            {
                // Capture the existing configured oxygen base element mass (vanilla is 0.03125f)
                var oxygen = converter.outputElements.FirstOrDefault(oe => oe.elementHash == SimHashes.Oxygen);
                if (oxygen.elementHash == SimHashes.Oxygen)
                    baseOxygenElementRate = oxygen.massGenerationRate;
                // Converter UI is enabled on prefab patch below
            }

            if (gasConsumer != null)
            {
                // Force constant CO2 pull from environment; hide consumer UI in scaler to avoid flicker
                gasConsumer.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;
                gasConsumer.showInStatusPanel = false;
                gasConsumer.showDescriptor = false;
            }

            // Grab Irrigation/Fertilization components if present
            irrigation = GetComponent("Irrigation");
            fertilization = GetComponent("Fertilization");

            // Apply domesticated irrigation/fertilization if already replanted
            TryApplyDomesticatedIrrigationAndFertilization();
        }

        public void Sim1000ms(float dt)
        {
            if (converter == null)
                return;

            int cell = Grid.PosToCell(transform.GetPosition());
            float lux = Grid.IsValidCell(cell) ? (float)Grid.LightIntensity[cell] : 0f;
            float t = Mathf.Clamp01(lux / OxyfernTuning.LUX_SATURATION);

            bool isDomesticated = receptacle != null && receptacle.Replanted;

            float baseCO2 = isDomesticated ? OxyfernTuning.DOMESTIC_CO2_RATE : OxyfernTuning.WILD_CO2_RATE;
            float baseO2 = isDomesticated ? OxyfernTuning.DOMESTIC_O2_RATE : OxyfernTuning.WILD_O2_RATE;

            // Desired rates based on lux interpolation
            float desiredCO2 = Mathf.Lerp(baseCO2, OxyfernTuning.TARGET_CO2_RATE, t);
            float desiredO2 = Mathf.Lerp(baseO2, OxyfernTuning.TARGET_O2_RATE, t);

            // Only update when changed enough
            if (Mathf.Approximately(desiredCO2, lastDesiredCO2) && Mathf.Approximately(desiredO2, lastDesiredO2))
            {
                // Still ensure constant env pull (defensive)
                if (gasConsumer != null && !Mathf.Approximately(gasConsumer.consumptionRate, OxyfernTuning.CONST_ENV_CO2_PULL))
                {
                    gasConsumer.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;
                    gasConsumer.showInStatusPanel = false;
                    gasConsumer.showDescriptor = false;
                }
                return;
            }

            // Compute output multiplier so that produced oxygen equals desiredO2
            float multiplier = desiredO2 / Mathf.Max(baseOxygenElementRate, 1e-6f);

                // Tune O2 to desiredO2 via output multiplier
                converter.outputMultiplier = multiplier;

                // IMPORTANT: ElementConverter does NOT multiply inputs by outputMultiplier.
                // Therefore, set the CO2 input rate directly to desiredCO2 (do NOT divide by multiplier).
                for (int i = 0; i < converter.consumedElements.Length; i++)
                {
                    var consumed = converter.consumedElements[i];
                    if (consumed.Tag == co2Tag)
                    {
                        converter.consumedElements[i].MassConsumptionRate = desiredCO2;
                        break;
                    }
                }
      

            // Keep the gas consumer as a constant raw pull from the environment (do not show in UI)
            if (gasConsumer != null)
            {
                gasConsumer.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;
                gasConsumer.showInStatusPanel = false;
                gasConsumer.showDescriptor = false;
            }

            lastDesiredCO2 = desiredCO2;
            lastDesiredO2 = desiredO2;
        }

        // Applies domesticated irrigation/fertilization rates when replanted.
        // Updates Irrigation/Fertilization consumedElements entries for Water and Dirt.
        public void TryApplyDomesticatedIrrigationAndFertilization()
        {
            if (receptacle == null || !receptacle.Replanted)
                return;

            // Helper to adjust a component that owns PlantElementAbsorber.ConsumeInfo[] consumedElements
            void AdjustRatesOn(object comp)
            {
                if (comp == null) return;

                var t = Traverse.Create(comp);
                var infos = t.Field("consumedElements").GetValue<PlantElementAbsorber.ConsumeInfo[]>();
                if (infos == null || infos.Length == 0)
                    return;

                bool changed = false;
                for (int i = 0; i < infos.Length; i++)
                {
                    if (infos[i].tag == waterTag && !Mathf.Approximately(infos[i].massConsumptionRate, OxyfernTuning.DOMESTIC_WATER_RATE))
                    {
                        infos[i].massConsumptionRate = OxyfernTuning.DOMESTIC_WATER_RATE;
                        changed = true;
                    }
                    else if (infos[i].tag == dirtTag && !Mathf.Approximately(infos[i].massConsumptionRate, OxyfernTuning.DOMESTIC_DIRT_RATE))
                    {
                        infos[i].massConsumptionRate = OxyfernTuning.DOMESTIC_DIRT_RATE;
                        changed = true;
                    }
                }

                if (changed)
                {
                    // Write back in case the array is copied on set
                    t.Field("consumedElements").SetValue(infos);

                    // Try common refresh methods present on Irrigation/Fertilization
                    var type = comp.GetType();
                    var refresh =
                        type.GetMethod("RefreshConsumptionRate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetMethod("OnPrefabInit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (refresh != null)
                    {
                        try { refresh.Invoke(comp, null); } catch { /* ignore */ }
                    }
                }
            }

            AdjustRatesOn(irrigation);
            AdjustRatesOn(fertilization);
        }
    }

    // Ensure our scaler is present on each Oxyfern; also ensure Storage is visible in UI on instances
    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.OnSpawn))]
    public static class Oxyfern_OnSpawn_Patch
    {
        public static void Postfix(Oxyfern __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;

            // Attach scaler
            go.AddOrGet<OxyfernLightScaler>();

            // Ensure storage shows in UI on spawned instances (covers existing saves)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
                storage.showInUI = true;

          
        }
    }

    // Re-apply domesticated irrigation/fertilizer when the plant is replanted
    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.OnReplanted))]
    public static class Oxyfern_OnReplanted_Patch
    {
        public static void Postfix(Oxyfern __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;

            var scaler = go.GetComponent<OxyfernLightScaler>();
            scaler?.TryApplyDomesticatedIrrigationAndFertilization();

            // Keep constant environment CO2 pull after vanilla SetConsumptionRate and hide consumer UI
            var consumers = go.GetComponents<ElementConsumer>();
            if (consumers != null)
            {
                foreach (var c in consumers)
                {
                    if (c != null && c.elementToConsume == SimHashes.CarbonDioxide)
                    {
                        c.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;
                        c.showInStatusPanel = false;
                        c.showDescriptor = false;
                    }
                }
            }
        }
    }

    // Force constant CO2 pull immediately after vanilla SetConsumptionRate runs
    [HarmonyPatch(typeof(Oxyfern), nameof(Oxyfern.SetConsumptionRate))]
    public static class Oxyfern_SetConsumptionRate_Patch
    {
        public static void Postfix(Oxyfern __instance)
        {
            var consumers = __instance.GetComponents<ElementConsumer>();
            if (consumers == null) return;

            foreach (var c in consumers)
            {
                if (c != null && c.elementToConsume == SimHashes.CarbonDioxide)
                {
                    // Constant raw pull from environment; do not scale elsewhere; hide consumer UI
                    c.consumptionRate = OxyfernTuning.CONST_ENV_CO2_PULL;
                    c.showInStatusPanel = false;
                    c.showDescriptor = false;
                }
            }
        }
    }

    // Ensure UI descriptors reflect domesticated irrigation/fertilization rates,
    // show converter in UI, hide consumer UI, and show Storage in UI on prefab
    [HarmonyPatch(typeof(OxyfernConfig), nameof(OxyfernConfig.CreatePrefab))]
    public static class OxyfernConfig_CreatePrefab_Patch
    {
        public static void Postfix(GameObject __result)
        {
            if (__result == null) return;

            // Update Irrigation UI (Water)
            var irrigDef = __result.GetDef<IrrigationMonitor.Def>();
            if (irrigDef != null && irrigDef.consumedElements != null)
            {
                var waterTag = SimHashes.Water.CreateTag();
                for (int i = 0; i < irrigDef.consumedElements.Length; i++)
                {
                    if (irrigDef.consumedElements[i].tag == waterTag)
                    {
                        irrigDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_WATER_RATE; // 0.015f
                    }
                }
            }

            // Update Fertilization UI (Dirt)
            var fertDef = __result.GetDef<FertilizationMonitor.Def>();
            if (fertDef != null && fertDef.consumedElements != null)
            {
                var dirtElementTag = SimHashes.Dirt.CreateTag();
                var dirtGameTag = GameTags.Dirt;

                for (int i = 0; i < fertDef.consumedElements.Length; i++)
                {
                    var tag = fertDef.consumedElements[i].tag;
                    if (tag == dirtElementTag || tag == dirtGameTag)
                    {
                        fertDef.consumedElements[i].massConsumptionRate = OxyfernTuning.DOMESTIC_DIRT_RATE; // 0.015f
                    }
                }
            }

            // Show Storage in UI on the prefab
            var storage = __result.GetComponent<Storage>();
            if (storage != null)
                storage.showInUI = true;

            // Show ElementConverter in UI so conversion rates can be tracked
            var converter = __result.GetComponent<ElementConverter>();
            if (converter != null)
            {
                // Prefer direct fields/properties if available in your build
                try { converter.showDescriptors = true; } catch { /* older builds may differ */ }
                try { converter.ShowInUI = true; } catch { /* older builds may differ */ }
            }

            // Hide ElementConsumer UI/descriptor on prefab for CO2 consumers
            var consumers = __result.GetComponents<ElementConsumer>();
            if (consumers != null)
            {
                foreach (var c in consumers)
                {
                    if (c != null && c.elementToConsume == SimHashes.CarbonDioxide)
                    {
                        c.showInStatusPanel = false;
                        c.showDescriptor = false;
                    }
                }
            }
        }
    }
}