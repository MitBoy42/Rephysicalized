
using HarmonyLib; 
using KSerialization; 
using System.Collections.Generic; 
using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace Rephysicalized
{

    [HarmonyPatch(typeof(SublimationStationConfig), "ConfigureBuildingTemplate")]
    //Sublimation
    public static class SublimationStationConfig_SandOutputPatch
    {
        private const float Sand_PER_LOAD = 30f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (OrganicOverhaulIntegration.IsPresent())
                return;
            // Ensure ElementConverter exists, or add if missing
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null)
            {
                elementConverter = go.AddComponent<ElementConverter>();
                elementConverter.consumedElements = new ElementConverter.ConsumedElement[0];
            }
            var outputs = elementConverter.outputElements?.ToList() ?? new System.Collections.Generic.List<ElementConverter.OutputElement>();
            // Remove any existing Sand output
            outputs.RemoveAll(o => o.elementHash == SimHashes.Sand);
            // Always add/overwrite with a Sand output (AirFilter values)
            outputs.Add(new ElementConverter.OutputElement(
                0.34f,      // massGenerationRate (AirFilter)
                SimHashes.Sand,  // output element
                0.0f,            // temperatureOperation
                storeOutput: true,
                diseaseWeight: 0.25f
            ));
            elementConverter.outputElements = outputs.ToArray();

            // Ensure Storage uses StandardSealedStorage modifiers (like AirFilter)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            }
            // Ensure ElementDropper exists and is set up to drop Sand in 30kg loads
            var elementDropper = go.AddComponent<ElementDropper>();

            elementDropper.emitTag = new Tag("Sand");
            elementDropper.emitMass = 40f;
            elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
        }
    }

    // Configure Sublimation Station with: // - two independent real converters (toxic sand / bleachstone) //
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
                    GameObject.DestroyImmediate(existingConverters[i]);
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
            if (OrganicOverhaulIntegration.IsPresent()) { fetcher.RequestedItemTag = SimHashes.ToxicMud.CreateTag(); }
            if (!OrganicOverhaulIntegration.IsPresent())
            { fetcher.RequestedItemTag = SimHashes.ToxicSand.CreateTag(); }

            fetcher.capacity = 600f;
            fetcher.refillMass = 240f;
            fetcher.choreTypeIDHash = Db.Get().ChoreTypes.FetchCritical.IdHash;



            // Allow both items in storage
            if (storage.storageFilters == null)
                storage.storageFilters = new List<Tag>();
          

                storage.storageFilters.Add(SimHashes.ToxicSand.CreateTag());
            storage.storageFilters.Add(SimHashes.BleachStone.CreateTag());

            if (OrganicOverhaulIntegration.IsPresent())
            { storage.storageFilters.Add(SimHashes.ToxicMud.CreateTag()); }
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

            // Respect automation: never override Operational.SetActive from here.
            // If automation disabled the station, keep converters off.
            if (operational != null && !operational.IsOperational)
            {
                sandConverter.enabled = false;
                bleachConverter.enabled = false;
                stopGraceTimer = 0f;
                return;
            }

            // Debounced internal gating ONLY for converter enablement.
            // (Operational state machine remains the authority.)
            if (anyHasEnough)
            {
                stopGraceTimer = 0f;
            }
            else if (!anyCanConvert)
            {
                stopGraceTimer += 0.2f; // Sim200ms cadence
            }
            else
            {
                stopGraceTimer = 0f;
            }

            // Enable whichever converter can operate; leave enabled if it still "can convert"
            bool enableSand = (sandHasEnough || sandCanConvert) && (stopGraceTimer < StopGraceSeconds);
            bool enableBleach = (bleachHasEnough || bleachCanConvert) && (stopGraceTimer < StopGraceSeconds);

            sandConverter.enabled = enableSand;
            bleachConverter.enabled = enableBleach;
        }
    }

    // Side-screen selector to choose delivered solid and adjust capacities for Bleachstone
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class SublimationStationDeliverySelector : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
        public static readonly Tag ToxicSandTag = SimHashes.ToxicSand.CreateTag();
        public static readonly Tag BleachStoneTag = SimHashes.BleachStone.CreateTag();
        public static readonly Tag ToxicMudTag = SimHashes.ToxicMud.CreateTag();



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


            if (!OrganicOverhaulIntegration.IsPresent())
            {
                if (!selectedDeliveryTag.IsValid)
                    selectedDeliveryTag = ToxicSandTag; // default is Toxic Sand
            }
            else
            {
                if (!selectedDeliveryTag.IsValid)
                    selectedDeliveryTag = ToxicMudTag; // default is Toxic Sand
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplySelection();
        }

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var options = new FewOptionSideScreen.IFewOptionSideScreen.Option[2];

            if (!OrganicOverhaulIntegration.IsPresent())
            {
                var label = ToxicSandTag.ProperName() ?? ToxicSandTag.ToString();
                var sprite = Def.GetUISprite((object)ToxicSandTag);
                options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    ToxicSandTag, label, sprite, "Deliver Polluted Dirt");
            }
        
            else
            {
                 var label = ToxicSandTag.ProperName() ?? ToxicSandTag.ToString();
        var sprite = Def.GetUISprite((object)ToxicMudTag);
        options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
            ToxicSandTag, label, sprite, "Deliver Polluted Mud");
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