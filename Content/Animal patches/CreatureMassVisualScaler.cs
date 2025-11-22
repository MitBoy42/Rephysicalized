using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Visuals
{
    // Universal non-stacking visual scaler:
    // - Baseline scale: prefab's vanilla transform.localScale.
    // - Baseline mass: tracker.GetVisualBaselineMass() or prefab PE mass if tracker not ready.
    // - Applies absolute scale = prefabScale * TargetScale(mass/baselineMass).
    // - Anchors bottom via KBatchedAnimController.Offset using pre/post bounds; never moves transform position.
    public sealed class CreatureVisualScaler : KMonoBehaviour
    {
        [MyCmpGet] private KBatchedAnimController anim;
        [MyCmpGet] private PrimaryElement primary;
        private Rephysicalized.CreatureMassTracker tracker;

        // Cache
        private KPrefabID kpid;
        private Vector3 prefabScaleCache = Vector3.zero;
        private float baselineMassCache = 0f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            tracker = gameObject.GetComponent<Rephysicalized.CreatureMassTracker>();
            kpid = GetComponent<KPrefabID>();
            ApplyScaleForCurrentMass(); // correct on load/deserialization
        }

        public void OnMassChanged()
        {
            ApplyScaleForCurrentMass();
        }

        private void ApplyScaleForCurrentMass()
        {
            if (primary == null) primary = GetComponent<PrimaryElement>();
            if (tracker == null) tracker = GetComponent<Rephysicalized.CreatureMassTracker>();
            if (kpid == null) kpid = GetComponent<KPrefabID>();

            if (primary == null || tracker == null || !tracker.ENABLE_VISUAL_SCALING)
                return;

            // Resolve prefab (vanilla) scale once
            if (prefabScaleCache == Vector3.zero)
                prefabScaleCache = ResolvePrefabScale(kpid);

            // Resolve baseline mass (prefer tracker snapshot, else prefab PE mass, else current mass)
            if (!(baselineMassCache > 0f))
                baselineMassCache = ResolveBaselineMass(tracker, kpid, primary);

            float targetScale = ComputeTargetVisualScale(tracker, primary.Mass, baselineMassCache, tracker.MAX_MASS_MULTIPLE_FOR_MAX_SCALE, tracker.SCALE_AT_START_MASS, tracker.SCALE_AT_100X_MASS);

            if (anim == null) anim = GetComponent<KBatchedAnimController>();
            float bottomBefore = 0f;
            bool canAnchor = tracker.ANCHOR_DOWNWARD && anim != null && TryGetBoundsBottom(anim, out bottomBefore);

            // Absolute, non-stacking application
            transform.localScale = new Vector3(
                prefabScaleCache.x * targetScale,
                prefabScaleCache.y * targetScale,
                prefabScaleCache.z
            );

            if (canAnchor && TryGetBoundsBottom(anim, out float bottomAfter))
            {
                float dy = bottomBefore - bottomAfter;
                if (Mathf.Abs(dy) > 1e-5f)
                {
                    var off = anim.Offset;
                    anim.Offset = new Vector3(off.x, off.y + dy, off.z);
                }
            }
        }

        private static float ComputeTargetVisualScale(Rephysicalized.CreatureMassTracker tracker, float currentMass, float baselineMass, float maxMultiple, float scaleAtStart, float scaleAtMax)
        {
            float start = Mathf.Max(0.001f, baselineMass);
            float mass = Mathf.Max(0.001f, currentMass);

            float maxMult = Mathf.Max(1.0001f, maxMultiple);
            float multiple = Mathf.Clamp(mass / start, 1f, maxMult);

            float denom = Mathf.Max(0.0001f, maxMult - 1f);
            float t = (multiple - 1f) / denom;
            return Mathf.Lerp(scaleAtStart, scaleAtMax, Mathf.Clamp01(t));
        }

        private static Vector3 ResolvePrefabScale(KPrefabID kpid)
        {
            try
            {
                if (kpid != null && kpid.PrefabTag.IsValid)
                {
                    var prefab = Assets.GetPrefab(kpid.PrefabTag);
                    if (prefab != null)
                        return prefab.transform.localScale;
                }
            }
            catch { }
            return Vector3.one;
        }

        private static float ResolveBaselineMass(Rephysicalized.CreatureMassTracker tracker, KPrefabID kpid, PrimaryElement currentPE)
        {
            // Prefer the instance's prefab mass snapshot (captured at its first spawn)
            float m = tracker != null ? tracker.GetVisualBaselineMass() : 0f;
            if (m > 0f)
                return m;

            // Fallback to prefab PrimaryElement mass
            try
            {
                if (kpid != null && kpid.PrefabTag.IsValid)
                {
                    var prefab = Assets.GetPrefab(kpid.PrefabTag);
                    var pe = prefab != null ? prefab.GetComponent<PrimaryElement>() : null;
                    if (pe != null && pe.Mass > 0f)
                        return pe.Mass;
                }
            }
            catch { }

            // Final fallback: current mass
            return Mathf.Max(0.001f, currentPE != null ? currentPE.Mass : 1f);
        }

        private static bool TryGetBoundsBottom(KBatchedAnimController a, out float bottomY)
        {
            bottomY = 0f;
            try
            {
                var b = a.bounds;
                if (b.size.sqrMagnitude > 0f)
                {
                    bottomY = b.min.y;
                    return true;
                }
            }
            catch { }
            return false;
        }
    }

    // Hooks to apply scaler universally:
    // - When the tracker spawns (covers existing creatures loaded from save)
    // - On any mass write (runtime updates)
    // - On prefab spawn/deserialization
    [HarmonyPatch(typeof(Rephysicalized.CreatureMassTracker), nameof(Rephysicalized.CreatureMassTracker.OnSpawn))]
    internal static class CreatureMassTracker_OnSpawn_AttachScaler
    {
        [HarmonyPostfix]
        private static void Postfix(Rephysicalized.CreatureMassTracker __instance)
        {
            if (__instance == null || !__instance.ENABLE_VISUAL_SCALING)
                return;

            var go = __instance.gameObject;
            var scaler = go.AddOrGet<Rephysicalized.Visuals.CreatureVisualScaler>();
            scaler.OnMassChanged();
        }
    }

    [HarmonyPatch(typeof(PrimaryElement), "set_Mass")]
    internal static class PrimaryElement_set_Mass_NotifyVisualScaler
    {
        [HarmonyPostfix]
        private static void Postfix(PrimaryElement __instance)
        {
            if (__instance == null)
                return;

            var go = __instance.gameObject;
            var tracker = go.GetComponent<Rephysicalized.CreatureMassTracker>();
            if (tracker != null && tracker.ENABLE_VISUAL_SCALING)
            {
                var scaler = go.AddOrGet<Rephysicalized.Visuals.CreatureVisualScaler>();
                scaler.OnMassChanged();
            }
        }
    }

    [HarmonyPatch(typeof(KPrefabID), nameof(KPrefabID.OnSpawn))]
    internal static class KPrefabID_OnSpawn_AttachScaler
    {
        [HarmonyPostfix]
        private static void Postfix(KPrefabID __instance)
        {
            if (__instance == null)
                return;

            var go = __instance.gameObject;
            var tracker = go.GetComponent<Rephysicalized.CreatureMassTracker>();
            if (tracker != null && tracker.ENABLE_VISUAL_SCALING)
            {
                var scaler = go.AddOrGet<Rephysicalized.Visuals.CreatureVisualScaler>();
                scaler.OnMassChanged();
            }
        }
    }
}