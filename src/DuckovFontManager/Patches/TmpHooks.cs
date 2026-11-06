using System;
using System.Reflection;
using TMPro;

namespace DuckovFontManager
{
    /// <summary>Harmony postfixes on TextMeshPro. Postfix-only: the original code always runs.</summary>
    public static class TmpHooks
    {
        public static void OnEnablePostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying || __instance == null) return;
            try
            {
                var s = TextRegistry.GetOrCreate(__instance);
                FontApplier.Apply(s, false);
            }
            catch (Exception e) { Log.ErrorOnce("hook.enable", "OnEnable hook failed", e); }
        }

        public static void FontPostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying) return;
            try
            {
                var s = TextRegistry.TryGet(__instance);
                if (s == null) return; // captured on enable
                var f = __instance.font;
                if (f == null || FontLibrary.IsOurs(f) || f == s.OrigFont) return;
                s.OrigFont = f;
                s.OrigMat = __instance.fontSharedMaterial;
                s.ExclusionVersion = -1;
                FontLibrary.RegisterGameFont(f, RuleEngine.Categorize(f));
                s.SaveToMarker();
                FontApplier.Apply(s, true);
            }
            catch (Exception e) { Log.ErrorOnce("hook.font", "font hook failed", e); }
        }

        public static void MaterialPostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying) return;
            try
            {
                var s = TextRegistry.TryGet(__instance);
                if (s == null) return;
                var m = __instance.fontSharedMaterial;
                if (m == null || MaterialMapper.IsOurs(m) || m == s.OrigMat) return;
                if (s.AppliedFont != null && m == s.AppliedFont.material) return;
                s.OrigMat = m;
                s.SaveToMarker();
                FontApplier.Apply(s, true);
            }
            catch (Exception e) { Log.ErrorOnce("hook.mat", "material hook failed", e); }
        }

        public static void SizePostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying) return;
            var s = TextRegistry.TryGet(__instance);
            if (s == null) return;
            s.OrigSize = __instance.fontSize;
            s.SaveToMarker();
            FontApplier.Apply(s, true);
        }

        public static void SizeMinPostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying) return;
            var s = TextRegistry.TryGet(__instance);
            if (s == null) return;
            s.OrigMin = __instance.fontSizeMin;
            s.SaveToMarker();
            FontApplier.Apply(s, true);
        }

        public static void SizeMaxPostfix(TMP_Text __instance)
        {
            if (!FontManagerCore.Active || FontApplier.Applying) return;
            var s = TextRegistry.TryGet(__instance);
            if (s == null) return;
            s.OrigMax = __instance.fontSizeMax;
            s.SaveToMarker();
            FontApplier.Apply(s, true);
        }
    }

    internal static class PatchService
    {
        public const string HarmonyId = "com.duckov.fontmanager";
        private const int PriorityLast = 0; // Harmony Priority.Last: run after other mods' postfixes

        /// <summary>True when OnEnable hooks are in place (otherwise periodic scanning is used).</summary>
        public static bool HooksActive { get; private set; }
        public static int PatchCount { get; private set; }

        public static void Apply()
        {
            PatchCount = 0;
            HooksActive = false;
            if (!HarmonyShim.Init(HarmonyId, Paths.ModDir)) return;

            const BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            int enableHooks = 0;
            if (Patch(typeof(TextMeshProUGUI).GetMethod("OnEnable", inst), nameof(TmpHooks.OnEnablePostfix))) enableHooks++;
            if (Patch(typeof(TextMeshPro).GetMethod("OnEnable", inst), nameof(TmpHooks.OnEnablePostfix))) enableHooks++;
            HooksActive = enableHooks == 2;

            Patch(Setter("font"), nameof(TmpHooks.FontPostfix));
            Patch(Setter("fontSharedMaterial"), nameof(TmpHooks.MaterialPostfix));
            Patch(Setter("fontMaterial"), nameof(TmpHooks.MaterialPostfix));
            Patch(Setter("fontSize"), nameof(TmpHooks.SizePostfix));
            Patch(Setter("fontSizeMin"), nameof(TmpHooks.SizeMinPostfix));
            Patch(Setter("fontSizeMax"), nameof(TmpHooks.SizeMaxPostfix));
            Log.Info($"Applied {PatchCount} patches (OnEnable hooks: {(HooksActive ? "yes" : "no, using scan mode")}).");
        }

        public static void Remove()
        {
            HarmonyShim.UnpatchAll(HarmonyId);
            HooksActive = false;
            PatchCount = 0;
        }

        private static MethodInfo Setter(string property) =>
            typeof(TMP_Text).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);

        private static bool Patch(MethodBase original, string hookName)
        {
            if (original == null)
            {
                Log.Warn($"Patch target for {hookName} not found");
                return false;
            }
            var hook = typeof(TmpHooks).GetMethod(hookName, BindingFlags.Static | BindingFlags.Public);
            bool ok = HarmonyShim.Postfix(original, hook, PriorityLast);
            if (ok) PatchCount++;
            return ok;
        }
    }
}
