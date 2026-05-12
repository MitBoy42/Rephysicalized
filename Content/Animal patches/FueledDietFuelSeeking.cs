using Klei.AI;
using System;
using System.Linq;
using UnityEngine;

namespace Rephysicalized.Chores
{
    public sealed class SolidFuelMonitor : GameStateMachine<SolidFuelMonitor, SolidFuelMonitor.Instance, IStateMachineTarget, SolidFuelMonitor.Def>
    {
        private State looking;

        public sealed class Def : BaseDef
        {
            public int scanWindowCells = 16;
            public float minPickupKg = 0.01f;
            public Vector3[] possibleEatPositionOffsets = new[] { Vector3.zero };
            public Vector2 navigatorSize = Vector2.one;
        }

        public sealed class Instance : GameInstance
        {
            public GameObject targetGO;
            public Vector3 targetOffset;
            public int targetCost = -1;

            public readonly Navigator navigator;
            public readonly DrowningMonitor drowning;
            public readonly KPrefabID kpid;

            public Instance(IStateMachineTarget master, Def def) : base(master, def)
            {
                gameObject.TryGetComponent(out navigator);
                gameObject.TryGetComponent(out drowning);
                gameObject.TryGetComponent(out kpid);
            }

            // Keep an assignment unless it truly becomes invalid (destroyed, stored, or too small).
            public bool TargetStillValid()
            {
                if (targetGO == null) return false;

                var pickup = targetGO.GetComponent<Pickupable>();
                if (pickup == null) return false;
                if (pickup.storage != null)
                {
                    // CreatureFeeder exception
                    if (pickup.storage.GetComponent<CreatureFeeder>() == null)
                        return false;
                }

                var pe = targetGO.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= def.minPickupKg) return false;

                // Do not re-evaluate path cost every tick; let MoveTo handle reachability.
                return true;
            }

            public void ClearAssignment()
            {
                // Do not manage reservations here; just clear assignment
                targetGO = null;
                targetOffset = Vector3.zero;
                targetCost = -1;

                var fuelSm = gameObject.GetSMI<SolidFuelStates.Instance>();
                if (fuelSm != null)
                {
                    fuelSm.TargetGO = null;
                    fuelSm.standCell = Grid.InvalidCell;
                }
            }

            public void Assign(GameObject edible, int cost, Vector3 offset)
            {
                // Do not reserve here; reservation is chore responsibility (if used at all)
                targetGO = edible;
                targetCost = cost;
                targetOffset = offset;

                var fuelSm = gameObject.GetSMI<SolidFuelStates.Instance>();
                if (fuelSm != null)
                {
                    fuelSm.TargetGO = edible;
                    fuelSm.standCell = ComputeStandCell(edible, offset);
                }
            }

            public int GetCost(int cell)
            {
                if (drowning != null && drowning.canDrownToDeath && !drowning.livesUnderWater && !drowning.IsCellSafe(cell))
                    return -1;
                return navigator != null ? navigator.GetNavigationCost(cell) : -1;
            }

            public Vector3 GetBestOffset(GameObject edible)
            {
                int best = int.MaxValue;
                Vector3 bestOff = Vector3.zero;
                foreach (var off in def.possibleEatPositionOffsets)
                {
                    int cell = ComputeStandCell(edible, off);
                    int cost = GetCost(cell);
                    if (cost != -1 && cost < best)
                    {
                        best = cost;
                        bestOff = off;
                    }
                }
                return bestOff;
            }

            public int ComputeStandCell(GameObject edible, Vector3 eatOffset)
            {
                Vector3 pos = edible.transform.position + eatOffset;
                if (eatOffset.x > 0f) pos += new Vector3(def.navigatorSize.x / 2f, 0f, 0f);
                else if (eatOffset.x < 0f) pos -= new Vector3(def.navigatorSize.x / 2f, 0f, 0f);
                if (eatOffset.y > 0f) pos += new Vector3(0f, def.navigatorSize.y / 2f, 0f);
                else if (eatOffset.y < 0f) pos -= new Vector3(0f, def.navigatorSize.y / 2f, 0f);
                return Grid.PosToCell(pos);
            }

