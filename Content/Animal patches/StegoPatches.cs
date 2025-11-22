using HarmonyLib;
using Klei.AI;
using Rephysicalized.Chores; // SolidFuelStates/SolidFuelMonitor live here
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    // Inject SolidFuelStates.Def into Stego's ChoreTable build chain, before the last PopInterruptGroup.
    [HarmonyPatch(typeof(BaseStegoConfig), nameof(BaseStegoConfig.BaseStego))]
    internal static class BaseStegoConfig_BaseStego_SolidFuelChoreInjection
    {
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SolidFuelStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            var pop = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.PopInterruptGroup));
            var inject = AccessTools.Method(typeof(BaseStegoConfig_BaseStego_SolidFuelChoreInjection), nameof(Inject));

            if (pop != null && inject != null)
            {
                int lastPopIdx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Calls(pop))
                        lastPopIdx = i;
                }

                if (lastPopIdx >= 0)
                    list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
            }
            return list;
        }
    }

    // Post-configure Stego prefab: attach FueledDiet, add SolidFuelMonitor.
    // Split into two variants to support Stego (Peat) and AlgaeStego (Algae).
    [HarmonyPatch(typeof(BaseStegoConfig), nameof(BaseStegoConfig.BaseStego))]
    internal static class Stego_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
            if (__result == null) return;

            __result.AddOrGet<KSelectable>();

            // Keep vanilla edible diet
            var cal = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
            if (cal != null && cal.minConsumedCaloriesBeforePooping < 0f)
                cal.minConsumedCaloriesBeforePooping = 0f;

            // Ensure SolidConsumer diet is not overridden here
            var solidConsumer = __result.GetDef<SolidConsumerMonitor.Def>();
            if (solidConsumer != null)
                solidConsumer.diet = null;

            // Decide which FueledDiet to attach based on prefab id/tag
            var kpid = __result.GetComponent<KPrefabID>();
            Tag prefabTag = kpid != null ? kpid.PrefabTag : Tag.Invalid;

            // Default to Stego diet (Peat) if unrecognized to match prior behavior
            bool isAlgaeStego = prefabTag == TagManager.Create("AlgaeStego");

            FueledDiet diet = isAlgaeStego
                ? BuildAlgaeStegoFueledDietAlgae()
                : BuildStegoFueledDietPeat();

            var controller = __result.AddOrGet<FueledDietController>();
            controller.RefillThreshold = 300f;
            ConfigureFueledDiet(controller, diet);

            // Solid fuel monitor drives the SOLIDFUEL chore/state
            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
            monitor.navigatorSize = new Vector2(2f, 2f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            
            };

            // Optional registry for reload/rebuild
            if (kpid != null)
            {
                FueledDietRegistry.Register(kpid.PrefabTag, diet);
            }
        }

        // Stego-specific fueled diet outputting Peat
        private static FueledDiet BuildStegoFueledDietPeat()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Shale.CreateTag()),
                new FuelInput(SimHashes.SandStone.CreateTag()),
                new FuelInput(SimHashes.Clay.CreateTag()),
                new FuelInput(SimHashes.SedimentaryRock.CreateTag()),
                new FuelInput(SimHashes.Mud.CreateTag()),
            };

            const float baseKgInputPerKgMain = 50f; // Vinefruit baseline
            const float kgOutputPerKgInput = 1.0f;
            var output = SimHashes.Peat.CreateTag();

            var conversions = new List<FuelConversion>(inputs.Count);
            foreach (var fi in inputs)
            {
                conversions.Add(new FuelConversion(
                    inputTag: fi.ElementTag,
                    outputTag: output,
                    kgInputPerKgMain: baseKgInputPerKgMain,
                    kgOutputPerKgInput: kgOutputPerKgInput,
                    outputTemperatureOverrideKelvin: 0f));
            }

            var mainFoodMultipliers = new Dictionary<Tag, float>();
            var mainFoodOverrides = new Dictionary<Tag, float>
            {
                { TagManager.Create(VineFruitConfig.ID), 50f },
                { TagManager.Create(PrickleFruitConfig.ID), 246f/5f },
                { TagManager.Create(SwampFruitConfig.ID), 283f * 1.5f },
            };

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: conversions,
                totalFuelCapacityKg: 600f,
                allowBlendedInputByOutput: true,
                mainFoodMultipliers: mainFoodMultipliers,
                mainFoodKgInputPerKgMainOverrides: mainFoodOverrides
            );
        }

        // AlgaeStego-specific fueled diet outputting Algae (instead of Peat)
        private static FueledDiet BuildAlgaeStegoFueledDietAlgae()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Shale.CreateTag()),
                new FuelInput(SimHashes.SandStone.CreateTag()),
                new FuelInput(SimHashes.RefinedCarbon.CreateTag()),
                new FuelInput(SimHashes.SedimentaryRock.CreateTag()),
                new FuelInput(SimHashes.Mud.CreateTag()),
            };

            const float baseKgInputPerKgMain = 33f; // Keep same baseline
            const float kgOutputPerKgInput = 1.0f;
            var output = SimHashes.Algae.CreateTag();

            var conversions = new List<FuelConversion>(inputs.Count);
            foreach (var fi in inputs)
            {
                conversions.Add(new FuelConversion(
                    inputTag: fi.ElementTag,
                    outputTag: output,
                    kgInputPerKgMain: baseKgInputPerKgMain,
                    kgOutputPerKgInput: kgOutputPerKgInput,
                    outputTemperatureOverrideKelvin: 0f));
            }

            // Keep the same main food tuning unless specified otherwise
            var mainFoodMultipliers = new Dictionary<Tag, float>();
            var mainFoodOverrides = new Dictionary<Tag, float>
            {
                { TagManager.Create(VineFruitConfig.ID), 33f },
                { TagManager.Create(PrickleFruitConfig.ID), 246f/5f * 0.66f },
                { TagManager.Create(SwampFruitConfig.ID), 283f * 1.5f * 0.66f },
            };

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: conversions,
                totalFuelCapacityKg: 600f,
                allowBlendedInputByOutput: true,
                mainFoodMultipliers: mainFoodMultipliers,
                mainFoodKgInputPerKgMainOverrides: mainFoodOverrides
            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
            try { controller.Configure(diet); } catch { }
        }
    }

    // Reliable disable of vanilla poop conversion for Stego by clearing consumed calories just before Poop.
    // Scope: Only Stego prefabs (matches any PrefabID containing 'Stego', case-insensitive).
    [HarmonyPatch(typeof(CreatureCalorieMonitor.Instance), nameof(CreatureCalorieMonitor.Instance.Poop))]
    internal static class Stego_DisableVanillaPoop_Prefix
    {
        [HarmonyPrefix]
        private static void Prefix(CreatureCalorieMonitor.Instance __instance)
        {
            var go = __instance?.gameObject;
            var kpid = go.GetComponent<KPrefabID>();

            string pid = kpid.PrefabID().ToString();
            if (string.IsNullOrEmpty(pid) || pid.IndexOf("Stego", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            var stomach = __instance.stomach;
            var diet = stomach?.diet;
            if (diet?.infos == null) return;

            // Union of all consumed tags in the active diet
            var tags = new HashSet<Tag>();
            for (int i = 0; i < diet.infos.Length; i++)
            {
                var info = diet.infos[i];
                if (info?.consumedTags == null) continue;
                foreach (var t in info.consumedTags)
                    if (t.IsValid) tags.Add(t);
            }
            // Subtract a large amount per tag to zero the stomach’s consumed-calorie counters
            // (vanilla uses these to compute poop mass). Using stomach.Consume aligns with vanilla logic.
            foreach (var t in tags)
                stomach.Consume(t, -1e12f);
        }
    }

    // Changes the onDeathDropCount: 12f inside BaseStego(...) to 4f
    [HarmonyPatch(typeof(BaseStegoConfig), nameof(BaseStegoConfig.BaseStego))]
    public static class BaseStegoMeatDropTranspiler
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - 12f) < 0.0001f)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 4f);
                    continue;
                }
                yield return instr;
            }
        }
    }
}