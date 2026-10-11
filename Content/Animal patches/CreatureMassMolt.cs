using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Molt-only handling:
    // - Adds an additional condition that the creature mass must exceed (moltMass + startingMass).
    // - When the creature molts, subtract moltMass from CreatureMassTracker body mass via AddExternalMass(-moltMass).
    [HarmonyPatch]
    internal static class CreatureMassMolt_AdditionalCondition_AndMassDrain
    {

    // 1) Predicate hook: only allow molt if creatureMass > moltMass + startingMass
    [HarmonyPatch(typeof(MoltDropperMonitor.Instance), nameof(MoltDropperMonitor.Instance.ShouldDropElement))]
    [HarmonyPostfix]
    private static void ShouldDropElement_Postfix(MoltDropperMonitor.Instance __instance, ref bool __result)
    {
        if (__instance == null)
            return;

        if (!__result)
            return;

        var def = __instance.def;
        if (def == null)
            return;

        var go = __instance.gameObject;
        if (go == null)
            return;

        var tracker = go.GetComponent<CreatureMassTracker>();
        if (tracker == null)
            return;

        float moltMass = Mathf.Max(0f, def.massToDrop);
        if (moltMass <= 0f)
            return;

        float creatureMass = tracker.GetCurrentMass();
        float startingMass = Mathf.Max(0.001f, tracker.STARTING_MASS);

        if (!(creatureMass > moltMass + startingMass))
            __result = false;
    }

    // 2) After actual drop: subtract moltMass using CreatureMassTracker external mass.
    // Target all instance methods named Drop* except ShouldDropElement.
    private static IEnumerable<MethodBase> TargetDropMethods()
    {
        var instType = typeof(MoltDropperMonitor.Instance);
        var methods = AccessTools.GetDeclaredMethods(instType);
        foreach (var m in methods)
        {
            if (m == null || m.IsStatic || m.IsAbstract || m.IsConstructor)
                continue;

            string name = m.Name ?? string.Empty;
            if (name.StartsWith("Drop", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("ShouldDropElement", StringComparison.OrdinalIgnoreCase))
                yield return m;
        }
    }

    [HarmonyPatch]
    private static class DropMassDrain_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods() => CreatureMassMolt_AdditionalCondition_AndMassDrain.TargetDropMethods();

        private static void Postfix(MoltDropperMonitor.Instance __instance)
        {
            if (__instance == null)
                return;

            var def = __instance.def;
            if (def == null)
                return;

            float moltMass = Mathf.Max(0f, def.massToDrop);
            if (moltMass <= 0f)
                return;

            var tracker = __instance.gameObject != null ? __instance.gameObject.GetComponent<CreatureMassTracker>() : null;
            if (tracker == null)
                return;

            tracker.AddExternalMass(-moltMass);
        }
    }
}



}