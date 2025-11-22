using HarmonyLib;
using KSerialization;
using STRINGS;
using System;
using System.Linq;
using UnityEngine;
using UtilLibs;

namespace Rephysicalized
{
    // Minimal controller: visual bank swapping with pending-swap until after emptying; dispenser wiring.
    // Meter mirroring is handled in the RefreshMeters postfix below.
    [SerializationConfig(MemberSerialization.OptIn)]
    public class MilkSeparatorModeController : KMonoBehaviour, FewOptionSideScreen.IFewOptionSideScreen
    {
        public enum Mode { Milk, Naphtha }

        [Serialize] private Mode currentMode = Mode.Milk;
        [Serialize] private Mode appliedMode = Mode.Milk;

        [MyCmpGet] private KBatchedAnimController kbac;
        [MyCmpGet] private ConduitDispenser dispenser;
        [MyCmpGet] private Storage storage;

        private KAnimFile[] originalAnimFiles;
        private KAnimFile napthaBase;

        [Serialize] private bool pendingAnimSwap = false;

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (kbac != null)
                originalAnimFiles = kbac.AnimFiles;

            // Preload naphtha building bank (clip names same as vanilla)
            napthaBase = GetAnim("milk_separator_naphtha_kanim");
            EnsureSoundLinks(); // copy sounds from vanilla to naphtha once

            // Apply saved visuals on load
            ApplyVisuals(currentMode); // reflect saved mode immediately

            // Wire dispenser by mode
            ApplyDispenserFilter();

            if (storage != null)
                storage.Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
        }

        public override void OnCleanUp()
        {
            if (storage != null)
                storage.Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            base.OnCleanUp();
        }

        // Side screen
        public FewOptionSideScreen.IFewOptionSideScreen.Option[] GetOptions()
        {
            var milkTag = SimHashes.Milk.CreateTag();
            var napTag = SimHashes.Naphtha.CreateTag();
            return new[]
            {
                new FewOptionSideScreen.IFewOptionSideScreen.Option(milkTag, STRINGS.BUILDINGS.MILKSEPARATOR.MILKMODE, Def.GetUISprite((object)milkTag), STRINGS.BUILDINGS.MILKSEPARATOR.MILKMODE_TOOLTIP),
                new FewOptionSideScreen.IFewOptionSideScreen.Option(napTag, STRINGS.BUILDINGS.MILKSEPARATOR.NAPHTHAMODE, Def.GetUISprite((object)napTag), STRINGS.BUILDINGS.MILKSEPARATOR.NAPHTHAMODE_TOOLTIP),
            };
        }

        public void OnOptionSelected(FewOptionSideScreen.IFewOptionSideScreen.Option option)
        {
            var target = option.tag == SimHashes.Milk.CreateTag() ? Mode.Milk : Mode.Naphtha;
            currentMode = target;

            // Defer visual swap if emptying required; else swap immediately.
            if (RequiresEmptyingForMode(target))
            {
                pendingAnimSwap = true;
            }
            else
            {
                ApplyVisuals(target);
            }

            ApplyDispenserFilter();

            // Ask the SM to re-evaluate transitions (may create/clear empty chore, start/stop running)
            gameObject.Trigger((int)GameHashes.OnStorageChange, null);
        }

        public Tag GetSelectedOption() =>
            currentMode == Mode.Milk ? SimHashes.Milk.CreateTag() : SimHashes.Naphtha.CreateTag();

        private void OnStorageChanged(object _)
        {
            // After any storage changes (including empty completion), try to finalize a pending swap.
            if (pendingAnimSwap && !RequiresEmptyingForMode(currentMode))
            {
                ApplyVisuals(currentMode);
                pendingAnimSwap = false;
            }
        }

        // Called from DropMilkFat postfix when emptying completes; finalize any pending swap.
        public void OnEmptied()
        {
            if (pendingAnimSwap)
            {
                ApplyVisuals(currentMode);
                pendingAnimSwap = false;
            }

            // Ensure we don't stick on pst: forcing idle right here avoids the stale sprite
            if (kbac != null)
                kbac.Play((HashedString)"on", KAnim.PlayMode.Loop);
        }

        private void ApplyVisuals(Mode mode)
        {
            if (kbac == null || originalAnimFiles == null) return;

            if (mode == Mode.Naphtha && napthaBase != null)
            {
                var files = Combine(napthaBase, originalAnimFiles);
                kbac.SwapAnims(files);
                appliedMode = Mode.Naphtha;
            }
            else
            {
                kbac.SwapAnims(originalAnimFiles);
                appliedMode = Mode.Milk;
            }
        }

        private void ApplyDispenserFilter()
        {
            if (dispenser == null) return;
            dispenser.conduitType = ConduitType.Liquid;
            dispenser.invertElementFilter = true;
            dispenser.elementFilter = currentMode == Mode.Milk
                ? new[] { SimHashes.Milk }
                : new[] { SimHashes.Naphtha };
        }

