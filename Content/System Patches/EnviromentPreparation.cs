using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Rephysicalized
{
    public class EnviromenmentalPreparation : KMonoBehaviour, ISim4000ms
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

        // New: number of 4s ticks required when conditions are met. 1 => instant (default behavior)
        [SerializeField] public int time = 1;

        // Runtime counter for consecutive qualifying ticks
        private int pendingTicks = 0;

        // Progress bar instance while transforming
        private ProgressBar progressBar;

        public void Sim4000ms(float dt)
        {
            if (element == null || gameObject == null || !gameObject.activeInHierarchy)
            {
                pendingTicks = 0;
                HideProgressBar();
                return;
            }

            float tempK = element.Temperature;
            bool tempOk = enableFreezing ? (tempK <= temperature) : (tempK >= temperature);

            int cell = Grid.PosToCell(this);
            bool cellOk = Grid.IsValidCell(cell);

            Element cellElement = cellOk ? Grid.Element[cell] : null;

            bool triggerOk = true;
            if (triggeringElements != null && triggeringElements.Count > 0)
            {
                triggerOk = cellOk && triggeringElements.Contains(cellElement.id);
            }

            bool pressureOk = true;
            if (requirePressure && pressureThreshold > 0f)
            {
                pressureOk = cellOk && Grid.Mass[cell] >= pressureThreshold;
            }

            bool canTransform = tempOk && cellOk && triggerOk && pressureOk;

            // Output mass scaling; clamp to >= 0; allow >1
            float outputMassScale = Mathf.Max(0f, massConversionRatio);
            float envConsumePercent = Mathf.Max(0f, elementConsumedRatio);

            // Additional gate: ensure required environmental mass is available if we are going to consume any
            if (canTransform && envConsumePercent > 0f && cellOk)
            {
                float outputMass = Mathf.Max(0f, element.Mass * outputMassScale);
                float desiredRemoval = outputMass * envConsumePercent;
                float available = Grid.Mass[cell];
                if (desiredRemoval > available + 1e-6f) // require enough mass in cell
                {
                    canTransform = false;
                }
            }

            if (!canTransform)
            {
                // Reset countdown and hide any active progress bar
                pendingTicks = 0;
                HideProgressBar();
                return;
            }

            if (time <= 1)
            {
                TransformTo(ID, outputMassScale, envConsumePercent, cell, cellElement);
                return;
            }

            // Show/update progress bar while counting down
            EnsureProgressBar();
            UpdateProgressBar();

            pendingTicks++;
            UpdateProgressBar();
            if (pendingTicks >= time)
            {
                TransformTo(ID, outputMassScale, envConsumePercent, cell, cellElement);
                pendingTicks = 0;
                HideProgressBar();
            }
        }

        private void TransformTo(string targetId, float outputMassScale, float envConsumePercent, int cell, Element cellElement)
        {
            GameObject targetPrefab = Assets.GetPrefab(new Tag(targetId));
            if (targetPrefab == null)
            {
                Debug.LogWarning($"[EnviromentCookablePatch] Target prefab not found for id='{targetId}' on '{gameObject?.name}'.");
                return;
            }

            float outputMass = Mathf.Max(0f, element.Mass * outputMassScale);
            float desiredRemoval = outputMass * envConsumePercent;

            Vector3 position = transform.GetPosition();
            position.z = Grid.GetLayerZ(Grid.SceneLayer.Ore);

            GameObject newGO = Util.KInstantiate(targetPrefab, position);
            newGO.SetActive(true);

            var currentSel = gameObject.GetComponent<KSelectable>();
            if (SelectTool.Instance != null && SelectTool.Instance.selected != null && SelectTool.Instance.selected == currentSel)
            {
                var newSel = newGO.GetComponent<KSelectable>();
                if (newSel != null)
                    SelectTool.Instance.Select(newSel);
            }

            PrimaryElement newPE = newGO.GetComponent<PrimaryElement>();
            if (newPE != null)
            {
                newPE.Temperature = element.Temperature;
                newPE.Mass = outputMass;
            }

            if (desiredRemoval > 0f && Grid.IsValidCell(cell))
            {
                float available = Grid.Mass[cell];
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

            gameObject.DeleteObject();
        }

        private void EnsureProgressBar()
        {
            if (progressBar == null)
            {
                try
                {
                    progressBar = ProgressBar.CreateProgressBar(gameObject, () => GetProgress01());
                    progressBar.autoHide = false; // we control visibility
                    progressBar.SetVisibility(true);
                }
                catch
                {
                    // swallow if UI not initialized
                    progressBar = null;
                }
            }
            else
            {
                // Retarget in case position changed
                progressBar.Retarget(gameObject);
                progressBar.SetUpdateFunc(() => GetProgress01());
                progressBar.SetVisibility(true);
            }
        }

        private float GetProgress01()
        {
            if (time <= 1) return 1f;
            // pendingTicks counts completed ticks; show progress including current frame
            float v = Mathf.Clamp01((float)pendingTicks / (float)time);
            return v;
        }

        private void UpdateProgressBar()
        {
            if (progressBar != null && progressBar.updatePercentFull != null)
            {
                progressBar.PercentFull = GetProgress01();
                progressBar.Retarget(gameObject);
            }
        }

        private void HideProgressBar()
        {
            if (progressBar != null)
            {
                try
                {
                    progressBar.SetVisibility(false);
                    progressBar.gameObject.DeleteObject();
                }
                catch { }
                progressBar = null;
            }
        }

        public override void OnCleanUp()
        {
            HideProgressBar();
            base.OnCleanUp();
        }
    }
}
