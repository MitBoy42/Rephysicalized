using Klei.AI;
using UnityEngine;

namespace Rephysicalized
{
    /// <summary>
    /// Shared trunk-to-branch mass transfer for branch-growing trees (Forest Tree, Space Tree).
    ///
    /// Every growing, non-wilting branch drains a fixed amount of mass per cycle from the trunk. The
    /// trunk is only drained while it sits above <see cref="TrunkBaselineKg"/>, and the whole tick is
    /// scaled down if the trunk cannot supply the full amount, so a trunk near the baseline feeds its
    /// branches proportionally instead of stopping dead.
    ///
    /// Subclasses supply the per-cycle drain and decide which branches are eligible. Both mass moves go
    /// through PlantMassTrackerComponent when present so the Plant Mass Tracker accounting stays correct,
    /// falling back to the raw PrimaryElement otherwise.
    /// </summary>
    public abstract class TreeTrunkDrainControllerBase : KMonoBehaviour, ISim1000ms
    {
        private const float SecondsPerCycle = 600f;

        [MyCmpGet] private PrimaryElement _trunkPE;
        [MyCmpGet] private WiltCondition _trunkWilt;

        private PlantMassTrackerComponent _trunkPMT;
        private PlantBranchGrower.Instance _grower;

        /// <summary>Mass each eligible branch takes from the trunk per cycle.</summary>
        protected abstract float DrainPerCycleKg { get; }

        /// <summary>Mass the trunk keeps in reserve; it is never drained below this.</summary>
        protected virtual float TrunkBaselineKg => 10f;

        /// <summary>True when this branch should take mass from the trunk right now.</summary>
        protected abstract bool IsBranchEligible(GameObject branch);

        /// <summary>Hook for trees that only drain when cultivated.</summary>
        protected virtual bool IsDrainSuspended() => false;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _trunkPMT = GetComponent<PlantMassTrackerComponent>();
            _grower = gameObject.GetSMI<PlantBranchGrower.Instance>();
        }

        public void Sim1000ms(float dt)
        {
            if (IsDrainSuspended())
                return;

            if (_trunkWilt == null)
                _trunkWilt = GetComponent<WiltCondition>();
            if (_trunkWilt != null && _trunkWilt.IsWilting())
                return; // no drain while the trunk is wilting

            if (_grower == null)
                _grower = gameObject.GetSMI<PlantBranchGrower.Instance>();
            if (_grower == null)
                return;

            // HashSet rather than a list: ActionPerBranch can visit the same branch more than once and a
            // duplicate would otherwise be drained twice in the same tick.
            var branches = HashSetPool<GameObject, TreeTrunkDrainControllerBase>.Allocate();
            try
            {
                _grower.ActionPerBranch(branch =>
                {
                    if (branch != null)
                        branches.Add(branch);
                });

                int eligibleCount = 0;
                foreach (var branch in branches)
                {
                    if (branch != null && IsBranchEligible(branch))
                        eligibleCount++;
                }

                if (eligibleCount == 0)
                    return;

                float availableAboveBaseline = GetTrunkMass() - TrunkBaselineKg;
                if (availableAboveBaseline <= 0f)
                    return;

                float perBranchDesired = Mathf.Max(0.001f, dt) * DrainPerCycleKg / SecondsPerCycle;
                float totalDesired = perBranchDesired * eligibleCount;
                float scale = Mathf.Min(1f, availableAboveBaseline / totalDesired);
                if (scale <= 0f)
                    return;

                float totalApplied = 0f;
                foreach (var branch in branches)
                {
                    if (branch == null || !IsBranchEligible(branch))
                        continue;

                    float applied = perBranchDesired * scale;
                    if (applied <= 0f)
                        continue;

                    AddMassToBranch(branch, applied);
                    totalApplied += applied;
                }

                if (totalApplied > 0f)
                    ApplyDrainToTrunk(totalApplied);
            }
            finally
            {
                branches.Recycle();
            }
        }

        private float GetTrunkMass()
        {
            if (_trunkPMT != null)
                return Mathf.Max(0f, _trunkPMT.TrackedMassKg);
            if (_trunkPE != null)
                return Mathf.Max(0f, _trunkPE.Mass);
            return 0f;
        }

        private void ApplyDrainToTrunk(float kg)
        {
            if (kg <= 0f)
                return;

            if (_trunkPMT != null)
            {
                _trunkPMT.AddExternalMass(-kg);
                if (_trunkPE != null)
                    _trunkPE.Mass = Mathf.Max(1f, _trunkPE.Mass - kg);
            }
            else if (_trunkPE != null)
            {
                _trunkPE.Mass = Mathf.Max(1f, _trunkPE.Mass - kg);
            }
        }

        private static void AddMassToBranch(GameObject branch, float kg)
        {
            if (branch == null || kg <= 0f)
                return;

            var pmt = branch.GetComponent<PlantMassTrackerComponent>();
            if (pmt != null)
            {
                pmt.AddExternalMass(+kg);
                var pe = branch.GetComponent<PrimaryElement>();
                if (pe != null)
                    pe.Mass = Mathf.Max(1f, pe.Mass + kg);
                return;
            }

            var bpe = branch.GetComponent<PrimaryElement>();
            if (bpe != null)
                bpe.Mass = Mathf.Max(1f, bpe.Mass + kg);
        }
    }
}
