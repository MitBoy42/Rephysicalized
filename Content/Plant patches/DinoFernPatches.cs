using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    // Pool identifier marker types (must be non-static, non-abstract)
    internal sealed class DinofernConsumerPoolId { }
    internal sealed class DinofernStoragePoolId { }
    internal sealed class DinofernRemoverStoragePoolId { }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Dinofern_Registration
    {
        public static void Postfix()
        {
            // Assuming these types exist in your project:
            // PlantMassTrackerRegistry, MaterialYield
            PlantMassTrackerRegistry.ApplyToCrop(
                plantPrefabId: "Dinofern",
                yields: new List<MaterialYield>
                {
                    new MaterialYield("Sand", 0.5f),
                    new MaterialYield("BleachStone", 0.5f),
                },
                realHarvestSubtractKg: 36f
            );
        }
    }

    [HarmonyPatch(typeof(Dinofern), nameof(Dinofern.OnSpawn))]
    public static class Dinofern_OnSpawn_Patch
    {
        public static void Postfix(Dinofern __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;

            // 1) Ensure the consumer stores gas in Storage so PMT can observe a removal delta.
            // Dinofern consumes ChlorineGas via ElementConsumer.
            var consumers = ListPool<ElementConsumer, DinofernConsumerPoolId>.Allocate();
            try
            {
                go.GetComponents(consumers);

                var childConsumers = go.GetComponentsInChildren<ElementConsumer>(includeInactive: true);
                if (childConsumers != null)
                {
                    for (int i = 0; i < childConsumers.Length; i++)
                    {
                        var c = childConsumers[i];
                        if (c != null && !consumers.Contains(c))
                            consumers.Add(c);
                    }
                }

                for (int i = 0; i < consumers.Count; i++)
                {
                    var c = consumers[i];
                    if (c == null) continue;
                    // Optional filter if you want to affect only the chlorine consumer:
                    // if (c.configuration != ElementConsumer.Configuration.Element || c.elementToConsume != SimHashes.ChlorineGas) continue;
                    c.storeOnConsume = true;
                }
            }
            finally
            {
                consumers.Recycle();
            }

            // 2) Ensure PMT exists and initialize from registry.
            var tracker = go.AddOrGet<PlantMassTrackerComponent>();
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null && PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
            {
                tracker.InitializeFromConfig(cfg);
            }

            // 3) Register storages for ChlorineGas Tag so PMT counts its consumption.
            RegisterDinofernStoragesForChlorine(tracker, go);

            // 4) Attach remover that clears stored chlorine; PMT will observe the delta and add mass.
            go.AddOrGet<DinofernChlorineRemover>();
        }

        private static void RegisterDinofernStoragesForChlorine(PlantMassTrackerComponent tracker, GameObject root)
        {
            if (tracker == null || root == null) return;

            var storages = ListPool<Storage, DinofernStoragePoolId>.Allocate();
            try
            {
                root.GetComponents(storages);

                var childStorages = root.GetComponentsInChildren<Storage>(includeInactive: true);
                if (childStorages != null)
                {
                    for (int i = 0; i < childStorages.Length; i++)
                    {
                        var s = childStorages[i];
                        if (s != null && !storages.Contains(s))
                            storages.Add(s);
                    }
                }

                // Correct Tag for ChlorineGas. PMT expects Tag[] not string[].
                // In ONI, this is simply new Tag("ChlorineGas") or SimHashes.ChlorineGas.CreateTag().
                Tag chlorineTag = SimHashes.ChlorineGas.CreateTag();

                for (int i = 0; i < storages.Count; i++)
                {
                    var s = storages[i];
                    if (s == null) continue;
                    tracker.RegisterMonitorStorage(s, new[] { chlorineTag });
                }
            }
            finally
            {
                storages.Recycle();
            }
        }
    }

    // Removes any stored ChlorineGas items so Storage mass decreases,
    // which PMT translates into "consumed by plant" and adds to plant mass.
    public sealed class DinofernChlorineRemover : KMonoBehaviour, ISim1000ms
    {
        private static readonly SimHashes TargetGas = SimHashes.ChlorineGas;

        public void Sim1000ms(float dt)
        {
            var storages = ListPool<Storage, DinofernRemoverStoragePoolId>.Allocate();
            try
            {
                GetComponents(storages);

                var childStorages = gameObject.GetComponentsInChildren<Storage>(includeInactive: true);
                if (childStorages != null)
                {
                    for (int i = 0; i < childStorages.Length; i++)
                    {
                        var s = childStorages[i];
                        if (s != null && !storages.Contains(s))
                            storages.Add(s);
                    }
                }

                for (int sIdx = 0; sIdx < storages.Count; sIdx++)
                {
                    var storage = storages[sIdx];
                    if (storage == null) continue;

                    var items = storage.items;
                    if (items == null) continue;

                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        var go = items[i];
                        if (go == null) continue;

                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe == null) continue;

                        if (pe.ElementID == TargetGas)
                        {
                            // Removing the stored item triggers a storage decrease; PMT counts this as consumed mass.
                            storage.Remove(go, true);
                            Util.KDestroyGameObject(go);
                        }
                    }
                }
            }
            finally
            {
                storages.Recycle();
            }
        }
    }
}