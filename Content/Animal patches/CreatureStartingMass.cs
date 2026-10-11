using HarmonyLib;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized.Content
{
    internal class CreatureStartingMass
    {

        // Configurable default mass (kg) per species prefix. Update values as needed.
        internal static class BaseCreatureDefaultMassConfig
        {
            public static readonly Dictionary<string, float> DefaultSpeciesMassKg =
                new Dictionary<string, float>(StringComparer.InvariantCultureIgnoreCase)
                {
                // Vanilla
                { "Hatch", 1f },
                { "Puft", 0.25f },
                { "Drecko", 1f },
                { "Squirrel", 1f },
                { "Pacu", 0.75f },
                { "OilFloater", 1f },
                { "LightBug", 0.1f },
                { "Crab", 1f },
                { "DivergentBeetle", 1f },
                { "Divergent", 1f },
                { "Staterpillar", 1f },
                { "Mole", 1f },
                { "Bee", 1f },
                { "Moo", 10f },
                { "Glom", 1f },

               
                { "WoodDeer", 1f },
                { "GlassDeer", 1f },
                { "Seal", 1f },
                { "GoldBelly", 4f },
                { "IceBelly", 4f },
                { "Stego", 4f },
                 { "Raptor", 4f },
                { "Butterfly", 1f },
                { "Mosquito", 1f },
                { "Chameleon", 0.5f },
                { "PrehistoricPacu", 2f },
                     { "AlgaeStego", 4f },

            //Aqua list
            {"ParrotFish", 1f  },
               {"Squid", 2f  },
                {"PufferFish", 1f  },
                {"Snail", 0.25f  },
                {"Seahorse", 1f  },
                {"SeaTurtle", 2f  },
                  {"SeaFairy", 10f  },
                };

            public static bool TryGetDesiredMass(string prefabId, out float mass)
            {
                mass = 0f;
                if (string.IsNullOrEmpty(prefabId))
                    return false;

                foreach (var kvp in DefaultSpeciesMassKg)
                {
                    // Prefix match to cover variants and babies
                    if (prefabId.StartsWith(kvp.Key, true, CultureInfo.InvariantCulture))
                    {
                        mass = kvp.Value;
                        return mass > 0f;
                    }
                }
                return false;
            }
        }

        // Patch all overloads of EntityTemplates.CreatePlacedEntity where arg4 is float mass.
        [HarmonyPatch]
        internal static class BaseCreatureDefaultMassPatch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                var methods = AccessTools.GetDeclaredMethods(typeof(EntityTemplates))
                                         .Where(m => m.Name == "CreatePlacedEntity");

                foreach (var m in methods)
                {
                    var p = m.GetParameters();
                    if (p.Length >= 5
                        && p[0].ParameterType == typeof(string)     // id
                        && p[1].ParameterType == typeof(string)     // name
                        && p[2].ParameterType == typeof(string)     // desc
                        && p[3].ParameterType == typeof(float))     // mass
                    {

                        yield return m;
                    }
                }
            }

            // Common prefix for all matched overloads
            static void Prefix(ref string id, ref float mass)
            {
                if (BaseCreatureDefaultMassConfig.TryGetDesiredMass(id, out var desiredMass))
                {
                    if (!Mathf.Approximately(mass, desiredMass))
                    {

                        mass = desiredMass;
                    }
                }

            }
        }
    }
}
