using KSerialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized
{
    [AddComponentMenu("KMonoBehaviour/Creatures/CreatureMassTracker")]
    public partial class CreatureMassTracker : KMonoBehaviour
    {
        public enum AccumulationMode
        {
            Calories = 0,
            ConsumedMass = 1
        }

        [Serialize] public AccumulationMode Mode = AccumulationMode.Calories;

        // Baseline and conversion knobs
        [Serialize] public float STARTING_MASS = 1f;
        [Serialize] public float CALORIE_RATIO = 100000f; // kg = calories / CALORIE_RATIO (Calories mode)
        [Serialize] public float MASS_RATIO = 1f;         // kg delta = consumed_kg * MASS_RATIO (ConsumedMass mode)

        // Intake accumulators (telemetry; mass is applied directly to PE.Mass)
        [Serialize] private float caloriesConsumedTotal = 0f;
        [Serialize] private float massConsumedTotal = 0f;

        [MyCmpGet] private PrimaryElement primaryElement;

        [Serialize] public List<ExtraDropSpec> ExtraDrops;

        // Snapshot the prefab's mass once; used as baseline for visuals
        [Serialize] private bool initializedFromPrefabMass = false;
        [Serialize] private float prefabStartingMassSnapshot = 1f;

        // ===== Visual scaling config (defaults defined as constants below) =====
        // NOTE: These per-instance fields are serialized; we add a config-hash upgrade so they
        //       get overwritten when you ship a mod update with new compile-time defaults.
        [Serialize] public bool ENABLE_VISUAL_SCALING = true;
        [Serialize] public float SCALE_AT_START_MASS = DEFAULT_SCALE_AT_START_MASS;
        [Serialize] public float SCALE_AT_100X_MASS = DEFAULT_SCALE_AT_100X_MASS;
        [Serialize] public float MAX_MASS_MULTIPLE_FOR_MAX_SCALE = DEFAULT_MAX_MULTIPLE_FOR_MAX_SCALE;
        [Serialize] public bool ANCHOR_DOWNWARD = DEFAULT_ANCHOR_DOWNWARD;

        // Versioning: the config-hash that was last applied to this instance
        [Serialize] private int visualScaleConfigHash = 0;

        // Compile-time defaults (bump these in code when you want new defaults)
        private const bool DEFAULT_ENABLE_VISUAL_SCALING = true;
        private const float DEFAULT_SCALE_AT_START_MASS = 0.9f;
        private const float DEFAULT_SCALE_AT_100X_MASS = 1.35f;
        private const float DEFAULT_MAX_MULTIPLE_FOR_MAX_SCALE = 100f;
        private const bool DEFAULT_ANCHOR_DOWNWARD = true;

        // Hash for the current compiled defaults
        private static readonly int CURRENT_VISUAL_CONFIG_HASH =
            ComputeVisualConfigHash(
                DEFAULT_ENABLE_VISUAL_SCALING,
                DEFAULT_SCALE_AT_START_MASS,
                DEFAULT_SCALE_AT_100X_MASS,
                DEFAULT_MAX_MULTIPLE_FOR_MAX_SCALE,
                DEFAULT_ANCHOR_DOWNWARD
            );

        // Cache for optional direct notifications
        [NonSerialized] private Rephysicalized.Visuals.CreatureVisualScaler cachedVisualScaler;

        private const int CALORIES_CONSUMED_EVENT_ID = -2038961714;

        [Serializable]
        public class ExtraDropSpec
        {
            [Serialize] public string id;
            [Serialize] public float fraction = 1f;
        }

        private static readonly Dictionary<Tag, List<ExtraDropSpec>> s_DefaultDropsByPrefabTag = new Dictionary<Tag, List<ExtraDropSpec>>();

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

                initializedFromPrefabMass = true;
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

            Subscribe(CALORIES_CONSUMED_EVENT_ID, OnCaloriesConsumed);

            // DO NOT re-baseline STARTING_MASS here; leave it as the original snapshot unless explicitly changed.

            // Upgrade visual scaling config if the compiled defaults changed since this instance was saved
            UpgradeVisualScalingConfigIfNeeded();

            // Ensure a visual scaler exists if enabled and apply initial size
            EnsureVisualScaler();
            NotifyVisualScaler();
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

        // Convenience getter
        public float GetCurrentMass()
        {
            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            return Mathf.Max(0.001f, primaryElement != null ? primaryElement.Mass : STARTING_MASS);
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

            if (Mode == AccumulationMode.Calories)
            {
                caloriesConsumedTotal += caloriesFromEvent;

                // kg = calories / CALORIE_RATIO
                float kgGain = (CALORIE_RATIO > 0f) ? (caloriesFromEvent / CALORIE_RATIO) : 0f;
                if (kgGain > 0f)
                {
                    primaryElement.Mass = Mathf.Max(0.001f, primaryElement.Mass + kgGain);
                    NotifyVisualScaler();
                }
            }
            else // AccumulationMode.ConsumedMass
            {
                // Convert calories to consumed kg via diet info
                float consumedKg = info.ConvertCaloriesToConsumptionMass(caloriesFromEvent);
                if (consumedKg > 0f)
                    ReportConsumedMass(consumedKg);
            }
        }

        public void ReportConsumedMass(float kilograms)
        {
            if (kilograms <= 0f)
                return;

            massConsumedTotal += kilograms;

            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            if (primaryElement == null)
                return;

            float kgGain = kilograms * Mathf.Max(0f, MASS_RATIO);
            if (kgGain > 0f)
            {
                primaryElement.Mass = Mathf.Max(0.001f, primaryElement.Mass + kgGain);
                NotifyVisualScaler();
            }
        }
        public void AddExternalMass(float kilograms)
        {
            if (kilograms == 0f) return;
            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            if (primaryElement == null)
                return;

            primaryElement.Mass = Mathf.Max(0.001f, primaryElement.Mass + kilograms);
            NotifyVisualScaler();
        }

        public float GetTotalCaloriesConsumed() => caloriesConsumedTotal;
        public float GetTotalMassConsumed() => massConsumedTotal;

        // Explicit: sets a new absolute baseline; clears accumulators and writes PE mass
        public void SetMassAbsolute(float newMassKg)
        {
            float clamped = Mathf.Max(0.001f, newMassKg);

            if (primaryElement == null) primaryElement = GetComponent<PrimaryElement>();
            if (primaryElement != null)
            {
                primaryElement.Mass = clamped;
                NotifyVisualScaler();
            }

            STARTING_MASS = clamped;
            caloriesConsumedTotal = 0f;
            massConsumedTotal = 0f;
        }

        public void SetAccumulationMode(AccumulationMode newMode, bool keepCurrentMassAsBaseline = true)
        {
            if (newMode == Mode)
                return;

            if (keepCurrentMassAsBaseline)
            {
                STARTING_MASS = GetCurrentMass();
                caloriesConsumedTotal = 0f;
                massConsumedTotal = 0f;
            }

            Mode = newMode;
        }

        public void CopyFrom(CreatureMassTracker other, bool carryOverAsStartingMass = true)
        {
            if (other == null) return;

            float carryMass = other.GetCurrentMass();
            SetMassAbsolute(carryMass); // sets PE and baseline

            CALORIE_RATIO = other.CALORIE_RATIO;
            MASS_RATIO = other.MASS_RATIO;
            Mode = other.Mode;

            if (!carryOverAsStartingMass)
            {
                this.caloriesConsumedTotal = other.caloriesConsumedTotal;
                this.massConsumedTotal = other.massConsumedTotal;
            }

            NotifyVisualScaler();
        }

        // Visual scaler wiring
        private void EnsureVisualScaler()
        {
            if (!ENABLE_VISUAL_SCALING)
                return;

            if (cachedVisualScaler == null)
            {
                cachedVisualScaler = gameObject.AddOrGet<Rephysicalized.Visuals.CreatureVisualScaler>();
            }
        }

        private void NotifyVisualScaler()
        {
            if (!ENABLE_VISUAL_SCALING)
                return;

            if (cachedVisualScaler == null)
                EnsureVisualScaler();

            if (cachedVisualScaler != null)
                cachedVisualScaler.OnMassChanged();
        }

        // ===== Visual scaling config upgrade helpers =====

        private static int ComputeVisualConfigHash(bool enabled, float start, float at100x, float maxMult, bool anchorDown)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (enabled ? 1 : 0);
                h = h * 31 + BitConverter.ToInt32(BitConverter.GetBytes(start), 0);
                h = h * 31 + BitConverter.ToInt32(BitConverter.GetBytes(at100x), 0);
                h = h * 31 + BitConverter.ToInt32(BitConverter.GetBytes(maxMult), 0);
                h = h * 31 + (anchorDown ? 1 : 0);
                return h;
            }
        }

        private void UpgradeVisualScalingConfigIfNeeded()
        {
            if (visualScaleConfigHash == CURRENT_VISUAL_CONFIG_HASH)
                return;

            // Overwrite serialized instance fields with the new compiled defaults
            ENABLE_VISUAL_SCALING = DEFAULT_ENABLE_VISUAL_SCALING;
            SCALE_AT_START_MASS = DEFAULT_SCALE_AT_START_MASS;
            SCALE_AT_100X_MASS = DEFAULT_SCALE_AT_100X_MASS;
            MAX_MASS_MULTIPLE_FOR_MAX_SCALE = DEFAULT_MAX_MULTIPLE_FOR_MAX_SCALE;
            ANCHOR_DOWNWARD = DEFAULT_ANCHOR_DOWNWARD;

            visualScaleConfigHash = CURRENT_VISUAL_CONFIG_HASH;

            // Ensure visuals are refreshed with the new config
            NotifyVisualScaler();
        }
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

            if (adultTracker != null && babyTracker != null)
            {
                adultTracker.CopyFrom(babyTracker, carryOverAsStartingMass: true);
            }
            else
            {
                var peAdult = GetComponent<PrimaryElement>();
                var peBaby = babyGo.GetComponent<PrimaryElement>();
                if (peAdult != null && peBaby != null)
                    peAdult.Mass = Mathf.Max(0.001f, peBaby.Mass);
            }

            // Ensure visuals in sync next frame
            GameScheduler.Instance.ScheduleNextFrame("MassCarryOverEnforce", _ =>
            {
                var pe = GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    pe.Mass = Mathf.Max(0.001f, pe.Mass);
                    var scaler = GetComponent<Rephysicalized.Visuals.CreatureVisualScaler>();
                    if (scaler != null) scaler.OnMassChanged();
                }
            });
        }
    }
}



