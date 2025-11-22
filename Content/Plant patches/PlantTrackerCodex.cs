using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches
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
            if (_target == null)
            {
             //   Debug.LogWarning("[PlantHarvestCodex] Could not find CodexEntryGenerator.GeneratePlantDescriptionContainers; panel patch disabled.");
                return false;
            }
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

                // Fallback: search by name/signature
                foreach (var mi in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (mi.Name != "GeneratePlantDescriptionContainers") continue;
                    var ps = mi.GetParameters();
                    if (ps.Length == 2 && typeof(GameObject).IsAssignableFrom(ps[0].ParameterType))
                        return mi;
                }
            }

            // Older alt
            var alt = AccessTools.TypeByName("CodexEntryGenerator_Plants");
            if (alt != null)
            {
                var m = AccessTools.Method(alt, "GeneratePlantDescriptionContainers", new Type[]
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
            if (yields == null || yields.Count == 0)
            {
         //       Debug.Log($"[PlantHarvestCodex] No registry yields found for '{prefabId}', skipping panel.");
                return;
            }

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
            // Preferred: PlantMassTrackerRegistry.TryGetConfig(prefabId, out cfg).yields
            var regType = AccessTools.TypeByName("Rephysicalized.PlantMassTrackerRegistry")
                          ?? AccessTools.TypeByName("PlantMassTrackerRegistry");
            if (regType == null)
                return new List<YieldSpec>();

            // Try TryGetConfig(string, out T)
            foreach (var m in regType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.Name != "TryGetConfig") continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].IsOut)
                {
                    var args = new object[] { prefabId, null };
                    bool ok = false;
                    try { ok = (bool)m.Invoke(null, args); } catch { ok = false; }
                    if (ok && args[1] != null)
                    {
                        var cfgObj = args[1];
                        var yields = ReadYieldsList(cfgObj);
                        if (yields.Count > 0) return yields;
                    }
                }
            }

            // Fallback: scan static fields for a registry dict keyed by string, with value that has a yields list
            foreach (var f in regType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var ft = f.FieldType;
                if (!IsDictionaryOf<string>(ft)) continue;

                var dictObj = f.GetValue(null);
                if (dictObj == null) continue;

                var tryGet = ft.GetMethod("TryGetValue");
                if (tryGet == null) continue;

                // Prepare args for TryGetValue(key, out value)
                var valueType = ft.GetGenericArguments()[1];
                object[] args2 = new object[] { prefabId, null };
                bool found = false;
                try { found = (bool)tryGet.Invoke(dictObj, args2); } catch { found = false; }
                if (found && args2[1] != null)
                {
                    var cfgObj = args2[1];
                    var yields = ReadYieldsList(cfgObj);
                    if (yields.Count > 0) return yields;
                }
            }

            return new List<YieldSpec>();
        }

        private static bool IsDictionaryOf<TK>(Type t)
        {
            return t.IsGenericType
                   && (t.GetGenericTypeDefinition() == typeof(Dictionary<,>) || t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                   && t.GetGenericArguments()[0] == typeof(TK);
        }

        private static List<YieldSpec> ReadYieldsList(object cfgObj)
        {
            var list = new List<YieldSpec>();
            if (cfgObj == null) return list;

            BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object yieldsObj = null;

            // Common names: yields, _yields, HarvestYields
            foreach (var name in new[] { "yields", "_yields", "HarvestYields", "harvestYields" })
            {
                var f = cfgObj.GetType().GetField(name, BF);
                if (f != null)
                {
                    yieldsObj = f.GetValue(cfgObj);
                    if (yieldsObj != null) break;
                }
                var p = cfgObj.GetType().GetProperty(name, BF);
                if (p != null && p.CanRead)
                {
                    yieldsObj = p.GetValue(cfgObj, null);
                    if (yieldsObj != null) break;
                }
            }

            if (yieldsObj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;

                    // MaterialYield: fields id (string) and multiplier (float)
                    string id = null;
                    float mult = 0f;

                    var it = item.GetType();
                    var fid = it.GetField("id", BF) ?? it.GetField("Id", BF);
                    if (fid != null)
                        id = fid.GetValue(item) as string ?? fid.GetValue(item)?.ToString();

                    var fmult = it.GetField("multiplier", BF) ?? it.GetField("Multiplier", BF);
                    if (fmult != null)
                    {
                        var v = fmult.GetValue(item);
                        if (v is float fv) mult = fv;
                        else if (v is double dv) mult = (float)dv;
                        else if (v is int iv) mult = iv;
                    }

                    if (!string.IsNullOrEmpty(id) && mult > 0f)
                        list.Add(new YieldSpec { id = id, multiplier = mult });
                }
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