using Database;          // Db.Get()
using HarmonyLib;
using Klei;
using Klei.AI;          // Effects
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.BuildingPatches
{
    // Extend AutoStorageDropper filter to also dump any flushed liquids from the suit.
    [HarmonyPatch(typeof(DecontaminationShowerConfig), nameof(DecontaminationShowerConfig.ConfigureBuildingTemplate))]
    internal static class DecontaminationShower_Filter_Patch
    {
        private static readonly SimHashes[] ExtraLiquids =
        {
        
            SimHashes.DirtyWater,
            SimHashes.SaltWater,
            SimHashes.Brine,
            SimHashes.LiquidGunk
        };

        private static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;

            var dropperDef = go.GetDef<AutoStorageDropper.Def>();
            if (dropperDef == null) return;

            // Merge existing filter with the desired set, without duplicates
            var desired = new HashSet<SimHashes>(ExtraLiquids);
            if (dropperDef.elementFilter != null)
            {
                for (int i = 0; i < dropperDef.elementFilter.Length; i++)
                    desired.Add(dropperDef.elementFilter[i]);
            }

            var merged = new SimHashes[desired.Count];
            int idx = 0;
            foreach (var e in desired) merged[idx++] = e;
            dropperDef.elementFilter = merged;
        }
    }

    // On complete work in DecontaminationShower:
    // - Find equipped AirtightSuit via MinionAssignablesProxy/Equipment
    // - Remove target liquids from all suit storages (root + children)
    // - Add the same removed liquids to the shower storage
    // - Clear SoiledSuit effect
    [HarmonyPatch(typeof(Workable), nameof(Workable.OnCompleteWork))]
    internal static class DecontaminationShower_SuitFlush_Patch
    {
        private static readonly SimHashes[] FlushLiquids =
        {
            SimHashes.DirtyWater,
            SimHashes.SaltWater,
            SimHashes.Brine,
            SimHashes.LiquidGunk
        };

        private static void Postfix(Workable __instance, WorkerBase worker)
        {
            if (__instance == null || worker == null) return;

            // Target only DecontaminationShower
            var kpid = __instance.GetComponent<KPrefabID>();
            if (kpid == null || kpid.PrefabID().Name != DecontaminationShowerConfig.ID)
                return;

            var showerStorage = __instance.GetComponent<Storage>();
            if (showerStorage == null)
                return;

            var identity = worker.gameObject.GetComponent<MinionIdentity>();
            if (identity == null)
            {
                TryClearSoiledSuit(worker);
                return;
            }

            var proxy = FindProxy(identity);
            if (proxy == null)
            {
                TryClearSoiledSuit(worker);
                return;
            }

            var equipment = proxy.GetComponent<Equipment>();
            if (equipment == null)
            {
                TryClearSoiledSuit(worker);
                return;
            }

            var suitSlot = equipment.GetSlot(Db.Get().AssignableSlots.Suit);
            var equippable = suitSlot != null ? suitSlot.assignable as Equippable : null;
            if (equippable == null) { TryClearSoiledSuit(worker); return; }

            var suitGO = equippable.gameObject;
            if (suitGO == null || !suitGO.HasTag(GameTags.AirtightSuit))
            {
                TryClearSoiledSuit(worker);
                return;
            }

            var suitStorages = new List<Storage>();
            suitGO.GetComponentsInChildren(true, suitStorages);
            if (suitStorages.Count == 0)
            {
                TryClearSoiledSuit(worker);
                return;
            }

            // Flush each target element from all suit storages and add into shower storage as-is
            for (int s = 0; s < suitStorages.Count; s++)
            {
                var st = suitStorages[s];
                if (st == null) continue;

                for (int i = 0; i < FlushLiquids.Length; i++)
                {
                    var hash = FlushLiquids[i];
                    var elem = ElementLoader.FindElementByHash(hash);
                    if (elem == null) continue;

                    st.ConsumeAndGetDisease(elem.tag, float.PositiveInfinity, out float consumed, out SimUtil.DiseaseInfo di, out float aggTemp);
                    if (consumed <= 0f) continue;

                    float temp = (aggTemp > 0f && !float.IsNaN(aggTemp)) ? aggTemp : 293.15f;
                    showerStorage.AddLiquid(hash, consumed, temp, di.idx, di.count, keep_zero_mass: true, do_disease_transfer: false);
                }
            }

            // Clear status after flushing
            TryClearSoiledSuit(worker);
        }

        private static MinionAssignablesProxy FindProxy(MinionIdentity identity)
        {
            foreach (var p in Components.MinionAssignablesProxy.Items)
            {
                if (p != null && p.target == identity)
                    return p;
            }
            return null;
        }

        private static void TryClearSoiledSuit(WorkerBase worker)
        {
            var effects = worker.GetComponent<Effects>();
            if (effects != null && effects.HasEffect("SoiledSuit"))
                effects.Remove("SoiledSuit");
        }
    }
}