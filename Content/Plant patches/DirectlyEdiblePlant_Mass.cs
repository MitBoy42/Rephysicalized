using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized
{
    // Swap DirectlyEdiblePlant_Growth -> DirectlyEdiblePlant_Mass with per-plant tuning, enforce uniqueness, and log results.
    [HarmonyPatch(typeof(EntityConfigManager), nameof(EntityConfigManager.LoadGeneratedEntities))]
    public static class DirectlyEdiblePlant_MassConsumption_AfterEntitiesGenerated_Patch
    {
        private const bool LOG_SWAPS = true;

        private sealed class SwapConfig
        {
            public string[] CandidateIds;
            public float MassConsumptionScale;
            public float GrowthRemovalPercentPerKg;
            public float MinRemainingMassKg;

            public SwapConfig(string id, float scale, float growthPerKg, float minRemain = 1f)
            {
                CandidateIds = new[] { id };
                MassConsumptionScale = scale;
                GrowthRemovalPercentPerKg = growthPerKg;
                MinRemainingMassKg = minRemain;
            }

            public SwapConfig(string[] ids, float scale, float growthPerKg, float minRemain = 1f)
            {
                CandidateIds = ids ?? Array.Empty<string>();
                MassConsumptionScale = scale;
                GrowthRemovalPercentPerKg = growthPerKg;
                MinRemainingMassKg = minRemain;
            }
        }

        public static void Postfix()
        {
            var configs = new[]
            {
                new SwapConfig("HardSkinBerryPlant", 1f, 0.06667f, 1f),
                new SwapConfig(new[] { "PrickleFlower", "PrickleFLower" }, 1f, 0.008333f, 1f),
                new SwapConfig("BasicFabricPlant", 1f, 1f / 180f, 1f), // Thimble Reed
                new SwapConfig("BasicSingleHarvestPlant", 1f, 1f / 30f, 1f), // Mealwood
                new SwapConfig("SpiceVine", 1f, 1f / 288f, 1f), // Pincha Pepper
                new SwapConfig("SwampLily", 1f, 1f / 100f, 1f),
                new SwapConfig("ForestTreeBranch", 1f, 1f / 100f, 1f),
                new SwapConfig("SpaceTreeBranch", 1f, 1f / 40f, 1f),
                new SwapConfig("CarrotPlant", 1f, 1f / 135f, 1f),
                new SwapConfig("BeanPlant", 1f, 1f / 525f, 1f),
                new SwapConfig("KelpPlant", 1f, 1f / 100f, 1f),
               new SwapConfig("GasGrass", 1f, 1f / 200f, 1f),
            };

            foreach (var cfg in configs)
                ApplySwap(cfg);
        }

        private static void ApplySwap(SwapConfig cfg)
        {
            if (cfg?.CandidateIds == null || cfg.CandidateIds.Length == 0)
                return;

            GameObject targetGo = null;

            // Direct lookup by id
            foreach (var id in cfg.CandidateIds)
            {
                targetGo = Assets.GetPrefab(id);
                if (targetGo != null) break;
            }

            // Fallback: find any prefab with Growth that matches one of the IDs
            if (targetGo == null)
            {
                List<GameObject> prefabs = Assets.GetPrefabsWithComponent<DirectlyEdiblePlant_Growth>();
                if (prefabs != null)
                {
                    for (int i = 0; i < prefabs.Count && targetGo == null; i++)
                    {
                        var go = prefabs[i];
                        var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
                        if (kpid == null) continue;

                        foreach (var id in cfg.CandidateIds)
                        {
                            if (string.Equals(kpid.PrefabTag.Name, id, StringComparison.Ordinal))
                            {
                                targetGo = go;
                                break;
                            }
                        }
                    }
                }
            }

            if (targetGo == null)
                return;

            // Remove vanilla growth-based directly edible component if present
            var removed = false;
            var oldComp = targetGo.GetComponent<DirectlyEdiblePlant_Growth>();
            if (oldComp != null)
            {
                UnityEngine.Object.DestroyImmediate(oldComp);
                removed = true;
            }

            // Add or configure the mass-based variant (PMT-aware)
            var newComp = targetGo.GetComponent<DirectlyEdiblePlant_Mass>() ?? targetGo.AddOrGet<DirectlyEdiblePlant_Mass>();
            newComp.massConsumptionScale = cfg.MassConsumptionScale;
            newComp.growthRemovalPercentPerKg = cfg.GrowthRemovalPercentPerKg;
            newComp.minRemainingMassKg = cfg.MinRemainingMassKg;

            if (LOG_SWAPS)
            {
                var kpid = targetGo.GetComponent<KPrefabID>();
                var id = kpid != null ? kpid.PrefabID().Name : targetGo.name;
                var comps = targetGo.GetComponents<IPlantConsumptionInstructions>()?.Length ?? 0;
            //    Debug.Log($"[DirectEdibleSwap] {id}: mass={newComp != null}, removedGrowth={removed}, totalConsumableComps={comps}");
            }
        }
    }

    [HarmonyPatch(typeof(DirectlyEdiblePlant_Growth), nameof(DirectlyEdiblePlant_Growth.ConsumePlant))]
    internal static class DirectlyEdiblePlant_Growth_ConsumePlant_Guard
    {
        private static bool Prefix(DirectlyEdiblePlant_Growth __instance, ref float __result)
        {
            if (__instance != null && __instance.gameObject.GetComponent<DirectlyEdiblePlant_Mass>() != null)
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A mass-based variant of directly edible plant consumption.
    /// - Consumes plant PrimaryElement mass (kg).
    /// - Optionally reduces maturity as a byproduct.
    /// - Emits PMT deltas so tracked mass remains consistent with consumption.
    /// - Keeps FoodType EatPlantDirectly for diet compatibility.
    /// </summary>
    public class DirectlyEdiblePlant_Mass : KMonoBehaviour, IPlantConsumptionInstructions
    {
        [MyCmpGet] private Growing growing;
        [MyCmpGet] private PrimaryElement primary;
        [MyCmpGet] private PlantMassTrackerComponent pmt;

        // Removed minMaturityPercentToEat (maturity-gated check).
        [SerializeField] public float minRemainingMassKg = 1f;
        [SerializeField] public float massConsumptionScale = 1f;
        [SerializeField, Range(0f, 1f)] public float growthRemovalPercentPerKg = 0.0f;

        // New: minimum available mass (above floor) required before the plant can be eaten.
        [SerializeField] public float minMassToEatKg = 5f;

        private float GetEffectiveFloorKg()
        {
            // When PMT tracks this plant, enforce the 1 kg baseline PMT uses
            float baseline = (pmt != null) ? 1f : 0f;
            return Mathf.Max(minRemainingMassKg, baseline);
        }

        public bool CanPlantBeEaten()
        {
            if (primary == null) return false;

            float floor = GetEffectiveFloorKg();
            float availableKg = Mathf.Max(0f, primary.Mass - floor);
            return availableKg >= Mathf.Max(0f, minMassToEatKg);
        }

        public float ConsumePlant(float desiredUnitsToConsume)
        {
            if (primary == null || desiredUnitsToConsume <= 0f) return 0f;

            float requestedKg = Mathf.Max(0f, desiredUnitsToConsume) * Mathf.Max(0f, massConsumptionScale);
            float floor = GetEffectiveFloorKg();
            float availableKg = Mathf.Max(0f, primary.Mass - floor);
            float kgToConsume = Mathf.Min(requestedKg, availableKg);
            if (kgToConsume <= 0f) return 0f;

            // Important: avoid double subtraction when PMT is present.
            // - If PMT exists, only emit the PMT delta; PMT will update its tracked mass and the PE mass consistently.
            // - If PMT does not exist, update the PE mass directly.
            if (pmt != null)
            {
                pmt.AddExternalMass(-kgToConsume);
            }
            else
            {
                primary.Mass = Mathf.Max(floor, primary.Mass - kgToConsume);
            }

            // Optional maturity reduction side-effect
            if (growing != null && growthRemovalPercentPerKg > 0f)
            {
                var maturity = Db.Get().Amounts.Maturity.Lookup(growing.gameObject);
                if (maturity != null)
                {
                    float maturityMax = maturity.GetMax();
                    if (maturityMax > 0f)
                    {
                        float maturityReduction = Mathf.Clamp(kgToConsume * growthRemovalPercentPerKg * maturityMax, 0f, maturity.value);
                        if (maturityReduction > 0f)
                            growing.ConsumeGrowthUnits(maturityReduction, 1f);
                    }
                }
            }

            return kgToConsume;
        }

        public float PlantProductGrowthPerCycle() => 0f;

        public string GetFormattedConsumptionPerCycle(float consumer_KGWorthOfCaloriesLostPerSecond)
        {
            float massPerCycle = Mathf.Max(0f, consumer_KGWorthOfCaloriesLostPerSecond) * Mathf.Max(0f, massConsumptionScale) * 600f;
            string s = GameUtil.GetFormattedMass(massPerCycle);
            if (growthRemovalPercentPerKg > 0f && massPerCycle > 0f)
            {
                float maturityPercentPerCycle = growthRemovalPercentPerKg * massPerCycle * 100f;
                return $"{s}/cycle + {GameUtil.GetFormattedPercent(maturityPercentPerCycle)} growth";
            }
            return $"{s}/cycle";
        }

        public CellOffset[] GetAllowedOffsets() => null;
        public Diet.Info.FoodType GetDietFoodType() => Diet.Info.FoodType.EatPlantDirectly;
    }
}