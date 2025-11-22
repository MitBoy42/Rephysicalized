using System;
using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Per-prefab settings: threshold to trigger poop based on fueled mass and whether to append or replace vanilla emission
    internal static class FueledPoopSettings
    {
        private static readonly Dictionary<Tag, Settings> map = new Dictionary<Tag, Settings>();

        internal struct Settings
        {
            public float minFueledKgToPoop;      // trigger threshold
            public bool replaceVanillaEmission;  // not used by default; enable if you also zero vanilla poop
        }

        private static readonly Settings Default = new Settings
        {
            minFueledKgToPoop = 0.5f,     // Default trigger: 0.5 kg pending
            replaceVanillaEmission = false
        };

        public static void SetFor(Tag prefabTag, float minFueledKgToPoop = 0.5f, bool replaceVanillaEmission = false)
        {
            if (!prefabTag.IsValid) return;
            map[prefabTag] = new Settings
            {
                minFueledKgToPoop = Mathf.Max(0f, minFueledKgToPoop),
                replaceVanillaEmission = replaceVanillaEmission
            };
        }

        public static Settings GetFor(GameObject go)
        {
            var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
            if (kpid != null && map.TryGetValue(kpid.PrefabTag, out var s))
                return s;
            return Default;
        }
    }

    // Allow poop when enough fueled mass is pending
    [HarmonyPatch(typeof(CreatureCalorieMonitor), "ReadyToPoop")]
    internal static class CCM_ReadyToPoop_Fueled
    {
        static void Postfix(CreatureCalorieMonitor.Instance smi, ref bool __result)
        {
            if (__result || smi == null) return;

            var go = smi.gameObject;
            if (go == null) return;

            // Cooldown and pause checks same as vanilla
            if (smi.IsInsideState(smi.sm.pause)) return;
            if (Time.time - smi.lastMealOrPoopTime < smi.def.minimumTimeBeforePooping) return;

            var controller = go.GetComponent<FueledDietController>();
            if (controller == null) return;

            var settings = FueledPoopSettings.GetFor(go);
            if (controller.PeekPendingTotalMass() + 1e-6f >= settings.minFueledKgToPoop)
                __result = true;
        }
    }

    // Emit fueled outputs during Poop. We do not modify stomach or calories.
    [HarmonyPatch(typeof(CreatureCalorieMonitor.Instance), nameof(CreatureCalorieMonitor.Instance.Poop))]
    internal static class FueledPoopFlush_Postfix
    {
        [HarmonyPostfix]
        private static void Postfix(CreatureCalorieMonitor.Instance __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;
            if (go == null) return;

            var controller = go.GetComponent<FueledDietController>();
            if (controller == null) return;

            var outputs = ListPool<FueledDietController.FueledOutput, FueledDietController>.Allocate();
            try
            {
                if (controller.DequeuePendingOutputs(outputs) <= 0)
                    return;

                EmitFueled(go, outputs);
            }
            finally
            {
                outputs.Recycle();
            }
        }

        private static void EmitFueled(GameObject go, List<FueledDietController.FueledOutput> items)
        {
            if (items == null || items.Count == 0) return;

            // Coalesce by tag so we emit one chunk per element
            var byTag = new Dictionary<Tag, (float mass, float tempMass, byte dIdx, int dCnt)>();
            foreach (var it in items)
            {
                if (!it.Tag.IsValid || it.MassKg <= 0f) continue;
                if (!byTag.TryGetValue(it.Tag, out var v))
                    v = (0f, 0f, it.DiseaseIdx, 0);
                // Merge disease: keep idx with highest count
                if (it.DiseaseCount > v.dCnt)
                    v.dIdx = it.DiseaseIdx;
                v.mass += it.MassKg;
                v.tempMass += it.MassKg * it.TemperatureK;
                v.dCnt += it.DiseaseCount;
                byTag[it.Tag] = v;
            }

            int rootCell = Grid.PosToCell(go.transform.position);
            if (!Grid.IsValidCell(rootCell))
                rootCell = Grid.PosToCell(new Vector3(Mathf.Round(go.transform.position.x), Mathf.Round(go.transform.position.y), go.transform.position.z));

            foreach (var kv in byTag)
            {
                var tag = kv.Key;
                var v = kv.Value;

                float mass = v.mass;
                if (mass <= 0f) continue;

                float tempK = v.tempMass > 0f ? (v.tempMass / Mathf.Max(0.0001f, mass)) : (go.GetComponent<PrimaryElement>()?.Temperature ?? 300f);
                byte dIdx = v.dIdx;
                int dCnt = v.dCnt;

                if (!TryResolveElement(tag, out var element))
                {
                    // Fallback: spawn prefab by tag for solids
                    var prefab = Assets.GetPrefab(tag);
                    if (prefab != null)
                    {
                        var pos = Grid.CellToPos(rootCell, CellAlignment.Top, Grid.SceneLayer.Ore);
                        var spawned = GameUtil.KInstantiate(prefab, pos, Grid.SceneLayer.Ore);
                        var pe = spawned.GetComponent<PrimaryElement>();
                        if (pe != null)
                        {
                            pe.Mass = mass;
                            pe.Temperature = tempK;
                            pe.AddDisease(dIdx, dCnt, "FueledPoop");
                        }
                        spawned.SetActive(true);
                    }
                    continue;
                }

                // PopFX resource (like vanilla)
                try
                {
                    if (!string.IsNullOrEmpty(element.name))
                        PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Resource, element.name, go.transform);
                }
                catch { }

                int cell = FindNearbyNonSolidCell(rootCell);

                try
                {
                    if (element.IsLiquid)
                    {
                        // Single particle per element, remainder to sim to avoid particle storms
                        const float MaxParticleMass = 50f; // keep single-chunk feel
                        float particleMass = Mathf.Min(mass, MaxParticleMass);

                        FallingWater.instance?.AddParticle(cell, element.idx, particleMass, tempK, dIdx, dCnt, true);

                        float remaining = mass - particleMass;
                        if (remaining > 0f)
                        {
                            SimMessages.AddRemoveSubstance(cell, element.idx, CellEventLogger.Instance.ElementEmitted, remaining, tempK, dIdx, dCnt);
                        }
                    }
                    else if (element.IsGas)
                    {
                        SimMessages.AddRemoveSubstance(cell, element.idx, CellEventLogger.Instance.ElementEmitted, mass, tempK, dIdx, dCnt);
                    }
                    else
                    {
                        // Solid
                        element.substance.SpawnResource(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore), mass, tempK, dIdx, dCnt);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[FueledPoop] Emission failed for {tag}: {e}");
                }
            }
        }

        private static bool TryResolveElement(Tag tag, out Element element)
        {
            element = null;
            try
            {
                foreach (var el in ElementLoader.elements)
                {
                    if (el != null && el.tag == tag) { element = el; return true; }
                }
            }
            catch { }
            return false;
        }

        private static int FindNearbyNonSolidCell(int root)
        {
            if (Grid.IsValidCell(root) && !Grid.Solid[root]) return root;
            int[] offsets = { 0, 1, -1, Grid.WidthInCells, -Grid.WidthInCells,
                              Grid.WidthInCells + 1, Grid.WidthInCells - 1, -Grid.WidthInCells + 1, -Grid.WidthInCells - 1 };
            for (int i = 0; i < offsets.Length; i++)
            {
                int c = root + offsets[i];
                if (Grid.IsValidCell(c) && !Grid.Solid[c]) return c;
            }
            return root;
        }
    }
}