using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches.RealBreathing
{
    public sealed class AnimalSuffocation : KMonoBehaviour, ISim1000ms
    {
        private const float SuffocationDurationSeconds = 75f;
        internal static readonly Tag SuffocatingTag = TagManager.Create("RephysicalizedAnimalSuffocating");
        private static StatusItem suffocatingStatus;

        [Serialize] private float timeUntilDeath = SuffocationDurationSeconds;

        private KPrefabID prefabID;
        private AnimalBreathing breathing;

        public override void OnSpawn()
        {
            base.OnSpawn();
            prefabID = GetComponent<KPrefabID>();
            breathing = GetComponent<AnimalBreathing>();
        }

        public void Sim1000ms(float dt)
        {
            if (!Config.Instance.RealBreathing)
                return;
            if (dt <= 0f || prefabID == null || breathing == null || prefabID.HasTag(GameTags.Dead))
                return;

            if (prefabID.HasTag(GameTags.Creatures.Wild))
            {
                if (prefabID.HasTag(SuffocatingTag))
                    prefabID.RemoveTag(SuffocatingTag);
                timeUntilDeath = SuffocationDurationSeconds;
                return;
            }

            bool suffocating = breathing.IsOutOfOxygen;
            if (suffocating)
            {
                if (!prefabID.HasTag(SuffocatingTag))
                    prefabID.AddTag(SuffocatingTag);
                timeUntilDeath -= dt;
                if (timeUntilDeath <= 0f)
                    gameObject.GetSMI<DeathMonitor.Instance>()?.Kill(Db.Get().Deaths.Suffocation);
            }
            else
            {
                if (prefabID.HasTag(SuffocatingTag))
                    prefabID.RemoveTag(SuffocatingTag);
                timeUntilDeath = SuffocationDurationSeconds;
            }
        }

        internal static StatusItem GetSuffocatingStatus()
        {
            if (suffocatingStatus == null)
            {
                suffocatingStatus = new StatusItem(
                    "RephysicalizedAnimalSuffocating",
                    (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.ANIMALSUFFOCATING.NAME,
                    (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.ANIMALSUFFOCATING.TOOLTIP,
                    "status_item_suit_locker_no_oxygen",
                    StatusItem.IconType.Custom,
                    NotificationType.BadMinor,
                    false,
                    OverlayModes.None.ID);
            }

            return suffocatingStatus;
        }
    }

    public sealed class AnimalSuffocatingStates : GameStateMachine<AnimalSuffocatingStates, AnimalSuffocatingStates.Instance, IStateMachineTarget, AnimalSuffocatingStates.Def>
    {
        public GameStateMachine<AnimalSuffocatingStates, Instance, IStateMachineTarget, Def>.State suffocating;
        public GameStateMachine<AnimalSuffocatingStates, Instance, IStateMachineTarget, Def>.State moveToOxidizer;

        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
            default_state = suffocating;
            root.ToggleStatusItem(AnimalSuffocation.GetSuffocatingStatus())
                .TagTransition(AnimalSuffocation.SuffocatingTag, null, true)
                .ToggleCritterEmotion(Db.Get().CritterEmotions.Suffocating);

            suffocating.PlayAnim("drown_pre").QueueAnim("drown_loop", true)
                .Update(UpdateEscapeTarget, UpdateRate.SIM_1000ms, true);
            moveToOxidizer.MoveTo(FindOxidizerCell, suffocating, suffocating);
        }

        private static void UpdateEscapeTarget(Instance smi, float dt)
        {
            if (!smi.HasTag(AnimalSuffocation.SuffocatingTag))
                return;

            var query = new OxidizerCellQuery();
            var navigator = smi.GetComponent<Navigator>();
            if (navigator == null)
                return;

            navigator.RunQuery((PathFinderQuery)query);
            int targetCell = query.GetResultCell();
            if (Grid.IsValidCell(targetCell))
            {
                smi.targetCell = targetCell;
                smi.GoTo((StateMachine.BaseState)smi.sm.moveToOxidizer);
            }
            else
            {
                smi.targetCell = Grid.InvalidCell;
            }
        }

        private static int FindOxidizerCell(Instance smi) => smi.targetCell;

        public class Def : StateMachine.BaseDef { }

        public new class Instance : GameStateMachine<AnimalSuffocatingStates, Instance, IStateMachineTarget, Def>.GameInstance
        {
            public int targetCell = Grid.InvalidCell;

            public Instance(Chore<Instance> chore, Def def) : base(chore, def)
            {
                chore.AddPrecondition(ChorePreconditions.instance.HasTag, AnimalSuffocation.SuffocatingTag);
            }
        }

        private sealed class OxidizerCellQuery : PathFinderQuery
        {
            public override bool IsMatch(int cell, int parent_cell, int cost)
            {
                if (!Grid.IsValidCell(cell))
                    return false;

                return HasOxidizer(cell) || HasOxidizer(Grid.CellAbove(cell));
            }

            private static bool HasOxidizer(int cell)
            {
                if (!Grid.IsValidCell(cell))
                    return false;

                Element element = Grid.Element[cell];
                return element != null && element.IsGas && element.HasTag(ModTags.OxidizerGas) && Grid.Mass[cell] > 0f;
            }
        }
    }

    [HarmonyPatch(typeof(BaseSquirrelConfig), nameof(BaseSquirrelConfig.BaseSquirrel))]
    internal static class BaseSquirrelConfig_AnimalSuffocatingStates_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var drowningConstructor = AccessTools.Constructor(typeof(DrowningStates.Def));
            var suffocatingConstructor = AccessTools.Constructor(typeof(AnimalSuffocatingStates.Def));
            var addMethod = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.Add), new[] { typeof(StateMachine.BaseDef), typeof(bool), typeof(int) });

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                bool addsDrowningState = false;
                if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, addMethod))
                {
                    for (int j = Math.Max(0, i - 4); j < i; j++)
                    {
                        if (codes[j].opcode == OpCodes.Newobj && Equals(codes[j].operand, drowningConstructor))
                        {
                            addsDrowningState = true;
                            break;
                        }
                    }
                }

                if (addsDrowningState)
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Newobj, suffocatingConstructor));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4_1));
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldc_I4_M1));
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Callvirt, addMethod));
                    i += 4;
                }
            }

            return codes;
        }
    }
}
