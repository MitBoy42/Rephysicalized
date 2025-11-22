using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    // Capture all Util.KInstantiate overloads that return GameObject so we can know
    // which GOs are spawned during Crop.SpawnSomeFruit.
    [HarmonyPatch]
    internal static class Util_KInstantiate_CaptureForCropSpawn
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Util))
                .Where(m => m.Name == "KInstantiate" && typeof(GameObject).IsAssignableFrom(m.ReturnType));
        }

        private static void Postfix(GameObject __result)
        {
            if (__result == null) return;
            Crop_SpawnSomeFruit_ButterflyMassPatch.CaptureIfActive(__result);
        }
    }

    // Handles transferring mass from ButterflyPlant to spawned Butterfly,
    // and updates the PlantMassTrackerComponent so it won't override the change.
    [HarmonyPatch(typeof(Crop), nameof(Crop.SpawnSomeFruit))]
    internal static class Crop_SpawnSomeFruit_ButterflyMassPatch
    {
        private static readonly Tag ButterflyTag = new Tag("Butterfly");
        private static readonly Tag ButterflyPlantTag = new Tag("ButterflyPlant");
        private const float TargetPlantMass = 1f;

        // Thread-local capture stack to support nested calls
        [ThreadStatic] private static Stack<List<GameObject>> captureStack;

        internal static bool IsCapturing => captureStack != null && captureStack.Count > 0;

        internal static void CaptureIfActive(GameObject go)
        {
            if (IsCapturing)
                captureStack.Peek().Add(go);
        }

        private static void Prefix()
        {
            (captureStack ??= new Stack<List<GameObject>>()).Push(new List<GameObject>(4));
        }

        private static void Postfix(Crop __instance, Tag cropID)
        {
            List<GameObject> captured = null;
            try
            {
                captured = (captureStack != null && captureStack.Count > 0) ? captureStack.Pop() : null;
                if (__instance == null || captured == null || captured.Count == 0)
                    return;

                // Ensure this is the Butterfly plant and it spawned Butterfly
                var plantKpid = __instance.GetComponent<KPrefabID>();
                if (plantKpid == null || plantKpid.PrefabTag != ButterflyPlantTag)
                    return;

                if (!cropID.Equals(ButterflyTag))
                    return;

                // Prefer captured object whose PrefabTag matches cropID
                GameObject spawned = captured.FirstOrDefault(go =>
                {
                    var kpid = go != null ? go.GetComponent<KPrefabID>() : null;
                    return kpid != null && kpid.PrefabTag == cropID;
                }) ?? captured.LastOrDefault();

                if (spawned == null)
                    return;

                TransferMassToSpawn(__instance, spawned);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized] Crop_SpawnSomeFruit_ButterflyMassPatch.Postfix error: {e}");
            }
            finally
            {
                if (captureStack != null && captureStack.Count == 0)
                    captureStack.TrimExcess();
            }
        }

        private static void TransferMassToSpawn(Crop plantCrop, GameObject spawned)
        {
            var plantPE = plantCrop.GetComponent<PrimaryElement>();
            var spawnPE = spawned.GetComponent<PrimaryElement>();
            if (plantPE == null || spawnPE == null)
                return;

            // Amount of mass we want to move off the plant (down to 1 kg)
            float massToTransfer = plantPE.Mass - TargetPlantMass;
            if (massToTransfer <= 0f)
                return;

            // 1) Clamp plant PE mass to baseline 1 kg
            plantPE.Mass = TargetPlantMass;

            // 2) Add removed mass to the spawned butterfly (prefer AddMass if available)
            bool added = false;
            try
            {
                var addMass = AccessTools.Method(typeof(PrimaryElement), "AddMass", new[] { typeof(float), typeof(float) });
                if (addMass != null)
                {
                    addMass.Invoke(spawnPE, new object[] { massToTransfer, plantPE.Temperature });
                    added = true;
                }
            }
            catch { /* fall back below */ }

            if (!added)
                spawnPE.Mass += massToTransfer;

            // 3) Adjust the plant mass tracker so it doesn’t re-apply the removed mass.
            // Use AddMassDelta with a delta that brings tracked mass down by the same amount, clamped to >= 1 kg.
            var pmt = plantCrop.GetComponent<Rephysicalized.PlantMassTrackerComponent>();
            if (pmt != null)
            {
                float currentTracked = pmt.TrackedMassKg; // public getter
                float desiredTracked = Mathf.Max(TargetPlantMass, currentTracked - massToTransfer);
                float delta = desiredTracked - currentTracked; // typically negative
                if (Mathf.Abs(delta) > 0.0001f)
                    pmt.AddMassDelta(delta); // this also applies the visual if DebugExposeMassOnPrimaryElement is true
            }

#if DEBUG
            var plantKpid = plantCrop.GetComponent<KPrefabID>();
            var spawnKpid = spawned.GetComponent<KPrefabID>();
            Debug.Log($"[Rephysicalized] Transferred {massToTransfer:F3} kg from {(plantKpid?.PrefabTag.ToString() ?? "plant")} to {(spawnKpid?.PrefabTag.ToString() ?? "spawn")}, plant set to 1 kg, tracker synced.");
#endif
        }
    }
}