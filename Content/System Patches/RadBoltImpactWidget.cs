using System;
using System.Collections.Generic;
using UnityEngine;
using STRINGS;

namespace Rephysicalized.Content.ModDb
{
    public static class RadBoltWidget
    {
        // Build an aggregated ContentContainer containing CodexConversionPanel widgets for all HEPImpactConfig entries.
        // This container can be appended by the codex registration code into a dedicated page (Radiation).
        public static ContentContainer BuildAllRadboltPanelsContainer()
        {
            try
            {
                var configs = RadBoltImpactList.Configs;
                if (configs == null || configs.Count == 0)
                    return null;

                GameObject spawner = null;
                try { spawner = Assets.GetPrefab((Tag)"HighEnergyParticleSpawner"); } catch { }
                string title = STRINGS.CODEX.PANELS.RADBOLT;

                var widgets = new List<ICodexWidget>();
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cfg in configs)
                {
                    if (cfg == null) continue;

                    var panel = BuildPanelFromConfig(title, spawner, cfg);
                    if (panel == null) continue;

                    string key = BuildPanelKey(cfg);
                    if (!unique.Add(key))
                        continue;

                    widgets.Add(panel);
                }

                if (widgets.Count == 0)
                    return null;

                return new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized.RadBolt] BuildAllRadboltPanelsContainer failed: {e}");
                return null;
            }
        }

        private static string BuildPanelKey(RadBoltImpactList.HEPImpactConfig cfg)
        {
            if (cfg == null) return "null";
            // For uniqueness, summarise by first emitted element key (if any)
            string emittedKey = "none";
            if (cfg.emittedElements != null && cfg.emittedElements.Count > 0)
            {
                foreach (var kv in cfg.emittedElements) { emittedKey = kv.Key.ToString(); break; }
            }
            return $"{cfg.id ?? emittedKey}::{cfg.emissionMultiplier:F3}::{cfg.removalMultiplier:F3}";
        }

        private static CodexConversionPanel BuildPanelFromConfig(string title, GameObject prefab, RadBoltImpactList.HEPImpactConfig cfg)
        {
            try
            {
                if (cfg == null) return null;

                var inputs = new List<ElementUsage>();
                if (cfg.affectedElements != null)
                {
                    foreach (var h in cfg.affectedElements)
                    {
                        var inEl = ElementLoader.FindElementByHash(h);
                        if (inEl == null) continue;
                        float inAmt = 0.001f * cfg.removalMultiplier;
                        inputs.Add(new ElementUsage(inEl.tag, inAmt, false, (t, a, c) => inEl.name));
                    }
                }

                var outputs = new List<ElementUsage>();
                if (cfg.emittedElements != null && cfg.emittedElements.Count > 0)
                {
                    // Normalize fractions like runtime emission
                    float totalFraction = 0f;
                    foreach (var kv in cfg.emittedElements)
                        totalFraction += kv.Value;
                    if (totalFraction <= 0f) totalFraction = 1f;

                    foreach (var kv in cfg.emittedElements)
                    {
                        var outEl = ElementLoader.FindElementByHash(kv.Key);
                        if (outEl == null) continue;
                        float frac = kv.Value / totalFraction;
                        float outAmt = 0.001f * cfg.emissionMultiplier * frac;
                        outputs.Add(new ElementUsage(outEl.tag, outAmt, false));
                    }
                }

                if (inputs.Count == 0 || outputs.Count == 0) return null;

                return new CodexConversionPanel(title, inputs.ToArray(), outputs.ToArray(), prefab, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized.RadBolt] BuildPanelFromConfig failed: {e}");
                return null;
            }
        }
    }
}
