using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized
{ // Forces plants created via ExtendEntityToBasicPlant to be Dirt instead of Creature
    [HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.ExtendEntityToBasicPlant))]
    internal static class ExtendEntityToBasicPlant_SetDirtElement_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject template)
        {
            if (template == null) return;
            var pe = template.GetComponent<PrimaryElement>();
            if (pe == null) return;

            // Only switch if it’s still the generic Creature element; preserves plants that already set a concrete element.
            if (pe.ElementID == SimHashes.Creature)
            {
  
                pe.SetElement(SimHashes.Dirt);
      
            }
        }
    }

    // All plants start at 1 kg when placed
    [HarmonyPatch]
    internal static class ForestTreePlacedEntityMassEarlyPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(EntityTemplates)))
            {
                if (m.Name != nameof(EntityTemplates.CreatePlacedEntity) || m.ReturnType != typeof(GameObject))
                    continue;
                var ps = m.GetParameters();
                if (ps.Length >= 4 && ps[0].ParameterType == typeof(string) && ps[3].ParameterType == typeof(float))
                    yield return m;
            }
        }

        private static void Prefix([HarmonyArgument(0)] string id, [HarmonyArgument(3)] ref float mass)
        {
            if (id == "ForestTree" || id == "ForestTreeBranch" || id == "ColdBreather" || id == "BlueGrass" || id == "SaltPlant"
                || id == "SpaceTree" || id == "SpaceTreeBranch" || id == "VineMother")
                mass = 1f;
        }
    }

}