using System.Collections.Generic;
using HarmonyLib;
using STRINGS;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{
    /// <summary>
    /// The network limit, moved from Klei's six fixed buckets to a continuous per-item value.
    ///
    /// Every reader of a network's limit goes through two methods of ElectricalUtilityNetwork:
    /// UpdateOverloadTime (the sim, every 200 ms) and GetMaxSafeWattage (the overlay tint, the
    /// build hover card, the details panel and the power diagnostic, all through
    /// CircuitManager.GetMaxSafeWattageForCircuit). Those two are replaced; the per-item limits
    /// they read are cached once per spawn and folded into one minimum per network inside the
    /// rebuild passes vanilla already runs. Nothing runs per frame and nothing per wire per tick.
    ///
    /// A wire's base rating is read THROUGH Wire.GetMaxWattageAsFloat, never replaced, so the
    /// eleven top-500 mods that patch it (Bigger Capacity, Customize Buildings, ...) still set
    /// the base and the material factor multiplies whatever they return.
    /// </summary>
    public static class WattageCapacityLimits
    {
        // Per-network minimums, indexed by UtilityNetwork.id. Vanilla numbers its networks 0..n-1 as it
        // rebuilds them (UtilityNetworkManager.RebuildNetworks, id = networks.Count) and resets every old
        // network first, so a slot is cleared before its id is reused. 
        private static readonly List<float> wireMin = new List<float>();
        private static readonly List<float> bridgeMin = new List<float>();

        private static void Ensure(int id)
        {
            while (wireMin.Count <= id)
            {
                wireMin.Add(float.PositiveInfinity);
                bridgeMin.Add(float.PositiveInfinity);
            }
        }

        // Wires and wire bridges in the world, with their cached limit in watts.
        private static readonly Dictionary<KMonoBehaviour, float> itemLimit = new Dictionary<KMonoBehaviour, float>();

        private static readonly List<KMonoBehaviour> candidates = new List<KMonoBehaviour>();

        private static readonly AccessTools.FieldRef<CircuitManager, HashSet<WireUtilityNetworkLink>> bridgesRef =
            AccessTools.FieldRefAccess<CircuitManager, HashSet<WireUtilityNetworkLink>>("bridges");

        private static readonly AccessTools.FieldRef<ElectricalUtilityNetwork, float> timeOverloadedRef =
            AccessTools.FieldRefAccess<ElectricalUtilityNetwork, float>("timeOverloaded");

        private static readonly AccessTools.FieldRef<ElectricalUtilityNetwork, float> notificationTimeRef =
            AccessTools.FieldRefAccess<ElectricalUtilityNetwork, float>("timeOverloadNotificationDisplayed");

        private static readonly AccessTools.FieldRef<ElectricalUtilityNetwork, GameObject> targetRef =
            AccessTools.FieldRefAccess<ElectricalUtilityNetwork, GameObject>("targetOverloadedWire");

        private static readonly AccessTools.FieldRef<ElectricalUtilityNetwork, Notification> notificationRef =
            AccessTools.FieldRefAccess<ElectricalUtilityNetwork, Notification>("overloadedNotification");

        private delegate void RemoveNotificationFn(ElectricalUtilityNetwork net);

        private static readonly RemoveNotificationFn removeNotification =
            AccessTools.MethodDelegate<RemoveNotificationFn>(
                AccessTools.Method(typeof(ElectricalUtilityNetwork), "RemoveOverloadedNotification"));

        public static bool Active
        {
            get
            {
                if (Config.Instance.WireCapacity)
                    return true;
                else return false;
            }
        }

        /// <summary>Recompute every cached limit and rebuild the networks: a settings change is
        /// live within one power tick.</summary>
        public static void Refresh()
        {
            var keys = new List<KMonoBehaviour>(itemLimit.Keys);
            foreach (KMonoBehaviour item in keys)
            {
                if (item == null)
                {
                    itemLimit.Remove(item);
                    continue;
                }
                itemLimit[item] = Compute(item);
            }
            if (Game.Instance != null && Game.Instance.electricalConduitSystem != null)
            {
                Game.Instance.electricalConduitSystem.ForceRebuildNetworks();
            }
        }

        /// <summary>The limit of one wire or wire bridge in watts, as this mod rates it.</summary>
        public static float LimitOf(KMonoBehaviour item)
        {
            if (item == null)
            {
                return float.PositiveInfinity;
            }
            if (!itemLimit.TryGetValue(item, out float limit))
            {
                limit = Compute(item);
                itemLimit[item] = limit;
            }
            return limit;
        }

        /// <summary>The cached minimum of a network, or 0 for an empty network (vanilla's answer).</summary>
        public static float NetworkLimit(ElectricalUtilityNetwork net)
        {
            if (net == null || net.id < 0 || net.id >= wireMin.Count)
            {
                return 0f;
            }
            float min = Mathf.Min(wireMin[net.id], bridgeMin[net.id]);
            return float.IsPositiveInfinity(min) ? 0f : min;
        }

        private static float Compute(KMonoBehaviour item)
        {
            Wire.WattageRating rating;
            if (item is Wire wire)
            {
                rating = wire.MaxWattageRating;
            }
            else if (item is WireUtilityNetworkLink link)
            {
                rating = link.GetMaxWattageRating();
            }
            else
            {
                return float.PositiveInfinity;
            }
            PrimaryElement pe = item.GetComponent<PrimaryElement>();
            if (pe == null)
            {
                return Wire.GetMaxWattageAsFloat(rating);
            }
            KPrefabID kpid = item.GetComponent<KPrefabID>();
            return Rate(rating, kpid != null ? kpid.PrefabTag.Name : null, pe.ElementID);
        }

        /// <summary>The limit a wire or bridge of this rating and prefab would have if built from
        /// this element: the same rule the sim uses, for the build menu.</summary>
        public static float Rate(Wire.WattageRating rating, string prefabId, SimHashes element)
        {
            float baseWatts = Wire.GetMaxWattageAsFloat(rating);
            float typeMul = 1f;
        
            float factor = WireCapacitySettings.GetMultiplier(element) * typeMul;
            if (factor == 1f)
            {
                return baseWatts;
            }
            return Round(baseWatts * factor);
        }

        public static float Round(float watts)
        {
      
            float step = watts >= 10000f ? 100f : 10f;
            if (!(step > 0f))
            {
                return watts;
            }
            return Mathf.Floor(watts / step + 1e-4f) * step;
        }

        // ---------------------------------------------------------------- H1: cache per item

        [HarmonyPatch(typeof(Wire), "OnSpawn")]
        private static class Wire_OnSpawn
        {
            private static void Postfix(Wire __instance)
            {
                itemLimit[__instance] = Compute(__instance);
            }
        }

        [HarmonyPatch(typeof(Wire), "OnCleanUp")]
        private static class Wire_OnCleanUp
        {
            private static void Prefix(Wire __instance)
            {
                itemLimit.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(WireUtilityNetworkLink), "OnSpawn")]
        private static class Link_OnSpawn
        {
            private static void Postfix(WireUtilityNetworkLink __instance)
            {
                itemLimit[__instance] = Compute(__instance);
            }
        }

        [HarmonyPatch(typeof(UtilityNetworkLink), "OnCleanUp")]
        private static class Link_OnCleanUp
        {
            private static void Prefix(UtilityNetworkLink __instance)
            {
                if (__instance is WireUtilityNetworkLink)
                {
                    itemLimit.Remove(__instance);
                }
            }
        }

        // ------------------------------------------------- H2: wire minimum, in vanilla's pass

        [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.AddItem))]
        private static class Network_AddItem
        {
            private static void Postfix(ElectricalUtilityNetwork __instance, object item)
            {
                if (item is Wire wire)
                {
                    int id = __instance.id;
                    Ensure(id);
                    wireMin[id] = Mathf.Min(wireMin[id], LimitOf(wire));
                }
            }
        }

        [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.Reset))]
        private static class Network_Reset
        {
            private static void Postfix(ElectricalUtilityNetwork __instance)
            {
                int id = __instance.id;
                if (id >= 0 && id < wireMin.Count)
                {
                    wireMin[id] = float.PositiveInfinity;
                    bridgeMin[id] = float.PositiveInfinity;
                }
            }
        }

        // ------------------------------------------------------------- H3: bridge minimum

        [HarmonyPatch(typeof(CircuitManager), nameof(CircuitManager.Rebuild))]
        private static class CircuitManager_Rebuild
        {
            private static void Postfix(CircuitManager __instance)
            {
                var system = Game.Instance.electricalConduitSystem;
                IList<UtilityNetwork> all = system.GetNetworks();
                for (int i = 0; i < all.Count; i++)
                {
                    int id = all[i].id;
                    Ensure(id);
                    bridgeMin[id] = float.PositiveInfinity;
                }
                foreach (WireUtilityNetworkLink bridge in bridgesRef(__instance))
                {
                    if (bridge == null)
                    {
                        continue;
                    }
                    ushort id = __instance.GetCircuitID(bridge);
                    if (id == ushort.MaxValue)
                    {
                        continue;
                    }
                    if (system.GetNetworkByID(id) is ElectricalUtilityNetwork net)
                    {
                        Ensure(net.id);
                        bridgeMin[net.id] = Mathf.Min(bridgeMin[net.id], LimitOf(bridge));
                    }
                }
            }
        }

        // ------------------------------------------------------------ H5: the UI's answer

        [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.GetMaxSafeWattage))]
        private static class Network_GetMaxSafeWattage
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ElectricalUtilityNetwork __instance, ref float __result)
            {
                if (!Active)
                {
                    return true;
                }
                __result = NetworkLimit(__instance);
                return false;
            }
        }

        // ------------------------------------------------------------- H4: the overload tick

        /// <summary>
        /// Vanilla's UpdateOverloadTime line for line (ElectricalUtilityNetwork.cs:69-143,
        /// build 744825), with two changes: the overload test is one compare against the cached
        /// minimum instead of the six-bucket scan, and the damage target is drawn at random from
        /// EVERY wire and bridge whose own limit is below the load (owner ruling D4, the rule
        /// Stationeers' PowerTick.GetBreakableCables uses), with one Random.Range call per damage
        /// event as vanilla makes. The 6 s accumulator, the 0.95 decay, the sticky target, the
        /// notification and the 1 HP damage payload are unchanged.
        ///
        /// Priority.First (M5): Harmony skips a later bool prefix once an earlier one returned false
        /// (MethodCreator.AddPrefixes tests runOriginal before each one), so of two replacements the
        /// first to run wins. Giga-watt Wire replaces this method too; running first keeps the
        /// material rule in force whatever the load order, and its extra tiers still rate through
        /// GetMaxWattageAsFloat. Disabled, this returns true and the next prefix (or vanilla) runs.
        /// </summary>
        [HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.UpdateOverloadTime))]
        private static class Network_UpdateOverloadTime
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ElectricalUtilityNetwork __instance, float dt, float watts_used,
                List<WireUtilityNetworkLink>[] bridgeGroups)
            {
                if (!Active)
                {
                    return true;
                }
                ElectricalUtilityNetwork net = __instance;
                float limit = NetworkLimit(net);
                bool overloaded = limit > 0f && watts_used > limit + POWER.FLOAT_FUDGE_FACTOR;
                ref float timeOverloaded = ref timeOverloadedRef(net);
                if (overloaded)
                {
                    timeOverloaded += dt;
                    if (!(timeOverloaded > 6f))
                    {
                        return false;
                    }
                    timeOverloaded = 0f;
                    ref GameObject target = ref targetRef(net);
                    if (target == null)
                    {
                        candidates.Clear();
                        for (int g = 0; g < bridgeGroups.Length; g++)
                        {
                            List<WireUtilityNetworkLink> list = bridgeGroups[g];
                            if (list == null)
                            {
                                continue;
                            }
                            for (int i = 0; i < list.Count; i++)
                            {
                                WireUtilityNetworkLink b = list[i];
                                if (b != null && watts_used > LimitOf(b) + POWER.FLOAT_FUDGE_FACTOR)
                                {
                                    candidates.Add(b);
                                }
                            }
                        }
                        for (int i = 0; i < net.allWires.Count; i++)
                        {
                            Wire w = net.allWires[i];
                            if (w != null && watts_used > LimitOf(w) + POWER.FLOAT_FUDGE_FACTOR)
                            {
                                candidates.Add(w);
                            }
                        }
                        if (candidates.Count > 0)
                        {
                            target = candidates[Random.Range(0, candidates.Count)].gameObject;
                        }
                        candidates.Clear();
                    }
                    if (target != null)
                    {
                        target.BoxingTrigger(-794517298, new BuildingHP.DamageSourceInfo
                        {
                            damage = 1,
                            source = STRINGS.BUILDINGS.DAMAGESOURCES.CIRCUIT_OVERLOADED,
                            popString = UI.GAMEOBJECTEFFECTS.DAMAGE_POPS.CIRCUIT_OVERLOADED,
                            takeDamageEffect = SpawnFXHashes.BuildingSpark,
                            fullDamageEffectName = "spark_damage_kanim",
                            statusItemID = Db.Get().BuildingStatusItems.Overloaded.Id
                        });
                    }
                    ref Notification notification = ref notificationRef(net);
                    if (notification == null && target != null)
                    {
                        notificationTimeRef(net) = 0f;
                        notification = new Notification(MISC.NOTIFICATIONS.CIRCUIT_OVERLOADED.NAME, NotificationType.BadMinor,
                            null, null, expires: true, 0f, null, null, target.transform);
                        GameScheduler.Instance.Schedule("Power Tutorial", 2f, delegate
                        {
                            Tutorial.Instance.TutorialMessage(Tutorial.TutorialMessages.TM_Power);
                        });
                        Game.Instance.FindOrAdd<Notifier>().Add(notification);
                    }
                }
                else
                {
                    timeOverloaded = Mathf.Max(0f, timeOverloaded - dt * 0.95f);
                    ref float shown = ref notificationTimeRef(net);
                    shown += dt;
                    if (shown > 5f)
                    {
                        removeNotification(net);
                    }
                }
                return false;
            }
        }
    }
}
