using HarmonyLib;

using System.Collections.Generic;

using System.Runtime.CompilerServices;
using TUNING;
using UnityEngine;



namespace Rephysicalized
{

    [HarmonyPatch(typeof(AlgaeHabitatConfig), nameof(AlgaeHabitatConfig.CreateBuildingDef))]
    public static class AlgaeHabitatConfig_CreateBuildingDef_Patch
    {
        public static void Postfix(ref BuildingDef __result)
        {
            // Modify the construction recipe to include algae as the second material
            string[] modifiedMaterials = new string[]
            {
            "Farmable", // First material: Default farmable material
            "Algae"            // Second material: Algae
            };

            float[] modifiedMass = new float[]
            {
            BUILDINGS.CONSTRUCTION_MASS_KG.TIER2[0], // First material mass
            BUILDINGS.CONSTRUCTION_MASS_KG.TIER4[0]  // Second material mass (adjust as necessary)
            };

            // Apply new materials and masses to the BuildingDef
            __result.MaterialCategory = modifiedMaterials;
            __result.Mass = modifiedMass;
        }
    }


    public static class AlgaeHabitatTuning
    {
        // Default values copied from your original config
        public static float CO2 = 0.001f;
        public static float WATER_RATE = 0.3f;
        public static float OXYGEN_RATE = 0.04f;
        public static float CO2_RATE = 0.001f;
        public static float CO2_CAPACITY = 3f;
        public static float WATER_CAPACITY = 360f;
        public static float PWATER_OUTPUT_RATE = 0.260f;
        public static float ALGAE_CAPACITY = 120f;
        public static float ALGAE_OUTPUT_RATE = 0.001f;

    }

