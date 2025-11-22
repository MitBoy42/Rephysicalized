using HarmonyLib;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Rephysicalized
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class CritterMassFilter : KMonoBehaviour, ISaveLoadable
    {
        public const float AbsoluteMinKg = 0f;
        public const float AbsoluteMaxKg = 10000f;

        [Serialize, SerializeField] private float minMassKg = AbsoluteMinKg;
        [Serialize, SerializeField] private float maxMassKg = AbsoluteMaxKg;

        public float MinMassKg
        {
            get => minMassKg;
            set
            {
                minMassKg = Mathf.Clamp(value, AbsoluteMinKg, AbsoluteMaxKg);
                if (maxMassKg < minMassKg) maxMassKg = minMassKg;
            }
        }

        public float MaxMassKg
        {
            get => maxMassKg;
            set
            {
                maxMassKg = Mathf.Clamp(value, AbsoluteMinKg, AbsoluteMaxKg);
                if (minMassKg > maxMassKg) minMassKg = maxMassKg;
            }
        }

        public bool IsWithin(float massKg) => massKg >= minMassKg && massKg <= maxMassKg;

        public bool IsWithin(GameObject critter)
        {
            if (critter == null) return false;
            var pe = critter.GetComponent<PrimaryElement>();
            var mass = pe != null ? pe.Mass : 0f;
            return IsWithin(mass);
        }

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Normalize();
            Subscribe((int)GameHashes.CopySettings, OnCopySettings);
        }

        public override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.CopySettings);
            base.OnCleanUp();
        }

        private void Normalize()
        {
            if (minMassKg < AbsoluteMinKg) minMassKg = AbsoluteMinKg;
            if (maxMassKg > AbsoluteMaxKg) maxMassKg = AbsoluteMaxKg;
            if (maxMassKg < minMassKg) maxMassKg = minMassKg;
        }

        private void OnCopySettings(object data)
        {
            if (data is GameObject src)
            {
                var other = src.GetComponent<CritterMassFilter>();
                if (other != null)
                {
                    minMassKg = other.minMassKg;
                    maxMassKg = other.maxMassKg;
                    Normalize();
                }
            }
        }
    }

    // Proxies now persist to CritterMassFilter to ensure values survive deselection/save/load.
    public sealed class CritterMassMinProxy : MonoBehaviour, IUserControlledCapacity
    {
        private CritterMassFilter filter;

        private void Awake()
        {
            // Ensure filter exists on existing saves too.
            filter = gameObject.AddOrGet<CritterMassFilter>();
        }

        public CritterMassMinProxy() { }
        public CritterMassMinProxy(IUserControlledCapacity _) { /* kept for compatibility; unused now */ }

        public float UserMaxCapacity
        {
            get => filter != null ? filter.MinMassKg : 0f;
            set
            {
                if (filter == null) return;
                var clamped = Mathf.Clamp(value, CritterMassFilter.AbsoluteMinKg, Mathf.Min(filter.MaxMassKg, CritterMassFilter.AbsoluteMaxKg));
                filter.MinMassKg = clamped;
            }
        }

        public float AmountStored => GetComponent<PrimaryElement>()?.Mass ?? 0f;
        public float MinCapacity => CritterMassFilter.AbsoluteMinKg;
        public float MaxCapacity => CritterMassFilter.AbsoluteMaxKg;
        public bool WholeValues => false;

        // Fix CS8702 for runtimes without default interface impl support
        public bool ControlEnabled() => true;

        public LocString CapacityUnits => (LocString)"kg";
    }

    public sealed class CritterMassMaxProxy : MonoBehaviour, IUserControlledCapacity
    {
        private CritterMassFilter filter;

        private void Awake()
        {
            filter = gameObject.AddOrGet<CritterMassFilter>();
        }

        public CritterMassMaxProxy() { }
        public CritterMassMaxProxy(IUserControlledCapacity _) { }

        public float UserMaxCapacity
        {
            get => filter != null ? filter.MaxMassKg : 0f;
            set
            {
                if (filter == null) return;
                var clamped = Mathf.Clamp(value, Mathf.Max(filter.MinMassKg, CritterMassFilter.AbsoluteMinKg), CritterMassFilter.AbsoluteMaxKg);
                filter.MaxMassKg = clamped;
            }
        }

        public float AmountStored => GetComponent<PrimaryElement>()?.Mass ?? 0f;
        public float MinCapacity => CritterMassFilter.AbsoluteMinKg;
        public float MaxCapacity => CritterMassFilter.AbsoluteMaxKg;
        public bool WholeValues => false;

        // Fix CS8702
        public bool ControlEnabled() => true;

        public LocString CapacityUnits => (LocString)"kg";
    }

    // Marker to tag each cloned CapacityControlSideScreen as Min or Max (used at runtime for detection).
    public sealed class CritterMassScreenMarker : KMonoBehaviour
    {
        public enum Kind { Min, Max }
        public Kind kind;
    }
}

