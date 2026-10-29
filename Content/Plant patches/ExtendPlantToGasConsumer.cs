using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;
using STRINGS;

namespace Rephysicalized.Content.Plant_patches
{

    /// Flow:
    ///   environment cell --(ElementConsumer)--> Storage on the plant --(this component)--> plant growth
    /// Deliberately different from EntityTemplates.ExtendPlantToFertilizable/Irrigated:
    ///   * No ManualDeliveryKG, no FarmFetch chore - nothing is delivered by a duplicant.
    ///   * Storage and ElementConsumer live on the plant itself rather than on the farm plot it is planted in.
    [Serializable]
    public sealed class PlantGasConsumerInfo
    {
        public Tag elementTag;

        public float massConsumptionRate = 0.02f;

        public Vector3 consumerSampleOffset = new Vector3(0f, 1f, 0f);

        public float storageCapacityKg = 1f;

        /// Minimum gas (kg) that must remain in the internal storage for the plant to stay healthy.
        public float requiredMassKg = 0.02f;
        /// <summary>Optional draw multiplier override. If unset, the PlantGasConsumer component value is used.</summary>
        public float drawRateMultiplier;

        public byte consumptionRadius = 3;
        public bool showStorageInUI = true;

        public string wiltStateString = STRINGS.STATUSITEMS.NOGAS;

        /// <summary>
        /// Whether the plant wilts when its gas buffer runs dry. Set false for plants that treat gas as a
        /// growth bonus rather than a requirement - an empty buffer then simply stops growth.
        /// </summary>
        public bool wiltsWhenGasDepleted = true;

        public PlantGasConsumerInfo(Tag elementTag, float massConsumptionRate)
        {
            this.elementTag = elementTag;
            this.massConsumptionRate = massConsumptionRate;
        }

        public static PlantGasConsumerInfo Create(
            Tag elementTag,
            float massConsumptionRate,
            float storageCapacityKg = 1f,
            float requiredMassKg = 0.05f)
        {
            return new PlantGasConsumerInfo(elementTag, massConsumptionRate)
            {
                storageCapacityKg = storageCapacityKg,
                requiredMassKg = requiredMassKg
            };
        }
    }

    public static class ExtendPlantToGasConsumer
    {

        public static GameObject Extend(GameObject template, PlantGasConsumerInfo info)
        {
            if (template == null)
                return null;
      

            var element = ElementLoader.FindElementByTag(info.elementTag);
         
            // Internal gas buffer lives on the plant itself, NOT on the farm plot it is planted in.
            // capacityKg also gates the ElementConsumer via its own capacityKG, so drawing pauses
            // once the buffer is full and resumes automatically when this component drains it.
            var storage = template.GetComponent<PlantGasConsumerStorage>();
            if (storage == null)
                storage = template.AddOrGet<PlantGasConsumerStorage>();
            storage.Configure(info);

            var consumer = template.AddOrGet<ElementConsumer>();
            consumer.elementToConsume = element.id;
            consumer.configuration = ElementConsumer.Configuration.Element;
            consumer.consumptionRate = info.massConsumptionRate;
            consumer.capacityKG = info.storageCapacityKg;
            consumer.consumptionRadius = info.consumptionRadius;
            consumer.sampleCellOffset = info.consumerSampleOffset;
            consumer.showInStatusPanel = true;
            consumer.isRequired = false;
            consumer.ignoreActiveChanged = true;
            consumer.storeOnConsume = false;

            var monitor = template.AddOrGet<PlantGasConsumer>();
            monitor.Configure(info);

            return template;
        }

        public static GameObject Extend(GameObject template, Tag elementTag, float massConsumptionRate)
        {
            return Extend(template, new PlantGasConsumerInfo(elementTag, massConsumptionRate));
        }
    }

    /// <summary>
    /// The plant's internal gas buffer. A dedicated Storage subclass so a gas plant can be identified
    /// reliably (there is only ever one per plant) without scanning every Storage on the GameObject.
    /// </summary>
    [AddComponentMenu("KMonoBehaviour/Workable/PlantGasConsumerStorage")]
    public sealed class PlantGasConsumerStorage : Storage
    {
        [Serialize]
        public Tag consumedElementTag;

        internal void Configure(PlantGasConsumerInfo info)
        {
            consumedElementTag = info.elementTag;
            capacityKg = info.storageCapacityKg;
            showInUI = info.showStorageInUI;
            allowItemRemoval = true;
            useGunForDelivery = false;
            doDiseaseTransfer = true;
            storageFilters = new List<Tag> { info.elementTag };
            SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
        }
    }

