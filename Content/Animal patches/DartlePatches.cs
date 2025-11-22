using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Rephysicalized
{
    // Shared helper to apply the adjustments exactly once.
    internal static class ChameleonAdjustments
    {
        private static bool applied;

        public static void ApplyOnce()
        {
            if (applied) return;

            var t = typeof(ChameleonConfig);
            var fDrips = AccessTools.Field(t, "DRIPS_EATEN_PER_CYCLE");
            var fCalPerDrip = AccessTools.Field(t, "CALORIES_PER_DRIP_EATEN");
            var fKgPerDrip = AccessTools.Field(t, "KG_POOP_PER_DRIP");
            var fMinPoopKg = AccessTools.Field(t, "MIN_POOP_SIZE_IN_KG");

            float oldDrips = (float)fDrips.GetValue(null);
            if (oldDrips <= 0f) oldDrips = 1f; // guard against zero/div

            float newDrips = 20f;
            float scale = 1 / newDrips;

            // Apply requested changes:
            // - Double number of drips eaten
            fDrips.SetValue(null, newDrips);

            // - Halve calories per drip so the creature actually eats two drips to reach the same per-cycle calories
            fCalPerDrip.SetValue(null, (float)fCalPerDrip.GetValue(null) * scale);

            // - Keep poop per cycle the same by halving per-drip poop and min poop size
            fKgPerDrip.SetValue(null, (float)fKgPerDrip.GetValue(null) * scale * 0.95f);
            fMinPoopKg.SetValue(null, (float)fMinPoopKg.GetValue(null) * scale * 0.1f);

            applied = true;
        }
    }

    // Apply adjustments as early as possible so Codex/Database uses the updated values.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class Db_Initialize_Prefix
    {
        public static void Prefix() => ChameleonAdjustments.ApplyOnce();
    }

    // Safety net: if something builds the prefab without going through Db.Initialize,
    // ensure the adjustments are still applied before prefab creation.
    [HarmonyPatch(typeof(ChameleonConfig))]
    public static class ChameleonConfig_Create_Prefix_AdjustBaseConstants
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(ChameleonConfig);
            var names = new[] { "CreateChameleon", "CreatePrefab" };
            foreach (var n in names)
            {
                var m = AccessTools.Method(t, n);
                if (m != null) yield return m;
            }
        }

        public static void Prefix() => ChameleonAdjustments.ApplyOnce();
    }

    [HarmonyPatch(typeof(CodexEntryGenerator_Creatures), "GenerateCreatureDescriptionContainers")]
    public static class CodexEntryGenerator_Creatures_GenerateCreatureDescriptionContainers_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var il = new List<CodeInstruction>(instructions);
            var kgPerKcalField = AccessTools.Field(typeof(CaloriesConsumedElementProducer), "kgProducedPerKcalConsumed");

            bool replaced = false;

            for (int i = 0; i < il.Count; i++)
            {
                var ci = il[i];

                // Look for: ldfld CaloriesConsumedElementProducer::kgProducedPerKcalConsumed
                if (!replaced && ci.opcode == OpCodes.Ldfld && Equals(ci.operand, kgPerKcalField))
                {
                    // Within the next few instructions, find the literal 2.0 used in the multiply chain
                    // and replace it with 1.0 so it doesn't double the displayed value.
                    for (int j = i + 1; j < il.Count && j <= i + 12; j++)
                    {
                        var cj = il[j];

                        // ldc.r4 2.0f
                        if (cj.opcode == OpCodes.Ldc_R4 && cj.operand is float f && (f == 2f || f == 2.0f))
                        {
                            il[j] = new CodeInstruction(OpCodes.Ldc_R4, 1.0f);
                            replaced = true;
                            break;
                        }

                        // ldc.r8 2.0
                        if (cj.opcode == OpCodes.Ldc_R8 && cj.operand is double d && (d == 2.0 || d == 2d))
                        {
                            // Use r8 1.0 to keep the numeric type consistent
                            il[j] = new CodeInstruction(OpCodes.Ldc_R8, 1.0);
                            replaced = true;
                            break;
                        }
                    }
                }
            }

            if (!replaced)
            {
                Debug.LogWarning("[Rephysicalized] Codex transpiler: could not find 2.0 literal after kgProducedPerKcalConsumed; no replacement applied.");
            }

            return il;
        }
    }
}