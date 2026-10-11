﻿using HarmonyLib;
using JetBrains.Annotations;
using Klei.AI;
using STRINGS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using KAttribute = Klei.AI.Attribute;


namespace Rephysicalized
{

    // Patch tuning + derived constants as early as possible.
    // BionicOxygenTankMonitor.OXYGEN_TANK_CAPACITY_KG is a static readonly computed once from DUPLICANTSTATS.
    // If that type initializes before our Db.Initialize patch, the capacity won’t reflect DuplicantOxygenUse.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Rephys_DbInitialize_Patch
    {
        // Run early among other Db.Initialize postfix/prefix patches.
        [HarmonyPriority(Priority.High)]
        [HarmonyPrefix]
        private static void Prefix()
        {
            ApplyTo(global::TUNING.DUPLICANTSTATS.STANDARD);
            ApplyTo(global::TUNING.DUPLICANTSTATS.BIONICS);


            ForceSetBionicTankCapacityKg();
        }

        private static void ApplyTo(global::TUNING.DUPLICANTSTATS stats)
        {
            if (stats == null) return;

            var sec = stats.Secretions;
            if (sec != null)
            {
                sec.PEE_PER_FLOOR_PEE = 5f;
                sec.PEE_PER_TOILET_PEE = 5f;
            }

            var baseStats = stats.BaseStats;
            if (baseStats != null)
            {
                baseStats.OXYGEN_USED_PER_SECOND = baseStats.OXYGEN_USED_PER_SECOND * Config.Instance.DuplicantOxygenUse;
                baseStats.OXYGEN_TO_CO2_CONVERSION = baseStats.OXYGEN_USED_PER_SECOND / Config.Instance.DuplicantOxygenUse / 2.5f;
                baseStats.MAX_CALORIES = 6_000_000f;
            }
        }

        private static void ForceSetBionicTankCapacityKg()
        {
         
                var capField = typeof(BionicOxygenTankMonitor).GetField(
                    "OXYGEN_TANK_CAPACITY_KG",
                    BindingFlags.Public | BindingFlags.Static);

                if (capField == null) return;

                float newCap = 2400f * TUNING.DUPLICANTSTATS.BIONICS.BaseStats.OXYGEN_USED_PER_SECOND;

                capField.SetValue(null, newCap);
      
        }
    }




    [SkipSaveFileSerialization]
    public sealed class HealingConsumptionScaler : KMonoBehaviour, ISim1000ms
    {
        private const float SecondsPerCycle = 600f;
        private const string BionicWattageModId = "rephys_healing_wattage";
        private const string LogPrefix = "[Rephys-Healing]";

        // Enable/disable debug logs here
        private static bool DebugEnabled = false;

        // Manual (lazy) binding to avoid [MyCmpReq] issues in some builds
        private Health health;
        private Attributes attributes;

        // Calories
        private KAttribute caloriesDeltaAttribute; // resolved from Amounts.Calories.deltaAttribute
        private AttributeInstance caloriesDeltaInst; // attribute instance on this dupe
        private AttributeModifier caloriesDeltaMod; // our additive modifier (we only change Value)

        private float lastHP;

        // Tuning
        [SerializeField] public float caloriesPercentPer100HpPerCycle = 100f; // +100% at 100 HP/cycle
        [SerializeField] public float maxCalorieMultiplier = 5f; // hard cap (5x baseline)

        // Debug throttling
        private float _lastHealLogTime;
        private float _lastBindLogTime;
        private float _lastBionicLogTime;

        private MinionModifiers minionMods;

        public override void OnSpawn()
        {
            base.OnSpawn();

            TryBindCore();
            TryBindCaloriesDelta();

            lastHP = Mathf.Max(0f, health != null ? health.hitPoints : 0f);
        }

        public override void OnCleanUp()
        {
           
                if (attributes != null && caloriesDeltaMod != null)
                {
                    attributes.Remove(caloriesDeltaMod);
                }

                // Clean bionic modifier if present
                var smi = this.GetSMI<BionicBatteryMonitor.Instance>();
                if (smi != null)
                {
                    smi.RemoveModifier(BionicWattageModId, true);
                }
           

            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            if (dt <= 0f) return;

            if (!TryBindCore())
            {
                if (DebugEnabled && Time.unscaledTime - _lastBindLogTime > 5f)
                {
                    _lastBindLogTime = Time.unscaledTime;
                }
                return;
            }

            if ((caloriesDeltaInst == null) || (caloriesDeltaMod == null))
            {
                if (!TryBindCaloriesDelta())
                {
                    if (DebugEnabled && Time.unscaledTime - _lastBindLogTime > 5f)
                    {
                        _lastBindLogTime = Time.unscaledTime;
                    }
                    return;
                }
            }

            float currentHP = health.hitPoints;
            float dHP = Mathf.Max(0f, currentHP - lastHP);
            lastHP = currentHP;

            if (dHP <= 0.0001f)
            {
                // Not healing: reset modifiers and return
                SetModifierValue(caloriesDeltaMod, 0f);

                var smiOff = this.GetSMI<BionicBatteryMonitor.Instance>();
                if (smiOff != null)
                    smiOff.RemoveModifier(BionicWattageModId, true);

                if (DebugEnabled && Time.unscaledTime - _lastHealLogTime > 5f)
                {
                    _lastHealLogTime = Time.unscaledTime;
                }
                return;
            }

            // HP regen rate
            float regenPerSec = dHP / dt;
            float regenPerCycle = regenPerSec * SecondsPerCycle;

            // Drain bionic metal meter if healing (1f metal = 1 hp) and has enough metal
            if (Config.Instance.BionicMetalMeter)
            {
                Amount metalAmount = Db.Get().Amounts.Get("BionicMetalMeter");
                if (metalAmount != null)
                {
                    AmountInstance metalInst = metalAmount.Lookup(gameObject);
                    if (metalInst != null && metalInst.value >= 1f)
                    {
                        metalInst.ApplyDelta(-dHP * 0.2f);
                    }
                }
            }

            float calMult = 1f + (regenPerCycle / 100f) * (caloriesPercentPer100HpPerCycle / 100f);

            if (maxCalorieMultiplier > 0f) calMult = Mathf.Min(calMult, maxCalorieMultiplier);
            // Apply calories scaling (baseline-preserving additive)
            if (caloriesDeltaInst != null && caloriesDeltaMod != null)
            {
                float currentWithOur = caloriesDeltaInst.GetTotalValue(); // includes our mod
                float ourCurrent = GetModifierValue(caloriesDeltaMod);
                float baseline = currentWithOur - ourCurrent; // isolate baseline (usually negative)
                float extra = baseline * (calMult - 1f); // extra negative -> more consumption
                SetModifierValue(caloriesDeltaMod, extra);

                if (DebugEnabled && Time.unscaledTime - _lastHealLogTime > 1.0f)
                {
                    _lastHealLogTime = Time.unscaledTime;
                }
            }
        }

