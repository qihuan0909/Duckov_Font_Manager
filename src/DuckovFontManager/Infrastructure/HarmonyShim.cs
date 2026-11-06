using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DuckovFontManager
{
    /// <summary>
    /// Version-agnostic access to Harmony via reflection.
    /// Several Harmony builds can be present at once (Harmony 2.4.1 bundled by mods, HarmonyX 2.9 from
    /// BepInEx). Their public signatures differ (e.g. HarmonyX's Patch() has an extra parameter), so
    /// compiling against one of them causes MissingMethodException with the other. We bind at runtime to
    /// whichever "0Harmony" the process already uses and only rely on members common to all versions.
    /// </summary>
    internal static class HarmonyShim
    {
        private static object _harmony;
        private static Type _harmonyMethodType;
        private static MethodInfo _patch;
        private static MethodInfo _unpatchAll;

        public static string Description { get; private set; } = "none";
        public static bool Ready => _harmony != null;

        public static bool Init(string id, string modDir)
        {
            if (_harmony != null) return true;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "0Harmony" && a.GetType("HarmonyLib.Harmony", false) != null);
                if (asm == null && !string.IsNullOrEmpty(modDir))
                {
                    var bundled = Path.Combine(modDir, "0Harmony.dll");
                    if (File.Exists(bundled)) asm = Assembly.LoadFrom(bundled);
                }
                if (asm == null)
                {
                    Log.Warn("Harmony not found; falling back to scan mode.");
                    return false;
                }

                var harmonyType = asm.GetType("HarmonyLib.Harmony", true);
                _harmonyMethodType = asm.GetType("HarmonyLib.HarmonyMethod", true);
                _patch = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Patch")
                    .Where(m => { var p = m.GetParameters(); return p.Length >= 2 && p[0].ParameterType == typeof(MethodBase); })
                    .OrderBy(m => m.GetParameters().Length)
                    .FirstOrDefault();
                _unpatchAll = harmonyType.GetMethod("UnpatchAll", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                if (_patch == null) throw new MissingMethodException("HarmonyLib.Harmony.Patch");

                _harmony = Activator.CreateInstance(harmonyType, id);
                Description = $"{asm.GetName().Name} {asm.GetName().Version}";
                Log.Info($"Using Harmony: {Description} ({SafeLocation(asm)})");
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Harmony initialisation failed; falling back to scan mode.", e);
                _harmony = null;
                return false;
            }
        }

        /// <summary>Adds a postfix. Returns false (and logs) on failure instead of throwing.</summary>
        public static bool Postfix(MethodBase original, MethodInfo postfix, int priority)
        {
            if (_harmony == null || original == null || postfix == null) return false;
            try
            {
                var hm = Activator.CreateInstance(_harmonyMethodType, postfix);
                var prio = _harmonyMethodType.GetField("priority", BindingFlags.Public | BindingFlags.Instance);
                prio?.SetValue(hm, priority);

                var ps = _patch.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    if (i == 0) args[i] = original;
                    else if (ps[i].Name == "postfix") args[i] = hm;
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                }
                _patch.Invoke(_harmony, args);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to patch {original.DeclaringType?.Name}.{original.Name}", e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e);
                return false;
            }
        }

        public static void UnpatchAll(string id)
        {
            if (_harmony == null || _unpatchAll == null) return;
            try { _unpatchAll.Invoke(_harmony, new object[] { id }); }
            catch (Exception e) { Log.Error("Unpatch failed", e); }
        }

        private static string SafeLocation(Assembly a)
        {
            try { return a.Location; } catch { return "?"; }
        }
    }
}
