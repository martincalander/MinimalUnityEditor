using System;

namespace MinimalEditor.Editor
{
    public static class EditorFeatureToggles
    {
        public static bool ShortWindowTitle
        {
            get => WindowTitleShortener.Active;
            set => WindowTitleShortener.Active = value;
        }

        public static bool HideStatusBarExtras
        {
            get => StatusBarExtrasHider.Active;
            set => StatusBarExtrasHider.Active = value;
        }

        public static bool HideInspectorHelpButton
        {
            get => InspectorHeaderButtonsHider.HideHelp;
            set => InspectorHeaderButtonsHider.HideHelp = value;
        }

        public static bool HideInspectorPresetButton
        {
            get => InspectorHeaderButtonsHider.HidePreset;
            set => InspectorHeaderButtonsHider.HidePreset = value;
        }

        public static bool HideInspectorAssetLabels
        {
            get => InspectorAssetLabelsHider.Active;
            set => InspectorAssetLabelsHider.Active = value;
        }

        public static bool QuitAutomatedEditor
        {
            get => AutomatedEditorQuit.Active;
            set => AutomatedEditorQuit.Active = value;
        }

        public static bool HideAssetPostprocessors
        {
            get => EditorElementHider.HideAssetPostprocessors;
            set => EditorElementHider.HideAssetPostprocessors = value;
        }

        public static bool HideStatusBarDebugger
        {
            get => EditorElementHider.HideStatusBarDebugger;
            set => EditorElementHider.HideStatusBarDebugger = value;
        }

        public static void SetShortWindowTitleInput(Func<bool> input)
        {
            WindowTitleShortener.ActiveInput = input;
        }

        public static void SetStatusBarExtrasInput(Func<bool> input)
        {
            StatusBarExtrasHider.ActiveInput = input;
        }
    }
}
