using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{

    // Minimal patch: set Mercury power to 120f
    [HarmonyPatch(typeof(MercuryCeilingLightConfig), nameof(MercuryCeilingLightConfig.CreateBuildingDef))]
    internal static class MercuryLight_Energy_Patch
    {
        private static void Postfix(ref BuildingDef __result)
        {
                __result.EnergyConsumptionWhenActive = 120f;
        }
    }
    [HarmonyPatch(typeof(MercuryCeilingLightConfig), nameof(MercuryCeilingLightConfig.ConfigureBuildingTemplate))]
    internal static class Light_Lux_Patch
    {
        private static void Postfix(GameObject go)
        {
            var light = go.AddOrGetDef<MercuryLight.Def>();
         
                light.MAX_LUX = 40000f;
            
        }
    }

    [HarmonyPatch(typeof(MercuryCeilingLightConfig), nameof(MercuryCeilingLightConfig.DoPostConfigureComplete))]
    internal static class MercuryLight_Lux_Patch
    {
        private static void Postfix(GameObject go)
        {
        
            var light = go.GetComponent<Light2D>();
                light.Lux = 40000;
            
        }
    }



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
                var s = spawned.AddComponent<Storage>();
                s.capacityKg = Mathf.Max(1200f, s.capacityKg);
                s.showInUI = true;
                s.showDescriptor = false;
                s.allowItemRemoval = false;
                s.onlyFetchMarkedItems = false;
                s.storageFilters = new List<Tag> { SimHashes.Cinnabar.CreateTag() };
                s.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);


                io.ByproductStorage = s;

                // Ensure ElementTileMakerPatch exists and is configured
                var tilemaker = spawned.GetComponent<ElementTileMaker>() ?? spawned.AddComponent<ElementTileMaker>();
                tilemaker.emitTag = SimHashes.Cinnabar.CreateTag();
                tilemaker.emitMass = 520f;
                tilemaker.storage = s;
                if (!Config.Instance.CloggedBuildings) tilemaker.MakeTiles = false;
                tilemaker.emitCellOffsets = new List<CellOffset>
                {
                    new CellOffset(0, 0),
                    new CellOffset(1, 0),
                    new CellOffset(-1, 0),
                };

            };
        }


        // Mirror actual mercury consumption; add equivalent Cinnabar into the byproduct storage
        [HarmonyPatch(typeof(MercuryLight.Instance), nameof(MercuryLight.Instance.ConsumeFuelUpdate))]
        internal static class MercuryLightInstance_ConsumeFuelUpdate_Byproduct_Patch
        {
            // Sample mercury mass and temperature before vanilla consumption
            public static void Prefix(MercuryLight.Instance __instance, float dt)
            {

                var go = ResolveInstanceGO(__instance);
                var io = go.GetComponent<MercuryByproductIO>();
                io.MercuryKgBefore = 0f;
                io.SampleTemp = 243.15f;
                io.SampleDiseaseIdx = byte.MaxValue;
                io.SampleDiseaseCount = 0;
                var inputStorage = ResolveInputStorage(__instance, go);
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

                var go = ResolveInstanceGO(__instance);
                var io = go.GetComponent<MercuryByproductIO>();
                var inputStorage = ResolveInputStorage(__instance, go);

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
                var master = Traverse.Create(smi).Field("master").GetValue<IStateMachineTarget>();
                if (master is KMonoBehaviour kmb && kmb != null)
                    return kmb.gameObject;
                if (master is Component comp && comp != null)
                    return comp.gameObject;

                var kmb2 = smi.GetComponent<KMonoBehaviour>();
                return kmb2 != null ? kmb2.gameObject : null;
            }

            private static Storage ResolveInputStorage(MercuryLight.Instance smi, GameObject go)
            {

                var s = Traverse.Create(smi).Field("storage").GetValue<Storage>();
                if (s != null) return s;

                return go != null ? go.GetComponent<Storage>() : null;
            }
        }
    }
}