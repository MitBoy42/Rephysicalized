using HarmonyLib;
using UnityEngine;

namespace CO2ScrubberRework
{
    internal static class ScrubberLog
    {
        public const bool Enabled = false;
        private const string Pfx = "[CO2ScrubberRework] ";

        public static void Info(string msg)
        {
            if (Enabled) Debug.Log(Pfx + msg);
        }

        public static void Warn(string msg)
        {
            if (Enabled) Debug.LogWarning(Pfx + msg);
        }

        public static void Error(string msg)
        {
            if (Enabled) Debug.LogError(Pfx + msg);
        }
    }

    // Helper component to hold references to special storages
    public sealed class ScrubberStorageRefs : KMonoBehaviour
    {
        [MyCmpGet] public Storage primaryStorage; // existing building storage (typically water input + PW in vanilla)
        public Storage co2MainStorage;            // 60 kg storage (CO2 only, visible, connected to gas port)
        public Storage co2IntakeStorage;          // Hidden CO2 intake storage, bound to PEC (pre-conversion)
        public Storage pwBufferStorage;           // Dirty Water buffer (hidden, attached to liquid dispenser)
    }

    internal static class StorageUiHider
    {
        // Hide storage from all UI/descriptor/status panel displays.
        public static void HideStorageFromUI(Storage s)
        {
            if (s == null) return;
            s.showInUI = false;
            s.showDescriptor = false;
            s.allowItemRemoval = false;

            // Some ONI versions display a status item if this flag is true. Turn it off via reflection if present.
            try
            {
                var f = AccessTools.Field(typeof(Storage), "showCapacityStatusItem");
                if (f != null) f.SetValue(s, false);
            }
            catch { /* ignore if field missing */ }

            // Some versions use a property; try as fallback.
            try
            {
                var p = AccessTools.Property(typeof(Storage), "showCapacityStatusItem");
                if (p != null && p.CanWrite) p.SetValue(s, false, null);
            }
            catch { /* ignore */ }
        }

        public static float RemainingCapacityForTag(Storage s, Tag tag)
        {
            if (s == null) return 0f;
            float used = s.GetAmountAvailable(tag);
            return Mathf.Max(0f, s.capacityKg - used);
        }
    }

    // Secondary gas port descriptor for this building
    public static class CO2ScrubberSecondaryPorts
    {
        // Secondary gas output at offset (1, 0)
        public static readonly ConduitPortInfo CO2_GAS_SECONDARY_OUTPUT = new ConduitPortInfo(ConduitType.Gas, new CellOffset(1, 0));
    }

    // Patch building after prefab configuration
    [HarmonyPatch(typeof(CO2ScrubberConfig), nameof(CO2ScrubberConfig.DoPostConfigureComplete))]
    public static class CO2Scrubber_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            var refs = go.AddOrGet<ScrubberStorageRefs>();

            // Passive CO2 consumer setup
            var pec = go.AddOrGet<PassiveElementConsumer>();
            pec.elementToConsume = SimHashes.CarbonDioxide;
            pec.consumptionRate = 0.6f; // kg/s
            pec.capacityKG = 2f;       // increased storage capacity to 60 kg as per spec
            pec.consumptionRadius = 3;
            pec.showInStatusPanel = false; // hide PEC line in status panel
            pec.showDescriptor = false;    // avoid "0 kg/s" idle descriptor
            pec.isRequired = true;
            pec.storeOnConsume = true;
            pec.ignoreActiveChanged = false;

            // Hidden CO2 intake storage (PEC writes here; controller siphons it)
            var co2Intake = go.AddComponent<Storage>();
            co2Intake.capacityKg = 2f; // match PEC cap; this is pre-conversion buffer
            co2Intake.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            co2Intake.onlyTransferFromLowerPriority = false;
            co2Intake.storageFilters = new System.Collections.Generic.List<Tag>
            {
                SimHashes.CarbonDioxide.CreateTag()
            };
            StorageUiHider.HideStorageFromUI(co2Intake);

            // Main CO2 storage (60 kg) - visible (piped out via gas secondary port), receives CO2 only after conversion
            var co2Store = go.AddComponent<Storage>();
            co2Store.capacityKg = 240f;
            co2Store.showInUI = true;
            co2Store.showDescriptor = true;
            co2Store.allowItemRemoval = false;
            co2Store.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            co2Store.onlyTransferFromLowerPriority = false;
            co2Store.storageFilters = new System.Collections.Generic.List<Tag>
            {
                SimHashes.CarbonDioxide.CreateTag()
            };

