using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;


namespace Rephysicalized
{

    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]

    // 1) Add storage and an ElementConsumer that pulls Oxygen at 0.05 kg/s and stores it (capacity 5 kg).
    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]
    internal static class GlomConfig_CreatePrefab_AddOxygenConsumer
    {
        public static void Postfix(ref GameObject __result)
        {

            var ec = __result.AddComponent<ElementConsumer>();
            ec.elementToConsume = SimHashes.Oxygen;
            ec.consumptionRate = 0.05f;   
            ec.consumptionRadius = 2;     
            ec.capacityKG = 1f;
            ec.sampleCellOffset = Vector3.zero;
            ec.storeOnConsume = true;
            ec.showDescriptor = true;
            ec.isRequired = false;
            ec.ignoreActiveChanged = true;

            // Ensure O2 consumption turns on when Glom spawns
            __result.AddOrGet<EnableElementConsumptionOnSpawn>();

            // Resume consumption whenever storage changes (e.g., after an emission frees capacity)
            __result.AddOrGet<ResumeO2ConsumptionOnStorageChange>();

            // Marker used by the DropElement prefix for O2 deduction
            __result.AddOrGet<GlomOxygenGateMarker>();
        }
    }

    // Helper: force-enable ElementConsumer on spawn (creatures have no Operational)
    public sealed class EnableElementConsumptionOnSpawn : KMonoBehaviour
    {
        public override void OnSpawn()
        {
            base.OnSpawn();
            var ec = GetComponent<ElementConsumer>();
            ec.EnableConsumption(true);
            ec.RefreshConsumptionRate();

        }
    }

    // Helper: when storage contents change, re-enable ElementConsumer so intake resumes after freeing capacity
    public sealed class ResumeO2ConsumptionOnStorageChange : KMonoBehaviour
    {
        private ElementConsumer _ec;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _ec = GetComponent<ElementConsumer>();
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            base.OnCleanUp();
        }

        private void OnStorageChanged(object data)
        {
            if (_ec != null)
            {
                _ec.EnableConsumption(true);
                _ec.RefreshConsumptionRate();
            }
        }
    }

    // Utility marker with explicit O2 consumption and storage sealing
    public sealed class GlomOxygenGateMarker : KMonoBehaviour
    {
        internal Storage Storage => GetComponent<Storage>();

        internal static bool IsDirtyOxygen(SimHashes elem) => elem == SimHashes.ContaminatedOxygen;

        internal float GetTotalOxygenKg()
        {
            var st = Storage;
            if (st == null || st.items == null) return 0f;

            float total = 0f;
            for (int i = 0; i < st.items.Count; i++)
            {
                var go = st.items[i];
                if (go == null) continue;
                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.ElementID != SimHashes.Oxygen) continue;
                total += pe.Mass;
            }
            return total;
        }

        internal bool TryConsumeOxygen(float amountKg)
        {
            if (amountKg <= 0f) return false;
            var st = Storage;
            if (st == null || st.items == null) return false;

            float available = GetTotalOxygenKg();
            if (available + 1e-6f < amountKg)
                return false;

            float remaining = amountKg;
            // Iterate a copy because we may remove/destroy entries
            List<GameObject> itemsCopy = new List<GameObject>(st.items);

            for (int i = 0; i < itemsCopy.Count && remaining > 1e-6f; i++)
            {
                var go = itemsCopy[i];
                if (go == null) continue;

                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.ElementID != SimHashes.Oxygen) continue;

                float take = Mathf.Min(pe.Mass, remaining);
                if (take <= 0f) continue;

                pe.Mass -= take;
                remaining -= take;

                if (pe.Mass <= 1e-6f)
                {
                    // Remove emptied stack from storage and destroy it
                    st.Remove(go);
                    UnityEngine.Object.Destroy(go);
                }
            }

            bool success = remaining <= 1e-6f;

            if (success)
            {
                // Notify all listeners (including ElementConsumer) that storage mass changed
                st.Trigger((int)GameHashes.OnStorageChange, null);

                // Proactively resume consumption
                var ec = GetComponent<ElementConsumer>();
                if (ec != null)
                {
                    ec.EnableConsumption(true);
                    ec.RefreshConsumptionRate();
                }
            }

            return success;
        }
    }

    // Gate ALL element emissions at the single choke point: DropElement
    [HarmonyPatch(typeof(ElementDropperMonitor.Instance), nameof(ElementDropperMonitor.Instance.DropElement))]
    internal static class Glom_DropElement_GatedByOxygen
    {
        // We don't modify args; we only allow/skip emission
        public static bool Prefix(ElementDropperMonitor.Instance __instance, float mass, SimHashes element_id, byte disease_idx, int disease_count)
        {
            // Only act for Glom instances
            var kpid = __instance.GetComponent<KPrefabID>();
            if (kpid == null || kpid.PrefabID().Name != GlomConfig.ID)
                return true;

            // Only gate dirty oxygen emissions
            if (!GlomOxygenGateMarker.IsDirtyOxygen(element_id))
                return true;

            // Ensure marker + storage exist, then try to deduct O2 equal to the emission mass
            var marker = __instance.GetComponent<GlomOxygenGateMarker>();
            if (marker == null)
                return true;

            return marker.TryConsumeOxygen(Mathf.Max(0f, mass));
        }
    }




}
