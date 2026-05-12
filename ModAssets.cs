
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized
{
    public class ModTags
    {
        public static Tag OxidizerGas = TagManager.Create("OxidizerGas");
        public static Tag RichSoil = TagManager.Create("RichSoil");
        public static Tag Distillable = TagManager.Create("Distillable");
        public static Tag RephysSublimator = TagManager.Create("RephysSublimator");
    }

    public class ModAssets
    {
        public static Sprite MetalCapacityIcon;


    

        [HarmonyPatch(typeof(Assets), "OnPrefabInit")]
        public class Assets_OnPrefabInit_Patch
        {

            public static string MetalCapacity = "ui_icon_metalcapacity";
            [HarmonyPriority(Priority.LowerThanNormal)]
            public static void Prefix(Assets __instance)
            {
                ModAssets.MetalCapacityIcon = InjectionMethods.AddSpriteToAssets(__instance, MetalCapacity);
            }
        }
    }
}



