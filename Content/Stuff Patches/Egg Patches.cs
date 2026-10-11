using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Eggs
{
    [HarmonyPatch(typeof(IncubationMonitor), nameof(IncubationMonitor.InitializeStates))]
    public static class Patch_IncubationMonitor_Cryostasis_PreventDeltas
    {


        private static bool IsInFishMilkCell(IncubationMonitor.Instance smi)
        {
            if (smi == null || smi.gameObject == null) return false;
            int cell = Grid.PosToCell(smi.transform.GetPosition());
            if (!Grid.IsValidCell(cell)) return false;
            return Grid.Element[cell] != null && Grid.Element[cell].id == SimHashes.FishMilk;
        }

        private static readonly Dictionary<IncubationMonitor.Instance, Effect> fishMilkOvoleneEffects = new();

        private static Effect GetOrCreateFishMilkOvoleneEffect(IncubationMonitor.Instance smi)
        {
            if (smi == null) return null;

            if (fishMilkOvoleneEffects.TryGetValue(smi, out var eff))
                return eff;

            eff = new Effect(
                id: "FishMilkOvoleneIncubation",
                name: STRINGS.STATUSITEMS.FISHMILKEGG.NAME,
                description: STRINGS.STATUSITEMS.FISHMILKEGG.TOOLTIP,
                duration: 0f,
                show_in_ui: true,
                trigger_floating_text: false,
                is_bad: false
            );

            eff.Add(new AttributeModifier(Db.Get().Amounts.Incubation.deltaAttribute.Id, 1f, STRINGS.STATUSITEMS.FISHMILKEGG.NAME, is_multiplier: true));


            fishMilkOvoleneEffects[smi] = eff;
            return eff;
        }


        private static bool HasFishMilkOvolene(Effects effects)
        {
            return effects != null && effects.HasEffect("FishMilkOvoleneIncubation");
        }

        private const float CryoThresholdK = 173.15f;
      
        private static readonly Dictionary<IncubationMonitor.Instance, Effect> cryoEffects = new();



        // Build/get our lightweight UI effect
        private static Effect GetOrCreateCryoEffect(IncubationMonitor.Instance smi)
        {
            if (smi == null) return null;
            if (!cryoEffects.TryGetValue(smi, out var eff))
            {
                eff = new Effect(
                    id: "Cryostasis",
                    name: STRINGS.STATUSITEMS.CRYOEGG.NAME,
                    description: STRINGS.STATUSITEMS.CRYOEGG.TOOLTIP,
                    duration: 0f,
                    show_in_ui: true,
                    trigger_floating_text: false,
                    is_bad: false
                );
                cryoEffects[smi] = eff;
            }
            return eff;
        }


        private static bool IsCryo(IncubationMonitor.Instance smi)
        {
            var pe = smi?.GetComponent<PrimaryElement>();
            return pe != null && pe.Temperature < CryoThresholdK;
        }

        public static void Postfix(IncubationMonitor __instance, ref StateMachine.BaseState default_state)
        {
            var sm = __instance;

            // Overlay: every second, enforce “no incubation/viability deltas” while in cryostasis
            sm.root.Update((smi, dt) =>
            {
                bool cryo = IsCryo(smi);
                bool fishMilk = IsInFishMilkCell(smi);

                var effects = smi.GetComponent<Effects>();
                if (effects == null) return;

                if (fishMilk)
                {
                    if (!HasFishMilkOvolene(effects))
                    {
                        var eff = GetOrCreateFishMilkOvoleneEffect(smi);
                        if (eff != null)
                            effects.Add(eff, true);
                    }
                }
                else
                {
                    if (HasFishMilkOvolene(effects))
                        effects.Remove("FishMilkOvoleneIncubation");
                }


                // Ensure our UI effect presence
                var cryoEff = GetOrCreateCryoEffect(smi);
                if (cryo)
                {
                    // 1) Remove the incubatingEffect (added by incubating state)
                    if (smi.incubatingEffect != null && effects.HasEffect(smi.incubatingEffect.Id))
                        effects.Remove(smi.incubatingEffect.Id);

                    // 2) Remove the suppressedEffect (added by suppressed state)
                    if (effects.HasEffect("IncubationSuppressed"))
                        effects.Remove("IncubationSuppressed");

                    // Add our UI effect
                    if (cryoEff != null && !effects.HasEffect(cryoEff.Id))
                        effects.Add(cryoEff, true);
                }
                else
                {
                    if (cryoEff != null && effects.HasEffect(cryoEff.Id))
                        effects.Remove(cryoEff.Id);
                }
            }, UpdateRate.SIM_4000ms);
        }
    }


    [HarmonyPatch(typeof(IncubationMonitor), nameof(IncubationMonitor.InitializeStates))]
    public static class Patch_IncubationMonitor_RadiationOverlay
    {
        private const float ThresholdRadsPerSec = 60f; 
        private const float BaseDeltaAtThreshold = -0.002f;


        private static readonly HashSet<Tag> ExemptEggs = new HashSet<Tag>
        {
            (Tag) "MoleEgg", "MoleDelicacyEgg", "LightBugEgg" , "LightBugOrangeEgg", "LightBugPurpleEgg" , "LightBugPinkEgg" , "LightBugBlueEgg" , "LightBugCrystalEgg" , "LightBugBlackEgg"
        };

        // Per-instance store of our dynamic effect and its modifier
        private sealed class Rec
        {
            public Effect effect;
            public AttributeModifier viaDeltaMod;
        }

        private static readonly Dictionary<IncubationMonitor.Instance, Rec> perSmi = new Dictionary<IncubationMonitor.Instance, Rec>(64);

        private static bool IsExempt(IncubationMonitor.Instance smi)
        {
            var kpid = smi?.GetComponent<KPrefabID>();
            if (kpid == null) return false;
            foreach (var t in ExemptEggs)
            {
                if (kpid.HasTag(t)) return true;
            }
            return false;
        }

        private static float GetCellRadiation(IncubationMonitor.Instance smi)
        {
            if (smi == null) return 0f;
            int cell = Grid.PosToCell(smi.transform.GetPosition());
            if (!Grid.IsValidCell(cell)) return 0f;
            // Grid.Radiation is rads/s
            return Grid.Radiation[cell];
        }

        private static float ComputeViabilityDelta(float radsPerSec)
        {
            if (radsPerSec < ThresholdRadsPerSec) return 0f;
            float mult = radsPerSec / ThresholdRadsPerSec;
            return BaseDeltaAtThreshold * mult; // negative
        }

        private static Rec EnsureEffect(IncubationMonitor.Instance smi)
        {
            if (smi == null) return null;
            if (perSmi.TryGetValue(smi, out var rec)) return rec;

            var viaDeltaId = Db.Get().Amounts.Viability.deltaAttribute.Id;
            var mod = new AttributeModifier(viaDeltaId, 0f, "Radiation Exposure");

            var eff = new Effect(
                id: "RadiationExposure",
                name: "Radiation Exposure",
                description: "This Egg is exposed to radiation.",
                duration: 0f,
                show_in_ui: true,
                trigger_floating_text: false,
                is_bad: true
            );

            // Keep effect description dynamic by updating Effects tooltip via modifier value string, not by rebuilding Effect
            eff.Add(mod);

            rec = new Rec { effect = eff, viaDeltaMod = mod };
            perSmi[smi] = rec;
            return rec;
        }

        private static void ApplyOrUpdate(IncubationMonitor.Instance smi, float radsPerSec)
        {
            var rec = EnsureEffect(smi);
            if (rec == null) return;

            float delta = ComputeViabilityDelta(radsPerSec);
            if (delta >= 0f) { Remove(smi); return; }

            // Update modifier and effect description to show current loss speed
            rec.viaDeltaMod.SetValue(delta);

            var effects = smi.GetComponent<Effects>();
            if (effects == null) return;


            if (!effects.HasEffect(rec.effect.Id))
            {
                effects.Add(rec.effect, true);
            }
            else
            {
               
            }
        }

        private static void Remove(IncubationMonitor.Instance smi)
        {
            if (smi == null) return;
            var effects = smi.GetComponent<Effects>();
            if (effects != null && effects.HasEffect("RadiationExposure"))
            {
                effects.Remove("RadiationExposure");
            }
        }

        public static void Postfix(IncubationMonitor __instance, ref StateMachine.BaseState default_state)
        {
            var sm = __instance;

   
            sm.root.Update((smi, dt) =>
            {
                
                {
                    if (smi == null || smi.gameObject == null) return;
                    if (IsExempt(smi))
                    {
                        Remove(smi);
                        return;
                    }

                    float rads = GetCellRadiation(smi);
                    if (rads >= ThresholdRadsPerSec)
                        ApplyOrUpdate(smi, rads);
                    else
                        Remove(smi);

                    if (IncubationMonitor.NoLongerViable(smi))
                    {
                        // Ensure our radiation overlay effect is cleared so UI doesn't linger
                        Remove(smi);
                        smi.GoTo(smi.sm.not_viable);
                        return;
                    }
                }


        }, UpdateRate.SIM_4000ms);
        }
    }

}