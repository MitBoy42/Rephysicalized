using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;

namespace Rephysicalized
{
    internal static class EventPayloadUtil
    {
        public static bool TryGetCaloriesConsumedEvent(object data, out CreatureCalorieMonitor.CaloriesConsumedEvent evt)
        {
            evt = default;
            if (data == null) return false;

            if (data is CreatureCalorieMonitor.CaloriesConsumedEvent direct)
            {
                evt = direct;
                return true;
            }

            var t = data.GetType();

            if (t.IsGenericType && t.Name.StartsWith("Boxed", StringComparison.Ordinal))
            {
               
                    var valueField = t.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (valueField != null)
                    {
                        var val = valueField.GetValue(data);
                        if (val is CreatureCalorieMonitor.CaloriesConsumedEvent boxed)
                        {
                            evt = boxed;
                            return true;
                        }
                    }
                    else
                    {
                        var valueProp = t.GetProperty("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (valueProp != null)
                        {
                            var val = valueProp.GetValue(data, null);
                            if (val is CreatureCalorieMonitor.CaloriesConsumedEvent boxed)
                            {
                                evt = boxed;
                                return true;
                            }
                        }
                    }
            }

         
                var fTag = t.GetField("tag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fCalories = t.GetField("calories", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fTag != null && fCalories != null)
                {
                    var tagObj = fTag.GetValue(data);
                    var calObj = fCalories.GetValue(data);
                    if (tagObj is Tag tagVal && calObj is float calVal)
                    {
                        evt = new CreatureCalorieMonitor.CaloriesConsumedEvent { tag = tagVal, calories = calVal };
                        return true;
                    }
                }
          

            return false;
        }
    }

    [HarmonyPatch(typeof(BasePrehistoricPacuConfig), nameof(BasePrehistoricPacuConfig.CreatePrefab))]
    public static class PrehistoricPacuDietPatch
    {
        private const float PreyCaloriesPerKg = 50000f;
        private const float PreyProducedConversionRate = 0.5f;

        static void Postfix(GameObject __result)
        {
            var poopElem = ElementLoader.GetElement(PrehistoricPacuTuning.POOP_ELEMENT);
            Tag poopTag = poopElem.tag;

            var preyTags = new HashSet<Tag>
            {
                (Tag)"Pacu",
                (Tag)"PacuCleaner",
                (Tag)"PacuTropical",
                (Tag)"Seal"
            };

            var preyInfo = new Diet.Info(
                consumed_tags: preyTags,
                produced_element: poopTag,
                calories_per_kg: PreyCaloriesPerKg,
                produced_conversion_rate: PreyProducedConversionRate,
                food_type: Diet.Info.FoodType.EatPrey
            );

            var fishMeatInfo = new Diet.Info(
                consumed_tags: new HashSet<Tag> { (Tag)"FishMeat" },
                produced_element: poopTag,
                calories_per_kg: GetCaloriesPerKgPacuMeat(),
                produced_conversion_rate: 0f
            );

            var newDiet = new Diet(new[] { preyInfo, fishMeatInfo });

            var calDef = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
            calDef.diet = newDiet;

            var solidDef = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
            solidDef.diet = newDiet;
        }

        private static float GetCaloriesPerKgPacuMeat()
        {
            return PrehistoricPacuTuning.STANDARD_CALORIES_PER_CYCLE / 1f;
        }
    }

    public sealed class ExtraPoopBuffer : MonoBehaviour
    {
        [NonSerialized] public Tag poopElement = Tag.Invalid;
        [NonSerialized] public float pendingKg = 0f;
    }

    [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
    public static class SolidConsumerPacuEatAdjustPatch
    {
        private struct EatState
        {
            public bool Active;
            public float PreCalories;
            public Tag ConsumedTag;
            public bool IsPacuPrey;
            public float PreyMass;
        }

        private static bool IsPacu(KPrefabID kpid) =>
            kpid.HasTag((Tag)"Pacu") || kpid.HasTag((Tag)"PacuCleaner") || kpid.HasTag((Tag)"PacuTropical") || kpid.HasTag((Tag)"Seal");

        [HarmonyPriority(Priority.First)]
        static void Prefix(SolidConsumerMonitor.Instance __instance, object data, out EatState __state)
        {
            __state = default;

            var prey = ResolveKPrefabID(data);
            if (prey == null) return;

            var diet = __instance.diet;
            if (diet == null) return;

            var dietInfo = diet.GetDietInfo(prey.PrefabTag);
            if (dietInfo == null) return;

            if (dietInfo.foodType != Diet.Info.FoodType.EatPrey &&
                dietInfo.foodType != Diet.Info.FoodType.EatButcheredPrey)
                return;

            bool isPacu = IsPacu(prey);
            __state.IsPacuPrey = isPacu;
            __state.ConsumedTag = prey.PrefabTag;

            if (isPacu)
            {
                var butcherable = prey.GetComponent<Butcherable>();
                if (butcherable != null)
                    UnityEngine.Object.Destroy(butcherable);
            }

            GameObject eater = __instance.smi?.gameObject;
            if (eater != null)
            {
                var cal = Db.Get().Amounts.Calories.Lookup(eater);
                __state.PreCalories = cal != null ? cal.value : 0f;
            }

            float preyMass = 0f;
            var pe = prey.GetComponent<PrimaryElement>();
            if (pe != null) preyMass = pe.Mass;
            __state.PreyMass = preyMass;

            if (isPacu && preyMass > 0f)
            {
                float extraPoop = Mathf.Max(preyMass - 1.5f, 0f);
                if (extraPoop > 0f && eater != null)
                {
                    var buffer = eater.GetComponent<ExtraPoopBuffer>() ?? eater.AddComponent<ExtraPoopBuffer>();
                    var poopElem = ElementLoader.GetElement(PrehistoricPacuTuning.POOP_ELEMENT);
                    buffer.poopElement = poopElem != null ? poopElem.tag : Tag.Invalid;
                    buffer.pendingKg += extraPoop;
                }
            }

            __state.Active = true;
        }

        [HarmonyPriority(Priority.Last)]
        static void Postfix(SolidConsumerMonitor.Instance __instance, object data, EatState __state)
        {
            if (!__state.Active) return;

            GameObject eater = __instance.smi?.gameObject;
            if (eater == null) return;

            var cmi = eater.GetSMI<CreatureCalorieMonitor.Instance>();
            if (cmi == null) return;

            var cal = cmi.calories;
            float gained = Mathf.Max(0f, cal.value - __state.PreCalories);
            const float MaxCaloriesPerMeal = 100000f;

            if (gained > MaxCaloriesPerMeal)
            {
                float excess = gained - MaxCaloriesPerMeal;

                cal.value = Mathf.Max(0f, cal.value - excess);

                var list = cmi.stomach.GetCalorieEntries();
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].tag == __state.ConsumedTag && list[i].calories > 0f)
                    {
                        float newCalories = Mathf.Max(0f, list[i].calories - excess);
                        list[i] = new CreatureCalorieMonitor.Stomach.CaloriesConsumedEntry
                        {
                            tag = list[i].tag,
                            calories = newCalories
                        };
                        break;
                    }
                }
            }
        }

