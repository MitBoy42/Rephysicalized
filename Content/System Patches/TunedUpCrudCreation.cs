using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Klei.AI;

using System.Reflection;
using Rephysicalized.ModElements;

namespace Rephysicalized.Content.System_Patches
{
    internal class TunedUpCrudCreation
    {



        [HarmonyPatch(typeof(Tinkerable), nameof(Tinkerable.OnCompleteWork))]
        public static class Tinkerable_CrudReward_Patch
        {
            public static void Postfix(Tinkerable __instance)
            {
                if (!Config.Instance.TunedUpCrudCreation)
                    return;
                GameObject go = __instance.gameObject;
                if (go.TryGetComponent(out KPrefabID prefabID) 
                    && prefabID.HasTag(RoomConstraints.ConstraintTags.GeneratorType)) 
                {
                    Vector3 pos = __instance.transform.GetPosition();
                    ElementLoader.GetElement(ModElementRegistration.CrudByproduct.Tag).substance.SpawnResource(Grid.CellToPosCBC(Grid.PosToCell(pos), Grid.SceneLayer.Ore), 5f, 293.15f, byte.MaxValue, 0, false, go);
                }
            }
        }
    }
}