            public bool AllowedByFuelOrDiet(Pickupable pickup)
            {
                if (pickup == null)
                    return false;

                if (!pickup.TryGetComponent<PrimaryElement>(out var pe) || pe.Mass <= def.minPickupKg)
                    return false;

                var controller = gameObject.GetComponent<FueledDietController>();
                var diet = controller != null ? controller.fueledDiet : null;
                if (diet?.FuelInputs == null || diet.FuelInputs.Count == 0)
                    return false;

                Tag elementTag = FueledDietUtils.ResolveElementTag(pe);
                Tag prefabTag = Tag.Invalid;
                if (pickup.TryGetComponent<KPrefabID>(out var pkpid))
                    prefabTag = pkpid.PrefabTag;

                bool allowed = diet.FuelInputs.Any(fi =>
                    fi != null && (fi.ElementTag == prefabTag || fi.ElementTag == elementTag));

                if (allowed)
                    return true;

              
                return false;
            }
        }

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = looking;

            // Toggle behaviour when we have an assignment; avoids thrash on transient path checks.
            root.ToggleBehaviour(SolidFuelStates.WantsToSolidFuel, smi => smi.targetGO != null);

            looking.PreBrainUpdate(FindFuel);
            
        }


        private static void FindFuel(Instance smi)
        {
            if (smi.targetGO != null)
            {
                if (smi.TargetStillValid())
                    return;
                smi.ClearAssignment();
            }

            Grid.PosToXY(smi.gameObject.transform.position, out int cx, out int cy);
            int half = smi.def.scanWindowCells / 2;
            int lx = cx - half;
            int ly = cy - half;

// 2. CreatureFeeder exception (like vanilla SolidConsumerMonitor)
            int feederScanSize = 32;
            int fx = cx - feederScanSize / 2;
            int fy = cy - feederScanSize / 2;

            foreach (CreatureFeeder feeder in Components.CreatureFeeders.GetItems(smi.GetMyWorldId()))
            {
                Vector2I targetFeederXY = feeder.GetTargetFeederCell();
                if (targetFeederXY.x < fx || targetFeederXY.x > fx + feederScanSize ||
                    targetFeederXY.y < fy || targetFeederXY.y > fy + feederScanSize)
                    continue;

                if (feeder.StoragesAreEmpty()) continue;

                int feederCell = Grid.XYToCell(targetFeederXY.x, targetFeederXY.y);
                int feederCost = smi.GetCost(feederCell);
                if (feederCost == -1) continue;

                if (smi.targetGO != null && feederCost >= smi.targetCost) continue;

                bool foundItem = false;
                foreach (Storage storage in feeder.storages)
                {
                    if (storage == null || storage.IsEmpty()) continue;
                    foreach (GameObject itemGO in storage.items)
                    {
                        if (itemGO == null) continue;
                        Pickupable pu = itemGO.GetComponent<Pickupable>();
                        if (pu != null && smi.AllowedByFuelOrDiet(pu))
                        {
                            smi.Assign(itemGO, feederCost, Vector3.zero);
                            foundItem = true;
                            break;
                        }
                    }
                    if (foundItem) break;
                }
                if (foundItem) break;
            }

            // 3. Ground pickupables (existing scan)
            var hits = ListPool<ScenePartitionerEntry, GameScenePartitioner>.Allocate();
            GameScenePartitioner.Instance.GatherEntries(
                lx, ly, smi.def.scanWindowCells, smi.def.scanWindowCells,
                GameScenePartitioner.Instance.pickupablesLayer,
                hits
            );

            GameObject best = null;
            int bestCost = -1;
            Vector3 bestOff = Vector3.zero;

            for (int i = 0; i < hits.Count; i++)
            {
                var entry = hits[i];
                if (!(entry.obj is Pickupable pickup)) continue;
                if (pickup.storage != null) continue;

                // Do not use tag-based reservation here
                if (!smi.AllowedByFuelOrDiet(pickup)) continue;

                Vector3 off = smi.GetBestOffset(pickup.gameObject);
                int cell = smi.ComputeStandCell(pickup.gameObject, off);
                int cost = smi.GetCost(cell);
                if (cost == -1) continue;

                if (best == null || cost < bestCost)
                {
                    best = pickup.gameObject;
                    bestCost = cost;
                    bestOff = off;
                    if (bestCost < 3) break;
                }
            }

            hits.Recycle();

            if (best != null)
                smi.Assign(best, bestCost, bestOff);
        }
    }

}