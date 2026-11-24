using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using STRINGS;
using static STRINGS.BUILDING.STATUSITEMS;



namespace Rephysicalized.Content.Plant_patches
{


    // OxyCoral: consume primary element mass while producing oxygen
    // - Primary element mass decreases by the oxygen bubble mass created each tick
    // - Production stops when 5 kg of primary element mass remain
    [HarmonyPatch(typeof(OxyCoral.Instance), nameof(OxyCoral.Instance.ProduceOxygenUpdate))]
    internal static class OxyCoral_ConsumePrimaryElementWhileProducingPatch
    {
        private const float MIN_PRODUCTION_RATE = 0.1f;
        private const float MAX_PRODUCTION_RATE = 0.3f;
        private const float MIN_PRODUCTION_LUX = 2500f;
        private const float MAX_PRODUCTION_LUX = 10000f;
        private const float WILD_PRODUCTION_RATE_MODIFIER = 0.25f;
        private const float MIN_REMAINING_MASS = 5f;

        private static float GetOxygenProductionRate(OxyCoral.Instance __instance)
        {
            float maxLux = 0f;
            int plantCell = Grid.PosToCell(__instance.transform.GetPosition());
            for (int i = 0; i < __instance.def.OutputBubbleCells.Length; i++)
            {
                int bubbleCell = Grid.OffsetCell(plantCell, __instance.def.OutputBubbleCells[i]);
                if (Grid.IsValidCell(bubbleCell))
                {
                    float lux = Grid.LightIntensity[bubbleCell];
                    if (lux > maxLux) maxLux = lux;
                }
            }

            float lightProgress = Mathf.InverseLerp(MIN_PRODUCTION_LUX, MAX_PRODUCTION_LUX, maxLux);
            return Mathf.Lerp(MIN_PRODUCTION_RATE, MAX_PRODUCTION_RATE, lightProgress);
        }

        private static bool Prefix(OxyCoral.Instance __instance, float dt)
        {
            if (__instance == null)
                return true;

            var pe = __instance.primaryElement;
            bool isWild = __instance.IsWild;
            float oxygenProductionRate = GetOxygenProductionRate(__instance);
            if (isWild)
                oxygenProductionRate *= WILD_PRODUCTION_RATE_MODIFIER;

            float mass = oxygenProductionRate * dt;
            if (mass <= 0f)
                return false;

            if (!isWild)
            {
                float availableMass = Mathf.Max(0f, pe.Mass - MIN_REMAINING_MASS);
                mass = Mathf.Min(mass, availableMass);
                if (mass <= 0f)
                    return false;

                pe.Mass -= mass;
            }

            // Reproduce vanilla bubble spawn logic (random output bubble cell).
            int gameCell = Grid.OffsetCell(Grid.PosToCell((StateMachine.Instance)__instance), __instance.def.OutputBubbleCells[UnityEngine.Random.Range(0, __instance.def.OutputBubbleCells.Length)]);
            __instance.CreateOxygenBubble(gameCell, mass);

            return false;
        }

        [HarmonyPatch(typeof(OxyCoral), nameof(OxyCoral.InitializeStates))]
        private static class OxyCoral_InitializeStates_Patch
        {
            private static bool Prefix(OxyCoral __instance, out StateMachine.BaseState default_state)
            {
                // Fully vanilla override initialise states; only change: scale BubbleGasProduction status item with light multiplier.
                __instance.serializable = StateMachine.SerializeType.ParamsOnly;
                default_state = (StateMachine.BaseState)__instance.noProducing;

                __instance.noProducing.DefaultState(__instance.noProducing.noLight);
                __instance.noProducing.noLight
                    .EventTransition(GameHashes.Uprooted, __instance.noProducing.dead)
                    .EventTransition(GameHashes.Wilt, __instance.noProducing.wilted,
                        new StateMachine<OxyCoral, OxyCoral.Instance, IStateMachineTarget, OxyCoral.Def>.Transition.ConditionCallback(OxyCoral.IsWilted))
                    .PlayAnim("idle_loop", KAnim.PlayMode.Loop)
                    .UpdateTransition(__instance.producing, new Func<OxyCoral.Instance, float, bool>(OxyCoral.IsThereEnoughtLight));

                __instance.noProducing.wilted
                    .TriggerOnEnter(GameHashes.PoopStationUpdate)
                    .TriggerOnExit(GameHashes.PoopStationUpdate)
                    .EventTransition(GameHashes.Uprooted, __instance.noProducing.dead)
                    .EventTransition(GameHashes.WiltRecover, __instance.noProducing.noLight,
                        GameStateMachine<OxyCoral, OxyCoral.Instance, IStateMachineTarget, OxyCoral.Def>.Not(
                            new StateMachine<OxyCoral, OxyCoral.Instance, IStateMachineTarget, OxyCoral.Def>.Transition.ConditionCallback(OxyCoral.IsWilted)))
                    .PlayAnim("wilt");

                __instance.noProducing.dead.Enter((StateMachine<OxyCoral, OxyCoral.Instance, IStateMachineTarget, OxyCoral.Def>.State.Callback)(smi =>
                {
                    GameUtil.KInstantiate(Assets.GetPrefab((Tag)EffectConfigs.PlantDeathId), smi.transform.GetPosition(), Grid.SceneLayer.FXFront).SetActive(true);
                    smi.Trigger(1623392196);
                    smi.DestroySelf((object)null);
                }));

                // Keep ONI vanilla status items intact (avoid cached BubbleGasProduction tuple).
                __instance.producing.EventTransition(GameHashes.Uprooted, __instance.noProducing.dead)
                    .EventTransition(GameHashes.Wilt, __instance.noProducing.wilted)
                    .UpdateTransition(__instance.noProducing.noLight, new Func<OxyCoral.Instance, float, bool>(OxyCoral.LightLostUpdate))
                    .PlayAnim("oxygen_idle_loop", KAnim.PlayMode.Loop)
                    .Update(new System.Action<OxyCoral.Instance, float>(OxyCoral.ProduceOxygenUpdate), UpdateRate.SIM_1000ms);

                return false; // skip original InitializeStates
            }
        }



        [HarmonyPatch(typeof(OxyCoralConfig), nameof(OxyCoralConfig.CreatePrefab))]
        private static class OxyCoralConfig_CreatePrefab_Patch
        {
            private static void Postfix(ref GameObject __result)
            {
                // Re-apply fertilizer requirement with updated rate.
                // UrchinPlantConfig uses RefinedCarbon as FertilizerElement.
                EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Lime.CreateTag(),
                        massConsumptionRate = 0.016666667f
                    }
                });

                EntityTemplates.ExtendPlantToIrrigated(__result, new PlantElementAbsorber.ConsumeInfo[]
             {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.SaltWater.CreateTag(),
                        massConsumptionRate = 0.1f
                    }
             });
            }
        }
    }
}

