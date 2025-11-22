using UnityEngine;
using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
namespace Rephysicalized
{

    public class DualGasElementConsumer : KMonoBehaviour, ISim200ms
    {
        public float consumptionRate; // Set externally
        public float capacityKG;      // Set externally
        public byte consumptionRadius; // Set externally
        public Vector3 sampleCellOffset; // Set externally
        public bool storeOnConsume;   // Set externally
        public bool isRequired;       // Set externally
        public bool showInStatusPanel; // Set externally
        public bool showDescriptor;   // Set externally
        public bool ignoreActiveChanged; // Set externally
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
            // if (totalConsumed > 0f)
            //     Debug.Log($"[DualGasElementConsumer] Total consumed {totalConsumed}kg of breathable gases this tick");
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
