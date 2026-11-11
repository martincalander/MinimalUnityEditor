using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;

namespace MinimalEditor.Editor
{
    // Hides single Editor elements that Unity draws from one method each, by skipping that method:
    // the Asset PostProcessors list in importer Inspectors and the debugger button in the status bar.
    // Editor configuration persists across Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class EditorElementHider
    {
        internal const string HarmonyId = "com.martincalander.minimaleditor.editor.element-hider";

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal enum Element
        {
            AssetPostprocessors,
            StatusBarDebugger
        }

        static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        static readonly bool[] s_Patched = new bool[2];
        static readonly bool[] s_Failed = new bool[2];
        static object s_Harmony;

        internal static string LastPatchError { get; private set; }

        internal static bool IsPatched(Element element) => s_Patched[(int)element];

        internal static bool IsSupported(Element element) =>
            !s_Failed[(int)element] && GetTargetMethod(element) != null && GetHarmonyType() != null;

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
            // Patching takes tens of milliseconds, so code loads skip elements that stay visible.
            if (HideAssetPostprocessors)
                TryPatch(Element.AssetPostprocessors);
            if (HideStatusBarDebugger)
                TryPatch(Element.StatusBarDebugger);
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Shutdown()
        {
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            try
            {
                s_Harmony?.GetType().GetMethod("UnpatchAll", new[] { typeof(string) })
                    ?.Invoke(s_Harmony, new object[] { HarmonyId });
            }
            catch
            {
                // Leave the Editor usable if patch cleanup fails during code reload.
            }
            finally
            {
                s_Harmony = null;
                Array.Clear(s_Patched, 0, s_Patched.Length);
                Array.Clear(s_Failed, 0, s_Failed.Length);
                LastPatchError = null;
            }
        }

        internal static bool HideAssetPostprocessors
        {
            get => UnityEditorPreferences.HideAssetPostprocessors;
            set
            {
                if (HideAssetPostprocessors == value) return;
                UnityEditorPreferences.HideAssetPostprocessors = value;
                if (value)
                    TryPatch(Element.AssetPostprocessors);
                InspectorAssetLabelsHider.RefreshInspectors();
            }
        }

        internal static bool HideStatusBarDebugger
        {
            get => UnityEditorPreferences.HideStatusBarDebugger;
            set
            {
                if (HideStatusBarDebugger == value) return;
                UnityEditorPreferences.HideStatusBarDebugger = value;
                if (value)
                    TryPatch(Element.StatusBarDebugger);
                InternalEditorUtility.RepaintAllViews();
            }
        }

        internal static MethodInfo GetTargetMethod(Element element)
        {
            switch (element)
            {
                case Element.AssetPostprocessors:
                    return GetMethod("UnityEditor.AssetImporters.AssetImporterEditor", "DrawAssetPostprocessors");
                case Element.StatusBarDebugger:
                    return GetMethod("UnityEditor.ManagedDebuggerToggle", "OnGUI");
                default:
                    return null;
            }
        }

        static MethodInfo GetMethod(string typeName, string methodName)
        {
            var method = EditorAssembly.GetType(typeName)?.GetMethod(methodName, AnyInstance, null, Type.EmptyTypes, null);
            return method != null && method.ReturnType == typeof(void) ? method : null;
        }

        internal static void TryPatch(Element element)
        {
            var index = (int)element;
            if (s_Patched[index] || s_Failed[index])
                return;

            var target = GetTargetMethod(element);
            var harmonyType = GetHarmonyType();
            if (target == null || harmonyType == null)
                return;

            try
            {
                LastPatchError = null;
                if (s_Harmony == null)
                    s_Harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });

                var prefixName = element == Element.AssetPostprocessors
                    ? nameof(AssetPostprocessorsPrefix)
                    : nameof(StatusBarDebuggerPrefix);
                var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: true);
                var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) })
                    ?? throw new MissingMethodException(harmonyMethodType.FullName, ".ctor(MethodInfo)");
                var patchMethod = harmonyType.GetMethod("Patch", new[]
                {
                    typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType
                }) ?? throw new MissingMethodException(harmonyType.FullName, "Patch");
                var prefix = constructor.Invoke(new object[] { typeof(EditorElementHider).GetMethod(prefixName, AnyStatic) });
                patchMethod.Invoke(s_Harmony, new[] { target, prefix, null, null, null });
                s_Patched[index] = true;
            }
            catch (Exception exception)
            {
                // A failed patch leaves Unity's method untouched, so the element simply stays visible.
                LastPatchError = exception.ToString();
                s_Failed[index] = true;
            }
        }

        static Type GetHarmonyType()
        {
            // Reflection keeps the selected Harmony runtime build out of the script API surface.
            return Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
        }

        // Returning false skips Unity's method.
        internal static bool AssetPostprocessorsPrefix()
        {
            return !HideAssetPostprocessors;
        }

        internal static bool StatusBarDebuggerPrefix()
        {
            return !HideStatusBarDebugger;
        }
    }
}
