using System;
using System.Collections.Generic;
using KSerialization;
using Klei.AI;
using UnityEngine;
using STRINGS;
using HarmonyLib;

namespace Rephysicalized.Content.Plant_patches
{
    [Serializable]
    public sealed class MultiFertilizerInfo
    {
        public Tag tag;
        public float massConsumptionRate;
        public float storageCapacity;
        public bool showStorageInUI = true;

        public MultiFertilizerInfo(Tag tag, float massConsumptionRate, float storageCapacity = 0f)
        {
            this.tag = tag;
            this.massConsumptionRate = massConsumptionRate;
            this.storageCapacity = storageCapacity > 0f ? storageCapacity : massConsumptionRate * 600f * 3f;
        }
    }

    public static class ExtendPlantToMultiFertilizable
    {
        public static GameObject Extend(GameObject template, params MultiFertilizerInfo[] fertilizers)
        {
            if (template == null)
                return null;
            if (fertilizers == null || fertilizers.Length == 0)
                throw new ArgumentException("At least one fertilizer is required.", nameof(fertilizers));

            var modifiers = template.GetComponent<Modifiers>();
            if (modifiers != null)
                modifiers.initialAttributes.Add(Db.Get().PlantAttributes.FertilizerUsageMod.Id);

            var monitor = template.AddOrGet<MultiFertilizationMonitor>();
            monitor.Configure(fertilizers);
            var consumedElements = new PlantElementAbsorber.ConsumeInfo[fertilizers.Length];
            for (int i = 0; i < fertilizers.Length; i++)
                consumedElements[i] = new PlantElementAbsorber.ConsumeInfo(fertilizers[i].tag, fertilizers[i].massConsumptionRate);
            template.AddOrGetDef<FertilizationMonitor.Def>().consumedElements = consumedElements;
            var choreType = Db.Get().ChoreTypes.FarmFetch.IdHash;

            foreach (var fertilizer in fertilizers)
            {
                if (!fertilizer.tag.IsValid || fertilizer.massConsumptionRate <= 0f)
                    throw new ArgumentException("Fertilizer tags must be valid and consumption rates must be positive.", nameof(fertilizers));

                var delivery = template.AddComponent<ManualDeliveryKG>();
                delivery.RequestedItemTag = fertilizer.tag;
                delivery.capacity = fertilizer.massConsumptionRate * 600f * 3f;
                delivery.refillMass = fertilizer.massConsumptionRate * 600f * 0.5f;
                delivery.MinimumMass = fertilizer.massConsumptionRate * 600f * 0.5f;
                delivery.operationalRequirement = Operational.State.Functional;
                delivery.choreTypeIDHash = choreType;
                delivery.Pause(true, "init");
            }

            return template;
        }

