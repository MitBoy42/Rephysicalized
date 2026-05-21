//using HarmonyLib;
//using Klei.AI;
//using System;
//using System.Collections.Generic;
//using UnityEngine;


//namespace Rephysicalized.Content.Stuff_Patches
//{
//    internal static class CookingAnimals
//    {
//        private const float DeepFryTempK = 373.15f;

//        [HarmonyPatch(typeof(Butcherable), nameof(Butcherable.OnButcherComplete))]
//        private static class Butcherable_OnButcherComplete_CrabFreshWater_DeepFryShellfish
//        {
//            private static bool Prefix(Butcherable __instance)
//            {
//                if (__instance == null)
//                    return true;

//                GameObject go = __instance.gameObject;
//                if (go == null)
//                    return true;

//                // Must be CrabFreshWater
//                var kpid = go.GetComponent<KPrefabID>();
//                Tag prefabTag = kpid.PrefabTag;
//                // Tag can throw in Equals/ToString in some setups; compare underlying string safely.
//                string prefabTagStr = prefabTag.IsValid ? prefabTag.ToString() : null;
//                if (!string.Equals(prefabTagStr, CrabFreshWaterConfig.ID, StringComparison.Ordinal))
//                    return true;


//                // Must have tile element: RefinedLipid
//                int cell = Grid.PosToCell(go);
//                if (!Grid.IsValidCell(cell))
//                    return true;

//                Element cellElement = Grid.Element[cell];
//                if (cellElement == null || cellElement.id != SimHashes.RefinedLipid)
//                    return true;

//                // Temperature check is on refined lipid element's cell (same style as EnviromentPreparation)
//                float cellTemp = Grid.Temperature[cell];
//                if (cellTemp < DeepFryTempK)
//                    return true;



//                // Replace drops with DeepFriedShellfish x1
//                __instance.drops = new Dictionary<string, float>(StringComparer.Ordinal)
//                {
//                    [DeepFriedShellfishConfig.ID] = 1f
//                };

//                // Continue into vanilla butcher completion with replaced drops
//                return true;
//            }
//        }
//    }
//}

