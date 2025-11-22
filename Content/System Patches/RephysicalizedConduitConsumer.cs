using STRINGS;
using STRINGS;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized
{

    [SkipSaveFileSerialization]
    [AddComponentMenu("KMonoBehaviour/scripts/RephysicalizedConduitConsumer")]
    public class RephysicalizedConduitConsumer : KMonoBehaviour, IConduitConsumer
    {
        [SerializeField] public ConduitType conduitType;
        [SerializeField] public bool ignoreMinMassCheck;

        // Replacement for capacityTag: allow multiple tags and/or explicit elements
        [SerializeField] public Tag[] acceptedTags = Array.Empty<Tag>();
        [SerializeField] public SimHashes[] acceptedElements = Array.Empty<SimHashes>();

        [SerializeField] public float capacityKG = float.PositiveInfinity;
        [SerializeField] public bool forceAlwaysSatisfied;
        [SerializeField] public bool alwaysConsume;
        [SerializeField] public bool keepZeroMassObject = true;
        [SerializeField] public bool useSecondaryInput;
        [SerializeField] public bool isOn = true;

        [NonSerialized] public bool isConsuming = true;
        [NonSerialized] public bool consumedLastTick = true;

        [MyCmpReq] public Operational operational;
        [MyCmpReq] protected Building building;

        public Operational.State OperatingRequirement;
        public ISecondaryInput targetSecondaryInput;

        [MyCmpGet] public Storage storage;
        [MyCmpGet] private BuildingComplete m_buildingComplete;

        private int utilityCell = -1;

        // Same default as vanilla; ConfigureBuildingTemplate should set a finite value
        public float consumptionRate = float.PositiveInfinity;

        public SimHashes lastConsumedElement = SimHashes.Vacuum;
        private HandleVector<int>.Handle partitionerEntry;
        private bool satisfied;

        public ConduitConsumer.WrongElementResult wrongElementResult;

        public Storage Storage => storage;
        public ConduitType ConduitType => conduitType;

        public bool IsConnected =>
            (UnityEngine.Object)Grid.Objects[utilityCell, conduitType == ConduitType.Gas ? 12 : 16] != (UnityEngine.Object)null &&
            (UnityEngine.Object)m_buildingComplete != (UnityEngine.Object)null;

        public bool CanConsume
        {
            get
            {
                bool canConsume = false;
                if (IsConnected)
                    canConsume = GetConduitManager().GetContents(utilityCell).mass > 0.0f;
                return canConsume;
            }
        }

        // Vanilla-equivalent:
        // - If accepting any, use total storage mass.
        // - Otherwise, sum only items whose stored GameObject has any accepted tag OR whose element is explicitly accepted.
        public float stored_mass
        {
            get
            {
                if ((UnityEngine.Object)storage == (UnityEngine.Object)null)
                    return 0.0f;

                if (AcceptsAny)
                    return storage.MassStored();

                float total = 0f;
                var items = storage.items;
                for (int i = 0; i < items.Count; i++)
                {
                    var go = items[i];
                    if ((UnityEngine.Object)go == (UnityEngine.Object)null) continue;

                    bool matchesTag = HasAnyAcceptedTag(go);
                    if (!matchesTag)
                    {
                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe != null)
                            matchesTag = IsExplicitAccepted(pe.ElementID);
                    }

                    if (matchesTag)
                    {
                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe != null)
                            total += pe.Mass;
                    }
                }
                return total;
            }
        }

        public float space_remaining_kg
        {
            get
            {
                float b = capacityKG - stored_mass;
                return (UnityEngine.Object)storage != (UnityEngine.Object)null
                    ? Mathf.Min(storage.RemainingCapacity(), b)
                    : b;
            }
        }

        public void SetConduitData(ConduitType type) => conduitType = type;
        public ConduitType TypeOfConduit => conduitType;

        public bool IsAlmostEmpty => !ignoreMinMassCheck && MassAvailable < ConsumptionRate * 30.0f;

        public bool IsEmpty
        {
            get
            {
                if (ignoreMinMassCheck)
                    return false;
                return MassAvailable == 0.0f || MassAvailable < ConsumptionRate;
            }
        }

        public float ConsumptionRate => consumptionRate;

        public bool IsSatisfied
        {
            get => satisfied || !isConsuming;
            set => satisfied = value || forceAlwaysSatisfied;
        }

        private ConduitFlow GetConduitManager()
        {
            switch (conduitType)
            {
                case ConduitType.Gas:
                    return Game.Instance.gasConduitFlow;
                case ConduitType.Liquid:
                    return Game.Instance.liquidConduitFlow;
                default:
                    return (ConduitFlow)null;
            }
        }

        public float MassAvailable
        {
            get
            {
                ConduitFlow mgr = GetConduitManager();
                int inputCell = GetInputCell(mgr.conduitType);
                return mgr.GetContents(inputCell).mass;
            }
        }

        protected virtual int GetInputCell(ConduitType inputConduitType)
        {
            if (!useSecondaryInput)
                return building.GetUtilityInputCell();

            ISecondaryInput[] components = GetComponents<ISecondaryInput>();
            foreach (ISecondaryInput secondaryInput in components)
            {
                if (secondaryInput.HasSecondaryConduitType(inputConduitType))
                    return Grid.OffsetCell(building.NaturalBuildingCell(), secondaryInput.GetSecondaryConduitOffset(inputConduitType));
            }
            Debug.LogWarning("No secondaryInput of type was found");
            return Grid.OffsetCell(building.NaturalBuildingCell(), components[0].GetSecondaryConduitOffset(inputConduitType));
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            GameScheduler.Instance.Schedule("PlumbingTutorial", 2f, obj => Tutorial.Instance.TutorialMessage(Tutorial.TutorialMessages.TM_Plumbing), null, null);
            utilityCell = GetInputCell(GetConduitManager().conduitType);
            partitionerEntry = GameScenePartitioner.Instance.Add(
                "RephysicalizedConduitConsumer.OnSpawn",
                gameObject,
                utilityCell,
                GameScenePartitioner.Instance.objectLayers[conduitType == ConduitType.Gas ? 12 : 16],
                new System.Action<object>(OnConduitConnectionChanged)
            );
            GetConduitManager().AddConduitUpdater(new System.Action<float>(ConduitUpdate), ConduitFlowPriority.Default);
            OnConduitConnectionChanged(null);
        }

        public override void OnCleanUp()
        {
            GetConduitManager().RemoveConduitUpdater(new System.Action<float>(ConduitUpdate));
            GameScenePartitioner.Instance.Free(ref partitionerEntry);
            base.OnCleanUp();
        }

        // Keep this EXACT as requested (vanilla form with the event hash literal)
        private void OnConduitConnectionChanged(object data) => this.Trigger(-2094018600, (object)this.IsConnected);

        public void SetOnState(bool onState) => isOn = onState;

        private void ConduitUpdate(float dt)
        {
            if (!isConsuming || !isOn)
                return;

            ConduitFlow conduitManager = GetConduitManager();
            Consume(dt, conduitManager);
        }

        private void Consume(float dt, ConduitFlow conduit_mgr)
        {
            IsSatisfied = false;
            consumedLastTick = false;

            if (building.Def.CanMove)
                utilityCell = GetInputCell(conduit_mgr.conduitType);

            if (!IsConnected)
                return;

            ConduitFlow.ConduitContents contents = conduit_mgr.GetContents(utilityCell);
            if (contents.mass <= 0.0f)
                return;

            IsSatisfied = true;

            if (!alwaysConsume && !operational.MeetsRequirements(OperatingRequirement))
                return;

            float delta = Mathf.Min(ConsumptionRate * dt, space_remaining_kg);
            Element elementByHash1 = ElementLoader.FindElementByHash(contents.element);

            if (contents.element != lastConsumedElement)
                DiscoveredResources.Instance.Discover(elementByHash1.tag, elementByHash1.materialCategory);

            float mass = 0.0f;
            if (delta > 0.0f)
            {
                ConduitFlow.ConduitContents conduitContents = conduit_mgr.RemoveElement(utilityCell, delta);
                mass = conduitContents.mass;
                lastConsumedElement = conduitContents.element;
            }

            bool accepted = IsAcceptedForIntake(elementByHash1);

            // Use DoDamage (not DidDamage) as requested
            if (mass > 0.0f && !AcceptsAny && !accepted)
            {
                this.Trigger((int)GameHashes.DoBuildingDamage, new BuildingHP.DamageSourceInfo() { damage = 1, source = BUILDINGS.DAMAGESOURCES.BAD_INPUT_ELEMENT, popString = UI.GAMEOBJECTEFFECTS.DAMAGE_POPS.WRONG_ELEMENT });
            }

            if (accepted || wrongElementResult == ConduitConsumer.WrongElementResult.Store || contents.element == SimHashes.Vacuum || AcceptsAny)
            {
                if (mass <= 0.0f)
                    return;

                consumedLastTick = true;
                int disease_count = (int)(contents.diseaseCount * (mass / contents.mass));
                Element elementByHash2 = ElementLoader.FindElementByHash(contents.element);

                switch (conduitType)
                {
                    case ConduitType.Gas:
                        if (elementByHash2.IsGas)
                            storage.AddGasChunk(contents.element, mass, contents.temperature, contents.diseaseIdx, disease_count, keepZeroMassObject, false);
                        else
                            Debug.LogWarning("Gas conduit consumer consuming non gas: " + elementByHash2.id.ToString());
                        break;

                    case ConduitType.Liquid:
                        if (elementByHash2.IsLiquid)
                            storage.AddLiquid(contents.element, mass, contents.temperature, contents.diseaseIdx, disease_count, keepZeroMassObject, false);
                        else
                            Debug.LogWarning("Liquid conduit consumer consuming non liquid: " + elementByHash2.id.ToString());
                        break;
                }
            }
            else
            {
                if (mass <= 0.0f)
                    return;

                consumedLastTick = true;

                if (wrongElementResult != ConduitConsumer.WrongElementResult.Dump)
                    return;

                int disease_count = (int)(contents.diseaseCount * (mass / contents.mass));
                SimMessages.AddRemoveSubstance(
                    Grid.PosToCell(this.transform.GetPosition()),
                    contents.element,
                    CellEventLogger.Instance.ConduitConsumerWrongElement,
                    mass,
                    contents.temperature,
                    contents.diseaseIdx,
                    disease_count
                );
            }
        }

        // True if we accept all inputs (vanilla capacityTag == GameTags.Any)
        private bool AcceptsAny =>
            (acceptedTags == null || acceptedTags.Length == 0) &&
            (acceptedElements == null || acceptedElements.Length == 0);

        // Vanilla-equivalent intake check: in vanilla it's element.HasTag(capacityTag)
        // Here, accept if the element has any accepted tag OR the element is explicitly whitelisted.
        private bool IsAcceptedForIntake(Element element)
        {
            if (AcceptsAny || element == null) return true;

            // Check category tags on element
            if (acceptedTags != null)
            {
                for (int i = 0; i < acceptedTags.Length; i++)
                    if (element.HasTag(acceptedTags[i]))
                        return true;
            }

            // Check explicit elements
            if (acceptedElements != null)
            {
                for (int i = 0; i < acceptedElements.Length; i++)
                    if (acceptedElements[i] == element.id)
                        return true;
            }

            return false;
        }

        private bool HasAnyAcceptedTag(GameObject go)
        {
            if (acceptedTags == null || acceptedTags.Length == 0) return false;
            for (int i = 0; i < acceptedTags.Length; i++)
                if (go.HasTag(acceptedTags[i]))
                    return true;
            return false;
        }

        private bool IsExplicitAccepted(SimHashes id)
        {
            if (acceptedElements == null || acceptedElements.Length == 0) return false;
            for (int i = 0; i < acceptedElements.Length; i++)
                if (acceptedElements[i] == id)
                    return true;
            return false;
        }
    }
}
