using Database;
using HarmonyLib;
using Klei.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized.Content.Plant_patches
{
    // Patch the PlantMutations constructor so all built-in mutations are already created.
    [HarmonyPatch(typeof(PlantMutations))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(ResourceSet) })]
    public static class PlantMutationsMinRadiationPatch
    {
        // Mapping for desired MinRadiationThreshold values per mutation
        private static readonly Dictionary<Func<PlantMutations, PlantMutation>, float> Targets =
            new Dictionary<Func<PlantMutations, PlantMutation>, float>
            {
                { pm => pm.moderatelyLoose,     50f  },
                { pm => pm.moderatelyTight,     100f },
                { pm => pm.extremelyTight,      150f },
                { pm => pm.bonusLice,           50f  },
                { pm => pm.sunnySpeed,          100f },
                { pm => pm.blooms,              0f   },
                { pm => pm.loadedWithFruit,     100f },
                { pm => pm.slowBurn,            50f  },
                { pm => pm.heavyFruit,          100f },
                // Intentionally leaving rottenHeaps unchanged
            };

       public static void Postfix(PlantMutations __instance)
        {
            foreach (var entry in Targets)
            {
                PlantMutation mutation = null;
                try
                {
                    mutation = entry.Key(__instance);
                }
                catch
                {
                    // If a field is missing in a particular version, skip it
                }

                if (mutation != null)
                {
                    SetMinRadiationThresholdSafe(mutation, entry.Value);
                }
            }
        }

        // Attempts to find and update the existing MinRadiationThreshold AttributeModifier.
        // If not found (different game version), adds a new one as a fallback.
        private static void SetMinRadiationThresholdSafe(PlantMutation mutation, float newValue)
        {
        
                var minAttr = Db.Get().PlantAttributes.MinRadiationThreshold;
                var modifiersField = AccessTools.Field(typeof(PlantMutation), "attributeModifiers");
                var modsObj = modifiersField?.GetValue(mutation) as IEnumerable;

                bool updated = false;
                if (modsObj != null)
                {
                    foreach (var am in modsObj)
                    {
                        if (IsMinRadiationModifier(am, minAttr))
                        {
                            if (TrySetModifierValue(am, newValue))
                            {
                                updated = true;
                                break;
                            }
                        }
                    }
                }

                if (!updated)
                {
                    // Fallback: add a new modifier (best-effort if internal layout changes)
                    mutation.AttributeModifier(minAttr, newValue);
                }
            }
   
        private static bool IsMinRadiationModifier(object attributeModifier, Klei.AI.Attribute minAttr)
        {
            if (attributeModifier == null) return false;

            // Typical Klei.AI.AttributeModifier has a "attributeId" string or an "attribute" reference.
            var amType = attributeModifier.GetType();

            // Try attributeId (string)
            var attrIdField = AccessTools.Field(amType, "attributeId");
            if (attrIdField != null)
            {
                var id = attrIdField.GetValue(attributeModifier) as string;
                if (!string.IsNullOrEmpty(id))
                    return string.Equals(id, minAttr.Id, StringComparison.Ordinal);
            }

            // Try attribute (Klei.AI.Attribute)
            var attrField = AccessTools.Field(amType, "attribute");
            if (attrField != null)
            {
                var attrObj = attrField.GetValue(attributeModifier);
                if (attrObj != null)
                {
                    // Try property Id or field Id
                    var idProp = AccessTools.Property(attrObj.GetType(), "Id")?.GetGetMethod(true);
                    if (idProp != null)
                    {
                        var id = idProp.Invoke(attrObj, null) as string;
                        if (!string.IsNullOrEmpty(id))
                            return string.Equals(id, minAttr.Id, StringComparison.Ordinal);
                    }

                    var idField = AccessTools.Field(attrObj.GetType(), "Id");
                    if (idField != null)
                    {
                        var id = idField.GetValue(attrObj) as string;
                        if (!string.IsNullOrEmpty(id))
                            return string.Equals(id, minAttr.Id, StringComparison.Ordinal);
                    }
                }
            }

            return false;
        }

        private static bool TrySetModifierValue(object attributeModifier, float newValue)
        {
            if (attributeModifier == null) return false;

            var amType = attributeModifier.GetType();

            // Try "Value" property first
            var valueProp = AccessTools.Property(amType, "Value");
            var setMethod = valueProp?.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(attributeModifier, new object[] { newValue });
                return true;
            }

            // Try "Value" field
            var valueField = AccessTools.Field(amType, "Value") ?? AccessTools.Field(amType, "value");
            if (valueField != null)
            {
                valueField.SetValue(attributeModifier, newValue);
                return true;
            }

            return false;
        }
    }

}
