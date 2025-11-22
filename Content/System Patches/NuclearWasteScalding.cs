using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace Rephysicalized
{
    internal static class CorrosiveScaldingConfig
    {
        // Mass threshold in kg
        public static float WasteMassThresholdKg = 20f;

        // Direct damage per second while in corrosive liquid
        public static float DamagePerSecond = 0.5f;

        // Immune creature prefab IDs (case-insensitive)
        public static readonly HashSet<string> ImmuneCritterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HatchMetal",
            "HatchMetalBaby",
            "StaterPillarLiquid",
            "BabyStaterPillarLiquid",
            "BabyBee",
            "Bee",
            "LightBugBlack",
            "LightBugCrystal",
             "Glom",
        };
    }

    internal static class CorrosiveScaldingStatus
    {
        public const string Id = "CorrosiveLiquidScalding_Creature";
        public static StatusItem Item;
    }

    // Create our new status item alongside the built-in CreatureStatusItems
    [HarmonyPatch(typeof(Database.CreatureStatusItems), "CreateStatusItems")]
    internal static class DB_CreatureStatusItems_CreateStatusItems_Postfix
    {
        private static void Postfix(Database.CreatureStatusItems __instance)
        {
            if (CorrosiveScaldingStatus.Item != null)
                return;

            CorrosiveScaldingStatus.Item = new StatusItem(
      id: CorrosiveScaldingStatus.Id,
      name: "Corrosive liquid scalding",
      tooltip: "This critter is scalding from corrosive liquid",
      icon: "status_item_exclamation",
      icon_type: StatusItem.IconType.Exclamation,
      notification_type: NotificationType.Bad,   // not used since we won’t add notifications
      allow_multiples: false,
      render_overlay: OverlayModes.None.ID,
  
      showWorldIcon: false                           // do not show world icon
  );

        }
    }

    // Attach our hazard component to all creatures
    [HarmonyPatch(typeof(CreatureBrain), "OnPrefabInit")]
    internal static class CreatureBrain_OnPrefabInit_AddCorrosiveScalding
    {
        private static void Postfix(CreatureBrain __instance)
        {
            if (__instance == null) return;
            var go = __instance.gameObject;
            if (go.GetComponent<CorrosiveScaldingCritter>() == null)
                go.AddComponent<CorrosiveScaldingCritter>();
        }
    }

    // Applies status and direct damage when a creature stands in > 20 kg Nuclear Waste
    public sealed class CorrosiveScaldingCritter : KMonoBehaviour, ISim4000ms
    {
        private KPrefabID _kpid;
        private KSelectable _selectable;
        private Health _health;
        private bool _statusActive;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _kpid = GetComponent<KPrefabID>();
            _selectable = GetComponent<KSelectable>();
            _health = GetComponent<Health>();
        }

        public override void OnCleanUp()
        {
            SetStatus(false);
            base.OnCleanUp();
        }

        // Runs every ~1000 ms
        public void Sim4000ms(float dt)
        {
            // Only creatures; ignore dupes/plants
            if (_kpid == null || !_kpid.HasTag(GameTags.Creature))
                return;

            // Respect immunity list
            string id = _kpid.PrefabID().Name;
            if (CorrosiveScaldingConfig.ImmuneCritterIds.Contains(id))
            {
                SetStatus(false);
                return;
            }

            bool inHazard = IsInHeavyNuclearWaste(gameObject, CorrosiveScaldingConfig.WasteMassThresholdKg);

            if (inHazard)
            {
                SetStatus(true);

                // Apply direct damage per second (matches cadence with scalding-like DOT)
                if (_health != null && CorrosiveScaldingConfig.DamagePerSecond > 0f)
                {
                    float dmg = CorrosiveScaldingConfig.DamagePerSecond * Mathf.Max(0.001f, dt);
                    _health.Damage(dmg);
                }
            }
            else
            {
                SetStatus(false);
            }
        }

        private void SetStatus(bool on)
        {
            if (_selectable == null || CorrosiveScaldingStatus.Item == null)
                return;

            if (on && !_statusActive)
            {
                _selectable.AddStatusItem(CorrosiveScaldingStatus.Item, this);
                _statusActive = true;
            }
            else if (!on && _statusActive)
            {
                _selectable.RemoveStatusItem(CorrosiveScaldingStatus.Item, false);
                _statusActive = false;
            }
        }

        private static bool IsInHeavyNuclearWaste(GameObject go, float thresholdKg)
        {
            int root = Grid.PosToCell(go);
            if (!Grid.IsValidCell(root))
                return false;

            var occ = go.GetComponent<OccupyArea>();
            if (occ != null && occ.OccupiedCellsOffsets != null && occ.OccupiedCellsOffsets.Length > 0)
            {
                for (int i = 0; i < occ.OccupiedCellsOffsets.Length; i++)
                {
                    int c = Grid.OffsetCell(root, occ.OccupiedCellsOffsets[i]);
                    if (!Grid.IsValidCell(c)) continue;
                    if (Grid.Element[c].id == SimHashes.NuclearWaste && Grid.Mass[c] > thresholdKg)
                        return true;
                }
                return false;
            }

            return Grid.Element[root].id == SimHashes.NuclearWaste && Grid.Mass[root] > thresholdKg;
        }
    }
}