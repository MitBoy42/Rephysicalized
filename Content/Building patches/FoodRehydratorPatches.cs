using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized
{

    // Apply tweaks at prefab configuration time so values are correct before any OnSpawn logic runs.
    [HarmonyPatch(typeof(FoodDehydratorConfig), nameof(FoodDehydratorConfig.ConfigureBuildingTemplate))]
    public static class FoodDehydratorConfig_ConfigureBuildingTemplate_Tweaks
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // 1) Bump CO2 output to 0.02 kg/s
            var ec = go.GetComponent<ElementConverter>();
            if (ec != null)
            {
                var outputs = ec.outputElements;
                if (outputs != null)
                {
                    bool foundCO2 = false;
                    for (int i = 0; i < outputs.Length; i++)
                    {
                        if (outputs[i].elementHash == SimHashes.CarbonDioxide)
                        {
                            outputs[i].massGenerationRate = 0.02f;
                            foundCO2 = true;
                            break;
                        }
                    }

                    if (!foundCO2)
                    {
                        // If CO2 isn't present for some reason, add it.
                        var list = new List<ElementConverter.OutputElement>(outputs)
                        {
                            new ElementConverter.OutputElement(0.02f, SimHashes.CarbonDioxide, 348.15f, outputElementOffsety: 1f)
                        };
                        outputs = list.ToArray();
                    }

                    ec.outputElements = outputs; // reassign to persist struct edits
                }
            }

            // 2) Set the empty workable time to 20 seconds
            var workable = go.GetComponent<FoodDehydratorWorkableEmpty>();
            if (workable != null)
            {
                // Prefer SetWorkTime if available (ensures any internal bookkeeping is updated)
                workable.SetWorkTime(20f);
                // If SetWorkTime didn't exist in a variant, workable.workTime = 20f would also work.
            }
        }
    }


    // Bootstrap: run after Db.Initialize and then apply our rehydrator patches safely.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Rehydrator_LatePatch_Bootstrap
    {
        public static void Postfix()
        {
            var harmony = new Harmony("rephysicalized.rehydrator_plastic.runtime");

            // Try both qualified and unqualified names for compatibility
            var dmType = AccessTools.TypeByName("FoodRehydrator.DehydratedManager") ??
                         AccessTools.TypeByName("DehydratedManager");

            if (dmType == null)
            {
                Debug.LogWarning("[Rephysicalized] Could not find DehydratedManager type for patching.");
                return;
            }

            // Patch OnSpawn -> attach/find plastic storage and dropper
            var onSpawn = AccessTools.Method(dmType, "OnSpawn");
            if (onSpawn != null)
            {
                harmony.Patch(
                    onSpawn,
                    postfix: new HarmonyMethod(typeof(Rehydrator_LatePatch_Bootstrap), nameof(OnSpawn_Postfix))
                );
            }
            else
            {
                Debug.LogWarning("[Rephysicalized] DehydratedManager.OnSpawn not found.");
            }

            // Patch ConsumeResourcesForRehydration(GameObject, GameObject) -> add 2 kg plastic
            var consume = AccessTools.Method(dmType, "ConsumeResourcesForRehydration", new[] { typeof(GameObject), typeof(GameObject) });
            if (consume != null)
            {
                harmony.Patch(
                    consume,
                    postfix: new HarmonyMethod(typeof(Rehydrator_LatePatch_Bootstrap), nameof(Consume_Postfix))
                );
            }
            else
            {
                Debug.LogWarning("[Rephysicalized] DehydratedManager.ConsumeResourcesForRehydration not found.");
            }
        }

        // Postfix for DehydratedManager.OnSpawn (instance will be a KMonoBehaviour)
        public static void OnSpawn_Postfix(KMonoBehaviour __instance)
        {
            if (__instance == null) return;

            GameObject go = __instance.gameObject;
            Tag plasticTag = SimHashes.Polypropylene.CreateTag();

            // Find existing plastic-only storage (avoid duplicates)
            Storage plasticStorage = null;
            var storages = go.GetComponents<Storage>();
            for (int i = 0; i < storages.Length; i++)
            {
                var s = storages[i];
                if (s != null && s.storageFilters != null && s.storageFilters.Contains(plasticTag))
                {
                    plasticStorage = s;
                    break;
                }
            }

            // If not present, create a new dedicated storage for plastic
            if (plasticStorage == null)
            {
                plasticStorage = go.AddComponent<Storage>();
                plasticStorage.capacityKg = 100f;

                // Visible in UI and user-manageable
                plasticStorage.showInUI = true;
                plasticStorage.showDescriptor = true;
                plasticStorage.showCapacityStatusItem = false;
                plasticStorage.showCapacityAsMainStatus = false;
                plasticStorage.allowItemRemoval = true;

                plasticStorage.storageFilters = new List<Tag> { plasticTag };
                plasticStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            }

            // Ensure a dropper exists and targets the plastic storage
            var dropper = go.AddOrGet<DropWhenFullStorage>();
            if (dropper != null)
            {
                dropper.SetOrFindTarget(plasticStorage);
            }
        }

        // Postfix for DehydratedManager.ConsumeResourcesForRehydration
        public static void Consume_Postfix(KMonoBehaviour __instance)
        {
            TryAddPlasticByproduct(__instance, 2f);
        }

        private static void TryAddPlasticByproduct(KMonoBehaviour inst, float massKg)
        {
            if (inst == null || massKg <= 0f) return;

            Tag plasticTag = SimHashes.Polypropylene.CreateTag();

            // Find the plastic-only storage by its filter
            Storage plasticStorage = null;
            var storages = inst.GetComponents<Storage>();
            for (int i = 0; i < storages.Length; i++)
            {
                var s = storages[i];
                if (s != null && s.storageFilters != null && s.storageFilters.Contains(plasticTag))
                {
                    plasticStorage = s;
                    break;
                }
            }

            if (plasticStorage == null)
                return;

            var element = ElementLoader.FindElementByHash(SimHashes.Polypropylene);
            if (element == null || element.substance == null)
                return;

            float temp = 293.15f;
            var pe = inst.GetComponent<PrimaryElement>();
            if (pe != null)
                temp = pe.Temperature;

            // Spawn a plastic chunk and immediately store it in the plastic storage
            GameObject chunk = element.substance.SpawnResource(inst.transform.GetPosition(), massKg, temp, byte.MaxValue, 0, false);
            if (chunk != null)
            {
                plasticStorage.Store(chunk, false);
            }
        }
    }

    // Helper component that listens for storage changes and drops everything when full
    public class DropWhenFullStorage : KMonoBehaviour
    {
        [SerializeField] public Storage target;

        private bool hasSpawned;

        // Safe helper to assign or auto-find the plastic storage
        public void SetOrFindTarget(Storage preferred)
        {
            if (preferred != null)
            {
                // If we were already subscribed to a different target, unsubscribe
                if (target != null && target != preferred)
                {
                    Unsubscribe(target.gameObject, (int)GameHashes.OnStorageChange, OnTargetStorageChanged);
                }
                target = preferred;
                // Subscribe to the new target if we're spawned already
                if (hasSpawned)
                {
                    Subscribe(target.gameObject, (int)GameHashes.OnStorageChange, OnTargetStorageChanged);
                }
                return;
            }

            // Fallback: try find by filter
            Tag plasticTag = SimHashes.Polypropylene.CreateTag();
            var storages = GetComponents<Storage>();
            for (int i = 0; i < storages.Length; i++)
            {
                var s = storages[i];
                if (s != null && s.storageFilters != null && s.storageFilters.Contains(plasticTag))
                {
                    target = s;
                    break;
                }
            }

            if (hasSpawned && target != null)
            {
                Subscribe(target.gameObject, (int)GameHashes.OnStorageChange, OnTargetStorageChanged);
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            hasSpawned = true;

            if (target == null)
            {
                SetOrFindTarget(null);
            }

            if (target != null)
            {
                Subscribe(target.gameObject, (int)GameHashes.OnStorageChange, OnTargetStorageChanged);
            }
        }

        public override void OnCleanUp()
        {
            if (target != null)
            {
                Unsubscribe(target.gameObject, (int)GameHashes.OnStorageChange, OnTargetStorageChanged);
            }
            base.OnCleanUp();
        }

        private void OnTargetStorageChanged(object _)
        {
            if (target == null)
                return;

            // If at or above capacity, drop everything to the world
            const float epsilon = 0.0001f;
            if (target.MassStored() >= target.capacityKg - epsilon && target.items != null && target.items.Count > 0)
            {
                target.DropAll();
            }
        }
    }

    // Applies globally and only affects items tagged as Rehydrated.
    [HarmonyPatch(typeof(Edible), nameof(Edible.GetFeedingTime))]
    public static class RehydratedEatingSpeedPatch
    {
        // Original: float GetFeedingTime(WorkerBase worker)
        // We halve the computed time when the edible is rehydrated -> 2x faster eating.
        public static void Postfix(Edible __instance, WorkerBase worker, ref float __result)
        {
            if (__instance != null && __instance.gameObject != null
                && __instance.gameObject.HasTag(GameTags.Rehydrated))
            {
                __result *= 0.3f;
            }
        }
    }
}