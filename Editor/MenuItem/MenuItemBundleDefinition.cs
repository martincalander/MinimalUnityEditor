using System;
using System.Collections.Generic;

namespace MinimalEditor.MenuItem.Editor
{
    internal sealed class MenuItemBundleDefinition
    {
        internal MenuItemBundleDefinition(
            string id,
            string displayName,
            bool defaultEnabled,
            bool isAvailable,
            string[] menuPaths)
        {
            Id = id;
            DisplayName = displayName;
            DefaultEnabled = defaultEnabled;
            IsAvailable = isAvailable;
            MenuPaths = menuPaths ?? Array.Empty<string>();
        }

        internal string Id { get; }
        internal string DisplayName { get; }
        internal bool DefaultEnabled { get; }
        internal bool IsAvailable { get; }
        internal IReadOnlyList<string> MenuPaths { get; }
    }
}
