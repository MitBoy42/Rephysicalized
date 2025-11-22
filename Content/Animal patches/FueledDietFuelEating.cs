using Klei.AI;
using UnityEngine;
using System.Linq;

namespace Rephysicalized.Chores
{
    // Partial companion to SolidFuelStates with actual consumption logic.
    public sealed partial class SolidFuelStates
    {
        // Perform one bite-and-store against the current target, reflection-free.
        // Important: Always clear assignment after any successful store to avoid chore lock/stutter.
        internal static ConsumeResult TryConsumeOnce(Instance smi)
        {
            try
            {
                if (smi == null || smi.TargetGO == null)
                    return ConsumeResult.Done;

                var pu = smi.TargetGO.GetComponent<Pickupable>();
                var pe = smi.TargetGO.GetComponent<PrimaryElement>();
                if (pu == null || pe == null || pe.Mass <= 0.0005f)
                    return ConsumeResult.Done;

                var controller = smi.gameObject.GetComponent<FueledDietController>();
                var storage = controller != null ? controller.FuelStorage : null;
                var diet = controller != null ? controller.fueledDiet : null;
                if (controller == null || storage == null || diet?.FuelInputs == null || diet.FuelInputs.Count == 0)
                    return ConsumeResult.Done;

                // Resolve tags for matching
                Tag elementTag = Tag.Invalid;
                var elem = ElementLoader.FindElementByHash(pe.ElementID);
                if (elem != null)
                    elementTag = elem.tag;

                Tag prefabTag = Tag.Invalid;
                if (smi.TargetGO.TryGetComponent<KPrefabID>(out var kpid))
                    prefabTag = kpid.PrefabTag;

                // Allowed if any FuelInput matches either element tag OR prefab tag
                bool allowed = diet.FuelInputs.Any(fi =>
                    fi != null &&
                    (fi.ElementTag == prefabTag || fi.ElementTag == elementTag));
                if (!allowed)
                    return ConsumeResult.Done;

                // Capacity
                float globalCap = controller.FuelStorageCapacityKg > 0f
                    ? controller.FuelStorageCapacityKg
                    : (storage.capacityKg > 0f ? storage.capacityKg : float.PositiveInfinity);

                float storedBefore = GetTotalStoredMass(storage);
                float remain = Mathf.Max(0f, globalCap - storedBefore);
                if (remain <= 0.0005f)
                    return ConsumeResult.Done;

                float available = pe.Mass;
                float bite = Mathf.Min(available, remain);
                if (bite <= 0.0005f)
                    return ConsumeResult.Done;

                // Always split from the world pickupable and store the split; do not store the original target
                var splitPU = pu.Take(bite);
                if (splitPU == null)
                    return ConsumeResult.Done;

                storage.Store(splitPU.gameObject, true);

                // If source chunk runs out, destroy it
                if (pe.Mass <= 0.0001f)
                    Util.KDestroyGameObject(smi.TargetGO);

                // Clear assignment on any successful bite so the behaviour tag drops and Glom can move/seek normally
                smi.TargetGO = null;

                return ConsumeResult.Success;
            }
            catch
            {
                return ConsumeResult.Done;
            }
        }

        private static float GetTotalStoredMass(Storage storage)
        {
            if (storage == null || storage.items == null) return 0f;
            float total = 0f;
            for (int i = 0; i < storage.items.Count; i++)
            {
                var go = storage.items[i];
                if (go == null) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null) total += pe.Mass;
            }
            return total;
        }
    }
}