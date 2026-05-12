using HarmonyLib;
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
    // IMPORTANT: Apply duplicant stat tweaks BEFORE the database builds Amounts/Attributes,
    // so dependent values (e.g., bionic oxy tank max) use the adjusted oxygen consumption.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Rephys_DbInitialize_Patch
    {
        public static float DuplicantOxygenUse => Config.Instance.DuplicantOxygenUse;
        // Set tuning first so Db builds with these values
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                ApplyTo(global::TUNING.DUPLICANTSTATS.STANDARD);
                ApplyTo(global::TUNING.DUPLICANTSTATS.BIONICS);
      //          Debug.Log("[Rephysicalized] Db.Initialize Prefix: applied DUPLICANTSTATS (O2 use, CO2 conversion, calories, pee rates).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] Db.Initialize Prefix failed: {e}");
            }
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
                // Apply BEFORE DB initializes amounts so dependent maxima rebuild correctly
                baseStats.OXYGEN_USED_PER_SECOND = baseStats.OXYGEN_USED_PER_SECOND * DuplicantOxygenUse;
                baseStats.OXYGEN_TO_CO2_CONVERSION = baseStats.OXYGEN_USED_PER_SECOND / DuplicantOxygenUse / 2.5f;
                baseStats.MAX_CALORIES = 6_000_000f;
            }
        }
    }

    // If IsTooCold() would be true but the duplicant is standing in a liquid cell
    // with less than 5 kg, force it to false (ignore tiny puddles).
    [HarmonyPatch(typeof(ExternalTemperatureMonitor.Instance), nameof(ExternalTemperatureMonitor.Instance.IsTooCold))]
    public static class ExternalTemperatureMonitor_IsTooCold_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(ExternalTemperatureMonitor.Instance __instance, ref bool __result)
        {
            if (!__result)
                return;

            // Determine the cell the dupe occupies
            int cell = Grid.PosToCell((StateMachine.Instance)__instance);
            if (!Grid.IsValidCell(cell))
                return;

            var element = Grid.Element[cell];
            if (element != null && element.IsLiquid)
            {
                float massKg = Grid.Mass[cell];
                // Ignore liquid contact if it's a tiny puddle
                if (massKg < 5f)
                {
                    __result = false;
                }
            }
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
            private KAttribute caloriesDeltaAttribute;     // resolved from Amounts.Calories.deltaAttribute
            private AttributeInstance caloriesDeltaInst;   // attribute instance on this dupe
            private AttributeModifier caloriesDeltaMod;    // our additive modifier (we only change Value)

            private float lastHP;

            // Tuning
            [SerializeField] public float caloriesPercentPer100HpPerCycle = 100f; // +100% at 100 HP/cycle
            [SerializeField] public float maxCalorieMultiplier = 5f;              // hard cap (5x baseline)


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
                try
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
                }
                catch (Exception e)
                {
           
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

            } }
             
                float calMult = 1f + (regenPerCycle / 100f) * (caloriesPercentPer100HpPerCycle / 100f);

                if (maxCalorieMultiplier > 0f) calMult = Mathf.Min(calMult, maxCalorieMultiplier);
                // Apply calories scaling (baseline-preserving additive)
                if (caloriesDeltaInst != null && caloriesDeltaMod != null)
                {
                    float currentWithOur = caloriesDeltaInst.GetTotalValue(); // includes our mod
                    float ourCurrent = GetModifierValue(caloriesDeltaMod);
                    float baseline = currentWithOur - ourCurrent; // isolate baseline (usually negative)
                    float extra = baseline * (calMult - 1f);      // extra negative -> more consumption
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
                {
                    try
                    {
                        bool beforeHealth = health != null; bool beforeAttrs = attributes != null; bool beforeMods = minionMods != null;

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

                        if (DebugEnabled && (!beforeHealth || !beforeMods || !beforeAttrs))
                        {
           
                        }

                        return health != null && attributes != null;
                    }
                    catch (Exception e)
                    {
                        return false;
                    }
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

                    if (DebugEnabled && changed)
                    {
                    }

                    return caloriesDeltaInst != null && caloriesDeltaMod != null;
                }
                catch (Exception e)
                {
                    return false;
                }
            }

            // ------------- AttributeModifier value helpers -------------

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
            // Original: static void BasePrefabInit(GameObject go, Tag duplicantModel)
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


