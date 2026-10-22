using NUnit.Framework;
using MinimalEditor.Console.Editor;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.Console
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ConsoleLogFilterInstallerTests
    {
        private ConsoleLogFilterSettingsData originalSettings;
        private ILogHandler originalLogHandler;
        private bool originalLoggerOwnership;
        private static readonly FieldInfo LoggerOwnership = typeof(ConsoleLogFilterInstaller).GetField(
            "s_hasInstalledLogHandler", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            originalSettings = ConsoleLogFilterSettings.Snapshot;
            ConsoleLogFilterInstaller.Shutdown();
            originalLogHandler = Debug.unityLogger.logHandler;
            originalLoggerOwnership = (bool)LoggerOwnership.GetValue(null);
            LoggerOwnership.SetValue(null, false);
            Apply(cleanStackTraces: true);
            ConsoleLogFilterInstaller.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            ConsoleLogFilterInstaller.Shutdown();
            Debug.unityLogger.logHandler = originalLogHandler;
            LoggerOwnership.SetValue(null, originalLoggerOwnership);
            Restore(originalSettings);
            ConsoleLogFilterInstaller.Initialize();
            ConsoleAssistantToolbarButtonController.Initialize();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticConsoleEntryMaintenance_RequiresActiveRules(bool hasActiveRule)
        {
            if (hasActiveRule)
                ApplyWithActiveRule(cleanStackTraces: true);
            else
                Apply(cleanStackTraces: true);

            Assert.That(ConsoleLogFilterInstaller.AutomaticConsoleEntryMaintenanceEnabled, Is.EqualTo(hasActiveRule));
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

        [TestCase(false)]
        [TestCase(true)]
        public void EnteringMode_PreservesForeignLoggerOwnership(bool wrapsFilter)
        {
            var firstHandler = new RecordingLogHandler();
            Debug.unityLogger.logHandler = firstHandler;
            ApplyWithActiveRule();
            var filteringHandler = Debug.unityLogger.logHandler;
            var replacementHandler = new RecordingLogHandler(wrapsFilter ? filteringHandler : null);
            Debug.unityLogger.logHandler = replacementHandler;

            ConsoleLogFilterInstaller.OnEnteringMode();
            ConsoleLogFilterInstaller.Initialize();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(replacementHandler));
            replacementHandler.LogFormat(LogType.Warning, null, "keep me");
            Assert.That(firstHandler.MessageCount, Is.EqualTo(wrapsFilter ? 1 : 0));
            Assert.That(replacementHandler.MessageCount, Is.EqualTo(1));

            ConsoleLogFilterInstaller.Shutdown();
            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(replacementHandler));
            replacementHandler.LogFormat(LogType.Warning, null, "ignore me");
            Assert.That(firstHandler.MessageCount, Is.EqualTo(wrapsFilter ? 2 : 0),
                "A wrapper retained by another logger must stop filtering after shutdown.");

            ConsoleLogFilterInstaller.Initialize();
            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(replacementHandler),
                "Reinitializing must not wrap a foreign logger around our retained instance.");
            replacementHandler.LogFormat(LogType.Warning, null, "keep me");
            Assert.That(firstHandler.MessageCount, Is.EqualTo(wrapsFilter ? 3 : 0));
        }

        [Test]
        public void CleanStackMode_DisablesFilterRetainedByForeignLogger()
        {
            var inner = new RecordingLogHandler();
            Debug.unityLogger.logHandler = inner;
            ApplyWithActiveRule();
            var replacement = new RecordingLogHandler(Debug.unityLogger.logHandler);
            Debug.unityLogger.logHandler = replacement;

            ApplyWithActiveRule(cleanStackTraces: true);
            replacement.LogFormat(LogType.Warning, null, "ignore me");

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(replacement));
            Assert.That(inner.MessageCount, Is.EqualTo(1));
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
        public void ToolbarLifecycle_RegistersOnceAndUnsubscribesOnShutdown()
        {
            ConsoleAssistantToolbarButtonController.Initialize();
            ConsoleAssistantToolbarButtonController.Initialize();
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleAssistantToolbarButtonController)), Is.EqualTo(1));

            ConsoleAssistantToolbarButtonController.Shutdown();
            ConsoleAssistantToolbarButtonController.Shutdown();
            Assert.That(SettingsSubscriptionCount(typeof(ConsoleAssistantToolbarButtonController)), Is.Zero);
        }

        [TestCase(LogType.Log, 0)]
        [TestCase(LogType.Warning, 1)]
        [TestCase(LogType.Error, 0)]
        [TestCase(LogType.Assert, 0)]
        [TestCase(LogType.Exception, 0)]
        public void WarningsOnly_LiveHandlerFormatsAndFiltersOnlyWarnings(LogType logType, int formattingCount)
        {
            var inner = new RecordingLogHandler();
            Debug.unityLogger.logHandler = inner;
            ApplyWithActiveRule();
            var matching = new CountingFormatArgument("ignore me");
            var kept = new CountingFormatArgument();

            Debug.unityLogger.logHandler.LogFormat(logType, null, "{0}", matching);
            Debug.unityLogger.logHandler.LogFormat(logType, null, "{0}", kept);

            Assert.That(matching.FormatCount, Is.EqualTo(formattingCount));
            Assert.That(kept.FormatCount, Is.EqualTo(formattingCount));
            Assert.That(inner.MessageCount, Is.EqualTo(logType == LogType.Warning ? 1 : 2));
        }

        [Test]
        public void WarningsOnly_DoesNotFormatExceptions()
        {
            var inner = new RecordingLogHandler();
            Debug.unityLogger.logHandler = inner;
            ApplyWithActiveRule();
            var exception = new CountingException();

            Debug.unityLogger.logHandler.LogException(exception, null);

            Assert.That(exception.FormatCount, Is.Zero);
            Assert.That(inner.MessageCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_RetainedHandlerDoesNotFormatArguments()
        {
            var inner = new RecordingLogHandler();
            Debug.unityLogger.logHandler = inner;
            ApplyWithActiveRule();
            var retainedHandler = Debug.unityLogger.logHandler;
            ConsoleLogFilterInstaller.Shutdown();
            var argument = new CountingFormatArgument();

            retainedHandler.LogFormat(LogType.Warning, null, "{0}", argument);

            Assert.That(argument.FormatCount, Is.Zero);
            Assert.That(inner.MessageCount, Is.EqualTo(1));
        }

        [TestCase("Log", "ignore me", false)]
        [TestCase("ScriptingLog", "ignore me: warning CS1234", false)]
        [TestCase("ScriptingError", "ignore me: warning CS1234", false)]
        [TestCase("ScriptingException", "ignore me", false)]
        [TestCase("ScriptCompileError", "ignore me", false)]
        [TestCase("ScriptingWarning", "ignore me", true)]
        [TestCase("ScriptCompileWarning", "ignore me", true)]
        [TestCase("ScriptingError, ScriptCompileWarning", "ignore me", false)]
        [TestCase("0", "ignore me: warning CS1234", true)]
        [TestCase("0", "ignore me", false)]
        public void WarningsOnly_ConsoleRescanPreservesOtherLogTypes(string mode, string message, bool shouldRemove)
        {
            ApplyWithActiveRule(cleanStackTraces: true);
            var entries = new MemoryConsoleEntries(CreateEntry(mode, message));

            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out var removed, out _), Is.True);
            Assert.That(removed, Is.EqualTo(shouldRemove));
            Assert.That(entries.Rows.Count, Is.EqualTo(shouldRemove ? 0 : 1));
        }

        [Test]
        public void RegexTimeout_PreservesLogAndDisablesRuleUntilSettingsAreSaved()
        {
            var inner = new RecordingLogHandler();
            Debug.unityLogger.logHandler = inner;
            Apply(false, new ConsoleLogFilterRuleData
            {
                matchType = ConsoleLogFilterMatchType.Regex,
                pattern = "(a+)+$"
            });
            Assert.That(ConsoleLogFilterInstaller.TryCreateRegex("(a+)+$", false, out var regex, out _), Is.True);
            Assert.That(regex.MatchTimeout.TotalMilliseconds, Is.InRange(1, 100));

            Assert.DoesNotThrow(() => Debug.unityLogger.logHandler.LogFormat(LogType.Warning, null, new string('a', 10000) + "!"));
            Debug.unityLogger.logHandler.LogFormat(LogType.Warning, null, "aaaa");
            Assert.That(inner.MessageCount, Is.EqualTo(2), "The timed-out rule must stay disabled on later messages.");

            ConsoleLogFilterSettings.Save();
            Debug.unityLogger.logHandler.LogFormat(LogType.Warning, null, "aaaa");
            Assert.That(inner.MessageCount, Is.EqualTo(2), "Saving settings should re-enable a valid regex rule.");
        }

        [TestCase("Read", "Read failed.")]
        [TestCase("Missing", "Could not read Console entry")]
        [TestCase("Clone", "Clone failed.")]
        [TestCase("EndRead", "EndRead failed.")]
        [TestCase("Filtered", "filtered, collapsed, or changed")]
        [TestCase("Changed", "changed during the scan")]
        public void ConsoleRescan_IncompleteSnapshotPreservesRowsAndCanRetry(string failure, string expectedError)
        {
            ApplyWithActiveRule(cleanStackTraces: true);
            var original = new[] { CreateEntry("ScriptingWarning", "ignore me"), CreateEntry("ScriptingLog", "keep me") };
            var added = failure == "Changed" ? CreateEntry("ScriptingLog", "arrived during the scan") : null;
            var expected = added == null ? original : original.Concat(new[] { added }).ToArray();
            var entries = new MemoryConsoleEntries(original)
            {
                Failure = failure,
                VisibleCount = failure == "Filtered" ? 1 : (int?)null,
                EntryAddedAfterRead = added
            };

            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out var removed, out var error), Is.False);
            Assert.That(removed, Is.False);
            Assert.That(error, Does.Contain(expectedError));
            Assert.That(entries.ClearCount, Is.Zero);
            Assert.That(entries.Rows, Is.EqualTo(expected));
            Assert.That(entries.EndReadCount, Is.EqualTo(1));

            entries.Failure = null;
            entries.VisibleCount = null;
            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out removed, out error), Is.True, error);
            Assert.That(removed, Is.True);
            Assert.That(entries.Rows.Count, Is.EqualTo(expected.Length - 1), "A failed pass must release its reentrancy guard.");
        }

        [Test]
        public void ConsoleRescan_RestorationFailureIsReportedAndOtherRowsAreRestored()
        {
            ApplyWithActiveRule(cleanStackTraces: true);
            var entries = new MemoryConsoleEntries(CreateEntry("ScriptingWarning", "ignore me"),
                CreateEntry("ScriptingLog", "first"), CreateEntry("ScriptingLog", "second")) { Failure = "Add" };

            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("Could not restore 1 Console entry"));
            Assert.That(entries.AddCount, Is.EqualTo(2));
            Assert.That(entries.Rows.Count, Is.EqualTo(1));
        }

        [TestCase("MinimalEditor.Console.Editor.ConsoleLogFilterInstaller:LogFormat()\n")]
        [TestCase("at Filter (Packages/com.martincalander.minimaleditor/Editor/Console/ConsoleLogFilterInstaller.cs:1)\n")]
        public void CleanStackTrace_CleansCombinedMessageAndPreservesUnicodeOffsets(string ownFrame)
        {
            Apply(cleanStackTraces: true);
            const string prefix = "Meddelande ö: MinimalEditor.Console.Editor.ConsoleLogFilterInstaller\n";
            const string userFrame = "Game.Player:Update() (at Assets/Player.cs:12)";
            var entry = CreateEntry("ScriptingLog", prefix + ownFrame + userFrame);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = entry.GetType();
            var utf16 = type.GetField("callstackTextStartUTF16", flags);
            var utf8 = type.GetField("callstackTextStartUTF8", flags);
            utf16.SetValue(entry, prefix.Length);
            utf8.SetValue(entry, Encoding.UTF8.GetByteCount(prefix));
            var entries = new MemoryConsoleEntries(entry);

            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out var removed, out var message), Is.True, message);

            Assert.That(removed, Is.False);
            Assert.That(entries.ClearCount, Is.EqualTo(1));
            Assert.That(entries.Rows.Count, Is.EqualTo(1));
            var cleaned = entries.Rows[0];
            Assert.That(type.GetField("message", flags).GetValue(cleaned), Is.EqualTo(prefix + userFrame));
            Assert.That(type.GetField("message", flags).GetValue(entry), Is.EqualTo(prefix + ownFrame + userFrame),
                "Cleaning the retained snapshot must not mutate the original entry.");
            Assert.That(utf16.GetValue(cleaned), Is.EqualTo(prefix.Length));
            Assert.That(utf8.GetValue(cleaned), Is.EqualTo(Encoding.UTF8.GetByteCount(prefix)));
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        public void CleanStackTrace_InvalidOrAbsentOffsetPreservesMessage(int offset, bool fromEnd)
        {
            Apply(cleanStackTraces: true);
            const string text = "MinimalEditor.Console.Editor.ConsoleLogFilterInstaller is in this message";
            var entry = CreateEntry("ScriptingLog", text);
            entry.GetType().GetField("callstackTextStartUTF16").SetValue(entry, fromEnd ? text.Length + offset : offset);
            var entries = new MemoryConsoleEntries(entry);

            Assert.That(ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out _, out var message), Is.True, message);

            Assert.That(entries.ClearCount, Is.Zero);
            Assert.That(entry.GetType().GetField("message").GetValue(entries.Rows[0]), Is.EqualTo(text));
        }

        [Test]
        public void CleanStackTrace_UnchangedSnapshotDoesNotNeedAnotherCopy()
        {
            Apply(cleanStackTraces: true);
            var entry = CreateEntry("ScriptingLog", "keep me");
            entry.GetType().GetField("file").SetValue(entry, string.Empty);
            var entries = new MemoryConsoleEntries(entry);

            var copy = entries.Clone(entry, out var cleaned);

            Assert.That(copy, Is.SameAs(entry));
            Assert.That(cleaned, Is.False);
        }

        private static object CreateEntry(string mode, string message)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var type = assembly.GetType("UnityEditor.LogEntry", true);
            var entry = Activator.CreateInstance(type);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            type.GetField("message", flags).SetValue(entry, message);
            var field = type.GetField("mode", flags);
            var modeValue = Convert.ToInt32(Enum.Parse(assembly.GetType("UnityEditor.ConsoleWindow+Mode", true), mode));
            field.SetValue(entry, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, modeValue) : (object)modeValue);
            return entry;
        }

        private sealed class MemoryConsoleEntries : ConsoleLogFilterInstaller.ConsoleEntryAccess
        {
            internal readonly List<object> Rows;
            internal string Failure;
            internal int ClearCount;
            internal int EndReadCount;
            internal int AddCount;
            internal int? VisibleCount;
            internal object EntryAddedAfterRead;

            internal MemoryConsoleEntries(params object[] rows) => Rows = new List<object>(rows);
            internal override int GetTotalCount() => Rows.Count;
            internal override int BeginRead() => VisibleCount ?? Rows.Count;
            internal override object Read(int index)
            {
                if (index == 1 && Failure == "Read")
                    throw new InvalidOperationException("Read failed.");
                return index == 1 && Failure == "Missing" ? null : Rows[index];
            }

            internal override object Clone(object entry, out bool cleanedStackTrace)
            {
                if (Failure == "Clone")
                    throw new InvalidOperationException("Clone failed.");
                return base.Clone(entry, out cleanedStackTrace);
            }

            internal override void EndRead()
            {
                EndReadCount++;
                if (EntryAddedAfterRead != null)
                {
                    Rows.Add(EntryAddedAfterRead);
                    EntryAddedAfterRead = null;
                }
                if (Failure == "EndRead")
                    throw new InvalidOperationException("EndRead failed.");
            }

            internal override void Clear()
            {
                ClearCount++;
                Rows.Clear();
            }

            internal override void Add(object entry)
            {
                AddCount++;
                if (Failure == "Add" && AddCount == 1)
                    throw new InvalidOperationException("Add failed.");
                Rows.Add(entry);
            }
        }

        private static int SettingsSubscriptionCount(Type owner)
        {
            var settingsChanged = typeof(ConsoleLogFilterSettings).GetField(
                "SettingsChanged", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(settingsChanged, Is.Not.Null);
            var callback = settingsChanged.GetValue(null) as Delegate;
            return callback?.GetInvocationList().Count(handler => handler.Method.DeclaringType == owner) ?? 0;
        }

        private static void ApplyWithActiveRule(bool cleanStackTraces = false)
        {
            Apply(cleanStackTraces, new ConsoleLogFilterRuleData
            {
                enabled = true,
                matchType = ConsoleLogFilterMatchType.Contains,
                pattern = "ignore me"
            });
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            private readonly ILogHandler inner;
            internal int MessageCount { get; private set; }

            internal RecordingLogHandler(ILogHandler inner = null) => this.inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                MessageCount++;
                inner?.LogException(exception, context);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                MessageCount++;
                inner?.LogFormat(logType, context, format, args);
            }
        }

        private sealed class CountingFormatArgument
        {
            private readonly string message;
            internal int FormatCount;
            internal CountingFormatArgument(string message = "keep me") => this.message = message;
            public override string ToString()
            {
                FormatCount++;
                return message;
            }
        }

        private sealed class CountingException : Exception
        {
            internal int FormatCount;
            public override string ToString()
            {
                FormatCount++;
                return "keep me";
            }
        }

        private static void Apply(bool cleanStackTraces, params ConsoleLogFilterRuleData[] rules)
        {
            var settings = ConsoleLogFilterSettings.Data;
            settings.enabled = true;
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