        private static float SumOtherModifiers(List<BionicBatteryMonitor.WattageModifier> list, string excludeId)
        {
            if (list == null || list.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m.id != excludeId)
                    sum += m.value;
            }
            return sum;
        }

        // ----------- Binding helpers -----------

        private bool TryBindCore()
        {
            try
            {
                if (health == null)
                    health = GetComponent<Health>();

                // Primary path: Attributes live on MinionModifiers
                if (minionMods == null)
                    minionMods = GetComponent<MinionModifiers>();

                if (minionMods != null && attributes == null)
                    attributes = minionMods.attributes; // may still be null very early

                // Fallback: in case Attributes actually is a component in this build
                if (attributes == null)
                    attributes = GetComponent<Attributes>();

                return health != null && attributes != null;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private bool TryBindCaloriesDelta()
        {
            if (attributes == null) return false;

            try
            {
                bool changed = false;

                if (caloriesDeltaAttribute == null)
                {
                    var calAmount = Db.Get().Amounts.Calories;
                    if (calAmount != null)
                    {
                        var tAmt = Traverse.Create(calAmount);
                        caloriesDeltaAttribute =
                            tAmt.Field("deltaAttribute").GetValue<KAttribute>() ??
                            tAmt.Property("deltaAttribute")?.GetValue<KAttribute>();
                    }

                    if (caloriesDeltaAttribute == null)
                    {
                        caloriesDeltaAttribute =
                            Db.Get().Attributes.TryGet("CaloriesDelta") ??
                            Db.Get().Attributes.TryGet("CalorieDelta") ??
                            Db.Get().Attributes.TryGet("CaloriesBurnRate");
                    }

                    changed = true;
                }

                if (caloriesDeltaAttribute != null && caloriesDeltaInst == null)
                {
                    caloriesDeltaInst = attributes.Get(caloriesDeltaAttribute);
                    changed = true;
                }

                if (caloriesDeltaInst != null && caloriesDeltaMod == null)
                {
                    var displayName = STRINGS.DUPLICANTS.HEALINGMETABOLISM.NAME;
                    caloriesDeltaMod = new AttributeModifier(caloriesDeltaInst.Attribute.Id, 0f, displayName);
                    attributes.Add(caloriesDeltaMod);
                    changed = true;
                }

                return caloriesDeltaInst != null && caloriesDeltaMod != null;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private static void SetModifierValue(AttributeModifier mod, float value)
        {
            if (mod == null) return;
            var t = Traverse.Create(mod);
            try { t.Property("Value")?.SetValue(value); return; } catch { }
            try { t.Field("Value")?.SetValue(value); } catch { }
        }

        private static float GetModifierValue(AttributeModifier mod)
        {
            if (mod == null) return 0f;
            var t = Traverse.Create(mod);
            try
            {
                var p = t.Property("Value");
                if (p != null) return p.GetValue<float>();
            }
            catch { }
            try
            {
                var f = t.Field("Value");
                if (f != null) return f.GetValue<float>();
            }
            catch { }
            return 0f;
        }
    }

    // Ensure new prefabs get the scaler (covers standard & bionics)
    [HarmonyPatch(typeof(BaseMinionConfig), nameof(BaseMinionConfig.BasePrefabInit))]
    public static class BaseMinionConfig_BasePrefabInit_AttachHealingScaler_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            if (go == null) return;
            go.AddOrGet<HealingConsumptionScaler>();
        }
    }

    // Also ensure dupes loaded from saves get the scaler
    [HarmonyPatch(typeof(MinionIdentity), nameof(MinionIdentity.OnSpawn))]
    public static class MinionIdentity_OnSpawn_AttachHealingScaler_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(MinionIdentity __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;
            if (go == null) return;
            go.AddOrGet<HealingConsumptionScaler>();
        }
    }

 

}

