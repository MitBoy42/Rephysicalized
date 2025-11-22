using HarmonyLib;
using Klei;
using ProcGen;
using ProcGenGame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rephysicalized
{
    // Patch DoSettleSim and scale the incoming cells[] mass BEFORE the sim is initialized.
    // Metals (materialCategory == Metal or RefinedMetal) are set to 0.5 kg.
    // Other solids are scaled by Config.Instance.SolidMassMult.
    [HarmonyPatch]
    public static class DoSettleSim_ScaleInputMass_Prefix
    {
        // Configure your multiplier
        public static float MassMultiplier => Config.Instance.SolidMassMult;

        // Cache: metal flags by Element index
        private static bool[] _isMetalByIdx;
        private static int _cachedElementCount = -1;

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            var t = typeof(WorldGenSimUtil);
            return HarmonyLib.AccessTools.GetDeclaredMethods(t)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "DoSettleSim") return false;
                    var p = m.GetParameters();
                    if (p.Length != 11) return false;

                    // Expected signature (with ref on indices 3,4,5):
                    // (WorldGenSettings, BinaryWriter, uint,
                    //  ref Sim.Cell[], ref float[], ref Sim.DiseaseCell[],
                    //  WorldGen.OfflineCallbackFunction, Klei.Data, List<TemplateSpawning.TemplateSpawner>,
                    //  Action<OfflineWorldGen.ErrorInfo>, int)
                    bool ok =
                        p[0].ParameterType == typeof(WorldGenSettings) &&
                        p[1].ParameterType == typeof(BinaryWriter) &&
                        p[2].ParameterType == typeof(uint) &&
                        p[3].ParameterType.IsByRef && p[3].ParameterType.GetElementType() == typeof(Sim.Cell[]) &&
                        p[4].ParameterType.IsByRef && p[4].ParameterType.GetElementType() == typeof(float[]) &&
                        p[5].ParameterType.IsByRef && p[5].ParameterType.GetElementType() == typeof(Sim.DiseaseCell[]) &&
                        p[6].ParameterType == typeof(WorldGen.OfflineCallbackFunction) &&
                        p[7].ParameterType.FullName == "Klei.Data" &&
                        p[8].ParameterType == typeof(List<TemplateSpawning.TemplateSpawner>) &&
                        p[9].ParameterType == typeof(Action<OfflineWorldGen.ErrorInfo>) &&
                        p[10].ParameterType == typeof(int);

                    return ok;
                });
        }

        [HarmonyPrefix]
        public static void Prefix(ref Sim.Cell[] cells)
        {
            try
            {
                if (cells == null || cells.Length == 0)
                    return;

                var elements = ElementLoader.elements;
                if (elements == null || elements.Count == 0)
                    return;

                EnsureMetalCache(elements);

                // Only scale when multiplier is meaningful; metals are overridden regardless.
                bool applyScale = MassMultiplier > 0f && Mathf.Abs(MassMultiplier - 1f) > 1e-6f;

                for (int i = 0; i < cells.Length; i++)
                {
                    var elemIdx = cells[i].elementIdx;
                    if (elemIdx < 0 || elemIdx >= elements.Count)
                        continue;

                    var elem = elements[elemIdx];
                    if (elem == null || !elem.IsSolid)
                        continue;

                    float m = cells[i].mass;
                    if (float.IsNaN(m) || m <= 0f)
                        continue;

                    if (_isMetalByIdx[elemIdx])
                    {
                        // Metals: set to fixed 0.5 
                        cells[i].mass = m * (0.25f + (MassMultiplier / 2f));
                    }
                    else if (applyScale)
                    {
                        // Other solids: apply multiplier
                        cells[i].mass = m * MassMultiplier;
                    }
                }
            }
            catch (Exception)
            {
                // Swallow to avoid breaking worldgen.
            }
        }

        private static void EnsureMetalCache(List<Element> elements)
        {
            if (_isMetalByIdx != null && _cachedElementCount == elements.Count)
                return;

            _isMetalByIdx = new bool[elements.Count];
            _cachedElementCount = elements.Count;

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];

                // Detect metals strictly by materialCategory Tag: Metal or RefinedMetal
                // Note: in current ONI builds, Element.materialCategory is Tag? (nullable Tag)
                bool isMetal = false;
                if (e != null)
                {
                    Tag? cat = e.materialCategory;
                    if (cat.HasValue)
                    {
                        Tag t = cat.Value;
                        isMetal = t == GameTags.Metal || t == GameTags.RefinedMetal;
                    }
                }

                _isMetalByIdx[i] = isMetal;
            }
        }
    }
}