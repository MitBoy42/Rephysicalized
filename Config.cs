using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace Rephysicalized
{
    [Serializable]
    [RestartRequired]
    [ConfigFile(SharedConfigLocation: true)]
    [ModInfo("Rephysicalized")]
    public class Config : SingletonOptions<Config>
    {
        [Option("STRINGS.MODCONFIG.SOlLIDMASSMULT.NAME", "STRINGS.MODCONFIG.SOlLIDMASSMULT.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 0.5f)]
        public float SolidMassMult { get; set; }

        [Option("STRINGS.MODCONFIG.COMETMASSMULT.NAME", "STRINGS.MODCONFIG.COMETMASSMULT.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 1f)]
        public float CometMassMult { get; set; }

        [Option("STRINGS.MODCONFIG.WATERGEYSEROUTPUT.NAME", "STRINGS.MODCONFIG.WATERGEYSEROUTPUT.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 1f)]
        public float WaterGeyserOutput { get; set; }


        //[Option("STRINGS.MODCONFIG.METALVOCANOOUTPUT.NAME", "STRINGS.MODCONFIG.METALVOCANOOUTPUT.TOOLTIP")]
        //[JsonProperty]
        //[Limit(0.1f, 1f)]
        //public float MetalVolcanoOutput { get; set; }

        [Option("STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.NAME", "STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 1f)]
        public float DuplicantOxygenUse { get; set; }


        [Option("STRINGS.MODCONFIG.BUILDINGHEATEXCHANGE.NAME", "STRINGS.MODCONFIG.BUILDINGHEATEXCHANGE.TOOLTIP")]
        [JsonProperty]
        public bool BuildingFoundationTemperatureExchange { get; set; }

        [Option("STRINGS.MODCONFIG.LIGHTSINUTILITY.NAME", "STRINGS.MODCONFIG.LIGHTSINUTILITY.TOOLTIP")]
        [JsonProperty]
        public bool LightsInUtility { get; set; }

        [Option("STRINGS.MODCONFIG.STARTWITHZEROSCALES.NAME", "STRINGS.MODCONFIG.STARTWITHZEROSCALES.TOOLTIP")]
        [JsonProperty]
        public bool StartWithZeroScale { get; set; }

        [Option("STRINGS.MODCONFIG.CLOGGEDBUILDINGS.NAME", "STRINGS.MODCONFIG.CLOGGEDBUILDINGS.TOOLTIP")]
        [JsonProperty]
        public bool CloggedBuildings { get; set; }

        [Option("STRINGS.MODCONFIG.BATTERYWATERDAMAGE.NAME", "STRINGS.MODCONFIG.BATTERYWATERDAMAGE.TOOLTIP")]
        [JsonProperty]
        public bool BatteryWaterDamage { get; set; }

        [Option("STRINGS.MODCONFIG.REPHYSICALIZEDMETALORE.NAME", "STRINGS.MODCONFIG.REPHYSICALIZEDMETALORE.TOOLTIP")]
        [JsonProperty]
        public bool RephysicalizedMetalOre { get; set; }

        [Option("STRINGS.MODCONFIG.BIONICMETALMETER.NAME", "STRINGS.MODCONFIG.BIONICMETALMETER.TOOLTIP")]
        [JsonProperty]
        public bool BionicMetalMeter { get; set; }


        [Option("STRINGS.MODCONFIG.TUNEDUPCRUDCREATION.NAME", "STRINGS.MODCONFIG.TUNEDUPCRUDCREATION.TOOLTIP")]
        [JsonProperty]
        public bool TunedUpCrudCreation { get; set; }




        public Config()
        {
            WaterGeyserOutput = 0.25f;
            //MetalVolcanoOutput = 0.5f;
            SolidMassMult = 0.25f;
            CometMassMult = 0.5f;
            DuplicantOxygenUse = 0.5f;
            BuildingFoundationTemperatureExchange = false;
            LightsInUtility = false;
            StartWithZeroScale = true;
            CloggedBuildings = true;
            BatteryWaterDamage = true;
            RephysicalizedMetalOre = true;
            BionicMetalMeter = true;
            TunedUpCrudCreation = true;

        }
    }
}
