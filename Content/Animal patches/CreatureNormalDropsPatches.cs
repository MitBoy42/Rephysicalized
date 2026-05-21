using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{


    [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction")]
    public static class Consolidated_EntityTemplates_DeathDropFunction
    {
        public static void Prefix(GameObject inst, ref float onDeathDropCount, ref string onDeathDropID)
        {

            var kpid = inst.GetComponent<KPrefabID>();

            Tag pt = kpid.PrefabTag;

                if (pt == new Tag(SquirrelHugConfig.ID) || pt == new Tag(BabySquirrelHugConfig.ID))
                {
                    onDeathDropID = "Meat";
                    onDeathDropCount = 0.5f;
                    return;
                }
         if (pt == new Tag(SealConfig.ID) || pt == new Tag(BabySealConfig.ID))
                {
                    onDeathDropID = "Tallow";
                    onDeathDropCount = 1f;
                    return;
                }

                if (pt == new Tag(MoleConfig.ID) || pt == new Tag(BabyMoleConfig.ID) || pt == new Tag(MoleDelicacyConfig.ID) || pt == new Tag(BabyMoleDelicacyConfig.ID))
                {
                    onDeathDropCount = 1f;
                    return;
                }
            if (pt == new Tag(PrehistoricPacuConfig.ID) || pt == new Tag(BabyPrehistoricPacuConfig.ID))
            {
                onDeathDropCount = 2f;
                return;
            }
            if (pt == new Tag(IceBellyConfig.ID) || pt == new Tag(BabyIceBellyConfig.ID) || pt == new Tag(GoldBellyConfig.ID) || pt == new Tag(BabyGoldBellyConfig.ID) 
                || pt == new Tag(StegoConfig.ID) || pt == new Tag(BabyStegoConfig.ID) || pt == new Tag(AlgaeStegoConfig.ID) || pt == new Tag(BabyAlgaeStegoConfig.ID))
            {
                onDeathDropCount = 4f;
                return;
            }
        }
    }
}