using NUnit.Framework;
using UnityEditor;
using MinimalEditor.Editor;

namespace MinimalEditor.Tests.Editor.UnityEditor
{
    public sealed class UnityEditorPreferencesTests
    {
        (bool HasKey, bool Value) shortWindowTitleSnapshot;
        (bool HasKey, bool Value) hideStatusBarExtrasSnapshot;

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
