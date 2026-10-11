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


            var outs = BuildOutputs(drops);

            if (outs.Count == 0) return;

            var widgets = new List<ICodexWidget>();

            widgets.Add(new CodexSpacer());

            // Conversion panel with title shown in the panel header
            widgets.Add(new CodexConversionPanel(
                title: STRINGS.CODEX.PANELS.EXTRADROPS,
                ins: Array.Empty<ElementUsage>(),
                outs: outs.ToArray(),
                converter: creature // center widget uses creature's icon/name
            ));

            var panelContainer = new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);

            int insertIndex = FindAfterButcherableInsertIndex(containers);

            containers.Insert(insertIndex < 0 ? containers.Count : insertIndex, panelContainer);
        }

  
        private static List<CreatureMassTracker.ExtraDropSpec> GetExtraDropsForCreature(GameObject creature, out bool hasTracker)
        {
            hasTracker = false;

            var kpid = creature.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                var reg = CreatureMassTracker.GetRegisteredDrops(kpid.PrefabTag);
                if (reg != null && reg.Count > 0)
                    return reg;
            }

            var tracker = creature.GetComponent<CreatureMassTracker>();
            if (tracker == null)
                return null;

            hasTracker = true;

            if (tracker.ExtraDrops != null && tracker.ExtraDrops.Count > 0)
                return tracker.ExtraDrops;

            return null;
        }

        private static List<ElementUsage> BuildOutputs(List<CreatureMassTracker.ExtraDropSpec> drops)
        {
            var outs = new List<ElementUsage>(drops.Count);
            foreach (var spec in drops)
            {
                if (spec == null) continue;
                CodexDropPanel.AddOutput(outs, spec.id, spec.fraction);
            }

            return outs;
        }

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

            List<ICodexWidget> widgets = container.content;
            if (widgets == null) return false;

            foreach (var w in widgets)
            {
                if (w is CodexText text && text.style == CodexTextStyle.Subtitle
                    && string.Equals(text.text, subtitleText, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}


