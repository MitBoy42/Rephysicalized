using HarmonyLib;
using Klei.AI;
using STRINGS;
using System;
using System.Text;
using TUNING;
using UnityEngine;
using System.Linq;


namespace Rephysicalized.Content.System_Patches.Helper_Components
{
    public class BionicMetalDisplayer : AsPercentAmountDisplayer
    {
        public BionicMetalDisplayer(GameUtil.TimeSlice deltaTimeSlice) : base(deltaTimeSlice) { }

        public override string GetTooltip(Amount master, AmountInstance instance)
        {
            StringBuilder sb = GlobalStringBuilderPool.Alloc();
            sb.AppendFormat(master.description, formatter.GetFormattedValue(instance.value));
            sb.Append("\n\n");
            float totalDisplayValue = instance.deltaAttribute.GetTotalDisplayValue();
           
            for (int i = 0; i < instance.deltaAttribute.Modifiers.Count; i++)
            {
                AttributeModifier modifier = instance.deltaAttribute.Modifiers[i];
                float modifierContribution = instance.deltaAttribute.GetModifierContribution(modifier);
                sb.Append("\n");
                sb.AppendFormat(UI.MODIFIER_ITEM_TEMPLATE, modifier.GetDescription(), formatter.GetFormattedValue(ToPercent(modifierContribution, instance), formatter.DeltaTimeSlice));
            }
            return GlobalStringBuilderPool.ReturnAndFree(sb);
    }
  }



    [HarmonyPatch(typeof(Database.Amounts), "Load")]
    public static class AddBionicMetalMeterAmount
    {

        public static void Postfix(Database.Amounts __instance)
        {

      if (!Config.Instance.BionicMetalMeter)
            return;
        
                Amount metalMeter = __instance.CreateAmount("BionicMetalMeter", 0f, 100f, true, Units.Flat, 0.5f, true, "", ModAssets.MetalCapacityIcon.name);
                metalMeter.Name = STRINGS.DUPLICANTS.STATS.BIONICMETALMETER.NAME;
                metalMeter.description = STRINGS.DUPLICANTS.STATS.BIONICMETALMETER.TOOLTIP;
                metalMeter.SetDisplayer(new BionicMetalDisplayer(GameUtil.TimeSlice.PerCycle));
            
            
        }
    }

    [HarmonyPatch(typeof(BionicMinionConfig), "GetAmounts")]
    public static class PatchGetAmounts
    {
        public static void Postfix(ref string[] __result)
        {
            if (!Config.Instance.BionicMetalMeter)
                return;
            var list = __result.ToList();
            list.Add("BionicMetalMeter");
            __result = list.ToArray();
        }
    }

    [HarmonyPatch(typeof(MinionVitalsPanel), "Init")]
    public static class PatchMinionVitalsPanelInit
    {
        public static void Postfix(MinionVitalsPanel __instance)
        {
                if (!Config.Instance.BionicMetalMeter)
            return;
            Amount metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
            if (metalAmount != null)
            {
                __instance.AddAmountLine(metalAmount);
            }
        
        }
    }

    [HarmonyPatch(typeof(BionicMinionConfig), "OnPrefabInit")]
    public static class InitBionicMetalMeter
    {
        public static void Postfix(GameObject go)
        {
            if (!Config.Instance.BionicMetalMeter)
                return;
            Amount metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
            if (metalAmount != null)
            {
                AmountInstance metalInst = metalAmount.Lookup(go);
                if (metalInst != null)
                {
                    metalInst.value = 100f;
                }
            }

            var smc = go.AddOrGet<StateMachineController>();
            var lowMetalSmi = go.GetSMI<BionicLowMetalStatusSM.Instance>();
            if (lowMetalSmi == null)
            {
                lowMetalSmi = new BionicLowMetalStatusSM.Instance(smc, new BionicLowMetalStatusSM.Def());
                lowMetalSmi.StartSM();
            }

        }
    }

