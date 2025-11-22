using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches
{
    

    // Register SaltPlant with PMT: 100% Salt yield, no real harvest mass subtraction
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class SaltPlant_PMT_Registration
    {
        private static void Postfix()
        {
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: "SaltPlant",
                yields: new List<MaterialYield> { new MaterialYield("Salt", 1f) },
                realHarvestSubtractKg: 30f
            );
        }
    }

    // Set vanilla Salt crop yield to 0 so only PMT governs harvest output
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Salt_ZeroYieldPatch
    {
        private static void Postfix()
        {
            var crops = TUNING.CROPS.CROP_TYPES;
            string saltId = SimHashes.Salt.ToString(); // "Salt"
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

            // Create a hidden Storage for Chlorine on the main GO 
            var chlorineStorage = __result.AddComponent<Storage>();
            chlorineStorage.showInUI = false;
            chlorineStorage.capacityKg = 1f;
            chlorineStorage.allowItemRemoval = true;
            chlorineStorage.storageFilters = new List<Tag> { SimHashes.ChlorineGas.CreateTag() };

            // Ensure ElementConsumer stores consumed Chlorine into our storage
            var consumer = __result.GetComponent<ElementConsumer>();
            if (consumer != null)
            {
                consumer.storeOnConsume = true;
                consumer.elementToConsume = SimHashes.ChlorineGas;
                consumer.configuration = ElementConsumer.Configuration.Element;

                // Bind storage if the field is public in this version
                try
                {
                    var storageField = typeof(ElementConsumer).GetField("storage", BindingFlags.Instance | BindingFlags.Public);
                    if (storageField != null && storageField.FieldType == typeof(Storage))
                        storageField.SetValue(consumer, chlorineStorage);
                }
                catch
                {
                    // Ignore if no public field is available in this build
                }
            }

            // Helpers
            __result.AddOrGet<SaltPlantChlorineRemover>();
            __result.AddOrGet<PlantMassTrackerComponent>();
        }
    }

    // OnSpawn: initialize PMT config and ignore the chlorine storage to avoid double counting
    [HarmonyPatch(typeof(SaltPlant), nameof(SaltPlant.OnSpawn))]
    internal static class SaltPlant_OnSpawn_Patch
    {
        private static void Postfix(SaltPlant __instance)
        {
            if (__instance == null) return;

            var go = __instance.gameObject;

            // PMT init from registry
            var pmt = go.AddOrGet<PlantMassTrackerComponent>();
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null && PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
                pmt.InitializeFromConfig(cfg);

            // Find any on-GO storage filtered to Chlorine and ask PMT to ignore it
            var storages = ListPool<Storage, GameObject>.Allocate();
            try
            {
                go.GetComponents(storages);
                Tag chlorineTag = SimHashes.ChlorineGas.CreateTag();
                for (int i = 0; i < storages.Count; i++)
                {
                    var s = storages[i];
                    if (s == null) continue;
                    var filters = s.storageFilters;
                    if (filters != null && filters.Contains(chlorineTag))
                    {
                        pmt.IgnoreAndUnregisterStorage(s);
                        // do not break; if multiple chlorine storages exist, ignore them all
                    }
                }
            }
            finally
            {
                storages.Recycle();
            }
        }
    }

    // Removes any stored Chlorine immediately and credits PMT with the exact mass; brief grace to preserve starting mass
    public sealed class SaltPlantChlorineRemover : KMonoBehaviour, ISim1000ms
    {
        private static readonly SimHashes TargetGas = SimHashes.ChlorineGas;
        private float _spawnGraceSeconds = 1f;

        private readonly List<Storage> _storages = new List<Storage>();
        private bool _subscribed;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _spawnGraceSeconds = 1f;

            _storages.Clear();
            // storage lives on the same GO (no child), but include children in case another mod adds one
            GetComponentsInChildren(_storages);

            // Subscribe to storage events for immediate removal
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
                return;
            }

            // Safety sweep
            for (int sIdx = _storages.Count - 1; sIdx >= 0; sIdx--)
            {
                RemoveChlorineFromStorage(_storages[sIdx]);
            }
        }

        private void OnAnyStorageChanged(object _)
        {
            if (_spawnGraceSeconds > 0f) return;
            for (int i = 0; i < _storages.Count; i++)
            {
                RemoveChlorineFromStorage(_storages[i]);
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
                if (pe == null) continue;

                if (pe.ElementID == TargetGas)
                {
                    // Credit plant mass by the exact consumed Chlorine mass
                    var pmt = GetComponent<PlantMassTrackerComponent>();
                    if (pmt != null && pe.Mass > 0f)
                        pmt.AddExternalMass(pe.Mass);

                    storage.Remove(go, true);
                    Util.KDestroyGameObject(go);
                }
            }
        }
    }

    
}