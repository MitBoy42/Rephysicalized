using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using KSerialization;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{
internal class RadiationLightPatches
    {
        [HarmonyPatch(typeof(RadiationLightConfig), nameof(RadiationLightConfig.ConfigureBuildingTemplate))]
        public static class RadiationLightConfig_ConfigureBuildingTemplate_Patch
        {
            public static void Postfix(GameObject go, Tag prefab_tag)
            {
              
              
              // Get vanilla components
                var storage = go.GetComponent<Storage>();
                var manualDelivery = go.GetComponent<ManualDeliveryKG>();
                var oreConverter = go.GetComponent<ElementConverter>(); // Vanilla ore converter
                var elementDropper = go.AddOrGet<ElementDropper>();

                if (storage == null || manualDelivery == null || oreConverter == null)
                    return;

                // Ensure storage accepts both fuels
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                storage.storageFilters.Add(SimHashes.UraniumOre.CreateTag());
                storage.storageFilters.Add(SimHashes.EnrichedUranium.CreateTag());

                oreConverter.consumedElements = new[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.UraniumOre.CreateTag(), 0.01666667f)
                };
                oreConverter.outputElements = new[]
                {
                    new ElementConverter.OutputElement(0.01666667f, SimHashes.DepletedUranium, 0.0f, storeOutput: true, diseaseWeight: 0.5f)
                };


                // Add enriched uranium converter (matches ore consumption rate, 1:1 to depleted)
                var enrichedConverter = go.AddComponent<ElementConverter>();
                enrichedConverter.consumedElements = new[]
                {
                    new ElementConverter.ConsumedElement(SimHashes.EnrichedUranium.CreateTag(), 0.008333333f)
                };
                enrichedConverter.outputElements = new[]
                {
                    new ElementConverter.OutputElement(0.008333334f, SimHashes.DepletedUranium, 0.0f, storeOutput: true, diseaseWeight: 0.5f)
                };

                // Add controller for toggling converters, operational, and radiation strength
                var controller = go.AddOrGet<RadiationLightConvertersController>();
                controller.oreConverter = oreConverter;
                controller.enrichedConverter = enrichedConverter;

                // Sync vanilla radiation emitter
                var radiationEmitter = go.AddOrGet<RadiationEmitter>();
                radiationEmitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
                radiationEmitter.emitRads = 240f;
                radiationEmitter.Refresh();

                // Add delivery selector for switching between ore/enriched
                go.AddOrGet<RadiationLightDeliverySelector>();
            }
        }

        [HarmonyPatch(typeof(RadiationLight), nameof(RadiationLight.HasEnoughFuel))]
        public static class RadiationLight_HasEnoughFuel_Patch
        {
            public static bool Prefix(RadiationLight __instance, ref bool __result)
            {
                var converters = __instance.GetComponents<ElementConverter>();
                foreach (var converter in converters)
                {
                    if (converter.HasEnoughMassToStartConverting())
                    {
                        __result = true;
                        return false;
                    }
                }
                __result = false;
                return false;
            }
        }

        public sealed class RadiationLightConvertersController : KMonoBehaviour, ISim200ms
        {
            [SerializeField] public ElementConverter oreConverter;
            [SerializeField] public ElementConverter enrichedConverter;

            [MyCmpGet] private Operational operational;
            [MyCmpGet] private RadiationEmitter radiationEmitter;

            private float stopGraceTimer;
            private const float StopGraceSeconds = 0.2f;

            public void Sim200ms(float dt)
            {
                if (oreConverter == null || enrichedConverter == null || radiationEmitter == null)
                    return;

                // Check readiness
                bool oreHasEnough = oreConverter.HasEnoughMassToStartConverting();
                bool enrichedHasEnough = enrichedConverter.HasEnoughMassToStartConverting();

                bool oreCanConvert = oreConverter.CanConvertAtAll();
                bool enrichedCanConvert = enrichedConverter.CanConvertAtAll();

                bool anyHasEnough = oreHasEnough || enrichedHasEnough;
                bool anyCanConvert = oreCanConvert || enrichedCanConvert;

                // Operational flag
                if (anyHasEnough)
                {
                    stopGraceTimer = 0f;
                    operational?.SetActive(true);
                }
                else if (!anyCanConvert)
                {
                    stopGraceTimer += 0.2f;
                    if (stopGraceTimer >= StopGraceSeconds)
                        operational?.SetActive(false);
                }
                else
                {
                    stopGraceTimer = 0f;
                }

                // Toggle converters
                oreConverter.enabled = oreHasEnough || oreCanConvert;
                enrichedConverter.enabled = enrichedHasEnough || enrichedCanConvert;

                // Double radiation if enriched is active/preferred
                bool usingEnriched = enrichedHasEnough || (enrichedCanConvert && !oreHasEnough);
                radiationEmitter.emitRads = usingEnriched ? 720f : 240f;
                radiationEmitter.Refresh();
            }
        }

        [SerializationConfig(MemberSerialization.OptIn)]
        public sealed class RadiationLightDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
        {
            public static readonly Tag UraniumOreTag = SimHashes.UraniumOre.CreateTag();
            public static readonly Tag EnrichedUraniumTag = SimHashes.EnrichedUranium.CreateTag();

            [KSerialization.Serialize] public Tag selectedDeliveryTag = default;

            [MyCmpGet] private ManualDeliveryKG fetcher;
            [MyCmpGet] private Storage storage;

            private const float OreCapacity = 50f;
            private const float OreRefill = 5f;
            private const float EnrichedCapacity = 50f;
            private const float EnrichedRefill = 5f;

            public override void OnPrefabInit()
            {
                base.OnPrefabInit();
                if (!selectedDeliveryTag.IsValid)
                    selectedDeliveryTag = UraniumOreTag; // Default to ore
            }

            public override void OnSpawn()
            {
                base.OnSpawn();
                ApplySelection();
            }

            public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
            {
                var options = new FewOptionSideScreen.IFewOptionSideScreen.Option[2];
                options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    UraniumOreTag, UraniumOreTag.ProperName(), Def.GetUISprite(UraniumOreTag), "Deliver Uranium Ore");
                options[1] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    EnrichedUraniumTag, EnrichedUraniumTag.ProperName(), Def.GetUISprite(EnrichedUraniumTag), "Deliver Enriched Uranium");
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
                if (fetcher == null) fetcher = GetComponent<ManualDeliveryKG>();
                if (storage == null) storage = GetComponent<Storage>();

                fetcher.RequestedItemTag = selectedDeliveryTag;

                if (selectedDeliveryTag == EnrichedUraniumTag)
                {
                    fetcher.capacity = EnrichedCapacity;
                    fetcher.refillMass = EnrichedRefill;
                }
                else
                {
                    fetcher.capacity = OreCapacity;
                    fetcher.refillMass = OreRefill;
                }

                // Ensure storage permits both
                if (storage?.storageFilters != null)
                {
                    if (!storage.storageFilters.Contains(UraniumOreTag))
                        storage.storageFilters.Add(UraniumOreTag);
                    if (!storage.storageFilters.Contains(EnrichedUraniumTag))
                        storage.storageFilters.Add(EnrichedUraniumTag);
                }
            }
        }
    }
}
