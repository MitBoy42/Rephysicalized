using HarmonyLib;
using Klei;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.ModDb
{
	class ModTagUiSprites
	{



		internal static void SetMissingTagSprites(object item, ref Tuple<Sprite, Color> __result)
		{


			if (__result.first.name == "unknown" && item is Tag t)
			{
				if (t == ModTags.OxidizerGas)
				{
                    Element element = ElementLoader.GetElement(SimHashes.Oxygen.CreateTag());

                    var UISprite = Def.GetUISprite(element);
                    __result = UISprite;
                }
				else if (t == ModTags.RichSoil)
				{

					Element element = ElementLoader.GetElement(SimHashes.Dirt.CreateTag());

					var UISprite = Def.GetUISprite(element);
					__result = UISprite;


				}
                else if (t == ModTags.Distillable)
                {

                    Element element = ElementLoader.GetElement(SimHashes.SlimeMold.CreateTag());

                    var UISprite = Def.GetUISprite(element);
                    __result = UISprite;


                }
            }
		}
	}
}