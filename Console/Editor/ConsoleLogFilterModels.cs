using System;
using System.Collections.Generic;

namespace Essentials.Console.Editor
{
    internal enum ConsoleLogFilterMatchType
    {
        Contains,
        Regex
    }

    [Serializable]
    internal sealed class ConsoleLogFilterRuleData
    {
        public bool enabled = true;
        public ConsoleLogFilterMatchType matchType = ConsoleLogFilterMatchType.Contains;
        public bool caseSensitive;
        public string pattern = string.Empty;

        public ConsoleLogFilterRuleData Clone()
        {
            return new ConsoleLogFilterRuleData
            {
                enabled = enabled,
                matchType = matchType,
                caseSensitive = caseSensitive,
                pattern = pattern
            };
        }
    }

    [Serializable]
    internal sealed class ConsoleLogFilterSettingsData
    {
        public bool enabled = true;
        public bool warningsOnly = true;
        public bool cleanStackTraces = true;
        public List<ConsoleLogFilterRuleData> rules = new();

        public ConsoleLogFilterSettingsData Clone()
        {
            var clone = new ConsoleLogFilterSettingsData
            {
                enabled = enabled,
                warningsOnly = warningsOnly,
                cleanStackTraces = cleanStackTraces,
                rules = new List<ConsoleLogFilterRuleData>(rules.Count)
            };

            for (var i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null)
                    continue;

                clone.rules.Add(rules[i].Clone());
            }

            return clone;
        }
    }
}