            // Dirty Water buffer storage - fully hidden (buffer)
            var pwBuffer = go.AddComponent<Storage>();
            pwBuffer.capacityKg = 20f;        // enough to hold a few seconds of output
            pwBuffer.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            pwBuffer.onlyTransferFromLowerPriority = false;
            pwBuffer.storageFilters = new System.Collections.Generic.List<Tag>
            {
                SimHashes.DirtyWater.CreateTag()
            };
            StorageUiHider.HideStorageFromUI(pwBuffer);

            refs.co2IntakeStorage = co2Intake;
            refs.co2MainStorage = co2Store;
            refs.pwBufferStorage = pwBuffer;

            // Keep the ElementConverter (for general descriptors), but we will disable it at runtime.
            var converter = go.AddOrGet<ElementConverter>();
            var waterTag = SimHashes.Water.CreateTag();
            converter.consumedElements = new ElementConverter.ConsumedElement[]
            {
                new ElementConverter.ConsumedElement(waterTag, 1.0f, isActive: true)
            };
            converter.outputElements = new ElementConverter.OutputElement[]
            {
                new ElementConverter.OutputElement(1.01f, SimHashes.DirtyWater, 0.0f, useEntityTemperature: false, storeOutput: true)
            };

            // Secondary gas output for CO2 at (1, 0)
            var secOut = go.AddOrGet<ConduitSecondaryOutput>();
            secOut.portInfo = CO2ScrubberSecondaryPorts.CO2_GAS_SECONDARY_OUTPUT;

            // Separate gas dispenser for the secondary output (dispense only CO2 from MAIN storage)
            var co2GasDispenser = go.AddComponent<ConduitDispenser>();
            co2GasDispenser.conduitType = ConduitType.Gas;
            co2GasDispenser.storage = co2Store; // dispense CO2 from the MAIN storage only
          //  co2GasDispenser.elementFilter = new[] { SimHashes.CarbonDioxide.CreateTag() };
            co2GasDispenser.invertElementFilter = false;
            co2GasDispenser.useSecondaryOutput = true; // route to the secondary gas port
            co2GasDispenser.alwaysDispense = true;

            // Ensure the secondary gas output is registered on the gas network
            go.AddOrGet<CO2Scrubber_CO2SecondaryGasOutputInitializer>();

