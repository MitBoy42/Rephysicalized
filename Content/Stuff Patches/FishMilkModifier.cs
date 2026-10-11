using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Klei.AI;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.Animal_patches
{

    internal class FishMilkModifier
    {
        private static Effect fishMilkHappinessEffect;

        private static void EnsureEffect()
        {
      
            fishMilkHappinessEffect = new Effect(
                STRINGS.STATUSITEMS.INFISHMILK.NAME,
                 STRINGS.STATUSITEMS.INFISHMILK.NAME,
                 STRINGS.STATUSITEMS.INFISHMILK.TOOLTIP,
                0.0f,
                true,
                true,
                false);

                fishMilkHappinessEffect.Add(new AttributeModifier(Db.Get().CritterAttributes.Happiness.Id, 1f, STRINGS.STATUSITEMS.INFISHMILK.EFFECT));
            fishMilkHappinessEffect.Add(new AttributeModifier(Db.Get().Amounts.HitPoints.deltaAttribute.Id, 0.1f, STRINGS.STATUSITEMS.INFISHMILK.EFFECT))
        ;
        }


        private static bool CreatureIsInFishMilk(CritterTemperatureMonitor.Instance smi)
        {
            var occ = smi.occupyArea;
            if (occ == null || occ.OccupiedCellsOffsets == null || occ.OccupiedCellsOffsets.Length == 0)
                return false;

            int cachedCell = smi.pickupable != null ? smi.pickupable.cachedCell : -1;
            if (cachedCell < 0)
                return false;

            for (int i = 0; i < occ.OccupiedCellsOffsets.Length; i++)
            {
                int cell = Grid.OffsetCell(cachedCell, occ.OccupiedCellsOffsets[i]);
                if (Grid.IsValidCell(cell) && Grid.Element[cell].id == SimHashes.FishMilk)
                    return true;
            }

            return false;
        }

        private static void UpdateFishMilkEffect(CritterTemperatureMonitor.Instance smi)
        {
            if (smi == null)
                return;

            EnsureEffect();


            var effects = smi.gameObject.GetComponent<Effects>();
            if (effects == null)
                return;

            bool inFishMilk = CreatureIsInFishMilk(smi);

            if (inFishMilk)
            {
                if (!effects.HasEffect(fishMilkHappinessEffect.Id))
                    effects.Add(fishMilkHappinessEffect, false);
            }
            else
            {
                if (effects.HasEffect(fishMilkHappinessEffect.Id))
                    effects.Remove(fishMilkHappinessEffect.Id);
            }
        }

        [HarmonyPatch(typeof(CritterTemperatureMonitor), nameof(CritterTemperatureMonitor.UpdateInternalTemperature))]
        private static class CritterTemperatureMonitor_UpdateInternalTemperature_Patch
        {
            private static void Postfix(CritterTemperatureMonitor.Instance smi, float dt)
            {
                UpdateFishMilkEffect(smi);
            }
        }
    }
}

