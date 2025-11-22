//using HarmonyLib;
//using System;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TUNING;
//using UnityEngine;
//using UnityEngine.UI;
//using STRINGS;
//using static global::STRINGS.ITEMS.FOOD;


//namespace Rephysicalized
//{

//    // Patch ConfigureBuildingTemplate to attach ComplexFabricator and its storages/workables without touching vanilla logic.
//    [HarmonyPatch(typeof(GeneticAnalysisStationConfig), nameof(GeneticAnalysisStationConfig.ConfigureBuildingTemplate))]
//    public static class GAS_ConfigureBuildingTemplate_Patch
//    {
//        public static void Postfix(GameObject go, Tag prefab_tag)
//        {
//            if (go == null) return;

//            // Attach ComplexFabricator; AddOrGet is idempotent and safe if run multiple times.
//            var fabricator = go.AddOrGet<ComplexFabricator>();
//            fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
//            fabricator.duplicantOperated = true;
//            fabricator.outputOffset = new Vector3(-3f, 1.5f, 0.0f);

//            // Keep fabricator choreing and UI consistent with other fabricators
//            go.AddOrGet<FabricatorIngredientStatusManager>();
//            go.AddOrGet<CopyBuildingSettings>();

//            // Add workable and separate storages, avoiding any interference with the station's seed storage.
//            go.AddOrGet<ComplexFabricatorWorkable>();
//            BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);

//            // NOTE: Prioritizable is already added by vanilla config; no need to add again.
//        }
//    }

//    // Patch DoPostConfigureComplete to configure anims/skills and to register our single recipe.
//    [HarmonyPatch(typeof(GeneticAnalysisStationConfig), nameof(GeneticAnalysisStationConfig.DoPostConfigureComplete))]
//    public static class GAS_DoPostConfigureComplete_Patch
//    {
//        private static bool s_recipeRegistered;

//        public static void Postfix(GameObject go)
//        {
//            if (go == null)
//                return;

//            // Configure the ComplexFabricatorWorkable at spawn to match the station's existing analysis behavior.
//            var kpid = go.GetComponent<KPrefabID>();
//            if (kpid != null)
//            {
//                kpid.prefabSpawnFn += game_object =>
//                {
//                    var workable = game_object.GetComponent<ComplexFabricatorWorkable>();
//                    if (workable == null) return;

//                    // Research skill group and experience, same as GeneticAnalysisStationWorkable
//                    workable.WorkerStatusItem = Db.Get().DuplicantStatusItems.AnalyzingGenes;
//                    workable.AttributeConverter = Db.Get().AttributeConverters.ResearchSpeed;
//                    workable.AttributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.PART_DAY_EXPERIENCE;
//                    workable.SkillExperienceSkillGroup = Db.Get().SkillGroups.Research.Id;
//                    workable.SkillExperienceMultiplier = SKILLS.PART_DAY_EXPERIENCE;

//                    // Require same skill perk as seed analysis
//                    workable.requiredSkillPerk = Db.Get().SkillPerks.CanIdentifyMutantSeeds.Id;

//                    // Use the same interaction anims as the normal GeneticAnalysisStation workable
//                    var anim = Assets.GetAnim((HashedString)"anim_interacts_genetic_analysisstation_kanim");
//                    workable.overrideAnims = new KAnimFile[] { anim };
//                    workable.workAnims = new HashedString[] { (HashedString)"working_pre", (HashedString)"working_loop" };
//                    workable.synchronizeAnims = false;

//                    // Pick a random dupe interact from the anim file (excluding pre/pst), similar to SupermaterialRefinery
//                    if (anim != null)
//                    {
//                        var data = anim.GetData();
//                        var dupeInteractAnims = new List<HashedString>();
//                        for (int i = 0; i < data.animCount; i++)
//                        {
//                            var name = (HashedString)data.GetAnim(i).name;
//                            if (name != (HashedString)"working_pre" && name != (HashedString)"working_pst")
//                                dupeInteractAnims.Add(name);
//                        }

//                        if (dupeInteractAnims.Count > 0)
//                        {
//                            workable.GetDupeInteract = () => new HashedString[]
//                            {
//                                (HashedString)"working_loop",
//                                dupeInteractAnims.GetRandom<HashedString>()
//                            };
//                        }
//                    }
//                };
//            }

//            if (!s_recipeRegistered)
//            {
//                s_recipeRegistered = true;

//                ComplexRecipe.RecipeElement[] recipeElementArray1 = new ComplexRecipe.RecipeElement[]
//            {
//                    new ComplexRecipe.RecipeElement("DinofernSeed", 1f),
//              new ComplexRecipe.RecipeElement(DatabankHelper.TAG, 30f),
//                               new ComplexRecipe.RecipeElement("ColdWheatSeed", 1f),

//            };

//                ComplexRecipe.RecipeElement[] recipeElementArray2 = new ComplexRecipe.RecipeElement[]
//                {
//                    new ComplexRecipe.RecipeElement("DinofernSeed", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
//                };

