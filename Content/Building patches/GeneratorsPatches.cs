using HarmonyLib;
using Rephysicalized;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{

    //Solar Panel: not a heavy duty generator anymore, you can place it in a ranch. 
    // Patch the game's SolarPanelConfig.ConfigureBuildingTemplate method
    [HarmonyPatch(typeof(SolarPanelConfig), nameof(SolarPanelConfig.ConfigureBuildingTemplate))]
    public static class SolarPanelConfig_ConfigureBuildingTemplate_Patch
    {
        // Postfix runs after the original method, allowing us to remove the tag that was added
        [HarmonyPostfix]
        private static void Postfix(GameObject go, Tag prefab_tag)
        {
            var prefabId = go.GetComponent<KPrefabID>();
            if (prefabId != null)
            {
                // Remove the HeavyDutyGeneratorType tag
                prefabId.RemoveTag(RoomConstraints.ConstraintTags.HeavyDutyGeneratorType);
            }
        }
    }

    // GeneratorConfig: Patch to require both coal and oxygen as EnergyGenerator inputs (AirFilter logic)
    [HarmonyPatch(typeof(GeneratorConfig), "ConfigureBuildingTemplate")]
    public static class GeneratorConfig_OxygenHybridPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            // Ensure storage uses StandardSealedStorage modifiers (like AirFilter)
            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            // Add DualGasElementConsumer for both oxygen and contaminated oxygen
            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;


            // Add PrioritizedGasConsumerConfig for prioritized gas consumption
            var gasConfig = go.AddOrGet<PrioritizedGasConsumerConfig>();
            gasConfig.prioritizedGases = new List<SimHashes> { SimHashes.Oxygen, SimHashes.ContaminatedOxygen };
            gasConfig.consumptionRate = 0.003f;

            // Patch the EnergyGenerator formula to require both coal and oxygen (inputs only)
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen != null)
            {
                var oldFormula = energyGen.formula;
                energyGen.formula = new EnergyGenerator.Formula
                {
                    inputs = new EnergyGenerator.InputItem[]
                    {
                            new EnergyGenerator.InputItem(SimHashes.Carbon.CreateTag(), 1f, 600f),
                            new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.030f, 300f)
                    },
                    outputs = new EnergyGenerator.OutputItem[]
                    {
                        new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.04f, false, new CellOffset(0, 1), 383.15f),
                        new EnergyGenerator.OutputItem(ModElementRegistration.AshByproduct, 0.99f, true, default , 313.15f),
                    }
                };
            }
            var tilemaker = go.AddComponent<ElementTileMakerPatch>();
            tilemaker.emitTag = new Tag("AshByproduct");
            tilemaker.emitMass = 1200f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;
      

        }
    }

    // PeatGeneratorConfig: Patch to require both peat and oxygen as EnergyGenerator inputs (AirFilter logic)
    [HarmonyPatch(typeof(PeatGeneratorConfig), "ConfigureBuildingTemplate")]
    public static class PeatGeneratorConfig_OxygenHybridPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            // Add DualGasElementConsumer for both oxygen and contaminated oxygen
            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

            // Add PrioritizedGasConsumerConfig for prioritized gas consumption
            var gasConfig = go.AddOrGet<PrioritizedGasConsumerConfig>();
            gasConfig.prioritizedGases = new List<SimHashes> { SimHashes.Oxygen, SimHashes.ContaminatedOxygen };
            gasConfig.consumptionRate = 0.006f;

            // Patch the EnergyGenerator formula to require both peat and oxygen (inputs only)
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen != null)
            {
                var oldFormula = energyGen.formula;
                energyGen.formula = new EnergyGenerator.Formula
                {
                    inputs = new EnergyGenerator.InputItem[]
                    {
                            new EnergyGenerator.InputItem(SimHashes.Peat.CreateTag(), 1f, 600f),
                            new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.03f, 0.300f)
                    },
                    outputs = new EnergyGenerator.OutputItem[]
                    {
                        new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.04f, false, new CellOffset(0, 1), 383.15f),
                         new EnergyGenerator.OutputItem(SimHashes.DirtyWater, 0.2f, false, new CellOffset(1, 1), 313.15f),
                        new EnergyGenerator.OutputItem(ModElementRegistration.AshByproduct, 0.79f, true, default, 313.15f),
                    }
                };
            }
            var tilemaker = go.AddComponent<ElementTileMakerPatch>();
            tilemaker.emitTag = new Tag("AshByproduct");
            tilemaker.emitMass = 1200f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;


        }
    }
    // Ensure the DualGasElementConsumer on the generator is always enabled
    [HarmonyPatch(typeof(EnergyGenerator), "OnSpawn")]
    public static class EnergyGenerator_ForceElementConsumerPatch
    {
        public static void Postfix(EnergyGenerator __instance)
        {
            // Ensure custom dual gas consumer stays enabled
            var dualConsumer = __instance.GetComponent<DualGasElementConsumer>();
            if (dualConsumer != null)
            {
                dualConsumer.enabled = true;
            }

            // Ensure any ElementConsumer with ignoreActiveChanged consumes immediately
            var ecs = __instance.GetComponents<ElementConsumer>();
            if (ecs != null)
            {
                foreach (var ec in ecs)
                {
                    if (ec != null && ec.ignoreActiveChanged)
                        ec.EnableConsumption(true);
                }
            }
        }
    }

    // Patch EnergyGenerator to consume prioritized gases only when running, using config
    [HarmonyPatch(typeof(EnergyGenerator), nameof(EnergyGenerator.EnergySim200ms))]
    public static class EnergyGenerator_PrioritizedGasConsumptionPatch
    {
        public static void Postfix(EnergyGenerator __instance, float dt)
        {
            var config = __instance.GetComponent<PrioritizedGasConsumerConfig>();
            var storage = __instance.GetComponent<Storage>();
            var operational = __instance.GetComponent<Operational>();
            if (config == null || storage == null || operational == null || !operational.IsActive)
                return;
            foreach (var gas in config.prioritizedGases)
            {
                float needed = config.consumptionRate * dt;
                float available = storage.GetMassAvailable(gas.CreateTag());
                if (available >= needed)
                {
                    storage.ConsumeIgnoringDisease(gas.CreateTag(), needed);
                    break;
                }
            }
        }
    }
    // WoodGasGeneratorConfig: Patch to require both wood and oxygen as EnergyGenerator inputs (AirFilter logic)
    [HarmonyPatch(typeof(WoodGasGeneratorConfig), "DoPostConfigureComplete")]
    public static class WoodGasGeneratorConfig_OxygenHybridPatch
    {
        public static void Postfix(GameObject go)
        {
            // Ensure storage uses StandardSealedStorage modifiers (like AirFilter)
            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas,

                };


            }

            // Add DualGasElementConsumer for both oxygen and contaminated oxygen
            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;


            // Add PrioritizedGasConsumerConfig for prioritized gas consumption
            var gasConfig = go.AddOrGet<PrioritizedGasConsumerConfig>();
            gasConfig.prioritizedGases = new List<SimHashes> { SimHashes.Oxygen, SimHashes.ContaminatedOxygen };
            gasConfig.consumptionRate = 0.006f;

            // Patch the EnergyGenerator formula to require both wood and oxygen (inputs only)
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen != null)
            {
                var oldFormula = energyGen.formula;
                energyGen.formula = new EnergyGenerator.Formula
                {
                    inputs = new EnergyGenerator.InputItem[]
                    {
                            new EnergyGenerator.InputItem(SimHashes.WoodLog.CreateTag(), 1.2f, 720f),
                            new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.03f, 300f)
                    },
                    outputs = new EnergyGenerator.OutputItem[]
                    {
                            new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.17f, false, new CellOffset(0, 1), 383.15f),
                           new EnergyGenerator.OutputItem(ModElementRegistration.AshByproduct, 1.06f, true, new CellOffset(0, 0), 313.15f),
                    }

                };

                var tilemaker = go.AddComponent<ElementTileMakerPatch>();
                tilemaker.emitTag = new Tag("AshByproduct");
                tilemaker.emitMass = 1200f;
                tilemaker.emitOffset = new Vector3(0f, 0f);
                tilemaker.storage = storage;

            }
        }
    }
    // HydrogenGeneratorConfig: Add Oxygen ElementConsumer and dual-input formula
    [HarmonyPatch(typeof(HydrogenGeneratorConfig), nameof(HydrogenGeneratorConfig.DoPostConfigureComplete))]
    public static class HydrogenGenerator_OxygenConsumerAndFormulaPatch
    {
        public static readonly ConduitPortInfo OXYGEN_GAS_PORT = new ConduitPortInfo(ConduitType.Gas, new CellOffset(2, 0));

        public static void Postfix(GameObject go)
        {
            // Ensure there is a Storage the EnergyGenerator uses
            var storage = go.GetComponent<Storage>() ?? go.AddOrGet<Storage>();
            storage.showInUI = true;
            storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            go.AddOrGet<OxidizerLowStatus>();
            // Ambient Oxygen consumer (from world)
            var oxygenConsumer = go.AddOrGet<ElementConsumer>();
            oxygenConsumer.elementToConsume = SimHashes.Oxygen;
            oxygenConsumer.consumptionRate = 1f;
            oxygenConsumer.capacityKG = 4f;
            oxygenConsumer.consumptionRadius = 3;
            oxygenConsumer.sampleCellOffset = new Vector3(2f, 1f, 0f);
            oxygenConsumer.isRequired = true;
            oxygenConsumer.storeOnConsume = true;
            oxygenConsumer.showInStatusPanel = true;
            oxygenConsumer.showDescriptor = false;
            oxygenConsumer.ignoreActiveChanged = true;
            oxygenConsumer.storage = storage;
            oxygenConsumer.EnableConsumption(true);

            // Secondary gas input for Oxygen via pipe
            var gasSecondaryInput = go.AddComponent<ConduitSecondaryInput>();
            gasSecondaryInput.portInfo = OXYGEN_GAS_PORT;
            gasSecondaryInput.HasSecondaryConduitType(ConduitType.Gas);

            var oxygenPipeConsumer = go.AddComponent<ConduitConsumer>();
            oxygenPipeConsumer.conduitType = ConduitType.Gas;
            oxygenPipeConsumer.capacityTag = SimHashes.Oxygen.CreateTag();
            oxygenPipeConsumer.capacityKG = 4f;
            oxygenPipeConsumer.storage = storage;
            oxygenPipeConsumer.forceAlwaysSatisfied = true;
            oxygenPipeConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            oxygenPipeConsumer.alwaysConsume = true;
            oxygenPipeConsumer.useSecondaryInput = true;

            // Register the secondary gas input with the gas network on spawn
            go.AddComponent<HydrogenGenerator_O2SecondaryGasInitializer>();

            var eg = go.GetComponent<EnergyGenerator>();
            if (eg != null)
            {
                eg.formula = new EnergyGenerator.Formula
                {
                    inputs = new[]
                    {
                        new EnergyGenerator.InputItem(SimHashes.Hydrogen.CreateTag(), 0.1f, 2f),
                        new EnergyGenerator.InputItem(SimHashes.Oxygen.CreateTag(), 0.8f, 4f),
                    },
                    outputs = new[]
                    {
                        new EnergyGenerator.OutputItem(SimHashes.Water, 0.9f, false, new CellOffset(-1, 2), 343f),
                    }
                };
            }
        }
    }

    // Handles network registration of the Hydrogen Generator's Oxygen secondary gas input
    public class HydrogenGenerator_O2SecondaryGasInitializer : KMonoBehaviour
    {
        private int gasInputCell = 0;
        public override void OnSpawn()
        {
            base.OnSpawn();
            var go = gameObject;
            var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
            gasInputCell = Grid.OffsetCell(Grid.PosToCell(go), HydrogenGenerator_OxygenConsumerAndFormulaPatch.OXYGEN_GAS_PORT.offset);
            var gasNetworkItem = new FlowUtilityNetwork.NetworkItem(ConduitType.Gas, Endpoint.Sink, gasInputCell, go);
            gasNetworkManager.AddToNetworks(gasInputCell, gasNetworkItem, true);
        }

        public override void OnCleanUp()
        {
            if (gasInputCell != 0)
            {
                var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
                if (gasNetworkManager != null)
                    gasNetworkManager.RemoveFromNetworks(gasInputCell, gameObject, true);
            }
            base.OnCleanUp();
        }
    }

    [HarmonyPatch(typeof(MethaneGeneratorConfig), "DoPostConfigureComplete")]
    public static class MethaneGenerator_OxygenConsumerAndFormulaPatch
    {
        public static readonly ConduitPortInfo OXYGEN_GAS_PORT = new ConduitPortInfo(ConduitType.Gas, new CellOffset(-1, 2));
        public static void Postfix(GameObject go)
        {
            // Ensure storage uses StandardSealedStorage modifiers (like AirFilter)
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            go.AddOrGet<OxidizerLowStatus>();

            // Add DualGasElementConsumer for both oxygen and contaminated oxygen
            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.6f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(-1f, 2f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

            // Secondary gas input for Oxygen via pipe
            var gasSecondaryInput = go.AddComponent<ConduitSecondaryInput>();
            gasSecondaryInput.portInfo = OXYGEN_GAS_PORT;
            gasSecondaryInput.HasSecondaryConduitType(ConduitType.Gas);

            var oxygenPipeConsumer = go.AddComponent<ConduitConsumer>();
            oxygenPipeConsumer.conduitType = ConduitType.Gas;
            oxygenPipeConsumer.capacityTag = ModTags.OxidizerGas;
            oxygenPipeConsumer.capacityKG = 1f;
            oxygenPipeConsumer.storage = storage;
            oxygenPipeConsumer.forceAlwaysSatisfied = true;
            oxygenPipeConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            oxygenPipeConsumer.alwaysConsume = true;
            oxygenPipeConsumer.useSecondaryInput = true;

            // Register the secondary gas input with the gas network on spawn
            go.AddComponent<MethaneGenerator_O2SecondaryGasInitializer>();

            // Add PrioritizedGasConsumerConfig for prioritized gas consumption
            var gasConfig = go.AddOrGet<PrioritizedGasConsumerConfig>();
            gasConfig.prioritizedGases = new List<SimHashes> { SimHashes.Oxygen, SimHashes.ContaminatedOxygen };
            gasConfig.consumptionRate = 0.06f;

            // Patch the EnergyGenerator formula to require both methane and oxygen (inputs only)
            var energyGen = go.GetComponent<EnergyGenerator>();
            if (energyGen != null)
            {
                var oldFormula = energyGen.formula;
                energyGen.formula = new EnergyGenerator.Formula
                {
                    inputs = new EnergyGenerator.InputItem[2]
                    {
                            new EnergyGenerator.InputItem(SimHashes.Methane.CreateTag(), 0.09f, 0.9f),
                            new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.06f, 0.300f)
                    },
                    outputs = new EnergyGenerator.OutputItem[2]
      {
        new EnergyGenerator.OutputItem(SimHashes.DirtyWater, 0.08f, false, new CellOffset(1, 1), 313.15f),
        new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.07f, true, new CellOffset(0, 2), 383.15f)
      }
                };
            }

            ConduitDispenser conduitDispenser = go.AddOrGet<ConduitDispenser>();
            conduitDispenser.conduitType = ConduitType.Gas;
            conduitDispenser.invertElementFilter = true;
            conduitDispenser.elementFilter = new SimHashes[4]
            {
      SimHashes.Methane,
      SimHashes.Syngas,
           SimHashes.Oxygen,
      SimHashes.ContaminatedOxygen,
            };

        }

        // Handles network registration of the Methane Generator's Oxygen secondary gas input
        public class MethaneGenerator_O2SecondaryGasInitializer : KMonoBehaviour
        {
            private int gasInputCell = 0;
            public override void OnSpawn()
            {
                base.OnSpawn();
                var go = gameObject;
                var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
                gasInputCell = Grid.OffsetCell(Grid.PosToCell(go), MethaneGenerator_OxygenConsumerAndFormulaPatch.OXYGEN_GAS_PORT.offset);
                var gasNetworkItem = new FlowUtilityNetwork.NetworkItem(ConduitType.Gas, Endpoint.Sink, gasInputCell, go);
                gasNetworkManager.AddToNetworks(gasInputCell, gasNetworkItem, true);
            }

            public override void OnCleanUp()
            {
                if (gasInputCell != 0)
                {
                    var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
                    if (gasNetworkManager != null)
                        gasNetworkManager.RemoveFromNetworks(gasInputCell, gameObject, true);
                }
                base.OnCleanUp();
            }
        }



        //PetroleumGeneratorConfig: Patch to add gas consumer and secondary liquid input  


        [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
        public static class PetroleumGenerator_AddToDescription
        {
            public static void Prefix()
            {
                const string key = "STRINGS.BUILDINGS.PREFABS.PETROLEUMGENERATOR.EFFECT";

                // Append gas input functionality to the description
                Strings.Add(key, Strings.Get(key) + "\n\n <b>Requires Oxidizer gas input pipe</b>");
            }
        }

        [HarmonyPatch(typeof(PetroleumGeneratorConfig), nameof(PetroleumGeneratorConfig.DoPostConfigureComplete))]
        public static class Patch_PetroleumGeneratorConfig_GasConsumer
        {
            public static readonly ConduitPortInfo GAS_PORT = new ConduitPortInfo(ConduitType.Gas, new CellOffset(0, 0));
            public static readonly ConduitPortInfo LIQUID_PORT = new ConduitPortInfo(ConduitType.Liquid, new CellOffset(-1, 0));

            public static void Postfix(GameObject go)
            {
                // Attach a custom initializer that will run once the building is fully initialized
                go.AddComponent<GasAndLiquidInitializer>();
                var gasSecondaryInput = go.AddComponent<ConduitSecondaryInput>();
                gasSecondaryInput.portInfo = GAS_PORT;
                gasSecondaryInput.HasSecondaryConduitType(ConduitType.Gas);
                go.AddOrGet<OxidizerLowStatus>();
            }

            private class GasAndLiquidInitializer : KMonoBehaviour
            {
                private int gasInputCell = 0;
                private int liquidInputCell = -1;

                public override void OnSpawn()
                {
                    base.OnSpawn();

                    // Ensure storage is available
                    var go = gameObject;
                    var storage = go.GetComponent<Storage>() ?? go.AddOrGet<Storage>();

                    // Update the existing liquid consumer to treat it as secondary input
                    ConduitConsumer existingLiquidConsumer = go.GetComponent<ConduitConsumer>();
                    existingLiquidConsumer.useSecondaryInput = true;
                    existingLiquidConsumer.storage = storage;

                    // Add Secondary Gas ConduitConsumer

                    var gasConsumer = go.AddComponent<ConduitConsumer>();
                    gasConsumer.conduitType = ConduitType.Gas;
                    gasConsumer.capacityTag = ModTags.OxidizerGas;
                    gasConsumer.capacityKG = 10f;
                    gasConsumer.storage = storage;
                    gasConsumer.forceAlwaysSatisfied = true;
                    gasConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
                    gasConsumer.alwaysConsume = true;
                    gasConsumer.useSecondaryInput = true;

                    // Register the gas input with its network
                    var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);

                    gasInputCell = Grid.OffsetCell(Grid.PosToCell(go), GAS_PORT.offset);
                    var gasNetworkItem = new FlowUtilityNetwork.NetworkItem(ConduitType.Gas, Endpoint.Sink, gasInputCell, go);
                    gasNetworkManager.AddToNetworks(gasInputCell, gasNetworkItem, true);



                    // Register the liquid input with its network
                    var liquidNetworkManager = Conduit.GetNetworkManager(ConduitType.Liquid);

                    liquidInputCell = Grid.OffsetCell(Grid.PosToCell(go), LIQUID_PORT.offset);
                    var liquidNetworkItem = new FlowUtilityNetwork.NetworkItem(ConduitType.Liquid, Endpoint.Sink, liquidInputCell, go);
                    liquidNetworkManager.AddToNetworks(liquidInputCell, liquidNetworkItem, true);


                    // Update Energy Generator formula
                    var energyGen = go.GetComponent<EnergyGenerator>();

                    energyGen.formula = new EnergyGenerator.Formula
                    {
                        inputs = new EnergyGenerator.InputItem[] {
                    new EnergyGenerator.InputItem(GameTags.CombustibleLiquid, 1.75f, 20f), // Liquid input
                    new EnergyGenerator.InputItem(ModTags.OxidizerGas, 0.25f, 20f)      // Gas input
                },
                        outputs = new EnergyGenerator.OutputItem[]
                {
                           new EnergyGenerator.OutputItem(SimHashes.CarbonDioxide, 0.5f, false, new CellOffset(0, 3), 383.15f),
        new EnergyGenerator.OutputItem(SimHashes.DirtyWater,   1f, false, new CellOffset(1, 1), 313.15f),

                new EnergyGenerator.OutputItem(ModElementRegistration.CrudByproduct, 0.5f, true, new CellOffset(-1, 0), 333.15f),
                }
                    };

                    var elementDropper = go.AddComponent<ElementDropper>();

                    elementDropper.emitTag = new Tag("CrudByproduct");
                    elementDropper.emitMass = 50f;
                    elementDropper.emitOffset = new Vector3(-1.0f, 0.0f, 0.0f);

                }

                public override void OnCleanUp()
                {
                    // Cleanup logic when the Petroleum Generator is destroyed
                    var go = gameObject;

                    // Unregister gas input
                    if (gasInputCell != 0)
                    {
                        var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
                        if (gasNetworkManager != null)
                        {
                            gasNetworkManager.RemoveFromNetworks(gasInputCell, go, true);
                        }
                    }

                    // Unregister liquid input
                    if (liquidInputCell != -1)
                    {
                        var liquidNetworkManager = Conduit.GetNetworkManager(ConduitType.Liquid);
                        if (liquidNetworkManager != null)
                        {
                            liquidNetworkManager.RemoveFromNetworks(liquidInputCell, go, true);
                        }
                    }

                    base.OnCleanUp();
                }
            }
        }

    }

    }
