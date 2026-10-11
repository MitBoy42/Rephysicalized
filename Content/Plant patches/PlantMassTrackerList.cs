using System.Collections.Generic;
using HarmonyLib;

namespace Rephysicalized
{

    //[HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    //internal static class Registration
    //{
    //    private static void Postfix()
    //    {
    //        PlantMassTrackerRegistry.ApplyToCrop(
    //            plantPrefabId: "ForestTreeBranch",
    //            yields: new List<MaterialYield> { new MaterialYield("WoodLog", 1f) },
    //            realHarvestSubtractKg: 30f);
    //    }
    //}

    //// Register PMT for SpaceTreeBranch (drops WoodLog via PMT)
    //[HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    //internal static class SpaceTreeBranch_PMT_Registration
    //{
    //    private static void Postfix()
    //    {
    //        PlantMassTrackerRegistry.ApplyToCrop(
    //            plantPrefabId: "SpaceTreeBranch",
    //            yields: new List<MaterialYield>
    //            {
    //                new MaterialYield("WoodLog", 1f)
    //            },
    //            realHarvestSubtractKg: 0f
    //        );
    //    }
    //}


    // Compact registration of all PMT crop configs in one place
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class PlantMassTracker_RegistrationList
    {
        private struct Entry
        {
            public string Id;
            public float RealHarvestSubtractKg;
            public MaterialYield[] Yields;
            public Entry(string id, float sub, params MaterialYield[] yields)
            {
                Id = id;
                RealHarvestSubtractKg = sub;
                Yields = yields;
            }
        }

        private static readonly Entry[] Entries =
        {
            // Basic plants
            new Entry("BasicSingleHarvestPlant", 1f,
                new MaterialYield("RotPile", 0.9f),
                new MaterialYield("CarbonDioxide", 0.1f)
            ),
   
            new Entry("SeaLettuce", 12f,
                new MaterialYield("Mud", 1f)
            ),
            new Entry("PrickleFlower", 1f * Rephysicalized.FoodDensityRebalance.IngredientMultiplier[PrickleFruitConfig.ID.ToTag()],
                new MaterialYield("Mud", 0.96f),
                new MaterialYield("Oxygen", 0.04f)
            ),
            new Entry("ColdWheat", 18f,
                new MaterialYield("Mud", 0.99f),
                new MaterialYield("CarbonDioxide", 0.01f)
            ),
            new Entry("Mushroom", 1f,
                new MaterialYield("RotPile", 0.5f),
                new MaterialYield("CarbonDioxide", 0.5f)
            ),
            new Entry("SpiceVine", 4f, new MaterialYield("ToxicMud", 1f)),
            new Entry("SwampHarvestPlant", 1f * Rephysicalized.FoodDensityRebalance.IngredientMultiplier[SwampFruitConfig.ID.ToTag()],
                new MaterialYield("ToxicMud", 0.8f),
                new MaterialYield("Sulfur", 0.2f)
            ),
                new Entry("BeanPlant", 12f, new MaterialYield("ToxicMud", 0.96f), new MaterialYield("Methane", 0.04f)),
                new Entry("SaltPlant", 30f,
                new MaterialYield("Salt", 1f)
            ),
                new Entry("VineMother", 0f,
                new MaterialYield("Mud", 1f)
            ),
                new Entry("WormPlant", 1f,
                new MaterialYield("Sand", 1f)
            ),
                new Entry("Dinofern", 36f,
                new MaterialYield("CrudByproduct", 0.75f),
                new MaterialYield("BleachStone", 0.25f)
            ),
                new Entry("SuperWormPlant", 8f,
                new MaterialYield("Sand", 0.95f),
                new MaterialYield("CarbonDioxide", 0.05f)
            ),
                new Entry("CarrotPlant", 1f *  Rephysicalized.FoodDensityRebalance.IngredientMultiplier[CarrotConfig.ID.ToTag()],
                new MaterialYield("Ice", 0.99f),
                new MaterialYield("CarbonDioxide", 0.01f) 
            ),
            new Entry("HardSkinBerryPlant", 1f,
                new MaterialYield("RotPile", 0.8f),
                new MaterialYield("CarbonDioxide", 0.20f)
            ),
            new Entry("SwampLily", 2f,
                new MaterialYield("Sand", 0.80f),
                new MaterialYield("BleachStone", 0.2f)
            ),
            new Entry("BasicFabricPlant", 1f,
                new MaterialYield("ToxicMud", 0.98f),
                new MaterialYield("ContaminatedOxygen", 0.02f) 
            ),
                new Entry("GardenFoodPlant", 1f, new MaterialYield("DirtyWater", 0.5f), new MaterialYield("Shale", 0.5f)),
                new Entry("KelpPlant", 10f, new MaterialYield("Kelp", 1f)),

                new Entry("ButterflyPlant", 0f, new MaterialYield("Shale", 1f)),

                new Entry("GasGrass", 50f, new MaterialYield("PlantFiber", 1f)),

                new Entry("ColdBreather", 0f, new MaterialYield("PlantFiber", 1f)),
                new Entry("BlueGrass", 20f,
                new MaterialYield("Oxyrock", 1f)
            ),
                new Entry("DewDripperPlant", 20f,
                new MaterialYield("DewDrip", 1f)
            ),
                        new Entry("CritterTrapPlant", 2f,
                new MaterialYield("Sand", 1f)
            ),
                new Entry("ForestTree", 0f,
                 new MaterialYield("ToxicMud", 0.6f),
             new MaterialYield("WoodLog", 0.4f)
            ),
                new Entry("SpaceTree", 0f,
                 new MaterialYield("Mud", 0.6f),
             new MaterialYield("WoodLog", 0.4f)
            ),

                       new Entry("ForestTreeBranch", 30f,
             new MaterialYield("WoodLog", 1f)
            ),
                new Entry("SpaceTreeBranch", 0f,
             new MaterialYield("WoodLog", 1f)
            ),
//Aqua plants
              new Entry("SaltySticksPlant", 1f,
              new MaterialYield("Sand", 1f)
            ),

              new Entry("Clam", 50f,
              new MaterialYield("CrudByproduct", 1f)
            ),

              new Entry("DewPalm", 50f,
              new MaterialYield("PalmWood", 1f)
            ),
              new Entry("TubeWorm", 200f,
              new MaterialYield("ToxicMud", 1f)
            ),
              new Entry("UrchinPlant", 1f,
              new MaterialYield("Urchin", 1f)
            ),
              new Entry("PlanktonCoral", 40f,
              new MaterialYield("Phosphorite", 1f)
            ),
              new Entry("SeaTree", 0f,
              new MaterialYield("ToxicSand", 1f)
            ),
              new Entry("OxyCoral", 0f,
              new MaterialYield("Corallium", 1f)
            ),




        };

        private static void Postfix()
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                PlantMassTrackerRegistry.ApplyToCrop(
                    plantPrefabId: e.Id,
                    yields: new List<MaterialYield>(e.Yields),
                    realHarvestSubtractKg: e.RealHarvestSubtractKg
                );
            }
        }
    }

 
}