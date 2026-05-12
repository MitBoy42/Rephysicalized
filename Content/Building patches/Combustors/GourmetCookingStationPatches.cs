using HarmonyLib;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized
{


    public sealed class GourmetFueledFabricator : StateMachineComponent<GourmetFueledFabricator.StatesInstance>
    {
        [SerializeField]
        public Tag methaneTag = SimHashes.Methane.CreateTag();
        [SerializeField]
        public Tag oxidizerTag = ModTags.OxidizerGas;
        [SerializeField]
        public float minMethane = 0.1f; // must meet element converter rate
        [SerializeField]
        public float minOxidizer = 0.02f; // must meet element converter rate
        [SerializeField]
        public float hysteresisFactor = 0.5f; // percent drop before flipping back to waiting

        [MyCmpReq] private ComplexFabricator fabricator;
        [MyCmpReq] private Operational operational;
        [SerializeField] public Storage storage; // explicitly assigned from patches to fabricator.inStorage
        [MyCmpReq] private ElementConverter converter;

        private static readonly Operational.Flag gourmetFuelFlag = new Operational.Flag("gourmet_fuel_gate", Operational.Flag.Type.Requirement);

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (converter != null) converter.enabled = false;
            smi.StartSM();
        }

        public float GetAvailable(Tag t) => storage != null ? storage.GetAmountAvailable(t) : 0f;
        public bool HasFuelReady()
        {
            return GetAvailable(methaneTag) >= minMethane && GetAvailable(oxidizerTag) >= minOxidizer;
        }
        public bool BelowHysteresis()
        {
            return GetAvailable(methaneTag) <= (minMethane * hysteresisFactor) || GetAvailable(oxidizerTag) <= (minOxidizer * hysteresisFactor);
        }

        public void MarkQueueDirtyOnce()
        {
            // Debounce: only dirty when transitioning to ready; the SM handles that
            fabricator.SetQueueDirty();
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, GourmetFueledFabricator, object>.GameInstance
        {
            public StatesInstance(GourmetFueledFabricator smi) : base(smi) { }
        }

        public class States : GameStateMachine<States, StatesInstance, GourmetFueledFabricator>
        {
            public static StatusItem waitingForFuelStatus;
            public State waiting;
            public State ready;

            public override void InitializeStates(out BaseState default_state)
            {
                if (waitingForFuelStatus == null)
                {
                    waitingForFuelStatus = new StatusItem("waitingForFuelStatus", global::STRINGS.BUILDING.STATUSITEMS.ENOUGH_FUEL.NAME, global::STRINGS.BUILDING.STATUSITEMS.ENOUGH_FUEL.TOOLTIP, "status_item_no_gas_to_pump", StatusItem.IconType.Custom, NotificationType.BadMinor, false, OverlayModes.None.ID, 129022, true, null);
                    waitingForFuelStatus.resolveStringCallback = (str, obj) =>
                    {
                        var inst = (GourmetFueledFabricator)obj;
                        return string.Format(str, inst.oxidizerTag.ProperName(), GameUtil.GetFormattedMass(Mathf.Max(inst.minMethane, inst.minOxidizer)));
                    };
                }

                default_state = waiting;

                waiting
                    .Enter(smi => { smi.master.converter.enabled = false; smi.master.operational.SetFlag(gourmetFuelFlag, false); })
                    .ToggleStatusItem(waitingForFuelStatus, smi => smi.master)
                    .EventTransition(GameHashes.OnStorageChange, ready, smi => smi.master.HasFuelReady());

                ready
                    .Enter(smi =>
                    {
                        smi.master.converter.SetStorage(smi.master.storage);
                        smi.master.converter.enabled = true;
                        smi.master.operational.SetFlag(gourmetFuelFlag, true);
                        smi.master.MarkQueueDirtyOnce();
                    })
                    // Use hysteresis to avoid rapid bounce due to tiny consumption ticks
                    .EventTransition(GameHashes.OnStorageChange, waiting, smi => smi.master.BelowHysteresis());
            }
        }
    }

    // Patch GourmetCookingStation.GetAvailableFuel so it reads from the fuel gate's storage (outStorage)
    [HarmonyPatch(typeof(GourmetCookingStation), nameof(GourmetCookingStation.GetAvailableFuel))]
    internal static class GourmetCookingStation_GetAvailableFuel_UseOutStorage
    {
        static bool Prefix(GourmetCookingStation __instance, ref float __result)
        {
            var fuelGate = __instance.GetComponent<GourmetFueledFabricator>();
            if (fuelGate != null && fuelGate.storage != null)
            {
                // Require BOTH methane and oxidizer in the gate's storage (outStorage) to report fuel available
                float methane = fuelGate.storage.GetAmountAvailable(fuelGate.methaneTag);
                float oxidizer = fuelGate.storage.GetAmountAvailable(fuelGate.oxidizerTag);
                if (methane >= fuelGate.minMethane && oxidizer >= fuelGate.minOxidizer)
                {
                    __result = methane; // report methane amount when both present
                }
                else
                {
                    __result = 0f; // gate readiness when oxidizer missing or below threshold
                }
                return false; // skip original
            }
            return true; // fallback to original behavior if our gate isn't present
        }
    }

    // Disable the original GourmetCookingStation state machine after it starts
    [HarmonyPatch(typeof(GourmetCookingStation), nameof(GourmetCookingStation.OnSpawn))]
    internal static class GourmetCookingStation_OnSpawn_DisableOriginalSM
    {
        static void Postfix(GourmetCookingStation __instance)
        {
            try
            {
                var fi = typeof(GourmetCookingStation).GetField("smi", BindingFlags.Instance | BindingFlags.NonPublic);
                var smiObj = fi?.GetValue(__instance);
                var stop = smiObj?.GetType().GetMethod("StopSM", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                stop?.Invoke(smiObj, null);
            }
            catch
            {
                // ignore; GetAvailableFuel gating still prevents original SM readiness
            }
        }
    }


    [HarmonyPatch(typeof(GourmetCookingStationConfig), "ConfigureBuildingTemplate")]
    public static class GourmetCookingStation_OxygeninputPatch
    {
        public const float OXYGEN_INPUT = 0.2f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var fabricator = go.AddOrGet<GourmetCookingStation>();

            var fuelStorage = go.AddComponent<Storage>();
            fuelStorage.capacityKg = 50f;

            // Ambient gas consumer that fills the oxidizer storage
            var dualGasConsumer = go.AddComponent<DualGasElementConsumer>();
            dualGasConsumer.storage = fuelStorage;

            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.storage = fuelStorage;

            // Fuel gate: require both methane and oxidizer present in outStorage (hysteresis to prevent thrashing)
            var fuelGate = go.AddOrGet<GourmetFueledFabricator>();
            fuelGate.storage = fuelStorage;
            fuelGate.methaneTag = SimHashes.Methane.CreateTag();
            fuelGate.oxidizerTag = ModTags.OxidizerGas;
            fuelGate.minMethane = 0.1f;
            fuelGate.minOxidizer = 0.02f;
            fuelGate.hysteresisFactor = 0.5f;

            // Converter: bind to outStorage once and don't reassign elsewhere
            var elementConverter = go.GetComponent<ElementConverter>();
            elementConverter.storage = fuelStorage;
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[2]
            {
                new ElementConverter.ConsumedElement(SimHashes.Methane.CreateTag(), 0.1f),
                new ElementConverter.ConsumedElement(ModTags.OxidizerGas, 0.02f)
            };
            elementConverter.outputElements = new ElementConverter.OutputElement[2]
            {
                new ElementConverter.OutputElement(0.025f, SimHashes.CarbonDioxide, 348.15f, outputElementOffsety: 2f),
                new ElementConverter.OutputElement(0.095f, ModElementRegistration.AshByproduct, 313.15f, storeOutput:true)
            };

            // Status component: explicitly point to the oxidizer tag
            var status = go.AddOrGet<OxidizerLowStatus>();
            status.oxidizerTag = ModTags.OxidizerGas;
            status.explicitStorage = fuelStorage;

            var elementDropper = go.AddComponent<ElementTileMaker>();

            elementDropper.emitTag = ModElementRegistration.AshByproduct.id;
            elementDropper.emitMass = 40f;
            elementDropper.emitOffset = new Vector3(1.0f, 0.0f, 0.0f);
            elementDropper.storage = fuelStorage;
            elementDropper.MakeTiles = false;




        }
    }

    [HarmonyPatch(typeof(GourmetCookingStationConfig), nameof(GourmetCookingStationConfig.ConfigureRecipes))]
    internal static class GourmetCookingStation_AltIngredients_Patch
    {
        private const float Meat_CookedMeatAmount = 1f; private const float Meat_SmokedDinoAmount = 0.8f;
        private const float Fish_CookedFishAmount = 1f;
        private const float Fish_SmokedFishAmount = 1600f / 2800f;

        [HarmonyPostfix]
        private static void Postfix()
        {
            // Surf and Turf
            var sttRecipeField = AccessTools.Field(typeof(SurfAndTurfConfig), "recipe");
            var sttRecipe = sttRecipeField?.GetValue(null) as ComplexRecipe;
            if (sttRecipe != null)
            {
                ReplaceIngredientWithAlternativesWithAmounts(
                    sttRecipe,
                    wanted: TagManager.Create("CookedMeat"),
                    alts: new Tag[] { TagManager.Create("CookedMeat"), TagManager.Create("SmokedDinosaurMeat") },
                    possibleMaterialAmounts: new float[] { Meat_CookedMeatAmount, Meat_SmokedDinoAmount }
                );

                ReplaceIngredientWithAlternativesWithAmounts(
                    sttRecipe,
                    wanted: TagManager.Create("CookedFish"),
                    alts: new Tag[] { TagManager.Create("CookedFish"), TagManager.Create("SmokedFish") },
                    possibleMaterialAmounts: new float[] { Fish_CookedFishAmount, Fish_SmokedFishAmount }
                );
            }

            // Burger
            var burgerRecipeField = AccessTools.Field(typeof(BurgerConfig), "recipe");
            var burgerRecipe = burgerRecipeField?.GetValue(null) as ComplexRecipe;
            if (burgerRecipe != null)
            {
                ReplaceIngredientWithAlternativesWithAmounts(
                    burgerRecipe,
                    wanted: TagManager.Create("CookedMeat"),
                    alts: new Tag[] { TagManager.Create("CookedMeat"), TagManager.Create("SmokedDinosaurMeat") },
                    possibleMaterialAmounts: new float[] { Meat_CookedMeatAmount, Meat_SmokedDinoAmount }
                );
            }
        }

        // Replaces the first matching ingredient with an alt-material element and sets per-option amounts.
        private static void ReplaceIngredientWithAlternativesWithAmounts(ComplexRecipe recipe, Tag wanted, Tag[] alts, float[] possibleMaterialAmounts)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Length == 0) return;
            if (alts == null || alts.Length == 0) return;

            // If caller provided mismatched amounts length, fall back to same amount for all
            float defaultAmount = (possibleMaterialAmounts != null && possibleMaterialAmounts.Length > 0) ? possibleMaterialAmounts[0] : 1f;

            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                var ing = recipe.ingredients[i];
                if (ing.material == wanted)
                {
                    // Construct using single-amount ctor (required by this build)
                    var el = new ComplexRecipe.RecipeElement(alts, defaultAmount);
                    // Then override per-option amounts if provided and matches length
                    if (possibleMaterialAmounts != null && possibleMaterialAmounts.Length == alts.Length)
                    {
                        el.possibleMaterialAmounts = possibleMaterialAmounts;
                    }

                    recipe.ingredients[i] = el;
                    return;
                }
            }
        }
    }
}