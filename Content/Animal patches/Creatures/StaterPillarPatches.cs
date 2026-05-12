using HarmonyLib;
using Klei.AI;
using Rephysicalized;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.Animal_patches
{


    // Sets KG_ORE_EATEN_PER_CYCLE to 20f and recalculates CALORIES_PER_KG_OF_ORE
    // right before the prefab is created.
    [HarmonyPatch(typeof(StaterpillarConfig), nameof(StaterpillarConfig.CreateStaterpillar))]
    public static class StaterpillarConfig_CreateStaterpillar_Prefix_AdjustTuning
    {
        static void Prefix()
        {
            var kgField = AccessTools.Field(typeof(StaterpillarConfig), "KG_ORE_EATEN_PER_CYCLE");
            var caloriesField = AccessTools.Field(typeof(StaterpillarConfig), "CALORIES_PER_KG_OF_ORE");
            var stdCalsField = AccessTools.Field(typeof(StaterpillarTuning), "STANDARD_CALORIES_PER_CYCLE");

            // Read STANDARD_CALORIES_PER_CYCLE (const or static readonly)
            float stdCals;
            if (stdCalsField.IsLiteral)
                stdCals = (float)stdCalsField.GetRawConstantValue();
            else
                stdCals = (float)stdCalsField.GetValue(null);

            // Apply desired tuning
            kgField.SetValue(null, 20f);
            caloriesField.SetValue(null, stdCals / 20f);
        }
    }



        [HarmonyPatch(typeof(Db), "Initialize")]
        public static class Db_Initialize_StaterpillarTuning_Force015
        {
            [HarmonyPostfix]
            public static void Postfix()
            { 
                        StaterpillarTuning.POOP_CONVERSTION_RATE = 0.15f;
                
            }

    }

    // Postfix patches to replace diet for Gas and Liquid staterpillars and restore poop conversion rate
    [HarmonyPatch(typeof(StaterpillarGasConfig), nameof(StaterpillarGasConfig.CreateStaterpillarGas))]
    public static class StaterpillarGasConfig_CreateStaterpillar_Postfix_AdjustDiet
    {
        static void Postfix(GameObject __result)
        {
            var kgField = AccessTools.Field(typeof(StaterpillarGasConfig), "KG_ORE_EATEN_PER_CYCLE");
            var caloriesField = AccessTools.Field(typeof(StaterpillarGasConfig), "CALORIES_PER_KG_OF_ORE");
                var stdCalsField = AccessTools.Field(typeof(StaterpillarTuning), "STANDARD_CALORIES_PER_CYCLE");



            // Read STANDARD_CALORIES_PER_CYCLE (const or static readonly)
            float stdCals;
                if (stdCalsField.IsLiteral)
                    stdCals = (float)stdCalsField.GetRawConstantValue();
                else
                    stdCals = (float)stdCalsField.GetValue(null);

                caloriesField.SetValue(null, stdCals / 20f);

            // Apply desired tuning
            kgField.SetValue(null, 20f);
            caloriesField.SetValue(null, stdCals / 20f);

            float caloriesPerKg = caloriesField != null ? (float)caloriesField.GetValue(null) : (StaterpillarTuning.STANDARD_CALORIES_PER_CYCLE / 20f);

            var dietInfos = new List<Diet.Info>();
         
                var poopTag = ModElementRegistration.CrudByproduct.CreateTag();
                var eatSet = new HashSet<Tag> { SimHashes.Sulfur.CreateTag() };
                dietInfos.Add(new Diet.Info(eatSet, poopTag, caloriesPerKg, 0.95f, null, 0f));
                BaseStaterpillarConfig.SetupDiet(__result, dietInfos);
        }
    }

    [HarmonyPatch(typeof(StaterpillarLiquidConfig), nameof(StaterpillarLiquidConfig.CreateStaterpillarLiquid))]
    public static class StaterpillarLiquidConfig_CreateStaterpillar_Postfix_AdjustDiet
    {
        static void Postfix(GameObject __result)
        {
            var kgField = AccessTools.Field(typeof(StaterpillarLiquidConfig), "KG_ORE_EATEN_PER_CYCLE");
            var caloriesField = AccessTools.Field(typeof(StaterpillarLiquidConfig), "CALORIES_PER_KG_OF_ORE");
            var stdCalsField = AccessTools.Field(typeof(StaterpillarTuning), "STANDARD_CALORIES_PER_CYCLE");


            // Read STANDARD_CALORIES_PER_CYCLE (const or static readonly)
            float stdCals;
            if (stdCalsField.IsLiteral)
                stdCals = (float)stdCalsField.GetRawConstantValue();
            else
                stdCals = (float)stdCalsField.GetValue(null);

            // Apply desired tuning
            kgField.SetValue(null, 20f);
            caloriesField.SetValue(null, stdCals / 20f);

                float caloriesPerKg = caloriesField != null ? (float)caloriesField.GetValue(null) : (StaterpillarTuning.STANDARD_CALORIES_PER_CYCLE / 20f);

                // Build a simple diet that consumes Sulfur and produces CrudByproduct at 0.95 conversion
                var dietInfos = new List<Diet.Info>();
         
                var poopTag = ModElementRegistration.CrudByproduct.CreateTag();
            var eatSet = new HashSet<Tag> { SimHashes.Sulfur.CreateTag() };
            dietInfos.Add(new Diet.Info(eatSet, poopTag, caloriesPerKg, 0.95f, null, 0f));

                BaseStaterpillarConfig.SetupDiet(__result, dietInfos);

            }
           
        }
    }





