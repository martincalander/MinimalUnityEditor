using System;
using System.Collections.Generic;
using MinimalEditor.MenuItem.Editor;

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

        public static bool HideMenuItems
        {
            get => MenuItemFeatureToggles.HideMenuItems;
            set => MenuItemFeatureToggles.HideMenuItems = value;
        }

        public static void SetMenuItemPaths(IEnumerable<string> paths)
        {
            MenuItemFeatureToggles.SetMenuItemPaths(paths);
        }

        public static void SetMenuItemPathsInput(Func<IEnumerable<string>> input)
        {
            MenuItemFeatureToggles.SetMenuItemPathsInput(input);
        }

        public static void SetShortWindowTitleInput(Func<bool> input)
        {
            WindowTitleShortener.ActiveInput = input;
        }

        public static void SetStatusBarExtrasInput(Func<bool> input)
        {
            StatusBarExtrasHider.ActiveInput = input;
        }

        public static void SetMenuItemsInput(Func<bool> input)
        {
            MenuItemFeatureToggles.SetHideInput(input);
        }

    }
}
