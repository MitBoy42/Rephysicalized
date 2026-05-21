using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using STRINGS;
namespace Rephysicalized.Content.ElectricityPatches
{


    // Building effects: replace MAX_WATTAGE descriptor (per-wire) with effective capacity
    [HarmonyPatch(typeof(Building), nameof(Building.GetDescriptors))]
    public static class Building_GetDescriptors_EffectiveMax_Patch
    {
        public static void Postfix([HarmonyArgument(0)] GameObject go, ref List<Descriptor> __result)
        {
            if (!Config.Instance.WattageCapacity) return;

            var wire = go.GetComponent<Wire>();

            float maxEff = WireMaterialCapacity.GetEffectiveMaxCached(wire);



            string tooltipKey = (string)UI.BUILDINGEFFECTS.TOOLTIPS.MAX_WATTAGE;

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
    }
    // Wire UI statuses: ensure per-wire and circuit statuses use effective capacity
    [HarmonyPatch(typeof(Wire), nameof(Wire.OnPrefabInit))]
    public static class Wire_OnPrefabInit_StatusUIPatch
    {


        public static void Postfix()
        {
            if (!Config.Instance.WattageCapacity) return;
            var circuitStatus = Wire.WireCircuitStatus;


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
                    float maxEff = Mathf.Max(0f, WireMaterialCapacity.GetEffectiveMaxCached(wire));

                    // GameUtil thresholds assume vanilla max-wattage mapping; recompute using our effective max instead.

                    // This ensures UI color matches the gameplay overload calculation.
                    float ratio = maxEff <= 0f ? 1f : (wattsUsedByCircuit / maxEff);
                    string whiteHex = Color.white.ToHexString();
                    string wireLoadColor = ratio > 1f ? Color.red.ToHexString() : whiteHex;

                    string current = wattsUsedByCircuit < 0f ? "?" : GameUtil.GetFormattedWattage(wattsUsedByCircuit, unit);
                    string coloredCurrent = (wireLoadColor == whiteHex) ? current : ("<color=#" + wireLoadColor + ">" + current + "</color>");
                    str = str.Replace("{CurrentLoadAndColor}", coloredCurrent);
                    str = str.Replace("{MaxLoad}", GameUtil.GetFormattedWattage(maxEff, unit));
                    str = str.Replace("{WireType}", wire.GetProperName());
                    return str;
                };
            }

            var maxStatus = Wire.WireMaxWattageStatus; 

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
                    float maxEff = Mathf.Max(0f, WireMaterialCapacity.GetEffectiveMaxCached(wire));
                    string warnHex = new Color(0.9843137f, 0.6901961f, 0.2313726f).ToHexString();
                    string totalPotential = neededWhenActive <= maxEff ? GameUtil.GetFormattedWattage(neededWhenActive, unit) : ("<color=#" + warnHex + ">" + GameUtil.GetFormattedWattage(neededWhenActive, unit) + "</color>");
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
            if (!Config.Instance.WattageCapacity) return;
            if (element == null || descriptors == null)
                return;

            float mult = WireMaterialCapacity.GetMultiplier(element);
            if (Mathf.Approximately(mult, 1f))
                return;

            float deltaPct = (mult - 1f) * 100f;
            string sign = deltaPct >= 0f ? "+" : "";
            string value = sign + deltaPct.ToString("0.#") + "%";

            var d = new Descriptor();
            d.SetupDescriptor(string.Format((string)STRINGS.MATERIAL_MODIFIERS.WATTAGE, value),
                              string.Format((string)STRINGS.MATERIAL_MODIFIERS.TOOLTIP.WATTAGE, value));
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
            if (!Config.Instance.WattageCapacity) return;
            var elem = ElementLoader.GetElement(tag);
            if (elem == null) return;
            WireWattageDescriptorHelper.AppendDescriptor(elem, ref __result);

        }
    }

    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Element) })]
    internal static class GameUtil_GetMaterialDescriptors_Element_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Element element, ref List<Descriptor> __result)
        {

            if (!Config.Instance.WattageCapacity) return;
            WireWattageDescriptorHelper.AppendDescriptor(element, ref __result);


        }
    }

    [HarmonyPatch(typeof(MaterialSelector), nameof(MaterialSelector.ConfigureMaterialTooltips))]
    internal static class MaterialSelector_ConfigureMaterialTooltips_Patch
    {
        private static void Postfix(MaterialSelector __instance)
        {
            if (!Config.Instance.WattageCapacity) return;
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

    }

}

