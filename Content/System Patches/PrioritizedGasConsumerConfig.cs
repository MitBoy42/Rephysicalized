using UnityEngine;
using System.Collections.Generic;

namespace Rephysicalized
{
    public class PrioritizedGasConsumerConfig : KMonoBehaviour
    {
        // List of gases to consume, in order of preference
        public List<SimHashes> prioritizedGases = new List<SimHashes>();
        // Consumption rate in kg/s
        public float consumptionRate;
    }
}
