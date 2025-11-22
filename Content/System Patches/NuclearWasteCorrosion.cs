using Database;
using HarmonyLib;
using Klei.AI;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using UnityEngine;
using UnityEngine;
using UtilLibs;
using static ElementLoader;

namespace Rephysicalized
{
    // Public strings the UI (or other mods) can call into
    public static class CorrosiveWasteStrings
    {
        // As requested: a public static LocString used for corrosion damage source/poptext
        public static LocString CORROSIVE_ELEMENT = (LocString)"corrosive element";
    }

    // Global manager that scans for high-mass nuclear waste and corrodes neighboring solids
    public sealed class NuclearWasteCorrosionManager : KMonoBehaviour, ISim1000ms
    {
        // Config
        public static float MassThresholdKg = 100f;      // Cells with >= this mass of Nuclear Waste will corrode neighbors
        public static float TileFracturePerTick = 0.02f; // Fracture input amount to WorldDamage (like Comet)
        public static int BuildingHpDamagePerTick = 10;  // Raw HP damage per tick to constructed tiles/buildings
        public static bool IncludeDiagonalNeighbors = false;

        private static readonly CellOffset[] Cardinal = new[]
        {
            new CellOffset( 1, 0),
            new CellOffset(-1, 0),
            new CellOffset( 0, 1),
            new CellOffset( 0,-1),
        };

        private static readonly CellOffset[] Diagonals = new[]
        {
            new CellOffset( 1, 1),
            new CellOffset( 1,-1),
            new CellOffset(-1, 1),
            new CellOffset(-1,-1),
        };

        private Element nuclearWaste;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            nuclearWaste = ElementLoader.FindElementByHash(SimHashes.NuclearWaste);
        }

        // Runs every ~1000ms on the Sim thread scheduler
        public void Sim1000ms(float dt)
        {
            if (nuclearWaste == null)
                return;

            int activeWorldId = ClusterManager.Instance.activeWorldId;
            int cellCount = Grid.CellCount;

            for (int cell = 0; cell < cellCount; cell++)
            {
                // Restrict to active world for performance
                if ((int)Grid.WorldIdx[cell] != activeWorldId)
                    continue;

                if (Grid.Element[cell] != nuclearWaste)
                    continue;

                if (Grid.Mass[cell] < MassThresholdKg)
                    continue;

                DamageNeighbors(cell);
            }
        }

        private void DamageNeighbors(int wasteCell)
        {
            var neighbors = IncludeDiagonalNeighbors ? Cardinal.Concat(Diagonals) : Cardinal;

            foreach (var off in neighbors)
            {
                int nCell = Grid.OffsetCell(wasteCell, off);
                if (!Grid.IsValidCell(nCell))
                    continue;

                // We only want to damage solids or solid-occupying tiles
                bool isSolid = Grid.Solid[nCell];
                GameObject tile_go = Grid.Objects[nCell, 9]; // Same layer index used by Comet to find constructed tiles
                bool hasConstructedTile = tile_go != null;

                if (!isSolid && !hasConstructedTile)
                    continue;

                Element e = GetElementForCell(nCell, tile_go);
                if (IsImmune(e))
                    continue;

                ApplyDamageToCell(nCell, wasteCell, e, tile_go);
            }
        }

        // Determine the affected element (constructed tile may have PrimaryElement that differs from Grid.Element)
        private static Element GetElementForCell(int cell, GameObject tile_go)
        {
            if (tile_go != null)
            {
                var sco = tile_go.GetComponent<SimCellOccupier>();
                if (sco != null && !sco.doReplaceElement)
                {
                    var pe = tile_go.GetComponent<PrimaryElement>();
                    if (pe != null)
                        return pe.Element;
                }
            }

            return Grid.Element[cell];
        }

        private static bool IsImmune(Element element)
        {
            if (element == null)
                return false;

            // Nickel or any element with hardness > 50 are immune
            if (element.id == SimHashes.Nickel || element.id == SimHashes.HardPolypropylene || element.id == SimHashes.Lead)
                return true;

            return element.hardness > 49f;
        }

