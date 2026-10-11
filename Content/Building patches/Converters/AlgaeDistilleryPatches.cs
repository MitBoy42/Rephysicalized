using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace Rephysicalized
{

    // - Allow storage of both Kelp and Slime
    // - Add a simple side-screen selector to switch delivery tag only
    [HarmonyPatch(typeof(AlgaeDistilleryConfig), nameof(AlgaeDistilleryConfig.ConfigureBuildingTemplate))]
    public static class AlgaeDistilleryConfig_ConfigureBuildingTemplate_Patch
    {

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            // Reconfigure converter to consume Distillable (single input), preserving existing rate and outputs
            var converter = go.GetComponent<ElementConverter>();
            if (converter != null)
            {
                var outputs = converter.outputElements;
                float rate = 0.6f; // default fallback
                if (converter.consumedElements != null && converter.consumedElements.Length > 0)
                {
                    rate = converter.consumedElements[0].MassConsumptionRate;
                }

                if (ModTags.Distillable.IsValid)
                {
                    converter.consumedElements = new ElementConverter.ConsumedElement[]
                    {
                        new ElementConverter.ConsumedElement(ModTags.Distillable, rate)
                    };
                }

                // Preserve original outputs unchanged
                converter.outputElements = outputs;
            }

            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();

                if (ModTags.Distillable.IsValid && !storage.storageFilters.Contains(ModTags.Distillable))
                    storage.storageFilters.Add(ModTags.Distillable);
            }


            go.AddOrGet<DistilleryDeliverySelector>();

            // Initialize default delivery to Slime
            var fetcher = go.GetComponent<ManualDeliveryKG>();
            if (fetcher != null)
            {
                fetcher.RequestedItemTag = SimHashes.SlimeMold.CreateTag();
            }
        }
    }


    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class DistilleryDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
    
        public static readonly Tag SlimeTag = SimHashes.SlimeMold.CreateTag();

        // NOTE: do not create Kelp/Nori tags eagerly via `new Tag("...")`.
        // Some DLC content registers tags/sprites later; early creation can produce invalid sprites/options.
        private static Tag GetKelpTag() => new Tag("Kelp");
        private static Tag GetNoriTag() => new Tag("Nori");



        [KSerialization.Serialize] public Tag selectedDeliveryTag = default;

        [MyCmpGet] private ManualDeliveryKG fetcher;
        [MyCmpGet] private Storage storage;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (!selectedDeliveryTag.IsValid)
                selectedDeliveryTag = SlimeTag; // default
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplySelection();
        }

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            // IMPORTANT: FewOptionSideScreen.SpawnRows assumes every returned Option is fully populated.
            // If an entry is default/uninitialized, it will crash when it tries to access label/icon tuple.
            var list = new List<FewOptionSideScreen.IFewOptionSideScreen.Option>(3);

            // Slime is always available
            {
                var label = SlimeTag.ProperName() ?? SlimeTag.ToString();
                var spriteTuple = GetIconTuple(SlimeTag);
                if (spriteTuple != null)
                {
                    list.Add(new FewOptionSideScreen.IFewOptionSideScreen.Option(
                        SlimeTag,
                        label,
                        spriteTuple,
                        STRINGS.BUILDINGS.ALGAEDISTILLERY.SLIME
                    ));
                }
                else
                {
                    // Last-resort fallback to avoid returning an invalid icon tuple
                    list.Add(new FewOptionSideScreen.IFewOptionSideScreen.Option(
                        SlimeTag,
                        label,
                        Def.GetUISprite((object)SlimeTag),
                        STRINGS.BUILDINGS.ALGAEDISTILLERY.SLIME
                    ));
                }
            }

            // DLC-gated options
            if (DlcManager.IsContentSubscribed("DLC4_ID"))
            {
                var kelpTag = GetKelpTag();
                if (kelpTag.IsValid)
                    TryAddOption(list, kelpTag, STRINGS.BUILDINGS.ALGAEDISTILLERY.KELP);
            }

            if (DlcManager.IsContentSubscribed("DLC5_ID"))
            {
                var noriTag = GetNoriTag();
                if (noriTag.IsValid)
                    TryAddOption(list, noriTag, STRINGS.BUILDINGS.ALGAEDISTILLERY.NORI);
            }

            return list.ToArray();
        }

        private void TryAddOption(
            List<FewOptionSideScreen.IFewOptionSideScreen.Option> list,
            Tag tag,
            string tooltipText)
        {
            var label = tag.ProperName() ?? tag.ToString();
            var spriteTuple = GetIconTuple(tag);
            if (spriteTuple == null)
                return;

            list.Add(new FewOptionSideScreen.IFewOptionSideScreen.Option(tag, label, spriteTuple, tooltipText));
        }

        private Tuple<Sprite, Color> GetIconTuple(Tag tag)
        {
            try
            {
                var obj = (object)tag;
                var tuple = Def.GetUISprite(obj);
                // Def.GetUISprite can return null in missing-DLC scenarios
                if (tuple == null)
                    return null;
                return tuple;
            }
            catch
            {
                return null;
            }
        }

        

        public void OnOptionSelected(FewOptionSideScreen.IFewOptionSideScreen.Option option)
        {
            if (!option.tag.IsValid || option.tag == selectedDeliveryTag)
                return;

            selectedDeliveryTag = option.tag;
            ApplySelection();
        }

        public Tag GetSelectedOption() => selectedDeliveryTag;

        private void ApplySelection()
        {
            // Ensure storage permits both (and the shared Distillable tag) for manual drops or other mods
            if (storage == null) storage = GetComponent<Storage>();
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
       
                if (ModTags.Distillable.IsValid && !storage.storageFilters.Contains(ModTags.Distillable))
                    storage.storageFilters.Add(ModTags.Distillable);
            }

            // Switch delivery tag only; conversion uses ModTags.Distillable
            if (fetcher == null) fetcher = GetComponent<ManualDeliveryKG>();
                fetcher.RequestedItemTag = selectedDeliveryTag;
        }
    }
}