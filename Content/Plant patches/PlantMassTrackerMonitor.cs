﻿using HarmonyLib;
using Database;
using KSerialization;
using Klei.AI;
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

        // Config injected by registry
        private List<MaterialYield> _yields = new List<MaterialYield> { new MaterialYield("RotPile", 1f) };
        private float _harvestMassSubtractKg = 1f;
        private string _tinkerEffectId = "FarmTinker";


        // Tracked mass used at runtime only; no longer persisted. Yields are calculated
        // directly from the PrimaryElement mass (PE) on harvest/uproot.
        private float trackedMassKg = 1f;
        public float TrackedMassKg
        {
            get => trackedMassKg;
            private set => trackedMassKg = value;
        }

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

            foreach (var storage in _storages)
                PlantMassTrackerRegistry.UnregisterStorageTracker(storage, this);

            _storages.Clear();

            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            // Decay harvest->dig suppression window
            if (_suppressDigTicksAfterHarvest > 0)
                _suppressDigTicksAfterHarvest--;

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

            // Only register storages whose monitor declares at least one consumed tag.
            var tags = consumedTags?.Where(t => t != null).Distinct().ToList();
            if (tags == null || tags.Count == 0)
                return;

            foreach (var tag in tags)
                _allowedConsumptionTags.Add(tag);

            if (!_storages.Contains(storage))
            {
                _storages.Add(storage);
                PlantMassTrackerRegistry.RegisterStorageTracker(storage, this);
            }
        }

        internal void AddConsumedStorageMass(Storage storage, List<PlantElementAbsorber.Planner.ConsumeCommand> consumeCommands)
        {
            if (storage == null || !_storages.Contains(storage) || _suppressAccumulation || consumeCommands == null)
                return;

            float consumedMass = 0f;
            for (int i = 0; i < consumeCommands.Count; i++)
            {
                var command = consumeCommands[i];
                if (command.primaryElement == null || command.deltaMass <= 0f)
                    continue;

                var consumedItem = command.primaryElement.gameObject;
                foreach (var tag in _allowedConsumptionTags)
                {
                    if (consumedItem.HasTag(tag))
                    {
                        consumedMass += command.deltaMass;
                        break;
                    }
                }
            }

            if (consumedMass > 0f)
                AddMassDelta(consumedMass);
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

        private void OnHarvested(object data)
        {
            // Detect PlantFiber spawn for subtraction
            _pendingPlantFiberSubtractKg = 0f;
            var fiberProducer = GetComponent<PlantFiberProducer>();
            if (fiberProducer?.amount > 0f && data is Harvestable harvestable && harvestable.completed_by is WorkerBase completer)
            {
                try
                {
                    if (completer.GetComponent<MinionResume>()?.HasPerk(Db.Get()?.SkillPerks?.CanSalvagePlantFiber) == true)
                        _pendingPlantFiberSubtractKg = fiberProducer.amount;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PMT] PlantFiber eval error: {e}");
                }
            }

            ApplyVisualFromTrackerNow();
            SpawnYieldsAndReset(isHarvest: true);
            _suppressDigTicksAfterHarvest = 2;
        }

        private void OnUprooted(object data)
        {
            if (_suppressDigTicksAfterHarvest > 0)
                return;

            ApplyVisualFromTrackerNow();
            _pendingPlantFiberSubtractKg = 0f;

            SpawnYieldsAndReset(isHarvest: false);
        }

        // Returns the mass to convert. Use the PrimaryElement mass directly.
        private float GetMassForConversion() => _primaryElement != null ? Mathf.Max(0f, _primaryElement.Mass) : Mathf.Max(0f, TrackedMassKg);

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

            // Mutation adjustments
            float yieldMult = 1f;
            bool hasRottenHeaps = false;
            if (isHarvest)
            {
                var mutPlant = GetComponent<MutantPlant>();
                if (mutPlant != null && mutPlant.MutationIDs != null)
                {
                    hasRottenHeaps = mutPlant.MutationIDs.Contains("rottenHeaps");
                    var yieldAttrId = Db.Get().PlantAttributes.YieldAmount.Id;
                    foreach (var mutId in mutPlant.MutationIDs)
                    {
                        var mutation = Db.Get().PlantMutations.Get(mutId);
                        if (mutation != null)
                        {
                            foreach (var mod in mutation.SelfModifiers)
                            {
                                if (mod.AttributeId == yieldAttrId)
                                {
                                    if (mod.IsMultiplier)
                                        yieldMult *= (1f + mod.Value);
                                    else
                                        yieldMult += mod.Value;
                                }
                            }
                        }
                    }
                }
            }
            float totalSubtract = isHarvest ? (effectiveHarvestSubtract * yieldMult + (hasRottenHeaps ? 4f : 0f)) : 0f;

            // Always compute yields from the PrimaryElement mass (PE)
     
            float peMass = (_primaryElement != null) ? Mathf.Max(1f, _primaryElement.Mass) : 1f;
            float peExcess = Mathf.Max(0f, peMass - baselineSubtractKg);
            float net = Mathf.Max(0f, peExcess - totalSubtract);

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

            var go = GameUtil.KInstantiate(prefab, transform.GetPosition(), Grid.SceneLayer.Ore);
            go.SetActive(true);
            var pe = go.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                pe.Temperature = temperature;
                pe.Mass = mass;
            }
        }

    }
}

