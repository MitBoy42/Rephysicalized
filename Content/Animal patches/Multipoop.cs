using HarmonyLib;
using Klei.AI;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    // Registry for extra poop outputs per species (PrefabTag)
    internal static class SecondaryPoopRegistry
    {
        internal struct SecondaryOutput
        {
            public SimHashes element;
            public float ratioPerBase; // kg extra per kg base poop mass
            public SecondaryOutput(SimHashes elem, float ratio) { element = elem; ratioPerBase = ratio; }
        }

        // Map prefab tag -> list of extra outputs
        private static readonly Dictionary<Tag, List<SecondaryOutput>> _map = new Dictionary<Tag, List<SecondaryOutput>>();

        // Ensure defaults aren't registered twice even if multiple Db.Initialize patches call us
        private static bool _defaultsRegistered;

        // Public API to register an extra output for a prefab ID (string) or Tag
        public static void Register(string prefabId, SimHashes element, float ratioPerBase)
            => Register(new Tag(prefabId), element, ratioPerBase);

        public static void Register(Tag prefabTag, SimHashes element, float ratioPerBase)
        {
   

            if (!_map.TryGetValue(prefabTag, out var list))
            {
                list = new List<SecondaryOutput>(1);
                _map[prefabTag] = list;
            }

            // De-duplicate per element; update ratio if already present
            int idx = list.FindIndex(o => o.element == element);
            if (idx >= 0)
            {
                list[idx] = new SecondaryOutput(element, ratioPerBase);
            }
            else
            {
                list.Add(new SecondaryOutput(element, ratioPerBase));
            }
        }

        public static bool TryGet(Tag prefabTag, out List<SecondaryOutput> outputs) => _map.TryGetValue(prefabTag, out outputs);

        // Registers the same secondary poop behavior as your previous bespoke patches
        public static void RegisterDefaults()
        {
            if (_defaultsRegistered) return; // guard against double-registration
            _defaultsRegistered = true;


            // Staterpillars: spawn Sand at 18x of base poop for all variants
            // Applies to adult + baby, and Gas/Liquid variants + babies
            Register(StaterpillarConfig.ID, SimHashes.Sand, 18f);
            Register(BabyStaterpillarConfig.ID, SimHashes.Sand, 18f);
   

            // Hatches:
            // Base + Hard: Sand at 69/70
            Register(HatchConfig.ID, ModElementRegistration.CrudByproduct, 69f / 70f);
            Register(BabyHatchConfig.ID, ModElementRegistration.CrudByproduct, 69f / 70f);
            Register(HatchHardConfig.ID, ModElementRegistration.CrudByproduct, 69f / 70f);
            Register(BabyHatchHardConfig.ID, ModElementRegistration.CrudByproduct, 69f / 70f);

            // Veggie: Sand at 0.35
            Register(HatchVeggieConfig.ID, ModElementRegistration.CrudByproduct, 65 / 70f);
            Register(BabyHatchVeggieConfig.ID, ModElementRegistration.CrudByproduct, 65 / 70f);

            // Metal: Mercury at 20/75 if DLC2, otherwise Sand
            var metalByproduct = DlcManager.CheckForDLCFileInstallation(DlcManager.DLC2_ID) ? SimHashes.Mercury : SimHashes.Sand;
            Register(HatchMetalConfig.ID, metalByproduct, 20f / 75f);
            Register(BabyHatchMetalConfig.ID, metalByproduct, 20f / 75f);

            //sweetles and oilfloaters if chemistrier is not enabled
            if (Config.Instance.ChemistrierSweetles)
            {
                Register(DivergentBeetleConfig.ID, SimHashes.Sand, 0.9f);
                Register(BabyDivergentBeetleConfig.ID, SimHashes.Sand, 0.9f);
            }
            if (Config.Instance.ChemistrierSlicksters)
            {
                Register(OilFloaterConfig.ID, ModElementRegistration.CrudByproduct, 0.9f);
                Register(OilFloaterBabyConfig.ID, ModElementRegistration.CrudByproduct, 0.9f);
                Register(OilFloaterHighTempBabyConfig.ID, ModElementRegistration.CrudByproduct, 0.9f);
                Register(OilFloaterHighTempConfig.ID, ModElementRegistration.CrudByproduct, 0.9f);
            }
        }
    }

    // Harmony: emit secondary outputs after the vanilla stomach has produced poop.
    [HarmonyPatch(typeof(CreatureCalorieMonitor.Stomach), nameof(CreatureCalorieMonitor.Stomach.PoopInStorage), new Type[]
    {
        typeof(Storage),
        typeof(bool),
        typeof(Sprite),
        typeof(string)
    })]
    internal static class SecondaryPoop_Patch
    {
        internal struct State
        {
            public bool valid;
            public Tag prefabTag;
            public float before;
        }

        // Capture previous total only if this species is registered
        static void Prefix(CreatureCalorieMonitor.Stomach __instance, out State __state)
        {
            __state = default;

            var owner = __instance.owner;
            var kpid = owner ? owner.GetComponent<KPrefabID>() : null;

            if (kpid == null) return;

            var pt = kpid.PrefabTag;
            if (!SecondaryPoopRegistry.TryGet(pt, out _))
                return;

            float before = 0f;
            Game.Instance.savedInfo.creaturePoopAmount.TryGetValue(pt, out before);

            __state.valid = true;
            __state.prefabTag = pt;
            __state.before = before;

        }

        // Compute delta and spawn any registered extra outputs
        static void Postfix(CreatureCalorieMonitor.Stomach __instance, bool skipPoopSpawn, State __state)
        {
            if (!__state.valid || skipPoopSpawn)
                return;

            // Mass produced by this Poop() call
            Game.Instance.savedInfo.creaturePoopAmount.TryGetValue(__state.prefabTag, out float after);
            float baseMassThisPoop = Mathf.Max(0f, after - __state.before);
            if (baseMassThisPoop <= 0f) return;

            if (!SecondaryPoopRegistry.TryGet(__state.prefabTag, out var outputs) || outputs == null || outputs.Count == 0)
                return;

            var owner = __instance.owner;
            if (owner == null)
                return;
            int cell = Grid.PosToCell(owner.transform.GetPosition());

            float tempK = owner.GetComponent<PrimaryElement>()?.Temperature ?? 293.15f;

            foreach (var outp in outputs)
            {
                float extraKg = baseMassThisPoop * Mathf.Max(0f, outp.ratioPerBase);
                var elem = ElementLoader.FindElementByHash(outp.element);
                if (elem == null)
                    continue;

                if (elem.IsSolid)
                {
                    // Solids: spawn at +0.3 cell x-offset
                    Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore) + new Vector3(0.3f, 0f, 0f);
                    elem.substance?.SpawnResource(pos, extraKg, tempK, byte.MaxValue, 0);
                }
                else
                {
                    // Liquids/Gases: use SimMessages in-cell
                    SimMessages.AddRemoveSubstance(
                        cell,
                        outp.element,
                        CellEventLogger.Instance.ElementConsumerSimUpdate,
                        extraKg,
                        tempK,
                        byte.MaxValue,
                        0
                    );
                }
            }
        }
    }

    // Register defaults once the database is up (safe time to know DLCs and IDs)
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class SecondaryPoop_RegisterDefaults_Patch
    {
        static void Postfix()
        {
            SecondaryPoopRegistry.RegisterDefaults();
        }
    }

    // Inject extra poop outputs into CodexConversionPanel only for DIET panels of creatures
    // that are registered in SecondaryPoopRegistry (e.g., slugs, hatches).
    [HarmonyPatch(typeof(CodexConversionPanel))]
    internal static class CodexConversionPanel_DietSecondaryPoop_Patch
    {
        // CodexConversionPanel(string, Tag, float, bool, Tag, float, bool, GameObject)
        [HarmonyPatch(MethodType.Constructor, new Type[]
        {
            typeof(string),
            typeof(Tag),
            typeof(float),
            typeof(bool),
            typeof(Tag),
            typeof(float),
            typeof(bool),
            typeof(GameObject)
        })]
        [HarmonyPostfix]
        private static void CtorSimple_Postfix(CodexConversionPanel __instance, GameObject converter)
        {
            TryAppendSecondaryOutputs(__instance, converter);
        }

        // CodexConversionPanel(string, Tag, float, bool, Func<Tag,float,bool,string>, Tag, float, bool, Func<Tag,float,bool,string>, GameObject)
        [HarmonyPatch(MethodType.Constructor, new Type[]
        {
            typeof(string),
            typeof(Tag),
            typeof(float),
            typeof(bool),
            typeof(Func<Tag, float, bool, string>),
            typeof(Tag),
            typeof(float),
            typeof(bool),
            typeof(Func<Tag, float, bool, string>),
            typeof(GameObject)
        })]
        [HarmonyPostfix]
        private static void CtorWithFormatters_Postfix(CodexConversionPanel __instance, GameObject converter)
        {
            TryAppendSecondaryOutputs(__instance, converter);
        }

        // CodexConversionPanel(string, ElementUsage[] ins, ElementUsage[] outs, GameObject converter)
        [HarmonyPatch(MethodType.Constructor, new Type[]
        {
            typeof(string),
            typeof(ElementUsage[]),
            typeof(ElementUsage[]),
            typeof(GameObject)
        })]
        [HarmonyPostfix]
        private static void CtorArray_Postfix(CodexConversionPanel __instance, GameObject converter)
        {
            TryAppendSecondaryOutputs(__instance, converter);
        }

        private static void TryAppendSecondaryOutputs(CodexConversionPanel panel, GameObject converter)
        {
            if (panel == null || converter == null)
                return;

            var kpid = converter.GetComponent<KPrefabID>();
            if (kpid == null || !SecondaryPoopRegistry.TryGet(kpid.PrefabTag, out var extraOutputs) ||
                extraOutputs == null || extraOutputs.Count == 0)
                return;

            var outs = panel.outs ?? Array.Empty<ElementUsage>();
            var ins = panel.ins ?? Array.Empty<ElementUsage>();
            if (outs.Length == 0 || ins.Length == 0)
                return;

            var baseOut = outs[0];
            var list = new List<ElementUsage>(outs);
            int insertIndex = Math.Min(1, list.Count);

            bool AlreadyHas(Tag tag)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i].tag == tag) return true;
                return false;
            }

            int inserted = 0;

            foreach (var sec in extraOutputs)
            {
                var elem = ElementLoader.FindElementByHash(sec.element);
                if (elem == null)
                    continue;

                var extraTag = elem.tag;
                if (AlreadyHas(extraTag))
                    continue;

                float ratio = Mathf.Max(0f, sec.ratioPerBase);
                if (ratio <= 0f)
                    continue;

                float extraAmount = baseOut.amount * ratio;
                var extraUsage = new ElementUsage(extraTag, extraAmount, baseOut.continuous, baseOut.customFormating);

                list.Insert(insertIndex++, extraUsage);
                inserted++;
            }

            if (inserted > 0)
                panel.outs = list.ToArray();
        }
    }
}
