using HarmonyLib;
using Rephysicalized.ModElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Rephysicalized {


    [HarmonyPatch(typeof(EntityTemplates))]
    internal static class ExtendEntityToBasicPlant_SetDirtElement_Patch
    {
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            // Select the overload that matches the parameters of the one most commonly used for crop plants.
            // signature (decompiled):
            // ExtendEntityToBasicPlant(GameObject, float, float, float, float, SimHashes[], bool, float, float, string, bool, bool, bool, bool require_Backwall_Foundation, bool should_grow_old, float max_age, float min_radiation, float max_radiation, string baseTraitId, string baseTraitName)
            return typeof(EntityTemplates).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(EntityTemplates.ExtendEntityToBasicPlant)
                                     && m.GetParameters().Length == 20
                                     && m.GetParameters()[0].ParameterType == typeof(GameObject));
        }

        [HarmonyPostfix]
        private static void Postfix(GameObject template)
        {
            var pe = template.GetComponent<PrimaryElement>();
            if (pe.ElementID == SimHashes.Creature)
            {


                pe.SetElement(ModElementRegistration.PlantFiberRP);


            }
        }
    }


    // Shared floats between PlantPatches (crop yield amounts) and PlantMassTracker list
    // (real harvest subtract kg). Keyed by the crop/output id.
    public static class PlantPatches
    {
        public static readonly Dictionary<string, float> SharedYieldAndHarvestSubtractKg =
            new Dictionary<string, float>(StringComparer.Ordinal)
        {
            { "DewDrip", 10f },
            { "Kelp", 10f },
            { "WoodLog", 30f },
            { "OxyRock", Config.Instance.RealBreathing ? 2f : 20f },
            { "PlantFiber", 50f },
            { "PalmWood", 350f },
            { "Phosphorite", 40f },
            { "Urchin", 50f },
        };
    }


    // Consolidated: adjust yields for specific crop IDs in one pass
    // Directly modifies each plant config's HARVEST CropVal (shared reference with plant prefabs)
    // instead of TUNING.CROPS.CROP_TYPES, which only affects tuning UI data.
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class Consolidated_Crop_Yield_Patch
    {
        static Consolidated_Crop_Yield_Patch()
        {
            Debug.Log("[Consolidated_Crop_Yield_Patch] Static constructor - class loaded");
        }

        private static void Postfix()
        {
            Debug.Log("[Consolidated_Crop_Yield_Patch] Postfix started");

            // Set each plant config's HARVEST product quantity directly.
            // Since CropVal is a reference type shared with plant prefab Crop components,
            // modifying it here changes the actual harvest amounts.
            SetHarvestQuantity(DewDripperPlantConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["DewDrip"]);                  // DewDrip
            SetHarvestQuantity(KelpPlantConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["Kelp"]);                            // Kelp
            SetHarvestQuantity(ForestTreeConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["WoodLog"]);      // WoodLog
            SetHarvestQuantity(GasGrassConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["PlantFiber"]);                       // GasGrass
            SetHarvestQuantity(DewPalmConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["PalmWood"]);                        // DewPalm
            SetHarvestQuantity(UrchinPlantConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["Urchin"]); //PlanktonCoral
            SetHarvestQuantity(PlanktonCoralConfig.HARVEST, (int)PlantPatches.SharedYieldAndHarvestSubtractKg["Phosphorite"]); //PlanktonCoral

        }

        private static void SetHarvestQuantity(Crop.CropVal harvest, int amount)
        {
            if (harvest?.products == null)
            {
                Debug.LogWarning("[Consolidated_Crop_Yield_Patch] SetHarvestQuantity: harvest or products is null");
                return;
            }

           

            for (int i = 0; i < harvest.products.Count; i++)
            {
                var product = harvest.products[i];
                product.quantity = amount;
                harvest.products[i] = product;
            }
        }

     
    }

    // All plants start at 1 kg when placed
    [HarmonyPatch]
    internal static class ForestTreePlacedEntityMassEarlyPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(EntityTemplates)))
            {
                if (m.Name != nameof(EntityTemplates.CreatePlacedEntity) || m.ReturnType != typeof(GameObject))
                    continue;
                var ps = m.GetParameters();
                if (ps.Length >= 4 && ps[0].ParameterType == typeof(string) && ps[3].ParameterType == typeof(float))
                    yield return m;
            }
        }

        private static void Prefix([HarmonyArgument(0)] string id, [HarmonyArgument(3)] ref float mass)
        {
            if (id == "ForestTree" || id == "ForestTreeBranch" || id == "ColdBreather" || id == "BlueGrass" || id == "SaltPlant"
                || id == "SpaceTree" || id == "SpaceTreeBranch" || id == "VineMother" || id == "SpiceVine" 
                || id == "KelpPlant" || id == "DewPalm" || id == "UrchinPlant" || id == "SeaTreeRoot" || id == "SeaTree" || id == "CritterTrapPlant")
                mass = 1f;
        }
    }


    public static class PlantSafeAtmosphereTranspiler
    {
        /// <summary>
        /// Collapses the first `new SimHashes[] { ... }` array literal in the method to length zero.
        /// Safe to call from a patch class's own Transpiler.
        /// </summary>
        public static IEnumerable<CodeInstruction> CollapseSafeAtmospheres(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);

            for (int i = 0; i < list.Count - 5; i++)
            {
                if (IsLdcI4(list[i], 1) &&
                    list[i + 1].opcode == OpCodes.Newarr &&
                    Equals(list[i + 1].operand, typeof(SimHashes)) &&
                    list[i + 2].opcode == OpCodes.Dup &&
                    IsLdcI4(list[i + 3], 0) &&
                    list[i + 5].opcode == OpCodes.Stelem_I4)
                {
                    // new SimHashes[1] { X }  ->  new SimHashes[0] { }
                    // The store instructions are neutralised because writing to index 0 of a zero-length
                    // array would throw at runtime.
                    list[i] = new CodeInstruction(OpCodes.Ldc_I4_0);
                    list[i + 2] = new CodeInstruction(OpCodes.Nop);
                    list[i + 3] = new CodeInstruction(OpCodes.Nop);
                    list[i + 4] = new CodeInstruction(OpCodes.Nop);
                    list[i + 5] = new CodeInstruction(OpCodes.Nop);
                    break;
                }
            }

            return list;
        }

        private static bool IsLdcI4(CodeInstruction ci, int value)
        {
            if (ci.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (ci.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (ci.opcode == OpCodes.Ldc_I4_2) return value == 2;
            if (ci.opcode == OpCodes.Ldc_I4_3) return value == 3;
            if (ci.opcode == OpCodes.Ldc_I4_S || ci.opcode == OpCodes.Ldc_I4)
                return ci.operand is int v && v == value;
            return false;
        }
    }

}
