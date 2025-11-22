using HarmonyLib;
using STRINGS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;


namespace Rephysicalized
{
    internal static class ReactorPatchConfig
    {
        // Toggle to enable/disable verbose logs
        public static bool DebugLog = false;
    }

    // Thread-local scope used to force Water emission during a Dumpable.Dump for reactor coolant
    internal static class ForceWaterScope
    {
        [ThreadStatic] public static int Depth;
    }

    // 1) Make the reactor produce 1x waste (fuel mass) instead of 100x (supports float or double constants).
    [HarmonyPatch(typeof(Reactor), "DumpSpentFuel")]
    internal static class Reactor_DumpSpentFuel_Transpiler
    {
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int replacements = 0;

            foreach (var instr in instructions)
            {
                // Replace the first ldc.r4/ldc.r8 100 with 1
                if (replacements == 0 && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Mathf.Approximately(f, 100f))
                {
                    instr.operand = 1f;
                    replacements++;
                }
                else if (replacements == 0 && instr.opcode == OpCodes.Ldc_R8 && instr.operand is double d && Math.Abs(d - 100d) < 1e-6)
                {
                    instr.operand = 1d;
                    replacements++;
                }

                yield return instr;
            }

           
        }
    }


    // Implementation notes:
    // - We do NOT change fuel consumption (spentFuel), only the thermal effects.
    // - Heat generation: Postfix on Reactor.React adds an extra identical temperature delta.
    // - Conduction: Prefix on Reactor.Cool applies an extra ForceConduction before the original,
    //   so the original venting threshold check still sees the increased coolant temperature.
    internal static class ReactorHeatPatches
    {
        // Private Reactor fields used to locate active fuel/coolant without calling its private methods
        private static readonly FieldInfo FI_reactionStorage = AccessTools.Field(typeof(Reactor), "reactionStorage");
        private static readonly FieldInfo FI_fuelTag = AccessTools.Field(typeof(Reactor), "fuelTag");
        private static readonly FieldInfo FI_coolantTag = AccessTools.Field(typeof(Reactor), "coolantTag");

        private static Storage GetReactionStorage(Reactor r) => (Storage)FI_reactionStorage?.GetValue(r);

        private static PrimaryElement FindFirstPE(Storage storage, Tag tag)
        {
            if (storage == null) return null;
            var go = storage.FindFirst(tag);
            return go != null ? go.GetComponent<PrimaryElement>() : null;
        }

        private static PrimaryElement GetActiveFuel(Reactor r)
        {
            var storage = GetReactionStorage(r);
            var tag = (Tag)FI_fuelTag.GetValue(r);
            return FindFirstPE(storage, tag);
        }

        private static PrimaryElement GetActiveCoolant(Reactor r)
        {
            var storage = GetReactionStorage(r);
            // Use the reactor's coolantTag (AnyWater) to match base behavior
            var tag = (Tag)FI_coolantTag.GetValue(r);
            return FindFirstPE(storage, tag);
        }

        // Double the heat added to the active fuel each React tick (without changing spentFuel).
        [HarmonyPatch(typeof(Reactor), "React")]
        internal static class Reactor_React_DoubleHeat_Postfix
        {
            [HarmonyPostfix]
            public static void Postfix(Reactor __instance, float dt)
            {
                try
                {
                    var fuel = GetActiveFuel(__instance);
                    if (fuel == null || fuel.Mass < 0.25f) return;

                    // Base game adds: TemperatureDelta(-100f * dt * mass)
                    // Add it once more to effectively double the temperature increase
                    float extraDelta = GameUtil.EnergyToTemperatureDelta(-100f * dt * fuel.Mass * 0.5f, fuel);
                    fuel.Temperature += extraDelta;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Rephysicalized] Reactor_React_DoubleHeat_Postfix failed: {e}");
                }
            }
        }

        // Increase the conduction from fuel to coolant by running an extra conduction pass
        // BEFORE the original Cool, so the original vent threshold check sees the higher temp.
        [HarmonyPatch(typeof(Reactor), "Cool")]
        internal static class Reactor_Cool_DoubleConduction_Prefix
        {
            [HarmonyPrefix]
            public static void Prefix(Reactor __instance, float dt)
            {
                try
                {
                    var fuel = GetActiveFuel(__instance);
                    var coolant = GetActiveCoolant(__instance);
                    if (fuel == null || coolant == null) return;


                    // Do one extra pass with the same timestep multiplier to increase the effect.
                    GameUtil.ForceConduction(fuel, coolant, dt * 2.5f);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Rephysicalized] Reactor_Cool_DoubleConduction_Prefix failed: {e}");
                }
            }
        }
    }

    // Central place to define reactor coolant contamination values and shared constants
    internal class ReactorCoolantContaminationDB
    {
        // Title to use in Codex conversion panels
        public static string PanelTitle = STRINGS.CODEX.PANELS.COOLANTCONTAMINATION;

        // Correct building ID for the Nuclear Reactor in ONI
        public static string BuildingID => NuclearReactorConfig.ID; 

        public static readonly SimHashes Steam = SimHashes.Steam;
        public static readonly SimHashes NuclearWaste = SimHashes.NuclearWaste;

        // Fractions of Nuclear Waste emitted from coolant when dumped
        // Water is implicitly 0% (no waste)
        private static readonly Dictionary<SimHashes, float> WasteFractions = new Dictionary<SimHashes, float>
        {
            // Polluted Water (DirtyWater) and Salt Water => 14% waste
            { SimHashes.DirtyWater, 0.20f },
            { SimHashes.SaltWater, 0.10f },

            // Brine => 60% waste
            { SimHashes.Brine, 0.50f },
        };

        public static bool TryGetWasteFraction(SimHashes coolant, out float fraction)
        {
            if (WasteFractions.TryGetValue(coolant, out fraction))
            {
                fraction = Mathf.Clamp01(fraction);
                return true;
            }
            fraction = 0f;
            return false;
        }

        public static IEnumerable<SimHashes> AllSpecialCoolants() => WasteFractions.Keys;
    }

    // ------------------------------------------------------------------------------------------------
    // Reactor coolant dump util: uses central contamination values
    // ------------------------------------------------------------------------------------------------
    internal static class DumpableReactorWaterPatchUtil
    {
        private static readonly FieldInfo FI_ReactorWasteStorage =
            AccessTools.Field(typeof(Reactor), "wasteStorage");

        // Returns true if handled (we emitted waste + water ourselves), false if not a reactor coolant case
        internal static bool TryHandleReactorCoolantDump(Dumpable dumpable, Vector3 pos)
        {
            if (dumpable == null)
                return false;

            // Only act when the dumped item belongs to a Reactor
            var reactor = dumpable.transform.GetComponentInParent<Reactor>();
            if (reactor == null)
                return false;

            var pe = dumpable.GetComponent<PrimaryElement>();
            if (pe == null || pe.Element == null || !pe.Element.IsLiquid)
                return false;

            SimHashes id = pe.ElementID;

            // Pure Water => let the game do its normal dump
            if (id == SimHashes.Water)
                return false;

            // Configured contaminated coolants
            if (!ReactorCoolantContaminationDB.TryGetWasteFraction(id, out float fraction))
                return false;

            float totalMass = pe.Mass;
            if (totalMass <= 0f)
                return true; // nothing to emit; skip original

            float wasteMass = totalMass * fraction;
            float waterMass = totalMass - wasteMass;

            float temp = pe.Temperature;
            byte diseaseIdx = pe.DiseaseIdx;
            int diseaseCount = pe.DiseaseCount;
            int cell = Grid.PosToCell(pos);

            // 1) Put the fraction into Nuclear Waste (prefer the reactor's internal waste storage)
            if (wasteMass > 0f)
            {
                var wasteStorage = FI_ReactorWasteStorage?.GetValue(reactor) as Storage;
                if (wasteStorage != null)
                {
                    wasteStorage.AddLiquid(ReactorCoolantContaminationDB.NuclearWaste, wasteMass, temp, diseaseIdx, diseaseCount);
                }
                else
                {
                    SimMessages.AddRemoveSubstance(
                        cell,
                        ReactorCoolantContaminationDB.NuclearWaste,
                        CellEventLogger.Instance.Dumpable,
                        wasteMass,
                        temp,
                        diseaseIdx,
                        diseaseCount
                    );
                }
            }

            // 2) Emit the remainder as pure Water (FallingWater for liquids, like Dumpable does)
            if (waterMass > 0f)
            {
                ushort waterIdx = ElementLoader.GetElementIndex(SimHashes.Water);
                if (waterIdx < 0)
                    waterIdx = ElementLoader.FindElementByHash(SimHashes.Water).idx;

                FallingWater.instance.AddParticle(
                    cell,
                    waterIdx,
                    waterMass,
                    temp,
                    diseaseIdx,
                    diseaseCount,
                    true
                );
            }

            // 3) Consume and destroy the dumped item, and skip original
            pe.Mass = 0f;
            Util.KDestroyGameObject(dumpable.gameObject);
            return true;
        }
    }

    // Patch: Dumpable.Dump(Vector3 pos) -> route reactor contaminated coolant through our util
    [HarmonyPatch(typeof(Dumpable), nameof(Dumpable.Dump), new Type[] { typeof(Vector3) })]
    internal static class Dumpable_Dump_WithPos_Prefix
    {
        [HarmonyPrefix]
        private static bool Prefix(Dumpable __instance, Vector3 pos)
        {
            // If handled (reactor contaminated coolant), skip original; otherwise allow original
            return !DumpableReactorWaterPatchUtil.TryHandleReactorCoolantDump(__instance, pos);
        }
    }

    // Patch: Dumpable.Dump() -> same logic as above, position-less overload
    [HarmonyPatch(typeof(Dumpable), nameof(Dumpable.Dump), new Type[] { })]
    internal static class Dumpable_Dump_NoPos_Prefix
    {
        [HarmonyPrefix]
        private static bool Prefix(Dumpable __instance)
        {
            Vector3 pos = __instance.transform.GetPosition();
            return !DumpableReactorWaterPatchUtil.TryHandleReactorCoolantDump(__instance, pos);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Codex: Add "coolant contamination" conversion panels to the Nuclear Reactor building entry
    // Hook the actual entry creation method in your build:
    //   CodexEntryGenerator.GenerateSingleBuildingEntry(BuildingDef def, string categoryEntryID)
    // ------------------------------------------------------------------------------------------------
    [HarmonyPatch(typeof(CodexEntryGenerator), "GenerateSingleBuildingEntry", new Type[] { typeof(BuildingDef), typeof(string) })]
    internal static class NuclearReactor_CodexPanels_Patch
    {
        private static readonly Func<Tag, float, bool, string> PercentFormatter =
            (tag, amount, continuous) => $"{Mathf.RoundToInt(amount * 100f)}%";

        [HarmonyPostfix]
        private static void Postfix(BuildingDef def, string categoryEntryID, ref CodexEntry __result)
        {
            try
            {
                if (__result == null || def == null)
                    return;

                // Only for the Nuclear Reactor
                if (!string.Equals(def.PrefabID, ReactorCoolantContaminationDB.BuildingID, StringComparison.Ordinal))
                    return;

                // Get the entry's ContentContainer list (the Codex UI chunks)
                var containers = GetEntryContainers(__result);
                if (containers == null)
                    return;

                // Do not add duplicates if this ran earlier
                if (EntryAlreadyHasOurPanels(containers))
                    return;

                // Build "Water 100% > Reactor > Steam 100%" panel
                var waterPanel = BuildSingleOutputPanel(
                    input: SimHashes.Water,
                    output: ReactorCoolantContaminationDB.Steam,
                    amountIn: 1f,
                    amountOut: 1f,
                    converterGO: def.BuildingComplete);

                if (waterPanel != null)
                    containers.Add(waterPanel);

                // Build split panels for each configured contaminated coolant
                foreach (var coolant in ReactorCoolantContaminationDB.AllSpecialCoolants())
                {
                    if (!ReactorCoolantContaminationDB.TryGetWasteFraction(coolant, out float fraction))
                        continue;

                    var splitPanel = BuildSplitWastePanel(coolant, fraction, def.BuildingComplete);
                    if (splitPanel != null)
                        containers.Add(splitPanel);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rephysicalized] NuclearReactor_CodexPanels_Patch.Postfix failed: {e}");
            }
        }

        private static List<ContentContainer> GetEntryContainers(CodexEntry entry)
        {
            if (entry == null) return null;

            // Find List<ContentContainer> field by reflection (matches your build)
            var field = AccessTools.GetDeclaredFields(typeof(CodexEntry))
                                   .FirstOrDefault(f => typeof(List<ContentContainer>).IsAssignableFrom(f.FieldType));
            return field?.GetValue(entry) as List<ContentContainer>;
        }

        private static bool EntryAlreadyHasOurPanels(List<ContentContainer> containers)
        {
            if (containers == null) return false;
            foreach (var cc in containers)
            {
                if (cc == null) continue;

                // ContentContainer exposes its content via a field; reflect it
                var fiContent = AccessTools.Field(typeof(ContentContainer), "content");
                var content = fiContent?.GetValue(cc) as List<ICodexWidget>;
                if (content == null) continue;

                foreach (var w in content)
                {
                    if (w is CodexConversionPanel panel)
                    {
                        var fiTitle = AccessTools.Field(typeof(CodexConversionPanel), "title");
                        var title = fiTitle?.GetValue(panel) as string;
                        if (string.Equals(title, ReactorCoolantContaminationDB.PanelTitle, StringComparison.OrdinalIgnoreCase))
                            return true; // already present
                    }
                }
            }
            return false;
        }

        private static ContentContainer BuildSingleOutputPanel(SimHashes input, SimHashes output, float amountIn, float amountOut, GameObject converterGO)
        {
            var inElem = ElementLoader.FindElementByHash(input);
            var outElem = ElementLoader.FindElementByHash(output);
            if (inElem == null || outElem == null) return null;

            var ins = new[]
            {
                new ElementUsage(inElem.tag, amountIn, false, PercentFormatter)
            };
            var outs = new[]
            {
                new ElementUsage(outElem.tag, amountOut, false, PercentFormatter)
            };
            var widget = (ICodexWidget)new CodexConversionPanel(ReactorCoolantContaminationDB.PanelTitle, ins, outs, converterGO);
            return new ContentContainer(new List<ICodexWidget> { widget }, ContentContainer.ContentLayout.Vertical);
        }

        private static ContentContainer BuildSplitWastePanel(SimHashes input, float wasteFraction, GameObject converterGO)
        {
            var inElem = ElementLoader.FindElementByHash(input);
            var steam = ElementLoader.FindElementByHash(ReactorCoolantContaminationDB.Steam);
            var waste = ElementLoader.FindElementByHash(ReactorCoolantContaminationDB.NuclearWaste);
            if (inElem == null || steam == null || waste == null) return null;

            float x = Mathf.Clamp01(wasteFraction);
            float steamPct = 1f - x;

            var ins = new[]
            {
                new ElementUsage(inElem.tag, 1f, false, PercentFormatter)
            };
            var outs = new[]
            {
                new ElementUsage(steam.tag, steamPct, false, PercentFormatter),
                new ElementUsage(waste.tag, x, false, PercentFormatter)
            };
            var widget = (ICodexWidget)new CodexConversionPanel(ReactorCoolantContaminationDB.PanelTitle, ins, outs, converterGO);
            return new ContentContainer(new List<ICodexWidget> { widget }, ContentContainer.ContentLayout.Vertical);
        }
    }


}