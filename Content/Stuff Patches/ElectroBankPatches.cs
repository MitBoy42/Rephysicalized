using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

using Rephysicalized.Content.System_Patches;

namespace Rephysicalized
{
    public class ElectrobankWaterZapPatch : KMonoBehaviour, ISim1000ms
    {
        [MyCmpGet]
        private Pickupable pickupable;
        private Building building;
        private Electrobank electrobank;
        private KSelectable selectable;

        public override void OnSpawn()
        {
            base.OnSpawn();
            building = GetComponent<Building>();
            electrobank = GetComponent<Electrobank>();
            selectable = GetComponent<KSelectable>();
        }

        public override void OnCleanUp()
        {
            ZapComponent zap = GetComponent<ZapComponent>();
            zap?.StopZapping();
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            bool isStored = pickupable?.KPrefabID?.HasTag(GameTags.Stored) ?? false;
            bool isEquipped = pickupable?.KPrefabID?.HasTag(GameTags.Equipped) ?? false;

            if (isStored || isEquipped)
            {
                GetComponent<ZapComponent>()?.StopZapping();
                return;
            }

            bool isInWater = false;
            int myCell = pickupable.cachedCell;


            if (Grid.IsValidCell(myCell) && Grid.Element[myCell].HasTag(GameTags.AnyWater))
            {
                isInWater = true;
            }

            ZapComponent zap = GetComponent<ZapComponent>();
            if (isInWater)
            {
                zap ??= gameObject.AddComponent<ZapComponent>();
                zap.DecorativeBeamScale = 0.5f;
                zap.DecorativeBeamOffset = new Vector3(0f, -0.2f, 0f);
                zap.DamageBeamOffset = new Vector3(0f, -1.3f, 0f);
                zap.DAMAGE_RATE = 2f;
                zap.StartZapping();
            }
            else
            {
                zap?.StopZapping();
            }
        }
    }


    [HarmonyPatch(typeof(ElectrobankConfig), "CreatePrefab")]
    internal static class ElectrobankConfig_CreatePrefab_WaterZap_Patch
    {
        private static void Postfix(ref GameObject __result)
        {
   
            __result.AddComponent<ElectrobankWaterZapPatch>();
        }
    }

    [HarmonyPatch(typeof(DisposableElectrobankConfig), "CreateDisposableElectrobank")]
    internal static class DisposableElectrobankConfig_CreateDisposableElectrobank_WaterZap_Patch
    {
        private static void Postfix(GameObject __result)
        {
            if (__result == null) return;
            __result.AddComponent<ElectrobankWaterZapPatch>();
        }
    }
    // Disposable Electrobank behavior:
    // - On empty, drop from storage and replace with:
    //    * DepletedUranium if uranium ore disposable
    //    * Crud if metal ore disposable

    [HarmonyPatch(typeof(Electrobank), "OnEmpty")]
    public static class Electrobank_OnEmpty_Disposable_Patch
    {
        public static bool Prefix(Electrobank __instance, bool dropWhenEmpty)
        {
       
                var go = __instance?.gameObject;
                if (go == null)
                    return true;

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null)
                    return true;

                // Only handle disposable portable batteries; let others run as normal
                if (!kpid.HasTag(GameTags.DisposablePortableBattery))
                    return true;

                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null)
                    return true;

                var uraniumTag = (Tag)DisposableElectrobankConfig.ID_URANIUM_ORE;
                var rawMetalTag = (Tag)DisposableElectrobankConfig.ID_METAL_ORE;

                SimHashes outHash = SimHashes.Vacuum;
                bool handled = false;

                if (kpid.PrefabTag == uraniumTag)
                {
                    // Uranium-ore disposable -> Depleted Uranium
                    outHash = SimHashes.DepletedUranium;
                    handled = true;
                }
                else if (kpid.PrefabTag == rawMetalTag)
                {
                    // Raw metal disposable -> its primary element (e.g., IronOre, CopperOre, etc.)
                    outHash = ModElementRegistration.CrudByproduct;
                    handled = true;
                }

                if (!handled)
                    return true; // Not our case: let original handle

                // Remove the battery from storage if present (so it drops as usual)
                var pickupable = go.GetComponent<Pickupable>();
                var storage = pickupable != null ? pickupable.storage : null;
                if (storage != null)
                {
                    storage.Remove(go);
                }

                // Spawn the debris at the same position, with the same mass/temp/disease as the battery
                Vector3 pos = go.transform.GetPosition();
                Element outElem = ElementLoader.FindElementByHash(outHash);
                if (outElem != null && outElem.substance != null)
                {
                    outElem.substance.SpawnResource(
                        pos,
                        pe.Mass,
                        pe.Temperature,
                        pe.DiseaseIdx,
                        pe.DiseaseCount
                    );
                }

                // Destroy the depleted battery object
                Util.KDestroyGameObject(go);

                // We handled the behavior; skip the original OnEmpty
                return false;
            
          
        }
    }


    // Ensures any Electrobank instance defaults to IronOre if its PrimaryElement was left as Creature
    [HarmonyPatch(typeof(Electrobank), nameof(Electrobank.OnSpawn))]
    internal static class Electrobank_OnSpawn_DefaultElement_Patch
    {
        private static void Postfix(Electrobank __instance)
        {
            if (__instance == null) return;

            var pe = __instance.GetComponent<PrimaryElement>();
            if (pe == null) return;

            // Some spawns use the CreateLooseEntity default (Creature). Normalize to IronOre.
            if (pe.ElementID == SimHashes.Creature)
            {
                float mass = pe.Mass;
                float temp = pe.Temperature;

                pe.SetElement(ModElementRegistration.CrudByproduct);

                // Preserve existing mass/temperature
                pe.Mass = mass;
                pe.Temperature = temp;
            }
        }
    }

        // CreateDisposableElectrobank currently calls CreateLooseEntity without passing 'element',
    // which defaults to Creature. This enforces the element argument (e.g., Cuprite/UraniumOre)
    // so Disposable banks never end up as Creature.
    [HarmonyPatch(typeof(DisposableElectrobankConfig), "CreateDisposableElectrobank")]
    internal static class DisposableElectrobankConfig_CreateDisposableElectrobank_ElementFix_Patch
    {
        // Signature must match the original to receive the 'element' argument
        private static void Postfix(GameObject __result, SimHashes element)
        {
            if (__result == null) return;

            var pe = __result.GetComponent<PrimaryElement>();
            if (pe == null) return;

            // If left at default Creature, set to the intended element for this variant
            if (pe.ElementID == SimHashes.Creature)
            {
                float mass = pe.Mass;
                float temp = pe.Temperature;

                pe.SetElement(element);

                pe.Mass = mass;
                pe.Temperature = temp;
            }
        }
    }

    // Additional patch: Force raw metal disposable base element to CrudByproduct at creation
    [HarmonyPatch(typeof(DisposableElectrobankConfig), "CreateDisposableElectrobank")]
    internal static class DisposableElectrobankConfig_CreateDisposableElectrobank_RawMetalBaseElement_Patch
    {
        private static void Postfix(GameObject __result, string id)
        {
            if (__result == null) return;
            if (id != DisposableElectrobankConfig.ID_METAL_ORE) return;  // "DisposableElectrobank_RawMetal"

            var pe = __result.GetComponent<PrimaryElement>();
            if (pe == null) return;

            float mass = pe.Mass;
            float temp = pe.Temperature;

            pe.SetElement(ModElementRegistration.CrudByproduct);

            pe.Mass = mass;
            pe.Temperature = temp;
        }
    }
}
