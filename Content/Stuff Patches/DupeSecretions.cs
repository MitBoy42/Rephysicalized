using HarmonyLib;
using System;
using KSerialization;
using UnityEngine;

namespace Rephysicalized.Content.Stuff_Patches
{
    public sealed class DupeSecretions : KMonoBehaviour
    {
        private const bool DebugLogging = false;
        private int eatCompleteHandle = -1;

        [Serialize] private float accumulatedMass;
        [Serialize] private float activeFloorPeeMass;
        [Serialize] private float activeFloorPeeBladderFraction;
        [Serialize] private bool floorPeeInProgress;

        public override void OnSpawn()
        {
            base.OnSpawn();
            eatCompleteHandle = Subscribe((int)GameHashes.EatCompleteEater, (Action<object, object>)OnEatComplete, this);
        }

        public override void OnCleanUp()
        {
            Unsubscribe(ref eatCompleteHandle);
            base.OnCleanUp();
        }

        public void AddMass(float mass, string source)
        {
            if (mass > 0f)
            {
                accumulatedMass += mass;
                LogMassChange(source, mass);
            }
        }

        public void RemoveMass(float mass, string source)
        {
            if (mass > 0f)
            {
                float previousMass = accumulatedMass;
                accumulatedMass = Mathf.Max(0f, accumulatedMass - mass);
                LogMassChange(source, accumulatedMass - previousMass);
            }
        }

        public float ConsumeMass(string source)
        {
            float mass = accumulatedMass;
            accumulatedMass = 0f;
            if (mass > 0f)
                LogMassChange(source, -mass);
            return mass;
        }

        public float GetFloorPeeMass(float bladderFraction, float emittedFraction)
        {
            if (!floorPeeInProgress)
            {
                activeFloorPeeMass = ConsumeMass("Floor pee");
                activeFloorPeeBladderFraction = Mathf.Clamp01(bladderFraction);
                floorPeeInProgress = true;
            }

            if (activeFloorPeeBladderFraction <= 0f)
                return 0f;

            return activeFloorPeeMass * Mathf.Max(0f, emittedFraction) / activeFloorPeeBladderFraction;
        }

        public void CompleteFloorPee()
        {
            activeFloorPeeMass = 0f;
            activeFloorPeeBladderFraction = 0f;
            floorPeeInProgress = false;
        }

        private void OnEatComplete(object component, object data)
        {
            var edible = data as Edible;
            if (edible != null && edible.unitsConsumed > 0f)
                AddMass(edible.unitsConsumed * edible.MassPerUnit, "Eating " + edible.FoodID);
        }

        private void LogMassChange(string source, float massChange)
        {
            if (DebugLogging)
                Debug.Log($"[Rephysicalized][DupeSecretions] {gameObject.name}: {source} {massChange:+0.####;-0.####;0} kg; accumulated {accumulatedMass:0.####} kg.");
        }
    }