    /// <summary>
    /// Drains the plant's internal gas buffer at the plant's consumption rate and reports starvation to
    /// WiltCondition via the AtmosphereElement condition (mirrors how IrrigationMonitor reports a dry plot).
    /// The ElementConsumer is intentionally left enabled always - only the *buffer* running dry wilts the plant.
    /// </summary>
    public sealed class PlantGasConsumer : KMonoBehaviour, ISim1000ms, IWiltCause
    {
        private const float WildConsumptionMultiplier = 0.25f;

        [MyCmpGet]
        private PlantGasConsumerStorage storage;

        [MyCmpGet]
        private ReceptacleMonitor receptacle;

        [MyCmpGet]
        private WiltCondition _wiltCondition;

        [MyCmpGet]
        private PlantMassTrackerComponent _massTracker;

        [SerializeField]
        private SimHashes consumedElement = SimHashes.Void;

        [SerializeField]
        private float consumptionRatePerSecond = 0.02f;

        [SerializeField]
        private float baseConsumptionRatePerSecond;

        [SerializeField]
        private float drawRateMultiplier = 4f;

        [SerializeField]
        private float requiredMassKg = 0.05f;

        [SerializeField]
        private string wiltStateString = STRINGS.STATUSITEMS.NOGAS;

        [SerializeField]
        private bool wiltsWhenGasDepleted = true;

        private bool? _wasStarved;

        WiltCondition.Condition[] IWiltCause.Conditions =>
            wiltsWhenGasDepleted
                ? new[] { WiltCondition.Condition.AtmosphereElement }
                : Array.Empty<WiltCondition.Condition>();

        string IWiltCause.WiltStateString => wiltStateString;

        public float AvailableMass => storage != null ? storage.MassStored() : 0f;

        public bool Starved => AvailableMass < requiredMassKg;

        public SimHashes ConsumedElement => consumedElement;


        public float InternalConsumptionRate => consumptionRatePerSecond;

        public bool WiltsWhenGasDepleted => wiltsWhenGasDepleted;

        /// <summary>
        /// Changes the plant's base gas consumption at runtime, e.g. to scale with light.
        ///
        /// Wild-state scaling is applied to this base rate, and the ElementConsumer refill rate is
        /// retuned to preserve the configured draw ratio.
        /// </summary>
        public void SetConsumptionRate(float kgPerSecond)
        {
            if (kgPerSecond <= 0f)
                return;

            baseConsumptionRatePerSecond = kgPerSecond;
            ApplyEffectiveConsumptionRate();
        }

        private void ApplyEffectiveConsumptionRate()
        {
            if (receptacle == null)
                receptacle = GetComponent<ReceptacleMonitor>();

            bool isWild = receptacle != null && !receptacle.Replanted;
            float effectiveRate = baseConsumptionRatePerSecond *
                (isWild ? WildConsumptionMultiplier : 1f);
            bool rateChanged = !Mathf.Approximately(effectiveRate, consumptionRatePerSecond);
            consumptionRatePerSecond = effectiveRate;

            var consumer = GetComponent<ElementConsumer>();
            if (consumer == null)
                return;

            float desired = consumptionRatePerSecond * Mathf.Max(1f, drawRateMultiplier);
            if (!rateChanged && Mathf.Approximately(consumer.consumptionRate, desired))
                return;

            consumer.consumptionRate = desired;
            consumer.EnableConsumption(true);
            consumer.RefreshConsumptionRate();
        }

        internal void Configure(PlantGasConsumerInfo info)
        {
            consumedElement = ElementLoader.GetElementID(info.elementTag);
            baseConsumptionRatePerSecond = info.massConsumptionRate;
            consumptionRatePerSecond = info.massConsumptionRate;
            if (info.drawRateMultiplier > 0f)
                drawRateMultiplier = info.drawRateMultiplier;
            requiredMassKg = info.requiredMassKg;
            wiltStateString = string.IsNullOrEmpty(info.wiltStateString) ? STRINGS.STATUSITEMS.NOGAS : info.wiltStateString;
            wiltsWhenGasDepleted = info.wiltsWhenGasDepleted;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (storage == null)
                storage = GetComponent<PlantGasConsumerStorage>();

            if (receptacle == null)
                receptacle = GetComponent<ReceptacleMonitor>();

            var consumer = GetComponent<ElementConsumer>();
            if (consumer == null)
                return;

            consumedElement = consumer.elementToConsume;



            if (baseConsumptionRatePerSecond <= 0f)
                baseConsumptionRatePerSecond = consumptionRatePerSecond > 0f
                    ? consumptionRatePerSecond
                    : consumer.consumptionRate;


            requiredMassKg = Mathf.Clamp(requiredMassKg, 0f, Mathf.Max(0f, consumer.capacityKG * 0.5f));

            if (string.IsNullOrEmpty(wiltStateString))
                wiltStateString = STRINGS.STATUSITEMS.NOGAS;

            consumer.storeOnConsume = true;
            consumer.storage = storage;
            consumer.EnableConsumption(true);
            ApplyEffectiveConsumptionRate();
            RegisterWithMassTracker();
        }

