using Database;
using ElementUtilNamespace;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

using UtilLibs;

using static ElementLoader;
using static Rephysicalized.STRINGS.ELEMENTS;



namespace Rephysicalized.ModElements
{


    public class ElementPatches
	{
		/// Credit: akis beached 
		[HarmonyPatch(typeof(ElementLoader))]
		[HarmonyPatch(nameof(ElementLoader.Load))]
		public class ElementLoader_Load_Patch
		{
			public static void Prefix(Dictionary<string, SubstanceTable> substanceTablesByDlc)
			{
				var list = substanceTablesByDlc[DlcManager.VANILLA_ID].GetList();
				ModElementRegistration.RegisterSubstances(list);
			}
		}
		[HarmonyPatch(typeof(ElementLoader), nameof(ElementLoader.FinaliseElementsTable))]
		public class ElementLoader_FinaliseElementsTable_Patch
		{
			public static void Postfix()
			{
				ModElements.ModifyExistingElements();
			}
		}



		[HarmonyPatch(typeof(ElementsAudio), "LoadData")]
		public class ElementsAudio_LoadData_Patch
		{
			public static void Postfix(ElementsAudio __instance, ref ElementsAudio.ElementAudioConfig[] ___elementAudioConfigs)
			{
				___elementAudioConfigs = ___elementAudioConfigs.AddRangeToArray(ModElementRegistration.CreateAudioConfigs(__instance));
			}
		}

		/// Required for Simhashes conversion to string to include the modded elements
		// Credit: Heinermann (Blood mod)
		public static class EnumPatch
		{
			[HarmonyPatch(typeof(Enum), "ToString", new Type[] { })]
			public class SimHashes_ToString_Patch
			{
				public static bool Prefix(ref Enum __instance, ref string __result) => SgtElementUtil.SimHashToString_EnumPatch(__instance, ref __result);
			}

			[HarmonyPatch(typeof(Enum), nameof(Enum.Parse), new Type[] { typeof(Type), typeof(string), typeof(bool) })]
			private class SimHashes_Parse_Patch
			{
				private static bool Prefix(Type enumType, string value, ref object __result) => SgtElementUtil.SimhashParse_EnumPatch(enumType, value, ref __result);
			}
		}
		public class ModElements 
		{


			static void AddTagsToElementAndEnable(SimHashes element, Tag[] tags = null)
			{
				var elementMaterial = ElementLoader.FindElementByHash(element);
				if (elementMaterial == null)
					return;
				elementMaterial.disabled = false;

				if (tags == null || tags.Length == 0)
					return;

				if (elementMaterial.oreTags == null)
					elementMaterial.oreTags = tags;
				else if (tags.Any())
					elementMaterial.oreTags = elementMaterial.oreTags.Concat(tags).ToArray();

			}
			internal static void ModifyExistingElements()
			{
				AddTagsToElementAndEnable(SimHashes.Oxygen, new[] { ModTags.OxidizerGas });
				AddTagsToElementAndEnable(SimHashes.ContaminatedOxygen, new[] { ModTags.OxidizerGas });
			
                AddTagsToElementAndEnable(ModElementRegistration.AshByproduct, new Tag[] { GameTags.Farmable });
                AddTagsToElementAndEnable(ModElementRegistration.CrudByproduct, new Tag[] { GameTags.BuildableRaw });

                AddTagsToElementAndEnable(SimHashes.Dirt, new[] { ModTags.RichSoil });
            AddTagsToElementAndEnable(ModElementRegistration.AshByproduct.SimHash, new[] { ModTags.RichSoil });
             
                AddTagsToElementAndEnable(SimHashes.SlimeMold, new[] { ModTags.Distillable });
                AddTagsToElementAndEnable(SimHashes.ToxicSand, new[] { ModTags.RephysSublimator });
                AddTagsToElementAndEnable(SimHashes.BleachStone, new[] { ModTags.RephysSublimator });
            }
		}


    }

	
[HarmonyPatch(typeof(UnstableGroundManager), "OnPrefabInit")]
public static class Patch_UnstableGroundManager_AddAshByproductFX
{
    public static void Prefix(UnstableGroundManager __instance)
    {
        // Use reflection to access EffectInfo type and effects field safely
        var effectsField = typeof(UnstableGroundManager).GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var effectInfos = (UnstableGroundManager.EffectInfo[])effectsField.GetValue(__instance);

        // If AshByproduct already exists, no need to patch
        if (effectInfos.Any(e => e.element == ModElementRegistration.AshByproduct))
            return;

        // Find a template (sand) effect to copy prefab from
        var template = effectInfos.FirstOrDefault(e =>
            e.element == SimHashes.Regolith
        );

        if (template.prefab == null)
        {
            Debug.LogError("[UnstableAsh] Could not find sand or regolith prefab to copy for AshByproduct!");
            return;
        }

        var newList = effectInfos.ToList();
        newList.Add(new UnstableGroundManager.EffectInfo
        {
            element = ModElementRegistration.AshByproduct,
            prefab = template.prefab
        });

        effectsField.SetValue(__instance, newList.ToArray());
     //   Debug.Log("[UnstableAsh] Registered falling FX for AshByproduct.");
    }
}


