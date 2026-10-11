using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using STRINGS;
using Rephysicalized.Content.Animal_patches;
namespace Rephysicalized
{

    // Diet tuning
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.SetupDiet))]
    public static class Belly_Diet_Tuning_Patch
    {
        private const float CaloriesPerCycle = 1777777f;

        public static void Prefix(
            GameObject prefab,
            List<Diet.Info> diet_infos,
            float referenceCaloriesPerKg,
            float minPoopSizeInKg)
        {

            for (int i = 0; i < diet_infos.Count; i++)
            {
                var info = diet_infos[i];

                if (info.consumedTags.Contains("CarrotPlant"))
                {
                    float originalCaloriesPerKg = info.caloriesPerKg > 0f ? info.caloriesPerKg : referenceCaloriesPerKg > 0f ? referenceCaloriesPerKg : CaloriesPerCycle;
                    float originalProducedConversion = info.producedConversionRate;

                    float targetKgPerCycle = 30f;

                    // Re-tune calories per kg to hit the target mass consumption per cycle
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;

                    info.producedConversionRate = 0.9667f;

                    diet_infos[i] = info;
                    continue;
                }

                if (info.consumedTags.Contains("BeanPlant"))
                {
                    float targetKgPerCycle = 30f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0.9667f;
                    diet_infos[i] = info;
                    continue;
                }

                if (info.consumedTags.Contains("BeanPlantSeed") || info.consumedTags.Contains("FriesCarrot"))
                {
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
                if (info.consumedTags.Contains(CarrotConfig.ID))
                {
                    float targetKgPerCycle = 1.778f;
                    info.caloriesPerKg = CaloriesPerCycle / targetKgPerCycle;
                    info.producedConversionRate = 0f;

                    diet_infos[i] = info;
                    continue;
                }
            }
        }
    }


    //Changing the Bammoths mass screws up its heat production, and I can't access the sim to fix it. 
    public sealed class BellyHeatOffset : KMonoBehaviour, ISim4000ms
    {
        [SerializeField] public float PowerWatts = -1274f;

        private const float SpecificHeat_kJ_per_kg_K = 3.47f;

        private const float SlopeWattsPerKg = -325f;

        private PrimaryElement pe;
        private float baselineMass; // not serialized; resets each load/spawn

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            pe = GetComponent<PrimaryElement>();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (pe == null)
                pe = GetComponent<PrimaryElement>();

            baselineMass = (pe != null && pe.Mass > 0f) ? pe.Mass : 4f; // fallback 4 if something odd
        }

        public void Sim4000ms(float dt)
        {
            float m = pe.Mass;
            if (m <= 0f) return;

            float powerScaledWatts = SlopeWattsPerKg * (m - baselineMass);

            float energy_kJ = (powerScaledWatts * dt) / 1000f;
            float dT = energy_kJ / (m * SpecificHeat_kJ_per_kg_K);

            if (dT == 0f || float.IsNaN(dT) || float.IsInfinity(dT)) return;

            float newT = pe.Temperature + dT;
            if (newT > 0f && newT < 10000f)
                pe.Temperature = newT;

        }

        [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
        public static class Patch_BaseBellyConfig_AddNegativeHeat
        {
            public static void Postfix(ref GameObject __result)
            {
 __result.AddOrGet<BellyHeatOffset>();
            }
        }
    }

    // Diet tuning
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
    internal static class GoldBelly_SolidFuelChoreInjection
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
            var inject = AccessTools.Method(typeof(GoldBelly_SolidFuelChoreInjection), nameof(Inject));

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
    // Inject SolidFuelStates.Def into Deer's ChoreTable build chain 
    [HarmonyPatch(typeof(GoldBellyConfig), nameof(GoldBellyConfig.CreateGoldBelly))]
    internal static class GoldBelly_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {

            __result.AddOrGet<KSelectable>();

            var diet = BuildFueledDiet();
            var controller = __result.AddOrGet<FueledDietController>();
            ConfigureFueledDiet(controller, diet);
            controller.RefillThreshold = 250f; 
            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();

            monitor.navigatorSize = new Vector2(2f, 2f);
            monitor.possibleEatPositionOffsets = new[]
            {
                Vector3.zero,
            };

            var crownFuel = __result.AddOrGet<GoldBellyCrownFuelController>();

            var shearable = __result.GetDef<WellFedShearable.Def>();
            shearable.growthDurationCycles = 5f;

            var kpid = __result.GetComponent<KPrefabID>();
         FueledDietRegistry.Register(kpid.PrefabTag, diet); 
              
        }

        private static FueledDiet BuildFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput(SimHashes.Katairite.CreateTag()),

            };

            return new FueledDiet(
                fuelInputs: inputs,
                conversions: null,
                totalFuelCapacityKg: 500f,
                allowBlendedInputByOutput: true 
            );
        }

        private static void ConfigureFueledDiet(FueledDietController controller, FueledDiet diet)
        {
            controller.Configure(diet);
        }
    }
    // GoldBelly: growth of the crown is paid for with fuel; crown mass at 100% is 250kg.
    public sealed class GoldBellyCrownFuelController : ScaleFuelGrowthController
    {
        protected override float FullScaleMassKg => 250f;
    }


    [HarmonyPatch(typeof(ModifierSet), "CreateCritteEffects")]
    public static class GoldBelly_FasterScaleGrowth_Patch
    {
        public static void Postfix(ModifierSet __instance)
        {
            var effect = __instance.effects.Get("GoldBellyWellFed");
            if (effect != null)
            {
               
                effect.Add(new AttributeModifier(Db.Get().Amounts.ScaleGrowth.deltaAttribute.Id, 0.01666667f, "Rephysicalized Addition"));
            }
        }
    }
}