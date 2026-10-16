using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    /// <summary>
    /// M5: after every mod has loaded, list the other mods that also replace (bool prefix) a
    /// method ElecNet replaces. Nothing is changed for them; the log line says which mod's code
    /// runs. With no such mod installed this logs one "no conflicts" line and does nothing else.
    /// </summary>
    public static class WireCompatability
    {
        /// <summary>"owner on Type.Method" for each foreign skip prefix found; read by the rig.</summary>
        public static readonly List<string> ForeignReplacements = new List<string>();

        private static IEnumerable<MethodBase> Replaced()
        {
            yield return AccessTools.Method(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.UpdateOverloadTime));
            yield return AccessTools.Method(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.GetMaxSafeWattage));
        }

        public static void Report(string ownId)
        {
            ForeignReplacements.Clear();
            foreach (MethodBase m in Replaced())
            {
                HarmonyLib.Patches info = m == null ? null : Harmony.GetPatchInfo(m);
                if (info == null)
                {
                    continue;
                }
                foreach (Patch p in info.Prefixes)
                {
                    if (p.owner != ownId && p.PatchMethod.ReturnType == typeof(bool))
                    {
                        ForeignReplacements.Add(p.owner + " on " + m.DeclaringType.Name + "." + m.Name);
                    }
                }
            }
            if (ForeignReplacements.Count == 0)
            {
                Debug.Log("[ElecNet] compat: no other mod replaces the overload tick");
                return;
            }
            foreach (string r in ForeignReplacements)
            {
                Debug.LogWarning("[ElecNet] compat: " + r + " also replaces this method. ElecNet's runs first while enabled "
                 );
            }
        }
    }
}
