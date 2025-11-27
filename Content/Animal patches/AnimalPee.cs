using HarmonyLib;
using Klei.AI;
using Klei.CustomSettings;
using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using STRINGS;
namespace Rephysicalized
{


    [HarmonyPatch]
    internal static class DrinkMilkPeePatch
    {
        // Map creature prefabId to pee element. Default handled in GetPeeElement.
        private static readonly Dictionary<string, SimHashes> PeeElementOverrides = new Dictionary<string, SimHashes>(StringComparer.OrdinalIgnoreCase)
        {

{ "Seal", SimHashes.Ethanol },
{ "Drecko", SimHashes.Water },
{ "DreckoPlastic", SimHashes.Water },
{ "Chameleon", SimHashes.Chlorine },
{ "Raptor", SimHashes.BrineIce },
{ "OilFloater", SimHashes.CrudeOil },
{ "OilFloaterHighTemp", SimHashes.Petroleum },
{ "HatchMetal", SimHashes.Mercury },
{ "DivergentBeetle", SimHashes.SugarWater },
{ "DivergentWorm", SimHashes.Mud },
{ "DieselMoo", SimHashes.RefinedLipid },
{ "Moo", SimHashes.Brine },
};

        private const float PeeMassKg = 4f;
        private const float MassGainKg = 1f;
        private const string DefaultDiseaseId = "FoodPoisoning";
        private const int DefaultDiseaseCount = 100_000;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DrinkMilkStates), "DrinkMilkComplete")]
        private static void DrinkMilkComplete_Postfix(DrinkMilkStates.Instance smi)
        {
            if (smi == null) return;

            GameObject go;
            try
            {
                go = smi.gameObject;
            }
            catch
            {
                // SMI target was destroyed
                return;
            }
            if (go == null || go.Equals(null)) return;
            if (go.PrefabID().Name == "SweepBot") return;



            
                // 1) Increase creature mass by 1 kg if it has PrimaryElement
                var pe = go.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    pe.Mass += MassGainKg;
                }

                // 2) Spawn pee element with disease at the creature's location, using creature's body temperature
                var prefabId = go.PrefabID();
                var peeElement = GetPeeElement(prefabId != null ? prefabId.Name : null);
                var diseaseIdx = Db.Get().Diseases.GetIndex(DefaultDiseaseId);
                if (diseaseIdx < 0) diseaseIdx = byte.MaxValue; // no disease if not found

                float peeTemperature = 0f;
                if (pe != null)
                    peeTemperature = pe.Temperature; // Kelvin

                SpawnElementAt(go.transform.GetPosition(), peeElement, PeeMassKg, diseaseIdx, DefaultDiseaseCount, peeTemperature);
            
         
        }

        public static SimHashes GetPeeElement(string prefabId)
        {
            if (!string.IsNullOrEmpty(prefabId) && PeeElementOverrides.TryGetValue(prefabId, out var hash))
                return hash;
            // Default dirty water
            return SimHashes.DirtyWater;
        }
        private static void SpawnElementAt(Vector3 position, SimHashes element, float massKg, byte diseaseIdx, int diseaseCount, float temperatureK = 0f)
        {
            int cell = Grid.PosToCell(position); if (!Grid.IsValidCell(cell)) return;
            var el = ElementLoader.FindElementByHash(element);
            if (el == null) return;

            float temp = temperatureK;
            if (temp <= 0f)
            {
                temp = Grid.Temperature[cell];
                if (temp <= 0f)
                {
                    temp = el.defaultValues.temperature > 0f ? el.defaultValues.temperature : 300f;
                }
            }

            if (el.IsSolid)
            {
                // Spawn pickupable solid debris at world position with disease
                // SpawnResource(Vector3 position, float mass, float temperature, byte disease_idx, int disease_count, bool prevent_merge = false, bool skip_spawn_effects = false)
                GameObject debris = el.substance.SpawnResource(position, massKg, temp, diseaseIdx, diseaseCount, prevent_merge: false);

                // Optional: ensure it uses the element’s disease settings if diseaseIdx invalid
                if (debris != null)
                {
                    var pe = debris.GetComponent<PrimaryElement>();
                    if (pe != null)
                    {
                        // Make sure temperature and disease are applied (SpawnResource should already do this)
                        pe.Temperature = temp;
                        if (diseaseIdx != byte.MaxValue)
                            pe.AddDisease(diseaseIdx, diseaseCount, "CreaturePee");
                    }
                }
                return;
            }

            // Liquids and gases: particle or cell substance
            if ((el.IsLiquid || el.IsGas) && FallingWater.instance != null)
            {
                FallingWater.instance.AddParticle(
                    cell,
                    el.idx,
                    massKg,
                    temp,
                    diseaseIdx,
                    diseaseCount
                );
            }
            else
            {
                SimMessages.AddRemoveSubstance(
                    cell,
                    element,
                    CellEventLogger.Instance.ElementEmitted,
                    massKg,
                    temp,
                    diseaseIdx,
                    diseaseCount
                );
            }
        }
    }


      [HarmonyPatch(typeof(CodexEntryGenerator_Creatures), "GenerateCreatureDescriptionContainers")]
    public static class PeeCodexConversionPanelPatch
    {
        private const float PeeAmountKg = 4f;
        [HarmonyPostfix]
        public static void Postfix(GameObject creature, List<ContentContainer> containers)
        {
            if (creature == null || containers == null) return;

            // Resolve pee element via the main pee patch (no reflection)
            string prefabId = null;
         prefabId = creature.PrefabID().Name;

            SimHashes peeHash = SimHashes.DirtyWater;
           peeHash = DrinkMilkPeePatch.GetPeeElement(prefabId);

            var el = ElementLoader.FindElementByHash(peeHash);
         
            Tag peeTag = el.tag;

            // Left: creature as an "input" so CodexConversionPanel renders its icon on the left
            var creatureTag = creature.PrefabID();
            var ins = new[]
            {
            new ElementUsage(
                creatureTag,
                1f,
                false,
                CreatureFormatter 
            )
        };

            // Right: pee element 4 kg
            var outs = new[]
            {
            new ElementUsage(
                peeTag,
                PeeAmountKg,
                false,
                KgFormatter
            )
        };

            // Center: MilkFeeder icon (fallback to creature)
            GameObject converterGo = Assets.GetPrefab("MilkFeeder");
            if (converterGo == null) converterGo = creature;

            var widgets = new List<ICodexWidget>();
            widgets.Add(new CodexSpacer());
            widgets.Add(new CodexConversionPanel(
                title: STRINGS.CODEX.PANELS.ANIMALPEE,
                ins: ins,
                outs: outs,
                converter: converterGo
            ));

            var panelContainer = new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical);

            int insertIndex = FindAfterCritterDrops(containers);
            if (insertIndex < 0 || insertIndex > containers.Count)
                insertIndex = containers.Count;

            containers.Insert(insertIndex, panelContainer);
        }

        private static int FindAfterCritterDrops(List<ContentContainer> containers)
        {
            // Insert after the critter drops section if present, else append
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c != null)
                {
                    // Try to detect by scanning for a CodexConversionPanel whose title contains drops,
                    // or a subtitle text equal to CODEX.HEADERS.CRITTERDROPS
                    if (ContainerHasSubtitle(c, CODEX.HEADERS.CRITTERDROPS))
                        return Math.Min(i + 1, containers.Count);
                }
            }
            return -1;
        }

        private static bool ContainerHasSubtitle(ContentContainer container, string subtitleText)
        {
            if (container == null || string.IsNullOrEmpty(subtitleText)) return false;

            // Access private "content" list via Traverse
            var tc = Traverse.Create(container);
            List<ICodexWidget> widgets = null;
            try { widgets = tc.Field("content").GetValue<List<ICodexWidget>>(); } catch { }
            if (widgets == null)
            {
                try { widgets = tc.Property("Content")?.GetValue<List<ICodexWidget>>(); } catch { }
            }
            if (widgets == null) return false;

            foreach (var w in widgets)
            {
                if (w == null) continue;
                if (w.GetType().Name == "CodexText")
                {
                    var tw = Traverse.Create(w);
                    string text = null;
                    try { text = tw.Field("text").GetValue<string>(); } catch { }
                    if (string.IsNullOrEmpty(text))
                    {
                        try { text = tw.Property("Text")?.GetValue<string>(); } catch { }
                    }
                    if (!string.IsNullOrEmpty(text) && string.Equals(text, subtitleText, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static string CreatureFormatter(Tag tag, float amount, bool continuous)
        {
            // Display the creature’s proper name; ignore amount/continuous for clarity
            try
            {
                // ProperName() is available on Tag via Klei’s extensions
                return tag.ProperName();
            }
            catch
            {
                return "Creature";
            }
        }

        private static string KgFormatter(Tag tag, float amount, bool continuous)
        {
            float rounded = (amount >= 10f) ? Mathf.Round(amount) : Mathf.Round(amount * 10f) / 10f;
            return $"{rounded} kg";
        }
    }
}