    [HarmonyPatch(typeof(AlgaeHabitatConfig), nameof(AlgaeHabitatConfig.ConfigureBuildingTemplate))]
    public static class Patch_AlgaeHabitatTemplate
    {
        public static bool Prefix(GameObject go, Tag prefab_tag)
        {
            // This will make sure that the original method code is NOT run

            // Easy-to-edit references
            var storageMods = new List<Storage.StoredItemModifier>(){
                Storage.StoredItemModifier.Hide,
                Storage.StoredItemModifier.Seal
            };
            var dirtyWaterFilter = new List<Tag>() { SimHashes.DirtyWater.CreateTag() };
            var tagCO2 = SimHashes.CarbonDioxide.CreateTag();
            var tagWater = SimHashes.Water.CreateTag();

            // Storage 1 for algae/water
            Storage storage1 = go.AddOrGet<Storage>();
            storage1.showInUI = true;
            storage1.capacityKg = AlgaeHabitatTuning.CO2_CAPACITY;



            // Storage 2 for polluted water output
            Storage storage2 = go.AddComponent<Storage>();
            storage2.capacityKg = AlgaeHabitatTuning.WATER_CAPACITY;
            storage2.showInUI = true;
            storage2.SetDefaultStoredItemModifiers(storageMods);
            storage2.allowItemRemoval = false;
            storage2.storageFilters = dirtyWaterFilter;

            //Storage 3 for algae output
            Storage storage3 = go.AddComponent<Storage>();
            storage3.capacityKg = AlgaeHabitatTuning.ALGAE_CAPACITY;
            storage3.showInUI = true;
            storage3.SetDefaultStoredItemModifiers(storageMods);
            storage3.allowItemRemoval = false;
            storage3.storageFilters = [SimHashes.Algae.CreateTag()];

            var tilemaker = go.AddComponent<ElementTileMakerPatch>();
            tilemaker.emitTag = new Tag("Algae");
            tilemaker.emitMass = 400f;
            tilemaker.emitOffset = new Vector3(0f, 1f);
            tilemaker.storage = storage3;

            // Delivery for water
            var deliveryWater = go.AddComponent<ManualDeliveryKG>();
            deliveryWater.SetStorage(storage1);
            deliveryWater.RequestedItemTag = tagWater;
            deliveryWater.capacity = AlgaeHabitatTuning.WATER_CAPACITY;
            deliveryWater.refillMass = AlgaeHabitatTuning.WATER_CAPACITY / 72f; // 72f default
            deliveryWater.choreTypeIDHash = Db.Get().ChoreTypes.FetchCritical.IdHash;

            // Empty/animation/etc
            var kanims = new KAnimFile[] { Assets.GetAnim(new HashedString("anim_interacts_algae_terarrium_kanim")) };
            var algaeEmpty = go.AddOrGet<AlgaeHabitatEmpty>();
            algaeEmpty.workTime = 5f;
            algaeEmpty.overrideAnims = kanims;
            algaeEmpty.workLayer = Grid.SceneLayer.BuildingFront;


            var algaeHarvest = go.AddOrGet<AlgaeHabitatHarvest>();
            algaeHarvest.workTime = 10f;
            algaeHarvest.workLayer = Grid.SceneLayer.BuildingFront;



            var algaeHabitat = go.AddOrGet<AlgaeHabitat>();
            algaeHabitat.lightBonusMultiplier = 1f;
            algaeHabitat.pressureSampleOffset = new CellOffset(0, 1);

            // Element converter - this building uses two!
            var elementConverter = go.AddComponent<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[] {
                new ElementConverter.ConsumedElement(tagWater, AlgaeHabitatTuning.WATER_RATE)
            };
            go.AddComponent<ElementConverter>().consumedElements = new ElementConverter.ConsumedElement[] {
                new ElementConverter.ConsumedElement(tagCO2, AlgaeHabitatTuning.CO2),
            };

            elementConverter.outputElements = new ElementConverter.OutputElement[] {
                new ElementConverter.OutputElement(AlgaeHabitatTuning.OXYGEN_RATE, SimHashes.Oxygen, 303.15f, outputElementOffsety:1f)
            };
            go.AddComponent<ElementConverter>().outputElements = new ElementConverter.OutputElement[] {
                new ElementConverter.OutputElement(AlgaeHabitatTuning.PWATER_OUTPUT_RATE, SimHashes.DirtyWater, 303.15f, storeOutput:true, outputElementOffsety:1f),
            };
            var algaeOutputConverter = go.AddComponent<ElementConverter>();
            algaeOutputConverter.outputElements = new ElementConverter.OutputElement[] {
        new ElementConverter.OutputElement(AlgaeHabitatTuning.ALGAE_OUTPUT_RATE, SimHashes.Algae, 303.15f, storeOutput:true, outputElementOffsety:1f)
    };
            algaeOutputConverter.SetStorage(storage3);

            go.AddOrGet<AlgaeHarvestMonitor>();


            // Element Consumer
            var elementConsumer = go.AddOrGet<ElementConsumer>();
            elementConsumer.elementToConsume = SimHashes.CarbonDioxide;
            elementConsumer.consumptionRate = 1f;
            elementConsumer.consumptionRadius = 2;
            elementConsumer.showInStatusPanel = false;
            elementConsumer.sampleCellOffset = new Vector3(0.0f, 1f, 0.0f);
            elementConsumer.isRequired = true;
            elementConsumer.storeOnConsume = true;
            elementConsumer.showDescriptor = false;
            elementConsumer.storage = storage1;
            elementConsumer.capacityKG = 3f;
            elementConsumer.ignoreActiveChanged = true;
            elementConsumer.EnableConsumption(true);


            // Passive Consumer
            var passiveElementConsumer = go.AddComponent<PassiveElementConsumer>();
            passiveElementConsumer.elementToConsume = SimHashes.Water;
            passiveElementConsumer.consumptionRate = 1.2f;
            passiveElementConsumer.consumptionRadius = 1;
            passiveElementConsumer.showDescriptor = false;
            passiveElementConsumer.storeOnConsume = true;
            passiveElementConsumer.capacityKG = AlgaeHabitatTuning.WATER_CAPACITY;
            passiveElementConsumer.showInStatusPanel = false;
            passiveElementConsumer.ignoreActiveChanged = true;
            passiveElementConsumer.EnableConsumption(true);

            // Simple UI/animation
            go.AddOrGet<KBatchedAnimController>().randomiseLoopedOffset = true;
            go.AddOrGet<AnimTileable>();
            Prioritizable.AddRef(go);
            
   

            return false; // <- SKIP original
        }
    }