        public static GameObject Extend(GameObject template, params PlantElementAbsorber.ConsumeInfo[] fertilizers)
        {
            if (fertilizers == null)
                throw new ArgumentNullException(nameof(fertilizers));
            var infos = new MultiFertilizerInfo[fertilizers.Length];
            for (int i = 0; i < fertilizers.Length; i++)
                infos[i] = new MultiFertilizerInfo(fertilizers[i].tag, fertilizers[i].massConsumptionRate);
            return Extend(template, infos);
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class MultiFertilizerStorage : Storage
    {
        [Serialize]
        public Tag consumedTag;

        internal void Configure(MultiFertilizerInfo info)
        {
            consumedTag = info.tag;
            capacityKg = info.storageCapacity;
            showInUI = info.showStorageInUI;
            allowItemRemoval = true;
            useGunForDelivery = false;
            doDiseaseTransfer = true;
            storageFilters = new List<Tag> { info.tag };
            SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
        }
    }

    public sealed class MultiFertilizationMonitor : KMonoBehaviour, IWiltCause, ISim1000ms
    {
        [Serialize]
        private MultiFertilizerInfo[] fertilizers;
        [Serialize]
        private Tag[] fertilizerTags;
        [Serialize]
        private float[] fertilizerRates;
        private ManualDeliveryKG[] deliveries;
        private Storage planterStorage;
        private HandleVector<int>.Handle[] absorberHandles;
        private bool active;
        private bool initialized;

        public void Configure(MultiFertilizerInfo[] infos)
        {
            fertilizers = infos;
            fertilizerTags = new Tag[infos.Length];
            fertilizerRates = new float[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                fertilizerTags[i] = infos[i].tag;
                fertilizerRates[i] = infos[i].massConsumptionRate;
            }
        }
        WiltCondition.Condition[] IWiltCause.Conditions => new[] { WiltCondition.Condition.Fertilized };
        string IWiltCause.WiltStateString => CREATURES.STATUSITEMS.NEEDSFERTILIZER.NAME;

        public override void OnSpawn()
        {
            base.OnSpawn();
            deliveries = GetComponents<ManualDeliveryKG>();
            Subscribe(1309017699, new Action<object>(OnPlanterStorageChanged));
            Subscribe((int)GameHashes.Wilt, new Action<object>(OnWiltStateChanged));
            Subscribe((int)GameHashes.WiltRecover, new Action<object>(OnWiltStateChanged));
            if ((fertilizers == null || fertilizers.Length == 0) && fertilizerTags != null && fertilizerRates != null && fertilizerTags.Length == fertilizerRates.Length)
            {
                fertilizers = new MultiFertilizerInfo[fertilizerTags.Length];
                for (int i = 0; i < fertilizers.Length; i++)
                    fertilizers[i] = new MultiFertilizerInfo(fertilizerTags[i], fertilizerRates[i]);
            }

            if (fertilizers == null || fertilizers.Length == 0)
            {
                var stateMachine = GetComponent<StateMachineController>();
                var definition = stateMachine != null ? stateMachine.GetDef<FertilizationMonitor.Def>() : null;
                var consumedElements = definition?.consumedElements;
                if (consumedElements != null)
                {
                    fertilizers = new MultiFertilizerInfo[consumedElements.Length];
                    for (int i = 0; i < fertilizers.Length; i++)
                        fertilizers[i] = new MultiFertilizerInfo(consumedElements[i].tag, consumedElements[i].massConsumptionRate);
                }
            }

            absorberHandles = new HandleVector<int>.Handle[fertilizers?.Length ?? 0];
            for (int i = 0; i < absorberHandles.Length; i++)
                absorberHandles[i] = HandleVector<int>.Handle.InvalidHandle;

            if (planterStorage == null)
                planterStorage = GetComponent<ReceptacleMonitor>()?.GetReceptacle()?.storage;
            initialized = true;
            if (planterStorage != null)
                SetStorage(planterStorage);

            UpdateAbsorption();
        }

        public void Sim1000ms(float dt)
        {
            EnsureDeliveriesReady();
            UpdateAbsorption();
        }

        private void UpdateAbsorption()
        {
            if (!initialized || absorberHandles == null || fertilizers == null)
                return;

            WiltCondition wiltCondition = GetComponent<WiltCondition>();
            bool shouldAbsorb = HasAllFertilizers() && (wiltCondition == null || !wiltCondition.IsWilting());
            if (shouldAbsorb == active)
                return;

            active = shouldAbsorb;
            if (active)
            {
                for (int i = 0; i < fertilizers.Length; i++)
                {
                    var info = fertilizers[i];
                    absorberHandles[i] = Game.Instance.plantElementAbsorbers.Add(planterStorage, new[] { new PlantElementAbsorber.ConsumeInfo(info.tag, info.massConsumptionRate) });
                    for (int j = 0; j < deliveries.Length; j++)
                        if (deliveries[j].RequestedItemTag == info.tag)
                            deliveries[j].Pause(false, "fertilized");
                }
            }
            else
            {
                for (int i = 0; i < absorberHandles.Length; i++)
                {
                    if (absorberHandles[i].IsValid())
                        Game.Instance.plantElementAbsorbers.Remove(absorberHandles[i]);
                    absorberHandles[i] = HandleVector<int>.Handle.InvalidHandle;
                }
                for (int i = 0; i < deliveries.Length; i++)
                    deliveries[i].Pause(false, "starved");
            }
        }

        public void SetStorage(Storage storage)
        {
            planterStorage = storage;
            if (deliveries != null)
                for (int i = 0; i < deliveries.Length; i++)
                {
                    deliveries[i].SetStorage(storage);
                }
            EnsureDeliveriesReady();
            if (initialized)
                UpdateAbsorption();
        }

        private void EnsureDeliveriesReady()
        {
            if (planterStorage == null || deliveries == null)
                return;

            for (int i = 0; i < deliveries.Length; i++)
            {
                var delivery = deliveries[i];
                bool needsRefresh = !delivery.enabled || delivery.IsPaused;
                delivery.enabled = true;
                if (delivery.IsPaused)
                    delivery.Pause(false, "planter storage assigned");
                if (needsRefresh)
                    delivery.UpdateDeliveryState();
            }
        }

        private void OnPlanterStorageChanged(object data)
        {
            SetStorage(data as Storage);
        }

        private void OnWiltStateChanged(object data)
        {
            UpdateAbsorption();
        }

        private bool HasAllFertilizers()
        {
            if (fertilizers == null || planterStorage == null)
                return false;
            for (int i = 0; i < fertilizers.Length; i++)
                if (planterStorage.GetMassAvailable(fertilizers[i].tag) < fertilizers[i].massConsumptionRate * 0.2f)
                    return false;
            return true;
        }

        public bool HasAllFertilizersAvailable => HasAllFertilizers();

        public override void OnCleanUp()
        {
            for (int i = 0; absorberHandles != null && i < absorberHandles.Length; i++)
                if (absorberHandles[i].IsValid())
                    Game.Instance.plantElementAbsorbers.Remove(absorberHandles[i]);
            base.OnCleanUp();
        }
    }

    [HarmonyPatch(typeof(PlantablePlot), "RegisterWithPlant")]
    internal static class PlantablePlot_RegisterWithPlant_MultiFertilizer_Patch
    {
        private static void Postfix(PlantablePlot __instance, GameObject plant)
        {
            plant?.GetComponent<MultiFertilizationMonitor>()?.SetStorage(__instance.storage);
        }
    }

    [HarmonyPatch(typeof(FertilizationMonitor.Instance), nameof(FertilizationMonitor.Instance.StartAbsorbing))]
    internal static class FertilizationMonitor_StartAbsorbing_MultiFertilizer_Patch
    {
        private static bool Prefix(FertilizationMonitor.Instance __instance)
        {
            return __instance.gameObject.GetComponent<MultiFertilizationMonitor>() == null;
        }
    }

    [HarmonyPatch(typeof(FertilizationMonitor.Instance), nameof(FertilizationMonitor.Instance.UpdateFertilization))]
    internal static class FertilizationMonitor_UpdateFertilization_MultiFertilizer_Patch
    {
        private static bool Prefix(FertilizationMonitor.Instance __instance)
        {
            var monitor = __instance.gameObject.GetComponent<MultiFertilizationMonitor>();
            if (monitor == null)
                return true;

            bool ready = monitor.HasAllFertilizersAvailable;
            __instance.sm.isFertilized.Set(ready, __instance.smi);
            return false;
        }
    }
}
