using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TUNING;
using UnityEngine;

namespace Rephysicalized.Patches
{
    // Shared bootstrap for creating the planted/preview prefabs
    internal static class PinkRockCarvedBootstrap
    {
        private const string SeedId = "PinkRockCarved";
        public const string PlantedId = "PinkRockCarved_Planted";
        public const string PreviewId = "PinkRockCarved_Planted_Preview";

        private static bool created;

        // Ensure both planted and preview prefabs exist; safe to call multiple times
        public static void EnsurePlantedAndPreviewPrefabs()
        {
            if (!Dlc2Gate.Enabled)
                return;

            // If already created (and Assets contains them), skip
            if (created && Assets.TryGetPrefab(PlantedId) != null && Assets.TryGetPrefab(PreviewId) != null)
                return;

            bool plantedCreated = Assets.TryGetPrefab(PlantedId) != null;
            bool previewCreated = Assets.TryGetPrefab(PreviewId) != null;

            // Planted (decor, light, placed entity)
            if (!plantedCreated)
            {
                KAnimFile anim = Assets.GetAnim((HashedString)"pinkrock_decor_kanim");

                {
                    EffectorValues decor = BUILDINGS.DECOR.BONUS.TIER3;
                    EffectorValues noise = NOISE_POLLUTION.NOISY.TIER0;

                    var planted = EntityTemplates.CreatePlacedEntity(
                        id: PlantedId,
                        name: STRINGS.BUILDINGS.PINKROCK_PLANTED.NAME,
                        desc: STRINGS.BUILDINGS.PINKROCK_PLANTED.DESC,
                        mass: 25f,
                        anim: anim,
                        initialAnim: "idle",
                        sceneLayer: Grid.SceneLayer.BuildingBack,
                        width: 1,
                        height: 1,
                        decor: decor,
                        noise: noise,
                        element: SimHashes.Granite,
                        additionalTags: null,
                        defaultTemperature: 293.15f
                    );

                    // Occupy a building layer so it behaves like a placed decor
                    planted.AddOrGet<OccupyArea>().objectLayers = new ObjectLayer[1] { ObjectLayer.Building };

                    // Pink Rock light component
                    var light = planted.AddOrGet<Light2D>();
                    light.overlayColour = LIGHT2D.PINKROCK_COLOR;
                    light.Color = LIGHT2D.PINKROCK_COLOR;
                    light.Range = 3f;
                    light.Angle = 0f;
                    light.Direction = LIGHT2D.PINKROCK_DIRECTION;
                    light.Offset = new Vector2(0.05f, -0.5f);
                    light.shape = LightShape.Circle;
                    light.drawOverlay = true;
                    light.enabled = true;
                    light.Lux = 1000;

                    planted.AddOrGet<KSelectable>();
                    planted.AddOrGet<Prioritizable>();
                    planted.AddOrGet<Uprootable>();
                    planted.AddOrGet<PinkRockPlantedReturner>(); // return seed on uproot

                    var kbac2 = planted.GetComponent<KBatchedAnimController>();
                    kbac2.isMovable = false;
                    kbac2.sceneLayer = Grid.SceneLayer.BuildingBack;

                    var pe = planted.AddOrGet<PrimaryElement>();
                    if (pe.ElementID == SimHashes.Vacuum) pe.SetElement(SimHashes.Granite);
                    pe.Mass = 25f;

                    // Register prefab and mark as building + light source so room constraints see it
                    var kpid = planted.AddOrGet<KPrefabID>();
                    kpid.PrefabTag = new Tag(PlantedId);
                    kpid.AddTag(GameTags.RoomProberBuilding, true);
                    kpid.AddTag(RoomConstraints.ConstraintTags.LightSource, true);
                    Assets.AddPrefab(kpid);

                    plantedCreated = true;
                }
            }

            // Preview for seed planting visualization
            if (!previewCreated)
            {
                KAnimFile animPrev = Assets.GetAnim((HashedString)"pinkrock_decor_kanim");
                if (animPrev == null)
                {
                    var plantedPrefab = Assets.TryGetPrefab(PlantedId);
                    var kbacPlanted = plantedPrefab ? plantedPrefab.GetComponent<KBatchedAnimController>() : null;
                    if (kbacPlanted != null && kbacPlanted.AnimFiles != null && kbacPlanted.AnimFiles.Length > 0)
                        animPrev = kbacPlanted.AnimFiles[0];
                }

                if (animPrev != null)
                {
                    var preview = EntityTemplates.CreateAndRegisterPreview(
                        id: PreviewId,
                        anim: animPrev,
                        initial_anim: "idle",
                        object_layer: ObjectLayer.Building,
                        width: 1,
                        height: 1
                    );

                    var kbacPrev = preview.GetComponent<KBatchedAnimController>();
                    if (kbacPrev != null) kbacPrev.sceneLayer = Grid.SceneLayer.BuildingFront;

                    previewCreated = true;
                }
            }

            // Only mark done when both exist
            created = plantedCreated && previewCreated;
        }
    }

    // Ensure prefabs exist once the core prefab list has been created (Postfix; anims should now be available)
    [HarmonyPatch(typeof(Assets), nameof(Assets.CreatePrefabs))]
    internal static class PinkRockCarved_Planted_Register
    {
        private static bool Prepare() => Dlc2Gate.Enabled;

        private static void Postfix()
        {
            PinkRockCarvedBootstrap.EnsurePlantedAndPreviewPrefabs();
        }
    }

    public sealed class PinkRockPlantedReturner : KMonoBehaviour
    {
        private static readonly Tag SeedTag = new Tag("PinkRockCarved");

