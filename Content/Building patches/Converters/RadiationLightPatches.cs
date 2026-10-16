using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using KSerialization;


namespace Rephysicalized.Content.Building_patches
{
internal class RadiationLightPatches
    {
        private const float BaseRadiationRate = 240f;

        private static void UpdateRadiationRate(RadiationLight light, bool isEmitting)
        {
            var emitter = light.GetComponent<RadiationEmitter>();
            if (emitter == null)
                return;

            var converters = light.GetComponents<ElementConverter>();
            bool enrichedIsConverting = isEmitting && converters.Length > 1 && converters[1].CanConvertAtAll();
            emitter.emitRads = enrichedIsConverting ? BaseRadiationRate * 2f : BaseRadiationRate;
            emitter.Refresh();
        }

        [HarmonyPatch(typeof(RadiationLight.States), nameof(RadiationLight.States.InitializeStates))]
        public static class RadiationLightStates_InitializeStates_Patch
        {
            public static void Postfix(RadiationLight.States __instance)
            {
                __instance.ready.on.EventHandler(
                    GameHashes.OnStorageChange,
                    (RadiationLight.StatesInstance smi) => UpdateRadiationRate(smi.master, true));
            }
        }

        [HarmonyPatch(typeof(RadiationLight.StatesInstance), nameof(RadiationLight.StatesInstance.ToggleEmitter))]
        public static class RadiationLightStatesInstance_ToggleEmitter_Patch
        {
            public static void Postfix(RadiationLight.StatesInstance __instance, bool on)
            {
                UpdateRadiationRate(__instance.master, on);
            }
        }

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
                    UraniumOreTag, UraniumOreTag.ProperName(), Def.GetUISprite(UraniumOreTag), STRINGS.BUILDINGS.RADIATIONLIGHT.ORE);
                options[1] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    EnrichedUraniumTag, EnrichedUraniumTag.ProperName(), Def.GetUISprite(EnrichedUraniumTag), STRINGS.BUILDINGS.RADIATIONLIGHT.ENRICHED);
                return options;
            }

            public void OnOptionSelected(FewOptionSideScreen.IFewOptionSideScreen.Option option)
            {
                if ((option.tag != UraniumOreTag && option.tag != EnrichedUraniumTag) || option.tag == selectedDeliveryTag)
                    return;

                DropStoredFuel(option.tag == UraniumOreTag ? EnrichedUraniumTag : UraniumOreTag);
                selectedDeliveryTag = option.tag;
                ApplySelection();
            }

            public Tag GetSelectedOption() => selectedDeliveryTag;

            private void DropStoredFuel(Tag tag)
            {
                if (storage == null)
                    storage = GetComponent<Storage>();
                if (storage == null)
                    return;

                GameObject item;
                while ((item = storage.FindFirst(tag)) != null)
                    storage.Drop(item, true);
            }

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
