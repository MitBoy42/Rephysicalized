using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TUNING;
using UnityEngine;


namespace Rephysicalized
{

    [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
  
public static class ExtendFoundationTemperatureExtents_ForAllBuildings
    {

        [HarmonyPostfix]

        public static void Postfix(BuildingComplete __instance)
        {

            if (!Config.Instance.BuildingFoundationTemperatureExchange) return;
            var building = __instance.GetComponent<Building>();


                var def = building.Def;


                // Exclusions
                if (def.IsTilePiece ||
                    def.BuildLocationRule == BuildLocationRule.Conduit ||
                    def.BuildLocationRule == BuildLocationRule.WireBridge ||
                    def.BuildLocationRule == BuildLocationRule.LogicBridge)
                {
                    return;
                }

                // Prefer Rotatable’s notion of current orientation
                var rot = __instance.GetComponent<Rotatable>();
                Orientation orientation = rot != null ? rot.GetOrientation() : building.Orientation;

                var dirs = GetExtensionDirections(def.BuildLocationRule, orientation);
                if (dirs.Count == 0)
                    return;

                var ext = building.GetExtents();
                var extended = MergeExtended(ext, dirs);

                if (extended.x == ext.x && extended.y == ext.y && extended.width == ext.width && extended.height == ext.height)
                    return;

                var handle = GameComps.StructureTemperatures.GetHandle(__instance.gameObject);
                if (!handle.IsValid()) return;

                var payload = GameComps.StructureTemperatures.GetPayload(handle);
                payload.OverrideExtents(extended);
                GameComps.StructureTemperatures.SetPayload(handle, ref payload);
            }
        

        private static List<Vector2Int> GetExtensionDirections(BuildLocationRule rule, Orientation orientation)
        {

        
            var result = new List<Vector2Int>();

            switch (rule)
                {
                    case BuildLocationRule.OnFloor:
                    case BuildLocationRule.OnFloorOverSpace:
                        // Fixed foundation below
                        result.Add(Down);
                        break;

                    case BuildLocationRule.OnCeiling:
                        // Fixed foundation above
                        result.Add(Up);
                        break;

                    case BuildLocationRule.OnWall:
                        // Horizontal side depends on orientation
                        result.Add(HorizontalDirForWall(orientation));
                        break;

                    case BuildLocationRule.OnFoundationRotatable:
                        // Foundation side rotates with orientation (see Rotatable rotations)
                        result.Add(DirForFoundationRotatable(orientation));
                        break;

                    case BuildLocationRule.WallFloor:
                        // Connected to wall and floor
                        result.Add(Down);
                        AddUnique(result, HorizontalDirForWall(orientation));
                        break;

                    case BuildLocationRule.InCorner:
                        // Connected to wall and ceiling
                        result.Add(Up);
                        AddUnique(result, HorizontalDirForWall(orientation));
                        break;

                    default:
                        // skip other rules
                        break;
                }

                return result;
            }
        

        private static readonly Vector2Int Up = new Vector2Int(0, +1);
        private static readonly Vector2Int Down = new Vector2Int(0, -1);
        private static readonly Vector2Int Left = new Vector2Int(-1, 0);
        private static readonly Vector2Int Right = new Vector2Int(+1, 0);

        private static void AddUnique(List<Vector2Int> list, Vector2Int v)
        {
            foreach (var e in list)
                if (e == v) return;
            list.Add(v);
        }

        private static Vector2Int HorizontalDirForWall(Orientation o)
        {
           
            switch (o)
            {
                case Orientation.FlipH: return Right;
                default: return Left;
            }
        }

        // Orientation mapping for rotatable foundations:
        // Derived from Rotatable.GetRotatedCellOffset 90° steps.
        private static Vector2Int DirForFoundationRotatable(Orientation o)
        {
            switch (o)
            {
                case Orientation.R90: return Left;   // 90° cw rotates "down" to "left"
                case Orientation.R180: return Up;
                case Orientation.R270: return Right;
                case Orientation.FlipH: return Right;
                case Orientation.FlipV: return Up;
                default: return Down;   // Neutral
            }
        }

        // Create a bounding rectangle that covers base extents and the added 1-cell strips in given directions.
        private static Extents MergeExtended(Extents baseExt, List<Vector2Int> dirs)
        {
            int minX = baseExt.x;
            int minY = baseExt.y;
            int maxX = baseExt.x + baseExt.width - 1;
            int maxY = baseExt.y + baseExt.height - 1;

            foreach (var d in dirs)
            {
                if (d.x < 0) minX -= 1;
                if (d.x > 0) maxX += 1;
                if (d.y < 0) minY -= 1;
                if (d.y > 0) maxY += 1;
            }

            return new Extents(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    [HarmonyPatch(typeof(SolarPanelConfig), nameof(SolarPanelConfig.DoPostConfigureComplete))]
    public static class SolarPanel_OccupyFoundation_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {

            if (!Config.Instance.BuildingFoundationTemperatureExchange) return;
            
            // Ensure a MakeBaseSolid.Def exists and set it to occupy the foundation layer
            var def = go.AddOrGetDef<MakeBaseSolid.Def>();
            def.occupyFoundationLayer = true;

            // Keep the 7-wide row of offsets the vanilla config sets; if not present, initialize to default row
            if (def.solidOffsets == null || def.solidOffsets.Length == 0)
            {
                def.solidOffsets = new CellOffset[7];
                for (int i = 0; i < 7; i++)
                    def.solidOffsets[i] = new CellOffset(i - 3, 0);
            }
        }
    }

 
}


