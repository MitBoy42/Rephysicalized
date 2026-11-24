//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Reflection.Emit;
//using UnityEngine;
//using Rephysicalized.Content.Plant_patches;
//using HarmonyLib;

//namespace Rephysicalized
//{
//    // Centralized hook class: adds PlantGasConsumer (Oxidizer gas consumer) to specified plants.
//    // Consumption: 0.002 kg/s, Storage: 1.2 kg for most plants.
//    // Vinemother & WoodTree: 0.006 kg/s, 2.4 kg storage.

//    internal class PlantGasConsumerHook
//    {
//        public enum PlantGasConsumerPlants
//        {
//            SaltySticksPlant,
//            SuperPlant,
//            WolPlant,
//            SwampHarvestPlant,
//            BasicSingleHarvestPlant,
//            CarrotPlant,
//            HardSkinBerryPlant,
//            ColdWheat,
//            VineMother,
//            WoodTree,
//            GardenFoodPlant,
//            ButterflyPlant,
//            FlyTrapPlant
//        }

//        // Applies ExtendPlantToGasConsumer with given rates to a plant prefab.
//        private static void AddGasConsumer(GameObject plant, float consumptionRate, float storageCapacity)
//        {
//            var info = new PlantGasConsumerInfo(ModTags.OxidizerGas, consumptionRate)
//            {
//                elementTag = ModTags.OxidizerGas,
//                massConsumptionRate = consumptionRate,
//                storageCapacityKg = storageCapacity,
//                requiredMassKg = 0.02f,
//                drawRateMultiplier = 1f,
//                consumptionRadius = 3,
//                showStorageInUI = true,
//                wiltsWhenGasDepleted = true
//            };
//            ExtendPlantToGasConsumer.Extend(plant, info);
//        }

//        // Patches for default-rate plants (0.002f, 1.2f storage).
//        [HarmonyPatch(typeof(SaltySticksPlantConfig), nameof(SaltySticksPlantConfig.CreatePrefab))]
//        internal static class SaltySticksPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(SuperWormPlantConfig), nameof(SuperWormPlantConfig.CreatePrefab))] internal static class SuperWormPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(WormPlantConfig), nameof(WormPlantConfig.CreatePrefab))] internal static class WormPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(SwampHarvestPlantConfig), nameof(SwampHarvestPlantConfig.CreatePrefab))] internal static class SwampHarvestPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(BasicSingleHarvestPlantConfig), nameof(BasicSingleHarvestPlantConfig.CreatePrefab))] internal static class BasicSingleHarvestPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(CarrotPlantConfig), nameof(CarrotPlantConfig.CreatePrefab))] internal static class CarrotPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(HardSkinBerryPlantConfig), nameof(HardSkinBerryPlantConfig.CreatePrefab))] internal static class HardSkinBerryPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(ColdWheatConfig), nameof(ColdWheatConfig.CreatePrefab))] internal static class ColdWheatConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(GardenFoodPlantConfig), nameof(GardenFoodPlantConfig.CreatePrefab))] internal static class GardenFoodPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(ButterflyPlantConfig), nameof(ButterflyPlantConfig.CreatePrefab))] internal static class ButterflyPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }
//        [HarmonyPatch(typeof(FlyTrapPlantConfig), nameof(FlyTrapPlantConfig.CreatePrefab))] internal static class FlyTrapPlantConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.002f, 1.2f); } }

//        // Special-rate plants (0.006f, 2.4f storage).
//        [HarmonyPatch(typeof(VineMotherConfig), nameof(VineMotherConfig.CreatePrefab))] internal static class VineMotherConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.006f, 2.4f); } }
//        [HarmonyPatch(typeof(ForestTreeConfig), nameof(ForestTreeConfig.CreatePrefab))] internal static class ForestTreeConfig_CreatePrefab_Patch { [HarmonyPostfix] private static void Postfix(GameObject __result) { AddGasConsumer(__result, 0.006f, 2.4f); } }
//    }
//}