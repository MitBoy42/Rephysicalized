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

                    // Current ONI signature (decompiled):
                
                    if (p.Length != 9) return false;

                    bool ok =
                        p[0].ParameterType == typeof(WorldGenSettings) &&
                        p[1].ParameterType == typeof(BinaryWriter) &&
                        p[2].ParameterType == typeof(uint) &&
                        p[3].ParameterType.IsByRef && p[3].ParameterType.GetElementType() == typeof(WorldgenSimData) &&
                        p[4].ParameterType == typeof(WorldGen.OfflineCallbackFunction) &&
                        p[5].ParameterType.FullName == "Klei.Data" &&
                        p[6].ParameterType == typeof(List<TemplateSpawning.TemplateSpawner>) &&
                        p[7].ParameterType == typeof(Action<OfflineWorldGen.ErrorInfo>) &&
                        p[8].ParameterType == typeof(int);

                    return ok;
                });
        }

        [HarmonyPrefix]
        public static void Prefix(ref WorldgenSimData simData)
        {
            try
            {
                if ((simData.cells == null || simData.cells.Length == 0) && (simData.backwallCells == null || simData.backwallCells.Length == 0))
                    return;

                var cells = simData.cells;
                var backwallCells = simData.backwallCells;


                var elements = ElementLoader.elements;
                if (elements == null || elements.Count == 0)
                    return;

                EnsureMetalCache(elements);

                bool applyScale = MassMultiplier > 0f && Mathf.Abs(MassMultiplier - 1f) > 1e-6f;

                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];

                    var elemIdx = cell.elementIdx;
                    if (elemIdx < 0 || elemIdx >= elements.Count)
                        continue;

                    var elem = elements[elemIdx];
                    if (elem == null || !elem.IsSolid)
                        continue;

                    float m = cell.mass;
                    if (float.IsNaN(m) || m <= 0f)
                        continue;

                    if (_isMetalByIdx[elemIdx])
                    {
                        cell.mass = m * (0.25f + (MassMultiplier / 2f));
                    }
                    else if (applyScale)
                    {
                        cell.mass = m * MassMultiplier;
                    }

                    cells[i] = cell;
                }

                // Scale backwall masses as well
                if (backwallCells != null)
                {
                    int count = Math.Min(backwallCells.Length, cells.Length);
                    for (int i = 0; i < count; i++)
                    {
                        var bw = backwallCells[i];
                        var elemIdx = bw.elementIdx;

                        if (elemIdx < 0 || elemIdx >= elements.Count)
                            continue;

                        var elem = elements[elemIdx];
                        if (elem == null || !elem.IsSolid)
                            continue;

                        float m = bw.mass;
                        if (float.IsNaN(m) || m <= 0f)
                            continue;

                        if (_isMetalByIdx[elemIdx])
                        {
                            bw.mass = m * (0.25f + (MassMultiplier / 2f));
                        }
                        else if (applyScale)
                        {
                            bw.mass = m * MassMultiplier;
                        }

                        backwallCells[i] = bw;
                    }
                }
            } 

            catch (Exception)
            {
                // Swallow to avoid breaking worldgen.
            }
        }
        private static readonly HashSet<SimHashes> MetalElementAllowlist = new HashSet<SimHashes>
        {
            SimHashes.Katairite
        };

        private static void EnsureMetalCache(List<Element> elements)
        {
            if (_isMetalByIdx != null && _cachedElementCount == elements.Count)
                return;

            _isMetalByIdx = new bool[elements.Count];
            _cachedElementCount = elements.Count;

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                bool isMetal = false;

                if (e != null)
                {
                
                        Tag? cat = e.materialCategory; // nullable Tag in current ONI builds
                        if (cat.HasValue)
                        {
                            Tag t = cat.Value;
                            if (t == GameTags.Metal || t == GameTags.RefinedMetal)
                                isMetal = true;
                        }
   
                        if (MetalElementAllowlist.Contains(e.id))
                            isMetal = true;
                    
                }

                _isMetalByIdx[i] = isMetal;
            }
        }
    }
}