//                ComplexRecipe complexRecipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("GeneticAnalysisStation", (IList<ComplexRecipe.RecipeElement>)recipeElementArray1, (IList<ComplexRecipe.RecipeElement>)recipeElementArray2), recipeElementArray1, recipeElementArray2, DlcManager.DLC3)
//                {
//                    time = 120f,
//                    description = STRINGS.BUILDINGS.GENETICANALYSISSTATION.DINOFERN,
//                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
//                    fabricators = new List<Tag> { TagManager.Create(GeneticAnalysisStationConfig.ID) }
//                };


//                ComplexRecipe.RecipeElement[] recipeElementArray3 = new ComplexRecipe.RecipeElement[]
//              {
//                    new ComplexRecipe.RecipeElement("ColdBreatherSeed", 1f),
//              new ComplexRecipe.RecipeElement(DatabankHelper.TAG, 50f),
//                               new ComplexRecipe.RecipeElement(SimHashes.Phosphorus.CreateTag(), 1f),

//              };

//                ComplexRecipe.RecipeElement[] recipeElementArray4 = new ComplexRecipe.RecipeElement[]
//                {
//                    new ComplexRecipe.RecipeElement("ColdBReatherSeed", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
//                };


//                ComplexRecipe complexRecipe2 = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("GeneticAnalysisStation", (IList<ComplexRecipe.RecipeElement>)recipeElementArray3, (IList<ComplexRecipe.RecipeElement>)recipeElementArray4), recipeElementArray3, recipeElementArray4)
//                {
//                    time = 120f,
//                    description = STRINGS.BUILDINGS.GENETICANALYSISSTATION.COLDBREATHER,
//                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
//                    fabricators = new List<Tag> { TagManager.Create(GeneticAnalysisStationConfig.ID) }
//                };
//                ComplexRecipe.RecipeElement[] recipeElementArray5 = new ComplexRecipe.RecipeElement[]
//      {
//                    new ComplexRecipe.RecipeElement("OxyfernSeed", 1f),
//              new ComplexRecipe.RecipeElement(DatabankHelper.TAG, 20f),
//                               new ComplexRecipe.RecipeElement(SimHashes.Algae.CreateTag(), 1f),

//      };

//                ComplexRecipe.RecipeElement[] recipeElementArray6 = new ComplexRecipe.RecipeElement[]
//                {
//                    new ComplexRecipe.RecipeElement("OxyfernSeed", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
//                };


//                ComplexRecipe complexRecipe3 = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("GeneticAnalysisStation", (IList<ComplexRecipe.RecipeElement>)recipeElementArray5, (IList<ComplexRecipe.RecipeElement>)recipeElementArray6), recipeElementArray5, recipeElementArray6)
//                {
//                    time = 120f,
//                    description = STRINGS.BUILDINGS.GENETICANALYSISSTATION.OXYFERN,
//                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
//                    fabricators = new List<Tag> { TagManager.Create(GeneticAnalysisStationConfig.ID) }
//                };
//                if (DlcManager.IsExpansion1Active())
//                {
//                    ComplexRecipe.RecipeElement[] recipeElementArray7 = new ComplexRecipe.RecipeElement[]
//  {
//                    new ComplexRecipe.RecipeElement("CritterTrapPlantSeed", 1f),
//              new ComplexRecipe.RecipeElement(DatabankHelper.TAG, 40f),
//                               new ComplexRecipe.RecipeElement("SwampHarvestPlantSeed", 1f),

//  };
//                    ComplexRecipe.RecipeElement[] recipeElementArray8 = new ComplexRecipe.RecipeElement[]
//                {
//                    new ComplexRecipe.RecipeElement("CritterTrapPlantSeed", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
//                };


//                    ComplexRecipe complexRecipe4 = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("GeneticAnalysisStation", (IList<ComplexRecipe.RecipeElement>)recipeElementArray7, (IList<ComplexRecipe.RecipeElement>)recipeElementArray8), recipeElementArray7, recipeElementArray8)
//                    {
//                        time = 120f,
//                        description = STRINGS.BUILDINGS.GENETICANALYSISSTATION.CRITTERTRAPPLANT,
//                        nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
//                        fabricators = new List<Tag> { TagManager.Create(GeneticAnalysisStationConfig.ID) }
//                    };
//                }



//                ComplexRecipe.RecipeElement[] recipeElementArray9 = new ComplexRecipe.RecipeElement[]
//            {
//                    new ComplexRecipe.RecipeElement("EvilFlowerSeed", 1f),
//              new ComplexRecipe.RecipeElement(DatabankHelper.TAG, 200f),
//                               new ComplexRecipe.RecipeElement("MushroomSeed", 1f),

//            };

//                ComplexRecipe.RecipeElement[] recipeElementArray10 = new ComplexRecipe.RecipeElement[]
//                {
//                    new ComplexRecipe.RecipeElement("EvilFlowerSeed", 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
//                };

//                ComplexRecipe complexRecipe5 = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("GeneticAnalysisStation", (IList<ComplexRecipe.RecipeElement>)recipeElementArray9, (IList<ComplexRecipe.RecipeElement>)recipeElementArray10), recipeElementArray9, recipeElementArray10)
//                {
//                    time = 120f,
//                    description = STRINGS.BUILDINGS.GENETICANALYSISSTATION.SPORECHID,
//                    nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
//                    fabricators = new List<Tag> { TagManager.Create(GeneticAnalysisStationConfig.ID) }
//                };

//            }
//        }
//    }





//}


