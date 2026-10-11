using System;
using System.Collections.Generic;
using System.Text;
using Klei.AI;
using STRINGS;
using UnityEngine;
using HarmonyLib;

namespace Rephysicalized.Content.System_Patches
{
    internal class IrradiatedCritters : GameStateMachine<IrradiatedCritters, IrradiatedCritters.Instance, IStateMachineTarget, IrradiatedCritters.Def>
    {
        internal static readonly HashSet<Tag> ExcludedCritterIds = new HashSet<Tag>
        {
            (Tag)"LightBug",
            (Tag)"LightBugBaby",
            (Tag)"LightBugBlack",
            (Tag)"LightBugBlackBaby",
            (Tag)"LightBugBlue",
            (Tag)"LightBugBlueBaby",
            (Tag)"LightBugCrystal",
            (Tag)"LightBugCrystalBaby",
            (Tag)"LightBugOrange",
            (Tag)"LightBugOrangeBaby",
            (Tag)"LightBugPink",
            (Tag)"LightBugPinkBaby",
            (Tag)"LightBugPurple",
            (Tag)"LightBugPurpleBaby",
            (Tag)"Moo",
            (Tag)"DieselMoo",
            (Tag)"Mole",
            (Tag)"MoleBaby",
            (Tag)"MoleDelicacy",
            (Tag)"MoleDelicacyBaby",
            (Tag)"Bee",
            (Tag)"BeeBaby",
    (Tag)"Glom"
        };

        public GameStateMachine<IrradiatedCritters, IrradiatedCritters.Instance, IStateMachineTarget, IrradiatedCritters.Def>.State notIrradiated;
        public GameStateMachine<IrradiatedCritters, IrradiatedCritters.Instance, IStateMachineTarget, IrradiatedCritters.Def>.State mildIrradiation;
        public GameStateMachine<IrradiatedCritters, IrradiatedCritters.Instance, IStateMachineTarget, IrradiatedCritters.Def>.State highIrradiation;
        public Effect mildEffect;
        public Effect highEffect;

        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
            default_state = (StateMachine.BaseState)this.notIrradiated;

