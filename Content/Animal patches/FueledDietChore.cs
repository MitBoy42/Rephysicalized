using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized.Chores
{

    public sealed partial class SolidFuelStates : GameStateMachine<SolidFuelStates, SolidFuelStates.Instance, IStateMachineTarget, SolidFuelStates.Def>
    {
        public static readonly Tag WantsToSolidFuel = TagManager.Create("WantsToSolidFuel");
        public const string ChoreId = "SOLIDFUEL";

        public sealed class Def : BaseDef
        {
            public string preAnim = "graze_pre";
            public string loopAnim = "graze_loop";
            public string pstAnim = "graze_pst";

            public Vector3 workAnimOffset = new Vector3(0f, 0f, 0f);
  
            public override string ToString() => ChoreId;
        }

        public new sealed class Instance : GameInstance
        {
            public int standCell = Grid.InvalidCell;
            public KBatchedAnimController kbac;
            public Navigator navigator;
            public float originalSpeed;

            // Current MoveTo target cell we are pathing towards
            public int activeMoveCell = Grid.InvalidCell;
            // Track progress to detect stuck/no movement
            public int lastPosCell = Grid.InvalidCell;
            public float stuckTimer = 0f;
            // Track reachability failures
            public float unreachableTimer = 0f;
            // Avoid thrash after failure (local only)
            public float retryCooldown = 0f;

            public Instance(Chore<Instance> chore, Def def) : base(chore, def)
            {
                chore.AddPrecondition(ChorePreconditions.instance.CheckBehaviourPrecondition, WantsToSolidFuel);
                chore.AddPrecondition(HasFuelTarget, null);

                gameObject.TryGetComponent(out kbac);
                gameObject.TryGetComponent(out navigator);
                originalSpeed = navigator != null ? navigator.defaultSpeed : 1f;
            }

            public GameObject TargetGO
            {
                get => sm.target.Get(this);
                set => sm.target.Set(value, this);
            }
        }

        internal static void PrioritizeUpdateBrain(StateMachine.Instance smi)
        {
            if (smi == null) return; GameObject go; try { go = smi.gameObject; } catch { return; }  PrioritizeUpdateBrain(go); }

        internal static void PrioritizeUpdateBrain(GameObject go)
        {
            if (go == null) return;
            if (go.TryGetComponent<CreatureBrain>(out var brain))
                Game.BrainScheduler?.PrioritizeBrain(brain);
        }

        public sealed class MovingStates : State { public State moving; }
        public sealed class GulpingStates : State
        {
            public State pre;
            public State loop;
            public State pst;
            public State pst_interrupt;
        }

        private MovingStates moving;
        private GulpingStates gulping;
        private State abort; // concrete abort
        private TargetParameter target;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = moving;

            // Abort state ends the SM cleanly and fully cancels seeking/eating
            abort
                .Enter(smi =>
                {
                    AbortFuelSeeking(smi, "abort: target moved/unreachable");
                    // StopSM is safe if the chore wasn't already cancelled
                    smi.StopSM("abort");
                });

            root
                .Enter(smi =>
                {
                    var mon = smi.gameObject.GetSMI<SolidFuelMonitor.Instance>();
                    if (smi.TargetGO == null && mon != null && mon.targetGO != null)
                        smi.TargetGO = mon.targetGO;

                    if (smi.TargetGO == null)
                    {
                        smi.GoTo(abort);
                        return;
                    }

                    int targetCell = Grid.PosToCell(smi.TargetGO);
                    smi.standCell = Grid.IsValidCell(targetCell)
                        ? targetCell
                        : Grid.PosToCell(smi.transform.position);
                }

                )
               ;

            moving
                .DefaultState(moving.moving)
                .ToggleStatusItem(
                    name: STRINGS.STATUSITEMS.FUEL_SEEKING.NAME,
                    tooltip: STRINGS.STATUSITEMS.FUEL_SEEKING.TOOLTIP,
                    category: Db.Get().StatusItemCategories.Main)
                // If target object is destroyed/unassigned, bail to abort
                .OnTargetLost(target, abort);

            moving.moving
                .Enter("SpeedupOrSkip", smi =>
                {
                    if (!IsRefillNeeded(smi)) { smi.GoTo(abort); return; }

                    if (smi.TargetGO == null)
                    {
                        var mon = smi.gameObject.GetSMI<SolidFuelMonitor.Instance>();
                        if (mon != null && mon.targetGO != null)
                            smi.TargetGO = mon.targetGO;
                        if (smi.TargetGO == null) { smi.GoTo(abort); return; }
                    }

                    // Bind the current target cell at entry
                    int tc = Grid.PosToCell(smi.TargetGO);
                    if (!Grid.IsValidCell(tc)) { smi.GoTo(abort); return; }

                    smi.standCell = tc;
                    smi.activeMoveCell = smi.standCell;

                    // Initialize movement tracking
                    smi.lastPosCell = Grid.PosToCell(smi.gameObject);
                    smi.stuckTimer = 0f;
                    smi.unreachableTimer = 0f;

                    if (Grid.PosToCell(smi.gameObject) == smi.standCell)
                    {
                        smi.GoTo(gulping);
                        return;
                    }
                    if (smi.navigator != null)
                        smi.navigator.defaultSpeed = smi.originalSpeed ;
                })
                // Success -> gulping, Failure -> abort (stop trying)
                .MoveTo(smi => smi.standCell, gulping, abort, false)
                .Update((smi, dt) =>
                {
                    if (!IsRefillNeeded(smi)) { smi.GoTo(abort); return; }
                    if (smi.TargetGO == null) { smi.GoTo(abort); return; }

                    // If target moved, try to re-path if the new cell is reachable; else abort
                    int tcNow = Grid.PosToCell(smi.TargetGO);
                    if (!Grid.IsValidCell(tcNow))
                    {
                        smi.GoTo(abort);
                        return;
                    }

                    if (tcNow != smi.activeMoveCell)
                    {
                        // Try to re-path smoothly if reachable; otherwise abort and let brain reacquire
                        if (smi.navigator != null && CanReachCellSafe(smi.navigator, tcNow))
                        {
                            smi.standCell = tcNow;
                            smi.GoTo(moving.moving); // re-enter to issue a fresh MoveTo to the new cell
                            return;
                        }
                        else
                        {
                            smi.GoTo(abort);
                            return;
                        }
                    }

                    // If we arrived, go run
                    if (Grid.PosToCell(smi.gameObject) == smi.standCell)
                    {
                        smi.GoTo(gulping);
                        return;
                    }

                    // Reachability guard
                    if (smi.navigator != null)
                    {
                        bool canReach = CanReachCellSafe(smi.navigator, smi.standCell);
                        if (!canReach)
                        {
                            smi.unreachableTimer += dt;
                            if (smi.unreachableTimer > 0.75f)
                            {
                                smi.GoTo(abort);
                                return;
                            }
                        }
                        else
                        {
                            smi.unreachableTimer = 0f;
                        }
                    }

                    // Stuck/no progress: after a few seconds, abort (prevents loop)
                    int curCell = Grid.PosToCell(smi.gameObject);
                    if (curCell == smi.lastPosCell)
                    {
                        smi.stuckTimer += dt;
                        if (smi.stuckTimer > 3.0f)
                        {
                            smi.GoTo(abort);
                            return;
                        }
                    }
                    else
                    {
                        smi.lastPosCell = curCell;
                        smi.stuckTimer = 0f;
                    }
                }, UpdateRate.SIM_200ms)
                .Exit("RestoreSpeed", smi =>
                {
                    if (smi.navigator != null)
                        smi.navigator.defaultSpeed = smi.originalSpeed;
                    smi.activeMoveCell = Grid.InvalidCell;
                    smi.stuckTimer = 0f;
                    smi.unreachableTimer = 0f;
                });

            gulping
                .DefaultState(gulping.pre)
                .OnTargetLost(target, gulping.pst)
                .ToggleTag(GameTags.PerformingWorkRequest)
                .ToggleStatusItem(
                    name: STRINGS.STATUSITEMS.FUEL_EATING.NAME,
                    tooltip: STRINGS.STATUSITEMS.FUEL_EATING.TOOLTIP,
                    category: Db.Get().StatusItemCategories.Main)
                .EventTransition(GameHashes.ChoreInterrupt, gulping.pst_interrupt);

            gulping.pre
                .Enter(smi =>
                {
                    if (smi.TargetGO != null)
                        smi.GetComponent<Facing>()?.Face(smi.TargetGO.transform.position.x);

                    string pre = null;
                    if (smi.kbac != null)
                    {
                        smi.kbac.Offset += smi.def.workAnimOffset;
                      

                        pre = ResolveAnim(smi.kbac, AnimPhase.Pre, smi.def.preAnim);
                        if (!string.IsNullOrEmpty(pre))
                            smi.kbac.Play(pre, KAnim.PlayMode.Once);
                    }

                    if (string.IsNullOrEmpty(pre))
                        smi.GoTo(gulping.loop);
                })
                .OnAnimQueueComplete(gulping.loop);

            gulping.loop
                .Enter(smi =>
                {
                    if (smi.kbac != null)
                    {
                        var loop = ResolveAnim(smi.kbac, AnimPhase.Loop, smi.def.loopAnim);
                        if (!string.IsNullOrEmpty(loop))
                            smi.kbac.Queue(loop, KAnim.PlayMode.Loop);
                    }
                })
                .Update((smi, dt) =>
                {
                    // Validate we still need to refill
                    if (!IsRefillNeeded(smi)) { smi.GoTo(gulping.pst); return; }

                    // Target must exist and still be at the current cell
                    if (smi.TargetGO == null)
                    {
                        smi.GoTo(gulping.pst);
                        return;
                    }

                    int tcNow = Grid.PosToCell(smi.TargetGO);
                    if (!Grid.IsValidCell(tcNow))
                    {
                        smi.GoTo(gulping.pst);
                        return;
                    }

                    // If target moved during eating, only continue if still at-or-adjacent; else exit to pst then abort
                    if (tcNow != smi.standCell)
                    {
                        // If the new cell is reachable and adjacent, we can exit to pst (clean exit) and then restart via brain
                        smi.GoTo(gulping.pst);
                        return;
                    }

                    // Must be at or adjacent to target cell to consume
                    if (!IsAtOrAdjacent(Grid.PosToCell(smi.gameObject), smi.standCell))
                    {
                        smi.GoTo(gulping.pst);
                        return;
                    }

                    var result = TryConsumeOnce(smi);
                    if (result != ConsumeResult.Pending)
                        smi.GoTo(gulping.pst);
                }, UpdateRate.SIM_200ms)
                .Exit(smi =>
                {
                    if (smi.kbac != null)
                    {
                        smi.kbac.Offset -= smi.def.workAnimOffset;
                        smi.kbac.PlaySpeedMultiplier = 1f;
                    }
                });

            // Exit pst immediately if we have no pst animation
            gulping.pst
                .Enter(smi =>
                {
                    string pst = null;
                    if (smi.kbac != null)
                    {
                        pst = ResolveAnim(smi.kbac, AnimPhase.Pst, smi.def.pstAnim);
                        if (!string.IsNullOrEmpty(pst))
                            smi.kbac.Play(pst, KAnim.PlayMode.Once);
                    }

                    if (string.IsNullOrEmpty(pst))
                    {
                        smi.GoTo(abort);
                        return;
                    }
                })
                .OnAnimQueueComplete(abort);

            // Immediate-exit behavior for the interrupt pst
            gulping.pst_interrupt
                .ToggleTag(GameTags.PreventChoreInterruption)
                .Enter(smi =>
                {
                    string pst = null;
                    if (smi.kbac != null)
                    {
                        pst = ResolveAnim(smi.kbac, AnimPhase.Pst, smi.def.pstAnim);
                        if (!string.IsNullOrEmpty(pst))
                            smi.kbac.Play(pst, KAnim.PlayMode.Once);
                    }

                    if (string.IsNullOrEmpty(pst))
                    {
                        smi.GoTo(abort);
                        return;
                    }
                })
                .OnAnimQueueComplete(abort);
        }

        public enum AnimPhase { Pre, Loop, Pst }

        private static readonly string[] BaseCandidates = new[]
        {
            "eat","inhale","graze","consume","gulp","chew","swallow","suck","devour","feed"
        };

        public static string ResolveAnim(KBatchedAnimController kbac, AnimPhase phase, string preferredBase = null)
        {
            if (kbac == null) return null;

            var bases = (preferredBase != null
                ? new[] { preferredBase }.Concat(BaseCandidates)
                : BaseCandidates).Distinct();

            foreach (var b in bases)
            {
                string name = phase switch
                {
                    AnimPhase.Pre => $"{b}_pre",
                    AnimPhase.Loop => $"{b}_loop",
                    _ => $"{b}_pst"
                };

                if (!string.IsNullOrEmpty(name) && kbac.HasAnimation(name))
                    return name;
            }

            if (phase == AnimPhase.Loop)
            {
                if (kbac.HasAnimation("idle_loop"))
                    return "idle_loop";
                return null;
            }

            return null;
        }

        private static bool IsRefillNeeded(Instance smi)
        {
            if (smi == null) return false;
            var go = smi.gameObject;

            var controller = go.GetComponent<FueledDietController>();
            var storage = controller != null ? controller.FuelStorage : null;
            if (storage == null)
                return true;

            float cap = controller != null && controller.FuelStorageCapacityKg > 0f ? controller.FuelStorageCapacityKg : storage.capacityKg;
            if (cap <= 0f) return false;

            float used = GetStorageUsedKg(storage);
            float remain = Mathf.Max(0f, cap - used);

            float threshold = GetRefillThresholdKg(go, storage);
            if (threshold > 0f)
                return used < threshold;

            return remain > 0.01f;

        }

        private static float GetStorageUsedKg(Storage storage)
        {
            float used = 0f;
            if (storage != null && storage.items != null)
            {
                for (int i = 0; i < storage.items.Count; i++)
                {
                    var go = storage.items[i];
                    var pe = go != null ? go.GetComponent<PrimaryElement>() : null;
                    if (pe != null) used += pe.Mass;
                }
            }
            return used;
        }

        private static float GetRefillThresholdKg(GameObject go, Storage storage)
        {
            if (go == null) return 0f;
            var controller = go.GetComponent<FueledDietController>();
            if (controller == null) return 0f;

            float threshold = controller.RefillThreshold;
            if (float.IsNaN(threshold) || threshold <= 0f)
                return 0f;

            return threshold;
        }

        // Navigator has CanReach(int) in many builds; guard it
        private static bool CanReachCellSafe(Navigator nav, int cell)
        {
            try
            {
                if (nav == null || !Grid.IsValidCell(cell)) return false;
                return nav.CanReach(cell);
            }
            catch
            {
                return false;
            }
        }

        // Range check for consumption: at or orthogonally adjacent to the target cell
        private static bool IsAtOrAdjacent(int myCell, int targetCell)
        {
            if (!Grid.IsValidCell(myCell) || !Grid.IsValidCell(targetCell)) return false;
            if (myCell == targetCell) return true;
            if (myCell == Grid.OffsetCell(targetCell, 1, 0)) return true;
            if (myCell == Grid.OffsetCell(targetCell, -1, 0)) return true;
            if (myCell == Grid.OffsetCell(targetCell, 0, 1)) return true;
            if (myCell == Grid.OffsetCell(targetCell, 0, -1)) return true;
            return false;
        }

        internal enum ConsumeResult { Pending, Success, Done }

        // Hard gate: target must exist and refilling is meaningful now
        private static readonly Chore.Precondition HasFuelTarget = new Chore.Precondition
        {
            id = "HasFuelTarget",
            description = "Has a valid fuel target and capacity/need",
            fn = (ref Chore.Precondition.Context ctx, object data) =>
            {
                const float MinTakeKg = 0.01f;
                var go = ctx.consumerState?.gameObject;
                if (go == null) return false;

                var mon = go.GetSMI<SolidFuelMonitor.Instance>();
                if (mon == null || mon.targetGO == null)
                    return false;

                var controller = go.GetComponent<FueledDietController>();
                var storage = controller != null ? controller.FuelStorage : null;
                if (storage == null)
                    return true;

                float cap = controller != null && controller.FuelStorageCapacityKg > 0f ? controller.FuelStorageCapacityKg : storage.capacityKg;
                if (cap <= 0f) return false;

                float used = GetStorageUsedKg(storage);
                float remain = Mathf.Max(0f, cap - used);

                float threshold = GetRefillThresholdKg(go, storage);
                if (threshold > 0f)
                    return (used < threshold) && (remain > MinTakeKg);

                return remain > MinTakeKg;
            }
        };

        // Schedules an immediate and delayed brain nudge to force chore/monitor re-evaluation
        private static void NudgeBrainSoon(GameObject go, float delay = 0.2f)
        {
          
                    GameScheduler.Instance.Schedule("FueledDiet.Recheck", delay, _ => PrioritizeUpdateBrain(go));
              
        }

        // Fully abort seeking/eating: clear monitor target, end current chore via ChoreDriver, and nudge brain
        private static void AbortFuelSeeking(SolidFuelStates.Instance smi, string reason)
        {
            if (smi == null) return;

        
                // Clear local target (for SM transitions)
                smi.TargetGO = null;

                // Clear the monitor's target so the brain can reacquire later
                var mon = smi.gameObject != null ? smi.gameObject.GetSMI<SolidFuelMonitor.Instance>() : null;
                if (mon != null)
                    mon.targetGO = null;
         

         
            
                // Gracefully stop the running chore via ChoreDriver
                var driver = smi.gameObject != null ? smi.gameObject.GetComponent<ChoreDriver>() : null;
                if (driver != null && driver.HasChore())
                    driver.StopChore(); // no-arg in your build; EndChore("ChoreDriver.SignalStop") will run
            
       
                PrioritizeUpdateBrain(smi);
         
                    GameScheduler.Instance.Schedule("FueledDiet.Recheck", 0.25f, _ => PrioritizeUpdateBrain(smi));
          
        }
    }
}

