using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized
{
    // Centralized list of emitted element distributions and impact configs for radbolt impacts.
    public static class RadBoltImpactList
    {
        // Impact configuration for specific solid elements.
        public sealed class HEPImpactConfig
        {
            public string[] sfxCandidates;                 // Optional, if null -> no custom audio
            public Action<Vector3> spawnVfx;               // Optional, if null -> no custom vfx
            public string id;
            public HashSet<SimHashes> affectedElements = new HashSet<SimHashes>();

            // Behavior toggles
            public bool launchNearby = true;

            // Emission: map of elements to fraction values; fractions will be normalized at runtime
            public Dictionary<SimHashes, float> emittedElements = null;
            public float spawnTempTargetK = 273f + 150f; // will be clamped to element bounds
            public float emissionMultiplier = 1f;  // emit mass = payloadKg * emissionMultiplier
            public float removalMultiplier = 1f;   // remove mass = payloadKg * removalMultiplier

            // Heating (ΔT for impacted solid)
            public float tempDeltaPerPayload = 0.2f;  // Uranium default: payload * 0.2f
            public float tempDeltaMin = 0f;
            public float tempDeltaMax = 300f;


            public bool Matches(SimHashes elem) => affectedElements != null && affectedElements.Contains(elem);
        }

        // Registry holding all impact configurations.
        public static readonly List<HEPImpactConfig> Configs = new List<HEPImpactConfig>();

        static RadBoltImpactList()
        {
            InitDefaults();
        }

        private static void InitDefaults()
        {
            // UraniumOre configuration
            Configs.Add(new HEPImpactConfig
            {
                id = "uranium",
                affectedElements = new HashSet<SimHashes> { SimHashes.UraniumOre },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.EnrichedUranium, 1f } },
                spawnTempTargetK = 1000f,
                emissionMultiplier = 2f,
                removalMultiplier = 2f,
                tempDeltaPerPayload = 0.2f,
                tempDeltaMin = 10f,
                sfxCandidates = new[] { "Meteor_Nuclear_Impact", "Meteor_Impact_Uranium", "Meteor_Impact_Nuclear" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f); }

            });
            // UraniumDepleted configuration
            Configs.Add(new HEPImpactConfig
            {
                id = "uraniumdepleted",
                affectedElements = new HashSet<SimHashes> { SimHashes.DepletedUranium },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Lead, 0.87f }, { SimHashes.SolidNuclearWaste, 0.13f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Nuclear_Impact", "Meteor_Impact_Uranium", "Meteor_Impact_Nuclear" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f); }

            });


            // Lead
            Configs.Add(new HEPImpactConfig
            {
                id = "Lead",
                affectedElements = new HashSet<SimHashes> { SimHashes.Lead },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.SolidMercury, 0.965f }, { SimHashes.SolidNuclearWaste, 0.035f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });


            // Mercury
            Configs.Add(new HEPImpactConfig
            {
                id = "Mercury",
                affectedElements = new HashSet<SimHashes> { SimHashes.SolidMercury},
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Gold, 0.985f }, { SimHashes.SolidNuclearWaste, 0.015f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });
            // Cinnabar
            Configs.Add(new HEPImpactConfig
            {
                id = "Cinnabar",
                affectedElements = new HashSet<SimHashes> {  SimHashes.Cinnabar },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Gold, 0.845f }, { SimHashes.Sulfur, 0.14f }, { SimHashes.SolidNuclearWaste, 0.015f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });

          

            // Gold 
            Configs.Add(new HEPImpactConfig
            {
                id = "Gold",
                affectedElements = new HashSet<SimHashes> { SimHashes.Gold },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Tungsten, 0.935f }, { SimHashes.SolidNuclearWaste, 0.065f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });

            // Iridium 
            Configs.Add(new HEPImpactConfig
            {
                id = "Iridium",
                affectedElements = new HashSet<SimHashes> { SimHashes.Iridium },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Tungsten, 0.96f }, { SimHashes.SolidNuclearWaste, 0.04f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });
            // Copper
            Configs.Add(new HEPImpactConfig
            {
                id = "Copper",
                affectedElements = new HashSet<SimHashes> { SimHashes.Copper },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Nickel, 0.93f }, { SimHashes.SolidNuclearWaste, 0.07f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }

            });

            // Nickel
            Configs.Add(new HEPImpactConfig
            {
                id = "Nickel",
                affectedElements = new HashSet<SimHashes> { SimHashes.Nickel },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Cobalt, 1f } }, //Cobalt's atomic weight is same as Nickels 
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); } });

            // Cobalt
            Configs.Add(new HEPImpactConfig
            {
                id = "Cobalt",
                affectedElements = new HashSet<SimHashes> { SimHashes.Cobalt },
                launchNearby = true,
                emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Iron, 0.95f }, { SimHashes.SolidNuclearWaste, 0.05f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }  });

            //If ores are NOT Rephysicalized
            if (!Config.Instance.RephysicalizedMetalOre)
            {
              
                Configs.Add(new HEPImpactConfig
                {
                    id = "Gold Amalgam",
                    affectedElements = new HashSet<SimHashes> {  SimHashes.GoldAmalgam },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Tungsten, 0.935f }, { SimHashes.SolidNuclearWaste, 0.065f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }     });


                Configs.Add(new HEPImpactConfig
                {   id = "Copper Ore",
                    affectedElements = new HashSet<SimHashes> { SimHashes.Cuprite },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Nickel, 0.93f }, { SimHashes.SolidNuclearWaste, 0.07f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }  });

                Configs.Add(new HEPImpactConfig
                {   id = "Nickel Ore",
                    affectedElements = new HashSet<SimHashes> { SimHashes.NickelOre },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Cobalt, 1f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });


                Configs.Add(new HEPImpactConfig
                {  id = "Cobalt Ore",
                    affectedElements = new HashSet<SimHashes> {  SimHashes.Cobaltite },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Iron, 0.95f }, { SimHashes.SolidNuclearWaste, 0.05f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }   });  
            }
            //IF ORES ARE REPHYSICALIZED
            if (Config.Instance.RephysicalizedMetalOre)
            {
                // Gold Amalgam
                Configs.Add(new HEPImpactConfig
                {
                    id = "Gold Amalgam",
                    affectedElements = new HashSet<SimHashes> { SimHashes.GoldAmalgam },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Tungsten, 0.64f }, { SimHashes.Gold, 0.31f }, { SimHashes.SolidNuclearWaste, 0.05f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }  });

                // Copper Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Copper Ore",
                    affectedElements = new HashSet<SimHashes> { SimHashes.Cuprite },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Nickel, 0.74f }, { SimHashes.SolidOxygen, 0.2f }, { SimHashes.SolidNuclearWaste, 0.06f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,

                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }    });

                // Nickel Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Nickel Ore",
                    affectedElements = new HashSet<SimHashes> { SimHashes.NickelOre},
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Cobalt, 0.73f }, { SimHashes.Sulfur, 0.27f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });

                // Cobalt Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Cobalt Ore",
                    affectedElements = new HashSet<SimHashes> { SimHashes.Cobaltite },
                    launchNearby = true,
                    emittedElements = new Dictionary<SimHashes, float> { { SimHashes.Iron, 67f }, { SimHashes.Sulfur, 29f }, { SimHashes.SolidNuclearWaste, 0.04f } },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    tempDeltaPerPayload = 0.02f,

                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });
      
            }
        }

        public static HEPImpactConfig FindMatch(SimHashes elem)
        {
            for (int i = 0; i < Configs.Count; i++)
            {
                var cfg = Configs[i];
                if (cfg != null && cfg.Matches(elem))
                    return cfg;
            }
            return null;
        }

        public static Dictionary<SimHashes, float> GetEmittedElements(string id)
        {
            var cfg = Configs.Find(c => c != null && c.id == id);
            return cfg?.emittedElements ?? new Dictionary<SimHashes, float>();
        }
    }
}
