using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace Rephysicalized.Content.Animal_patches
{
    public sealed class CreatureMassSize : KMonoBehaviour, ISim4000ms
    {
        private readonly struct MassRange
        {
            public readonly float startingMass;
            public readonly float fullMass;

            public MassRange(float startingMass, float fullMass)
            {
                this.startingMass = startingMass;
                this.fullMass = fullMass;
            }
        }

        private static readonly Dictionary<Tag, MassRange> massRangesByPrefabTag = new Dictionary<Tag, MassRange>();

        [Serialize] private float startingMass;
        [Serialize] private float fullMass;
        [Serialize] private float originalAnimScale = 1f;
        [Serialize] private bool hasOriginalAnimScale;

        private KBatchedAnimController animController;
        private PrimaryElement primaryElement;
        private CreatureMassTracker massTracker;

        public void Configure(float startingMass, float fullMass)
        {
            this.startingMass = Mathf.Max(0f, startingMass);
            this.fullMass = fullMass;

            var prefabId = GetComponent<KPrefabID>();
            if (prefabId != null && prefabId.PrefabTag.IsValid)
                massRangesByPrefabTag[prefabId.PrefabTag] = new MassRange(this.startingMass, this.fullMass);

            CaptureOriginalAnimScale();
        }

        public override void OnPrefabInit()
        {
            if (!(Config.Instance.CreatureSize))
                return;
            base.OnPrefabInit();
            CaptureOriginalAnimScale();
        }

        public override void OnSpawn()
        {
            if (!(Config.Instance.CreatureSize))
                return;
            base.OnSpawn();
            LoadRegisteredMassRange();
        }

        public void Sim4000ms(float dt)
        {
            if (animController == null)
                animController = GetComponent<KBatchedAnimController>();
            if (animController == null)
                return;
            if (!(Config.Instance.CreatureSize))
                return;

            LoadRegisteredMassRange();
            CaptureOriginalAnimScale();

            if (primaryElement == null)
                primaryElement = GetComponent<PrimaryElement>();
            if (massTracker == null)
                massTracker = GetComponent<CreatureMassTracker>();

            float currentMass = primaryElement != null
                ? primaryElement.Mass
                : massTracker != null ? massTracker.GetCurrentMass() : startingMass;
            float progress = fullMass > startingMass
                ? Mathf.Clamp01((currentMass - startingMass) / (fullMass - startingMass))
                : 0f;
            float sizeModifier = Mathf.Lerp(0.9f, 1.3f, progress);

            animController.animScale = originalAnimScale * sizeModifier;
        }

        private void LoadRegisteredMassRange()
        {
            var prefabId = GetComponent<KPrefabID>();
            if (prefabId != null && prefabId.PrefabTag.IsValid && massRangesByPrefabTag.TryGetValue(prefabId.PrefabTag, out var range))
            {
                startingMass = range.startingMass;
                fullMass = range.fullMass;
            }
        }

        private void CaptureOriginalAnimScale()
        {
            if (hasOriginalAnimScale)
                return;

            if (animController == null)
                animController = GetComponent<KBatchedAnimController>();
            if (animController == null)
                return;

            originalAnimScale = animController.animScale;
            hasOriginalAnimScale = true;
        }
    }
}