        private static KPrefabID ResolveKPrefabID(object data)
        {
            if (data == null) return null;
            if (data is KPrefabID kpid) return kpid;
            if (data is GameObject go) return go.GetComponent<KPrefabID>();
            if (data is Component comp) return comp.GetComponent<KPrefabID>();
            return null;
        }
    }

    [HarmonyPatch(typeof(CreatureCalorieMonitor.Stomach), nameof(CreatureCalorieMonitor.Stomach.Poop))]
    public static class StomachPoopExtraSpawnPatch
    {
        private static readonly FieldInfo OwnerField = AccessTools.Field(typeof(CreatureCalorieMonitor.Stomach), "owner");
        private static readonly FieldInfo StorePoopField = AccessTools.Field(typeof(CreatureCalorieMonitor.Stomach), "storePoop");

        static void Postfix(CreatureCalorieMonitor.Stomach __instance)
        {
            if (OwnerField == null) return;

            var owner = OwnerField.GetValue(__instance) as GameObject;
            if (owner == null) return;

            var buffer = owner.GetComponent<ExtraPoopBuffer>();
            if (buffer == null || buffer.pendingKg <= 0f || !buffer.poopElement.IsValid)
                return;

            int cell = Grid.PosToCell(owner.transform.GetPosition());
            if (!Grid.IsValidCell(cell)) return;

            var elem = ElementLoader.GetElement(buffer.poopElement);
            if (elem == null) return;

            float temperature = owner.GetComponent<PrimaryElement>()?.Temperature ?? 293.15f;

            bool storePoop = false;
            if (StorePoopField != null)
            {
                try { storePoop = (bool)StorePoopField.GetValue(__instance); }
                catch { storePoop = false; }
            }

            if (storePoop)
            {
                var storage = owner.GetComponent<Storage>();
                if (storage != null)
                {
                    if (elem.IsLiquid)
                        storage.AddLiquid(elem.id, buffer.pendingKg, temperature, byte.MaxValue, 0);
                    else if (elem.IsGas)
                        storage.AddGasChunk(elem.id, buffer.pendingKg, temperature, byte.MaxValue, 0, false);
                    else
                        storage.AddOre(elem.id, buffer.pendingKg, temperature, byte.MaxValue, 0);
                }
            }
            else
            {
                if (elem.IsLiquid)
                {
                    FallingWater.instance.AddParticle(cell, elem.idx, buffer.pendingKg, temperature, byte.MaxValue, 0, true);
                }
                else if (elem.IsGas)
                {
                    SimMessages.AddRemoveSubstance(cell, elem.idx, CellEventLogger.Instance.ElementConsumerSimUpdate, buffer.pendingKg, temperature, byte.MaxValue, 0);
                }
                else
                {
                    elem.substance.SpawnResource(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore), buffer.pendingKg, temperature, byte.MaxValue, 0);
                }
            }

     
                PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Resource, elem.name, owner.transform);
    

