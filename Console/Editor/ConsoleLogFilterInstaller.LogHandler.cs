using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MartinCalander.Essentials.Console.Editor
{
    internal static partial class ConsoleLogFilterInstaller
    {
        private readonly struct CompiledRule
        {
            private readonly string m_pattern;
            private readonly StringComparison m_stringComparison;
            private readonly Regex m_regex;

            private CompiledRule(string pattern, StringComparison stringComparison, Regex regex)
            {
                m_pattern = pattern;
                m_stringComparison = stringComparison;
                m_regex = regex;
            }

            public bool Matches(string message)
            {
                if (m_regex != null)
                    return m_regex.IsMatch(message);

                return message.IndexOf(m_pattern, m_stringComparison) >= 0;
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

        private abstract class FilterLogHandlerBase : IFilteringLogHandler
        {
            private volatile bool m_enabled;
            private volatile bool m_warningsOnly;
            private volatile CompiledRule[] m_rules = Array.Empty<CompiledRule>();
            private ILogHandler m_inner;

            public bool FilteringEnabled => m_enabled && m_rules.Length > 0;
            public ILogHandler Inner => m_inner;

            public void Configure(bool enabled, bool warningsOnly, CompiledRule[] rules)
            {
                m_enabled = enabled;
                m_warningsOnly = warningsOnly;
                m_rules = rules ?? Array.Empty<CompiledRule>();
            }

            public void SetInner(ILogHandler inner)
            {
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                m_inner = inner;
            }

            protected void LogExceptionCore(Exception exception, Object context)
            {
                var inner = m_inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                if (ShouldSuppressLogType(LogType.Exception, exception?.ToString()))
                    return;

                inner.LogException(exception, context);
            }

            protected void LogFormatCore(LogType logType, Object context, string format, params object[] args)
            {
                var inner = m_inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                if (ShouldSuppressLogType(logType, FormatMessage(format, args)))
                    return;

                inner.LogFormat(logType, context, format, args);
            }

            public bool ShouldSuppressMode(int mode, string message)
            {
                if (!m_enabled)
                    return false;

                if (m_warningsOnly)
                {
                    var isWarning = IsWarningMode(mode);
                    var isError = IsErrorMode(mode);
                    var looksLikeCompilerWarning = IsCompilerWarningMessage(message);

                    // Keep true error rows visible even when warning filtering is enabled.
                    // Mixed mode rows (some compiler/import warnings) are treated as warning-capable.
                    if (isError && !isWarning && !looksLikeCompilerWarning)
                        return false;
                }

                return MatchesRules(message);
            }

            protected bool ShouldSuppressLogType(LogType logType, string message)
            {
                if (!m_enabled)
                    return false;

                if (m_warningsOnly && logType != LogType.Warning)
                    return false;

                return MatchesRules(message);
            }

            private bool MatchesRules(string message)
            {
                if (string.IsNullOrEmpty(message))
                    return false;

                var rules = m_rules;
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

            public abstract void LogException(Exception exception, Object context);
            public abstract void LogFormat(LogType logType, Object context, string format, params object[] args);
        }

        private sealed class FilterLogHandler : FilterLogHandlerBase
        {
            public override void LogException(Exception exception, Object context)
            {
                LogExceptionCore(exception, context);
            }

            public override void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                LogFormatCore(logType, context, format, args);
            }
        }

        private sealed class DeclutteredFilterLogHandler : FilterLogHandlerBase
        {
            [HideInCallstack]
            public override void LogException(Exception exception, Object context)
            {
                var inner = Inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                if (ShouldSuppressLogType(LogType.Exception, exception?.ToString()))
                    return;

                inner.LogException(exception, context);
            }

            [HideInCallstack]
            public override void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                var inner = Inner;
                if (inner == null || ReferenceEquals(inner, this))
                    return;

                if (ShouldSuppressLogType(logType, FormatMessage(format, args)))
                    return;

                inner.LogFormat(logType, context, format, args);
            }
        }
    }
}
