using System;
using HarmonyLib;
using Klei;
using UnityEngine;

namespace Rephysicalized
{
    // Patch FarmTile prefab configuration to attach our checker
    [HarmonyPatch(typeof(FarmTileConfig), nameof(FarmTileConfig.DoPostConfigureComplete))]
    public static class FarmTileConfig_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            // Ensure every FarmTile instance gets our checker
            go.AddOrGet<UnstableFarmTileChecker>();
        }
    }

    // Component that evaluates at spawn and when the support cell changes
    public class UnstableFarmTileChecker : KMonoBehaviour
    {
        private static readonly Tag UnstableTag = TagManager.Create("unstable");

        [MyCmpReq] private PrimaryElement primaryElement;
        [MyCmpGet] private Deconstructable deconstructable; // used to deconstruct instead of hard-destroy

        // Partitioner handle to listen for solid cell changes beneath this tile
        private HandleVector<int>.Handle solidChangedPartitioner;

        public override void OnSpawn()
        {
            base.OnSpawn();

            // IMPORTANT: If built unsupported at spawn, don't refund materials.
            // Hard-destroy to avoid "tile remains + refund" duplication.
            if (DestroyImmediatelyIfUnsupportedAtSpawn())
                return;

            // Subscribe to solid-changed events for the cell below
            SubscribeToBelowSolidChanges();
        }

        public override void OnCleanUp()
        {
            // Unsubscribe from partitioner on cleanup
            if (solidChangedPartitioner.IsValid())
            {
                GameScenePartitioner.Instance.Free(ref solidChangedPartitioner);
            }
            base.OnCleanUp();
        }

        // Returns true if we destroyed the object (unsupported + unstable at spawn)
        private bool DestroyImmediatelyIfUnsupportedAtSpawn()
        {
            try
            {
                if (!IsMadeFromUnstableMaterial())
                    return false;

                if (HasSolidCellUnderneath())
                    return false;

                // Remove without refund to prevent duplication.
                Util.KDestroyGameObject(gameObject);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnstableFarmTiles] Error while evaluating farm tile at spawn: {e}");
                return false;
            }
        }

        private void SubscribeToBelowSolidChanges()
        {
            int below = GetBelowCell();
            if (!Grid.IsValidCell(below))
                return;

            // Extents expects x,y,width,height — not a cell index.
            Grid.CellToXY(below, out int x, out int y);

            var extents = new Extents(x, y, 1, 1);
            solidChangedPartitioner = GameScenePartitioner.Instance.Add(
                "UnstableFarmTileChecker:below",
                gameObject,
                extents,
                GameScenePartitioner.Instance.solidChangedLayer,
                OnBelowSolidChanged
            );
        }

        private void OnBelowSolidChanged(object data)
        {
            // Re-evaluate whenever the below cell changes solid state
            TryDeconstructIfUnstableAndUnsupported();
        }

        private void TryDeconstructIfUnstableAndUnsupported()
        {
            try
            {
                if (!IsMadeFromUnstableMaterial())
                    return;

                if (HasSolidCellUnderneath())
                    return;

                // Post-spawn: deconstruct instantly (refund is OK here).
                if (deconstructable != null)
                {
                    deconstructable.SetAllowDeconstruction(true);
                    // Instant path: complete deconstruction immediately.
                    // If your ONI version has ForceDeconstruct(), prefer that; otherwise OnCompleteWork is a workable instant path.
                    deconstructable.OnCompleteWork(null);
                }
                else
                {
                    // Fallback: if somehow no Deconstructable is present, hard destroy
                    Util.KDestroyGameObject(gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnstableFarmTiles] Error while evaluating farm tile: {e}");
            }
        }

        private bool IsMadeFromUnstableMaterial()
        {
            if (primaryElement == null)
                return false;

            Element element = ElementLoader.FindElementByHash(primaryElement.ElementID);
            if (element == null)
                return false;

            return element.HasTag(UnstableTag);
        }

        private bool HasSolidCellUnderneath()
        {
            int below = GetBelowCell();
            if (!Grid.IsValidCell(below))
                return false; // invalid below cell counts as "no solid underneath"

            return Grid.Solid[below];
        }

        private int GetBelowCell()
        {
            int here = Grid.PosToCell(gameObject);
            if (!Grid.IsValidCell(here))
                return Grid.InvalidCell;
            return Grid.CellBelow(here);
        }
    }
}