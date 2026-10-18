using HarmonyLib;
using Klei.AI;
using UnityEngine;
using Rephysicalized.Content.Building_patches.Boosted_Buildings;

namespace Rephysicalized.Content.Building_patches.Fabricators
{
    [HarmonyPatch(typeof(ArcadeMachineConfig), nameof(ArcadeMachineConfig.ConfigureBuildingTemplate))]
    internal static class ArcadeMachineConfig_BoostedBuilding_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            BoostedBuildings.Configure(go, new BoostedBuildings.Configuration
            {
                ItemTag = (Tag)DatabankHelper.ID,
                RequiredAmount = 1f,
                StorageCapacity = 100f,
                RefillMass = 5f,
                BoostedAnimations = new[] { Assets.GetAnim("arcade_cabinet_boosted_kanim") },
                EnableButtonText = Rephysicalized.STRINGS.BUILDINGS.ARCADECABINET.BOOSTBUTTON,
                EnableButtonTooltip = Rephysicalized.STRINGS.BUILDINGS.ARCADECABINET.BOOSTBUTTONDESC,
                DisableButtonText = Rephysicalized.STRINGS.BUILDINGS.ARCADECABINET.BOOSTBUTTONOFF,
                DisableButtonTooltip = Rephysicalized.STRINGS.BUILDINGS.ARCADECABINET.BOOSTBUTTONDESCOFF
            });
        }
    }

    [HarmonyPatch(typeof(ArcadeMachineWorkable), "OnCompleteWork")]
    internal static class ArcadeMachineWorkable_OnCompleteWork_BoostedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ArcadeMachineWorkable __instance, WorkerBase worker)
        {
            BoostedBuildings booster = __instance.owner.GetComponent<BoostedBuildings>();
            if (booster == null || !booster.TryConsumeRequiredItems())
                return true;

            Effects effects = worker.GetComponent<Effects>();
            effects.Add("RecentlyPlayedArcade", true);
            effects.Add("PlayedBoostedArcade", true);
            return false;
        }
    }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class PlayedBoostedArcadeEffect_Patch
    {
        private static bool effectAdded;

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (effectAdded)
                return;

            effectAdded = true;
            Effect effect = new Effect(
                "PlayedBoostedArcade",
                Rephysicalized.STRINGS.DUPLICANTS.PLAYEDBOOSTEDARCADE.NAME,
                Rephysicalized.STRINGS.DUPLICANTS.PLAYEDBOOSTEDARCADE.TOOLTIP,
                600f,
                true,
                true,
                false);
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.QualityOfLife.Id,
                6f,
                Rephysicalized.STRINGS.DUPLICANTS.PLAYEDBOOSTEDARCADE.NAME));
            Db.Get().effects.Add(effect);
        }
    }
}
