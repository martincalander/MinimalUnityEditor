# User Guide

Minimal Editor has no central dashboard. Each setting lives beside the Unity
surface it affects.

## Console

Open **Preferences > Console**.

The filter defaults to enabled when no earlier preference exists, but the
initial rule list is empty. A rule can use:

- **Contains** for a plain-text match;
- **Regex** for a regular expression;
- **Case Sensitive** when letter case matters.

**Warnings Only** is enabled by default. Turn it off when rules should also
apply to other Console message types. **Clean Stack Traces** removes the
filter's own helper frames from displayed stacks.

When active rules exist, the package revisits current Console rows while the
Editor is open. A newly written matching row can therefore appear briefly
before it is removed. Invalid regular expressions are reported beside the rule
and are not applied.

If Unity AI Assistant is installed and its Console integration is detected,
**Hide Assistant Toolbar Button** removes the **Add to Assistant** button. The
toggle is disabled when that optional integration is not present.

Console settings are stored in the user-global Editor preference key
`Essentials.Console.HiddenLogFilters`. They carry across Unity projects opened
by the same operating-system user.

## Menu Item

Open **Project Settings > Essentials > Menu Item**.

The module starts enabled. Nearly every available built-in bundle also starts
enabled; **D3D12/Vulkan** is the intentional exception. Unavailable optional
bundles remain visible, disabled, and marked **not installed**.

The catalog covers TextMeshPro, legacy UI, UI Toolkit, Jobs, UPM packaging,
Terrain, XR, glTF, extra Asset and GameObject commands, Version Control, and
supported optional packages. Availability follows the relevant Unity module or
package gate; for example, XR Options follows the Input System package. Hover a
bundle to see the paths it owns.

Turning off a bundle stops its removal on the next apply pass, but Unity does
not safely expose a live menu rebuild for this module. Already removed entries
return after a normal domain reload or Editor reopen.

### Custom Paths

Expand **Advanced Settings** to enter custom definitions.

- Put one path on each line, or separate paths with `;`.
- Prefix a full line with `#` or `//` to make it a comment.
- Surrounding single or double quotes are ignored.
- Matching is case-insensitive.
- A trailing `*` matches everything below a path prefix.

For example:

```text
# Hide one exact command
Assets/Import Package/Custom Package...

# Hide one submenu
Window/Analysis/*
```

Unity does not expose every menu path safely. Root-only paths and `Services/**`
entries are ignored, and known error-prone entries are reported in the
settings page.

Menu choices are saved in `ProjectSettings/MenuItemSettings.json` and should
be reviewed like any other project setting.

## Unity Editor Chrome

Open **Preferences > Essentials > Unity Editor**.

- **Short Window Title** uses `PlayerSettings.productName` as the main Editor
  window title.
- **Hide Status Bar Extras** hides Unity's animation and memory statistics.

Both options start enabled when no earlier preference exists. Their user-global
`EditorPrefs` values carry across Unity projects. If a Unity release changes
the internal status-bar contract, that refinement does not apply.

## Package Manager

This module compiles only on Unity 6 or newer.

On Package Manager's **In Project** page, an Asset Store-backed row is hidden
when its matching read-only UPM package already resolves outside the project's
`Packages/` folder. A matching embedded package or submodule keeps its Asset
Store row; that row's details toolbar receives **Go To Local Package** instead.

**Window > Package Management > Re-resolve All Packages** deletes
`Packages/packages-lock.json` and asks Unity Package Manager to resolve the
project again. There is no confirmation dialog. Commit or otherwise preserve
intentional lockfile changes before using it, then review the regenerated file.

The Package Manager integration patches exact Unity internals. If those
internals differ, the affected convenience is left inactive rather than
changing unrelated Package Manager pages.

## FMOD

When `com.fmod.fmod-for-unity-2-03` is installed, equivalent FMOD commands are
registered under **Tools > FMOD** and the original root entries are removed on
a best-effort basis. Commands whose matching FMOD API is unavailable remain
disabled or absent.

There is no FMOD settings page in Minimal Editor.

## Scripting

The package keeps small public Editor APIs for projects that need to configure
the same behavior from code:

- `Essentials.Console.Editor.ConsoleLogFilters`;
- `Essentials.Console.Editor.ConsoleHyperlinks`;
- `Essentials.MenuItem.Editor.MenuItemFeatureToggles`;
- `Essentials.Editor.EditorFeatureToggles`.

Production asmdefs are not auto-referenced. Add the matching `Essentials.*`
assembly reference to the consuming Editor asmdef. Programmatic path and
delegate inputs are session state and must be registered again after a domain
reload. See [Compatibility and scripting](compatibility.md) for an overview.
