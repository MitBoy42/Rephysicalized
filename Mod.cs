using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using UtilLibs;
using System.Reflection;

namespace Rephysicalized
{
    public class Mod : UserMod2
    {
        public static Harmony HarmonyInstance;


        public override void OnLoad(Harmony harmony)
        {
            PUtil.InitLibrary(false);
            new POptions().RegisterOptions(this, typeof(Config));
            base.OnLoad(harmony);

            // Register shared metadata for other patches/components.
            Rephysicalized.Content.System_Patches.Helper_Components.RephysicalizedMetalDictionary.RegisterToPRegistry();

            SgtLogger.LogVersion(this, harmony);
        }


    }

    // DLC2 gate helper: all patches in this file will be disabled when DLC2 is not enabled
    internal static class Dlc2Gate
    {
        internal static readonly bool Enabled = DlcManager.IsContentSubscribed(DlcManager.DLC2_ID);
    }

    internal static class Dlc3Gate
    {
        internal static readonly bool Enabled = DlcManager.IsContentSubscribed(DlcManager.DLC3_ID);
    }

    internal static class Dlc4Gate
    {
        internal static readonly bool Enabled = DlcManager.IsContentSubscribed(DlcManager.DLC4_ID);
    }
}
