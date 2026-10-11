﻿using HarmonyLib;
using TUNING;
using System;

using UnityEngine;

namespace Rephysicalized.Content.Plant_patches.AquaPlants
{
    internal static class AquaSeaTreePatches
    {
            // SeaTree harvest completion is driven by SeaTreeBranch.SpawnCritter.
        // Postfix applies:
        // - subtract 10f from the SeaTree root mass
        // - spawn 20% of the current root mass as ToxicSand at the root temperature
        [HarmonyPatch(typeof(SeaTreeBranch.Instance), nameof(SeaTreeBranch.Instance.SpawnCritter))]

        internal static class SeaTreeBranch_Instance_SpawnCritter_ToxicSandAndRootLoss_Patch
        {
            private const float RootFlatLossKg = 10f;
            private const float PercentageToSpawn = 0.20f;

            private static void Postfix(SeaTreeBranch.Instance __instance)
            {
                if (__instance == null)
                    return;

                var rootGO = __instance.Root;
                if (rootGO == null || !rootGO)
                    return;

   
                // Only SeaTreeBranch instances should match this patch, but we still keep this check tight.
                var pmt = rootGO.GetComponent<PlantMassTrackerComponent>();

                var pe = pmt == null ? rootGO.GetComponent<PrimaryElement>() : null;

                float currentRootMass;
                if (pmt != null)
                    currentRootMass = Mathf.Max(1f, pmt.TrackedMassKg);
                else if (pe != null)
                    currentRootMass = Mathf.Max(1f, pe.Mass);
                else
                    return;

                float desiredSpawn = currentRootMass * PercentageToSpawn;
                float desiredTotalRemoval = desiredSpawn + RootFlatLossKg;

                // Clamp not to drop below 1kg baseline.
                float maxRemovable = Mathf.Max(0f, currentRootMass - 1f);
                float actualRemoved = Mathf.Min(desiredTotalRemoval, maxRemovable);

                // Spawn amount is the portion of actualRemoved that corresponds to the percentage.
                float toxicSandToSpawn = Mathf.Min(desiredSpawn, actualRemoved);

                if (toxicSandToSpawn > 0f)
                {
                    float tempK = pe != null ? pe.Temperature : 293.15f;
                    var toxicSandElement = ElementLoader.FindElementByHash(SimHashes.ToxicSand);
                    Vector3 pos = rootGO.transform.GetPosition();

                    toxicSandElement.substance.SpawnResource(
                        pos,
                        toxicSandToSpawn,
                        tempK,
                        byte.MaxValue,
                        0,
                        prevent_merge: false,
                        forceTemperature: true,
                        manual_activation: false
                    );
                }

                // Apply flat+percentage mass loss to the root.
                if (actualRemoved > 0f)
                {
                    if (pmt != null)
                        pmt.AddExternalMass(-actualRemoved);
                    else if (pe != null)
                        pe.Mass = Mathf.Max(1f, pe.Mass - actualRemoved);
                }
            }
        }
    }
}

