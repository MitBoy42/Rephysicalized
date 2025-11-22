using Database;
using HarmonyLib;
using Rephysicalized.Chores;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Rephysicalized
{
    internal static class GlomSlimeLog
    {
        public const string Prefix = "[Rephysicalized][GlomSlime]";
    }

    // Inject SolidFuelStates.Def into Glom's chore table chain before IdleStates.Def (no fallback, no logging).
    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]
    internal static class Glom_SolidFuel_Transpiler
    {
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SolidFuelStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase _)
        {
            var list = new List<CodeInstruction>(instructions);

            var idleCtor = AccessTools.Constructor(typeof(IdleStates.Def), Type.EmptyTypes);
            var inject = AccessTools.Method(typeof(Glom_SolidFuel_Transpiler), nameof(Inject));
            if (idleCtor == null || inject == null)
                return list;

            // Insert our Inject(builder) right before new IdleStates.Def()
            for (int i = 0; i < list.Count; i++)
            {
                var ci = list[i];
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == idleCtor)
                {
                    list.Insert(i, new CodeInstruction(OpCodes.Call, inject));
                    break;
                }
            }
            return list;
        }
    }

    // Post-configure Glom prefab with FueledDiet (inputs only) and SolidFuelMonitor. Adds assimilation component.
    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]
    internal static class Glom_FueledDiet_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
    

            // FueledDiet: inputs only, no conversions
            var diet = BuildGlomFueledDiet();

            // Attach FueledDiet via controller
            var controller = __result.AddOrGet<FueledDietController>();
            controller.Configure(diet);
            controller.RefillThreshold = 40f;

            // Solid fuel monitor drives the SOLIDFUEL chore/state; set navigator to 1x1
            var monitor = __result.AddOrGetDef<SolidFuelMonitor.Def>();
            monitor.navigatorSize = new Vector2(1f, 1f);
            monitor.possibleEatPositionOffsets = new[] { Vector3.zero };

            // Assimilation: consume stored fuel and add to Glom mass
            __result.AddOrGet<GlomAssimilationConsumer>();

            // Optional: register for external lookup if your monitor uses a registry fallback
            var kpid = __result.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                try { FueledDietRegistry.Register(kpid.PrefabTag, diet); } catch { }
            }
        }

        // Fueled diet: inputs only (no conversions). 
        private static FueledDiet BuildGlomFueledDiet()
        {
            var inputs = new List<FuelInput>
            {
                new FuelInput("IceBellyPoop"),
                new FuelInput(SimHashes.Mud.CreateTag()),
                new FuelInput(SimHashes.Gunk.CreateTag()),
                   new FuelInput("RotPile"),
   new FuelInput(SimHashes.Rust.CreateTag()),


            };
            var seen = new HashSet<Tag>(inputs.Select(fi => fi.ElementTag));

            var loadedFoods = EdiblesManager.GetAllLoadedFoodTypes();

            foreach (var food in loadedFoods)
            {
                if (food == null || string.IsNullOrWhiteSpace(food.Id))
                    continue;

                var tag = new Tag(food.Id); // FoodInfo.Id is the prefab/food tag id
                if (!tag.IsValid)
                    continue;

                if (seen.Add(tag))
                    inputs.Add(new FuelInput(tag));
            }


            return new FueledDiet(
                fuelInputs: inputs,
                conversions: null,
                totalFuelCapacityKg: 60f
            );
        }
    }

    public sealed class GlomAssimilationConsumer : KMonoBehaviour, ISim1000ms
    {
        // Rate at which Glom assimilates stored fuel into body mass (kg/s)
        public float kgPerSecond = 0.03f;

        private FueledDietController controller;
        private Storage storage; // Fuel storage owned by the controller
        private PrimaryElement bodyPe;
        private CreatureMassTracker massTracker;

        private const float EPS = 1e-6f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            controller = gameObject.GetComponent<FueledDietController>();
            storage = controller != null ? controller.FuelStorage : null;
            bodyPe = gameObject.GetComponent<PrimaryElement>();
            massTracker = gameObject.GetComponent<CreatureMassTracker>();
        }

        public override void OnCleanUp()
        {
            // No contribution bookkeeping required in the simplified tracker
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {

            float remaining = kgPerSecond * Mathf.Max(0f, dt);
            if (remaining <= EPS)
                return;

            var items = storage.items;
            if (items == null || items.Count == 0)
                return;

            float gained = 0f;

            // Iterate backwards because we may destroy items
            for (int i = items.Count - 1; i >= 0 && remaining > EPS; i--)
            {
                var go = items[i];
                if (go == null)
                    continue;

                var pe = go.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass <= EPS)
                    continue;

                // Only assimilate solids (fuel). Never consume gases/liquids here.
                var elem = ElementLoader.FindElementByHash(pe.ElementID);
                if (elem == null || !elem.IsSolid)
                    continue;

                float take = Mathf.Min(remaining, pe.Mass);
                if (take <= EPS)
                    continue;

                // Deduct mass in-place; do not store the split back into storage.
                pe.Mass -= take;
                remaining -= take;
                gained += take;

                // If the source chunk runs out, destroy it
                if (pe.Mass <= EPS)
                {
                    Util.KDestroyGameObject(go);
                }
            }

            if (gained > EPS)
            {
                {
                    // Fallback if tracker missing (should be rare)
                    bodyPe.Mass = Mathf.Max(0.001f, bodyPe.Mass + gained);
                }
            }
        }
    
}
        internal static class GlomGridCompat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(int cell) => Grid.IsValidCell(cell);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldId(int cell) => Grid.IsValidCell(cell) ? (int)Grid.WorldIdx[cell] : -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SameWorld(int a, int b)
        {
            if (!Grid.IsValidCell(a) || !Grid.IsValidCell(b)) return false;
            return Grid.WorldIdx[a] == Grid.WorldIdx[b];
        }
    }

    // Inject SlimeArtillery state into Glom's chore table before Idle (no fallback, no logging).
    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]
    internal static class Glom_SlimeArtillery_Transpiler
    {
        private static ChoreTable.Builder Inject(ChoreTable.Builder builder)
        {
            return builder.Add(new SlimeArtilleryStates.Def());
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase _)
        {
            var list = new List<CodeInstruction>(instructions);

            var idleCtor = AccessTools.Constructor(typeof(IdleStates.Def), Type.EmptyTypes);
            var inject = AccessTools.Method(typeof(Glom_SlimeArtillery_Transpiler), nameof(Inject));
            if (idleCtor == null || inject == null)
                return list;

            for (int i = 0; i < list.Count; i++)
            {
                var ci = list[i];
                if (ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == idleCtor)
                {
                    list.Insert(i, new CodeInstruction(OpCodes.Call, inject));
                    break;
                }
            }

            return list;
        }
    }

    // Ensure the monitor is present on Glom
    [HarmonyPatch(typeof(GlomConfig), nameof(GlomConfig.CreatePrefab))]
    internal static class Glom_SlimeArtillery_Postfix
    {
        [HarmonyPostfix]
        private static void Post(ref GameObject __result)
        {
            __result.AddOrGetDef<SlimeArtilleryMonitor.Def>();
        
        }
    }

    // Monitor: acquires a dupe target within range & LOS, ensures Glom body mass >= 35 kg, toggles behaviour (no cooldown)
    public sealed class SlimeArtilleryMonitor : GameStateMachine<SlimeArtilleryMonitor, SlimeArtilleryMonitor.Instance, IStateMachineTarget, SlimeArtilleryMonitor.Def>
    {
        public const float RequiredKg = 48f; // minimum body mass to fire

        public sealed class Def : BaseDef
        {
            public int rangeTiles = 16;
            public float requiredFuelKg = RequiredKg; // interpreted as required body mass
        }

        public sealed class Instance : GameInstance
        {
            public GameObject targetDupe;
            internal bool lastGate; // for logging state changes
            
            public Instance(IStateMachineTarget master, Def def) : base(master, def) { }

            public void ClearTarget()
            {
                if (targetDupe != null)
                    Debug.Log($"{GlomSlimeLog.Prefix} Clearing target");
                targetDupe = null;
            }

            // Gate now checks Glom body mass instead of stored fuel
            public bool HasEnoughFuel()
            {
                var pe = gameObject.GetComponent<PrimaryElement>();
                if (pe == null) return false;
                return pe.Mass >= def.requiredFuelKg - 1e-4f;
            }
        }

        private State scanning;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = scanning;

            // Toggle behaviour tag while gate is satisfied (no cooldown)
            root.ToggleBehaviour(SlimeArtilleryStates.WantsToFire, smi =>
            {
                bool allowed = smi.targetDupe != null && smi.HasEnoughFuel();
                // Log only on gate change to avoid spam
                if (allowed != smi.lastGate)
                {
                 //   Debug.Log($"{GlomSlimeLog.Prefix} Gate {(allowed ? "ON" : "OFF")} (target={(smi.targetDupe != null)}, bodyMassOK={smi.HasEnoughFuel()})");
                    smi.lastGate = allowed;
                }
                return allowed;
            });

            scanning.PreBrainUpdate(Scan);
        }

        private static void Scan(Instance smi)
        {
            // Validate existing target
            if (smi.targetDupe != null && !IsValidTarget(smi, smi.targetDupe))
            {
       
                smi.targetDupe = null;
            }

            if (smi.targetDupe != null) return;

            GameObject best = null;
            int bestDist = int.MaxValue;

            int myCell = Grid.PosToCell(smi.gameObject);
            foreach (var id in Components.LiveMinionIdentities.Items)
            {
                if (id == null || id.gameObject == null) continue;

                int dupeCell = Grid.PosToCell(id.gameObject);
                if (!GlomGridCompat.IsValid(myCell) || !GlomGridCompat.IsValid(dupeCell)) continue;
                if (!GlomGridCompat.SameWorld(myCell, dupeCell)) continue;
                if (!IsValidTarget(smi, id.gameObject)) continue;

                int d = Grid.GetCellDistance(myCell, dupeCell);
                if (d < bestDist)
                {
                    best = id.gameObject;
                    bestDist = d;
                }
            }

            if (best != null)
     //           Debug.Log($"{GlomSlimeLog.Prefix} Acquired target: {best.name} at d={bestDist}");
            smi.targetDupe = best;
        }

        private static bool IsValidTarget(Instance smi, GameObject dupe)
        {
            if (dupe == null || dupe.GetComponent<MinionIdentity>() == null) return false;

            int a = Grid.PosToCell(smi.gameObject);
            int b = Grid.PosToCell(dupe);
            if (!GlomGridCompat.IsValid(a) || !GlomGridCompat.IsValid(b)) return false;
            if (!GlomGridCompat.SameWorld(a, b)) return false;

            if (Grid.GetCellDistance(a, b) > smi.def.rangeTiles) return false;

            Grid.CellToXY(a, out int ax, out int ay);
            Grid.CellToXY(b, out int bx, out int by);
            bool los = Grid.FastTestLineOfSightSolid(ax, ay, bx, by);
            return los;
        }
    }

    // States: play 'harvest' during aiming, immediately kill Glom on firing, spawn comet after death (sim-timed), with constant animScale and correct angle.
    public sealed class SlimeArtilleryStates : GameStateMachine<SlimeArtilleryStates, SlimeArtilleryStates.Instance, IStateMachineTarget, SlimeArtilleryStates.Def>
    {
        public static readonly Tag WantsToFire = TagManager.Create("GlomWantsToFireSlime");

        public sealed class Def : BaseDef
        {
            public string harvestAnim = "harvest";   // visual during aiming
            public float fireSpeedMultiplier = 1f;
            public float spawnAfterDeathDelay = 2f;  // seconds after killing Glom to spawn comet (sim time)
        }

        public sealed class Instance : GameInstance
        {
            public KBatchedAnimController kbac;
            public float cachedMass;

            // guard to prevent duplicate scheduling
            public bool hasScheduled;

            public Instance(Chore<Instance> chore, Def def) : base(chore, def)
            {
                gameObject.TryGetComponent(out kbac);
                chore.AddPrecondition(ChorePreconditions.instance.CheckBehaviourPrecondition, WantsToFire);
            }
        }

        private State aiming, firing;

        public override void InitializeStates(out BaseState default_state)
        {
            default_state = aiming;

            aiming
                .Enter(smi =>
                {
                    Debug.Log($"{GlomSlimeLog.Prefix} Enter aiming (harvest)");
                    var mon = smi.gameObject.GetSMI<SlimeArtilleryMonitor.Instance>();
                    if (mon?.targetDupe != null)
                        smi.GetComponent<Facing>()?.Face(mon.targetDupe.transform.position.x);

                    // Play harvest unconditionally on kbac if present (no fallback)
                    if (smi.kbac != null)
                    {
                        smi.kbac.PlaySpeedMultiplier = smi.def.fireSpeedMultiplier;
                        smi.kbac.Play(smi.def.harvestAnim, KAnim.PlayMode.Once);
                        Debug.Log($"{GlomSlimeLog.Prefix} Playing anim: {smi.def.harvestAnim}");
                    }

                    var pe = smi.gameObject.GetComponent<PrimaryElement>();
                    smi.cachedMass = pe != null ? pe.Mass : 0f;
                    smi.hasScheduled = false;

                    smi.GoTo(firing);
                });

            firing
                .Enter(smi =>
                {
                    // Immediately kill and schedule comet spawn (no explosion delay)
                    KillAndScheduleComet(smi);
                });
        }

        // Kills Glom immediately, then schedules comet spawn later via a helper that outlives Glom
        private static void KillAndScheduleComet(Instance smi)
        {
            if (smi == null || smi.gameObject == null) return;
            if (smi.hasScheduled) return;

            var mon = smi.gameObject.GetSMI<SlimeArtilleryMonitor.Instance>();
            var pe = smi.gameObject.GetComponent<PrimaryElement>();

            // Spawn origin offset +0.4 cells upward
            var from = smi.transform.position + new Vector3(0f, 0.8f, 0f);
            var to = mon?.targetDupe != null ? mon.targetDupe.transform.position : from + Vector3.down;

            float mass = pe != null ? Mathf.Max(0f, pe.Mass) : Mathf.Max(0f, smi.cachedMass);
            if (mass <= 0f) mass = SlimeArtilleryMonitor.RequiredKg;

            float afterDeathDelay = smi.def.spawnAfterDeathDelay;


            // set flag first to prevent re-entry
            smi.hasScheduled = true;

            // clear target now to reduce chance of starting another chore before death completes
            mon?.ClearTarget();

            // Proxy spawner
            var proxyGO = new GameObject("GlomCometSpawnProxy");
            var proxy = proxyGO.AddComponent<DelayedCometSpawner>();
            proxy.DelaySeconds = afterDeathDelay;
            proxy.From = from;
            proxy.To = to;
            proxy.MassKg = mass;

            // Kill Glom (plays death animation/explosion if applicable)
            try
            {
                var death = smi.gameObject.GetSMI<DeathMonitor.Instance>();
                if (death != null)
                {
                    death.Kill(Db.Get().Deaths.Generic);
                }
                else
                {
                    Util.KDestroyGameObject(smi.gameObject);
                }
            }
            catch
            {
                Util.KDestroyGameObject(smi.gameObject);
            }
        }

        // In SlimeArtilleryStates class
        private const string SLIME_COMET_ID = "SlimeComet"; // keep as-is

        // Spawns a comet with specified mass; scale via KBatchedAnimController.animScale and set angle via kbac.Rotation.
        internal static void LaunchSlimeComet(Vector3 from, Vector3 to, float cometMassKg)
        {
            var prefab = Assets.GetPrefab(new Tag(SLIME_COMET_ID));
        
            var go = Util.KInstantiate(prefab);
            go.transform.SetPosition(from);
            go.SetActive(true);

            var comet = go.GetComponent<Comet>();
            var pe = go.GetComponent<PrimaryElement>();
            var kbac = go.GetComponent<KBatchedAnimController>();
       

            // Force payload to specified mass and clamp any mass ranges (version-safe)
            pe.Mass = cometMassKg;
            TrySetField(comet, "massRange", new Vector2(cometMassKg, cometMassKg));
            TrySetField(comet, "explosionMass", cometMassKg);
            TrySetField(comet, "oreMassRange", new Vector2(cometMassKg, cometMassKg));
            TrySetField(comet, "totalMass", cometMassKg);

            // Direction and velocity
            Vector3 dir3 = (to - from);
            if (dir3.sqrMagnitude < 0.01f) dir3 = Vector3.down;
            dir3.Normalize();
            float speed = 6f;
            comet.Velocity = new Vector2(dir3.x, dir3.y) * speed;
            comet.canHitDuplicants = false;

            // Orientation and scale: prefer KAnim controller
            float angleDeg = Mathf.Atan2(dir3.y, dir3.x) * Mathf.Rad2Deg + 90f; // +90 to align sprite forward; adjust if needed
            if (kbac != null)
            {
                kbac.Rotation = angleDeg;
                kbac.animScale = 0.003f; // constant size independent of angle
            }

            // Optional: enable comet auto-rotation to velocity if your version supports it
            TrySetField(comet, "rotateToVelocity", true);
            TrySetField(comet, "rotateWithVelocity", true);

        }
        private static void TrySetField(object obj, string fieldName, object value)
        {
            try
            {
                var f = AccessTools.Field(obj.GetType(), fieldName);
                if (f == null) return;
                var ft = f.FieldType;

                if (value is Vector2 v2)
                {
                    if (ft == typeof(Vector2))
                    {
                        f.SetValue(obj, v2);
                  //      Debug.Log($"{GlomSlimeLog.Prefix} Set {fieldName}=({v2.x},{v2.y})");
                    }
                    else if (ft.FullName?.Contains("FloatRange") == true)
                    {
                        var fr = Activator.CreateInstance(ft);
                        var minF = AccessTools.Field(ft, "min");
                        var maxF = AccessTools.Field(ft, "max");
                        if (minF != null && maxF != null)
                        {
                            minF.SetValue(fr, v2.x);
                            maxF.SetValue(fr, v2.y);
                            f.SetValue(obj, fr);
                    //        Debug.Log($"{GlomSlimeLog.Prefix} Set {fieldName}.min/max=({v2.x},{v2.y})");
                        }
                    }
                }
                else
                {
                    if (ft.IsAssignableFrom(value.GetType()))
                    {
                        f.SetValue(obj, value);
               //         Debug.Log($"{GlomSlimeLog.Prefix} Set {fieldName}={value}");
                    }
                }
            }
            catch { }
        }
    }

    // Helper component that survives Glom's destruction and spawns the comet later (sim-based timing)
    public sealed class DelayedCometSpawner : KMonoBehaviour
    {
        public float DelaySeconds;
        public Vector3 From;
        public Vector3 To;
        public float MassKg;

        public override void OnSpawn()
        {
            base.OnSpawn();
            StartCoroutine(SpawnLater());
        }

        private IEnumerator SpawnLater()
        {
            yield return new WaitForSeconds(DelaySeconds);
            SlimeArtilleryStates.LaunchSlimeComet(From, To, MassKg);
            Util.KDestroyGameObject(gameObject);
        }
    }
}