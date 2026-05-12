﻿using Epic.OnlineServices.UserInfo;
using JetBrains.Annotations;
using Rephysicalized.Content.System_Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ElementUtilNamespace;
using Rephysicalized.ModElements;

namespace Rephysicalized.Content.System_Patches
{
    // Interface for ore dissolving configurations - renamed to avoid conflict with vanilla IOreConfig
    // Use vanilla IOreConfig interface to get automatic ElementLoader registration
    public interface IOreDissolvesConfig : IOreConfig
    {
    }



    internal static class OrePrefabHelpers
    {
        public static GameObject GetOrCreateOrePrefab(SimHashes elementID)
        {
            try
            {
                var tag = new Tag(elementID.ToString());
                var existing = Assets.TryGetPrefab(tag);
                if (existing != null)
                    return existing;
            }
            catch { }

            return EntityTemplates.CreateSolidOreEntity(elementID);
        }
       
    }

    public class SaltConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Salt;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.07f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.SaltWater;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }

    public class IsoResinConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Isoresin;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ;
            oreDissolves.usedoreratio = 0.25f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.Resin;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }
    public class SugarWaterConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Sucrose;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ;
            oreDissolves.usedoreratio = 0.77f;
       
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.SugarWater;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }
    public class ToxicSandConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.ToxicSand;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.01f;
            oreDissolves.conversionspeed = 0.01f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.DirtyWater;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }

    public class GunkConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Gunk;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.01f;

            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.DirtyWater;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }

    public class BleachStoneConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.BleachStone;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.2f;

            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 100f;
            oreDissolves.emittedfluid = SimHashes.RefinedLipid;
            oreDissolves.absorbedFluid = SimHashes.PhytoOil;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }

    public class SlimeConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.SlimeMold;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.1f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 100f;
            oreDissolves.emittedfluid = SimHashes.Ethanol;
            oreDissolves.emittedore = SimHashes.Dirt;
            oreDissolves.EmittedFluidMult = 0.75f;
            oreDissolves.EmittedOreMult = 0.25f;
            oreDissolves.absorbedFluid = SimHashes.SugarWater;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
     
          
        }
    }
    public class ToxicMudConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.ToxicMud;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddComponent<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.025f;
            oreDissolves.conversionspeed = 0.01f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedfluid = SimHashes.DirtyWater;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }


    public class IronConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Iron;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.6f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedore = SimHashes.Rust;
            oreDissolves.absorbedFluid = SimHashes.Water;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }
    public class CopperConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Copper;

        public GameObject CreatePrefab()
        {
                GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            if (Config.Instance.RephysicalizedMetalOre)
            {
                var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
                oreDissolves.usedoreratio = 0.8f;
                oreDissolves.conversionspeed = 0.02f;
                oreDissolves.minmass = 10f;
                oreDissolves.emittedore = SimHashes.Cuprite;
                oreDissolves.absorbedFluid = SimHashes.Water;
                oreDissolves.DeltaT = 0f;
            }
                return solidOreEntity;
            
        }
    }

    public class NickelConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Nickel;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            if (Config.Instance.RephysicalizedMetalOre)
            {
                var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
                oreDissolves.usedoreratio = 0.73f;
                oreDissolves.conversionspeed = 0.02f;
                oreDissolves.minmass = 10f;
                oreDissolves.emittedore = SimHashes.NickelOre;
                oreDissolves.absorbedFluid = SimHashes.LiquidSulfur;
                oreDissolves.DeltaT = 0f;
            }
            return solidOreEntity;

        }
    }

    public class CobaltConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Cobalt;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            if (Config.Instance.RephysicalizedMetalOre)
            {
                var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
                oreDissolves.usedoreratio = 0.71f;
                oreDissolves.conversionspeed = 0.02f;
                oreDissolves.minmass = 10f;
                oreDissolves.emittedore = SimHashes.Cobaltite;
                oreDissolves.absorbedFluid = SimHashes.LiquidSulfur;
                oreDissolves.DeltaT = 0f;
            }
            return solidOreEntity;

        }
    }
    public class AlumniumConfig : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Aluminum;
        public GameObject CreatePrefab()
        {
           GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            if (Config.Instance.RephysicalizedMetalOre)
            {

                var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
                oreDissolves.usedoreratio = 0.6f;

                oreDissolves.conversionspeed = 0.02f;
                oreDissolves.minmass = 10f;
                oreDissolves.emittedore = SimHashes.AluminumOre;
                oreDissolves.absorbedFluid = SimHashes.Water;


                oreDissolves.DeltaT = 0f;
            }
                return solidOreEntity;
                 }
    }
    public class ToxicMudFertilizer : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.ToxicMud;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddComponent<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 0.75f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedore = SimHashes.Fertilizer;
            oreDissolves.absorbedFluid = SimHashes.LiquidPhosphorus;
            oreDissolves.DeltaT = 10f;
            return solidOreEntity;
        }
    }


    public class Dirt : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Dirt;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 1f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedore = SimHashes.ToxicSand;
            oreDissolves.absorbedFluid = SimHashes.DirtyWater;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }
 public class Mud : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Mud;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>(); ; ;
            oreDissolves.usedoreratio = 1f;
            oreDissolves.conversionspeed = 0.02f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedore = SimHashes.ToxicMud;
            oreDissolves.absorbedFluid = SimHashes.DirtyWater;
            oreDissolves.DeltaT = 0f;
            return solidOreEntity;
        }
    }

    public class Gold : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.Gold;
        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            if (Config.Instance.RephysicalizedMetalOre)
            {
                var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>();
                oreDissolves.usedoreratio = 0.6f;
                oreDissolves.conversionspeed = 0.02f;
                oreDissolves.minmass = 10f;
                oreDissolves.emittedore = SimHashes.GoldAmalgam;
                oreDissolves.absorbedFluid = SimHashes.Mercury;
                oreDissolves.DeltaT = 0f;
            }
                return solidOreEntity;
             }
    }

    public class RefinedCarbon : IOreDissolvesConfig
    {
        public SimHashes ElementID => SimHashes.RefinedCarbon;

        public GameObject CreatePrefab()
        {
            GameObject solidOreEntity = OrePrefabHelpers.GetOrCreateOrePrefab(this.ElementID);
            var oreDissolves = solidOreEntity.AddOrGet<OreDissolves>();
            oreDissolves.usedoreratio = 0.5f;
            oreDissolves.conversionspeed = 0.001f;
            oreDissolves.minmass = 10f;
            oreDissolves.emittedore = SimHashes.Diamond;
            oreDissolves.absorbedFluid = SimHashes.MoltenCarbon;
            oreDissolves.DeltaT = -10f;
            return solidOreEntity;
        }
    }
}





