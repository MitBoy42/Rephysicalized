using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches
{
    

    // Set vanilla Salt crop yield to 0 so only PMT governs harvest output
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Salt_ZeroYieldPatch
    {
        private static void Postfix()
        {
            var crops = TUNING.CROPS.CROP_TYPES;
            string saltId = SimHashes.Salt.ToString(); 
            for (int i = 0; i < crops.Count; i++)
            {
                if (crops[i].cropId == saltId)
                {
                    var c = crops[i];
                    crops[i] = new Crop.CropVal(c.cropId, c.cropDuration, 30);
                    break;
                }
            }
        }
    }

    // Prefab patch: configure ElementConsumer to store chlorine into a hidden on-GO storage
    [HarmonyPatch(typeof(SaltPlantConfig), nameof(SaltPlantConfig.CreatePrefab))]
    internal static class SaltPlantConfig_CreatePrefab_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            var chlorineTag = SimHashes.ChlorineGas.CreateTag();

            // Create a hidden Storage for Chlorine on the main GO 
            var chlorineStorage = __result.GetComponent<Storage>();
            if (chlorineStorage == null)
            {
                chlorineStorage = __result.AddComponent<Storage>();
            }
            chlorineStorage.showInUI = false;
            chlorineStorage.capacityKg = 1f;
            chlorineStorage.allowItemRemoval = true;
            chlorineStorage.storageFilters = new List<Tag> { chlorineTag };

            // Ensure ElementConsumer stores consumed Chlorine into our storage
            var consumer = __result.GetComponent<ElementConsumer>();
            if (consumer != null)
            {
                consumer.storeOnConsume = true;
                consumer.elementToConsume = SimHashes.ChlorineGas;
                consumer.configuration = ElementConsumer.Configuration.Element;
                consumer.isRequired = true;
                consumer.showInStatusPanel = true;
                consumer.showDescriptor = true;

                consumer.storage = chlorineStorage;
            }

            // Helpers
            __result.AddOrGet<SaltPlantChlorineRemover>();
            __result.AddOrGet<PlantMassTrackerComponent>();
        }
    }

    // OnSpawn: initialize PMT config and register the chlorine storage for mass tracking
    [HarmonyPatch(typeof(SaltPlant), nameof(SaltPlant.OnSpawn))]
    internal static class SaltPlant_OnSpawn_Patch
    {
        private static readonly Tag[] ChlorineGasTags = { SimHashes.ChlorineGas.CreateTag() };

        private static void Postfix(SaltPlant __instance)
        {
            var go = __instance.gameObject;

            // PMT init from registry
            var pmt = go.AddOrGet<PlantMassTrackerComponent>();
            if (PlantMassTrackerRegistry.TryGetConfig(go.GetComponent<KPrefabID>().PrefabID().Name, out var cfg))
                pmt.InitializeFromConfig(cfg);

            // Register chlorine storage so PMT credits mass when the remover empties it.
            foreach (var storage in go.GetComponentsInChildren<Storage>(includeInactive: true))
            {
                if (storage == null)
                    continue;

                var filters = storage.storageFilters;
                if (filters != null && filters.Contains(ChlorineGasTags[0]))
                    pmt.RegisterMonitorStorage(storage, ChlorineGasTags);
            }
        }
    }

    // Removes stored Chlorine so PMT observes the storage decrease and credits its mass to the plant.
    public sealed class SaltPlantChlorineRemover : KMonoBehaviour, ISim1000ms
    {
        private static readonly SimHashes TargetGas = SimHashes.ChlorineGas;

        public void Sim1000ms(float dt)
        {
            foreach (var storage in gameObject.GetComponentsInChildren<Storage>(includeInactive: true))
            {
                RemoveChlorineFromStorage(storage);
            }
        }

        private void RemoveChlorineFromStorage(Storage storage)
        {
            if (storage == null) return;

            var items = storage.items;
            if (items == null || items.Count == 0) return;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var go = items[i];
                if (go == null) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.ElementID != TargetGas)
                    continue;

                storage.Remove(go, true);
                Util.KDestroyGameObject(go);
            }
        }
    }

    
}