using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Console.Editor
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
            "Hides Console filter frames from stack traces so logs focus on your code.");

        private static readonly GUIContent HideAssistantToolbarButtonContent = new(
            "Hide Assistant Toolbar Button",
            "When Unity AI Assistant is installed, removes its 'Add to Assistant' button from the Console toolbar.");

        private static readonly GUIContent PatternContent = new(
            "Pattern",
            "String or regex pattern to hide.");
        private static readonly GUIContent RemoveRuleContent = BuildRemoveRuleContent();

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
                    "assistant",
                    "ai",
                    "toolbar"
                }
            };
        }

        private static GUIContent BuildRemoveRuleContent()
        {
            const string tooltip = "Remove this rule.";

            var trashIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (trashIcon?.image != null)
                return new GUIContent(trashIcon.image, tooltip);

            var darkTrashIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
            if (darkTrashIcon?.image != null)
                return new GUIContent(darkTrashIcon.image, tooltip);

            return new GUIContent("X", tooltip);
        }

        private static void DrawGUI(string searchContext)
        {
            var settings = ConsoleLogFilterSettings.Data;
            var changed = false;
            var assistantToolbarAvailable = ConsoleAssistantToolbarButtonController.IsAssistantConsoleToolbarAvailable;

            EditorGUILayout.HelpBox(
                "Matching logs are hidden before they are written to Console. Current rows are also re-filtered while the editor is open.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            settings.enabled = EditorGUILayout.ToggleLeft(EnableFiltersContent, settings.enabled);
            settings.cleanStackTraces = EditorGUILayout.ToggleLeft(CleanStackTracesContent, settings.cleanStackTraces);

            using (new EditorGUI.DisabledScope(!assistantToolbarAvailable))
            {
                settings.hideAssistantToolbarButton = EditorGUILayout.ToggleLeft(
                    HideAssistantToolbarButtonContent,
                    settings.hideAssistantToolbarButton);
            }

            using (new EditorGUI.DisabledScope(!settings.enabled))
            {
                settings.warningsOnly = EditorGUILayout.ToggleLeft(WarningsOnlyContent, settings.warningsOnly);
            }

            if (EditorGUI.EndChangeCheck())
                changed = true;

            if (!assistantToolbarAvailable)
            {
                EditorGUILayout.HelpBox(
                    "Unity AI Assistant console toolbar integration was not detected in this project, so the Assistant toolbar toggle has no effect right now.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);

            if (settings.rules.Count == 0)
            {
                EditorGUILayout.HelpBox("No rules yet. Add a rule below.", MessageType.None);
            }

            for (var i = 0; i < settings.rules.Count; i++)
            {
                var rule = settings.rules[i];
                if (rule == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var removeRule = false;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        rule.enabled = EditorGUILayout.ToggleLeft("Enabled", rule.enabled);
                        if (EditorGUI.EndChangeCheck())
                            changed = true;

                        if (GUILayout.Button(
                                RemoveRuleContent,
                                EditorStyles.miniButton,
                                GUILayout.Width(24f),
                                GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        {
                            removeRule = true;
                        }
                    }

                    if (removeRule)
                    {
                        settings.rules.RemoveAt(i);
                        changed = true;
                        i--;
                        continue;
                    }

                    EditorGUI.BeginChangeCheck();
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
                }
            }

            if (GUILayout.Button("Add Rule"))
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

            if (settings.rules.Count > 0 && GUILayout.Button("Clear All Rules"))
            {
                if (EditorUtility.DisplayDialog(
                        "Console",
                        "Clear all rules?",
                        "Clear All Rules",
                        "Cancel"))
                {
                    settings.rules.Clear();
                    changed = true;
                }
            }

            if (!changed)
                return;

            ConsoleLogFilterSettings.Save();
        }
    }
}
