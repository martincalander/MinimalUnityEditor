using NUnit.Framework;
using UnityEditor;
using MinimalEditor.Editor;

namespace MinimalEditor.Tests.Editor.UnityEditor
{
    public sealed class UnityEditorPreferencesTests
    {
        (bool HasKey, bool Value) shortWindowTitleSnapshot;
        (bool HasKey, bool Value) hideStatusBarExtrasSnapshot;
        (bool HasKey, bool Value) hideInspectorHelpSnapshot;
        (bool HasKey, bool Value) hideInspectorPresetSnapshot;
        (bool HasKey, bool Value) hideInspectorAssetLabelsSnapshot;
        (bool HasKey, bool Value) hideAssetPostprocessorsSnapshot;
        (bool HasKey, bool Value) hideStatusBarDebuggerSnapshot;

        [SetUp]
        public void SetUp()
        {
            shortWindowTitleSnapshot = Capture(UnityEditorPreferences.ShortWindowTitleKey);
            hideStatusBarExtrasSnapshot = Capture(UnityEditorPreferences.HideStatusBarExtrasKey);
            hideInspectorHelpSnapshot = Capture(UnityEditorPreferences.HideInspectorHelpButtonKey);
            hideInspectorPresetSnapshot = Capture(UnityEditorPreferences.HideInspectorPresetButtonKey);
            hideInspectorAssetLabelsSnapshot = Capture(UnityEditorPreferences.HideInspectorAssetLabelsKey);
            hideAssetPostprocessorsSnapshot = Capture(UnityEditorPreferences.HideAssetPostprocessorsKey);
            hideStatusBarDebuggerSnapshot = Capture(UnityEditorPreferences.HideStatusBarDebuggerKey);
        }

        [TearDown]
        public void TearDown()
        {
            Restore(
                UnityEditorPreferences.ShortWindowTitleKey,
                UnityEditorPreferences.ShortWindowTitleDefaultValue,
                shortWindowTitleSnapshot,
                value => EditorFeatureToggles.ShortWindowTitle = value);
            Restore(
                UnityEditorPreferences.HideStatusBarExtrasKey,
                UnityEditorPreferences.HideStatusBarExtrasDefaultValue,
                hideStatusBarExtrasSnapshot,
                value => EditorFeatureToggles.HideStatusBarExtras = value);
            Restore(
                UnityEditorPreferences.HideInspectorHelpButtonKey,
                UnityEditorPreferences.HideInspectorHelpButtonDefaultValue,
                hideInspectorHelpSnapshot,
                value => EditorFeatureToggles.HideInspectorHelpButton = value);
            Restore(
                UnityEditorPreferences.HideInspectorPresetButtonKey,
                UnityEditorPreferences.HideInspectorPresetButtonDefaultValue,
                hideInspectorPresetSnapshot,
                value => EditorFeatureToggles.HideInspectorPresetButton = value);
            Restore(
                UnityEditorPreferences.HideInspectorAssetLabelsKey,
                UnityEditorPreferences.HideInspectorAssetLabelsDefaultValue,
                hideInspectorAssetLabelsSnapshot,
                value => EditorFeatureToggles.HideInspectorAssetLabels = value);
            Restore(
                UnityEditorPreferences.HideAssetPostprocessorsKey,
                UnityEditorPreferences.HideAssetPostprocessorsDefaultValue,
                hideAssetPostprocessorsSnapshot,
                value => EditorFeatureToggles.HideAssetPostprocessors = value);
            Restore(
                UnityEditorPreferences.HideStatusBarDebuggerKey,
                UnityEditorPreferences.HideStatusBarDebuggerDefaultValue,
                hideStatusBarDebuggerSnapshot,
                value => EditorFeatureToggles.HideStatusBarDebugger = value);
        }

        [Test]
        public void SettingsProvider_IsUserScopedAtMinimalEditorUnityEditorPath()
        {
            SettingsProvider provider = UnityEditorPreferencesProvider.CreateProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Minimal Editor/Unity Editor"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));
        }

