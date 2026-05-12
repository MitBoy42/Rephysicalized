using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized
{ 
    // Forces plants created via ExtendEntityToBasicPlant to be Dirt instead of Creature
    [HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.ExtendEntityToBasicPlant))]
    internal static class ExtendEntityToBasicPlant_SetDirtElement_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject template)
        {
            var pe = template.GetComponent<PrimaryElement>();
            if (pe.ElementID == SimHashes.Creature)
            {
  
                pe.SetElement(SimHashes.Dirt);
      
            }
        }
    }


    // Consolidated: adjust yields for specific crop IDs in one pass, no PlantFiber special case.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Consolidated_Crop_Yield_Patch
    {
        private static void Postfix()
        {
            var crops = TUNING.CROPS.CROP_TYPES;
            if (crops == null || crops.Count == 0)
                return;

            // Target amounts per cropId; durations are preserved from existing values.
            var targetAmounts = new Dictionary<string, int>
            {
                { DewDripConfig.ID, 20 },                  // DewDrip
                { "SwampLily", 10 },                       // Balm Lily (SwampLily)
                { "Kelp", 10 },                            // Kelp
                { SimHashes.WoodLog.ToString(), 30 },      // WoodLog
                { SimHashes.OxyRock.ToString(), 20 },      // OxyRock
                { "PlantFiber", 50 }, 
            { "PlantMeat", 1 },     };
        
            for (int i = 0; i < crops.Count; i++)
            {
                var cv = crops[i];
                if (cv.cropId == null)
                    continue;

                if (!targetAmounts.TryGetValue(cv.cropId, out int amount))
                    continue;

                // Preserve original duration
                crops[i] = new Crop.CropVal(cv.cropId, cv.cropDuration, amount);
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
                || id == "SpaceTree" || id == "SpaceTreeBranch" || id == "VineMother" || id == "SpiceVine" || id == "KelpPlant")
                mass = 1f;
        }
    }

}