using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    /// <summary>
    /// Gas Grass is irrigated with chlorine at 10 kg per cycle (vanilla 0.0008333334) and fertilised with
    /// RichSoil instead of Dirt at 40 kg per cycle (vanilla 0.04166667).
    ///
    /// Vanilla inlines those rates into ExtendPlantToIrrigated/Fertilizable, but they remain readable on
    /// the monitor Defs afterwards - and those Defs are what the simulation, the info panel and the Codex
    /// all read, so the displayed rate and the simulated rate cannot drift apart.
    /// </summary>
    [HarmonyPatch(typeof(GasGrassConfig), nameof(GasGrassConfig.CreatePrefab))]
    internal static class GasGrassConfig_CreatePrefab_Patch
    {
        private const float ChlorineRate = 10f / 600f;
        private const float FertilizerRate = 40f / 600f;

        private static void Postfix(GameObject __result)
        {
            if (__result == null)
                return;

            var dirt = SimHashes.Dirt.CreateTag();
            var RichSoil = ModTags.RichSoil;

            var irrigation = __result.GetDef<IrrigationMonitor.Def>();
            if (irrigation?.consumedElements != null)
            {
                for (int i = 0; i < irrigation.consumedElements.Length; i++)
                {
                    if (irrigation.consumedElements[i].tag == GameTags.Chlorine)
                        irrigation.consumedElements[i].massConsumptionRate = ChlorineRate;
                }
            }

            var fertilization = __result.GetDef<FertilizationMonitor.Def>();
            if (fertilization?.consumedElements != null)
            {
                for (int i = 0; i < fertilization.consumedElements.Length; i++)
                {
                    if (fertilization.consumedElements[i].tag != dirt)
                        continue;

                    fertilization.consumedElements[i].tag = RichSoil;
                    fertilization.consumedElements[i].massConsumptionRate = FertilizerRate;
                }
            }

            // Farmers must be asked for RichSoil rather than Dirt.
            foreach (var delivery in __result.GetComponents<ManualDeliveryKG>())
            {
                if (delivery != null && delivery.RequestedItemTag == dirt)
                    delivery.RequestedItemTag = RichSoil;
            }

            // ManualDeliveryKG is sized from the rate while the prefab is being built
            // (capacity = rate * 600 * 3, refill/minimum = rate * 600 * 0.5). Applying a new rate
            // afterwards has to resize it too, otherwise the farmer keeps delivering the old amount.
            foreach (var delivery in __result.GetComponents<ManualDeliveryKG>())
            {
                if (delivery == null)
                    continue;

                float rate = delivery.RequestedItemTag == GameTags.Chlorine ? ChlorineRate
                           : delivery.RequestedItemTag == RichSoil ? FertilizerRate
                           : 0f;
                if (rate <= 0f)
                    continue;

                float perCycle = rate * 600f;
                delivery.capacity = perCycle * 3f;
                delivery.refillMass = perCycle * 0.5f;
                delivery.MinimumMass = perCycle * 0.5f;
            }
        }
    }
}
