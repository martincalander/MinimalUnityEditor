using System;

namespace Essentials.Console.Editor
{
    public static class ConsoleLogFilters
    {
        public static void SetEnabled(bool enabled)
        {
            var settings = ConsoleLogFilterSettings.Data;
            settings.enabled = enabled;
            ConsoleLogFilterSettings.Save();
        }

        public static void ClearRules()
        {
            var settings = ConsoleLogFilterSettings.Data;
            settings.rules.Clear();
            ConsoleLogFilterSettings.Save();
        }

        public static void AddStringRule(string value, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            AddRule(new ConsoleLogFilterRuleData
            {
                enabled = true,
                matchType = ConsoleLogFilterMatchType.Contains,
                caseSensitive = caseSensitive,
                pattern = value
            });
        }

        public static void AddRegexRule(string pattern, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return;

            AddRule(new ConsoleLogFilterRuleData
            {
                enabled = true,
                matchType = ConsoleLogFilterMatchType.Regex,
                caseSensitive = caseSensitive,
                pattern = pattern
            });
        }

        private static void AddRule(ConsoleLogFilterRuleData newRule)
        {
            var settings = ConsoleLogFilterSettings.Data;
            foreach (var existingRule in settings.rules)
            {
                if (existingRule == null)
                    continue;

                if (existingRule.matchType != newRule.matchType)
                    continue;

                if (existingRule.caseSensitive != newRule.caseSensitive)
                    continue;

                if (!string.Equals(existingRule.pattern, newRule.pattern, StringComparison.Ordinal))
                    continue;

                existingRule.enabled = true;
                ConsoleLogFilterSettings.Save();
                return;
            }

            settings.rules.Add(newRule);
            ConsoleLogFilterSettings.Save();
        }
    }
}
