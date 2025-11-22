using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace Rephysicalized.Patches
{
    // Attach the scaler at prefab build time (earliest reliable point)
    [HarmonyPatch(typeof(AirConditionerConfig), nameof(AirConditionerConfig.DoPostConfigureComplete))]
    public static class AirConditionerConfig_DoPostConfigureComplete_AddScaler
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            if (go == null) return;
            go.AddOrGet<ConditionerPowerScaler>();
      //      ConditionerPowerScaler.MaybeLog($"[CondPowerScaler] Attached via AirConditionerConfig to prefab {go.name}");
        }
    }

    [HarmonyPatch(typeof(LiquidConditionerConfig), nameof(LiquidConditionerConfig.DoPostConfigureComplete))]
    public static class LiquidConditionerConfig_DoPostConfigureComplete_AddScaler
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            if (go == null) return;
            go.AddOrGet<ConditionerPowerScaler>();
        //    ConditionerPowerScaler.MaybeLog($"[CondPowerScaler] Attached via LiquidConditionerConfig to prefab {go.name}");
        }
    }

    // Safety net: ensure scaler exists even if another mod replaced config hooks
    [HarmonyPatch(typeof(AirConditioner), nameof(AirConditioner.OnSpawn))]
    public static class AirConditioner_OnSpawn_AddScaler
    {
        [HarmonyPostfix]
        private static void Postfix(AirConditioner __instance)
        {
            if (__instance == null) return;
            __instance.gameObject.AddOrGet<ConditionerPowerScaler>();
          //  ConditionerPowerScaler.MaybeLog($"[CondPowerScaler] Ensured on AirConditioner.OnSpawn for {__instance.gameObject.name}");
        }
    }

    // Scales EnergyConsumer.BaseWattageRating based on the specific heat capacity
    // of the fluid currently processed. Baselines: gas=4, liquid=6.
    [SkipSaveFileSerialization]
    public sealed class ConditionerPowerScaler : KMonoBehaviour, ISim1000ms
    {
        // Toggle for logs
        public static bool DebugEnabled = false;

        [MyCmpReq] private EnergyConsumer energyConsumer;
        [MyCmpReq] private BuildingComplete building;
        [MyCmpReq] private AirConditioner air;

        // Optional fallbacks; not all conditioners buffer or expose last element
        [MyCmpGet] private Storage storage;
        [MyCmpGet] private ConduitConsumer conduitConsumer;

        private float baseWatts;    // unscaled draw from Def
        private float baselineShc;  // 4 for gas, 6 for liquid
        private float lastApplied = float.NaN;

        private int outputCell = Grid.InvalidCell;

        // Debug throttles
        private float lastProbeLog;
        private float lastApplyLog;

        public override void OnSpawn()
        {
            base.OnSpawn();

            // Determine baseline by mode (same AirConditioner handles both via this flag)
            baselineShc = air.isLiquidConditioner ? 6f : 4f;

            // Base watts from building def
            baseWatts = (building != null && building.Def != null)
                ? building.Def.EnergyConsumptionWhenActive
                : energyConsumer.BaseWattageRating;

            // Ensure consumer starts from base
            energyConsumer.BaseWattageRating = baseWatts;

            // Cache output cell (same as used by AirConditioner internally)
            try { outputCell = building.GetUtilityOutputCell(); } catch { outputCell = Grid.InvalidCell; }

            MaybeLog($"[CondPowerScaler] OnSpawn {gameObject.name} mode={(air.isLiquidConditioner ? "Liquid" : "Gas")} baseWatts={baseWatts} baselineShc={baselineShc} outCell={outputCell}");
        }

        public void Sim1000ms(float dt)
        {
            if (energyConsumer == null || air == null) return;

            float shc = ProbeCurrentSHC();
            if (shc <= 0f)
            {
                Apply(baseWatts, "idle/no-fluid");
                return;
            }

            float mult = Mathf.Max(0f, shc / Mathf.Max(0.0001f, baselineShc));
            float target = baseWatts * mult;
            Apply(target, $"shc={shc:0.###}, mult={mult:0.###}");
        }

        private float ProbeCurrentSHC()
        {
            // 1) Read from the conduit output contents (authoritative)
            float shc = ProbeSHCFromConduitOutput();
            if (shc > 0f)
            {
                ThrottledProbeLog($"[CondPowerScaler] {gameObject.name} SHC from conduit out={shc:0.###}");
                return shc;
            }

            // 2) Fallback: ConduitConsumer fields (varies by build)
            shc = ProbeSHCFromConduitConsumer();
            if (shc > 0f)
            {
                ThrottledProbeLog($"[CondPowerScaler] {gameObject.name} SHC from conduit consumer={shc:0.###}");
                return shc;
            }

            // 3) Fallback: scan storage buffer
            try
            {
                if (storage != null && storage.items != null)
                {
                    for (int i = 0; i < storage.items.Count; i++)
                    {
                        var go = storage.items[i];
                        if (go == null) continue;

                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe == null || pe.Mass <= 0f) continue;

                        // Liquid conditioner processes liquids; air conditioner processes gases
                        if (air.isLiquidConditioner)
                        {
                            if (pe.Element.IsLiquid) return pe.Element.specificHeatCapacity;
                        }
                        else
                        {
                            if (pe.Element.IsGas) return pe.Element.specificHeatCapacity;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MaybeLog($"[CondPowerScaler] Probe SHC failed on {gameObject.name}: {e}");
            }

            ThrottledProbeLog($"[CondPowerScaler] {gameObject.name} no valid fluid detected");
            return 0f;
        }

        private float ProbeSHCFromConduitOutput()
        {
            try
            {
                if (!Grid.IsValidCell(outputCell)) return 0f;

                if (air.isLiquidConditioner)
                {
                    var contents = Game.Instance.liquidConduitFlow.GetContents(outputCell);
                    if (contents.mass > 0f)
                    {
                        var elem = ElementLoader.FindElementByHash(contents.element);
                        if (elem != null && elem.IsLiquid) return elem.specificHeatCapacity;
                    }
                }
                else
                {
                    var contents = Game.Instance.gasConduitFlow.GetContents(outputCell);
                    if (contents.mass > 0f)
                    {
                        var elem = ElementLoader.FindElementByHash(contents.element);
                        if (elem != null && elem.IsGas) return elem.specificHeatCapacity;
                    }
                }
            }
            catch (Exception e)
            {
                MaybeLog($"[CondPowerScaler] Conduit out probe failed on {gameObject.name}: {e}");
            }
            return 0f;
        }

        private float ProbeSHCFromConduitConsumer()
        {
            try
            {
                if (conduitConsumer == null) return 0f;

                // Try a property named lastSelectedElement
                var elemProp = conduitConsumer.GetType().GetProperty("lastSelectedElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (elemProp != null)
                {
                    var elemObj = elemProp.GetValue(conduitConsumer, null);
                    if (elemObj is Element e1)
                    {
                        if (air.isLiquidConditioner && e1.IsLiquid) return e1.specificHeatCapacity;
                        if (!air.isLiquidConditioner && e1.IsGas) return e1.specificHeatCapacity;
                    }
                }

                // Common private fields across builds
                var tc = Traverse.Create(conduitConsumer);

                // Element field
                try
                {
                    var e = tc.Field("lastSelectedElement").GetValue<Element>();
                    if (e != null)
                    {
                        if (air.isLiquidConditioner && e.IsLiquid) return e.specificHeatCapacity;
                        if (!air.isLiquidConditioner && e.IsGas) return e.specificHeatCapacity;
                    }
                }
                catch { }

                // Hash field (SimHashes)
                try
                {
                    var hash = tc.Field("lastSelectedElementID").GetValue<SimHashes>();
                    if (hash != SimHashes.Unobtanium)
                    {
                        var e2 = ElementLoader.FindElementByHash(hash);
                        if (e2 != null)
                        {
                            if (air.isLiquidConditioner && e2.IsLiquid) return e2.specificHeatCapacity;
                            if (!air.isLiquidConditioner && e2.IsGas) return e2.specificHeatCapacity;
                        }
                    }
                }
                catch { }
            }
            catch (Exception e)
            {
                MaybeLog($"[CondPowerScaler] Conduit consumer probe failed on {gameObject.name}: {e}");
            }

            return 0f;
        }

        private void Apply(float watts, string reason)
        {
            if (watts <= 0f) return;

            // Avoid tiny jitter; still log occasionally if nothing changes
            if (!float.IsNaN(lastApplied) && Mathf.Abs(lastApplied - watts) < 0.1f)
            {
                ThrottledApplyLog($"[CondPowerScaler] {gameObject.name} stable watts={watts:0.##} (base={baseWatts:0.##}) reason={reason}");
                return;
            }

            energyConsumer.BaseWattageRating = watts;
            lastApplied = watts;

            MaybeLog($"[CondPowerScaler] {gameObject.name} set watts={watts:0.##} (base={baseWatts:0.##}) reason={reason}");
        }

        private void ThrottledProbeLog(string msg)
        {
            if (!DebugEnabled) return;
            if (Time.unscaledTime - lastProbeLog > 2f)
            {
                lastProbeLog = Time.unscaledTime;
                Debug.Log(msg);
            }
        }

        private void ThrottledApplyLog(string msg)
        {
            if (!DebugEnabled) return;
            if (Time.unscaledTime - lastApplyLog > 2f)
            {
                lastApplyLog = Time.unscaledTime;
                Debug.Log(msg);
            }
        }

        public static void MaybeLog(string msg)
        {
            if (DebugEnabled)
                Debug.Log(msg);
        }
    }
}