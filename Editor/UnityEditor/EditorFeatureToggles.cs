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
