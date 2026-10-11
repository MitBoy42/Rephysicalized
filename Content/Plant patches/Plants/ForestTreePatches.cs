using HarmonyLib;
using Klei.AI;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    /// <summary>
    /// Forest Tree tweaks: more water and fertiliser, RichSoil instead of Dirt, a 10x higher pressure
    /// ceiling, and trunk mass feeding its branches.
    /// </summary>
    [HarmonyPatch(typeof(ForestTreeConfig), nameof(ForestTreeConfig.CreatePrefab))]
    internal static class ForestTreeConfig_CreatePrefab_Patch
    {
        // Vanilla rates (per second), from ForestTreeConfig.CreatePrefab.
        private const float VanillaDirtyWaterRate = 0.1166667f;
        private const float VanillaDirtRate = 0.01666667f;

        // Replacement rates (per second).
        private const float DirtyWaterRate = 0.225f;    // 135 kg/cycle
        private const float DirtRate = 0.05277778f;     // ~31.66668 kg/cycle

        private const float Epsilon = 0.000001f;
        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= Epsilon;

        /// <summary>
        /// Retunes only the two plant consumption literals.

        /// </summary>
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);

            for (int i = 0; i < list.Count - 3; i++)
            {
                // PlantElementAbsorber.ConsumeInfo[] array initialiser
                if (list[i].opcode != OpCodes.Newarr ||
                    !Equals(list[i].operand, typeof(PlantElementAbsorber.ConsumeInfo)))
                {
                    continue;
                }

                // Inside it: a Tag field store and a float rate store, e.g. ldc.r4 <rate>; stfld MassConsumptionRate
                for (int j = i + 1; j < list.Count && list[j].opcode != OpCodes.Newarr; j++)
                {
                    if (list[j].opcode != OpCodes.Stfld ||
                        !Equals(list[j].operand, AccessTools.Field(typeof(PlantElementAbsorber.ConsumeInfo), "massConsumptionRate")))
                    {
                        continue;
                    }

                    if (list[j - 1].opcode == OpCodes.Ldc_R4 && list[j - 1].operand is float rate)
                    {
                        if (Approximately(rate, VanillaDirtyWaterRate))
                            list[j - 1].operand = DirtyWaterRate;
                        else if (Approximately(rate, VanillaDirtRate))
                            list[j - 1].operand = DirtRate;
                    }
                }
            }

            return list;
        }

        [HarmonyPostfix]
        private static void Postfix(ref GameObject __result)
        {
            if (__result == null)
                return;

            __result.AddOrGet<ForestTreeTrunkDrainController>();

            RaisePressureThresholds(__result);
            SwapDirtToRichSoil(__result);
        }

        /// <summary>Forest Trees tolerate 10x more pressure before warning and before dying.</summary>
        private static void RaisePressureThresholds(GameObject prefab)
        {
            var pv = prefab.GetComponent<PressureVulnerable>();
            if (pv == null)
                return;

            pv.pressure_sensitive = true;
            pv.pressureWarning_High *= 10f;
            pv.pressureLethal_High *= 10f;
        }


        /// Requires RichSoil rather than Dirt. 
  
        private static void SwapDirtToRichSoil(GameObject prefab)
        {
            var fertDef = prefab.GetDef<FertilizationMonitor.Def>();
            if (fertDef?.consumedElements != null)
            {
                for (int i = 0; i < fertDef.consumedElements.Length; i++)
                {
                    if (fertDef.consumedElements[i].tag == GameTags.Dirt)
                        fertDef.consumedElements[i].tag = ModTags.RichSoil;
                }
            }

            var deliveries = prefab.GetComponents<ManualDeliveryKG>();
            foreach (var delivery in deliveries)
            {
                if (delivery != null && delivery.RequestedItemTag == GameTags.Dirt)
                    delivery.RequestedItemTag = ModTags.RichSoil;
            }
        }
    }

    /// <summary>
    /// Shows the real fertiliser requirement on the plant instead of vanilla's hardcoded Dirt label.
    /// Vanilla's GetFertilizationLabel assumes a Dirt tag and null-checks loosely, so the label is rebuilt
    /// here from the Def, which keeps working for custom fertilisers such as RichSoil.
    /// </summary>
    [HarmonyPatch(typeof(MinionVitalsPanel), nameof(MinionVitalsPanel.GetFertilizationLabel))]
    internal static class MinionVitalsPanel_GetFertilizationLabel_Patch
    {
        private static bool Prefix(GameObject go, ref string __result)
        {
            string header = Db.Get().Amounts.Fertilization.Name;
            __result = header;

            try
            {
                if (go == null)
                    return false;

                var consumed = go.GetSMI<FertilizationMonitor.Instance>()?.def?.consumedElements;
                if (consumed == null || consumed.Length == 0)
                    return false;

                float usageMultiplier = 1f;
                var attributes = go.GetAttributes();
                if (attributes != null)
                {
                    var amount = attributes.Get(Db.Get().PlantAttributes.FertilizerUsageMod);
                    if (amount != null)
                        usageMultiplier = amount.GetTotalValue();
                }

                foreach (var entry in consumed)
                {
                    // Resolve as an element where possible; fall back to the tag name for custom items.
                    var element = ElementLoader.GetElement(entry.tag);
                    string name = element != null ? element.name : entry.tag.ProperName();

                    // ONI formats a per-cycle mass from a per-second rate via GameUtil.
                    float perSecond = entry.massConsumptionRate * usageMultiplier;
                    header += "\n    • " + name + " " + GameUtil.GetFormattedMass(perSecond, GameUtil.TimeSlice.PerCycle);
                }

                __result = header;
            }
            catch
            {
                // Fail safe: show just the section header rather than breaking the panel.
                __result = Db.Get().Amounts.Fertilization.Name;
            }

            return false; // skip vanilla, which assumes Dirt
        }
    }

    /// <summary>
    /// Branches drop WoodLog through the Plant Mass Tracker instead of vanilla's hardcoded spawn, so the
    /// amount matches the branch's tracked mass.
    /// </summary>
    internal static class ForestTreeBranch_MassTracker
    {
        [HarmonyPatch(typeof(ForestTreeBranchConfig), nameof(ForestTreeBranchConfig.CreatePrefab))]
        internal static class CreatePrefab_Patch
        {
            private static void Postfix(ref GameObject __result)
            {
                __result?.AddOrGet<PlantMassTrackerComponent>();
            }
        }


        // PlantBranch has no OnSpawn, so StartSM is where a spawned branch is initialised.
        [HarmonyPatch(typeof(PlantBranch.Instance), nameof(PlantBranch.Instance.StartSM))]
        internal static class StartSM_Patch
        {
            private static void Postfix(PlantBranch.Instance __instance)
            {
                if (__instance == null)
                    return;

                var kpid = __instance.gameObject.GetComponent<KPrefabID>();
                if (kpid == null || kpid.PrefabTag != (Tag)"ForestTreeBranch")
                    return;

                var pmt = __instance.gameObject.AddOrGet<PlantMassTrackerComponent>();
                if (PlantMassTrackerRegistry.TryGetConfig(kpid.PrefabID().Name, out var cfg) && cfg != null)
                    pmt.InitializeFromConfig(cfg);
            }
        }
    }

    /// <summary>
    /// Moves trunk mass into each growing branch. Shares its implementation with the Space Tree via
    /// </summary>
    public sealed class ForestTreeTrunkDrainController : TreeTrunkDrainControllerBase
    {
        // Vanilla effectively doubles this, so the observed drain is ~50 kg per branch per cycle.
        protected override float DrainPerCycleKg => 25f;

        protected override bool IsBranchEligible(GameObject branch)
        {
            if (branch == null)
                return false;

            var growing = branch.GetComponent<Growing>();
            if (growing != null && growing.IsGrown())
                return false; // fully grown: no drain

            var wilt = branch.GetComponent<WiltCondition>();
            if (wilt != null && wilt.IsWilting())
                return false; // wilted branch: no drain

            return true;
        }
    }
}
