using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized.Content.Plant_patches
{
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using HarmonyLib;
    using UnityEngine;

    namespace Rephysicalized.Content.Rockets
    {
        // Target: RocketEngineCluster.StatesInstance.DoBurn(float dt)
        // Change the loop upper bound from 10 (indexes 1..9) to 4 (indexes 1..3),
        // which reduces the heated rectangle under the engine from 3x9 to 3x3.
        [HarmonyPatch(typeof(RocketEngineCluster.StatesInstance), nameof(RocketEngineCluster.StatesInstance.DoBurn))]
        internal static class RocketEngineCluster_StatesInstance_DoBurn_Transpiler
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool replaced = false;

                foreach (var code in instructions)
                {
                    // Replace the first constant 10 loaded for "int num1 = 10;" with 4.
                    if (!replaced)
                    {
                        // ldc.i4.s 10
                        if (code.opcode == OpCodes.Ldc_I4_S && code.operand is sbyte sb && sb == 10)
                        {
                            // use ldc.i4.4
                            code.opcode = OpCodes.Ldc_I4_4;
                            code.operand = null;
                            replaced = true;
                        }
                        // ldc.i4 10
                        else if (code.opcode == OpCodes.Ldc_I4 && code.operand is int iv && iv == 10)
                        {
                            code.opcode = OpCodes.Ldc_I4_4;
                            code.operand = null;
                            replaced = true;
                        }
                    }

                    yield return code;
                }

                if (!replaced)
                    Debug.LogWarning("[Rephysicalized] RocketEngineCluster.DoBurn loop bound not patched (constant 10 not found). Exhaust area may remain 3x9.");
            }
        }
    }
}