namespace Rephysicalized.Patches
{
    [HarmonyPatch(typeof(CritterPickUpConfig), nameof(CritterPickUpConfig.DoPostConfigureComplete))]
    public static class CritterPickUp_MassFilter_Patch
    {
        public static void Postfix(GameObject go)
        {
            if (go == null) return;
            go.AddOrGet<Rephysicalized.CritterMassFilter>();
            go.AddOrGet<Rephysicalized.CritterMassMinProxy>();
            go.AddOrGet<Rephysicalized.CritterMassMaxProxy>();

            // Keep original reflection fallback for capacity check delegate
            var def = go.AddOrGetDef<FixedCapturePoint.Def>();
            if (def != null) TryWrapIsAmountStoredOverCapacity(def);
        }

        private static void TryWrapIsAmountStoredOverCapacity(FixedCapturePoint.Def def)
        {
            var fld = typeof(FixedCapturePoint.Def).GetField("isAmountStoredOverCapacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fld == null) return;

            var existing = fld.GetValue(def) as Delegate;
            var f = existing as Func<FixedCapturePoint.Instance, FixedCapturableMonitor.Instance, bool>;

            if (f == null)
            {
                Func<FixedCapturePoint.Instance, FixedCapturableMonitor.Instance, bool> baseImpl = (smi, capturable) =>
                {
                    var critterGo = ResolveCritterGo(capturable);
                    var filter = smi?.gameObject?.GetComponent<Rephysicalized.CritterMassFilter>();
                    if (critterGo == null || filter == null) return true;
                    return filter.IsWithin(critterGo);
                };
                fld.SetValue(def, baseImpl);
            }
            else
            {
                Func<FixedCapturePoint.Instance, FixedCapturableMonitor.Instance, bool> wrapped = (smi, capturable) =>
                {
                    bool withinCapacity = f(smi, capturable);
                    if (!withinCapacity) return false;

                    var critterGo = ResolveCritterGo(capturable);
                    var filter = smi?.gameObject?.GetComponent<Rephysicalized.CritterMassFilter>();
                    if (critterGo == null || filter == null) return withinCapacity;

                    return filter.IsWithin(critterGo);
                };
                fld.SetValue(def, wrapped);
            }
        }

        private static GameObject ResolveCritterGo(object capturable)
        {
            if (capturable == null) return null;
            if (capturable is FixedCapturableMonitor.Instance inst) return inst.gameObject;

            var prop = capturable.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(capturable, null) as GameObject;

            var fld = capturable.GetType().GetField("master", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                      ?? capturable.GetType().GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return fld?.GetValue(capturable) as GameObject;
        }
    }

    [HarmonyPatch(typeof(CritterDropOffConfig), nameof(CritterDropOffConfig.ConfigureBuildingTemplate))]
    public static class CritterDropOff_MassFilter_Patch
    {
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            if (go == null) return;
            go.AddOrGet<Rephysicalized.CritterMassFilter>();
            go.AddOrGet<Rephysicalized.CritterMassMinProxy>();
            go.AddOrGet<Rephysicalized.CritterMassMaxProxy>();
        }
    }

