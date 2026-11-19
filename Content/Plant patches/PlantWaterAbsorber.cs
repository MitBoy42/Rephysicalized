using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches
{
  
    // Consumer is disabled on prefab; it will be wired and enabled when planted via Irrigation SetStorage.
    [HarmonyPatch(typeof(BasicFabricMaterialPlantConfig), nameof(BasicFabricMaterialPlantConfig.CreatePrefab))]
    internal static class BasicFabricAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 400f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    // Consumer is disabled on prefab; it will be wired and enabled when planted via Irrigation SetStorage.
    [HarmonyPatch(typeof(ColdWheatConfig), nameof(ColdWheatConfig.CreatePrefab))]
    internal static class ColdWheatAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Water;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(CarrotPlantConfig), nameof(CarrotPlantConfig.CreatePrefab))]
    internal static class CarrotPlantAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Ethanol;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }
    [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
    internal static class BeanPlantAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Ethanol;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(ForestTreeConfig), nameof(ForestTreeConfig.CreatePrefab))]
    internal static class ForestTreeAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 2;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 200f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(SwampHarvestPlantConfig), nameof(SwampHarvestPlantConfig.CreatePrefab))]
    internal static class SwampHarvestPlantAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(CritterTrapPlantConfig), nameof(CritterTrapPlantConfig.CreatePrefab))]
    internal static class CritterTrapPlantAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(SpiceVineConfig), nameof(SpiceVineConfig.CreatePrefab))]
    internal static class SpiceVineAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
            consumer.sampleCellOffset = new Vector3(0f, 2f, 0.0f);
        }
    }

    [HarmonyPatch(typeof(FlyTrapPlantConfig), nameof(FlyTrapPlantConfig.CreatePrefab))]
    internal static class FlyTrapPlantAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
            consumer.sampleCellOffset = new Vector3(0f, 2f, 0.0f);
        }
    }

    [HarmonyPatch(typeof(PrickleFlowerConfig), nameof(PrickleFlowerConfig.CreatePrefab))]
    internal static class PrickleFlowerAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Water;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(VineMotherConfig), nameof(VineMotherConfig.CreatePrefab))]
    internal static class VineMotherAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Water;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }
    
    [HarmonyPatch(typeof(OxyfernConfig), nameof(OxyfernConfig.CreatePrefab))]
    internal static class OxyfernAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.Water;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 1;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(SeaLettuceConfig), nameof(SeaLettuceConfig.CreatePrefab))]
    internal static class SeaLettuceAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.SaltWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 2;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;

        }
    }
    [HarmonyPatch(typeof(TubeWormConfig), nameof(TubeWormConfig.CreatePrefab))]
    internal static class TubeWormAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.MurkyBrine;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 2;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }
    [HarmonyPatch(typeof(OxyCoralConfig), nameof(OxyCoralConfig.CreatePrefab))]
    internal static class OxyCoralAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.SaltWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 2;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    [HarmonyPatch(typeof(SeaTreeRootConfig), nameof(SeaTreeRootConfig.CreatePrefab))]
    internal static class SeaTreeRootAbsorber_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            var consumer = __result.AddComponent<PassiveElementConsumer>();
            consumer.elementToConsume = SimHashes.DirtyWater;
            consumer.consumptionRate = 0.5f;
            consumer.consumptionRadius = 2;
            consumer.showDescriptor = false;
            consumer.showInStatusPanel = false;
            consumer.capacityKG = 100f;
            consumer.storeOnConsume = true;
            consumer.enabled = false;
        }
    }

    internal static class LiquidConsumerWireUtil
    {
        public static void WireLiquidPECs(GameObject plant, Storage plotStorage)
        {
            if (plant == null) return;

            var consumers = plant.GetComponents<PassiveElementConsumer>();
            if (consumers == null || consumers.Length == 0) return;

            for (int i = 0; i < consumers.Length; i++)
            {
                var c = consumers[i];
                if (c == null) continue;

                // Only wire liquid consumers
                var elem = ElementLoader.FindElementByHash(c.elementToConsume);
                if (elem == null || !elem.IsLiquid) continue;

                if (plotStorage == null)
                {
                    c.EnableConsumption(false);
                    c.enabled = false;
                    c.storeOnConsume = false;
                    c.storage = null;
                    ClearLimiter(plant, c, c.elementToConsume, Mathf.Max(0.1f, c.capacityKG));
                    continue;
                }

                // Wire to plot storage and enable
                c.storeOnConsume = true;
                c.storage = plotStorage;
                c.showDescriptor = false;
                c.showInStatusPanel = false;
            
                c.isRequired = false;

                c.EnableConsumption(true);
                c.enabled = true;

                // Attach limiter to enforce per-plant cap in the plot storage
                EnsureLimiter(plant, c, plotStorage, c.elementToConsume, Mathf.Max(0.1f, c.capacityKG));
            }
        }

        private static void ClearLimiter(GameObject plant, PassiveElementConsumer consumer, SimHashes element, float capKg)
        {
            var existing = plant.GetComponents<LiquidPECStorageLimiter>();
            if (existing == null) return;

            for (int i = 0; i < existing.Length; i++)
            {
                var limiter = existing[i];
                if (limiter != null && limiter.Consumer == consumer)
                    limiter.Configure(consumer, null, element, capKg);
            }
        }

        private static void EnsureLimiter(GameObject plant, PassiveElementConsumer consumer, Storage plotStorage, SimHashes element, float capKg)
        {
            // Reuse existing limiter for this consumer if present
            var existing = plant.GetComponents<LiquidPECStorageLimiter>();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    var lim = existing[i];
                    if (lim != null && lim.Consumer == consumer)
                    {
                        lim.Configure(consumer, plotStorage, element, capKg);
                        lim.ApplyNow();
                        return;
                    }
                }
            }

            var limiter = plant.AddComponent<LiquidPECStorageLimiter>();
            limiter.Configure(consumer, plotStorage, element, capKg);
            limiter.ApplyNow(); // immediate evaluation so the consumer starts/stops right away
        }
    }

    /// <summary>
    /// Enforces a per-plant cap for a liquid PassiveElementConsumer when storing into plot storage,
    /// by toggling the consumer on/off based on the element mass in the plot storage.

    /// </summary>
    public sealed class LiquidPECStorageLimiter : KMonoBehaviour, ISim4000ms
    {
        private static readonly HashSet<LiquidPECStorageLimiter> activeLimiters = new HashSet<LiquidPECStorageLimiter>();

        public PassiveElementConsumer Consumer { get; private set; }
        private Storage _plotStorage;
        private Tag _elementTag;
        private SimHashes _elementHash;
        private float _capKg = 100f;

        // Optional small hysteresis to reduce rapid toggling
        private const float Hysteresis = 1.0f;

        public void Configure(PassiveElementConsumer consumer, Storage plotStorage, SimHashes element, float capKg)
        {
            activeLimiters.Add(this);
            Consumer = consumer;
            _plotStorage = plotStorage;
            _elementHash = element;
            _elementTag = element.CreateTag();
            _capKg = Mathf.Max(0.1f, capKg);
        }

        public override void OnCleanUp()
        {
            activeLimiters.Remove(this);
            base.OnCleanUp();
        }

        internal static void DetachFromStorage(Storage storage)
        {
            if (storage == null) return;

            foreach (var limiter in activeLimiters)
            {
                if (limiter == null || limiter._plotStorage != storage) continue;

                var consumer = limiter.Consumer;
                if (consumer != null && consumer.storage == storage)
                {
                    consumer.EnableConsumption(false);
                    consumer.enabled = false;
                    consumer.storeOnConsume = false;
                    consumer.storage = null;
                }

                limiter._plotStorage = null;
            }
        }

        public void ApplyNow()
        {
            EvaluateAndToggle();
        }

        public void Sim4000ms(float dt)
        {
            EvaluateAndToggle();
        }

        private void EvaluateAndToggle()
        {
            if (Consumer == null || _plotStorage == null) return;

            float mass = GetElementMassInStorage(_plotStorage, _elementTag, _elementHash);

            if (mass >= _capKg)
            {
                if (Consumer.enabled)
                {
                    Consumer.EnableConsumption(false);
                    Consumer.enabled = false;
                }
            }
            else if (mass <= (_capKg - Hysteresis))
            {
                if (!Consumer.enabled)
                {
                    Consumer.EnableConsumption(true);
                    Consumer.enabled = true;
                }
            }
        }

        private static float GetElementMassInStorage(Storage storage, Tag elementTag, SimHashes elementHash)
        {
            float amt = storage.GetAmountAvailable(elementTag);
            if (!float.IsNaN(amt) && amt >= 0f)
                return amt;

            float sum = 0f;
            var items = storage.items;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var go = items[i];
                    if (go == null) continue;
                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe == null) continue;
                    if (pe.ElementID == elementHash)
                        sum += pe.Mass;
                }
            }
            return sum;
        }
    }

    [HarmonyPatch(typeof(Storage), nameof(Storage.OnCleanUp))]
    internal static class Storage_OnCleanUp_LiquidPEC_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Storage __instance)
        {
            LiquidPECStorageLimiter.DetachFromStorage(__instance);
        }
    }

    [HarmonyPatch(typeof(Deconstructable), nameof(Deconstructable.OnCompleteWork))]
    internal static class Deconstructable_OnCompleteWork_LiquidPEC_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Deconstructable __instance)
        {
            var storage = __instance.GetComponent<Storage>();
            LiquidPECStorageLimiter.DetachFromStorage(storage);
        }
    }

    // Irrigation SetStorage hook: wire plant PECs to the plot storage when planted.
    [HarmonyPatch(typeof(IrrigationMonitor.Instance), nameof(IrrigationMonitor.Instance.SetStorage))]
    internal static class IrrigationMonitor_Instance_SetStorage_Patch_LiquidPEC
    {
        private static void Postfix(IrrigationMonitor.Instance __instance, object obj)
        {
            var storage = obj as Storage;
            if (__instance == null) return;

            LiquidConsumerWireUtil.WireLiquidPECs(__instance.gameObject, storage);
        }
    }
}