using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches.Boosted_Buildings
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class BoostedBuildings : StateMachineComponent<BoostedBuildings.SMInstance>
    {
        public sealed class Configuration
        {
            public Tag ItemTag;
            public float RequiredAmount = 1f;
            public float StorageCapacity = 100f;
            public float DeliveryCapacity = 50f;
            public float RefillMass = 5f;
            public KAnimFile[] BoostedAnimations;
            public LocString EnableButtonText;
            public LocString EnableButtonTooltip;
            public LocString DisableButtonText;
            public LocString DisableButtonTooltip;
        }

        [MyCmpReq]
        private Storage storage;

        [MyCmpReq]
        private ManualDeliveryKG manualDelivery;

        [MyCmpReq]
        private KBatchedAnimController animController;

        [Serialize]
        private bool deliverItems;

        private Tag itemTag;
        private float requiredAmount;
        private float deliveryCapacity;
        private float refillMass;
        private KAnimFile[] normalAnimations;
        private KAnimFile[] boostedAnimations;
        private LocString enableButtonText;
        private LocString enableButtonTooltip;
        private LocString disableButtonText;
        private LocString disableButtonTooltip;

        public bool HasRequiredItems => storage.GetAmountAvailable(itemTag) >= requiredAmount;

        public static BoostedBuildings Configure(GameObject go, Configuration configuration)
        {
            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = configuration.StorageCapacity;
            storage.storageFilters = new List<Tag> { configuration.ItemTag };
            storage.SetDefaultStoredItemModifiers(Storage.StandardFabricatorStorage);

            ManualDeliveryKG manualDelivery = go.AddOrGet<ManualDeliveryKG>();
            manualDelivery.SetStorage(storage);
            manualDelivery.RequestedItemTag = configuration.ItemTag;
            manualDelivery.choreTypeIDHash = Db.Get().ChoreTypes.Fetch.IdHash;
            manualDelivery.capacity = 0f;
            manualDelivery.refillMass = configuration.RefillMass;
            manualDelivery.MinimumMass = 0f;

            BoostedBuildings booster = go.AddOrGet<BoostedBuildings>();
            booster.itemTag = configuration.ItemTag;
            booster.requiredAmount = configuration.RequiredAmount;
            booster.deliveryCapacity = configuration.DeliveryCapacity;
            booster.refillMass = configuration.RefillMass;
            booster.boostedAnimations = configuration.BoostedAnimations;
            booster.enableButtonText = configuration.EnableButtonText;
            booster.enableButtonTooltip = configuration.EnableButtonTooltip;
            booster.disableButtonText = configuration.DisableButtonText;
            booster.disableButtonTooltip = configuration.DisableButtonTooltip;
            return booster;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            normalAnimations = GetComponent<Building>().Def.AnimFiles;
            manualDelivery.SetStorage(storage);
            SetItemDelivery(deliverItems);
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
            smi.StartSM();
            smi.sm.hasRequiredItems.Set(HasRequiredItems, smi);
        }

        public bool TryConsumeRequiredItems()
        {
            if (!HasRequiredItems)
                return false;

            storage.ConsumeIgnoringDisease(itemTag, requiredAmount);
            return true;
        }

        private void SetBoostedAnimation(bool boosted)
        {
            if (boosted)
            {
                if (boostedAnimations != null && boostedAnimations.Length > 0 && boostedAnimations[0] != null)
                    animController.SwapAnims(boostedAnimations);
            }
            else if (normalAnimations != null)
            {
                animController.SwapAnims(normalAnimations);
            }
        }

        private void SetItemDelivery(bool enabled)
        {
            deliverItems = enabled;
            manualDelivery.AbortDelivery("Changing boosted item delivery request");
            manualDelivery.capacity = enabled ? deliveryCapacity : 0f;
            manualDelivery.refillMass = enabled ? refillMass : 0f;
            manualDelivery.MinimumMass = enabled ? refillMass : 0f;
            smi.RefreshUIButton();
        }

        private void OnStorageChanged(object data)
        {
            smi.sm.hasRequiredItems.Set(HasRequiredItems, smi);
        }

        private void OnRefreshUserMenu(object data)
        {
            Game.Instance.userMenu.AddButton(gameObject, smi.buttonInfo);
        }

        public class States : GameStateMachine<States, SMInstance, BoostedBuildings>
        {
            public StateMachine<States, SMInstance, BoostedBuildings, object>.BoolParameter hasRequiredItems;
            public GameStateMachine<States, SMInstance, BoostedBuildings, object>.State normal;
            public GameStateMachine<States, SMInstance, BoostedBuildings, object>.State boosted;

            public override void InitializeStates(out StateMachine.BaseState default_state)
            {
                default_state = normal;
                normal.ParamTransition<bool>(hasRequiredItems, boosted, (smi, hasItems) => hasItems);
                boosted.ToggleStatusItem(
                        "Boosted",
                        "This building is boosted while it has the required items.",
                        icon_type: StatusItem.IconType.Info,
                        notification_type: NotificationType.Neutral,
                        render_overlay: OverlayModes.None.ID)
                    .Enter("Use boosted animation", smi => smi.master.SetBoostedAnimation(true))
                    .Exit("Restore original animation", smi => smi.master.SetBoostedAnimation(false))
                    .ParamTransition<bool>(hasRequiredItems, normal, (smi, hasItems) => !hasItems);
            }

        }

        public class SMInstance : GameStateMachine<States, SMInstance, BoostedBuildings, object>.GameInstance
        {
            public KIconButtonMenu.ButtonInfo buttonInfo;

            public SMInstance(BoostedBuildings master) : base(master)
            {
                buttonInfo = new KIconButtonMenu.ButtonInfo("action_speed_up", (string)master.enableButtonText,
                    tooltipText: (string)master.enableButtonTooltip);
                RefreshUIButton();
            }

            public void RefreshUIButton()
            {
                buttonInfo.iconName = "action_speed_up";
                buttonInfo.text = master.deliverItems
                    ? (string)master.disableButtonText
                    : (string)master.enableButtonText;
                buttonInfo.tooltipText = master.deliverItems
                    ? (string)master.disableButtonTooltip
                    : (string)master.enableButtonTooltip;
                buttonInfo.onClick = () => master.SetItemDelivery(!master.deliverItems);
            }
        }
    }
}