    // Specific signature patch (if present)
    [HarmonyPatch(typeof(FixedCapturePoint.Instance), "CanCapturableBeCapturedAtCapturePoint", new[] {
        typeof(FixedCapturableMonitor.Instance), typeof(FixedCapturePoint.Instance), typeof(CavityInfo), typeof(int)
    })]
    public static class FixedCapturePoint_CanCapturableBeCapturedAtCapturePoint_MassGate_Patch
    {
        public static bool Prefix(FixedCapturableMonitor.Instance capturable, FixedCapturePoint.Instance capture_point, CavityInfo capture_cavity_info, int capture_cell, ref bool __result)
        {
            try
            {
                if (capturable == null || capture_point == null) return true;

                var filter = capture_point.gameObject?.GetComponent<Rephysicalized.CritterMassFilter>();
                if (filter == null) return true;

                var mass = capturable.gameObject?.GetComponent<PrimaryElement>()?.Mass ?? 0f;
                if (mass > 0f && !filter.IsWithin(mass))
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }
    }

    // Generic fallback using nested Instance reflection
    [HarmonyPatch]
    public static class FixedCapturePoint_CanCapture_Generic_MassGate_Patch
    {
        private static MethodBase s_target;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            try
            {
                var instanceType = typeof(FixedCapturePoint).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(t => t.Name.EndsWith("Instance"));
                if (instanceType == null) return false;
                s_target = AccessTools.Method(instanceType, "CanCapturableBeCapturedAtCapturePoint");
                return s_target != null;
            }
            catch { return false; }
        }

        [HarmonyTargetMethod] public static MethodBase TargetMethod() => s_target;

        public static bool Prefix(ref bool __result, object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2) return true;

                var capturable = __args[0];
                var capturePoint = __args[1];

                GameObject capGO = (capturePoint as KMonoBehaviour)?.gameObject
                                   ?? capturePoint?.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(capturePoint, null) as GameObject;
                if (capGO == null) return true;

                var filter = capGO.GetComponent<Rephysicalized.CritterMassFilter>();
                if (filter == null) return true;

                GameObject critterGO = (capturable as KMonoBehaviour)?.gameObject
                                       ?? capturable?.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(capturable, null) as GameObject;
                if (critterGO == null) return true;

