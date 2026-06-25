using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MartinCalander.Essentials.Console.Editor
{
    internal static partial class ConsoleLogFilterInstaller
    {
        private static bool TryApplyFiltersToCurrentConsoleEntries(bool force, out bool removedAny, out string message)
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

            if (s_logEntriesStartGettingEntriesMethod == null ||
                s_logEntriesEndGettingEntriesMethod == null ||
                s_logEntriesGetEntryInternalMethod == null ||
                s_logEntriesClearMethod == null ||
                s_logEntriesAddMessageMethod == null ||
                s_logEntryType == null)
            {
                message = "Unity internal Console API changed. Could not update existing logs.";
                return false;
            }

            var keptEntries = new List<object>();
            var removedCount = 0;
            var cleanedStackTraceCount = 0;
            var started = false;

            try
            {
                var entryCount = Convert.ToInt32(s_logEntriesStartGettingEntriesMethod.Invoke(null, null), CultureInfo.InvariantCulture);
                started = true;

                var getEntryArgs = new object[2];
                for (var i = 0; i < entryCount; i++)
                {
                    try
                    {
                        getEntryArgs[0] = i;
                        getEntryArgs[1] = Activator.CreateInstance(s_logEntryType);

                        if (!Convert.ToBoolean(s_logEntriesGetEntryInternalMethod.Invoke(null, getEntryArgs), CultureInfo.InvariantCulture))
                            continue;

                        var entry = getEntryArgs[1];
                        if (entry == null)
                            continue;

                        var entryMessage = ReadLogEntryMessage(entry);
                        var entryMode = ReadLogEntryMode(entry);

                        if (canSuppressByRule && s_activeFilteringLogHandler.ShouldSuppressMode(entryMode, entryMessage))
                        {
                            removedCount++;
                            continue;
                        }

                        var clonedEntry = CloneLogEntry(entry, out var cleanedStackTrace);
                        if (clonedEntry != null)
                        {
                            keptEntries.Add(clonedEntry);
                            if (cleanedStackTrace)
                                cleanedStackTraceCount++;
                        }
                    }
                    catch
                    {
                        // Skip malformed rows instead of aborting the whole filter pass.
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
            finally
            {
                if (started)
                {
                    try
                    {
                        s_logEntriesEndGettingEntriesMethod.Invoke(null, null);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (removedCount <= 0 && cleanedStackTraceCount <= 0)
            {
                if (force)
                    message = "No currently shown logs matched active rules.";

                return true;
            }

            try
            {
                s_isUpdatingConsoleEntries = true;
                s_logEntriesClearMethod.Invoke(null, null);

                var addMessageArgs = new object[1];
                for (var i = 0; i < keptEntries.Count; i++)
                {
                    try
                    {
                        addMessageArgs[0] = keptEntries[i];
                        s_logEntriesAddMessageMethod.Invoke(null, addMessageArgs);
                    }
                    catch
                    {
                        // Drop only the failing row; keep processing the rest.
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
            finally
            {
                s_isUpdatingConsoleEntries = false;
            }

            removedAny = removedCount > 0;
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
            if (entry == null || s_logEntryType == null)
                return null;

            object clone;
            try
            {
                clone = Activator.CreateInstance(s_logEntryType);
            }
            catch
            {
                return null;
            }

            if (clone == null)
                return null;

            for (var i = 0; i < s_logEntryFields.Length; i++)
            {
                var field = s_logEntryFields[i];
                try
                {
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
                catch
                {
                    // ignored
                }
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

            return line.IndexOf("MartinCalander.Essentials.Console.Editor.ConsoleLogFilterInstaller", StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("Packages/com.essentials.extensions/Console/Editor/ConsoleLogFilterInstaller", StringComparison.OrdinalIgnoreCase) >= 0 ||
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

            try
            {
                return Convert.ToInt32(s_logEntryModeField.GetValue(entry), CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryGetConsoleFingerprint(out (int rows, int errors, int warnings, int logs) fingerprint)
        {
            fingerprint = default;
            if (s_logEntriesGetCountMethod == null)
                return false;

            try
            {
                var rows = Convert.ToInt32(s_logEntriesGetCountMethod.Invoke(null, null), CultureInfo.InvariantCulture);
                var errors = 0;
                var warnings = 0;
                var logs = 0;

                if (s_logEntriesGetCountsByTypeMethod != null)
                {
                    var countArgs = new object[] { 0, 0, 0 };
                    s_logEntriesGetCountsByTypeMethod.Invoke(null, countArgs);
                    errors = Convert.ToInt32(countArgs[0], CultureInfo.InvariantCulture);
                    warnings = Convert.ToInt32(countArgs[1], CultureInfo.InvariantCulture);
                    logs = Convert.ToInt32(countArgs[2], CultureInfo.InvariantCulture);
                }

                fingerprint = (rows, errors, warnings, logs);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
