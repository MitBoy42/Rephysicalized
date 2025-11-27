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

    internal static class HEP_UraniumImpact_Util
    {
        internal static bool IsUranium(SimHashes elem) =>
            elem == SimHashes.UraniumOre || elem == SimHashes.EnrichedUranium || elem == SimHashes.DepletedUranium;

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

            if (!Grid.IsValidCell(cell) || !Grid.IsSolidCell(cell))
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

        // Computes spawn temperature for Enriched Uranium; target 3000 K with a safe clamp
        internal static float ComputeEnrichedSpawnTempK(int cell)
        {
            const float targetK = 3000f; // requested target
            var enriched = ElementLoader.FindElementByHash(SimHashes.EnrichedUranium);
            if (enriched == null)
                return targetK; // fallback: exact target

            float minK = enriched.lowTemp + 1f;
            float maxK = enriched.highTemp - 1f;
            return Mathf.Clamp(targetK, minK, maxK);
        }
    }

    // Always fire VFX/SFX when colliding with uranium solids
    [HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.Collide))]
    internal static class HEP_UraniumImpact_FX
    {
        private static void Postfix(HighEnergyParticle __instance, HighEnergyParticle.CollisionType collisionType)
        {

            if (collisionType != HighEnergyParticle.CollisionType.Solid)
                return;

            int cell = Grid.PosToCell(__instance.transform.GetPosition());
            if (!Grid.IsValidCell(cell)) return;

            if (!HEP_UraniumImpact_Util.TryGetSolidAtCell(cell, out var elem, out _, out _) || !HEP_UraniumImpact_Util.IsUranium(elem))
                return;

            Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront);

            // Visual FX
            Game.Instance.SpawnFX(SpawnFXHashes.MeteorImpactUranium, pos, 0f);

            // Audio: try a few candidates
            string[] sfxCandidates =
            {
                    "Meteor_Nuclear_Impact",
                    "Meteor_Impact_Uranium",
                    "Meteor_Impact_Nuclear"
                };

            foreach (var evt in sfxCandidates)
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

    // Replace vanilla payload emission when impacting uranium-type solids:

    [HarmonyPatch(typeof(HighEnergyParticle.States), "EmitRemainingPayload")]
    internal static class HEP_EmitRemainingPayload_UraniumOnlyEnriched
    {
        public static bool Prefix(HighEnergyParticle.StatesInstance smi)
        {
            try
            {
                var hep = smi.master;
                if (hep == null) return true;
                if (hep.collision != HighEnergyParticle.CollisionType.Solid) return true;

                int cell = Grid.PosToCell(hep.gameObject);
                if (!HEP_UraniumImpact_Util.TryGetSolidAtCell(cell, out var solidElem, out var buildingPE, out _))
                    return true;
                if (!HEP_UraniumImpact_Util.IsUranium(solidElem))
                    return true;

                // NEW: launch nearby items (with internal debug logging)
                HEP_UraniumImpact_Util.LaunchNearbyStuff(cell);

                hep.emitter.emitRadiusX = 6;
                hep.emitter.emitRadiusY = 6;
                hep.emitter.emitRads = (float)((hep.payload * 0.5f) * 1400.0f / 9.0f);
                hep.emitter.Refresh();

                float totalMassKg = hep.payload * 0.001f;
                if (totalMassKg > 0f)
                {
                    float spawnTempK = HEP_UraniumImpact_Util.ComputeEnrichedSpawnTempK(cell);

                    var enriched = ElementLoader.FindElementByHash(SimHashes.EnrichedUranium);
                    if (enriched != null)
                    {
                        Vector3 spawnPos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);
                        enriched.substance.SpawnResource(
                            spawnPos,
                            totalMassKg,
                            spawnTempK,
                            byte.MaxValue,
                            0
                        );
                    }

                    HEP_UraniumImpact_Util.RemoveSolidMassAtCell(cell, totalMassKg);

                    float deltaK = Mathf.Clamp(hep.payload * 0.2f, 10f, 300f);
                    HEP_UraniumImpact_Util.IncreaseSolidTemperatureAtCell(cell, deltaK);


                }

                // Destroy particle after delay (vanilla behavior)
                smi.Schedule(1f, _ => UnityEngine.Object.Destroy(hep.gameObject), null);

                // We fully handled emission
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rephysicalized] Uranium emission prefix failed, falling back to vanilla: " + e);
                return true;
            }
        }

    }
}