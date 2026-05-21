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


}






