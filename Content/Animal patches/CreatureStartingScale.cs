using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Helper to read/write ScaleGrowth amount as [0..1]
    internal static class ScaleAmountIO
    {
        public static AmountInstance Lookup(GameObject go) => Db.Get()?.Amounts?.ScaleGrowth?.Lookup(go);

        public static void SetPercent01(GameObject go, float pct01)
        {
            var amt = Lookup(go);
            if (amt == null) return;
            float max = amt.GetMax(); // typically 100
            amt.SetValue(Mathf.Clamp01(pct01) * max);
        }

        public static float GetPercent01(GameObject go)
        {
            var amt = Lookup(go);
            if (amt == null) return 0f;
            float max = Mathf.Max(1f, amt.GetMax());
            return Mathf.Clamp01(amt.value / max);
        }

        public static void ForceZero(GameObject go)
        {
            var amt = Lookup(go);
            if (amt == null) return;
            amt.SetValue(amt.GetMin());
        }
    }

    [HarmonyPatch(typeof(ScaleGrowthMonitor.Instance), MethodType.Constructor, new Type[] { typeof(IStateMachineTarget), typeof(ScaleGrowthMonitor.Def) })]
    public static class Patch_ScaleGrowthMonitor_Instance_Ctor_BabyZero
    {
        public static void Postfix(ScaleGrowthMonitor.Instance __instance)
        {
            var go = __instance?.gameObject; 
   

            var amt = Db.Get()?.Amounts?.ScaleGrowth?.Lookup(go);
        
                // Force 0% after ctor forced max
                amt.SetValue(amt.GetMin());
                // Optional: update visuals if available
               ScaleGrowthMonitor.UpdateScales(__instance, 0f); 
            
        }
    }
    [HarmonyPatch(typeof(WellFedShearable.Instance), MethodType.Constructor, new Type[] { typeof(IStateMachineTarget), typeof(WellFedShearable.Def) })]
    public static class Patch_WellFedShearable_Instance_Ctor_BabyZero
    {
        public static void Postfix(WellFedShearable.Instance __instance)
        {
            var go = __instance?.gameObject;


            var amt = Db.Get()?.Amounts?.ScaleGrowth?.Lookup(go);
         
                amt.SetValue(amt.GetMin());
               WellFedShearable.UpdateScales(__instance, 0f); 
            
        }
    }



        internal static class AdultScaleOverride
    {
        public static void CarryCurrent(GameObject adult)
        {
        

        }
    }


    // List of baby configs that use ScaleGrowth (scales)
    [HarmonyPatch(typeof(BabyDreckoPlasticConfig), nameof(BabyDreckoPlasticConfig.CreatePrefab))]
    public static class Patch_BabyDreckoPlasticConfig_AdultScale
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    [HarmonyPatch(typeof(BabyDreckoConfig), nameof(BabyDreckoConfig.CreatePrefab))]
    public static class Patch_BabyDreckoConfig_AdultScale
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    [HarmonyPatch(typeof(BabyGoldBellyConfig), nameof(BabyGoldBellyConfig.CreatePrefab))]
    public static class Patch_BabyGoldBellyConfig_AdultScale
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    [HarmonyPatch(typeof(BabyIceBellyConfig), nameof(BabyIceBellyConfig.CreatePrefab))]
    public static class Patch_BabyIceBellyConfig_AdultScale
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    [HarmonyPatch(typeof(BabyRaptorConfig), nameof(BabyRaptorConfig.CreatePrefab))]
    public static class Patch_BabyRaptorConfig_AdultScale
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    // List of baby configs that use antlers (WellFedShearable species)
    [HarmonyPatch(typeof(BabyWoodDeerConfig), nameof(BabyWoodDeerConfig.CreatePrefab))]
    public static class Patch_BabyWoodDeerConfig_AdultAntlers
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }

    [HarmonyPatch(typeof(BabyGlassDeerConfig), nameof(BabyGlassDeerConfig.CreatePrefab))]
    public static class Patch_BabyGlassDeerConfig_AdultAntlers
    {
        public static void Postfix(GameObject __result)
        {
            if (!Config.Instance.StartWithZeroScale) return;
            var def = __result.AddOrGetDef<BabyMonitor.Def>();
            def.configureAdultOnMaturation = AdultScaleOverride.CarryCurrent;
        }
    }


}