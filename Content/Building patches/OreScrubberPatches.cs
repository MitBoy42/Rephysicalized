using HarmonyLib;
using Klei;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized.Content.Building_patches
{

    //Ore scrubber

    [HarmonyPatch(typeof(OreScrubberConfig), nameof(OreScrubberConfig.ConfigureBuildingTemplate))]
    public static class OreScrubberPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var oreScrubber = go.AddOrGet<OreScrubber>();
            oreScrubber.massConsumedPerUse = 1.0f;
            oreScrubber.diseaseRemovalCount = OreScrubberConfig.DISEASE_REMOVAL_COUNT * 3;

            var work = go.AddOrGet<OreScrubber.Work>();
            work.workTime = 3.5f;
        }
    }
    [HarmonyPatch(typeof(OreScrubber.Work), nameof(OreScrubber.Work.OnCompleteWork))]
    public static class OreScrubber_EmitChlorine_OnCompleteWork
    {
        public static void Postfix(OreScrubber.Work __instance, WorkerBase worker)
        {
            try
            {
                var go = __instance.gameObject;
                if (go == null) return;

                var scrubber = go.GetComponent<OreScrubber>();
                var storage = go.GetComponent<Storage>();
                if (scrubber == null || storage == null) return;

                // We only emit if the consumed element is a gas
                var element = ElementLoader.FindElementByHash(scrubber.consumedElement);
                if (element == null || !element.IsGas) return;

                // Consume up to 1kg from the internal storage to maintain mass balance
                const float requestMassKg = 1.0f;
                float consumedMassKg;
                SimUtil.DiseaseInfo diseaseInfo;
                float aggregateTempK;

                storage.ConsumeAndGetDisease(
                    element.tag,
                    requestMassKg,
                    out consumedMassKg,
                    out diseaseInfo,
                    out aggregateTempK
                );

                if (consumedMassKg <= 0f) return;

                // Determine emission temperature: prefer aggregate temp from consumed mass; fallback to building temp; otherwise element default
                if (aggregateTempK <= 0f || float.IsNaN(aggregateTempK))
                {
                    var pe = go.GetComponent<PrimaryElement>();
                    aggregateTempK = pe != null && pe.Temperature > 0f ? pe.Temperature : element.defaultValues.temperature;
                }

                int cell = Grid.PosToCell(go);
                if (!Grid.IsValidCell(cell)) return;

                // Emit into the world. Event type is used for debug/logging; ElementConsumerSimUpdate is a safe, existing event.
                SimMessages.AddRemoveSubstance(
                    cell,
                    element.idx,
                    CellEventLogger.Instance.ElementConsumerSimUpdate,
                    consumedMassKg,
                    aggregateTempK,
                    diseaseInfo.idx,
                    diseaseInfo.count
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OreScrubberEmitChlorinePatch] Failed to emit chlorine on complete work: {ex}");
            }
        }
    }
}
