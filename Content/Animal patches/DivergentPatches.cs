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
    // Final patch:
    // - Forces Sulfur diet to output Sand at 0.98 conversion by redirecting BaseDivergentConfig.BasicSulfurDiet call to a wrapper.
    // - Fixes Sucrose diet conversion to 0.966667 by inserting a call after the sucrose Add to adjust the Diet.Info in the list.
    [HarmonyPatch(typeof(DivergentWormConfig), nameof(DivergentWormConfig.CreateWorm))]
    public static class DivergentWormConfig_CreateWorm_DietTranspiler
    {
        // Wrapper: enforces Sand output and 0.98 conversion, preserves other args.
        private static List<Diet.Info> BasicSulfurDiet_ForceSandAndRate(
            Tag _outputElement, float caloriesPerKg, float _producedConversionRate, string diseaseId, float diseasePerKg)
        {
            return BaseDivergentConfig.BasicSulfurDiet(SimHashes.Sand.CreateTag(), caloriesPerKg, 0.98f, diseaseId, diseasePerKg);
        }

        // Adjusts the sucrose diet entry to producedConversionRate = 0.966667 (and ensures produced element is Mud).
        private static void FixSucroseDiet(List<Diet.Info> infos)
        {
            Tag sucrose = SimHashes.Sucrose.CreateTag();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (info.consumedTags.Contains(sucrose))
                {
                    info.producedElement = SimHashes.Mud.CreateTag();
                    info.producedConversionRate = 0.966667f;
                    infos[i] = info;
#if DEBUG
               //     Debug.Log("[DivergentWormDietTranspiler] Sucrose diet fixed to 0.966667.");
#endif
                    break;
                }
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            var il = new List<CodeInstruction>(instructions);

            // Resolve types/methods
            var dietInfoType = AccessTools.Inner(typeof(Diet), "Info");
            var listOfDietInfoType = typeof(List<>).MakeGenericType(dietInfoType);

            // List<Diet.Info>.Add(Diet.Info)
            var listAdd = AccessTools.Method(listOfDietInfoType, "Add", new[] { dietInfoType });

            // BaseDivergentConfig.BasicSulfurDiet(Tag, float, float, string, float)
            var basicSulfurDiet = AccessTools.Method(
                typeof(BaseDivergentConfig),
                "BasicSulfurDiet",
                new[] { typeof(Tag), typeof(float), typeof(float), typeof(string), typeof(float) });

            var basicSulfurDietWrapper = AccessTools.Method(
                typeof(DivergentWormConfig_CreateWorm_DietTranspiler),
                nameof(BasicSulfurDiet_ForceSandAndRate));

            var fixSucrose = AccessTools.Method(
                typeof(DivergentWormConfig_CreateWorm_DietTranspiler),
                nameof(FixSucroseDiet));

            if (basicSulfurDiet == null || basicSulfurDietWrapper == null || listAdd == null || fixSucrose == null)
            {
#if DEBUG
                Debug.Log("[DivergentWormDietTranspiler] Failed to resolve required methods for transpile.");
#endif
                return il;
            }

            // 1) Locate the local variable index for the diet list (result of BasicSulfurDiet) before we change the call.
            int dietListLocal = -1;
            int basicSulfurCallIdx = -1;
            for (int i = 0; i < il.Count; i++)
            {
                if ((il[i].opcode == OpCodes.Call || il[i].opcode == OpCodes.Callvirt) && Equals(il[i].operand, basicSulfurDiet))
                {
                    basicSulfurCallIdx = i;

                    // Next stloc.* within a few instructions holds the List<Diet.Info>
                    for (int j = i + 1; j < Math.Min(i + 8, il.Count); j++)
                    {
                        int? idx = GetStlocIndex(il[j]);
                        if (idx.HasValue)
                        {
                            dietListLocal = idx.Value;
                            break;
                        }
                    }
                    break;
                }
            }



            // 2) Redirect the BasicSulfurDiet call to our wrapper (forces Sand + 0.98f)
            if (basicSulfurCallIdx != -1)
            {
                il[basicSulfurCallIdx].operand = basicSulfurDietWrapper;
#if DEBUG
        //        Debug.Log("[DivergentWormDietTranspiler] Redirected BasicSulfurDiet call to wrapper (Sand + 0.98).");
#endif
            }

            // 3) Insert FixSucroseDiet(list) immediately after the first List<Diet.Info>.Add(...) call
            for (int i = 0; i < il.Count; i++)
            {
                if ((il[i].opcode == OpCodes.Callvirt || il[i].opcode == OpCodes.Call) && Equals(il[i].operand, listAdd))
                {
                    int insertAt = i + 1;
                    il.Insert(insertAt, new CodeInstruction(OpCodes.Ldloc, dietListLocal));
                    il.Insert(insertAt + 1, new CodeInstruction(OpCodes.Call, fixSucrose));
#if DEBUG
               //     Debug.Log("[DivergentWormDietTranspiler] Inserted FixSucroseDiet after List<Diet.Info>.Add.");
#endif
                    break; // Only need to fix once
                }
            }

            return il;
        }

        private static int? GetStlocIndex(CodeInstruction ci)
        {
            if (ci.opcode == OpCodes.Stloc_0) return 0;
            if (ci.opcode == OpCodes.Stloc_1) return 1;
            if (ci.opcode == OpCodes.Stloc_2) return 2;
            if (ci.opcode == OpCodes.Stloc_3) return 3;
            if (ci.opcode == OpCodes.Stloc_S || ci.opcode == OpCodes.Stloc)
            {
                if (ci.operand is LocalBuilder lb) return lb.LocalIndex;
                if (ci.operand is int i) return i;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(DivergentBeetleConfig), nameof(DivergentBeetleConfig.CreateDivergentBeetle))]
    public static class DivergentBeetle_FueledDiet_Patch
    {
        private const float NewKgOreEatenPerCycle = 10f;
        private const float SandPerKgSulfur = 0.95f;
        private const float MinPoopSizeKg = 4f;
        private const float FuelStorageCapacityKg = 13f;
        private const float Co2PullRateKgPerS = 0.04f;
        private const float Co2PerKgSulfur = 1.3f;
        private const float SucrosePerKgSulfur = 1.35f;

        private const float KgOutputPerKgInput = SucrosePerKgSulfur / Co2PerKgSulfur; // ~10.384615

        static void Postfix(GameObject __result)
        {
            try
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
                if (kpid != null)
                    FueledDietRegistry.Register(kpid.PrefabTag, fueledDiet);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] Failed to apply FueledDiet to DivergentBeetle: {e}");
            }
        }
    }
}