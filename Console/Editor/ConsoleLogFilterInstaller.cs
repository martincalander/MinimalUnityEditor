using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Essentials.Console.Editor
{
    [InitializeOnLoad]
    internal static partial class ConsoleLogFilterInstaller
    {
        private const double SyncIntervalSeconds = 0.5d;
        private const double ConsoleRescanIntervalSeconds = 0.25d;
        private const double PeriodicRescanIntervalSeconds = 2d;
        private const double StartupForceRefilterSeconds = 30d;

        private static readonly FilterLogHandler s_fullStackFilteringLogHandler = new();
        private static readonly DeclutteredFilterLogHandler s_cleanStackFilteringLogHandler = new();
        private static readonly Type s_logEntryType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry");
        private static readonly Type s_logEntriesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
        private static readonly Type s_consoleWindowModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow+Mode");
        private static readonly MethodInfo s_logEntriesClearMethod = GetLogEntriesMethod("Clear");
        private static readonly MethodInfo s_logEntriesGetCountMethod = GetLogEntriesMethod("GetCount");
        private static readonly MethodInfo s_logEntriesGetCountsByTypeMethod = GetLogEntriesMethod("GetCountsByType");
        private static readonly MethodInfo s_logEntriesStartGettingEntriesMethod = GetLogEntriesMethod("StartGettingEntries");
        private static readonly MethodInfo s_logEntriesEndGettingEntriesMethod = GetLogEntriesMethod("EndGettingEntries");
        private static readonly MethodInfo s_logEntriesGetEntryInternalMethod = GetLogEntriesMethod("GetEntryInternal");
        private static readonly MethodInfo s_logEntriesAddMessageMethod = GetLogEntriesAddMessageMethod();
        private static readonly FieldInfo[] s_logEntryFields = s_logEntryType?.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? Array.Empty<FieldInfo>();
        private static readonly FieldInfo s_logEntryMessageField = s_logEntryType?.GetField("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_logEntryConditionField = s_logEntryType?.GetField("condition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_logEntryModeField = s_logEntryType?.GetField("mode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_logEntryFileField = s_logEntryType?.GetField("file", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly int s_warningModeMask = BuildModeMask(modeName =>
            modeName.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0);
        private static readonly int s_errorModeMask = BuildModeMask(modeName =>
            modeName.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
            modeName.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
            modeName.IndexOf("Assert", StringComparison.OrdinalIgnoreCase) >= 0 ||
            modeName.IndexOf("Fatal", StringComparison.OrdinalIgnoreCase) >= 0);
        private static readonly int s_logModeMask = BuildModeMask(modeName =>
            modeName.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0);

        private static double s_nextSyncTime;
        private static double s_nextConsoleRescanTime;
        private static double s_nextPeriodicRescanTime;
        private static bool s_isUpdatingConsoleEntries;
        private static bool s_hasConsoleFingerprint;
        private static (int rows, int errors, int warnings, int logs) s_consoleFingerprint;
        private static IFilteringLogHandler s_activeFilteringLogHandler = s_cleanStackFilteringLogHandler;
        private static bool s_cleanStackTracesEnabled = true;
        private static readonly double s_forceRefilterUntilTime = EditorApplication.timeSinceStartup + StartupForceRefilterSeconds;

        internal static bool AutomaticConsoleEntryMaintenanceEnabled =>
            s_activeFilteringLogHandler != null && s_activeFilteringLogHandler.FilteringEnabled;

        static ConsoleLogFilterInstaller()
        {
            ConsoleLogFilterSettings.SettingsChanged += ApplySettings;
            EditorApplication.update += OnEditorUpdate;
            ApplySettings();
        }

        internal static bool TryCreateRegex(string pattern, bool caseSensitive, out Regex regex, out string error)
        {
            regex = null;
            error = null;

            if (string.IsNullOrEmpty(pattern))
            {
                error = "Pattern cannot be empty.";
                return false;
            }

            try
            {
                var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
                if (!caseSensitive)
                    options |= RegexOptions.IgnoreCase;

                regex = new Regex(pattern, options);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void OnEditorUpdate()
        {
            var time = EditorApplication.timeSinceStartup;

            if (time >= s_nextSyncTime)
            {
                s_nextSyncTime = time + SyncIntervalSeconds;
                EnsureLogHandlerState();
            }

            if (time < s_nextConsoleRescanTime)
                return;

            s_nextConsoleRescanTime = time + ConsoleRescanIntervalSeconds;
            TryApplyFiltersToCurrentConsoleEntriesIfNeeded();
        }

        private static void ApplySettings()
        {
            var snapshot = ConsoleLogFilterSettings.Snapshot;
            var compiledRules = BuildCompiledRules(snapshot.rules);
            s_cleanStackTracesEnabled = snapshot.cleanStackTraces;
            SelectActiveLogHandler(snapshot.cleanStackTraces);
            s_activeFilteringLogHandler.Configure(snapshot.enabled, snapshot.warningsOnly, compiledRules);
            EnsureLogHandlerState();
            s_nextPeriodicRescanTime = 0d;
            ResetConsoleFingerprint();
            TryApplyFiltersToCurrentConsoleEntries(force: true, out _, out _);
        }

        private static CompiledRule[] BuildCompiledRules(List<ConsoleLogFilterRuleData> rules)
        {
            if (rules == null || rules.Count == 0)
                return Array.Empty<CompiledRule>();

            var compiledRules = new List<CompiledRule>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || !rule.enabled || string.IsNullOrWhiteSpace(rule.pattern))
                    continue;

                if (rule.matchType == ConsoleLogFilterMatchType.Contains)
                {
                    compiledRules.Add(CompiledRule.ForContains(rule.pattern, rule.caseSensitive));
                    continue;
                }

                if (TryCreateRegex(rule.pattern, rule.caseSensitive, out var regex, out _))
                    compiledRules.Add(CompiledRule.ForRegex(regex));
            }

            return compiledRules.Count == 0 ? Array.Empty<CompiledRule>() : compiledRules.ToArray();
        }

        private static void SelectActiveLogHandler(bool cleanStackTraces)
        {
            var selectedHandler = cleanStackTraces
                ? (IFilteringLogHandler)s_cleanStackFilteringLogHandler
                : s_fullStackFilteringLogHandler;

            if (ReferenceEquals(s_activeFilteringLogHandler, selectedHandler))
                return;

            var currentHandler = UnityEngine.Debug.unityLogger.logHandler;
            var previousHandler = s_activeFilteringLogHandler;

            if (previousHandler != null)
            {
                var previousInner = previousHandler.Inner;
                if (previousInner != null && !IsManagedFilteringHandler(previousInner))
                    selectedHandler.SetInner(previousInner);
            }

            if (ReferenceEquals(currentHandler, previousHandler))
                UnityEngine.Debug.unityLogger.logHandler = selectedHandler;

            s_activeFilteringLogHandler = selectedHandler;
        }

        private static void EnsureLogHandlerState()
        {
            var activeHandler = s_activeFilteringLogHandler ?? s_cleanStackFilteringLogHandler;
            var currentHandler = UnityEngine.Debug.unityLogger.logHandler;

            // In clean-stack mode we avoid wrapping Unity's logger to prevent handler frames
            // from entering stack traces; filtering/cleanup is done by console-entry rescans.
            if (s_cleanStackTracesEnabled)
            {
                if (!IsManagedFilteringHandler(currentHandler))
                    return;

                if (currentHandler is IFilteringLogHandler filteringHandler)
                {
                    var cleanStackFallbackHandler = filteringHandler.Inner;
                    if (cleanStackFallbackHandler != null && !IsManagedFilteringHandler(cleanStackFallbackHandler))
                        UnityEngine.Debug.unityLogger.logHandler = cleanStackFallbackHandler;
                }

                return;
            }

            if (activeHandler.FilteringEnabled)
            {
                if (ReferenceEquals(currentHandler, activeHandler))
                    return;

                if (currentHandler != null && !ReferenceEquals(currentHandler, activeHandler))
                {
                    if (IsManagedFilteringHandler(currentHandler))
                    {
                        if (currentHandler is IFilteringLogHandler filteringHandler && filteringHandler.Inner != null)
                            activeHandler.SetInner(filteringHandler.Inner);
                    }
                    else
                    {
                        activeHandler.SetInner(currentHandler);
                    }
                }

                UnityEngine.Debug.unityLogger.logHandler = activeHandler;
                return;
            }

            if (!ReferenceEquals(currentHandler, activeHandler))
                return;

            var fallbackHandler = activeHandler.Inner;
            if (fallbackHandler != null && !IsManagedFilteringHandler(fallbackHandler))
                UnityEngine.Debug.unityLogger.logHandler = fallbackHandler;
        }

        internal static bool TryUpdateCurrentLogs(out string message)
        {
            EnsureLogHandlerState();

            if (!s_activeFilteringLogHandler.FilteringEnabled)
            {
                message = "Enable hidden log filters and add at least one active rule first.";
                return false;
            }

            if (!TryApplyFiltersToCurrentConsoleEntries(force: true, out _, out message))
                return false;

            RefreshConsoleFingerprint();
            return true;
        }

        private static bool IsManagedFilteringHandler(ILogHandler handler)
        {
            return ReferenceEquals(handler, s_fullStackFilteringLogHandler) ||
                   ReferenceEquals(handler, s_cleanStackFilteringLogHandler);
        }

        private static MethodInfo GetLogEntriesMethod(string methodName, params Type[] argumentTypes)
        {
            if (s_logEntriesType == null)
                return null;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            if (argumentTypes == null || argumentTypes.Length == 0)
                return s_logEntriesType.GetMethod(methodName, flags);

            for (var i = 0; i < argumentTypes.Length; i++)
            {
                if (argumentTypes[i] != null)
                    continue;

                var methods = s_logEntriesType.GetMethods(flags);
                for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    var method = methods[methodIndex];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    if (method.GetParameters().Length == argumentTypes.Length)
                        return method;
                }

                return null;
            }

            return s_logEntriesType.GetMethod(
                methodName,
                flags,
                null,
                argumentTypes ?? Type.EmptyTypes,
                null);
        }

        private static MethodInfo GetLogEntriesAddMessageMethod()
        {
            if (s_logEntriesType == null)
                return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var candidateNames = new[]
            {
                "AddMessageWithDoubleClickCallback",
                "AddMessage",
                "AddMessageWithDoubleClickCallbackInternal"
            };

            var methods = s_logEntriesType.GetMethods(flags);
            for (var nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                var candidateName = candidateNames[nameIndex];
                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (!string.Equals(method.Name, candidateName, StringComparison.Ordinal))
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != 1)
                        continue;

                    if (s_logEntryType == null)
                        return method;

                    var parameterType = parameters[0].ParameterType;
                    if (parameterType == s_logEntryType || parameterType.IsAssignableFrom(s_logEntryType))
                        return method;
                }
            }

            return null;
        }

        private static int BuildModeMask(Predicate<string> includeModeName)
        {
            if (s_consoleWindowModeType == null || includeModeName == null)
                return 0;

            var mask = 0;
            var modeFields = s_consoleWindowModeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var i = 0; i < modeFields.Length; i++)
            {
                var modeField = modeFields[i];
                if (modeField == null)
                    continue;

                if (!includeModeName(modeField.Name))
                    continue;

                try
                {
                    mask |= Convert.ToInt32(modeField.GetValue(null), CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignored
                }
            }

            return mask;
        }

        private static bool IsWarningMode(int mode)
        {
            if (s_warningModeMask == 0)
                return false;

            return (mode & s_warningModeMask) != 0;
        }

        private static bool IsErrorMode(int mode)
        {
            if (s_errorModeMask == 0)
                return false;

            return (mode & s_errorModeMask) != 0;
        }

        private static bool IsLogMode(int mode)
        {
            if (s_logModeMask == 0)
                return false;

            return (mode & s_logModeMask) != 0;
        }

        private static void TryApplyFiltersToCurrentConsoleEntriesIfNeeded()
        {
            var filteringEnabled = AutomaticConsoleEntryMaintenanceEnabled;
            if (!filteringEnabled)
            {
                if (TryGetConsoleFingerprint(out var inactiveFingerprint))
                {
                    s_consoleFingerprint = inactiveFingerprint;
                    s_hasConsoleFingerprint = true;
                }

                return;
            }

            // Automatic maintenance is only needed while rules are actively suppressing
            // rows. In clean-stack mode without rules the logger is not wrapped, so
            // polling and cloning the Console every update just burns editor time.
            TryApplyFiltersToCurrentConsoleEntries(force: false, out _, out _);
            RefreshConsoleFingerprint();
        }

        private static void ResetConsoleFingerprint()
        {
            s_hasConsoleFingerprint = false;
        }

        private static void RefreshConsoleFingerprint()
        {
            if (!TryGetConsoleFingerprint(out var fingerprint))
                return;

            s_consoleFingerprint = fingerprint;
            s_hasConsoleFingerprint = true;
        }

        private static string FormatMessage(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            if (args == null || args.Length == 0)
                return format;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
