using HarmonyLib;
using Rephysicalized.Content.ModDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized
{
	class Def_Patches
	{

		[HarmonyPatch(typeof(Def), nameof(Def.GetUISprite), [typeof(object), typeof(string), typeof(bool)])]
		public class Def_GetUISprite_Patch
		{
			public static void Postfix(object item, ref Tuple<Sprite, Color> __result)
			{
				ModTagUiSprites.SetMissingTagSprites(item, ref __result);
			}
		}
	}

    class KanimGroupFile_Patches
    {
        [HarmonyPatch(typeof(KAnimGroupFile), nameof(KAnimGroupFile.Load))]
        public class KAnimGroupFile_Load_Patch
        {
            private const string MILKSEPARATOR_NAPHTHA = "anim_interacts_milk_separator_naphtha_kanim";
            public static void Prefix(KAnimGroupFile __instance)
            {
                InjectionMethods.RegisterCustomInteractAnim(__instance, MILKSEPARATOR_NAPHTHA);
            }
        }
    } 
}
