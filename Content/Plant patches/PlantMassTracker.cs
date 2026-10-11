using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static STRINGS.BUILDINGS.PREFABS;

namespace Rephysicalized
{
    [Serializable]
    public sealed class MaterialYield
    {
        public string id;
        public float multiplier;
        public MaterialYield(string id, float multiplier)
        {
            this.id = id;
            this.multiplier = multiplier;
        }
    }

    [Serializable]
    public sealed class PlantMassTrackerConfig
    {
        public string plantPrefabId;
        public List<MaterialYield> yields;
        public float harvestMassSubtractKg;
        public string tinkerEffectId;

        public PlantMassTrackerConfig(
            string plantPrefabId,
            List<MaterialYield> yields,
            float harvestMassSubtractKg,
            string tinkerEffectId)
        {
            this.plantPrefabId = plantPrefabId;
            this.yields = (yields != null && yields.Count > 0) ? yields : new List<MaterialYield> { new MaterialYield("RotPile", 1f) };
            this.harvestMassSubtractKg = harvestMassSubtractKg;
            this.tinkerEffectId = tinkerEffectId;
        }
    }

    internal static class AssetHookInstaller
    {
        private static bool _installed;
        private static readonly HashSet<string> PlotPrefabIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "PlanterBox", "FarmTile", "HydroponicFarm", "WideFarmTile" , "LargeBackWallFarm"
        };
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            // no-op: PlotMassStore persistence removed
        }

    }

    public static class PlantMassTrackerRegistry
    {
        private static readonly Dictionary<string, PlantMassTrackerConfig> _configs =
            new Dictionary<string, PlantMassTrackerConfig>(StringComparer.Ordinal);
        private static readonly Dictionary<Storage, HashSet<PlantMassTrackerComponent>> _storageTrackers =
            new Dictionary<Storage, HashSet<PlantMassTrackerComponent>>();

        public static bool IsRegisteredPlant(string plantPrefabId) =>
            !string.IsNullOrWhiteSpace(plantPrefabId) && _configs.ContainsKey(plantPrefabId);

        public static void ApplyToCrop(
            string plantPrefabId,
            List<MaterialYield> yields = null,
            float realHarvestSubtractKg = 1f,
            string tinkerEffectId = "FarmTinker")
        {
            if (string.IsNullOrWhiteSpace(plantPrefabId))
            {
                Debug.LogWarning("[PlantMassTracker] ApplyToCrop called with empty plantPrefabId - ignored.");
                return;
            }

            AssetHookInstaller.Install();

            var config = new PlantMassTrackerConfig(
                plantPrefabId,
                yields,
                realHarvestSubtractKg,
                tinkerEffectId);

            _configs[plantPrefabId] = config;
        }

        internal static bool TryGetConfig(string prefabId, out PlantMassTrackerConfig config) =>
            _configs.TryGetValue(prefabId, out config);

        internal static PlantMassTrackerComponent EnsureTracker(GameObject plant)
        {
            if (plant == null)
                return null;

            var kpid = plant.GetComponent<KPrefabID>();
            if (kpid == null || !TryGetConfig(kpid.PrefabID().Name, out var config))
                return null;

            var tracker = plant.AddOrGet<PlantMassTrackerComponent>();
            tracker.InitializeFromConfig(config);
            return tracker;
        }

        // Strongly-typed hooks call into this to wire storages to trackers
        internal static void OnMonitorSetStorage(GameObject plant, Storage storage, IEnumerable<Tag> consumedTags)
        {
            var tracker = plant.GetComponent<PlantMassTrackerComponent>();
            if (tracker == null)
            {
                var kpid = plant.GetComponent<KPrefabID>();
                if (kpid == null) return;

                if (!TryGetConfig(kpid.PrefabID().Name, out var config))
                    return;

                tracker = plant.AddOrGet<PlantMassTrackerComponent>();
                tracker.InitializeFromConfig(config);
            }

            tracker.RegisterMonitorStorage(storage, consumedTags ?? Enumerable.Empty<Tag>());
        }

        internal static void RegisterStorageTracker(Storage storage, PlantMassTrackerComponent tracker)
        {
            if (storage == null || tracker == null)
                return;

            if (!_storageTrackers.TryGetValue(storage, out var trackers))
            {
                trackers = new HashSet<PlantMassTrackerComponent>();
                _storageTrackers[storage] = trackers;
            }

            trackers.Add(tracker);
        }

        internal static void UnregisterStorageTracker(Storage storage, PlantMassTrackerComponent tracker)
        {
            if (storage == null || tracker == null || !_storageTrackers.TryGetValue(storage, out var trackers))
                return;

            trackers.Remove(tracker);
            if (trackers.Count == 0)
                _storageTrackers.Remove(storage);
        }

        internal static void NotifyPlantStorageConsumed(Storage storage, List<PlantElementAbsorber.Planner.ConsumeCommand> consumeCommands)
        {
            if (storage == null || consumeCommands == null || !_storageTrackers.TryGetValue(storage, out var trackers))
                return;

            foreach (var tracker in trackers)
                tracker?.AddConsumedStorageMass(storage, consumeCommands);
        }

        // New: id-based notify that matches updated game signatures
        internal static void NotifyEffectAddedById(Effects effects, string effectId)
        {
            if (effects == null || string.IsNullOrEmpty(effectId)) return;

            var go = effects.gameObject;
            if (go == null) return;

            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return;

            if (!TryGetConfig(kpid.PrefabID().Name, out var config)) return;
            if (string.IsNullOrEmpty(config.tinkerEffectId)) return;

            if (!string.Equals(effectId, config.tinkerEffectId, StringComparison.Ordinal))
                return;

            var tracker = go.GetComponent<PlantMassTrackerComponent>();
            tracker?.AddTinkerMass(5f);
        }

    }

    [SerializationConfig(MemberSerialization.OptIn)]


    [HarmonyPatch(typeof(FertilizationMonitor.Instance), nameof(FertilizationMonitor.Instance.SetStorage))]
    internal static class FertilizationMonitor_Instance_SetStorage_Patch
    {
        private static void Postfix(FertilizationMonitor.Instance __instance, object obj)
        {
            var storage = obj as Storage;
            if (storage == null) return;

            var plantGO = __instance.gameObject;

            var tags = (__instance.def != null && __instance.def.consumedElements != null)
                ? __instance.def.consumedElements.Select(ci => ci.tag)
                : Enumerable.Empty<Tag>();

            PlantMassTrackerRegistry.OnMonitorSetStorage(plantGO, storage, tags);
        }
    }

    [HarmonyPatch(typeof(IrrigationMonitor.Instance), nameof(IrrigationMonitor.Instance.SetStorage))]
    internal static class IrrigationMonitor_Instance_SetStorage_Patch
    {
        private static void Postfix(IrrigationMonitor.Instance __instance, object obj)
        {
            var storage = obj as Storage;
            if (storage == null) return;

            var plantGO = __instance.gameObject;

            var tags = (__instance.def != null && __instance.def.consumedElements != null)
                ? __instance.def.consumedElements.Select(ci => ci.tag)
                : Enumerable.Empty<Tag>();

            PlantMassTrackerRegistry.OnMonitorSetStorage(plantGO, storage, tags);
        }
    }

    [HarmonyPatch(typeof(PlantElementAbsorber), nameof(PlantElementAbsorber.PlanConsume), new Type[] { typeof(float), typeof(List<PlantElementAbsorber.Planner.ConsumeCommand>) })]
    internal static class PlantElementAbsorber_PlanConsume_PlantMassTracker_Patch
    {
        private static void Postfix(ref PlantElementAbsorber __instance, List<PlantElementAbsorber.Planner.ConsumeCommand> consumeCommands, bool __result)
        {
            if (!__result || consumeCommands == null)
                return;

            PlantMassTrackerRegistry.NotifyPlantStorageConsumed(__instance.storage, consumeCommands);
        }
    }

    // Current build: Effects.Add(string effect_id, bool should_save) -> EffectInstance
    [HarmonyPatch(typeof(Effects), nameof(Effects.Add), new Type[] { typeof(string), typeof(bool) })]
    internal static class Effects_Add_StringBool_Patch
    {
        private static void Postfix(Effects __instance, string effect_id, bool should_save, EffectInstance __result)
        {
            // Prefer the id from the resulting EffectInstance when possible
            string id = __result != null && __result.effect != null ? __result.effect.Id : effect_id;
            if (!string.IsNullOrEmpty(id))
                PlantMassTrackerRegistry.NotifyEffectAddedById(__instance, id);
        }
    }

    // Optional component: attach to a plant prefab to customize what the PlantMassTracker
    // spawns on harvest or uproot. If not present on a plant, PMT uses its base config yields.
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PlantMassTrackerYieldModifier : KMonoBehaviour
    {
        // If true, replace the base yields for harvest with 'harvestYields'.
        // If false, 'harvestYields' are added to the base yields.
        [Serialize] public bool overrideHarvestYields = false;

        // If true, replace the base yields for dig with 'digYields'.
        // If false, 'digYields' are added to the base yields.
        [Serialize] public bool overrideDigYields = false;

        // Extra/override yields when the plant is harvested.
        [Serialize] public List<MaterialYield> harvestYields = new List<MaterialYield>();

        // Extra/override yields when the plant is uprooted.
        [Serialize] public List<MaterialYield> digYields = new List<MaterialYield>();
    }
 
}