    // Power Tinker Tools (microchip) metal consumption and block
    [HarmonyPatch(typeof(BionicMicrochipMonitor.Instance), "CreateMicrochip")]
    public static class MicrochipMetalConsumption
    {
        public static bool Prefix(BionicMicrochipMonitor.Instance __instance)
        {
            if (!Config.Instance.BionicMetalMeter)
                return true;
            var minionGO = __instance.master.gameObject;
            var metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
            if (metalAmount == null) return true;
            var metalInst = metalAmount.Lookup(minionGO);
            if (metalInst == null) return true;
            if (metalInst.value <5f)
            {

                return false; // Block
            }
            return true;
        }

        public static void Postfix(BionicMicrochipMonitor.Instance __instance)
        {
            if (!Config.Instance.BionicMetalMeter)
                return;
            var minionGO = __instance.master.gameObject;
            var metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
            if (metalAmount == null) return;
            var metalInst = metalAmount.Lookup(minionGO);
            if (metalInst == null) return;
            metalInst.ApplyDelta(-5f);
        }


        // Fixes: Start at 100%, skip progress bar/update when <5% metal, add "Low Metal" status item
        [HarmonyPatch(typeof(BionicMicrochipMonitor.Instance), "CreateProgressBar")]
        public static class SkipProgressBarLowMetal
        {
            public static bool Prefix(BionicMicrochipMonitor.Instance __instance)
            {
                if (!Config.Instance.BionicMetalMeter)
                    return true;
                var metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
                if (metalAmount == null) return true;
                var metalInst = metalAmount.Lookup(__instance.master.gameObject);
                if (metalInst.value < 5f) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(BionicMicrochipMonitor), "ProgressUpdate")]
        public static class SkipProgressUpdateLowMetal
        {
            public static bool Prefix(BionicMicrochipMonitor.Instance smi, float dt)
            {
                if (!Config.Instance.BionicMetalMeter)
                    return true;
                var metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
                if (metalAmount == null) return true;
                var metalInst = metalAmount.Lookup(smi.master.gameObject);
                if (metalInst.value < 5f) return false;
                return true;
            }
        }
    }

    // Low bionic metal status monitor (Beehive style)
    public sealed class BionicLowMetalStatusSM : GameStateMachine<BionicLowMetalStatusSM, BionicLowMetalStatusSM.Instance, IStateMachineTarget>
    {
        public State normal;
        public State lowMetal;

        public new class Instance : GameStateMachine<BionicLowMetalStatusSM, BionicLowMetalStatusSM.Instance, IStateMachineTarget>.GameInstance
        {
            public Instance(IStateMachineTarget master, BionicLowMetalStatusSM.Def def) : base(master, def) { }
        }

        public class Def : StateMachine.BaseDef { }

        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
       
            default_state = normal;
            if (!Config.Instance.BionicMetalMeter)
                return;
            normal
                .Update(delegate (Instance smi, float dt)
                {
                    
                    Amount metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
                    if (metalAmount != null)
                    {
                        AmountInstance metalInst = metalAmount.Lookup(smi.master.gameObject);
                        if (metalInst != null && metalInst.value < 20f)
                            smi.GoTo(lowMetal);
                    }
                }, UpdateRate.SIM_1000ms);

            lowMetal
                .ToggleStatusItem(STRINGS.DUPLICANTS.BIONICLOWMETAL.NAME, STRINGS.DUPLICANTS.BIONICLOWMETAL.DESC, "", StatusItem.IconType.Exclamation, NotificationType.BadMinor, false, OverlayModes.None.ID)
                .Update(delegate (Instance smi, float dt)
                {
                    Amount metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
                    if (metalAmount != null)
                    {
                        AmountInstance metalInst = metalAmount.Lookup(smi.master.gameObject);
                        if (metalInst != null && metalInst.value >= 20f)
                            smi.GoTo(normal);
                    }
                }, UpdateRate.SIM_1000ms); 
        }
    }

  }


