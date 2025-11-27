using System.Collections.Generic;
using HarmonyLib;

namespace Rephysicalized
{
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
                new MaterialYield("RotPile", 0.8f),
                new MaterialYield("CarbonDioxide", 0.2f)
            ),
            new Entry("SeaLettuce", 12f,
                new MaterialYield("Water", 1f)
            ),
            new Entry("PrickleFlower", Rephysicalized.FoodDensityRebalance.PricklefruitMultiplier,
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
            new Entry("SpiceVine", 4f,
                new MaterialYield("ToxicMud", 1f)
               
            ),
            new Entry("SwampHarvestPlant", Rephysicalized.FoodDensityRebalance.SwampfruitMultiplier,
                new MaterialYield("ToxicMud", 0.8f),
                new MaterialYield("Sulfur", 0.2f)
            ),
            new Entry("BeanPlant", 12f,
                new MaterialYield("ToxicMud", 0.98f),
                new MaterialYield("Methane", 0.02f)

            ),
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
                new MaterialYield("Sand", 0.5f),
                    new MaterialYield("BleachStone", 0.5f)
            ),
            new Entry("SuperWormPlant", 8f,
                new MaterialYield("Sand", 0.95f),
                new MaterialYield("CarbonDioxide", 0.05f)
            ),
            new Entry("CarrotPlant",  Rephysicalized.FoodDensityRebalance.CarrotMultiplier,
                new MaterialYield("Ice", 0.98f),
                new MaterialYield("CarbonDioxide", 0.02f) 
            ),
            new Entry("HardSkinBerryPlant", 1f,
                new MaterialYield("RotPile", 0.8f),
                new MaterialYield("CarbonDioxide", 0.20f)
            ),
            new Entry("SwampLily", 10f,
                new MaterialYield("Sand", 0.80f),
                new MaterialYield("BleachStone", 0.2f)
            ),
            new Entry("BasicFabricPlant", 1f,
                new MaterialYield("ToxicMud", 0.98f),
                new MaterialYield("ContaminatedOxygen", 0.02f) 
            ),
            new Entry("GardenFoodPlant", 1f,
                new MaterialYield("DirtyWater", 0.5f),
                new MaterialYield("Shale", 0.5f)

            ),
                new Entry("KelpPlant", 10f,
                new MaterialYield("Kelp", 1f)

            ),
                     new Entry("ButterflyPlant", 0f,
                new MaterialYield("RotPile", 1f)

            ),
                                 new Entry("GasGrass", 50f,
                new MaterialYield("PlantFIber", 1f)

            ),
                                                        new Entry("ColdBreather", 0f,
                new MaterialYield("PlantFIber", 1f)

            ),
                                                                new Entry("BlueGrass", 20f,
                new MaterialYield("Oxyrock", 1f)

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