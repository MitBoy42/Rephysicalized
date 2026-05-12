using HarmonyLib;
using Klei.AI;
using STRINGS;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

namespace Rephysicalized.Content.Stuff_Patches
{
    /// <summary>
    /// Patches to add SuperCoolant and ViscoGel as additional lubricant types for bionic dupes.
    /// Uses standard Harmony patches.
    /// </summary>
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class BionicOilPatches
    {
        private static bool lubricantsAdded = false;

        [HarmonyPostfix]
        public static void AddNewLubricantTypes()
        {
            if (lubricantsAdded)
                return;

            lubricantsAdded = true;

            var dict = BionicOilMonitor.LUBRICANT_TYPE_EFFECT;

            // Create SuperCoolant effect
            var effectSP = new Effect(
                "FreshOil_SuperCoolant",
                DUPLICANTS.MODIFIERS.FRESHOIL.NAME,
                DUPLICANTS.MODIFIERS.FRESHOIL.TOOLTIP,
                4800f,
                true,
                true,
                false,
                null,
                0f,
                null,
                false);
            effectSP.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, 4f, DUPLICANTS.MODIFIERS.FRESHOIL.NAME));
            effectSP.Add(new AttributeModifier(Db.Get().Amounts.Stress.deltaAttribute.Id, -0.03333334f, DUPLICANTS.MODIFIERS.FRESHOIL.NAME));
            dict[SimHashes.SuperCoolant] = effectSP;

            // Create ViscoGel effect
            var effectVG = new Effect(
                "FreshOil_ViscoGel",
                DUPLICANTS.MODIFIERS.FRESHOIL.NAME,
                DUPLICANTS.MODIFIERS.FRESHOIL.TOOLTIP,
                4800f,
                true,
                true,
                false,
                null,
                0f,
                null,
                false);
            effectVG.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, 3f, DUPLICANTS.MODIFIERS.FRESHOIL.NAME));
            effectVG.Add(new AttributeModifier(Db.Get().Amounts.Stress.deltaAttribute.Id, -0.01666667f, DUPLICANTS.MODIFIERS.FRESHOIL.NAME));
            effectVG.Add(new AttributeModifier(Db.Get().Attributes.RadiationResistance.Id, 0.25f, DUPLICANTS.MODIFIERS.FRESHOIL.NAME));
            dict[SimHashes.ViscoGel] = effectVG;
        }
    }


    }

