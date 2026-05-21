using System;
using System.Collections.Generic;
using STRINGS;
using HarmonyLib;
using UnityEngine;

namespace Rephysicalized.Content.ElectricityPatches
{
internal static class WireCapacitySettings
    {
        // Fast path: map from SimHashes enum to multiplier for built-in elements.
        private static readonly Dictionary<SimHashes, float> SimMultipliers = new Dictionary<SimHashes, float>();
        // Fallback for string-only element ids (third-party mods etc.).
        private static readonly Dictionary<string, float> StringMultipliers = new Dictionary<string, float>(StringComparer.Ordinal);


        public static void InitializeBaseMultipliers()
        {
            SimMultipliers.Clear();
            StringMultipliers.Clear();

            // Built-in simhash-based entrie
            SimMultipliers[SimHashes.Copper] = 1.4f;
            SimMultipliers[SimHashes.Cuprite] = 1.2f;
            SimMultipliers[SimHashes.Gold] = 1.2f;
            SimMultipliers[SimHashes.GoldAmalgam] = 1.1f;
            SimMultipliers[SimHashes.Aluminum] = 1.2f;
            SimMultipliers[SimHashes.AluminumOre] = 1.1f;
            SimMultipliers[SimHashes.Iridium] = 1.2f;
            SimMultipliers[SimHashes.TempConductorSolid] = 1.2f;
            SimMultipliers[SimHashes.Lead] = 0.8f;
            SimMultipliers[SimHashes.DepletedUranium] = 0.7f;
            SimMultipliers[SimHashes.UraniumOre] = 0.8f;
            SimMultipliers[SimHashes.Cinnabar] = 0.8f;
            SimMultipliers[SimHashes.SolidMercury] = 2.5f;

            // Ronivan / mod-provided element names - keep as string keys
            StringMultipliers["SolidSilver"] = 1.4f;
            StringMultipliers["SolidZinc"] = 1.2f;
            StringMultipliers["AurichalciteOre"] = 1.1f;
            StringMultipliers["ArgentiteOre"] = 1.2f;
            StringMultipliers["Plasteel"] = 0.2f;
            StringMultipliers["AIO_FerroChrome_Solid"] = 0.2f;
            StringMultipliers["AIO_Chromium_Solid"] = 0.8f;
            StringMultipliers["CarbonFiber"] = 0.2f;
        }

        public static float GetMultiplier(SimHashes id)
        {
            // Fast path: lookup by SimHashes enum when possible (no allocation).
            if (SimMultipliers.TryGetValue(id, out var m))
                return Mathf.Max(0f, m);

            // Fallback: convert to tag string once and lookup in string dictionary.
            var key = id.CreateTag().ToString();
            if (StringMultipliers.TryGetValue(key, out m))
                return Mathf.Max(0f, m);

            return 1f;
        }

    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class WireCapacitySettingsInitializer
    {
        public static void Postfix()
        {
            WireCapacitySettings.InitializeBaseMultipliers();
        }

        // Hook for external config: when this returns false all material-based wattage patches should be skipped.
        // Default is enabled. Your mod config code can set WireCapacitySettings.IsWattageCapacityEnabled = () => false;
        public static Func<bool> IsWattageCapacityEnabled = () => true;

        public static bool IsEnabled() => IsWattageCapacityEnabled == null ? true : IsWattageCapacityEnabled();
    }