        public override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe((int)GameHashes.Uprooted, OnUprooted);
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.Uprooted, OnUprooted);
            base.OnCleanUp();
        }

        private void OnUprooted(object _)
        {
            GameObject self = gameObject;

            // Spawn the loose seed back
            int cell = Grid.PosToCell(self.transform.GetPosition());
            if (Grid.IsValidCell(cell))
            {
                var seedPrefab = Assets.TryGetPrefab(SeedTag);
                if (seedPrefab != null)
                {
                    var dropped = Util.KInstantiate(seedPrefab);
                    dropped.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore));
                    dropped.SetActive(true);

                    var srcPe = seedPrefab.GetComponent<PrimaryElement>();
                    var dstPe = dropped.GetComponent<PrimaryElement>();
                    if (srcPe != null && dstPe != null)
                    {
                        dstPe.Mass = Mathf.Max(0.1f, srcPe.Mass);
                        dstPe.Temperature = srcPe.Temperature;
                    }
                }
            }
            // Destroy the planted occupant to prevent duplication
            Util.KDestroyGameObject(self);
        }
    }

    // Make PinkRockCarved plant the placed occupant and use the preview
    [HarmonyPatch(typeof(PinkRockCarvedConfig), nameof(PinkRockCarvedConfig.CreatePrefab))]
    internal static class PinkRockCarved_AsDecorSeed_Patch
    {
        private static bool Prepare() => Dlc2Gate.Enabled;

        private static void Postfix(GameObject __result)
        {
            PinkRockCarvedBootstrap.EnsurePlantedAndPreviewPrefabs();

            var kpid = __result.AddOrGet<KPrefabID>();
            kpid.AddTag(GameTags.DecorSeed, true); // vase/planter will accept it

            // Ensure seed mass/element
            var pe = __result.AddOrGet<PrimaryElement>();
            if (pe.ElementID == SimHashes.Vacuum)
                pe.SetElement(SimHashes.Granite);
            pe.Mass = 25f;

            __result.AddOrGet<Pickupable>(); // keep it a proper loose item

            var ps = __result.AddOrGet<PlantableSeed>();
            ps.PlantID = new Tag(PinkRockCarvedBootstrap.PlantedId); // spawn the placed occupant (not the seed)
            ps.PreviewID = new Tag(PinkRockCarvedBootstrap.PreviewId);
            ps.direction = SingleEntityReceptacle.ReceptacleDirection.Top;
            ps.replantGroundTag = new Tag("NeverSelfPlant");
        }
    }

    // skip PlanterSideScreen auto-restore to avoid UI assumptions for our special occupant.
    [HarmonyPatch(typeof(PlanterSideScreen), "RestoreSelectionFromOccupant")]
    internal static class PlanterSideScreen_RestoreSelectionFromOccupant_SafeNoop
    {
        private static bool Prepare() => Dlc2Gate.Enabled;

        private static bool Prefix() => false;
    }

    // Transpile ConfigureBuildingTemplate for FlowerVaseHangingFancy:
    // Replace any assignment to PlantablePlot.plantLayer with GasFront.
    [HarmonyPatch(typeof(FlowerVaseHangingFancyConfig), nameof(FlowerVaseHangingFancyConfig.ConfigureBuildingTemplate))]
    internal static class FlowerVaseHangingFancyConfig_Transpile_PlantLayer
    {
        private static bool Prepare() => Dlc2Gate.Enabled;

        private static readonly int GasFrontValue = (int)Grid.SceneLayer.GasFront;
        private static readonly System.Reflection.FieldInfo PlantLayerField =
            AccessTools.Field(typeof(PlantablePlot), nameof(PlantablePlot.plantLayer));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (PlantLayerField == null)
                return instructions;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];
                if (instr.opcode == OpCodes.Stfld && Equals(instr.operand, PlantLayerField))
                {
                    int prev = i - 1;
                    if (prev >= 0)
                    {
                        // Overwrite the value being assigned to GasFront
                        codes[prev] = new CodeInstruction(OpCodes.Ldc_I4, GasFrontValue);
                    }
                }
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class RoomConstraints_LightCountsPlants_Patch
    {
        private static void Postfix()
        {
            // Get the existing LIGHT constraint instance; do not replace the instance to keep RoomType references intact.
            var light = RoomConstraints.LIGHT;
            if (light == null)
                return;

            light.room_criteria = room =>
            {
                // Original behavior: any creature with a Light2D
                foreach (var creature in room.cavity.creatures)
                {
                    if (creature != null && creature.GetComponent<Light2D>() != null)
                        return true;
                }

                // Original behavior: any building with Light2D that's enabled or has satisfied RequireInputs
                foreach (var building in room.buildings)
                {
                    if (building == null) continue;

                    var l = building.GetComponent<Light2D>();
                    if (l != null)
                    {
                        var req = building.GetComponent<RequireInputs>();
                        if (l.enabled || (req != null && req.RequirementsMet))
                            return true;
                    }
                }

                // NEW: Consider planted occupants (plants) as potential light sources
                foreach (var plant in room.plants)
                {
                    if (plant == null) continue;

                    // Primary: Light2D enabled or gated by RequireInputs (mirrors building logic)
                    var l = plant.GetComponent<Light2D>();
                    if (l != null)
                    {
                        var req = plant.GetComponent<RequireInputs>();
                        if (l.enabled || (req != null && req.RequirementsMet))
                            return true;
                    }

                    // Secondary: explicitly tagged as a light source (e.g., PinkRockCarved_Planted with LightSource tag)
                    if (plant.HasTag(RoomConstraints.ConstraintTags.LightSource))
                        return true;
                }

                return false;
            };
        }
    }
}