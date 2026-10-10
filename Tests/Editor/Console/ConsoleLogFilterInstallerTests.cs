using NUnit.Framework;
using Essentials.Console.Editor;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Essentials.Tests.Editor.Console
{
    public sealed class ConsoleLogFilterInstallerTests
    {
        private ConsoleLogFilterSettingsData originalSettings;
        private ILogHandler originalLogHandler;

        [SetUp]
        public void SetUp()
        {
            originalSettings = ConsoleLogFilterSettings.Snapshot;
            ConsoleLogFilterInstaller.Shutdown();
            originalLogHandler = Debug.unityLogger.logHandler;
            ConsoleLogFilterInstaller.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            ConsoleLogFilterInstaller.Shutdown();
            Debug.unityLogger.logHandler = originalLogHandler;
            Restore(originalSettings);
            ConsoleLogFilterInstaller.Initialize();
            ConsoleAssistantToolbarButtonController.Initialize();
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
                        "at Filter (Packages/com.martincalander.minimaleditor/Editor/Console/ConsoleLogFilterInstaller.cs:1)"
                    }),
                Is.True);
        }

        [Test]
        public void Initialize_Repeatedly_KeepsOneLoggerWrapperAndSettingsSubscription()
        {
            var recordingHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = recordingHandler;
            ApplyWithActiveRule();
            var filteringHandler = Debug.unityLogger.logHandler;

            ConsoleLogFilterInstaller.Initialize();
            ConsoleLogFilterInstaller.Initialize();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(filteringHandler));
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleLogFilterInstaller)), Is.EqualTo(1));
            filteringHandler.LogFormat(LogType.Warning, null, "ignore me");
            filteringHandler.LogFormat(LogType.Warning, null, "keep me");
            Assert.That(recordingHandler.MessageCount, Is.EqualTo(1));
        }

        [Test]
        public void EnteringMode_RebindsReplacedLoggerWithoutNestingWrappers()
        {
            var firstHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = firstHandler;
            ApplyWithActiveRule();
            var filteringHandler = Debug.unityLogger.logHandler;
            var replacementHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = replacementHandler;

            ConsoleLogFilterInstaller.OnEnteringMode();
            ConsoleLogFilterInstaller.OnEnteringMode();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(filteringHandler));
            filteringHandler.LogFormat(LogType.Warning, null, "keep me");
            Assert.That(firstHandler.MessageCount, Is.Zero);
            Assert.That(replacementHandler.MessageCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_RestoresPreviousLoggerAndUnsubscribesSettings()
        {
            var recordingHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = recordingHandler;
            ApplyWithActiveRule();

            ConsoleLogFilterInstaller.Shutdown();
            ConsoleLogFilterInstaller.Shutdown();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(recordingHandler));
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleLogFilterInstaller)), Is.Zero);

            ConsoleLogFilterInstaller.Initialize();
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleLogFilterInstaller)), Is.EqualTo(1));
            Assert.That(Debug.unityLogger.logHandler, Is.Not.SameAs(recordingHandler));
        }

        [Test]
        public void Shutdown_PreservesLoggerInstalledByAnotherOwner()
        {
            Debug.unityLogger.logHandler = new RecordingLogHandler();
            ApplyWithActiveRule();
            var replacementHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = replacementHandler;

            ConsoleLogFilterInstaller.Shutdown();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(replacementHandler));
        }

        [Test]
        public void ToolbarLifecycle_RegistersOnceAndUnsubscribesOnShutdown()
        {
            ConsoleAssistantToolbarButtonController.Initialize();
            ConsoleAssistantToolbarButtonController.Initialize();
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleAssistantToolbarButtonController)), Is.EqualTo(1));

            ConsoleAssistantToolbarButtonController.Shutdown();
            ConsoleAssistantToolbarButtonController.Shutdown();
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleAssistantToolbarButtonController)), Is.Zero);
        }

        private static int SettingsSubscriptionCount(Type owner)
        {
            var settingsChanged = typeof(ConsoleLogFilterSettings).GetField(
                "SettingsChanged", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(settingsChanged, Is.Not.Null);
            var callback = settingsChanged.GetValue(null) as Delegate;
            return callback?.GetInvocationList().Count(handler => handler.Method.DeclaringType == owner) ?? 0;
        }

        private static void ApplyWithActiveRule()
        {
            Apply(true, false, new ConsoleLogFilterRuleData
            {
                enabled = true,
                matchType = ConsoleLogFilterMatchType.Contains,
                pattern = "ignore me"
            });
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            internal int MessageCount { get; private set; }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                MessageCount++;
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                MessageCount++;
            }
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
