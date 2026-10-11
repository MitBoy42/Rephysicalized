using HarmonyLib;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches.Aqua_Creatures
{

    // Postfix: scale HatchVeggie conversion efficiency without referencing TUNING.
    [HarmonyPatch(typeof(BaseSeaHorseConfig), nameof(BaseSeaHorseConfig.CreatePrefab))]
    public static class Seahorse_AdjustConversionEfficiency_Postfix
    {
        private static bool _applied;

        [HarmonyPostfix]
        public static void Postfix(GameObject __result)
        {
            if (_applied || __result == null) return;


            const float factor = 1f/12f*1.5625f;

            var calDef = __result.GetDef<CreatureCalorieMonitor.Def>();
            var solidDef = __result.GetDef<SolidConsumerMonitor.Def>();

            if (calDef?.diet?.infos == null || calDef.diet.infos.Length == 0)
                return;

            var oldInfos = calDef.diet.infos;
            var newInfos = new List<Diet.Info>(oldInfos.Length);

            foreach (var info in oldInfos)
            {
                // Clone consumed tags so we don't mutate shared sets
                var consumed = new HashSet<Tag>(info.consumedTags);

                // Rebuild Diet.Info with producedConversionRate scaled by factor
                var newInfo = new Diet.Info(
                    consumed,
                    info.producedElement,
                    info.caloriesPerKg,
                    info.producedConversionRate * factor,
                    disease_per_kg_produced: info.diseasePerKgProduced,
                    produce_solid_tile: info.produceSolidTile,
                    food_type: info.foodType,
                    emmit_disease_on_cell: info.emmitDiseaseOnCell,
                    eat_anims: info.eatAnims
                );

                newInfos.Add(newInfo);
            }

            var newDiet = new Diet(newInfos.ToArray());
            calDef.diet = newDiet;

            if (solidDef != null)
                solidDef.diet = newDiet;

            if (calDef.minConsumedCaloriesBeforePooping > 0f)
                calDef.minConsumedCaloriesBeforePooping /= factor;

            _applied = true;

        }
    }

    internal static class SeaHorseMilkMassGate
    {
        private const float MILK_MASS_TO_DELETE_KG = 100f;
        private const float REFILL_THRESHOLD_KG = 100f;

        public const float MilkMassToDeleteKg = MILK_MASS_TO_DELETE_KG;
        public const float RefillThresholdKg = REFILL_THRESHOLD_KG;

        public static bool HasEnoughStorageMass(GameObject go)
        {
            if (go == null) return false;
            var storage = go.GetComponent<Storage>();
            if (storage == null) return false;

            // Storage.mass = total mass of all stored PrimaryElements (kg)
            float mass = 0f;
            if (storage.items != null)
            {
                for (int i = storage.items.Count - 1; i >= 0; i--)
                {
                    var itemGo = storage.items[i];
                    if (itemGo == null) continue;
                    var pe = itemGo.GetComponent<PrimaryElement>();
                    if (pe != null) mass += pe.Mass;
                }
            }

            return mass >= REFILL_THRESHOLD_KG;
        }

        public static void DrainSeahorseStorageMass(Storage storage, float amountKg)
        {
            if (storage == null) return;
            if (amountKg <= 0f) return;
            if (storage.items == null) return;

            float remaining = amountKg;

            for (int i = storage.items.Count - 1; i >= 0 && remaining > 0f; i--)
            {
                var itemGo = storage.items[i];
                if (itemGo == null) continue;

                var pe = itemGo.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f) continue;

                float take = Mathf.Min(remaining, pe.Mass);
                if (take <= 0f) continue;

                pe.Mass -= take;
                remaining -= take;

                if (pe.Mass <= 0.0001f)
                    Util.KDestroyGameObject(itemGo);
            }
        }
    }

    [HarmonyPatch(typeof(BaseSeaHorseConfig), nameof(BaseSeaHorseConfig.CreatePrefab))]
    internal static class SeaHorseConfig_CreateSeaHorse_SolidFuelChoreInjection

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
            var inject = AccessTools.Method(typeof(SeaHorseConfig_CreateSeaHorse_SolidFuelChoreInjection), nameof(Inject));

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
    // Inject SolidFuelStates.Def into ChoreTable build chain 
    [HarmonyPatch(typeof(SeaHorseConfig), nameof(SeaHorseConfig.CreateSeaHorse))]
    internal static class SeaHorse_FertilityMilkMass_Gating
    {


        [HarmonyPatch(typeof(FertilityShearable.Instance), nameof(FertilityShearable.Instance.IsReadyToBeMilked))]
        [HarmonyPrefix]
        private static bool Prefix_IsReadyToBeMilked(FertilityShearable.Instance __instance, ref bool __result)
        {
            // Let vanilla fertility + happiness + wildness + elderly gates run first (Harmony prefix can't call original).
            // We instead only further restrict readiness.
            // If vanilla already rejected, __result is false.
            // If vanilla might allow, we re-check storage.

            // If storage doesn't have enough mass, it can't be milked.
            return SeaHorseMilkMassGate.HasEnoughStorageMass(__instance.gameObject);
        }

        [HarmonyPatch(typeof(FertilityShearable.Instance), nameof(FertilityShearable.Instance.MilkingComplete))]
        [HarmonyPostfix]
        private static void Post_MilkingComplete(FertilityShearable.Instance __instance, Storage storage)
        {
            // Only delete from the creature's own storage (the passed storage is the milking target).
            var creatureStorage = __instance.gameObject.GetComponent<Storage>();
            if (creatureStorage == null) return;

SeaHorseMilkMassGate.DrainSeahorseStorageMass(creatureStorage, SeaHorseMilkMassGate.MilkMassToDeleteKg);
        }
    }

    [HarmonyPatch(typeof(SeaHorseConfig), nameof(SeaHorseConfig.CreateSeaHorse))]
    internal static class SeaHorse_FueledDiet_Postfix
    {

        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {

            __result.AddOrGet<KSelectable>();


            var diet = BuildSeaHorseFueledDiet();
            var controller = __result.AddOrGet<FueledDietController>();
            ConfigureFueledDiet(controller, diet);
            controller.RefillThreshold = 100f; // adjust as desired (kg)


            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();

            monitor.navigatorSize = new Vector2(2f, 1f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };


            var kpid = __result.GetComponent<KPrefabID>();

            FueledDietRegistry.Register(kpid.PrefabTag, diet);
        }


        private static FueledDiet BuildSeaHorseFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Mud.CreateTag()),
                new FuelInput(SimHashes.ToxicSand.CreateTag()),
                new FuelInput(SimHashes.ToxicMud.CreateTag()),
                new FuelInput(SimHashes.Dirt.CreateTag()),
                new FuelInput(SimHashes.Gunk.CreateTag()),
                new FuelInput(SimHashes.Peat.CreateTag()),
                new FuelInput(SimHashes.Sucrose.CreateTag()),
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
}
