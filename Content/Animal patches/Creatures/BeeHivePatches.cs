using HarmonyLib;
using UnityEngine;
using Klei.AI;
using System.Reflection;
using STRINGS;
using TUNING;

namespace Rephysicalized.Content.AnimalPatches.Creatures
{
    // User-fixed MassBasedECController (preserve)
    public sealed class MassBasedECController : KMonoBehaviour, ISim4000ms
    {
        [MyCmpGet] private ElementConsumer ec;
        [MyCmpGet] private PrimaryElement pe;

        public override void OnSpawn()
        {
            base.OnSpawn();
        }

        public void Sim4000ms(float dt)
        {
            if (ec == null || pe == null) return;
            bool enable = pe.Mass <= 20f;
            ec.EnableConsumption(enable);
    }

    // Bee->Hive mass conservation: copy bee mass to new hive when bee transforms
    [HarmonyPatch(typeof(BeeMakeHiveStates.Instance), nameof(BeeMakeHiveStates.Instance.BuildHome))]
    internal static class BeeToHiveMassTransfer_Patch
    {
        public static void Postfix(BeeMakeHiveStates.Instance __instance)
        {
            var peBee = __instance.master.GetComponent<PrimaryElement>();
            if (peBee == null || peBee.Mass <= 0f) return;

            int cell = __instance.targetBuildCell;
            Vector3 pos = Grid.CellToPos(cell, CellAlignment.Bottom, Grid.SceneLayer.Creatures);
            GameObject hiveGO = null;
            float minDist = float.MaxValue;
            foreach (BeeHive.StatesInstance smi in Components.BeeHives)
            {
                if (smi.master == null) continue;
                Vector3 hivePos = smi.transform.GetPosition();
                float dist = Vector3.Distance(hivePos, pos);
                if (dist < minDist && dist < 1f)
                {
                    minDist = dist;
                    hiveGO = smi.master.gameObject;
                }
            }

            if (hiveGO == null) 
            {
                // Fallback: check objects at cell layers
                for (int layer = 0; layer < Grid.ObjectLayers.Length; layer++)
                {
                    var go = Grid.Objects[cell, layer];
                    if (go != null && go.GetComponent<BeeHive.StatesInstance>() != null)
                    {
                        hiveGO = go;
                        break;
                    }
                }
            }

            if (hiveGO == null) return;

            var peHive = hiveGO.GetComponent<PrimaryElement>();
            var hiveTracker = hiveGO.GetComponent<CreatureMassTracker>();
            if (hiveTracker != null)
            {
                hiveTracker.SetMassAbsolute(peBee.Mass);
         //       Debug.Log($"BeeToHiveMassTransfer: SetAbsolute bee mass {peBee.Mass:F2}kg to new hive {hiveGO.name} ({minDist:F2}m) at {cell}");
            }
            else if (peHive != null)
            {
                peHive.Mass = peBee.Mass;
           //     Debug.Log($"BeeToHiveMassTransfer: Fallback copied bee mass {peBee.Mass:F2}kg to new hive {hiveGO.name} ({minDist:F2}m) at {cell}");
            }
        }
    }
}


    // Low mass status SM (like FueledDietWidget SolidFuelRefillStatusSM)
    public sealed class LowMassStatusSM : GameStateMachine<LowMassStatusSM, LowMassStatusSM.Instance, IStateMachineTarget>
    {

        public State normal;
        public State lowMass;

        public new class Instance : GameStateMachine<LowMassStatusSM, LowMassStatusSM.Instance, IStateMachineTarget>.GameInstance
        {
            public Instance(IStateMachineTarget master) : base(master) { }
        }

        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
            default_state = normal;

            normal
                .Update(delegate(Instance smi, float dt)
                {
                    var pe = smi.master.gameObject.GetComponent<PrimaryElement>();
                    if (pe != null && pe.Mass < 2f)
                        smi.GoTo(lowMass);
                }, UpdateRate.SIM_4000ms);