        [Test]
        public void Preferences_UseExistingFeatureDefaults_WhenKeysAreMissing()
        {
            EditorPrefs.DeleteKey(UnityEditorPreferences.ShortWindowTitleKey);
            EditorPrefs.DeleteKey(UnityEditorPreferences.HideStatusBarExtrasKey);

            Assert.That(EditorFeatureToggles.ShortWindowTitle, Is.True);
            Assert.That(EditorFeatureToggles.HideStatusBarExtras, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PublicToggle_PersistsBothValuesInEditorPrefs(bool shortTitle)
        {
            string key = shortTitle ? UnityEditorPreferences.ShortWindowTitleKey : UnityEditorPreferences.HideStatusBarExtrasKey;
            foreach (bool value in new[] { false, true })
            {
                if (shortTitle)
                    EditorFeatureToggles.ShortWindowTitle = value;
                else
                    EditorFeatureToggles.HideStatusBarExtras = value;
                Assert.That(EditorPrefs.GetBool(key, !value), Is.EqualTo(value));
            }
        }

        [Test]
        public void InspectorButtons_AreVisibleByDefault_AndPersistIndependently()
        {
            EditorFeatureToggles.HideInspectorHelpButton = false;
            EditorFeatureToggles.HideInspectorPresetButton = false;
            EditorPrefs.DeleteKey(UnityEditorPreferences.HideInspectorHelpButtonKey);
            EditorPrefs.DeleteKey(UnityEditorPreferences.HideInspectorPresetButtonKey);
            Assert.That(EditorFeatureToggles.HideInspectorHelpButton, Is.False);
            Assert.That(EditorFeatureToggles.HideInspectorPresetButton, Is.False);

            EditorFeatureToggles.HideInspectorHelpButton = true;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideInspectorHelpButtonKey, false), Is.True);
            Assert.That(EditorFeatureToggles.HideInspectorPresetButton, Is.False);

            EditorFeatureToggles.HideInspectorPresetButton = true;
            EditorFeatureToggles.HideInspectorHelpButton = false;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideInspectorHelpButtonKey, true), Is.False);
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideInspectorPresetButtonKey, false), Is.True);
        }

        [Test]
        public void InspectorAssetLabels_DefaultFollowsAddressables_AndStoresExplicitChoice()
        {
            var addressablesInstalled =
                global::UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.addressables") != null;
            Assert.That(UnityEditorPreferences.HideInspectorAssetLabelsDefaultValue, Is.EqualTo(addressablesInstalled));

            EditorPrefs.DeleteKey(UnityEditorPreferences.HideInspectorAssetLabelsKey);
            Assert.That(EditorFeatureToggles.HideInspectorAssetLabels, Is.EqualTo(addressablesInstalled));

            // Choosing the current default must still be stored, so it survives a later default change.
            EditorFeatureToggles.HideInspectorAssetLabels = addressablesInstalled;
            Assert.That(EditorPrefs.HasKey(UnityEditorPreferences.HideInspectorAssetLabelsKey), Is.True);

            EditorFeatureToggles.HideInspectorAssetLabels = !addressablesInstalled;
            Assert.That(
                EditorPrefs.GetBool(UnityEditorPreferences.HideInspectorAssetLabelsKey, addressablesInstalled),
                Is.EqualTo(!addressablesInstalled));
        }

        [Test]
        public void ElementHiders_AreOffByDefault_PersistIndependently_AndSkipOnlyWhileHidden()
        {
            EditorFeatureToggles.HideAssetPostprocessors = false;
            EditorFeatureToggles.HideStatusBarDebugger = false;
            EditorPrefs.DeleteKey(UnityEditorPreferences.HideAssetPostprocessorsKey);
            EditorPrefs.DeleteKey(UnityEditorPreferences.HideStatusBarDebuggerKey);
            Assert.That(EditorFeatureToggles.HideAssetPostprocessors, Is.False);
            Assert.That(EditorFeatureToggles.HideStatusBarDebugger, Is.False);
            Assert.That(EditorElementHider.AssetPostprocessorsPrefix(), Is.True);
            Assert.That(EditorElementHider.StatusBarDebuggerPrefix(), Is.True);

            EditorFeatureToggles.HideAssetPostprocessors = true;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideAssetPostprocessorsKey, false), Is.True);
            Assert.That(EditorElementHider.AssetPostprocessorsPrefix(), Is.False, "Unity's list must be skipped.");
            Assert.That(EditorElementHider.StatusBarDebuggerPrefix(), Is.True);
            Assert.That(EditorFeatureToggles.HideStatusBarDebugger, Is.False);

            EditorFeatureToggles.HideStatusBarDebugger = true;
            EditorFeatureToggles.HideAssetPostprocessors = false;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideStatusBarDebuggerKey, false), Is.True);
            Assert.That(EditorElementHider.StatusBarDebuggerPrefix(), Is.False, "Unity's button must be skipped.");
            Assert.That(EditorElementHider.AssetPostprocessorsPrefix(), Is.True);
        }

        static (bool HasKey, bool Value) Capture(string key)
        {
            return (EditorPrefs.HasKey(key), EditorPrefs.GetBool(key, false));
        }

        static void Restore(
            string key,
            bool defaultValue,
            (bool HasKey, bool Value) snapshot,
            System.Action<bool> apply)
        {
            apply(snapshot.HasKey ? snapshot.Value : defaultValue);

            if (!snapshot.HasKey)
                EditorPrefs.DeleteKey(key);
        }
    }
}
