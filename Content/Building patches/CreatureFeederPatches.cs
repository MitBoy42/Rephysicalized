using HarmonyLib;
using Klei.AI;
using Rephysicalized.Chores; // FueledDietRegistry
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized
{
    [HarmonyPatch(typeof(DiscoveredResources), nameof(DiscoveredResources.GetDiscoveredResourcesFromTag))]
    internal static class DiscoveredResources_FueledDiet
    {
        static void Postfix(Tag tag, ref HashSet<Tag> __result)
        {
            if (FueledDietRegistry.TryGet(tag, out FueledDiet diet) && diet.FuelInputs != null)
            {
                foreach (var fi in diet.FuelInputs)
                {
                    if (fi.ElementTag.IsValid)
                    {
                        var elem = ElementLoader.GetElement(fi.ElementTag);
                        if (elem != null && elem.IsSolid)
                            __result.Add(fi.ElementTag);
                    }
                }
            }
        }
    }
}

