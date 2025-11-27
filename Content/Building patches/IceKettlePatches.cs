using HarmonyLib;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace Rephysicalized.Content.Building_patches
{
    public class IceKettleOxidizerController : KMonoBehaviour
    {
        [SerializeField] public Storage oxidizerStorage;
        // Ratio constants
        public const float OxidizerPerWood = 0.1065f;   // kg oxidizer required per kg wood
        public const float AshPerWood = 0.9645f;        // kg ash per kg wood
        public const float AshTempK = 273.15f + 40f;    // 40C

        // Elements/tags – adjust if your mod uses custom IDs
        public static readonly Tag OxidizerGasTag = SimHashes.Oxygen.CreateTag(); // replace with ModTags.OxidizerGas if you have one
        public static readonly Tag AshTag = ModElementRegistration.AshByproduct.Tag;  // provided by your mod registry

        public override void OnSpawn()
        {
            base.OnSpawn();
            // If not wired by patch (unlikely), try to find nearby storage that is set for gases
            if (oxidizerStorage == null)
                oxidizerStorage = GetComponent<Storage>();
        }

        public float GetAvailableOxidizer() => oxidizerStorage != null ? oxidizerStorage.GetMassAvailable(OxidizerGasTag) : 0f;

        public bool HasOxidizerForWood(float woodKg) => GetAvailableOxidizer() >= woodKg * OxidizerPerWood;

        // Consume oxidizer proportional to wood actually consumed
        public void ConsumeOxidizerFor(float woodKg)
        {
            if (oxidizerStorage == null || woodKg <= 0) return;
            float need = woodKg * OxidizerPerWood;
            if (need <= 0) return;
            oxidizerStorage.ConsumeIgnoringDisease(OxidizerGasTag, need);
        }

        public void SpawnAshFor(float woodKg)
        {
            if (woodKg <= 0f) return;

            float ashMass = woodKg * AshPerWood;

            // Prefer storing in an on-building storage if available
           

                int cell = Grid.PosToCell(this);
                var element = ElementLoader.GetElement(AshTag);
                if (element?.substance != null && Grid.IsValidCell(cell))
                {
                    Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);
                    element.substance.SpawnResource(pos, ashMass, AshTempK, byte.MaxValue, 0, prevent_merge: false);
                }
            
       
    }
    }

    // Patch ConfigureBuildingTemplate to add oxidizer storage + consumer + controller
    [HarmonyPatch(typeof(IceKettleConfig), nameof(IceKettleConfig.ConfigureBuildingTemplate))]
    public static class IceKettleConfig_OxidizerPatch
    {
        // Match Kiln rates; adjust if needed
        private const float OXYGEN_INPUT_RATE = 0.4f;
        private const float OXIDIZER_CAPACITY_KG = 2f;

        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            
              

                // Add a dedicated storage for oxidizer gas
                var oxidizerStorage = go.AddComponent<Storage>();
                oxidizerStorage.capacityKg = OXIDIZER_CAPACITY_KG;
                oxidizerStorage.showInUI = true;
                oxidizerStorage.allowItemRemoval = false;
                oxidizerStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                // Limit to gases to avoid clutter
                oxidizerStorage.storageFilters = new List<Tag> { GameTags.Gas };

                // Gas consumer feeding that storage. If you have a custom element/tag, set it here.
                var gasConsumer = go.AddOrGet<DualGasElementConsumer>();
                gasConsumer.capacityKG = OXIDIZER_CAPACITY_KG;
                gasConsumer.consumptionRate = OXYGEN_INPUT_RATE;
                gasConsumer.storeOnConsume = true;
                gasConsumer.consumptionRadius = 2;
                gasConsumer.isRequired = true;
                gasConsumer.showInStatusPanel = true;
                gasConsumer.showDescriptor = true;
                gasConsumer.ignoreActiveChanged = true;
    
                gasConsumer.storage = oxidizerStorage;

                // Controller
                var ctrl = go.AddOrGet<IceKettleOxidizerController>();
                ctrl.oxidizerStorage = oxidizerStorage;
            var status = go.AddOrGet<OxidizerLowStatus>();
            status.explicitStorage = oxidizerStorage;
            status.oxidizerTag = ModTags.OxidizerGas;

        }
    }

    // Extend “can run” condition: require oxidizer for next batch (0.1065 × required wood)
    [HarmonyPatch(typeof(IceKettle), nameof(IceKettle.HasEnoughFuelForNextBacth))]
    public static class IceKettle_CanRun_ExtendWithOxidizer
    {
        // Replace vanilla check with: (no solids => true) OR (enough wood AND enough oxidizer for that wood)
        public static bool Prefix(IceKettle.Instance smi, ref bool __result)
        {
            try
            {
                // Original first clause: if no solids stored, return true (no need to gate)
                if (smi.kettleStorage == null || smi.kettleStorage.MassStored() <= 0f)
                {
                    __result = true;
                    return false;
                }

                // Compute wood required for next batch (vanilla does this later)
                float solidsTemp = smi.kettleStorage.items != null && smi.kettleStorage.items.Count > 0
                    ? smi.kettleStorage.items[0].GetComponent<PrimaryElement>()?.Temperature ?? 0f
                    : 0f;

                float woodRequired = smi.GetUnitsOfFuelRequiredToMelt(
                    ElementLoader.GetElement(smi.def.targetElementTag),
                    smi.def.KGToMeltPerBatch,
                    solidsTemp);

                // Need enough wood and enough oxidizer
                bool hasWood = smi.FuelUnitsAvailable >= woodRequired;

                var ctrl = smi.GetComponent<IceKettleOxidizerController>();
                bool hasOxidizer = false;
                if (ctrl != null)
                    hasOxidizer = ctrl.HasOxidizerForWood(woodRequired);

                __result = hasWood && hasOxidizer;
                return false; // skip original
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized][IceKettle] Fuel check patch failed: {e}");
                // Fallback to vanilla behavior on error
                return true;
            }
        }
    }

    // Consume oxidizer and spawn ash when melting a batch
    [HarmonyPatch(typeof(IceKettle.Instance), nameof(IceKettle.Instance.MeltNextBatch))]
    public static class IceKettle_Melt_ConsumeOxidizerAndSpawnAsh
    {
        public static void Prefix(IceKettle.Instance __instance, out float __state)
        {
            // __state will carry the planned woodRequired so we can compute matching oxidizer and ash post-consumption if needed
            __state = 0f;
            try
            {
                if (!(__instance.kettleStorage?.MassStored() > 0f)) return;

                float solidsTemp = __instance.kettleStorage.items != null && __instance.kettleStorage.items.Count > 0
                    ? __instance.kettleStorage.items[0].GetComponent<PrimaryElement>()?.Temperature ?? 0f
                    : 0f;

                __state = __instance.GetUnitsOfFuelRequiredToMelt(
                    ElementLoader.GetElement(__instance.def.targetElementTag),
                    __instance.def.KGToMeltPerBatch,
                    solidsTemp);
            }
            catch { __state = 0f; }
        }

        public static void Postfix(IceKettle.Instance __instance, float __state)
        {
            try
            {
              

                // Get controller
                var ctrl = __instance.GetComponent<IceKettleOxidizerController>();
                if (ctrl == null) return;

              
                float woodConsumed = Mathf.Max(0f, __state);
                if (woodConsumed <= 0f) return;

                // Consume oxidizer proportional to wood actually used
                ctrl.ConsumeOxidizerFor(woodConsumed);

                // Spawn ash at 40C
                ctrl.SpawnAshFor(woodConsumed);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized][IceKettle] Post-melt oxidizer/ash patch failed: {e}");
            }
        }
    }
}