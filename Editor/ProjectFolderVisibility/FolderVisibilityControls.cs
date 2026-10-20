using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.ProjectFolderVisibility.Editor
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    internal static class FolderVisibilityControls
    {
        internal const string PreferencesPath = "Preferences/Minimal Editor/Project Folders";

        static GUIContent s_EnabledContent;
        static GUIContent s_DisabledContent;
        static GUIContent s_MenuContent;
        static DefaultAsset s_FolderToAdd;
        static string s_AddError;

        internal static void Install(Type browserType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = browserType.GetMethod("ToggleHiddenPackagesVisibility", flags, null, Type.EmptyTypes, null) ??
                         throw new MissingMethodException(browserType.FullName, "ToggleHiddenPackagesVisibility");
            AddressablesFolderVisibility.Patch(method, typeof(FolderVisibilityControls), postfix: nameof(DrawToolbar));
        }

        static void DrawToolbar()
        {
            try
            {
                if (s_EnabledContent == null)
                {
                    var icon = EditorGUIUtility.IconContent("Folder Icon").image;
                    s_EnabledContent = new GUIContent(icon,
                        "Folder hiding is on. Click to show all configured folders.");
                    s_DisabledContent = new GUIContent(icon,
                        "Folder hiding is off. Click to hide the folders selected in Preferences.");
                    s_MenuContent = new GUIContent(string.Empty, "Choose which Project folders to hide.");
                }

                // This hook runs inside the Project browser's existing toolbar layout group.
                var enabled = FolderVisibilitySettings.Enabled;
                var content = enabled ? s_EnabledContent : s_DisabledContent;
                var nextEnabled = GUILayout.Toggle(enabled, content, EditorStyles.toolbarButton, GUILayout.Width(26f));
                if (nextEnabled != enabled)
                    FolderVisibilitySettings.Enabled = nextEnabled;

                var rect = GUILayoutUtility.GetRect(s_MenuContent, EditorStyles.toolbarDropDown, GUILayout.Width(16f));
                if (EditorGUI.DropdownButton(rect, s_MenuContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    CreateMenu().DropDown(rect);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                AddressablesFolderVisibility.DisableAfterFailure(exception);
            }
        }

        static GenericMenu CreateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Hide selected folders"), FolderVisibilitySettings.Enabled,
                () => FolderVisibilitySettings.Enabled = !FolderVisibilitySettings.Enabled);
            menu.AddSeparator(string.Empty);
            foreach (var rule in FolderVisibilitySettings.Folders)
            {
                var path = rule.Path;
                var hidden = rule.Hidden;
                var label = "Hide " + path.Substring("Assets/".Length).Replace("/", " > ");
                menu.AddItem(new GUIContent(label, path), hidden,
                    () => FolderVisibilitySettings.SetFolderHidden(path, !hidden));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Preferences..."), false,
                () => SettingsService.OpenUserPreferences(PreferencesPath));
            return menu;
        }

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(PreferencesPath, SettingsScope.User)
            {
                label = "Project Folders",
                guiHandler = _ => DrawPreferences(),
                keywords = new HashSet<string>
                {
                    "Minimal Editor", "Project", "Folder", "Visibility", "Hide", "Addressables", "StreamingAssets",
                    "Plugins", "Gizmos", "Settings"
                }
            };
        }

        static void DrawPreferences()
        {
            EditorGUILayout.LabelField("Project Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose folders to hide in this project's Project windows. These preferences are saved locally " +
                "for this project. Files remain in place and stay available to imports and builds.", MessageType.Info);
            var enabled = EditorGUILayout.Toggle("Hide selected folders", FolderVisibilitySettings.Enabled);
            if (enabled != FolderVisibilitySettings.Enabled)
                FolderVisibilitySettings.Enabled = enabled;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Folders", EditorStyles.boldLabel);
            string removePath = null;
            var rules = FolderVisibilitySettings.Folders;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                EditorGUILayout.BeginHorizontal();
                var hidden = EditorGUILayout.ToggleLeft(new GUIContent(rule.Path, "Hide this folder and its contents."),
                    rule.Hidden);
                if (hidden != rule.Hidden)
                    FolderVisibilitySettings.SetFolderHidden(rule.Path, hidden);
                if (FolderVisibilitySettings.IsBuiltInFolder(rule.Path))
                    GUILayout.Space(24f);
                else if (GUILayout.Button(new GUIContent("-", "Remove this folder from the list."), GUILayout.Width(24f)))
                    removePath = rule.Path;
                EditorGUILayout.EndHorizontal();
            }
            if (removePath != null)
                FolderVisibilitySettings.RemoveFolder(removePath);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            s_FolderToAdd = (DefaultAsset)EditorGUILayout.ObjectField("Add folder", s_FolderToAdd, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
                s_AddError = null;
            using (new EditorGUI.DisabledScope(s_FolderToAdd == null))
            {
                if (GUILayout.Button("Add", GUILayout.Width(48f)))
                {
                    var path = AssetDatabase.GetAssetPath(s_FolderToAdd);
                    if (AssetDatabase.IsValidFolder(path) && FolderVisibilitySettings.AddFolder(path))
                    {
                        s_FolderToAdd = null;
                        s_AddError = null;
                    }
                    else
                    {
                        s_AddError = "Choose a folder under Assets that is not already in the list.";
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            if (s_AddError != null)
                EditorGUILayout.HelpBox(s_AddError, MessageType.Warning);
            if (AddressablesFolderVisibility.PatchError != null)
                EditorGUILayout.HelpBox("Folder hiding is unavailable: " + AddressablesFolderVisibility.PatchError,
                    MessageType.Warning);
        }
    }
}
