﻿﻿using System;
using HarmonyLib;
using STRINGS;
using TUNING;
using UnityEngine;
using Database;
using Klei.AI;


namespace Rephysicalized.Content.Building_patches
{
    internal class MilkFeederPatches
    {
        private const string HadSugarwaterEffectId = "HadSugarwater";
        private const float SugarwaterEffectDurationSeconds = 600f;

        // Harmony target for EffectsPerDrinkableLiquid is inconsistent across decompiles/publised builds.
        // Using a Prefix that directly edits the static array avoids relying on patching a missing getter.
        private static void EnsureSugarwaterMapping()
        {

            var arr = MilkFeederConfig.EffectsPerDrinkableLiquid;
            if (arr == null)
                return;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].second == HadSugarwaterEffectId)
                    return;
            }

            var newArr = new Tuple<Tag, string>[arr.Length + 1];
            Array.Copy(arr, newArr, arr.Length);
            newArr[newArr.Length - 1] = new Tuple<Tag, string>(SimHashes.SugarWater.CreateTag(), HadSugarwaterEffectId);

            // Try to set the field via reflection (may be readonly depending on decompile).
       
                var field = typeof(MilkFeederConfig).GetField(
                    nameof(MilkFeederConfig.EffectsPerDrinkableLiquid),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                field?.SetValue(null, newArr);
      




        }

        [HarmonyPatch(typeof(MilkFeederConfig), "CreateBuildingDef")]
        private static class MilkFeederConfig_CreateBuildingDef_Patch
        {
            private static void Postfix()
            {
                EnsureSugarwaterMapping();
            }
        }





        // Add the new effect to ModifierSet, identical in behavior to HadMilk.
        [HarmonyPatch(typeof(ModifierSet), "CreateCritteEffects")]
        private static class ModifierSet_CreateCritteEffects_Patch
        {
            public static void Postfix(ModifierSet __instance)
            {
                if (__instance == null || __instance.effects == null)
                    return;

                string name = STRINGS.STATUSITEMS.HADSUGARWATER.NAME;
                string tooltip = STRINGS.STATUSITEMS.HADSUGARWATER.TOOLTIP;



                Effect effect = new Effect(
                    HadSugarwaterEffectId,
                    name,
                    tooltip,
                    SugarwaterEffectDurationSeconds,
                    true,
                    true,
                    false);

                effect.Add(new AttributeModifier(Db.Get().CritterAttributes.Happiness.Id, 2f, name));
                __instance.effects.Add(effect);
            }
        }
    }
}


