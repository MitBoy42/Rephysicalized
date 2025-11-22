

using HarmonyLib;
using Klei.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using TUNING;
using UnityEngine;
using KMod;



namespace Rephysicalized
{

    // Seal and BabySeal: adjust death loot and diet consumption/production without affecting calorie tracker
    [HarmonyPatch(typeof(SealConfig), nameof(SealConfig.CreateSeal))]
    public static class SealConfig_CreateSeal_Postfix
    {
        public static void Postfix(string id, bool is_baby, ref GameObject __result)
        {
           
            

                // Only affect Seal and SealBaby prefabs
                if (!(id == SealConfig.ID || id == BabySealConfig.ID)) return;

                // 1) Adjust death loot to Tallow x1
                var butcher = __result.GetComponent<Butcherable>();
                {
                    var drops = new System.Collections.Generic.Dictionary<string, float>
                    {
                        { "Tallow", 1f }
                    };
                    butcher.SetDrops(drops);

                    // Ensure runtime instances also use Tallow x1 by overriding during prefab init
                    var kpid = __result.GetComponent<KPrefabID>();
                    {
                        kpid.prefabInitFn += (KPrefabID.PrefabFn)(inst =>
                        {
                            var b = inst.GetComponent<Butcherable>();

                            {
                                var d = new System.Collections.Generic.Dictionary<string, float>
                                {
                                    { "Tallow", 1f }
                                };
                                b.SetDrops(d);
                            }
                        });
                    }
                }

                // 2) Adjust diet: consume 60 kg sugarwater or 60 kg sugar per cycle, produce 52 kg ethanol
                const float targetCaloriesPerKg = SealTuning.STANDARD_CALORIES_PER_CYCLE / 60f; // 100000 / 60 = 1666.6667
                const float producedPerConsumed = 52f / 60f; // 0.8666667

                var calDef = __result.GetDef<CreatureCalorieMonitor.Def>();
                if (calDef?.diet?.infos != null)
                {
                    var newInfos = new System.Collections.Generic.List<Diet.Info>(calDef.diet.infos.Length);
                    foreach (var info in calDef.diet.infos)
                    {
                        bool isPlantStorage = info.foodType == Diet.Info.FoodType.EatPlantStorage && info.consumedTags.Contains(new Tag("SpaceTree"));
                        bool isSugar = info.consumedTags.Contains(SimHashes.Sucrose.CreateTag());

                        if (isPlantStorage || isSugar)
                        {
                            var consumed = new System.Collections.Generic.HashSet<Tag>(info.consumedTags);
                            var newInfo = new Diet.Info(
                                consumed,
                                info.producedElement,
                                targetCaloriesPerKg,
                                producedPerConsumed,
                                disease_id: null,
                                disease_per_kg_produced: 0f,
                                produce_solid_tile: info.produceSolidTile,
                                food_type: info.foodType,
                                emmit_disease_on_cell: info.emmitDiseaseOnCell,
                                eat_anims: info.eatAnims
                            );
                            newInfos.Add(newInfo);
                        }
                        else
                        {
                            newInfos.Add(new Diet.Info(info));
                        }
                    }

                    var newDiet = new Diet(newInfos.ToArray());
                    calDef.diet = newDiet;

                    // Update SolidConsumerMonitor.Def diet if present
                    var solidDef = __result.GetDef<SolidConsumerMonitor.Def>();

                    solidDef.diet = newDiet;
                }
       
        }
    }

    [HarmonyPatch(typeof(EntityTemplates), "DeathDropFunction")]
    public static class EntityTemplates_DeathDropFunction_Seal_Adjust
    {
        public static void Prefix(GameObject inst, ref float onDeathDropCount, ref string onDeathDropID)
        {

            var kpid = inst.GetComponent<KPrefabID>();

            Tag pt = kpid.PrefabTag;
            if (pt == new Tag(SealConfig.ID) || pt == new Tag(BabySealConfig.ID))
            {
                onDeathDropID = "Tallow";
                onDeathDropCount = 1f;
            }

        }

    }
}

