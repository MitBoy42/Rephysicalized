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

                    // If the prefab contains multiple OreDissolves components (e.g. ToxicMud has 2 patches),
                    // create a separate Codex panel for each component.
                    var allDissolves = prefab.GetComponents<OreDissolves>();
                    if (allDissolves == null || allDissolves.Length == 0)
                        continue;

                    foreach (var dissolvesInstance in allDissolves)
                    {
                        if (dissolvesInstance == null)
                            continue;

                        string key = BuildPanelKeyFromComponent(dissolvesInstance, element);
                        if (!unique.Add(key))
                            continue;

                        var panel = BuildPanelFromComponent(element, dissolvesInstance);
                        if (panel != null)
                            widgets.Add(panel);
                    }
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
            // include emitted fluid/ore + absorbed fluid to distinguish patches on the same prefab
            return $"{element.id}::{dissolves.emittedfluid}::{dissolves.emittedore}::{dissolves.absorbedFluid}";
        }

        private static ICodexWidget BuildPanelFromComponent(Element element, OreDissolves dissolves)
        {
            try
            {
                // Converter (absorbed fluid / liquid input in sim) vs absorbed ore (solid input in sim).
                // UI requirement:
                // - converter (middle / fabricator icon) should be the absorbed fluid (liquid input in sim)
                // - input (left) should be the absorbed ore (solid input in sim)
                var converterElement = ElementLoader.FindElementByHash(dissolves.absorbedFluid);
                if (converterElement == null)
                    return null;

                // The solid being dissolved is the element represented by this prefab (primary element).
                var oreInputElement = element;


                // Outputs: dissolving can emit both liquid and ore at the same time.
                Element emittedFluidElement = null;
                if (dissolves.emittedfluid != SimHashes.Vacuum)
                    emittedFluidElement = ElementLoader.GetElement(dissolves.emittedfluid.CreateTag());

                Element emittedOreElement = null;
                if (dissolves.emittedore != SimHashes.Vacuum)
                    emittedOreElement = ElementLoader.GetElement(dissolves.emittedore.CreateTag());

                if (emittedFluidElement == null && emittedOreElement == null)
                    return null;

                // Use the solid element prefab
                GameObject prefab = null;
                try
                {
                    prefab = Assets.GetPrefab(element.tag);
                }
                catch { }

                if (prefab == null) return null;

                // Input list (left side): absorbed ore (solid)
                var inputs = new List<ElementUsage>();
                inputs.Add(new ElementUsage(oreInputElement.tag, 1f, false, (t, a, c) => oreInputElement.name));

                // Converter (middle icon) should be the absorbed liquid.
                // CodexConversionPanel expects a prefab gameobject for the converter icon, not a Tag.
                // Using the absorbed fluid's prefab provides the correct middle icon.
                GameObject converterPrefab = null;
                try
                {
                    converterPrefab = Assets.GetPrefab(converterElement.tag);
                }
                catch { }

                if (converterPrefab == null)
                    converterPrefab = prefab;

                // Output list (right side): emitted substances (both can exist)
                var outputs = new List<ElementUsage>();

                if (emittedFluidElement != null)
                    outputs.Add(new ElementUsage(emittedFluidElement.tag, 1f, false, (t, a, c) => emittedFluidElement.name));

                if (emittedOreElement != null)
                    outputs.Add(new ElementUsage(emittedOreElement.tag, 1f, false, (t, a, c) => emittedOreElement.name));

                if (inputs.Count == 0 || outputs.Count == 0) return null;

                // Title: show whether this patch dissolves into fluid or reacts into ore.
                string title = element.name + " (" + (dissolves.emittedfluid != SimHashes.Vacuum ? "Dissolves" : "Reacts") + ")";
                var panel = new CodexConversionPanel(title, inputs.ToArray(), outputs.ToArray(), converterPrefab, null);


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

