using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Essentials.Tests.Editor.Console
{
    /// <summary>
    /// Verifies that Unity's internal Console API — accessed via reflection in
    /// ConsoleLogFilterInstaller — still matches what the production code expects.
    ///
    /// A failure here means a Unity update has renamed or removed an internal type,
    /// method, or field that the log-filter or clean-stack-trace feature depends on.
    /// </summary>
    public sealed class ConsoleReflectionBridgeTests
    {
        static readonly Assembly UnityEditorAssembly = typeof(EditorWindow).Assembly;

        static readonly Type LogEntryType         = UnityEditorAssembly.GetType("UnityEditor.LogEntry");
        static readonly Type LogEntriesType       = UnityEditorAssembly.GetType("UnityEditor.LogEntries");
        static readonly Type ConsoleWindowModeType = UnityEditorAssembly.GetType("UnityEditor.ConsoleWindow+Mode");

        const BindingFlags AnyStatic   = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void RedundantConsolePreferencesMenuItemPath_IsNotRegistered()
        {
            Type providerType = Type.GetType(
                "Essentials.Console.Editor.ConsoleLogFilterSettingsProvider, Essentials.Console.Editor");
            Assert.That(providerType, Is.Not.Null);

            foreach (MethodInfo method in providerType.GetMethods(AnyStatic))
            {
                foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(MenuItem) || attribute.ConstructorArguments.Count == 0)
                        continue;

                    Assert.That(
                        attribute.ConstructorArguments[0].Value,
                        Is.Not.EqualTo("Window/Analysis/Console Preferences"));
                }
            }
        }

        // ── Types ────────────────────────────────────────────────────────────

        [Test]
        public void LogEntry_TypeExists_InUnityEditorAssembly()
        {
            Assert.That(LogEntryType, Is.Not.Null,
                "UnityEditor.LogEntry could not be found. The console log-filter and " +
                "clean-stack-trace features will not work.");
        }

        [Test]
        public void LogEntries_TypeExists_InUnityEditorAssembly()
        {
            Assert.That(LogEntriesType, Is.Not.Null,
                "UnityEditor.LogEntries could not be found. The console log-filter feature will not work.");
        }

        [Test]
        public void ConsoleWindowMode_TypeExists_InUnityEditorAssembly()
        {
            Assert.That(ConsoleWindowModeType, Is.Not.Null,
                "UnityEditor.ConsoleWindow+Mode could not be found. " +
                "Log-type mode masking (warning/error/log classification) will produce empty masks.");
        }

        // ── LogEntries methods ───────────────────────────────────────────────
        // Production code: ConsoleLogFilterInstaller.GetLogEntriesMethod(name)

        [Test]
        public void LogEntries_Clear_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("Clear", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.Clear() was not found. The console cannot be cleared during a re-filter pass.");
        }

        [Test]
        public void LogEntries_GetCount_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("GetCount", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.GetCount() was not found. Console entry counting will not work.");
        }

        [Test]
        public void LogEntries_GetCountsByType_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("GetCountsByType", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.GetCountsByType() was not found. Console fingerprinting will not work.");
        }

        [Test]
        public void LogEntries_StartGettingEntries_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("StartGettingEntries", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.StartGettingEntries() was not found. Console entry iteration will not work.");
        }

        [Test]
        public void LogEntries_EndGettingEntries_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("EndGettingEntries", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.EndGettingEntries() was not found. Console entry iteration will not work.");
        }

        [Test]
        public void LogEntries_GetEntryInternal_StaticMethodExists()
        {
            Assume.That(LogEntriesType, Is.Not.Null);
            var method = LogEntriesType.GetMethod("GetEntryInternal", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "LogEntries.GetEntryInternal() was not found. Reading individual console entries will not work.");
        }

        [Test]
        public void LogEntries_AddMessage_AtLeastOneVariantExists_WithSingleParameter()
        {
            // Production code tries these names in priority order:
            //   "AddMessageWithDoubleClickCallback" → "AddMessage" → "AddMessageWithDoubleClickCallbackInternal"
            // Any one of them is sufficient; the feature degrades gracefully if none are found,
            // but finding none means modified entries cannot be written back to the console.
            Assume.That(LogEntriesType, Is.Not.Null);

            var candidateNames = new[]
            {
                "AddMessageWithDoubleClickCallback",
                "AddMessage",
                "AddMessageWithDoubleClickCallbackInternal",
            };

            var allMethods = LogEntriesType.GetMethods(AnyStatic);
            var found = allMethods.Any(m => candidateNames.Contains(m.Name) && m.GetParameters().Length == 1);

            Assert.That(found, Is.True,
                "None of the expected single-parameter AddMessage variants " +
                "(AddMessageWithDoubleClickCallback, AddMessage, AddMessageWithDoubleClickCallbackInternal) " +
                "were found on LogEntries. Re-injecting filtered console entries will not work.");
        }

        // ── LogEntry fields ──────────────────────────────────────────────────
        // Production code: ConsoleLogFilterInstaller s_logEntry*Field static fields

        [Test]
        public void LogEntry_message_FieldExists_AsString()
        {
            Assume.That(LogEntryType, Is.Not.Null);
            var field = LogEntryType.GetField("message", AnyInstance);
            Assert.That(field, Is.Not.Null,
                "LogEntry.message field was not found. Log message text cannot be read.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)),
                "LogEntry.message is no longer a string — message text extraction will be broken.");
        }

        // NOTE: LogEntry.condition existed in Unity 2022 and earlier. In Unity 6+ it was removed:
        // the message and stack trace are combined in the single 'message' field, and
        // 'callstackTextStartUTF8'/'callstackTextStartUTF16' integer fields mark where the
        // stack trace begins within that string. The two tests below cover the new layout.

        [Test]
        public void LogEntry_callstackTextStartUTF8_FieldExists_AsInt()
        {
            Assume.That(LogEntryType, Is.Not.Null);
            var field = LogEntryType.GetField("callstackTextStartUTF8", AnyInstance);
            Assert.That(field, Is.Not.Null,
                "LogEntry.callstackTextStartUTF8 field was not found. " +
                "This is the current-Unity byte offset that marks where the stack trace begins " +
                "inside the 'message' field — if it disappears, the stack-trace layout has changed again.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(int)),
                "LogEntry.callstackTextStartUTF8 is no longer an int.");
        }

        [Test]
        public void LogEntry_callstackTextStartUTF16_FieldExists_AsInt()
        {
            Assume.That(LogEntryType, Is.Not.Null);
            var field = LogEntryType.GetField("callstackTextStartUTF16", AnyInstance);
            Assert.That(field, Is.Not.Null,
                "LogEntry.callstackTextStartUTF16 field was not found. " +
                "This is the current-Unity byte offset that marks where the stack trace begins " +
                "inside the 'message' field — if it disappears, the stack-trace layout has changed again.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(int)),
                "LogEntry.callstackTextStartUTF16 is no longer an int.");
        }

        [Test]
        public void LogEntry_mode_FieldExists_AsIntOrIntEnum()
        {
            // Production code converts this value to int via bitwise masking (IsWarningMode, IsErrorMode, …)
            Assume.That(LogEntryType, Is.Not.Null);
            var field = LogEntryType.GetField("mode", AnyInstance);
            Assert.That(field, Is.Not.Null,
                "LogEntry.mode field was not found. Log-type classification (warning/error/log) will not work.");

            var isIntType  = field.FieldType == typeof(int);
            var isIntEnum  = field.FieldType.IsEnum && Enum.GetUnderlyingType(field.FieldType) == typeof(int);
            Assert.That(isIntType || isIntEnum, Is.True,
                $"LogEntry.mode has unexpected type '{field.FieldType.FullName}'. " +
                "Expected int or an int-backed enum — bitwise mode masking will be broken.");
        }

        [Test]
        public void LogEntry_file_FieldExists_AsString()
        {
            Assume.That(LogEntryType, Is.Not.Null);
            var field = LogEntryType.GetField("file", AnyInstance);
            Assert.That(field, Is.Not.Null,
                "LogEntry.file field was not found. Source file information cannot be read from console entries.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)),
                "LogEntry.file is no longer a string — source file path extraction will be broken.");
        }

        [Test]
        public void LogEntry_CanBeInstantiated_ViaActivator()
        {
            // Production code: Activator.CreateInstance(s_logEntryType) to create a scratch entry
            // for re-injection into the console.
            Assume.That(LogEntryType, Is.Not.Null);
            Assert.DoesNotThrow(
                () => Activator.CreateInstance(LogEntryType),
                "LogEntry cannot be instantiated via Activator.CreateInstance. " +
                "Console re-injection of filtered entries will not work.");
        }
    }
}
