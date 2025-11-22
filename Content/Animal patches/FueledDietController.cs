using Rephysicalized.Chores;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rephysicalized
{
    [RequireComponent(typeof(KPrefabID))]
    [SkipSaveFileSerialization]
    public class FueledDietController : KMonoBehaviour
    {
        [MyCmpReq] private KPrefabID prefabID;
        [MyCmpGet] private Storage storage;
        [MyCmpGet] private PrimaryElement primaryElement;

        public FueledDiet fueledDiet;

        // Gas/liquid consumers only; solids are handled by the dedicated solid fuel chore
        private readonly List<ElementConsumer> consumers = new List<ElementConsumer>();
        private float storageCapacityOverride;

        // Public accessors
        public Storage FuelStorage => storage;
        public float RefillThreshold;
        public float FuelStorageCapacityKg => storageCapacityOverride > 0f
            ? storageCapacityOverride
            : (storage != null ? storage.capacityKg : 0f);

        // Enable verbose logging for diagnosis
        public bool EnableDebugLogs = false;

        // Accumulate pending emissions (converted outputs). Emission/poop hookup is handled elsewhere.
        private struct PendingEmission
        {
            public float mass;
            public float tempMass; // sum(mass_i * temp_i) for averaging
            public byte diseaseIdx;
            public int diseaseCount;
        }
        private readonly Dictionary<Tag, PendingEmission> pendingOutputs = new Dictionary<Tag, PendingEmission>();

        // Public DTO for safe dequeue without reflection
        public readonly struct FueledOutput
        {
            public readonly Tag Tag;
            public readonly float MassKg;
            public readonly float TemperatureK;
            public readonly byte DiseaseIdx;
            public readonly int DiseaseCount;

            public FueledOutput(Tag tag, float massKg, float temperatureK, byte diseaseIdx, int diseaseCount)
            {
                Tag = tag;
                MassKg = massKg;
                TemperatureK = temperatureK;
                DiseaseIdx = diseaseIdx;
                DiseaseCount = diseaseCount;
            }
        }

        // Peek total pending fueled mass (kg), no clearing
        public float PeekPendingTotalMass()
        {
            float sum = 0f;
            foreach (var kv in pendingOutputs) sum += kv.Value.mass;
            return sum;
        }

        // Dequeue all pending outputs into 'buffer' and clear internal queue. Returns number of entries added.
        public int DequeuePendingOutputs(List<FueledOutput> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int beforeCount = buffer.Count;

            float fallbackTemp = primaryElement != null ? primaryElement.Temperature : 300f;

            foreach (var kv in pendingOutputs)
            {
                var tag = kv.Key;
                var p = kv.Value;
                if (!tag.IsValid || p.mass <= 0f) continue;

                float avgTemp = p.tempMass > 0f ? (p.tempMass / p.mass) : fallbackTemp;
                buffer.Add(new FueledOutput(tag, p.mass, avgTemp, p.diseaseIdx, p.diseaseCount));
            }

            pendingOutputs.Clear();
            return buffer.Count - beforeCount;
        }

        public void Configure(FueledDiet diet)
        {
            fueledDiet = diet ?? throw new ArgumentNullException(nameof(diet));
            EnsureStorage();

            storageCapacityOverride = fueledDiet.TotalFuelCapacityKg;
            if (storageCapacityOverride > 0f)
                storage.capacityKg = storageCapacityOverride;

         
            SetupElementConsumers(diet);

            if (EnableDebugLogs)
                Debug.Log($"[FueledDiet] {name}: configured; capacity={storage?.capacityKg:F1}kg; conversions={fueledDiet.Conversions.Count}");
        }

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            EnsureStorage();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            EnsureStorage();

            if (storage != null)
            {
                storage.showInUI = true;
                storage.showDescriptor = true;
            }

            if (fueledDiet == null && prefabID != null && FueledDietRegistry.TryGet(prefabID.PrefabTag, out var regDiet) && regDiet != null)
            {
                fueledDiet = regDiet;
                storageCapacityOverride = fueledDiet.TotalFuelCapacityKg;
                if (storageCapacityOverride > 0f && storage != null)
                    storage.capacityKg = storageCapacityOverride;
            }

       
            EnableInstanceConsumers();

            if (EnableDebugLogs)
                Debug.Log($"[FueledDiet] {name}: OnSpawn complete");
        }

        // Total pending mass (for diagnostics/testing)
        public float TotalPendingMass
        {
            get
            {
                float sum = 0f;
                foreach (var kv in pendingOutputs) sum += kv.Value.mass;
                return sum;
            }
        }

        private void EnsureStorage()
        {
            if (storage == null)
                storage = gameObject.AddComponent<Storage>();

            storage.showInUI = true;
            storage.showDescriptor = true;
            storage.allowItemRemoval = false;
            storage.storageFilters = null;
        }

      

        private void SetupElementConsumers(FueledDiet diet)
        {
            foreach (var c in consumers)
                if (c != null) Util.KDestroyGameObject(c);
            consumers.Clear();

            foreach (var fi in diet.FuelInputs)
            {
                if (!fi.IsGas && !fi.IsLiquid)
                    continue;

                if (!TryResolveSimHash(fi.ElementTag, out var simHash))
                    continue;

                var elem = ElementLoader.FindElementByHash(simHash);
                if (elem != null && elem.IsSolid)
                    continue;

                var ec = gameObject.AddComponent<ElementConsumer>();
                ec.configuration = ElementConsumer.Configuration.Element;
                ec.elementToConsume = simHash;
                ec.consumptionRate = Mathf.Max(0f, fi.ConsumptionRateKgPerSecond);
                ec.consumptionRadius = fi.ConsumptionRadius > 0 ? fi.ConsumptionRadius : (byte)2;
                ec.minimumMass = 0f;

                float consumerCap = fi.CapacityKg > 0f ? fi.CapacityKg : (storageCapacityOverride > 0f ? storageCapacityOverride : 0f);
                ec.capacityKG = consumerCap;
                if (storage != null && storage.capacityKg > 0f)
                    ec.capacityKG = Mathf.Min(ec.capacityKG, storage.capacityKg);

                ec.storeOnConsume = true;
                ec.showInStatusPanel = true;
                ec.showDescriptor = true;
                ec.isRequired = false;
                ec.ignoreActiveChanged = true;
                ec.sampleCellOffset = Vector3.zero;

                ec.EnableConsumption(true);
                consumers.Add(ec);
            }
        }

        private void EnableInstanceConsumers()
        {
            if (consumers.Count == 0)
            {
                var ecs = GetComponents<ElementConsumer>();
                if (ecs != null && ecs.Length > 0)
                    consumers.AddRange(ecs.Where(ec =>
                    {
                        if (ec == null) return false;
                        var elem = ElementLoader.FindElementByHash(ec.elementToConsume);
                        return elem != null && !elem.IsSolid;
                    }));
            }

            foreach (var ec in consumers)
            {
                if (ec == null) continue;

                ec.ignoreActiveChanged = true;
                ec.storeOnConsume = true;
                if (ec.consumptionRadius == 0) ec.consumptionRadius = 2;
                if (ec.capacityKG <= 0f && storageCapacityOverride > 0f)
                    ec.capacityKG = storageCapacityOverride;
                if (storage != null && storage.capacityKg > 0f)
                    ec.capacityKG = Mathf.Min(ec.capacityKG, storage.capacityKg);

                ec.EnableConsumption(true);
            }
        }

        private static bool TryResolveSimHash(Tag tag, out SimHashes hash)
        {
            foreach (var elem in ElementLoader.elements)
            {
                if (elem != null && elem.tag == tag)
                {
                    hash = elem.id;
                    return true;
                }
            }
            hash = SimHashes.Vacuum;
            return false;
        }

        internal void NudgeConsumers()
        {
            if (consumers.Count == 0)
            {
                var ecs = GetComponents<ElementConsumer>();
                if (ecs != null && ecs.Length > 0)
                    consumers.AddRange(ecs.Where(ec =>
                    {
                        if (ec == null) return false;
                        var elem = ElementLoader.FindElementByHash(ec.elementToConsume);
                        return elem != null && !elem.IsSolid;
                    }));
            }

            foreach (var ec in consumers)
            {
                if (ec == null) continue;
                ec.EnableConsumption(false);
                ec.EnableConsumption(true);
            }
        }

    
        // Preferred API: caller supplies the specific main food tag eaten.
        public void TryApplyFuelConversion(float consumedMainMassKg, Diet.Info usedDietInfo, Tag mainFoodTag, bool poopWasProduced)
        {
            if (fueledDiet == null || storage == null)
                return;

            if (consumedMainMassKg <= 0f)
                return;

            bool freedAnyFuel = false;

            if (fueledDiet.AllowBlendedInputByOutput)
            {
                foreach (var group in fueledDiet.Conversions.GroupBy(c => c.OutputTag))
                {
                    var first = group.FirstOrDefault();
                    if (first == null)
                        continue;

                    float baseKgPerKgMain = Mathf.Max(0f, first.KgInputPerKgMain);
                    float effectiveKgPerKgMain = fueledDiet.ResolveKgInputPerKgMain(first.OutputTag, mainFoodTag, baseKgPerKgMain);
                    float requiredInputKg = Mathf.Max(0f, consumedMainMassKg * effectiveKgPerKgMain);
                    if (requiredInputKg <= 0f)
                        continue;

                    var available = new Dictionary<Tag, float>();
                    foreach (var conv in group)
                    {
                        if (!available.ContainsKey(conv.InputTag))
                            available[conv.InputTag] = GetAvailableInStorage(conv.InputTag);
                    }

                    if (EnableDebugLogs)
                        Debug.Log($"[FueledDiet] {name}: main={mainFoodTag} consumed={consumedMainMassKg:F3} kg -> need {requiredInputKg:F3} kg for {first.OutputTag}");

                    if (!fueledDiet.TryBuildInputPlanForOutput(first.OutputTag, requiredInputKg, available, out var plan))
                    {
                        if (EnableDebugLogs)
                            Debug.Log($"[FueledDiet] {name}: no plan for output {first.OutputTag} (available may be insufficient).");
                        continue;
                    }

                    foreach (var kv in plan)
                    {
                        var inputTag = kv.Key;
                        var toRequest = kv.Value;
                        if (toRequest <= 0f) continue;

                        var conv = group.FirstOrDefault(c => c.InputTag == inputTag);
                        if (conv == null) continue;

                        float takenKg = ConsumeFromStorage(inputTag, toRequest, out float sourceTempK, out byte diseaseIdx, out int diseaseCount);
                        if (EnableDebugLogs)
                            Debug.Log($"[FueledDiet] {name}: take {takenKg:F3}/{toRequest:F3} kg {inputTag} for {conv.OutputTag}");

                        if (takenKg <= 0f) continue;
                        freedAnyFuel = true;

                        float outputKg = takenKg * Mathf.Max(0f, conv.KgOutputPerKgInput);
                        if (outputKg > 0f)
                        {
                            float tempK = conv.OutputTemperatureOverrideKelvin > 0f
                                ? conv.OutputTemperatureOverrideKelvin
                                : (sourceTempK > 0f ? sourceTempK : (primaryElement != null ? primaryElement.Temperature : 300f));

                            AccumulatePending(conv.OutputTag, outputKg, tempK, diseaseIdx, diseaseCount);
                            if (EnableDebugLogs)
                                Debug.Log($"[FueledDiet] {name}: pending[{conv.OutputTag}] += {outputKg:F3} kg (now {pendingOutputs[conv.OutputTag].mass:F3} kg)");
                        }
                    }
                }

                if (freedAnyFuel)
                {
                    NudgeConsumers();
                    SolidFuelStates.PrioritizeUpdateBrain(gameObject);
                }
                return;
            }

            foreach (var conv in fueledDiet.Conversions)
            {
                float effectiveRate = fueledDiet.ResolveKgInputPerKgMain(conv.OutputTag, mainFoodTag, conv.KgInputPerKgMain);
                float requestKg = Mathf.Max(0f, effectiveRate * consumedMainMassKg);
                if (requestKg <= 0f)
                    continue;

                float takenKg = ConsumeFromStorage(conv.InputTag, requestKg, out float sourceTempK, out byte diseaseIdx, out int diseaseCount);
                if (EnableDebugLogs)
                    Debug.Log($"[FueledDiet] {name}: take {takenKg:F3}/{requestKg:F3} kg {conv.InputTag} for {conv.OutputTag}");

                if (takenKg <= 0f)
                    continue;

                freedAnyFuel = true;

                float outputKg = takenKg * Mathf.Max(0f, conv.KgOutputPerKgInput);
                if (outputKg <= 0f)
                    continue;

                float tempK2 = conv.OutputTemperatureOverrideKelvin > 0f
                    ? conv.OutputTemperatureOverrideKelvin
                    : (sourceTempK > 0f ? sourceTempK : (primaryElement != null ? primaryElement.Temperature : 300f));

                AccumulatePending(conv.OutputTag, outputKg, tempK2, diseaseIdx, diseaseCount);
                if (EnableDebugLogs)
                    Debug.Log($"[FueledDiet] {name}: pending[{conv.OutputTag}] += {outputKg:F3} kg (now {pendingOutputs[conv.OutputTag].mass:F3} kg)");
            }

            if (freedAnyFuel)
            {
                NudgeConsumers();
                SolidFuelStates.PrioritizeUpdateBrain(gameObject);
            }
        }

        // Public API for binders/monitors to report main-diet consumption by mass (kg).
        public void CreditMainDietKg(float consumedMainMassKg)
        {
            if (EnableDebugLogs)
                Debug.Log($"[FueledDiet] {name}: CreditMainDietKg {consumedMainMassKg:F3} kg (no tag)");
            if (consumedMainMassKg <= 0f) return;
            TryApplyFuelConversion(consumedMainMassKg, usedDietInfo: null, mainFoodTag: Tag.Invalid, poopWasProduced: false);
        }

        // Public API with main-food tag specified (e.g., CarbonDioxide for slicksters).
        public void CreditMainDietKg(float consumedMainMassKg, Tag mainFoodTag)
        {
            if (EnableDebugLogs)
                Debug.Log($"[FueledDiet] {name}: CreditMainDietKg {consumedMainMassKg:F3} kg tag={mainFoodTag}");
            if (consumedMainMassKg <= 0f) return;
            TryApplyFuelConversion(consumedMainMassKg, usedDietInfo: null, mainFoodTag: mainFoodTag, poopWasProduced: false);
        }

        private float GetAvailableInStorage(Tag inputTag)
        {
            if (storage == null) return 0f;
            try
            {
                return storage.GetAmountAvailable(inputTag);
            }
            catch
            {
                float sum = 0f;
                if (storage.items != null)
                {
                    for (int i = 0; i < storage.items.Count; i++)
                    {
                        var go = storage.items[i];
                        if (go == null || !go.HasTag(inputTag)) continue;
                        var pe = go.GetComponent<PrimaryElement>();
                        if (pe != null) sum += pe.Mass;
                    }
                }
                return sum;
            }
        }

        private void AccumulatePending(Tag outputTag, float massKg, float tempK, byte diseaseIdx, int diseaseCount)
        {
            if (massKg <= 0f || outputTag == Tag.Invalid)
                return;

            if (!pendingOutputs.TryGetValue(outputTag, out var p))
            {
                p = new PendingEmission
                {
                    mass = 0f,
                    tempMass = 0f,
                    diseaseIdx = diseaseIdx,
                    diseaseCount = 0
                };
            }

            // Merge disease sensibly
            if (p.diseaseIdx == byte.MaxValue || p.diseaseIdx == diseaseIdx)
            {
                p.diseaseIdx = diseaseIdx;
                p.diseaseCount += diseaseCount;
            }
            else if (diseaseCount > p.diseaseCount)
            {
                p.diseaseIdx = diseaseIdx;
                p.diseaseCount = diseaseCount;
            }

            p.mass += massKg;
            p.tempMass += massKg * tempK;

            pendingOutputs[outputTag] = p;
        }

        private float ConsumeFromStorage(Tag inputTag, float requestedKg, out float avgTempK, out byte diseaseIdx, out int diseaseCount)
        {
            avgTempK = 0f;
            diseaseIdx = byte.MaxValue;
            diseaseCount = 0;

            if (requestedKg <= 0f || storage == null)
                return 0f;

            float remaining = requestedKg;
            float weightedTempSum = 0f;
            float takenTotal = 0f;

            // Drain physical items (solid chunks) from storage
            var items = ListPool<GameObject, FueledDietController>.Allocate();
            if (storage.items != null)
                items.AddRange(storage.items);

            for (int i = 0; i < items.Count && remaining > 1e-6f; i++)
            {
                var go = items[i];
                if (go == null) continue;
                if (!go.HasTag(inputTag)) continue;

                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= 0f) continue;

                float beforeMass = pe.Mass;
                float take = Mathf.Min(beforeMass, remaining);
                if (take <= 0f) continue;

                pe.Mass -= take;

                weightedTempSum += take * pe.Temperature;
                takenTotal += take;
                remaining -= take;

                if (pe.DiseaseIdx != byte.MaxValue && pe.DiseaseCount > 0)
                {
                    var fraction = take / beforeMass;
                    int takenDisease = Mathf.RoundToInt(pe.DiseaseCount * fraction);

                    if (diseaseIdx == byte.MaxValue || diseaseIdx == pe.DiseaseIdx)
                    {
                        diseaseIdx = pe.DiseaseIdx;
                        diseaseCount += takenDisease;
                    }
                    else if (takenDisease > diseaseCount)
                    {
                        diseaseIdx = pe.DiseaseIdx;
                        diseaseCount = takenDisease;
                    }
                }

                if (pe.Mass <= 1e-6f)
                {
                    storage.Drop(go, true);
                    Util.KDestroyGameObject(go);
                }
            }
            items.Recycle();

            // Drain the "virtual" mass for NON-solid fuels (gas/liquid consumed by ElementConsumers)
            if (remaining > 1e-6f)
            {
                bool isSolidElement = false;
                if (TryResolveSimHash(inputTag, out var hash))
                {
                    var elem = ElementLoader.FindElementByHash(hash);
                    isSolidElement = elem != null && elem.IsSolid;
                }

                if (!isSolidElement)
                {
                    float taken2;
                    Klei.SimUtil.DiseaseInfo di2;
                    float temp2;

                    storage.ConsumeAndGetDisease(inputTag, remaining, out taken2, out di2, out temp2);

                    if (taken2 > 0f)
                    {
                        weightedTempSum += taken2 * temp2;
                        takenTotal += taken2;
                        remaining -= taken2;

                        if (di2.idx != byte.MaxValue && di2.count > 0)
                        {
                            if (diseaseIdx == byte.MaxValue || diseaseIdx == di2.idx)
                            {
                                diseaseIdx = di2.idx;
                                diseaseCount += di2.count;
                            }
                            else if (di2.count > diseaseCount)
                            {
                                diseaseIdx = di2.idx;
                                diseaseCount = di2.count;
                            }
                        }
                    }
                }
            }

            if (takenTotal > 0f)
                avgTempK = weightedTempSum / takenTotal;

            return takenTotal;
        }
    }
}