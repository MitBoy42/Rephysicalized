
using HarmonyLib; 
using KSerialization; 
using System.Collections.Generic; 
using UnityEngine;

using System;

using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized
{

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
            storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            // Remove any existing ElementConverters so we fully control definitions
            var existingConverters = go.GetComponents<ElementConverter>();
            if (existingConverters != null)
            {
                for (int i = 0; i < existingConverters.Length; i++)
                    GameObject.DestroyImmediate(existingConverters[i]);
            }


            // Converter 1: base input -> Polluted Oxygen
            var sandConverter = go.AddComponent<ElementConverter>();
       
            sandConverter.consumedElements = new[]
            {
            new ElementConverter.ConsumedElement(SimHashes.ToxicSand.CreateTag(), 1f)
        };
            if (Config.Instance.RealBreathing)
            {
                // Real breathing: output is polluted oxygen and sand
                var sandOutputs = new List<ElementConverter.OutputElement>
                {
                    new ElementConverter.OutputElement(0.66f, SimHashes.ContaminatedOxygen, 303.15f),
                    new ElementConverter.OutputElement(0.34f, SimHashes.Sand, 0.0f, storeOutput: true, diseaseWeight: 0.25f)
                };
                sandConverter.outputElements = sandOutputs.ToArray();
            }
            else
            {
                // Classic: output is oxygen and chlorine gas
                var classicOutputs = new List<ElementConverter.OutputElement>
                {
                    new ElementConverter.OutputElement(0.066f, SimHashes.ContaminatedOxygen, 303.15f),
                    new ElementConverter.OutputElement(0.934f, SimHashes.Sand, 0.0f, storeOutput: true, diseaseWeight: 0.25f)
                };
                sandConverter.outputElements = classicOutputs.ToArray();
            }
        
            var sandDropper = go.AddOrGet<ElementDropper>();
                sandDropper.emitTag = SimHashes.Sand.CreateTag();
                sandDropper.emitMass = 40f;
                sandDropper.emitOffset = Vector3.zero;
       

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

            // Delivery selector and defaults
            go.AddOrGet<SublimationStationDeliverySelector>();

            var fetcher = go.AddOrGet<ManualDeliveryKG>();
            fetcher.SetStorage(storage);
            fetcher.RequestedItemTag = SimHashes.ToxicSand.CreateTag();

            fetcher.capacity = 600f;
            fetcher.refillMass = 240f;
            fetcher.choreTypeIDHash = Db.Get().ChoreTypes.FetchCritical.IdHash;



            // Allow both inputs in storage
            if (storage.storageFilters == null)
                storage.storageFilters = new List<Tag>();
                storage.storageFilters.Add(SimHashes.ToxicSand.CreateTag());
                storage.storageFilters.Add(SimHashes.BleachStone.CreateTag());
        }
    }

    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.HasEnoughMassToStartConverting))]
    public static class SublimationStationElementConverter_HasEnoughMassToStartConverting_Patch
    {
        public static void Postfix(ElementConverter __instance, ref bool __result)
        {
            if (__result || !IsPrimarySublimationConverter(__instance))
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

        private static bool IsPrimarySublimationConverter(ElementConverter converter)
        {
            return converter.GetComponent<Electrolyzer>() != null &&
                converter.GetComponent<ElementConverter>() == converter &&
                converter.GetComponents<ElementConverter>().Length > 1;
        }
    }

    [HarmonyPatch(typeof(ElementConverter), nameof(ElementConverter.CanConvertAtAll))]
    public static class SublimationStationElementConverter_CanConvertAtAll_Patch
    {
        public static void Postfix(ElementConverter __instance, ref bool __result)
        {
            if (__result || !IsPrimarySublimationConverter(__instance))
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

        private static bool IsPrimarySublimationConverter(ElementConverter converter)
        {
            return converter.GetComponent<Electrolyzer>() != null &&
                converter.GetComponent<ElementConverter>() == converter &&
                converter.GetComponents<ElementConverter>().Length > 1;
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
            Tag baseInputTag = ToxicSandTag;
            if (!selectedDeliveryTag.IsValid ||
                (selectedDeliveryTag != baseInputTag && selectedDeliveryTag != BleachStoneTag))
                selectedDeliveryTag = baseInputTag;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ApplySelection();
        }

        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var options = new FewOptionSideScreen.IFewOptionSideScreen.Option[2];
            Tag baseInputTag = ToxicSandTag;

         
                var label = baseInputTag.ProperName() ?? baseInputTag.ToString();
                var sprite = Def.GetUISprite((object)baseInputTag);
                options[0] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    baseInputTag, label, sprite, "Deliver Polluted Dirt");
      
        
         
                var labelbleach = BleachStoneTag.ProperName() ?? BleachStoneTag.ToString();
                var spritebleach = Def.GetUISprite((object)BleachStoneTag);
                options[1] = new FewOptionSideScreen.IFewOptionSideScreen.Option(
                    BleachStoneTag, labelbleach, spritebleach, "Deliver Bleach Stone");
            

            return options;
        }

        public void OnOptionSelected(FewOptionSideScreen.IFewOptionSideScreen.Option option)
        {
            Tag baseInputTag = ToxicSandTag;
            if ((option.tag != baseInputTag && option.tag != BleachStoneTag) || option.tag == selectedDeliveryTag)
                return;

            DropStoredItems(option.tag == BleachStoneTag ? baseInputTag : BleachStoneTag);
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

        private void ApplySelection()
        {
            if (storage == null) storage = GetComponent<Storage>();
            if (fetcher == null) fetcher = GetComponent<ManualDeliveryKG>();

            // Ensure storage permits both items
            if (storage != null)
            {
                if (storage.storageFilters == null)
                    storage.storageFilters = new List<Tag>();
                Tag baseInputTag =  ToxicSandTag;
                if (!storage.storageFilters.Contains(baseInputTag))
                    storage.storageFilters.Add(baseInputTag);
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
