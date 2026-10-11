using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace Rephysicalized
{
    internal sealed class WildPlantErosion : KMonoBehaviour, ISim4000ms
    {
        private const float FertilizationScale = 1f / 16f;

        private PlantMassTrackerComponent _tracker;
        private Growing _growing;
        private WiltCondition _wiltCondition;
        private FertilizationMonitor _fertilizationMonitor;
        private bool _growsFromAbove;

        public override void OnSpawn()
        {
            base.OnSpawn();

            _growing = GetComponent<Growing>();
            _wiltCondition = GetComponent<WiltCondition>();
            _fertilizationMonitor = GetComponent<FertilizationMonitor>();
            _tracker = PlantMassTrackerRegistry.EnsureTracker(gameObject);

            var seedProducer = GetComponent<SeedProducer>();
            if (seedProducer != null && !string.IsNullOrEmpty(seedProducer.seedInfo.seedId))
            {
                var seedPrefab = Assets.GetPrefab(new Tag(seedProducer.seedInfo.seedId));
                var seed = seedPrefab?.GetComponent<PlantableSeed>();
                _growsFromAbove = seed != null &&
                    seed.Direction == SingleEntityReceptacle.ReceptacleDirection.Bottom;
            }
        }

        public void Sim4000ms(float dt)
        {
            if (_tracker == null || _growing == null || !_growing.IsWildPlanted() || !_growing.IsGrowing())
                return;

            if (_wiltCondition != null && _wiltCondition.IsWilting())
                return;

            if (_fertilizationMonitor == null)
                _fertilizationMonitor = GetComponent<FertilizationMonitor>();

            var monitor = gameObject.GetSMI<FertilizationMonitor.Instance>();
            if (monitor == null || monitor.def?.consumedElements == null)
                return;

            var consumedElements = monitor.def.consumedElements;
            if (consumedElements.Length == 0)
                return;

            float fertilizerRate = consumedElements[0].massConsumptionRate *
                gameObject.GetAttributes().Get(Db.Get().PlantAttributes.FertilizerUsageMod).GetTotalValue();
            float requestedMass = fertilizerRate * FertilizationScale * dt;
            if (requestedMass <= 0f)
                return;

            int plantCell = Grid.PosToCell(transform.GetPosition());
            int cell = _growsFromAbove ? Grid.CellAbove(plantCell) : Grid.CellBelow(plantCell);
            if (!Grid.IsValidCell(cell) || Grid.Mass[cell] <= 0f)
                return;

            float consumedMass = Mathf.Min(requestedMass, Grid.Mass[cell]);
            SimMessages.ConsumeMass(cell, Grid.Element[cell].id, consumedMass, 1);
            _tracker.AddMassDelta(consumedMass);
        }
    }

    [HarmonyPatch(typeof(Growing), "OnSpawn")]
    internal static class Growing_OnSpawn_WildPlantErosion_Patch
    {
        private static void Postfix(Growing __instance)
        {
            var go = __instance?.gameObject;
            if (go == null)
                return;
            if (!Config.Instance.Erosion)
                return;
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null || !PlantMassTrackerRegistry.IsRegisteredPlant(kpid.PrefabID().Name))
                return;

            go.AddOrGet<WildPlantErosion>();
        }
    }
}
