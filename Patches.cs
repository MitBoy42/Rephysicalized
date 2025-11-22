using Database;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;
using static Rephysicalized.ModAssets;

namespace Rephysicalized
{

    [HarmonyPatch(typeof(Localization), "Initialize")]
    public static class Localization_Initialize_Patch
    {
        public static void Postfix()
        {
            LocalisationUtil.Translate(typeof(STRINGS), true);
        }
    }


//Fix EnergyGenerator to emit solids correctly
[HarmonyPatch(typeof(EnergyGenerator), "Emit")]
class FixSolidStorePatch
{
    static bool Prefix(EnergyGenerator.OutputItem output, float dt, PrimaryElement root_pe, EnergyGenerator __instance)
    {
        // Replicate the vanilla behaviour except for solids + store=true
        Element elementByHash = ElementLoader.FindElementByHash(output.element);
        float num1 = output.creationRate * dt;
        if (output.store)
        {
            if (elementByHash.IsGas)
                __instance.storage.AddGasChunk(output.element, num1, root_pe.Temperature, byte.MaxValue, 0, true);
            else if (elementByHash.IsLiquid)
                __instance.storage.AddLiquid(output.element, num1, root_pe.Temperature, byte.MaxValue, 0, true);
            else // SOLIDS
            {
                GameObject go = elementByHash.substance.SpawnResource(Vector3.zero, num1, root_pe.Temperature, byte.MaxValue, 0);
                if (go != null)
                {
                    go.SetActive(false);
                    __instance.storage.Store(go, true);
                }
            }
            return false; // skip original
        }
        // else: let original run (emits into world)
        return true;
    }
}

    public static class ModMaterials
    {
   
       public static readonly string[] ALGAE = new[] { GameTags.Algae.ToString() };
    }



    public class NaturalDig
    {
        //GLOBAL
        [HarmonyPatch(typeof(WorldDamage), nameof(WorldDamage.OnDigComplete))]
        public class WorldDamage_OnDigComplete_Patch
        {
            // Transpiler to replace 0.5f with 1.0f in the calculation
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int replaced = 0;

                Debug.Log("[Rephysicalized] Transpiler for OnDigComplete running. Instruction count: " + codes.Count);

                for (int i = 0; i < codes.Count; i++)
                {
                    var code = codes[i];
                    // Replace ldc.r4 0.5 with ldc.r4 1.0
                    if (code.operand is float f && f == 0.5f)
                    {
                        code.operand = 1.0f;
                        replaced++;
                        Debug.Log("[Rephysicalized] Replaced ldc.r4 0.5f with 1.0f at instruction " + i);
                    }
                    yield return code;
                }

            }
        }

    }


}