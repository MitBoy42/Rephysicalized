using System;
using System.Collections.Generic;
using UnityEngine;
using STRINGS;
using Rephysicalized.Content.System_Patches;

namespace Rephysicalized.Content.ModDb
{
    public static class OreDissolvesWidget
    {
        // Build an aggregated ContentContainer containing CodexConversionPanel widgets for all IOreDissolvesConfig entries.
        // This container can be appended by the codex registration code into a dedicated page (Dissolving).
        public static ContentContainer BuildAllDissolvingPanelsContainer()
        {
            try
            {
                var widgets = new List<ICodexWidget>();
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Scan all elements and check if they have OreDissolves component in their prefab
                foreach (Element element in ElementLoader.elements)
                {
                    if (element == null || !element.IsSolid) continue;

                    GameObject prefab = null;
                    try
                    {
                        prefab = Assets.TryGetPrefab(element.tag);
                    }
                    catch { continue; }

                    if (prefab == null) continue;

                    var oreDissolves = prefab.GetComponent<OreDissolves>();
                    if (oreDissolves == null) continue;

                    string key = BuildPanelKeyFromComponent(oreDissolves, element);
                    if (!unique.Add(key))
                        continue;

                    var panel = BuildPanelFromComponent(element, oreDissolves);
                    if (panel != null)
                        widgets.Add(panel);
                }

                if (widgets.Count == 0)
                    return null;

                return new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized.OreDissolves] BuildAllDissolvingPanelsContainer failed: {e}");
                return null;
            }
        }


        private static string BuildPanelKeyFromComponent(OreDissolves dissolves, Element element)
        {
            return $"{element.id}::{dissolves.emittedfluid}::{dissolves.absorbedFluid}";
        }

        private static ICodexWidget BuildPanelFromComponent(Element element, OreDissolves dissolves)
        {
            try
            {
                // Get elements
                var absorbedElement = ElementLoader.FindElementByHash(dissolves.absorbedFluid);
                // Show whichever configured output makes sense for the UI.
                // Note: the runtime can emit both; the codex shows a single aggregated output.
                SimHashes emittedHash = dissolves.emittedfluid != SimHashes.Vacuum ? dissolves.emittedfluid : dissolves.emittedore;
                var emittedElement = ElementLoader.GetElement(emittedHash.CreateTag());

                if (absorbedElement == null || emittedElement == null)
                    return null;

                // Use the solid element prefab
                GameObject prefab = null;
                try
                {
                    prefab = Assets.GetPrefab(element.tag);
                }
                catch { }

                if (prefab == null) return null;

                // Create inputs list (left side): Absorbed Fluid
                var inputs = new List<ElementUsage>();
                inputs.Add(new ElementUsage(absorbedElement.tag, 1f, false, (t, a, c) => absorbedElement.name));

                // Create outputs list (right side): Emitted substance
                var outputs = new List<ElementUsage>();

                outputs.Add(new ElementUsage(emittedElement.tag, 1f, false, (t, a, c) => emittedElement.name));

                if (inputs.Count == 0 || outputs.Count == 0) return null;

                string title = element.name + " (" + (dissolves.emittedfluid != SimHashes.Vacuum ? "Dissolves" : "Reacts") + ")";
                var panel = new CodexConversionPanel(title, inputs.ToArray(), outputs.ToArray(), prefab, null);

                return panel;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized.OreDissolves] BuildPanelFromComponent failed: {e}");
                return null;
            }
        }
    }
}