    //Consumption Enabler probably redundant at this point lol

    [HarmonyPatch(typeof(AlgaeHabitat.SMInstance), nameof(AlgaeHabitat.SMInstance.HasEnoughMass))]
    public static class AlgaeHabitat_SMInstance_HasEnoughMass_Patch
    {
        public static void Postfix(AlgaeHabitat.SMInstance __instance, Tag tag, ref bool __result)
        {
            // Only override for CO2 and Water
            if (tag == SimHashes.CarbonDioxide.CreateTag() || tag == SimHashes.Water.CreateTag())
            {
                var storage = __instance.master.GetComponent<Storage>();
                float mass = storage.GetMassAvailable(tag);
                float required = (tag == SimHashes.CarbonDioxide.CreateTag()) ? 0.03f : 0.3f; // match your ElementConverter rates
                __result = mass >= required;
            }
        }
    }



    /// <summary>
    /// Workable component for harvesting algae from the Algae Habitat.
    /// </summary>
    [AddComponentMenu("KMonoBehaviour/Workable/AlgaeHabitatHarvest")]
    public class AlgaeHabitatHarvest : Workable
    {
        private static readonly HashedString[] HARVEST_ANIMS =
        [
        (HashedString) "harvest_pre",
        (HashedString) "harvest_loop"
        ];
        private static readonly HashedString PST_ANIM = new("harvest_pst");


        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            this.workerStatusItem = Db.Get().DuplicantStatusItems.Harvesting;
            this.multitoolHitEffectTag = (Tag)"fx_harvest_splash";
            this.multitoolContext = (HashedString)"harvest";
            this.workingStatusItem = Db.Get().MiscStatusItems.PendingHarvest;
            this.attributeConverter = Db.Get().AttributeConverters.HarvestSpeed;
            this.attributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.PART_DAY_EXPERIENCE;
            this.skillExperienceSkillGroup = Db.Get().SkillGroups.Farming.Id;
            this.skillExperienceMultiplier = SKILLS.PART_DAY_EXPERIENCE;
            this.SetOffsetTable(OffsetGroups.InvertedStandardTable);

