using System;
using HarmonyLib;
using Rephysicalized.ModElements;
using UnityEngine;

namespace YourModNamespace.Patches
{
    // Simple accumulator that computes ash per completed recipe and emits chunks
    public sealed class GourmetCookingStationAshAccumulator : KMonoBehaviour
    {
        [SerializeField] public SimHashes ashHash = SimHashes.Void; // set in config patch
        [SerializeField] public float rateKgPerSecond = 0.075f;     // requested production rate

        [SerializeField] public float outputTemperature = 348.15f;

        private float accumulatedKg;

        public void AddFromRecipe(ComplexRecipe recipe)
        {
            if (recipe == null) return;
            float mass = Mathf.Max(0f, rateKgPerSecond * recipe.time);
            accumulatedKg += mass;
        }

        public void TryEmit(Vector3 worldPos)
        {
            if (accumulatedKg <= 0f || ashHash == SimHashes.Void)
                return;

            float toEmit = 0f;

            
                // Emit everything accumulated so far (emit on each recipe completion)
                toEmit = accumulatedKg;
                accumulatedKg = 0f;
           

            var elem = ElementLoader.FindElementByHash(ashHash);
            if (elem != null && elem.substance != null && toEmit > 0f)
            {
                elem.substance.SpawnResource(worldPos, toEmit, outputTemperature, byte.MaxValue, 0);
            }
        }
    }

    // Configure: remove any ash OutputElement from the ElementConverter and attach accumulator
    [HarmonyPatch(typeof(GourmetCookingStationConfig), nameof(GourmetCookingStationConfig.ConfigureBuildingTemplate))]
    public static class GourmetCookingStation_AshSetupPatch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter != null && elementConverter.outputElements != null && elementConverter.outputElements.Length > 0)
            {
                // Filter out any previously-added ash output to avoid per-second emissions via auto-ejection
                var ash = ModElementRegistration.AshByproduct;
                var kept = new System.Collections.Generic.List<ElementConverter.OutputElement>(elementConverter.outputElements.Length);
                foreach (var oe in elementConverter.outputElements)
                {
                    // Note: OutputElement exposes elementHash
                    if (oe.elementHash != ash)
                        kept.Add(oe);
                }
                elementConverter.outputElements = kept.ToArray();
            }

            // Ensure there is no ElementDropper targeting solids we intend to control manually
            // Not strictly necessary now that we don't store ash, but harmless to keep tidy if a custom dropper was added earlier.
            var dropper = go.GetComponent<ElementDropper>();
            if (dropper != null && dropper.emitTag == ModElementRegistration.AshByproduct.CreateTag())
            {
                UnityEngine.Object.DestroyImmediate(dropper);
            }

            // Attach or configure the accumulator
            var acc = go.AddOrGet<GourmetCookingStationAshAccumulator>();
            acc.ashHash = ModElementRegistration.AshByproduct;
            acc.rateKgPerSecond = 0.075f;

            acc.outputTemperature = 348.15f;
        }
    }

    [HarmonyPatch(typeof(GourmetCookingStation), nameof(GourmetCookingStation.SpawnOrderProduct))]
    public static class GourmetCookingStation_EmitAshOnRecipeCompletePatch
    {
        // Signature: protected override List<GameObject> SpawnOrderProduct(ComplexRecipe recipe)
        public static void Postfix(GourmetCookingStation __instance, ComplexRecipe recipe)
        {
            var acc = __instance.GetComponent<GourmetCookingStationAshAccumulator>();
            if (acc == null)
                return;

            acc.AddFromRecipe(recipe);

            // Apply spawn position offset (-1, 0)
            Vector3 spawnPos = __instance.transform.GetPosition() + new Vector3(-1f, 0f, 0f);
            acc.TryEmit(spawnPos);
        }
    }
}