            // Controller that handles the actual logic
            go.AddOrGet<CO2ScrubberReworkController>();
        }
    }

    // Manual controller:
    // - PEC writes into hidden intake storage (never main).
    // - Internal CO2 buffer (float) capped at BufferCapacity; nothing shows in Building Contents.
    // - We siphon from hidden intake storage each tick: destroy 1%, add net to internal buffer, remove from intake.
    // - Consumes Water and produces Polluted Water into hidden PW buffer storage.
    // - Moves CO2 from internal buffer to MAIN CO2 storage at 0.6 kg per 1 kg water processed.
    // - Gas secondary port dispenses only from MAIN storage, so no bypass is possible.
    // - Stops when there is no room in MAIN CO2 storage.
    // - Uses ISim1000ms to run once per simulated second.
    public sealed class CO2ScrubberReworkController : KMonoBehaviour, ISim1000ms
    {
        private const float Co2PerWater = 0.6f;            // 0.6 kg CO2 per 1.0 kg Water (gross CO2 intake basis)
        private const float PwPerWater = 1.01f;            // PW per Water
        private const float Co2DestroyFrac = 0.01f;        // destroy 1% upon intake
        private const float DefaultTemp = 293.15f;         // 20C default temp for added elements

        // Internal buffer capacity and hysteresis to avoid rapid on/off
        private const float BufferCapacity = 2.0f;         // 2.0 kg internal buffer cap
        private const float StartBufferThreshold = 0.05f;  // start converting if buffer >= 50 g
        private const float StopBufferThreshold = 0.01f;   // stop converting if buffer < 10 g

        // Components and storages
        private PassiveElementConsumer pec;
        private ElementConverter converter;   // kept only for UI, disabled for logic
        private Storage waterStorage;         // ConduitConsumer's storage (water input only)
        private Storage co2IntakeStorage;     // Hidden intake storage (PEC destination)
        private Storage co2Storage;           // MAIN CO2 storage (visible, piped via secondary gas)
        private Storage pwBufferStorage;      // PW buffer for dispenser (hidden)
        private ConduitDispenser liquidDispenser; // LIQUID dispenser for PW
        private ConduitDispenser gasDispenser;    // GAS dispenser (secondary port)
        private Operational operational;

        private ScrubberStorageRefs refs;

        // Tags
        private Tag co2Tag;
        private Tag waterTag;
        private Tag pwTag;

        // Internal buffer and tracking
        private float co2Buffer;              // internal CO2 buffer (kg), not a Storage (net after 1% loss)
        private float prevIntakeCo2;          // last known hidden intake CO2 amount (kg)
        private float grossCo2Pending;        // gross CO2 consumed by PEC but not yet matched by water processing (kg)
        private bool lastActive;

        // Logging throttling
        private float logTimer;
        private const float LogInterval = 2.0f;

        public override void OnSpawn()
        {
            base.OnSpawn();

            co2Tag = SimHashes.CarbonDioxide.CreateTag();
            waterTag = SimHashes.Water.CreateTag();
            pwTag = SimHashes.DirtyWater.CreateTag();

            pec = GetComponent<PassiveElementConsumer>();
            converter = GetComponent<ElementConverter>();
            operational = GetComponent<Operational>();
            refs = GetComponent<ScrubberStorageRefs>();

            if (refs == null || pec == null)
            {
                ScrubberLog.Error("Missing required components on spawn. refs=" + (refs != null) + " pec=" + (pec != null));
                return;
            }

            co2IntakeStorage = refs.co2IntakeStorage;
            co2Storage = refs.co2MainStorage;
            pwBufferStorage = refs.pwBufferStorage;

            // Resolve the storage used by ConduitConsumer (water input only)
            waterStorage = FindConsumerStorage();
            if (waterStorage == null)
                waterStorage = GetComponent<Storage>();

            // Ensure PEC writes into the HIDDEN INTAKE storage (never main)
            try
            {
                var fld = AccessTools.Field(typeof(ElementConsumer), "storage");
                if (fld != null && co2IntakeStorage != null)
                    fld.SetValue(pec, co2IntakeStorage);
            }
            catch { /* ignore if field not found */ }

            // Discover dispensers and bind the LIQUID one to PW buffer; GAS one already uses MAIN CO2 storage
            var dispensers = GetComponents<ConduitDispenser>();
            foreach (var d in dispensers)
            {
                if (d == null) continue;
                if (d.conduitType == ConduitType.Liquid)
                    liquidDispenser = d;
                else if (d.conduitType == ConduitType.Gas)
                    gasDispenser = d;
            }
            RebindDispenserToStorage(liquidDispenser, pwBufferStorage);

            // Ensure PEC configuration
            pec.elementToConsume = SimHashes.CarbonDioxide;
            pec.consumptionRate = 0.6f;
            pec.capacityKG = 240f;
            pec.isRequired = true;
            pec.storeOnConsume = true;
            pec.ignoreActiveChanged = false;
            pec.showInStatusPanel = false;
            pec.showDescriptor = false;

            // Disable ElementConverter so it doesn't interfere (UI will still show its static descriptors)
            if (converter != null)
            {
                try
                {
                    converter.enabled = false;
                    converter.SetAllConsumedActive(false);
                }
                catch { converter.enabled = false; }
            }

            // Initialize tracking
            co2Buffer = 0f;
            grossCo2Pending = 0f;
            prevIntakeCo2 = co2IntakeStorage != null ? co2IntakeStorage.GetAmountAvailable(co2Tag) : 0f;

            // Start in standby
            SetActiveState(false);

            ScrubberLog.Info($"OnSpawn: waterStorage={(waterStorage ? waterStorage.name : "null")}, co2Intake={(co2IntakeStorage ? co2IntakeStorage.name : "null")}, co2Main={(co2Storage ? co2Storage.name : "null")}, pwBuffer={(pwBufferStorage ? pwBufferStorage.name : "null")}, hasLiquidDispenser={(liquidDispenser != null)}, hasGasDispenser={(gasDispenser != null)}");
        }

        // Run once per simulated second
        public void Sim1000ms(float dt)
        {
            if (pec == null || co2IntakeStorage == null || co2Storage == null || waterStorage == null || pwBufferStorage == null || (operational != null && !operational.IsOperational))
            {
                SafeEnableConsumption(pec, false);
                SetActiveState(false);
                return;
            }

            // 0) Gate PEC by internal buffer headroom (gross intake is limited by net buffer space)
            bool bufferHasRoom = co2Buffer <= (BufferCapacity - 0.001f);
            SafeEnableConsumption(pec, bufferHasRoom);

            // 1) Siphon newly stored CO2 from HIDDEN INTAKE storage into internal buffer with 1% loss
            float intakeCo2 = co2IntakeStorage.GetAmountAvailable(co2Tag);
            float deltaFromPEC = intakeCo2 - prevIntakeCo2; // positive if PEC added since last tick
            if (deltaFromPEC > 0.00001f && bufferHasRoom)
            {
                // Siphon limited by buffer net room; gross pending tracks before loss
                float netRoom = BufferCapacity - co2Buffer;
                // toSiphon is gross mass we will remove from intake storage
                float toSiphon = Mathf.Min(deltaFromPEC, Mathf.Max(0f, netRoom / (1f - Co2DestroyFrac)));
                if (toSiphon > 0f)
                {
                    float loss = toSiphon * Co2DestroyFrac;
                    float toBuffer = Mathf.Max(0f, toSiphon - loss);

                    // Remove gross from intake, add net to internal buffer
                    co2IntakeStorage.ConsumeIgnoringDisease(co2Tag, toSiphon);
                    intakeCo2 -= toSiphon;
                    co2Buffer += toBuffer;

                    // Track gross intake to enforce exact water ratio (kept from your version)
                    grossCo2Pending += toSiphon;

                    ScrubberLog.Info($"Siphon: intake->buffer: gross={toSiphon:F3}kg, destroyed={loss:F3}kg, buffered+={toBuffer:F3}kg, bufferNow={co2Buffer:F3}kg, grossPending={grossCo2Pending:F3}kg");
                }
            }
            prevIntakeCo2 = intakeCo2;

            // 2) Manual conversion: compute how much we can process this tick
            // Hysteresis on internal buffer (net)
            bool bufferSufficient = co2Buffer >= StartBufferThreshold || (lastActive && co2Buffer > StopBufferThreshold);

            // Water caps
            float waterAvailable = waterStorage.GetAmountAvailable(waterTag);
            float maxWaterRateBySupply = dt > 0f ? (waterAvailable / dt) : 0f;

            // Buffer cap (net) converted to water equivalent
            float maxWaterRateByBuffer = dt > 0f ? (co2Buffer / Co2PerWater / dt) : 0f;

            // Gross cap ensures exact 1 kg water per 0.6 kg gross CO2 consumed (pre-loss)
            float maxWaterRateByGross = dt > 0f ? (grossCo2Pending / Co2PerWater / dt) : 0f;

            // CO2 MAIN storage room cap: stop if we cannot insert more CO2
            float room = StorageUiHider.RemainingCapacityForTag(co2Storage, co2Tag);
            float maxWaterRateByRoom = dt > 0f ? (room / Co2PerWater / dt) : 0f;

            // Respect 1 kg/s water cap and all constraints
            float desiredWaterRate = Mathf.Min(1.0f, maxWaterRateBySupply, maxWaterRateByBuffer, maxWaterRateByGross, maxWaterRateByRoom);
            bool shouldRun = bufferSufficient && desiredWaterRate > 0.0001f;

            float waterConsumed = 0f;
            float pwProduced = 0f;
            float co2Moved = 0f;

            if (shouldRun)
            {
                float waterToConsume = desiredWaterRate * dt;
                // Clamp again to absolute avail to avoid rounding issues
                waterToConsume = Mathf.Min(waterToConsume, waterStorage.GetAmountAvailable(waterTag));

                if (waterToConsume > 0f)
                {
                    // Consume water
                    waterStorage.ConsumeIgnoringDisease(waterTag, waterToConsume);
                    waterConsumed = waterToConsume;

                    // Produce polluted water into the hidden PW buffer
                    float pwToAdd = waterConsumed * PwPerWater;
                    pwBufferStorage.AddElement(SimHashes.DirtyWater, pwToAdd, DefaultTemp, byte.MaxValue, 0);
                    pwProduced = pwToAdd;

                    // Move CO2 from internal buffer to MAIN storage
                    float co2Needed = waterConsumed * Co2PerWater;
                    float co2Pull = Mathf.Min(co2Needed, co2Buffer, StorageUiHider.RemainingCapacityForTag(co2Storage, co2Tag));
                    if (co2Pull > 0f)
                    {
                        co2Buffer -= co2Pull;
                        co2Storage.AddElement(SimHashes.CarbonDioxide, co2Pull, DefaultTemp, byte.MaxValue, 0);
                        co2Moved = co2Pull;

                        // Deduct gross pending matched by this water (exact 1:0.6 on gross)
                        grossCo2Pending = Mathf.Max(0f, grossCo2Pending - co2Needed);
                    }

                    ScrubberLog.Info($"Convert: water={waterConsumed:F3}kg, pw={pwProduced:F3}kg, co2Moved={co2Moved:F3}kg; bufferCO2={co2Buffer:F3}kg, co2Main={co2Storage.GetAmountAvailable(co2Tag):F3}kg, grossPending={grossCo2Pending:F3}kg");
                }
            }

            // Power/standby
            SetActiveState(shouldRun && waterConsumed > 0f);

            // Periodic status log
            logTimer += dt;
            if (logTimer >= LogInterval)
            {
                logTimer = 0f;
                ScrubberLog.Info($"Status: active={lastActive}, bufferCO2={co2Buffer:F3} kg, waterAvail={waterStorage.GetAmountAvailable(waterTag):F3} kg, PWbuf={pwBufferStorage.GetAmountAvailable(pwTag):F3} kg, roomMainCO2={StorageUiHider.RemainingCapacityForTag(co2Storage, co2Tag):F3} kg");
            }
        }

        private void SetActiveState(bool active)
        {
            if (lastActive == active) return;
            lastActive = active;

            if (operational != null)
                operational.SetActive(active);

            ScrubberLog.Info("Operational.SetActive(" + active + ")");
        }

        private static void SafeEnableConsumption(PassiveElementConsumer pec, bool enable)
        {
            if (pec == null) return;
            try { pec.EnableConsumption(enable); }
            catch { pec.enabled = enable; }
        }

        // Find the storage actually used by the pipe consumer (water input)
        private Storage FindConsumerStorage()
        {
            var consumer = GetComponent<ConduitConsumer>();
            if (consumer != null)
            {
                try
                {
                    var fld = AccessTools.Field(typeof(ConduitConsumer), "storage");
                    var s = fld != null ? fld.GetValue(consumer) as Storage : null;
                    if (s != null)
                    {
                        ScrubberLog.Info("Water storage resolved from ConduitConsumer -> " + s.name);
                        return s;
                    }
                }
                catch (System.Exception ex)
                {
                    ScrubberLog.Warn("Failed to read ConduitConsumer.storage: " + ex.Message);
                }
            }

            // Final fallback: first Storage on the object
            var first = GetComponent<Storage>();
            ScrubberLog.Warn("Water storage fallback to first Storage -> " + (first ? first.name : "null"));
            return first;
        }

        // Rebind a specific ConduitDispenser's storage (used for LIQUID dispenser -> PW buffer)
        private static void RebindDispenserToStorage(ConduitDispenser dispenser, Storage target)
        {
            if (dispenser == null || target == null) return;
            try
            {
                var fld = AccessTools.Field(typeof(ConduitDispenser), "storage");
                if (fld != null)
                {
                    fld.SetValue(dispenser, target);
                    ScrubberLog.Info($"Bound {dispenser.conduitType} ConduitDispenser.storage -> {target.name}");
                }
                else
                {
                    // Some versions have public field; assign directly as fallback
                    dispenser.storage = target;
                    ScrubberLog.Info($"Bound (direct) {dispenser.conduitType} ConduitDispenser.storage -> {target.name}");
                }
            }
            catch (System.Exception ex)
            {
                ScrubberLog.Error("Failed to bind ConduitDispenser.storage: " + ex);
            }
        }
    }

    // Registers the secondary gas output port as a network source at the correct cell
    public sealed class CO2Scrubber_CO2SecondaryGasOutputInitializer : KMonoBehaviour
    {
        private int gasOutputCell = -1;

        public override void OnSpawn()
        {
            base.OnSpawn();
            var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
            gasOutputCell = Grid.OffsetCell(Grid.PosToCell(gameObject), CO2ScrubberSecondaryPorts.CO2_GAS_SECONDARY_OUTPUT.offset);
            var gasNetworkItem = new FlowUtilityNetwork.NetworkItem(ConduitType.Gas, Endpoint.Source, gasOutputCell, gameObject);
            gasNetworkManager.AddToNetworks(gasOutputCell, gasNetworkItem, true);
            ScrubberLog.Info("Registered secondary gas output at cell " + gasOutputCell);
        }

        public override void OnCleanUp()
        {
            if (gasOutputCell != -1)
            {
                var gasNetworkManager = Conduit.GetNetworkManager(ConduitType.Gas);
                if (gasNetworkManager != null)
                    gasNetworkManager.RemoveFromNetworks(gasOutputCell, gameObject, true);
            }
            base.OnCleanUp();
        }
    }
}