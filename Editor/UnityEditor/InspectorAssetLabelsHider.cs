using System;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.Editor
{
    // Hides the Asset Labels and AssetBundle footer that Unity draws at the bottom of asset Inspectors.
    // Unity draws the footer on its own for assets without a preview, and below the preview otherwise.
    // This skips the footer itself, and reports no inspected assets while the preview area is drawn so
    // Unity does not leave an empty Asset Labels bar behind. Previews are unaffected.
    // Editor configuration persists across Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class InspectorAssetLabelsHider
    {
        internal const string HarmonyId = "com.martincalander.minimaleditor.editor.inspector-asset-labels";
        internal const string DrawMethodName = "DrawPreviewAndLabels";
        internal const string FooterMethodName = "DrawFooter";
        internal const string InspectedAssetsMethodName = "GetInspectedAssets";

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        static readonly Type PropertyEditorType = EditorAssembly.GetType("UnityEditor.PropertyEditor");

        static object s_Harmony;
        static bool s_Patched;
        static bool s_PatchFailed;
        static bool s_DrawingFooter;

        internal static string LastPatchError { get; private set; }
        internal static bool IsPatched => s_Patched;

        internal static bool IsSupported =>
            !s_PatchFailed && GetDrawMethod() != null && GetInspectedAssetsMethod() != null && GetHarmonyType() != null;

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
            // Patching takes tens of milliseconds, so code loads skip it while the footer stays visible.
            if (Active)
                TryPatch();
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
                RemovePatches();
            }
            catch
            {
                // Leave the Inspector usable if patch cleanup fails during code reload.
            }
            finally
            {
                s_Harmony = null;
                s_Patched = false;
                s_PatchFailed = false;
                s_DrawingFooter = false;
                LastPatchError = null;
            }
        }

        internal static bool Active
        {
            get => UnityEditorPreferences.HideInspectorAssetLabels;
            set
            {
                var changed = Active != value;
                // Always store the choice so it wins over the Addressables-based default.
                UnityEditorPreferences.HideInspectorAssetLabels = value;
                if (value)
                    TryPatch();
                if (changed)
                    RefreshInspectors();
            }
        }

        internal static MethodInfo GetDrawMethod() =>
            PropertyEditorType?.GetMethod(DrawMethodName, AnyInstance, null, Type.EmptyTypes, null);

        // Editors that draw the footer only inside the preview area have no separate footer method.
        internal static MethodInfo GetFooterMethod() =>
            PropertyEditorType?.GetMethod(FooterMethodName, AnyInstance, null, Type.EmptyTypes, null);

        internal static MethodInfo GetInspectedAssetsMethod()
        {
            var method = PropertyEditorType?.GetMethod(InspectedAssetsMethodName, AnyInstance, null, Type.EmptyTypes, null);
            return method != null && method.ReturnType == typeof(UnityEngine.Object[]) ? method : null;
        }

        internal static void TryPatch()
        {
            if (s_Patched || s_PatchFailed)
                return;

            var drawMethod = GetDrawMethod();
            var footerMethod = GetFooterMethod();
            var inspectedAssetsMethod = GetInspectedAssetsMethod();
            var harmonyType = GetHarmonyType();
            if (drawMethod == null || inspectedAssetsMethod == null || harmonyType == null)
                return;

            try
            {
                LastPatchError = null;
                s_Harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(drawMethod, prefix: Local(nameof(DrawPrefix)), finalizer: Local(nameof(DrawFinalizer)));
                if (footerMethod != null)
                    Patch(footerMethod, prefix: Local(nameof(FooterPrefix)));
                Patch(inspectedAssetsMethod, postfix: Local(nameof(InspectedAssetsPostfix)));
                s_Patched = true;
            }
            catch (Exception exception)
            {
                LastPatchError = exception.ToString();
                s_PatchFailed = true;
                try
                {
                    RemovePatches();
                }
                catch
                {
                    // Keep the original failure while cleaning up best-effort.
                }

                s_Harmony = null;
                s_Patched = false;
            }
        }

        static MethodInfo Local(string name) => typeof(InspectorAssetLabelsHider).GetMethod(name, AnyStatic);

        static void Patch(MethodInfo target, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo finalizer = null)
        {
            var harmonyType = s_Harmony.GetType();
            var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: true);
            var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) })
                ?? throw new MissingMethodException(harmonyMethodType.FullName, ".ctor(MethodInfo)");
            var patchMethod = harmonyType.GetMethod("Patch", new[]
            {
                typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType
            }) ?? throw new MissingMethodException(harmonyType.FullName, "Patch");
            patchMethod.Invoke(s_Harmony, new[]
            {
                target,
                prefix == null ? null : constructor.Invoke(new object[] { prefix }),
                postfix == null ? null : constructor.Invoke(new object[] { postfix }),
                null,
                finalizer == null ? null : constructor.Invoke(new object[] { finalizer })
            });
        }

        static Type GetHarmonyType()
        {
            // Reflection keeps the selected Harmony runtime build out of the script API surface.
            return Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
        }

        static void RemovePatches()
        {
            if (s_Harmony == null)
                return;

            var harmonyType = s_Harmony.GetType();
            var method = harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) })
                ?? throw new MissingMethodException(harmonyType.FullName, "UnpatchAll");
            method.Invoke(s_Harmony, new object[] { HarmonyId });
        }

        internal static void DrawPrefix()
        {
            s_DrawingFooter = true;
        }

        internal static void DrawFinalizer()
        {
            s_DrawingFooter = false;
        }

        // Returning false skips Unity's footer.
        internal static bool FooterPrefix()
        {
            return !Active;
        }

        static void InspectedAssetsPostfix(ref UnityEngine.Object[] __result)
        {
            if (s_DrawingFooter && Active)
                __result = Array.Empty<UnityEngine.Object>();
        }

        // Unity keeps the footer's last layout until its Inspector is rebuilt, so a repaint is not enough.
        internal static void RefreshInspectors()
        {
            if (PropertyEditorType == null)
                return;

            try
            {
                var trackerProperty = PropertyEditorType.GetProperty("tracker", AnyInstance);
                foreach (var inspector in UnityEngine.Resources.FindObjectsOfTypeAll(PropertyEditorType))
                {
                    if (!(inspector is EditorWindow window))
                        continue;
                    (trackerProperty?.GetValue(window) as ActiveEditorTracker)?.ForceRebuild();
                    window.Repaint();
                }
            }
            catch
            {
                // Refreshing is cosmetic; the next Inspector rebuild shows the change.
            }
        }
    }
}
