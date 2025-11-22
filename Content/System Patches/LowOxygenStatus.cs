using System;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches
{
    //
    // Usage:
    //   var s = go.AddOrGet<OxidizerLowStatus>();
    //   s.minRequiredMass = 0.05f;     // optional override (kg)
    //   s.oxidizerTag = ModTags.OxidizerGas; // default, override if needed
    //   s.explicitStorage = yourStorageComponent; // optional; recommended when the building has multiple storages
    //
    public sealed class OxidizerLowStatus : KMonoBehaviour, ISim1000ms
    {
        [MyCmpReq] private KSelectable selectable;   // required to display status items

        // If the building has a single Storage, this will be resolved.
        // If there are multiple storages (e.g., Kiln), prefer setting explicitStorage.
        [MyCmpGet] private Storage defaultStorage;

        // Optional: explicitly point at the storage that actually holds the oxidizer
        [SerializeField] public Storage explicitStorage;

        public Tag oxidizerTag = default;
        public float minRequiredMass = 0.04f;

        private Guid statusGuid = Guid.Empty;
        private static StatusItem cachedStatusItem;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();

            if (oxidizerTag == default)
                oxidizerTag = ModTags.OxidizerGas;

            if (cachedStatusItem == null)
            {
                cachedStatusItem = Db.Get().BuildingStatusItems.MegaBrainNotEnoughOxygen;
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            EvaluateAndUpdateStatus();
        }

        public override void OnCleanUp()
        {
            if (selectable != null && statusGuid != Guid.Empty)
            {
                selectable.RemoveStatusItem(statusGuid);
                statusGuid = Guid.Empty;
            }
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            EvaluateAndUpdateStatus();
        }

        private void EvaluateAndUpdateStatus()
        {
            float mass = GetAvailableOxidizerMass();
            bool hasEnough = mass >= minRequiredMass;
            bool shouldShow = !hasEnough;
            UpdateStatus(shouldShow);
        }

        private float GetAvailableOxidizerMass()
        {
            var tag = oxidizerTag == default ? ModTags.OxidizerGas : oxidizerTag;

            // 1) Prefer explicitly assigned storage (for buildings with multiple storages like Kiln)
            if (explicitStorage != null)
                return explicitStorage.GetMassAvailable(tag);

            // 2) Fall back to the default resolved storage
            if (defaultStorage != null)
                return defaultStorage.GetMassAvailable(tag);

            // 3) Last resort: sum all storages on this GO (not children)
            float total = 0f;
            var storages = GetComponents<Storage>();
            if (storages != null)
            {
                for (int i = 0; i < storages.Length; i++)
                {
                    var s = storages[i];
                    if (s != null)
                        total += s.GetMassAvailable(tag);
                }
            }
            return total;
        }

        private void UpdateStatus(bool show)
        {
            if (selectable == null || cachedStatusItem == null)
                return;

            if (show)
            {
                if (statusGuid == Guid.Empty)
                    statusGuid = selectable.AddStatusItem(cachedStatusItem, this);
            }
            else
            {
                if (statusGuid != Guid.Empty)
                {
                    selectable.RemoveStatusItem(statusGuid);
                    statusGuid = Guid.Empty;
                }
            }
        }
    }
}