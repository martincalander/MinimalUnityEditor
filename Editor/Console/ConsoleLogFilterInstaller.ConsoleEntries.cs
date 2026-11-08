using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MinimalEditor.Console.Editor
{
    internal static partial class ConsoleLogFilterInstaller
    {
        internal abstract class ConsoleEntryAccess
        {
            private int? m_scannedVersion;
            private int m_scannedCount;

            // A count alone cannot detect a clear followed by equally many new rows.
            internal virtual int? GetChangeVersion() => null;
            internal bool IsUnchanged(int? version, int count) =>
                version.HasValue && m_scannedVersion == version && m_scannedCount == count;
            internal void RememberSnapshot(int? version, int count)
            {
                m_scannedVersion = version;
                m_scannedCount = count;
            }
            internal void Invalidate() => m_scannedVersion = null;

            internal abstract int GetTotalCount();
            internal abstract int BeginRead();
            // Each read returns an independent snapshot that survives EndRead.
            internal abstract object Read(int index);
            internal abstract void EndRead();
            internal virtual object Clone(object entry, out bool cleanedStackTrace) => CloneLogEntry(entry, out cleanedStackTrace);
            internal abstract void Clear();
            internal abstract void Add(object entry);
        }

        private sealed class ReflectedConsoleEntryAccess : ConsoleEntryAccess
        {
            private readonly object[] m_countArguments = { 0, 0, 0 };
            private readonly object[] m_readArguments = new object[2];
            private readonly object[] m_addArguments = new object[1];

            internal override int? GetChangeVersion() => Volatile.Read(ref s_consoleChangeVersion);

            internal override int GetTotalCount()
            {
                s_logEntriesGetCountsByTypeMethod.Invoke(null, m_countArguments);
                return checked((int)m_countArguments[0] + (int)m_countArguments[1] + (int)m_countArguments[2]);
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

            internal override void EndRead()
            {
                m_readArguments[1] = null;
                s_logEntriesEndGettingEntriesMethod.Invoke(null, null);
            }
            internal override void Clear() => s_logEntriesClearMethod.Invoke(null, null);

            internal override void Add(object entry)
            {
                m_addArguments[0] = entry;
                try
                {
                    s_logEntriesAddMessageMethod.Invoke(null, m_addArguments);
                }
                finally
                {
                    m_addArguments[0] = null;
                }
            }
        }

        private static readonly ReflectedConsoleEntryAccess s_consoleEntryAccess = new();

        // Initialize after the containing type has resolved Unity's fields. Partial
        // class field initializers do not guarantee ordering between source files.
#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        private static class LogEntryAccessors
        {
            internal static readonly Func<object, int> Mode = CreateIntReader(s_logEntryModeField);
            internal static readonly Func<object, int> StackStart = CreateIntReader(s_logEntryCallstackStartField);
            internal static readonly Func<object, object> Copy = CreateCopy();
            internal static readonly StringField[] Strings = CreateStringFields();

            internal sealed class StringField
            {
                internal FieldInfo Field;
                internal bool Normalize;
                internal bool Message;
                internal bool Stack;
            }

            private static StringField[] CreateStringFields()
            {
                var fields = new List<StringField>();
                foreach (var field in s_logEntryFields)
                {
                    if (field.FieldType != typeof(string))
                        continue;
                    fields.Add(new StringField
                    {
                        Field = field,
                        Normalize = field.Equals(s_logEntryMessageField) || field.Equals(s_logEntryConditionField) || field.Equals(s_logEntryFileField),
                        Message = field.Equals(s_logEntryMessageField),
                        Stack = IsStackTraceField(field.Name)
                    });
                }
                return fields.ToArray();
            }

            private static Func<object, int> CreateIntReader(FieldInfo field)
            {
                if (field == null)
                    return _ => 0;
                try
                {
                    if (field.FieldType == typeof(int))
                    {
                        var entry = Expression.Parameter(typeof(object));
                        return Expression.Lambda<Func<object, int>>(
                            Expression.Field(Expression.Convert(entry, field.DeclaringType), field), entry).Compile();
                    }
                }
                catch
                {
                    // Older runtimes can restrict compiled access to internal fields.
                }
                return entry => Convert.ToInt32(field.GetValue(entry), CultureInfo.InvariantCulture);
            }

            private static Func<object, object> CreateCopy()
            {
                try
                {
                    return (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>),
                        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic));
                }
                catch
                {
                    return CopyFields;
                }
            }

            private static object CopyFields(object entry)
            {
                var clone = Activator.CreateInstance(s_logEntryType);
                foreach (var field in s_logEntryFields)
                    field.SetValue(clone, field.GetValue(entry));
                return clone;
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

            return TryApplyFiltersToConsoleEntries(s_consoleEntryAccess, force, out removedAny, out message);
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
            List<object> keptEntries = null;
            var removedCount = 0;
            var cleanedStackTraceCount = 0;
            var started = false;
            var snapshotComplete = false;
            var snapshotCount = 0;
            int? snapshotVersion = null;

            try
            {
                snapshotVersion = entries.GetChangeVersion();
                var totalCount = entries.GetTotalCount();
                if (!force && entries.IsUnchanged(snapshotVersion, totalCount))
                    return true;
                entries.Invalidate();
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
                    keptEntries ??= new List<object>(entryCount - removedCount);
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
                entries.RememberSnapshot(snapshotVersion, snapshotCount);
                if (force)
                    message = "No currently shown logs matched active rules.";
                return true;
            }

            try
            {
                // Background logging can add rows while the snapshot is processed.
                // Keep the original Console if its contents grew after the read session.
                if (entries.GetTotalCount() != snapshotCount || entries.GetChangeVersion() != snapshotVersion)
                {
                    message = "Console entries changed during the scan. Current logs were preserved.";
                    return false;
                }

                entries.Clear();
                removedAny = removedCount > 0;
                var failedCount = 0;
                string firstFailure = null;
                for (var i = 0; keptEntries != null && i < keptEntries.Count; i++)
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
            object clone = null;
            var fields = LogEntryAccessors.Strings;
            for (var i = 0; i < fields.Length; i++)
            {
                var descriptor = fields[i];
                var field = descriptor.Field;

                var original = field.GetValue(entry) as string;
                var value = original;
                if (descriptor.Normalize)
                    value ??= string.Empty;

                if (s_cleanStackTracesEnabled && !string.IsNullOrEmpty(value))
                {
                    string cleaned = value;
                    if (descriptor.Message &&
                        s_logEntryCallstackStartField?.FieldType == typeof(int))
                    {
                        var stackStart = LogEntryAccessors.StackStart(entry);
                        if (stackStart > 0 && stackStart < value.Length)
                            cleaned = CleanStackTraceAfterOffset(value, stackStart);
                    }
                    else if (descriptor.Stack)
                    {
                        cleaned = CleanStackTrace(value);
                    }

                    cleanedStackTrace |= !string.Equals(cleaned, value, StringComparison.Ordinal);
                    value = cleaned;
                }

                if (string.Equals(original, value, StringComparison.Ordinal))
                    continue;

                // Read already owns a complete snapshot. Copy it only when a field
                // needs normalization or cleanup, never for every unchanged row.
                if (clone == null)
                    clone = LogEntryAccessors.Copy(entry);
                field.SetValue(clone, value);
            }

            return clone ?? entry;
        }

        private static bool IsStackTraceField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return false;

            return fieldName.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CleanStackTrace(string stackTrace)
        {
            return string.IsNullOrEmpty(stackTrace) ? stackTrace : CleanStackTraceAfterOffset(stackTrace, 0);
        }

        private static string CleanStackTraceAfterOffset(string stackTrace, int stackStart)
        {
            if (!ShouldStripStackTraceLine(stackTrace, stackStart, stackTrace.Length - stackStart))
                return stackTrace;

            var builder = new StringBuilder(stackTrace.Length);
            builder.Append(stackTrace, 0, stackStart);
            var keptLineCount = 0;
            var start = stackStart;
            while (start <= stackTrace.Length)
            {
                var end = stackTrace.IndexOf('\n', start);
                var lastLine = end < 0;
                if (lastLine)
                    end = stackTrace.Length;
                var length = end - start;
                if (!lastLine && length > 0 && stackTrace[end - 1] == '\r')
                    length--;
                if (!ShouldStripStackTraceLine(stackTrace, start, length))
                {
                    if (keptLineCount > 0)
                        builder.Append('\n');
                    builder.Append(stackTrace, start, length);
                    keptLineCount++;
                }
                if (lastLine)
                    break;
                start = end + 1;
            }

            // Trim only the cleaned stack. The message prefix and its UTF offsets
            // must remain byte-for-byte equivalent to the original snapshot.
            var cleanEnd = builder.Length;
            while (cleanEnd > stackStart && builder[cleanEnd - 1] == '\n')
                cleanEnd--;
            builder.Length = cleanEnd;
            var cleanStart = stackStart;
            while (cleanStart < cleanEnd && builder[cleanStart] == '\n')
                cleanStart++;
            if (cleanStart > stackStart)
                builder.Remove(stackStart, cleanStart - stackStart);
            return builder.ToString();
        }

        private static bool ShouldStripStackTraceLine(string text, int start, int count) =>
            text.IndexOf("MinimalEditor.Console.Editor.ConsoleLogFilterInstaller", start, count, StringComparison.Ordinal) >= 0 ||
            text.IndexOf("Packages/com.martincalander.minimaleditor/Editor/Console/ConsoleLogFilterInstaller", start, count, StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("UnityEngine.DebugLogHandler:LogFormat", start, count, StringComparison.Ordinal) >= 0;

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

            return LogEntryAccessors.Mode(entry);
        }

    }
}