        private bool RequiresEmptyingForMode(Mode mode)
        {
            if (storage == null) return false;

            var def = GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            float fat = storage.GetAmountAvailable(fatTag);
            float sulfur = storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag());

            if (mode == Mode.Naphtha)
                return (fat > 0f) || (sulfur >= cap);
            else
                return (sulfur > 0f) || (fat >= cap);
        }

        private void EnsureSoundLinks()
        {
            try
            {
                // Copy vanilla sound table to naphtha bank so events resolve
                var src = Assets.GetAnim("milk_separator_kanim");
                var dst = Assets.GetAnim("milk_separator_naphtha_kanim");
                if (src != null && dst != null)
                    SoundUtils.CopySoundsToAnim("milk_separator_naphtha_kanim", "milk_separator_kanim");
            }
            catch { /* cosmetic */ }
        }

        private static KAnimFile GetAnim(string id)
        {
            try
            {
                var f = Assets.GetAnim((HashedString)id);
                if (f != null) return f;
                if (!id.EndsWith("_kanim"))
                    return Assets.GetAnim((HashedString)(id + "_kanim"));
            }
            catch { }
            return null;
        }

        private static KAnimFile[] Combine(KAnimFile primary, KAnimFile[] fallbacks)
        {
            if (primary == null) return fallbacks ?? Array.Empty<KAnimFile>();
            if (fallbacks == null || fallbacks.Length == 0) return new[] { primary };
            var res = new KAnimFile[1 + fallbacks.Length];
            res[0] = primary;
            Array.Copy(fallbacks, 0, res, 1, fallbacks.Length);
            return res;
        }
    }

    // Meter: mirror current bank and show combined fullness (MilkFat + Sulfur) using vanilla meter_fat clip.
    [HarmonyPatch(typeof(MilkSeparator.Instance), nameof(MilkSeparator.Instance.RefreshMeters))]
    public static class MilkSeparator_Instance_RefreshMeters_Patch
    {
        public static void Postfix(MilkSeparator.Instance __instance)
        {
            if (__instance == null) return;

            var meter = HarmonyLib.Traverse.Create(__instance).Field("fatMeter").GetValue<MeterController>();
            if (meter == null || meter.meterController == null) return;

            // Mirror bank from building
            var kbac = __instance.GetComponent<KBatchedAnimController>();
            if (kbac?.AnimFiles != null && kbac.AnimFiles.Length > 0)
                meter.meterController.SwapAnims(kbac.AnimFiles);

            // Combined fullness (milkfat + sulfur)
            float capacity = 15f;
            Tag milkFatTag = ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;
            var def = __instance.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            if (def != null) { capacity = def.MILK_FAT_CAPACITY; milkFatTag = def.MILK_FAT_TAG; }

            var storage = __instance.GetComponent<Storage>();
            float milkFat = storage != null ? storage.GetAmountAvailable(milkFatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;
            float fullness = capacity > 0f ? Mathf.Clamp01(Mathf.Min(milkFat + sulfur, capacity) / capacity) : 0f;

            if (meter.meterController.currentAnim != (HashedString)"meter_fat")
                meter.meterController.Play((HashedString)"meter_fat", KAnim.PlayMode.Paused);

            meter.SetPositionPercent(fullness);
        }
    }

    // --- State machine gates: emptying and run rules for both modes ---

    [HarmonyPatch(typeof(MilkSeparator), nameof(MilkSeparator.RequiresEmptying))]
    public static class MilkSeparator_RequiresEmptying_Patch
    {
        public static bool Prefix(MilkSeparator.Instance smi, ref bool __result)
        {
            if (smi == null) return true;

            var go = smi.gameObject;
            var ctrl = go.GetComponent<MilkSeparatorModeController>();
            if (ctrl == null) return true; // vanilla behavior for others

            var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            var storage = go.GetComponent<Storage>();
            float fat = storage != null ? storage.GetAmountAvailable(fatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;

            if (ctrl.GetSelectedOption() == SimHashes.Naphtha.CreateTag())
            {
                // Naphtha mode: wrong solid is MilkFat or Sulfur full
                __result = (fat > 0f) || (sulfur >= cap);
                return false;
            }
            else
            {
                // Milk mode: wrong solid is Sulfur or MilkFat full
                __result = (sulfur > 0f) || (fat >= cap);
                return false; 
            }
        }
    }

    [HarmonyPatch(typeof(MilkSeparator), nameof(MilkSeparator.CanBeginSeparate))]
    public static class MilkSeparator_CanBeginSeparate_Patch
    {
        public static bool Prefix(MilkSeparator.Instance smi, ref bool __result)
        {
            if (smi == null) return true;

            var go = smi.gameObject;
            var ctrl = go.GetComponent<MilkSeparatorModeController>();
            if (ctrl == null) return true;

            var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            var storage = go.GetComponent<Storage>();
            float fat = storage != null ? storage.GetAmountAvailable(fatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;

            if (ctrl.GetSelectedOption() == SimHashes.Naphtha.CreateTag())
            {
                bool hasNaphtha = storage != null && storage.GetAmountAvailable(SimHashes.Naphtha.CreateTag()) > 0.0001f;
                __result = hasNaphtha && (fat <= 0f) && (sulfur < cap);
                return false;
            }
            else
            {
                bool hasMilk = storage != null && storage.GetAmountAvailable(SimHashes.Milk.CreateTag()) > 0.0001f;
                __result = hasMilk && (sulfur <= 0f) && (fat < cap);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(MilkSeparator), nameof(MilkSeparator.CanKeepSeparating))]
    public static class MilkSeparator_CanKeepSeparating_Patch
    {
        public static bool Prefix(MilkSeparator.Instance smi, ref bool __result)
        {
            if (smi == null) return true;

            var go = smi.gameObject;
            var ctrl = go.GetComponent<MilkSeparatorModeController>();
            if (ctrl == null) return true;

            var def = go.GetComponent<KPrefabID>()?.GetDef<MilkSeparator.Def>();
            float cap = def != null ? def.MILK_FAT_CAPACITY : 15f;
            Tag fatTag = def != null ? def.MILK_FAT_TAG : ElementLoader.FindElementByHash(SimHashes.MilkFat).tag;

            var storage = go.GetComponent<Storage>();
            float fat = storage != null ? storage.GetAmountAvailable(fatTag) : 0f;
            float sulfur = storage != null ? storage.GetAmountAvailable(SimHashes.Sulfur.CreateTag()) : 0f;

            if (ctrl.GetSelectedOption() == SimHashes.Naphtha.CreateTag())
            {
                bool hasNaphtha = storage != null && storage.GetAmountAvailable(SimHashes.Naphtha.CreateTag()) > 0.0001f;
                __result = hasNaphtha && (fat <= 0f) && (sulfur < cap);
                return false;
            }
            else
            {
                bool hasMilk = storage != null && storage.GetAmountAvailable(SimHashes.Milk.CreateTag()) > 0.0001f;
                __result = hasMilk && (sulfur <= 0f) && (fat < cap);
                return false;
            }
        }
    }

    // Drop any remaining solids (e.g., Sulfur) when emptying completes, and notify controller to finalize any pending visual swap.
    [HarmonyPatch(typeof(MilkSeparator.Instance), "DropMilkFat")]
    public static class MilkSeparatorInstance_DropMilkFat_Postfix
    {
        public static void Postfix(MilkSeparator.Instance __instance)
        {
            if (__instance == null) return;

            var storage = __instance.GetComponent<Storage>();
            if (storage != null && storage.items != null)
            {
                var snapshot = storage.items.ToArray(); // avoid modifying while iterating
                foreach (var go in snapshot)
                {
                    if (go == null) continue;
                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe == null || pe.Element == null) continue;

                    // Drop any remaining solids; MilkFat was already dropped by vanilla
                    if (pe.Element.IsSolid && pe.ElementID != SimHashes.MilkFat)
                        storage.Drop(go);
                }
            }

            // Finalize any pending visual swap and ensure idle
            var ctrl = __instance.GetComponent<MilkSeparatorModeController>();
            if (ctrl != null)
                ctrl.OnEmptied();
        }
    }

    // Adjust the emptying interact animation to match the currently applied visual bank.
    // If the building is using the naphtha bank and the naphtha interact exists, use it;
    // otherwise, use the vanilla milk-separator interact.
    [HarmonyPatch(typeof(MilkSeparator), "CreateEmptyChore")]
    public static class MilkSeparator_CreateEmptyChore_SetInteract_Postfix
    {
        public static void Postfix(MilkSeparator.Instance smi, ref Chore __result)
        {
            if (smi == null || __result == null || smi.workable == null)
                return;

            // Determine which building bank is applied by inspecting the building's KBAC anim files
            var kbac = smi.GetComponent<KBatchedAnimController>();
            var napthaBank = Assets.GetAnim("milk_separator_naphtha_kanim");

            bool appliedNaphtha = false;
            if (kbac?.AnimFiles != null && napthaBank != null)
            {
                for (int i = 0; i < kbac.AnimFiles.Length; i++)
                {
                    if (ReferenceEquals(kbac.AnimFiles[i], napthaBank))
                    {
                        appliedNaphtha = true;
                        break;
                    }
                }
            }

            // Resolve interact banks
            var vanillaInteract = Assets.GetAnim("anim_interacts_milk_separator_kanim");
            var naphthaInteract = Assets.GetAnim("anim_interacts_milk_separator_naphtha_kanim");

            // Build the override list: prefer naphtha when applied and available; include vanilla as fallback
            KAnimFile[] overrides = null;
            if (appliedNaphtha && naphthaInteract != null)
            {
                overrides = (vanillaInteract != null)
                    ? new[] { naphthaInteract, vanillaInteract }
                    : new[] { naphthaInteract };
            }
            else if (vanillaInteract != null)
            {
                overrides = new[] { vanillaInteract };
            }

            if (overrides == null || overrides.Length == 0)
                return;

            // 1) Apply to the building's workable
            if (smi.workable is Workable workable)
                workable.overrideAnims = overrides;

            // 2) Apply to the WorkChore's private override fields so the worker uses these anims
            // Handle both single KAnimFile and KAnimFile[] shapes defensively.

            // Chore-level overrideAnims
            var choreOverrideField = AccessTools.Field(__result.GetType(), "overrideAnims");
            if (choreOverrideField != null)
            {
                if (choreOverrideField.FieldType == typeof(KAnimFile[]))
                    choreOverrideField.SetValue(__result, overrides);
                else if (choreOverrideField.FieldType == typeof(KAnimFile))
                    choreOverrideField.SetValue(__result, overrides[0]);
            }

            // StatesInstance-level overrideAnims
            var smiField = AccessTools.Field(__result.GetType(), "smi");
            if (smiField != null)
            {
                var statesInstance = smiField.GetValue(__result);
                if (statesInstance != null)
                {
                    var stateOverrideField = AccessTools.Field(statesInstance.GetType(), "overrideAnims");
                    if (stateOverrideField != null)
                    {
                        if (stateOverrideField.FieldType == typeof(KAnimFile[]))
                            stateOverrideField.SetValue(statesInstance, overrides);
                        else if (stateOverrideField.FieldType == typeof(KAnimFile))
                            stateOverrideField.SetValue(statesInstance, overrides[0]);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Workable), "OnStartWork")]
    public static class MilkSeparator_Emptying_ForceInteract_Postfix
    {
        public static void Postfix(Workable __instance, [HarmonyArgument(0)] object worker)
        {
            if (!(__instance is EmptyMilkSeparatorWorkable)) return; if (worker is not Component workerComp) return;
            var workerKbac = workerComp.GetComponent<KBatchedAnimController>();
            var buildingKbac = __instance.GetComponent<KBatchedAnimController>();
            if (workerKbac == null || buildingKbac == null) return;

            var napthaBank = Assets.GetAnim("milk_separator_naphtha_kanim");
            bool appliedNaphtha = napthaBank != null
                                  && buildingKbac.AnimFiles != null
                                  && System.Array.IndexOf(buildingKbac.AnimFiles, napthaBank) >= 0;

            var vanillaInteract = Assets.GetAnim("anim_interacts_milk_separator_kanim");
            var naphthaInteract = Assets.GetAnim("anim_interacts_milk_separator_naphtha_kanim");

            // Run after vanilla SM has applied its overrides, so ours take precedence
            GameScheduler.Instance.Schedule("MilkSep:ForceInteractOverride", 0f, _ =>
            {
                if (appliedNaphtha && naphthaInteract != null)
                {
                    if (vanillaInteract != null) workerKbac.RemoveAnimOverrides(vanillaInteract);
                    workerKbac.AddAnimOverrides(naphthaInteract, 1f);
                }
                else
                {
                    if (naphthaInteract != null) workerKbac.RemoveAnimOverrides(naphthaInteract);
                    if (vanillaInteract != null) workerKbac.AddAnimOverrides(vanillaInteract, 1f);
                }
            });
        }
    }
    [HarmonyPatch(typeof(Workable), "OnStopWork")]
    public static class MilkSeparator_Emptying_ForceInteract_Cleanup_Postfix
    {
        public static void Postfix(Workable __instance, [HarmonyArgument(0)] object worker)
        {
            // Only care about Milk Separator emptying
            if (!(__instance is EmptyMilkSeparatorWorkable)) return;
            if (worker is not Component workerComp) return;

            var workerKbac = workerComp.GetComponent<KBatchedAnimController>();
            if (workerKbac == null) return;

            var vanillaInteract = Assets.GetAnim("anim_interacts_milk_separator_kanim");
            var naphthaInteract = Assets.GetAnim("anim_interacts_milk_separator_naphtha_kanim");

            // Remove both to guarantee we don't leak overrides regardless of which was applied
            if (vanillaInteract != null) workerKbac.RemoveAnimOverrides(vanillaInteract);
            if (naphthaInteract != null) workerKbac.RemoveAnimOverrides(naphthaInteract);
        }
    }
}