        private void RegisterWithMassTracker()
        {
            if (storage == null || consumedElement == SimHashes.Void)
                return;

            var go = gameObject;
            var tracker = go.AddOrGet<PlantMassTrackerComponent>();

            if (PlantMassTrackerRegistry.TryGetConfig(go.GetComponent<KPrefabID>().PrefabID().Name, out var cfg))
                tracker.InitializeFromConfig(cfg);

            tracker.RegisterMonitorStorage(storage, new[] { consumedElement.CreateTag() });
        }

        public void Sim1000ms(float dt)
        {
            if (storage == null || consumedElement == SimHashes.Void)
                return;

            // Do not consume internal gas while the plant is wilted.
            bool wilted = _wiltCondition != null && _wiltCondition.IsWilting();

            ApplyEffectiveConsumptionRate();
            float consumed = 0f;
            if (!wilted)
                consumed = Consume(consumptionRatePerSecond * dt);
            if (wiltsWhenGasDepleted)
                UpdateWiltState();
            EnsureConsumerEnabledForRefill();
            RefreshConsumerIfBufferHasRoom();

            if (consumed > 0f)
            {
                if (_massTracker == null)
                    _massTracker = GetComponent<PlantMassTrackerComponent>();
                _massTracker?.AddMassDelta(consumed);
            }
        }

        /// <summary>
        /// Re-enables the refill consumer while the buffer is empty.
        ///
        /// Most plant state machines (Blue Grass, Dew Palm, and others) call EnableConsumption(false)
        /// when they leave their growing state, and wilting is exactly such a transition. That would
        /// mean an empty buffer causes a wilt, the wilt stops the refill, and the plant could never
        /// recover even once the gas came back - a deadlock the plant could not escape.
        ///
        /// Only forced while starved. The plant is eating from an empty buffer then, so letting the
        /// consumer draw cannot over-consume; a full or healthy plant is left under the control of
        /// whatever state machine owns it.
        /// </summary>
        private void EnsureConsumerEnabledForRefill()
        {
            if (!Starved)
                return;

            var consumer = GetComponent<ElementConsumer>();
            if (consumer == null)
                return;

            // EnableConsumption early-outs when already enabled, so this is cheap to call every tick.
            consumer.EnableConsumption(true);
        }


        private void RefreshConsumerIfBufferHasRoom()
        {
            var consumer = GetComponent<ElementConsumer>();
            if (consumer == null || consumer.capacityKG <= 0f)
                return;

            if (AvailableMass >= consumer.capacityKG)
                return;

            var payload = storage.FindPrimaryElement(consumedElement)?.gameObject ?? gameObject;
            storage.Trigger((int)GameHashes.OnStorageChange, payload);
        }

    
        private float Consume(float mass)
        {
            if (mass <= 0f)
                return 0f;

            var items = storage.items;
            if (items == null)
                return 0f;

            float consumed = 0f;
            for (int i = items.Count - 1; i >= 0 && mass > 0f; i--)
            {
                var item = items[i];
                if (item == null)
                    continue;

                var pe = item.GetComponent<PrimaryElement>();
                if (pe == null || pe.ElementID != consumedElement)
                    continue;

                if (pe.Mass <= mass)
                {
                    consumed += pe.Mass;
                    mass -= pe.Mass;
                    storage.Remove(item, true);
                    Util.KDestroyGameObject(item);
                }
                else
                {
                    pe.Mass -= mass;
                    consumed += mass;
                    mass = 0f;
                }
            }

            return consumed;

         
        }
        private void UpdateWiltState()
        {
            bool starved = Starved;
            if (_wasStarved == starved)
                return;

            _wasStarved = starved;
            Trigger((int)(starved ? GameHashes.WrongAtmosphere : GameHashes.CorrectAtmosphere));
        }
    }


}
