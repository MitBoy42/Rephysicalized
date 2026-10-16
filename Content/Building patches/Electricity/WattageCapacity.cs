using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using STRINGS;
using Klei;
using static Wire;
using Rephysicalized.Content.System_Patches;


namespace Rephysicalized
{
    internal static class WireCapacitySettings
    {
        private static readonly Dictionary<SimHashes, float> SimMultipliers = new Dictionary<SimHashes, float>();
        private static readonly Dictionary<string, float> StringMultipliers = new Dictionary<string, float>((IEqualityComparer<string>)StringComparer.Ordinal);

        public static void InitializeBaseMultipliers()
        {
            WireCapacitySettings.SimMultipliers.Clear();
            WireCapacitySettings.StringMultipliers.Clear();
            WireCapacitySettings.SimMultipliers[SimHashes.Copper] = 1.3f;
            WireCapacitySettings.SimMultipliers[SimHashes.Cuprite] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.Gold] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.Zinc] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.ZincOre] = 1.1f;
            WireCapacitySettings.SimMultipliers[SimHashes.GoldAmalgam] = 1.1f;
            WireCapacitySettings.SimMultipliers[SimHashes.Aluminum] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.AluminumOre] = 1.1f;
            WireCapacitySettings.SimMultipliers[SimHashes.Iridium] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.TempConductorSolid] = 1.2f;
            WireCapacitySettings.SimMultipliers[SimHashes.Lead] = 0.8f;
            WireCapacitySettings.SimMultipliers[SimHashes.Galena] = 0.8f;
            WireCapacitySettings.SimMultipliers[SimHashes.DepletedUranium] = 0.7f;
            WireCapacitySettings.SimMultipliers[SimHashes.UraniumOre] = 0.8f;
            WireCapacitySettings.SimMultipliers[SimHashes.Cinnabar] = 0.8f;
            WireCapacitySettings.SimMultipliers[SimHashes.SolidMercury] = 2.5f;
            WireCapacitySettings.StringMultipliers["SolidSilver"] = 1.3f;
            WireCapacitySettings.StringMultipliers["ArgentiteOre"] = 1.2f;
            WireCapacitySettings.StringMultipliers["Plasteel"] = 0.2f;
            WireCapacitySettings.StringMultipliers["AIO_FerroChrome_Solid"] = 0.2f;
            WireCapacitySettings.StringMultipliers["AIO_Chromium_Solid"] = 0.8f;
            WireCapacitySettings.StringMultipliers["CarbonFiber"] = 0.2f;
        }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class WireCapacitySettingsInitializer
    {
        public static void Postfix()
        {
            WireCapacitySettings.InitializeBaseMultipliers();
        }
    }

   

        public static float GetMultiplier(SimHashes id)
        {
            float b;
            if (WireCapacitySettings.SimMultipliers.TryGetValue(id, out b))
                return Mathf.Max(0.0f, b);
            string key = id.CreateTag().ToString();
            return WireCapacitySettings.StringMultipliers.TryGetValue(key, out b) ? Mathf.Max(0.0f, b) : 1f;
        }
    }

    internal static class WattageCapacityDescriptorHelper
    {
        public static void AppendDescriptor(Element element, ref List<Descriptor> descriptors)
        {
            if (element == null || descriptors == null)
                return;

            float mult = WireCapacitySettings.GetMultiplier(element.id) ;
         //   float mult = WireCapacitySettings.GetMultiplier(element.id) * 2f - 1f;
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



    // Patch GameUtil material descriptor builders to include capacity modifier lines
    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Tag) })]
    internal static class GameUtil_GetWattageCapacityDescriptors_Tag_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Tag tag, ref List<Descriptor> __result)
        {

            var elem = ElementLoader.GetElement(tag);
            if (elem == null) return;
            WattageCapacityDescriptorHelper.AppendDescriptor(elem, ref __result);

        }
    }

    [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), new System.Type[] { typeof(Element) })]
    internal static class GameUtil_GetWattageCapacityDescriptors_Element_Patch
    {
        private static void Postfix([HarmonyArgument(0)] Element element, ref List<Descriptor> __result)
        {

            WattageCapacityDescriptorHelper.AppendDescriptor(element, ref __result);


        }
    }

    [HarmonyPatch(typeof(MaterialSelector), nameof(MaterialSelector.ConfigureMaterialTooltips))]
    internal static class MaterialSelector_ConfigureMaterialWattageTooltips_Patch
    {
        private static void Postfix(MaterialSelector __instance)
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
                WattageCapacityDescriptorHelper.AppendDescriptor(elem, ref list);
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

    [HarmonyPatch(typeof(ElectricalUtilityNetwork), "UpdateOverloadTime")]
    internal static class ElectricalUtilityNetwork_UpdateOverloadTime_Postfix
    {
        // Looked up once, not on every call (this postfix runs for every circuit every 200 ms).
        private static readonly System.Reflection.FieldInfo wireGroupsField = typeof(ElectricalUtilityNetwork)
            .GetField("wireGroups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Every wire ZapComponent this postfix has started. Wires only ever get a ZapComponent from
        // the StartZapping below, so stopping these is the same as calling GetComponent<ZapComponent>()
        // on every wire of the network, which cost ~7.6 ms per 200 ms tick on a 27 MB save.
        private static readonly HashSet<ZapComponent> zapping = new HashSet<ZapComponent>();
        private static readonly List<ZapComponent> stopped = new List<ZapComponent>();

        private static void StopZappingOn(ElectricalUtilityNetwork network)
        {
            if (zapping.Count == 0)
                return;
            var allWires = network.allWires;
            foreach (var zap in zapping)
            {
                if (zap == null)
                {
                    stopped.Add(zap);
                    continue;
                }
                var wire = zap.GetComponent<Wire>();
                if (wire != null && allWires != null && allWires.Contains(wire))
                {
                    zap.StopZapping();
                    stopped.Add(zap);
                }
            }
            foreach (var zap in stopped)
                zapping.Remove(zap);
            stopped.Clear();
        }

        public static void Postfix(ElectricalUtilityNetwork __instance, float dt, float watts_used, List<WireUtilityNetworkLink>[] bridgeGroups)
        {
            try
            {
                // Replicate vanilla overload flag condition: watts_used above some rating's max
                // AND there is at least one wire/link in that rating group.
                bool isOverloadedCondition = false;

                var wireGroups = wireGroupsField?.GetValue(__instance) as List<Wire>[];

                for (int rating = 0; rating < 6 && !isOverloadedCondition; rating++)
                {
                    var wireGroup = wireGroups != null ? wireGroups[rating] : null;
                    var bridgeGroup = bridgeGroups != null && rating < bridgeGroups.Length ? bridgeGroups[rating] : null;

                    float max = Wire.GetMaxWattageAsFloat((Wire.WattageRating)rating) + TUNING.POWER.FLOAT_FUDGE_FACTOR;

                    if ((double)watts_used > (double)max &&
                        ((bridgeGroup != null && bridgeGroup.Count > 0) || (wireGroup != null && wireGroup.Count > 0)))
                    {
                        isOverloadedCondition = true;
                    }
                }

                // Extra condition: if vanilla overload condition is false, stop zapping instantly.
                if (!isOverloadedCondition)
                {
                    StopZappingOn(__instance);
                    return;
                }

                var targetOverloadedWire = __instance.targetOverloadedWire;
                if (targetOverloadedWire == null)
                {
                    // Vanilla hasn't selected a target yet.
                    StopZappingOn(__instance);
                    return;
                }

                if (!DlcManager.IsContentSubscribed("DLC3_ID"))
                    return;
                var hp = targetOverloadedWire.GetComponent<BuildingHP>();
                if (hp == null || hp.IsBroken)
                    return;
                var zap = targetOverloadedWire.GetComponent<ZapComponent>();
                if (zap == null)
                    zap = targetOverloadedWire.AddComponent<ZapComponent>();

                zap.DecorativeBeamScale = 0.5f;
                zap.DecorativeBeamOffset = new Vector3(0f, 0.5f, 0f);
                zap.DamageBeamOffset = new Vector3(0f, -1f, 0f);
                zap.SHOCK_RADIUS = 4f;
                zap.DAMAGE_RATE = 2f;
                zap.enabled = true;
                zap.StartZapping();
                zapping.Add(zap);

            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] WireCapacity postfix failed: {e}");
            }

        }
    }


}