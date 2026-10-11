using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    // Adds a single "Harvest Drops (Rephysicalized)" panel to plant Codex entries.
    // The yields are read from PlantMassTrackerRegistry for the plant's prefab ID.
    [HarmonyPatch(typeof(CodexEntryGenerator), "GeneratePlantDescriptionContainers")]
    internal static class PlantHarvestCodexPanelPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject plant, List<ContentContainer> containers)
        {
            if (plant == null || containers == null) return;

            var kpid = plant.GetComponent<KPrefabID>();
            if (kpid == null) return;

            // Direct lookup: PlantMassTrackerRegistry lives in namespace Rephysicalized,
            // so it is reachable from this nested namespace without a using directive.
            if (!PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var config)) return;

            List<MaterialYield> yields = config?.yields;
            if (yields == null || yields.Count == 0) return;

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

            // Vanilla calls GenerateMadeAndUsedContainers *after* this method (CodexEntryGenerator
            // line 491), so appending here lands the panel between the plant description and the
            // "Made & Used" section. That is the intended slot, so no index search is needed.
            containers.Add(panel);
        }

        private static List<ElementUsage> BuildOutputs(List<MaterialYield> yields)
        {
            var outs = new List<ElementUsage>(yields.Count);
            foreach (var y in yields)
                CodexDropPanel.AddOutput(outs, y.id, y.multiplier);

            return outs;
        }
    }
}