using System;
using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;
using System.Linq;
using Rephysicalized.ModElements;


namespace Rephysicalized
{
    [HarmonyPatch(typeof(OxyliteRefineryConfig), nameof(OxyliteRefineryConfig.ConfigureBuildingTemplate))]
    public static class OxyliteRefineryConfig_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null)
                return;

            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();

                AddStorageFilter(storage, SimHashes.Gold.CreateTag());
                AddStorageFilter(storage, SimHashes.Iridium.CreateTag());
            }

            go.AddOrGet<OxyliteRefineryDeliverySelector>();

            if (go.GetComponents<ElementConverter>().Length > 1)
                return;

            var iridiumConverter = go.AddComponent<ElementConverter>();
            iridiumConverter.consumedElements = new[]
            {
                new ElementConverter.ConsumedElement(SimHashes.Oxygen.CreateTag(), 1f),
                new ElementConverter.ConsumedElement(SimHashes.Iridium.CreateTag(), 0.01f)
            };
            iridiumConverter.outputElements = new[]
            {
                new ElementConverter.OutputElement(1.0f, SimHashes.OxyRock, 303.15f, storeOutput: true),
                 new ElementConverter.OutputElement(0.01f, ModElementRegistration.CrudByproduct, 303.15f, storeOutput: true)
            };
        }

        private static void AddStorageFilter(Storage storage, Tag tag)
        {
            if (!storage.storageFilters.Contains(tag))
                storage.storageFilters.Add(tag);
        }
    }

    // OxyliteRefineryConfig: Patch to add additional output of GoldAmalgam
    [HarmonyPatch(typeof(OxyliteRefineryConfig), "ConfigureBuildingTemplate")]
    public static class OxyliteRefineryConfig_GoldAmalgamOutputPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure ElementConverter exists, or add if missing
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null)
            {
                elementConverter = go.AddComponent<ElementConverter>();
                elementConverter.consumedElements = new ElementConverter.ConsumedElement[0];
            }

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();

            // Add GoldAmalgam output
            outputs.Add(new ElementConverter.OutputElement(
                0.003f,
               SimHashes.GoldAmalgam,
                1.0f,
                storeOutput: true,
                outputElementOffsety: 1f
            ));

            ManualDeliveryKG manualDeliveryKg = go.GetComponent<ManualDeliveryKG>();

            manualDeliveryKg.capacity = 20f;

            elementConverter.outputElements = outputs.ToArray();

            // Ensure ElementDropper exists and is set up to drop GoldAmalgam
            var elementDropper = go.AddComponent<ElementDropper>();
          
            
            elementDropper.emitTag = new Tag("GoldAmalgam");
            elementDropper.emitMass = 5f;
            elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
            var elementDropper2 = go.AddComponent<ElementDropper>();


            elementDropper2.emitTag = new Tag("CrudByproduct");
            elementDropper2.emitMass = 5f;
            elementDropper2.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
        }
    }

    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.HasEnoughMassToStartConverting))]
    public static class OxyliteRefineryElementConverter_HasEnoughMassToStartConverting_Patch
    {
        public static void Postfix(ElementConverter __instance, ref bool __result)
        {
            if (__result || !IsPrimaryRefineryConverter(__instance))
                return;

            var converters = __instance.GetComponents<ElementConverter>();
            for (int i = 1; i < converters.Length; i++)
            {
                if (converters[i].HasEnoughMassToStartConverting())
                {
                    __result = true;
                    return;
                }
            }
        }

        private static bool IsPrimaryRefineryConverter(ElementConverter converter)
        {
            return converter.GetComponent<OxyliteRefinery>() != null &&
                converter.GetComponent<ElementConverter>() == converter;
        }
    }

    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.CanConvertAtAll))]
    public static class OxyliteRefineryElementConverter_CanConvertAtAll_Patch
    {
        public static void Postfix(ElementConverter __instance, ref bool __result)
        {
            if (__result || !IsPrimaryRefineryConverter(__instance))
                return;

            var converters = __instance.GetComponents<ElementConverter>();
            for (int i = 1; i < converters.Length; i++)
            {
                if (converters[i].CanConvertAtAll())
                {
                    __result = true;
                    return;
                }
            }
        }

        private static bool IsPrimaryRefineryConverter(ElementConverter converter)
        {
            return converter.GetComponent<OxyliteRefinery>() != null &&
                converter.GetComponent<ElementConverter>() == converter;
        }
    }


    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class OxyliteRefineryDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
        public static readonly Tag GoldTag = SimHashes.Gold.CreateTag();
        public static readonly Tag IridiumTag = SimHashes.Iridium.CreateTag();

        [KSerialization.Serialize] public Tag selectedDeliveryTag = default;

        [MyCmpGet] private ManualDeliveryKG fetcher;
        [MyCmpGet] private Storage storage;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (!selectedDeliveryTag.IsValid)
                selectedDeliveryTag = GoldTag;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplySelection();
        }

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var options = new List<FewOptionSideScreen.IFewOptionSideScreen.Option>(2);
            AddOption(options, GoldTag, "Deliver Gold");
            AddOption(options, IridiumTag, "Deliver Iridium");
            return options.ToArray();
        }

        public void OnOptionSelected(FewOptionSideScreen.IFewOptionSideScreen.Option option)
        {
            if ((option.tag != GoldTag && option.tag != IridiumTag) || option.tag == selectedDeliveryTag)
                return;

            DropStoredItems(option.tag == GoldTag ? IridiumTag : GoldTag);
            selectedDeliveryTag = option.tag;
            ApplySelection();
        }

        public Tag GetSelectedOption() => selectedDeliveryTag;

        private void DropStoredItems(Tag tag)
        {
            if (storage == null)
                storage = GetComponent<Storage>();
            if (storage == null)
                return;

            GameObject item;
            while ((item = storage.FindFirst(tag)) != null)
                storage.Drop(item, true);
        }

        private static void AddOption(
            List<FewOptionSideScreen.IFewOptionSideScreen.Option> options,
            Tag tag,
            string tooltip)
        {
            Tuple<Sprite, Color> sprite = null;
            try
            {
                sprite = Def.GetUISprite((object)tag);
            }
            catch
            {
            }

            if (sprite != null)
            {
                options.Add(new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    tag, tag.ProperName() ?? tag.ToString(), sprite, tooltip));
            }
        }

        private void ApplySelection()
        {
            if (fetcher == null)
                fetcher = GetComponent<ManualDeliveryKG>();
            if (storage == null)
                storage = GetComponent<Storage>();

            if (fetcher != null)
                fetcher.RequestedItemTag = selectedDeliveryTag;

            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                if (!storage.storageFilters.Contains(GoldTag))
                    storage.storageFilters.Add(GoldTag);
                if (!storage.storageFilters.Contains(IridiumTag))
                    storage.storageFilters.Add(IridiumTag);
            }
        }
    }
}