            buffer.pendingKg = 0f;
        }
    }

    [HarmonyPatch(typeof(SolidConsumerMonitor.Instance), nameof(SolidConsumerMonitor.Instance.OnEatSolidComplete))]
    public static class SolidConsumerPreyDropPatch
    {
        static bool Prefix(SolidConsumerMonitor.Instance __instance, object data)
        {
            var cmp = data as KPrefabID; if (cmp == null) return true;

            var diet = __instance.diet;
            if (diet == null)
                return true;

            var info = diet.GetDietInfo(cmp.PrefabTag);
            if (info == null)
                return true;

            if (info.foodType != Diet.Info.FoodType.EatPrey &&
                info.foodType != Diet.Info.FoodType.EatButcheredPrey)
            {
                return true;
            }

            var pe = cmp.GetComponent<PrimaryElement>();
            if (pe == null)
                return true;

            var amount = Db.Get().Amounts.Calories.Lookup(__instance.smi.gameObject);
            if (amount == null)
                return true;

            string properName = cmp.GetProperName();
            PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, properName, cmp.transform);

            float remainingCapacity = amount.GetMax() - amount.value;
            float preyCalories = diet.AvailableCaloriesInPrey(cmp.PrefabTag);

            // Consume the prey fully (no drops)
            pe.Mass = 0f;

            // Award calories via event using the canonical boxed payload
            float caloriesGained = Mathf.Min(remainingCapacity, preyCalories);
            var payload = new CreatureCalorieMonitor.CaloriesConsumedEvent
            {
                tag = cmp.PrefabTag,
                calories = caloriesGained
            };
            var boxed = Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent>.Get(payload); __instance.Trigger((int)GameHashes.CaloriesConsumed, boxed); boxed.Release();

        

            __instance.targetEdible = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(CreatureCalorieMonitor.Instance), nameof(CreatureCalorieMonitor.Instance.OnCaloriesConsumed))]
    public static class CreatureCalorieMonitor_OnCaloriesConsumed_SafePatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(CreatureCalorieMonitor.Instance __instance, object data)
        {
            if (!EventPayloadUtil.TryGetCaloriesConsumedEvent(data, out var evData))
            {
                return true;
            }

            __instance.calories.value += evData.calories;
            __instance.stomach.Consume(evData.tag, evData.calories);
            __instance.lastMealOrPoopTime = Time.time;

            return false;
        }
    }

    [HarmonyPatch(typeof(BasePrehistoricPacuConfig), nameof(BasePrehistoricPacuConfig.CreatePrefab))]
    public static class BasePrehistoricPacuConfig_CreatePrefab_DeathDrop_Patch
    {
        private const float OldCount = 12f;
        private const float NewCount = 2f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;

            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Mathf.Approximately(f, OldCount))
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, NewCount);
                    replaced++;
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }
}