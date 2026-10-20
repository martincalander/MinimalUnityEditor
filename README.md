# Minimal Editor

A quieter Unity Editor without a new workflow to learn.

<p align="center">
  <img alt="UPM minimum 2021.3.0f1" src="https://img.shields.io/badge/UPM%20minimum-2021.3.0f1-222C37?logo=unity&logoColor=white">
  <img alt="Editor-only package" src="https://img.shields.io/badge/scope-Editor%20only-555">
</p>

> [!WARNING]
> **Under development.** This package has not been publicly released yet and
> may change without notice.

Minimal Editor collects the small editor adjustments that tend to be copied
from project to project: a calmer Console, shorter window titles, less crowded
menus, folder visibility controls, and a few Package Manager conveniences. It
stays out of scenes, assets, builds, and runtime code.

## What It Changes

| Area | What it controls | Location |
| --- | --- | --- |
| Console | Filters, stack-trace cleanup, Assistant button | **Preferences > Console** |
| Menu Item | Built-in bundles and custom menu paths | **Project Settings > Minimal Editor > Menu Item** |
| Unity Editor | Short title and status-bar extras | **Preferences > Minimal Editor > Unity Editor** |
| Project Folders | Built-in and custom folder visibility | **Preferences > Minimal Editor > Project Folders** |
| Package Manager | Unity 6 **In Project** duplicate filtering and **Go To Local Package** | No settings page |
| FMOD | Automatic menu relocation when FMOD is installed | No settings page |

Console and Unity Editor choices use user-global `EditorPrefs` and carry across
Unity projects opened by the same operating-system user. Menu Item settings are
shared with the project in `ProjectSettings/MenuItemSettings.json`. Project
Folders choices are local to the current user and project.

## Requirements

| Component | Requirement |
| --- | --- |
| Unity | Package manifest minimum: `2021.3.0f1` |
| Package Manager refinements | Unity 6 or newer |
| Project Folders | Unity 7 with the CoreCLR Editor runtime |
| Menu hiding on Unity 6.0 | `6000.0.43f1` or newer |
| Runtime footprint | None; every assembly is Editor-only |

The manifest minimum is an eligibility floor, not a claim that every intervening
Unity release has been validated. Reflection-backed features stop applying when
their required Editor internals are unavailable.

## Installation

Both installation methods require Git.

Add it as an editable submodule:

```bash
git submodule add -b main \
  https://github.com/martincalander/MinimalUnityEditor.git \
  Packages/com.martincalander.minimaleditor
```

Or add it as a read-only Git dependency in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.martincalander.minimaleditor": "https://github.com/martincalander/MinimalUnityEditor.git"
  }
}
```

The Git dependency follows the mutable `main` branch. A committed submodule
gitlink already pins the exact package commit checked out in the parent project.

## Quick Start

1. Install the package and let Unity finish its domain reload.
2. Open **Project Settings > Minimal Editor > Menu Item**. Most available menu
   bundles are enabled by default, so keep the ones that suit the project and
   turn off the rest. A removed menu entry returns after the next domain reload
   or Editor reopen.
3. Open **Preferences > Console** and add a rule. Filtering defaults to enabled
   when no earlier preference exists, but the default rule list is empty.
4. Open **Preferences > Minimal Editor > Unity Editor** to adjust the title and
   status-bar options. Both default to enabled when no earlier preference
   exists.
5. On Unity 7, open **Preferences > Minimal Editor > Project Folders** to hide
   `Assets/AddressableAssetsData`, `Assets/StreamingAssets`, `Assets/Plugins`,
   `Assets/Gizmos`, `Assets/Settings`, or custom folders. Addressables data starts
   hidden; the other built-in folders start visible.

The Package Manager and FMOD refinements activate automatically when their
version and package requirements are met.

## Compatibility and Opt-Outs

Use `MINIMAL_EDITOR_DISABLE_ALL` to disable every feature and test
asmdef without removing the package files. Module symbols provide
narrower control, with one important dependency:
`MINIMAL_EDITOR_DISABLE_MENU_ITEM` also disables the Editor chrome assembly because
that assembly references the Menu Item module.

Assembly names, namespaces, and public editor APIs use `MinimalEditor.*`.
Update consuming code, assembly references, and scripting symbols when upgrading
from an earlier package identity. Preferences use the package's current keys,
so earlier local preferences need to be configured again. Unity asset GUIDs
are preserved. See
[Compatibility](Documentation~/compatibility.md) for the assembly, symbol, and
API overview.

## Documentation

- [Start here](Documentation~/index.md)
- [User guide](Documentation~/user-guide.md)
- [Compatibility and scripting](Documentation~/compatibility.md)
- [Troubleshooting](Documentation~/troubleshooting.md)
- [Changelog](CHANGELOG.md)

## Third-Party Software

The Package Manager and Project Folders modules share the package-owned,
Editor-only Harmony builds selected for the current Editor runtime. See
[Third Party Notices](Third%20Party%20Notices.md).

## License

This package is proprietary. See [LICENSE.md](LICENSE.md) for the applicable
terms.
