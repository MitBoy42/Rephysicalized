using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Patches
{
    // Adds our runtime component during building post-config
    [HarmonyPatch(typeof(UraniumCentrifugeConfig), nameof(UraniumCentrifugeConfig.DoPostConfigureComplete))]
    internal static class UraniumCentrifuge_Radiation_DoPostConfigureComplete_Patch
    {
        private static void Postfix(GameObject go)
        {
            // Attach the controller that manages radiation emission while converting
            go.AddOrGet<CentrifugeRadiationDuringFabrication>();
        }
    }

    // Enables a RadiationEmitter only while the centrifuge is actively fabricating (Operational.Active)
    public sealed class CentrifugeRadiationDuringFabrication : KMonoBehaviour
    {
        [MyCmpReq] private Operational operational;
        [MyCmpGet] private ComplexFabricator fabricator; // not strictly needed, but useful if you want finer-grained checks later

        private RadiationEmitter emitter;

        public override void OnSpawn()
        {
            base.OnSpawn();

            // Ensure and configure the emitter
            emitter = gameObject.AddOrGet<RadiationEmitter>();
            ConfigureEmitterDefaults(emitter);

            // Subscribe to active state changes (driven by fabrication activity for ComplexFabricator)
            Subscribe((int)GameHashes.ActiveChanged, OnActiveChanged);

            // Initialize emission state to current active state
            SetEmitting(operational != null && operational.IsActive);
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.ActiveChanged, OnActiveChanged);
            base.OnCleanUp();
        }

        private void OnActiveChanged(object data)
        {
            bool isActive = operational != null && operational.IsActive;
            SetEmitting(isActive);
        }

        private void ConfigureEmitterDefaults(RadiationEmitter e)
        {
            // Requested defaults
            e.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            e.radiusProportionalToRads = false;
            e.emitRads = 1000f;        // base emission strength
            e.emitRate = 0f;           // keep 0; emission is toggled by SetEmitting/enable
            e.emissionOffset = new Vector3(0f, 2f);
        }

        private void SetEmitting(bool on)
        {
            if (emitter == null)
                return;

            // Prefer an explicit API if available
            try { emitter.SetEmitting(on); } catch { /* older versions may not have SetEmitting */ }

            // Fallbacks to ensure effect
            emitter.enabled = on;
            emitter.emitRads = on ? 1000f : 0f; 
        }
    }
}