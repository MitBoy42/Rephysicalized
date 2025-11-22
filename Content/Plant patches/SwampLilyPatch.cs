using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Rephysicalized
{
    // Patch 1: After the SwampLily prefab is created:
    // - add salt fertilizer requirement
    // - set Crop amount to 10 (Codex)
    [HarmonyPatch(typeof(SwampLilyConfig), nameof(SwampLilyConfig.CreatePrefab))]
    public static class SwampLily_CreatePrefab_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            try
            {
                // Re-add fertilizer: consume Salt at 10 kg/cycle == 0.01666667 kg/s
                EntityTemplates.ExtendPlantToFertilizable(__result, new PlantElementAbsorber.ConsumeInfo[]
                {
                    new PlantElementAbsorber.ConsumeInfo
                    {
                        tag = SimHashes.Salt.CreateTag(),
                        massConsumptionRate = 0.01666667f
                    }
                });

                var crop = __result.GetComponent<Crop>();
                if (crop != null && TrySetCropComponentAmount(crop, 10))
                 //   Debug.Log("[Rephysicalized] SwampLily: Codex yield set to 10, salt fertilizer added.")
                 ;
            }
            catch (Exception e)
            {
          //      Debug.LogError($"[Rephysicalized] SwampLily prefab post-process failed: {e}");
            }
        }

        private static bool TrySetCropComponentAmount(Crop crop, int desired)
        {
            var cropValField = AccessTools.Field(typeof(Crop), "cropVal");
            var boxed = cropValField.GetValue(crop);
            var cvt = boxed.GetType();

            var countField =
                AccessTools.Field(cvt, "numProduced") ??
                AccessTools.Field(cvt, "amount") ??
                AccessTools.Field(cvt, "count") ??
                AccessTools.Field(cvt, "quantity");

            object valueToSet =
                countField.FieldType == typeof(int) ? desired
                : (countField.FieldType == typeof(float) || countField.FieldType == typeof(double))
                    ? Convert.ChangeType((double)desired, countField.FieldType)
                    : null;

            countField.SetValue(boxed, valueToSet);
            cropValField.SetValue(crop, boxed); // write back boxed struct
            return true;
        }
    }

    // Patch 2: After the database initializes, set TUNING.CROPS entry for SwampLilyFlower to 10 (runtime harvest)
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class Db_Initialize_SwampLilyCropTuning_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                const string cropId = "SwampLilyFlower"; // SwampLilyFlowerConfig.ID
                const int desired = 10;

                if (TrySetCropAmountInCropsTuning(cropId, desired)) ;

            }
            catch (Exception e)
            {
 
            }
        }

        private static bool TrySetCropAmountInCropsTuning(string cropId, int desired)
        {
            var cropsType = AccessTools.TypeByName("TUNING.CROPS");
            bool changed = false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            foreach (var field in cropsType.GetFields(flags))
            {
                var container = field.GetValue(null);

                try
                {
                    // Arrays
                    if (field.FieldType.IsArray)
                    {
                        var arr = (Array)container;
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var elem = arr.GetValue(i);
                            if (TrySetAmountOnCropDescriptor(elem, cropId, desired))
                            {
                                arr.SetValue(elem, i); // write-back for struct elements
                                changed = true;
                            }
                        }
                        continue;
                    }

                    // Lists (IList handles List<T>)
                    if (typeof(IList).IsAssignableFrom(field.FieldType))
                    {
                        var list = (IList)container;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var elem = list[i];
                            if (TrySetAmountOnCropDescriptor(elem, cropId, desired))
                            {
                                list[i] = elem; // write-back for struct elements
                                changed = true;
                            }
                        }
                        continue;
                    }

                    // Single descriptor
                    if (TrySetAmountOnCropDescriptor(container, cropId, desired))
                    {
                        if (field.FieldType.IsValueType)
                            field.SetValue(null, container); // write back if struct
                        changed = true;
                    }
                }
                catch
                {
                    // Skip any field we cannot process
                }
            }

            return changed;
        }

        private static bool TrySetAmountOnCropDescriptor(object descriptor, string cropId, int desired)
        {
            var t = descriptor.GetType();
            var id = TryGetId(descriptor);
            if (!string.Equals(id, cropId, StringComparison.Ordinal))
                return false;

            var amountField =
                AccessTools.Field(t, "numProduced") ??
                AccessTools.Field(t, "amount") ??
                AccessTools.Field(t, "count") ??
                AccessTools.Field(t, "quantity");

            if (amountField != null)
            {
                if (amountField.FieldType == typeof(int))
                    amountField.SetValue(descriptor, desired);
                else if (amountField.FieldType == typeof(float) || amountField.FieldType == typeof(double))
                    amountField.SetValue(descriptor, Convert.ChangeType((double)desired, amountField.FieldType));
                else
                    return false;

                return true;
            }

            var amountProp =
                AccessTools.Property(t, "numProduced") ??
                AccessTools.Property(t, "amount") ??
                AccessTools.Property(t, "count") ??
                AccessTools.Property(t, "quantity");

            if (amountProp?.CanWrite == true)
            {
                if (amountProp.PropertyType == typeof(int))
                    amountProp.SetValue(descriptor, desired, null);
                else if (amountProp.PropertyType == typeof(float) || amountProp.PropertyType == typeof(double))
                    amountProp.SetValue(descriptor, Convert.ChangeType((double)desired, amountProp.PropertyType), null);
                else
                    return false;

                return true;
            }

            return false;
        }

        private static string TryGetId(object o)
        {
            var ot = o.GetType();

            foreach (var f in AccessTools.GetDeclaredFields(ot))
            {
                if (f.FieldType == typeof(string))
                {
                    var name = f.Name.ToLowerInvariant();
                    if (name == "id" || name == "cropid" || name == "crop_id" || name.Contains("produced") || name.Contains("product"))
                        return f.GetValue(o) as string;
                }
            }

            foreach (var p in AccessTools.GetDeclaredProperties(ot))
            {
                if (p.CanRead && p.PropertyType == typeof(string))
                {
                    var name = p.Name.ToLowerInvariant();
                    if (name == "id" || name == "cropid" || name == "crop_id" || name.Contains("produced") || name.Contains("product"))
                        return p.GetValue(o, null) as string;
                }
            }

            return null;
        }
    }
}