    // Precompute multiplier/effective max at wire spawn to avoid repeated GetComponent calls later
    [HarmonyPatch(typeof(Wire), nameof(Wire.OnSpawn))]
    public static class Wire_OnSpawn_Patch
    {
        public static void Postfix(Wire __instance)
        {
            if (__instance == null) return;
            if (!Config.Instance.WattageCapacity) return;
            try { WireMaterialCapacity.GetEffectiveMaxCached(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(Wire), nameof(Wire.OnCleanUp))]
    public static class Wire_OnCleanUp_Patch
    {
        public static void Postfix(Wire __instance)
        {
            if (__instance == null) return;
            if (!Config.Instance.WattageCapacity) return;
            try { WireMaterialCapacity.InvalidateCachesForWire(__instance); } catch { }
        }
    }

    // Invalidate caches when wires/links are added/removed/reset on networks.
    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.AddItem))]
    public static class ElectricalUtilityNetwork_AddItem_Patch
    {
        public static void Postfix(object item)
        {
            // OnSpawn handles wires; only precompute for links added to networks here.
            if (item is WireUtilityNetworkLink l)
                WireMaterialCapacity.GetEffectiveMaxCached(l);
        }
    }

    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.RemoveItem))]
    public static class ElectricalUtilityNetwork_RemoveItem_Patch
    {
        public static void Postfix(object item)
        {
            if (!Config.Instance.WattageCapacity) return;
            if (item is Wire w) WireMaterialCapacity.InvalidateCachesForWire(w);
            else if (item is WireUtilityNetworkLink l) WireMaterialCapacity.InvalidateCachesForLink(l);
        }
    }

    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.Reset))]
    public static class ElectricalUtilityNetwork_Reset_Patch
    {
        public static void Postfix()
        {
            if (!Config.Instance.WattageCapacity) return;
            WireMaterialCapacity.InvalidateAllCaches();
        }
    }

 
    [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.GetMaxSafeWattage))]
    public static class ElectricalUtilityNetwork_GetMaxSafeWattage_Patch
    {
        public static bool Prefix(ElectricalUtilityNetwork __instance, List<WireUtilityNetworkLink>[] bridgeGroups, ref float __result)
        {
            if (!Config.Instance.WattageCapacity) return true;
            if (__instance == null) return true; // let original run

            try
            {
                var wireGroups = __instance.wireGroups;
                float minEff = WireMaterialCapacity.GetNetworkMinEffectiveMax(wireGroups, bridgeGroups);
                __result = minEff;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public static class WireMaterialCapacity
    {
        // Cache effective max per wire/link to avoid recomputing PrimaryElement + multiplier every tick.
        // Keyed by UnityEngine.Object instance references.
        private static readonly Dictionary<Wire, float> WireEffectiveMaxCache = new Dictionary<Wire, float>(256);
        private static readonly Dictionary<WireUtilityNetworkLink, float> LinkEffectiveMaxCache = new Dictionary<WireUtilityNetworkLink, float>(256);

        // Optional faster per-wire prepopulated cache keyed by instance id to remove dictionary lookups by UnityEngine.Object
        // in hot paths. We keep both caches for compatibility; GetEffectiveMaxCached will prefer the object-keyed cache.
        private static readonly Dictionary<int, float> WireInstanceIdCache = new Dictionary<int, float>(256);

        public static void InvalidateCachesForWire(Wire wire)
        {
            if (wire == null) return;
            WireEffectiveMaxCache.Remove(wire);
            WireInstanceIdCache.Remove(wire.GetInstanceID());
        }

        public static void InvalidateCachesForLink(WireUtilityNetworkLink link)
        {
            if (link == null) return;
            LinkEffectiveMaxCache.Remove(link);
        } 

        public static void InvalidateAllCaches()
        {
            WireEffectiveMaxCache.Clear();
            LinkEffectiveMaxCache.Clear();
            WireInstanceIdCache.Clear();
        }

        private static float ComputeEffectiveMax(Wire wire)
        {
            if (wire == null)
                return 0f;
            var baseMax = Wire.GetMaxWattageAsFloat(wire.MaxWattageRating);
            return baseMax * GetMultiplier(wire.gameObject);
        }

        private static float ComputeEffectiveMax(WireUtilityNetworkLink link)
        {
            if (link == null)
                return 0f;
            var baseMax = Wire.GetMaxWattageAsFloat(link.maxWattageRating);
            return baseMax * GetMultiplier(link.gameObject);
        }

        public static float GetEffectiveMaxCached(Wire wire)
        {
            if (wire == null)
                return 0f;

            // Fast instance id cache check first
            int id = wire.GetInstanceID();
            if (WireInstanceIdCache.TryGetValue(id, out var instVal))
                return instVal;

            if (WireEffectiveMaxCache.TryGetValue(wire, out var eff))
            {
                // populate instance cache for faster next access
                WireInstanceIdCache[id] = eff;
                return eff;
            }

            eff = ComputeEffectiveMax(wire);
            WireEffectiveMaxCache[wire] = eff;
            WireInstanceIdCache[id] = eff;
            return eff;
        }

        public static float GetEffectiveMaxCached(WireUtilityNetworkLink link)
        {
            if (link == null)
                return 0f;

            if (LinkEffectiveMaxCache.TryGetValue(link, out var eff))
                return eff;

            eff = ComputeEffectiveMax(link);
            LinkEffectiveMaxCache[link] = eff;
            return eff;
        }

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

        // Public API: prefer cached getters for performance
        // Use GetEffectiveMaxCached for wires and links.

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
                        var eff = GetEffectiveMaxCached(w);
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
                        var eff = GetEffectiveMaxCached(link);
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
        private const float RecalcIntervalSeconds = 1f;

        // Throttle min-effective-capacity threshold recomputation per-network.
        private static readonly Dictionary<ElectricalUtilityNetwork, float> LastRecalcTime = new Dictionary<ElectricalUtilityNetwork, float>();
        private static readonly Dictionary<ElectricalUtilityNetwork, float[]> CachedMinEffectiveByRating = new Dictionary<ElectricalUtilityNetwork, float[]>();


        public static bool Prefix(ElectricalUtilityNetwork __instance, float dt, float watts_used, List<WireUtilityNetworkLink>[] bridgeGroups)
        {
            if (!Config.Instance.WattageCapacity) return true;
            var wireGroups = __instance.wireGroups;

                bool flag = false;
                List<Wire> wireList = null;
                List<WireUtilityNetworkLink> utilityNetworkLinkList = null;

                // Recompute min effective capacity per rating group only every RecalcIntervalSeconds.
                // If throttled, reuse the previously cached min values.
                float cachedMinTime = 0f;
                CachedMinEffectiveByRating.TryGetValue(__instance, out var cachedArr);
                LastRecalcTime.TryGetValue(__instance, out cachedMinTime);
                bool shouldRecalc = cachedArr == null || (Time.time - cachedMinTime) >= RecalcIntervalSeconds;

                if (shouldRecalc)
                {
                    cachedArr = new float[5];

                    for (int rating = 0; rating < 5; ++rating)
                    {
                        var wireGroup = wireGroups[rating];
                        var bridgeGroup = bridgeGroups[rating];

                        float minEff = float.PositiveInfinity;

                        if (wireGroup != null)
                        {
                            for (int i = 0; i < wireGroup.Count; ++i)
                            {
                                var w = wireGroup[i];
                                if (w == null) continue;
                                float eff = WireMaterialCapacity.GetEffectiveMaxCached(w);
                                if (eff < minEff) minEff = eff;
                            }
                        }
                        if (bridgeGroup != null)
                        {
                            for (int i = 0; i < bridgeGroup.Count; ++i)
                            {
                                var l = bridgeGroup[i];
                                if (l == null) continue;
                                float eff = WireMaterialCapacity.GetEffectiveMaxCached(l);
                                if (eff < minEff) minEff = eff;
                            }
                        }

                        if (float.IsPositiveInfinity(minEff))
                            minEff = Wire.GetMaxWattageAsFloat((Wire.WattageRating)rating);

                        cachedArr[rating] = minEff;
                    }

                    CachedMinEffectiveByRating[__instance] = cachedArr;
                    LastRecalcTime[__instance] = Time.time;
                }

                for (int rating = 0; rating < 5; ++rating)
                {
                    var wireGroup = wireGroups[rating];
                    var bridgeGroup = bridgeGroups[rating];

                    float threshold = cachedArr[rating] + TUNING.POWER.FLOAT_FUDGE_FACTOR;

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
                    float timeOverloaded = __instance.timeOverloaded + dt;

                    if (timeOverloaded > 6f)
                    {
                        timeOverloaded = 0f;
                        var targetOverloadedWire = __instance.targetOverloadedWire;

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
                                    float eff = WireMaterialCapacity.GetEffectiveMaxCached(link);
                                    if (eff < globalMinEff) globalMinEff = eff;

                                }
                            }
                            if (wireList != null)
                            {
                                for (int i = 0; i < wireList.Count; i++)
                                {
                                    var w2 = wireList[i];
                                    if (w2 == null) continue;
                                    float eff = WireMaterialCapacity.GetEffectiveMaxCached(w2);
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
                                    float eff = WireMaterialCapacity.GetEffectiveMaxCached(link);
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
                                    float eff = WireMaterialCapacity.GetEffectiveMaxCached(w2);
                                    if (Mathf.Abs(eff - globalMinEff) <= EPS)

                                        weakest.Add(w2.gameObject);
                                }
                            }
                            if (weakest.Count > 0)
                                targetOverloadedWire = weakest[UnityEngine.Random.Range(0, weakest.Count)];
                            __instance.targetOverloadedWire = targetOverloadedWire;

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

                        // Add ZapComponent to overloaded target wire 
                        if (DlcManager.IsContentSubscribed("DLC3_ID"))
                        {
                            var hp = targetOverloadedWire.GetComponent<BuildingHP>();
                            if (hp != null && !hp.IsBroken)
                            {
                                ZapComponent zap = targetOverloadedWire.GetComponent<ZapComponent>();
                                zap ??= targetOverloadedWire.AddComponent<ZapComponent>();
                                zap.DecorativeBeamScale = 0.5f;
                                zap.DecorativeBeamOffset = new Vector3(0f, 0.5f, 0f);
                                zap.DamageBeamOffset = new Vector3(0f, -1f, 0f);
                                zap.SHOCK_RADIUS = 4f;
                                zap.StartZapping();
                            }
                        }
                        }

                        var overloadedNotification = __instance.overloadedNotification;

                        if (overloadedNotification == null && targetOverloadedWire != null)
                        {
                            __instance.timeOverloadNotificationDisplayed = 0f;
                            overloadedNotification = new Notification(MISC.NOTIFICATIONS.CIRCUIT_OVERLOADED.NAME, NotificationType.BadMinor, click_focus: targetOverloadedWire.transform);
                            __instance.overloadedNotification = overloadedNotification;
                            GameScheduler.Instance.Schedule("Power Tutorial", 2f, _ => Tutorial.Instance.TutorialMessage(Tutorial.TutorialMessages.TM_Power));
                            Game.Instance.FindOrAdd<Notifier>().Add(overloadedNotification);
                        }

                    }
                    __instance.timeOverloaded = timeOverloaded;
                }

                else
                {
               
                    if (DlcManager.IsContentSubscribed("DLC3_ID"))
                    {
                        var allWiresLocal = __instance.allWires;

                        if (allWiresLocal != null)
                        {
                            for (int i = 0; i < allWiresLocal.Count; i++)
                            {
                                var w = allWiresLocal[i];
                                if (w == null) continue;
                                var zap = w.GetComponent<ZapComponent>();
                                zap?.StopZapping();
                            }
                        }
                    }

                    float timeOverloaded = __instance.timeOverloaded;

                    timeOverloaded = Mathf.Max(0f, timeOverloaded - dt * 0.95f);
                    __instance.timeOverloaded = timeOverloaded;
                    float timeOverloadNotificationDisplayed = __instance.timeOverloadNotificationDisplayed;
                    timeOverloadNotificationDisplayed += dt;
                    if (timeOverloadNotificationDisplayed > 5f)
                    {
                        var overloadedNotification = __instance.overloadedNotification;

                        if (overloadedNotification != null)
                        {
                            Game.Instance.FindOrAdd<Notifier>().Remove(overloadedNotification);
                            __instance.overloadedNotification = null;

                        }
                        __instance.timeOverloadNotificationDisplayed = 0f;
                    }
                    else
                    {
                        __instance.timeOverloadNotificationDisplayed = timeOverloadNotificationDisplayed;
                    }
                }
                return false;
          
        }
    }
}