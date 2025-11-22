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

        [Option("STRINGS.MODCONFIG.WATERGEYSEROUTPUT.NAME", "STRINGS.MODCONFIG.WATERGEYSEROUTPUT.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 1f)]
        public float WaterGeyserOutput { get; set; }

        [Option("STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.NAME", "STRINGS.MODCONFIG.DUPLICANTOXYGENUSE.TOOLTIP")]
        [JsonProperty]
        [Limit(0.1f, 1f)]
        public float DuplicantOxygenUse { get; set; }

        public Config()
        {
            WaterGeyserOutput = 0.25f;
            SolidMassMult = 0.25f;
            DuplicantOxygenUse = 0.5f;
        }
    }
}
