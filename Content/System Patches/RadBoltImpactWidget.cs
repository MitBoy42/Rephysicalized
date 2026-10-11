using HarmonyLib;
using STRINGS;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{




        public static class RadBoltWidget
        {
            public static ContentContainer BuildRadboltPanelsContainerForElement(Tag elementTag)
            {
                try
                {
                    List<RadBoltImpactList.HEPImpactConfig> configs = RadBoltImpactList.Configs;
                    if (configs == null || configs.Count == 0)
                        return (ContentContainer)null;

                    GameObject prefab = (GameObject)null;
                
                        prefab = Assets.GetPrefab((Tag)"HighEnergyParticleSpawner");
                

                    string radbolt = (string)STRINGS.CODEX.PANELS.RADBOLT;
                    List<ICodexWidget> content = new List<ICodexWidget>();
                    HashSet<string> stringSet = new HashSet<string>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);

                    foreach (RadBoltImpactList.HEPImpactConfig cfg in configs)
                    {
                        if (cfg == null)
                            continue;

                        bool matches = false;

                        // Emitted element match
                        if (cfg.emittedElements != null && cfg.emittedElements.Count > 0)
                        {
                            foreach (KeyValuePair<string, float> emittedElementId in cfg.emittedElements)
                            {
                                if (emittedElementId.Key == null)
                                    continue;
                                if (ElementLoader.FindElementByTag((Tag)emittedElementId.Key)?.tag == elementTag)


                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }

                        // Affected element match (consumed)
                        if (!matches && cfg.affectedElements != null)
                        {
                            foreach (string affectedElementId in cfg.affectedElements)
                            {
                                if (affectedElementId == null)
                                    continue;
                                if (ElementLoader.FindElementByTag((Tag)affectedElementId)?.tag == elementTag)
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }

                        if (!matches)
                            continue;

                        CodexConversionPanel codexConversionPanel = RadBoltWidget.BuildPanelFromConfig(radbolt, prefab, cfg);
                        if (codexConversionPanel == null)
                            continue;

                        string str = RadBoltWidget.BuildPanelKey(cfg);
                        if (stringSet.Add(str))
                            content.Add((ICodexWidget)codexConversionPanel);
                    }

                    return content.Count == 0 ? (ContentContainer)null : new ContentContainer(content, ContentContainer.ContentLayout.Vertical);
                }
                catch (Exception ex)
                {
                    return (ContentContainer)null;
                }
            }

            public static ContentContainer BuildAllRadboltPanelsContainer()

            {
                try
                {
                    List<RadBoltImpactList.HEPImpactConfig> configs = RadBoltImpactList.Configs;
                    if (configs == null || configs.Count == 0)
                        return (ContentContainer)null;
                    GameObject prefab = (GameObject)null;
                    try
                    {
                        prefab = Assets.GetPrefab((Tag)"HighEnergyParticleSpawner");
                    }
                    catch
                    {
                    }
                    string radbolt = "Fission";
                    List<ICodexWidget> content = new List<ICodexWidget>();
                    HashSet<string> stringSet = new HashSet<string>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
                    foreach (RadBoltImpactList.HEPImpactConfig cfg in configs)
                    {
                        if (cfg != null)
                        {
                            CodexConversionPanel codexConversionPanel = RadBoltWidget.BuildPanelFromConfig(radbolt, prefab, cfg);
                            if (codexConversionPanel != null)
                            {
                                string str = RadBoltWidget.BuildPanelKey(cfg);
                                if (stringSet.Add(str))
                                    content.Add((ICodexWidget)codexConversionPanel);
                            }
                        }
                    }
                    return content.Count == 0 ? (ContentContainer)null : new ContentContainer(content, ContentContainer.ContentLayout.Vertical);
                }
                catch (Exception ex)
                {
         
                    return (ContentContainer)null;
                }
            }

            private static string BuildPanelKey(RadBoltImpactList.HEPImpactConfig cfg)
            {
                if (cfg == null)
                    return "null";
                string str = "none";
                if (cfg.emittedElements != null && cfg.emittedElements.Count > 0)
                {
                    using (Dictionary<string, float>.Enumerator enumerator = cfg.emittedElements.GetEnumerator())
                    {
                        if (enumerator.MoveNext())
                            str = enumerator.Current.Key;
                    }
                }
                return string.Format("{0}::{1:F3}::{2:F3}", (object)(cfg.id ?? str), (object)cfg.emissionMultiplier, (object)cfg.removalMultiplier);
            }

            private static CodexConversionPanel BuildPanelFromConfig(
              string title,
              GameObject prefab,
              RadBoltImpactList.HEPImpactConfig cfg)
            {
                try
                {
                    if (cfg == null)
                        return (CodexConversionPanel)null;
                    List<ElementUsage> elementUsageList1 = new List<ElementUsage>();
                    if (cfg.affectedElements != null)
                    {
                        foreach (string affectedElementId in cfg.affectedElements)
                        {
                            Element inEl = ElementLoader.FindElementByTag((Tag)affectedElementId);
                            if (inEl != null)
                            {
                                float amount = 1f / 1000f * cfg.removalMultiplier;
                                elementUsageList1.Add(new ElementUsage(inEl.tag, amount, false, (Func<Tag, float, bool, string>)((t, a, c) => inEl.name)));
                            }
                        }
                    }
                    List<ElementUsage> elementUsageList2 = new List<ElementUsage>();
                    if (cfg.emittedElements != null && cfg.emittedElements.Count > 0)
                    {
                        float num1 = 0.0f;
                        foreach (KeyValuePair<string, float> emittedElementId in cfg.emittedElements)
                            num1 += emittedElementId.Value;
                        if ((double)num1 <= 0.0)
                            num1 = 1f;
                        foreach (KeyValuePair<string, float> emittedElementId in cfg.emittedElements)
                        {
                            Element elementByTag = ElementLoader.FindElementByTag((Tag)emittedElementId.Key);
                            if (elementByTag != null)
                            {
                                float num2 = emittedElementId.Value / num1;
                                float amount = 1f / 1000f * cfg.emissionMultiplier * num2;
                                elementUsageList2.Add(new ElementUsage(elementByTag.tag, amount, false));
                            }
                        }
                    }
                    return elementUsageList1.Count == 0 || elementUsageList2.Count == 0 ? (CodexConversionPanel)null : new CodexConversionPanel(title, elementUsageList1.ToArray(), elementUsageList2.ToArray(), prefab, (CodexConversionPanel.IconSettings)null);
                }
                catch (Exception ex)
                {
                    return (CodexConversionPanel)null;
                }
            }
        }

        [HarmonyPatch(typeof(CodexEntryGenerator_Elements), nameof(CodexEntryGenerator_Elements.GenerateMadeAndUsedContainers))]
        internal class CodexEntryGenerator_Elements_GenerateMadeAndUsedContainers_RadBoltIntegration_Patch
        {
            private static readonly string _header = "Radbolt (Fission)";

            public static void Postfix(Tag tag, List<ContentContainer> containers)
            {
                if (containers == null)
                    return;

                ContentContainer radboltPanels = RadBoltWidget.BuildRadboltPanelsContainerForElement(tag);

                if (radboltPanels == null)
                    return;

                ContentContainer contentsRadboltFission = radboltPanels;
                containers.Add(new ContentContainer(new List<ICodexWidget>()
            {
                new CodexSpacer(),
                new CodexCollapsibleHeader("RadBolt Fission", contentsRadboltFission)
            }, ContentContainer.ContentLayout.Vertical));
                containers.Add(contentsRadboltFission);

            }

        }


    }
