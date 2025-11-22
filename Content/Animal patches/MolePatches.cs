using System;
using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using UnityEngine;
using Rephysicalized.ModElements;

namespace Rephysicalized
{
    // Ensure the base calorie-per-kg for dirt is increased early for MoleDelicacy.
    [HarmonyPatch(typeof(MoleDelicacyConfig))]
    public static class MoleDelicacyConfig_StaticCtor_Patch
    {
        [HarmonyPatch(MethodType.StaticConstructor)]
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var f = AccessTools.Field(typeof(MoleDelicacyConfig), "CALORIES_PER_KG_OF_DIRT");
                f.SetValue(null, 10000f);
            }
            catch { }
        }
    }

    // After MoleDelicacy is created, update its diet to include AshByproduct and use 10000f kcal/kg.
    [HarmonyPatch(typeof(MoleDelicacyConfig), nameof(MoleDelicacyConfig.CreateMole))]
    public static class MoleDelicacyConfig_CreateMole_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            try
            {
                var oreTags = new List<Tag>
                {
                    SimHashes.Regolith.CreateTag(),
                    SimHashes.Dirt.CreateTag(),
                    SimHashes.IronOre.CreateTag(),
                    ModElementRegistration.AshByproduct.Tag
                };

                var dietInfos = BaseMoleConfig.SimpleOreDiet(
                    oreTags,
                    10000f,
                    TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL
                ).ToArray();

                var newDiet = new Diet(dietInfos);

                var calDef = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
                calDef.diet = newDiet;

                var solidConsumerDef = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
                solidConsumerDef.diet = newDiet;
            }
            catch { }
        }
    }

    // Ensure the base calorie-per-kg for dirt is increased early for normal Mole.
    [HarmonyPatch(typeof(MoleConfig))]
    public static class MoleConfig_StaticCtor_Patch
    {
        [HarmonyPatch(MethodType.StaticConstructor)]
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var f = AccessTools.Field(typeof(MoleConfig), "CALORIES_PER_KG_OF_DIRT");
                f.SetValue(null, 10000f);
            }
            catch { }
        }
    }

    // After Mole is created, update its diet to include AshByproduct and use 10000f kcal/kg.
    [HarmonyPatch(typeof(MoleConfig), nameof(MoleConfig.CreateMole))]
    public static class MoleConfig_CreateMole_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            try
            {
                var oreTags = new List<Tag>
                {
                    SimHashes.Regolith.CreateTag(),
                    SimHashes.Dirt.CreateTag(),
                    SimHashes.IronOre.CreateTag(),
                    ModElementRegistration.AshByproduct.Tag
                };

                var dietInfos = BaseMoleConfig.SimpleOreDiet(
                    oreTags,
                    10000f,
                    TUNING.CREATURES.CONVERSION_EFFICIENCY.NORMAL
                ).ToArray();

                var newDiet = new Diet(dietInfos);

                var calDef = __result.AddOrGetDef<CreatureCalorieMonitor.Def>();
                calDef.diet = newDiet;

                var solidConsumerDef = __result.AddOrGetDef<SolidConsumerMonitor.Def>();
                solidConsumerDef.diet = newDiet;
            }
            catch { }
        }

    }
    // Force BaseMole to always use 1 as on_death_drop_count (default drop ID is Meat)
    [HarmonyPatch(typeof(BaseMoleConfig), nameof(BaseMoleConfig.BaseMole))]
    public static class BaseMoleConfig_BaseMole_Prefix
    {
        // Map by argument name; if this ever fails, switch to: [HarmonyArgument(11)]
        static void Prefix([HarmonyArgument("on_death_drop_count")] ref int dropCount)
        {
            dropCount = 1;
        }
    }
}

  