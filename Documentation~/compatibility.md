# Compatibility and Scripting

## Unity Compatibility

| Module | Availability |
| --- | --- |
| Console | Declared package baseline: Unity `2021.3.0f1` |
| Menu Item | Declared baseline; disabled on Unity 6.0 before `6000.0.43f1` |
| Unity Editor chrome | Declared package baseline |
| Package Manager | Unity 6 or newer |
| FMOD | Only when `com.fmod.fmod-for-unity-2-03` is installed |

The manifest minimum is not a record of validation against every Unity release.
Several refinements use guarded reflection against Editor internals. When a
required member is absent, the individual refinement remains inactive.

## Assembly Identities

The package retains these Editor assembly names:

- `Essentials.Console.Editor`
- `Essentials.MenuItem.Editor`
- `Essentials.PackageManager.Editor`
- `Essentials.FMOD.Editor`
- `Essentials.Editor`

They are Editor-only and have `autoReferenced` disabled. A consuming Editor
asmdef must explicitly reference the assembly that exposes the API it uses.

## Public API Overview

### Console

`Essentials.Console.Editor.ConsoleLogFilters` provides:

- `SetEnabled(bool)`
- `ClearRules()`
- `AddStringRule(string, bool)`
- `AddRegexRule(string, bool)`

`Essentials.Console.Editor.ConsoleHyperlinks` registers and unregisters handlers
for Console hyperlink `href` values.

### Menu and Editor Chrome

`Essentials.MenuItem.Editor.MenuItemFeatureToggles` controls the menu module,
bundle states, custom definitions, and programmatic path inputs.

`Essentials.Editor.EditorFeatureToggles` controls the short title, status-bar
extras, and the linked Menu Item behavior.

Persisted properties write the settings described below. Delegate and manual
path inputs are session-only and need to be registered again after a domain
reload. These types preserve their established namespaces and
assembly-qualified names.

## Compile Symbols

| Symbol | Effect |
| --- | --- |
| `ESSENTIALS_DISABLE_MINIMAL_EDITOR` | Disables every package feature and test asmdef |
| `ESSENTIALS_DISABLE_ALL` | Disables all retained Essentials modules |
| `ESSENTIALS_DISABLE_CONSOLE` | Disables Console features |
| `ESSENTIALS_DISABLE_MENU_ITEM` | Disables Menu Item and the dependent Editor chrome assembly |
| `ESSENTIALS_DISABLE_PACKAGE_MANAGER` | Disables Unity 6 Package Manager refinements |
| `ESSENTIALS_DISABLE_FMOD` | Disables FMOD menu relocation |
| `ESSENTIALS_DISABLE_UNITY_EDITOR` | Disables short title and status-bar features |

`ESSENTIALS_DISABLE_UNITY_EDITOR` leaves Menu Item available.
`ESSENTIALS_DISABLE_MENU_ITEM` is broader because `Essentials.Editor` directly
references `Essentials.MenuItem.Editor`.

## Persisted Settings

| Area | Storage |
| --- | --- |
| Console | EditorPrefs key `Essentials.Console.HiddenLogFilters` |
| Menu Item | `ProjectSettings/MenuItemSettings.json` |
| Short title | EditorPrefs key `Essentials.Editor.WindowTitleShortener.Enabled` |
| Status-bar extras | EditorPrefs key `Essentials.Editor.StatusBarExtrasHider.Enabled` |

The package rename did not change these keys, public namespaces, assembly names,
or existing Unity asset GUIDs.

## Third-Party Binary

The Package Manager assembly explicitly references Harmony `2.4.1` from
`ThirdParty/Essentials.MinimalEditor.Harmony.dll`. It is package-owned and
Editor-only; no shared Harmony UPM dependency is required. See
[Third Party Notices](../Third%20Party%20Notices.md).
