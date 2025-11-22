using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized
{
    // Ensure the spawn timer is 1800f for all toilets
    [HarmonyPatch(typeof(Toilet), nameof(Toilet.OnSpawn))]
    public static class Toilet_OnSpawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Toilet __instance)
        {
            // smi is available after base OnSpawn/startup
            if (__instance?.smi != null)
            {
                __instance.smi.monsterSpawnTime = 1800f;
            }
        }
    }

    // After a monster spawns, re-schedule the next spawn so it can happen repeatedly
    [HarmonyPatch(typeof(Toilet), "SpawnMonster")]
    public static class Toilet_SpawnMonster_Patch
    {
        // Cache MethodInfo for efficiency; Harmony can patch/invoke private methods
        private static readonly MethodInfo SpawnMonsterMI = AccessTools.Method(typeof(Toilet), "SpawnMonster");

        [HarmonyPostfix]
        public static void Postfix(Toilet __instance)
        {
            var smi = __instance?.smi;
            if (smi == null)
                return;

            // Always keep the timer at 1800f as requested
            smi.monsterSpawnTime = 18f;

            // Only keep respawning while the toilet is still in the "fullWaitingForClean" state,
            // which is the state where the game schedules the original spawn.
            var current = smi.GetCurrentState();
            var fullWaiting = smi.sm.fullWaitingForClean;
            if (current == fullWaiting)
            {
                // Schedule another spawn in monsterSpawnTime seconds.
                // Use reflection to call the original private SpawnMonster method so our postfix runs again,
                // creating a repeating schedule as long as the state remains valid.
                smi.Schedule(smi.monsterSpawnTime, _ =>
                {
                    if (__instance != null && __instance.smi != null &&
                        __instance.smi.GetCurrentState() == __instance.smi.sm.fullWaitingForClean)
                    {
                        SpawnMonsterMI.Invoke(__instance, null);
                    }
                }, null);
            }
        }
    }
}
