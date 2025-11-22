using Klei;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

[AddComponentMenu("KMonoBehaviour/scripts/ElementTileMakerPatch")]
public class ElementTileMakerPatch : KMonoBehaviour
{
    [SerializeField] public Tag emitTag;
    [SerializeField] public float emitMass;

    // Single-target world-space offset (backward compatible)
    [SerializeField] public Vector3 emitOffset = Vector3.zero;

    // Multi-target: grid cell offsets relative to the building's origin cell
    [SerializeField] public List<CellOffset> emitCellOffsets = new List<CellOffset>();

    [SerializeField] public Storage storage;

    private static readonly EventSystem.IntraObjectHandler<ElementTileMakerPatch> OnStorageChangedDelegate =
        new EventSystem.IntraObjectHandler<ElementTileMakerPatch>((component, data) => component.OnStorageChanged(data));

    public override void OnSpawn()
    {
        base.OnSpawn();
        // Hook storage change to attempt emission (GameHashes.OnStorageChange may be -1697596308 in some builds)
        Subscribe(-1697596308, OnStorageChangedDelegate);
    }

    private void OnStorageChanged(object _)
    {
        if (storage == null || emitMass <= 0f || emitTag == Tag.Invalid)
            return;

        // Resolve target cells first to avoid consuming if there are no valid targets
        List<int> targetCells = GetTargetCells();
        if (targetCells.Count == 0)
            return;

        // Ensure enough mass is available
        if (storage.GetMassAvailable(emitTag) < emitMass)
            return;

        // Remove the mass from storage
        float consumedMass;
        SimUtil.DiseaseInfo diseaseInfo;
        float aggregateTemperature;

        storage.ConsumeAndGetDisease(
            emitTag,
            emitMass,
            out consumedMass,
            out diseaseInfo,
            out aggregateTemperature
        );

        if (consumedMass <= 0f)
            return;

        // Determine temperature to apply to all cells
        float temperature = aggregateTemperature != 0.0f
            ? aggregateTemperature
            : (GetComponent<PrimaryElement>()?.Temperature ?? 300f);

        // Resolve element once
        Element element = ElementLoader.GetElement(emitTag);
        if (element == null)
            return;

        // Split mass equally among valid cells (use consumedMass to account for rounding/availability)
        float perCellMass = consumedMass / targetCells.Count;

        // Emit to each cell and schedule a dig order
        foreach (int cell in targetCells)
        {
            if (!Grid.IsValidCell(cell))
                continue;

            // Spawn as a tile in the cell (applied asynchronously by sim thread)
            SimMessages.ReplaceAndDisplaceElement(
                cell,
                element.id,
                CellEventLogger.Instance.ReceiveElementChanged,
                perCellMass,
                temperature,
                diseaseInfo.idx,
                diseaseInfo.count
            );

            // Defer the dig order until sim has applied the new element
            DigOrderUtil.TryQueueDigWithRetries(cell, attempts: 3, delaySeconds: 0.15f);
        }
    }

    /// <summary>
    /// Computes the list of target cells to emit into:
    /// - If emitCellOffsets has entries, uses them relative to the building's origin cell.
    /// - Otherwise, uses a single cell at transform position + emitOffset.
    /// Only valid cells are returned; duplicates are removed.
    /// </summary>
    private List<int> GetTargetCells()
    {
        var result = new List<int>();

        if (emitCellOffsets != null && emitCellOffsets.Count > 0)
        {
            int origin = Grid.PosToCell(transform.GetPosition());
            for (int i = 0; i < emitCellOffsets.Count; i++)
            {
                int cell = Grid.OffsetCell(origin, emitCellOffsets[i]);
                if (Grid.IsValidCell(cell))
                    result.Add(cell);
            }
        }
        else
        {
            // Single world-space offset
            Vector3 spawnPos = transform.GetPosition() + emitOffset;
            int cell = Grid.PosToCell(spawnPos);
            if (Grid.IsValidCell(cell))
                result.Add(cell);
        }

        // Deduplicate cells if any duplicates exist
        if (result.Count > 1)
        {
            var set = new HashSet<int>(result);
            if (set.Count != result.Count)
                result = new List<int>(set);
        }

        return result;
    }
}

/// <summary>
/// Utility to queue a dig chore at a cell. This version:
/// - Retries with a delay to let sim apply element changes
/// - Creates a Diggable safely (no NREs)
/// - Triggers the Klei spawn pipeline so OnSpawn runs (creates the chore)
/// - Emits detailed debug logs to diagnose failures
/// </summary>
public static class DigOrderUtil
{
    private const bool LOG = true;

    public static void TryQueueDigWithRetries(int cell, int attempts = 4, float delaySeconds = 0.3f)
    {
        if (!Grid.IsValidCell(cell) || attempts <= 0)
            return;

        GameScheduler.Instance.Schedule("QueueDigAtCell", delaySeconds, _ =>
        {
            if (!QueueDigAtCellImmediate(cell))
            {
                TryQueueDigWithRetries(cell, attempts - 1, delaySeconds);
            }
            else
            {
            }
        });
    }

    private static bool QueueDigAtCellImmediate(int cell)
    {
        if (!Grid.IsValidCell(cell))
        {
            return false;
        }
        // Already marked?
        var existing = Diggable.GetDiggable(cell);
        if (existing != null)
        {
            return true;
        }

        // Must be a valid dig target (depends on sim-applied state)
        bool diggableNow = Diggable.IsDiggable(cell);
        if (!diggableNow)
            return false;

        // Use the same path as the dig tool (MarkCellDigAction)
        var go = DigTool.PlaceDig(cell, 0);
        if (go == null)
        {
            return false;
        }
        // Match tool behavior: set priority to last selected (if available)
        try
        {
            var prio = go.GetComponent<Prioritizable>();
            var toolMenu = ToolMenu.Instance;
            var prioScreen = toolMenu != null ? toolMenu.PriorityScreen : null;
            if (prio != null && prioScreen != null)
                prio.SetMasterPriority(prioScreen.GetLastSelectedPriority());
        }
        catch { /* ignore */ }

        // Verify registration
        var spawned = Diggable.GetDiggable(cell);
        return spawned != null;
    }

    private static bool SafeSolid(int cell)
    {
        try { return Grid.Solid[cell]; } catch { return false; }
    }

    private static string SafeElementName(int cell)
    {
        try { var el = Grid.Element[cell]; return el != null ? el.name : "null"; } catch { return "exc"; }
    }
}