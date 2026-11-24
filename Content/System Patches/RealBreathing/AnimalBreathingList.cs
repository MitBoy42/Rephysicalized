using HarmonyLib;
using Rephysicalized;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches.RealBreathing
{
    internal class AnimalBreathingList
    {
        private sealed class CreatureConfig
        {
            public readonly System.Type Type;
            public readonly string PrefabId;
            public readonly bool IsBaby;
            public readonly float OxygenCapacityKg;
            public readonly float BreathingRateKgPerSecond;

            public CreatureConfig(System.Type type, string prefabId, bool isBaby, float oxygenCapacityKg = 1.2f, float breathingRateKgPerSecond = 0.002f)
            {
                Type = type;
                PrefabId = prefabId;
                IsBaby = isBaby;
                OxygenCapacityKg = oxygenCapacityKg;
                BreathingRateKgPerSecond = breathingRateKgPerSecond;
            }
        }

        private static readonly CreatureConfig[] Creatures =
        {
            new CreatureConfig(typeof(SquirrelConfig), SquirrelConfig.ID, false),
            new CreatureConfig(typeof(BabySquirrelConfig), BabySquirrelConfig.ID, true),
            new CreatureConfig(typeof(SquirrelHugConfig), SquirrelHugConfig.ID, false),
            new CreatureConfig(typeof(BabySquirrelHugConfig), BabySquirrelHugConfig.ID, true),
            new CreatureConfig(typeof(HatchConfig), HatchConfig.ID, false),
            new CreatureConfig(typeof(BabyHatchConfig), BabyHatchConfig.ID, true),
           new CreatureConfig(typeof(HatchHardConfig), HatchHardConfig.ID, false),
            new CreatureConfig(typeof(BabyHatchHardConfig), BabyHatchHardConfig.ID, true),
         new CreatureConfig(typeof(HatchMetalConfig), HatchMetalConfig.ID, false),
            new CreatureConfig(typeof(BabyHatchMetalConfig), BabyHatchMetalConfig.ID, true),
            new CreatureConfig(typeof(HatchVeggieConfig), HatchVeggieConfig.ID, false),
            new CreatureConfig(typeof(BabyHatchVeggieConfig), BabyHatchVeggieConfig.ID, true),
            new CreatureConfig(typeof(WoodDeerConfig), WoodDeerConfig.ID, false),
            new CreatureConfig(typeof(BabyWoodDeerConfig), BabyWoodDeerConfig.ID, true),
               new CreatureConfig(typeof(GlassDeerConfig), GlassDeerConfig.ID, false),
            new CreatureConfig(typeof(BabyGlassDeerConfig), BabyGlassDeerConfig.ID, true),
            new CreatureConfig(typeof(IceBellyConfig), IceBellyConfig.ID, false, 2.4f, 0.004f),
            new CreatureConfig(typeof(BabyIceBellyConfig), BabyIceBellyConfig.ID, true, 2.4f, 0.004f),
              new CreatureConfig(typeof(GoldBellyConfig), GoldBellyConfig.ID, false, 2.4f, 0.004f),
            new CreatureConfig(typeof(BabyGoldBellyConfig), BabyGoldBellyConfig.ID, true, 2.4f, 0.004f),
           new CreatureConfig(typeof(StegoConfig), StegoConfig.ID, false, 2.4f, 0.004f),
            new CreatureConfig(typeof(BabyStegoConfig), BabyStegoConfig.ID, true, 2.4f, 0.004f),
              new CreatureConfig(typeof(AlgaeStegoConfig), AlgaeStegoConfig.ID, false, 2.4f, 0.004f),
            new CreatureConfig(typeof(BabyAlgaeStegoConfig), BabyAlgaeStegoConfig.ID, true, 2.4f, 0.004f),
             new CreatureConfig(typeof(RaptorConfig), RaptorConfig.ID, false, 2.4f, 0.004f),
            new CreatureConfig(typeof(BabyRaptorConfig), BabyRaptorConfig.ID, true, 2.4f, 0.004f),

          new CreatureConfig(typeof(SnailConfig), SnailConfig.ID, false),
            new CreatureConfig(typeof(BabySnailConfig), BabySnailConfig.ID, true),
                      new CreatureConfig(typeof(SnailIronConfig), SnailIronConfig.ID, false),
            new CreatureConfig(typeof(BabySnailIronConfig), BabySnailIronConfig.ID, true),

          new CreatureConfig(typeof(CrabConfig), CrabConfig.ID, false),
            new CreatureConfig(typeof(BabyCrabConfig), BabyCrabConfig.ID, true),
             new CreatureConfig(typeof(CrabWoodConfig), CrabWoodConfig.ID, false),
            new CreatureConfig(typeof(BabyCrabWoodConfig), BabyCrabWoodConfig.ID, true),
              new CreatureConfig(typeof(CrabFreshWaterConfig), CrabFreshWaterConfig.ID, false),
            new CreatureConfig(typeof(BabyCrabFreshWaterConfig), BabyCrabFreshWaterConfig.ID, true),

                  new CreatureConfig(typeof(DivergentWormConfig), DivergentWormConfig.ID, false),
            new CreatureConfig(typeof(BabyWormConfig), BabyWormConfig.ID, true),

          new CreatureConfig(typeof(DivergentBeetleConfig), DivergentBeetleConfig.ID, false),
            new CreatureConfig(typeof(BabyDivergentBeetleConfig), BabyDivergentBeetleConfig.ID, true),


          new CreatureConfig(typeof(SealConfig), SealConfig.ID, false),
            new CreatureConfig(typeof(BabySealConfig), BabySealConfig.ID, true),


        };

        internal static bool IsBaby(KPrefabID prefabID)
        {
            return prefabID != null && Creatures.Any(creature =>
                creature.IsBaby && prefabID.HasTag((Tag)creature.PrefabId));
        }

        [HarmonyPatch]
        private static class AnimalBreathingPrefabPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return Creatures.Select(creature => AccessTools.Method(creature.Type, nameof(SquirrelConfig.CreatePrefab)));
            }

            [HarmonyPostfix]
            private static void Postfix(MethodBase __originalMethod, GameObject __result)
            {
                if (!Config.Instance.RealBreathing)
                    return;
                var creature = Creatures.FirstOrDefault(entry => entry.Type == __originalMethod.DeclaringType);
                if (creature != null)
                    AnimalBreathing.ConfigurePrefab(__result, seedInitialOxygen: !creature.IsBaby, oxygenCapacityKg: creature.OxygenCapacityKg, breathingRateKgPerSecond: creature.BreathingRateKgPerSecond);
            }
        }
    }
}
