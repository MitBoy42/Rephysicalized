using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

namespace Rephysicalized.ModElements
{

    public static class PlantFiberElementBindingPatches
    {
        private static readonly Tag PlantFiberPrefabTag = new Tag(PlantFiberConfig.ID);


        [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
        internal static class Db_Initialize_PlantFiberBinding
        {
            private static void Postfix()
            {
                var element = ElementLoader.FindElementByHash(ModElementRegistration.PlantFiberRP);
                if (element == null)
                    return;

                // Codex "Made & Used" and element-to-prefab lookups go through element.tag.
                element.tag = PlantFiberPrefabTag;
                element.substance.nameTag = PlantFiberPrefabTag;
            }
        }


        [HarmonyPatch(typeof(PlantFiberConfig), nameof(PlantFiberConfig.CreatePrefab))]
        internal static class PlantFiberConfig_CreatePrefab_Binding
        {
            private static void Postfix(GameObject __result)
            {
                if (__result == null)
                    return;

                var kpid = __result.GetComponent<KPrefabID>();
                if (kpid == null)
                    return;

                kpid.prefabInitFn += new KPrefabID.PrefabFn(ApplyElement);
                kpid.prefabSpawnFn += new KPrefabID.PrefabFn(ApplyElement);
            }

            private static void ApplyElement(GameObject inst)
            {
                var pe = inst?.GetComponent<PrimaryElement>();
                if (pe == null)
                    return;

                var element = ElementLoader.FindElementByHash(ModElementRegistration.PlantFiberRP);
                if (element != null)
                    pe.SetElement(element.id);
            }
        }
    }
}
