using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{

    [HarmonyPatch(typeof(CodexEntryGenerator_Creatures), nameof(CodexEntryGenerator_Creatures.GenerateCreatureDescriptionContainers))]
    internal static class RephysicalizedDietCodexPatch
    {
        public static string PanelTitle =  STRINGS.CODEX.PANELS.FUELEDDIET;

        [HarmonyPostfix]
        private static void Postfix(GameObject creature, List<ContentContainer> containers)
        {
            if (creature == null || containers == null)
                return;

            // Resolve FueledDiet from registry by prefab tag
            if (!TryGetFueledDiet(creature, out var fueledDiet))
                return;

            // Build inputs from FueledDiet.FuelInputs
            var inputs = BuildInputs(fueledDiet);
            if (inputs.Length == 0)
                return; // nothing to show

            // Build outputs from conversions (if present); otherwise leave blank
            var outs = BuildOutputs(fueledDiet);

            var panel = new CodexConversionPanel(PanelTitle, inputs, outs, creature);

            var widgets = new List<ICodexWidget>
            {
                new CodexSpacer(),
                panel
            };

            containers.Add(new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical));
        }

        private static bool TryGetFueledDiet(GameObject prefab, out FueledDiet diet)
        {
            diet = null;
            var kpid = prefab.GetComponent<KPrefabID>();
            if (kpid == null)
                return false;

            return FueledDietRegistry.TryGet(kpid.PrefabTag, out diet) && diet != null;
        }

        private static ElementUsage[] BuildInputs(FueledDiet diet)
        {
            if (diet?.FuelInputs == null || diet.FuelInputs.Count == 0)
                return Array.Empty<ElementUsage>();

            // Distinct input tags from FuelInputs
            var tags = diet.FuelInputs
                .Select(fi => fi.ElementTag)
                .Where(t => t.IsValid)
                .Distinct();

            // Use non-zero amount to ensure icons render even when outputs are empty; hide text via EmptyFormatter
            return tags
                .Select(t => new ElementUsage(t, amount: 1f, continuous: true, customFormating: EmptyFormatter))
                .ToArray();
        }

        private static ElementUsage[] BuildOutputs(FueledDiet diet)
        {
            if (diet?.Conversions == null || diet.Conversions.Count == 0)
                return Array.Empty<ElementUsage>();

            // Group by output tag (valid only), compute percent = 100 * average(KgOutputPerKgInput)
            var groups = diet.Conversions
                .Where(c => c.OutputTag.IsValid)
                .GroupBy(c => c.OutputTag);

            var outs = new List<ElementUsage>();
            foreach (var g in groups)
            {
                float avgRatio = g.Any() ? Mathf.Max(0f, g.Average(x => x.KgOutputPerKgInput)) : 0f;
                float percent = avgRatio * 100f;

                outs.Add(new ElementUsage(
                    g.Key,
                    amount: percent,
                    continuous: true,
                    customFormating: PercentFormatter));
            }

            return outs.ToArray();
        }

        // Suppress any numeric text under input icons
        private static string EmptyFormatter(Tag tag, float amount, bool continuous) => string.Empty;

        // Format output amount as a percentage with one decimal place
        private static string PercentFormatter(Tag tag, float amount, bool continuous) => $"{amount:0.#}%";
    }


    // Always-on state machine that shows the NO_SOLID_FUEL status item
    // whenever the creature's FueledDiet storage is below the refill threshold.
    //
    // This does not depend on having a target or an active chore.
    public sealed class SolidFuelRefillStatusSM
        : GameStateMachine<SolidFuelRefillStatusSM, SolidFuelRefillStatusSM.Instance, IStateMachineTarget>
    {
        public sealed class Instance : GameInstance
        {
            public Instance(IStateMachineTarget master) : base(master) { }

            public GameObject GO
            {
                get
                {
                    var kmb = master as KMonoBehaviour;
                    return kmb != null ? kmb.gameObject : null;
                }
            }
        }

        private State satisfied;     // no status
        private State needs_refill;  // shows NO_SOLID_FUEL

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = satisfied;

            satisfied
                .Update((smi, dt) =>
                {
                    if (IsRefillNeeded(smi.GO))
                        smi.GoTo(needs_refill);
                }, UpdateRate.SIM_4000ms);

            needs_refill
                .ToggleStatusItem(
                    name: STRINGS.STATUSITEMS.NO_SOLID_FUEL.NAME,
                    tooltip: STRINGS.STATUSITEMS.NO_SOLID_FUEL.TOOLTIP,
                    category: Db.Get().StatusItemCategories.Stored)
                .Update((smi, dt) =>
                {
                    if (!IsRefillNeeded(smi.GO))
                        smi.GoTo(satisfied);
                }, UpdateRate.SIM_4000ms);
        }

        // Same condition as in SolidFuelStates.IsRefillNeeded, adapted to run outside the chore SM.
        private static bool IsRefillNeeded(GameObject go)
        {
            if (go == null) return false;

            var controller = go.GetComponent<FueledDietController>();
            var storage = controller != null ? controller.FuelStorage : null;
            if (storage == null)
                return true;

            float cap = controller != null && controller.FuelStorageCapacityKg > 0f
                ? controller.FuelStorageCapacityKg
                : storage.capacityKg;

            if (cap <= 0f) return false;

            float used = GetStorageUsedKg(storage);
            float remain = Mathf.Max(0f, cap - used);

            float threshold = GetRefillThresholdKg(controller);
            if (threshold > 0f)
                return used < threshold;

            return remain > 0.01f;
        }

        private static float GetStorageUsedKg(Storage storage)
        {
            float used = 0f;
            if (storage != null && storage.items != null)
            {
                for (int i = 0; i < storage.items.Count; i++)
                {
                    var go = storage.items[i];
                    var pe = go != null ? go.GetComponent<PrimaryElement>() : null;
                    if (pe != null) used += pe.Mass;
                }
            }
            return used;
        }

        private static float GetRefillThresholdKg(FueledDietController controller)
        {
            if (controller == null) return 0f;
            float threshold = controller.RefillThreshold;
            if (float.IsNaN(threshold) || threshold <= 0f)
                return 0f;
            return threshold;
        }
    }

    // Wire the always-on status monitor when FueledDietController spawns.
    // This keeps all existing files unchanged.
    [HarmonyPatch(typeof(FueledDietController), "OnSpawn")]
    internal static class FueledDietController_OnSpawn_RefillStatus_Patch
    {
        private static void Postfix(FueledDietController __instance)
        {
            if (__instance == null) return;

            // Ensure a StateMachineController exists to host SMIs
            __instance.gameObject.AddOrGet<StateMachineController>();

            // Start the SMI if not already running
            if (__instance.gameObject.GetSMI<SolidFuelRefillStatusSM.Instance>() == null)
            {
                var smi = new SolidFuelRefillStatusSM.Instance(__instance);
                smi.StartSM();
            }
        }
    }
}