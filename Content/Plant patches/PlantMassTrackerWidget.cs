using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    // Adds a single "Harvest Drops (Rephysicalized)" panel to plant Codex entries.
    // The yields are read from PlantMassTrackerRegistry for the plant's prefab ID.
    [HarmonyPatch]
    internal static class PlantHarvestCodexPanelPatch
    {
        private static MethodBase _target;

        public static bool Prepare()
        {
            _target = FindTarget();

            return true;
        }

        public static MethodBase TargetMethod() => _target;

        private static MethodBase FindTarget()
        {
            // Primary path in U56 builds
            var t = AccessTools.TypeByName("CodexEntryGenerator");
            if (t != null)
            {
                var m = AccessTools.Method(t, "GeneratePlantDescriptionContainers", new Type[]
                {
                    typeof(GameObject),
                    typeof(List<ContentContainer>)
                });
                if (m != null) return m;
            }

            return null;
        }

        // Postfix signature must match TargetMethod
        public static void Postfix(GameObject plant, List<ContentContainer> containers)
        {
            if (plant == null || containers == null) return;

            var kpid = plant.GetComponent<KPrefabID>();
            if (kpid == null) return;

            string prefabId = kpid.PrefabID().Name; // e.g., "SwampLily"

            var yields = GetHarvestYieldsFromRegistry(prefabId);


            var outs = BuildOutputs(yields);
            if (outs.Count == 0) return;

            var widgets = new List<ICodexWidget>
            {
                new CodexSpacer(),
                new CodexConversionPanel(
                    title: STRINGS.CODEX.PANELS.EXTRADROPS,
                    ins: Array.Empty<ElementUsage>(),
                    outs: outs.ToArray(),
                    converter: plant
                )
            };
            var panel = new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);

            int insertIndex = FindAfterHarvestInsertIndex(containers);
            if (insertIndex < 0 || insertIndex > containers.Count)
                insertIndex = containers.Count;

            containers.Insert(insertIndex, panel);
        }

        // --- Registry integration ---

        private struct YieldSpec
        {
            public string id;        // prefab or element id
            public float multiplier; // 1.0 => 100%
        }

        private static List<YieldSpec> GetHarvestYieldsFromRegistry(string prefabId)
        {
            // Strongly-typed lookup: avoids reflection/traversal for registry data.
            if (string.IsNullOrWhiteSpace(prefabId))
                return new List<YieldSpec>();

            if (!Rephysicalized.PlantMassTrackerRegistry.TryGetConfig(prefabId, out var cfg) || cfg == null)
                return new List<YieldSpec>();

            if (cfg.yields == null || cfg.yields.Count == 0)
                return new List<YieldSpec>();

            var list = new List<YieldSpec>(cfg.yields.Count);
            for (int i = 0; i < cfg.yields.Count; i++)
            {
                var y = cfg.yields[i];
                if (y == null) continue;
                if (string.IsNullOrWhiteSpace(y.id)) continue;
                if (y.multiplier <= 0f) continue;

                list.Add(new YieldSpec { id = y.id, multiplier = y.multiplier });
            }

            return list;
        }


        // --- UI helpers ---

        private static List<ElementUsage> BuildOutputs(List<YieldSpec> yields)
        {
            var outs = new List<ElementUsage>(yields.Count);
            foreach (var y in yields)
            {
                if (string.IsNullOrEmpty(y.id) || y.multiplier <= 0f) continue;
                if (!TryResolveTag(y.id, out var tag)) continue;

                float pct = Mathf.Clamp01(y.multiplier);
                outs.Add(new ElementUsage(
                    tag: tag,
                    amount: pct,
                    continuous: false,
                    customFormating: PercentFormatter
                ));
            }
            return outs;
        }

        private static string PercentFormatter(Tag tag, float amount, bool continuous)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(amount * 100f), 0, 10000);
            return $"{pct}%";
        }

        private static bool TryResolveTag(string id, out Tag tag)
        {
            tag = Tag.Invalid;
            if (string.IsNullOrEmpty(id)) return false;

            // Try prefab
            try
            {
                var prefab = Assets.GetPrefab(id);
                if (prefab != null)
                {
                    var kpid = prefab.GetComponent<KPrefabID>();
                    if (kpid != null) { tag = kpid.PrefabTag; return true; }
                }
            }
            catch { }

            // Try element by Tag
            try
            {
                var elem = ElementLoader.GetElement(new Tag(id));
                if (elem != null) { tag = elem.tag; return true; }
            }
            catch { }

            // Try element by SimHashes enum name
            try
            {
                if (Enum.TryParse<SimHashes>(id, true, out var hash))
                {
                    var elem = ElementLoader.FindElementByHash(hash);
                    if (elem != null) { tag = elem.tag; return true; }
                }
            }
            catch { }

            return false;
        }

        private static int FindAfterHarvestInsertIndex(List<ContentContainer> containers)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                if (ContainerHasSubtitle(containers[i], "Harvest"))
                    return Math.Min(i + 2, containers.Count);
            }
            return containers.Count;
        }

        private static bool ContainerHasSubtitle(ContentContainer container, string subtitleText)
        {
            if (container == null || string.IsNullOrEmpty(subtitleText)) return false;

            var widgets = GetWidgets(container);
            if (widgets == null) return false;

            foreach (var w in widgets)
            {
                if (w == null) continue;
                if (w.GetType().Name == "CodexText")
                {
                    var tv = Traverse.Create(w);
                    string text = null;
                    object style = null;

                    try { text = tv.Field("text").GetValue<string>(); } catch { }
                    if (string.IsNullOrEmpty(text))
                        try { text = tv.Property("Text")?.GetValue<string>(); } catch { }

                    try { style = tv.Field("style").GetValue<object>(); } catch { }
                    if (style == null)
                        try { style = tv.Property("Style")?.GetValue<object>(); } catch { }

                    if (!string.IsNullOrEmpty(text) && text.Equals(subtitleText, StringComparison.Ordinal))
                    {
                        if (style == null || style.ToString().Equals("Subtitle", StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            return false;
        }

        private static List<ICodexWidget> GetWidgets(ContentContainer container)
        {
            if (container == null) return null;

            var tc = Traverse.Create(container);
            List<ICodexWidget> list = null;
            try { list = tc.Field("content").GetValue<List<ICodexWidget>>(); } catch { }
            if (list == null)
                try { list = tc.Property("Content")?.GetValue<List<ICodexWidget>>(); } catch { }
            return list;
        }
    }
}