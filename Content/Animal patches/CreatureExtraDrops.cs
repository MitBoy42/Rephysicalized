using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Rephysicalized.Content.Animal_patches
{
    internal static class ScaleInfoLocal
    {
        public static bool TryGet(GameObject go, out Tag itemTag, out float dropMass, out float percent01)
        {
            itemTag = Tag.Invalid; dropMass = 0f; percent01 = 0f; if (go == null) return false;

            // Percent: prefer ScaleGrowth, else ElementGrowth
            float pct01 = 0f;
            var db = Db.Get();
            var scaleAmt = db?.Amounts?.ScaleGrowth?.Lookup(go);
            if (scaleAmt != null)
                pct01 = Mathf.Clamp01(scaleAmt.value / 100f);
            else
            {
                var elemAmt = db?.Amounts?.ElementGrowth?.Lookup(go);
                if (elemAmt != null)
                    pct01 = Mathf.Clamp01(elemAmt.value / 100f);
            }
            if (pct01 <= 0f) return false;

            // Definition: prefer ScaleGrowthMonitor, else WellFedShearable
            Tag tag = Tag.Invalid;
            float fullMass = 0f;

            var sgm = go.GetSMI<ScaleGrowthMonitor.Instance>();
            if (sgm?.def != null)
            {
                tag = sgm.def.itemDroppedOnShear;
                fullMass = Mathf.Max(0f, sgm.def.dropMass);
            }
            else
            {
                var wfs = go.GetSMI<WellFedShearable.Instance>();
                if (wfs?.def != null)
                {
                    tag = wfs.def.itemDroppedOnShear;
                    fullMass = Mathf.Max(0f, wfs.def.dropMass);
                }
            }

            if (!tag.IsValid || fullMass <= 0f) return false;

            itemTag = tag;
            dropMass = fullMass;
            percent01 = pct01;
            return true;
        }

        public static float GetCreatureTemperature(GameObject go)
        {
            var pe = go != null ? go.GetComponent<PrimaryElement>() : null;
            return pe != null ? pe.Temperature : 300f;
        }
    }

    [HarmonyPatch(typeof(Butcherable), nameof(Butcherable.CreateDrops))]
    public static class Butcherable_CreateDrops_AddScaleAndMilk_ThenExtra
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

                // Start with any already produced drops
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

                // Start mass bank from body+derived mass at death time (PE mass)
                float massBank = pe.Mass;
                var go = __instance.gameObject;

                // Determine spawn cells/positions once
                int baseCell = Grid.PosToCell(go);
                int cellAbove = Grid.CellAbove(baseCell);

                // Solids: prefer above if not solid, else base
                int solidCell = (Grid.IsValidCell(cellAbove) && !Grid.Solid[cellAbove]) ? cellAbove : baseCell;
                Vector3 solidPos = Grid.CellToPosCCC(solidCell, Grid.SceneLayer.Ore);

                // Liquids: base cell
                int liquidCell = baseCell;

                // Gases: one tile above if valid, else base cell
                int gasCell = Grid.IsValidCell(cellAbove) ? cellAbove : baseCell;

                float temp = pe.Temperature;

                // 1) Scales: spawn solids equal to floor(percent * dropMass), subtract from mass bank
                if (ScaleInfoLocal.TryGet(go, out var scaleTag, out var scaleFullDropMass, out var pct01))
                {
                    int scaleWholeMass = Mathf.FloorToInt(pct01 * scaleFullDropMass);
                    if (scaleWholeMass > 0)
                    {
                        var el = ElementLoader.GetElement(scaleTag);
                        if (el != null && el.IsSolid)
                        {
                            var spawned = el.substance.SpawnResource(solidPos, scaleWholeMass, temp, byte.MaxValue, 0);
                            if (spawned != null)
                                dropsOut.Add(spawned);
                        }
                        else
                        {
                            var prefab = Scenario.SpawnPrefab(solidCell, 0, 0, scaleTag.ToString());
                            if (prefab != null)
                            {
                                prefab.SetActive(true);
                                var scalePE = prefab.GetComponent<PrimaryElement>();
                                if (scalePE != null)
                                {
                                    scalePE.Mass = scaleWholeMass;
                                    scalePE.Temperature = temp;
                                }
                                dropsOut.Add(prefab);
                            }
                        }

                        massBank = Mathf.Max(0f, massBank - scaleWholeMass);
                    }
                }

                // 2) Milk: deposit liquid equal to current MilkAmount, using def.element, subtract from bank
                {
                    var milkSmi = go.GetSMI<MilkProductionMonitor.Instance>();
                    if (milkSmi != null)
                    {
                        float milkKg = Mathf.Max(0f, milkSmi.MilkAmount);
                        if (milkKg > 0f)
                        {
                            Element milkEl = ElementLoader.FindElementByHash(milkSmi.def.element);
                            if (milkEl != null && milkEl.IsLiquid)
                            {
                                SimMessages.AddRemoveSubstance(
                                    liquidCell,
                                    milkEl.id,
                                    CellEventLogger.Instance.ElementEmitted,
                                    milkKg,
                                    temp,
                                    byte.MaxValue,
                                    0,
                                    do_vertical_solid_displacement: true,
                                    callbackIdx: -1
                                );

                                massBank = Mathf.Max(0f, massBank - milkKg);
                            }
                        }
                    }
                }

                if (massBank <= 0f)
                {
                    __result = dropsOut.ToArray();
                    return;
                }

                // 3) Vanilla drops added by game so far (solids only) count against the bank
                float vanillaDropsMass = 0f;
                for (int i = 0; i < dropsOut.Count; i++)
                {
                    var goDrop = dropsOut[i];
                    if (goDrop == null) continue;

                    var dropPE = goDrop.GetComponent<PrimaryElement>();
                    if (dropPE != null) vanillaDropsMass += dropPE.Mass;
                }

                // 4) Remaining mass for extra drops
                float extra = massBank - vanillaDropsMass;
                if (extra <= 0f)
                {
                    __result = dropsOut.ToArray();
                    return;
                }

                // Resolve extra drops for this instance or species defaults
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

                    // Prefer element spawn (state-aware)
                    if (TrySpawnElementStateAware(spec.id, liquidCell, gasCell, solidPos, massToSpawn, temp, out var elGo))
                    {
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
                Debug.LogError($"[Rephysicalized] Extra mass drop extension (scale+milk) failed: {e}");
            }
        }
    }
}
