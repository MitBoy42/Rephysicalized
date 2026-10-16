using System;
using System.Collections.Generic;
using HarmonyLib;
using STRINGS;
using UnityEngine;

namespace Rephysicalized
{
    /// <summary>
    /// M2: the four places a player reads a wire's rating (scope U1-U4). The sim-side readers
    /// (overlay tint, hover card, details panel, diagnostic) already follow H5 and need nothing.
    /// </summary>
    public static class WattageCapacityUI
    {
        public static LocString MAX_WATTAGE_BASE = "Max " + UI.FormatAsLink("Power", "POWER") + ": {0} base, scaled by material";
        public static LocString RATED = "Rated: {0} with {1}";
        public static LocString RATED_TOOLTIP = "Material-Rated Wires: this material carries {0} before the circuit overloads (base {1})";

        // ------------------------------------------------------------ U1: wire status text

        /// <summary>
        /// Wire.OnPrefabInit builds the two status items once, into private statics, with
        /// lambdas that read the bucket rating (Wire.cs:155-200). After vanilla builds them, their
        /// resolve callbacks are wrapped: vanilla's own lambda runs on the text first, then its
        /// "{MaxLoad}" (already substituted with the bucket value) is put back from this wire's
        /// own limit. The disabled path is vanilla's callback untouched.
        /// </summary>
        [HarmonyPatch(typeof(Wire), "OnPrefabInit")]
        private static class Wire_OnPrefabInit
        {
            private static readonly AccessTools.FieldRef<StatusItem> circuitRef =
                AccessTools.StaticFieldRefAccess<StatusItem>(AccessTools.Field(typeof(Wire), "WireCircuitStatus"));

            private static readonly AccessTools.FieldRef<StatusItem> maxRef =
                AccessTools.StaticFieldRefAccess<StatusItem>(AccessTools.Field(typeof(Wire), "WireMaxWattageStatus"));

            private static readonly HashSet<StatusItem> wrapped = new HashSet<StatusItem>();

            private static void Postfix()
            {
                Wrap(circuitRef());
                Wrap(maxRef());
            }

            private static void Wrap(StatusItem item)
            {
                if (item == null || !wrapped.Add(item))
                {
                    return;
                }
                Func<string, object, string> vanilla = item.resolveStringCallback;
                item.resolveStringCallback = (str, data) =>
                {
                    if (!WattageCapacityLimits.Active || !(data is Wire wire))
                    {
                        return vanilla(str, data);
                    }
                    return vanilla(str.Replace("{MaxLoad}", "\u0001"), data).Replace("\u0001", FormatLimit(WattageCapacityLimits.LimitOf(wire)));
                };
            }
        }

        public static string FormatLimit(float watts)
        {
            GameUtil.WattageFormatterUnit unit = watts >= 20000f ? GameUtil.WattageFormatterUnit.Kilowatts : GameUtil.WattageFormatterUnit.Watts;
            return GameUtil.GetFormattedWattage(watts, unit);
        }

        // ---------------------------------------------------------- U2: build-menu descriptor

        /// <summary>
        /// BaseWireConfig.DoPostConfigureComplete (BaseWireConfig.cs:53-65) adds "Max Power: 1 kW"
        /// as the def's last descriptor; it now says the value is the base. Bridges and joint
        /// plates have no such descriptor in vanilla, so none is added: U3 shows their rated value.
        /// </summary>
        [HarmonyPatch(typeof(BaseWireConfig), "DoPostConfigureComplete")]
        private static class BaseWireConfig_DoPostConfigureComplete
        {
            private static void Postfix(Wire.WattageRating rating, GameObject go)
            {
                if (!WattageCapacityLimits.Active)
                {
                    return;
                }
                List<Descriptor> list = go.GetComponent<Building>().Def.EffectDescription;
                if (list == null || list.Count == 0)
                {
                    return;
                }
                Descriptor d = list[list.Count - 1];
                d.SetupDescriptor(string.Format(MAX_WATTAGE_BASE, GameUtil.GetFormattedWattage(Wire.GetMaxWattageAsFloat(rating))), d.tooltipText);
                list[list.Count - 1] = d;
            }
        }

        // ---------------------------------------------------------- U3: material picker line

        // The def whose material is being shown, set only for the length of MaterialSelector.SetEffects.
        [ThreadStatic] private static GameObject effectsPrefab;

