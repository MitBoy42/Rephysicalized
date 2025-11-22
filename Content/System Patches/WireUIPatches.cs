using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using STRINGS;
namespace Rephysicalized.Content.System_Patches
{
    // Circuit UI capacity: report min effective capacity across the network for UI queries
    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.GetMaxSafeWattage))]
    public static class ElectricalUtilityNetwork_GetMaxSafeWattage_Patch
    {
        private static readonly System.Reflection.FieldInfo F_wireGroups = AccessTools.Field(typeof(ElectricalUtilityNetwork), "wireGroups");
        // Include bridges if the field exists on this ONI version
        private static readonly System.Reflection.FieldInfo F_bridgeGroups = AccessTools.Field(typeof(ElectricalUtilityNetwork), "bridgeGroups");

        public static bool Prefix(ElectricalUtilityNetwork __instance, ref float __result)
        {
            try
            {
                var wireGroups = (List<Wire>[])F_wireGroups.GetValue(__instance);

                float minEff;
                if (F_bridgeGroups != null)
                {
                    var bridgeGroups = (List<WireUtilityNetworkLink>[])F_bridgeGroups.GetValue(__instance);
                    minEff = WireMaterialCapacity.GetNetworkMinEffectiveMax(wireGroups, bridgeGroups);
                }
                else
                {
                    minEff = WireMaterialCapacity.GetNetworkMinEffectiveMax(wireGroups);
                }

                __result = minEff;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized/WireCapacity] GetMaxSafeWattage ERROR: {e}");
                return true;
            }
        }
    }

    // Building effects: replace MAX_WATTAGE descriptor (per-wire) with effective capacity
    [HarmonyPatch(typeof(Building), nameof(Building.GetDescriptors))]
    public static class Building_GetDescriptors_EffectiveMax_Patch
    {
        public static void Postfix([HarmonyArgument(0)] GameObject go, ref List<Descriptor> __result)
        {
            try
            {
                if (go == null || __result == null) return;
                var wire = go.GetComponent<Wire>();
                if (wire == null) return;
                float maxEff = WireMaterialCapacity.GetEffectiveMax(wire);

                // Tooltip key used by the vanilla "Max Wattage" descriptor
                string tooltipKey = (string)UI.BUILDINGEFFECTS.TOOLTIPS.MAX_WATTAGE;

                // Text template to identify the descriptor by its visible text as a fallback
                string template = (string)UI.BUILDINGEFFECTS.MAX_WATTAGE; // e.g., "Max Wattage: {0}"
                string prefix = template;
                int ph = template.IndexOf("{0}", StringComparison.Ordinal);
                if (ph >= 0) prefix = template.Substring(0, ph);

                for (int i = 0; i < __result.Count; i++)
                {
                    var d = __result[i];
                    bool isMaxLine =
                        string.Equals(d.tooltipText, tooltipKey, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(d.text) && d.text.StartsWith(prefix, StringComparison.Ordinal));

                    if (isMaxLine)
                    {
                        var nd = new Descriptor();
                        nd.SetupDescriptor(
                            string.Format((string)UI.BUILDINGEFFECTS.MAX_WATTAGE, GameUtil.GetFormattedWattage(maxEff)),
                            d.tooltipText ?? tooltipKey
                        );
                        __result[i] = nd;
                        break;
                    }
                }
            }
            catch { }
        }
    }
    // Wire UI statuses: ensure per-wire and circuit statuses use effective capacity
    [HarmonyPatch(typeof(Wire), nameof(Wire.OnPrefabInit))]
    public static class Wire_OnPrefabInit_StatusUIPatch
    {
        private static readonly System.Reflection.FieldInfo F_WireCircuitStatus = AccessTools.Field(typeof(Wire), "WireCircuitStatus");
        private static readonly System.Reflection.FieldInfo F_WireMaxWattageStatus = AccessTools.Field(typeof(Wire), "WireMaxWattageStatus");

        public static void Postfix()
        {
            var circuitStatus = (StatusItem)F_WireCircuitStatus.GetValue(null);
            if (circuitStatus != null)
            {
                circuitStatus.resolveStringCallback = (str, data) =>
                {
                    var wire = data as Wire;
                    if (wire == null) return str;
                    int cell = Grid.PosToCell(wire.transform.GetPosition());
                    var circuitManager = Game.Instance.circuitManager;
                    ushort circuitId = circuitManager.GetCircuitID(cell);
                    float wattsUsedByCircuit = circuitManager.GetWattsUsedByCircuit(circuitId);
                    GameUtil.WattageFormatterUnit unit = wire.MaxWattageRating >= Wire.WattageRating.Max20000 ? GameUtil.WattageFormatterUnit.Kilowatts : GameUtil.WattageFormatterUnit.Watts;
                    float maxEff = WireMaterialCapacity.GetEffectiveMax(wire);
                    float neededWhenActive = circuitManager.GetWattsNeededWhenActive(circuitId);
                    string wireLoadColor = GameUtil.GetWireLoadColor(wattsUsedByCircuit, maxEff, neededWhenActive);
                    string current = wattsUsedByCircuit < 0f ? "?" : GameUtil.GetFormattedWattage(wattsUsedByCircuit, unit);
                    string coloredCurrent = (wireLoadColor == Color.white.ToHexString()) ? current : ("<color=#" + wireLoadColor + ">" + current + "</color>");
                    str = str.Replace("{CurrentLoadAndColor}", coloredCurrent);
                    str = str.Replace("{MaxLoad}", GameUtil.GetFormattedWattage(maxEff, unit));
                    str = str.Replace("{WireType}", wire.GetProperName());
                    return str;
                };
            }

            var maxStatus = (StatusItem)F_WireMaxWattageStatus.GetValue(null);
            if (maxStatus != null)
            {
                maxStatus.resolveStringCallback = (str, data) =>
                {
                    var wire = data as Wire;
                    if (wire == null) return str;
                    GameUtil.WattageFormatterUnit unit = wire.MaxWattageRating >= Wire.WattageRating.Max20000 ? GameUtil.WattageFormatterUnit.Kilowatts : GameUtil.WattageFormatterUnit.Watts;
                    int cell = Grid.PosToCell(wire.transform.GetPosition());
                    var circuitManager = Game.Instance.circuitManager;
                    float neededWhenActive = circuitManager.GetWattsNeededWhenActive(circuitManager.GetCircuitID(cell));
                    float maxEff = WireMaterialCapacity.GetEffectiveMax(wire);
                    string totalPotential = neededWhenActive <= maxEff ? GameUtil.GetFormattedWattage(neededWhenActive, unit) : ("<color=#" + new Color(0.9843137f, 0.6901961f, 0.2313726f).ToHexString() + ">" + GameUtil.GetFormattedWattage(neededWhenActive, unit) + "</color>");
                    str = str.Replace("{TotalPotentialLoadAndColor}", totalPotential);
                    str = str.Replace("{MaxLoad}", GameUtil.GetFormattedWattage(maxEff, unit));
                    return str;
                };
            }


        }
    }

    // Material descriptor helper for wattage capacity
    internal static class WireWattageDescriptorHelper
    {
        public static void AppendDescriptor(Element element, ref List<Descriptor> descriptors)
        {
            if (element == null || descriptors == null)
                return;

            float mult = WireMaterialCapacity.GetMultiplier(element);
            if (Mathf.Approximately(mult, 1f))
                return;

            float deltaPct = (mult - 1f) * 100f;
            string sign = deltaPct >= 0f ? "+" : "";
            string value = sign + deltaPct.ToString("0.#") + "%";

            var d = new Descriptor();
            d.SetupDescriptor(string.Format((string)Rephysicalized.STRINGS.MATERIAL_MODIFIERS.WATTAGE, value),
                              string.Format((string)Rephysicalized.STRINGS.MATERIAL_MODIFIERS.TOOLTIP.WATTAGE, value));
            d.IncreaseIndent();
            descriptors.Add(d);
        }
    }

    // Patch GameUtil material descriptor builders to include wattage capacity modifier lines
    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Tag) })]
    internal static class GameUtil_GetMaterialDescriptors_Tag_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Tag tag, ref List<Descriptor> __result)
        {
            try
            {
                var elem = ElementLoader.GetElement(tag);
                if (elem == null) return;
                WireWattageDescriptorHelper.AppendDescriptor(elem, ref __result);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Element) })]
    internal static class GameUtil_GetMaterialDescriptors_Element_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Element element, ref List<Descriptor> __result)
        {
            try
            {
                WireWattageDescriptorHelper.AppendDescriptor(element, ref __result);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(MaterialSelector), nameof(MaterialSelector.ConfigureMaterialTooltips))]
    internal static class MaterialSelector_ConfigureMaterialTooltips_Patch
    {
        private static void Postfix(MaterialSelector __instance)
        {
            try
            {
                if (__instance == null || __instance.ElementToggles == null)
                    return;

                foreach (var kvp in __instance.ElementToggles)
                {
                    var tag = kvp.Key;
                    var toggle = kvp.Value;
                    if (toggle == null || toggle.gameObject == null)
                        continue;

                    var tt = toggle.gameObject.GetComponent<ToolTip>();
                    if (tt == null)
                        continue;

                    var elem = ElementLoader.GetElement(tag);
                    if (elem == null)
                        continue;

                    var list = new List<Descriptor>(1);
                    WireWattageDescriptorHelper.AppendDescriptor(elem, ref list);
                    if (list.Count == 0)
                        continue;

                    string extraLine = list[0].text;
                    if (string.IsNullOrEmpty(extraLine))
                        continue;

                    // Avoid adding the same line multiple times if tooltips are rebuilt
                    if (!tt.multiStringToolTips.Contains(extraLine))
                    {
                        tt.AddMultiStringTooltip(extraLine, PluginAssets.Instance.defaultTextStyleSetting);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized/WireCapacity] Failed to append wattage descriptor to material selector tooltip: {e}");
            }
        }
    }
}
