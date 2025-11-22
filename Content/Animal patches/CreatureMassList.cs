using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized
{ 

       // Configurable default mass (kg) per species prefix. Update values as needed.
    internal static class BaseCreatureDefaultMassConfig
{
    public static readonly Dictionary<string, float> DefaultSpeciesMassKg =
        new Dictionary<string, float>(StringComparer.InvariantCultureIgnoreCase)
        {
                // Vanilla
                { "Hatch", 1f },
                { "Puft", 0.25f },
                { "Drecko", 1f },
                { "Squirrel", 1f },
                { "Pacu", 2f },
                { "OilFloater", 1f },
                { "LightBug", 0.1f },
                { "Crab", 1f },
                { "DivergentBeetle", 1f },
                { "Divergent", 1f },
                { "Staterpillar", 1f },
                { "Mole", 1f },
                { "Bee", 1f },
                { "Moo", 10f },
                { "Glom", 1f },

                // Modded / extended list
                { "WoodDeer", 1f },
                    { "GlassDeer", 1f },
                { "Seal", 1f },
                { "GoldBelly", 4f },
                { "IceBelly", 4f },
                { "Stego", 4f },
                 { "Raptor", 4f },
                { "Butterfly", 1f },
                { "Mosquito", 1f },
                { "Chameleon", 0.5f },
                { "PrehistoricPacu", 2f },
                     { "AlgaeStego", 4f },
        };

    public static bool TryGetDesiredMass(string prefabId, out float mass)
    {
        mass = 0f;
        if (string.IsNullOrEmpty(prefabId))
            return false;

        foreach (var kvp in DefaultSpeciesMassKg)
        {
            // Prefix match to cover variants and babies
            if (prefabId.StartsWith(kvp.Key, true, CultureInfo.InvariantCulture))
            {
                mass = kvp.Value;
                return mass > 0f;
            }
        }
        return false;
    }
}

// Patch all overloads of EntityTemplates.CreatePlacedEntity where arg4 is float mass.
[HarmonyPatch]
internal static class BaseCreatureDefaultMassPatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = AccessTools.GetDeclaredMethods(typeof(EntityTemplates))
                                 .Where(m => m.Name == "CreatePlacedEntity");

        foreach (var m in methods)
        {
            var p = m.GetParameters();
            if (p.Length >= 5
                && p[0].ParameterType == typeof(string)     // id
                && p[1].ParameterType == typeof(string)     // name
                && p[2].ParameterType == typeof(string)     // desc
                && p[3].ParameterType == typeof(float))     // mass
            {

                yield return m;
            }
        }
    }

    // Common prefix for all matched overloads
    static void Prefix(ref string id, ref float mass)
    {
        if (BaseCreatureDefaultMassConfig.TryGetDesiredMass(id, out var desiredMass))
        {
            if (!Mathf.Approximately(mass, desiredMass))
            {

                mass = desiredMass;
            }
        }

    }
}


    //WoodDeer tracker

    [HarmonyPatch(typeof(GlassDeerConfig), nameof(GlassDeerConfig.CreatePrefab))]
    public static class GlassDeerConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 1f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            __result.AddOrGet<CreatureMassTrackerLink>();



        }
    }
    //WoodDeer tracker

    [HarmonyPatch(typeof(WoodDeerConfig), nameof(WoodDeerConfig.CreatePrefab))]
    public static class WoodDeerConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 1f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            __result.AddOrGet<CreatureMassTrackerLink>();



        }
    }

    // Apply the tracker to baby WoodDeer as well so it carries into adulthood
    [HarmonyPatch(typeof(BabyWoodDeerConfig), nameof(BabyWoodDeerConfig.CreatePrefab))]
    public static class BabyWoodDeerConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 1f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;

        }
    }

    // Ensure wooddeer uses ConsumedMass mode with MASS_RATIO=1 so we can apply per-food ratios cleanly.
    [HarmonyPatch(typeof(BaseDeerConfig), nameof(BaseDeerConfig.BaseDeer))]
    internal static class WoodDeer_MassTracker_DeerSetup_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
            if (__result == null) return;

            var tracker = __result.AddOrGet<CreatureMassTracker>();

            // Freeze current computed mass as baseline and switch to mass mode
            tracker.SetAccumulationMode(CreatureMassTracker.AccumulationMode.ConsumedMass, keepCurrentMassAsBaseline: true);

            // Global MASS_RATIO must be 1 since we handle ratios per-food below.
            tracker.MASS_RATIO = 1f;
        }
    }

    // Apply deer-specific mass-gain per food by intercepting the tracker’s event handler for deer only.
    [HarmonyPatch(typeof(CreatureMassTracker), "OnCaloriesConsumed")]
    internal static class WoodDeer_PerFoodMassGain_Prefix
    {
        private static readonly Tag HardSkinBerryItem = new Tag("HardSkinBerry");
        private static readonly Tag PrickleFruitItem = new Tag("PrickleFruit");
        private static readonly Tag Katairite = new Tag("Katairite");
        private static readonly Tag HardSkinBerryPlant = new Tag("HardSkinBerryPlant");
        private static readonly Tag PrickleFlowerPlantA = new Tag("PrickleFlower");
     

        // Return false to skip original when we handle the event here.
        private static bool Prefix(CreatureMassTracker __instance, object data)
        {
            try
            {
                // Only handle wooddeer while in mass mode
                var go = __instance.gameObject;
                if (!IsWoodDeer(go) || __instance.Mode != CreatureMassTracker.AccumulationMode.ConsumedMass)
                    return true;

                if (data is not CreatureCalorieMonitor.CaloriesConsumedEvent evt || evt.calories <= 0f)
                    return true;

                // Resolve the consumed kg via the diet entry used
                var smi = go.GetSMI<CreatureCalorieMonitor.Instance>();
                var diet = smi?.stomach?.diet;
                var info = diet?.GetDietInfo(evt.tag);
                if (info == null)
                    return false; // politely skip any mass update (matches tracker’s safe behavior)

                float consumedKg = info.ConvertCaloriesToConsumptionMass(evt.calories);
                if (consumedKg <= 0f)
                    return false;

                // Apply deer-specific per-food ratios
                float ratio = GetDeerPerFoodMassGainRatio(evt.tag);
                if (ratio <= 0f)
                    return false;

                __instance.ReportConsumedMass(consumedKg * ratio);

                // We’ve handled this event for deer; skip the original to avoid double-counting
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] WoodDeer_PerFoodMassGain_Prefix failed: {e}");
                // Fall through to original in case of any issue
                return true;
            }
        }

        private static bool IsWoodDeer(GameObject go)
        {
            var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
            if (kpid == null) return false;

            string id = kpid.PrefabTag.Name;
            if (string.IsNullOrEmpty(id)) return false;

            // Accept both adult and baby variants by substring
            return id.IndexOf("WoodDeer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float GetDeerPerFoodMassGainRatio(Tag consumedTag)
        {
      

            // Plants:
            if (consumedTag == HardSkinBerryPlant)
                return 0.3334f; // ~1/3
            if (consumedTag == PrickleFlowerPlantA )
                return 1f / 12f; // ~0.0833333
            if (consumedTag == Katairite)
                return 0.005f;

            // Default: no special scaling (treat as 1:1)
            return 1f;
        }
    }

    //Bammoth tracker

    [HarmonyPatch(typeof(IceBellyConfig), nameof(IceBellyConfig.CreatePrefab))]
    public static class IceBellyConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            __result.AddOrGet<CreatureMassTrackerLink>();

            tracker.ExtraDrops = new List<CreatureMassTracker.ExtraDropSpec>
            {

                 new CreatureMassTracker.ExtraDropSpec { id = "Meat", fraction = 0.75f },
                                  new CreatureMassTracker.ExtraDropSpec { id = "Rotpile", fraction = 0.25f },
            };
            var kpid = __result.GetComponent<KPrefabID>();
            CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, tracker.ExtraDrops);

        }
    }

    // Apply the tracker to baby IceBelly as well so it carries into adulthood
    [HarmonyPatch(typeof(BabyIceBellyConfig), nameof(BabyIceBellyConfig.CreatePrefab))]
    public static class BabyIceBellyConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            tracker.ExtraDrops = new List<CreatureMassTracker.ExtraDropSpec>
            {


                 new CreatureMassTracker.ExtraDropSpec { id = "Meat", fraction = 0.75f },
                                  new CreatureMassTracker.ExtraDropSpec { id = "Rotpile", fraction = 0.25f },
            };
            var kpid = __result.GetComponent<KPrefabID>();
            CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, tracker.ExtraDrops);

        }
    }
    //GoldBammoth tracker

    [HarmonyPatch(typeof(GoldBellyConfig), nameof(GoldBellyConfig.CreatePrefab))]
    public static class GoldBellyConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            __result.AddOrGet<CreatureMassTrackerLink>();

            tracker.ExtraDrops = new List<CreatureMassTracker.ExtraDropSpec>
            {

                 new CreatureMassTracker.ExtraDropSpec { id = "Meat", fraction = 0.75f },
                                  new CreatureMassTracker.ExtraDropSpec { id = "Rotpile", fraction = 0.25f },
            };
            var kpid = __result.GetComponent<KPrefabID>();
            CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, tracker.ExtraDrops);

        }
    }

    // Apply the tracker to baby GoldBelly as well so it carries into adulthood
    [HarmonyPatch(typeof(BabyGoldBellyConfig), nameof(BabyGoldBellyConfig.CreatePrefab))]
    public static class BabyGoldBellyConfig_CreatePrefab_AddMassTracker
    {
        public static void Postfix(ref GameObject __result)
        {

            var tracker = __result.AddOrGet<CreatureMassTracker>();
            tracker.STARTING_MASS = 4f;
            tracker.MASS_RATIO = 1f;
            tracker.Mode = CreatureMassTracker.AccumulationMode.ConsumedMass;
            tracker.ExtraDrops = new List<CreatureMassTracker.ExtraDropSpec>
            {


                 new CreatureMassTracker.ExtraDropSpec { id = "Meat", fraction = 0.75f },
                                  new CreatureMassTracker.ExtraDropSpec { id = "Rotpile", fraction = 0.25f },
            };
            var kpid = __result.GetComponent<KPrefabID>();
            CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, tracker.ExtraDrops);
        }
    }

    // Ensure IceBelly uses ConsumedMass mode with MASS_RATIO=1 so we can apply per-food ratios cleanly.
    [HarmonyPatch(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly))]
    internal static class IceBelly_MassTracker_IceBellySetup_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
            if (__result == null) return;

            var tracker = __result.AddOrGet<CreatureMassTracker>();

            // Freeze current computed mass as baseline and switch to mass mode
            tracker.SetAccumulationMode(CreatureMassTracker.AccumulationMode.ConsumedMass, keepCurrentMassAsBaseline: true);

            // Global MASS_RATIO must be 1 since we handle ratios per-food below.
            tracker.MASS_RATIO = 1f;
        }
    }

    // Apply belly-specific mass-gain per food by intercepting the tracker’s event handler for belly only.
    [HarmonyPatch(typeof(CreatureMassTracker), "OnCaloriesConsumed")]
    internal static class IceBelly_PerFoodMassGain_Prefix
    {
        private static readonly Tag Carrot = new Tag(CarrotConfig.ID);
        private static readonly Tag Bean = new Tag("BeanPlantSeed");

        private static readonly Tag CarrotPlant = new Tag("CarrotPlant");
        private static readonly Tag BeanPlant = new Tag("BeanPlant");


        // Return false to skip original when we handle the event here.
        private static bool Prefix(CreatureMassTracker __instance, object data)
        {
            try
            {
                // Only handle wooddeer while in mass mode
                var go = __instance.gameObject;
                if (!IsBelly(go) || __instance.Mode != CreatureMassTracker.AccumulationMode.ConsumedMass)
                    return true;

                if (data is not CreatureCalorieMonitor.CaloriesConsumedEvent evt || evt.calories <= 0f)
                    return true;

                // Resolve the consumed kg via the diet entry used
                var smi = go.GetSMI<CreatureCalorieMonitor.Instance>();
                var diet = smi?.stomach?.diet;
                var info = diet?.GetDietInfo(evt.tag);
                if (info == null)
                    return false; // politely skip any mass update (matches tracker’s safe behavior)

                float consumedKg = info.ConvertCaloriesToConsumptionMass(evt.calories);
                if (consumedKg <= 0f)
                    return false;

                // Apply belly-specific per-food ratios
                float ratio = GetDeerPerFoodMassGainRatio(evt.tag);
                if (ratio <= 0f)
                    return false;

                __instance.ReportConsumedMass(consumedKg * ratio);

                // We’ve handled this event for belly; skip the original to avoid double-counting
                return false;
            }
            catch (Exception e)
            {
                //Debug.LogWarning($"[Rephysicalized] Belly_PerFoodMassGain_Prefix failed: {e}");
                // Fall through to original in case of any issue
                return true;
            }
        }

        private static bool IsBelly(GameObject go)
        {
            var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
            if (kpid == null) return false;

            string id = kpid.PrefabTag.Name;
            if (string.IsNullOrEmpty(id)) return false;

            // Accept both adult and baby variants by substring
            return id.IndexOf("Belly", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float GetDeerPerFoodMassGainRatio(Tag consumedTag)
        {
     

            // Plants:
            if (consumedTag == CarrotPlant)
                return 0.033334f;
            if (consumedTag == BeanPlant)
                return 0.033334f;

            // Default: no special scaling (treat as 1:1)
            return 1f;
        }
    }



    [HarmonyPatch]
    internal static class CreatureMassTracker_CompactLists
    {
        // Minimal shape to describe extra drops
        private readonly struct Drop
        {
            public readonly string id;
            public readonly float fraction;
            public Drop(string id, float fraction) { this.id = id; this.fraction = fraction; }
        }

        // Generic entry used for both calorie- and mass-based configs
        private sealed class Entry
        {
            public readonly Type configType;       // Config class type (e.g., typeof(HatchConfig))
            public readonly string methodName;     // Usually "CreatePrefab" (some mods use "CreateStego", etc.)
            public readonly float? startMass;
            public readonly float? calorieRatio;   // If set => calorie-based
            public readonly float? massRatio;      // If set or explicit mass mode => mass-based
            public readonly float? maxScaleMultiple;
            public readonly bool addLink;
            public readonly Drop[] drops;
            public readonly float? growDropUnits;  // e.g., BabyCrab Wood/Normal

            public Entry(Type type, string method, float? startMass = null, float? cal = null, float? mass = null,
                         float? maxMult = null, bool addLink = false, Drop[] drops = null)
            {
                this.configType = type;
                this.methodName = method;
                this.startMass = startMass;
                this.calorieRatio = cal;
                this.massRatio = mass;
                this.maxScaleMultiple = maxMult;
                this.addLink = addLink;
                this.drops = drops;
              
            }
        }

        // Calorie-ratio (CALORIE_RATIO) species
        private static readonly Entry[] CalorieEntries =
        {
          

            // Hatches
            new Entry(typeof(HatchConfig), nameof(HatchConfig.CreatePrefab), startMass: 1f, cal: 700000f, addLink: true),
            new Entry(typeof(BabyHatchConfig), nameof(BabyHatchConfig.CreatePrefab), startMass: 1f, cal: 700000f),

            new Entry(typeof(HatchHardConfig), nameof(HatchHardConfig.CreatePrefab), startMass: 1f, cal: 700000f, addLink: true,
                      drops: new[] { new Drop("SedimentaryRock", 1f) }),
            new Entry(typeof(BabyHatchHardConfig), nameof(BabyHatchHardConfig.CreatePrefab), startMass: 1f, cal: 700000f,
                      drops: new[] { new Drop("SedimentaryRock", 1f) }),

            new Entry(typeof(HatchMetalConfig), nameof(HatchMetalConfig.CreatePrefab), startMass: 1f, cal: 140000f, maxMult: 500f, addLink: true,
                      drops: new[] { new Drop("Katairite", 1f) }),
            new Entry(typeof(BabyHatchMetalConfig), nameof(BabyHatchMetalConfig.CreatePrefab), startMass: 1f, cal: 140000f, maxMult: 500f,
                      drops: new[] { new Drop("Katairite", 1f) }),

            new Entry(typeof(HatchVeggieConfig), nameof(HatchVeggieConfig.CreatePrefab), startMass: 1f, cal: 140000f, maxMult: 500f, addLink: true,
                      drops: new[] { new Drop("Algae", 1f) }),
        
            new Entry(typeof(BabyHatchVeggieConfig), nameof(BabyHatchVeggieConfig.CreatePrefab), startMass: 1f, cal: 140000f, maxMult: 500f,
                      drops: new[] { new Drop("Algae", 1f) }),


                 // Raptors
            new Entry(typeof(RaptorConfig), nameof(RaptorConfig.CreatePrefab), startMass: 4f, cal: 80000f, maxMult: 100f, addLink: true,
                      drops: new[] { new Drop("DinosaurMeat", 0.2f), new Drop("RotPile", 0.8f) }),
            new Entry(typeof(BabyRaptorConfig), nameof(BabyRaptorConfig.CreatePrefab), startMass: 4f, cal: 800000f, maxMult: 100f,
                      drops: new[] { new Drop("DinosaurMeat", 0.2f), new Drop("RotPile", 0.8f) }),
                
            // Seals
            new Entry(typeof(SealConfig), nameof(SealConfig.CreatePrefab), startMass: 1f, cal: 12500f, maxMult: 700f, addLink: true,
                      drops: new[] { new Drop("Tallow", 1f) }),
            new Entry(typeof(BabySealConfig), nameof(BabySealConfig.CreatePrefab), startMass: 1f, cal: 12500f, maxMult: 700f,
                      drops: new[] { new Drop("Tallow", 1f) }),
                // PrehistoricPacu
            new Entry(typeof(PrehistoricPacuConfig), nameof(PrehistoricPacuConfig.CreatePrefab), startMass: 2f, cal: 50000f, maxMult: 100f, addLink: true,
                      drops: new[] { new Drop("PrehistoricPacuFillet", 0.75f), new Drop("RotPile", 0.25f) }),
            new Entry(typeof(PrehistoricPacuConfig), nameof(PrehistoricPacuConfig.CreatePrefab), startMass: 2f, cal: 50000f, maxMult: 100f,
                      drops: new[] { new Drop("PrehistoricPacuFillet", 0.75f), new Drop("RotPile", 0.25f)  }),

            // Drecko (base)
            new Entry(typeof(DreckoConfig), nameof(DreckoConfig.CreatePrefab), startMass: 1f, cal: 2000000f, maxMult: 150f, addLink: true),
            new Entry(typeof(BabyDreckoConfig), nameof(BabyDreckoConfig.CreatePrefab), startMass: 1f, cal: 2000000f, maxMult: 150f),

            // Pufts
            new Entry(typeof(PuftConfig), nameof(PuftConfig.CreatePrefab), startMass: 0.25f, cal: 80000f, maxMult: 1000f, addLink: true),
            new Entry(typeof(BabyPuftConfig), nameof(BabyPuftConfig.CreatePrefab), startMass: 0.25f, cal: 80000f, maxMult : 1000f),

            new Entry(typeof(PuftOxyliteConfig), nameof(PuftOxyliteConfig.CreatePrefab), startMass: 0.25f, cal: 80000f, maxMult: 1000f, addLink: true),
            new Entry(typeof(BabyPuftOxyliteConfig), nameof(BabyPuftOxyliteConfig.CreatePrefab), startMass: 0.25f, cal: 80000f, maxMult: 1000f),

            new Entry(typeof(PuftBleachstoneConfig), nameof(PuftBleachstoneConfig.CreatePrefab), startMass: 0.25f, cal: 133333f, maxMult: 500f, addLink: true),
            new Entry(typeof(BabyPuftBleachstoneConfig), nameof(BabyPuftBleachstoneConfig.CreatePrefab), startMass: 0.25f, cal: 133333f, maxMult: 500f),

            new Entry(typeof(PuftAlphaConfig), nameof(PuftAlphaConfig.CreatePrefab), startMass: 0.25f, cal: 7407f, maxMult: 8000f, addLink: true,
                      drops: new[] { new Drop("ContaminatedOxygen", 1f) }),
            new Entry(typeof(BabyPuftAlphaConfig), nameof(BabyPuftAlphaConfig.CreatePrefab), startMass: 0.25f, cal: 7407f, maxMult: 8000f,
                      drops: new[] { new Drop("ContaminatedOxygen", 1f) }),

            // Crabs
            new Entry(typeof(CrabConfig), nameof(CrabConfig.CreatePrefab), startMass: 1f, cal: 10000f, addLink: true,
                      drops: new[] { new Drop("SedimentaryRock", 1f) }),
            new Entry(typeof(BabyCrabConfig), nameof(BabyCrabConfig.CreatePrefab), startMass: 1f, cal: 10000f,
                      drops: new[] { new Drop("SedimentaryRock", 1f) }),

            new Entry(typeof(CrabWoodConfig), nameof(CrabWoodConfig.CreatePrefab), startMass: 1f, cal: 952.38095f, maxMult: 5000f, addLink: true,
                      drops: new[] { new Drop("Woodlog", 1f) }), // kept original string
            new Entry(typeof(BabyCrabWoodConfig), nameof(BabyCrabWoodConfig.CreatePrefab), startMass: 1f, cal: 952.38095f, maxMult: 5000f,
                      drops: new[] { new Drop("Woodlog", 1f) }),

            new Entry(typeof(CrabFreshWaterConfig), nameof(CrabFreshWaterConfig.CreatePrefab), startMass: 1f, cal: 10000f, addLink: true,
                      drops: new[] { new Drop("Clay", 1f) }),
            new Entry(typeof(BabyCrabFreshWaterConfig), nameof(BabyCrabFreshWaterConfig.CreatePrefab), startMass: 1f, cal: 10000f,
                      drops: new[] { new Drop("Clay", 1f) }),

            // Staterpillars (Plug/Sponge/Gas Slug families)
            new Entry(typeof(StaterpillarConfig), nameof(StaterpillarConfig.CreatePrefab), startMass: 1f, cal: 2000000f, addLink: true,
                      drops: new[] { new Drop("RotPile", 1f) }),
            new Entry(typeof(BabyStaterpillarConfig), nameof(BabyStaterpillarConfig.CreatePrefab), startMass: 1f, cal: 2000000f,
                      drops: new[] { new Drop("RotPile", 1f) }),

            new Entry(typeof(StaterpillarLiquidConfig), nameof(StaterpillarLiquidConfig.CreatePrefab), startMass: 1f, cal: 2000000f, addLink: true,
                      drops: new[] { new Drop("RotPile", 1f) }),
            new Entry(typeof(BabyStaterpillarLiquidConfig), nameof(BabyStaterpillarLiquidConfig.CreatePrefab), startMass: 1f, cal: 2000000f,
                      drops: new[] { new Drop("RotPile", 1f) }),

            new Entry(typeof(StaterpillarGasConfig), nameof(StaterpillarGasConfig.CreatePrefab), startMass: 1f, cal: 2000000f, addLink: true,
                      drops: new[] { new Drop("RotPile", 1f) }),
            new Entry(typeof(BabyStaterpillarGasConfig), nameof(BabyStaterpillarGasConfig.CreatePrefab), startMass: 1f, cal: 2000000f,
                      drops: new[] { new Drop("RotPile", 1f) }),

            // Moo
            new Entry(typeof(MooConfig), nameof(MooConfig.CreatePrefab), startMass: 10f, cal: 100000f, maxMult: 10f, addLink: true,
                      drops: new[] { new Drop("Tallow", 1f) }),
                   new Entry(typeof(DieselMooConfig), nameof(DieselMooConfig.CreatePrefab), startMass: 10f, cal: 100000f, maxMult: 10f, addLink: true,
                      drops: new[] { new Drop("Tallow", 1f) }),


            // Moles
            new Entry(typeof(MoleConfig), nameof(MoleConfig.CreatePrefab), startMass: 1f, cal: 20000f, maxMult: 100000f, addLink: true,
                      drops: new[] { new Drop("CrudByproduct", 0.983f), new Drop("Meat", 0.007f), new Drop("RefinedLipid", 0.01f) }),
            new Entry(typeof(BabyMoleConfig), nameof(BabyMoleConfig.CreatePrefab), startMass: 1f, cal: 20000f, maxMult: 100000f,
                      drops: new[] { new Drop("CrudByproduct", 0.983f), new Drop("Meat", 0.005f), new Drop("RefinedLipid", 0.01f) }),

            new Entry(typeof(MoleDelicacyConfig), nameof(MoleDelicacyConfig.CreatePrefab), startMass: 1f, cal: 20000f, maxMult: 100000f, addLink: true,
                      drops: new[] { new Drop("CrudByproduct", 0.9865f), new Drop("Meat", 0.0035f), new Drop("RefinedLipid", 0.01f) }),
            new Entry(typeof(BabyMoleDelicacyConfig), nameof(BabyMoleDelicacyConfig.CreatePrefab), startMass: 1f, cal: 20000f, maxMult: 100000f,
                      drops: new[] { new Drop("CrudByproduct", 0.9865f), new Drop("Meat", 0.0035f), new Drop("RefinedLipid", 0.01f) }),

            // Divergents
            new Entry(typeof(DivergentBeetleConfig), nameof(DivergentBeetleConfig.CreatePrefab), startMass: 1f, cal: 1400000f, addLink: true),
            new Entry(typeof(BabyDivergentBeetleConfig), nameof(BabyDivergentBeetleConfig.CreatePrefab), startMass: 1f, cal: 1400000f),

            new Entry(typeof(DivergentWormConfig), nameof(DivergentWormConfig.CreatePrefab), startMass: 1f, cal: 700000f, addLink: true),
            new Entry(typeof(BabyWormConfig), nameof(BabyWormConfig.CreatePrefab), startMass: 1f, cal: 1400000f),

            // Chameleon (Dartle)
            new Entry(typeof(ChameleonConfig), nameof(ChameleonConfig.CreatePrefab), startMass: 0.5f, cal: 4000000f,  maxMult: 250f, addLink: true),
            new Entry(typeof(BabyChameleonConfig), nameof(BabyChameleonConfig.CreatePrefab), startMass: 0.5f, cal: 4000000f, maxMult: 250f),

            // Light bugs (all colors)
            new Entry(typeof(LightBugConfig), nameof(LightBugConfig.CreatePrefab), startMass: 0.1f, cal: 240963f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBabyConfig), nameof(LightBugBabyConfig.CreatePrefab), startMass: 1f, cal: 240963f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugOrangeConfig), nameof(LightBugOrangeConfig.CreatePrefab), startMass: 0.1f, cal: 200000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugOrangeBabyConfig), nameof(LightBugOrangeBabyConfig.CreatePrefab), startMass: 1f, cal: 200000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugPurpleConfig), nameof(LightBugPurpleConfig.CreatePrefab), startMass: 0.1f, cal: 32000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugPurpleBabyConfig), nameof(LightBugPurpleBabyConfig.CreatePrefab), startMass: 1f, cal: 32000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugPinkConfig), nameof(LightBugPinkConfig.CreatePrefab), startMass: 0.1f, cal: 320000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugPinkBabyConfig), nameof(LightBugPinkBabyConfig.CreatePrefab), startMass: 1f, cal: 32000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugBlueConfig), nameof(LightBugBlueConfig.CreatePrefab), startMass: 0.1f, cal: 32000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBlueBabyConfig), nameof(LightBugBlueBabyConfig.CreatePrefab), startMass: 1f, cal: 32000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugBlackConfig), nameof(LightBugBlackConfig.CreatePrefab), startMass: 0.1f, cal: 32000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugBlackBabyConfig), nameof(LightBugBlackBabyConfig.CreatePrefab), startMass: 1f, cal: 32000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            new Entry(typeof(LightBugCrystalConfig), nameof(LightBugCrystalConfig.CreatePrefab), startMass: 0.1f, cal: 40000f, maxMult: 250f, addLink: true,
                      drops: new[] { new Drop("Glass", 1f) }),
            new Entry(typeof(LightBugCrystalBabyConfig), nameof(LightBugCrystalBabyConfig.CreatePrefab), startMass: 1f, cal: 40000f, maxMult: 250f,
                      drops: new[] { new Drop("Glass", 1f) }),

            // Slicksters (Oil Floater)
            new Entry(typeof(OilFloaterConfig), nameof(OilFloaterConfig.CreatePrefab), startMass: 1f, cal: 120000f, addLink: true),
            new Entry(typeof(OilFloaterBabyConfig), nameof(OilFloaterBabyConfig.CreatePrefab), startMass: 1f, cal: 120000f),

            new Entry(typeof(OilFloaterHighTempConfig), nameof(OilFloaterHighTempConfig.CreatePrefab), startMass: 1f, cal: 120000f, addLink: true),
            new Entry(typeof(OilFloaterHighTempBabyConfig), nameof(OilFloaterHighTempBabyConfig.CreatePrefab), startMass: 1f, cal: 120000f),
        };

        // Mass-ratio (Mode=ConsumedMass) species
        private static readonly Entry[] MassEntries =
        {
              // Squirrels (keep simple: tracker + optional link)
            new Entry(typeof(SquirrelConfig), nameof(SquirrelConfig.CreatePrefab), mass: 0.05f, addLink: true),
            new Entry(typeof(BabySquirrelConfig), nameof(BabySquirrelConfig.CreatePrefab), mass: 0.05f),
            new Entry(typeof(SquirrelHugConfig), nameof(SquirrelHugConfig.CreatePrefab), mass: 0.05f, addLink: true),
            new Entry(typeof(BabySquirrelHugConfig), nameof(BabySquirrelHugConfig.CreatePrefab), mass: 0.05f),
            // Drecko Plastic
            new Entry(typeof(DreckoPlasticConfig), nameof(DreckoPlasticConfig.CreatePrefab), mass: 0.82f, maxMult: 1000f, addLink: true),
            new Entry(typeof(BabyDreckoPlasticConfig), nameof(BabyDreckoPlasticConfig.CreatePrefab), startMass: 1f, mass: 0.7f, maxMult: 1000f),

            // Pacu families
            new Entry(typeof(PacuConfig), nameof(PacuConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f, addLink: true),
            new Entry(typeof(BabyPacuConfig), nameof(BabyPacuConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f),

            new Entry(typeof(PacuTropicalConfig), nameof(PacuTropicalConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f, addLink: true),
            new Entry(typeof(BabyPacuTropicalConfig), nameof(BabyPacuTropicalConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f),

            new Entry(typeof(PacuCleanerConfig), nameof(PacuCleanerConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f, addLink: true),
           
            new Entry(typeof(BabyPacuCleanerConfig), nameof(BabyPacuCleanerConfig.CreatePrefab), startMass: 2f, mass: 0.5f, maxMult: 100f),

            // Longhair slickster (decor)
            new Entry(typeof(OilFloaterDecorConfig), nameof(OilFloaterDecorConfig.CreatePrefab), startMass: 1f, mass: 1f, maxMult: 8000f, addLink: true,
                      drops: new[] { new Drop("OxyRock", 0.5f), new Drop("Oxygen", 0.5f) }),
            new Entry(typeof(OilFloaterDecorBabyConfig), nameof(OilFloaterDecorBabyConfig.CreatePrefab), startMass: 1f, mass: 1f, maxMult: 8000f,
                      drops: new[] { new Drop("OxyRock", 0.5f), new Drop("Oxygen", 0.5f) }),
                 // Bee 
            new Entry(typeof(BeeConfig),  nameof(BeeConfig.CreatePrefab), startMass: 1f, mass: 1f, addLink: true,
                      drops: new[] { new Drop("SolidNuclearWaste", 1f) }),
            new Entry(typeof(BabyBeeConfig), nameof(BabyBeeConfig.CreatePrefab), startMass: 1f, mass: 1f,
                      drops: new[] { new Drop("SolidNuclearWaste", 1f) }),

            // Stego (uses CreateStego)
            new Entry(typeof(StegoConfig), "CreateStego", startMass: 4f, mass: 1f, addLink: true,
                      drops: new[] { new Drop("DinosaurMeat", 0.12f), new Drop("RotPile", 0.88f)  }),
            new Entry(typeof(BabyStegoConfig), nameof(BabyStegoConfig.CreatePrefab), startMass: 4f, mass: 1f,
                      drops: new[] { new Drop("DinosaurMeat", 0.12f),  new Drop("RotPile", 0.88f) }),
                 new Entry(typeof(AlgaeStegoConfig), "CreateStego", startMass: 4f, mass: 1f, addLink: true,
                      drops: new[] { new Drop("DinosaurMeat", 0.12f), new Drop("RotPile", 0.88f)  }),
            new Entry(typeof(BabyAlgaeStegoConfig), nameof(BabyAlgaeStegoConfig.CreatePrefab), startMass: 4f, mass: 1f,
                      drops: new[] { new Drop("DinosaurMeat", 0.12f),  new Drop("RotPile", 0.88f) }),
 //Morb
            new Entry(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab), startMass: 1f, mass: 1f, maxMult: 100f,
                      drops: new[] { new Drop("Slime", 0f) }),
             //Butterfly
            new Entry(typeof(ButterflyConfig), nameof(ButterflyConfig.CreatePrefab), startMass: 1f, mass: 1f, maxMult: 100f,
                      drops: new[] { new Drop("Shale", 1f) }),


            // WoodDeer and Belly are intentionally left OUT (custom logic remains in separate patches)
        };

        private static List<MethodBase> _targets;

        // Collect all target methods from both lists
        private static IEnumerable<MethodBase> TargetMethods()
        {
            _targets ??= new List<MethodBase>(CalorieEntries.Length + MassEntries.Length);
            _targets.Clear();

            void AddTargets(Entry[] entries)
            {
                foreach (var e in entries)
                {
                    var m = AccessTools.Method(e.configType, e.methodName);
                    if (m != null) _targets.Add(m);
                    else Debug.LogWarning($"[Rephysicalized] Could not resolve {e.configType?.Name}.{e.methodName} for mass list.");
                }
            }

            AddTargets(CalorieEntries);
            AddTargets(MassEntries);
            return _targets;
        }



        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod, ref GameObject __result)
        {
            if (__result == null || __originalMethod == null) return;

            // Find the matching entry and apply
            var entry = FindEntry(__originalMethod, CalorieEntries) ?? FindEntry(__originalMethod, MassEntries);
            if (entry == null) return;

            try
            {
                ApplyEntry(__result, entry);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] Mass tracker apply failed for {__originalMethod.DeclaringType?.Name}: {e}");
            }
        }

        private static Entry FindEntry(MethodBase m, Entry[] entries)
        {
            var dt = m.DeclaringType;
            var name = m.Name;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.configType == dt && string.Equals(e.methodName, name, StringComparison.Ordinal))
                    return e;
            }
            return null;
        }

        private static void ApplyEntry(GameObject go, Entry e)
        {
            var tracker = go.AddOrGet<CreatureMassTracker>();

            if (e.startMass.HasValue) tracker.STARTING_MASS = e.startMass.Value;
            if (e.maxScaleMultiple.HasValue) tracker.MAX_MASS_MULTIPLE_FOR_MAX_SCALE = e.maxScaleMultiple.Value;

            // Calorie vs Mass mode
            if (e.calorieRatio.HasValue)
            {
                tracker.CALORIE_RATIO = e.calorieRatio.Value;
            }
            else
            {
                // Mass mode (ConsumedMass)
                tracker.SetAccumulationMode(CreatureMassTracker.AccumulationMode.ConsumedMass, keepCurrentMassAsBaseline: false);
                if (e.massRatio.HasValue) tracker.MASS_RATIO = e.massRatio.Value;
            }

            // Extra drops
            if (e.drops != null && e.drops.Length > 0)
            {
                var list = new List<CreatureMassTracker.ExtraDropSpec>(e.drops.Length);
                for (int i = 0; i < e.drops.Length; i++)
                    list.Add(new CreatureMassTracker.ExtraDropSpec { id = e.drops[i].id, fraction = e.drops[i].fraction });

                tracker.ExtraDrops = list;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid != null)
                    CreatureMassTracker.RegisterDefaultDropsForPrefab(kpid.PrefabTag, list);
            }

            // Link component for adults or where needed
            if (e.addLink)
                go.AddOrGet<CreatureMassTrackerLink>();

         
        }
    }
}

