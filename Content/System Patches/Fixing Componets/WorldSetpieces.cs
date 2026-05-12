using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Patches
{
    [HarmonyPatch]
    internal static class CeresBaseFieldRationPatch
    {
        private const string TargetKey = "ceresbase";
        private const string TargetBuildingId = "RationBox";
        private const string SourceItemId = "Pemmican";
        private const string TargetItemId = "FieldRation";
        private const double TargetUnits = 20;

        public static bool IsCeresBaseTemplate(string name)
        {
            return !string.IsNullOrEmpty(name) && 
                   name.IndexOf(TargetKey, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool ReplacePemmicanInRationBox(object templateContainer)
        {
            if (templateContainer == null)
                return false;

            var trav = Traverse.Create(templateContainer);
            var buildings = GetList(trav, "buildings");
            if (buildings == null || buildings.Count == 0)
                return false;

            Debug.Log("[Rephysicalized] Buildings count: " + buildings.Count);

            // Find RationBox
            foreach (var building in buildings)
            {
                if (building == null) continue;
                
                // Get building ID - try property first, then field
                var buildingId = GetIdValue(building);
                Debug.Log("[Rephysicalized] Building id: " + buildingId);
                
                if (!string.Equals(buildingId, TargetBuildingId, StringComparison.OrdinalIgnoreCase))
                    continue;

                Debug.Log("[Rephysicalized] Found RationBox!");

                // Get storage
                var bTrav = Traverse.Create(building);
                var storage = GetList(bTrav, "storage");
                if (storage == null || storage.Count == 0)
                {
                    Debug.Log("[Rephysicalized] No storage found");
                    continue;
                }

                Debug.Log("[Rephysicalized] Storage items: " + storage.Count);

                // Replace Pemmican
                foreach (var item in storage)
                {
                    if (item == null) continue;
                    if (TryReplaceItem(item))
                    {
                        Debug.Log("[Rephysicalized] Successfully replaced!");
                        return true;
                    }
                }
            }

            return false;
        }

        private static string GetIdValue(object obj)
        {
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            // Try property
            var prop = t.GetProperty("id", BF);
            if (prop != null)
            {
                try { return AsString(prop.GetValue(obj, null)); } catch { }
            }
            
            // Try field
            var field = t.GetField("id", BF);
            if (field != null)
            {
                try { return AsString(field.GetValue(obj)); } catch { }
            }
            
            return null;
        }

        private static bool TryReplaceItem(object item)
        {
            var itemId = GetIdValue(item);
            Debug.Log("[Rephysicalized] Storage item id: " + itemId);
            
            if (!string.Equals(itemId, SourceItemId, StringComparison.OrdinalIgnoreCase))
                return false;

            Debug.Log("[Rephysicalized] Found Pemmican, replacing...");

            var t = item.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            // Set id
            var idProp = t.GetProperty("id", BF);
            if (idProp != null) idProp.SetValue(item, TargetItemId, null);
            
            // Set units
            var unitsProp = t.GetProperty("units", BF);
            if (unitsProp != null) unitsProp.SetValue(item, (float)TargetUnits, null);
            
            return true;
        }

        private static List<object> GetList(Traverse trav, string fieldName)
        {
            try
            {
                var field = trav.Field(fieldName).GetValue();
                if (field is IEnumerable enumerable && !(field is string))
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(item);
                    return list;
                }
            }
            catch { }
            
            try
            {
                var prop = trav.Property(fieldName).GetValue();
                if (prop is IEnumerable enumerable && !(prop is string))
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(item);
                    return list;
                }
            }
            catch { }
            
            return null;
        }

        private static string AsString(object v)
        {
            return v as string ?? v?.ToString();
        }
    }

    [HarmonyPatch]
    internal static class Unified_TemplateCache_GetTemplate_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var types = new[] { "ProcGenGame.TemplateCache", "ProcGen.TemplateCache", "TemplateCache" };
            var found = new List<MethodBase>();

            foreach (var typeName in types)
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) continue;

                var m1 = AccessTools.Method(t, "GetTemplate", new[] { typeof(string) });
                if (m1 != null) found.Add(m1);

                var m2 = AccessTools.Method(t, "GetTemplate", new[] { typeof(string), typeof(bool) });
                if (m2 != null) found.Add(m2);
            }
            return found;
        }

        static void Postfix(object __result, object[] __args)
        {
            if (__result == null || __args == null || __args.Length == 0)
                return;

            var key = __args[0] as string;
            if (string.IsNullOrEmpty(key))
                return;

  

            if (!CeresBaseFieldRationPatch.IsCeresBaseTemplate(key))
                return;

            CeresBaseFieldRationPatch.ReplacePemmicanInRationBox(__result);
        }
    }
}

