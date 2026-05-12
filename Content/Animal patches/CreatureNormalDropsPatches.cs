using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{

    [HarmonyPatch]
    public static class ConsolidatedDeathDropCountTranspiler
    {
        // Describe per-target float replacements
        private static readonly Dictionary<MethodBase, (float from, float to)[]> _replacements;

        static ConsolidatedDeathDropCountTranspiler()
        {
            _replacements = new Dictionary<MethodBase, (float from, float to)[]>();

            TryAdd(typeof(BasePrehistoricPacuConfig), nameof(BasePrehistoricPacuConfig.CreatePrefab), new (float, float)[] { (12f, 2f) });
            TryAdd(typeof(BaseBellyConfig), nameof(BaseBellyConfig.BaseBelly), new (float, float)[] { (14f, 4f) });
            TryAdd(typeof(BaseStegoConfig), nameof(BaseStegoConfig.BaseStego), new (float, float)[] { (12f, 4f) });
        }

        private static void TryAdd(Type type, string methodName, (float from, float to)[] pairs)
        {
           
                var m = AccessTools.Method(type, methodName);
                if (m != null)
                    _replacements[m] = pairs;
        }

        // Harmony will patch all methods returned here with the same transpiler
        static IEnumerable<MethodBase> TargetMethods()
        {
            return _replacements.Keys;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            if (!_replacements.TryGetValue(__originalMethod, out var pairs) || pairs == null || pairs.Length == 0)
            {
                foreach (var i in instructions) yield return i;
                yield break;
            }

            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f)
                {
                    // Look for a matching "from" constant and replace with "to"
                    bool replaced = false;
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        if (Mathf.Approximately(f, pairs[i].from))
                        {
                            yield return new CodeInstruction(OpCodes.Ldc_R4, pairs[i].to);
                            replaced = true;
                            break;
                        }
                    }

                    if (!replaced)
                        yield return instr;
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }


    [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction")]
    public static class Consolidated_EntityTemplates_DeathDropFunction
    {
        public static void Prefix(GameObject inst, ref float onDeathDropCount, ref string onDeathDropID)
        {

            var kpid = inst.GetComponent<KPrefabID>();

            Tag pt = kpid.PrefabTag;

                if (pt == new Tag(SquirrelHugConfig.ID) || pt == new Tag(BabySquirrelHugConfig.ID))
                {
                    onDeathDropID = "Meat";
                    onDeathDropCount = 0.5f;
                    return;
                }
         if (pt == new Tag(SealConfig.ID) || pt == new Tag(BabySealConfig.ID))
                {
                    onDeathDropID = "Tallow";
                    onDeathDropCount = 1f;
                    return;
                }

                if (pt == new Tag(MoleConfig.ID) || pt == new Tag(BabyMoleConfig.ID) || pt == new Tag(MoleDelicacyConfig.ID) || pt == new Tag(BabyMoleDelicacyConfig.ID))
                {
                    onDeathDropCount = 1f;
                    return;
                }
    
        }
    }
}