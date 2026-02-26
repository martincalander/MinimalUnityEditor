using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.Console.Editor
{
    internal static class ConsoleLogFilterSettingsProvider
    {
        internal const string SettingsPath = "Preferences/Console";

        private static readonly GUIContent EnableFiltersContent = new(
            "Enable Hidden Log Filters",
            "Suppresses matching logs before they are written to the Console.");

        private static readonly GUIContent WarningsOnlyContent = new(
            "Warnings Only",
            "When enabled, filters are only applied to warning logs.");

        private static readonly GUIContent CleanStackTracesContent = new(
            "Clean Stack Traces",
            "Hides Essentials Console filter frames from stack traces so logs focus on your code.");

        private static readonly GUIContent PatternContent = new(
            "Pattern",
            "String or regex pattern to hide.");

        private static readonly GUIContent UpdateCurrentLogsContent = new(
            "Update Current Logs",
            "Reapplies active hidden-log rules to logs currently visible in the Console.");

        [MenuItem("Window/Analysis/Console Preferences", false, 3200)]
        private static void OpenSettings()
        {
            SettingsService.OpenUserPreferences(SettingsPath);
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Console",
                guiHandler = DrawGUI,
                keywords = new HashSet<string>
                {
                    "console",
                    "log",
                    "warning",
                    "regex",
                    "filter",
                    "hide",
                    "stack trace",
                    "essentials"
                }
            };
        }

        private static void DrawGUI(string searchContext)
        {
            var settings = ConsoleLogFilterSettings.Data;
            var changed = false;

            EditorGUILayout.HelpBox(
                "Matching logs are hidden before they are written to Console. Current rows are also re-filtered while the editor is open.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!settings.enabled || settings.rules.Count == 0))
            {
                if (GUILayout.Button(UpdateCurrentLogsContent))
                {
                    if (!ConsoleLogFilterInstaller.TryUpdateCurrentLogs(out var message))
                        EditorUtility.DisplayDialog("Console", $"Failed to update logs:\n\n{message}", "OK");
                }
            }

            EditorGUI.BeginChangeCheck();
            settings.enabled = EditorGUILayout.ToggleLeft(EnableFiltersContent, settings.enabled);
            settings.cleanStackTraces = EditorGUILayout.ToggleLeft(CleanStackTracesContent, settings.cleanStackTraces);

            using (new EditorGUI.DisabledScope(!settings.enabled))
            {
                settings.warningsOnly = EditorGUILayout.ToggleLeft(WarningsOnlyContent, settings.warningsOnly);
            }

            if (EditorGUI.EndChangeCheck())
                changed = true;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);

            if (settings.rules.Count == 0)
            {
                EditorGUILayout.HelpBox("No rules yet. Add a string or regex rule below.", MessageType.None);
            }

            for (var i = 0; i < settings.rules.Count; i++)
            {
                var rule = settings.rules[i];
                if (rule == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();

                    rule.enabled = EditorGUILayout.ToggleLeft("Enabled", rule.enabled);
                    rule.matchType = (ConsoleLogFilterMatchType)EditorGUILayout.EnumPopup("Match Type", rule.matchType);
                    rule.caseSensitive = EditorGUILayout.ToggleLeft("Case Sensitive", rule.caseSensitive);
                    rule.pattern = EditorGUILayout.TextField(PatternContent, rule.pattern);

                    if (EditorGUI.EndChangeCheck())
                        changed = true;

                    if (rule.matchType == ConsoleLogFilterMatchType.Regex &&
                        !string.IsNullOrWhiteSpace(rule.pattern) &&
                        !ConsoleLogFilterInstaller.TryCreateRegex(rule.pattern, rule.caseSensitive, out _, out var regexError))
                    {
                        EditorGUILayout.HelpBox(regexError, MessageType.Error);
                    }

                    if (GUILayout.Button("Remove Rule"))
                    {
                        settings.rules.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add String Rule"))
                {
                    settings.rules.Add(new ConsoleLogFilterRuleData
                    {
                        enabled = true,
                        matchType = ConsoleLogFilterMatchType.Contains,
                        caseSensitive = false,
                        pattern = string.Empty
                    });
                    changed = true;
                }

                if (GUILayout.Button("Add Regex Rule"))
                {
                    settings.rules.Add(new ConsoleLogFilterRuleData
                    {
                        enabled = true,
                        matchType = ConsoleLogFilterMatchType.Regex,
                        caseSensitive = false,
                        pattern = string.Empty
                    });
                    changed = true;
                }
            }

            if (settings.rules.Count > 0 && GUILayout.Button("Clear All Rules"))
            {
                settings.rules.Clear();
                changed = true;
            }

            if (!changed)
                return;

            ConsoleLogFilterSettings.Save();
        }
    }
}
