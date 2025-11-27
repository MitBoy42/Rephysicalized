using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

namespace Rephysicalized
{
   

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class OxyRockZeroYieldPatch
    {
        private static void Postfix()
        {
            var crops = TUNING.CROPS.CROP_TYPES;
            string oxyId = SimHashes.OxyRock.ToString();
            for (int i = 0; i < crops.Count; i++)
            {
                if (crops[i].cropId == oxyId)
                {
                    var c = crops[i];
                    crops[i] = new Crop.CropVal(c.cropId, c.cropDuration, 20);
                    break;
                }
            }
        }
    }



    [HarmonyPatch(typeof(BlueGrassConfig), nameof(BlueGrassConfig.CreatePrefab))]
    internal static class BlueGrassConfig_CreatePrefab_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            // Dedicated CO2 storage (capacity 1 kg), hidden
            var co2Tag = SimHashes.CarbonDioxide.CreateTag();
            var co2Storage = __result.AddComponent<Storage>();
            co2Storage.capacityKg = 1f;
            co2Storage.showInUI = false;
            co2Storage.allowItemRemoval = false;
            co2Storage.storageFilters = new List<Tag> { co2Tag };

            // Dedicated Carbon storage (internal), hidden
            var carbonTag = SimHashes.Dirt.CreateTag();
            var carbonStorage = __result.AddComponent<Storage>();
            carbonStorage.capacityKg = 1000f;
            carbonStorage.showInUI = true;
            carbonStorage.allowItemRemoval = false;
            carbonStorage.storageFilters = new List<Tag> { carbonTag };

            // Ensure ElementConsumer stores consumed gas to CO2 storage
            var consumer = __result.GetComponent<ElementConsumer>();
            if (consumer != null)
            {
                consumer.storeOnConsume = true;
                consumer.elementToConsume = SimHashes.CarbonDioxide;
                consumer.configuration = ElementConsumer.Configuration.Element;
                consumer.capacityKG = 1f;
                consumer.consumptionRadius = 3;
                try
                {
                    var field = typeof(ElementConsumer).GetField("storage", BindingFlags.Instance | BindingFlags.Public);
                    if (field != null && field.FieldType == typeof(Storage))
                        field.SetValue(consumer, co2Storage);
                }
                catch { /* ignore */ }
            }

            // ElementConverter for Carbon production (no inputs; stored output)
            var converter = __result.AddOrGet<ElementConverter>();
            converter.ShowInUI = false;               // hide EC status items
            converter.showDescriptors = false;        // don't add descriptors
            converter.consumedElements = System.Array.Empty<ElementConverter.ConsumedElement>();
            converter.outputElements = new[]
            {
                new ElementConverter.OutputElement(
                    kgPerSecond: 0.0005f,              // base rate at 0 lux
                    element: SimHashes.Dirt,
                    minOutputTemperature: 0f,
                    useEntityTemperature: true,       // use plant temperature
                    storeOutput: true,
                    outputElementOffsetx: 0f,
                    outputElementOffsety: 0f,
                    diseaseWeight: 1f,
                    addedDiseaseIdx: 255,
                    addedDiseaseCount: 0,
                    isActive: true
                )
            };
            // Direct the converter to store into the Carbon storage (not the first Storage on GO)
            converter.SetStorage(carbonStorage);

            // Helpers
            __result.AddOrGet<BlueGrassLightScaler>();             // scales ElementConsumer (CO2) and logs
            __result.AddOrGet<BlueGrassCO2Remover>();              // credits PMT with CO2 mass
            __result.AddOrGet<BlueGrassCarbonConverterController>(); // scales converter + subtracts mass
            __result.AddOrGet<BlueGrassHarvestDropper>();          // drop Carbon on harvest

