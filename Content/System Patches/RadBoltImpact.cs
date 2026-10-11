using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized
{
    // Make HighEnergyParticle (radbolt) deal damage equal to its payload instead of a fixed 20 HP
    // We specifically target the two calls within HighEnergyParticle.CheckCollision that do:
    //   Health.Damage(20f)
    // and replace the constant 20f with `this.payload` via a transpiler.
    [HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.CheckCollision))]
    public static class HighEnergyParticle_CheckCollision_DamageEqualsPayload
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var payloadField = AccessTools.Field(typeof(HighEnergyParticle), "payload");

            for (int i = 0; i < code.Count; i++)
            {
                var instr = code[i];
                // Look for the constant 20f used for damage and replace with this.payload
                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - 20f) < 0.0001f)
                {
                    code[i] = new CodeInstruction(OpCodes.Ldarg_0);
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Ldfld, payloadField));
                    i++; // skip over the inserted ldfld
                }
            }

            return code;
        }
    }

    // Generic utilities for HEP solid impacts
    internal static class HEP_Impact_Util
    {
        // Returns the available mass on the impacted cell/building that can be removed/emitted.
        // For buildings, mass lives on the PrimaryElement.
        internal static bool TryGetSolidAvailableMassAtCell(int cell, out float availableKg)
        {
            availableKg = 0f;
            if (!TryGetSolidAtCell(cell, out _, out PrimaryElement buildingPE, out _))
                return false;

            if (buildingPE != null)
            {
                availableKg = buildingPE.Mass;
                return true;
            }

            availableKg = Grid.Mass[cell];
            return true;
        }

        internal static void LaunchNearbyStuff(int cell)
        {

            if (!Grid.IsValidCell(cell))
                return;

            var gathered = ListPool<ScenePartitionerEntry, Comet>.Allocate();
            try
            {
                Grid.CellToXY(cell, out int cx, out int cy);
                GameScenePartitioner.Instance.GatherEntries(
                    cx - 1,
                    cy - 1,
                    3, 3,
                    GameScenePartitioner.Instance.pickupablesLayer,
                    gathered
                );

                Vector3 origin = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);

                for (int i = 0; i < gathered.Count; i++)
                {
                    var entry = gathered[i];
                    if (entry.obj == null)
                        continue;

                    var pickupable = entry.obj as Pickupable;
                    if (pickupable == null)
                        continue;

                    GameObject go = pickupable.gameObject;
                    if (go == null)
                        continue;

                    // Skip stored items
                    var kpid = pickupable.GetComponent<KPrefabID>();
                    if (kpid != null && kpid.HasTag(GameTags.Stored))
                        continue;

                    // Skip dupes, critters, and robots
                    if (go.GetComponent<MinionIdentity>() != null ||
                        go.GetComponent<CreatureBrain>() != null ||
                        go.GetDef<RobotAi.Def>() != null)
                        continue;

                    Vector3 goPos = go.transform.GetPosition();
                    Vector2 initialVelocity = (Vector2)(goPos - origin);

                    initialVelocity = initialVelocity.normalized;
                    initialVelocity *= UnityEngine.Random.Range(1f, 2f);
                    initialVelocity.y += UnityEngine.Random.Range(1f, 2f);

                    if (GameComps.Fallers.Has(go))
                        GameComps.Fallers.Remove(go);
                    if (GameComps.Gravities.Has(go))
                        GameComps.Gravities.Remove(go);

                    GameComps.Fallers.Add(go, initialVelocity);
                }
            }
            finally
            {
                gathered.Recycle();
            }
        }

        // Returns true if a solid exists at cell; outputs the solid element and (optionally) the building PE if present
        internal static bool TryGetSolidAtCell(int cell, out SimHashes solidElem, out PrimaryElement buildingPE, out GameObject buildingGo)
        {
            solidElem = SimHashes.Vacuum;
            buildingPE = null;
            buildingGo = null;

            if (!Grid.IsValidCell(cell) || !Grid.IsSolidCell(cell) || Grid.Foundation[cell])
                return false;



            // Building layer
            GameObject go = Grid.Objects[cell, (int)ObjectLayer.Building];
            if (go != null)
            {
                buildingGo = go;
                buildingPE = go.GetComponent<PrimaryElement>();
                if (buildingPE != null && buildingPE.ElementID != SimHashes.Vacuum)
                {
                    solidElem = buildingPE.ElementID;
                    return true;
                }
            }

            solidElem = Grid.Element[cell].id;
            return true;
        }

        // Removes up to removeKg mass from the impacted solid at cell.
        internal static void RemoveSolidMassAtCell(int cell, float removeKg)
        {
            if (removeKg <= 0f) return;

            if (!TryGetSolidAtCell(cell, out var solidElem, out var pe, out _))
                return;

            if (pe != null)
            {
                // Building mass lives on PrimaryElement, not in sim cell
                float newMass = Mathf.Max(0f, pe.Mass - removeKg);
                pe.Mass = newMass;
                return;
            }

            // Natural solid in sim
            float available = Grid.Mass[cell];
            float delta = Mathf.Min(available, removeKg);
            if (delta <= 0f) return;

            // Remove from existing solid element
            var diseaseIdx = Grid.DiseaseIdx[cell];
            var diseaseCount = Grid.DiseaseCount[cell];
            float cellTemp = Grid.Temperature[cell];

            // Negative mass removes from the cell
            SimMessages.AddRemoveSubstance(
                cell,
                solidElem,
                CellEventLogger.Instance.ElementEmitted,
                -delta,
                cellTemp,
                diseaseIdx,
                diseaseCount
            );
        }

        // Increase temperature of the impacted solid at cell by deltaK (Kelvin/Celsius)
        internal static void IncreaseSolidTemperatureAtCell(int cell, float deltaK)
        {
            if (deltaK <= 0f) return;

            if (!TryGetSolidAtCell(cell, out var solidElem, out var pe, out _))
                return;

            // Natural solid cell: add energy = m * c * ΔT (kJ)
            Element element = ElementLoader.FindElementByHash(solidElem);
            float mass = Grid.Mass[cell];
            if (element != null && mass > 0f)
            {
                float kJ = (element.specificHeatCapacity * mass * deltaK) * 1f;

                SimMessages.ModifyEnergy(cell, kJ, 2000f, SimMessages.EnergySourceID.DebugHeat);
                return;
            }
        }


    }

    // Optional custom FX when colliding with solids, driven by impact configuration.
    [HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.Collide))]
    internal static class HEP_Impact_FX_Generic
    {
        private static void Postfix(HighEnergyParticle __instance, HighEnergyParticle.CollisionType collisionType)
        {
            if (collisionType != HighEnergyParticle.CollisionType.Solid)
                return;

            int cell = Grid.PosToCell(__instance.transform.GetPosition());
            if (!Grid.IsValidCell(cell)) return;

            if (!HEP_Impact_Util.TryGetSolidAtCell(cell, out var elem, out _, out _))
                return;

            string elemTag = elem.CreateTag().ToString();
            // RadBoltImpactList stores affectedElements as tag strings.
            var cfg = RadBoltImpactList.FindMatch(elemTag);


            if (cfg == null || cfg == null)
                return; // no custom FX -> let vanilla stand

            Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront);


            cfg.spawnVfx?.Invoke(pos);


            // Audio: try configured candidates
            if (cfg.sfxCandidates != null)
            {
                foreach (var evt in cfg.sfxCandidates)
                {
                    string sfx = GlobalAssets.GetSound(evt);
                    if (!string.IsNullOrEmpty(sfx))
                    {
                        KFMOD.PlayOneShot(sfx, pos);
                        break;
                    }
                }
            }
        }
    }

    // Replace vanilla payload emission when impacting configured solid elements.
    [HarmonyPatch(typeof(HighEnergyParticle.States), "EmitRemainingPayload")]
    internal static class HEP_EmitRemainingPayload_Configurable
    {
        public static bool Prefix(HighEnergyParticle.StatesInstance smi)
        {
            HighEnergyParticle hep = smi.master;
            if ((UnityEngine.Object)hep == (UnityEngine.Object)null || hep.collision != HighEnergyParticle.CollisionType.Solid)
                return true;
            int cell = Grid.PosToCell(hep.gameObject);
            SimHashes solidElem;
            if (!HEP_Impact_Util.TryGetSolidAtCell(cell, out solidElem, out PrimaryElement _, out GameObject _))
                return true;
            string solidTag = solidElem.CreateTag().ToString();
            RadBoltImpactList.HEPImpactConfig match = RadBoltImpactList.FindMatch(solidTag);

            if (match == null)
                return true;
            if (match.launchNearby)
                HEP_Impact_Util.LaunchNearbyStuff(cell);
            hep.emitter.emitRadiusX = (short)6;
            hep.emitter.emitRadiusY = (short)6;
            hep.emitter.emitRads = (float)((double)hep.payload * 0.5 * 1400.0 / 9.0);
            hep.emitter.Refresh();
            float num1 = hep.payload * (1f / 1000f);
            float num2 = num1 * Mathf.Max(0.0f, match.emissionMultiplier) * Config.Instance.FissionMult; ;
            float removeKg = num1 * Mathf.Max(0.0f, match.removalMultiplier) * Config.Instance.FissionMult; ;

            // Clamp emitted mass so it can't exceed the mass that can actually be depleted from the cell.
            float availableKg;
            if (!HEP_Impact_Util.TryGetSolidAvailableMassAtCell(cell, out availableKg))
                availableKg = 0f;
            float clampedRemovedKg = Mathf.Min(removeKg, availableKg);
            float clampedEmittedKg = Mathf.Min(num2, clampedRemovedKg);

            if ((double)clampedEmittedKg > 0.0)
            {

                Dictionary<string, float> emittedElementIds = match.emittedElements;
                if (emittedElementIds != null && emittedElementIds.Count > 0)
                {
                    float num3 = 0.0f;
                    foreach (KeyValuePair<string, float> keyValuePair in emittedElementIds)
                        num3 += keyValuePair.Value;
                    if ((double)num3 <= 0.0)
                        num3 = 1f;
                    Vector3 posCcc = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);
                    foreach (KeyValuePair<string, float> keyValuePair in emittedElementIds)
                    {
                        string key = keyValuePair.Key;
                        float num4 = keyValuePair.Value;
                        SimHashes result;
            

                        // Fallback: allow pure tag strings in emittedElements that are not SimHashes.
                        if (!string.IsNullOrEmpty(key))
                        {
                            Element elementByTag = ElementLoader.FindElementByTag((Tag)key);
                            if (elementByTag != null)
                            {
                                float mass = clampedEmittedKg * (num4 / num3);
                                if ((double)mass > 0.0)
                                {
                                    float spawnTempTargetK = match.spawnTempTargetK;
                                    elementByTag.substance.SpawnResource(posCcc, mass, spawnTempTargetK, byte.MaxValue, 0);
                                }
                            }
                        }

                    }
                }
            }
            if ((double)removeKg > 0.0)
                HEP_Impact_Util.RemoveSolidMassAtCell(cell, removeKg);
            float deltaK = Mathf.Clamp(hep.payload * match.tempDeltaPerPayload, match.tempDeltaMin, match.tempDeltaMax);
            HEP_Impact_Util.IncreaseSolidTemperatureAtCell(cell, deltaK);
            smi.Schedule(1f, (System.Action<object>)(_ => UnityEngine.Object.Destroy((UnityEngine.Object)hep.gameObject)), (object)null);
            return false;
        }
    }
}

