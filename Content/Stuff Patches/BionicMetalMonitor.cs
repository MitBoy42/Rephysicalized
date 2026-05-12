using HarmonyLib;
using Klei.AI;
using Rephysicalized.Content.System_Patches;
using STRINGS;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Stuff_Patches
{
    // Custom ClosestMetalSensor mimicking ClosestLubricantSensor (assumed structure)
    public class ClosestMetalSensor : ClosestPickupableSensor<Pickupable>
    {
        public ClosestMetalSensor(Sensors sensors, bool shouldStartActive)
          : base(sensors, SimHashes.Iron.CreateTag(), shouldStartActive)
        {
        }
    }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Db_Initialize_BionicMetalChore_Patch
    {
        public static void Postfix()
        {
           
            var urges = Db.Get().Urges;
        
                BionicMetalMonitor.MetalRefill = urges.Add(new Urge("MetalRefill"));
            
            var choreTypes = Db.Get().ChoreTypes;
          
                BionicMetalMonitor.BionicMetalChange = choreTypes.Add("BionicMetalChange", new string[0], "MetalRefill", new string[0], STRINGS.DUPLICANTS.REFILLMETALCHORE.NAME, STRINGS.DUPLICANTS.REFILLMETALCHORE.STATUS_MESSAGE, STRINGS.DUPLICANTS.REFILLMETALCHORE.TOOLTIP, false);
BionicMetalMonitor.BionicMetalChange.priority = 7375;
BionicMetalMonitor.BionicMetalChange.explicitPriority = 7375;
BionicMetalMonitor.BionicMetalChange.interruptPriority = 97710;

            
            
        }
    }


    public class BionicMetalMonitor : GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>
    {
        
        public static Urge MetalRefill;
        public static ChoreType BionicMetalChange;

        public const float METAL_CAPACITY = 100f;
        public const float METAL_TANK_DURATION = 6000f;
        public const float METAL_REFILL_THRESHOLD = 0.2f;
        public const string NO_METAL_EFFECT_NAME_MINOR = "NoMetalMinor";
        public const string NO_METAL_EFFECT_NAME_MAJOR = "NoMetalMajor";

        public GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State offline;
        public BionicMetalMonitor.OnlineStates online;
        public StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Signal MetalFilledSignal;
        public StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Signal MetalRanOutSignal;
        public StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Signal MetalValueChanged;
        public StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Signal OnClosestSolidMetalChangedSignal;



        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
            this.serializable = StateMachine.SerializeType.ParamsOnly;
            default_state = this.offline;
            this.root.Update(new System.Action<BionicMetalMonitor.Instance, float>(MetalAmountInstanceWatcherUpdate));
                         
            this.offline.EventTransition(GameHashes.BionicOnline, this.online, new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Transition.ConditionCallback(IsBionicOnline));
            this.online.EventTransition(GameHashes.BionicOffline, this.offline, GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Not(new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Transition.ConditionCallback(IsBionicOnline)))
                           .DefaultState(this.online.idle)
                           .Enter(new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State.Callback(EnableSolidMetalSensor))
                           .Exit(new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State.Callback(DisableSolidMetalSensor));
            this.online.idle.EnterTransition(this.online.seeking, new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Transition.ConditionCallback(WantsMetalChange))
                            .OnSignal(this.MetalValueChanged, this.online.seeking, new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Parameter<StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.SignalParameter>.Callback(WantsMetalChange));
            this.online.seeking.OnSignal(this.MetalFilledSignal, this.online.idle)
                               .OnSignal(this.MetalValueChanged, this.online.idle, new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Parameter<StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.SignalParameter>.Callback(HasDecentAmountOfMetal))
                               .DefaultState(this.online.seeking.hasMetal)
.Enter((smi) => {
    bool wants = WantsMetalChange(smi);
    var closest = smi.GetClosestSolidMetal();
}).ToggleUrge(MetalRefill).ToggleChore((Func<BionicMetalMonitor.Instance, Chore>)(smi => new UseSolidMetalChore(smi.master)), this.online.idle);
            this.online.seeking.hasMetal.EnterTransition(this.online.seeking.noMetal, GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Not(new StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.Transition.ConditionCallback(HasAnyAmountOfMetal)))
                                         .OnSignal(this.MetalRanOutSignal, this.online.seeking.noMetal);
                 this.online.seeking.noMetal
                                        .EventTransition(GameHashes.AssignedRoleChanged, this.online.seeking.hasMetal);
        }

        public static bool IsBionicOnline(BionicMetalMonitor.Instance smi) {
            bool online = smi.IsOnline;
          
            return online;
        }
        public static bool HasAnyAmountOfMetal(BionicMetalMonitor.Instance smi) => smi.CurrentMetalMass > 0f;
        public static bool HasDecentAmountOfMetal(BionicMetalMonitor.Instance smi) => HasDecentAmountOfMetal(smi, default);
        public static bool HasDecentAmountOfMetal(BionicMetalMonitor.Instance smi, StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.SignalParameter param) {
            bool decent = smi.CurrentMetalPercentage > BionicMetalMonitor.METAL_REFILL_THRESHOLD;
            return decent;
        }
        public static bool WantsMetalChange(BionicMetalMonitor.Instance smi) => WantsMetalChange(smi, default);
        public static bool WantsMetalChange(BionicMetalMonitor.Instance smi, StateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.SignalParameter param) {
            bool wants = smi.CurrentMetalPercentage <= BionicMetalMonitor.METAL_REFILL_THRESHOLD;
            return wants;
        }


        public static void MetalAmountInstanceWatcherUpdate(BionicMetalMonitor.Instance smi, float dt)
        {
            float delta = smi.CurrentMetalMass - smi.LastMetalAmountMassRecorded;
            if (delta == 0f) return;
            smi.LastMetalAmountMassRecorded = smi.CurrentMetalMass;
            if (!smi.HasMetal) smi.ReportMetalRanOut();
            smi.ReportMetalValueChanged(delta);
        }

        public static void EnableSolidMetalSensor(BionicMetalMonitor.Instance smi) => smi.SetSolidMetalSensorActiveState(true);
        public static void DisableSolidMetalSensor(BionicMetalMonitor.Instance smi) => smi.SetSolidMetalSensorActiveState(false);





        public class Def : StateMachine.BaseDef { }

        public class WantsMetalChangeState : GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State
        {
            public GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State hasMetal;
            public GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State noMetal;
        }

        public class OnlineStates : GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State
        {
            public GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.State idle;
            public BionicMetalMonitor.WantsMetalChangeState seeking;
        }

        public new class Instance : GameStateMachine<BionicMetalMonitor, BionicMetalMonitor.Instance, IStateMachineTarget, BionicMetalMonitor.Def>.GameInstance
        {
            public float LastMetalAmountMassRecorded = -1f;
            public System.Action<float> OnMetalValueChanged;
            public MinionResume resume;
            public Effects effects;
            public HashedString currentNoMetalEffectApplied;
            public ClosestMetalSensor closestSolidMetalSensor;

            public bool IsOnline = true;
            // => this.batterySMI != null && this.batterySMI.IsOnline;

            public bool HasMetal => this.CurrentMetalMass > 0f;
            public float CurrentMetalPercentage => this.metalAmount.GetMax() > 0 ? this.CurrentMetalMass / this.metalAmount.GetMax() : 0f;
            public float CurrentMetalMass => this.metalAmount?.value ?? 0f;
            public AmountInstance metalAmount { private set; get; }
            public BionicBatteryMonitor.Instance batterySMI;

            public Instance(IStateMachineTarget master, BionicMetalMonitor.Def def) : base(master, def)
            {
                this.metalAmount = Db.Get().Amounts.Get("BionicMetalMeter").Lookup(this.gameObject);
                this.batterySMI = this.gameObject.GetSMI<BionicBatteryMonitor.Instance>(); }

            public override void StartSM()
            {
                var sensors = this.GetComponent<Sensors>();
                if (sensors != null)
                {
                    this.closestSolidMetalSensor = sensors.GetSensor<ClosestMetalSensor>();
                    if (this.closestSolidMetalSensor == null)
                    {
                        this.closestSolidMetalSensor = new ClosestMetalSensor(sensors, false);
                        sensors.Add(this.closestSolidMetalSensor);
                    }
                    this.closestSolidMetalSensor.OnItemChanged += this.OnClosestSolidMetalChanged;
                }
                else
                {
                }
                this.LastMetalAmountMassRecorded = this.CurrentMetalMass;
                base.StartSM();
            }

            public string GetEffect() => !this.resume.HasPerk(Db.Get().SkillPerks.EfficientBionicGears) ? BionicMetalMonitor.NO_METAL_EFFECT_NAME_MAJOR : BionicMetalMonitor.NO_METAL_EFFECT_NAME_MINOR;

            public void ReportMetalTankFilled() => this.sm.MetalFilledSignal.Trigger(this);
            public void ReportMetalRanOut() => this.sm.MetalRanOutSignal.Trigger(this);
            public void ReportMetalValueChanged(float delta)
            {
                this.sm.MetalValueChanged.Trigger(this);
                this.OnMetalValueChanged?.Invoke(delta);
            }

            public void SetMetalMassValue(float value) => this.metalAmount?.SetValue(value);
            public void RefillMetal(float amount)
            {
                this.metalAmount.SetValue(this.CurrentMetalMass + amount);
                this.ReportMetalTankFilled();
            }

            private void OnClosestSolidMetalChanged(Pickupable newItem)
            {
                this.sm.OnClosestSolidMetalChangedSignal.Trigger(this);
            }
            public Pickupable GetClosestSolidMetal() => this.closestSolidMetalSensor?.GetItem();

            public void SetSolidMetalSensorActiveState(bool active)
            {
                this.closestSolidMetalSensor?.SetActive(active);
                if (active)
                    this.closestSolidMetalSensor?.Update();
            }


        }


    }



    public class UseSolidMetalChore : Chore<UseSolidMetalChore.Instance>
    {
        public static readonly Chore.Precondition SolidMetalIsNotNull = new Chore.Precondition
        {
            id = "SolidMetalIsNotNull",
            description =  "Metal to refill is nearby",
fn = (Chore.PreconditionFn)((ref Chore.Precondition.Context context, object data) => {
    var smi = context.consumerState.consumer.GetSMI<BionicMetalMonitor.Instance>();
    var closest = smi?.GetClosestSolidMetal();
    bool pass = closest != null;
    return pass;
})
        };

public UseSolidMetalChore(IStateMachineTarget target) : base(BionicMetalMonitor.BionicMetalChange, target, target.GetComponent<ChoreProvider>(), false, master_priority_class: PriorityScreen.PriorityClass.personalNeeds)
        {
            this.smi = new UseSolidMetalChore.Instance(this, target.gameObject);
            this.AddPrecondition(ChorePreconditions.instance.IsNotRedAlert, null);
            this.AddPrecondition(UseSolidMetalChore.SolidMetalIsNotNull, null);
        }

        public override void End(string reason)
        {
            base.End(reason);
        }

        public override void Begin(Chore.Precondition.Context context)
        {
            BionicMetalMonitor.Instance smi = context.consumerState.consumer.GetSMI<BionicMetalMonitor.Instance>();
            Pickupable closestSolidMetal = smi.GetClosestSolidMetal();
            this.smi.sm.solidMetalSource.Set(closestSolidMetal.gameObject, this.smi, false);
            this.smi.sm.dupe.Set(context.consumerState.consumer, this.smi);
            base.Begin(context);
        }

        public static void ConsumeMetal(UseSolidMetalChore.Instance smi)
        {
            PrimaryElement pe = smi.sm.pickedUpSolidMetal.Get(smi).GetComponent<PrimaryElement>();
            float amount = Mathf.Min(pe.Mass, BionicMetalMonitor.METAL_CAPACITY - smi.metalMonitor.metalAmount.value);
            smi.metalMonitor.RefillMetal(amount);
            if (amount >= pe.Mass)
            {
                Util.KDestroyGameObject(pe.gameObject);
                smi.sm.pickedUpSolidMetal.Set(null, smi);
            }
            else
            {
                pe.Mass -= amount;
            }
        }

        public static void SetOverrideAnimSymbol(UseSolidMetalChore.Instance smi, bool overriding)
        {
            GameObject go = smi.sm.pickedUpSolidMetal.Get(smi);
            if ((UnityEngine.Object)go != (UnityEngine.Object)null)
            {
                KBatchedAnimTracker component = go.GetComponent<KBatchedAnimTracker>();
                if ((UnityEngine.Object)component != (UnityEngine.Object)null)
                    component.enabled = !overriding;
                Storage.MakeItemInvisible(go, overriding, false);
            }
            if (!overriding)
            {
                smi.RemoveSymbolOverrideObject();
            }
            else
            {
                if (!((UnityEngine.Object)go != (UnityEngine.Object)null))
                    return;
                PrimaryElement component = go.GetComponent<PrimaryElement>();
                smi.ShowMetalSymbolOverrideObject(component.Element);
            }
        }



        public class States : GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>
        {
            public StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.FloatParameter amountRequested = new StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.FloatParameter(BionicMetalMonitor.METAL_CAPACITY);
            public StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.FloatParameter actualunits;
            public GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.FetchSubState fetch;
            public UseSolidMetalChore.States.InstallState consume;
            public GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State complete;
            public StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.TargetParameter dupe;
            public StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.TargetParameter solidMetalSource;
            public StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.TargetParameter pickedUpSolidMetal;

            public override void InitializeStates(out StateMachine.BaseState default_state)
            {
                default_state = this.fetch;
                this.Target(this.dupe);
                this.fetch.InitializeStates(this.dupe, this.solidMetalSource, this.pickedUpSolidMetal, this.amountRequested, this.actualunits, (GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State)this.consume);
                                this.consume.DefaultState(this.consume.pre).ToggleAnims("anim_bionic_kanim").Enter("Add Symbol Override", (StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State.Callback)(smi => UseSolidMetalChore.SetOverrideAnimSymbol(smi, true))).Exit("Revert Symbol Override", (StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State.Callback)(smi => UseSolidMetalChore.SetOverrideAnimSymbol(smi, false)));
                this.consume.pre.PlayAnim("consume_pre", KAnim.PlayMode.Once).OnAnimQueueComplete(this.consume.loop);
                this.consume.loop.PlayAnim("consume_loop", KAnim.PlayMode.Loop).ScheduleGoTo(3f, this.consume.pst);
                this.consume.pst.PlayAnim("consume_pst", KAnim.PlayMode.Once).OnAnimQueueComplete(this.complete);
                this.complete.Enter(new StateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State.Callback(UseSolidMetalChore.ConsumeMetal)).ReturnSuccess();
            }

            public class InstallState : GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State
            {
                public GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State pre;
                public GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State loop;
                public GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.State pst;
            }
        }
         
        public class Instance : GameStateMachine<UseSolidMetalChore.States, UseSolidMetalChore.Instance, UseSolidMetalChore, object>.GameInstance
        {
            public KBatchedAnimController metalChunkSymbolOverrideObject;
            public BionicMetalMonitor.Instance metalMonitor => this.sm.dupe.Get(this).GetSMI<BionicMetalMonitor.Instance>();

            public Instance(UseSolidMetalChore master, GameObject dupe_go) : base(master) { }

            public void ShowMetalSymbolOverrideObject(Element elementOfChunk)
            {
                if ((UnityEngine.Object)this.metalChunkSymbolOverrideObject == (UnityEngine.Object)null)
                {
                    KAnimFile[] anims = elementOfChunk.substance.anims;
                    GameObject gameObject = Util.NewGameObject(this.gameObject, "metal_chunk_symbol");
                    gameObject.transform.SetParent(this.gameObject.transform, false);
                    gameObject.SetActive(false);
                   
                    this.metalChunkSymbolOverrideObject = gameObject.AddComponent<KBatchedAnimController>();
                    this.metalChunkSymbolOverrideObject.AnimFiles = anims;
                    this.metalChunkSymbolOverrideObject.initialAnim = "idle1";
                    KBatchedAnimTracker kbatchedAnimTracker = gameObject.AddComponent<KBatchedAnimTracker>();
                    kbatchedAnimTracker.symbol = new HashedString("object");
                    kbatchedAnimTracker.offset = new Vector3(0f, -1f, 0f);
                    kbatchedAnimTracker.matchParentOffset = false;
                    kbatchedAnimTracker.forceAlwaysAlive = true;
                    kbatchedAnimTracker.forceAlwaysVisible = true;
                    gameObject.SetActive(true);
                }
                KBatchedAnimController component = this.GetComponent<KBatchedAnimController>();
                Vector3 column = (Vector3)component.GetSymbolTransform((HashedString)"object", out bool _).GetColumn(3) with
                {
                    z = this.metalChunkSymbolOverrideObject.transform.parent.position.z - 0.01f,
                    
                };
                this.metalChunkSymbolOverrideObject.transform.position = column;
                component.SetSymbolVisiblity((KAnimHashedString)"object", false);
            }

            public void RemoveSymbolOverrideObject()
            {
                if ((UnityEngine.Object)this.metalChunkSymbolOverrideObject != (UnityEngine.Object)null)
                {
                    this.metalChunkSymbolOverrideObject.gameObject.DeleteObject();
                    this.metalChunkSymbolOverrideObject = (KBatchedAnimController)null;
                }
                KBatchedAnimController component = this.GetComponent<KBatchedAnimController>();
                if ((UnityEngine.Object)component != (UnityEngine.Object)null)
                {
                    component.SetSymbolVisiblity((KAnimHashedString)"canister", true);
                    component.SetSymbolVisiblity((KAnimHashedString)"cap", true);
                }
            }

            public override void OnCleanUp()
            {
                this.RemoveSymbolOverrideObject();
                base.OnCleanUp();
            }
        }
    }

// Patch to integrate BionicMetalMonitor into BionicMinionConfig
[HarmonyPatch(typeof(BionicMinionConfig), "OnPrefabInit")]
    public static class AddBionicMetalMonitorSMI
    {
        public static void Postfix(GameObject go)
        {
            if (go == null) return;
            if (!Config.Instance.BionicMetalMeter)
                return;
            go.AddOrGetDef<BionicMetalMonitor.Def>();
        }
    }

    // Patch to add ClosestMetalSensor to bionic prefab
    [HarmonyPatch(typeof(BionicMinionConfig), "OnPrefabInit")]
    public static class AddClosestMetalSensor
    {
        public static void Postfix(GameObject go)
        {
            if (!Config.Instance.BionicMetalMeter)
                return;
            var sensors = go.GetComponent<Sensors>();
           
                sensors.Add(new ClosestMetalSensor(sensors, false));
          
            
            
        }
    }

}

