using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using UnityEngine;

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
            "PlanterBox", "FarmTile", "HydroponicFarm"
        };

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Assets.RegisterOnAddPrefab(OnAddPrefab);
        }

        private static void OnAddPrefab(KPrefabID kpid)
        {
            if (kpid == null) return;
            string id = kpid.PrefabID().Name;
            if (PlotPrefabIds.Contains(id))
                kpid.gameObject.AddOrGet<PlotMassStore>();
        }
    }

    public static class PlantMassTrackerRegistry
    {
        private static readonly Dictionary<string, PlantMassTrackerConfig> _configs =
            new Dictionary<string, PlantMassTrackerConfig>(StringComparer.Ordinal);

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

        // Back-compat path if an Effect object is available elsewhere
        internal static void NotifyEffectAdded(Effects effects, Effect effect)
        {
            if (effect == null) return;
            NotifyEffectAddedById(effects, effect.Id);
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PlotMassStore : KMonoBehaviour
    {
        [Serialize] public float mass;
        public void Set(float kg) => mass = kg;
        public float Get() => mass;
    }

    internal static class PlantMassTrackerTeardown
    {
        private static readonly HashSet<Storage> SuppressedStorages = new HashSet<Storage>();

        public static void SuppressStoragesFrom(GameObject plotGo)
        {
            if (plotGo == null) return;
            var own = plotGo.GetComponents<Storage>();
            for (int i = 0; i < own.Length; i++)
            {
                var s = own[i];
                if (s == null) continue;
                SuppressedStorages.Add(s);
            }
        }

        public static void ClearSuppressedFor(GameObject plotGo)
        {
            if (plotGo == null) return;
            var own = plotGo.GetComponents<Storage>();
            for (int i = 0; i < own.Length; i++)
            {
                var s = own[i];
                if (s == null) continue;
                SuppressedStorages.Remove(s);
            }
        }

        public static bool IsSuppressed(Storage s) => s != null && SuppressedStorages.Contains(s);
    }

    [HarmonyPatch(typeof(PlantablePlot), "OnSpawn")]
    internal static class PlantablePlot_OnSpawn_Patch
    {
        private static void Postfix(PlantablePlot __instance)
        {
            if (__instance == null) return;
            __instance.gameObject.AddOrGet<PlotMassStore>();
        }
    }

    [HarmonyPatch(typeof(PlantablePlot), "OnCleanUp")]
    internal static class PlantablePlot_OnCleanUp_Patch
    {
        private static void Prefix(PlantablePlot __instance)
        {
            if (__instance == null) return;
            PlantMassTrackerTeardown.ClearSuppressedFor(__instance.gameObject);
        }
    }

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

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    internal static class PlotDeconstructionDetachPatch
    {
        public static void Prefix(Deconstructable __instance, WorkerBase worker)
        {
            try
            {
                if (__instance == null) return;

                var go = __instance.gameObject;
                if (go == null) return;

                var plot = go.GetComponent<PlantablePlot>();
                if (plot == null) return;

                PlantMassTrackerTeardown.SuppressStoragesFrom(go);

                var plotStore = go.GetComponent<PlotMassStore>();
                if (plotStore == null) return;

                var trackers = UnityEngine.Object.FindObjectsOfType<PlantMassTrackerComponent>();

                for (int i = 0; i < trackers.Length; i++)
                {
                    var t = trackers[i];
                    if (t == null) continue;

                    PlotMassStore theirStore = null;
                    try
                    {
                        theirStore = Traverse.Create(t).Field<PlotMassStore>("_plotStore").Value;
                    }
                    catch { }

                    if (theirStore != null && ReferenceEquals(theirStore, plotStore))
                    {
                        t.BeginFinalTeardown();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PMT] Deconstruction Prefix exception: {ex}");
            }
        }

        public static void Postfix(Deconstructable __instance, WorkerBase worker)
        {
            var go = __instance?.gameObject;
            if (go == null) return;

            var plot = go.GetComponent<PlantablePlot>();
            if (plot == null) return;

            PlantMassTrackerTeardown.ClearSuppressedFor(go);
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

 
}