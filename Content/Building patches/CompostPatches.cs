using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized
{

    // Doubles the Compost's ElementConverter throughput (both consumed and output rates).
    // Applied in both ConfigureBuildingTemplate and DoPostConfigureComplete to catch any later changes.
    [HarmonyPatch]
    internal static class CompostThroughputPatch
    {
        // Postfix after CompostConfig.ConfigureBuildingTemplate sets up the ElementConverter
        [HarmonyPatch(typeof(CompostConfig), nameof(CompostConfig.ConfigureBuildingTemplate))]
        [HarmonyPostfix]
        private static void ConfigureBuildingTemplate_Postfix(GameObject go, Tag prefab_tag)
        {
            TryDoubleConverterRates(go);
        }



        private static void TryDoubleConverterRates(GameObject go)
        {
            if (go == null) return;

            var converter = go.GetComponent<ElementConverter>();
            if (converter == null) return;

            // Double consumed element rates
            if (converter.consumedElements != null)
            {
                for (int i = 0; i < converter.consumedElements.Length; i++)
                {
                    try
                    {
                        converter.consumedElements[i].MassConsumptionRate *= 2f;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Rephysicalized] Failed to scale consumed element {i} on Compost: {e}");
                    }
                }
            }

            // Double output element rates
            if (converter.outputElements != null)
            {
                for (int i = 0; i < converter.outputElements.Length; i++)
                {
                    try
                    {
                        converter.outputElements[i].massGenerationRate *= 2f;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Rephysicalized] Failed to scale output element {i} on Compost: {e}");
                    }
                }
            }
        }
    }

    // Small helper to track per-Compost spawn timers without reflection
    internal static class CompostGlomRespawn
    {
        private const float RespawnTime = 1800f; // 1800f as requested
        private static readonly Dictionary<Compost, float> ElapsedByCompost = new Dictionary<Compost, float>();

        public static void Start(Compost compost)
        {
            if (compost == null) return;
            // Reset elapsed when entering inert (AwaitingCompostFlip)
            ElapsedByCompost[compost] = 0f;
        }

        public static void Stop(Compost compost)
        {
            if (compost == null) return;
            // Clear when leaving inert or on clean-up
            ElapsedByCompost.Remove(compost);
        }

        public static void Update(Compost compost, float dt)
        {
            if (compost == null) return;

            if (!ElapsedByCompost.TryGetValue(compost, out var elapsed))
                elapsed = 0f;

            elapsed += dt;

            if (elapsed >= RespawnTime)
            {
                elapsed = 0f;
                TrySpawnGlom(compost);
            }

            ElapsedByCompost[compost] = elapsed;
        }

        // Explicit glom spawn, no reflection
        private static void TrySpawnGlom(Compost compost)
        {
            // Basic safety: only spawn if building exists, has a world and is active/inert
            if (compost == null || compost.gameObject == null)
                return;

            // Choose a spawn position: above the compost tile to avoid spawning inside solid
            Vector3 basePos = compost.transform.GetPosition();
            Vector3 spawnPos = basePos + new Vector3(0f, 1f, 0f);

            // Get Glom prefab and instantiate
            var prefab = Assets.GetPrefab(GlomConfig.ID);
            if (prefab == null)
                return;

            GameObject glom = Util.KInstantiate(prefab, spawnPos);
            if (glom == null)
                return;

            glom.SetActive(true);

            // Optional: small impulse so it doesn't overlap
            var kbac = glom.GetComponent<KBatchedAnimController>();
            if (kbac != null)
                kbac.Play("idle_loop", KAnim.PlayMode.Loop);
        }
    }

    // Hook into Compost state machine setup and add our inert-state loop, using concrete state names
    [HarmonyPatch(typeof(Compost.States), nameof(Compost.States.InitializeStates))]
    public static class Compost_States_InitializeStates_GlomRespawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Compost.States __instance)
        {
            // Add enter/exit/update handlers directly to the concrete 'inert' state, which toggles AwaitingCompostFlip
            __instance.inert
                .Enter("GlomRespawn_Enter", smi => CompostGlomRespawn.Start(smi.master))
                .Exit(smi => CompostGlomRespawn.Stop(smi.master))
                .Update("GlomRespawn_Update", (smi, dt) => CompostGlomRespawn.Update(smi.master, dt));
        }
    }

    // Ensure we clean our timer store when the building is cleaned up
    [HarmonyPatch(typeof(Compost), nameof(Compost.OnCleanUp))]
    public static class Compost_OnCleanUp_GlomRespawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Compost __instance)
        {
            CompostGlomRespawn.Stop(__instance);
        }
    }
}