            lowMass
                .ToggleStatusItem(STRINGS.STATUSITEMS.HIVE_DEPLETED.NAME, STRINGS.STATUSITEMS.HIVE_DEPLETED.TOOLTIP, "", StatusItem.IconType.Info, NotificationType.BadMinor, false, OverlayModes.None.ID)
                .Update(delegate(Instance smi, float dt)
                {
                    var pe = smi.master.gameObject.GetComponent<PrimaryElement>();
                    if (pe != null && pe.Mass >= 2f)
                        smi.GoTo(normal);
                }, UpdateRate.SIM_4000ms);
        }
    }

    // Prefab: Add ElementConsumer + controllers
    [HarmonyPatch(typeof(BaseBeeHiveConfig), nameof(BaseBeeHiveConfig.CreatePrefab))]
    public static class CreatePrefab_Patch
    {
        public static void Postfix(ref GameObject __result)
        {
            if (__result == null) return;

            var ec = __result.AddOrGet<ElementConsumer>();
            ec.elementToConsume = SimHashes.CarbonDioxide;
            ec.consumptionRate = 0.05f;
            ec.consumptionRadius = 3;
            ec.showInStatusPanel = true;
            ec.isRequired = false;
            ec.storeOnConsume = false;
            ec.showDescriptor = true;
            ec.ignoreActiveChanged = true;

         __result.AddOrGet<TemperatureVulnerable>().Configure(TUNING.CREATURES.TEMPERATURE.FREEZING_9, TUNING.CREATURES.TEMPERATURE.FREEZING_10, TUNING.CREATURES.TEMPERATURE.FREEZING_1, TUNING.CREATURES.TEMPERATURE.MODERATE);
            __result.AddOrGet<MassBasedECController>();
            __result.AddOrGet<StateMachineController>();
        }
    } 

    // Initial refresh
    [HarmonyPatch(typeof(BaseBeeHiveConfig), nameof(BaseBeeHiveConfig.OnSpawn))]
    public static class OnSpawn_Patch
    {
        public static void Postfix(GameObject inst)
        {
            var ec = inst.GetComponent<ElementConsumer>();
            if (ec != null)
            {
                ec.RefreshConsumptionRate();
                ec.UpdateStatusItem();
            }

            var lowMassSmi = inst.GetSMI<LowMassStatusSM.Instance>();
            if (lowMassSmi == null)
            {
                var smc = inst.AddOrGet<StateMachineController>();
                lowMassSmi = new LowMassStatusSM.Instance(smc);
                lowMassSmi.StartSM();
            }
        }
    }

    // CO2 mass gain
    [HarmonyPatch]
    internal static class AddMassInternal_Patch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(ElementConsumer), "AddMassInternal") ?? AccessTools.Method(typeof(ElementConsumer), "AddMass");

        public static void Postfix(ElementConsumer __instance, Sim.ConsumedMassInfo consumed_info)
        {
            if (__instance.elementToConsume != SimHashes.CarbonDioxide || consumed_info.mass <= 0f) return;

            var smi = __instance.GetSMI<BeeHive.StatesInstance>();
            if (smi == null) return;

            var tracker = smi.master.GetComponent<CreatureMassTracker>();
        
                tracker.AddExternalMass(consumed_info.mass);
        }
    }

    // Uranium mass gain
    [HarmonyPatch(typeof(BeehiveCalorieMonitor.Instance), nameof(BeehiveCalorieMonitor.Instance.OnCaloriesConsumed))]
    internal static class UraniumMassGain_Patch
    {
        public static void Postfix(BeehiveCalorieMonitor.Instance __instance, object data)
        {
            var evtBoxed = data as Boxed<CreatureCalorieMonitor.CaloriesConsumedEvent>;
            if (evtBoxed?.value == null || evtBoxed.value.tag != BeeHiveTuning.CONSUMED_ORE) return;

            float kg = evtBoxed.value.calories / BeeHiveTuning.CALORIES_PER_KG_OF_ORE;
            var tracker = __instance.gameObject.GetComponent<CreatureMassTracker>();
      
                tracker.AddExternalMass(0.1f * kg);
        }
    }

    // Bee spawn: -1f mass, skip if <2f (status auto-handled by SM)
[HarmonyPatch(typeof(BeeHive.StatesInstance), nameof(BeeHive.StatesInstance.SpawnNewBeeFromHive))]
    [HarmonyPatch(typeof(BeeHive.StatesInstance), nameof(BeeHive.StatesInstance.SpawnNewLarvaFromHive))]
    internal static class BeeSpawnMassCost_Patch
    {
        public static bool Prefix(BeeHive.StatesInstance __instance)
        {
            var go = __instance.master.gameObject;
            var tracker = go.GetComponent<CreatureMassTracker>();
            var pe = go.GetComponent<PrimaryElement>();
            if (pe == null || (tracker?.GetCurrentMass() ?? pe.Mass) < 2f)
            {
          //      Debug.Log($"BeeSpawnMassCost Prefix blocked spawn on {go.name}, mass {(tracker?.GetCurrentMass() ?? pe.Mass):F2}");
                return false;
            }
     //       Debug.Log($"BeeSpawnMassCost Prefix allowed spawn on {go.name}, mass {pe.Mass:F2}");
        
          
                float oldMass = tracker.GetCurrentMass();
                tracker.AddExternalMass(-1f);
              //  Debug.Log($"Bee spawn mass subtract: {oldMass:F2} -> {tracker.GetCurrentMass():F2} on {go.name}");
            
            return true;
        }
    }

    // BeeHive death: spawn SolidNuclearWaste = tracked mass - 5f (reserving base)
    [HarmonyPatch(typeof(BeeHive.StatesInstance), nameof(BeeHive.StatesInstance.OnCleanUp))]
    internal static class BeeHiveDeathDrop_Patch
    {
        public static void Postfix(BeeHive.StatesInstance __instance)
        {
            var go = __instance.master.gameObject;
            var tracker = go.GetComponent<CreatureMassTracker>();
            if (tracker == null) return;
            float dropMass = Mathf.Max(0f, tracker.GetCurrentMass() - 5f);
            if (dropMass <= 0f) return;
            var pe = go.GetComponent<PrimaryElement>();
            float temp = pe?.Temperature ?? 300f;
            int cell = Grid.PosToCell(go.transform.position);
            Vector3 pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);
            var element = ElementLoader.GetElement("SolidNuclearWaste");
            if (element != null && element.substance != null)
            {
                element.substance.SpawnResource(pos, dropMass, temp, byte.MaxValue, 0);
          //      Debug.Log($"BeeHiveDeathDrop: Spawned {dropMass:F2}kg SolidNuclearWaste from {go.name} tracker {tracker.GetCurrentMass():F2} at {cell}");
            }
        }
    }
}