            // Ensure a PlantMassTracker exists
            __result.AddOrGet<PlantMassTrackerComponent>();
        }
    }

    [HarmonyPatch(typeof(BlueGrass), nameof(BlueGrass.OnSpawn))]
    internal static class BlueGrass_OnSpawn_Patch
    {
        private static void Postfix(BlueGrass __instance)
        {
            if (__instance == null) return;

            var go = __instance.gameObject;

            // PMT init
            var pmt = go.AddOrGet<PlantMassTrackerComponent>();
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null && PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
                pmt.InitializeFromConfig(cfg);

            // Ignore CO2 and Carbon storages in PMT (we credit/debit mass explicitly)
            var storages = ListPool<Storage, GameObject>.Allocate();
            try
            {
                go.GetComponents(storages);
                Tag carbonTag = SimHashes.Dirt.CreateTag();
                Tag co2Tag = SimHashes.CarbonDioxide.CreateTag();
                for (int i = 0; i < storages.Count; i++)
                {
                    var s = storages[i];
                    if (s == null) continue;
                    var filters = s.storageFilters;
                    if (filters != null && (filters.Contains(carbonTag) || filters.Contains(co2Tag)))
                    {
                        pmt.IgnoreAndUnregisterStorage(s);
                    }
                }
            }
            finally
            {
                storages.Recycle();
            }

            // Ensure converter is ready to run (no inputs => we must set canConvert=true)
            var converter = go.GetComponent<ElementConverter>();
            if (converter != null)
                converter.SetAllConsumedActive(true);
        }
    }

    // Scales ElementConsumer.consumptionRate by lux each SIM second (1x → 100x) and logs debug.
    public sealed class BlueGrassLightScaler : KMonoBehaviour, ISim1000ms
    {
        private const float BASE_DOMESTICATED = 1f / 500f; // 0.002 kg/s
        private const float BASE_WILD = 0.0005f;           // 0.0005 kg/s
        private const float MAX_FACTOR = 100f; // scale up to 200x

        private readonly List<ElementConsumer> _consumers = new List<ElementConsumer>();
        [MyCmpGet] private ReceptacleMonitor _receptacle;
        [MyCmpGet] private PrimaryElement _pe;

        public override void OnSpawn()
        {
            base.OnSpawn();
            RefreshConsumers();
        }

        public void Sim1000ms(float dt)
        {
            if (_consumers.Count == 0) RefreshConsumers();
            if (_consumers.Count == 0 || GetComponent<BlueGrass>() == null) return;

            float baseRate = (_receptacle != null && _receptacle.Replanted) ? BASE_DOMESTICATED : BASE_WILD;
            int cell = Grid.PosToCell(transform.GetPosition());
            int lux = (Grid.IsValidCell(cell) ? Grid.LightIntensity[cell] : 0);
            float t = Mathf.Clamp01(lux / 200000f);
            float mult = Mathf.Lerp(1f, MAX_FACTOR, t);
            float desired = baseRate * mult;

            var actualRates = new List<string>(_consumers.Count);
            for (int i = 0; i < _consumers.Count; i++)
            {
                var c = _consumers[i];
                if (c == null) { actualRates.Add("n/a"); continue; }

                bool needsReinit =
                    Mathf.Abs(c.consumptionRate - desired) > 1e-06f ||
                    c.capacityKG < 1f - 1e-06f ||
                    c.consumptionRadius < 3;

                if (needsReinit)
                {
                    c.EnableConsumption(false);
                    c.storeOnConsume = true;
                    c.elementToConsume = SimHashes.CarbonDioxide;
                    c.configuration = ElementConsumer.Configuration.Element;
                    c.capacityKG = 1f;
                    c.consumptionRadius = 3;
                    c.sampleCellOffset = Vector3.zero;
                    c.consumptionRate = desired;
                    c.EnableConsumption(true);
                }

                actualRates.Add($"{c.consumptionRate:0.#####} ({c.capacityKG:0.#}/{c.consumptionRadius})");
            }

            var remover = GetComponent<BlueGrassCO2Remover>();
            float removedCO2kg = remover != null ? remover.ConsumeRemovedCO2ThisSecond() : 0f;

            var carbonCtrl = GetComponent<BlueGrassCarbonConverterController>();
            float producedCkg = carbonCtrl != null ? carbonCtrl.ConsumeProducedThisSecond() : 0f;

            var pmt = GetComponent<PlantMassTrackerComponent>();
            float tracked = (pmt != null) ? pmt.TrackedMassKg : -1f;
            float pemass = (_pe != null) ? _pe.Mass : -1f;

        
        }

        private void RefreshConsumers()
        {
            _consumers.Clear();
            GetComponents(_consumers);
        }
    }

    // Credits PMT with CO2 mass removed from storage; immediate removal to avoid capacity throttle.
    public sealed class BlueGrassCO2Remover : KMonoBehaviour, ISim1000ms
    {
        private static readonly SimHashes TargetGas = SimHashes.CarbonDioxide;
        private float _spawnGraceSeconds = 1f;

        private readonly List<Storage> _storages = new List<Storage>();
        private bool _subscribed;

        private float _removedCO2ThisSecond;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _spawnGraceSeconds = 1f;

            _storages.Clear();
            GetComponents(_storages);

            if (_storages.Count > 0 && !_subscribed)
            {
                for (int i = 0; i < _storages.Count; i++)
                {
                    var s = _storages[i];
                    if (s == null) continue;
                    s.Subscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
                    s.Subscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
                }
                _subscribed = true;
            }
        }

        public override void OnCleanUp()
        {
            if (_subscribed)
            {
                for (int i = 0; i < _storages.Count; i++)
                {
                    var s = _storages[i];
                    if (s == null) continue;
                    s.Unsubscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
                    s.Unsubscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
                }
                _subscribed = false;
            }

            _storages.Clear();
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            if (_spawnGraceSeconds > 0f)
            {
                _spawnGraceSeconds -= dt;
                _removedCO2ThisSecond = 0f;
                return;
            }

            // Safety sweep in case we missed an event
            for (int sIdx = _storages.Count - 1; sIdx >= 0; sIdx--)
            {
                var storage = _storages[sIdx];
                if (storage == null) continue;
                RemoveCO2FromStorage(storage, accumulate: true);
            }
        }

        private void OnAnyStorageChanged(object _)
        {
            if (_spawnGraceSeconds > 0f) return;

            for (int i = 0; i < _storages.Count; i++)
            {
                var s = _storages[i];
                if (s == null) continue;
                RemoveCO2FromStorage(s, accumulate: true);
            }
        }

        private void RemoveCO2FromStorage(Storage storage, bool accumulate)
        {
            var items = storage.items;
            if (items == null || items.Count == 0) return;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var go = items[i];
                if (go == null) continue;

                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null) continue;

                if (pe.ElementID == TargetGas)
                {
                    // Credit PMT mass equal to CO2 consumed
                    var pmt = GetComponent<PlantMassTrackerComponent>();
                    if (pmt != null && pe.Mass > 0f)
                        pmt.AddExternalMass(pe.Mass);

                    if (accumulate)
                        _removedCO2ThisSecond += pe.Mass;

                    storage.Remove(go, true);
                    Util.KDestroyGameObject(go);
                }
            }
        }

        public float ConsumeRemovedCO2ThisSecond()
        {
            float v = _removedCO2ThisSecond;
            _removedCO2ThisSecond = 0f;
            return v;
        }
    }

    // Controller that scales ElementConverter output by lux and subtracts plant mass by exactly the produced Carbon.
    public sealed class BlueGrassCarbonConverterController : KMonoBehaviour, ISim1000ms
    {
        private const float BASE_PRODUCTION_KG_PER_S = 0.0005f; // at 0 lux
        private const float MAX_FACTOR_AT_100K_LUX = 100f;      // 0.005 -> 0.05

        [MyCmpGet] private ElementConverter _converter;
        [MyCmpGet] private PrimaryElement _pe;
        [MyCmpGet] private WiltCondition _wilt;
        [MyCmpGet] private Growing _growing;

        private PlantMassTrackerComponent _pmt;
        private Storage _carbonStorage;

        // debug accumulator
        private float _producedThisSecond;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _pmt = GetComponent<PlantMassTrackerComponent>();

            // Resolve Carbon storage (filtered by Carbon)
            var storages = ListPool<Storage, BlueGrassCarbonConverterController>.Allocate();
            try
            {
                GetComponents(storages);
                Tag carbonTag = SimHashes.Dirt.CreateTag();
                for (int i = 0; i < storages.Count; i++)
                {
                    var s = storages[i];
                    if (s != null && s.storageFilters != null && s.storageFilters.Contains(carbonTag))
                    {
                        _carbonStorage = s;
                        break;
                    }
                }
            }
            finally
            {
                storages.Recycle();
            }

            if (_converter != null)
            {
                // Ensure converter stores into the Carbon storage and is allowed to run
                if (_carbonStorage != null)
                    _converter.SetStorage(_carbonStorage);

                _converter.OutputMultiplier = 1f;
                _converter.ShowInUI = false;
                _converter.showDescriptors = false;

                // Start disabled; Sim1000ms will enable if conditions allow
                _converter.SetAllConsumedActive(false);
            }
        }

        public void Sim1000ms(float dt)
        {
            if (_converter == null) return;

            // Gate production by plant state: only produce when growing and not wilting
            bool shouldProduce =
                (_growing == null || !_growing.IsGrown()) &&
                (_wilt == null || !_wilt.IsWilting());

            if (!shouldProduce)
            {
                // Pause converter and skip mass subtraction
                if (_converter.OutputMultiplier != 0f)
                    _converter.OutputMultiplier = 0f;
                _converter.SetAllConsumedActive(false);
                return;
            }

            _converter.SetAllConsumedActive(true);

            // Scale converter output by lux
            int cell = Grid.PosToCell(transform.GetPosition());
            int lux = (Grid.IsValidCell(cell) ? Grid.LightIntensity[cell] : 0);
            float t = Mathf.Clamp01(lux / 200000f);
            float prodMult = Mathf.Lerp(1f, MAX_FACTOR_AT_100K_LUX, t);

            _converter.OutputMultiplier = prodMult;

            // Subtract mass equal to Carbon produced this second
            float produced = BASE_PRODUCTION_KG_PER_S * prodMult * Mathf.Max(0.001f, dt);
            if (produced > 0f)
            {
                if (_pmt != null)
                    _pmt.AddExternalMass(-produced);
                else if (_pe != null)
                    _pe.Mass = Mathf.Max(0f, _pe.Mass - produced);

                _producedThisSecond += produced;
            }
        }

        public float ConsumeProducedThisSecond()
        {
            float v = _producedThisSecond;
            _producedThisSecond = 0f;
            return v;
        }
    }

    // Drops all Carbon stored on harvest.
    public sealed class BlueGrassHarvestDropper : KMonoBehaviour
    {
        private Storage _carbonStorage;
        private bool _subscribed;

        public override void OnSpawn()
        {
            base.OnSpawn();

            // Resolve Carbon storage
            var storages = ListPool<Storage, BlueGrassHarvestDropper>.Allocate();
            try
            {
                GetComponents(storages);
                Tag carbonTag = SimHashes.Dirt.CreateTag();
                for (int i = 0; i < storages.Count; i++)
                {
                    var s = storages[i];
                    if (s != null && s.storageFilters != null && s.storageFilters.Contains(carbonTag))
                    {
                        _carbonStorage = s;
                        break;
                    }
                }
            }
            finally
            {
                storages.Recycle();
            }

            if (!_subscribed)
            {
                Subscribe((int)GameHashes.Harvest, OnHarvested);
                _subscribed = true;
            }
        }

        public override void OnCleanUp()
        {
            if (_subscribed)
            {
                Unsubscribe((int)GameHashes.Harvest, OnHarvested);
                _subscribed = false;
            }
            base.OnCleanUp();
        }

        private void OnHarvested(object _)
        {
            if (_carbonStorage == null) return;
            _carbonStorage.DropAll();
        }
    }

    // Keep the SetConsumptionRate postfix to ensure any baseline reset is lux-scaled immediately
    [HarmonyPatch(typeof(BlueGrass), "SetConsumptionRate")]
    internal static class BlueGrass_SetConsumptionRate_Patch
    {
        private static void Postfix(BlueGrass __instance)
        {
            var consumers = ListPool<ElementConsumer, GameObject>.Allocate();
            try
            {
                __instance.GetComponents(consumers);
                if (consumers.Count == 0) return;

                var receptacle = __instance.GetComponent<ReceptacleMonitor>();
                float baseRate = (receptacle != null && receptacle.Replanted) ? (1f / 500f) : 0.0005f;

                int cell = Grid.PosToCell(__instance.transform.GetPosition());
                int lux = (Grid.IsValidCell(cell) ? Grid.LightIntensity[cell] : 0);
                float t = Mathf.Clamp01(lux / 200000f);
                float mult = Mathf.Lerp(1f, 100f, t);
                float desired = baseRate * mult;

                for (int i = 0; i < consumers.Count; i++)
                {
                    var c = consumers[i];
                    if (c == null) continue;
                    c.EnableConsumption(false);
                    c.consumptionRate = desired;
                    c.EnableConsumption(true);
                }
            }
            finally
            {
                consumers.Recycle();
            }
        }
    }
}