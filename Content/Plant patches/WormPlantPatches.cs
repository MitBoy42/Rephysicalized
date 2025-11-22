using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plants
{
    // Buffers mass keyed by cell for a short time window to bridge destroy<->spawn transforms.
    // Supports both orders:
    // - Cleanup first: SaveMass() -> OnSpawn applies immediately.
    // - Spawn first: OnSpawn registers a pending target -> SaveMass() applies to the pending target.
    internal static class PlantMassCarryoverBuffer
    {
        private struct SaveEntry
        {
            public float mass;
            public float timeSaved;
        }

        private struct PendingEntry
        {
            public GameObject target;
            public float timeSpawned;
        }

        private static readonly Dictionary<int, SaveEntry> saved = new Dictionary<int, SaveEntry>();
        private static readonly Dictionary<int, PendingEntry> pending = new Dictionary<int, PendingEntry>();

        // Consider a replacement to be the “same plant” in the same cell within this window (seconds).
        private const float TTL_SECONDS = 2.0f;

        // Dedupe window to avoid multiple saves for the same destroy sequence (seconds)
        private const float DEDUPE_SECONDS = 0.1f;

        public static void SaveMass(GameObject source)
        {
            try
            {
                if (source == null) return;
                var pe = source.GetComponent<PrimaryElement>();
                if (pe == null) return;

                int cell = Grid.PosToCell(source.transform.GetPosition());
                if (!Grid.IsValidCell(cell)) return;

                // Deduplicate rapid repeats from multiple components' OnCleanUp
                if (saved.TryGetValue(cell, out var existing) && (Time.time - existing.timeSaved) < DEDUPE_SECONDS)
                    return;

                float mass = pe.Mass;
                if (mass <= 0f) return;

                saved[cell] = new SaveEntry { mass = mass, timeSaved = Time.time };
              //  Debug.Log($"[PlantMassCarryover] Saved {mass:0.###} kg from {GetPrefabId(source)} at cell {cell}.");

                // If a pending apply exists (spawn happened first), apply now
                if (pending.TryGetValue(cell, out var pend))
                {
                    if (pend.target != null && (Time.time - pend.timeSpawned) <= TTL_SECONDS)
                    {
                        ApplyMassToTarget(pend.target, mass, cell, "(pending-fulfill)");
                    }
                    pending.Remove(cell);
                    // Clear saved after applying (to avoid double apply if OnSpawn also tries)
                    saved.Remove(cell);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlantMassCarryover] SaveMass failed: {e}");
            }
        }

        public static void TryApplyMass(GameObject target)
        {
            try
            {
                if (target == null) return;
                var pe = target.GetComponent<PrimaryElement>();
                if (pe == null) return;

                int cell = Grid.PosToCell(target.transform.GetPosition());
                if (!Grid.IsValidCell(cell)) return;

                // If we already have a saved mass (cleanup happened first), apply now
                if (saved.TryGetValue(cell, out var entry))
                {
                    if ((Time.time - entry.timeSaved) <= TTL_SECONDS)
                    {
                        ApplyMassToTarget(target, entry.mass, cell, "(direct)");
                        saved.Remove(cell);
                        pending.Remove(cell);
                        return;
                    }
                    // Drop stale saves
                    saved.Remove(cell);
                }

                // Otherwise, register a pending apply (spawn happened first)
                pending[cell] = new PendingEntry { target = target, timeSpawned = Time.time };
       //         Debug.Log($"[PlantMassCarryover] Registered pending apply for {GetPrefabId(target)} at cell {cell}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlantMassCarryover] TryApplyMass failed: {e}");
            }
        }

        private static void ApplyMassToTarget(GameObject target, float mass, int cell, string reasonTag)
        {
            try
            {
                var pe = target.GetComponent<PrimaryElement>();
                if (pe == null) return;

                float before = pe.Mass;
                pe.Mass = mass;

                // Also sync PlantMassTracker and PlotMassStore so yields use the carried mass
                SyncTrackerAndPlotStore(target, mass);

          //      Debug.Log($"[PlantMassCarryover] Applied mass carryover {mass:0.###} kg to {GetPrefabId(target)} at cell {cell} {reasonTag} (was {before:0.###} kg).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlantMassCarryover] ApplyMassToTarget failed: {e}");
            }
        }

        private static void SyncTrackerAndPlotStore(GameObject go, float mass)
        {
            try
            {
                // 1) Update the per-plot store so future spawns load the value
                var plot = go.GetComponentInParent<PlantablePlot>();
                if (plot != null)
                {
                    var store = plot.gameObject.AddOrGet<PlotMassStore>();
                    store.Set(mass);
                }

                // 2) Update PlantMassTrackerComponent's trackedMassKg via reflection (private field)
                var tracker = go.GetComponent<Rephysicalized.PlantMassTrackerComponent>();
                if (tracker != null)
                {
                    var t = tracker.GetType();
                    BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

                    var fTracked = t.GetField("trackedMassKg", BF);
                    if (fTracked != null) fTracked.SetValue(tracker, mass);

                    var fLastApplied = t.GetField("_lastAppliedTrackedKg", BF);
                    if (fLastApplied != null) fLastApplied.SetValue(tracker, mass);

                    var fLastObserved = t.GetField("_lastObservedPlantMassKg", BF);
                    if (fLastObserved != null) fLastObserved.SetValue(tracker, mass);

                    var fSuppress = t.GetField("_suppressInitialExternalDelta", BF);
                    if (fSuppress != null) fSuppress.SetValue(tracker, false);

                    // If it bound a PlotMassStore, force a write (best-effort)
                    var fPlotStore = t.GetField("_plotStore", BF);
                    var plotStore = fPlotStore?.GetValue(tracker);
                    if (plotStore != null)
                    {
                        var mSet = plotStore.GetType().GetMethod("Set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        mSet?.Invoke(plotStore, new object[] { mass });
                    }
                }
            }
            catch (Exception e)
            {
            //    Debug.LogWarning($"[PlantMassCarryover] SyncTrackerAndPlotStore failed: {e}");
            }
        }

        private static string GetPrefabId(GameObject go)
        {
            try
            {
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid != null)
                {
                    var id = kpid.PrefabID();
                    return id != null ? id.ToString() : go.name;
                }
            }
            catch
            {
                // Fall through
            }
            return go != null ? go.name : "<null>";
        }
    }

    // Save mass right before cleanup only for transforming plants, once (Growing component only)
    [HarmonyPatch(typeof(KMonoBehaviour), "OnCleanUp")]
    public static class KMonoBehaviour_OnCleanUp_SaveMass_ForTransformingPlants
    {
        public static void Prefix(KMonoBehaviour __instance)
        {
            if (__instance == null) return;

            // Only run for the Growing component (avoid duplicates from other components)
            if (!(__instance is Growing)) return;

            var go = __instance.gameObject;
            // Only transforming plants: require TransformingPlant + PrimaryElement
            if (!go.TryGetComponent(out TransformingPlant _)) return;
            if (!go.TryGetComponent(out PrimaryElement _)) return;

            PlantMassCarryoverBuffer.SaveMass(go);
        }
    }

    // When a new plant (Growing) spawns, try to apply saved mass or register pending
    [HarmonyPatch(typeof(KMonoBehaviour), "OnSpawn")]
    public static class KMonoBehaviour_OnSpawn_ApplyMass_ForPlants
    {
        public static void Postfix(KMonoBehaviour __instance)
        {
            if (__instance == null) return;

            // Only run once for the Growing component
            if (!(__instance is Growing)) return;

            var go = __instance.gameObject;
            if (!go.TryGetComponent(out PrimaryElement _)) return;

            PlantMassCarryoverBuffer.TryApplyMass(go);
        }
    }
}