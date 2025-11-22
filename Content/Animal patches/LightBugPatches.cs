using HarmonyLib;
using KMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TUNING;
using UnityEngine;

namespace Rephysicalized
{

    public static class ShinebugLightSettings
    {
        // Tunable settings applied to Light2D per color
        public struct LightSettings
        {
            public Color OverlayColour;
            public float Range;
            public float Angle;
            public Vector2 Direction;
            public Vector2 Offset;
            public LightShape Shape;
            public bool DrawOverlay;
            public int Lux;
        }

        private static LightSettings CreateDefaults()
        {
            return new LightSettings
            {
                OverlayColour = LIGHT2D.LIGHTBUG_OVERLAYCOLOR,
                Range = 5f,
                Angle = 0.0f,
                Direction = LIGHT2D.LIGHTBUG_DIRECTION,
                Offset = LIGHT2D.LIGHTBUG_OFFSET,
                Shape = LightShape.Circle,
                DrawOverlay = true,
                Lux = 1800
            };
        }

        // Default (yellow / base) settings used for LIGHT2D.LIGHTBUG_COLOR
        public static LightSettings Default = CreateDefaults();

        // Individual settings per color. Adjust as desired.
        public static LightSettings Orange = CreateDefaults();
        public static LightSettings Purple = CreateDefaults();
        public static LightSettings Pink = CreateDefaults();
        public static LightSettings Blue = CreateDefaults();
        public static LightSettings Crystal = CreateDefaults();

        static ShinebugLightSettings()
        {
            // Default (yellow)
            Default.Lux = 1800;
            Default.Range = 5.0f;

            // Orange
            Orange.Lux = 3600;
            Orange.Range = 5.0f;

            // Purple
            Purple.Lux = 5400;
            Purple.Range = 5.5f;

            // Pink
            Pink.Lux = 7200;
            Pink.Range = 6.0f;

            // Blue
            Blue.Lux = 9000;
            Blue.Range = 6.5f;

            // Crystal
            Crystal.Lux = 18000;
            Crystal.Range = 7f;
        }

        // Map the given color to the appropriate settings, defaulting to Default
        public static LightSettings Resolve(Color color)
        {
            // Compare exactly to the game's constants (the configs pass these directly)
            if (color == LIGHT2D.LIGHTBUG_COLOR_ORANGE) return Orange;
            if (color == LIGHT2D.LIGHTBUG_COLOR_PINK) return Pink;
            if (color == LIGHT2D.LIGHTBUG_COLOR_PURPLE) return Purple;
            if (color == LIGHT2D.LIGHTBUG_COLOR_BLUE) return Blue;
            if (color == LIGHT2D.LIGHTBUG_COLOR_CRYSTAL) return Crystal;

            // LIGHT2D.LIGHTBUG_COLOR or any other non-black color not matching above
            return Default;
        }
    }

    [HarmonyPatch(typeof(BaseLightBugConfig), nameof(BaseLightBugConfig.BaseLightBug))]
    internal static class BaseLightBugConfig_BaseLightBug_Patch
    {
        // Postfix runs after vanilla code sets up the prefab, so we can safely override the light fields
        private static void Postfix(
            // Original method parameters (we only need 'lightColor' but can accept fewer if desired)
            string id, string name, string desc, string anim_file, string traitId,
            Color lightColor, EffectorValues decor, bool is_baby, string symbolOverridePrefix,
            // The created prefab
            ref GameObject __result)
        {
            if (__result == null)
                return;

            if (lightColor == Color.black)
                return;

            var light = __result.GetComponent<Light2D>();
            if (light == null)
                return;

            // Resolve and apply per-color settings
            var settings = ShinebugLightSettings.Resolve(lightColor);

            light.overlayColour = settings.OverlayColour;
            light.Range = settings.Range;
            light.Angle = settings.Angle;
            light.Direction = settings.Direction;
            light.Offset = settings.Offset;
            light.shape = settings.Shape;
            light.drawOverlay = settings.DrawOverlay;
            light.Lux = settings.Lux;
        }
    }

    [HarmonyPatch(typeof(BaseLightBugConfig), nameof(BaseLightBugConfig.BaseLightBug))]
    internal static class LightBug_DeathDrop_Transpiler
    {
        private const string TargetDropId = "Glass";
        private const float TargetDropKg = 0.1f;

        private const float WarningHighTempSentinel = 313.15f; // used to anchor the pattern
        private const float Epsilon = 0.0005f;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            for (int i = 0; i < code.Count; i++)
            {
                var inst = code[i];

                // Replace the default onDeathDropID "Meat" with "Glass"
                if (inst.opcode == OpCodes.Ldstr && inst.operand is string s && s == "Meat")
                {
                    code[i] = new CodeInstruction(OpCodes.Ldstr, TargetDropId);
                    continue;
                }

                // Replace the onDeathDropCount 0f that occurs shortly before the 313.15f literal
                if (IsLdcR4(inst, 0f) && IsFollowedBy(code, i, OpCodes.Ldc_R4, WarningHighTempSentinel, window: 8))
                {
                    code[i] = new CodeInstruction(OpCodes.Ldc_R4, TargetDropKg);
                    continue;
                }
            }

            return code;
        }

        private static bool IsLdcR4(CodeInstruction ci, float value)
        {
            if (ci.opcode != OpCodes.Ldc_R4) return false;
            if (ci.operand is float f) return Math.Abs(f - value) <= Epsilon;
            if (ci.operand is double d) return Math.Abs((float)d - value) <= Epsilon;
            return false;
        }

        private static bool IsFollowedBy(List<CodeInstruction> code, int startIdx, OpCode opcode, float operandF, int window)
        {
            int max = Math.Min(code.Count - 1, startIdx + window);
            for (int j = startIdx + 1; j <= max; j++)
            {
                var ci = code[j];
                if (ci.opcode == opcode)
                {
                    if (ci.operand is float f && Math.Abs(f - operandF) <= Epsilon) return true;
                    if (ci.operand is double d && Math.Abs((float)d - operandF) <= Epsilon) return true;
                }
            }
            return false;
        }
    }
}