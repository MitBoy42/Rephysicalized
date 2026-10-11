using Rephysicalized.ModElements;
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
            public string[] sfxCandidates;
            public System.Action<Vector3> spawnVfx;
            public string id;
            public HashSet<string> affectedElements = new HashSet<string>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
            public bool launchNearby = true;
            public Dictionary<string, float> emittedElements = (Dictionary<string, float>)null;
            public float spawnTempTargetK = 423f;
            public float emissionMultiplier = 1f;
            public float removalMultiplier = 1f;
            public float tempDeltaPerPayload = 0.02f;
            public float tempDeltaMin = 0.0f;
            public float tempDeltaMax = 300f;

            public bool MatchesTag(string elemTag) => this.affectedElements != null && !string.IsNullOrEmpty(elemTag) && this.affectedElements.Contains(elemTag);

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
                affectedElements = new HashSet<string> { SimHashes.UraniumOre.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.EnrichedUranium.CreateTag().ToString(), 1f } },
                spawnTempTargetK = 1000f,
                emissionMultiplier = 1000f * Config.Instance.EnrichedUraniumMult,
                removalMultiplier = 1000f * Config.Instance.EnrichedUraniumMult,
                tempDeltaPerPayload = 0.2f,
                tempDeltaMin = 10f,
                sfxCandidates = new[] { "Meteor_Nuclear_Impact", "Meteor_Impact_Uranium", "Meteor_Impact_Nuclear" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f); }

            });
            // UraniumDepleted configuration
            Configs.Add(new HEPImpactConfig
            {
                id = "uraniumdepleted",
                affectedElements = new HashSet<string> { SimHashes.DepletedUranium.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Lead.CreateTag().ToString(), 0.87f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.13f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Nuclear_Impact", "Meteor_Impact_Uranium", "Meteor_Impact_Nuclear" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f); }

            });


            // Lead
            Configs.Add(new HEPImpactConfig
            {
                id = "Lead",
                affectedElements = new HashSet<string> { SimHashes.Lead.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.SolidMercury.CreateTag().ToString(), 0.965f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.035f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });
            // Galena
            Configs.Add(new HEPImpactConfig
            {
                id = "Galena",
                affectedElements = new HashSet<string> { SimHashes.Galena.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.SolidMercury.CreateTag().ToString(), 0.84f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.03f }, { SimHashes.Sulfur.CreateTag().ToString(), 0.13f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });

            // Mercury
            Configs.Add(new HEPImpactConfig
            {
                id = "Mercury",
                affectedElements = new HashSet<string> { SimHashes.SolidMercury.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Gold.CreateTag().ToString(), 0.985f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.015f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });
            // Cinnabar
            Configs.Add(new HEPImpactConfig
            {
                id = "Cinnabar",
                affectedElements = new HashSet<string> { SimHashes.Cinnabar.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Gold.CreateTag().ToString(), 0.845f }, { SimHashes.Sulfur.CreateTag().ToString(), 0.14f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.015f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }

            });



            // Gold 
            Configs.Add(new HEPImpactConfig
            {
                id = "Gold",
                affectedElements = new HashSet<string> { SimHashes.Gold.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Tungsten.CreateTag().ToString(), 0.935f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.065f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });

            // Iridium 
            Configs.Add(new HEPImpactConfig
            {
                id = "Iridium",
                affectedElements = new HashSet<string> { SimHashes.Iridium.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Tungsten.CreateTag().ToString(), 0.96f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.04f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
            });
            // Zinc
            Configs.Add(new HEPImpactConfig
            {
                id = "Zinc",
                affectedElements = new HashSet<string> { SimHashes.Zinc.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Copper.CreateTag().ToString(), 0.97f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.03f },
                      },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

            // Copper
            Configs.Add(new HEPImpactConfig
            {
                id = "Copper",
                affectedElements = new HashSet<string> { SimHashes.Copper.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Nickel.CreateTag().ToString(), 0.93f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.07f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }

            });

            // Nickel
            Configs.Add(new HEPImpactConfig
            {
                id = "Nickel",
                affectedElements = new HashSet<string> { SimHashes.Nickel.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Cobalt.CreateTag().ToString(), 1f } }, //Cobalt's atomic weight is same as Nickels 
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

            // Cobalt
            Configs.Add(new HEPImpactConfig
            {
                id = "Cobalt",
                affectedElements = new HashSet<string> { SimHashes.Cobalt.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Iron.CreateTag().ToString(), 0.95f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,

                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

            //If ores are NOT Rephysicalized
            if (!Config.Instance.RephysicalizedMetalOre)
            {

                Configs.Add(new HEPImpactConfig
                {
                    id = "Gold Amalgam",
                    affectedElements = new HashSet<string> { SimHashes.GoldAmalgam.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Tungsten.CreateTag().ToString(), 0.935f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.065f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
                });


                Configs.Add(new HEPImpactConfig
                {
                    id = "Zinc Ore",
                    affectedElements = new HashSet<string> { SimHashes.ZincOre.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Copper.CreateTag().ToString(), 0.97f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.03f },
                      },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
                Configs.Add(new HEPImpactConfig
                {
                    id = "Copper Ore",
                    affectedElements = new HashSet<string> { SimHashes.Cuprite.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Nickel.CreateTag().ToString(), 0.93f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.07f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
                });

                Configs.Add(new HEPImpactConfig
                {
                    id = "Nickel Ore",
                    affectedElements = new HashSet<string> { SimHashes.NickelOre.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Cobalt.CreateTag().ToString(), 1f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });


                Configs.Add(new HEPImpactConfig
                {
                    id = "Cobalt Ore",
                    affectedElements = new HashSet<string> { SimHashes.Cobaltite.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Iron.CreateTag().ToString(), 0.95f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
            }


            //IF ORES ARE REPHYSICALIZED
            if (Config.Instance.RephysicalizedMetalOre)
            {
                // Gold Amalgam
                Configs.Add(new HEPImpactConfig
                {
                    id = "Gold Amalgam",
                    affectedElements = new HashSet<string> { SimHashes.GoldAmalgam.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Tungsten.CreateTag().ToString(), 0.64f }, { SimHashes.Gold.CreateTag().ToString(), 0.31f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.05f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
                });
                //Zinc Ore

                Configs.Add(new HEPImpactConfig
                {
                    id = "Zinc Ore",
                    affectedElements = new HashSet<string> { SimHashes.ZincOre.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Copper.CreateTag().ToString(), 0.66f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.02f },
                        { SimHashes.Sulfur.CreateTag().ToString(), 0.32f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
                // Copper Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Copper Ore",
                    affectedElements = new HashSet<string> { SimHashes.Cuprite.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Nickel.CreateTag().ToString(), 0.74f }, { SimHashes.SolidOxygen.CreateTag().ToString(), 0.2f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.06f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,

                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactDust, pos, 0f); }
                });

                // Nickel Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Nickel Ore",
                    affectedElements = new HashSet<string> { SimHashes.NickelOre.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Cobalt.CreateTag().ToString(), 0.73f }, { SimHashes.Sulfur.CreateTag().ToString(), 0.27f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });

                // Cobalt Ore
                Configs.Add(new HEPImpactConfig
                {
                    id = "Cobalt Ore",
                    affectedElements = new HashSet<string> { SimHashes.Cobaltite.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.Iron.CreateTag().ToString(), 67f }, { SimHashes.Sulfur.CreateTag().ToString(), 29f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.04f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,

                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });

            }

            Configs.Add(new HEPImpactConfig
            {
                id = "Cholorine",
                affectedElements = new HashSet<string> { SimHashes.SolidChlorine.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Sulfur.CreateTag().ToString(), 0.90f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.1f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });


            Configs.Add(new HEPImpactConfig
            {
                id = "Sulfur",
                affectedElements = new HashSet<string> { SimHashes.Sulfur.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Phosphorus.CreateTag().ToString(), 0.97f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.03f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });

            Configs.Add(new HEPImpactConfig
            {
                id = "Phosphrous",
                affectedElements = new HashSet<string> { SimHashes.Phosphorus.CreateTag().ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { ModElementRegistration.Silicon.Tag.ToString(), 0.90f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.1f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });


            Configs.Add(new HEPImpactConfig
            {
                id = "Silicon",
                affectedElements = new HashSet<string> { ModElementRegistration.Silicon.Tag.ToString() },
                launchNearby = true,
                emittedElements = new Dictionary<string, float> { { SimHashes.Aluminum.CreateTag().ToString(), 0.965f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.035f } },
                emissionMultiplier = 1000f,
                removalMultiplier = 1000f,
                tempDeltaPerPayload = 0.02f,
                sfxCandidates = new[] { "Meteor_Medium_Impact" },
                spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
            });



            if (RonivansIntegration.IsPresent())
            {
                Configs.Add(new HEPImpactConfig
                {
                    id = "Electrum Ronivans",
                    affectedElements = new HashSet<string> { SimHashes.Electrum.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { "SolidSilver", 0.4f }, { SimHashes.Tungsten.CreateTag().ToString(), 0.56f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.04f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
                Configs.Add(new HEPImpactConfig
                {
                    id = "Iron Ronivans",
                    affectedElements = new HashSet<string> { SimHashes.Iron.CreateTag().ToString(), SimHashes.IronOre.CreateTag().ToString(), },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { "AIO_Chromium_Solid", 0.93f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.07f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
                Configs.Add(new HEPImpactConfig
                {
                    id = "Pyrrite Ronivans",
                    affectedElements = new HashSet<string> { SimHashes.FoolsGold.CreateTag().ToString(), },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { "AIO_Chromium_Solid", 0.435f }, { SimHashes.Sulfur.CreateTag().ToString(), 0.49f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.075f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });

                Configs.Add(new HEPImpactConfig
                {
                    id = "Oxygen Ronivans",
                    affectedElements = new HashSet<string> { SimHashes.SolidOxygen.CreateTag().ToString() },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { "SolidNitrogen", 0.875f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.125f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });


                Configs.Add(new HEPImpactConfig
                {
                    id = "Nitrogen Ronivans",
                    affectedElements = new HashSet<string> { "SolidNitrogen" },
                    launchNearby = true,
                    emittedElements = new Dictionary<string, float> { { SimHashes.RefinedCarbon.CreateTag().ToString(), 0.855f }, { SimHashes.SolidNuclearWaste.CreateTag().ToString(), 0.145f } },
                    emissionMultiplier = 1000f,
                    removalMultiplier = 1000f,
                    tempDeltaPerPayload = 0.02f,
                    sfxCandidates = new[] { "Meteor_Medium_Impact" },
                    spawnVfx = pos => { Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactPhosphoric, pos, 0f); }
                });
            }
        }

        public static HEPImpactConfig FindMatch(string elemTag)
        {
            for (int i = 0; i < Configs.Count; i++)
            {
                var cfg = Configs[i];
                if (cfg != null && cfg.MatchesTag(elemTag))
                    return cfg;
            }
            return null;
        }


        public static Dictionary<string, float> GetEmittedElements(string id)
        {
            var cfg = Configs.Find(c => c != null && c.id == id);
            return cfg?.emittedElements ?? new Dictionary<string, float>();
        }
    }
}
