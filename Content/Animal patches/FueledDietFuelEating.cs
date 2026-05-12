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

                Tag elementTag = FueledDietUtils.ResolveElementTag(pe);
                Tag prefabTag = Tag.Invalid;
                if (smi.TargetGO.TryGetComponent<KPrefabID>(out var kpid))
                    prefabTag = kpid.PrefabTag;

                bool allowed = diet.FuelInputs.Any(fi =>
                    fi != null && (fi.ElementTag == prefabTag || fi.ElementTag == elementTag));
                if (!allowed)
                    return ConsumeResult.Done;

                // Capacity
                float globalCap = controller.FuelStorageCapacityKg > 0f
                    ? controller.FuelStorageCapacityKg
                    : (storage.capacityKg > 0f ? storage.capacityKg : float.PositiveInfinity);

                float storedBefore = FueledDietUtils.GetStorageUsedKg(storage);
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


    }

    internal static class FueledDietUtils
    {
        public static float GetStorageUsedKg(Storage storage)
        {
            if (storage?.items == null) return 0f;
            float used = 0f;
            for (int i = 0; i < storage.items.Count; i++)
            {
                var go = storage.items[i];
                var pe = go?.GetComponent<PrimaryElement>();
                if (pe != null) used += pe.Mass;
            }
            return used;
        }

        public static float ConsumeFromStorage(Storage storage, Tag tag, float maxKg, out float avgTempK, out byte diseaseIdx, out int diseaseCount)
        {
            avgTempK = 0f;
            diseaseIdx = byte.MaxValue;
            diseaseCount = 0;
            if (maxKg <= 0f || storage == null) return 0f;

            float remaining = maxKg;
            float weightedTempSum = 0f;
            float takenTotal = 0f;

            var items = ListPool<GameObject, Storage>.Allocate();
            if (storage.items != null) items.AddRange(storage.items);

            for (int i = 0; i < items.Count && remaining > 1e-6f; i++)
            {
                var go = items[i];
                if (go == null || !go.HasTag(tag)) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f) continue;

                float beforeMass = pe.Mass;
                float take = Mathf.Min(beforeMass, remaining);
                pe.Mass -= take;
                weightedTempSum += take * pe.Temperature;
                takenTotal += take;
                remaining -= take;

                // Disease merging
                if (pe.DiseaseIdx != byte.MaxValue && pe.DiseaseCount > 0)
                {
                    var fraction = take / beforeMass;
                    int takenDisease = Mathf.RoundToInt(pe.DiseaseCount * fraction);
                    MergeDisease(ref diseaseIdx, ref diseaseCount, pe.DiseaseIdx, takenDisease);
                }

                if (pe.Mass <= 1e-6f)
                {
                    storage.Drop(go, true);
                    UnityEngine.Object.Destroy(go);
                }
            }
            items.Recycle();

            if (takenTotal > 0f) avgTempK = weightedTempSum / takenTotal;
            return takenTotal;
        }

        public static float ConsumeFuelUpTo(Storage storage, float kgNeeded)
        {
            float remaining = Mathf.Max(0f, kgNeeded);
            float consumed = 0f;
            if (storage?.items == null) return 0f;

            for (int j = storage.items.Count - 1; j >= 0 && remaining > 0f; j--)
            {
                var go = storage.items[j];
                var pe = go?.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f) continue;

                float take = Mathf.Min(remaining, pe.Mass);
                pe.Mass -= take;
                remaining -= take;
                consumed += take;

                if (pe.Mass <= 0.0001f)
                    UnityEngine.Object.Destroy(go);
            }
            return consumed;
        }

        public static float TotalFuelKg(Storage storage) => GetStorageUsedKg(storage);

        private static void MergeDisease(ref byte idx, ref int count, byte candidateIdx, int candidateCount)
        {
            if (candidateCount <= 0) return;
            if (idx == byte.MaxValue || idx == candidateIdx)
            {
                idx = candidateIdx;
                count += candidateCount;
            }
            else if (candidateCount > count)
            {
                idx = candidateIdx;
                count = candidateCount;
            }
        }

        public static Tag ResolveElementTag(PrimaryElement pe)
        {
            if (pe == null || pe.ElementID == SimHashes.Vacuum) return Tag.Invalid;
            var elem = ElementLoader.FindElementByHash(pe.ElementID);
            return elem?.tag ?? Tag.Invalid;
        }

        public static bool TryResolveSimHash(Tag tag, out SimHashes hash)
        {
            foreach (var elem in ElementLoader.elements)
            {
                if (elem != null && elem.tag == tag)
                {
                    hash = elem.id;
                    return true;
                }
            }
            hash = SimHashes.Vacuum;
            return false;
        }

        public static bool CanReachCellSafe(Navigator nav, int cell)
        {
            try
            {
                return nav != null && Grid.IsValidCell(cell) && nav.CanReach(cell);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAtOrAdjacent(int myCell, int targetCell)
        {
            if (!Grid.IsValidCell(myCell) || !Grid.IsValidCell(targetCell)) return false;
            if (myCell == targetCell) return true;
            return myCell == Grid.OffsetCell(targetCell, 1, 0) ||
                   myCell == Grid.OffsetCell(targetCell, -1, 0) ||
                   myCell == Grid.OffsetCell(targetCell, 0, 1) ||
                   myCell == Grid.OffsetCell(targetCell, 0, -1);
        }

        public static float GetRefillThresholdKg(GameObject go, Storage storage)
        {
            var controller = go.GetComponent<FueledDietController>();
            float threshold = controller?.RefillThreshold ?? 0f;
            return !float.IsNaN(threshold) && threshold > 0f ? threshold : 0f;
        }
    }
}
