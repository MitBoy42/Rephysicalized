﻿using System;
using System.Collections.Generic;
using PeterHan.PLib.Core;

namespace Rephysicalized.Content.System_Patches.Helper_Components
{
    internal static class RephysicalizedMetalDictionary
    {
        internal const string PRegistryKeyMetalDictionary = "Rephysicalized.MetalDictionary";
        internal static void RegisterToPRegistry()
        {
            PRegistry.PutData(PRegistryKeyMetalDictionary, MetalOresAdditionalOutputs);
        }
        /// Key: element tag (string), value: list of additional outputs, with percentage of output mass.
        internal static readonly Dictionary<string, List<Tuple<string, float>>> MetalOresAdditionalOutputs =
            new Dictionary<string, List<Tuple<string, float>>>
            {
                [SimHashes.Cinnabar.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Mercury.CreateTag().ToString(), 0.86f),
                    new Tuple<string, float>(SimHashes.Sulfur.CreateTag().ToString(), 0.14f),
                },

                [SimHashes.IronOre.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Iron.CreateTag().ToString(), 0.67f),
                    new Tuple<string, float>(SimHashes.IgneousRock.CreateTag().ToString(), 0.33f),
                },

                [SimHashes.Cuprite.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Copper.CreateTag().ToString(), 0.80f),
                    new Tuple<string, float>(SimHashes.Oxygen.CreateTag().ToString(), 0.20f),
                },

                [SimHashes.Cobaltite.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Cobalt.CreateTag().ToString(), 0.71f),
                    new Tuple<string, float>(SimHashes.Sulfur.CreateTag().ToString(), 0.29f),
                },

                [SimHashes.NickelOre.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Nickel.CreateTag().ToString(), 0.73f),
                    new Tuple<string, float>(SimHashes.Sulfur.CreateTag().ToString(), 0.27f),
                },

                [SimHashes.Wolframite.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Tungsten.CreateTag().ToString(), 0.60f),
                    new Tuple<string, float>(SimHashes.Rust.CreateTag().ToString(), 0.40f),
                },

                [SimHashes.AluminumOre.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Aluminum.CreateTag().ToString(), 0.60f),
                    new Tuple<string, float>(SimHashes.Oxygen.CreateTag().ToString(), 0.20f),
                    new Tuple<string, float>(SimHashes.Water.CreateTag().ToString(), 0.20f),
                },

                [SimHashes.GoldAmalgam.CreateTag().ToString()] = new List<Tuple<string, float>>
                {
                    new Tuple<string, float>(SimHashes.Gold.CreateTag().ToString(), 0.67f),
                    new Tuple<string, float>(SimHashes.Mercury.CreateTag().ToString(), 0.33f),
                },
            };

    
    }
}