    internal class UraniumPatches
    {
        /// <summary>
        /// Set melting temperature (highTemp) for uranium-family elements to 1405°F.
        /// </summary>
        [HarmonyPatch(typeof(ElementLoader))]
        [HarmonyPatch(nameof(ElementLoader.CollectElementsFromYAML))]
        public static class Patch_ElementLoader_CollectElementsFromYAML_UraniumMelting
        {
            [HarmonyPriority(Priority.LowerThanNormal)]
            public static void Postfix(List<ElementEntry> __result)
            {

                  
					var elementIds = new[]
                    {
                        nameof(SimHashes.UraniumOre),
                        nameof(SimHashes.DepletedUranium),
                        nameof(SimHashes.EnrichedUranium)
                    };

                    foreach (var id in elementIds)
                    {
                        var entry = __result.FirstOrDefault(e => e.elementId == id);
                        if (entry != null)
                        {
                            entry.highTemp = 1405f;
                        }
                     
                    }
                

                // Resolve the modded AshByproduct ID string for YAML (Enum.ToString is patched, but use util for safety)
                string ashByproductId = "AshByProduct";

       
                SetHighTempSecondary(__result, nameof(SimHashes.WoodLog), ashByproductId, 0.5f);
                SetHighTempSecondary(__result, nameof(SimHashes.FabricatedWood), ashByproductId, 0.5f);

   
                SetHighTempSecondary(__result, nameof(SimHashes.MoltenSucrose), ashByproductId, 0.2f);

           
                SetHighTempSecondary(__result, nameof(SimHashes.Rust), nameof(SimHashes.Oxygen), 0.50f);
              
                SetHighTempSecondary(__result, nameof(SimHashes.Cinnabar), nameof(SimHashes.SulfurGas), 0.14f);

               
                SetHighTempSecondary(__result, nameof(SimHashes.LiquidGunk), nameof(SimHashes.RefinedCarbon), 0.20f);

                SetHighTempPrimary(__result, nameof(SimHashes.OxyRock), nameof(SimHashes.Iridium));
                SetSublimation(__result, nameof(SimHashes.OxyRock), 1f);
                SetHighTempSecondary(__result, nameof(SimHashes.OxyRock), nameof(SimHashes.Oxygen), 0.12f);
            }
              
                }
            

            private static void SetHighTempSecondary(List<ElementEntry> entries, string elementId, string oreId, float oreMassConversion)
        {
            var entry = entries?.FirstOrDefault(e => e.elementId == elementId);
            if (entry == null)
                return;

            entry.highTempTransitionOreId = oreId;
            entry.highTempTransitionOreMassConversion = oreMassConversion;
        }
        private static void SetHighTempPrimary(List<ElementEntry> entries, string elementId, string oreId)
        {
            var entry = entries?.FirstOrDefault(e => e.elementId == elementId);
            if (entry == null)
                return;

            entry.highTempTransitionTarget = oreId;
        
        }
        private static void SetSublimation(List<ElementEntry> entries, string elementId, float SublimateEfficiency)
        {
            var entry = entries?.FirstOrDefault(e => e.elementId == elementId);
            if (entry == null)
                return;

            entry.sublimateEfficiency = SublimateEfficiency;

        }
    }
}
            
        
    

