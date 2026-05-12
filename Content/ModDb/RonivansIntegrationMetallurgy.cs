using HarmonyLib;
using Klei.AI;
using Rephysicalized.Content.System_Patches;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace Rephysicalized.Content.ModDb
{
    // Ronivans integrations - BasicOilRefinery dynamic patch + Chemical_AdvancedKiln CrudByproduct spawn (all recipes, like Kiln ash)
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class RonivansIntegrationMetallurgy
    {
        private static void Postfix()
        {
            // call shared apply so patching can be triggered either via Harmony or manually
            ApplyPatches();
        }

        public static void ApplyPatches()
        {
            Debug.Log("[Rephysicalized] RonivansIntegration.ApplyPatches running");
            // BasicOilRefinery ConfigureBuildingTemplate postfix
            try
            {
                var targetType = Type.GetType("Metallurgy.Buildings.Metallurgy_BasicOilRefineryConfig, RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("Metallurgy.Buildings.Metallurgy_BasicOilRefineryConfig");
                if (targetType == null)
                {
                    Debug.Log("[Rephysicalized] BasicOilRefinery type not found; skipping patch.");
                }
                else
                {
                    var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                              ?? AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                    if (method == null)
                        Debug.Log("[Rephysicalized] BasicOilRefinery.ConfigureBuildingTemplate method not found; skipping.");
                    else
                    {
                        var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.BasicOilRefinery");
                        var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationMetallurgy), nameof(BasicOilRefinery_NaphthaOutputPatch)));
                        harmony.Patch(method, postfix: postfix);
                        Debug.Log("[Rephysicalized] Patched BasicOilRefinery ConfigureBuildingTemplate.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Rephysicalized] Exception patching BasicOilRefinery: {e.Message}");
            }

            // Patch PlasmaFurnace ConfigureBuildingTemplate to tweak the ports directly
            try
            {
                var plasmaType = Type.GetType("Metallurgy.Buildings.Metallurgy_PlasmaFurnaceConfig, RonivansLegacy_ChemicalProcessing")
                              ?? AccessTools.TypeByName("Metallurgy.Buildings.Metallurgy_PlasmaFurnaceConfig");
                if (plasmaType == null)
                {
                    Debug.Log("[Rephysicalized] PlasmaFurnace type not found; skipping patch.");
                }
                else
                {
                    var method = AccessTools.Method(plasmaType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                              ?? AccessTools.Method(plasmaType, "ConfigureBuildingTemplate");
                    if (method == null)
                        Debug.Log("[Rephysicalized] PlasmaFurnace.ConfigureBuildingTemplate method not found; skipping.");
                    else
                    {
                        var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.PlasmaFurnace");
                        var postfix = new HarmonyMethod(AccessTools.Method(typeof(RonivansIntegrationMetallurgy), nameof(PlasmaFurnace_ConfigurePatch)));
                        harmony.Patch(method, postfix: postfix);
                        Debug.Log("[Rephysicalized] Applied PlasmaFurnace ConfigureBuildingTemplate postfix patch.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[Rephysicalized] Exception while patching PlasmaFurnace: {e.Message}");
            } }

        // Postfix for PlasmaFurnace.ConfigureBuildingTemplate - add additional allowed tags to its ports
        private static void PlasmaFurnace_ConfigurePatch(GameObject go, Tag prefab_tag)
        {
           
            if (go == null) return;
                // Find waste output port dispenser and optional exhausts by type name
                var dispensers = go.GetComponents<Component>();
                foreach (var c in dispensers)
                {
                    var t = c.GetType();
                    if (t.Name == "PipedConduitDispenser")
                    {
                        // elementFilter or tagFilter
                        var fi = t.GetField("elementFilter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi != null)
                        {
                            var valObj = fi.GetValue(c);
                            var adds = new[] { SimHashes.LiquidOxygen.CreateTag(), SimHashes.Water.CreateTag(), SimHashes.LiquidSulfur.CreateTag() };

                            // If the field stores Tag[]
                            if (valObj is Tag[] existingTags)
                            {
                                var list = existingTags.ToList();
                                foreach (var a in adds) if (!list.Contains(a)) list.Add(a);
                                fi.SetValue(c, list.ToArray());
                            }
                            else if (valObj is SimHashes[] existingSim)
                            {
                                // convert adds (Tags) to SimHashes where possible and merge
                                var sims = existingSim.ToList();
                                foreach (var a in adds)
                                {
                                    var sh = TagToSimHash(a);
                                    if (sh.HasValue && !sims.Contains(sh.Value)) sims.Add(sh.Value);
                                }
                                fi.SetValue(c, sims.ToArray());
                            }
                            else if (valObj == null)
                            {
                                var fieldType = fi.FieldType;
                                if (fieldType == typeof(Tag[]))
                                {
                                    fi.SetValue(c, adds.ToArray());
                                }
                                else if (fieldType == typeof(SimHashes[]))
                                {
                                    var sims = new List<SimHashes>();
                                    foreach (var a in adds)
                                    {
                                        var sh = TagToSimHash(a);
                                        if (sh.HasValue && !sims.Contains(sh.Value)) sims.Add(sh.Value);
                                    }
                                    fi.SetValue(c, sims.ToArray());
                                }
                            }
                        }
                        var fi2 = t.GetField("tagFilter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi2 != null)
                        {
                            var valObj = fi2.GetValue(c);
                            var adds = new[] { SimHashes.LiquidOxygen.CreateTag(), SimHashes.Water.CreateTag(), SimHashes.LiquidSulfur.CreateTag() };
                            if (valObj is Tag[] existingTags2)
                            {
                                var list = existingTags2.ToList();
                                foreach (var a in adds) if (!list.Contains(a)) list.Add(a);
                                fi2.SetValue(c, list.ToArray());
                            }
                            else if (valObj is SimHashes[] existingSim2)
                            {
                                var sims = existingSim2.ToList();
                                foreach (var a in adds)
                                {
                                    var sh = TagToSimHash(a);
                                    if (sh.HasValue && !sims.Contains(sh.Value)) sims.Add(sh.Value);
                                }
                                fi2.SetValue(c, sims.ToArray());
                            }
                            else if (valObj == null)
                            {
                                var fieldType2 = fi2.FieldType;
                                if (fieldType2 == typeof(Tag[]))
                                {
                                    fi2.SetValue(c, adds.ToArray());
                                }
                                else if (fieldType2 == typeof(SimHashes[]))
                                {
                                    var sims = new List<SimHashes>();
                                    foreach (var a in adds)
                                    {
                                        var sh = TagToSimHash(a);
                                        if (sh.HasValue && !sims.Contains(sh.Value)) sims.Add(sh.Value);
                                    }
                                    fi2.SetValue(c, sims.ToArray());
                                }
                            }
                        }
                    }

                    if (t.Name == "PipedOptionalExhaust")
                    {
                        var fi = t.GetField("elementTag", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi != null)
                        {
                            var tagObj = fi.GetValue(c);
                            if (tagObj is Tag tag)
                            {
                                // no-op; optional exhausts handle elementTag singularly
                            }
                            else
                            {
                                // do nothing
                            }
                        }
                    }
                }
            }
       

        private static void BasicOilRefinery_NaphthaOutputPatch(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            var elementConverter = go.GetComponent<ElementConverter>();
            if (elementConverter == null) return;

            var outputs = elementConverter.outputElements?.ToList() ?? new List<ElementConverter.OutputElement>();
            var inputs = elementConverter.consumedElements?.ToList() ?? new List<ElementConverter.ConsumedElement>();

            outputs.Add(new ElementConverter.OutputElement(
                0.085f,
                ModElementRegistration.AshByproduct,
                348.15f,
                storeOutput: true,
                outputElementOffsety: 1f
            ));

            outputs.Add(new ElementConverter.OutputElement(
                2.41f,
                SimHashes.Naphtha,
                348.15f,
                storeOutput: false,
                outputElementOffsety: 1f,
                outputElementOffsetx: 1f
            ));
            inputs.Add(new ElementConverter.ConsumedElement(
                ModTags.OxidizerGas,
                0.035f
            ));
            elementConverter.outputElements = outputs.ToArray();
            elementConverter.consumedElements = inputs.ToArray();

            go.AddOrGet<OxidizerLowStatus>();
            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
                storage.storageFilters = new List<Tag> { ModTags.OxidizerGas };
            }

            var dualGasConsumer = go.AddOrGet<DualGasElementConsumer>();
            dualGasConsumer.consumptionRate = 0.2f;
            dualGasConsumer.capacityKG = 1f;
            dualGasConsumer.consumptionRadius = 2;
            dualGasConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            dualGasConsumer.isRequired = true;
            dualGasConsumer.storeOnConsume = true;
            dualGasConsumer.showInStatusPanel = true;
            dualGasConsumer.showDescriptor = true;
            dualGasConsumer.ignoreActiveChanged = true;
            dualGasConsumer.storage = storage;

        

            var tilemaker = go.AddComponent<ElementTileMaker>();
            tilemaker.emitTag = new Tag("AshByproduct");
            tilemaker.emitMass = 120f;
            tilemaker.emitOffset = new Vector3(0f, 0f);
            tilemaker.storage = storage;
            tilemaker.MakeTiles = false;
        }

        private static SimHashes? TagToSimHash(Tag t)
        {
            try
            {
                var name = t.ToString() ?? string.Empty;
                foreach (SimHashes sh in Enum.GetValues(typeof(SimHashes)))
                {
                    if (sh.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                        return sh;
                }
            }
            catch { }
            return null;
        }


    }

    public static class FabricatorHelpers
    {
        public static float GetSumIngredients(ComplexFabricator fabricator)
        {
            var recipe = fabricator.CurrentWorkingOrder;
            if (recipe != null && recipe.ingredients != null)
                return recipe.ingredients.Sum(i => i.amount);
            return 0f;
        }

        public static float GetSumResults(ComplexFabricator fabricator)
        {
            var recipe = fabricator.CurrentWorkingOrder;
            if (recipe != null && recipe.results != null)
                return recipe.results.Sum(r => r.amount);
            return 0f;
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "CompleteWorkingOrder")]
    public static class ChemicalAdvancedKiln_SpawnCrud_Patch
    {
        public static void Prefix(ComplexFabricator __instance)
        {
            // Target Chemical_AdvancedKiln only (all recipes)
            if (__instance.PrefabID().ToString() != "Chemical_AdvancedKiln")
                return;

            float sumIngredients = FabricatorHelpers.GetSumIngredients(__instance);
            float sumResults = FabricatorHelpers.GetSumResults(__instance);
            float amount = sumIngredients - sumResults; // no extra subtraction

            if (amount <= 0) return;

            float temp = 353.15f;

            // Sample from input
            var storage = __instance.inStorage;
            var sampleItem = storage.items.FirstOrDefault();
            var pe = sampleItem?.GetComponent<PrimaryElement>();
            if (pe != null)
                temp = pe.Temperature;

            // Spawn pos like Kiln ash
            int cell = Grid.PosToCell(__instance.transform.position);
            int spawnCell = Grid.OffsetCell(cell, 1, 0);
            Vector3 pos = Grid.CellToPosCCC(spawnCell, Grid.SceneLayer.Ore);

            var elem = ElementLoader.FindElementByTag(ModElementRegistration.CrudByproduct.Tag);
            if (elem != null)
            {
                var go = elem.substance.SpawnResource(pos, amount, temp, byte.MaxValue, 0);
                if (go != null) go.SetActive(true);
            }
        }
    }

    // Ronivans AdvancedMetalRefinery recipe rewrite - mimic MetalRefineryPatches for !chemicalProcessingEnabled case, *4 outputs
    internal static class AdvancedMetalRefinery_CinnabarSplit_RecipeCtorPatches
    {
        private const float InputKg = 400f; // Advanced recipes use 400kg input
        private const float Epsilon = 0.0001f;

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
        private static class CtorPatch_NoDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) })]
        private static class CtorPatch_WithDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, string[] requiredDlcIds)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        private static void ApplyAllRewrites(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
        {
            if (recipe == null) return;
            if (!Config.Instance.RephysicalizedMetalOre) return;

            // Check for AdvancedMetalRefinery ID
            if (string.IsNullOrEmpty(id) || !id.StartsWith("Chemical_AdvancedMetalRefinery", StringComparison.Ordinal)) return;

            // list of target ingredient tag -> outputs *4 (MetalRefinery amounts *4)
            var rewrites = new (Tag ingredientTag, (Tag tag, float amount)[] outputs)[] {
                (SimHashes.Cinnabar.CreateTag(), new[]{ (SimHashes.Mercury.CreateTag(), 344f), (SimHashes.Sulfur.CreateTag(), 56f) }),
                (SimHashes.IronOre.CreateTag(), new[]{ (SimHashes.Iron.CreateTag(), 268f), (SimHashes.IgneousRock.CreateTag(), 132f) }),
                (SimHashes.Cuprite.CreateTag(), new[]{ (SimHashes.Copper.CreateTag(), 320f), (SimHashes.Oxygen.CreateTag(), 80f) }),
                (SimHashes.Cobaltite.CreateTag(), new[]{ (SimHashes.Cobalt.CreateTag(), 284f), (SimHashes.Sulfur.CreateTag(), 116f) }),
                (SimHashes.NickelOre.CreateTag(), new[]{ (SimHashes.Nickel.CreateTag(), 292f), (SimHashes.Sulfur.CreateTag(), 108f) }),
                (SimHashes.Wolframite.CreateTag(), new[]{ (SimHashes.Tungsten.CreateTag(), 240f), (SimHashes.Rust.CreateTag(), 160f) }),
                (SimHashes.AluminumOre.CreateTag(), new[]{ (SimHashes.Aluminum.CreateTag(), 240f), (SimHashes.Oxygen.CreateTag(), 80f), (SimHashes.Water.CreateTag(), 80f) }),
                (SimHashes.GoldAmalgam.CreateTag(), new[]{ (SimHashes.Gold.CreateTag(), 268f), (SimHashes.Mercury.CreateTag(), 132f) })
            };

            foreach (var rw in rewrites)
                TryRewriteRecipe(recipe, id, ingredients, results, rw.ingredientTag, rw.outputs);
        }

        private static void TryRewriteRecipe(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, Tag targetIngredientTag, (Tag tag, float amount)[] outputs)
        {
            if (ingredients == null || ingredients.Length != 1) return;
            var ing = ingredients[0];
            if (ing.material != targetIngredientTag) return;
            if (Mathf.Abs(ing.amount - InputKg) > Epsilon) return;

            var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
            if (results != null && results.Length > 0) tempOp = results[0].temperatureOperation;

            var newResults = new ComplexRecipe.RecipeElement[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
            {
                newResults[i] = new ComplexRecipe.RecipeElement(outputs[i].tag, outputs[i].amount, tempOp);
            }

            recipe.results = newResults;
        }
    }

    // Ronivans Plasma Furnace recipe rewrite - target clean single-ore 500f recipes, apply x5 Rephys splits, preserve Melted temp
    internal static class PlasmaFurnace_CinnabarSplit_RecipeCtorPatches
    {
        private const float InputKg = 500f;
        private const float Epsilon = 0.0001f;

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
        private static class CtorPatch_NoDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        [HarmonyPatch(typeof(ComplexRecipe), MethodType.Constructor, new Type[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]), typeof(string[]) })]
        private static class CtorPatch_WithDlc
        {
            public static void Postfix(ComplexRecipe __instance, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, string[] requiredDlcIds)
            {
                ApplyAllRewrites(__instance, id, ingredients, results);
            }
        }

        private static void ApplyAllRewrites(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results)
        {
            if (recipe == null) return;
            if (!Config.Instance.RephysicalizedMetalOre) return;
            if (!id.StartsWith("Metallurgy_PlasmaFurnace", StringComparison.Ordinal)) return;

            var rewrites = new (Tag ingredientTag, (Tag tag, float amount)[] outputs)[] {
                (SimHashes.Cinnabar.CreateTag(), new[] { (SimHashes.Mercury.CreateTag(), 430f), (SimHashes.Sulfur.CreateTag(), 70f) }),
                (SimHashes.IronOre.CreateTag(), new[] { (SimHashes.MoltenIron.CreateTag(), 335f), (SimHashes.Magma.CreateTag(), 165f) }),
                (SimHashes.Cuprite.CreateTag(), new[] { (SimHashes.MoltenCopper.CreateTag(), 400f), (SimHashes.LiquidOxygen.CreateTag(), 100f) }),
                (SimHashes.Cobaltite.CreateTag(), new[] { (SimHashes.MoltenCobalt.CreateTag(), 355f), (SimHashes.LiquidSulfur.CreateTag(), 145f) }),
                (SimHashes.NickelOre.CreateTag(), new[] { (SimHashes.MoltenNickel.CreateTag(), 365f), (SimHashes.LiquidSulfur.CreateTag(), 135f) }),
                (SimHashes.Wolframite.CreateTag(), new[] { (SimHashes.MoltenTungsten.CreateTag(), 300f), (SimHashes.MoltenIron.CreateTag(), 120f), (SimHashes.LiquidOxygen.CreateTag(), 80f)  }),
                (SimHashes.AluminumOre.CreateTag(), new[] { (SimHashes.MoltenAluminum.CreateTag(), 300f), (SimHashes.LiquidOxygen.CreateTag(), 100f), (SimHashes.Water.CreateTag(), 100f) }),
                (SimHashes.GoldAmalgam.CreateTag(), new[] { (SimHashes.MoltenGold.CreateTag(), 335f), (SimHashes.Mercury.CreateTag(), 165f) }),

            };

            foreach (var rw in rewrites)
                TryRewriteRecipe(recipe, id, ingredients, results, rw.ingredientTag, rw.outputs);
        }

        private static void TryRewriteRecipe(ComplexRecipe recipe, string id, ComplexRecipe.RecipeElement[] ingredients, ComplexRecipe.RecipeElement[] results, Tag targetIngredientTag, (Tag tag, float amount)[] outputs)
        {
            if (ingredients == null || ingredients.Length != 1) return;
            var ing = ingredients[0];
            if (ing.material != targetIngredientTag) return;
            if (Mathf.Abs(ing.amount - InputKg) > Epsilon) return;

            var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.Melted;
            if (results != null && results.Length > 0) tempOp = results[0].temperatureOperation;

            var newResults = new ComplexRecipe.RecipeElement[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
            {
                newResults[i] = new ComplexRecipe.RecipeElement(outputs[i].tag, outputs[i].amount, tempOp);
            }

            recipe.results = newResults;
        }
    }

    [HarmonyPatch(typeof(ComplexRecipe))]

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new[] { typeof(string), typeof(ComplexRecipe.RecipeElement[]), typeof(ComplexRecipe.RecipeElement[]) })]
    public static class ComplexRecipe_GlassForgeResult_Postfix
    {
        static void Postfix(ComplexRecipe __instance)
        {
            var inputs = __instance.ingredients ?? System.Array.Empty<ComplexRecipe.RecipeElement>();
            var results = __instance.results ?? System.Array.Empty<ComplexRecipe.RecipeElement>();

            if (inputs.Length == 1 && inputs[0].material == SimHashes.Sand.CreateTag() && inputs[0].amount == 300f &&
                results.Length >= 1 && results.Any(r => r.material == SimHashes.MoltenGlass.CreateTag()))
            {
                // Add Crud (solid, dropped)
                if (!results.Any(r => r.material == ModElementRegistration.CrudByproduct.Tag))
                {
                    __instance.results = results
                        .Concat(new[]
                        {
                        new ComplexRecipe.RecipeElement(
                            ModElementRegistration.CrudByproduct.Tag,
                            200f,
                            ComplexRecipe.RecipeElement.TemperatureOperation.Heated)
                        })
                        .ToArray();
                }
            }
        }


        private static void GlassFoundryPostfix()
        {
            // GlassFoundry ConfigureBuildingTemplate postfix - identical to GlassForge
            try
            {
                var targetType = Type.GetType("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_GlassFoundryConfig, RonivansLegacy_ChemicalProcessing");
                var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.GlassFoundry");
                var postfix = new HarmonyMethod(typeof(RonivansIntegrationMetallurgy).GetMethod(nameof(Chemical_GlassFoundryConfigPatch), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, postfix: postfix);
            }
            catch { }
        }

        private static SimHashes? TagToSimHash(Tag t)
        {
            try
            {
                // Tag name may be element name; attempt to map via SimHashes parse
                var name = t.ToString() ?? string.Empty;
                foreach (SimHashes sh in Enum.GetValues(typeof(SimHashes)))
                {
                    if (sh.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                        return sh;
                }
            }
            catch { }
            return null;
        }

        private static void Chemical_GlassFoundryConfigPatch(GameObject go, Tag prefab_tag)
        {
            var dropper = go.AddComponent<ElementTileMaker>();
            dropper.emitTag = ModElementRegistration.CrudByproduct.Tag;
            dropper.emitMass = 200f;
            dropper.emitOffset = new Vector3(0f, 0f);
            dropper.MakeTiles = false;
        }
    }

    // Integration for Ronivans Jaw Crusher Mill (Chemical_SmallCrusherMill)
    [HarmonyPatch]
    public static class JawCrusherMillIntegration
    {
        // Run at DB initialize to register runtime patches against Ronivans types
        [HarmonyPatch(typeof(Db), "Initialize")]
        private static class Starter
        {
            private static void Postfix()
            {
                ApplyPatches();
            }
        }

        public static void ApplyPatches()
        {
            try
            {
                var targetType = Type.GetType("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_SmallCrusherMillConfig, RonivansLegacy_ChemicalProcessing")
                                 ?? AccessTools.TypeByName("Dupes_Industrial_Overhaul.Chemical_Processing.Buildings.Chemical_SmallCrusherMillConfig");
                if (targetType == null)
                    return;

                var method = AccessTools.Method(targetType, "ConfigureBuildingTemplate", new Type[] { typeof(GameObject), typeof(Tag) })
                             ?? AccessTools.Method(targetType, "ConfigureBuildingTemplate");
                if (method != null)
                {
                    var harmony = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.JawCrusher");
                    var postfix = new HarmonyMethod(AccessTools.Method(typeof(JawCrusherMillIntegration), nameof(JawCrusher_ConfigurePostfix)));
                    harmony.Patch(method, postfix: postfix);
                }

                // Patch ComplexFabricator.SpawnOrderProduct to drop outputs for the jaw crusher
                var cfMethod = AccessTools.Method(typeof(ComplexFabricator), "SpawnOrderProduct");
                if (cfMethod != null)
                {
                    var harmony2 = Rephysicalized.Mod.HarmonyInstance ?? new Harmony("Rephysicalized.Ronivans.JawCrusher.Spawn");
                    var postfix2 = new HarmonyMethod(AccessTools.Method(typeof(JawCrusherMillIntegration), nameof(JawCrusher_DropAfterRecipe)));
                    harmony2.Patch(cfMethod, postfix: postfix2);
                }
            }
            catch { }
        }

        // Postfix applied to Ronivans JawCrusher ConfigureBuildingTemplate
        private static void JawCrusher_ConfigurePostfix(GameObject go, Tag prefab_tag)
        {
            try
            {
                if (!Config.Instance.RephysicalizedMetalOre) return;

                var fabricator = go.GetComponent<ComplexFabricator>();
                if (fabricator != null)
                {
                    fabricator.storeProduced = true;

                    var dropper = go.AddComponent<WorldElementDropper>();
                    dropper.DropSolids = true;
                    dropper.DropLiquids = true;
                    dropper.DropGases = true;
                    dropper.TargetStorage = fabricator.outStorage;

                    var cmp = go.GetComponent<DropAllWorkable>();
                    if (cmp != null)
                    {
                        try { cmp.storages = new[] { fabricator.outStorage }; } catch { }
                    }
                }

                // Recipe adjustments: copy RockCrusher metal rewrites but skip salt recipe
                var crm = ComplexRecipeManager.Get();
                if (crm == null) return;

                Tag sandTag = SimHashes.Sand.CreateTag();


                // Metal rewrites (copy of RockCrusher logic)
                var metalRewrites = new (SimHashes ingredient, Tag[] tags, float[] amts)[]
                {
                   (SimHashes.Cinnabar,     new[]{ SimHashes.Mercury.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 42.5f, 50f, 12.5f }),
                    (SimHashes.IronOre,      new[]{ SimHashes.Iron.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 33f, 67f }),
                    (SimHashes.Cuprite,      new[]{ SimHashes.Copper.CreateTag(), SimHashes.ContaminatedOxygen.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 40f, 10f, 50f }),
                    (SimHashes.Cobaltite,    new[]{ SimHashes.Cobalt.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 35f, 50f, 15f }),
                    (SimHashes.NickelOre,    new[]{ SimHashes.Nickel.CreateTag(), SimHashes.Sand.CreateTag(), SimHashes.Sulfur.CreateTag() }, new[]{ 37.5f, 50f , 12.5f}),
                    (SimHashes.Wolframite,   new[]{ SimHashes.Tungsten.CreateTag(), SimHashes.Rust.CreateTag() , SimHashes.Sand.CreateTag() }, new[]{ 30f, 20f, 50f }),
                    (SimHashes.AluminumOre,  new[]{ SimHashes.Aluminum.CreateTag(), SimHashes.ContaminatedOxygen.CreateTag(), SimHashes.DirtyWater.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 30f, 20f, 20f, 30f }),
                    (SimHashes.GoldAmalgam,  new[]{ SimHashes.Gold.CreateTag(), SimHashes.Mercury.CreateTag(), SimHashes.Sand.CreateTag() }, new[]{ 35f, 15f, 50f })
                };

                foreach (var rw in metalRewrites)
                {
                    var elem = ElementLoader.FindElementByHash(rw.ingredient);
                    if (elem == null) continue;
                    var low = elem.highTempTransition?.lowTempTransition;
                    if (low == null || low == elem) continue;

                    var inputsMeta = new ComplexRecipe.RecipeElement[] { new ComplexRecipe.RecipeElement(elem.tag, 100f) };
                    var outputsMeta = new ComplexRecipe.RecipeElement[] {
                        new ComplexRecipe.RecipeElement(low.tag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature),
                        new ComplexRecipe.RecipeElement(sandTag, 50f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
                    };
                    string recipeId = ComplexRecipeManager.MakeRecipeID("Chemical_SmallCrusherMill", inputsMeta, outputsMeta);
                    var recipe = crm.GetRecipe(recipeId);
                    if (recipe == null) continue;

                    var tempOp = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
                    if (recipe.results != null && recipe.results.Length > 0) tempOp = recipe.results[0].temperatureOperation;

                    var newResults = new ComplexRecipe.RecipeElement[rw.tags.Length];
                    for (int i = 0; i < rw.tags.Length; i++)
                        newResults[i] = new ComplexRecipe.RecipeElement(rw.tags[i], rw.amts[i], tempOp, true);

                    recipe.results = newResults;
                }
            }
            catch { }
        }

        // Postfix to drop outputs after recipe completes
        public static void JawCrusher_DropAfterRecipe(ComplexFabricator __instance)
        {
            try
            {
                if (__instance.PrefabID().ToString() == "Chemical_SmallCrusherMill")
                {
                    if (!Config.Instance.RephysicalizedMetalOre) return;
                    var dropper = __instance.GetComponent<WorldElementDropper>();
                    var storage = __instance.outStorage;
                    if (dropper != null && storage != null)
                    {
                        storage.DropAll();
                    }
                }
            }
            catch { }
        }
    }
}






