using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TUNING;
using Rephysicalized.ModElements;


namespace Rephysicalized.Content.Stuff_Patches
{
    //Industrial 
    [HarmonyPatch(typeof(PlantFiberConfig), nameof(PlantFiberConfig.CreatePrefab))]
    public static class PantFiberPatch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 273.15f + 20f;
            comp.ID = "FabricatedWood";
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.NaturalResin);
            comp.triggeringElements.Add(SimHashes.Resin);
            comp.massConversionRatio = 1f / 0.9f;
            comp.pressureThreshold = 4000f;
            comp.elementConsumedRatio = 0.1f;
            comp.time = 40;

            var degradation = __result.AddComponent<EnviromenmentalPreparation>();

            degradation.temperature = 273.15f + 60f;
            degradation.ID = "Peat";
            degradation.triggeringElements = degradation.triggeringElements ?? new List<SimHashes>();
            degradation.triggeringElements.Clear();

            degradation.triggeringElements.Add(SimHashes.DirtyWater);
            degradation.massConversionRatio = 1 / 0.6f;
            degradation.elementConsumedRatio = 0.4f;
            degradation.pressureThreshold = 200f;
            degradation.time = 600;

            var dissolution = __result.AddComponent<EnviromenmentalPreparation>();

            dissolution.temperature = 273.15f + 20f;
            dissolution.ID = "Dirt";
            dissolution.triggeringElements = dissolution.triggeringElements ?? new List<SimHashes>();
            dissolution.triggeringElements.Clear();

            dissolution.triggeringElements.Add(SimHashes.Water);
            dissolution.massConversionRatio = 1 / 0.9f;
            dissolution.elementConsumedRatio = 0.9f;
            dissolution.pressureThreshold = 200f;
            dissolution.time = 600;
        }
    }
    [HarmonyPatch(typeof(CrabWoodShellConfig), nameof(CrabWoodShellConfig.CreatePrefab))]
    public static class CrabWoodShell_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 0f;
            comp.ID = "WoodLog";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 4000f;
            comp.time = 40;
        }
    }
    [HarmonyPatch(typeof(CrabShellConfig), nameof(CrabShellConfig.CreatePrefab))]
    public static class CrabShell_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 0f;
            comp.ID = "Lime";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 4000f;
            comp.time = 40;
        }

    }
    [HarmonyPatch(typeof(EggShellConfig), nameof(EggShellConfig.CreatePrefab))]
    public static class EggShell_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 0f;
            comp.ID = "Lime";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 2000f;
            comp.time = 40;
        }

    }

    [HarmonyPatch(typeof(GoldBellyCrownConfig), nameof(GoldBellyCrownConfig.CreatePrefab))]
    public static class GoldBellyCrownConfig_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 0f;
            comp.ID = "GoldAmalgam";
            comp.massConversionRatio = 1f;
            comp.pressureThreshold = 4000f;
            comp.time = 40;
        }

    }

    [HarmonyPatch(typeof(DewDripConfig), nameof(DewDripConfig.CreatePrefab))]
    public static class DewDripPatch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 80f + 273.15f;
            comp.ID = "Milk";
            comp.massConversionRatio = 1.0f;
            comp.time = 40;
            var compF = __result.AddComponent<EnviromenmentalPreparation>();
            compF.enableFreezing = true;
            compF.temperature = -40f + 273.15f;
            compF.ID = "MilkIce";
            compF.massConversionRatio = 1.0f;
            compF.time = 40;

        }
    }
    [HarmonyPatch(typeof(KelpConfig), nameof(KelpConfig.CreatePrefab))]
    public static class Kelp_CreatePrefab_Patch
    {
        private static void Postfix(GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();

            comp.temperature = 75f + 273.15f;
            comp.ID = "PhytoOil";
            comp.massConversionRatio = 4f;
            comp.pressureThreshold = 1000f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.Water);
            comp.elementConsumedRatio = 0.75f;
            comp.time = 40;
        }

    }

    //internal class DiamondDebris : IOreConfig
    //{
    //    public SimHashes ElementID => SimHashes.RefinedCarbon;

    //    public GameObject CreatePrefab()
    //    {
    //        GameObject OreEntity = EntityTemplates.CreateSolidOreEntity(this.ElementID);

    //        var comp = OreEntity.AddComponent<EnviromenmentalPreparation>();

    //        comp.temperature = 3500f + 273.15f;
    //        comp.ID = "Diamond";
    //        comp.massConversionRatio = 2f;
    //        comp.pressureThreshold = 4000f;
    //        comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
    //        comp.triggeringElements.Clear();
    //        comp.triggeringElements.Add(SimHashes.MoltenCarbon);
    //        comp.elementConsumedRatio = 0.5f;
    //        comp.time = 80;


    //        return OreEntity;
    //    }
    //}

     

    //Foods

    [HarmonyPatch]
    public static class EnvCookable_Carrot
    {

        private const float CookTemperatureK = 175f + 273.15f;


        [HarmonyPatch(typeof(CarrotConfig), nameof(CarrotConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void CarrotConfig_CreatePrefab_Postfix(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.FRIES_CARROT.Id;

            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.RefinedLipid);
            float friesMul = Rephysicalized.FoodDensityRebalance.IngredientMultiplier["FriesCarrot".ToTag()];
            float carrotMul = Rephysicalized.FoodDensityRebalance.IngredientMultiplier[CarrotConfig.ID.ToTag()];
            comp.elementConsumedRatio = 1.0f / friesMul;
            comp.massConversionRatio = 1f * friesMul / carrotMul;

        }
    }

    [HarmonyPatch(typeof(BeanPlantConfig), nameof(BeanPlantConfig.CreatePrefab))]
    public static class BeanSeedEnvCookablePatch
    {

        private const float CookTemperatureK = 175f + 273.15f;

        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            // Seed ID is defined by BeanPlantConfig.SEED_ID ("BeanPlantSeed")
            GameObject seed = Assets.GetPrefab(BeanPlantConfig.SEED_ID);

            var comp = seed.AddComponent<EnviromenmentalPreparation>();
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
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.FRIED_MUSHROOM.Id;
            comp.massConversionRatio = 1.0f;
        }

        [HarmonyPatch(typeof(BasicPlantFoodConfig), nameof(BasicPlantFoodConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void PickledLice_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.PICKLEDMEAL.Id;
            comp.massConversionRatio = 1f/3f;
        }

        [HarmonyPatch(typeof(PrickleFruitConfig), nameof(PrickleFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void PrickleFruitconfig_CreatePrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.GRILLED_PRICKLEFRUIT.Id;
            comp.massConversionRatio = 1.0f / Rephysicalized.FoodDensityRebalance.IngredientMultiplier[PrickleFruitConfig.ID.ToTag()]; // GRILLED_PRICKLEFRUIT
        }
        [HarmonyPatch(typeof(WormBasicFruitConfig), nameof(WormBasicFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void WormBasicFruitconfig_CreatePrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.WORMBASICFOOD.Id;
            comp.massConversionRatio = 1.0f;
        }
        [HarmonyPatch(typeof(HardSkinBerryConfig), nameof(HardSkinBerryConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void HardSkinBerryconfig_CreatePrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.COOKED_PIKEAPPLE.Id;
            comp.massConversionRatio = 1.0f;
        }
        [HarmonyPatch(typeof(MushBarConfig), nameof(MushBarConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void MushBarConfig_CreatePrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.FRIEDMUSHBAR.Id;
            comp.massConversionRatio = 1f;
        }
        [HarmonyPatch(typeof(SwampFruitConfig), nameof(SwampFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void SwampFruit_CreatePrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = CookTemperatureK;
            comp.ID = FOOD.FOOD_TYPES.SWAMP_DELIGHTS.Id;
            comp.massConversionRatio = 1.0f / Rephysicalized.FoodDensityRebalance.IngredientMultiplier[SwampFruitConfig.ID.ToTag()]; // SWAMP_DELIGHTS
        }
        [HarmonyPatch(typeof(WormSuperFruitConfig), nameof(WormSuperFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void WormSuperFruit_InSugarwatger(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.enableFreezing = true;
            comp.temperature = 273.15f - 80f;
            comp.ID = FOOD.FOOD_TYPES.WORMSUPERFOOD.Id;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.SugarWater);
            comp.massConversionRatio = 1f / 8f;
            comp.elementConsumedRatio = 4f;

        }

        [HarmonyPatch(typeof(WormSuperFruitConfig), nameof(WormSuperFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void WormSuperFruit_Sucrose(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
   
            comp.temperature = 273.15f + 200f;
            comp.ID = FOOD.FOOD_TYPES.WORMSUPERFOOD.Id;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.MoltenSucrose);
            comp.massConversionRatio = 1f / 8f;
            comp.elementConsumedRatio = 4f;

        }
        [HarmonyPatch(typeof(PrickleFruitConfig), nameof(PrickleFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void PrickleFruit_CreatePrefab_Postfix(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.enableFreezing = true;
            comp.temperature = 273.15f - 80f;
            comp.ID = FOOD.FOOD_TYPES.FRUITCAKE.Id;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.SugarWater);
            comp.massConversionRatio = 1f / Rephysicalized.FoodDensityRebalance.IngredientMultiplier[PrickleFruitConfig.ID.ToTag()];
            comp.elementConsumedRatio = 5f;
            comp.pressureThreshold = 2000f;
        }
        [HarmonyPatch(typeof(MeatConfig), nameof(MeatConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void PemmicanMeat_CreatePrefab_Postfix(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.enableFreezing = true;
            comp.temperature = 273.15f ;
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

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
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

            var comp = seed.AddComponent<EnviromenmentalPreparation>();

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
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 65f;
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
            var comp = seed.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 100f;
            comp.ID = FOOD.FOOD_TYPES.COLD_WHEAT_BREAD.Id;
            comp.massConversionRatio = 1f / 3f;
        }
        [HarmonyPatch(typeof(FernFoodConfig), nameof(FernFoodConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void FernFoodConfigPrefab_Postfix(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 100f;
            comp.ID = FOOD.FOOD_TYPES.COLD_WHEAT_BREAD.Id;
            comp.massConversionRatio = 1f / 3f;
        }
        [HarmonyPatch(typeof(ColdWheatBreadConfig), nameof(ColdWheatBreadConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void ColdWheatBreadConfigPrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 110f;
            comp.ID = "AshByproduct";
            comp.massConversionRatio = 1f;
        }
        [HarmonyPatch(typeof(ButterflyPlantConfig), nameof(ButterflyPlantConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void ButterflyPlantConfigPrefab_Postfix(ref GameObject __result)
        {
            GameObject seed = Assets.GetPrefab(ButterflyPlantConfig.SEED_ID);
            var comp = seed.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 100f;
            comp.ID = FOOD.FOOD_TYPES.BUTTERFLYFOOD.Id;
            comp.massConversionRatio = 1f / 3f;
        }

        [HarmonyPatch(typeof(MeatConfig), nameof(MeatConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void MeatConfigPrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 120f;
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
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 130f;
            comp.ID = "AshByproduct";
            comp.massConversionRatio = 1f;
        }
        [HarmonyPatch(typeof(RawEggConfig), nameof(RawEggConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void RawEggConfigPrefab_Postfix(ref GameObject __result)
        {
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 70f;
            comp.ID = FOOD.FOOD_TYPES.COOKED_EGG.Id;
            comp.massConversionRatio = 1f;
        }
        [HarmonyPatch(typeof(FishMeatConfig), nameof(FishMeatConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void FishMeatConfigPrefab_Postfix(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
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

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
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
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 80f;
            comp.ID = "AshByproduct";
            comp.massConversionRatio = 1f;
        }


        //Smoker
        [HarmonyPatch(typeof(FishMeatConfig), nameof(FishMeatConfig.CreatePrefab))]
        [HarmonyPostfix]

        public static void FishMeatSmoker(ref GameObject __result)
        {

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 60f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedFish";
            comp.massConversionRatio = 4f / 6f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }
        [HarmonyPatch(typeof(PrehistoricPacuFilletConfig), nameof(PrehistoricPacuFilletConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void JawboMeatSmoker(ref GameObject __result)
        {
            DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID);

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 60f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedFish";
            comp.massConversionRatio = 4f / 6f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }
    
 [HarmonyPatch(typeof(GardenFoodPlantFoodConfig), nameof(GardenFoodPlantFoodConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void GardenFoodPlantFoodSmoker(ref GameObject __result)
        {
            DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID);

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 70f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedVegetables";
            comp.massConversionRatio = 4f/7f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }
        [HarmonyPatch(typeof(HardSkinBerryConfig), nameof(HardSkinBerryConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void HardSkinBerrySmoker(ref GameObject __result)
        {
            DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID); 

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 70f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedVegetables";
            comp.massConversionRatio = 4f / 7f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }
        [HarmonyPatch(typeof(WormBasicFruitConfig), nameof(WormBasicFruitConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void WormBasicFruitSmoker(ref GameObject __result)
        {
            DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID); 
            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 70f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedVegetables";
            comp.massConversionRatio = 4f / 7f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }

        [HarmonyPatch(typeof(DinosaurMeatConfig), nameof(DinosaurMeatConfig.CreatePrefab))]
        [HarmonyPostfix]
        public static void DinosaurMeatSmoker(ref GameObject __result)
        {
            DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID); 

            var comp = __result.AddComponent<EnviromenmentalPreparation>();
            comp.temperature = 273.15f + 110f;
            comp.triggeringElements = comp.triggeringElements ?? new List<SimHashes>();
            comp.triggeringElements.Clear();
            comp.triggeringElements.Add(SimHashes.CarbonDioxide);
            comp.ID = "SmokedDinosaurMeat";
            comp.massConversionRatio = 3.2f / 6f;
            comp.pressureThreshold = 4f;
            comp.time = 600;

        }
    }
}


