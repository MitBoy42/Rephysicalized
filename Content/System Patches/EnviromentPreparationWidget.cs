using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Rephysicalized.Content.ModDb
{

    internal static class EnvCookablePanelTags
    {
        public static readonly Tag TemperatureFreezing = new Tag("crew_state_temp_down");
        public static readonly Tag TemperatureNormal = new Tag("crew_state_temp_up");

        // Registered by ModAssets as "ui_icon_pressure", so Assets.GetSprite resolves it.
        public static readonly Tag Pressure = new Tag("ui_icon_pressure");
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

            var canonical = EnvCookableCodexIntegration.TryCanonicalizeElementId(id);
            if (!string.IsNullOrEmpty(canonical) && entries.TryGetValue(canonical, out var e2))
                return e2;

            return null;
        }
    }

    [HarmonyPatch]
    internal static class EnvCookableCodexIntegration
    {
        private const string HeaderApplications = "Applications";
        private const string HeaderProducedBy = "Produced By";

        // Buckets of panels by output id/alias (target pages)
        private static readonly Dictionary<string, List<(GameObject sourcePrefab, EnviromenmentalPreparation patch)>> OutputBuckets
            = new Dictionary<string, List<(GameObject, EnviromenmentalPreparation)>>(256, StringComparer.OrdinalIgnoreCase);

        private static readonly ConditionalWeakTable<CodexEntry, HashSet<string>> EntryPanelKeys
            = new ConditionalWeakTable<CodexEntry, HashSet<string>>();

        [HarmonyPatch(typeof(CodexCache), "AddEntry")]
        [HarmonyPostfix]
        private static void CodexCache_AddEntry_Postfix(CodexEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                return;

            CodexEntriesRegistry.Add(entry);

            if (entry.contentContainers != null)
            {
                GameObject prefab = TryResolvePrefab(entry?.id);
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

                GameObject plantPrefab = TryResolvePrefab(entry?.id);
                TryAppendSourcePanels(entry, plantPrefab);

                if (entry.subEntries != null)
                {
                    foreach (var se in entry.subEntries)
                    {
                        if (se == null || se.contentContainers == null) continue;

                        GameObject seedPrefab = TryResolvePrefab(se?.id);
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

                GameObject prefab = TryResolvePrefab(entry?.id);
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

                GameObject prefab = TryResolvePrefab(entry?.id);
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

            var patches = prefab.GetComponents<EnviromenmentalPreparation>();
            if (patches.Length == 0) return;

            var seen = EntryPanelKeys.GetOrCreateValue(entry);

            foreach (var cookable in patches)
            {
                InsertCookingPanel(entry.contentContainers, HeaderApplications, prefab, cookable, seen);

                // Bucket for target pages and try immediate append
                BucketCookableOutputs(cookable, prefab);
                TryAppendAllForOutputId(cookable.ID);
            }
        }

 
        private static void InsertCookingPanel(
            List<ContentContainer> containers,
            string header,
            GameObject sourcePrefab,
            EnviromenmentalPreparation cookable,
            HashSet<string> seen)
        {
            if (containers == null || cookable == null) return;

            var panel = BuildEnvironmentalCookingPanel(sourcePrefab, cookable);
            if (panel == null) return;

            string key = BuildPanelKey(sourcePrefab, cookable);
            if (seen != null && !seen.Add(key)) return;

            int index = FindInsertIndexAfterHeader(containers, header);
            if (index < 0)
                index = FindInsertIndexFallback(containers);

            // FindInsertIndexFallback always yields a valid index, so no bounds re-check is needed.
            containers.Insert(index, new ContentContainer(
                new List<ICodexWidget> { panel }, ContentContainer.ContentLayout.Vertical));
        }

        private static void TryAppendSourcePanelsToSubEntry(SubEntry se, GameObject prefab)
        {
            if (se?.contentContainers == null || prefab == null) return;

            var patches = prefab.GetComponents<EnviromenmentalPreparation>();
            if (patches.Length == 0) return;

            foreach (var cookable in patches)
            {
                InsertCookingPanel(se.contentContainers, HeaderApplications, prefab, cookable, null);
                BucketCookableOutputs(cookable, prefab);
                TryAppendAllForOutputId(cookable.ID);
            }
        }

        // Bucket management

        private static void BucketCookableOutputs(EnviromenmentalPreparation cookable, GameObject prefab)
        {
            if (cookable == null || string.IsNullOrEmpty(cookable.ID)) return;

            AddToBucket(cookable.ID, prefab, cookable); // raw id

            // If it maps to an element, also map under aliases
            var aliases = GetElementAliases(cookable.ID);
            foreach (var a in aliases)
                AddToBucket(a, prefab, cookable);
        }

        private static void AddToBucket(string key, GameObject prefab, EnviromenmentalPreparation cookable)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!OutputBuckets.TryGetValue(key, out var list))
            {
                list = new List<(GameObject, EnviromenmentalPreparation)>();
                OutputBuckets[key] = list;
            }
            list.Add((prefab, cookable));
        }


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
                InsertCookingPanel(outEntry.contentContainers, HeaderProducedBy, sourcePrefab, cookable, seen);

            return true;
        }

        internal static ContentContainer BuildAllEnvCookablePanelsContainer()
        {
            try
            {
                var widgets = new List<ICodexWidget>();


                var unique = new HashSet<string>();
                foreach (var bucket in OutputBuckets)
                {
                    var list = bucket.Value;
                    if (list == null) continue;
                    foreach (var (sourcePrefab, cookable) in list)
                    {
                        var key = BuildPanelKey(sourcePrefab, cookable);
                        if (!unique.Add(key))
                            continue;

                        var panel = BuildEnvironmentalCookingPanel(sourcePrefab, cookable);
                        if (panel != null)
                            widgets.Add(panel);
                    }
                }

                if (widgets.Count <= 1) // only header present, nothing to show
                    return null;

                return new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);
            }
            catch
            {
                return null;
            }
        }

        // Panel construction

        private static string BuildPanelKey(GameObject sourcePrefab, EnviromenmentalPreparation cookable)
        {
            var src = sourcePrefab != null ? sourcePrefab.GetComponent<KPrefabID>()?.PrefabID().Name ?? "null" : "null";
            return $"{src}::{cookable?.ID ?? "null"}::{cookable?.temperature:F3}::{cookable?.enableFreezing}:{cookable?.requirePressure}:{cookable?.pressureThreshold:F3}";
        }

        private static CodexConversionPanel BuildEnvironmentalCookingPanel(GameObject sourcePrefab, EnviromenmentalPreparation cookable)
        {
            if (cookable == null || sourcePrefab == null) return null;

            var conditions = new List<ElementUsage>();


            Tag tempTag = cookable.enableFreezing
                ? EnvCookablePanelTags.TemperatureFreezing
                : EnvCookablePanelTags.TemperatureNormal;

            float tK = Mathf.Max(0f, cookable.temperature);
            conditions.Add(new ElementUsage(tempTag, tK, false,
                (tag, amount, continuous) => $" {GameUtil.GetFormattedTemperature(tK)}"));

            // Element gates
            if (cookable.triggeringElements != null)
            {
                foreach (var hash in cookable.triggeringElements)
                {
                    var el = ElementLoader.FindElementByHash(hash);
                    if (el == null) continue;
                    conditions.Add(new ElementUsage(el.tag, 0f, false, (tag, amount, continuous) => el.name));
                }
            }

            if (cookable.requirePressure && cookable.pressureThreshold > 0f)
            {
                conditions.Add(new ElementUsage(
                    EnvCookablePanelTags.Pressure,
                    cookable.pressureThreshold,
                    false,
                    (tag, amount, continuous) => $"Pressure >{GameUtil.GetFormattedMass(cookable.pressureThreshold)}"));
            }

            // Outputs
            var outputs = new List<ElementUsage>();
            if (!string.IsNullOrEmpty(cookable.ID))
                outputs.Add(new ElementUsage(new Tag(cookable.ID), Mathf.Max(0f, cookable.massConversionRatio), false));
            return new CodexConversionPanel(
                STRINGS.CODEX.PANELS.ENVIROMENTCOOKING, conditions.ToArray(), outputs.ToArray(), sourcePrefab, null);
        }


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

            int firstConv = IndexOfFirstConversionPanel(containers);
            if (firstConv != -1)
                return Mathf.Clamp(firstConv, 0, containers.Count);

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

        private static GameObject TryResolvePrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var go = Assets.TryGetPrefab(new Tag(id));
            if (go != null) return go;

            int idx = id.LastIndexOf("::", StringComparison.Ordinal);
            if (idx >= 0 && idx < id.Length - 2)
            {
                go = Assets.TryGetPrefab(new Tag(id.Substring(idx + 2)));
                if (go != null) return go;
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
            if (Enum.TryParse(idOrTag, true, out hash))
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
                if (Enum.TryParse(idOrTag, true, out hash))
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
            if (elementEntry?.contentContainers == null || aliases == null) return;

            var seen = EntryPanelKeys.GetOrCreateValue(elementEntry);

            foreach (var alias in aliases)
            {
                if (string.IsNullOrEmpty(alias)) continue;
                if (!OutputBuckets.TryGetValue(alias, out var list) || list.Count == 0) continue;

                foreach (var (sourcePrefab, cookable) in list)
                    InsertCookingPanel(elementEntry.contentContainers, HeaderProducedBy, sourcePrefab, cookable, seen);
            }
        }
    }

  
    [HarmonyPatch(typeof(CodexConversionPanel), "Configure")]
    internal static class EnvCookableConversionPanelFixups
    {
        // CODEX.HEAT.TITLE is UI.FormatAsLink("Heat", nameof(HEAT)), so the article id is "HEAT".
        private const string HeatArticleId = "HEAT";

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

      
        private static readonly FieldInfo InsField =
            FindField(typeof(CodexConversionPanel), "ins");

        private static readonly FieldInfo IngredientsField =
            FindField(typeof(CodexConversionPanel), "ingredientsContainer");

        private static readonly FieldInfo OnClickField =
            FindField(typeof(KButton), "onClick");

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, PrivateInstance);
                if (field != null) return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void Postfix(CodexConversionPanel __instance)
        {
            var ins = InsField?.GetValue(__instance) as ElementUsage[];
            var container = IngredientsField?.GetValue(__instance) as GameObject;
            if (ins == null || container == null) return;

            Transform rows = container.transform;

         
            int validCount = 0;
            foreach (var usage in ins)
            {
                if (usage.tag == Tag.Invalid) continue;
                validCount++;
            }

            int rowIndex = rows.childCount - validCount;
            if (rowIndex < 0) return;

            foreach (var usage in ins)
            {
                Tag tag = usage.tag;
                if (tag == Tag.Invalid) continue;
                if (rowIndex >= rows.childCount) break;

                var references = rows.GetChild(rowIndex).GetComponent<HierarchyReferences>();
                rowIndex++;
                if (references == null) continue;

                bool isFreezing = tag == EnvCookablePanelTags.TemperatureFreezing;
                bool isHeating = tag == EnvCookablePanelTags.TemperatureNormal;
                bool isPressure = tag == EnvCookablePanelTags.Pressure;

                if (!isFreezing && !isHeating && !isPressure) continue;

                var button = references.GetReference<KButton>("Button");

                if (isPressure)
                {
                    var icon = references.GetReference<Image>("Icon");
                    if (icon != null && ModAssets.PressureIcon != null)
                        icon.sprite = ModAssets.PressureIcon;

                    SetClickHandler(button, null);
                    continue;
                }

                var tempIcon = references.GetReference<Image>("Icon");
                if (tempIcon != null)
                {
                    tempIcon.sprite = Assets.GetSprite(
                        isFreezing ? (HashedString)"crew_state_temp_down" : (HashedString)"crew_state_temp_up");
                    tempIcon.color = isFreezing ? Color.blue : Color.red;
                }

                string description = GameUtil.SafeStringFormat(
                    (string)(isFreezing
                        ? global::STRINGS.CODEX.FORMAT_STRINGS.TEMPERATURE_UNDER
                        : global::STRINGS.CODEX.FORMAT_STRINGS.TEMPERATURE_OVER),
                    GameUtil.GetFormattedTemperature(usage.amount));

                var amount = references.GetReference<LocText>("Amount");
                if (amount != null)
                {
                    amount.text = description;
                    amount.color = isFreezing ? Color.blue : Color.red;
                }

                var tooltip = references.GetReference<ToolTip>("Tooltip");
                if (tooltip != null)
                    tooltip.toolTip = description;

                // Point the reader at the vanilla Heat page instead of a dead crew-state link.
                SetClickHandler(button, () => ManagementMenu.Instance.codexScreen.ChangeArticle(HeatArticleId));
            }
        }

    
        private static void SetClickHandler(KButton button, System.Action handler)
        {
            if (button == null || OnClickField == null) return;
            OnClickField.SetValue(button, handler);
        }
    }
}