        private static void ApplyDamageToCell(int cell, int fromCell, Element element, GameObject tile_go)
        {
            // Constructed tile/building case (use Overheatable-style BuildingHP.Trigger with raw hash)
            if (tile_go != null)
            {
                var sco = tile_go.GetComponent<SimCellOccupier>();
                bool constructed = sco != null && !sco.doReplaceElement;

                if (constructed)
                {
                    var bhp = tile_go.GetComponent<BuildingHP>();
                    if (bhp != null)
                    {
                        var damageInfo = new BuildingHP.DamageSourceInfo
                        {
                            damage = BuildingHpDamagePerTick,
                            source = (string)CorrosiveWasteStrings.CORROSIVE_ELEMENT,
                            popString = (string)CorrosiveWasteStrings.CORROSIVE_ELEMENT,
                            // Optional: reuse Overheatable's small smoke effect
                            fullDamageEffectName = "smoke_damage_kanim"
                        };

                        // -794517298 is the raw hash used by Overheatable for building HP damage
                        bhp.Trigger(-794517298, damageInfo);
                    }

                    return;
                }
            }

            // Natural solid tile (use WorldDamage fracture)
            if (element == null || element.strength <= 0f)
                return;

            float inputDamage = TileFracturePerTick;
            float amount = inputDamage / element.strength; // mirrors Comet’s approach: input scaled by element strength

            WorldDamage.Instance.ApplyDamage(
                cell,
                amount,
                fromCell,
                (string)CorrosiveWasteStrings.CORROSIVE_ELEMENT,
                (string)CorrosiveWasteStrings.CORROSIVE_ELEMENT
            );
        }
    }

    // Hook the manager once the Game spawns (mirrors common mod init pattern)
    [HarmonyPatch(typeof(Game), "OnSpawn")]
    internal static class Game_OnSpawn_Patch
    {
        private static void Postfix(Game __instance)
        {
            if (__instance != null && __instance.gameObject.GetComponent<NuclearWasteCorrosionManager>() == null)
            {
                __instance.gameObject.AddComponent<NuclearWasteCorrosionManager>();
            }
        }
    }



    internal class NuclearWasteRadiation
    {


        [HarmonyPatch(typeof(ElementLoader))]
        [HarmonyPatch(nameof(ElementLoader.CollectElementsFromYAML))]
        public static class Patch_ElementLoader_CollectElementsFromYAML_NuclearWaste
        {
            [HarmonyPriority(Priority.LowerThanNormal)]
            public static void Postfix(List<ElementEntry> __result)
            {
                if (__result == null) return;

                var nuclearWaste = __result.FirstOrDefault(ele => ele.elementId == nameof(SimHashes.NuclearWaste));
                if (nuclearWaste != null)
                {
                    nuclearWaste.radiationPer1000Mass *= 5f;
                }
            }
        }
    }

    // Adds an additional scalding source: standing in Nuclear Waste (> 100 kg) counts as scalding.
    // This does NOT replace the vanilla temperature-based check; it augments it.
    [HarmonyPatch(typeof(ScaldingMonitor.Instance), nameof(ScaldingMonitor.Instance.IsScalding))]
    internal static class NuclearWasteScaldingAugmentPatch
    {
        // Threshold in kilograms
        private const float WasteMassThresholdKg = 20f;

        [HarmonyPostfix]
        private static void Postfix(ScaldingMonitor.Instance __instance, ref bool __result)
        {
            try
            {
                // If already scalding by temperature, keep it
                if (__result)
                    return;

                // Otherwise, add nuclear waste condition
                if (IsOnHighMassNuclearWaste(__instance))
                    __result = true;
            }
            catch (Exception e)
            {
           //     Debug.LogWarning($"[Rephysicalized] NuclearWasteScaldingAugmentPatch failed: {e}");
                // On failure, leave original result unchanged
            }
        }

        private static bool IsOnHighMassNuclearWaste(ScaldingMonitor.Instance smi)
        {
            if (smi == null || smi.gameObject == null)
                return false;

            int rootCell = Grid.PosToCell(smi.gameObject);
            if (!Grid.IsValidCell(rootCell))
                return false;

            // Prefer OccupyArea if present (covers all cells for tall/wide creatures)
            var occupyArea = smi.gameObject.GetComponent<OccupyArea>();
            if (occupyArea != null && occupyArea.OccupiedCellsOffsets != null && occupyArea.OccupiedCellsOffsets.Length > 0)
            {
                for (int i = 0; i < occupyArea.OccupiedCellsOffsets.Length; i++)
                {
                    int c = Grid.OffsetCell(rootCell, occupyArea.OccupiedCellsOffsets[i]);
                    if (!Grid.IsValidCell(c))
                        continue;

                    if (Grid.Element[c].id == SimHashes.NuclearWaste && Grid.Mass[c] > WasteMassThresholdKg)
                        return true;
                }
                return false;
            }

            // Fallback: check just the root cell
            return Grid.Element[rootCell].id == SimHashes.NuclearWaste && Grid.Mass[rootCell] > WasteMassThresholdKg;
        }
    }


} 