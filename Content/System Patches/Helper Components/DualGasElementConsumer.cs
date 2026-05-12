using UnityEngine;
using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
namespace Rephysicalized
{

    public class DualGasElementConsumer : KMonoBehaviour, ISim200ms
    {
        [SerializeField] public float consumptionRate = 0.2f;
        [SerializeField]  public float capacityKG = 1f;
        [SerializeField] public byte consumptionRadius = 2;
        [SerializeField] public Vector3 sampleCellOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] public bool storeOnConsume = true;
        public bool isRequired = true;
      public bool showInStatusPanel = true;
        public bool showDescriptor = true;
        public bool ignoreActiveChanged = true;
          public Storage storage;

         private Storage Storage => this.storage;
        private Operational operational;

        public override void OnSpawn()
        {
            base.OnSpawn();
            // this.storage = GetComponent<Storage>();
            operational = GetComponent<Operational>();
            // Debug.Log($"[DualGasElementConsumer] OnSpawn: storage={(storage != null)}, operational={(operational != null)}");
        }


    public void Sim200ms(float dt)
{
    if (isRequired)
        ConsumeBreathableGases();
}

        private void ConsumeBreathableGases()
        {
            int cell = Grid.PosToCell(transform.GetPosition() + sampleCellOffset);
            List<int> cells = new List<int>();
            GridUtil.GetRadialCells(cell, consumptionRadius, cells);
            float totalConsumed = 0f;
            foreach (int c in cells)
            {
                Element element = Grid.Element[c];
                if (element.HasTag(ModTags.OxidizerGas) && Grid.Mass[c] > 0f)
                {
                    SimHashes gas = element.id;
                    float available = this.storage != null ? this.storage.GetMassAvailable(gas.CreateTag()) : 0f;
                    float toConsume = Mathf.Min(consumptionRate * Time.deltaTime, Grid.Mass[c], capacityKG - available);
                    if (toConsume > 0f)
                    {
                        SimMessages.ConsumeMass(c, gas, toConsume, consumptionRadius);
                        if (storeOnConsume && this.storage != null)
                        {
                            this.storage.AddGasChunk(gas, toConsume, Grid.Temperature[c], byte.MaxValue, 0, true);
                        }
                        totalConsumed += toConsume;
                    }
                }
            }
     
        }
    }

    // Utility for radial cell search
    public static class GridUtil
    {
        public static void GetRadialCells(int centerCell, int radius, List<int> result)
        {
            result.Clear();
            int cx, cy;
            Grid.CellToXY(centerCell, out cx, out cy);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (Grid.IsValidCell(Grid.XYToCell(x, y)))
                    {
                        result.Add(Grid.XYToCell(x, y));
                    }
                }
            }
        }
    }

}
