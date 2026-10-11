using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized
{
    [AddComponentMenu("KMonoBehaviour/Creatures/CreatureMassTracker")]
    public partial class CreatureMassTracker : KMonoBehaviour, ISim1000ms
    {
        // Baseline and conversion knobs
        [Serialize] public float STARTING_MASS = 1f;
        [Serialize] public float MASS_RATIO = 1f;         // kg delta = consumed_kg * MASS_RATIO 

        // Intake accumulators (telemetry)
        [Serialize] private float massConsumedTotal = 0f;

        [MyCmpGet] private PrimaryElement primaryElement;

        [Serialize] public List<ExtraDropSpec> ExtraDrops;
        [Serializable]
        public class PerFoodRatioSpec
        {
            [Serialize] public string consumedTag;
            [Serialize] public float ratio = 1f;
        }

        // Allow external application of per-food overrides and rebuild runtime lookup immediately.
        public void SetPerFoodOverrides(List<PerFoodRatioSpec> overrides)
        {
            PerFoodOverrides = overrides;
            perFoodLookup = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (PerFoodOverrides != null)
            {
                foreach (var o in PerFoodOverrides)
                {
                    if (o == null || string.IsNullOrEmpty(o.consumedTag)) continue;
                    perFoodLookup[o.consumedTag] = o.ratio;
                }
            }

            //try
            //{
            //    if (gameObject != null)
            //    {
            //        string creatureName = !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : "<unnamed>";
            //        Debug.Log($"[Rephysicalized][CreatureMassTracker] {creatureName} SetPerFoodOverrides: {string.Join(", ", perFoodLookup.Keys)}");
            //    }
            //}
            //catch { }
        }
        // Per-instance copy (not serialized). Per-food overrides are registered at prefab level

        public List<PerFoodRatioSpec> PerFoodOverrides;

        // Runtime lookup built from PerFoodOverrides for fast access (name -> ratio)
        private Dictionary<string, float> perFoodLookup;

        // Snapshot the prefab's mass once; used as baseline for visuals
        [Serialize] private bool initializedFromPrefabMass = false;
        [Serialize] private float prefabStartingMassSnapshot = 1f;

        // Body mass is the persistent, saveable part controlled by the tracker (excludes derived parts like scales/milk).
        [Serialize] private float bodyMassKg = 0f;

        private const int CALORIES_CONSUMED_EVENT_ID = -2038961714;

        [Serializable]
        public class ExtraDropSpec
        {
            [Serialize] public string id;
            [Serialize] public float fraction = 1f;
        }

        private static readonly Dictionary<Tag, List<ExtraDropSpec>> s_DefaultDropsByPrefabTag = new Dictionary<Tag, List<ExtraDropSpec>>();

        // Static registry for per-prefab per-food overrides. This is used as a fallback so
        // prefab-level registrations survive into spawned instances even if PerFoodOverrides
        // didn't serialize into instances for some reason.
        private static readonly Dictionary<Tag, List<PerFoodRatioSpec>> s_PerFoodOverridesByPrefabTag = new Dictionary<Tag, List<PerFoodRatioSpec>>();

        public static void RegisterPerFoodOverridesForPrefab(Tag prefabTag, List<PerFoodRatioSpec> overrides)
        {
            if (!prefabTag.IsValid || overrides == null) return;
            var copy = new List<PerFoodRatioSpec>(overrides.Count);
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                if (o != null) copy.Add(new PerFoodRatioSpec { consumedTag = o.consumedTag, ratio = o.ratio });
            }
            s_PerFoodOverridesByPrefabTag[prefabTag] = copy;
        }

        public static List<PerFoodRatioSpec> GetRegisteredPerFoodOverrides(Tag prefabTag)
        {
            if (!prefabTag.IsValid) return null;
            if (s_PerFoodOverridesByPrefabTag.TryGetValue(prefabTag, out var list))
            {
                var copy = new List<PerFoodRatioSpec>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    if (o != null) copy.Add(new PerFoodRatioSpec { consumedTag = o.consumedTag, ratio = o.ratio });
                }
                return copy;
            }
            return null;
        }

        public static void RegisterDefaultDropsForPrefab(Tag prefabTag, List<ExtraDropSpec> drops)
        {
            if (prefabTag.IsValid && drops != null)
            {
                var copy = new List<ExtraDropSpec>(drops.Count);
                for (int i = 0; i < drops.Count; i++)
                {
                    var d = drops[i];
                    if (d != null) copy.Add(new ExtraDropSpec { id = d.id, fraction = d.fraction });
                }
                s_DefaultDropsByPrefabTag[prefabTag] = copy;
            }
        }

        public static List<ExtraDropSpec> GetRegisteredDrops(Tag prefabTag)
        {
            if (prefabTag.IsValid && s_DefaultDropsByPrefabTag.TryGetValue(prefabTag, out var drops))
            {
                var copy = new List<ExtraDropSpec>(drops.Count);
                for (int i = 0; i < drops.Count; i++)
                {
                    var d = drops[i];
                    if (d != null) copy.Add(new ExtraDropSpec { id = d.id, fraction = d.fraction });
                }
                return copy;
            }
            return null;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (primaryElement == null)
                primaryElement = GetComponent<PrimaryElement>();

            if (!initializedFromPrefabMass && primaryElement != null)
            {
                // Capture vanilla mass once for this instance; used for visual baseline
                prefabStartingMassSnapshot = Mathf.Max(0.001f, primaryElement.Mass);
                if (STARTING_MASS <= 0f)
                    STARTING_MASS = prefabStartingMassSnapshot;

                // Initialize body mass to baseline (body only; derived masses will be added via sync)
                bodyMassKg = Mathf.Max(0.001f, STARTING_MASS);
                initializedFromPrefabMass = true;
            }
            else
            {
                // Ensure bodyMassKg is sane if loaded from save without init
                if (bodyMassKg <= 0f)
                    bodyMassKg = Mathf.Max(0.001f, STARTING_MASS > 0f ? STARTING_MASS : (primaryElement != null ? primaryElement.Mass : 1f));
            }

            if (ExtraDrops == null || ExtraDrops.Count == 0)
            {
                var kpid = GetComponent<KPrefabID>();
                var registered = (kpid != null) ? GetRegisteredDrops(kpid.PrefabTag) : null;
                if (registered != null && registered.Count > 0)
                    ExtraDrops = registered;
                else
                    ExtraDrops = new List<ExtraDropSpec>() { new ExtraDropSpec { id = RotPileConfig.ID, fraction = 1f } };
            }

            // Build runtime lookup for per-food overrides (case-insensitive by name)
            // Initialize lookup and always try to load per-food overrides from the prefab registry.
            perFoodLookup = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var kpid = GetComponent<KPrefabID>();
                var reg = kpid != null ? GetRegisteredPerFoodOverrides(kpid.PrefabTag) : null;
                if (reg != null && reg.Count > 0)
                {
                    SetPerFoodOverrides(reg);
                }
            }
            catch { }

            Subscribe(CALORIES_CONSUMED_EVENT_ID, OnCaloriesConsumed);
        }

        public override void OnCleanUp()
        {
            Unsubscribe(CALORIES_CONSUMED_EVENT_ID, OnCaloriesConsumed);
            base.OnCleanUp();
        }

        // Expose a robust baseline mass for visuals (prefab snapshot preferred)
        public float GetVisualBaselineMass()
        {
            float m = prefabStartingMassSnapshot > 0f ? prefabStartingMassSnapshot : STARTING_MASS;
            return Mathf.Max(0.001f, m);
        }

        // Effective mass = body mass + scale mass + milk mass
        public float GetCurrentMass()
        {
            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            float scale = ComputeScaleMassKgFromMonitors(gameObject);
            float milk = ComputeMilkMassKgFromMilkMonitor(gameObject);
            return Mathf.Max(0.001f, bodyMassKg + scale + milk);
        }

        // ISim1000ms: keep PrimaryElement.Mass in sync with effective mass without saving derived masses.
        public void Sim1000ms(float dt)
        {
            SyncPeMass();
        }

        private void SyncPeMass()
        {
            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            if (primaryElement == null) return;

            float scale = ComputeScaleMassKgFromMonitors(gameObject);
            float milk = ComputeMilkMassKgFromMilkMonitor(gameObject);
            float effective = Mathf.Max(0.001f, bodyMassKg + scale + milk);
            if (!Mathf.Approximately(primaryElement.Mass, effective))
                primaryElement.Mass = effective;
        }

        private void OnCaloriesConsumed(object data)
        {
            if (!(data is Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent> boxed))
                return;

            var evt = boxed.value;
            var consumedTag = evt.tag;
            var caloriesFromEvent = evt.calories;

            if (!consumedTag.IsValid || caloriesFromEvent <= 0f)
                return;

            // Resolve diet info for conversion where needed
            var smi = gameObject.GetSMI<CreatureCalorieMonitor.Instance>();
            var diet = smi?.stomach?.diet;
            var info = diet?.GetDietInfo(consumedTag);

            if (info == null)
                return;

            // Convert calories to consumed kg via diet info and report
            float consumedKg = info.ConvertCaloriesToConsumptionMass(caloriesFromEvent);
            if (consumedKg > 0f)
            {
                try
                {
                    //      string creatureName = string.IsNullOrEmpty(gameObject.name) ? "<unnamed>" : gameObject.name;
                    //     Debug.Log($"[Rephysicalized][CreatureMassTracker] {creatureName} consumed food. Calories={caloriesFromEvent}, Tag={consumedTag}, MassKg={consumedKg}");
                }
                catch { }

                // Use per-food override when available
                ReportConsumedMass(evt.tag, consumedKg);
            }
        }


        // Report mass by tag so per-food overrides can be applied. This is the preferred entry.
        public void ReportConsumedMass(Tag consumedTag, float kilograms)
        {
            if (kilograms <= 0f) return;

            float ratio = GetRatioForConsumedTag(consumedTag);
            ReportConsumedMass_Internal(kilograms, ratio);
        }

        // Backwards-compatible overload: no tag -> use default MASS_RATIO
        public void ReportConsumedMass(float kilograms)
        {
            ReportConsumedMass_Internal(kilograms, MASS_RATIO);
        }

        private void ReportConsumedMass_Internal(float kilograms, float ratio)
        {
            if (kilograms <= 0f)
                return;

            massConsumedTotal += kilograms;

            // Add to body mass only
            float kgGain = kilograms * Mathf.Max(0f, ratio);
            if (kgGain > 0f)
            {
                bodyMassKg = Mathf.Max(0.001f, bodyMassKg + kgGain);
            }
        }

        private float GetRatioForConsumedTag(Tag consumedTag)
        {
            if (consumedTag == null || !consumedTag.IsValid) return MASS_RATIO;
            if (perFoodLookup != null && !string.IsNullOrEmpty(consumedTag.Name) && perFoodLookup.TryGetValue(consumedTag.Name, out var r))
                return r;
            return MASS_RATIO;
        }

        public void AddExternalMass(float kilograms)
        {
            if (kilograms == 0f) return;
            // External adjustments affect body mass only
            bodyMassKg = Mathf.Max(0.001f, bodyMassKg + kilograms);
        }

        public float GetTotalCaloriesConsumed() => 0f;
        public float GetTotalMassConsumed() => massConsumedTotal;

        // Explicit: sets a new absolute baseline; clears accumulators and sets body mass
        public void SetMassAbsolute(float newMassKg)
        {
            float clamped = Mathf.Max(0.001f, newMassKg);

            // Update body mass; sync will update PE
            bodyMassKg = clamped;

            STARTING_MASS = clamped;
            massConsumedTotal = 0f;
        }


        public void CopyFrom(CreatureMassTracker other, bool carryOverAsStartingMass = true)
        {
            if (other == null) return;

            float carryMass = other.GetCurrentMass();
            SetMassAbsolute(carryMass); // sets body mass and baseline

            MASS_RATIO = other.MASS_RATIO;

            if (!carryOverAsStartingMass)
            {
                this.massConsumedTotal = other.massConsumedTotal;
            }
        }
        public void OnEggLaid_DecreaseBodyMass()
        {
            float toRemove = Mathf.Max(0f, STARTING_MASS) * 2f;
            float minBody = Mathf.Max(0.001f, STARTING_MASS);
            bodyMassKg = Mathf.Max(minBody, bodyMassKg - toRemove);
        }


        // Compute scale mass from monitors (ScaleGrowth/ElementGrowth + ScaleGrowthMonitor or WellFedShearable)
        internal static float ComputeScaleMassKgFromMonitors(GameObject go)
        {
            if (TryGetScaleDefAndPercent01(go, out _, out var dropMass, out var pct01))
                return pct01 * dropMass;
            return 0f;
        }

        // Compute milk mass from MilkProductionMonitor (kg). If no monitor or zero, returns 0.
        internal static float ComputeMilkMassKgFromMilkMonitor(GameObject go)
        {
            var milkSmi = go != null ? go.GetSMI<MilkProductionMonitor.Instance>() : null;
            if (milkSmi == null) return 0f;

            // MilkAmount is already in kg (MilkPercentage/100 * Capacity)
            float kg = milkSmi.MilkAmount;
            return Mathf.Max(0f, kg);
        }

        // Helper: resolve scale percent and drop def/tag
        internal static bool TryGetScaleDefAndPercent01(GameObject go, out Tag dropTag, out float dropMass, out float percent01)
        {
            dropTag = Tag.Invalid;
            dropMass = 0f;
            percent01 = 0f;
            if (go == null) return false;

            // Percent: prefer ScaleGrowth, else ElementGrowth
            float pct01 = 0f;
            var db = Db.Get();
            var scaleAmt = db?.Amounts?.ScaleGrowth?.Lookup(go);
            if (scaleAmt != null)
                pct01 = Mathf.Clamp01(scaleAmt.value / 100f);
            else
            {
                var elemAmt = db?.Amounts?.ElementGrowth?.Lookup(go);
                if (elemAmt != null)
                    pct01 = Mathf.Clamp01(elemAmt.value / 100f);
            }

            if (pct01 <= 0f)
                return false;

            // Definitions: prefer ScaleGrowthMonitor, else WellFedShearable
            Tag tag = Tag.Invalid;
            float fullMass = 0f;

            var sgm = go.GetSMI<ScaleGrowthMonitor.Instance>();
            if (sgm?.def != null)
            {
                tag = sgm.def.itemDroppedOnShear;
                fullMass = Mathf.Max(0f, sgm.def.dropMass);
            }
            else
            {
                var wfs = go.GetSMI<WellFedShearable.Instance>();
                if (wfs?.def != null)
                {
                    tag = wfs.def.itemDroppedOnShear;
                    fullMass = Mathf.Max(0f, wfs.def.dropMass);
                }
            }

            if (!tag.IsValid || fullMass <= 0f)
                return false;

            dropTag = tag;
            dropMass = fullMass;
            percent01 = pct01;
            return true;
        }

        // Optional: expose body mass for other patches (internal scope)
        internal float GetBodyMass() => bodyMassKg;
    }

    [AddComponentMenu("KMonoBehaviour/Creatures/CreatureMassTrackerLink")]
    public class CreatureMassTrackerLink : KMonoBehaviour
    {
        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.SpawnedFrom, OnSpawnedFrom);
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.SpawnedFrom, OnSpawnedFrom);
            base.OnCleanUp();
        }

        private void OnSpawnedFrom(object data)
        {
            var babyGo = data as GameObject;
            if (babyGo == null) return;

            var adultTracker = GetComponent<CreatureMassTracker>();
            var babyTracker = babyGo.GetComponent<CreatureMassTracker>();

            // Prefer copying fully configured tracker values when the baby has one.
            if (adultTracker != null && babyTracker != null)
            {
                adultTracker.CopyFrom(babyTracker, carryOverAsStartingMass: true);
                return;
            }

            // Fallback: copy only the mass baseline if baby has no tracker.
            var peAdult = GetComponent<PrimaryElement>();
            var peBaby = babyGo.GetComponent<PrimaryElement>();
            if (peAdult != null && peBaby != null)
                peAdult.Mass = Mathf.Max(0.001f, peBaby.Mass);
        }
    }

    [HarmonyPatch(typeof(FertilityMonitor.Instance), nameof(FertilityMonitor.Instance.LayEgg))]
    public static class Patch_Fertility_LayEgg_MassDrain
    {
        public static void Postfix(FertilityMonitor.Instance __instance)
        {
            var go = __instance.gameObject; var tracker = go.GetComponent<CreatureMassTracker>();
            if (tracker != null) tracker.OnEggLaid_DecreaseBodyMass();
        }
    }

}


