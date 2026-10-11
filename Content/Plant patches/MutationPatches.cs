using Database;
using HarmonyLib;
using Klei.AI;
using System;

namespace Rephysicalized.Content.Plant_patches
{

    [HarmonyPatch(typeof(PlantMutations))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(ResourceSet) })]
    public static class PlantMutationsMinRadiationPatch
    {
        private const float VanillaMinRadiation = 250f;

        public static void Postfix(PlantMutations __instance)
        {
            if (__instance == null)
                return;

            SetMinRadiation(__instance.moderatelyLoose, VanillaMinRadiation - 200f);
            SetMinRadiation(__instance.moderatelyTight, VanillaMinRadiation - 150f);
            SetMinRadiation(__instance.extremelyTight, VanillaMinRadiation - 100f);
            SetMinRadiation(__instance.bonusLice, VanillaMinRadiation - 200f);
            SetMinRadiation(__instance.sunnySpeed, VanillaMinRadiation - 150f);
            SetMinRadiation(__instance.blooms, VanillaMinRadiation - 250f);
            SetMinRadiation(__instance.loadedWithFruit, VanillaMinRadiation - 150f);
            SetMinRadiation(__instance.slowBurn, VanillaMinRadiation - 200f);
            SetMinRadiation(__instance.heavyFruit, VanillaMinRadiation - 150f);

            // rottenHeaps intentionally keeps vanilla''s 250 rads.
        }

 
        private static void SetMinRadiation(PlantMutation mutation, float value)
        {
            if (mutation == null)
                return;

            var attribute = Db.Get().PlantAttributes.MinRadiationThreshold;

            mutation.SelfModifiers.RemoveAll(modifier => modifier.AttributeId == attribute.Id);
            mutation.AttributeModifier(attribute, value);
        }
    }
}
