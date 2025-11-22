using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{

    [HarmonyPatch(typeof(Butcherable), nameof(Butcherable.CreateDrops))]
    public static class Butcherable_CreateDrops_AddRotPileForTracked
    {
        // Try to resolve an element by id (supports Tag string and SimHashes name)
        private static Element ResolveElement(string id)
        {
            Element element = null;

            // Try Tag-based lookup
            try { element = ElementLoader.GetElement(new Tag(id)); } catch { }

            // Try SimHashes parse if Tag lookup failed
            if (element == null && Enum.TryParse<SimHashes>(id, ignoreCase: true, out var hash))
            {
                try { element = ElementLoader.FindElementByHash(hash); } catch { }
            }

            return element;
        }

        // State-aware element spawn:
        private static bool TrySpawnElementStateAware(
            string id,
            int liquidCell,
            int gasCell,
            Vector3 solidPos,
            float mass,
            float temp,
            out GameObject spawned)
        {
            spawned = null;

            var element = ResolveElement(id);
            if (element == null)
                return false;

            if (element.IsSolid)
            {
                spawned = element.substance.SpawnResource(solidPos, mass, temp, byte.MaxValue, 0);
                return spawned != null;
            }

            if (element.IsLiquid)
            {
                if (Grid.IsValidCell(liquidCell) && !Grid.Solid[liquidCell])
                {
                    SimMessages.AddRemoveSubstance(
                        liquidCell,
                        element.id,
                        default(CellAddRemoveSubstanceEvent),
                        mass,
                        temp,
                        byte.MaxValue,
                        0,
                        do_vertical_solid_displacement: true,
                        callbackIdx: -1
                    );
                    return true;
                }
                return false;
            }

            if (element.IsGas)
            {
                if (Grid.IsValidCell(gasCell) && !Grid.Solid[gasCell])
                {
                    SimMessages.AddRemoveSubstance(
                        gasCell,
                        element.id,
                        default(CellAddRemoveSubstanceEvent),
                        mass,
                        temp,
                        byte.MaxValue,
                        0,
                        do_vertical_solid_displacement: true,
                        callbackIdx: -1
                    );
                    return true;
                }
                return false;
            }

            return false;
        }

        // Spawn any prefab (pickupable or live entity) and apply mass/temp if it has PrimaryElement
        private static GameObject TrySpawnPrefab(int targetCell, string id, float mass, float temp)
        {
            GameObject go = null;
            try
            {
                go = Scenario.SpawnPrefab(targetCell, 0, 0, id);
                if (go != null)
                {
                    go.SetActive(true);
                    var droppedPE = go.GetComponent<PrimaryElement>();
                    if (droppedPE != null)
                    {
                        droppedPE.Mass = mass;
                        droppedPE.Temperature = temp;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized][ExtraDrop][PFB] Exception spawning '{id}': {e.Message}");
            }
            return go;
        }

        public static void Postfix(Butcherable __instance, ref GameObject[] __result)
        {
            try
            {
                if (__instance == null)
                    return;

                // Ensure we always work with a non-null list and write back a non-null array
                var dropsOut = new List<GameObject>((__result != null) ? __result.Length : 0);
                if (__result != null && __result.Length > 0)
                    dropsOut.AddRange(__result);
                else
                    __result = Array.Empty<GameObject>();

                var pe = __instance.GetComponent<PrimaryElement>();
                if (pe == null)
                {
                    __result = dropsOut.ToArray();
                    return;
                }

                float totalMass = pe.Mass; // assumes get_Mass is patched to ComputedMass elsewhere

                // Sum mass of existing drops we already have in dropsOut
                float originalDropsMass = 0f;
                for (int i = 0; i < dropsOut.Count; i++)
                {
                    var go = dropsOut[i];
                    if (go == null) continue;
                    var dropPE = go.GetComponent<PrimaryElement>();
                    if (dropPE != null) originalDropsMass += dropPE.Mass;
                }

                float extra = totalMass - originalDropsMass;
                if (extra <= 0f)
                {
                    __result = dropsOut.ToArray();
                    return;
                }

                // Determine spawn cells/positions
                int baseCell = Grid.PosToCell(__instance.gameObject);
                int cellAbove = Grid.CellAbove(baseCell);

                // Solids: prefer above if not solid, else base
                int solidCell = (Grid.IsValidCell(cellAbove) && !Grid.Solid[cellAbove]) ? cellAbove : baseCell;
                Vector3 solidPos = Grid.CellToPosCCC(solidCell, Grid.SceneLayer.Ore);

                // Liquids: base cell
                int liquidCell = baseCell;

                // Gases: one tile above if valid, else base cell
                int gasCell = Grid.IsValidCell(cellAbove) ? cellAbove : baseCell;

                float temp = pe.Temperature;

                // Prefer instance-configured extra drops; fall back to registered species defaults.
                List<CreatureMassTracker.ExtraDropSpec> extraDrops = null;

                var tracker = __instance.GetComponent<CreatureMassTracker>(); // may be null
                if (tracker != null && tracker.ExtraDrops != null && tracker.ExtraDrops.Count > 0)
                {
                    extraDrops = tracker.ExtraDrops;
                }
                else
                {
                    var instKpid = __instance.GetComponent<KPrefabID>();
                    var registered = (instKpid != null) ? CreatureMassTracker.GetRegisteredDrops(instKpid.PrefabTag) : null;
                    if (registered != null && registered.Count > 0)
                        extraDrops = registered;
                }

                if (extraDrops == null || extraDrops.Count == 0)
                {
                    __result = dropsOut.ToArray();
                    return;
                }

                foreach (var spec in extraDrops)
                {
                    if (spec == null) continue;

                    float frac = Mathf.Max(0f, spec.fraction);
                    if (frac <= 0f) continue;

                    float massToSpawn = extra * frac;
                    if (massToSpawn <= 0f) continue;

                    // Prefer element spawn (state-aware: solids as pickupables, liquids/gases into sim)
                    if (TrySpawnElementStateAware(spec.id, liquidCell, gasCell, solidPos, massToSpawn, temp, out var elGo))
                    {
                        // Liquids/gases are sim-deposited (no GO); solids produce a GO we append
                        if (elGo != null)
                            dropsOut.Add(elGo);
                        continue;
                    }

                    // If not an element (or failed), spawn prefab as-is (can be a live entity)
                    var prefabGo = TrySpawnPrefab(solidCell, spec.id, massToSpawn, temp);
                    if (prefabGo != null)
                    {
                        dropsOut.Add(prefabGo);
                    }
                }

                __result = dropsOut.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] Extra mass drop extension failed: {e}");
            }
        }
    }
}
