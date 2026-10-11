using HarmonyLib;
using UnityEngine;
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
            if (OrganicOverhaulIntegration.IsPresent())
                return; // let OrganicOverhaul handle it

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
            if (OrganicOverhaulIntegration.IsPresent())
                return; // let OrganicOverhaul handle it

            var smi = __instance?.smi;
            if (smi == null)
                return;

            smi.monsterSpawnTime = 1800f;

            var current = smi.GetCurrentState();
            var fullWaiting = smi.sm.fullWaitingForClean;
            if (current == fullWaiting)
            {
 
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

    // If a gunk-clogged toilet is destroyed, spawn the missing gunk particles.

    [HarmonyPatch(typeof(Toilet), nameof(Toilet.OnCleanUp))]
    public static class Toilet_OnCleanUp_GunkSpawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Toilet __instance)
        {
            if (__instance?.smi == null)
            {   return;
            }

            var component = __instance.GetComponent<ToiletWorkableUse>();
  

            // Only spawn gunk particles if the last user type was the gunk monitor (not bladder).
            if (component.lastElementRemovedFromDupe != GunkMonitor.GunkElement)
                return;

            float massKg = component.lastAmountOfWasteMassRemovedFromDupe;
            if (massKg <= 0f)
                return;

            int cell = Grid.PosToCell(__instance.transform.GetPosition());

            if (FallingWater.instance == null)
                return;
            FallingWater.instance.AddParticle(
                cell,
                ElementLoader.GetElementIndex(SimHashes.LiquidGunk),
                massKg,
                273.15f + 20f,
                byte.MaxValue,
                0);

         
        }
    }
    [HarmonyPatch(typeof(FlushToilet), nameof(FlushToilet.OnCleanUp))]
    public static class FLushToilet_OnCleanUp_GunkSpawn_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Toilet __instance)
        {
            if (__instance?.smi == null)
            {
                return;
            }

            var component = __instance.GetComponent<ToiletWorkableUse>();


            // Only spawn gunk particles if the last user type was the gunk monitor (not bladder).
            if (component.lastElementRemovedFromDupe != GunkMonitor.GunkElement)
                return;

            float massKg = component.lastAmountOfWasteMassRemovedFromDupe;
            if (massKg <= 0f)
                return;

            int cell = Grid.PosToCell(__instance.transform.GetPosition());

            if (FallingWater.instance == null)
                return;
            FallingWater.instance.AddParticle(
                cell,
                ElementLoader.GetElementIndex(SimHashes.LiquidGunk),
                massKg,
                273.15f + 20f,
                byte.MaxValue,
                0);


        }
    }

    // Reset the cached waste mass after a successful gunk clean.

    [HarmonyPatch(typeof(ToiletWorkableClean), nameof(ToiletWorkableClean.OnCompleteWork))]
    public static class ToiletWorkableClean_ResetLastMass_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ToiletWorkableClean __instance, WorkerBase worker)
        {
            var use = __instance.GetComponent<ToiletWorkableUse>();
            if (use == null)
                return;
            use.lastAmountOfWasteMassRemovedFromDupe = 0f;
        
        }
    }
}


