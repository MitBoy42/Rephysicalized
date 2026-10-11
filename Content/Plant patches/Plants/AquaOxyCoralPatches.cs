﻿using System;
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
    // - If remaining primary element mass would drop below 1f, production stops
    [HarmonyPatch(typeof(OxyCoral.Instance), nameof(OxyCoral.Instance.ProduceOxygenUpdate))]
    internal static class OxyCoral_ConsumePrimaryElementWhileProducingPatch
    {
        private const float LIGHT_LUX_SATURATION = 20000f;
        private const float LIGHT_MULTIPLIER_BASE_ADD = 1f;

        private static float GetLightMultiplier(OxyCoral.Instance __instance)
        {
            // Light scaling: linear multiplier where lux=0 => 1x, lux=20,000 => 2x.
            // Uses the maximum light on the output bubble cells (similar to OxyCoral.IsThereEnoughLight()).
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

            float t = Mathf.Clamp01(maxLux / LIGHT_LUX_SATURATION);
            return LIGHT_MULTIPLIER_BASE_ADD + t;
        }

        private static bool Prefix(OxyCoral.Instance __instance, float dt)
        {
            if (__instance == null)
                return true;

            var pe = __instance.primaryElement;

            // Keep vanilla behavior for wild oxycorals.
            if (__instance.IsWild)
                return true;

            // Skip vanilla implementation and fully reproduce it so we can scale the oxygen bubble mass.
            float oxygenProductionRate = __instance.def != null ? __instance.def.OxygenProductionRate : 0f;
            oxygenProductionRate *= GetLightMultiplier(__instance);

            float mass = oxygenProductionRate * dt;
            if (mass <= 0f)
                return false;

            if (pe.Mass < 5f)
                return false;

            // Consume primary element mass that will become the produced oxygen bubble.
            pe.Mass -= mass;

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
                        massConsumptionRate = 0.025f
                    }
                });

                EntityTemplates.ExtendPlantToIrrigated(__result, new PlantElementAbsorber.ConsumeInfo[]
             {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.SaltWater.CreateTag(),
                        massConsumptionRate = 0.05f
                    }
             });
            }
        }
    }
}

