using HarmonyLib;
using Klei.AI;
using UnityEngine;
using Rephysicalized.Content.Building_patches.Boosted_Buildings;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(PhonoboxConfig), nameof(PhonoboxConfig.ConfigureBuildingTemplate))]
    internal static class PhonoboxConfig_BoostedBuilding_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            BoostedBuildings.Configure(go, new BoostedBuildings.Configuration
            {
                ItemTag = (Tag)DatabankHelper.ID,
                RequiredAmount = 1f,
                StorageCapacity = 150f,
                RefillMass = 5f,
                BoostedAnimations = new[] { Assets.GetAnim("jukebot_boosted_kanim") },
                EnableButtonText = STRINGS.BUILDINGS.PHONOBOX.BOOSTBUTTON,
                EnableButtonTooltip = STRINGS.BUILDINGS.PHONOBOX.BOOSTBUTTONDESC,
                DisableButtonText = STRINGS.BUILDINGS.PHONOBOX.BOOSTBUTTONOFF,
                DisableButtonTooltip = STRINGS.BUILDINGS.PHONOBOX.BOOSTBUTTONDESCOFF
            });
        }
    }

    [HarmonyPatch(typeof(PhonoboxWorkable), "OnCompleteWork")]
    internal static class PhonoboxWorkable_OnCompleteWork_BoostedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PhonoboxWorkable __instance, WorkerBase worker)
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
    internal static class PlayedBoostedPhonoboxEffect_Patch
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
                STRINGS.DUPLICANTS.BOOSTEDDANCED.NAME,
                STRINGS.DUPLICANTS.BOOSTEDDANCED.TOOLTIP,
                600f,
                true,
                true,
                false);
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.QualityOfLife.Id,
                4f,
                STRINGS.DUPLICANTS.BOOSTEDDANCED.NAME));
            effect.Add(new AttributeModifier(
                Db.Get().Attributes.Athletics.Id,
                1f,
                STRINGS.DUPLICANTS.BOOSTEDDANCED.NAME));
            Db.Get().effects.Add(effect);
        }
    }
}
