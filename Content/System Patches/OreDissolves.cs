// Decompiled with JetBrains decompiler
// Type: Rephysicalized.Content.System_Patches.OreDissolves
// Assembly: Rephysicalized, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 23D92239-9033-4FF1-BE3A-A22401005D7D
// Assembly location: C:\Users\MitBoy\Documents\Klei\OxygenNotIncluded\mods\Steam\3609531831\Rephysicalized.dll

using KSerialization;
using PeterHan.PLib.Options;
using System;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches
{
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
        public float DeltaT = 0.0f;
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
        private static readonly EventSystem.IntraObjectHandler<OreDissolves> OnAbsorbDelegate = new EventSystem.IntraObjectHandler<OreDissolves>((System.Action<OreDissolves, object>)((c, d) => c.OnAbsorb(d)));
        private static readonly EventSystem.IntraObjectHandler<OreDissolves> OnSplitFromChunkDelegate = new EventSystem.IntraObjectHandler<OreDissolves>((System.Action<OreDissolves, object>)((c, d) => c.OnSplitFromChunk(d)));

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            this.Subscribe<OreDissolves>(-2064133523, OreDissolves.OnAbsorbDelegate);
            this.Subscribe<OreDissolves>(1335436905, OreDissolves.OnSplitFromChunkDelegate);
            Element elementByHash = ElementLoader.FindElementByHash(this.absorbedFluid);
            if (elementByHash != null)
                this.absorbedFluidSHC = elementByHash.specificHeatCapacity;
            this.OreSHC = this.pickupable.PrimaryElement.Element.specificHeatCapacity;
        }

        private void OnAbsorb(object data)
        {
            if (!(data is Pickupable pickupable) || !((UnityEngine.Object)pickupable.PrimaryElement != (UnityEngine.Object)null))
                return;
            this.pickupable = pickupable;
            if (this.pickupable?.PrimaryElement?.Element != null)
                this.OreSHC = this.pickupable.PrimaryElement.Element.specificHeatCapacity;
            Element elementByHash = ElementLoader.FindElementByHash(this.absorbedFluid);
            if (elementByHash != null)
                this.absorbedFluidSHC = elementByHash.specificHeatCapacity;
        }

        private void OnSplitFromChunk(object data)
        {
        }

        public void Sim4000ms(float dt)
        {
            if ((UnityEngine.Object)this.pickupable == (UnityEngine.Object)null)
                this.pickupable = this.GetComponent<Pickupable>();
            if ((UnityEngine.Object)this.pickupable == (UnityEngine.Object)null || (UnityEngine.Object)this.pickupable.PrimaryElement == (UnityEngine.Object)null || SingletonOptions<Config>.Instance != null && !SingletonOptions<Config>.Instance.DebrisDissolving || this.respectsealed && this.pickupable.KPrefabID.HasTag(GameTags.Sealed))
                return;
            if ((double)this.OreSHC <= 0.0)
                this.OreSHC = this.pickupable.PrimaryElement.Element.specificHeatCapacity;
            Element elementByHash = ElementLoader.FindElementByHash(this.absorbedFluid);
            if (elementByHash != null && (double)this.absorbedFluidSHC <= 0.0)
                this.absorbedFluidSHC = elementByHash.specificHeatCapacity;
            this.EvaluateCell(dt);
        }

        private void EvaluateCell(float dt)
        {
            if ((UnityEngine.Object)this.pickupable == (UnityEngine.Object)null || (UnityEngine.Object)this.pickupable.PrimaryElement == (UnityEngine.Object)null)
                return;
            int cell = this.pickupable.GetCell();
            if (!Grid.IsValidCell(cell) || Grid.Element[cell] == null || Grid.Element[cell].id != this.absorbedFluid)
                return;
            this.Dissolve(cell);
        }

        public void Dissolve(int cell)
        {
            if ((UnityEngine.Object)this.pickupable == (UnityEngine.Object)null || (UnityEngine.Object)this.pickupable.PrimaryElement == (UnityEngine.Object)null || !Grid.IsValidCell(cell) || Grid.Element[cell] == null || Grid.Element[cell].id != this.absorbedFluid)
                return;
            this.celltemp = Grid.Temperature[cell];
            this.debristemp = this.pickupable.PrimaryElement.Temperature;
            if ((double)this.celltemp <= 1.0 || (double)this.debristemp <= 1.0)
                return;
            float temperature = Math.Max((float)(((double)this.celltemp * (double)this.absorbedFluidSHC * (1.0 - (double)this.usedoreratio) + (double)this.debristemp * (double)this.OreSHC * (double)this.usedoreratio) / ((double)this.absorbedFluidSHC * (1.0 - (double)this.usedoreratio) + (double)this.OreSHC * (double)this.usedoreratio)) + this.DeltaT, 1f);
            this.mass = this.pickupable.PrimaryElement.Mass;
            this.cellmass = Grid.Mass[cell];
            this.workingmass = this.mass;
            bool flag = (double)this.workingmass * (double)this.usedoreratio > (double)this.minmass;
            float f = this.workingmass * this.usedoreratio / this.pickupable.PrimaryElement.Mass;
            if (float.IsNaN(f) || float.IsInfinity(f) || (UnityEngine.Object)FallingWater.instance == (UnityEngine.Object)null)
                return;
            if (flag)
            {
                float num1 = this.workingmass * this.conversionspeed;
                float usedoreratio = this.usedoreratio;
                float num2 = num1 * usedoreratio;
                float mass1 = num1 * (1f - usedoreratio);
                Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront) + new Vector3(-0.5f, 0.0f, 0.0f);
                if ((UnityEngine.Object)Game.Instance != (UnityEngine.Object)null)
                    Game.Instance.SpawnFX(SpawnFXHashes.SplashStep, pos, 0.0f);
                SimMessages.ConsumeMass(cell, this.absorbedFluid, mass1, (byte)2);
                this.pickupable.PrimaryElement.Mass -= num2;
                this.pickupable.PrimaryElement.ModifyDiseaseCount((int)((double)(-1 * this.pickupable.PrimaryElement.diseaseCount) * (double)f), "Dissolving");
                this.UpdateStorage();
                float base_mass = num1 * this.EmittedFluidMult;
                if (this.emittedfluid != SimHashes.Vacuum && (double)base_mass > 0.0)
                    FallingWater.instance.AddParticle(cell, ElementLoader.GetElementIndex(this.emittedfluid), base_mass, temperature, this.pickupable.PrimaryElement.DiseaseIdx, (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)f * (1.0 / (double)this.usedoreratio / (1.0 - (double)this.usedoreratio))), true);
                float mass2 = num1 * this.EmittedOreMult;
                if (this.emittedore == SimHashes.Vacuum)
                    return;
                Element element = ElementLoader.GetElement(this.emittedore.CreateTag());
                if (element?.substance == null)
                    return;
                element.substance.SpawnResource(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore), mass2, temperature, this.pickupable.PrimaryElement.DiseaseIdx, (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)f * (1.0 / (double)this.usedoreratio)));
            }
            else
            {
                if (flag)
                    return;
                float num3 = this.minmass * this.conversionspeed;
                float usedoreratio = this.usedoreratio;
                float num4 = num3 * usedoreratio;
                float num5 = num3 * (1f - usedoreratio);
                float mass3 = num5;
                float mass4 = (num4 + num5) * this.EmittedOreMult;
                float base_mass = (num4 + num5) * this.EmittedFluidMult;
                if ((double)mass3 > 0.0)
                {
                    Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront) + new Vector3(-0.3f, 0.2f, 0.0f);
                    if ((UnityEngine.Object)Game.Instance != (UnityEngine.Object)null)
                        Game.Instance.SpawnFX(SpawnFXHashes.SplashStep, pos, 0.0f);
                    SimMessages.ConsumeMass(cell, this.absorbedFluid, mass3, (byte)2);
                }
                if (this.emittedfluid != SimHashes.Vacuum && (double)base_mass > 0.0)
                    FallingWater.instance.AddParticle(cell, ElementLoader.GetElementIndex(this.emittedfluid), base_mass, temperature, this.pickupable.PrimaryElement.DiseaseIdx, (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)f), true);
                if (this.emittedore != SimHashes.Vacuum)
                {
                    Element element = ElementLoader.GetElement(this.emittedore.CreateTag());
                    if (element?.substance == null)
                        return;
                    element.substance.SpawnResource(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore), mass4, temperature, this.pickupable.PrimaryElement.DiseaseIdx, (int)((double)this.pickupable.PrimaryElement.diseaseCount * (double)f));
                }
                this.pickupable.PrimaryElement.Mass -= num4;
                this.UpdateStorage();
            }
        }

        private void UpdateStorage()
        {
            Pickupable component = this.GetComponent<Pickupable>();
            if (component == null || component.storage == null)
                return;
            component.storage.Trigger(-1697596308, (object)this.gameObject);
        }

        public override void OnCleanUp() => base.OnCleanUp();
    }
}
