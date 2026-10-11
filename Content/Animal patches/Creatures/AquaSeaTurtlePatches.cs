using HarmonyLib;
using Klei.AI;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches.Aqua_Creatures
{
    internal class AquaSeaTurtlePatches
    {



        [HarmonyPatch(typeof(BaseSeaTurtleConfig), nameof(BaseSeaTurtleConfig.CreatePrefab))]
        internal static class SeaTurtleConfig_CreateSeaTurtle_SolidFuelChoreInjection
        {
            // Helper called by injected IL to add our state to the builder.
            private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
            {
                return builder.Add(new SolidFuelStates.Def());
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            {
                var list = new List<CodeInstruction>(instructions);

                var popInterrupt = AccessTools.Method(typeof(ChoreTable.Builder), nameof(ChoreTable.Builder.PopInterruptGroup));
                var inject = AccessTools.Method(typeof(WoodDeerConfig_CreateWoodDeer_SolidFuelChoreInjection), nameof(Inject));

                if (popInterrupt == null || inject == null)
                    return list;

                // Insert our Inject call immediately before the last PopInterruptGroup() call.
                // This mirrors prior behavior used on other creatures.
                int lastPopIdx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Calls(popInterrupt))
                        lastPopIdx = i;
                }

                if (lastPopIdx >= 0)
                {

                    list.Insert(lastPopIdx, new CodeInstruction(OpCodes.Call, inject));
                }

                return list;
            }
        }
        // Inject SolidFuelStates.Def into SeaTurtle's ChoreTable build chain 
        [HarmonyPatch(typeof(SeaTurtleConfig), nameof(SeaTurtleConfig.CreateSeaTurtle))]
        internal static class SeaTurtle_FueledDiet_Postfix
        {
            [HarmonyPostfix]
            private static void Post(ref GameObject __result)
            {

                __result.AddOrGet<KSelectable>();

                var diet = BuildDeerFueledDiet();
                var controller = __result.AddOrGet<FueledDietController>();
                ConfigureFueledDiet(controller, diet);
                controller.RefillThreshold = 180f;


                var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
                monitor.scanWindowCells = 16;
                monitor.minPickupKg = 0.01f;
                monitor.navigatorSize = new Vector2(2f, 2f);
                monitor.possibleEatPositionOffsets = new[]
                {
                Vector3.zero,
            };

                __result.AddOrGet<SeaTurtleScaleFuelController>();
                var kpid = __result.GetComponent<KPrefabID>();

                FueledDietRegistry.Register(kpid.PrefabTag, diet);
            }

            private static FueledDiet BuildDeerFueledDiet()
            {
                var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Coquina.CreateTag()),
                new FuelInput(SimHashes.Corallium.CreateTag()),
                new FuelInput(SimHashes.Fossil.CreateTag()),
             new FuelInput(SimHashes.Basalt.CreateTag()),
              new FuelInput(SimHashes.Rust.CreateTag()),
              new FuelInput(SimHashes.MaficRock.CreateTag()),

            };


                return new FueledDiet(
                    fuelInputs: inputs,
                    conversions: null,
                    totalFuelCapacityKg: 240f,
                    allowBlendedInputByOutput: true
                );
            }

            private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
            {
                controller.Configure(diet);
            }
        }
        // SeaTurtle: shell growth is paid for with fuel; shell mass at 100% is 250kg.
        public sealed class SeaTurtleScaleFuelController : ScaleFuelGrowthController
        {
            protected override float FullScaleMassKg => 250f;
        }

        [HarmonyPatch(typeof(BaseSeaTurtleConfig), nameof(BaseSeaTurtleConfig.CreatePrefab))]
        private static class BaseSeaTurtleConfig_SeaFairyDietRebalance
        {
            private static void Postfix(ref GameObject __result)
            {
                if (__result == null) return;

                // Replace the diet on both monitors that actually use it.
                var diet = BuildRebalancedSeaTurtleDiet();

                var cal = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
                cal.diet = diet;

                var solid = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
                solid.diet = diet;
            }

            private static Diet BuildRebalancedSeaTurtleDiet()
            {
                // SeaTurtle’s original diet has two entries:
                // 1) SeaFairy -> produced_element ToxicMud, 80000f, 4f (EatPrey)
                // 2) Nori      -> produced_element ToxicMud, 40000f, 2f
                // We rebalance SeaFairy to match entry #2 amounts.

                var seaFairyTags = new HashSet<Tag> { "SeaFairy".ToTag() };
                var noriTags = new HashSet<Tag> { "Nori".ToTag() };

                var infos = new List<Diet.Info>(capacity: 2)
                {
                    new Diet.Info(
                        consumed_tags: seaFairyTags,
                        produced_element: SeaTurtleTuning.POOP_ELEMENT,
                        calories_per_kg: 40000f,
                        produced_conversion_rate: 19/20f,
                        disease_id: null,
                        disease_per_kg_produced: 0f,
                        food_type: Diet.Info.FoodType.EatPrey
                    ),
                    new Diet.Info(
                        consumed_tags: noriTags,
                        produced_element: SeaTurtleTuning.POOP_ELEMENT,
                        calories_per_kg: 40000f,
                        produced_conversion_rate: 19/20f,
                        disease_id: null,
                        disease_per_kg_produced: 0f
                    )
                };

                return new Diet(infos.ToArray());
            }
        }



    }
}

