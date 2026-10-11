

using Newtonsoft.Json;
using PeterHan.PLib.Options;
using System;

namespace Rephysicalized
{
    [RestartRequired]
    [ConfigFile("config.json", false, true)]
    [ModInfo("Rephysicalized", null, false)]
    [Serializable]
    public class Config : SingletonOptions<Config>
    {
        [Option("STRINGS.MODCONFIG.SOlLIDMASSMULT.NAME", "STRINGS.MODCONFIG.SOlLIDMASSMULT.TOOLTIP", null)]
        [JsonProperty]
        [Limit(0.1, 0.5)]
        public float SolidMassMult { get; set; }

        [Option("STRINGS.MODCONFIG.COMETMASSMULT.NAME", "STRINGS.MODCONFIG.COMETMASSMULT.TOOLTIP", null)]
        [JsonProperty]
        [Limit(0.10, 1.0)]
        public float CometMassMult { get; set; }

        [Option("STRINGS.MODCONFIG.WATERGEYSEROUTPUT.NAME", "STRINGS.MODCONFIG.WATERGEYSEROUTPUT.TOOLTIP", null)]
        [JsonProperty]
        [Limit(0.1, 1.0)]
        public float WaterGeyserOutput { get; set; }

        [Option("STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.NAME", "STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.TOOLTIP", null)]
        [JsonProperty]
        [Limit(0.1, 1.0)]
        public float DuplicantOxygenUse { get; set; }
        [Option("STRINGS.MODCONFIG.METALELECTROBANKCOST.NAME", "STRINGS.MODCONFIG.METALELECTROBANKCOST.TOOLTIP", null)]
        [JsonProperty]
        [Limit(0.5, 1)]
        public float MetalElectroBankCost { get; set; }

        [Option("STRINGS.MODCONFIG.BUILDINGHEATEXCHANGE.NAME", "STRINGS.MODCONFIG.BUILDINGHEATEXCHANGE.TOOLTIP", null)]
        [JsonProperty]
        public bool BuildingFoundationTemperatureExchange { get; set; }

        [Option("STRINGS.MODCONFIG.LIGHTSINUTILITY.NAME", "STRINGS.MODCONFIG.LIGHTSINUTILITY.TOOLTIP", null)]
        [JsonProperty]
        public bool LightsInUtility { get; set; }

        [Option("STRINGS.MODCONFIG.STARTWITHZEROSCALES.NAME", "STRINGS.MODCONFIG.STARTWITHZEROSCALES.TOOLTIP", null)]
        [JsonProperty]
        public bool StartWithZeroScale { get; set; }

        [Option("STRINGS.MODCONFIG.CLOGGEDBUILDINGS.NAME", "STRINGS.MODCONFIG.CLOGGEDBUILDINGS.TOOLTIP", null)]
        [JsonProperty]
        public bool CloggedBuildings { get; set; }

        [Option("STRINGS.MODCONFIG.BATTERYWATERDAMAGE.NAME", "STRINGS.MODCONFIG.BATTERYWATERDAMAGE.TOOLTIP", null)]
        [JsonProperty]
        public bool BatteryWaterDamage { get; set; }

        [Option("STRINGS.MODCONFIG.REPHYSICALIZEDMETALORE.NAME", "STRINGS.MODCONFIG.REPHYSICALIZEDMETALORE.TOOLTIP", null)]
        [JsonProperty]
        public bool RephysicalizedMetalOre { get; set; }

        [Option("STRINGS.MODCONFIG.BIONICMETALMETER.NAME", "STRINGS.MODCONFIG.BIONICMETALMETER.TOOLTIP", null)]
        [JsonProperty]
        public bool BionicMetalMeter { get; set; }

        [Option("STRINGS.MODCONFIG.TUNEDUPCRUDCREATION.NAME", "STRINGS.MODCONFIG.TUNEDUPCRUDCREATION.TOOLTIP", null)]
        [JsonProperty]
        public bool TunedUpCrudCreation { get; set; }

        [Option("STRINGS.MODCONFIG.DEBRISDISSOLVING.NAME", "STRINGS.MODCONFIG.DEBRISDISSOLVING.TOOLTIP", null)]
        [JsonProperty]
        public bool DebrisDissolving { get; set; }




        [Option("STRINGS.MODCONFIG.EROSION.NAME", "STRINGS.MODCONFIG.EROSION.TOOLTIP", null)]
        [JsonProperty]
        public bool Erosion { get; set; }





        [Option("STRINGS.MODCONFIG.IRRADIATEDCRITTERS.NAME", "STRINGS.MODCONFIG.IRRADIATEDCRITTERS.TOOLTIP", null)]
        [JsonProperty]
        public bool IrradiatedCritters { get; set; }
        //[Option("STRINGS.MODCONFIG.SILICON.NAME", "STRINGS.MODCONFIG.SILICON.TOOLTIP", null)]
        //[JsonProperty]
        //public bool Silicon { get; set; }

