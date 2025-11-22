using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using STRINGS;
using Klei;
using static Wire;

namespace Rephysicalized
{
    internal static class WireCapacitySettings
    {
        private static readonly Dictionary<SimHashes, float> Multipliers = new Dictionary<SimHashes, float>
        {
            { SimHashes.Copper, 1.5f },
            { SimHashes.Cuprite, 1.2f },
            { SimHashes.Gold, 1.2f },
            { SimHashes.GoldAmalgam, 1.1f },
            { SimHashes.Aluminum, 1.2f },
            { SimHashes.AluminumOre, 1.1f },
            { SimHashes.Iridium, 1.2f },
            { SimHashes.TempConductorSolid, 1.2f },
            { SimHashes.Lead, 0.8f },
            { SimHashes.DepletedUranium, 0.7f },
            { SimHashes.UraniumOre, 0.8f },
            { SimHashes.Cinnabar, 0.8f },
            { SimHashes.SolidMercury, 2.5f },
        };

        public static float GetMultiplier(SimHashes id)
        {
            return Multipliers.TryGetValue(id, out var mult) ? Mathf.Max(0f, mult) : 1f;
        }
    }

    public static class WireMaterialCapacity
    {
        public static float GetMultiplier(GameObject go)
        {
            if (go == null)
                return 1f;
            var pe = go.GetComponent<PrimaryElement>();
            if (pe == null)
                return 1f;
            return WireCapacitySettings.GetMultiplier(pe.ElementID);
        }

        public static float GetMultiplier(Element element)
        {
            if (element == null)
                return 1f;
            return WireCapacitySettings.GetMultiplier(element.id);
        }

        public static float GetEffectiveMax(Wire wire)
        {
            if (wire == null)
                return 0f;
            var baseMax = Wire.GetMaxWattageAsFloat(wire.MaxWattageRating);
            return baseMax * GetMultiplier(wire.gameObject);
        }

        public static float GetEffectiveMax(WireUtilityNetworkLink link)
        {
            if (link == null)
                return 0f;
            var baseMax = Wire.GetMaxWattageAsFloat(link.maxWattageRating);
            return baseMax * GetMultiplier(link.gameObject);
        }

        public static float GetNetworkMinEffectiveMax(List<Wire>[] wireGroups)
        {
            float min = float.PositiveInfinity;
            if (wireGroups != null)
            {
                for (int i = 0; i < wireGroups.Length; i++)
                {
                    var group = wireGroups[i];
                    if (group == null)
                        continue;
                    for (int j = 0; j < group.Count; j++)
                    {
                        var w = group[j];
                        if (w == null) continue;
                        var eff = GetEffectiveMax(w);
                        if (eff < min)
                            min = eff;
                    }
                }
            }
            return float.IsPositiveInfinity(min) ? 0f : min;
        }

