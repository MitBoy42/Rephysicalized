using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Patches
{
    internal static class CeresBaseFieldRationPatch
    {
        private static readonly string[] KeyHints =
        {
            "dlc2::bases/ceresbase",
            "dlc2::templates/bases/ceresbase",
            "templates/bases/ceresbase",
            "bases/ceresbase",
            "ceresbase.yaml"
        };

        public static bool NameLooksLikeCeresBase(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < KeyHints.Length; i++)
                if (name.IndexOf(KeyHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        // Replace Pemmican -> FieldRation with element=Creature, units=20 (RationBox.storage preferred, generic fallback included).
        internal static bool ReplacePemmicanInRationBox(object templateContainer)
        {
            if (templateContainer == null)
                return false;

            bool changed = false;

            var trav = Traverse.Create(templateContainer);
            var buildingsEnum = GetEnumerable(trav, "buildings", "Buildings");
            if (buildingsEnum == null)
                return false;

            var buildings = ToObjectList(buildingsEnum);

            // Pass 1: Try known storage fields on RationBox only
            foreach (var b in buildings)
            {
                if (b == null) continue;
                var tb = Traverse.Create(b);
                string bid = AsString(GetFirstValue(tb, "id", "name", "prefab", "ID", "Id", "Name"));
                if (!string.Equals(bid, "RationBox", StringComparison.OrdinalIgnoreCase))
                    continue;

                var storageEnum = GetEnumerable(tb, "storage", "Storage", "storedItems", "StoredItems", "contents", "items", "Contents", "Items");
                if (storageEnum == null)
                    continue;

                foreach (var item in storageEnum)
                {
                    if (item == null) continue;
                    if (TryReplaceItem(item))
                        changed = true;
                }
            }

            // Pass 2: Generic fallback (if nothing in RationBox storage was replaced)
            if (!changed)
            {
                foreach (var b in buildings)
                {
                    if (b == null) continue;
                    if (ReplaceInAllEnumerables(b) > 0)
                        changed = true;
                }
            }

            return changed;
        }

        // Scan all enumerable members (fields/properties) on a building object and replace any Pemmican items found.
        private static int ReplaceInAllEnumerables(object building)
        {
            int replaced = 0;
            if (building == null) return replaced;

            var t = building.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Fields
            foreach (var fi in t.GetFields(BF))
            {
                var val = SafeGetMember(building, fi);
                foreach (var item in AsObjectsEnumerable(val) ?? Array.Empty<object>())
                {
                    if (item == null) continue;
                    if (TryReplaceItem(item))
                        replaced++;
                }
            }

            // Properties (skip indexers)
            foreach (var pi in t.GetProperties(BF))
            {
                if (pi.GetIndexParameters().Length != 0) continue;
                var val = SafeGetMember(building, pi);
                foreach (var item in AsObjectsEnumerable(val) ?? Array.Empty<object>())
                {
                    if (item == null) continue;
                    if (TryReplaceItem(item))
                        replaced++;
                }
            }

            return replaced;
        }

        // Try to replace an item whether it's a POCO or a dictionary-shaped object
        private static bool TryReplaceItem(object item)
        {
            // IDictionary (non-generic) path
            if (item is IDictionary dict)
            {
                string idStr = AsString(GetDictFirst(dict, "id", "name", "tag", "prefab", "Id", "Name"));
                if (!string.Equals(idStr, "Pemmican", StringComparison.OrdinalIgnoreCase))
                    return false;

                // id -> FieldRation
                TrySetDict(dict, new[] { "id", "name", "tag", "prefab", "Id", "Name" }, "FieldRation");
                // element -> Creature
                TrySetDict(dict, new[] { "element", "Element", "elem", "Elem" }, "Creature");
                // units -> 20
                TrySetNumberDict(dict, new[] { "units", "Units", "amount", "Amount", "count", "Count" }, 20);
                return true;
            }

            // POCO via Traverse (named members)
            var ti = Traverse.Create(item);
            string itemId = AsString(GetFirstValue(ti, "id", "name", "tag", "prefab", "Id", "Name"));
            if (!string.Equals(itemId, "Pemmican", StringComparison.OrdinalIgnoreCase))
            {
                // Pure reflection fallback: search for an "id-like" member containing "Pemmican"
                if (!TryGetMemberString(item, out var idMember, out var idValue) || !string.Equals(idValue, "Pemmican", StringComparison.OrdinalIgnoreCase))
                    return false;

                // write id
                SafeSetMember(item, idMember, "FieldRation");
                // write element
                if (TryFindMember(item, out var elemMember, "element", "Element", "elem", "Elem"))
                    SafeSetMember(item, elemMember, "Creature");
                // write units
                if (TryFindMember(item, out var unitsMember, "units", "Units", "amount", "Amount", "count", "Count"))
                    SafeSetNumericMember(item, unitsMember, 20);
                return true;
            }

            // Named-member happy path
            SetFirst(ti, "FieldRation", "id", "name", "tag", "prefab", "Id", "Name");
            SetFirst(ti, "Creature", "element", "Element", "elem", "Elem");
            SetNumberFirst(ti, 20, "units", "Units", "amount", "Amount", "count", "Count");
            return true;
        }

        // Helpers

        // Returns IEnumerable<object> for any object that implements IEnumerable (List<T>, arrays, etc.); null otherwise.
        private static IEnumerable<object> GetEnumerable(Traverse t, params string[] names)
        {
            foreach (var n in names)
            {
                // Field first
                try
                {
                    var v = t.Field(n).GetValue();
                    var e = AsObjectsEnumerable(v);
                    if (e != null) return e;
                }
                catch { }

                // Property fallback
                try
                {
                    var v = t.Property(n).GetValue();
                    var e = AsObjectsEnumerable(v);
                    if (e != null) return e;
                }
                catch { }
            }
            return null;
        }

        // Converts any IEnumerable (except string) to a List<object>, or returns null if not enumerable.
        private static IEnumerable<object> AsObjectsEnumerable(object seq)
        {
            if (seq == null || seq is string)
                return null;

            if (seq is IEnumerable e)
            {
                var list = new List<object>();
                foreach (var x in e)
                    list.Add(x);
                return list;
            }

            return null;
        }

        private static List<object> ToObjectList(IEnumerable<object> seq)
        {
            var list = new List<object>();
            if (seq == null) return list;
            foreach (var x in seq) list.Add(x);
            return list;
        }

        private static object GetFirstValue(Traverse t, params string[] names)
        {
            foreach (var n in names)
            {
                try { return t.Field(n).GetValue(); } catch { }
                try { return t.Property(n).GetValue(); } catch { }
            }
            return null;
        }

        private static string AsString(object v)
        {
            if (v == null) return null;
            try { return v as string ?? v.ToString(); }
            catch { return null; }
        }

        private static void SetFirst(Traverse t, object value, params string[] names)
        {
            foreach (var n in names)
            {
                try { t.Field(n).SetValue(value); return; } catch { }
                try { t.Property(n).SetValue(value); return; } catch { }
            }
        }

        private static void SetNumberFirst(Traverse t, double value, params string[] names)
        {
            foreach (var n in names)
            {
                // Field
                try
                {
                    var cur = t.Field(n).GetValue();
                    if (cur is double) { t.Field(n).SetValue((double)value); return; }
                    if (cur is float) { t.Field(n).SetValue((float)value); return; }
                    if (cur is int) { t.Field(n).SetValue((int)value); return; }
                    if (cur is long) { t.Field(n).SetValue((long)value); return; }
                    t.Field(n).SetValue(value); return;
                }
                catch { }

                // Property
                try
                {
                    var cur = t.Property(n).GetValue();
                    if (cur is double) { t.Property(n).SetValue((double)value); return; }
                    if (cur is float) { t.Property(n).SetValue((float)value); return; }
                    if (cur is int) { t.Property(n).SetValue((int)value); return; }
                    if (cur is long) { t.Property(n).SetValue((long)value); return; }
                    t.Property(n).SetValue(value); return;
                }
                catch { }
            }
        }

        // IDictionary helpers (keys may vary by case)
        private static object GetDictFirst(IDictionary dict, params string[] names)
        {
            foreach (var n in names)
            {
                if (dict.Contains(n)) return dict[n];
                foreach (var key in dict.Keys)
                {
                    if (string.Equals(key?.ToString(), n, StringComparison.OrdinalIgnoreCase))
                        return dict[key];
                }
            }
            return null;
        }

        private static void TrySetDict(IDictionary dict, IEnumerable<string> keys, object value)
        {
            foreach (var n in keys)
            {
                if (dict.Contains(n)) { dict[n] = value; return; }
                foreach (var key in dict.Keys)
                {
                    if (string.Equals(key?.ToString(), n, StringComparison.OrdinalIgnoreCase))
                    {
                        dict[key] = value;
                        return;
                    }
                }
            }
        }

        private static void TrySetNumberDict(IDictionary dict, IEnumerable<string> keys, double value)
        {
            foreach (var n in keys)
            {
                if (!dict.Contains(n))
                {
                    object foundKey = null;
                    foreach (var key in dict.Keys)
                    {
                        if (string.Equals(key?.ToString(), n, StringComparison.OrdinalIgnoreCase))
                        {
                            foundKey = key;
                            break;
                        }
                    }
                    if (foundKey == null) continue;
                    SetDictNumeric(dict, foundKey, value);
                    return;
                }
                else
                {
                    SetDictNumeric(dict, n, value);
                    return;
                }
            }
        }

        private static void SetDictNumeric(IDictionary dict, object key, double value)
        {
            var cur = dict[key];
            if (cur is double) dict[key] = (double)value;
            else if (cur is float) dict[key] = (float)value;
            else if (cur is int) dict[key] = (int)value;
            else if (cur is long) dict[key] = (long)value;
            else dict[key] = value;
        }

        // Pure reflection member helpers
        private static object SafeGetMember(object obj, FieldInfo fi)
        {
            try { return fi.GetValue(obj); } catch { return null; }
        }

        private static object SafeGetMember(object obj, PropertyInfo pi)
        {
            try { return pi.GetIndexParameters().Length == 0 ? pi.GetValue(obj, null) : null; } catch { return null; }
        }

        private static bool TryFindMember(object obj, out MemberInfo member, params string[] names)
        {
            member = null;
            if (obj == null) return false;
            var t = obj.GetType();
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in names)
            {
                var fi = t.GetField(name, BF);
                if (fi != null) { member = fi; return true; }
                var pi = t.GetProperty(name, BF);
                if (pi != null && pi.GetIndexParameters().Length == 0) { member = pi; return true; }
            }
            return false;
        }

        private static bool TryGetMemberString(object obj, out MemberInfo member, out string value)
        {
            member = null;
            value = null;
            if (obj == null) return false;

            string[] names = { "id", "name", "tag", "prefab", "Id", "ID", "Name" };
            if (!TryFindMember(obj, out member, names))
                return false;

            object raw = null;
            try
            {
                if (member is FieldInfo fi) raw = fi.GetValue(obj);
                else if (member is PropertyInfo pi) raw = pi.GetValue(obj, null);
            }
            catch { }

            value = AsString(raw);
            return value != null;
        }

        private static void SafeSetMember(object obj, MemberInfo member, object value)
        {
            try
            {
                if (member is FieldInfo fi) fi.SetValue(obj, value);
                else if (member is PropertyInfo pi) pi.SetValue(obj, value, null);
            }
            catch { }
        }

        private static void SafeSetNumericMember(object obj, MemberInfo member, double value)
        {
            try
            {
                if (member is FieldInfo fi)
                {
                    var t = fi.FieldType;
                    if (t == typeof(double)) fi.SetValue(obj, (double)value);
                    else if (t == typeof(float)) fi.SetValue(obj, (float)value);
                    else if (t == typeof(int)) fi.SetValue(obj, (int)value);
                    else if (t == typeof(long)) fi.SetValue(obj, (long)value);
                    else fi.SetValue(obj, value);
                }
                else if (member is PropertyInfo pi)
                {
                    var t = pi.PropertyType;
                    if (t == typeof(double)) pi.SetValue(obj, (double)value, null);
                    else if (t == typeof(float)) pi.SetValue(obj, (float)value, null);
                    else if (t == typeof(int)) pi.SetValue(obj, (int)value, null);
                    else if (t == typeof(long)) pi.SetValue(obj, (long)value, null);
                    else pi.SetValue(obj, value, null);
                }
            }
            catch { }
        }
    }

    // Hook TemplateCache.GetTemplate across known locations.
    [HarmonyPatch]
    internal static class Unified_TemplateCache_GetTemplate_Patch
    {
        private static readonly string[] CandidateTypes =
        {
            "ProcGenGame.TemplateCache",
            "ProcGen.TemplateCache",
            "TemplateCache"
        };

        static IEnumerable<MethodBase> TargetMethods()
        {
            var found = new List<MethodBase>();
            foreach (var typeName in CandidateTypes)
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) continue;

                var m1 = AccessTools.Method(t, "GetTemplate", new Type[] { typeof(string) });
                if (m1 != null) found.Add(m1);

                var m2 = AccessTools.Method(t, "GetTemplate", new Type[] { typeof(string), typeof(bool) });
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

            if (!CeresBaseFieldRationPatch.NameLooksLikeCeresBase(key))
                return;

            CeresBaseFieldRationPatch.ReplacePemmicanInRationBox(__result);
        }
    }
}