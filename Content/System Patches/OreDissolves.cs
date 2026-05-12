﻿using KSerialization;
using System;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches
{
    // Toggle debug output without spamming logs everywhere.
    internal static class OreDissolvesDebug
    {
        public const bool Enabled = true;
    }

    // Full credit to Powerfull's Organic Overhaul for this code. This is a direct copy.
    internal class OreDissolves : KMonoBehaviour, ISim4000ms
    {
        [Serialize]
        public float usedoreratio = 1f;

        public float conversionspeed;
        [Serialize]
        public float minmass = 1f;

        [Serialize]
        public bool respectsealed = true;

        [Serialize]
        public SimHashes absorbedFluid;

        [Serialize]
        public float absorbedFluidSHC;

        [Serialize]
        public float OreSHC;

        [Serialize]
        public float DeltaT;

        // Default emitted elements are Vacuum so nothing spawns unless explicitly configured.
        [Serialize]
        [HashedEnum]
        public SimHashes emittedfluid = SimHashes.Vacuum;

        [Serialize]
        public float EmittedFluidMult = 1f;

        [Serialize]
        public SimHashes emittedore = SimHashes.Vacuum;

        [Serialize]
        public float EmittedOreMult = 1f;


        [Serialize]
        public float processedMass;

        [MyCmpGet]
        private Pickupable pickupable;

        private float mass;
        private float cellmass;
        private float workingmass;

        private float debristemp;
        private float celltemp;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe(-2064133523, OnAbsorbDelegate);
            Subscribe(1335436905, OnSplitFromChunkDelegate);

            if (ElementLoader.FindElementByHash(this.absorbedFluid) is Element el)
                this.absorbedFluidSHC = el.specificHeatCapacity;

            this.OreSHC = pickupable.PrimaryElement.Element.specificHeatCapacity;
        }

        private static readonly EventSystem.IntraObjectHandler<OreDissolves> OnAbsorbDelegate = new((c, d) => c.OnAbsorb(d));
        private static readonly EventSystem.IntraObjectHandler<OreDissolves> OnSplitFromChunkDelegate = new((c, d) => c.OnSplitFromChunk(d));

        private void OnAbsorb(object data)
        {
            if (data is Pickupable p)
            {
                var other = p.GetComponent<OreDissolves>();
                if (other)
                    processedMass += other.processedMass;
            }
        }

        private void OnSplitFromChunk(object data)
        {
            if (data is Pickupable p)
            {
                var split = p.GetComponent<OreDissolves>();
                if (split)
                {
                    float total = mass + p.PrimaryElement.Mass;
                    float fraction = mass / total;
                    processedMass = split.processedMass * fraction;
                    split.processedMass *= 1f - fraction;
                }
            }
        }

        public void Sim4000ms(float dt)
        {
            if (this.respectsealed && this.pickupable.KPrefabID.HasTag(GameTags.Sealed))
                return;

            this.EvaluateCell(dt);
        }

        private void EvaluateCell(float dt)
        {
            int cell = Grid.PosToCell(this.transform.GetPosition());
            if (!Grid.IsValidCell(cell) || Grid.Element[cell].id != this.absorbedFluid)
                return;

            this.Dissolve(cell);
        }

        public void Dissolve(int cell)
        {
            // Safety: this sim can tick on partially-initialized prefabs.

            // Re-fetch current position-based cell to handle movement since last tick
            Vector3 currentPos = this.transform.GetPosition();
            cell = Grid.PosToCell(currentPos);
            if (!Grid.IsValidCell(cell))
                return;

            if (Grid.Element[cell].id != this.absorbedFluid)
                return;

            this.celltemp = Grid.Temperature[cell];

            this.debristemp = this.pickupable.PrimaryElement.Temperature;
            if ((double)this.celltemp <= 1.0 || (double)this.debristemp <= 1.0)
                return;

            float temperature = Math.Max(
                (float)(
                    ((double)this.celltemp * (double)this.absorbedFluidSHC * (double)(1f - this.usedoreratio) +
                     (double)this.debristemp * (double)this.OreSHC * (double)this.usedoreratio) /
                    ((double)this.absorbedFluidSHC * (double)(1f - this.usedoreratio) +
                     (double)this.OreSHC * (double)this.usedoreratio)) + this.DeltaT,
                1f);

            this.mass = this.pickupable.PrimaryElement.Mass;
            this.cellmass = Grid.Mass[cell];
            this.workingmass = this.mass;

            bool flag1 = (double)this.workingmass * (double)this.usedoreratio > (double)this.minmass;
            bool flag2 = (double)this.cellmass > (double)this.workingmass * (double)(1f - this.usedoreratio);
            float num = this.workingmass * this.usedoreratio / this.pickupable.PrimaryElement.Mass;

            if (flag1 & flag2)
            {
                // Reaction consumes both ore and absorbed fluid.
                float conversionMass = this.workingmass * this.conversionspeed;
                float oreRatio = this.usedoreratio;

                float oreConsumedMass = conversionMass * oreRatio;
                float fluidConsumedMass = conversionMass * (1f - oreRatio);

                SimMessages.ConsumeMass(cell, this.absorbedFluid, fluidConsumedMass, (byte)2);
                this.pickupable.PrimaryElement.Mass -= oreConsumedMass;
                this.pickupable.PrimaryElement.ModifyDiseaseCount((int)((double)(-1 * this.pickupable.PrimaryElement.diseaseCount) * (double)num), "Dissolving");
                this.UpdateStorage();

                // Emit both if both are configured.
                float emitFluidMass = conversionMass * this.EmittedFluidMult;
                if (this.emittedfluid != SimHashes.Vacuum && emitFluidMass > 0f)

                {
                    FallingWater.instance.AddParticle(
                        cell,
                        ElementLoader.GetElementIndex(this.emittedfluid),
                        emitFluidMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num * (double)((1 / this.usedoreratio) / (1f - this.usedoreratio))),
                        true);
                }

                float emitOreMass = conversionMass * this.EmittedOreMult;
                if (this.emittedore != SimHashes.Vacuum)

                {
                    ElementLoader.GetElement(this.emittedore.CreateTag()).substance.SpawnResource(
                        Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore),
                        emitOreMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num * (double)(1 / this.usedoreratio)));
                }
            }
            else if (!flag1)
            {
                // Ore is near depletion - dissolve remaining mass proportionally instead of instantly consuming all
                float conversionMass = this.minmass * this.conversionspeed;

                float oreRatio = this.usedoreratio;
                float oreConsumedMass = conversionMass * oreRatio;
                float fluidConsumedMass = conversionMass * (1f - oreRatio);

                float consumeMass = fluidConsumedMass;
                float emitOreMass = (oreConsumedMass + fluidConsumedMass) * this.EmittedOreMult;
                float emitFluidMass = (oreConsumedMass + fluidConsumedMass) * this.EmittedFluidMult;


                if (consumeMass > 0f)
                    SimMessages.ConsumeMass(cell, this.absorbedFluid, consumeMass, (byte)2);

                if (this.emittedfluid != SimHashes.Vacuum && emitFluidMass > 0f)
                {
                    FallingWater.instance.AddParticle(
                        cell,
                        ElementLoader.GetElementIndex(this.emittedfluid),
                        emitFluidMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num),
                        true);
                }

                if (this.emittedore != SimHashes.Vacuum)
                {
                    ElementLoader.GetElement(this.emittedore.CreateTag()).substance.SpawnResource(
                        Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore),
                        emitOreMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num));
                }

                this.pickupable.PrimaryElement.Mass -= oreConsumedMass;
                this.UpdateStorage();
            }
            else
            {
                if (flag2)
                    return;

                // Fluid-limited branch: consume ALL available liquid in the cell.
                float maxFluidForFullTick = this.workingmass * this.conversionspeed * (1f - this.usedoreratio);
                float fluidInCell = this.cellmass * (1f - this.usedoreratio);

                if (fluidInCell >= maxFluidForFullTick)
                    return;

                float fluidConsumedMass = fluidInCell;
                float oreConsumedMass = fluidConsumedMass * (this.usedoreratio / (1f - this.usedoreratio));

                if (fluidConsumedMass > 0f)
                    SimMessages.ConsumeMass(cell, this.absorbedFluid, fluidConsumedMass, (byte)2);

                this.pickupable.PrimaryElement.Mass -= oreConsumedMass;
                this.UpdateStorage();

                float emitOreMass = (oreConsumedMass + fluidConsumedMass) * this.EmittedOreMult;
                float emitFluidMass = (oreConsumedMass + fluidConsumedMass) * this.EmittedFluidMult;


                if (this.emittedfluid != SimHashes.Vacuum && emitFluidMass > 0f)
                {
                    FallingWater.instance.AddParticle(
                        cell,
                        ElementLoader.GetElementIndex(this.emittedfluid),
                        emitFluidMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num * (double)((1 / this.usedoreratio) / (1f - this.usedoreratio))),
                        true);
                }

                if (this.emittedore != SimHashes.Vacuum)
                {
                    ElementLoader.GetElement(this.emittedore.CreateTag()).substance.SpawnResource(
                        Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore),
                        emitOreMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num * (double)(1 / this.usedoreratio)));
                }
            }
        }

        private void UpdateStorage()
        {
            Pickupable component = this.GetComponent<Pickupable>();
            if (!((object)component != (object)null) || !((object)component.storage != (object)null))
                return;

            component.storage.Trigger(-1697596308, (object)this.gameObject);
        }

        public override void OnCleanUp() => base.OnCleanUp();
    }
}

