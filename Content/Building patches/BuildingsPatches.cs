using HarmonyLib;
using Klei;
using KSerialization;
using Rephysicalized;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;


namespace Rephysicalized.Content.Building_patches
{
    // Patch ClusterTelescopeEnclosedConfig.CreateBuildingDef to require both metal (same mass) and 100kg of Glass
    [HarmonyPatch(typeof(ClusterTelescopeEnclosedConfig), nameof(ClusterTelescopeEnclosedConfig.CreateBuildingDef))]
    public static class ClusterTelescopeEnclosed_MaterialsPatch
    {
        private const float GlassMassKg = 100f;

        // We’ll wrap the original call by intercepting the BuildingDef after it’s created and overwrite its mass/materials.
        public static void Postfix(ref BuildingDef __result)
        {
            if (__result == null)
                return;

            // Use the existing arrays; in this branch they are named Mass and MaterialCategory.
            var oldMats = __result.MaterialCategory;
            var oldMass = __result.Mass;

            // Safety guards
            if (oldMats == null || oldMass == null || oldMats.Length != oldMass.Length || oldMats.Length == 0)
            {
                // Fallback: keep vanilla TIER4 metals mass + add Glass
                __result.MaterialCategory = new[] { MATERIALS.ALL_METALS[0], MATERIALS.GLASS };
                __result.Mass = new[] { BUILDINGS.CONSTRUCTION_MASS_KG.TIER4[0], GlassMassKg };
                return;
            }

            // Build new arrays with one extra slot for Glass
            var newMats = new string[oldMats.Length + 1];
            var newMass = new float[oldMass.Length + 1];

            for (int i = 0; i < oldMats.Length; i++)
            {
                newMats[i] = oldMats[i];
                newMass[i] = oldMass[i];
            }

            // Append Glass
            newMats[oldMats.Length] = MATERIALS.GLASS;
            newMass[oldMass.Length] = GlassMassKg;

            __result.MaterialCategory = newMats;
            __result.Mass = newMass;
        }
    }

    // CampfireConfig: Patch to require both wood and oxygen as inputs
    [HarmonyPatch(typeof(CampfireConfig), "ConfigureBuildingTemplate")]
    public static class CampfireConfig_BuildingTemplate_Patch
    {
        private const float GAS_CONSUMPTION_RATE = 0.05f;
        private const float GAS_CAPACITY = 0.05f;

        public static void Postfix(GameObject go)
        {
            var storage = go.AddOrGet<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas, new Tag("WoodLog") };
            }

            go.AddOrGet<OxidizerLowStatus>();
            DualGasElementConsumer dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.capacityKG = GAS_CAPACITY;
            dualGasConsumer.consumptionRate = GAS_CONSUMPTION_RATE;
            dualGasConsumer.sampleCellOffset = new Vector3(0, 0);
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.isRequired = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;


            ElementConverter elementConverter = go.AddOrGet<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[2]
            {
            new ElementConverter.ConsumedElement(new Tag("WoodLog"), 0.025f),
            new ElementConverter.ConsumedElement(ModTags.OxidizerGas, 0.003f)
            };
            elementConverter.outputElements = new ElementConverter.OutputElement[2]
            {
            new ElementConverter.OutputElement(0.004f, SimHashes.CarbonDioxide, 303.15f, outputElementOffsety: 1f),
            new ElementConverter.OutputElement(0.024f, ModElementRegistration.AshByproduct, 303.15f, storeOutput: true)
            };

            var elementDropper = go.AddComponent<ElementDropper>();

