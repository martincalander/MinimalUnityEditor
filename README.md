# Minimal Editor

`com.essentials.minimaleditor` makes the Unity Editor quieter, cleaner, and less distracting. It groups small, opinionated editor-surface refinements without taking ownership of project content, import workflows, runtime systems, or IDE tooling.

## Features

- **Console** — filters unwanted log entries, cleans extension-owned stack frames, and can hide the Unity AI Assistant Console button when that optional integration is installed. Configure it under **Preferences > Console**.
- **Menu Item** — hides project-selected menu items and optional menu bundles. Configure it under **Project Settings > Essentials > Menu Item**; settings are stored in `ProjectSettings/MenuItemSettings.json`.
- **Package Manager (Unity 6+)** — hides duplicate Asset Store rows when a project-local package is already installed, adds **Go To Local Package**, and provides **Window > Package Management > Re-resolve All Packages**.
- **Editor chrome** — uses short project window titles and hides status-bar animation and memory statistics. Configure both under **Preferences > Essentials > Unity Editor**.
- **FMOD menu** — when `com.fmod.fmod-for-unity-2-03` is installed, moves FMOD's root menu entries below **Tools > FMOD**.

The package intentionally does not own HID scaffolding, Preset Manager workflows, Rider integration, Animator tooling, Aseprite or Pixel Perfect processing, Hierarchy scene auto-save, or the default Player-icon toolbar element. Those remain with their domain package or `com.essentials.extensions`.

## Installation

Install the private repository as an embedded package:

```bash
git submodule add -b main \
  https://github.com/EssentialsForUnity/com.essentials.minimaleditor.git \
  Packages/com.essentials.minimaleditor
```

Or add the authenticated Git dependency to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.essentials.minimaleditor": "https://github.com/EssentialsForUnity/com.essentials.minimaleditor.git"
  }
}
```

Installing `com.essentials.extensions` also installs the exact compatible Minimal Editor version.

## Compatibility

The extracted modules retain their existing assembly identities:

- `Essentials.Console.Editor`
- `Essentials.MenuItem.Editor`
- `Essentials.PackageManager.Editor`
- `Essentials.FMOD.Editor`
- `Essentials.Editor`

The public `EditorFeatureToggles`, `ConsoleHyperlinks`, `ConsoleLogFilters`, and `MenuItemFeatureToggles` APIs keep their existing namespaces and assembly-qualified names. Existing Editor preferences and project settings also keep their original keys and paths.

The package baseline is Unity 2021.3. Package Manager cleanup is enabled only on Unity 6 or newer because it integrates with Unity 6 Package Manager internals; the other modules remain available on the declared baseline when their own optional integrations are satisfied.

Define `ESSENTIALS_DISABLE_MINIMAL_EDITOR` to disable all Minimal Editor feature and test asmdefs while leaving the package installed. The Harmony binary remains importable so `com.essentials.extensions` can keep its retained Preset Manager integration active. The compatibility symbols `ESSENTIALS_DISABLE_ALL`, `ESSENTIALS_DISABLE_CONSOLE`, `ESSENTIALS_DISABLE_MENU_ITEM`, `ESSENTIALS_DISABLE_PACKAGE_MANAGER`, `ESSENTIALS_DISABLE_FMOD`, and `ESSENTIALS_DISABLE_UNITY_EDITOR` remain supported for granular control.

## Optional integrations

FMOD support is enabled only when `com.fmod.fmod-for-unity-2-03` is installed. Other optional menu bundles keep their existing package and module version gates; they are not hard dependencies.

## Third-party software

The package contains Harmony for its Package Manager patches. `com.essentials.extensions` also consumes that single binary from this package for its retained Preset Manager integration. See [Third Party Notices.md](Third%20Party%20Notices.md).

## License

This package is proprietary. See [LICENSE.md](LICENSE.md) for the applicable terms.
