using NUnit.Framework;
using Essentials.Console.Editor;
using System.Reflection;

namespace Essentials.Tests.Editor.Console
{
    public sealed class ConsoleLogFilterInstallerTests
    {
        private ConsoleLogFilterSettingsData originalSettings;

        [SetUp]
        public void SetUp()
        {
            originalSettings = ConsoleLogFilterSettings.Snapshot;
        }

        [TearDown]
        public void TearDown()
        {
            Restore(originalSettings);
        }

        [Test]
        public void AutomaticConsoleEntryMaintenance_Disabled_WhenNoActiveRulesExist()
        {
            Apply(enabled: true, cleanStackTraces: true);

            Assert.That(ConsoleLogFilterInstaller.AutomaticConsoleEntryMaintenanceEnabled, Is.False);
        }

        [Test]
        public void AutomaticConsoleEntryMaintenance_Enabled_WhenAtLeastOneRuleExists()
        {
            Apply(
                enabled: true,
                cleanStackTraces: true,
                new ConsoleLogFilterRuleData
                {
                    enabled = true,
                    matchType = ConsoleLogFilterMatchType.Contains,
                    caseSensitive = false,
                    pattern = "ignore me"
                });

            Assert.That(ConsoleLogFilterInstaller.AutomaticConsoleEntryMaintenanceEnabled, Is.True);
        }

        [Test]
        public void CleanStackTrace_StripsFramesFromMinimalEditorPackagePath()
        {
            MethodInfo method = typeof(ConsoleLogFilterInstaller).GetMethod(
                "ShouldStripStackTraceLine",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(
                method.Invoke(
                    null,
                    new object[]
                    {
                        "at Filter (Packages/com.essentials.minimaleditor/Editor/Console/ConsoleLogFilterInstaller.cs:1)"
                    }),
                Is.True);
        }

        private static void Apply(bool enabled, bool cleanStackTraces, params ConsoleLogFilterRuleData[] rules)
        {
            var settings = ConsoleLogFilterSettings.Data;
            settings.enabled = enabled;
            settings.warningsOnly = true;
            settings.cleanStackTraces = cleanStackTraces;
            settings.hideAssistantToolbarButton = true;
            settings.rules.Clear();

            if (rules != null)
                settings.rules.AddRange(rules);

            ConsoleLogFilterSettings.Save();
        }

        private static void Restore(ConsoleLogFilterSettingsData snapshot)
        {
            var settings = ConsoleLogFilterSettings.Data;
            settings.enabled = snapshot.enabled;
            settings.warningsOnly = snapshot.warningsOnly;
            settings.cleanStackTraces = snapshot.cleanStackTraces;
            settings.hideAssistantToolbarButton = snapshot.hideAssistantToolbarButton;
            settings.rules.Clear();
            settings.rules.AddRange(snapshot.rules);
            ConsoleLogFilterSettings.Save();
        }
    }
}
