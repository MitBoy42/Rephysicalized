using ElementUtilNamespace;
using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUNING;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized.Content.System_Patches
{
    internal static class CookedEgg_TempCook_Util
    {
        internal static void DisableTemperatureCookable(GameObject go)
        {
            if (go == null) return;

            var tc = go.GetComponent<TemperatureCookable>();
            if (tc != null)
            {
                tc.enabled = false;

                tc.cookedID = null;
            }
        }
        [HarmonyPatch(typeof(CookedEggConfig), nameof(CookedEggConfig.CreatePrefab))]
        public static class CookedEgg_CreatePrefab_Patch
        {
            // Postfix runs after the base game creates the prefab
            public static void Postfix(ref GameObject __result)
            {
                    DisableTemperatureCookable(__result);
             
            }
        }


            [HarmonyPatch(typeof(EntityTemplates))]
        public static class EntityTemplatesFoodElementPatch
        {
            [HarmonyPatch(nameof(EntityTemplates.ExtendEntityToFood), new Type[] { typeof(GameObject), typeof(EdiblesManager.FoodInfo), typeof(bool) })]
            [HarmonyPostfix]
            public static void Postfix(GameObject template)
            {
                try
                {
                    var pe = template.GetComponent<PrimaryElement>();
                    if (pe.ElementID == SimHashes.Creature || pe.ElementID == SimHashes.Vacuum)
                    {
                        pe.ElementID = SimHashes.Dirt;
                    }
                }
                catch (Exception e)
                {
                }
            }
        }


        [HarmonyPatch(typeof(Db), "Initialize")]
        public static class PemmicanFruitcakeSpoilPatch
        {
            private const float SecondsPerCycle = 600f;
            private const float SpoilSeconds = 48f * SecondsPerCycle; 

            public static void Postfix()
            {
                // Pemmican (DLC2)
                var pemmican = FOOD.FOOD_TYPES.PEMMICAN;
                pemmican.CanRot = true;
                pemmican.SpoilTime = SpoilSeconds;

                // Fruitcake (base)
                var fruitcake = FOOD.FOOD_TYPES.FRUITCAKE;
                fruitcake.CanRot = true;
                fruitcake.SpoilTime = SpoilSeconds;
            }
        }

        [HarmonyPatch(typeof(MedicinalPillWorkable), nameof(MedicinalPillWorkable.OnSpawn))]
        public static class MedicinalPillWorkable_OnSpawn_Patch
        {
            public static void Postfix(MedicinalPillWorkable __instance)
            {
                // Override the default SetWorkTime(10f) set by the original OnSpawn
                __instance.SetWorkTime(2f);
            }
        }

        [HarmonyPatch]
        public static class EnvCookable_Carrot
        {

            private const float CookTemperatureK = 177f + 273.15f;


            [HarmonyPatch(typeof(CarrotConfig), nameof(CarrotConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void CarrotConfig_CreatePrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.FRIES_CARROT.Id;

                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.RefinedLipid);
                comp.elementConsumedRatio = 1.0f / Rephysicalized.FoodDensityRebalance.FriesCarrotMultiplier;
               comp.massConversionRatio = 1f * Rephysicalized.FoodDensityRebalance.FriesCarrotMultiplier / Rephysicalized.FoodDensityRebalance.CarrotMultiplier;
              
            }
        }

        [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
        public static class BeanSeedEnvCookablePatch
        {

            private const float CookTemperatureK = 177f + 273.15f;

            [HarmonyPostfix]
            public static void Postfix(ref GameObject __result)
            {
                // Seed ID is defined by BeanPlantConfig.SEED_ID ("BeanPlantSeed")
                GameObject seed = Assets.GetPrefab(BeanPlantConfig.SEED_ID);

                var comp = seed.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.DEEP_FRIED_NOSH.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.RefinedLipid);
                comp.massConversionRatio = 1f / 6f;
                comp.elementConsumedRatio = 1.0f;
            }
        }

        [HarmonyPatch]
        public static class EnvCookable
        {
            private const float CookTemperatureK = 80f + 273.15f;
            [HarmonyPatch(typeof(MushroomConfig), nameof(MushroomConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void Mushroomconfig_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.FRIED_MUSHROOM.Id;
                comp.massConversionRatio = 1.0f;
            }

            [HarmonyPatch(typeof(PrickleFruitConfig), nameof(PrickleFruitConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void PrickleFruitconfig_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.GRILLED_PRICKLEFRUIT.Id;
                comp.massConversionRatio = 1.0f / Rephysicalized.FoodDensityRebalance.PricklefruitMultiplier;
            }
            [HarmonyPatch(typeof(WormBasicFruitConfig), nameof(WormBasicFruitConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void WormBasicFruitconfig_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.WORMBASICFOOD.Id;
                comp.massConversionRatio = 1.0f;
            }
            [HarmonyPatch(typeof(HardSkinBerryConfig), nameof(HardSkinBerryConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void HardSkinBerryconfig_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.COOKED_PIKEAPPLE.Id;
                comp.massConversionRatio = 1.0f;
            }
            [HarmonyPatch(typeof(MushBarConfig), nameof(MushBarConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void MushBarConfig_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.FRIEDMUSHBAR.Id;
                comp.massConversionRatio = 1.0f;
            }
            [HarmonyPatch(typeof(SwampFruitConfig), nameof(SwampFruitConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void SwampFruit_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = CookTemperatureK;
                comp.ID = FOOD.FOOD_TYPES.SWAMP_DELIGHTS.Id;
                comp.massConversionRatio = 1.0f   / Rephysicalized.FoodDensityRebalance.SwampfruitMultiplier;
            }
            [HarmonyPatch(typeof(WormSuperFruitConfig), nameof(WormSuperFruitConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void WormSuperFruit_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.enableFreezing = true;
                comp.temperature = 273.15f - 80f;
                comp.ID = FOOD.FOOD_TYPES.WORMSUPERFOOD.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.SugarWater);
                comp.massConversionRatio = 1f / 8f;
                comp.elementConsumedRatio = 4f;
        
            }
            [HarmonyPatch(typeof(PrickleFruitConfig), nameof(PrickleFruitConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void PrickleFruit_CreatePrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.enableFreezing = true;
                comp.temperature = 273.15f - 80f;
                comp.ID = FOOD.FOOD_TYPES.FRUITCAKE.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.SugarWater);
                comp.massConversionRatio = 1f / Rephysicalized.FoodDensityRebalance.PricklefruitMultiplier;
                comp.elementConsumedRatio = 5f;
                comp.pressureThreshold = 2000f;
            }
            [HarmonyPatch(typeof(MeatConfig), nameof(MeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void PemmicanMeat_CreatePrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.enableFreezing = true;
                comp.temperature = 273.15f - 7f;
                comp.ID = FOOD.FOOD_TYPES.PEMMICAN.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.RefinedLipid);
                comp.massConversionRatio = 1f;
                comp.elementConsumedRatio = 2f;
                comp.pressureThreshold = 2000f;
            }
            [HarmonyPatch(typeof(BasicPlantFoodConfig), nameof(BasicPlantFoodConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void BasicPlantFoodCreatePrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.enableFreezing = true;
                comp.temperature = 273.15f + 100f;
                comp.ID = FOOD.FOOD_TYPES.BASICPLANTBAR.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.Water);
                comp.massConversionRatio = 0.5f;
                comp.elementConsumedRatio = 1f;
                comp.pressureThreshold = 2000f;

            }
            [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void BeanPlantConfigPrefab_Postfix(ref GameObject __result)
            {
                GameObject seed = Assets.GetPrefab(BeanPlantConfig.SEED_ID);

                var comp = seed.AddComponent<EnviromentCookablePatch>();

                comp.enableFreezing = true;
                comp.temperature = 273.15f + 100f;
                comp.ID = FOOD.FOOD_TYPES.TOFU.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.Water);
                comp.massConversionRatio = 1f / 6f;
                comp.elementConsumedRatio = 1f;
                comp.pressureThreshold = 2000f;

            }
            [HarmonyPatch(typeof(RawEggConfig), nameof(RawEggConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void Rawegg_CreatePrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 68f;
                comp.ID = FOOD.FOOD_TYPES.PANCAKES.Id;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.Milk);
                comp.massConversionRatio = 1f;
                comp.elementConsumedRatio = 2.0f;
            }
            [HarmonyPatch(typeof(ColdWheatConfig), nameof(ColdWheatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void ColdWheatConfigPrefab_Postfix(ref GameObject __result)
            {
                GameObject seed = Assets.GetPrefab(ColdWheatConfig.SEED_ID);
                var comp = seed.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 100f;
                comp.ID = FOOD.FOOD_TYPES.COLD_WHEAT_BREAD.Id;
                comp.massConversionRatio = 1f / 3f;
            }
            [HarmonyPatch(typeof(FernFoodConfig), nameof(FernFoodConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void FernFoodConfigPrefab_Postfix(ref GameObject __result)
            {
             
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 100f;
                comp.ID = FOOD.FOOD_TYPES.COLD_WHEAT_BREAD.Id;
                comp.massConversionRatio = 1f / 3f;
            }
            [HarmonyPatch(typeof(ColdWheatBreadConfig), nameof(ColdWheatBreadConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void ColdWheatBreadConfigPrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 110f;
                comp.ID = "AshByproduct";
                comp.massConversionRatio = 1f;
            }
            [HarmonyPatch(typeof(ButterflyPlantConfig), nameof(ButterflyPlantConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void ButterflyPlantConfigPrefab_Postfix(ref GameObject __result)
            {
                GameObject seed = Assets.GetPrefab(ButterflyPlantConfig.SEED_ID);
                var comp = seed.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 100f;
                comp.ID = FOOD.FOOD_TYPES.BUTTERFLYFOOD.Id;
                comp.massConversionRatio = 1f / 3f;
            }
           
            [HarmonyPatch(typeof(MeatConfig), nameof(MeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void MeatConfigPrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 150f;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.CarbonDioxide);
                comp.ID = FOOD.FOOD_TYPES.COOKED_MEAT.Id;
                comp.massConversionRatio = 1f;
            }
            [HarmonyPatch(typeof(CookedMeatConfig), nameof(CookedMeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void CookedMeatConfigPrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 155f;
                comp.ID = "Dirt";
                comp.massConversionRatio = 1f;
            }
            [HarmonyPatch(typeof(RawEggConfig), nameof(RawEggConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void RawEggConfigPrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 71f;
                comp.ID = FOOD.FOOD_TYPES.COOKED_EGG.Id;
                comp.massConversionRatio = 1f;
            }
            [HarmonyPatch(typeof(FishMeatConfig), nameof(FishMeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void FishMeatConfigPrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 70f;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.CarbonDioxide);
                comp.ID = FOOD.FOOD_TYPES.COOKED_FISH.Id;
                comp.massConversionRatio = 1f;
            }
            [HarmonyPatch(typeof(ShellfishMeatConfig), nameof(ShellfishMeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void ShellfishMeatConfigPrefab_Postfix(ref GameObject __result)
            {

                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 70f;
                comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
                comp.triggeringElements.Clear();
                comp.triggeringElements.Add(SimHashes.CarbonDioxide);
                comp.ID = FOOD.FOOD_TYPES.COOKED_FISH.Id;
                comp.massConversionRatio = 1f;
            }

            [HarmonyPatch(typeof(CookedFishConfig), nameof(CookedMeatConfig.CreatePrefab))]
            [HarmonyPostfix]
            public static void CookedFishConfigPrefab_Postfix(ref GameObject __result)
            {
                var comp = __result.AddComponent<EnviromentCookablePatch>();
                comp.temperature = 273.15f + 77f;
                comp.ID = "AshByproduct";
                comp.massConversionRatio = 1f;
            }

        }
    }
}
