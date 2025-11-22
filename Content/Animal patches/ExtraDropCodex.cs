using System;
using System.Collections.Generic;
using HarmonyLib;
using STRINGS;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Insert an "Extra Drops" conversion panel right after the Butcherable (CRITTERDROPS) section
    [HarmonyPatch(typeof(CodexEntryGenerator_Creatures), "GenerateCreatureDescriptionContainers")]
    public static class ExtraDropsCodexConversionPanelPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject creature, List<ContentContainer> containers)
        {
            if (creature == null || containers == null) return;

            bool hasTracker;
            var drops = GetExtraDropsForCreature(creature, out hasTracker);

            // Fallback: tracker present but no specific extra drops => Rot Pile @ 100%
            if ((drops == null || drops.Count == 0) && hasTracker)
            {
                drops = new List<CreatureMassTracker.ExtraDropSpec>
                {
                    new CreatureMassTracker.ExtraDropSpec
                    {
                        id = "RotPile",
                        fraction = 1f
                    }
                };
            }

            if (drops == null || drops.Count == 0) return;

            // Build a conversion panel: center shows the creature as the "converter"
            // Inputs: none, Outputs: each extra drop with percentage formatter
            var outs = BuildOutputs(drops);

            // If we failed to resolve all outputs, do nothing
            if (outs.Count == 0) return;

            var widgets = new List<ICodexWidget>();

            // Optional spacer for visual separation from previous section
            widgets.Add(new CodexSpacer());

            // Conversion panel with title shown in the panel header
            widgets.Add(new CodexConversionPanel(
                title: STRINGS.CODEX.PANELS.EXTRADROPS,
                ins: Array.Empty<ElementUsage>(),
                outs: outs.ToArray(),
                converter: creature // center widget uses creature's icon/name
            ));

            var panelContainer = new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);

            // Insert after Butcherable (CRITTERDROPS) if found, otherwise append
            int insertIndex = FindAfterButcherableInsertIndex(containers);
            if (insertIndex < 0 || insertIndex > containers.Count)
                insertIndex = containers.Count;

            containers.Insert(insertIndex, panelContainer);
        }

        // Resolve registered species-level drops or per-prefab tracker
        private static List<CreatureMassTracker.ExtraDropSpec> GetExtraDropsForCreature(GameObject creature, out bool hasTracker)
        {
            hasTracker = false;
            try
            {
                var kpid = creature.GetComponent<KPrefabID>();
                if (kpid != null)
                {
                    var reg = CreatureMassTracker.GetRegisteredDrops(kpid.PrefabTag);
                    if (reg != null && reg.Count > 0)
                        return reg;
                }

                var tracker = creature.GetComponent<CreatureMassTracker>();
                if (tracker != null)
                {
                    hasTracker = true;
                    if (tracker.ExtraDrops != null && tracker.ExtraDrops.Count > 0)
                        return tracker.ExtraDrops;
                }
            }
            catch { /* ignore and fall through */ }

            return null;
        }

        // Build ElementUsage outputs with custom percentage formatter
        private static List<ElementUsage> BuildOutputs(List<CreatureMassTracker.ExtraDropSpec> drops)
        {
            var outs = new List<ElementUsage>(drops.Count);
            foreach (var spec in drops)
            {
                if (spec == null) continue;
                if (spec.fraction <= 0f) continue;

                if (!TryResolveTag(spec.id, out var tag))
                    continue;

                // continuous=false so the panel won't append per-cycle unit text
                outs.Add(new ElementUsage(
                    tag: tag,
                    amount: Mathf.Clamp01(spec.fraction),
                    continuous: false,
                    customFormating: PercentFormatter
                ));
            }
            return outs;
        }

        // Format amount as percentage (e.g., 25%)
        private static string PercentFormatter(Tag tag, float amount, bool continuous)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(amount * 100f), 0, 10000);
            return $"{pct}%";
        }

        // Resolve a drop id that can be an Element or a Prefab into a Tag suitable for CodexConversionPanel
        private static bool TryResolveTag(string id, out Tag tag)
        {
            tag = Tag.Invalid;
            if (string.IsNullOrEmpty(id)) return false;

            // Try prefab first
            try
            {
                var prefab = Assets.GetPrefab(id);
                if (prefab != null)
                {
                    var kpid = prefab.GetComponent<KPrefabID>();
                    if (kpid != null)
                    {
                        tag = kpid.PrefabTag;
                        return true;
                    }
                }
            }
            catch { }

            // Try element by Tag name
            try
            {
                var elem = ElementLoader.GetElement(new Tag(id));
                if (elem != null)
                {
                    tag = elem.tag;
                    return true;
                }
            }
            catch { }

            // Try element by SimHashes
            try
            {
                if (Enum.TryParse<SimHashes>(id, true, out var hash))
                {
                    var elem = ElementLoader.FindElementByHash(hash);
                    if (elem != null)
                    {
                        tag = elem.tag;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        // Find insertion: after butcherable header + its content
        private static int FindAfterButcherableInsertIndex(List<ContentContainer> containers)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                if (ContainerHasSubtitle(containers[i], CODEX.HEADERS.CRITTERDROPS))
                {
                    // Insert after header and its immediate content container if present
                    return Math.Min(i + 2, containers.Count);
                }
            }
            return -1;
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
                    var tw = HarmonyLib.Traverse.Create(w);
                    string text = null;
                    object styleObj = null;

                    try { text = tw.Field("text").GetValue<string>(); } catch { }
                    if (string.IsNullOrEmpty(text))
                    {
                        try { text = tw.Property("Text")?.GetValue<string>(); } catch { }
                    }

                    try { styleObj = tw.Field("style").GetValue<object>(); } catch { }
                    if (styleObj == null)
                    {
                        try { styleObj = tw.Property("Style")?.GetValue<object>(); } catch { }
                    }

                    if (!string.IsNullOrEmpty(text) && string.Equals(text, subtitleText, StringComparison.Ordinal))
                    {
                        // Match Subtitle style if available; fallback to text match
                        if (styleObj == null || string.Equals(styleObj.ToString(), "Subtitle", StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            return false;
        }

        private static List<ICodexWidget> GetWidgets(ContentContainer container)
        {
            if (container == null) return null;

            var tc = HarmonyLib.Traverse.Create(container);
            List<ICodexWidget> list = null;
            try { list = tc.Field("content").GetValue<List<ICodexWidget>>(); } catch { }
            if (list == null)
            {
                try { list = tc.Property("Content")?.GetValue<List<ICodexWidget>>(); } catch { }
            }
            return list;
        }
    }
}