            this.mildEffect = new Effect("EffectCritterIrradiationmild", (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.NAME, (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.TOOLTIP, 0.0f, true, false, true);
            this.mildEffect.Add(new AttributeModifier(Db.Get().CritterAttributes.Happiness.Id, -1f, STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.NAME));
            this.mildEffect.Add(new AttributeModifier(Db.Get().Amounts.Age.deltaAttribute.Id, Config.Instance.MildAging / 600f, STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.NAME));
            this.mildEffect.Add(new AttributeModifier(Db.Get().CritterAttributes.Metabolism.Id, Config.Instance.MildMetabolsim, STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.NAME, is_multiplier: true));
            this.mildEffect.Add(new AttributeModifier(Db.Get().Amounts.ScaleGrowth.deltaAttribute.Id, 0.5f, STRINGS.CREATURES.STATUSITEMS.MILDIRRADIATION.NAME, is_multiplier: true));

            this.highEffect = new Effect("EffectCritterIrradiationHigh", (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.NAME, (string)Rephysicalized.STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.TOOLTIP, 0.0f, true, false, true);
            this.highEffect.Add(new AttributeModifier(Db.Get().CritterAttributes.Happiness.Id, -2f, STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.NAME));
            this.highEffect.Add(new AttributeModifier(Db.Get().Amounts.Age.deltaAttribute.Id, Config.Instance.HighAging / 600f, STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.NAME));
            this.highEffect.Add(new AttributeModifier(Db.Get().CritterAttributes.Metabolism.Id, Config.Instance.HighMetabolism, STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.NAME, is_multiplier: true));
            this.highEffect.Add(new AttributeModifier(Db.Get().Amounts.ScaleGrowth.deltaAttribute.Id, 1f, STRINGS.CREATURES.STATUSITEMS.HIGHIRRADIATION.NAME, is_multiplier: true));

            this.notIrradiated.DoNothing();
            this.root.Update((System.Action<IrradiatedCritters.Instance, float>)((smi, dt) =>
            {
                StateMachine.BaseState targetState = smi.GetTargetState();
                if (smi.GetCurrentState() == targetState)
                    return;
                smi.GoTo(targetState);
            })).Update(new System.Action<IrradiatedCritters.Instance, float>(IrradiatedCritters.UpdateIrradiation), UpdateRate.SIM_4000ms);

            this.mildIrradiation.ToggleEffect((Func<IrradiatedCritters.Instance, Effect>) (smi => this.mildEffect));
            this.highIrradiation.ToggleEffect((Func<IrradiatedCritters.Instance, Effect>) (smi => this.highEffect));
        }

        public static void UpdateIrradiation(IrradiatedCritters.Instance smi, float dt)
        {
            if (smi.OnUpdate_GetIrradiation == null)
                return;
            smi.OnUpdate_GetIrradiation(dt, smi.GetIrradiation());
        }

        public class Def : StateMachine.BaseDef
        {
            public float radiationmild = Config.Instance.MildThreshold;
            public float radiationHigh = Config.Instance.HighThreshold;
        }

        public new class Instance : GameStateMachine<IrradiatedCritters, IrradiatedCritters.Instance, IStateMachineTarget, IrradiatedCritters.Def>.GameInstance
        {
            public Pickupable pickupable;
            public System.Action<float, float> OnUpdate_GetIrradiation;

            public Instance(IStateMachineTarget master, IrradiatedCritters.Def def)
                : base(master, def)
            {
                this.pickupable = master.GetComponent<Pickupable>();
            }

            public StateMachine.BaseState GetTargetState()
            {
                float irradiation = this.GetIrradiation();
                if (irradiation <= this.def.radiationmild)
                {
                    return (StateMachine.BaseState)this.sm.notIrradiated;
                }

                return irradiation <= this.def.radiationHigh
                    ? (StateMachine.BaseState)this.sm.mildIrradiation
                    : (StateMachine.BaseState)this.sm.highIrradiation;
            }

            public float GetIrradiation()
            {
                if (this.pickupable == null)
                {
                    this.pickupable = this.gameObject.GetComponent<Pickupable>();
                }

                if (this.pickupable == null)
                {
                    return 0f;
                }

                int cachedCell = this.pickupable.cachedCell;
                if (Grid.IsValidCell(cachedCell))
                {
                    return Grid.Radiation[cachedCell];
                }
                return 0f;
            }
        }
    }

    // Create a new component to hold the IrradiatedCritters state machine
    public class IrradiationMonitor : KMonoBehaviour, IStateMachineTarget
    {
        private IrradiatedCritters.Instance smi;
        private KBatchedAnimController animController;
        private Color32 originalTint;

        public override void OnSpawn()
        {
            base.OnSpawn();
            KPrefabID prefabID = GetComponent<KPrefabID>();
            if (!Config.Instance.IrradiatedCritters)
                return;
            if (prefabID != null && IrradiatedCritters.ExcludedCritterIds.Contains(prefabID.PrefabTag))
            {
                return;
            }
            animController = GetComponent<KBatchedAnimController>();
            originalTint = animController.TintColour;
            smi = new IrradiatedCritters.Instance(this, new IrradiatedCritters.Def());
            smi.OnUpdate_GetIrradiation = (dt, irradiation) => UpdateTint(irradiation, smi.def.radiationmild);
            smi.StartSM();
        }

        public override void OnCleanUp()
        {
            if (smi != null)
            {
                smi.StopSM("cleanup");
            }
            base.OnCleanUp();
        }

        public float GetEggChancePower()
        {
            if (smi == null)
            {
                return 1f;
            }

            float irradiation = smi.GetIrradiation();
            if (irradiation <= smi.def.radiationmild)
            {
                return 1f;
            }

            return irradiation <= smi.def.radiationHigh ? 0.75f : 0.5f;
        }

        private void UpdateTint(float irradiation, float mildThreshold)
        {
            float greenAmount = irradiation <= 0f
                ? 0f
                : irradiation <= mildThreshold ? 0.25f : 0.5f;

            Color tint = originalTint;
            float nonGreenMultiplier = 1f - greenAmount;
            tint.r *= nonGreenMultiplier;
            tint.b *= nonGreenMultiplier;
            animController.TintColour = tint;
        }

        // Implement IStateMachineTarget interface
        public string GetProperName()
        {
            return gameObject.GetProperName();
        }

        public Vector3 TransformPoint(Vector3 position)
        {
            return gameObject.transform.TransformPoint(position);
        }

        public GameObject gameObject => base.gameObject;

        public Transform transform => base.transform;
    }

    [HarmonyPatch]
    internal class IrradiatedCrittersPatch
    {
        [HarmonyPatch(typeof(CreatureBrain), "OnPrefabInit")]
        [HarmonyPostfix]
        private static void AddMonitorToCreature(CreatureBrain __instance)
        {
            if (__instance == null)
            {
                return;
            }

            KPrefabID prefabID = __instance.GetComponent<KPrefabID>();
            if (prefabID == null || IrradiatedCritters.ExcludedCritterIds.Contains(prefabID.PrefabTag))
            {
                return;
            }

            __instance.gameObject.AddOrGet<IrradiationMonitor>();
        }

        [HarmonyPatch(typeof(FertilityMonitor.Instance), nameof(FertilityMonitor.Instance.LayEgg))]
        [HarmonyPrefix]
        private static void TransformBreedingChances(
            FertilityMonitor.Instance __instance,
            ref List<float> __state)
        {
            IrradiationMonitor irradiationMonitor = __instance.gameObject.GetComponent<IrradiationMonitor>();
            float power = irradiationMonitor == null ? 1f : irradiationMonitor.GetEggChancePower();
            if (power >= 1f || __instance.breedingChances == null || __instance.breedingChances.Count == 0)
            {
                return;
            }

            float total = 0f;
            for (int i = 0; i < __instance.breedingChances.Count; i++)
            {
                total += Mathf.Pow(Mathf.Max(0f, __instance.breedingChances[i].weight), power);
            }

            if (total <= 0f)
            {
                return;
            }

            __state = new List<float>(__instance.breedingChances.Count);
            for (int i = 0; i < __instance.breedingChances.Count; i++)
            {
                FertilityMonitor.BreedingChance breedingChance = __instance.breedingChances[i];
                __state.Add(breedingChance.weight);
                breedingChance.weight = Mathf.Pow(Mathf.Max(0f, breedingChance.weight), power) / total;
            }
        }

        [HarmonyPatch(typeof(FertilityMonitor.Instance), nameof(FertilityMonitor.Instance.LayEgg))]
        [HarmonyPostfix]
        private static void RestoreBreedingChances(
            FertilityMonitor.Instance __instance,
            List<float> __state)
        {
            if (__state == null || __state.Count != __instance.breedingChances.Count)
            {
                return;
            }

            for (int i = 0; i < __state.Count; i++)
            {
                __instance.breedingChances[i].weight = __state[i];
            }
        }
    }
}