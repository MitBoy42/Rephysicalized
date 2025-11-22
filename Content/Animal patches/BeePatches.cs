using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.AnimalPatches
{
    // 1) Adjust weapon damage on adult Bee
    [HarmonyPatch(typeof(BaseBeeConfig), nameof(BaseBeeConfig.BaseBee))]
    public static class BaseBee_Weapon_Postfix
    {
        static void Postfix(GameObject __result, bool is_baby)
        {
            if (__result == null || is_baby) return;

            var weapon = __result.GetComponent<Weapon>();
            if (weapon == null)
            {
                weapon = __result.AddComponent<Weapon>();
                weapon.Configure(30f, 40f);
                return;
            }

            if (weapon.properties == null)
            {
                weapon.Configure(30f, 40f);
            }
            else
            {
                weapon.properties.base_damage_min = 30f;
                weapon.properties.base_damage_max = 40f;
                weapon.properties.attacker = weapon;
            }
        }
    }

    // 2) Tune Bee CO2 consumption rate (root and children)
    [HarmonyPatch(typeof(BaseBeeConfig), nameof(BaseBeeConfig.BaseBee))]
    internal static class BaseBeeConfig_BaseBee_Postfix
    {
        public static void Postfix(ref GameObject __result, bool is_baby)
        {
 

            const float desiredRate = 0.05f;

            // Root ElementConsumer (if any)
            var ec = __result.GetComponent<ElementConsumer>();
            if (ec != null && ec.elementToConsume == SimHashes.CarbonDioxide)
            {
                ec.consumptionRate = desiredRate;
                ec.RefreshConsumptionRate();
            }

            // Child ElementConsumers as well
            var ecs = __result.GetComponentsInChildren<ElementConsumer>(true);
            for (int i = 0; i < ecs.Length; i++)
            {
                var e = ecs[i];
                if (e != null && e.elementToConsume == SimHashes.CarbonDioxide)
                {
                    e.consumptionRate = desiredRate;
                    e.RefreshConsumptionRate();
                }
            }
        }
    }

    // 3) After ElementConsumer.AddMassInternal consumes CO2, add that mass to the Bee
    //    - If tracker is in ConsumedMass mode, use ReportConsumedMass so MASS_RATIO applies.
    //    - Otherwise, bump PrimaryElement.Mass directly.
    [HarmonyPatch]
    internal static class ElementConsumer_AddMassInternal_BeeCO2MassGain_Postfix
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            var t = typeof(ElementConsumer);
            var m = AccessTools.Method(t, "AddMassInternal");
            if (m == null) m = AccessTools.Method(t, "AddMass");
            return m;
        }

        public static void Postfix(ElementConsumer __instance, Sim.ConsumedMassInfo consumed_info)
        {
            

                if (__instance.elementToConsume != SimHashes.CarbonDioxide)
                    return;

                float mass = consumed_info.mass;
                if (!(mass > 0f))
                    return;

                // Resolve Bee root via Bee component in parent chain
                var bee = __instance.GetComponentInParent<Bee>();
                if (bee == null || bee.Equals(null))
                    return;

                var root = bee.gameObject;
                if (root == null || root.Equals(null))
                    return;

                var tracker = root.GetComponent<CreatureMassTracker>();
                if (tracker != null && tracker.Mode == CreatureMassTracker.AccumulationMode.ConsumedMass)
                {
                    tracker.ReportConsumedMass(mass);
                }
                else
                {
                    var pe = root.GetComponent<PrimaryElement>();
                    if (pe != null && !pe.Equals(null))
                        pe.Mass = Mathf.Max(0.001f, pe.Mass + mass);
                }
        
        }
    }

    // 4) On death, inject extra NuclearWaste so total drop equals PrimaryElement mass.
    //    Vanilla Bee.OnDeath adds a fixed 1 kg; we add (PE.Mass - 1) beforehand.
    [HarmonyPatch(typeof(Bee), nameof(Bee.OnDeath), new System.Type[] { typeof(object) })]
    internal static class Bee_OnDeath_ExtraWaste_Prefix
    {
        [HarmonyPrefix]
        private static void Prefix(Bee __instance, object data)
        {
 

            var storage = __instance.GetComponent<Storage>();
            var pe = __instance.GetComponent<PrimaryElement>();
            if (storage == null || pe == null) return;

            float extraKg = Mathf.Max(0f, pe.Mass - 1f);
            if (extraKg <= 0f)
                return;

            storage.AddOre(SimHashes.NuclearWaste, extraKg, pe.Temperature, byte.MaxValue, 0);
        }
    }
}