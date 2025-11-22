using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace Rephysicalized
{


    // Prefab patch for Algae Distillery:
 
    // - Allow storage of both Kelp and Slime
    // - Add a simple side-screen selector to switch delivery tag only
    [HarmonyPatch(typeof(AlgaeDistilleryConfig), nameof(AlgaeDistilleryConfig.ConfigureBuildingTemplate))]
    public static class AlgaeDistilleryConfig_ConfigureBuildingTemplate_Patch
    {
        private static bool Prepare() => Dlc4Gate.Enabled;
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
                    // Use whatever rate mods configured originally for Slime
                    rate = converter.consumedElements[0].MassConsumptionRate;
                }

                // IMPORTANT: Only set this at prefab time, never at runtime
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

            // Ensure storage can accept both Kelp and Slime items (and the shared Distillable tag if storage checks it)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();

                Tag slimeTag = SimHashes.SlimeMold.CreateTag();
                Tag kelpTag = new Tag("Kelp");

                if (ModTags.Distillable.IsValid && !storage.storageFilters.Contains(ModTags.Distillable))
                    storage.storageFilters.Add(ModTags.Distillable);

                if (!storage.storageFilters.Contains(slimeTag))
                    storage.storageFilters.Add(slimeTag);

                if (!storage.storageFilters.Contains(kelpTag))
                    storage.storageFilters.Add(kelpTag);
            }

            // Add the selector (side-screen) to let player choose delivery tag (Slime or Kelp)
            go.AddOrGet<DistilleryDeliverySelector>();

            // Initialize default delivery to Slime
            var fetcher = go.GetComponent<ManualDeliveryKG>();
            if (fetcher != null)
            {
                fetcher.RequestedItemTag = SimHashes.SlimeMold.CreateTag();
            }
        }
    }

    // Simple IceMachine-like option side-screen for choosing which solid dupes will deliver:
    // - Building consumes ModTags.Distillable internally, so either Kelp or Slime will be converted.
    // - We do not modify ElementConverter at runtime (avoids accumulator/state machine issues).
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class DistilleryDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
        private static bool Prepare() => Dlc4Gate.Enabled;
        public static readonly Tag SlimeTag = SimHashes.SlimeMold.CreateTag();
        public static readonly Tag KelpTag = new Tag("Kelp");


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

        // FewOption side screen implementation

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var options = new FewOptionSideScreen.IFewOptionSideScreen.Option[2];

            {
                var label = SlimeTag.ProperName() ?? SlimeTag.ToString();
                var sprite = Def.GetUISprite((object)SlimeTag);
                options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    SlimeTag, label, sprite, STRINGS.BUILDINGS.ALGAEDISTILLERY.SLIME);
            }
            {
                var label = KelpTag.ProperName() ?? KelpTag.ToString();
                var sprite = Def.GetUISprite((object)KelpTag);
                options[1] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    KelpTag, label, sprite, STRINGS.BUILDINGS.ALGAEDISTILLERY.KELP);
            }

            return options;
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
                if (!storage.storageFilters.Contains(SlimeTag)) storage.storageFilters.Add(SlimeTag);
                if (!storage.storageFilters.Contains(KelpTag)) storage.storageFilters.Add(KelpTag);
                if (ModTags.Distillable.IsValid && !storage.storageFilters.Contains(ModTags.Distillable))
                    storage.storageFilters.Add(ModTags.Distillable);
            }

            // Switch delivery tag only; conversion uses ModTags.Distillable
            if (fetcher == null) fetcher = GetComponent<ManualDeliveryKG>();
            if (fetcher != null)
                fetcher.RequestedItemTag = selectedDeliveryTag;
        }
    }
}