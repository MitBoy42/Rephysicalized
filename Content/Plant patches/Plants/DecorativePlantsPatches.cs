using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    internal static class DecorativePlantAirFilterHelper
    {
        private const float Rate = 0.005f;

        public static void Apply(GameObject go)
        {
            if (go == null)
                return;

            var storage = go.AddOrGet<Storage>();
            if (storage != null)
            {
                storage.capacityKg = 0.1f;
                storage.showInUI = false;
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            }

            var consumer = go.AddOrGet<ElementConsumer>();
            consumer.elementToConsume = SimHashes.ContaminatedOxygen;
            consumer.consumptionRate = Rate;
            consumer.capacityKG = Rate;
            consumer.consumptionRadius = 3;
            consumer.showInStatusPanel = false;
            consumer.sampleCellOffset = Vector3.zero;
            consumer.isRequired = false;
            consumer.storeOnConsume = true;
            consumer.showDescriptor = false;
            consumer.ignoreActiveChanged = true;

            var converter = go.AddOrGet<ElementConverter>();
            converter.consumedElements = new ElementConverter.ConsumedElement[]
            {
                new ElementConverter.ConsumedElement(new Tag("ContaminatedOxygen"), Rate)
            };
            converter.outputElements = new ElementConverter.OutputElement[]
            {
                new ElementConverter.OutputElement(Rate, SimHashes.Oxygen, 0.0f, diseaseWeight: 1f)
            };

            go.AddOrGet<DecorativePlantAirFilter>();
        }
    }

    [HarmonyPatch(typeof(LeafyPlantConfig), nameof(LeafyPlantConfig.CreatePrefab))]
    internal static class LeafyPlantConfig_CreatePrefab_DecorativeAirFilter_Patch
    {
        private static void Postfix(GameObject __result) => DecorativePlantAirFilterHelper.Apply(__result);
    }

    [HarmonyPatch(typeof(CylindricaConfig), nameof(CylindricaConfig.CreatePrefab))]
    internal static class CylindricaConfig_CreatePrefab_DecorativeAirFilter_Patch
    {
        private static void Postfix(GameObject __result) => DecorativePlantAirFilterHelper.Apply(__result);
    }

    [HarmonyPatch(typeof(GardenDecorPlantConfig), nameof(GardenDecorPlantConfig.CreatePrefab))]
    internal static class GardenDecorPlantConfig_CreatePrefab_DecorativeAirFilter_Patch
    {
        private static void Postfix(GameObject __result) => DecorativePlantAirFilterHelper.Apply(__result);
    }

    [HarmonyPatch(typeof(ToePlantConfig), nameof(ToePlantConfig.CreatePrefab))]
    internal static class ToePlantConfig_CreatePrefab_DecorativeAirFilter_Patch
    {
        private static void Postfix(GameObject __result) => DecorativePlantAirFilterHelper.Apply(__result);
    }

    public sealed class DecorativePlantAirFilter : KMonoBehaviour
    {
        private ElementConsumer consumer;
        private ElementConverter converter;

        public override void OnSpawn()
        {
            base.OnSpawn();

            consumer = GetComponent<ElementConsumer>();
            converter = GetComponent<ElementConverter>();

            Subscribe(-724860998, OnWilt);
            Subscribe(712767498, OnWiltRecover);

            var wilt = GetComponent<WiltCondition>();
            bool wilted = wilt != null && wilt.IsWilting();
            SetActive(!wilted);
        }

        private void OnWilt(object data = null) => SetActive(false);

        private void OnWiltRecover(object data = null) => SetActive(true);

        private void SetActive(bool active)
        {
            if (consumer != null)
                consumer.EnableConsumption(active);
            if (converter != null)
                converter.enabled = active;
        }
    }
}