            elementDropper.emitTag = new Tag("AshByproduct");
            elementDropper.emitMass = 24f;
            elementDropper.emitOffset = new Vector3(0.0f, 0.0f, 0.0f);
        }
        [HarmonyPatch(typeof(Campfire), "HasFuel")]
        public static class Campfire_HasFuel_Patch
        {
            private static readonly Tag WoodTag = new Tag("WoodLog");
            private const float WoodPerTick = 0.025f;
            private const float BreathablePerTick = 0.003f;

            public static bool Prefix(Campfire.Instance smi, ref bool __result)
            {
                var converter = smi.GetComponent<ElementConverter>();
                var storage = smi.GetComponent<Storage>();
                if (converter == null || storage == null)
                {
                    __result = false;
                    return false;
                }

                // Wood check as before
                bool hasWood = storage.GetMassAvailable(WoodTag) >= WoodPerTick;

                // Sum all breathable gases in storage
                float breathableMass = 0f;
                foreach (GameObject go in storage.items)
                {
                    if (go == null) continue;
                    PrimaryElement pe = go.GetComponent<PrimaryElement>();
                    if (pe == null) continue;
                    if (pe.Element.HasTag(GameTags.Breathable))
                        breathableMass += pe.Mass;
                }
                bool hasBreathable = breathableMass >= BreathablePerTick;

                __result = hasWood && hasBreathable;
                return false; // Skip original
            }
        }

    }



    //SmokerConfig
    // SmokerConfig: Add oxidizer storage + dual gas consumer + fueled fabricator

    // Smoker: Add oxidizer storage and gas consumer; add fueled fabricator; consume oxidizer during active fabrication only
    [HarmonyPatch(typeof(SmokerConfig), "ConfigureBuildingTemplate")]
    public static class SmokerConfig_OxidizerPatch
    {
        private const float OXYGEN_INPUT = 0.2f;        // same as Kiln
        private const float OXIDIZER_CONSUMPTION = 0.015f; // requested rate

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<OxidizerLowStatus>();
            // Use the same storage the Smoker uses for CO2 and its ConduitDispenser
            var dispenser = go.GetComponent<ConduitDispenser>();
            Storage baseStorage = dispenser != null ? dispenser.storage : go.GetComponent<Storage>();
            if (baseStorage == null)
                baseStorage = go.AddOrGet<Storage>();
            // Ensure the dispenser only outputs CO2, so stored oxygen isn't pumped out
            if (dispenser != null)
                dispenser.elementFilter = new SimHashes[] { SimHashes.CarbonDioxide };

            // Dual gas consumer stores into the same storage so the converter can consume from it
            DualGasElementConsumer dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRate = OXYGEN_INPUT;
            dualGasConsumer.sampleCellOffset = new Vector3(0, 0);
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.isRequired = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = baseStorage;

            // Fueled fabricator gate (uses baseStorage) so work only proceeds with enough oxidizer on hand
            var fueledFabricator = go.AddOrGet<FueledFabricator>();
            fueledFabricator.fuelTag = ModTags.OxidizerGas;
            fueledFabricator.START_FUEL_MASS = 0.1f;
            fueledFabricator.storage = baseStorage;

            // Combine oxidizer consumption with the existing CO2 converter so both run together
            ElementConverter elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter != null)
            {
                var consumed = elementConverter.consumedElements != null
                    ? elementConverter.consumedElements.ToList()
                    : new System.Collections.Generic.List<ElementConverter.ConsumedElement>();
                // Avoid duplicates
                consumed.Add(new ElementConverter.ConsumedElement(ModTags.OxidizerGas, OXIDIZER_CONSUMPTION));
                elementConverter.consumedElements = consumed.ToArray();
                elementConverter.OperationalRequirement = Operational.State.Active;
                elementConverter.SetStorage(baseStorage);
            }
        }
    }

    // Smoker: Spawn 97 kg of Ash at the end of the smoking work, offset (1, 0) from the building cell
    [HarmonyPatch(typeof(Workable), "OnCompleteWork")]
    public static class Smoker_Ash_OnCompleteWork_Patch
    {
        public static void Postfix(Workable __instance, WorkerBase worker)
        {
            // Only when this is the Smoker's emptying workable on a Smoker building
            if (!(__instance is FoodSmokerWorkableEmpty)) return;
            var prefab = __instance.GetComponent<KPrefabID>();
            if (prefab == null || prefab.PrefabID().ToString() != SmokerConfig.ID) return;

            float amount = 97f;
            float temp = 353.15f;

            int cell = Grid.PosToCell(__instance.transform.position);
            int spawnCell = Grid.OffsetCell(cell, 1, 0);
            Vector3 pos = Grid.CellToPosCCC(spawnCell, Grid.SceneLayer.Ore);

            var elem = ElementLoader.FindElementByTag(ModElementRegistration.AshByproduct.Tag);
            if (elem != null)
            {
                var go = elem.substance.SpawnResource(pos, amount, temp, byte.MaxValue, 0);
                if (go != null) go.SetActive(true);
            }
        }
    }
}



