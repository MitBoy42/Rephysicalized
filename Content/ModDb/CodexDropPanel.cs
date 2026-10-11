using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content
{
    // Shared building blocks for the Codex "drops" conversion panels
    // (plant harvest drops and creature extra drops). Both panels list each output
    // as a percentage rather than a mass, so the formatter and tag resolution are shared.
    internal static class CodexDropPanel
    {
        // Appends one "id = N%" row to a CodexConversionPanel output list, skipping
        // entries that are empty or cannot be resolved to a prefab/element Tag.
        public static void AddOutput(List<ElementUsage> outputs, string id, float fraction)
        {
            if (outputs == null) return;
            if (string.IsNullOrEmpty(id) || fraction <= 0f) return;
            if (!TryResolveTag(id, out var tag)) return;

            outputs.Add(new ElementUsage(
                tag: tag,
                amount: Mathf.Clamp01(fraction),
                // continuous=false so the panel won't append per-cycle unit text
                continuous: false,
                customFormating: PercentFormatter));
        }

        public static string PercentFormatter(Tag tag, float amount, bool continuous)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(amount * 100f), 0, 10000);
            return $"{pct}%";
        }

        // Resolve a drop id that can be either a prefab or an element into a Tag.
        public static bool TryResolveTag(string id, out Tag tag)
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

            // Try element by Tag name
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
    }
}
