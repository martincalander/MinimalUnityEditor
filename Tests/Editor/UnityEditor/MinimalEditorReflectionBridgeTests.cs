using System;
using System.Reflection;
using MinimalEditor.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MinimalEditor.Tests.Editor.UnityEditor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class MinimalEditorReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void StatusBarExtrasHider_FallbackPoll_RepairsResetVisibility()
        {
            var type = typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
            var field = type?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(bool)),
                "AppStatusBar.m_DrawExtraFeatures must exist as a bool for the status bar extras toggle.");
            var timestamp = typeof(StatusBarExtrasHider).GetField("s_lastPollTimestamp", AnyStatic);
            var poll = typeof(StatusBarExtrasHider).GetMethod("PollFallback", AnyStatic);
            var previousInput = StatusBarExtrasHider.ActiveInput;
            var previousTimestamp = timestamp.GetValue(null);
            var statusBar = UnityEngine.ScriptableObject.CreateInstance(type);
            try
            {
                StatusBarExtrasHider.ActiveInput = () => true;
                Assert.That(field.GetValue(statusBar), Is.False);

                field.SetValue(statusBar, true);
                timestamp.SetValue(null, -15d);
                poll.Invoke(null, null);
                Assert.That(field.GetValue(statusBar), Is.False,
                    "The fallback must repair a reset bar even when the number of bars stays unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(statusBar);
                StatusBarExtrasHider.ActiveInput = previousInput;
                timestamp.SetValue(null, previousTimestamp);
            }
        }

        [Test]
        public void StatusBarExtrasHider_Inactive_LeavesUnityVisibilityAlone()
        {
            var type = typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
            var field = type?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(bool)));
            var previousInput = StatusBarExtrasHider.ActiveInput;
            var statusBar = UnityEngine.ScriptableObject.CreateInstance(type);
            try
            {
                StatusBarExtrasHider.ActiveInput = () => false;
                field.SetValue(statusBar, false);
                StatusBarExtrasHider.Apply();
                Assert.That(field.GetValue(statusBar), Is.False,
                    "An inactive hider must not override extras that Unity turned off.");

                StatusBarExtrasHider.ActiveInput = () => true;
                Assert.That(field.GetValue(statusBar), Is.False);

                StatusBarExtrasHider.ActiveInput = () => false;
                Assert.That(field.GetValue(statusBar), Is.False,
                    "Turning the hider off must preserve extras that Unity had already hidden.");

                field.SetValue(statusBar, true);
                StatusBarExtrasHider.ActiveInput = () => true;
                Assert.That(field.GetValue(statusBar), Is.False);
                StatusBarExtrasHider.ActiveInput = () => false;
                Assert.That(field.GetValue(statusBar), Is.True, "Restore extras that this hider actually changed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(statusBar);
                StatusBarExtrasHider.ActiveInput = previousInput;
            }
        }

        [Test]
        public void StatusBarExtrasHider_ReinitializationPreservesOwnershipForEachBar()
        {
            var type = typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
            var field = type?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(bool)));
            var previousInput = StatusBarExtrasHider.ActiveInput;
            var visibleBar = UnityEngine.ScriptableObject.CreateInstance(type);
            var hiddenBar = UnityEngine.ScriptableObject.CreateInstance(type);
            try
            {
                StatusBarExtrasHider.ActiveInput = () => false;
                field.SetValue(visibleBar, true);
                field.SetValue(hiddenBar, false);
                StatusBarExtrasHider.ActiveInput = () => true;
                Assert.That(field.GetValue(visibleBar), Is.False);
                Assert.That(field.GetValue(hiddenBar), Is.False);

                typeof(StatusBarExtrasHider).GetMethod("Shutdown", AnyStatic).Invoke(null, null);
                typeof(StatusBarExtrasHider).GetMethod("Initialize", AnyStatic).Invoke(null, null);
                StatusBarExtrasHider.ActiveInput = () => false;

                Assert.That(field.GetValue(visibleBar), Is.True);
                Assert.That(field.GetValue(hiddenBar), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visibleBar);
                UnityEngine.Object.DestroyImmediate(hiddenBar);
                StatusBarExtrasHider.ActiveInput = previousInput;
            }
        }

        [Test]
        public void WindowTitleShortener_Callback_UsesProductNameWhenEnabled()
        {
            Assert.That(WindowTitleShortener.TryRefreshTitle(), Is.True,
                "Window-title changes must be refreshable through the Unity reflection bridge.");
            Assume.That(string.IsNullOrWhiteSpace(PlayerSettings.productName), Is.False);
            var previousInput = WindowTitleShortener.ActiveInput;
            try
            {
                WindowTitleShortener.ActiveInput = () => true;
                Assert.That(BuildMainWindowTitle(), Is.EqualTo(PlayerSettings.productName),
                    "The title callback must be registered even when the Editor exposes it as an internal event.");
            }
            finally
            {
                WindowTitleShortener.ActiveInput = previousInput;
            }
        }

        [Test]
        public void WindowTitleShortener_RepeatedInitialization_RegistersOneCallback_AndShutdownRemovesIt()
        {
            var shortener = typeof(WindowTitleShortener);
            var initialize = shortener.GetMethod("Initialize", AnyStatic);
            var shutdown = shortener.GetMethod("Shutdown", AnyStatic);
            var previousInput = WindowTitleShortener.ActiveInput;
            try
            {
                initialize.Invoke(null, null);
                initialize.Invoke(null, null);
                var calls = 0;
                WindowTitleShortener.ActiveInput = () => { calls++; return false; };
                calls = 0;
                BuildMainWindowTitle();
                Assert.That(calls, Is.EqualTo(1), "Repeated initialization must not duplicate the title callback.");

                shutdown.Invoke(null, null);
                shutdown.Invoke(null, null);
                Assert.That(WindowTitleShortener.ActiveInput, Is.Null);
                calls = 0;
                // Set the field directly so checking teardown does not itself refresh the title.
                shortener.GetField("s_activeInput", AnyStatic).SetValue(null, new Func<bool>(() => { calls++; return false; }));
                BuildMainWindowTitle();
                Assert.That(calls, Is.Zero, "Code unloading must release the Editor's title callback.");
            }
            finally
            {
                initialize.Invoke(null, null);
                WindowTitleShortener.ActiveInput = previousInput;
            }
        }

        [Test]
        public void InspectorHeaderButtonsHider_HidesHelpAndPresetIndividually_AndRestoresUnityOrder()
        {
            var listField = typeof(EditorGUIUtility).GetField("s_EditorHeaderItemsMethods", AnyStatic);
            Assert.That(listField, Is.Not.Null, "Unity's Inspector header item list must remain reachable.");
            var help = typeof(EditorGUI).GetMethod("HelpIconButton", AnyStatic);
            var preset = FindPresetButton();
            Assert.That(help, Is.Not.Null, "EditorGUI.HelpIconButton must exist for the help button toggle.");
            Assert.That(preset, Is.Not.Null, "PresetSelector.DrawPresetButton must exist for the preset button toggle.");

            var previousHelp = InspectorHeaderButtonsHider.HideHelp;
            var previousPreset = InspectorHeaderButtonsHider.HidePreset;
            try
            {
                InspectorHeaderButtonsHider.HideHelp = false;
                InspectorHeaderButtonsHider.HidePreset = false;
                EnsureHeaderItemsFilled(listField);
                var original = Methods(listField);
                Assert.That(original, Has.Member(help));
                Assert.That(original, Has.Member(preset));

                InspectorHeaderButtonsHider.HideHelp = true;
                Assert.That(Methods(listField), Has.No.Member(help));
                Assert.That(Methods(listField), Has.Member(preset));

                InspectorHeaderButtonsHider.HidePreset = true;
                Assert.That(Methods(listField), Has.No.Member(help));
                Assert.That(Methods(listField), Has.No.Member(preset));

                InspectorHeaderButtonsHider.HideHelp = false;
                Assert.That(Methods(listField), Has.Member(help));
                Assert.That(Methods(listField), Has.No.Member(preset));

                InspectorHeaderButtonsHider.HidePreset = false;
                Assert.That(Methods(listField), Is.EqualTo(original), "Restored buttons must keep Unity's order.");
            }
            finally
            {
                InspectorHeaderButtonsHider.HideHelp = previousHelp;
                InspectorHeaderButtonsHider.HidePreset = previousPreset;
            }
        }

        [TestCase((int)EditorElementHider.Element.AssetPostprocessors)]
        [TestCase((int)EditorElementHider.Element.StatusBarDebugger)]
        public void EditorElementHider_TargetExists_AndPatches(int elementIndex)
        {
            var element = (EditorElementHider.Element)elementIndex;
            Assert.That(EditorElementHider.GetTargetMethod(element), Is.Not.Null,
                "Unity's draw method for this element must stay reachable.");
            Assert.That(EditorElementHider.IsSupported(element), Is.True, EditorElementHider.LastPatchError);

            EditorElementHider.TryPatch(element);
            Assert.That(EditorElementHider.IsPatched(element), Is.True, EditorElementHider.LastPatchError);
        }

        [Test]
        public void InspectorAssetLabelsHider_ReportsNoInspectedAssets_OnlyWhileFooterIsDrawn()
        {
            Assert.That(InspectorAssetLabelsHider.GetDrawMethod(), Is.Not.Null,
                "PropertyEditor.DrawPreviewAndLabels must exist for the Asset Labels toggle.");
#if UNITY_7000_0_OR_NEWER
            Assert.That(InspectorAssetLabelsHider.GetFooterMethod(), Is.Not.Null,
                "PropertyEditor.DrawFooter draws the footer of assets without a preview.");
#endif
            var inspectedAssets = InspectorAssetLabelsHider.GetInspectedAssetsMethod();
            Assert.That(inspectedAssets, Is.Not.Null,
                "PropertyEditor.GetInspectedAssets must return Object[] for the Asset Labels toggle.");
            Assert.That(InspectorAssetLabelsHider.IsSupported, Is.True, InspectorAssetLabelsHider.LastPatchError);

            var inspectors = UnityEngine.Resources.FindObjectsOfTypeAll(inspectedAssets.DeclaringType);
            Assume.That(inspectors, Is.Not.Empty, "The test needs an open Inspector window.");
            var asset = AssetDatabase.LoadMainAssetAtPath("Packages/com.martincalander.minimaleditor/package.json");
            Assume.That(asset, Is.Not.Null);

            var hadKey = EditorPrefs.HasKey(UnityEditorPreferences.HideInspectorAssetLabelsKey);
            var previous = InspectorAssetLabelsHider.Active;
            var previousSelection = Selection.objects;
            try
            {
                Selection.objects = new[] { asset };
                InspectorAssetLabelsHider.Active = true;
                Assert.That(InspectorAssetLabelsHider.IsPatched, Is.True, InspectorAssetLabelsHider.LastPatchError);
                Assert.That(inspectedAssets.Invoke(inspectors[0], null), Is.Not.Empty,
                    "Other Inspector code must still see the inspected assets.");
                Assert.That(InspectorAssetLabelsHider.FooterPrefix(), Is.False, "The footer must be skipped while hidden.");

                InspectorAssetLabelsHider.DrawPrefix();
                try
                {
                    Assert.That(inspectedAssets.Invoke(inspectors[0], null), Is.Empty);
                    InspectorAssetLabelsHider.Active = false;
                    Assert.That(InspectorAssetLabelsHider.FooterPrefix(), Is.True);
                    Assert.That(inspectedAssets.Invoke(inspectors[0], null), Is.Not.Empty);
                }
                finally
                {
                    InspectorAssetLabelsHider.DrawFinalizer();
                }
            }
            finally
            {
                Selection.objects = previousSelection;
                InspectorAssetLabelsHider.Active = previous;
                if (!hadKey)
                    EditorPrefs.DeleteKey(UnityEditorPreferences.HideInspectorAssetLabelsKey);
            }
        }

        static System.Collections.Generic.List<MethodInfo> Methods(FieldInfo listField)
        {
            var result = new System.Collections.Generic.List<MethodInfo>();
            foreach (Delegate callback in (System.Collections.IList)listField.GetValue(null))
                result.Add(callback.Method);
            return result;
        }

        static MethodInfo FindPresetButton()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UnityEditor.Presets.PresetSelector", false);
                if (type != null) return type.GetMethod("DrawPresetButton", AnyStatic);
            }
            return null;
        }

        static void EnsureHeaderItemsFilled(FieldInfo listField)
        {
            // Unity fills the list while drawing the first Inspector header after a reload.
            Assume.That(listField.GetValue(null), Is.Not.Null,
                "Unity has not drawn an Inspector header since the last reload.");
        }

        static object BuildMainWindowTitle()
        {
            var method = typeof(EditorApplication).GetMethod("BuildMainWindowTitle", AnyStatic);
            Assert.That(method, Is.Not.Null, "The Editor title callback must remain accessible.");
            return method.Invoke(null, null);
        }
    }
}