                var mass = critterGO.GetComponent<PrimaryElement>()?.Mass ?? 0f;
                if (mass > 0f && !filter.IsWithin(mass))
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(FixedCapturableMonitor.Instance), "ShouldGoGetCaptured")]
    public static class FixedCapturableMonitor_ShouldGoGetCaptured_MassGate_Patch
    {
        public static void Postfix(FixedCapturableMonitor.Instance __instance, ref bool __result)
        {
            try
            {
                if (!__result) return;
                var cap = __instance.targetCapturePoint;
                if (cap == null) return;

                GameObject capGO = null;
                try { capGO = cap.gameObject; } catch { }
                if (capGO == null)
                {
                    var prop = cap.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null) capGO = prop.GetValue(cap, null) as GameObject;
                }
                if (capGO == null) return;

                var filter = capGO.GetComponent<Rephysicalized.CritterMassFilter>();
                if (filter == null) return;

                var mass = __instance.gameObject?.GetComponent<PrimaryElement>()?.Mass ?? 0f;
                if (mass > 0f && !filter.IsWithin(mass))
                    __result = false;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(DetailsScreen))]
    public static class DetailsScreen_MassScreens_Patch
    {
        internal const string MinRefName = "Rephys_MassMin";
        internal const string MaxRefName = "Rephys_MassMax";

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DetailsScreen.Refresh))]
        public static void Refresh_Postfix(DetailsScreen __instance, GameObject go)
        {
            UpdateMassSideScreensForTarget(__instance, go);
        }

        // Set the left-hand label using the private LocText field "title"
        private static void TrySetLeftTitle(CapacityControlSideScreen screen, string text)
        {
            try
            {
                if (screen == null) return;
                var fiTitle = AccessTools.Field(typeof(CapacityControlSideScreen), "title");
                var lt = fiTitle?.GetValue(screen) as LocText;
                if (lt != null)
                    lt.SetText(text);
            }
            catch { /* best effort */ }
        }

        private static void UpdateMassSideScreensForTarget(DetailsScreen screen, GameObject target)
        {
            try
            {
                var sideScreensField = AccessTools.Field(typeof(DetailsScreen), "sideScreens");
                var list = sideScreensField?.GetValue(screen) as List<DetailsScreen.SideScreenRef>;
                if (list == null) return;

                bool eligible = target != null && target.GetComponent<Rephysicalized.CritterMassFilter>() != null;
                var capacityRef = list.FirstOrDefault(r => r?.screenPrefab != null && r.screenPrefab.GetType().Name == "CapacityControlSideScreen");

                if (!eligible || capacityRef == null)
                {
                    RemoveMassScreenRefs(list);
                    return;
                }

                if (!list.Any(r => r?.name == MinRefName))
                    list.Add(new DetailsScreen.SideScreenRef { name = MinRefName, screenPrefab = capacityRef.screenPrefab, offset = Vector2.zero, tab = DetailsScreen.SidescreenTabTypes.Config, screenInstance = null });

                if (!list.Any(r => r?.name == MaxRefName))
                    list.Add(new DetailsScreen.SideScreenRef { name = MaxRefName, screenPrefab = capacityRef.screenPrefab, offset = Vector2.zero, tab = DetailsScreen.SidescreenTabTypes.Config, screenInstance = null });

                var originalRef = list.FirstOrDefault(r =>
                    r?.screenInstance is CapacityControlSideScreen &&
                    r.name != MinRefName && r.name != MaxRefName);
                var originalInstance = originalRef?.screenInstance as CapacityControlSideScreen;

                var parentGO = GetSideScreenContainer(screen, list, originalInstance);

                EnsureUniqueMassScreenInstance(list, capacityRef.screenPrefab as CapacityControlSideScreen, parentGO, MinRefName, "CapacityControlSideScreen (Min)");
                EnsureUniqueMassScreenInstance(list, capacityRef.screenPrefab as CapacityControlSideScreen, parentGO, MaxRefName, "CapacityControlSideScreen (Max)");

                foreach (var r in list)
                {
                    if (r?.screenInstance is CapacityControlSideScreen css &&
                        (r.name == MinRefName || r.name == MaxRefName))
                    {
                        var goInst = (css as Component)?.gameObject;
                        if (goInst != null && !goInst.activeSelf)
                            goInst.SetActive(true);

                        css.SetTarget(target);
                    }
                }
            }
            catch { }
        }

        private static GameObject GetSideScreenContainer(DetailsScreen screen, List<DetailsScreen.SideScreenRef> list, CapacityControlSideScreen originalInstance)
        {
            var anyInst = list.Select(r => r?.screenInstance as Component)
                              .FirstOrDefault(c => c != null && c.transform.parent != null);
            if (anyInst?.transform?.parent != null)
                return anyInst.transform.parent.gameObject;

            if (originalInstance != null && originalInstance.transform?.parent != null)
                return originalInstance.transform.parent.gameObject;

            var candidateFields = new[] { "sideScreenContent", "sideScreenContentBody", "sideScreenContainer", "sideScreensContent", "sideScreenContentRoot" };
            foreach (var name in candidateFields)
            {
                var f = AccessTools.Field(typeof(DetailsScreen), name);
                var val = f?.GetValue(screen);
                if (val == null) continue;

                if (val is GameObject go) return go;
                if (val is Component c) return c.gameObject;
                if (val is Transform t) return t.gameObject;
            }

            return screen.gameObject;
        }

        public static void EnsureUniqueMassScreenInstance(List<DetailsScreen.SideScreenRef> list,
                                                           CapacityControlSideScreen prefab,
                                                           GameObject parent,
                                                           string refName,
                                                           string cloneName)
        {
            var r = list.FirstOrDefault(x => x != null && x.name == refName);
            if (r == null || prefab == null || parent == null) return;

            var current = r.screenInstance as CapacityControlSideScreen;
            if (current == null)
            {
                var cloneGO = Util.KInstantiateUI(prefab.gameObject, parent, true);
                cloneGO.name = cloneName;

                var instance = cloneGO.GetComponent<CapacityControlSideScreen>();
                r.screenInstance = instance;

                // Tag the instance with a marker so runtime detection is trivial and reliable
                var marker = instance.gameObject.AddOrGet<Rephysicalized.CritterMassScreenMarker>();
                bool isMin = refName == MinRefName;
                marker.kind = isMin ? Rephysicalized.CritterMassScreenMarker.Kind.Min : Rephysicalized.CritterMassScreenMarker.Kind.Max;

                // Set the label immediately using the private "title" LocText
                TrySetLeftTitle(instance, isMin ? "Min" : "Max");

                // Ensure it is active so it’s visible immediately
                cloneGO.SetActive(true);
            }
        }

        private static void RemoveMassScreenRefs(List<DetailsScreen.SideScreenRef> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                if (r == null) continue;
                if (r.name == MinRefName || r.name == MaxRefName)
                {
                    try
                    {
                        if (r.screenInstance != null)
                        {
                            var go = (r.screenInstance as Component)?.gameObject;
                            SafeDestroyUI(go);
                        }
                    }
                    catch { }
                    list.RemoveAt(i);
                }
            }
        }

        private static void SafeDestroyUI(GameObject go)
        {
            if (go == null) return;
            try
            {
                if (Application.isPlaying || go.scene.IsValid())
                    UnityEngine.Object.Destroy(go);
                else
                    UnityEngine.Object.DestroyImmediate(go);
            }
            catch
            {
                try { UnityEngine.Object.Destroy(go); } catch { }
            }
        }
    }

    [HarmonyPatch(typeof(CapacityControlSideScreen))]
    public static class CapacityControlSideScreen_MassBinding_Patch
    {
        private const float B = 9801f;
        private const float A = 10000f / (B - 1f);

        private const string MinRefName = DetailsScreen_MassScreens_Patch.MinRefName;
        private const string MaxRefName = DetailsScreen_MassScreens_Patch.MaxRefName;

        private static float MassFromT(float t) => A * (Mathf.Pow(B, Mathf.Clamp01(t)) - 1f);
        private static float TFromMass(float mass)
        {
            mass = Mathf.Max(0f, mass);
            return Mathf.Log(1f + mass / A) / Mathf.Log(B);
        }

        private static bool TryResolveMassScreenMode(CapacityControlSideScreen screen, out bool isMin)
        {
            isMin = false;
            var ds = screen.GetComponentInParent<DetailsScreen>(true);
            if (ds == null) return false;

            var sideScreensField = AccessTools.Field(typeof(DetailsScreen), "sideScreens");
            var list = sideScreensField?.GetValue(ds) as List<DetailsScreen.SideScreenRef>;
            if (list == null) return false;

            var match = list.FirstOrDefault(r => r != null && r.screenInstance == (object)screen);
            if (match == null) return false;

            if (match.name == MinRefName) { isMin = true; return true; }
            if (match.name == MaxRefName) { isMin = false; return true; }
            return false;
        }

        // Cache the private UI fields we need to exclude/pick from
        private static readonly FieldInfo FI_Title = AccessTools.Field(typeof(CapacityControlSideScreen), "title");            // big title LocText
        private static readonly FieldInfo FI_Units = AccessTools.Field(typeof(CapacityControlSideScreen), "unitsLabel");       // "kg" LocText
        private static readonly FieldInfo FI_Input = AccessTools.Field(typeof(CapacityControlSideScreen), "numberInput");      // KNumberInputField
        private static readonly FieldInfo FI_Target = AccessTools.Field(typeof(CapacityControlSideScreen), "target");
        private static readonly FieldInfo FI_Slider = AccessTools.Field(typeof(CapacityControlSideScreen), "slider");

        // Robustly set the left-hand label that defaults to STRINGS.UI.UISIDESCREENS.CAPACITY_CONTROL_SIDE_SCREEN.MAX_LABEL
        private static void TrySetLeftTitle(CapacityControlSideScreen screen, string textNoColon)
        {
            try
            {
                if (screen == null) return;

                string desired = textNoColon?.EndsWith(":") == true ? textNoColon : $"{textNoColon}:";

                // 1) Prefer an exact private LocText field whose current text equals the localized MAX_LABEL or starts with "Max"
                var allFields = AccessTools.GetDeclaredFields(typeof(CapacityControlSideScreen));
                string maxLabelString = global::STRINGS.UI.UISIDESCREENS.CAPACITY_CONTROL_SIDE_SCREEN.MAX_LABEL; // e.g. "Max:"

                foreach (var f in allFields)
                {
                    if (f.FieldType != typeof(LocText)) continue;
                    var lt = f.GetValue(screen) as LocText;
                    if (lt == null) continue;

                    var curr = lt.text?.Trim();
                    if (string.IsNullOrEmpty(curr)) continue;

                    if (string.Equals(curr, maxLabelString, StringComparison.Ordinal)
                        || curr.StartsWith("Max", StringComparison.OrdinalIgnoreCase))
                    {
                        lt.SetText(desired);
                        return;
                    }
                }

                // 2) Fallback: scan children LocTexts while excluding the big title, units label, and anything under the number input
                var titleLt = FI_Title?.GetValue(screen) as LocText;
                var unitsLt = FI_Units?.GetValue(screen) as LocText;
                var num = FI_Input?.GetValue(screen) as KNumberInputField;
                Transform inputRoot = num ? num.transform : null;

                foreach (var lt in screen.GetComponentsInChildren<LocText>(true))
                {
                    if (lt == null) continue;
                    if (lt == titleLt) continue;
                    if (lt == unitsLt) continue;
                    if (inputRoot != null && lt.transform.IsChildOf(inputRoot)) continue;

                    // First remaining LocText is the left label in stock prefab
                    lt.SetText(desired);
                    return;
                }
            }
            catch
            {
                // best effort
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CapacityControlSideScreen.IsValidForTarget), typeof(GameObject))]
        public static bool IsValidForTarget_Prefix(CapacityControlSideScreen __instance, GameObject target, ref bool __result)
        {
            if (!TryResolveMassScreenMode(__instance, out _))
                return true;

            __result = target != null && target.GetComponent<Rephysicalized.CritterMassFilter>() != null;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CapacityControlSideScreen.SetTarget), typeof(GameObject))]
        public static bool SetTarget_Prefix(CapacityControlSideScreen __instance, GameObject new_target)
        {
            if (!TryResolveMassScreenMode(__instance, out var isMin))
                return true;

            // Ensure filter exists for existing saves
            var filter = new_target != null ? new_target.AddOrGet<Rephysicalized.CritterMassFilter>() : null;
            if (filter == null) return false;

            IUserControlledCapacity proxy = isMin
                ? (IUserControlledCapacity)new_target.AddOrGet<Rephysicalized.CritterMassMinProxy>()
                : (IUserControlledCapacity)new_target.AddOrGet<Rephysicalized.CritterMassMaxProxy>();

            var targetField = AccessTools.Field(typeof(CapacityControlSideScreen), "target");
            var sliderField = AccessTools.Field(typeof(CapacityControlSideScreen), "slider");
            var inputField = AccessTools.Field(typeof(CapacityControlSideScreen), "numberInput");
            var unitsField = AccessTools.Field(typeof(CapacityControlSideScreen), "unitsLabel");

            targetField?.SetValue(__instance, proxy);

            var slider = sliderField?.GetValue(__instance) as KSlider;
            var input = inputField?.GetValue(__instance) as KNumberInputField;
            var units = unitsField?.GetValue(__instance) as LocText;

            float mass = Mathf.Clamp(proxy.UserMaxCapacity, Rephysicalized.CritterMassFilter.AbsoluteMinKg, Rephysicalized.CritterMassFilter.AbsoluteMaxKg);

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                var wholeProp = slider.GetType().GetProperty("wholeNumbers");
                if (wholeProp != null && wholeProp.CanWrite) wholeProp.SetValue(slider, false, null);
                slider.value = TFromMass(mass);
            }

            if (input != null)
            {
                if (isMin)
                {
                    input.minValue = Rephysicalized.CritterMassFilter.AbsoluteMinKg;
                    input.maxValue = filter.MaxMassKg;
                }
                else
                {
                    input.minValue = filter.MinMassKg;
                    input.maxValue = Rephysicalized.CritterMassFilter.AbsoluteMaxKg;
                }
                input.decimalPlaces = 1;
                input.currentValue = mass;
                SetNumberDisplay(input, mass);
                input.Activate();
            }

            if (units != null)
                units.text = "kg";

            // Set the left label immediately
            TrySetLeftTitle(__instance, isMin ? "Min" : "Max");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ReceiveValueFromSlider", typeof(float))]
        public static bool ReceiveValueFromSlider_Prefix(CapacityControlSideScreen __instance, float newValue)
        {
            if (!TryResolveMassScreenMode(__instance, out var isMin))
                return true;

            var proxy = FI_Target?.GetValue(__instance) as IUserControlledCapacity;
            if (proxy == null) return false;

            var filter = (proxy as Component)?.GetComponent<Rephysicalized.CritterMassFilter>();
            if (filter == null) return false;

            float rawMass = Mathf.Clamp(MassFromT(newValue), Rephysicalized.CritterMassFilter.AbsoluteMinKg, Rephysicalized.CritterMassFilter.AbsoluteMaxKg);
            float clampedMass = isMin ? Mathf.Min(rawMass, filter.MaxMassKg) : Mathf.Max(rawMass, filter.MinMassKg);

            proxy.UserMaxCapacity = clampedMass;

            var input = FI_Input?.GetValue(__instance) as KNumberInputField;
            if (input != null)
            {
                input.currentValue = clampedMass;
                SetNumberDisplay(input, clampedMass);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ReceiveValueFromInput", typeof(float))]
        public static bool ReceiveValueFromInput_Prefix(CapacityControlSideScreen __instance, float newValue)
        {
            if (!TryResolveMassScreenMode(__instance, out var isMin))
                return true;

            var proxy = FI_Target?.GetValue(__instance) as IUserControlledCapacity;
            var slider = FI_Slider?.GetValue(__instance) as KSlider;
            if (proxy == null) return false;

            var filter = (proxy as Component)?.GetComponent<Rephysicalized.CritterMassFilter>();
            if (filter == null) return false;

            float mass = Mathf.Clamp(newValue, Rephysicalized.CritterMassFilter.AbsoluteMinKg, Rephysicalized.CritterMassFilter.AbsoluteMaxKg);
            mass = isMin ? Mathf.Min(mass, filter.MaxMassKg) : Mathf.Max(mass, filter.MinMassKg);

            proxy.UserMaxCapacity = mass;

            if (slider != null)
                slider.value = TFromMass(mass);

            var input = FI_Input?.GetValue(__instance) as KNumberInputField;
            if (input != null)
            {
                input.currentValue = mass;
                SetNumberDisplay(input, mass);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateMaxCapacityLabel")]
        public static bool UpdateMaxCapacityLabel_Prefix(CapacityControlSideScreen __instance)
        {
            if (!TryResolveMassScreenMode(__instance, out var isMin))
                return true;

            var proxy = FI_Target?.GetValue(__instance) as IUserControlledCapacity;
            var slider = FI_Slider?.GetValue(__instance) as KSlider;
            var input = FI_Input?.GetValue(__instance) as KNumberInputField;
            var units = FI_Units?.GetValue(__instance) as LocText;

            if (proxy == null) return false;

            var filter = (proxy as Component)?.GetComponent<Rephysicalized.CritterMassFilter>();
            float mass = Mathf.Clamp(proxy.UserMaxCapacity, Rephysicalized.CritterMassFilter.AbsoluteMinKg, Rephysicalized.CritterMassFilter.AbsoluteMaxKg);

            if (input != null)
            {
                if (isMin)
                {
                    input.minValue = Rephysicalized.CritterMassFilter.AbsoluteMinKg;
                    input.maxValue = filter != null ? filter.MaxMassKg : Rephysicalized.CritterMassFilter.AbsoluteMaxKg;
                }
                else
                {
                    input.minValue = filter != null ? filter.MinMassKg : Rephysicalized.CritterMassFilter.AbsoluteMinKg;
                    input.maxValue = Rephysicalized.CritterMassFilter.AbsoluteMaxKg;
                }
                input.decimalPlaces = 1;
                input.currentValue = mass;
                SetNumberDisplay(input, mass);
            }

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                var wholeProp = slider.GetType().GetProperty("wholeNumbers");
                if (wholeProp != null && wholeProp.CanWrite) wholeProp.SetValue(slider, false, null);
                slider.value = TFromMass(mass);
            }

            if (units != null)
                units.text = "kg";

            // Keep the label correct on refresh
            TrySetLeftTitle(__instance, isMin ? "Min" : "Max");

            return false;
        }

        private static void SetNumberDisplay(KNumberInputField input, float mass)
        {
            // Safely set inner input text via reflection (no direct text API)
            var inner = AccessTools.Field(input.GetType(), "inputField")?.GetValue(input);
            if (inner != null)
            {
                var textProp = inner.GetType().GetProperty("text");
                if (textProp != null && textProp.CanWrite)
                    textProp.SetValue(inner, mass.ToString("F1"), null);
                else
                {
                    var textField = inner.GetType().GetField("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    textField?.SetValue(inner, mass.ToString("F1"));
                }
            }
        }
    }
}