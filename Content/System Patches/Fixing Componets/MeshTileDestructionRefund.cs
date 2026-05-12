using HarmonyLib;
using UnityEngine;
using Klei;
using Klei.AI;


namespace Rephysicalized.Content.System_Patches
{
    [HarmonyPatch(typeof(BuildingHP), nameof(BuildingHP.DestroyOnDamaged))]
    public static class MeshTileRefundPatch
    {
        public static void Postfix(BuildingHP __instance)
        {
            if (__instance == null) return;

            BuildingComplete complete = __instance.GetComponent<BuildingComplete>();
            if (complete == null || complete.Def == null) return;

            KPrefabID prefabId = __instance.GetComponent<KPrefabID>();
            if (prefabId == null) return;

            string prefabTag = prefabId.PrefabTag.ToString();
            if (prefabTag != "MeshTile" && prefabTag != "GasPermeableMembrane") return;

            Vector3 pos = __instance.transform.GetPosition();
            int cell = Grid.PosToCell(pos);
            float temp = Grid.Temperature[cell];

            PrimaryElement pe = __instance.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                Vector3 dropPos = Grid.CellToPos(cell);
                pe.Element.substance.SpawnResource(dropPos, pe.Mass, pe.Temperature, byte.MaxValue, 0);
            }
        }
    }
}
