using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized
{

    [HarmonyPatch]
    internal static class CreatureMassTracker_CompactLists
    {
        // Minimal shape to describe extra drops
        private readonly struct Drop
        {
            public readonly string id;
            public readonly float fraction;
            public Drop(string id, float fraction) { this.id = id; this.fraction = fraction; }
        }

        // Per-food ratio override
        private readonly struct FoodRatio
        {
            public readonly string consumedTagName;
            public readonly float ratio;
            public FoodRatio(string consumedTagName, float ratio) { this.consumedTagName = consumedTagName; this.ratio = ratio; }
        }

        // Generic entry used for both calorie- and mass-based configs
        private sealed class Entry
        {
            public readonly Type configType;       // Config class type (e.g., typeof(HatchConfig))
            public readonly string methodName;     // Usually "CreatePrefab" (some mods use "CreateStego", etc.)
            public readonly float? startMass;
            public readonly float? massRatio;      // If set or explicit mass mode => mass-based

            public readonly bool addLink;
            public readonly Drop[] drops;
            public readonly FoodRatio[] perFood;
            public readonly float? growDropUnits;  // e.g., BabyCrab Wood/Normal

            public Entry(Type type, string method, float? startMass = null, float? mass = null,
                         float? maxMult = null, bool addLink = false, Drop[] drops = null, FoodRatio[] perFood = null)
            {
                this.configType = type;
                this.methodName = method;
                this.startMass = startMass;

                this.massRatio = mass;

                this.addLink = addLink;
                this.drops = drops;
                this.perFood = perFood;

            }
        }
        private static readonly Entry[] MassEntries =
        {      
    // Hatches
            new Entry(typeof(HatchConfig), nameof(HatchConfig.CreatePrefab), startMass: 1f, mass: 1f/140f, addLink: true,    drops: new[] { new Drop("CrudByproduct", 1f) }),
            new Entry(typeof(BabyHatchConfig), nameof(BabyHatchConfig.CreatePrefab), startMass: 1f, mass: 1f/140f,  drops: new[] { new Drop("CrudByproduct", 1f) }),

            new Entry(typeof(HatchHardConfig), nameof(HatchHardConfig.CreatePrefab), startMass: 1f, mass: 1f/140f, addLink: true,     drops: new[] { new Drop("SedimentaryRock", 1f) }),
            new Entry(typeof(BabyHatchHardConfig), nameof(BabyHatchHardConfig.CreatePrefab), startMass: 1f, mass: 1f/140f,   drops: new[] { new Drop("SedimentaryRock", 1f) }),

            new Entry(typeof(HatchMetalConfig), nameof(HatchMetalConfig.CreatePrefab), startMass: 1f, mass: 5f/100f, addLink: true,   drops: new[] { new Drop("Katairite", 1f) }),
            new Entry(typeof(BabyHatchMetalConfig), nameof(BabyHatchMetalConfig.CreatePrefab), startMass: 1f, mass: 5f/100f,   drops: new[] { new Drop("Katairite", 1f) }),

            new Entry(typeof(HatchVeggieConfig), nameof(HatchVeggieConfig.CreatePrefab), startMass: 1f, mass: 5f/140f, addLink: true,   drops: new[] { new Drop("Algae", 1f) }),
            new Entry(typeof(BabyHatchVeggieConfig), nameof(BabyHatchVeggieConfig.CreatePrefab), startMass: 1f, mass: 5f/140f,   drops: new[] { new Drop("Algae", 1f) }),

  // Raptors
            new Entry(typeof(RaptorConfig), nameof(RaptorConfig.CreatePrefab), startMass: 4f, mass: 1f, addLink: true,  drops: new[] { new Drop("DinosaurMeat", 1f) }  ),
            new Entry(typeof(BabyRaptorConfig), nameof(BabyRaptorConfig.CreatePrefab), startMass: 4f, mass: 1f,   drops: new[] { new Drop("DinosaurMeat", 1f) }    ),

  // Drecko (base)
            new Entry(typeof(DreckoConfig), nameof(DreckoConfig.CreatePrefab), startMass: 1f, mass: 1f/11f,  addLink: true),
            new Entry(typeof(BabyDreckoConfig), nameof(BabyDreckoConfig.CreatePrefab), startMass: 1f, mass: 1f/11f),

  // Pufts
            new Entry(typeof(PuftConfig), nameof(PuftConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f, addLink: true),
            new Entry(typeof(BabyPuftConfig), nameof(BabyPuftConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f),

            new Entry(typeof(PuftOxyliteConfig), nameof(PuftOxyliteConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f, addLink: true),
            new Entry(typeof(BabyPuftOxyliteConfig), nameof(BabyPuftOxyliteConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f),

            new Entry(typeof(PuftBleachstoneConfig), nameof(PuftBleachstoneConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f,  addLink: true),
            new Entry(typeof(BabyPuftBleachstoneConfig), nameof(BabyPuftBleachstoneConfig.CreatePrefab), startMass: 0.25f, mass: 0.05f),

            new Entry(typeof(PuftAlphaConfig), nameof(PuftAlphaConfig.CreatePrefab), startMass: 0.25f, mass: 0.9f, addLink: true, drops: new[] { new Drop("ContaminatedOxygen", 0.25f) , new Drop("CrudByproduct", 0.75f)}),
            new Entry(typeof(BabyPuftAlphaConfig), nameof(BabyPuftAlphaConfig.CreatePrefab), startMass: 0.25f, mass: 0.9f, drops: new[] { new Drop("ContaminatedOxygen", 0.25f) , new Drop("CrudByproduct", 0.75f)}),

  // Crabs
            new Entry(typeof(CrabConfig), nameof(CrabConfig.CreatePrefab), startMass: 1f, mass: 1f/7f, addLink: true,    drops: new[] { new Drop("Coquina", 1f) }),
            new Entry(typeof(BabyCrabConfig), nameof(BabyCrabConfig.CreatePrefab), startMass: 1f, mass: 1f/7f,   drops: new[] { new Drop("Coquina", 1f) }),

            new Entry(typeof(CrabWoodConfig), nameof(CrabWoodConfig.CreatePrefab), startMass: 1f, mass: 105f/140f, addLink: true, drops: new[] { new Drop("RotPile", 1f) }),
            new Entry(typeof(BabyCrabWoodConfig), nameof(BabyCrabWoodConfig.CreatePrefab), startMass: 1f, mass: 105f/140f,     drops: new[] { new Drop("RotPile", 1f) }),

            new Entry(typeof(CrabFreshWaterConfig), nameof(CrabFreshWaterConfig.CreatePrefab), startMass: 1f, mass: 10f/15f, addLink: true,  drops: new[] { new Drop("Clay", 1f) }),
            new Entry(typeof(BabyCrabFreshWaterConfig), nameof(BabyCrabFreshWaterConfig.CreatePrefab), startMass: 1f, mass: 10f/15f,  drops: new[] { new Drop("Clay", 1f) }),

    
    // Seals
            new Entry(typeof(SealConfig), nameof(SealConfig.CreatePrefab), startMass: 1f, mass: 8f/60f, addLink: true,    drops: new[] { new Drop("Tallow", 1f) }),
            new Entry(typeof(BabySealConfig), nameof(BabySealConfig.CreatePrefab), startMass: 1f, mass: 8f/60f, drops: new[] { new Drop("Tallow", 1f) }),
  // PrehistoricPacu
            new Entry(typeof(PrehistoricPacuConfig), nameof(PrehistoricPacuConfig.CreatePrefab), startMass: 2f, mass: 1f, addLink: true,   drops: new[] { new Drop("PrehistoricPacuFillet", 1f)  }),
            new Entry(typeof(PrehistoricPacuConfig), nameof(PrehistoricPacuConfig.CreatePrefab), startMass: 2f, mass: 1f,     drops: new[] { new Drop("PrehistoricPacuFillet", 1f)  }),
  // Staterpillars (Plug/Sponge/Gas Slug families)
            new Entry(typeof(StaterpillarConfig), nameof(StaterpillarConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true,  drops: new[] { new Drop("RotPile", 1f) }),
            new Entry(typeof(BabyStaterpillarConfig), nameof(BabyStaterpillarConfig.CreatePrefab), startMass: 1f, mass: 0.05f,  drops: new[] { new Drop("RotPile", 1f) }),

            new Entry(typeof(StaterpillarLiquidConfig), nameof(StaterpillarLiquidConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true,    drops: new[] { new Drop("CrudByproduct", 1f) }),
            new Entry(typeof(BabyStaterpillarLiquidConfig), nameof(BabyStaterpillarLiquidConfig.CreatePrefab), startMass: 1f, mass: 0.05f, drops: new[] { new Drop("CrudByproduct", 1f) }),

            new Entry(typeof(StaterpillarGasConfig), nameof(StaterpillarGasConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true,  drops: new[] { new Drop("CrudByproduct", 1f) }),
            new Entry(typeof(BabyStaterpillarGasConfig), nameof(BabyStaterpillarGasConfig.CreatePrefab), startMass: 1f, mass: 0.05f,  drops: new[] { new Drop("CrudByproduct", 1f) }),

   // Moles
            new Entry(typeof(MoleConfig), nameof(MoleConfig.CreatePrefab), startMass: 1f, mass: 0.5f, addLink: true,
                      drops: new[] { new Drop("CrudByproduct", 0.983f), new Drop("Meat", 0.007f), new Drop("RefinedLipid", 0.01f) }),
            new Entry(typeof(BabyMoleConfig), nameof(BabyMoleConfig.CreatePrefab), startMass: 1f, mass: 0.5f,
                      drops: new[] { new Drop("CrudByproduct", 0.983f), new Drop("Meat", 0.005f), new Drop("RefinedLipid", 0.01f) }),

            new Entry(typeof(MoleDelicacyConfig), nameof(MoleDelicacyConfig.CreatePrefab), startMass: 1f, mass: 0.5f, addLink: true,
                      drops: new[] { new Drop("CrudByproduct", 0.9865f), new Drop("Meat", 0.0035f), new Drop("RefinedLipid", 0.01f) }),
            new Entry(typeof(BabyMoleDelicacyConfig), nameof(BabyMoleDelicacyConfig.CreatePrefab), startMass: 1f, mass: 0.5f,
                      drops: new[] { new Drop("CrudByproduct", 0.9865f), new Drop("Meat", 0.0035f), new Drop("RefinedLipid", 0.01f) }),

   // Divergents
            new Entry(typeof(DivergentBeetleConfig), nameof(DivergentBeetleConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true),
            new Entry(typeof(BabyDivergentBeetleConfig), nameof(BabyDivergentBeetleConfig.CreatePrefab), startMass: 1f, mass: 0.05f),

            new Entry(typeof(DivergentWormConfig), nameof(DivergentWormConfig.CreatePrefab), startMass: 1f, mass: 0.025f, addLink: true,  perFood: new[] { new FoodRatio("Sucrose", 1f/30f) }),
            new Entry(typeof(BabyWormConfig), nameof(BabyWormConfig.CreatePrefab), startMass: 1f, mass: 0.025f, perFood: new[] { new FoodRatio("Sucrose", 1f/30f) }),

   // Chameleon (Dartle)
            new Entry(typeof(ChameleonConfig), nameof(ChameleonConfig.CreatePrefab), startMass: 0.5f, mass: 0.025f, addLink: true),
            new Entry(typeof(BabyChameleonConfig), nameof(BabyChameleonConfig.CreatePrefab), startMass: 0.5f, mass: 0.025f),
   // Light bugs (all colors)
            new Entry(typeof(LightBugConfig), nameof(LightBugConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true,    drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBabyConfig), nameof(LightBugBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,    drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugOrangeConfig), nameof(LightBugOrangeConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true,    drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugOrangeBabyConfig), nameof(LightBugOrangeBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,   drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugPurpleConfig), nameof(LightBugPurpleConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true,    drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugPurpleBabyConfig), nameof(LightBugPurpleBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,  drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugPinkConfig), nameof(LightBugPinkConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true,   drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugPinkBabyConfig), nameof(LightBugPinkBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,   drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugBlueConfig), nameof(LightBugBlueConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true, drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBlueBabyConfig), nameof(LightBugBlueBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,   drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugBlackConfig), nameof(LightBugBlackConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true,   drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBlackBabyConfig), nameof(LightBugBlackBabyConfig.CreatePrefab), startMass: 1f, mass: 1f,    drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugCrystalConfig), nameof(LightBugCrystalConfig.CreatePrefab), startMass: 0.1f, mass: 1f, addLink: true, drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugCrystalBabyConfig), nameof(LightBugCrystalBabyConfig.CreatePrefab), startMass: 1f, mass: 1f, drops: new[] { new Drop("Glass", 1f) }),
   // Slicksters (Oil Floater)
            new Entry(typeof(OilFloaterConfig), nameof(OilFloaterConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true, drops: new[] { new Drop("CrudByproduct", 1f) }),
            new Entry(typeof(OilFloaterBabyConfig), nameof(OilFloaterBabyConfig.CreatePrefab), startMass: 1f, mass: 0.05f, drops: new[] { new Drop("CrudByproduct", 1f) }),

            new Entry(typeof(OilFloaterHighTempConfig), nameof(OilFloaterHighTempConfig.CreatePrefab), startMass: 1f, mass: 0.05f, addLink: true, drops: new[] { new Drop("CrudByproduct", 1f) }),
            new Entry(typeof(OilFloaterHighTempBabyConfig), nameof(OilFloaterHighTempBabyConfig.CreatePrefab), startMass: 1f, mass: 0.05f, drops: new[] { new Drop("CrudByproduct", 1f) }),
   // Squirrels
            new Entry(typeof(SquirrelConfig), nameof(SquirrelConfig.CreatePrefab), mass: 1f/21f, addLink: true),
            new Entry(typeof(BabySquirrelConfig), nameof(BabySquirrelConfig.CreatePrefab), mass: 1f/21f),

            new Entry(typeof(SquirrelHugConfig), nameof(SquirrelHugConfig.CreatePrefab), mass: 1f/11f, addLink: true),
            new Entry(typeof(BabySquirrelHugConfig), nameof(BabySquirrelHugConfig.CreatePrefab), mass: 1f/11f),
  // Drecko Plastic
            new Entry(typeof(DreckoPlasticConfig), nameof(DreckoPlasticConfig.CreatePrefab), mass: 0.82f, addLink: true),
            new Entry(typeof(BabyDreckoPlasticConfig), nameof(BabyDreckoPlasticConfig.CreatePrefab), startMass: 1f, mass: 0.7f),

   // Pacu families
            new Entry(typeof(PacuConfig), nameof(PacuConfig.CreatePrefab), startMass: 0.75f, mass: 1f,  perFood: new[] { new FoodRatio("Algae", 1f / 3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f) , new FoodRatio("FishFood", 0.5f) }, addLink: true),
            new Entry(typeof(BabyPacuConfig), nameof(BabyPacuConfig.CreatePrefab), startMass: 0.75f,  mass: 1f,  perFood: new[] { new FoodRatio("Algae", 1f / 3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f) , new FoodRatio("FishFood", 0.5f) }),

            new Entry(typeof(PacuTropicalConfig), nameof(PacuTropicalConfig.CreatePrefab), startMass: 0.75f,  mass: 1f,  perFood: new[] { new FoodRatio("Algae", 1f/3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f) , new FoodRatio("FishFood", 0.5f) }, addLink: true),
            new Entry(typeof(BabyPacuTropicalConfig), nameof(BabyPacuTropicalConfig.CreatePrefab), startMass: 0.75f,  mass: 1f,  perFood: new[] { new FoodRatio("Algae", 1f / 3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f) , new FoodRatio("FishFood", 0.5f) }),

            new Entry(typeof(PacuCleanerConfig), nameof(PacuCleanerConfig.CreatePrefab), startMass: 0.75f, mass : 1f, perFood : new[] { new FoodRatio("Algae", 1f / 3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f), new FoodRatio("FishFood", 0.5f) }, addLink: true),
            new Entry(typeof(BabyPacuCleanerConfig), nameof(BabyPacuCleanerConfig.CreatePrefab), startMass: 0.75f, mass : 1f, perFood : new[] { new FoodRatio("Algae", 1f / 3f), new FoodRatio("Kelp", 0.5f), new FoodRatio("KelpPlant", 0.5f), new FoodRatio("FishFood", 0.5f) }),
            // PufferFish: default mass ratio 0.5 but specific foods override
            new Entry(typeof(PufferFishConfig), nameof(PufferFishConfig.CreatePrefab), startMass: 1f, mass: 0.5f, addLink: true,
                      perFood: new[] { new FoodRatio("SeaLettuce", 2f/17f), new FoodRatio("Lettuce", 0.5f) }),
            new Entry(typeof(BabyPufferFishConfig), nameof(BabyPufferFishConfig.CreatePrefab), startMass: 1f, mass: 0.5f,
                      perFood: new[] { new FoodRatio("SeaLettuce", 2f/17f), new FoodRatio("Lettuce", 0.5f) }),
//Snails
            new Entry(typeof(SnailConfig), nameof(SnailConfig.CreatePrefab), startMass: 10.25f, mass: 0.75f, addLink: true, drops: new[] { new Drop("Mucus", 1f) }),
            new Entry(typeof(BabySnailConfig), nameof(BabySnailConfig.CreatePrefab), startMass: 0.25f, mass: 0.75f, drops: new[] { new Drop("Mucus", 1f) }),

            new Entry(typeof(SnailIronConfig), nameof(SnailIronConfig.CreatePrefab), startMass: 10.25f, mass: 0.75f, addLink: true, drops: new[] { new Drop("Mucus", 1f) }),
            new Entry(typeof(BabySnailIronConfig), nameof(BabySnailIronConfig.CreatePrefab), startMass: 10.25f, mass: 0.75f, drops: new[] { new Drop("Mucus", 1f) }),
//Sea Horses
            new Entry(typeof(SeaHorseConfig), nameof(SeaHorseConfig.CreatePrefab), startMass: 2f, mass: 0.5f, addLink: true, drops: new[] { new Drop("Coquina", 1f) }),
            new Entry(typeof(BabySeaHorseConfig), nameof(BabySeaHorseConfig.CreatePrefab), startMass: 2f, mass: 0.5f, drops: new[] { new Drop("Coquina", 1f) }),

  //Squids
            new Entry(typeof(SquidConfig), nameof(SquidConfig.CreatePrefab), startMass: 2f, mass: 0.8f, addLink: true,  drops: new[] { new Drop("Coquina", 0.9f), new Drop("SquidMeat", 0.1f) }),
            new Entry(typeof(BabySquidConfig), nameof(BabySquidConfig.CreatePrefab), startMass: 2f, mass: 0.8f,  drops: new[] { new Drop("Coquina", 0.9f), new Drop("SquidMeat", 0.1f) }),
  //Seaturtle
            new Entry(typeof(SeaTurtleConfig), nameof(SeaTurtleConfig.CreatePrefab), startMass: 2f, mass: 0.025f, addLink: true,  drops: new[] { new Drop("Coquina", 0.85f), new Drop("ShellFishMeat", 0.15f) }),
            new Entry(typeof(BabySeaTurtleConfig), nameof(BabySeaTurtleConfig.CreatePrefab), startMass: 2f, mass: 0.025f,  drops: new[] { new Drop("Coquina", 0.85f), new Drop("ShellFishMeat", 0.15f) }),
//Beakons
            new Entry(typeof(ParrotFishConfig), nameof(ParrotFishConfig.CreatePrefab), startMass: 1f, mass: 0.5f, addLink: true),
            new Entry(typeof(BabyParrotFishConfig), nameof(BabyParrotFishConfig.CreatePrefab), startMass: 1f, mass: 0.5f),

  // Longhair slickster (decor)
            new Entry(typeof(OilFloaterDecorConfig), nameof(OilFloaterDecorConfig.CreatePrefab), startMass: 1f, mass: 1f, addLink: true,  drops: new[] { new Drop("OxyRock", 0.5f), new Drop("CrudByproduct", 0.5f) }),
            new Entry(typeof(OilFloaterDecorBabyConfig), nameof(OilFloaterDecorBabyConfig.CreatePrefab), startMass: 1f, mass: 1f, drops: new[] { new Drop("OxyRock", 0.5f), new Drop("CrudByproduct", 0.5f) }),
   // Bee 
            new Entry(typeof(BeeConfig),  nameof(BeeConfig.CreatePrefab), startMass: 1f, mass: 1f, addLink: true,   drops: new[] { new Drop("SolidNuclearWaste", 1f) }),
            new Entry(typeof(BabyBeeConfig), nameof(BabyBeeConfig.CreatePrefab), startMass: 1f, mass: 1f,  drops: new[] { new Drop("SolidNuclearWaste", 1f) }),
 // Stego (uses CreateStego)
            new Entry(typeof(StegoConfig), "CreateStego", startMass: 4f, mass: 1f, addLink: true,  drops: new[] { new Drop("DinosaurMeat", 0.12f), new Drop("RotPile", 0.88f)  }),
            new Entry(typeof(BabyStegoConfig), nameof(BabyStegoConfig.CreatePrefab), startMass: 4f, mass: 1f,    drops: new[] { new Drop("DinosaurMeat", 0.12f),  new Drop("RotPile", 0.88f) }),
            new Entry(typeof(AlgaeStegoConfig), "CreateStego", startMass: 4f, mass: 1f, addLink: true,   drops: new[] { new Drop("DinosaurMeat", 0.12f), new Drop("RotPile", 0.88f)  }),
            new Entry(typeof(BabyAlgaeStegoConfig), nameof(BabyAlgaeStegoConfig.CreatePrefab), startMass: 4f, mass: 1f,  drops: new[] { new Drop("DinosaurMeat", 0.12f),  new Drop("RotPile", 0.88f) }),
 //Morb
            new Entry(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab), startMass: 1f, mass: 1f,  drops: new[] { new Drop("Slime", 0f) }),
   //Butterfly
            new Entry(typeof(ButterflyConfig), nameof(ButterflyConfig.CreatePrefab), startMass: 1f, mass: 1f,   drops: new[] { new Drop("Shale", 1f) }),
   // Moo
            new Entry(typeof(MooConfig), nameof(MooConfig.CreatePrefab), startMass: 10f, mass: 0.9f, addLink: true   ),
            new Entry(typeof(DieselMooConfig), nameof(DieselMooConfig.CreatePrefab), startMass: 10f, mass: 0.95f, addLink: true   ),

            // Deer: Wood/Glass deer default mass ratio 1f but override some plants
            new Entry(typeof(WoodDeerConfig), nameof(WoodDeerConfig.CreatePrefab), startMass: 1f, mass: 1f, addLink: true,
                      perFood: new[] { new FoodRatio("HardSkinBerryPlant", 0.3334f), new FoodRatio("PrickleFlower", 1f/12f), new FoodRatio("Katairite", 0.5f) }),
            new Entry(typeof(BabyWoodDeerConfig), nameof(BabyWoodDeerConfig.CreatePrefab), startMass: 1f, mass: 1f,
                      perFood: new[] { new FoodRatio("HardSkinBerryPlant", 0.3334f), new FoodRatio("PrickleFlower", 1f/12f), new FoodRatio("Katairite", 0.5f) }),
            new Entry(typeof(GlassDeerConfig), nameof(GlassDeerConfig.CreatePrefab), startMass: 1f, mass: 1f, addLink: true,
                      perFood: new[] { new FoodRatio("HardSkinBerryPlant", 0.3334f), new FoodRatio("PrickleFlower", 1f/12f), new FoodRatio("Katairite", 0.5f) }),
            new Entry(typeof(BabyGlassDeerConfig), nameof(BabyGlassDeerConfig.CreatePrefab), startMass: 1f, mass: 1f,
                      perFood: new[] { new FoodRatio("HardSkinBerryPlant", 0.3334f), new FoodRatio("PrickleFlower", 1f/12f), new FoodRatio("Katairite", 0.5f) }),

            // Belly (IceBelly/GoldBelly): mostly massRatio 1f; override carrots/beans
            new Entry(typeof(IceBellyConfig), nameof(IceBellyConfig.CreatePrefab), startMass: 4f, mass: 1f, addLink: true,
                      perFood: new[] { new FoodRatio("CarrotPlant", 0.033334f), new FoodRatio("BeanPlant", 0.033334f) },
                      drops: new[] { new Drop("Meat", 0.66f), new Drop("Rotpile", 0.34f) }),
            new Entry(typeof(BabyIceBellyConfig), nameof(BabyIceBellyConfig.CreatePrefab), startMass: 4f, mass: 1f,
                      perFood: new[] { new FoodRatio("CarrotPlant", 0.033334f), new FoodRatio("BeanPlant", 0.033334f) },
                      drops: new[] { new Drop("Meat", 0.66f), new Drop("Rotpile", 0.34f) }),

            new Entry(typeof(GoldBellyConfig), nameof(GoldBellyConfig.CreatePrefab), startMass: 4f, mass: 1f, addLink: true,
                      perFood: new[] { new FoodRatio("CarrotPlant", 0.033334f), new FoodRatio("BeanPlant", 0.033334f) },
                      drops: new[] { new Drop("Meat", 1f) }),
            new Entry(typeof(BabyGoldBellyConfig), nameof(BabyGoldBellyConfig.CreatePrefab), startMass: 4f, mass: 1f,
                      perFood: new[] { new FoodRatio("CarrotPlant", 0.033334f), new FoodRatio("BeanPlant", 0.033334f) },
                      drops: new[] { new Drop("Meat", 1f) }),

            new Entry(typeof(BaseBeeHiveConfig), nameof(BaseBeeHiveConfig.CreatePrefab), mass: 0.0f,   drops: new[] { new Drop("SolidNuclearWaste", 1f) }), //Fake parameter cause beehive does not have butcherable
        };

        private static List<MethodBase> _targets;

        // Collect all target methods from the mass entries
        private static IEnumerable<MethodBase> TargetMethods()
        {
            _targets ??= new List<MethodBase>(MassEntries.Length);
            _targets.Clear();

            void AddTargets(Entry[] entries)
            {
                foreach (var e in entries)
                {
                    var m = AccessTools.Method(e.configType, e.methodName);
                    if (m != null) _targets.Add(m);
                }
            }

            AddTargets(MassEntries);
            return _targets;
        }

        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod, ref GameObject __result)
        {
            if (__result == null || __originalMethod == null) return;

            try
            {
                string methodInfo = __originalMethod.DeclaringType?.FullName + "." + __originalMethod.Name;
                string goName = __result != null ? __result.name : "<null>";
            }
            catch { }

            // Find the matching entry and apply
            var entry = FindEntry(__originalMethod, MassEntries);


            try
            {
                ApplyEntry(__result, entry);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] Mass tracker apply failed for {__originalMethod.DeclaringType?.Name}: {e}");
            }
        }

        private static Entry FindEntry(MethodBase m, Entry[] entries)
        {
            var dt = m.DeclaringType;
            var name = m.Name;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.configType == dt && string.Equals(e.methodName, name, StringComparison.Ordinal))
                    return e;
            }
            return null;
        }

        private static void ApplyEntry(GameObject go, Entry e)
        {
            var tracker = go.AddOrGet<CreatureMassTracker>();
            if (e.startMass.HasValue) tracker.STARTING_MASS = e.startMass.Value;
            if (e.massRatio.HasValue) tracker.MASS_RATIO = e.massRatio.Value;

            // Extra drops
            if (e.drops != null && e.drops.Length > 0)
            {
                var list = new List<CreatureMassTracker.ExtraDropSpec>(e.drops.Length);
                for (int i = 0; i < e.drops.Length; i++)
                    list.Add(new CreatureMassTracker.ExtraDropSpec { id = e.drops[i].id, fraction = e.drops[i].fraction });

                tracker.ExtraDrops = list;
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid != null)
                    CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, list);
            }

            // Link component for adults or where needed
            if (e.addLink)
                go.AddOrGet<CreatureMassTrackerLink>();

            // Per-food overrides: build instance override list, set on tracker, and register at prefab level
            if (e.perFood != null && e.perFood.Length > 0)
            {
                var list = new List<CreatureMassTracker.PerFoodRatioSpec>(e.perFood.Length);
                for (int i = 0; i < e.perFood.Length; i++)
                {
                    var p = e.perFood[i];
                    if (!string.IsNullOrEmpty(p.consumedTagName))
                        list.Add(new CreatureMassTracker.PerFoodRatioSpec { consumedTag = p.consumedTagName, ratio = p.ratio });
                }
                if (list.Count > 0)
                {
                    tracker.SetPerFoodOverrides(list);
                    try
                    {
                        var kpid = go.GetComponent<KPrefabID>();
                        if (kpid != null)
                            CreatureMassTracker.RegisterPerFoodOverridesForPrefab(kpid.PrefabTag, list);
                    }
                    catch { }
                }
            }
        }
    }
}

