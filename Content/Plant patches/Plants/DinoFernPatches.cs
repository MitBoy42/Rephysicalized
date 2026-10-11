using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(DinofernConfig), nameof(DinofernConfig.CreatePrefab))]
    internal static class DinofernConfig_CreatePrefab_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
            if (!Config.Instance.ChemistrierDinofern) return;
            EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
            {

                new PlantElementAbsorber.ConsumeInfo
                {
                    tag = ModTags.RichSoil,
                    massConsumptionRate = 0.004f
                }
            });
        }
    }

    [HarmonyPatch(typeof(Dinofern), nameof(Dinofern.OnSpawn))]
    public static class Dinofern_OnSpawn_Patch
    {

        private static readonly Tag[] ChlorineGasTags = { SimHashes.ChlorineGas.CreateTag()};

        public static void Postfix(Dinofern __instance)
        {
            var go = __instance.gameObject;
            Storage storage = go.GetComponent<Storage>();
            storage.showInUI = true;
            foreach (var consumer in go.GetComponentsInChildren<ElementConsumer>(includeInactive: true))
            {
                if (consumer != null)
                    consumer.storeOnConsume = true;
            }

            var tracker = go.AddOrGet<PlantMassTrackerComponent>();
            if (PlantMassTrackerRegistry.TryGetConfig(go.GetComponent<KPrefabID>().PrefabID().Name, out var cfg))
                tracker.InitializeFromConfig(cfg);

            RegisterDinofernStoragesForChlorine(tracker, go);

            go.AddOrGet<DinofernChlorineRemover>();
        }

        private static void RegisterDinofernStoragesForChlorine(PlantMassTrackerComponent tracker, GameObject root)
        {
            foreach (var storage in root.GetComponentsInChildren<Storage>(includeInactive: true))
            {
                if (storage != null)
                    tracker.RegisterMonitorStorage(storage, ChlorineGasTags);
            }
        }
    }

    // Removes any stored ChlorineGas items so Storage mass decreases,
    // which PMT translates into "consumed by plant" and adds to plant mass.
    public sealed class DinofernChlorineRemover : KMonoBehaviour, ISim1000ms
    {
   
        public void Sim1000ms(float dt)
        {
            foreach (var storage in gameObject.GetComponentsInChildren<Storage>(includeInactive: true))
            {
                if (storage == null)
                    continue;

                var items = storage.items;
                if (items == null)
                    continue;

                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (item == null)
                        continue;

                    var primaryElement = item.GetComponent<PrimaryElement>();
                    if (primaryElement == null || primaryElement.ElementID != SimHashes.ChlorineGas)
                        continue;

                    storage.Remove(item, true);
                    Util.KDestroyGameObject(item);
                }
            }
        }
    }
}