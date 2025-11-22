using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
    // Registers PinkRock and PinkRockCarved emitters as "pink" and forces a corrective re-add
    // so the limiter applies immediately.
    public sealed class PinkLightRegistrar : KMonoBehaviour
    {
        [MyCmpReq] private Light2D light2D;
        private LightGridManager.LightGridEmitter emitter;
        private bool registered;

        public override void OnSpawn()
        {
            base.OnSpawn();

                emitter = light2D?.emitter; // use the public property (confirmed by your Light2D.cs)
            
                PinkLightLimiter.RegisterEmitter(emitter);
                registered = true;

          
                PinkLightLimiter.MarkCorrectionPending(emitter);
                emitter.RemoveFromGrid();
                light2D.FullRefresh();

        }

        public override void OnCleanUp()
        {
            if (registered && emitter != null)
            {
                PinkLightLimiter.UnregisterEmitter(emitter);
            }
            base.OnCleanUp();
        }
    }

    internal static class PinkLightLimiter
    {
        // Pink emitters registry (keyed by emitter instance)
        private static readonly HashSet<LightGridManager.LightGridEmitter> PinkEmitters
            = new HashSet<LightGridManager.LightGridEmitter>(ReferenceEqualityComparer<LightGridManager.LightGridEmitter>.Instance);

        // Emitters that already added vanilla lux prior to registration; needs one full vanilla removal pass
        private static readonly HashSet<LightGridManager.LightGridEmitter> CorrectionPending
            = new HashSet<LightGridManager.LightGridEmitter>(ReferenceEqualityComparer<LightGridManager.LightGridEmitter>.Instance);

        // Per-emitter per-cell contribution tracking
        private static readonly Dictionary<LightGridManager.LightGridEmitter, Dictionary<int, int>> Contributions
            = new Dictionary<LightGridManager.LightGridEmitter, Dictionary<int, int>>(ReferenceEqualityComparer<LightGridManager.LightGridEmitter>.Instance);

        // Per-cell accumulation strictly from Pink emitters
        private static int[] pinkAccum;

        // Cached reflection into LightGridEmitter.State for base lux computation
        private static FieldInfo s_state_fi;
        private static FieldInfo s_state_origin_fi;
        private static FieldInfo s_state_shape_fi;
        private static FieldInfo s_state_direction_fi;
        private static FieldInfo s_state_intensity_fi;
        private static FieldInfo s_state_falloff_fi;

        internal enum Op { None, Add, Remove }
        internal static Op CurrentOp = Op.None;

        internal static void InitGridArrays()
        {
      
                pinkAccum = new int[Grid.CellCount];
        }

        internal static void ClearAll()
        {
            PinkEmitters.Clear();
            CorrectionPending.Clear();
            Contributions.Clear();
            pinkAccum = null;
        }

        internal static void RegisterEmitter(LightGridManager.LightGridEmitter emitter)
        {
            PinkEmitters.Add(emitter);
            EnsureStateReflection();
        }

        internal static void UnregisterEmitter(LightGridManager.LightGridEmitter emitter)
        {
            PinkEmitters.Remove(emitter);
            CorrectionPending.Remove(emitter);
            Contributions.Remove(emitter);
        }

        internal static bool IsPinkEmitter(object obj)
            => obj is LightGridManager.LightGridEmitter e && PinkEmitters.Contains(e);

        internal static void MarkCorrectionPending(LightGridManager.LightGridEmitter emitter)
            => CorrectionPending.Add(emitter);

        internal static bool IsCorrectionPending(object obj)
            => obj is LightGridManager.LightGridEmitter e && CorrectionPending.Contains(e);

        internal static void ClearCorrectionPending(object obj)
        {
            if (obj is LightGridManager.LightGridEmitter e)
                CorrectionPending.Remove(e);
        }

        internal static int ComputeBaseLux(object emitterObj, int cell)
        {
            if (pinkAccum == null || s_state_fi == null)
                return 0;

            var state = s_state_fi.GetValue(emitterObj);
            if (state == null) return 0;

            int origin = (int)s_state_origin_fi.GetValue(state);
            int intensity = (int)s_state_intensity_fi.GetValue(state);
            float falloff = (float)s_state_falloff_fi.GetValue(state);
            var shape = (LightShape)s_state_shape_fi.GetValue(state);
            var dir = (DiscreteShadowCaster.Direction)s_state_direction_fi.GetValue(state);

            int denom = LightGridManager.ComputeFalloff(falloff, cell, origin, shape, dir);
            if (denom <= 0) denom = 1;

            return intensity / denom;
        }

        internal static int TakeAddContribution(object emitterObj, int cell, int baseLux)
        {
            if (pinkAccum == null) return baseLux;

            int accum = pinkAccum[cell];
            int allowed = Mathf.Max(0, 2000 - accum);
            int contrib = Mathf.Min(baseLux, allowed);
            if (contrib <= 0) return 0;

            pinkAccum[cell] = accum + contrib;

            var emitter = (LightGridManager.LightGridEmitter)emitterObj;
            if (!Contributions.TryGetValue(emitter, out var perCell))
            {
                perCell = new Dictionary<int, int>();
                Contributions[emitter] = perCell;
            }

            // accumulate per-emitter per-cell contributions (handles repeated adds)
            perCell[cell] = perCell.TryGetValue(cell, out var prev) ? prev + contrib : contrib;

            return contrib;
        }

        internal static int TakeRemoveContribution(object emitterObj, int cell, int fallbackBaseLux)
        {
            var emitter = (LightGridManager.LightGridEmitter)emitterObj;

            if (!Contributions.TryGetValue(emitter, out var perCell))
            {
                // First correction pass after registration: subtract full vanilla lux once
                if (IsCorrectionPending(emitter))
                    return Mathf.Max(0, fallbackBaseLux);

                return 0;
            }

            if (!perCell.TryGetValue(cell, out var contrib))
                contrib = 0;

            if (contrib > 0 && pinkAccum != null)
                pinkAccum[cell] = Mathf.Max(0, pinkAccum[cell] - contrib);

            // Clear stored cell contribution (this emitter will re-add if still active)
            if (perCell.ContainsKey(cell))
                perCell.Remove(cell);

            return contrib;
        }

        private static void EnsureStateReflection()
        {
            if (s_state_fi != null) return;

            var emitterType = typeof(LightGridManager).GetNestedType("LightGridEmitter", BindingFlags.Public | BindingFlags.NonPublic);
            s_state_fi = AccessTools.Field(emitterType, "state");

            var stateType = typeof(LightGridManager.LightGridEmitter.State);
            s_state_origin_fi = AccessTools.Field(stateType, "origin");
            s_state_shape_fi = AccessTools.Field(stateType, "shape");
            s_state_direction_fi = AccessTools.Field(stateType, "direction");
            s_state_intensity_fi = AccessTools.Field(stateType, "intensity");
            s_state_falloff_fi = AccessTools.Field(stateType, "falloffRate");
        }

        // Reference-equality comparer to use emitters as dictionary keys without overrides
        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    // Allocate/clear our pink accumulation buffer with the grid
    [HarmonyPatch(typeof(LightGridManager), nameof(LightGridManager.Initialise))]
    internal static class LightGridManager_Initialise_Patch
    {
        private static void Postfix()
        {
            PinkLightLimiter.InitGridArrays();
        }
    }

    [HarmonyPatch(typeof(LightGridManager), nameof(LightGridManager.Shutdown))]
    internal static class LightGridManager_Shutdown_Patch
    {
        private static void Postfix()
        {
            PinkLightLimiter.ClearAll();
        }
    }

    // Mark operation context so ComputeLux knows whether we're adding or removing
    [HarmonyPatch(typeof(LightGridManager.LightGridEmitter), nameof(LightGridManager.LightGridEmitter.AddToGrid))]
    internal static class LightGridEmitter_AddToGrid_Patch
    {
        private static void Prefix()
        {
            PinkLightLimiter.CurrentOp = PinkLightLimiter.Op.Add;
        }

        private static void Postfix(object __instance)
        {
            // After first add finishes, clear correction flag
            PinkLightLimiter.ClearCorrectionPending(__instance);
            PinkLightLimiter.CurrentOp = PinkLightLimiter.Op.None;
        }
    }

    [HarmonyPatch(typeof(LightGridManager.LightGridEmitter), nameof(LightGridManager.LightGridEmitter.RemoveFromGrid))]
    internal static class LightGridEmitter_RemoveFromGrid_Patch
    {
        private static void Prefix()
        {
            PinkLightLimiter.CurrentOp = PinkLightLimiter.Op.Remove;
        }

        private static void Postfix()
        {
            PinkLightLimiter.CurrentOp = PinkLightLimiter.Op.None;
        }
    }

    // Cap per-cell lux contribution for Pink emitters; leave others vanilla
    [HarmonyPatch(typeof(LightGridManager.LightGridEmitter), "ComputeLux")]
    internal static class LightGridEmitter_ComputeLux_Patch
    {
        private static bool Prefix(object __instance, int cell, ref int __result)
        {
            if (!PinkLightLimiter.IsPinkEmitter(__instance))
                return true; // non-pink: run vanilla

            int baseLux = PinkLightLimiter.ComputeBaseLux(__instance, cell);

            if (PinkLightLimiter.CurrentOp == PinkLightLimiter.Op.Add)
            {
                __result = PinkLightLimiter.TakeAddContribution(__instance, cell, baseLux);
                return false;
            }
            if (PinkLightLimiter.CurrentOp == PinkLightLimiter.Op.Remove)
            {
                __result = PinkLightLimiter.TakeRemoveContribution(__instance, cell, baseLux);
                return false;
            }

            // Unknown context: return vanilla base to be safe
            __result = baseLux;
            return false;
        }
    }

    [HarmonyPatch(typeof(PinkRockConfig), nameof(PinkRockConfig.CreatePrefab))]
    internal static class PinkRockConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {
            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();
        
                pe.SetElement(SimHashes.Granite);

            __result.AddOrGet<PinkLightRegistrar>();
        }
    }

    [HarmonyPatch(typeof(PinkRockCarvedConfig), nameof(PinkRockCarvedConfig.CreatePrefab))]
    internal static class PinkRockCarvedConfig_CreatePrefab_Postfix
    {
        private static void Postfix(GameObject __result)
        {

            var pe = __result.GetComponent<PrimaryElement>() ?? __result.AddComponent<PrimaryElement>();
      
                pe.SetElement(SimHashes.Granite);
                pe.Mass = 25f;
         
            __result.AddOrGet<PinkLightRegistrar>();
        }
    }

    [HarmonyPatch(typeof(EntityTemplates))]
    internal static class EntityTemplates_CreatePlacedEntity_PinkRockCarved
    {
        // Patch all overloads named "CreatePlacedEntity"
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(EntityTemplates)))
            {
                if (m.Name == "CreatePlacedEntity")
                    yield return m;
            }
        }

        // In most overloads, the first string is id and the first float is mass
        public static void Prefix(string id, ref float mass)
        {
            if (string.Equals(id, "PinkRockCarved", StringComparison.Ordinal))
            {
                mass = 25f;
            }
        }
    }

    // Ensure mass = 25f when PinkRockCarved is created as a loose entity (safety for alt builds)
    [HarmonyPatch(typeof(EntityTemplates))]
    internal static class EntityTemplates_CreateLooseEntity_PinkRockCarved
    {
        // Match the commonly used CreateLooseEntity overload
        public static MethodBase TargetMethod()
        {
            var types = new Type[] {
                typeof(string), typeof(string), typeof(string), typeof(float), typeof(bool),
                typeof(KAnimFile), typeof(string), typeof(Grid.SceneLayer),
                typeof(EntityTemplates.CollisionShape), typeof(float), typeof(float),
                typeof(bool), typeof(int), typeof(SimHashes), typeof(List<Tag>)
            };
            return AccessTools.Method(typeof(EntityTemplates), "CreateLooseEntity", types);
        }

        public static void Prefix(string id, ref float mass /*, ref bool unitMass */)
        {
            if (string.Equals(id, "PinkRockCarved", StringComparison.Ordinal))
            {
                mass = 25f;
            }
        }
    }

}
