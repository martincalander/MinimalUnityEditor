using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Essentials.Console.Editor
{
    internal static partial class ConsoleLogFilterInstaller
    {
        internal abstract class ConsoleEntryAccess
        {
            internal abstract int GetTotalCount();
            internal abstract int BeginRead();
            internal abstract object Read(int index);
            internal abstract void EndRead();
            internal virtual object Clone(object entry, out bool cleanedStackTrace) => CloneLogEntry(entry, out cleanedStackTrace);
            internal abstract void Clear();
            internal abstract void Add(object entry);
        }

        private sealed class ReflectedConsoleEntryAccess : ConsoleEntryAccess
        {
            private readonly object[] m_readArguments = new object[2];
            private readonly object[] m_addArguments = new object[1];

            internal override int GetTotalCount()
            {
                var counts = new object[] { 0, 0, 0 };
                s_logEntriesGetCountsByTypeMethod.Invoke(null, counts);
                return checked((int)counts[0] + (int)counts[1] + (int)counts[2]);
            }

            internal override int BeginRead() => Convert.ToInt32(
                s_logEntriesStartGettingEntriesMethod.Invoke(null, null), CultureInfo.InvariantCulture);

            internal override object Read(int index)
            {
                m_readArguments[0] = index;
                m_readArguments[1] = Activator.CreateInstance(s_logEntryType);
                return Convert.ToBoolean(s_logEntriesGetEntryInternalMethod.Invoke(null, m_readArguments), CultureInfo.InvariantCulture)
                    ? m_readArguments[1]
                    : null;
            }

            internal override void EndRead() => s_logEntriesEndGettingEntriesMethod.Invoke(null, null);
            internal override void Clear() => s_logEntriesClearMethod.Invoke(null, null);

            internal override void Add(object entry)
            {
                m_addArguments[0] = entry;
                s_logEntriesAddMessageMethod.Invoke(null, m_addArguments);
            }
        }

        private static bool TryApplyFiltersToCurrentConsoleEntries(bool force, out bool removedAny, out string message)
        {
            if (s_logEntriesGetCountsByTypeMethod == null ||
                s_logEntriesStartGettingEntriesMethod == null ||
                s_logEntriesEndGettingEntriesMethod == null ||
                s_logEntriesGetEntryInternalMethod == null ||
                s_logEntriesClearMethod == null ||
                s_logEntriesAddMessageMethod == null ||
                s_logEntryType == null ||
                s_logEntryModeField == null)
            {
                removedAny = false;
                message = "Unity internal Console API changed. Could not update existing logs.";
                return false;
            }

            return TryApplyFiltersToConsoleEntries(new ReflectedConsoleEntryAccess(), force, out removedAny, out message);
        }

        internal static bool TryApplyFiltersToConsoleEntries(ConsoleEntryAccess entries, bool force, out bool removedAny, out string message)
        {
            removedAny = false;
            message = string.Empty;
            if (s_isUpdatingConsoleEntries)
            {
                message = "Console filter update already in progress.";
                return false;
            }

            var canSuppressByRule = s_activeFilteringLogHandler.FilteringEnabled;
            if (!canSuppressByRule && !s_cleanStackTracesEnabled)
            {
                message = "Enable hidden log filters and add at least one active rule first.";
                return false;
            }

            s_isUpdatingConsoleEntries = true;
            try
            {
                return ApplyFiltersToConsoleEntries(entries, force, canSuppressByRule, out removedAny, out message);
            }
            finally
            {
                s_isUpdatingConsoleEntries = false;
            }
        }

        private static bool ApplyFiltersToConsoleEntries(ConsoleEntryAccess entries, bool force, bool canSuppressByRule,
            out bool removedAny, out string message)
        {
            removedAny = false;
            message = string.Empty;
            var keptEntries = new List<object>();
            var removedCount = 0;
            var cleanedStackTraceCount = 0;
            var started = false;
            var snapshotComplete = false;
            var snapshotCount = 0;

            try
            {
                var totalCount = entries.GetTotalCount();
                var entryCount = entries.BeginRead();
                started = true;
                if (entryCount != totalCount)
                    throw new InvalidOperationException("Console rows are filtered, collapsed, or changed during the scan. Current logs were preserved.");

                for (var i = 0; i < entryCount; i++)
                {
                    var entry = entries.Read(i);
                    if (entry == null)
                        throw new InvalidOperationException($"Could not read Console entry {i}. Current logs were preserved.");

                    var entryMessage = ReadLogEntryMessage(entry);
                    var entryMode = ReadLogEntryMode(entry);
                    if (canSuppressByRule && s_activeFilteringLogHandler.ShouldSuppressMode(entryMode, entryMessage))
                    {
                        removedCount++;
                        continue;
                    }

                    var clonedEntry = entries.Clone(entry, out var cleanedStackTrace);
                    if (clonedEntry == null)
                        throw new InvalidOperationException($"Could not copy Console entry {i}. Current logs were preserved.");
                    keptEntries.Add(clonedEntry);
                    if (cleanedStackTrace)
                        cleanedStackTraceCount++;
                }

                snapshotCount = entryCount;
                snapshotComplete = true;
            }
            catch (Exception ex)
            {
                message = ex.InnerException?.Message ?? ex.Message;
            }
            finally
            {
                if (started)
                {
                    try
                    {
                        entries.EndRead();
                    }
                    catch (Exception ex)
                    {
                        snapshotComplete = false;
                        message = ex.InnerException?.Message ?? ex.Message;
                    }
                }
            }

            // Clearing is safe only when every retained row was copied successfully
            // and Unity released the read session.
            if (!snapshotComplete)
                return false;

            if (removedCount <= 0 && cleanedStackTraceCount <= 0)
            {
                if (force)
                    message = "No currently shown logs matched active rules.";
                return true;
            }

            try
            {
                // Background logging can add rows while the snapshot is processed.
                // Keep the original Console if its contents grew after the read session.
                if (entries.GetTotalCount() != snapshotCount)
                {
                    message = "Console entries changed during the scan. Current logs were preserved.";
                    return false;
                }

                entries.Clear();
                removedAny = removedCount > 0;
                var failedCount = 0;
                string firstFailure = null;
                for (var i = 0; i < keptEntries.Count; i++)
                {
                    try
                    {
                        entries.Add(keptEntries[i]);
                    }
                    catch (Exception ex)
                    {
                        // Continue restoring the other rows, but report partial failure.
                        failedCount++;
                        firstFailure ??= ex.InnerException?.Message ?? ex.Message;
                    }
                }

                if (failedCount > 0)
                {
                    message = $"Could not restore {failedCount} Console entr{(failedCount == 1 ? "y" : "ies")}: {firstFailure}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = ex.InnerException?.Message ?? ex.Message;
                return false;
            }

            if (removedCount > 0 && cleanedStackTraceCount > 0)
            {
                message = $"Removed {removedCount} matching console entr{(removedCount == 1 ? "y" : "ies")} and cleaned stack traces on {cleanedStackTraceCount} entr{(cleanedStackTraceCount == 1 ? "y" : "ies")}.";
            }
            else if (removedCount > 0)
            {
                message = $"Removed {removedCount} matching console entr{(removedCount == 1 ? "y" : "ies")}.";
            }
            else
            {
                message = $"Cleaned stack traces on {cleanedStackTraceCount} console entr{(cleanedStackTraceCount == 1 ? "y" : "ies")}.";
            }

            return true;
        }

        private static object CloneLogEntry(object entry, out bool cleanedStackTrace)
        {
            cleanedStackTrace = false;
            var clone = Activator.CreateInstance(s_logEntryType);
            for (var i = 0; i < s_logEntryFields.Length; i++)
            {
                var field = s_logEntryFields[i];
                var value = field.GetValue(entry);
                if (ReferenceEquals(field, s_logEntryMessageField) ||
                    ReferenceEquals(field, s_logEntryConditionField) ||
                    ReferenceEquals(field, s_logEntryFileField))
                    value ??= string.Empty;

                if (s_cleanStackTracesEnabled && value is string stringValue && IsStackTraceField(field.Name))
                {
                    var cleaned = CleanStackTrace(stringValue);
                    if (!string.Equals(cleaned, stringValue, StringComparison.Ordinal))
                    {
                        value = cleaned;
                        cleanedStackTrace = true;
                    }
                }

                field.SetValue(clone, value);
            }

            return clone;
        }

        private static bool IsStackTraceField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return false;

            return fieldName.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CleanStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return stackTrace;

            var lines = stackTrace.Replace("\r\n", "\n").Split('\n');
            var builder = new StringBuilder(stackTrace.Length);
            var keptLineCount = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (ShouldStripStackTraceLine(line))
                    continue;

                if (keptLineCount > 0)
                    builder.Append('\n');

                builder.Append(line);
                keptLineCount++;
            }

            if (keptLineCount == lines.Length)
                return stackTrace;

            return builder.ToString().Trim('\n');
        }

        private static bool ShouldStripStackTraceLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            return line.IndexOf("Essentials.Console.Editor.ConsoleLogFilterInstaller", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("Packages/com.martincalander.minimaleditor/Editor/Console/ConsoleLogFilterInstaller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Packages/com.essentials.minimaleditor/Editor/Console/ConsoleLogFilterInstaller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Packages/com.essentials.extensions/Editor/Console/ConsoleLogFilterInstaller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("UnityEngine.DebugLogHandler:LogFormat", StringComparison.Ordinal) >= 0;
        }

        private static string ReadLogEntryMessage(object entry)
        {
            if (entry == null)
                return string.Empty;

            var message = ReadLogEntryStringField(entry, s_logEntryMessageField);
            var condition = ReadLogEntryStringField(entry, s_logEntryConditionField);
            var file = ReadLogEntryStringField(entry, s_logEntryFileField);

            if (string.IsNullOrEmpty(message))
                message = condition;
            else if (!string.IsNullOrEmpty(condition) && message.IndexOf(condition, StringComparison.Ordinal) < 0)
                message = message + "\n" + condition;

            if (!string.IsNullOrEmpty(file) && message.IndexOf(file, StringComparison.OrdinalIgnoreCase) < 0)
                message = string.IsNullOrEmpty(message) ? file : message + "\n" + file;

            return message ?? string.Empty;
        }

        private static string ReadLogEntryStringField(object entry, System.Reflection.FieldInfo field)
        {
            if (entry == null || field == null)
                return string.Empty;

            return field.GetValue(entry) as string ?? string.Empty;
        }

        private static int ReadLogEntryMode(object entry)
        {
            if (entry == null || s_logEntryModeField == null)
                return 0;

            return Convert.ToInt32(s_logEntryModeField.GetValue(entry), CultureInfo.InvariantCulture);
        }

    }
}