            this.workAnims = HARVEST_ANIMS;
            this.workingPstComplete =
            [
            PST_ANIM
            ];
            this.workingPstFailed =
            [
            PST_ANIM
            ];
            this.synchronizeAnims = false;

        }

    }

    /// <summary>
    /// Monitors the algae storage on an AlgaeHabitat and triggers a harvest chore when full.
    /// </summary>
    public class AlgaeHarvestMonitor : KMonoBehaviour, ISim4000ms
    {
        private Storage algaeStorage;
        private AlgaeHabitat habitat;
        private bool needsCheck = false;


        public override void OnSpawn()
        {
            base.OnSpawn();
            habitat = GetComponent<AlgaeHabitat>();
            algaeStorage = habitat.GetAlgaeStorage();
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
        }

        private void OnStorageChanged(object data)
        {
            needsCheck = true;
        }

        public void Sim4000ms(float dt)
        {
            if (!needsCheck)
                return;

            needsCheck = false;

            if (habitat.AlgaeNeedsHarvesting())
            {
              //  Debug.Log("[AlgaeHarvestMonitor] Algae storage full, creating harvest chore.");
                habitat.CreateAlgaeHarvestChore();
            }
            else
            {
                //Debug.Log("[AlgaeHarvestMonitor] Algae storage not full, canceling harvest chore if any.");
                habitat.CancelAlgaeHarvestChore();
            }
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            base.OnCleanUp();
        }
    }
    //ALGAE HARVEST

    [HarmonyPatch(typeof(AlgaeHabitat))]
    public static class AlgaeHabitat_AlgaeStoragePatch
    {
        private static readonly ConditionalWeakTable<AlgaeHabitat, Storage> algaeStorageTable = new ConditionalWeakTable<AlgaeHabitat, Storage>();
        private static readonly ConditionalWeakTable<AlgaeHabitat, Chore> algaeHarvestChoreTable = new ConditionalWeakTable<AlgaeHabitat, Chore>();

        public static void ConfigureAlgaeOutput(this AlgaeHabitat instance)
        {
            Storage storage = null;
            Tag algaeTag = ElementLoader.FindElementByHash(SimHashes.Algae).tag;
            foreach (Storage component in instance.GetComponents<Storage>())
            {
                if (component.storageFilters.Contains(algaeTag))
                {
                    storage = component;
                    break;
                }
            }
            foreach (ElementConverter component in instance.GetComponents<ElementConverter>())
            {
                foreach (ElementConverter.OutputElement outputElement in component.outputElements)
                {
                    if (outputElement.elementHash == SimHashes.Algae)
                    {
                        component.SetStorage(storage);
                        break;
                    }
                }
            }
            if (storage != null)
                algaeStorageTable.AddOrUpdate(instance, storage);
        }

        public static bool AlgaeNeedsHarvesting(this AlgaeHabitat instance)
        {
            if (algaeStorageTable.TryGetValue(instance, out var storage))
            {
                bool needs = storage.RemainingCapacity() <= 0.0f;
            //    Debug.Log($"[AlgaeHabitat] AlgaeNeedsHarvesting: {needs} (remaining capacity: {storage.RemainingCapacity()})");
                return needs;
            }
         //   Debug.LogWarning("[AlgaeHabitat] AlgaeNeedsHarvesting: No algae storage found!");
            return false;
        }

        public static void CreateAlgaeHarvestChore(this AlgaeHabitat instance)
        {
            // Only create if not already running
            if (algaeHarvestChoreTable.TryGetValue(instance, out var existingChore) && existingChore != null && !existingChore.isComplete)
            {
          //      Debug.Log($"[AlgaeHabitat] Harvest chore already exists for {instance}, not creating another.");
                return;
            }

            var harvestComponent = instance.GetComponent<AlgaeHabitatHarvest>();
            if (harvestComponent == null)
            {
          //      Debug.LogError("[AlgaeHabitat] No AlgaeHabitatHarvest component found, cannot create harvest chore!");
                return;
            }

            var newChore = new WorkChore<AlgaeHabitatHarvest>(
                Db.Get().ChoreTypes.Harvest,
                harvestComponent,
                on_complete: (chore) => instance.OnAlgaeHarvestComplete(chore),
                ignore_building_assignment: true
            );
            newChore.AddPrecondition(ChorePreconditions.instance.IsNotARobot);
            algaeHarvestChoreTable.AddOrUpdate(instance, newChore);
        //    Debug.Log($"[AlgaeHabitat] Created new algae harvest chore for {instance}.");
        }

        public static void CancelAlgaeHarvestChore(this AlgaeHabitat instance)
        {
            if (algaeHarvestChoreTable.TryGetValue(instance, out var chore) && chore != null && !chore.isComplete)
            {
                chore.Cancel("Cancelled");
                algaeHarvestChoreTable.Remove(instance);
          //      Debug.Log($"[AlgaeHabitat] Cancelled algae harvest chore for {instance}.");
            }
        }

        public static void OnAlgaeHarvestComplete(this AlgaeHabitat instance, Chore chore)
        {
            algaeHarvestChoreTable.Remove(instance);
            if (algaeStorageTable.TryGetValue(instance, out var storage))
            {
          //      Debug.Log($"[AlgaeHabitat] Harvest complete, dropping all algae.");
                storage.DropAll();
            }
            else
            {
       //         Debug.LogWarning("[AlgaeHabitat] Harvest complete, but no algae storage found!");
            }
        }

        public static Storage GetAlgaeStorage(this AlgaeHabitat instance)
        {
            if (algaeStorageTable.TryGetValue(instance, out var storage))
                return storage;
            return null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AlgaeHabitat.OnSpawn))]
        public static void OnSpawn_Postfix(AlgaeHabitat __instance)
        {
            __instance.ConfigureAlgaeOutput();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AlgaeHabitat.OnCleanUp))]
        public static void OnCleanUp_Postfix(AlgaeHabitat __instance)
        {
            algaeStorageTable.Remove(__instance);
            algaeHarvestChoreTable.Remove(__instance);
        }
    }

    // Helper extension for ConditionalWeakTable update
    public static class ConditionalWeakTableExtensions
    {
        public static void AddOrUpdate<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, TValue value)
            where TKey : class
            where TValue : class
        {
            if (table.TryGetValue(key, out var existing))
                table.Remove(key);
            table.Add(key, value);
        }
    }

    [HarmonyPatch(typeof(AlgaeHabitat.States), "InitializeStates")]

    public static class AlgaeHabitat_InitializeStates_Patch
    {
        // Prefix will run BEFORE the original method.
        // If you return false, the original method will be skipped.
        public static bool Prefix(AlgaeHabitat.States __instance, out StateMachine.BaseState default_state)
        {
            // ==== COPY OF ORIGINAL CODE (EDIT AS YOU WISH) ====

            default_state = __instance.noAlgae;

            __instance.root
                .EventTransition(GameHashes.OperationalChanged, __instance.notoperational,
                    smi => !smi.master.operational.IsOperational)
                .EventTransition(GameHashes.OperationalChanged, __instance.noAlgae,
                    smi => smi.master.operational.IsOperational);

            __instance.notoperational.QueueAnim("off");
            __instance.gotAlgae.PlayAnim("on_pre").OnAnimQueueComplete(__instance.noWater);
            __instance.gotEmptied.PlayAnim("on_pre").OnAnimQueueComplete(__instance.generatingOxygen);
            __instance.lostAlgae.PlayAnim("on_pst").OnAnimQueueComplete(__instance.noAlgae);

            __instance.noAlgae.QueueAnim("off")
                 .Enter(smi => smi.master.GetComponent<PassiveElementConsumer>().EnableConsumption(true))
                .Enter(smi => smi.master.GetComponent<ElementConsumer>().EnableConsumption(true))
                .EventTransition(GameHashes.OnStorageChange, __instance.gotAlgae,
                    smi => smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()))
                .Enter(smi => smi.master.operational.SetActive(false));

            __instance.noWater.QueueAnim("on")
                .Enter(smi => smi.master.GetComponent<PassiveElementConsumer>().EnableConsumption(true))
                .Enter(smi => smi.master.GetComponent<ElementConsumer>().EnableConsumption(true))

                .EventTransition(GameHashes.OnStorageChange, __instance.lostAlgae,
                    smi => !smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()))
                .EventTransition(GameHashes.OnStorageChange, __instance.gotWater,
                    smi => smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()) && smi.HasEnoughMass(GameTags.Water));

            __instance.needsEmptying.QueueAnim("off")
                .Enter(smi => smi.CreateEmptyChore())
                .Exit(smi => smi.CancelEmptyChore())
                .ToggleStatusItem(Db.Get().BuildingStatusItems.HabitatNeedsEmptying)
                .EventTransition(GameHashes.OnStorageChange, __instance.noAlgae,
                    smi => !smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()) || !smi.HasEnoughMass(GameTags.Water))
                .EventTransition(GameHashes.OnStorageChange, __instance.gotEmptied,
                    smi => smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()) && smi.HasEnoughMass(GameTags.Water) && !smi.NeedsEmptying());

            __instance.gotWater.PlayAnim("working_pre").OnAnimQueueComplete(__instance.needsEmptying);

            __instance.generatingOxygen
                .Enter(smi => smi.master.operational.SetActive(true))
                .Exit(smi => smi.master.operational.SetActive(false))
                .Update("GeneratingOxygen", (AlgaeHabitat.SMInstance smi, float dt) =>
                {
                    int cell = Grid.PosToCell(smi.master.transform.GetPosition());
                    float CO2multiplier = Mathf.Min(1.0f + Grid.LightIntensity[cell] * 0.003f, 150f);
                    float Algaemultiplier = Mathf.Min(1.0f + Grid.LightIntensity[cell] * 0.0024f, 120f);
                    float Oxygenmultiplier = Mathf.Min(1.0f + Grid.LightIntensity[cell] * 0.000145f, 8.25f);
                    float Dirtymultiplier = Mathf.Max(1.0f - (Grid.LightIntensity[cell] * 0.00002f), 0f);

                    var converters = smi.master.GetComponents<ElementConverter>();
                    foreach (var converter in converters)
                    {


                        for (int i = 0; i < converter.consumedElements.Length; ++i)
                        {
                            var consumed = converter.consumedElements[i];

                            // Example: set a unique CO2 rate (replace baseCO2Rate with your value if dynamic)
                            if (consumed.Tag == SimHashes.CarbonDioxide.CreateTag())
                            {
                                float baseCO2Rate = /* your base CO2 consumption rate here, e.g.: */ AlgaeHabitatTuning.CO2_RATE;
                                // The above formula assumes you need to reset/base it, or just hardcode if static.

                                // Update the rate (change this field directly, allowed via for-loop)
                                converter.consumedElements[i].MassConsumptionRate = baseCO2Rate * CO2multiplier;
                            }

                            // You can add more "else if" cases for other consumed elements if desired.
                        }

                        foreach (var outputElement in converter.outputElements)
                        {
                            if (outputElement.elementHash == SimHashes.Oxygen)
                            {
                                converter.OutputMultiplier = Oxygenmultiplier;
                            }
                            else if (outputElement.elementHash == SimHashes.DirtyWater)
                            {
                                converter.OutputMultiplier = Dirtymultiplier;
                            }
                            else if (outputElement.elementHash == SimHashes.Algae)
                            {
                                converter.OutputMultiplier = Algaemultiplier;
                            }
                        }

                    }

                })
                .QueueAnim("working_loop", true)
                .EventTransition(GameHashes.OnStorageChange, __instance.stoppedGeneratingOxygen,
                    smi => !smi.HasEnoughMass(GameTags.Water) || !smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()) || smi.NeedsEmptying());

            __instance.stoppedGeneratingOxygen.PlayAnim("working_pst").OnAnimQueueComplete(__instance.stoppedGeneratingOxygenTransition);

            __instance.stoppedGeneratingOxygenTransition
                .EventTransition(GameHashes.OnStorageChange, __instance.needsEmptying,
                    smi => smi.NeedsEmptying())
                .EventTransition(GameHashes.OnStorageChange, __instance.noWater,
                    smi => !smi.HasEnoughMass(GameTags.Water))
                .EventTransition(GameHashes.OnStorageChange, __instance.lostAlgae,
                    smi => !smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()))
                .EventTransition(GameHashes.OnStorageChange, __instance.gotWater,
                    smi => smi.HasEnoughMass(GameTags.Water) && smi.HasEnoughMass(SimHashes.CarbonDioxide.CreateTag()));



            // Return false to prevent the original InitializeStates from running
            return false;
        }
    }
   
}
    
    