        /// <summary>
        /// MaterialSelector.SetEffects(Tag) (MaterialSelector.cs:480-498) asks
        /// GameUtil.GetMaterialDescriptors(Tag) for the element's lines. The selector's active
        /// recipe names the building; while SetEffects runs, a wire, bridge or joint plate adds one
        /// "Rated: X with Copper" line to that list. Adding to the list, not rebuilding the pane,
        /// leaves every other mod's SetEffects and GetMaterialDescriptors patches as they are.
        /// </summary>
        [HarmonyPatch(typeof(MaterialSelector), "SetEffects")]
        private static class MaterialSelector_SetEffects
        {
            private static readonly AccessTools.FieldRef<MaterialSelector, Recipe> recipeRef =
                AccessTools.FieldRefAccess<MaterialSelector, Recipe>("activeRecipe");

            private static void Prefix(MaterialSelector __instance)
            {
                effectsPrefab = null;
                if (!WattageCapacityLimits.Active)
                {
                    return;
                }
                Recipe recipe = recipeRef(__instance);
                BuildingDef def = recipe?.GetBuildingDef();
                if (def != null && def.BuildingComplete != null
                    && (def.BuildingComplete.GetComponent<Wire>() != null || def.BuildingComplete.GetComponent<WireUtilityNetworkLink>() != null))
                {
                    effectsPrefab = def.BuildingComplete;
                }
            }

            private static void Finalizer()
            {
                effectsPrefab = null;
            }
        }

        [HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetMaterialDescriptors), typeof(Tag))]
        private static class GameUtil_GetMaterialDescriptors
        {
            private static void Postfix(Tag tag, List<Descriptor> __result)
            {
                GameObject prefab = effectsPrefab;
                if (prefab == null || __result == null)
                {
                    return;
                }
                Element element = ElementLoader.GetElement(tag);
                if (element == null)
                {
                    return;
                }
                Wire.WattageRating rating;
                Wire wire = prefab.GetComponent<Wire>();
                if (wire != null)
                {
                    rating = wire.MaxWattageRating;
                }
                else
                {
                    rating = prefab.GetComponent<WireUtilityNetworkLink>().GetMaxWattageRating();
                }
                string id = prefab.GetComponent<KPrefabID>()?.PrefabTag.Name;
                float rated = WattageCapacityLimits.Rate(rating, id, element.id);
                float baseWatts = Wire.GetMaxWattageAsFloat(rating);
                Descriptor d = default(Descriptor);
                d.SetupDescriptor(string.Format(RATED, FormatLimit(rated), element.name),
                    string.Format(RATED_TOOLTIP, FormatLimit(rated), FormatLimit(baseWatts)));
                __result.Add(d);
            }
        }

        // ------------------------------------------------------------- U4: wattage sensor range

        /// <summary>
        /// LogicWattageSensor's range is a readonly field set to 1.5 x 50 kW (LogicWattageSensor.cs:19)
        /// and read by RangeMax, the slider range (:82) and the input field (:169). The constructor
        /// postfix raises it to 1.5 x the highest limit any metal can give a 50 kW-base wire, so a
        /// copper Conductive Heavi-Watt circuit can still be thresholded. Never lowered below vanilla.
        /// </summary>
        [HarmonyPatch(typeof(LogicWattageSensor), MethodType.Constructor)]
        private static class LogicWattageSensor_Ctor
        {
            private static readonly AccessTools.FieldRef<LogicWattageSensor, float> maxRef =
                AccessTools.FieldRefAccess<LogicWattageSensor, float>("maxWattage");

            private static void Postfix(LogicWattageSensor __instance)
            {
                if (!WattageCapacityLimits.Active || ElementLoader.elements == null)
                {
                    return;
                }
                float top = 1.5f * HighestLimit();
                if (top > maxRef(__instance))
                {
                    maxRef(__instance) = top;
                }
            }
        }

        /// <summary>The highest limit a 50 kW-base wire reaches with any metal and any type multiplier.</summary>
        public static float HighestLimit()
        {
            float factor = 1f;
            foreach (Element e in ElementLoader.elements)
            {
                if (e != null && e.IsSolid && (e.HasTag(GameTags.Metal) || e.HasTag(GameTags.RefinedMetal)))
                {
                    factor = Mathf.Max(factor, WireCapacitySettings.GetMultiplier(e.id));
                }
            }
            float typeMul = 1f;
   
            return WattageCapacityLimits.Round(Wire.GetMaxWattageAsFloat(Wire.WattageRating.Max50000) * factor * typeMul);
        }
    }
}