        //[Option("STRINGS.MODCONFIG.WATTAGECAPACITY.NAME", "STRINGS.MODCONFIG.WATTAGECAPACITY.TOOLTIP", null)]
        //[JsonProperty]
        //public bool WattageCapacity { get; set; }


        [Option("STRINGS.MODCONFIG.CHEMISTRIER.SWEETLES.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.SWEETLES.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]

        public bool ChemistrierSweetles { get; set; }

        [Option("STRINGS.MODCONFIG.CHEMISTRIER.SLICKSTERS.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.SLICKSTERS.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]
        public bool ChemistrierSlicksters { get; set; }

        [Option("STRINGS.MODCONFIG.CHEMISTRIER.BONBONTREE.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.BONBONTREE.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]
        public bool ChemistrierBonBonTrees { get; set; }
        [Option("STRINGS.MODCONFIG.CHEMISTRIER.DEWPALM.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.DEWPALM.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]
        public bool ChemistrierDewPalm { get; set; }
        [Option("STRINGS.MODCONFIG.CHEMISTRIER.DINOFERN.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.DINOFERN.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]
        public bool ChemistrierDinofern { get; set; }

        [Option("STRINGS.MODCONFIG.CHEMISTRIER.SNAILIRON.NAME", "STRINGS.MODCONFIG.CHEMISTRIER.SNAILIRON.TOOLTIP", "STRINGS.MODCONFIG.CHEMISTRIER.NAME")]
        [JsonProperty]
        public bool ChemistrierSnailIron { get; set; }

        [Option("STRINGS.MODCONFIG.FISSION.STRENGHT.NAME", "STRINGS.MODCONFIG.FISSION.STRENGHT.TOOLTIP", "STRINGS.MODCONFIG.FISSION.NAME")]
        [JsonProperty]
        [Limit(0.5f, 5f)]
        public float FissionMult { get; set; }

        [Option("STRINGS.MODCONFIG.FISSION.URANIUMENRICHMENT.NAME", "STRINGS.MODCONFIG.FISSION.URANIUMENRICHMENT.TOOLTIP", "STRINGS.MODCONFIG.FISSION.NAME")]
        [JsonProperty]
        [Limit(0.005f, 0.05f)]
        public float EnrichedUraniumMult { get; set; }


        [Option("STRINGS.MODCONFIG.FISSION.GOLDTOIRIDIUM.NAME", "STRINGS.MODCONFIG.FISSION.GOLDTOIRIDIUM.TOOLTIP", "STRINGS.MODCONFIG.FISSION.NAME")]
        [JsonProperty]
        public bool GoldToIridium { get; set; }


        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.MILDTHRESHOLD.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.MILDTHRESHOLD.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(20f, 150f)]
        public float MildThreshold { get; set; }

        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHTHRESHOLD.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHTHRESHOLD.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(100f, 300f)]
        public float HighThreshold { get; set; }

        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.MILDAGING.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.MILDAGING.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(0f, 3f)]
        public float MildAging { get; set; }

        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHAGING.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHAGING.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(0f, 6f)]
        public float HighAging { get; set; }

        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.MILDMETABOLISM.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.MILDMETABOLISM.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(1f, 3f)]
        public float MildMetabolsim { get; set; }

        [Option("STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHMETABOLISM.NAME", "STRINGS.MODCONFIG.RADIATIONRANCHING.HIGHMETABOLISM.TOOLTIP", "STRINGS.MODCONFIG.RADIATIONRANCHING.NAME")]
        [JsonProperty]
        [Limit(1f, 6f)]
        public float HighMetabolism { get; set; }



        public Config()
        {
            this.WaterGeyserOutput = 0.25f;
            this.SolidMassMult = 0.25f;
            this.CometMassMult = 0.5f;
            this.DuplicantOxygenUse = 0.8f;
            this.MetalElectroBankCost = 1f;
            this.BuildingFoundationTemperatureExchange = false;
            this.LightsInUtility = false;
            this.StartWithZeroScale = true;
            this.CloggedBuildings = true;
            this.BatteryWaterDamage = true;
            this.RephysicalizedMetalOre = true;
            this.BionicMetalMeter = true;
            this.TunedUpCrudCreation = true;
            this.DebrisDissolving = true;
            this.Erosion = true;
   
            this.IrradiatedCritters = true;
            this.ChemistrierSweetles = true;
            this.ChemistrierSlicksters = true;
            this.ChemistrierBonBonTrees = true;
            this.ChemistrierDewPalm = true;
            this.ChemistrierDinofern = true;
            this.ChemistrierSnailIron = true;

            this.FissionMult = 1f;
            this.EnrichedUraniumMult = 0.005f;
            this.GoldToIridium = false;

            MildThreshold = 75f;
            HighThreshold = 150f;
            MildAging = 1f;
            HighAging = 3f;
            MildMetabolsim = 2f;
            HighMetabolism = 3f;

            //  this.Silicon = true;
            // this.WattageCapacity = true;
        }
    }
}
