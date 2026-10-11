using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.Plant_patches.AquaPlants
{
    internal static class AquaDewPalmPatches
    {
  

        [HarmonyPatch(typeof(DewPalmConfig), nameof(DewPalmConfig.CreatePrefab))]
        private static class DewPalmConfig_CreatePrefab_Patch
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result == null)
                    return;

                EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Sulfur.CreateTag(),
                        massConsumptionRate = 0.1f
                    }
                });

                if (!Config.Instance.ChemistrierDewPalm)
                    return;

                        EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Sulfur.CreateTag(),
                        massConsumptionRate = 0.8f
                    }
                });

                ExtendPlantToGasConsumer.Extend(
                    __result,
                    new PlantGasConsumerInfo(SimHashes.CarbonDioxide.CreateTag(), 0.02f)
                    {
                               consumerSampleOffset = new Vector3(0f, 2f, 0f),
                        consumptionRadius = 4,
                        showStorageInUI = true,
         
                    });
            }
        }
    }
}
