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

        public static ElementInfo Silicon = ElementInfo.Solid("SiliconRP", UIUtils.rgb(100, 100, 160));
        public static ElementInfo MoltenSilicon = ElementInfo.Liquid("MoltenSiliconRP", UIUtils.rgb(110, 110, 170));
        public static ElementInfo SiliconGas = ElementInfo.Gas("SiliconGasRP", UIUtils.rgb(120, 120, 180));

        public static ElementInfo PlantFiberRP = ElementInfo.Solid("PlantFiberRP", "plant_matter_kanim", UIUtils.rgb(120, 180, 120));

        public static void RegisterSubstances(List<Substance> list)
        {
            var newElements = new HashSet<Substance>()
            {
                AshByproduct.CreateSubstance(),
                CrudByproduct.CreateSubstance(),
                Silicon.CreateSubstance(),
                MoltenSilicon.CreateSubstance(),
                SiliconGas.CreateSubstance(),
                PlantFiberRP.CreateSubstance(),
            };
            list.AddRange(newElements);

        }
        public static ElementsAudio.ElementAudioConfig[] CreateAudioConfigs(ElementsAudio instance)
        {
            return new[]
            {
                SgtElementUtil.CopyElementAudioConfig(SimHashes.Sand, AshByproduct),
                 
                        SgtElementUtil.CopyElementAudioConfig(SimHashes.IgneousRock, CrudByproduct),
                             SgtElementUtil.CopyElementAudioConfig(SimHashes.Glass, Silicon),
                             SgtElementUtil.CopyElementAudioConfig(SimHashes.Algae, PlantFiberRP),
            };

        }

        [HarmonyPatch(typeof(LegacyModMain), "ConfigElements")]
        public static class LegacyModMain_ConfigElements_Patch
        {
            public static void Postfix()
            {
             
                    ModElementRegistration.ConfigureElements();
           
                }
            }


        public static void AddElementOverheatModifier(SimHashes element, float degreeIncrease)
        {
            var elementMaterial = ElementLoader.FindElementByHash(element);
            if (elementMaterial == null)
                return;
            var attributeModifiers = Db.Get().BuildingAttributes;
            AttributeModifier overheatModifier = new AttributeModifier(attributeModifiers.OverheatTemperature.Id, degreeIncrease, elementMaterial.name);
            elementMaterial.attributeModifiers.Add(overheatModifier);
        }

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
           
                elementMaterial.attributeModifiers.Add(decorModifier);
      
        } 

        internal static void ConfigureElements()
        {
            AddElementDecorModifier(ModElementRegistration.CrudByproduct, -0.8f);
            AddElementOverheatModifier(ModElementRegistration.Silicon, 50f);
            AddElementDecorModifier(ModElementRegistration.Silicon, 0.2f);
            AddElementDecorModifier(SimHashes.Lime, 0.4f);

            ElementLoader.FindElementByHash(ModElementRegistration.Silicon).name = STRINGS.ELEMENTS.SILICONRP.NAME;
            var silicon = ElementLoader.FindElementByHash(ModElementRegistration.Silicon);
            silicon.nameUpperCase = silicon.name.ToUpperInvariant();
            silicon.tag = TagManager.Create("SiliconRP", silicon.name);
        }
    }
}   