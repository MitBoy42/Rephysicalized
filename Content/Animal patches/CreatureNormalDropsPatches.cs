using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{

    public static class Consolidated_EntityTemplates_DeathDropFunction
    {
        private static bool TryMapPrefabToDeathDrop(Tag prefabTag, out string newDropID, out float newDropCount)
        {
            newDropID = null;
            newDropCount = 0f;

            if (prefabTag == new Tag("SquirrelHug") || prefabTag == new Tag("SquirrelHugBaby"))
            {
                newDropID = "Meat";
                newDropCount = 0.5f;
                return true;
            }

            if (prefabTag == new Tag("Seal") || prefabTag == new Tag("SealBaby"))
            {
                newDropID = "Tallow";
                newDropCount = 1f;
                return true;
            }

            if (prefabTag == new Tag("Mole") || prefabTag == new Tag("MoleBaby") || prefabTag == new Tag("MoleDelicacy") || prefabTag == new Tag("MoleDelicacyBaby"))
            {
                newDropCount = 1f;
                return true;
            }

            if (prefabTag == new Tag("PrehistoricPacu") || prefabTag == new Tag("PrehistoricPacuBaby"))
            {
                newDropCount = 2f;
                return true;
            }

            if (prefabTag == new Tag("Squid") || prefabTag == new Tag("SquidBaby"))
            {
                newDropCount = 2f;
                return true;
            }

            if (prefabTag == new Tag("SeaTurtle") || prefabTag == new Tag("SeaTurtleBaby"))
            {
                newDropCount = 1f;
                return true;
            }

            // Default branch from original patch: apply only to these extra belly/stego variants.
            if (prefabTag == new Tag("IceBelly") || prefabTag == new Tag("IceBellyBaby") || prefabTag == new Tag("GoldBelly") || prefabTag == new Tag("GoldBellyBaby") || prefabTag == new Tag("Stego") || prefabTag == new Tag("StegoBaby") || prefabTag == new Tag("AlgaeStego") || prefabTag == new Tag("AlgaeStegoBaby"))
            {
                newDropCount = 4f;
                return true;
            }

            return false;
        }

        private static Tag GetPrefabTag(GameObject inst)
        {
            var prefabId = inst.GetComponent<KPrefabID>();
            return prefabId != null ? prefabId.PrefabTag : default;
        }

        [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction", new System.Type[] { typeof(UnityEngine.GameObject), typeof(float), typeof(string) })]
        public static class DeathDropFunction_Single
        {
            public static void Prefix(GameObject inst, ref float onDeathDropCount, ref string onDeathDropID)
            {
                Tag prefabTag = GetPrefabTag(inst);
                if (!TryMapPrefabToDeathDrop(prefabTag, out string newDropID, out float newDropCount))
                    return;

                if (newDropID != null)
                    onDeathDropID = newDropID;
                onDeathDropCount = newDropCount;
            }
        }

        [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction", new System.Type[] { typeof(UnityEngine.GameObject), typeof(float[]), typeof(string[]) })]
        public static class DeathDropFunction_Array
        {
            public static void Prefix(GameObject inst, ref float[] onDeathDropCounts, ref string[] onDeathDropIDs)
            {
                Tag prefabTag = GetPrefabTag(inst);
                if (!TryMapPrefabToDeathDrop(prefabTag, out string newDropID, out float newDropCount))
                    return;

                if (onDeathDropIDs == null || onDeathDropCounts == null)
                    return;

                int len = Math.Min(onDeathDropIDs.Length, onDeathDropCounts.Length);
                for (int i = 0; i < len; i++)
                {
                    if (!string.IsNullOrEmpty(onDeathDropIDs[i]) && onDeathDropCounts[i] > 0f)
                    {
                        if (newDropID != null)
                            onDeathDropIDs[i] = newDropID;
                        onDeathDropCounts[i] = newDropCount;
                    }
                }
            }
        }
    }
}

