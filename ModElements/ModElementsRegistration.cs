using ElementUtilNamespace;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;
using UtilLibs.ElementUtilNamespace;


namespace Rephysicalized.ModElements
{
    public class ModElementRegistration
    {
        public static ElementInfo
            AshByproduct = ElementInfo.Solid("AshByproduct", UIUtils.rgb(180, 180, 180));

        public static ElementInfo CrudByproduct = ElementInfo.Solid("CrudByproduct", UIUtils.rgb(80, 60, 60));

        public static void RegisterSubstances(List<Substance> list)
        {
            var newElements = new HashSet<Substance>()
            {
                AshByproduct.CreateSubstance(),
                CrudByproduct.CreateSubstance(),
            };
            list.AddRange(newElements);

        }
        public static ElementsAudio.ElementAudioConfig[] CreateAudioConfigs(ElementsAudio instance)
        {
            return new[]
            {
                SgtElementUtil.CopyElementAudioConfig(SimHashes.Sand, AshByproduct),
                        SgtElementUtil.CopyElementAudioConfig(SimHashes.IgneousRock, CrudByproduct),
            };

        }

        [HarmonyPatch(typeof(LegacyModMain), "ConfigElements")]
        public static class LegacyModMain_ConfigElements_Patch
        {
            public static void Postfix()
            {
                try
                {
                    ModElementRegistration.ConfigureElements();
                }
                catch (Exception ex)
                {
                }
            }
        }


        // Add debug logging only; no functional changes.
        public static void AddElementDecorModifier(SimHashes element, float decorBonusMultiplier)
        {

            var elementMaterial = ElementLoader.FindElementByHash(element);
            if (elementMaterial == null)
            {
                return;
            }

            var attributeModifiers = Db.Get().BuildingAttributes;
         

            AttributeModifier decorModifier = new AttributeModifier(
                attributeModifiers.Decor.Id,
                decorBonusMultiplier,
                elementMaterial.name,
                true,   // is_multiplier
                false,  // readOnly
                true    // uiOnly
            );

         

            try
            {
                elementMaterial.attributeModifiers.Add(decorModifier);
            
            }
            catch (System.Exception ex)
            {
                throw;
            }
        }

        internal static void ConfigureElements()
        {
            AddElementDecorModifier(ModElementRegistration.CrudByproduct, -0.8f);
        }
    }
}   