    [HarmonyPatch(typeof(BaseMinionConfig), nameof(BaseMinionConfig.BasePrefabInit))]
    public static class BaseMinionConfig_AttachDupeSecretions
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            if (go != null)
                go.AddOrGet<DupeSecretions>();
        }
    }

    [HarmonyPatch(typeof(MinionIdentity), nameof(MinionIdentity.OnSpawn))]
    public static class MinionIdentity_AttachDupeSecretions
    {
        [HarmonyPostfix]
        private static void Postfix(MinionIdentity __instance)
        {
            if (__instance != null)
                __instance.gameObject.AddOrGet<DupeSecretions>();
        }
    }

    [HarmonyPatch(typeof(WaterCooler.StatesInstance), nameof(WaterCooler.StatesInstance.Drink))]
    public static class WaterCoolerChore_TrackSecretionsMass
    {
        [HarmonyPrefix]
        private static void Prefix(WaterCooler.StatesInstance __instance, GameObject druplicant, bool triggerOnDrinkCallback, out bool __state)
        {
            __state = triggerOnDrinkCallback && druplicant != null && __instance.HasMinimumMass();
        }

        [HarmonyPostfix]
        private static void Postfix(GameObject druplicant, bool __state)
        {
            if (__state)
                druplicant.GetComponent<DupeSecretions>()?.AddMass(0.5f, "WaterCoolerChore drink");
        }
    }

    [HarmonyPatch(typeof(SodaFountainWorkable), nameof(SodaFountainWorkable.OnCompleteWork))]
    public static class SodaFountain_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(SodaFountainWorkable __instance, WorkerBase worker)
        {
            var sodaFountain = __instance.GetComponent<SodaFountain>();
            float mass = sodaFountain.waterMassPerUse + sodaFountain.ingredientMassPerUse;
            worker?.GetComponent<DupeSecretions>()?.AddMass(mass, "Soda Fountain");
        }
    }

    [HarmonyPatch(typeof(EspressoMachineWorkable), nameof(EspressoMachineWorkable.OnCompleteWork))]
    public static class EspressoMachine_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(WorkerBase worker)
        {
            float mass = EspressoMachine.WATER_MASS_PER_USE + EspressoMachine.INGREDIENT_MASS_PER_USE;
            worker?.GetComponent<DupeSecretions>()?.AddMass(mass, "Espresso Machine");
        }
    }

    [HarmonyPatch(typeof(JuicerWorkable), nameof(JuicerWorkable.OnCompleteWork))]
    public static class Juicer_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(JuicerWorkable __instance, WorkerBase worker)
        {
            var juicer = __instance.GetComponent<Juicer>();
            float mass = juicer.waterMassPerUse;
            for (int i = 0; i < juicer.ingredientMassesPerUse.Length; i++)
                mass += juicer.ingredientMassesPerUse[i];

            worker?.GetComponent<DupeSecretions>()?.AddMass(mass, "Juicer");
        }
    }

    [HarmonyPatch(typeof(Garnish), nameof(Garnish.Activate))]
    public static class Garnish_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(Garnish __instance, GameObject diner)
        {
            diner?.GetComponent<DupeSecretions>()?.AddMass(__instance.consumeRate, "Garnish");
        }
    }


    [HarmonyPatch(typeof(VomitChore.StatesInstance), nameof(VomitChore.StatesInstance.SpawnVomitLiquid))]
    public static class VomitChore_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(VomitChore.StatesInstance __instance, float dt)
        {
            if (dt <= 0f)
                return;

            var animController = __instance.GetComponent<KBatchedAnimController>();
            if (animController == null || animController.CurrentAnim.totalTime <= 0f)
                return;

            float vomitMass = TUNING.STRESS.VOMIT_AMOUNT * dt / animController.CurrentAnim.totalTime;
            __instance.master.gameObject.GetComponent<DupeSecretions>()?.RemoveMass(vomitMass, "Vomit");
        }
    }

    [HarmonyPatch(typeof(UglyCryChore.StatesInstance), nameof(UglyCryChore.StatesInstance.ProduceTears))]
    public static class UglyCryChore_TrackSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(UglyCryChore.StatesInstance __instance, float dt)
        {
            if (dt > 0f)
                __instance.master.gameObject.GetComponent<DupeSecretions>()?.RemoveMass(TUNING.STRESS.TEARS_RATE * dt, "Tears");
        }
    }

    [HarmonyPatch(typeof(PeeChore.StatesInstance), nameof(PeeChore.StatesInstance.SpawnDirtyWater))]
    public static class PeeChore_UseTrackedSecretionsMass
    {
        [HarmonyPrefix]
        private static bool Prefix(PeeChore.StatesInstance __instance, float dt)
        {
            var dupe = __instance.master.gameObject;
            var secretions = dupe.GetComponent<DupeSecretions>();
            if (secretions == null)
                return true;

            if (dt <= 0f || __instance.bladder == null)
                return false;

            float bladderMax = __instance.bladder.GetMax();
            if (bladderMax <= 0f)
                return false;

            float emittedFraction = dt * -__instance.bladder.GetDelta() / bladderMax;
            if (emittedFraction <= 0f)
                return false;

            float bladderFraction = __instance.bladder.value / bladderMax;
            float mass = secretions.GetFloorPeeMass(bladderFraction, emittedFraction);
            if (mass <= 0f)
                return false;

            byte diseaseIndex = Db.Get().Diseases.GetIndex((HashedString)TUNING.DUPLICANTSTATS.STANDARD.Secretions.PEE_DISEASE);
            var temperature = Db.Get().Amounts.Temperature.Lookup(dupe);
            int diseaseCount = Mathf.CeilToInt(TUNING.DUPLICANTSTATS.STANDARD.Secretions.DISEASE_PER_PEE * emittedFraction);
            var equippable = dupe.GetComponent<SuitEquipper>()?.IsWearingAirtightSuit();

            if (equippable != null)
            {
                equippable.GetComponent<Storage>().AddLiquid(SimHashes.DirtyWater, mass, temperature.value, diseaseIndex, diseaseCount);
            }
            else
            {
                int cell = Grid.PosToCell(dupe);
                SimMessages.AddRemoveSubstance(cell, SimHashes.DirtyWater, CellEventLogger.Instance.Vomit, mass, temperature.value, diseaseIndex, diseaseCount);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PeeChore.StatesInstance), nameof(PeeChore.StatesInstance.IsDonePeeing))]
    public static class PeeChore_ResetTrackedSecretionsMass
    {
        [HarmonyPostfix]
        private static void Postfix(PeeChore.StatesInstance __instance, bool __result)
        {
            if (__result)
                __instance.master.gameObject.GetComponent<DupeSecretions>()?.CompleteFloorPee();
        }
    }

    [HarmonyPatch(typeof(Workable), "OnCompleteWork")]
    public static class ToiletWorkableUse_UseTrackedSecretionsMass
    {
        [HarmonyPrefix]
        private static void Prefix(Workable __instance, WorkerBase worker)
        {
            if (!(__instance is ToiletWorkableUse toilet) || __instance.GetComponent<FlushToilet>() == null || worker == null || Db.Get().Amounts.Bladder.Lookup(worker) == null)
                return;

            var secretions = worker.GetComponent<DupeSecretions>();
            if (secretions != null)
                toilet.lastAmountOfWasteMassRemovedFromDupe = secretions.ConsumeMass("Toilet pee");
        }
    }

    [HarmonyPatch(typeof(Toilet), nameof(Toilet.FlushMultiple))]
    public static class Outhouse_UseTrackedSecretionsMass
    {
        [HarmonyPrefix]
        private static void Prefix(Toilet __instance, WorkerBase worker, out float __state)
        {
            __state = __instance.solidWastePerUse.mass;
            var secretions = worker != null ? worker.GetComponent<DupeSecretions>() : null;
            if (secretions == null)
                return;

            __instance.solidWastePerUse.mass += secretions.ConsumeMass("Toilet ToxicSand");
        }

        [HarmonyPostfix]
        private static void Postfix(Toilet __instance, float __state)
        {
            __instance.solidWastePerUse.mass = __state;
        }
    }
}
