using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System;
using System.Collections.Generic;
using System.Linq;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{

    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PlantMassTrackerComponent : KMonoBehaviour, ISim1000ms
    {
        [MyCmpGet] private KPrefabID _kPrefabId;
        [MyCmpGet] private PrimaryElement _primaryElement;

        [MyCmpGet] private Tinkerable _tinkerable;

        // Only tags explicitly registered as consumables are tracked
        private readonly HashSet<Tag> _allowedConsumptionTags = new HashSet<Tag>();
        private readonly List<Storage> _storages = new List<Storage>();
        private readonly Dictionary<Storage, Dictionary<Tag, float>> _prevTotalsByStorageTag = new Dictionary<Storage, Dictionary<Tag, float>>();

        // storages PMT should ignore (e.g., production storages like Carbon)
        private readonly HashSet<Storage> _ignoredStorages = new HashSet<Storage>();

        // Config injected by registry
        private List<MaterialYield> _yields = new List<MaterialYield> { new MaterialYield("RotPile", 1f) };
        private float _harvestMassSubtractKg = 1f;
        private string _tinkerEffectId = "FarmTinker";

        // Tinker dedup bookkeeping
        private bool _hadTinkerEffect;
        private bool _tinkerCreditGrantedExternally;

        // Persisted plant mass; also mirrored on the plot via PlotMassStore
        [Serialize] private float trackedMassKg = 1f;
        public float TrackedMassKg
        {
            get => trackedMassKg;
            private set
            {
                trackedMassKg = value;
                _plotStore?.Set(trackedMassKg);
            }
        }

        private PlotMassStore _plotStore;
        private bool _loadedFromPlotStore;

        // Observation/diff tracking
        private float _lastObservedPlantMassKg = -1f;
        private float _lastAppliedTrackedKg = -1f;
        private bool _suppressInitialExternalDelta = true;

        // Prevent dig conversion immediately after harvest conversion
        private int _suppressDigTicksAfterHarvest = 0;

        // Optional: set false to avoid writing our mass back onto PrimaryElement each tick
        public static bool DebugExposeMassOnPrimaryElement = true;

        // Suppression flag to avoid counting during teardown or when explicitly disabled
        private bool _suppressAccumulation = false;

        // Final teardown flag (kept for API completeness; used by external calls if needed)
        private bool _finalTeardown = false;

        // Pending mass to subtract at harvest due to PlantFiberProducer spawning fiber
        private float _pendingPlantFiberSubtractKg = 0f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (_primaryElement == null)
                _primaryElement = GetComponent<PrimaryElement>();

            // Events
            Subscribe((int)GameHashes.Uprooted, OnUprooted);
            Subscribe((int)GameHashes.Harvest, OnHarvested);

            // Try to bind plot store and restore mass
            TryBindPlotStoreFromPlantablePlot();

            if (_primaryElement != null)
            {
                _lastObservedPlantMassKg = Mathf.Max(0f, _primaryElement.Mass);
                _lastAppliedTrackedKg = TrackedMassKg;
            }
        }

        public override void OnCleanUp()
        {
            // IMPORTANT: stop accumulation and unsubscribe BEFORE base cleanup
            _suppressAccumulation = true;

            foreach (var s in _storages)
                s?.Unsubscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);

            _storages.Clear();
            _prevTotalsByStorageTag.Clear();
            _ignoredStorages.Clear();

            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            // Decay harvest->dig suppression window
            if (_suppressDigTicksAfterHarvest > 0)
                _suppressDigTicksAfterHarvest--;

            if (!_suppressAccumulation && !_finalTeardown)
                PollStoragesAndAccumulate();

            if (!DebugExposeMassOnPrimaryElement || _primaryElement == null)
                return;

            const float EPS = 0.0001f;

            float plantMassAtTickStartRaw = Mathf.Max(0f, _primaryElement.Mass);

            if (_suppressInitialExternalDelta)
            {
                _primaryElement.Mass = Mathf.Max(1f, plantMassAtTickStartRaw);
                _lastObservedPlantMassKg = plantMassAtTickStartRaw;
                _lastAppliedTrackedKg = TrackedMassKg;
                _suppressInitialExternalDelta = false;
                return;
            }

            float externalDelta = plantMassAtTickStartRaw - (_lastObservedPlantMassKg < 0f ? 0f : _lastObservedPlantMassKg);
            float trackerDelta = TrackedMassKg - _lastAppliedTrackedKg;
            if (trackerDelta < EPS && trackerDelta > -EPS) trackerDelta = 0f;

            float plantMassAfterRaw = plantMassAtTickStartRaw + trackerDelta;
            _primaryElement.Mass = Mathf.Max(1f, plantMassAfterRaw);

            float newAppliedTracker = _lastAppliedTrackedKg + externalDelta + trackerDelta;
            if (newAppliedTracker < 0f) newAppliedTracker = 0f;

            _lastAppliedTrackedKg = newAppliedTracker;
            TrackedMassKg = newAppliedTracker;

            _lastObservedPlantMassKg = Mathf.Max(0f, _primaryElement.Mass);
        }

        private void PollStoragesAndAccumulate()
        {
            if (_finalTeardown) return;

            float consumedThisPoll = 0f;

            foreach (var storage in _storages)
            {
                if (storage == null) continue;
                if (PlantMassTrackerTeardown.IsSuppressed(storage)) continue; // SKIP suppressed plot storages

                if (!_prevTotalsByStorageTag.TryGetValue(storage, out var prevByTag))
                {
                    prevByTag = new Dictionary<Tag, float>();
                    _prevTotalsByStorageTag[storage] = prevByTag;
                }

                // Seed any missing snapshots for allowed tags (no dynamic tag discovery)
                SeedMissingSnapshotsForAllowedTags(storage, prevByTag);

                foreach (var tag in _allowedConsumptionTags)
                {
                    float now = SumStorageByTag(storage, tag);
                    float prev = prevByTag.TryGetValue(tag, out var p) ? p : now;
                    float delta = prev - now; // positive if consumed
                    if (delta > 0f)
                        consumedThisPoll += delta;

                    prevByTag[tag] = now;
                }
            }

            if (consumedThisPoll > 0f)
                TrackedMassKg += consumedThisPoll;
        }

        // Called by registry once per plant, when config is known
        internal void InitializeFromConfig(PlantMassTrackerConfig config)
        {
            if (config == null) return;
            _yields = new List<MaterialYield>(config.yields ?? new List<MaterialYield> { new MaterialYield("RotPile", 1f) });
            _harvestMassSubtractKg = Mathf.Max(0f, config.harvestMassSubtractKg);
            if (!string.IsNullOrEmpty(config.tinkerEffectId))
                _tinkerEffectId = config.tinkerEffectId;
        }

        // Called by strongly-typed SetStorage patches for Fertilization/Irrigation monitors
        // Only registers tags explicitly provided in consumedTags; DOES NOT infer from storage filters/items.
        internal void RegisterMonitorStorage(Storage storage, IEnumerable<Tag> consumedTags)
        {
            if (storage == null) return;
            if (_ignoredStorages.Contains(storage)) return; // skip ignored storages

            // If no explicit tags provided, do not register/subscribe this storage yet
            bool anyAdded = false;
            if (consumedTags != null)
            {
                foreach (var tag in consumedTags)
                {
                    if (_allowedConsumptionTags.Add(tag))
                        anyAdded = true;
                }
            }
            if (!anyAdded)
                return;

            TryBindPlotStoreFromStorage(storage);

            bool alreadyRegistered = _storages.Contains(storage);

            if (!_prevTotalsByStorageTag.TryGetValue(storage, out var snapshot))
            {
                snapshot = new Dictionary<Tag, float>();
                _prevTotalsByStorageTag[storage] = snapshot;
            }

            // Seed baselines for allowed tags so existing mass is not counted as "consumed"
            foreach (var tag in _allowedConsumptionTags)
            {
                if (!snapshot.ContainsKey(tag))
                    snapshot[tag] = SumStorageByTag(storage, tag);
            }

            // Only subscribe and add once
            if (!alreadyRegistered)
            {
                storage.Subscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
                _storages.Add(storage);
            }
        }

        private void TryBindPlotStoreFromPlantablePlot()
        {
            if (_plotStore != null) return;

            var plot = GetComponentInParent<PlantablePlot>();
            if (plot == null) return;

            _plotStore = plot.gameObject.AddOrGet<PlotMassStore>();
            RestoreFromPlotStoreIfNeeded();
        }

        private void TryBindPlotStoreFromStorage(Storage storage)
        {
            if (_plotStore != null || storage == null) return;

            var plot = storage.GetComponentInParent<PlantablePlot>();
            if (plot == null) return;

            _plotStore = plot.gameObject.AddOrGet<PlotMassStore>();
            RestoreFromPlotStoreIfNeeded();
        }

        private void RestoreFromPlotStoreIfNeeded()
        {
            if (_loadedFromPlotStore || _plotStore == null) return;

            float saved = Mathf.Max(0f, _plotStore.Get());
            if (saved > 0.0001f)
                trackedMassKg = saved;          // resume from plot state
            else
                _plotStore.Set(trackedMassKg);   // initialize plot from current

            _loadedFromPlotStore = true;
        }

        // Ensure snapshot has entries for all allowed tags; no dynamic tag discovery
        private void SeedMissingSnapshotsForAllowedTags(Storage storage, Dictionary<Tag, float> prevByTag)
        {
            foreach (var tag in _allowedConsumptionTags)
            {
                if (!prevByTag.ContainsKey(tag))
                    prevByTag[tag] = SumStorageByTag(storage, tag);
            }
        }

        private void OnAnyStorageChanged(object _)
        {
            if (_suppressAccumulation || _finalTeardown)
                return;

            float consumedThisEvent = 0f;

            foreach (var storage in _storages)
            {
                if (storage == null) continue;
                if (PlantMassTrackerTeardown.IsSuppressed(storage)) continue; // SKIP suppressed plot storages

                if (!_prevTotalsByStorageTag.TryGetValue(storage, out var prevByTag))
                {
                    prevByTag = new Dictionary<Tag, float>();
                    _prevTotalsByStorageTag[storage] = prevByTag;
                }

                SeedMissingSnapshotsForAllowedTags(storage, prevByTag);

                foreach (var tag in _allowedConsumptionTags)
                {
                    float now = SumStorageByTag(storage, tag);
                    float prev = prevByTag.TryGetValue(tag, out var p) ? p : now;
                    float delta = prev - now;
                    if (delta > 0f) consumedThisEvent += delta;

                    prevByTag[tag] = now;
                }
            }

            if (consumedThisEvent > 0f)
                TrackedMassKg += consumedThisEvent;
        }

        private static float SumStorageByTag(Storage storage, Tag tag)
        {
            float sum = 0f;
            var items = storage.items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = items[i];
                if (go == null) continue;
                if (!go.HasTag(tag)) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null) sum += pe.Mass;
            }
            return sum;
        }

        // Public mass API (prey, tinker, etc.)
        public void AddMassDelta(float kgDelta)
        {
            if (Mathf.Approximately(kgDelta, 0f))
                return;

            TrackedMassKg = Mathf.Max(0f, TrackedMassKg + kgDelta);

            if (DebugExposeMassOnPrimaryElement)
                ApplyVisualFromTrackerNow();
        }

        public void AddExternalMass(float kg) => AddMassDelta(kg);

        public void AddTinkerMass(float kg)
        {
            _tinkerCreditGrantedExternally = kg > 0f;
            AddMassDelta(kg);
        }

        public void AddPreyMass(float kg) => AddMassDelta(kg);

        // Immediately apply the current tracker delta to the PrimaryElement mass
        public void ApplyVisualFromTrackerNow()
        {
            if (_primaryElement == null)
                _primaryElement = GetComponent<PrimaryElement>();
            if (_primaryElement == null)
                return;

            float plantMassAtTickStartRaw = Mathf.Max(0f, _primaryElement.Mass);
            float trackerDelta = TrackedMassKg - _lastAppliedTrackedKg;

            float plantMassAfterRaw = plantMassAtTickStartRaw + trackerDelta;
            _primaryElement.Mass = Mathf.Max(1f, plantMassAfterRaw);

            _lastAppliedTrackedKg = TrackedMassKg;
            _lastObservedPlantMassKg = Mathf.Max(0f, _primaryElement.Mass);
            _suppressInitialExternalDelta = false;
        }

        // Allow other systems to mark storages as "ignored"
        public void IgnoreStorage(Storage storage)
        {
            if (storage == null) return;
            _ignoredStorages.Add(storage);
        }

        public void IgnoreAndUnregisterStorage(Storage storage)
        {
            if (storage == null) return;
            _ignoredStorages.Add(storage);

            if (_storages.Remove(storage))
            {
                storage.Unsubscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
                _prevTotalsByStorageTag.Remove(storage);
            }
        }

        private void OnHarvested(object data)
        {
            // Detect if PlantFiber will be spawned by PlantFiberProducer and record the amount
            _pendingPlantFiberSubtractKg = 0f;
            try
            {
                // Only attempt when this plant has a PlantFiberProducer component
                var fiberProducer = GetComponent<PlantFiberProducer>();
                if (fiberProducer != null && fiberProducer.amount > 0f && data is Harvestable harvestable && harvestable != null)
                {
                    var completer = harvestable.completed_by;
                    if (completer != null)
                    {
                        var resume = completer.GetComponent<MinionResume>();
                        if (resume != null)
                        {
                            var perk = Db.Get()?.SkillPerks?.CanSalvagePlantFiber;
                            if (perk != null && resume.HasPerk(perk))
                            {
                                _pendingPlantFiberSubtractKg = Mathf.Max(0f, fiberProducer.amount);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PMT] Exception evaluating PlantFiber harvest subtraction: {e}");
                _pendingPlantFiberSubtractKg = 0f;
            }

            // Flush any last-second storage consumption before converting
            PollStoragesAndAccumulate();

            // Keep visual in sync for UI, but conversion will use tracked mass (not PE)
            ApplyVisualFromTrackerNow();

            SpawnYieldsAndReset(isHarvest: true);
            _suppressDigTicksAfterHarvest = 2;
        }

        private void OnUprooted(object data)
        {
            if (_suppressDigTicksAfterHarvest > 0)
                return;

            // Flush any last-second storage consumption before converting
            PollStoragesAndAccumulate();

            // Keep visual in sync for UI, but conversion will use tracked mass (not PE)
            ApplyVisualFromTrackerNow();

            // No PlantFiber on uproot; ensure pending subtraction is clear
            _pendingPlantFiberSubtractKg = 0f;

            SpawnYieldsAndReset(isHarvest: false);
        }

        // Returns the mass to convert. Use the tracked mass only to avoid PE clamp/timing skew.
        private float GetMassForConversion() => Mathf.Max(0f, TrackedMassKg);

        private void SpawnYieldsAndReset(bool isHarvest)
        {
            const float baselineSubtractKg = 1f;

            float effectiveHarvestSubtract = 0f;
            if (isHarvest)
            {
                var mod = GetComponent<PlantMassTrackerYieldModifier>(); // same-GO only
                bool modifierOverridesHarvest = mod != null && mod.overrideHarvestYields && mod.harvestYields != null && mod.harvestYields.Count > 0;
                effectiveHarvestSubtract = modifierOverridesHarvest ? 0f : Mathf.Max(0f, _harvestMassSubtractKg);
            }

            float tracked = Mathf.Max(0f, TrackedMassKg);
            float peMass = (_primaryElement != null) ? Mathf.Max(1f, _primaryElement.Mass) : Mathf.Max(1f, tracked);

            float trackedExcess = Mathf.Max(0f, tracked - baselineSubtractKg);
            float peExcess = Mathf.Max(0f, peMass - baselineSubtractKg);

            float baseNet = Mathf.Min(trackedExcess, peExcess);
            float net = Mathf.Max(0f, baseNet - (isHarvest ? effectiveHarvestSubtract : 0f));

            // Subtract PlantFiberProducer amount if this harvest will spawn fiber
            if (isHarvest && _pendingPlantFiberSubtractKg > 0f)
            {
                net = Mathf.Max(0f, net - _pendingPlantFiberSubtractKg);
            }

            if (net > 0.0001f)
            {
                float tempK = _primaryElement != null ? _primaryElement.Temperature : 293.15f;

                var cell = Grid.PosToCell(transform.GetPosition());
                var cellAbove = Grid.CellAbove(cell);
                if (!Grid.IsValidCell(cellAbove))
                    cellAbove = cell;

                var yieldsToUse = GetEffectiveYields(isHarvest);

                foreach (var y in yieldsToUse)
                {
                    if (y == null || y.multiplier <= 0f || string.IsNullOrWhiteSpace(y.id)) continue;

                    float mass = net * y.multiplier;
                    if (mass <= 0f) continue;

                    if (System.Enum.TryParse<SimHashes>(y.id, true, out var simHash)
                        && ElementLoader.FindElementByHash(simHash) != null)
                    {
                        var element = ElementLoader.FindElementByHash(simHash);
                        if (element.IsGas)
                            SpawnElementAsSim(cellAbove, simHash, mass, tempK);
                        else if (element.IsLiquid)
                            SpawnElementAsSim(cell, simHash, mass, tempK);
                        else
                            SpawnSolidDebris(simHash, mass, tempK);
                    }
                    else
                    {
                        SpawnPrefabMass(y.id, mass, tempK);
                    }
                }
            }

            // clear pending fiber subtraction after use
            _pendingPlantFiberSubtractKg = 0f;

            // Reset to 1 kg tracked and PE
            TrackedMassKg = 1f;

            if (_primaryElement != null)
                _primaryElement.Mass = 1f;

            _lastAppliedTrackedKg = 1f;
            _lastObservedPlantMassKg = 1f;
        }

        private List<MaterialYield> GetEffectiveYields(bool isHarvest)
        {
            var baseList = CloneYieldList(_yields);
            var mod = GetComponent<PlantMassTrackerYieldModifier>();
            if (mod == null)
                return baseList;

            var extras = isHarvest ? mod.harvestYields : mod.digYields;
            bool doOverride = isHarvest ? mod.overrideHarvestYields : mod.overrideDigYields;

            if (extras != null && extras.Count > 0)
            {
                if (doOverride)
                    return CloneYieldList(extras);
                else
                    MergeInto(baseList, extras);
            }
            return baseList;
        }

        private static List<MaterialYield> CloneYieldList(IEnumerable<MaterialYield> src)
        {
            var list = new List<MaterialYield>();
            if (src == null) return list;
            foreach (var y in src)
            {
                if (y == null) continue;
                list.Add(new MaterialYield(y.id, y.multiplier));
            }
            return list;
        }

        private static void MergeInto(List<MaterialYield> baseList, IEnumerable<MaterialYield> extras)
        {
            if (extras == null) return;
            foreach (var e in extras)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.id) || e.multiplier <= 0f)
                    continue;

                var existing = baseList.FirstOrDefault(x => x != null && string.Equals(x.id, e.id, System.StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    existing.multiplier += e.multiplier;
                else
                    baseList.Add(new MaterialYield(e.id, e.multiplier));
            }
        }

        private static void SpawnElementAsSim(int cell, SimHashes element, float mass, float temperature)
        {
            byte diseaseIdx = byte.MaxValue;
            int diseaseCount = 0;

            SimMessages.AddRemoveSubstance(
                gameCell: cell,
                new_element: element,
                ev: default(CellAddRemoveSubstanceEvent),
                mass: mass,
                temperature: temperature,
                disease_idx: diseaseIdx,
                disease_count: diseaseCount,
                do_vertical_solid_displacement: true
            );
        }

        private void SpawnSolidDebris(SimHashes element, float mass, float temperature)
        {
            var elem = ElementLoader.FindElementByHash(element);
            if (elem == null) return;

            var pos = transform.GetPosition();
            byte diseaseIdx = byte.MaxValue;
            byte diseaseCount = 0;

            elem.substance.SpawnResource(
                pos,
                mass,
                temperature,
                diseaseIdx,
                diseaseCount,
                prevent_merge: false,
                forceTemperature: true,
                manual_activation: false
            );
        }

        private void SpawnPrefabMass(string prefabId, float mass, float temperature)
        {
            var prefab = Assets.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[PMT] Unknown prefab '{prefabId}', skipping spawn.");
                return;
            }

            var go = GameUtil.KInstantiate(prefab, transform.GetPosition(), Grid.SceneLayer.Ore);
            go.SetActive(true);
            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                pe.Temperature = temperature;
                pe.Mass = mass;
            }
        }

        public void BeginFinalTeardown()
        {
            if (_finalTeardown) return;
            _finalTeardown = true;

            string plantId = _kPrefabId != null ? _kPrefabId.PrefabID().Name : gameObject.name;

            // 1) Unsubscribe immediately to avoid observing storage removals (or any spurious OnStorageChange)
            foreach (var s in _storages)
            {
                if (s == null) continue;
                s.Unsubscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
            }

            // 2) Reseat baselines to current amounts (defensive if any passive polling occurs)
            foreach (var s in _storages)
            {
                if (s == null) continue;

                if (!_prevTotalsByStorageTag.TryGetValue(s, out var prevByTag))
                {
                    prevByTag = new Dictionary<Tag, float>();
                    _prevTotalsByStorageTag[s] = prevByTag;
                }

                foreach (var tag in _allowedConsumptionTags)
                {
                    float now = SumStorageByTag(s, tag);
                    prevByTag[tag] = now;
                }
            }
        }
    }
}