        public static float GetNetworkMinEffectiveMax(List<Wire>[] wireGroups, List<WireUtilityNetworkLink>[] bridgeGroups)
        {
            float min = GetNetworkMinEffectiveMax(wireGroups);
            if (bridgeGroups != null)
            {
                for (int i = 0; i < bridgeGroups.Length; i++)
                {
                    var bg = bridgeGroups[i];
                    if (bg == null)
                        continue;
                    for (int j = 0; j < bg.Count; j++)
                    {
                        var link = bg[j];
                        if (link == null) continue;
                        var eff = GetEffectiveMax(link);
                        if (eff < min || float.IsPositiveInfinity(min))
                            min = eff;
                    }
                }
            }
            return float.IsPositiveInfinity(min) ? 0f : min;
        }
    }

    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.UpdateOverloadTime))]
    public static class ElectricalUtilityNetwork_UpdateOverloadTime_Prefix
    {
        private static readonly System.Reflection.FieldInfo F_wireGroups = AccessTools.Field(typeof(ElectricalUtilityNetwork), "wireGroups");
        private static readonly System.Reflection.FieldInfo F_allWires = AccessTools.Field(typeof(ElectricalUtilityNetwork), "allWires");
        private static readonly System.Reflection.FieldInfo F_targetOverloadedWire = AccessTools.Field(typeof(ElectricalUtilityNetwork), "targetOverloadedWire");
        private static readonly System.Reflection.FieldInfo F_timeOverloaded = AccessTools.Field(typeof(ElectricalUtilityNetwork), "timeOverloaded");
        private static readonly System.Reflection.FieldInfo F_timeOverloadNotificationDisplayed = AccessTools.Field(typeof(ElectricalUtilityNetwork), "timeOverloadNotificationDisplayed");
        private static readonly System.Reflection.FieldInfo F_overloadedNotification = AccessTools.Field(typeof(ElectricalUtilityNetwork), "overloadedNotification");

        public static bool Prefix(ElectricalUtilityNetwork __instance, float dt, float watts_used, List<WireUtilityNetworkLink>[] bridgeGroups)
        {
            try
            {
                var wireGroups = (List<Wire>[])F_wireGroups.GetValue(__instance);
                var allWires = (List<Wire>)F_allWires.GetValue(__instance);

                bool flag = false;
                List<Wire> wireList = null;
                List<WireUtilityNetworkLink> utilityNetworkLinkList = null;

                for (int rating = 0; rating < 5; ++rating)
                {
                    var wireGroup = wireGroups[rating];
                    var bridgeGroup = bridgeGroups[rating];

                    // Compute group min effective threshold
                    float minEff = float.PositiveInfinity;

                    if (wireGroup != null)
                    {
                        for (int i = 0; i < wireGroup.Count; ++i)
                        {
                            var w = wireGroup[i];
                            if (w == null) continue;
                            float eff = WireMaterialCapacity.GetEffectiveMax(w);
                            if (eff < minEff) minEff = eff;
                        }
                    }
                    if (bridgeGroup != null)
                    {
                        for (int i = 0; i < bridgeGroup.Count; ++i)
                        {
                            var l = bridgeGroup[i];
                            if (l == null) continue;
                            float eff = WireMaterialCapacity.GetEffectiveMax(l);
                            if (eff < minEff) minEff = eff;
                        }
                    }

                    if (float.IsPositiveInfinity(minEff))
                        minEff = Wire.GetMaxWattageAsFloat((Wire.WattageRating)rating);

                    float threshold = minEff + TUNING.POWER.FLOAT_FUDGE_FACTOR;

                    if (watts_used > threshold && ((bridgeGroup != null && bridgeGroup.Count > 0) || (wireGroup != null && wireGroup.Count > 0)))
                    {
                        flag = true;
                        wireList = wireGroup;
                        utilityNetworkLinkList = bridgeGroup;
                        break;
                    }
                }

                // Remove nulls like vanilla does to avoid stale refs
                wireList?.RemoveAll(x => x == null);
                utilityNetworkLinkList?.RemoveAll(x => x == null);

                if (flag)
                {
                    float timeOverloaded = (float)F_timeOverloaded.GetValue(__instance) + dt;
                    if (timeOverloaded > 6f)
                    {
                        timeOverloaded = 0f;
                        var targetOverloadedWire = (GameObject)F_targetOverloadedWire.GetValue(__instance);
                        if (targetOverloadedWire == null)
                        {
                            const float EPS = 0.01f;
                            float globalMinEff = float.PositiveInfinity;
                            if (utilityNetworkLinkList != null)
                            {
                                for (int i = 0; i < utilityNetworkLinkList.Count; i++)
                                {
                                    var link = utilityNetworkLinkList[i];
                                    if (link == null) continue;
                                    float eff = WireMaterialCapacity.GetEffectiveMax(link);
                                    if (eff < globalMinEff) globalMinEff = eff;
                                }
                            }
                            if (wireList != null)
                            {
                                for (int i = 0; i < wireList.Count; i++)
                                {
                                    var w2 = wireList[i];
                                    if (w2 == null) continue;
                                    float eff = WireMaterialCapacity.GetEffectiveMax(w2);
                                    if (eff < globalMinEff) globalMinEff = eff;
                                }
                            }
                            List<GameObject> weakest = new List<GameObject>();
                            if (utilityNetworkLinkList != null)
                            {
                                for (int i = 0; i < utilityNetworkLinkList.Count; i++)
                                {
                                    var link = utilityNetworkLinkList[i];
                                    if (link == null) continue;
                                    float eff = WireMaterialCapacity.GetEffectiveMax(link);
                                    if (Mathf.Abs(eff - globalMinEff) <= EPS)
                                        weakest.Add(link.gameObject);
                                }
                            }
                            if (wireList != null)
                            {
                                for (int i = 0; i < wireList.Count; i++)
                                {
                                    var w2 = wireList[i];
                                    if (w2 == null) continue;
                                    float eff = WireMaterialCapacity.GetEffectiveMax(w2);
                                    if (Mathf.Abs(eff - globalMinEff) <= EPS)
                                        weakest.Add(w2.gameObject);
                                }
                            }
                            if (weakest.Count > 0)
                                targetOverloadedWire = weakest[UnityEngine.Random.Range(0, weakest.Count)];
                            F_targetOverloadedWire.SetValue(__instance, targetOverloadedWire);
                        }

                        if (targetOverloadedWire != null)
                        {
                            // Use the exact vanilla path to avoid InvalidCastException in BuildingHP.OnDoBuildingDamage
                            // -794517298 is the event hash used by vanilla for DoBuildingDamage
                            targetOverloadedWire.BoxingTrigger<BuildingHP.DamageSourceInfo>(
                                -794517298,
                                new BuildingHP.DamageSourceInfo
                                {
                                    damage = 1,
                                    source = BUILDINGS.DAMAGESOURCES.CIRCUIT_OVERLOADED,
                                    popString = UI.GAMEOBJECTEFFECTS.DAMAGE_POPS.CIRCUIT_OVERLOADED,
                                    takeDamageEffect = SpawnFXHashes.BuildingSpark,
                                    fullDamageEffectName = "spark_damage_kanim",
                                    statusItemID = Db.Get().BuildingStatusItems.Overloaded.Id
                                });
                        }

                        var overloadedNotification = (Notification)F_overloadedNotification.GetValue(__instance);
                        if (overloadedNotification == null && targetOverloadedWire != null)
                        {
                            F_timeOverloadNotificationDisplayed.SetValue(__instance, 0f);
                            overloadedNotification = new Notification(MISC.NOTIFICATIONS.CIRCUIT_OVERLOADED.NAME, NotificationType.BadMinor, click_focus: targetOverloadedWire.transform);
                            F_overloadedNotification.SetValue(__instance, overloadedNotification);
                            GameScheduler.Instance.Schedule("Power Tutorial", 2f, _ => Tutorial.Instance.TutorialMessage(Tutorial.TutorialMessages.TM_Power));
                            Game.Instance.FindOrAdd<Notifier>().Add(overloadedNotification);
                        }
                    }
                    F_timeOverloaded.SetValue(__instance, timeOverloaded);
                }
                else
                {
                    float timeOverloaded = (float)F_timeOverloaded.GetValue(__instance);
                    timeOverloaded = Mathf.Max(0f, timeOverloaded - dt * 0.95f);
                    F_timeOverloaded.SetValue(__instance, timeOverloaded);

                    float timeOverloadNotificationDisplayed = (float)F_timeOverloadNotificationDisplayed.GetValue(__instance);
                    timeOverloadNotificationDisplayed += dt;
                    if (timeOverloadNotificationDisplayed > 5f)
                    {
                        var overloadedNotification = (Notification)F_overloadedNotification.GetValue(__instance);
                        if (overloadedNotification != null)
                        {
                            Game.Instance.FindOrAdd<Notifier>().Remove(overloadedNotification);
                            F_overloadedNotification.SetValue(__instance, null);
                        }
                        F_timeOverloadNotificationDisplayed.SetValue(__instance, 0f);
                    }
                    else
                    {
                        F_timeOverloadNotificationDisplayed.SetValue(__instance, timeOverloadNotificationDisplayed);
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rephysicalized/WireCapacity] Prefix ERROR: {e}");
                return true;
            }
        }
    }
}