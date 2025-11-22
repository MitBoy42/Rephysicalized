using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized
{
    public class EnviromentCookablePatch : KMonoBehaviour, ISim4000ms
    {
        [MyCmpReq] private PrimaryElement element;

        // Threshold (Kelvin). If enableFreezing == false: temp >= cookTemperature. If true: temp <= cookTemperature.
        [SerializeField] public float temperature = 273.15f;

        // Prefab ID to transform into on trigger.
        [SerializeField] public string ID;

        // Optional environmental element gate. Empty => skip element check.
        [SerializeField] public List<SimHashes> triggeringElements = new List<SimHashes>();

        // Fraction (>= 0) of the resulting output mass to remove from the cell's element. Values > 1 allowed.
        [SerializeField] public float elementConsumedRatio = 0f;

        // Fraction (>= 0) of this object's mass to assign to the output. Values > 1 allowed.
        [SerializeField] public float massConversionRatio = 1f;

        // Mode toggle only (no separate freeze fields anymore)
        [SerializeField] public bool enableFreezing = false;

        // Optional pressure/amount gate (applies to both modes)
        [SerializeField] public bool requirePressure = true;

        [SerializeField] public float pressureThreshold = 0f;

        public void Sim4000ms(float dt)
        {
            if (element == null || gameObject == null || !gameObject.activeInHierarchy)
                return;

      
            float thresholdK = temperature;
            float tempK = element.Temperature;

            // Flip comparator based on mode
            if (enableFreezing)
            {
                if (tempK > thresholdK)
                    return;
            }
            else
            {
                if (tempK < thresholdK)
                    return;
            }

            int cell = Grid.PosToCell(this);
            if (!Grid.IsValidCell(cell))
                return;

            // Cell element check (optional)
            Element cellElement = Grid.Element[cell];
     

            if (triggeringElements != null && triggeringElements.Count > 0)
            {
                SimHashes cellId = cellElement.id;
                bool match = false;
                for (int i = 0; i < triggeringElements.Count; i++)
                {
                    if (triggeringElements[i] == cellId)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                    return;
            }

            // Optional pressure gate
            if (requirePressure && pressureThreshold > 0f)
            {
                float cellMass = Grid.Mass[cell];
                if (cellMass < pressureThreshold)
                    return;
            }

            // Output mass scaling; clamp to >= 0; allow >1
            float outputMassScale = Mathf.Max(0f, massConversionRatio);
            float envConsumePercent = Mathf.Max(0f, elementConsumedRatio);

            // Execute transform
            TransformTo(ID, outputMassScale, envConsumePercent, cell, cellElement);
        }

        private void TransformTo(string targetId, float outputMassScale, float envConsumePercent, int cell, Element cellElement)
        {
            // Resolve target prefab
            GameObject targetPrefab = Assets.GetPrefab(new Tag(targetId));
            if (targetPrefab == null)
            {
                Debug.LogWarning($"[EnviromentCookablePatch] Target prefab not found for id='{targetId}' on '{gameObject?.name}'.");
                return;
            }

            // Compute output mass and desired environment removal
            float outputMass = Mathf.Max(0f, element.Mass * outputMassScale);

            // Allow envConsumePercent > 1; clamp removal to available cell mass later
            float desiredRemoval = outputMass * envConsumePercent;

            // Spawn the new prefab
            Vector3 position = transform.GetPosition();
            position.z = Grid.GetLayerZ(Grid.SceneLayer.Ore);

            GameObject newGO = Util.KInstantiate(targetPrefab, position);
            newGO.SetActive(true);

            // Preserve selection in UI if applicable
            var currentSel = gameObject.GetComponent<KSelectable>();
            if (SelectTool.Instance != null && SelectTool.Instance.selected != null && SelectTool.Instance.selected == currentSel)
            {
                var newSel = newGO.GetComponent<KSelectable>();
                if (newSel != null)
                    SelectTool.Instance.Select(newSel);
            }

            // Transfer temperature and mass
            PrimaryElement newPE = newGO.GetComponent<PrimaryElement>();
            if (newPE != null)
            {
                newPE.Temperature = element.Temperature;
                newPE.Mass = outputMass;
            }

            // Remove proportional amount from the cell (based on output mass), clamped to available mass
            if (desiredRemoval > 0f && Grid.IsValidCell(cell))
            {
                float available = Grid.Mass[cell]; // mass of the cell's current element
                float toRemove = Mathf.Min(available, desiredRemoval);

                if (toRemove > 0f)
                {
                    float cellTemp = Grid.Temperature[cell];
                    byte diseaseIdx = Grid.DiseaseIdx[cell];
                    int diseaseCount = Grid.DiseaseCount[cell];

                    SimMessages.AddRemoveSubstance(
                        cell,
                        cellElement.idx,
                        CellEventLogger.Instance.ElementConsumerSimUpdate,
                        -toRemove,
                        cellTemp,
                        diseaseIdx,
                        diseaseCount,
                        true,
                        -1
                    );
                }
            }

            // Replace original
            gameObject.DeleteObject();
        }
    }
}
