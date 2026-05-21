using KSerialization;
using System;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches
{

    // Full credit to Powerfull's Organic Overhaul for the setup, this code is significantly different from the original.
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
        public float DeltaT = 0f;

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
            // ElementChunk absorbs keep the surviving Pickupable, but it is not guaranteed that the
            // surviving OreDissolves component has consistent runtime state after the merge.
            //
            // Keep runtime references coherent after absorb/merge.
            // Dissolve logic must re-read pickupable.PrimaryElement.Mass each tick to avoid stale cached state.
            if (data is Pickupable p && p.PrimaryElement != null)

            {
                // Force our local references to the surviving pickupable.
                pickupable = p;

                if (pickupable?.PrimaryElement?.Element != null)
                {
                    this.OreSHC = pickupable.PrimaryElement.Element.specificHeatCapacity;
                }

                if (ElementLoader.FindElementByHash(this.absorbedFluid) is Element el)
                {
                    this.absorbedFluidSHC = el.specificHeatCapacity;
                }


            }
        }

        private void OnSplitFromChunk(object data)
        {

        }


        public void Sim4000ms(float dt)
        {
            if (this.pickupable == null)
                this.pickupable = this.GetComponent<Pickupable>();

            if (this.pickupable == null || this.pickupable.PrimaryElement == null)
                return;

            if (Config.Instance != null && !Config.Instance.DebrisDissolving)
                return;

            if (this.respectsealed && this.pickupable.KPrefabID.HasTag(GameTags.Sealed))
                return;


            // Keep SHC values coherent with current elements.
            if (this.OreSHC <= 0f)
                this.OreSHC = this.pickupable.PrimaryElement.Element.specificHeatCapacity;

            if (ElementLoader.FindElementByHash(this.absorbedFluid) is Element el && this.absorbedFluidSHC <= 0f)
                this.absorbedFluidSHC = el.specificHeatCapacity;

            this.EvaluateCell(dt);
        }

        private void EvaluateCell(float dt)
        {
            // When multiple element chunks stack, some component instances can have a stale
            // transform/cell relationship for a moment. Instead of trusting our own position,
            // check the grid cell where the absorbed fluid is and dissolve based on actual cell state.

            if (this.pickupable == null || this.pickupable.PrimaryElement == null)
                return;

            // Use Pickupable's cached cell for stability across absorb/stack merges.
            // This avoids transform/cell drift when stacked chunks merge.
            int cell = this.pickupable.GetCell();
            if (!Grid.IsValidCell(cell))
                return;

            if (Grid.Element[cell] == null)
                return;

            if (Grid.Element[cell].id != this.absorbedFluid)
                return;

            this.Dissolve(cell);
        }

        public void Dissolve(int cell)
        {
            // Safety: this sim can tick on partially-initialized prefabs.

            if (this.pickupable == null || this.pickupable.PrimaryElement == null)
                return;

            // Caller already computed the canonical cell; do not recompute again.
            if (!Grid.IsValidCell(cell))
                return;

            if (Grid.Element[cell] == null)
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
            float num = this.workingmass * this.usedoreratio / this.pickupable.PrimaryElement.Mass;

            if (float.IsNaN(num) || float.IsInfinity(num))
                return;

            if (FallingWater.instance == null)
                return;


            if (flag1)

            {
                // Reaction consumes both ore and absorbed fluid.
                float conversionMass = this.workingmass * this.conversionspeed;
                float oreRatio = this.usedoreratio;

                float oreConsumedMass = conversionMass * oreRatio;
                float fluidConsumedMass = conversionMass * (1f - oreRatio);

                // FX: play dissolving splash step when this tick actually reacts.
                Vector3 fxPos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront) + new Vector3(-0.5f, 0.0f, 0.0f);
                if (Game.Instance != null)

                    Game.Instance.SpawnFX(SpawnFXHashes.SplashStep, fxPos, 0.0f);



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
                    var oreElem = ElementLoader.GetElement(this.emittedore.CreateTag());
                    if (oreElem?.substance == null)
                        return;

                    oreElem.substance.SpawnResource(

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
                {
                    // FX: play dissolving splash step when this tick actually reacts.
                    Vector3 fxPos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront) + new Vector3(-0.3f, 0.2f, 0.0f);

                    if (Game.Instance != null)
                        Game.Instance.SpawnFX(SpawnFXHashes.SplashStep, fxPos, 0.0f);

                    SimMessages.ConsumeMass(cell, this.absorbedFluid, consumeMass, (byte)2);
                }


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
                    var oreElem = ElementLoader.GetElement(this.emittedore.CreateTag());
                    if (oreElem?.substance == null)
                        return;

                    oreElem.substance.SpawnResource(
                        Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore),
                        emitOreMass,
                        temperature,
                        this.pickupable.PrimaryElement.DiseaseIdx,
                        (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)num));
                }

                this.pickupable.PrimaryElement.Mass -= oreConsumedMass;
                this.UpdateStorage();
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

