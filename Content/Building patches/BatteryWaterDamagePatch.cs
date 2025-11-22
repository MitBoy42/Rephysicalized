using Rephysicalized;
using STRINGS;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized {

    public class BatteryWaterDamagePatch : KMonoBehaviour, ISim4000ms
    {
        private Building building;
        private BuildingHP hp;
        private KSelectable selectable;
        private bool isBroken = false;

        public const float WATER_DAMAGE_CHANCE = 25f;

        // Notification infrastructure
        [MyCmpAdd] private Notifier notifier;
        private Notification waterRiskNotification;

        // Our building status item (created lazily)
        private static StatusItem s_WaterRiskStatusItem;

        public override void OnSpawn()
        {
            base.OnSpawn();
            building = GetComponent<Building>();
            hp = GetComponent<BuildingHP>();
            selectable = GetComponent<KSelectable>();
        }

        public override void OnCleanUp()
        {
            // Ensure any active notification is removed
            if (notifier != null && waterRiskNotification != null)
            {
                notifier.Remove(waterRiskNotification);
                waterRiskNotification = null;
            }

            // Ensure status item is removed
            if (selectable != null && s_WaterRiskStatusItem != null)
            {
                selectable.RemoveStatusItem(s_WaterRiskStatusItem, false);
            }

            base.OnCleanUp();
        }

        public void Sim4000ms(float dt)
        {
            EvaluateWaterDamage(dt);
        }

        public void EvaluateWaterDamage(float dt)
        {
            // Early-out if broken (and cleanup UI)
            if (hp != null && hp.IsBroken)
            {
                ClearUI();
                return;
            }

            // Check all occupied cells by the building for liquid (water) presence
            bool isInWater = false;

            if (building == null)
                building = GetComponent<Building>();

            if (building != null)
            {
                var cells = building.PlacementCells;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    if (Grid.IsValidCell(cell) && Grid.Element[cell].HasTag(GameTags.AnyWater))
                    {
                        isInWater = true;
                        break;
                    }
                }
            }

            if (!isInWater)
            {
                ClearUI();
                return;
            }

            // Same gating as damage: must be a battery with charge available
            var battery = GetComponent<Battery>();
            if (battery == null || battery.JoulesAvailable <= 0f)
            {
                ClearUI();
                return;
            }

            // At this point: submerged, charged, and not broken -> show notification + status item
            EnsureWaterRiskNotification();
            EnsureWaterRiskStatusItem();

            // If already marked exploded (isBroken) but hp still > 0 (defensive), do nothing else
            if (isBroken && hp != null && hp.HitPoints > 0)
                return;

            // Chance to take damage per tick
            if (UnityEngine.Random.Range(1, 101) > WATER_DAMAGE_CHANCE)
                return;

            // Apply small damage
            if (hp != null)
            {
                float damage = UnityEngine.Random.Range(0.1f, 2.0f) * dt; // Tune as desired
                hp.DoDamage((int)damage);

                if (!hp.IsBroken)
                {
                    // Immediate feedback on damage tick
                    PopFXManager.Instance.SpawnFX(
                        PopFXManager.Instance.sprite_Negative,
                        (string)UI.GAMEOBJECTEFFECTS.DAMAGE_POPS.POWER_BANK_WATER_DAMAGE,
                        this.transform
                    );

                    KFMOD.PlayOneShot(
                        GlobalAssets.GetSound("Battery_sparks_short"),
                        this.transform.position
                    );
                }

                // Handle "explosion"/failure
                if (hp.HitPoints <= 0 && !isBroken)
                {
                    isBroken = true;
                    ClearUI();
                    Explode();
                }
            }
        }

        private void ClearUI()
        {
            // Clear notification
            if (notifier != null && waterRiskNotification != null)
            {
                notifier.Remove(waterRiskNotification);
                waterRiskNotification = null;
            }
            // Clear status item
            if (selectable != null && s_WaterRiskStatusItem != null)
            {
                selectable.RemoveStatusItem(s_WaterRiskStatusItem, false);
            }
        }

        private void EnsureWaterRiskNotification()
        {
            if (notifier == null)
                return;

            if (waterRiskNotification == null)
            {
                // Simple notification with a one-line tooltip
                waterRiskNotification = new Notification(
                    title: STRINGS.BATTERYDAMAGE.NOTIFICATION.NAME,
                    type: NotificationType.BadMinor,
                    tooltip: (notificationList, data) =>
                   
                    STRINGS.BATTERYDAMAGE.NOTIFICATION.TOOLTIP,
                    tooltip_data: null,
                    expires: false,
                    custom_click_callback: null
                );
                notifier.Add(waterRiskNotification);
            }
        }

        private static void EnsureStatusItemInstance()
        {
            if (s_WaterRiskStatusItem != null)
                return;

            // Create a basic building status item with exclamation icon
            s_WaterRiskStatusItem = new StatusItem(
                id: "BatteryWaterDamageRisk",
                name: STRINGS.BATTERYDAMAGE.STATUS.NAME,
                tooltip: STRINGS.BATTERYDAMAGE.STATUS.TOOLTIP,
                icon: "status_item_exclamation",
                icon_type: StatusItem.IconType.Exclamation,
                notification_type: NotificationType.BadMinor,
                allow_multiples: false,
                render_overlay: OverlayModes.None.ID,
                status_overlays: 129022,
                showWorldIcon: true
            );
        }

        private void EnsureWaterRiskStatusItem()
        {
            EnsureStatusItemInstance();
            if (selectable != null && s_WaterRiskStatusItem != null)
            {
                // AddStatusItem is idempotent for the same StatusItem reference on a KSelectable, so safe to call repeatedly
                selectable.AddStatusItem(s_WaterRiskStatusItem, null);
            }
        }

        private void Explode()
        {
            // Visual/sound effect; no extra element transformations beyond keeping current state
            Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactMetal, this.transform.position, 0.0f);
            KFMOD.PlayOneShot(GlobalAssets.GetSound("Battery_explode"), this.transform.position);

            var battery = GetComponent<Battery>();
            float charge = battery != null ? battery.JoulesAvailable : 0f;

            int cell = Grid.PosToCell(this.gameObject.transform.position);
            float temperature = Mathf.Clamp(
                Grid.Temperature[cell] + (Grid.Mass[cell] > 0f ? charge / (Grid.Mass[cell] * Grid.Element[cell].specificHeatCapacity) : 0f),
                1f, 9999f
            );

            SimMessages.ReplaceElement(
                cell,
                Grid.Element[cell].id,
                CellEventLogger.Instance.SandBoxTool,
                Grid.Mass[cell],
                temperature,
                Grid.DiseaseIdx[cell],
                Grid.DiseaseCount[cell]
            );

            Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactMetal, this.gameObject.transform.position, 0.0f);
            KFMOD.PlayOneShot(GlobalAssets.GetSound("Battery_explode"), this.gameObject.transform.position);
        }
    } }