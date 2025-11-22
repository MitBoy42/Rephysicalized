using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    // Minimal holder for per-instance data
    internal sealed class MercuryByproductIO : KMonoBehaviour
    {
        [NonSerialized] public Storage ByproductStorage;

        // one-tick sampling payload
        [NonSerialized] public float MercuryKgBefore;
        [NonSerialized] public float SampleTemp = 293.15f;
        [NonSerialized] public byte SampleDiseaseIdx = byte.MaxValue;
        [NonSerialized] public int SampleDiseaseCount;
    }

    // Attach byproduct storage and tilemaker on the spawned Mercury Light instance
    [HarmonyPatch(typeof(MercuryCeilingLightConfig), nameof(MercuryCeilingLightConfig.DoPostConfigureComplete))]
    internal static class MercuryCeilingLight_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
            if (kpid == null) return;

            kpid.prefabSpawnFn += spawned =>
            {
                if (spawned == null) return;

                // Ensure IO marker
                var io = spawned.GetComponent<MercuryByproductIO>() ?? spawned.AddComponent<MercuryByproductIO>();

                // Ensure or create a sealed, UI-visible Cinnabar-only storage
                var byprod = FindCinnabarStorage(spawned) ?? spawned.AddComponent<Storage>();
                ConfigureCinnabarStorage(byprod);
                io.ByproductStorage = byprod;

                // Ensure ElementTileMakerPatch exists and is configured
                var tilemaker = spawned.GetComponent<ElementTileMakerPatch>() ?? spawned.AddComponent<ElementTileMakerPatch>();
                tilemaker.emitTag = SimHashes.Cinnabar.CreateTag();
                tilemaker.emitMass = 780f;
                tilemaker.emitCellOffsets = new List<CellOffset>
                {
                    new CellOffset(0, 0),
                    new CellOffset(1, 0),
                    new CellOffset(-1, 0),
                };

                // Force the tilemaker to watch the byproduct storage specifically (not the fuel storage)
                Traverse.Create(tilemaker).Field("storage").SetValue(byprod);

                // Re-assert after one frame to win any OnSpawn [MyCmpGet] race
                GameScheduler.Instance.Schedule("BindTilemakerStorageNextFrame", 0f, _ =>
                {
                    var tm = spawned != null ? spawned.GetComponent<ElementTileMakerPatch>() : null;
                    if (tm != null)
                        Traverse.Create(tm).Field("storage").SetValue(byprod);
                });
            };
        }

        private static Storage FindCinnabarStorage(GameObject go)
        {
            var storages = go.GetComponents<Storage>();
            foreach (var s in storages)
            {
                if (s != null && s.storageFilters != null && s.storageFilters.Count == 1 &&
                    s.storageFilters[0] == SimHashes.Cinnabar.CreateTag())
                    return s;
            }
            return null;
        }

        private static void ConfigureCinnabarStorage(Storage s)
        {
            if (s == null) return;
            s.capacityKg = Mathf.Max(1200f, s.capacityKg);
            s.showInUI = true;
            s.showDescriptor = false;
            s.allowItemRemoval = false; // sealed
            s.onlyFetchMarkedItems = false;
            s.storageFilters = new List<Tag> { SimHashes.Cinnabar.CreateTag() };
            s.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
        }
    }

    // Mirror actual mercury consumption; add equivalent Cinnabar into the byproduct storage
    [HarmonyPatch(typeof(MercuryLight.Instance), nameof(MercuryLight.Instance.ConsumeFuelUpdate))]
    internal static class MercuryLightInstance_ConsumeFuelUpdate_Byproduct_Patch
    {
        // Sample mercury mass and temperature before vanilla consumption
        public static void Prefix(MercuryLight.Instance __instance, float dt)
        {
            if (__instance == null) return;

            var go = ResolveInstanceGO(__instance);
            if (go == null) return;

            var io = go.GetComponent<MercuryByproductIO>();
            if (io == null) return;

            io.MercuryKgBefore = 0f;
            io.SampleTemp = 243.15f;
            io.SampleDiseaseIdx = byte.MaxValue;
            io.SampleDiseaseCount = 0;

            var inputStorage = ResolveInputStorage(__instance, go);
            if (inputStorage == null) return;

            io.MercuryKgBefore = inputStorage.GetMassAvailable(SimHashes.Mercury.CreateTag());

            var pe = inputStorage.FindPrimaryElement(SimHashes.Mercury);
            if (pe != null)
            {
                io.SampleTemp = pe.Temperature;
                try
                {
                    io.SampleDiseaseIdx = pe.DiseaseIdx;
                    io.SampleDiseaseCount = pe.DiseaseCount;
                }
                catch
                {
                    io.SampleDiseaseIdx = byte.MaxValue;
                    io.SampleDiseaseCount = 0;
                }
            }
            else
            {
                var buildingPE = go.GetComponent<PrimaryElement>();
                if (buildingPE != null)
                    io.SampleTemp = buildingPE.Temperature;
            }
        }

        // Compute delta after vanilla consumption and deposit as Cinnabar
        public static void Postfix(MercuryLight.Instance __instance, float dt)
        {
            if (__instance == null) return;

            var go = ResolveInstanceGO(__instance);
            if (go == null) return;

            var io = go.GetComponent<MercuryByproductIO>();
            if (io == null || io.ByproductStorage == null) return;

            var inputStorage = ResolveInputStorage(__instance, go);
            if (inputStorage == null) return;

            float mercuryAfter = inputStorage.GetMassAvailable(SimHashes.Mercury.CreateTag());
            float consumed = Mathf.Max(0f, io.MercuryKgBefore - mercuryAfter);
            if (consumed <= 0f) return;

            io.ByproductStorage.AddOre(
                SimHashes.Cinnabar,
                consumed,
                io.SampleTemp,
                io.SampleDiseaseIdx,
                io.SampleDiseaseCount
            );
        }

        private static GameObject ResolveInstanceGO(MercuryLight.Instance smi)
        {
            // Try SM master first (most reliable)
            try
            {
                var master = Traverse.Create(smi).Field("master").GetValue<IStateMachineTarget>();
                if (master is KMonoBehaviour kmb && kmb != null)
                    return kmb.gameObject;
                if (master is Component comp && comp != null)
                    return comp.gameObject;
            }
            catch { /* ignore */ }

            // Fallbacks
         
         
            var kmb2 = smi.GetComponent<KMonoBehaviour>();
            return kmb2 != null ? kmb2.gameObject : null;
        }

        private static Storage ResolveInputStorage(MercuryLight.Instance smi, GameObject go)
        {
            // Use the exact storage the SMI uses (private [MyCmpGet])
            try
            {
                var s = Traverse.Create(smi).Field("storage").GetValue<Storage>();
                if (s != null) return s;
            }
            catch { /* ignore */ }
            return go != null ? go.GetComponent<Storage>() : null;
        }
    }
}