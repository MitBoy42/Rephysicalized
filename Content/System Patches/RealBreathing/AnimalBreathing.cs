using HarmonyLib;
using KSerialization;
using Rephysicalized;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches.RealBreathing
{
    public sealed class AnimalBreathing : KMonoBehaviour, ISim1000ms
    {
        [Serialize] private float OxygenCapacityKg = 1.2f;
        [Serialize] private float BreathingRateKgPerSecond = 0.002f;
        private const float CO2CapacityKg = 1f;
        private static readonly Tag CarbonDioxideTag = SimHashes.CarbonDioxide.CreateTag();
        internal static bool SuppressInitialOxygenForGrowUp;

        [Serialize] private bool initializedOxygen;
        [Serialize] private bool seedInitialOxygen = true;

        private Storage oxygenStorage;
        private Storage co2Storage;
        private KPrefabID prefabID;

        public bool IsOutOfOxygen => oxygenStorage == null || oxygenStorage.GetMassAvailable(ModTags.OxidizerGas) <= 0f;

        public static void ConfigurePrefab(GameObject prefab, bool seedInitialOxygen = true, float oxygenCapacityKg = 1.2f, float breathingRateKgPerSecond = 0.002f)
        {
            if (prefab == null)
                return;

            if (!Config.Instance.RealBreathing)
                return;

            var existingBreathing = prefab.GetComponent<AnimalBreathing>();
            if (existingBreathing != null)
            {
                existingBreathing.seedInitialOxygen = seedInitialOxygen;
                existingBreathing.OxygenCapacityKg = oxygenCapacityKg;
                existingBreathing.BreathingRateKgPerSecond = breathingRateKgPerSecond;

                var existingOxygenStorage = prefab.GetComponents<Storage>().FirstOrDefault(storage =>
                    storage != null && storage.storageFilters != null && storage.storageFilters.Contains(ModTags.OxidizerGas));
                if (existingOxygenStorage != null)
                    existingOxygenStorage.capacityKg = oxygenCapacityKg;

                prefab.AddOrGet<AnimalSuffocation>();
                return;
            }

            var oxygenStorage = prefab.AddComponent<Storage>();
            oxygenStorage.capacityKg = oxygenCapacityKg;
            oxygenStorage.storageFilters = new List<Tag> { ModTags.OxidizerGas, CarbonDioxideTag };
            oxygenStorage.allowItemRemoval = false;
            oxygenStorage.showInUI = true;
            oxygenStorage.showDescriptor = true;

            var co2Storage = prefab.AddComponent<Storage>();
            co2Storage.capacityKg = CO2CapacityKg;
            co2Storage.storageFilters = new List<Tag> { CarbonDioxideTag };
            co2Storage.allowItemRemoval = false;
            co2Storage.showInUI = true;
            co2Storage.showDescriptor = true;

            var consumer = prefab.AddComponent<DualGasElementConsumer>();
            consumer.consumptionRate = breathingRateKgPerSecond * 20f;
            consumer.consumptionRadius = 1;
            consumer.capacityKG = oxygenCapacityKg;
            consumer.storeOnConsume = true;
            consumer.storage = oxygenStorage;
            consumer.isRequired = true;
            consumer.showInStatusPanel = false;
            consumer.showDescriptor = false;

            var breathing = prefab.AddComponent<AnimalBreathing>();
            breathing.seedInitialOxygen = seedInitialOxygen;
            breathing.OxygenCapacityKg = oxygenCapacityKg;
            breathing.BreathingRateKgPerSecond = breathingRateKgPerSecond;
            prefab.AddOrGet<AnimalSuffocation>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            prefabID = GetComponent<KPrefabID>();
            BindStorage();
            Subscribe((int)GameHashes.BabyToAdult, OnBabyToAdult);

            var consumer = GetComponent<DualGasElementConsumer>();
            if (consumer != null && oxygenStorage != null)
            {
                consumer.storage = oxygenStorage;
                consumer.isRequired = true;
            }

            if (!initializedOxygen && oxygenStorage != null)
            {
                bool isBaby = AnimalBreathingList.IsBaby(prefabID);
                if (seedInitialOxygen && !isBaby && !SuppressInitialOxygenForGrowUp && oxygenStorage.GetMassAvailable(ModTags.OxidizerGas) <= 0f)
                {
                    var primaryElement = GetComponent<PrimaryElement>();
                    float temperature = primaryElement != null ? primaryElement.Temperature : 293.15f;
                    oxygenStorage.AddGasChunk(SimHashes.Oxygen, OxygenCapacityKg, temperature, byte.MaxValue, 0, true);
                }
                initializedOxygen = true;
            }
        }

        private void OnBabyToAdult(object data)
        {
            if (!AnimalBreathingList.IsBaby(prefabID) || !(data is GameObject adult))
                return;

            var adultBreathing = adult.GetComponent<AnimalBreathing>();
            if (adultBreathing == null)
                return;

            adultBreathing.BindStorage();
            if (adultBreathing.oxygenStorage == null || adultBreathing.co2Storage == null)
                return;

            ClearStoredGas(adultBreathing.oxygenStorage, ModTags.OxidizerGas);
            TransferStoredGas(oxygenStorage, adultBreathing.oxygenStorage, ModTags.OxidizerGas);
            TransferStoredGas(oxygenStorage, adultBreathing.co2Storage, CarbonDioxideTag);
            TransferStoredGas(co2Storage, adultBreathing.co2Storage, CarbonDioxideTag);
        }

        private static void ClearStoredGas(Storage storage, Tag tag)
        {
            float mass = storage.GetMassAvailable(tag);
            if (mass > 0f)
                storage.ConsumeAndGetDisease(tag, mass, out _, out _, out _);
        }

        private static void TransferStoredGas(Storage source, Storage destination, Tag tag)
        {
            float mass = source.GetMassAvailable(tag);
            if (mass > 0f)
                source.TransferMass(destination, tag, mass, hide_popups: true);
        }

        public void Sim1000ms(float dt)
        {
            if (!Config.Instance.RealBreathing)
                return;
            if (dt <= 0f || oxygenStorage == null || co2Storage == null || prefabID == null || prefabID.HasTag(GameTags.Dead))
                return;

            ConvertOxidizerToCarbonDioxide(dt);
            TransferAndVentCarbonDioxide();
        }

        private void ConvertOxidizerToCarbonDioxide(float dt)
        {
            float requestedMass = Mathf.Min(BreathingRateKgPerSecond * dt, oxygenStorage.GetMassAvailable(ModTags.OxidizerGas));
            if (requestedMass <= 0f)
                return;

            oxygenStorage.ConsumeAndGetDisease(ModTags.OxidizerGas, requestedMass,
                out float consumedMass, out Klei.SimUtil.DiseaseInfo diseaseInfo, out float temperature);
            if (consumedMass > 0f)
                co2Storage.AddGasChunk(SimHashes.CarbonDioxide, consumedMass, temperature, diseaseInfo.idx, diseaseInfo.count, true);
        }

        private void TransferAndVentCarbonDioxide()
        {
            float storedCO2 = co2Storage.GetMassAvailable(CarbonDioxideTag);
            float room = Mathf.Max(0f, CO2CapacityKg - storedCO2);
            float producedCO2 = oxygenStorage.GetMassAvailable(CarbonDioxideTag);
            float transferMass = Mathf.Min(room, producedCO2);
            if (transferMass > 0f)
                oxygenStorage.TransferMass(co2Storage, CarbonDioxideTag, transferMass, hide_popups: true);

            while (co2Storage.GetMassAvailable(CarbonDioxideTag) >= CO2CapacityKg)
                ExpelCarbonDioxide();
        }

        private void ExpelCarbonDioxide()
        {
            Vector3 position = transform.GetPosition() + new Vector3(0f, 0f, 0f);
            int cell = Grid.PosToCell(position);
            if (!Grid.IsValidCell(cell))
                return;

            co2Storage.ConsumeAndGetDisease(CarbonDioxideTag, CO2CapacityKg, out float consumedMass, out Klei.SimUtil.DiseaseInfo diseaseInfo, out float temperature);
            if (consumedMass <= 0f)
                return;

            if (CO2Manager.instance != null)
            {
                var facing = GetComponent<Facing>();
                CO2Manager.instance.SpawnBreath(position, consumedMass, temperature, facing != null && facing.GetFacing());
            }
            else
            {
                SimMessages.AddRemoveSubstance(cell, SimHashes.CarbonDioxide, CellEventLogger.Instance.OxygenModifierSimUpdate,
                    consumedMass, temperature, diseaseInfo.idx, diseaseInfo.count);
            }
        }

        internal Storage GetOxygenStorage()
        {
            if (oxygenStorage == null)
                BindStorage();
            return oxygenStorage;
        }

        private void BindStorage()
        {
            var storages = GetComponents<Storage>();
            oxygenStorage = storages.FirstOrDefault(storage =>
                storage != null && storage.storageFilters != null && storage.storageFilters.Contains(ModTags.OxidizerGas));
            co2Storage = storages.FirstOrDefault(storage =>
                storage != null && storage.storageFilters != null && storage.storageFilters.Contains(CarbonDioxideTag) &&
                !storage.storageFilters.Contains(ModTags.OxidizerGas));
        }
    }

    [HarmonyPatch(typeof(BabyMonitor.Instance), nameof(BabyMonitor.Instance.SpawnAdult))]
    internal static class BabyMonitor_SpawnAdult_AnimalBreathing_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(BabyMonitor.Instance __instance, out bool __state)
        {
            __state = AnimalBreathing.SuppressInitialOxygenForGrowUp;
            if (__instance != null && __instance.gameObject.GetComponent<AnimalBreathing>() != null)
                AnimalBreathing.SuppressInitialOxygenForGrowUp = true;
        }

        [HarmonyPostfix]
        private static void Postfix(bool __state)
        {
            AnimalBreathing.SuppressInitialOxygenForGrowUp = __state;
        }
    }
}
