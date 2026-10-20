using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MinimalEditor.Console.Editor
{
    internal static partial class ConsoleLogFilterInstaller
    {
        private sealed class CompiledRule
        {
            private readonly string m_pattern;
            private readonly StringComparison m_stringComparison;
            private readonly Regex m_regex;
            private volatile bool m_timedOut;

            private CompiledRule(string pattern, StringComparison stringComparison, Regex regex)
            {
                m_pattern = pattern;
                m_stringComparison = stringComparison;
                m_regex = regex;
            }

            public bool Matches(string message)
            {
                if (m_timedOut)
                    return false;

                if (m_regex == null)
                    return message.IndexOf(m_pattern, m_stringComparison) >= 0;

                try
                {
                    return m_regex.IsMatch(message);
                }
                catch (RegexMatchTimeoutException)
                {
                    // Leave the message visible and stop retrying this rule on every
                    // Console rescan. Saving settings creates a fresh compiled rule.
                    m_timedOut = true;
                    return false;
                }
            }

            public static CompiledRule ForContains(string pattern, bool caseSensitive)
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return new CompiledRule(pattern, comparison, null);
            }

            public static CompiledRule ForRegex(Regex regex)
            {
                return new CompiledRule(null, StringComparison.Ordinal, regex);
            }
        }

        private interface IFilteringLogHandler : ILogHandler
        {
            bool FilteringEnabled { get; }
            ILogHandler Inner { get; }
            void Configure(bool enabled, bool warningsOnly, CompiledRule[] rules);
            void SetInner(ILogHandler inner);
            bool ShouldSuppressMode(int mode, string message);
        }

        private sealed class FilterConfiguration
        {
            internal readonly bool Enabled;
            internal readonly bool WarningsOnly;
            internal readonly CompiledRule[] Rules;

            internal FilterConfiguration(bool enabled, bool warningsOnly, CompiledRule[] rules)
            {
                Rules = rules ?? Array.Empty<CompiledRule>();
                Enabled = enabled && Rules.Length > 0;
                WarningsOnly = warningsOnly;
            }
        }

        private sealed class FilterLogHandler : IFilteringLogHandler
        {
            private volatile FilterConfiguration m_configuration = new(false, true, null);
            private volatile ILogHandler m_inner;

            public bool FilteringEnabled => m_configuration.Enabled;
            public ILogHandler Inner => m_inner;

            public void Configure(bool enabled, bool warningsOnly, CompiledRule[] rules)
            {
                // Logs can arrive on worker threads while preferences change.
                m_configuration = new FilterConfiguration(enabled, warningsOnly, rules);
            }

            public void SetInner(ILogHandler inner)
            {
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                m_inner = inner;
            }

            public void LogException(Exception exception, Object context)
            {
                var inner = m_inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                var configuration = m_configuration;
                if (configuration.Enabled && !configuration.WarningsOnly &&
                    MatchesRules(configuration.Rules, exception?.ToString()))
                    return;

                inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                var inner = m_inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                var configuration = m_configuration;
                if (configuration.Enabled && (!configuration.WarningsOnly || logType == LogType.Warning) &&
                    MatchesRules(configuration.Rules, FormatMessage(format, args)))
                    return;

                inner.LogFormat(logType, context, format, args);
            }

            public bool ShouldSuppressMode(int mode, string message)
            {
                var configuration = m_configuration;
                if (!configuration.Enabled)
                    return false;

                if (configuration.WarningsOnly)
                {
                    var isWarning = IsWarningMode(mode);
                    var isError = IsErrorMode(mode);

                    // Unity displays mixed error/warning modes as errors. Keep those
                    // visible, and use message text only for otherwise unclassified rows.
                    if (isError || (!isWarning && (IsLogMode(mode) || !IsCompilerWarningMessage(message))))
                        return false;
                }

                return MatchesRules(configuration.Rules, message);
            }

            private static bool MatchesRules(CompiledRule[] rules, string message)
            {
                if (string.IsNullOrEmpty(message))
                    return false;

                for (var i = 0; i < rules.Length; i++)
                {
                    if (rules[i].Matches(message))
                        return true;
                }

                return false;
            }

            private static bool IsCompilerWarningMessage(string message)
            {
                if (string.IsNullOrEmpty(message))
                    return false;

                return message.IndexOf(" warning CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       message.IndexOf("): warning ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       message.IndexOf(": warning ", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
