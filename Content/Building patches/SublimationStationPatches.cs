
using System.Collections.Generic; using HarmonyLib; using KSerialization; using UnityEngine;

namespace Rephysicalized
{ // Configure Sublimation Station with: // - two independent real converters (toxic sand / bleachstone) //
  // - a proxy converter that reports readiness to the Electrolyzer SM (first ElementConverter on GO) //
  // - a delivery selector to switch fetch between the two items and adjust capacities
    [HarmonyPatch(typeof(SublimationStationConfig), nameof(SublimationStationConfig.ConfigureBuildingTemplate))]
    public static class SublimationStationConfig_ConfigureBuildingTemplate_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            // Ensure storage
            var storage = go.AddOrGet<Storage>();
            storage.capacityKg = 600f;
            storage.showInUI = true;

            // Remove any existing ElementConverters so we fully control definitions
            var existingConverters = go.GetComponents<ElementConverter>();
            if (existingConverters != null)
            {
                for (int i = 0; i < existingConverters.Length; i++)
                    Object.DestroyImmediate(existingConverters[i]);
            }

            // Converter 1: Toxic Sand -> Polluted Oxygen (vanilla behavior)
            var sandConverter = go.AddComponent<ElementConverter>();
            sandConverter.consumedElements = new[]
            {
            new ElementConverter.ConsumedElement(SimHashes.ToxicSand.CreateTag(), 1f)
        };
            sandConverter.outputElements = new[]
            {
            new ElementConverter.OutputElement(
                0.66f, SimHashes.ContaminatedOxygen, 303.15f)
        };

            // Converter 2: Bleachstone -> Chlorine Gas
            var bleachConverter = go.AddComponent<ElementConverter>();
            bleachConverter.consumedElements = new[]
            {
            new ElementConverter.ConsumedElement(SimHashes.BleachStone.CreateTag(), 0.2f)
        };
            bleachConverter.outputElements = new[]
            {
            new ElementConverter.OutputElement(
                0.2f, SimHashes.ChlorineGas, 303.15f)
        };

            // Controller: makes the building run if either recipe can start; toggles converters safely
            var controller = go.AddOrGet<SublimationStationConvertersController>();
            controller.sandConverter = sandConverter;
            controller.bleachConverter = bleachConverter;

            // Delivery selector and defaults
            go.AddOrGet<SublimationStationDeliverySelector>();

            var fetcher = go.AddOrGet<ManualDeliveryKG>();
            fetcher.SetStorage(storage);
            fetcher.RequestedItemTag = SimHashes.ToxicSand.CreateTag(); // default selection
            fetcher.capacity = 600f;
            fetcher.refillMass = 240f;
            fetcher.choreTypeIDHash = Db.Get().ChoreTypes.FetchCritical.IdHash;

            // Allow both items in storage
            if (storage.storageFilters == null)
                storage.storageFilters = new List<Tag>();
            var toxicSand = SimHashes.ToxicSand.CreateTag();
            var bleachStone = SimHashes.BleachStone.CreateTag();
            if (!storage.storageFilters.Contains(toxicSand))
                storage.storageFilters.Add(toxicSand);
            if (!storage.storageFilters.Contains(bleachStone))
                storage.storageFilters.Add(bleachStone);
        }
    }

    // Safely drives both converters without mutating recipes:
    // - Sets Operational active if either input can start
    // - Enables converter(s) that can run; disables the other to avoid ConvertMass on empty inputs
    public sealed class SublimationStationConvertersController : KMonoBehaviour, ISim200ms
    {
        [SerializeField] public ElementConverter sandConverter;
        [SerializeField] public ElementConverter bleachConverter;

        [MyCmpGet] private Operational operational;

        // Small debounce to avoid rapid flicker between waiting/convert when thresholds are on the edge
        private float stopGraceTimer;
        private const float StopGraceSeconds = 0.2f;

        public void Sim200ms(float dt)
        {
            if (sandConverter == null || bleachConverter == null)
                return;

            // Readiness from converters themselves
            bool sandHasEnough = sandConverter.HasEnoughMassToStartConverting();
            bool bleachHasEnough = bleachConverter.HasEnoughMassToStartConverting();

            bool sandCanConvert = sandConverter.CanConvertAtAll();
            bool bleachCanConvert = bleachConverter.CanConvertAtAll();

            bool anyHasEnough = sandHasEnough || bleachHasEnough;
            bool anyCanConvert = sandCanConvert || bleachCanConvert;

            // Debounced operational: start immediately if either has enough; stop only if neither can convert for grace window
            if (anyHasEnough)
            {
                stopGraceTimer = 0f;
                if (operational != null && !operational.IsActive)
                    operational.SetActive(true);
            }
            else
            {
                // no one has enough to start; if nobody can convert, start grace countdown
                if (!anyCanConvert)
                {
                    stopGraceTimer += 0.2f; // Sim200ms cadence
                    if (stopGraceTimer >= StopGraceSeconds && operational != null && operational.IsActive)
                        operational.SetActive(false);
                }
                else
                {
                    stopGraceTimer = 0f;
                }
            }

            // Enable whichever converter can operate; leave enabled if it still "can convert"
            sandConverter.enabled = sandHasEnough || sandCanConvert;
            bleachConverter.enabled = bleachHasEnough || bleachCanConvert;
        }
    }

    // Side-screen selector to choose delivered solid and adjust capacities for Bleachstone
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class SublimationStationDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
        public static readonly Tag ToxicSandTag = SimHashes.ToxicSand.CreateTag();
        public static readonly Tag BleachStoneTag = SimHashes.BleachStone.CreateTag();

        [KSerialization.Serialize] public Tag selectedDeliveryTag = default;

        [MyCmpGet] private ManualDeliveryKG fetcher;
        [MyCmpGet] private Storage storage;

        private const float ToxicSandCapacity = 600f;
        private const float ToxicSandRefill = 240f;

        private const float BleachCapacity = 200f;
        private const float BleachRefill = 100f;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (!selectedDeliveryTag.IsValid)
                selectedDeliveryTag = ToxicSandTag; // default is Toxic Sand
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplySelection();
        }

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var options = new FewOptionSideScreen.IFewOptionSideScreen.Option[2];

            {
                var label = ToxicSandTag.ProperName() ?? ToxicSandTag.ToString();
                var sprite = Def.GetUISprite((object)ToxicSandTag);
                options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    ToxicSandTag, label, sprite, "Deliver Toxic Sand");
            }
            {
                var label = BleachStoneTag.ProperName() ?? BleachStoneTag.ToString();
                var sprite = Def.GetUISprite((object)BleachStoneTag);
                options[1] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    BleachStoneTag, label, sprite, "Deliver Bleachstone");
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
            if (storage == null) storage = GetComponent<Storage>();
            if (fetcher == null) fetcher = GetComponent<ManualDeliveryKG>();

            // Ensure storage permits both items
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                if (!storage.storageFilters.Contains(ToxicSandTag))
                    storage.storageFilters.Add(ToxicSandTag);
                if (!storage.storageFilters.Contains(BleachStoneTag))
                    storage.storageFilters.Add(BleachStoneTag);
            }

            if (fetcher != null)
            {
                fetcher.RequestedItemTag = selectedDeliveryTag;

                if (selectedDeliveryTag == BleachStoneTag)
                {
                    fetcher.capacity = BleachCapacity;
                    fetcher.refillMass = BleachRefill;
                }
                else
                {
                    fetcher.capacity = ToxicSandCapacity;
                    fetcher.refillMass = ToxicSandRefill;
                }
            }
        }
    }
}