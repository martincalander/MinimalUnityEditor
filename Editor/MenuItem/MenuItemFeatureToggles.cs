using System;
using System.Collections.Generic;

namespace Essentials.MenuItem.Editor
{
    public static class MenuItemFeatureToggles
    {
        public static bool ModuleEnabled
        {
            get => MenuItemHider.Active;
            set => MenuItemHider.Active = value;
        }

        public static bool HideMenuItems
        {
            get => MenuItemHider.Active;
            set => MenuItemHider.Active = value;
        }

        public static bool CustomDefinitionsEnabled
        {
            get => MenuItemModuleSettings.CustomDefinitionsEnabled;
            set
            {
                MenuItemModuleSettings.CustomDefinitionsEnabled = value;
                MenuItemHider.Apply();
            }
        }

        public static string CustomDefinitionsText
        {
            get => MenuItemModuleSettings.CustomDefinitionsText;
            set
            {
                MenuItemModuleSettings.CustomDefinitionsText = value;
                if (MenuItemModuleSettings.CustomDefinitionsEnabled)
                {
                    MenuItemHider.Apply();
                }
            }
        }

        public static void SetMenuItemPaths(IEnumerable<string> paths)
        {
            MenuItemHider.SetMenuItems(paths);
        }

        public static void SetMenuItemPathsInput(Func<IEnumerable<string>> input)
        {
            MenuItemHider.MenuItemsInput = input;
        }

        public static void SetHideInput(Func<bool> input)
        {
            MenuItemHider.ActiveInput = input;
        }

        public static bool GetBundleEnabled(string bundleId)
        {
            return MenuItemModuleSettings.GetBundleEnabled(bundleId);
        }

        public static void SetBundleEnabled(string bundleId, bool enabled)
        {
            MenuItemModuleSettings.SetBundleEnabled(bundleId, enabled);
            MenuItemHider.Apply();
        }
    }
}
