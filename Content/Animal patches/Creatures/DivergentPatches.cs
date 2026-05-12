using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace YourModNamespace.Patches
{


    [HarmonyPatch(typeof(DivergentWormConfig), nameof(DivergentWormConfig.CreateWorm))]
    public static class DivergentWormConfig_CreateWorm_DietPatch
    {
        static void Postfix(GameObject __result)
        {
            var dietInfos = new List<Diet.Info>
            {
                new Diet.Info(
                    new HashSet<Tag> { SimHashes.Sulfur.CreateTag() },
                    SimHashes.Sand.CreateTag(),
                    DivergentWormConfig.CALORIES_PER_KG_OF_ORE,
                    0.98f,
                    null,
                    0f
                ),
                new Diet.Info(
                    new HashSet<Tag> { SimHashes.Sucrose.CreateTag() },
                    SimHashes.Mud.CreateTag(),
                    DivergentWormConfig.CALORIES_PER_KG_OF_SUCROSE,
                    0.966667f,
                    null,
                    0f
                )
            };

            BaseDivergentConfig.SetupDiet(__result, dietInfos, DivergentWormConfig.CALORIES_PER_KG_OF_ORE, DivergentWormConfig.MINI_POOP_SIZE_IN_KG);
        }
    }


    [HarmonyPatch(typeof(DivergentBeetleConfig), nameof(DivergentBeetleConfig.CreateDivergentBeetle))]
    public static class DivergentBeetle_FueledDiet_Patch
    {
        private const float NewKgOreEatenPerCycle = 10f;
        private const float SandPerKgSulfur = 0.95f;
        private const float MinPoopSizeKg = 4f;
        private const float FuelStorageCapacityKg = 20f;
        private const float Co2PullRateKgPerS = 0.05f;
        private const float Co2PerKgSulfur = 1.3f;
        private const float SucrosePerKgSulfur = 1.35f;

        private const float KgOutputPerKgInput = SucrosePerKgSulfur / Co2PerKgSulfur; // ~10.384615

        static void Postfix(GameObject __result)
        {
          
                float caloriesPerKg = DivergentTuning.STANDARD_CALORIES_PER_CYCLE / NewKgOreEatenPerCycle;

                List<Diet.Info> dietInfos = BaseDivergentConfig.BasicSulfurDiet(
                    SimHashes.Sand.CreateTag(),
                    caloriesPerKg,
                    SandPerKgSulfur,
                    null,
                    0f
                );

                BaseDivergentConfig.SetupDiet(__result, dietInfos, caloriesPerKg, MinPoopSizeKg);

                var controller = __result.AddOrGet<FueledDietController>();
            controller.RefillThreshold = FuelStorageCapacityKg * 0.5f; // Does nothing, just for the status item.
            var fueledDiet = new FueledDiet(
                    new[]
                    {
                        new FuelInput(
                            SimHashes.CarbonDioxide.CreateTag(),
                            FuelStorageCapacityKg,
                            Co2PullRateKgPerS,
                            isGas: true,
                            isLiquid: false,
                            consumptionRadius: 2
                        )
                    },
                    new[]
                    {
                        new FuelConversion(
                            SimHashes.CarbonDioxide.CreateTag(),
                            SimHashes.Sucrose.CreateTag(),
                            Co2PerKgSulfur,
                            KgOutputPerKgInput,
                            0f
                        )
                    },
                    totalFuelCapacityKg: FuelStorageCapacityKg
                );

                // Configure the prefab component (for ElementConsumers) and register for spawn-time rebuild
                controller.Configure(fueledDiet);
                var kpid = __result.GetComponent<KPrefabID>();
                    FueledDietRegistry.Register(kpid.PrefabTag, fueledDiet);
            
          
        }
    }
}