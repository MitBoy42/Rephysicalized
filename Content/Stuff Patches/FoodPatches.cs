using ElementUtilNamespace;
using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUNING;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.System_Patches
{
    internal static class CookedEgg_TempCook_Util
    {
        internal static void DisableTemperatureCookable(GameObject go)
        {
            if (go == null) return;

            var tc = go.GetComponent<TemperatureCookable>();
            if (tc != null)
            {
                tc.enabled = false;

                tc.cookedID = null;
            }
        }
        [HarmonyPatch(typeof(CookedEggConfig), nameof(CookedEggConfig.CreatePrefab))]
        public static class CookedEgg_CreatePrefab_Patch
        {
            // Postfix runs after the base game creates the prefab
            public static void Postfix(ref GameObject __result)
            {
                DisableTemperatureCookable(__result);

            }
        }


        [HarmonyPatch(typeof(EntityTemplates))]
        public static class EntityTemplatesFoodElementPatch
        {
            [HarmonyPatch(nameof(EntityTemplates.ExtendEntityToFood), new Type[] { typeof(GameObject), typeof(EdiblesManager.FoodInfo), typeof(bool) })]
            [HarmonyPostfix]
            public static void Postfix(GameObject template)
            {
                try
                {
                    var pe = template.GetComponent<PrimaryElement>();
                    if (pe.ElementID == SimHashes.Creature || pe.ElementID == SimHashes.Vacuum)
                    {
                        pe.ElementID = SimHashes.Dirt;
                    }
                }
                catch (Exception e)
                {
                }
            }
        }


        [HarmonyPatch(typeof(Db), "Initialize")]
        public static class PemmicanFruitcakeSpoilPatch
        {
            private const float SecondsPerCycle = 600f;
            private const float SpoilSeconds = 48f * SecondsPerCycle;

            public static void Postfix()
            {
                // Pemmican (DLC2)
                var pemmican = FOOD.FOOD_TYPES.PEMMICAN;
                pemmican.CanRot = true;
                pemmican.SpoilTime = SpoilSeconds;

                // Fruitcake (base)
                var fruitcake = FOOD.FOOD_TYPES.FRUITCAKE;
                fruitcake.CanRot = true;
                fruitcake.SpoilTime = SpoilSeconds;
            }
        }

        [HarmonyPatch(typeof(MedicinalPillWorkable), nameof(MedicinalPillWorkable.OnSpawn))]
        public static class MedicinalPillWorkable_OnSpawn_Patch
        {
            public static void Postfix(MedicinalPillWorkable __instance)
            {
                // Override the default SetWorkTime(10f) set by the original OnSpawn
                __instance.SetWorkTime(2f);
            }
        }

    }
  
     
}
