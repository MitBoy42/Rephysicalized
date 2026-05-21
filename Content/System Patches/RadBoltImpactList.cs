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
            public HashSet<string> affectedElementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Behavior toggles
            public bool launchNearby = true;

            // Emission: map of element id strings (SimHashes.CreateTag().ToString()) to fraction values; fractions will be normalized at runtime
            public Dictionary<string, float> emittedElementIds = null;
            public float spawnTempTargetK = 273f + 150f; // will be clamped to element bounds
            public float emissionMultiplier = 1f;  // emit mass = payloadKg * emissionMultiplier
            public float removalMultiplier = 1f;   // remove mass = payloadKg * removalMultiplier

            // Heating (ΔT for impacted solid)
            public float tempDeltaPerPayload = 0.02f;  // Uranium default: payload * 0.2f
            public float tempDeltaMin = 0f;
            public float tempDeltaMax = 300f;


            public bool Matches(SimHashes elem)
            {
                if (affectedElementIds == null) return false;
                var key = elem.CreateTag().ToString();
                return affectedElementIds.Contains(key);
            }
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
                affectedElementIds = new HashSet<string> { SimHashes.UraniumOre.CreateTag().ToString() },
          
                emittedElementIds = new Dictionary<string, float> { { SimHashes.EnrichedUranium.CreateTag().ToString(), 1f } },
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
                affectedElementIds = new HashSet<string> { SimHashes.DepletedUranium.CreateTag().ToString() },
      
                emittedElementIds = new Dictionary<string, float> { { SimHashes.Lead.CreateTag().ToString(), 0.87f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.13f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,

                sfxCandidates = new[] { "Meteor_Nuclear_Impact", "Meteor_Impact_Uranium", "Meteor_Impact_Nuclear" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f); }

            });


            // Lead
            Configs.Add(new HEPImpactConfig
            {
                id = "Lead",
                affectedElementIds = new HashSet<string> { SimHashes.Lead.CreateTag().ToString() },
       
                emittedElementIds = new Dictionary<string, float> { { SimHashes.SolidMercury.CreateTag().ToString(), 0.965f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.035f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });


            // Mercury
            Configs.Add(new HEPImpactConfig
            {
                id = "Mercury",
                affectedElementIds = new HashSet<string> { SimHashes.SolidMercury.CreateTag().ToString() },
      
                emittedElementIds = new Dictionary<string, float> { { SimHashes.Gold.CreateTag().ToString(), 0.985f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.015f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });
            // Cinnabar
            Configs.Add(new HEPImpactConfig
            {
                id = "Cinnabar",
                affectedElementIds = new HashSet<string> { SimHashes.Cinnabar.CreateTag().ToString() },
      
                emittedElementIds = new Dictionary<string, float> { { SimHashes.Gold.CreateTag().ToString(), 0.845f }, { SimHashes.Sulfur.CreateTag().ToString(), 0.14f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.015f } },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });

          

            // Gold 
            Configs.Add(new HEPImpactConfig
            {
                id = "Gold",
                affectedElementIds = new HashSet<string> { SimHashes.Gold.CreateTag().ToString() },
     
                emittedElementIds = new Dictionary<string, float>
                {
                    { SimHashes.Tungsten.CreateTag().ToString(), 0.935f },
                    { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.065f }
                },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });


            // Iridium 
            Configs.Add(new HEPImpactConfig
            {
                id = "Iridium",
                affectedElementIds = new HashSet<string> { SimHashes.Iridium.CreateTag().ToString() },
   
                emittedElementIds = new Dictionary<string, float>
                {
                    { SimHashes.Tungsten.CreateTag().ToString(), 0.96f },
                    { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.04f }
                },

                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });
            // Copper
            Configs.Add(new HEPImpactConfig
            {
                id = "Copper",
                affectedElementIds = new HashSet<string> { SimHashes.Copper.CreateTag().ToString() },
                emittedElementIds = new Dictionary<string, float>
                {
                    { SimHashes.Nickel.CreateTag().ToString(), 0.93f },
                    { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.07f }
                },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }

            });


            // Nickel
            Configs.Add(new HEPImpactConfig
            {
                id = "Nickel",
                affectedElementIds = new HashSet<string> { SimHashes.Nickel.CreateTag().ToString() },
                emittedElementIds = new Dictionary<string, float>
                {
                    { SimHashes.Cobalt.CreateTag().ToString(), 1f }
                }, //Cobalt's atomic weight is same as Nickels 
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

            // Cobalt
            Configs.Add(new HEPImpactConfig
            {
                id = "Cobalt",
                affectedElementIds = new HashSet<string> { SimHashes.Cobalt.CreateTag().ToString() },
                emittedElementIds = new Dictionary<string, float>
                {
                    { SimHashes.Iron.CreateTag().ToString(), 0.95f },
                    { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f }
                },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });


            //If ores are NOT Rephysicalized
            if (!Config.Instance.RephysicalizedMetalOre)
            {
              
                Configs.Add(new HEPImpactConfig
                {
                    id = "Gold Amalgam",
                    affectedElementIds = new HashSet<string> { SimHashes.GoldAmalgam.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Tungsten.CreateTag().ToString(), 0.935f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.065f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }     });



                Configs.Add(new HEPImpactConfig
                {   id = "Copper Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.Cuprite.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Nickel.CreateTag().ToString(), 0.93f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.07f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }  });




                Configs.Add(new HEPImpactConfig
                {
                    id = "Nickel Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.NickelOre.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Cobalt.CreateTag().ToString(), 1f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                            sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });


                Configs.Add(new HEPImpactConfig
                {
                    id = "Cobalt Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.Cobaltite.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Iron.CreateTag().ToString(), 0.95f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
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
                    affectedElementIds = new HashSet<string> { SimHashes.GoldAmalgam.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Tungsten.CreateTag().ToString(), 0.64f },
                        { SimHashes.Gold.CreateTag().ToString(), 0.31f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }  });

                // Copper Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Copper Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.Cuprite.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Nickel.CreateTag().ToString(), 0.74f },
                        { SimHashes.SolidOxygen.CreateTag().ToString(), 0.2f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.06f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
      
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }    });

                // Nickel Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Nickel Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.NickelOre.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Cobalt.CreateTag().ToString(), 0.73f },
                        { SimHashes.Sulfur.CreateTag().ToString(), 0.27f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });

                // Cobalt Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Cobalt Ore",
                    affectedElementIds = new HashSet<string> { SimHashes.Cobaltite.CreateTag().ToString() },
                    emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Iron.CreateTag().ToString(), 0.67f },
                        { SimHashes.Sulfur.CreateTag().ToString(), 0.29f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.04f }
                    },
                    emissionMultiplier = 200f,
                    removalMultiplier = 200f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }    });
      
            }
            //Ronivans Zinc

            Configs.Add(new HEPImpactConfig
            {
                id = "Zinc Ore",
                affectedElementIds = new HashSet<string> { "AurichalciteOre" },
                emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Copper.CreateTag().ToString(), 0.68f },
                        { SimHashes.SolidOxygen.CreateTag().ToString(), 0.15f },
                        { SimHashes.SolidCarbonDioxide.CreateTag().ToString(), 0.15f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.02f }
                    },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });
          
            //Ronivans Zinc
            Configs.Add(new HEPImpactConfig
            {
                id = "Zinc",
                affectedElementIds = new HashSet<string> { "SolidZinc" },
                emittedElementIds = new Dictionary<string, float>
                    {
                        { SimHashes.Copper.CreateTag().ToString(), 0.97f },
                        { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.03f }
                    },
                emissionMultiplier = 200f,
                removalMultiplier = 200f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

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

        public static Dictionary<string, float> GetEmittedElements(string id)
        {
            var cfg = Configs.Find(c => c != null && c.id == id);
            return cfg?.emittedElementIds ?? new Dictionary<string, float>();
        }
    }
}
