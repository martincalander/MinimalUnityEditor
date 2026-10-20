# Compatibility and Scripting

## Unity Compatibility

| Module | Availability |
| --- | --- |
| Console | Declared package baseline: Unity `2021.3.0f1` |
| Menu Item | Declared baseline; disabled on Unity 6.0 before `6000.0.43f1` |
| Unity Editor chrome | Declared package baseline |
| Package Manager | Unity 6 or newer |
| Project Folders | Unity 7 with the CoreCLR Editor runtime |
| FMOD | Only when `com.fmod.fmod-for-unity-2-03` is installed |

The manifest minimum is not a record of validation against every Unity release.
Several refinements use guarded reflection against Editor internals. When a
required member is absent, the individual refinement remains inactive.

The short window title uses the public callback on Unity 2023.2 and newer.
Earlier supported Editors use the equivalent internal callback through
reflection.

Console stack-trace cleanup is available at the declared baseline. Unity's
additional `HideInCallstack` attribute is used only on Unity 2022.2 and newer.

### Play Mode lifecycle

Domain reload can be disabled, with scene reload either on or off. Editor
preferences, reflection caches, hyperlink registrations, and installed patches
persist across Play sessions. They are Editor-session resources, so the package
does not clear them on each Play entry.

On Unity 6.5 and newer, `OnCodeInitializing` and `OnCodeUnloading` pair service
registration with cleanup. The Console also uses `OnEnteringPlayMode` and
`OnEnteringEditMode` to check its logger lifecycle. If another extension takes
over the log handler, Minimal Editor keeps that ownership intact and continues
filtering through Console rescans.
Older Editors use the corresponding initialization and reload events.
Registration is idempotent, and cleanup removes owned callbacks and Harmony
patches before code unloads.

See Unity's [code lifecycle guidance](https://docs.unity.com/en-us/engine/7000.0/manual/programming-environment/code-reload-serialization/code-reload/code-lifecycle).

On 2026-10-10, all 111 package tests passed in Unity `7000.0.0a8` on macOS,
with no failures or skips. This includes three Play sessions with scene reload
on and three with it off, checking that code does not reload, callbacks and
Harmony postfixes remain registered once, and Editor settings stay intact.
FMOD was not installed, so its optional assembly was not compiled. The older
Editor results below predate these lifecycle changes.

### Verified Editors

On 2026-10-20, all 199 Minimal Editor tests passed in Unity `7000.0.0a10` on
macOS Apple silicon, with no failures or skips. This includes the Console
snapshot and logger regressions, canceled-quit handling, menu settings recovery,
package-location checks, status-bar recovery, and repeated Play sessions.
A live Console probe confirmed that search and severity filters restrict the
enumerated rows while total counts remain unchanged; incomplete rescans now
leave those logs intact.

The optional FMOD source passed an isolated harness with 14 checks under each
of the modern and legacy lifecycle configurations. The harness uses Editor
stubs and fake FMOD types. FMOD was not installed in this workspace, so real
integration remains unverified. Older Editors were not rerun for this pass.

EditMode tests passed on macOS Apple silicon on 2026-10-08:

| Editor | Passed | Failed / skipped |
| --- | --- | --- |
| `2021.3.10f1` | 53 | 0 / 0 |
| `2022.3.62f3` | 53 | 0 / 0 |
| `6000.0.68f1` | 101 | 0 / 0 |
| `6000.5.10f1` | 101 | 0 / 0 |
| `6000.7.0b2` | 101 | 0 / 0 |
| `7000.0.0a8` | 101 | 0 / 0 |

The Unity 6+ runs include real Harmony patch registration and the selected
Mono/CoreCLR assembly. Earlier Editors exclude the Package Manager module.
Tests also exercise the actual window-title callback and package asset imports.
These results do not establish coverage for every patch release or host OS.
The recorded runs predate the Project Folders integration.

Both Harmony importers retain serialization version 2 so older Editors can
read their platform settings. Preserve that format and their GUIDs when
changing plugin settings, then validate in both an older Editor and Unity 7.

## Assembly Identities

The package retains these Editor assembly names:

- `Essentials.Console.Editor`
- `Essentials.MenuItem.Editor`
- `Essentials.PackageManager.Editor`
- `Essentials.ProjectFolderVisibility.Editor`
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
| `ESSENTIALS_DISABLE_PROJECT_FOLDER_VISIBILITY` | Disables Project Folders |
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
| Project Folders | EditorPrefs key `com.essentials.projectfoldervisibility.settings.` plus the hash of `Application.dataPath` |

The package rename did not change these keys, public namespaces, assembly names,
or existing Unity asset GUIDs. The Project Folders integration also retains its
previous assembly names, source GUIDs, and project-specific preference key.
Earlier Addressables visibility stored under
`com.essentials.projectfoldervisibility.show.` plus the same project hash is
still read when no newer folder rule overrides it.

Remove `com.essentials.projectfoldervisibility` when upgrading a project to the
integrated module. Keeping both packages would duplicate assembly identities,
source GUIDs, and Project window hooks. The test assembly retains the name
`Essentials.ProjectFolderVisibility.Editor.Tests` and now belongs to Minimal
Editor. Projects that explicitly list the old package in `testables` should
replace that entry with `com.martincalander.minimaleditor`.

## Third-Party Binary

The Package Manager and Project Folders assemblies share package-owned,
Editor-only Harmony builds:

- Unity 6 uses the existing Harmony `2.4.1` Mono build in
  `ThirdParty/Essentials.MinimalEditor.Harmony.dll`.
- Unity 7 and newer use Harmony `2.4.2` for CoreCLR in
  `ThirdParty/CoreCLR/Essentials.MinimalEditor.Harmony.dll`.

Complementary importer constraints select one build. The Package Manager module
and its tests remain disabled before Unity 6. Project Folders and its tests
require both Unity 7 or newer and `ENABLE_CORECLR`. No additional Harmony binary
is included for Project Folders. The Package Manager list refresh
supports both the legacy parameterless rebuild and the newer `Rebuild(bool)`
entry point. No shared Harmony UPM dependency is required. See
[Third Party Notices](../Third%20Party%20Notices.md).
