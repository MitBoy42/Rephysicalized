using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace Rephysicalized
{
    // Basic MB kept for completeness; not directly used by the CodexConversionPanel path
    public sealed class EnvCookableWidget : MonoBehaviour
    {
        [Header("Prefabs/References")][SerializeField] private RectTransform rootContainer; [SerializeField] private LocText headerText;
        [SerializeField] private RectTransform middleRow;
        [SerializeField] private Image temperatureIcon;
        [SerializeField] private LocText temperatureText;
        [SerializeField] private Image pressureIcon;
        [SerializeField] private LocText pressureText;

        [SerializeField] private RectTransform bottomRow;
        [SerializeField] private Image inputItemIcon;
        [SerializeField] private LocText inputItemText;

        [Header("Style")]
        [SerializeField] private TextStyleSetting headerStyle;
        [SerializeField] private TextStyleSetting bodyStyle;

        public void ConfigureSprites(Sprite tempSprite, Sprite pressureSprite, Sprite inputSprite)
        {
            if (temperatureIcon != null) temperatureIcon.sprite = tempSprite;
            if (pressureIcon != null) pressureIcon.sprite = pressureSprite;
            if (inputItemIcon != null) inputItemIcon.sprite = inputSprite;
        }

        public void Configure(string title, string inputItemDisplay, bool showTemperature = true, bool showPressure = true)
        {
            EnsureLayout();

            if (headerText != null)
            {
                if (headerStyle != null) SetTextStyleSetting.ApplyStyle(headerText, headerStyle);
                headerText.AllowLinks = true;
                headerText.text = title ?? string.Empty;
            }

            if (middleRow != null)
            {
                middleRow.gameObject.SetActive(showTemperature || showPressure);

                if (temperatureIcon != null) temperatureIcon.gameObject.SetActive(showTemperature);
                if (pressureIcon != null) pressureIcon.gameObject.SetActive(showPressure);
                if (pressureText != null) pressureText.gameObject.SetActive(showPressure);
            }
        }

        private void EnsureLayout()
        {
            if (rootContainer == null)
                rootContainer = gameObject.GetComponent<RectTransform>();

            var vlg = gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 8f;
                vlg.padding = new RectOffset(0, 0, 0, 0);
            }

            SetupRowLayout(middleRow);
            SetupRowLayout(bottomRow);
        }

        private static void SetupRowLayout(RectTransform row)
        {
            if (row == null) return;
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childForceExpandHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.spacing = 6f;
                hlg.padding = new RectOffset(0, 0, 0, 0);
            }
        }
    }

    internal static class CodexEntriesRegistry
    {
        private static readonly Dictionary<string, CodexEntry> entries = new Dictionary<string, CodexEntry>(256, StringComparer.OrdinalIgnoreCase);

        public static void Add(CodexEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            entries[entry.id] = entry;

            int idx = entry.id.LastIndexOf("::", StringComparison.Ordinal);
            if (idx >= 0 && idx < entry.id.Length - 2)
            {
                var suffix = entry.id.Substring(idx + 2);
                if (!entries.ContainsKey(suffix))
                    entries[suffix] = entry;
            }
        }

        public static void AddRange(Dictionary<string, CodexEntry> map)
        {
            if (map == null) return;
            foreach (var kv in map)
                Add(kv.Value);
        }

        public static CodexEntry FindByIdOrSuffix(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (entries.TryGetValue(id, out var e))
                return e;

            foreach (var kv in entries)
            {
                if (kv.Key.EndsWith($"::{id}", StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            var canonical = Rephysicalized.EnvCookableCodexIntegration.TryCanonicalizeElementId(id);
            if (!string.IsNullOrEmpty(canonical) && entries.TryGetValue(canonical, out var e2))
                return e2;

            return null;
        }
    }

    [HarmonyPatch]
    internal static class EnvCookableCodexIntegration
    {
        // Anchor headers for placement – keep exact strings as provided by the game
        private const string HeaderApplications = "Applications";
        private const string HeaderProducedBy = "Produced By";

        // Buckets of panels by output id/alias (target pages)
        private static readonly Dictionary<string, List<(GameObject sourcePrefab, EnviromentCookablePatch patch)>> OutputBuckets
            = new Dictionary<string, List<(GameObject, EnviromentCookablePatch)>>(256, StringComparer.OrdinalIgnoreCase);

        // Per-entry duplicate guard for panels; key is unique per patch+source
        private static readonly ConditionalWeakTable<CodexEntry, HashSet<string>> EntryPanelKeys
            = new ConditionalWeakTable<CodexEntry, HashSet<string>>();

        // Central hook: every codex entry added
        [HarmonyPatch(typeof(CodexCache), "AddEntry")]
        [HarmonyPostfix]
        private static void CodexCache_AddEntry_Postfix(CodexEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                return;

            CodexEntriesRegistry.Add(entry);

            // Source: if this page is for a prefab that has patches, add source panels (one per patch)
            if (entry.contentContainers != null)
            {
                GameObject prefab = TryResolvePrefabByEntryId(entry.id);
                if (prefab != null)
                    TryAppendSourcePanels(entry, prefab);
            }

            // Target: if this page corresponds to an element entry, append target panels for all producers
            if (!entry.id.StartsWith("ELEMENTS", StringComparison.OrdinalIgnoreCase))
            {
                var el = ElementLoader.FindElementByName(entry.id);
                if (el == null)
                {
                    var canonical = TryCanonicalizeElementId(entry.id);
                    if (!string.IsNullOrEmpty(canonical))
                        el = ElementLoader.FindElementByName(canonical);
                }
                if (el != null)
                    TryAppendAllForElementAliasesDirect(entry, GetElementAliases(el));
            }

            // Also handle non-element outputs (e.g., foods, equipment) via plain id
            TryAppendAllForOutputId(entry.id);
        }

        // Generators – ensure source panels for generated entries too (covers cases AddEntry timing differs)
        [HarmonyPatch(typeof(CodexEntryGenerator), "GenerateSingleBuildingEntry")]
        [HarmonyPostfix]
        private static void BuildingEntry_Postfix(ref CodexEntry __result, BuildingDef def)
        {
            if (__result == null || def == null) return;
            TryAppendSourcePanels(__result, def.BuildingComplete);
        }

        [HarmonyPatch(typeof(CodexEntryGenerator), "GeneratePlantEntries")]
        [HarmonyPostfix]
        private static void PlantEntries_Postfix(Dictionary<string, CodexEntry> __result)
        {
            if (__result == null) return;
            CodexEntriesRegistry.AddRange(__result);

            foreach (var kv in __result)
            {
                var entry = kv.Value;
                if (entry == null) continue;

                GameObject plantPrefab = TryResolvePrefabFromEntry(entry);
                TryAppendSourcePanels(entry, plantPrefab);

                if (entry.subEntries != null)
                {
                    foreach (var se in entry.subEntries)
                    {
                        if (se == null || se.contentContainers == null) continue;

                        GameObject seedPrefab = TryResolvePrefabFromSubEntry(se);
                        if (seedPrefab == null && plantPrefab != null)
                            seedPrefab = TryResolveSeedFromPlant(plantPrefab);

                        TryAppendSourcePanelsToSubEntry(se, seedPrefab);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CodexEntryGenerator), "GenerateFoodEntries")]
        [HarmonyPostfix]
        private static void FoodEntries_Postfix(Dictionary<string, CodexEntry> __result)
        {
            if (__result == null) return;
            CodexEntriesRegistry.AddRange(__result);

            foreach (var kv in __result)
            {
                var entry = kv.Value;
                if (entry == null) continue;

                GameObject prefab = TryResolvePrefabFromEntry(entry);
                TryAppendSourcePanels(entry, prefab);
            }
        }

        [HarmonyPatch(typeof(CodexEntryGenerator), "GenerateEquipmentEntries")]
        [HarmonyPostfix]
        private static void EquipmentEntries_Postfix(Dictionary<string, CodexEntry> __result)
        {
            if (__result == null) return;
            CodexEntriesRegistry.AddRange(__result);

            foreach (var kv in __result)
            {
                var entry = kv.Value;
                if (entry == null) continue;

                GameObject prefab = TryResolvePrefabFromEntry(entry);
                TryAppendSourcePanels(entry, prefab);
            }
        }

        [HarmonyPatch(typeof(CodexEntryGenerator_Elements), "GenerateEntries")]
        [HarmonyPostfix]
        private static void ElementEntries_Postfix(Dictionary<string, CodexEntry> __result)
        {
            if (__result == null) return;
            CodexEntriesRegistry.AddRange(__result);
        }

        // Insert source panels for each patch on prefab into this entry
        private static void TryAppendSourcePanels(CodexEntry entry, GameObject prefab)
        {
            if (entry?.contentContainers == null || prefab == null) return;

            var patches = prefab.GetComponents<EnviromentCookablePatch>();
            if (patches == null || patches.Length == 0) return;

            var seen = EntryPanelKeys.GetOrCreateValue(entry);

            foreach (var cookable in patches)
            {
                var panel = BuildEnvironmentalCookingPanel(prefab, cookable);
                if (panel == null) continue;

                var key = BuildPanelKey(prefab, cookable);
                if (seen.Contains(key))
                    continue;

                int insertIdx = FindInsertIndexAfterHeader(entry.contentContainers, HeaderApplications);
                if (insertIdx < 0)
                    insertIdx = FindInsertIndexFallback(entry.contentContainers);

                var cc = new ContentContainer(new List<ICodexWidget> { panel }, ContentContainer.ContentLayout.Vertical);
                if (insertIdx >= 0 && insertIdx <= entry.contentContainers.Count)
                    entry.contentContainers.Insert(insertIdx, cc);
                else
                    entry.contentContainers.Add(cc);

                seen.Add(key);

                // Bucket for target pages and try immediate append
                BucketCookableOutputs(cookable, prefab);
                TryAppendAllForOutputId(cookable.ID);
            }
        }

        private static void TryAppendSourcePanelsToSubEntry(SubEntry se, GameObject prefab)
        {
            if (se?.contentContainers == null || prefab == null) return;

            var patches = prefab.GetComponents<EnviromentCookablePatch>();
            if (patches == null || patches.Length == 0) return;

            foreach (var cookable in patches)
            {
                var panel = BuildEnvironmentalCookingPanel(prefab, cookable);
                if (panel == null) continue;

                int insertIdx = FindInsertIndexAfterHeader(se.contentContainers, HeaderApplications);
                if (insertIdx < 0)
                    insertIdx = FindInsertIndexFallback(se.contentContainers);

                var cc = new ContentContainer(new List<ICodexWidget> { panel }, ContentContainer.ContentLayout.Vertical);
                if (insertIdx >= 0 && insertIdx <= se.contentContainers.Count)
                    se.contentContainers.Insert(insertIdx, cc);
                else
                    se.contentContainers.Add(cc);

                BucketCookableOutputs(cookable, prefab);
                TryAppendAllForOutputId(cookable.ID);
            }
        }

        // Bucket management

        private static void BucketCookableOutputs(EnviromentCookablePatch cookable, GameObject prefab)
        {
            if (cookable == null || string.IsNullOrEmpty(cookable.ID)) return;

            AddToBucket(cookable.ID, prefab, cookable); // raw id

            // If it maps to an element, also map under aliases
            var aliases = GetElementAliases(cookable.ID);
            foreach (var a in aliases)
                AddToBucket(a, prefab, cookable);
        }

        private static void AddToBucket(string key, GameObject prefab, EnviromentCookablePatch cookable)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!OutputBuckets.TryGetValue(key, out var list))
            {
                list = new List<(GameObject, EnviromentCookablePatch)>();
                OutputBuckets[key] = list;
            }
            list.Add((prefab, cookable));
        }

        // Target insertion

        private static void TryAppendAllForOutputId(string outputId)
        {
            if (string.IsNullOrEmpty(outputId)) return;

            if (TryAppendAllForOutputIdInternal(outputId))
                return;

            var aliases = GetElementAliases(outputId);
            foreach (var alias in aliases)
            {
                if (TryAppendAllForOutputIdInternal(alias))
                    return;
            }
        }

        private static bool TryAppendAllForOutputIdInternal(string outputId)
        {
            if (!OutputBuckets.TryGetValue(outputId, out var list) || list.Count == 0)
                return false;

            var outEntry = CodexEntriesRegistry.FindByIdOrSuffix(outputId);
            if (outEntry?.contentContainers == null)
                return false;

            var seen = EntryPanelKeys.GetOrCreateValue(outEntry);

            foreach (var (sourcePrefab, cookable) in list)
            {
                var key = BuildPanelKey(sourcePrefab, cookable);
                if (seen.Contains(key))
                    continue;

                var panel = BuildEnvironmentalCookingPanel(sourcePrefab, cookable);
                if (panel == null) continue;

                int insertIdx = FindInsertIndexAfterHeader(outEntry.contentContainers, HeaderProducedBy);
                if (insertIdx < 0)
                    insertIdx = FindInsertIndexFallback(outEntry.contentContainers);

                var cc = new ContentContainer(new List<ICodexWidget> { panel }, ContentContainer.ContentLayout.Vertical);
                if (insertIdx >= 0 && insertIdx <= outEntry.contentContainers.Count)
                    outEntry.contentContainers.Insert(insertIdx, cc);
                else
                    outEntry.contentContainers.Add(cc);

                seen.Add(key);
            }

            return true;
        }

        // Panel construction

        private static string BuildPanelKey(GameObject sourcePrefab, EnviromentCookablePatch cookable)
        {
            var src = sourcePrefab != null ? sourcePrefab.GetComponent<KPrefabID>()?.PrefabID().Name ?? "null" : "null";
            return $"{src}::{cookable?.ID ?? "null"}::{cookable?.temperature:F3}::{cookable?.enableFreezing}:{cookable?.requirePressure}:{cookable?.pressureThreshold:F3}";
        }

        private static CodexConversionPanel BuildEnvironmentalCookingPanel(GameObject sourcePrefab, EnviromentCookablePatch cookable)
        {
            try
            {
                string title = STRINGS.CODEX.PANELS.ENVIROMENTCOOKING;
                var conditions = new List<ElementUsage>();

                // Temperature
                {
                    string tempIcon = cookable.enableFreezing ? "crew_state_temp_down" : "crew_state_temp_up";
                    Tag tempTag = new Tag(tempIcon);
                    float tK = Mathf.Max(0f, cookable.temperature);
                    string label = $" {GameUtil.GetFormattedTemperature(tK)}";
                    conditions.Add(new ElementUsage(tempTag, tK, false, (tag, amount, continuous) => label));
                }

                // Element gates
                if (cookable.triggeringElements != null && cookable.triggeringElements.Count > 0)
                {
                    foreach (var hash in cookable.triggeringElements)
                    {
                        var el = ElementLoader.FindElementByHash(hash);
                        if (el == null) continue;
                        conditions.Add(new ElementUsage(el.tag, 0f, false, (tag, amount, continuous) => el.name));
                    }
                }

                // Pressure
                if (cookable.requirePressure && cookable.pressureThreshold > 0f)
                {
                    Tag pressureTag = new Tag("Pressure");
                    string label = $"Pressure >{GameUtil.GetFormattedMass(cookable.pressureThreshold)}";
                    conditions.Add(new ElementUsage(pressureTag, cookable.pressureThreshold, false, (tag, amount, continuous) => label));
                }

                // Outputs
                var outputs = new List<ElementUsage>();
                if (!string.IsNullOrEmpty(cookable.ID))
                {
                    Tag outTag = new Tag(cookable.ID);
                    float ratio = Mathf.Max(0f, cookable.massConversionRatio);
                    outputs.Add(new ElementUsage(outTag, ratio, false));
                }

                return new CodexConversionPanel(title, conditions.ToArray(), outputs.ToArray(), sourcePrefab, null);
            }
            catch
            {
                return null;
            }
        }

        // Placement helpers

        private static int FindInsertIndexAfterHeader(List<ContentContainer> containers, string header)
        {
            if (containers == null || containers.Count == 0 || string.IsNullOrEmpty(header))
                return -1;

            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c?.content == null || c.content.Count == 0) continue;

                foreach (var w in c.content)
                {
                    if (w is CodexText ct)
                    {
                        var text = ct.text?.Trim();
                        if (!string.IsNullOrEmpty(text) &&
                            string.Equals(text, header, StringComparison.OrdinalIgnoreCase))
                        {
                            return Math.Min(i + 1, containers.Count);
                        }
                    }
                }
            }
            return -1;
        }

        private static int FindInsertIndexFallback(List<ContentContainer> containers)
        {
            if (containers == null || containers.Count == 0)
                return 0;

            // Before first conversion panel
            int firstConv = IndexOfFirstConversionPanel(containers);
            if (firstConv != -1)
                return Mathf.Clamp(firstConv, 0, containers.Count);

            // After first text block
            int firstText = IndexOfFirstTextContainer(containers);
            if (firstText != -1)
                return Mathf.Clamp(firstText + 1, 0, containers.Count);

            // Bottom by default
            return containers.Count;
        }

        private static int IndexOfFirstConversionPanel(List<ContentContainer> containers)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c?.content == null) continue;
                foreach (var w in c.content)
                {
                    if (w is CodexConversionPanel)
                        return i;
                }
            }
            return -1;
        }

        private static int IndexOfFirstTextContainer(List<ContentContainer> containers)
        {
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c?.content == null) continue;
                foreach (var w in c.content)
                {
                    if (w is CodexText)
                        return i;
                }
            }
            return -1;
        }

        // Prefab resolution helpers

        private static GameObject TryResolvePrefabByEntryId(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;

            var go = Assets.TryGetPrefab(new Tag(entryId));
            if (go != null) return go;

            int idx = entryId.LastIndexOf("::", StringComparison.Ordinal);
            if (idx >= 0 && idx < entryId.Length - 2)
            {
                string suffix = entryId.Substring(idx + 2);
                go = Assets.TryGetPrefab(new Tag(suffix));
                if (go != null) return go;
            }

            return null;
        }

        private static GameObject TryResolvePrefabFromEntry(CodexEntry entry)
        {
            if (entry == null) return null;

            if (!string.IsNullOrEmpty(entry.id))
            {
                var go = Assets.TryGetPrefab(new Tag(entry.id));
                if (go != null) return go;

                int idx = entry.id.LastIndexOf("::", StringComparison.Ordinal);
                if (idx >= 0 && idx < entry.id.Length - 2)
                {
                    string last = entry.id.Substring(idx + 2);
                    go = Assets.TryGetPrefab(new Tag(last));
                    if (go != null) return go;
                }
            }

            return null;
        }

        private static GameObject TryResolvePrefabFromSubEntry(SubEntry se)
        {
            if (se == null) return null;

            if (!string.IsNullOrEmpty(se.id))
            {
                var go = Assets.TryGetPrefab(new Tag(se.id));
                if (go != null) return go;

                int idx = se.id.LastIndexOf("::", StringComparison.Ordinal);
                if (idx >= 0 && idx < se.id.Length - 2)
                {
                    string last = se.id.Substring(idx + 2);
                    go = Assets.TryGetPrefab(new Tag(last));
                    if (go != null) return go;
                }
            }

            return null;
        }

        private static GameObject TryResolveSeedFromPlant(GameObject plantPrefab)
        {
            var kpid = plantPrefab.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                var name = kpid.PrefabID().Name;
                var candidate = Assets.GetPrefab(new Tag(name + "Seed"));
                if (candidate != null) return candidate;
            }

            return null;
        }

        // Element ID normalization

        internal static string TryCanonicalizeElementId(string idOrTag)
        {
            if (string.IsNullOrEmpty(idOrTag)) return null;

            Element el = ElementLoader.FindElementByName(idOrTag);
            if (el != null) return el.id.ToString();

            try
            {
                var tag = new Tag(idOrTag);
                el = ElementLoader.GetElement(tag);
                if (el != null) return el.id.ToString();
            }
            catch { }

            SimHashes hash;
            if (Enum.TryParse<SimHashes>(idOrTag, true, out hash))
            {
                el = ElementLoader.FindElementByHash(hash);
                if (el != null) return el.id.ToString();
            }

            return null;
        }

        internal static IEnumerable<string> GetElementAliases(string idOrTag)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Element el = ElementLoader.FindElementByName(idOrTag);
            if (el == null)
            {
                try { el = ElementLoader.GetElement(new Tag(idOrTag)); } catch { }
            }
            if (el == null)
            {
                SimHashes hash;
                if (Enum.TryParse<SimHashes>(idOrTag, true, out hash))
                    el = ElementLoader.FindElementByHash(hash);
            }

            if (el != null)
            {
                var canonical = el.id.ToString(); // e.g., "Oxygen"
                set.Add(canonical);
                set.Add(el.tag.ToString());       // e.g., "OXYGEN"
                set.Add($"ELEMENTS::{canonical}");
            }

            if (!string.IsNullOrEmpty(idOrTag))
                set.Add(idOrTag);

            return set;
        }

        internal static IEnumerable<string> GetElementAliases(Element el)
        {
            if (el == null) yield break;
            yield return el.id.ToString();
            yield return el.tag.ToString();
            yield return $"ELEMENTS::{el.id}";
        }

        private static void TryAppendAllForElementAliasesDirect(CodexEntry elementEntry, IEnumerable<string> aliases)
        {
            if (elementEntry?.contentContainers == null || aliases == null)
                return;

            var seen = EntryPanelKeys.GetOrCreateValue(elementEntry);

            foreach (var alias in aliases)
            {
                if (string.IsNullOrEmpty(alias)) continue;
                if (!OutputBuckets.TryGetValue(alias, out var list) || list.Count == 0)
                    continue;

                foreach (var (sourcePrefab, cookable) in list)
                {
                    var key = BuildPanelKey(sourcePrefab, cookable);
                    if (seen.Contains(key))
                        continue;

                    var panel = BuildEnvironmentalCookingPanel(sourcePrefab, cookable);
                    if (panel == null) continue;

                    int insertIdx = FindInsertIndexAfterHeader(elementEntry.contentContainers, HeaderProducedBy);
                    if (insertIdx < 0)
                        insertIdx = FindInsertIndexFallback(elementEntry.contentContainers);

                    var cc = new ContentContainer(new List<ICodexWidget> { panel }, ContentContainer.ContentLayout.Vertical);
                    if (insertIdx >= 0 && insertIdx <= elementEntry.contentContainers.Count)
                        elementEntry.contentContainers.Insert(insertIdx, cc);
                    else
                        elementEntry.contentContainers.Add(cc);

                    seen.Add(key);
                }
            }
        }
    }
}