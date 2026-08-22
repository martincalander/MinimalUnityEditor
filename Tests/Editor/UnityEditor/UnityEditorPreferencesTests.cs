using NUnit.Framework;
using UnityEditor;
using Essentials.Editor;

namespace Essentials.Tests.Editor.UnityEditor
{
    public sealed class UnityEditorPreferencesTests
    {
        PreferenceSnapshot shortWindowTitleSnapshot;
        PreferenceSnapshot hideStatusBarExtrasSnapshot;

        [SetUp]
        public void SetUp()
        {
            shortWindowTitleSnapshot = Capture(UnityEditorPreferences.ShortWindowTitleKey);
            hideStatusBarExtrasSnapshot = Capture(UnityEditorPreferences.HideStatusBarExtrasKey);
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
        }

        [Test]
        public void SettingsProvider_IsUserScopedAtEssentialsUnityEditorPath()
        {
            SettingsProvider provider = UnityEditorPreferencesProvider.CreateProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Essentials/Unity Editor"));
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

        [Test]
        public void PublicShortWindowTitleToggle_PersistsInEditorPrefs()
        {
            EditorFeatureToggles.ShortWindowTitle = false;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.ShortWindowTitleKey, true), Is.False);

            EditorFeatureToggles.ShortWindowTitle = true;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.ShortWindowTitleKey, false), Is.True);
        }

        [Test]
        public void PublicStatusBarExtrasToggle_PersistsInEditorPrefs()
        {
            EditorFeatureToggles.HideStatusBarExtras = false;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideStatusBarExtrasKey, true), Is.False);

            EditorFeatureToggles.HideStatusBarExtras = true;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.HideStatusBarExtrasKey, false), Is.True);
        }

        static PreferenceSnapshot Capture(string key)
        {
            return new PreferenceSnapshot(EditorPrefs.HasKey(key), EditorPrefs.GetBool(key, false));
        }

        static void Restore(
            string key,
            bool defaultValue,
            PreferenceSnapshot snapshot,
            System.Action<bool> apply)
        {
            apply(snapshot.HasKey ? snapshot.Value : defaultValue);

            if (!snapshot.HasKey)
                EditorPrefs.DeleteKey(key);
        }

        readonly struct PreferenceSnapshot
        {
            internal PreferenceSnapshot(bool hasKey, bool value)
            {
                HasKey = hasKey;
                Value = value;
            }

            internal bool HasKey { get; }
            internal bool Value { get; }
        }
    }
}
