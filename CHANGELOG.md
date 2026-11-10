# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

- Keep semicolon-separated examples inside menu comment lines inactive, and
  ignore definitions with incomplete or mismatched surrounding quotes.
- Resolve differently cased menu commands and submenus under standard roots
  before calling Unity's case-sensitive removal API.
- Preserve status-bar extras that Unity had already hidden when the hider is
  turned off, tracking owned changes separately for each bar across code reloads.
- Keep Asset Store rows visible when a package location is a relative path.
- Ignore unknown Console rule types and forward exceptions whose custom
  formatter throws to the original logger.
- Preserve Console rows when a change notification invalidates a scan even
  when the total row count stays the same.

- Apply Console rule patterns and custom menu definitions only after editing
  ends. Partly typed text no longer removes Console rows or whole menus.

- Restore **Go To Local Package** in Unity 7's separate Package Manager toolbar,
  keeping the button beside the native asset action in the current theme.
- Let **Go To Local Package** find a local package whose folder name differs
  from its package name when Package Manager cannot select it.

- Re-resolve packages even when the project has no package lockfile.
- Clean helper frames from Console entries that store message and stack text
  together, preserving Unicode message offsets.
- Apply logger configuration atomically when logs arrive from worker threads,
  and tolerate incompatible optional Console toolbar integrations.
- Protect blocked menu commands from wildcard and trailing-slash variants, and
  preserve manual menu rules when a replacement input cannot be enumerated.
- Keep FMOD originals when no usable replacement exists, and cancel queued
  menu mutations during code teardown.
- Keep menu hiding active when a quit request is canceled, and tolerate missing
  or null bundle entries in saved menu settings.
- Keep ordinary Console logs and errors when Warnings Only is enabled, bound
  regex matching time, and preserve Console rows when a rescan cannot read them.
- Avoid logger wrapper cycles when another extension replaces the log handler.
- Restore hidden status-bar extras after Unity resets or recreates the bar.
- Leave Unity's status-bar extras setting alone while the hider is off, only
  restoring extras that Minimal Editor hid earlier in the session.
- Keep Package Manager entries visible when their package location is unknown.
- Keep original FMOD commands when their replacement API cannot be called.

### Added

- On macOS, whole-root menu rules such as `Jobs/*` hide the menu-bar heading
  without hovering and keep it hidden when Unity recreates it. Native visibility
  is restored when the rule is disabled, without forcing Unity to rebuild menus.

- Integrated Project Folder Visibility into Minimal Editor on Unity 7 CoreCLR,
  preserving its asset GUIDs.
- Added built-in visibility toggles for `Assets/Plugins`, `Assets/Gizmos`, and
  `Assets/Settings`, alongside Addressables data, StreamingAssets, and custom
  folders. The three new built-ins start visible.
- Added **Preferences > Minimal Editor > Project Folders** and moved the menu
  toggle under **Tools > Minimal Editor > Project Folders**.

### Changed

- Skip Project Folders filtering while no hidden folder exists in the project,
  including the default Addressables rule, and remember results already found
  outside hidden folders until settings or project contents change. Unity runs
  this filter on every Project window GUI event; a 1,000-result pass fell from
  about 0.95 ms and 525 KB of allocation to under 0.03 ms with none.
- Add the Project Folders filter hooks only once a hidden rule points at an
  existing folder. The Project window toolbar control is still installed at
  every code load.
- Read every menu root that hiding rules need once, before removing any command.
  A repeated apply after a project change fell from 6.7 ms to 1.7 ms in the
  measured project. The same menu items are removed.
- Space out Console rescans by their cost. A rescan now waits at least 20 times
  as long as the previous one took. In the measured 10,000-row stack-cleanup
  case, rescanning a changing Console uses about 5 percent of main-thread time
  instead of about 37 percent.
- Skip the full Console scan at code load when no filter rule is active.
- Apply the Package Manager patches when a Package Manager window is open
  instead of at every code load. See the
  [2026-11-10 follow-up](Documentation~/performance.md#2026-11-10-follow-up)
  for measured load costs.
- Reduce Console scan allocations with cached field accessors, shallow snapshot
  copies, and stack cleanup that avoids temporary lines and substrings. Preserve
  metadata, Unicode offsets, and reflection fallbacks.
- Reuse repeated imported package names during Asset Store duplicate filtering
  and avoid allocating menu wildcard prefixes.
- Add reproducible performance benchmarks and a [measured audit](Documentation~/performance.md).

- Skip reading and copying unchanged Console rows while filters are active.
  Unity's log-change notification, row counts, and settings changes invalidate
  the saved scan; incomplete scans remain eligible for retry.

- Avoid copying unchanged Console entries and formatting logs excluded by the
  active severity setting. Removed the unused clean-stack logger subclass and
  manual rescan entry point.
- Cache Console regex validation and parsed menu definitions, skip unchanged
  settings writes, and scan only menu roots needed by wildcard rules.
- Poll hidden macOS menu headings at most five times per second, with no
  polling callback when no whole-root rule is active.
- Avoid per-asset path-prefix allocations and unchanged result-list copies
  during Project folder filtering; skip filtering when no folder is hidden.
- Standardized assembly names, namespaces, preference keys, settings pages,
  patch identifiers, and asset filenames on Minimal Editor. Existing asset
  GUIDs are preserved. Consumers must update assembly references and configure
  earlier local preferences again.
- Use `MINIMAL_EDITOR_DISABLE_ALL` to disable the package, with
  `MINIMAL_EDITOR_DISABLE_*` symbols for individual modules.
- Project Folders uses the existing Minimal Editor Harmony binary and follows
  the package-wide disable symbol, with its own
  `MINIMAL_EDITOR_DISABLE_PROJECT_FOLDER_VISIBILITY` opt-out.

- Adopted guarded code lifecycle callbacks with explicit event and Harmony
  cleanup, idempotent initialization, and logger lifecycle checks when
  entering Play or Edit Mode without a domain reload.
- Added regression coverage for repeated Play sessions with domain reload
  disabled and scene reload either enabled or disabled.

- Added a Unity 7 CoreCLR build of Harmony for the Package Manager integration,
  while retaining the Mono build for earlier Editors.
- Preserved Editor services across Play Mode transitions with explicit static
  lifecycle policies and updated loaded-assembly discovery for newer Editors.
- Updated Package Manager compatibility checks for Unity 7's imported-assets
  collection and retained patch failure details for troubleshooting.
- Restored immediate Package Manager list refresh on older Unity 6 Editors by
  supporting their legacy rebuild entry point.
- Restored short window titles on Unity 2021.3 and 2022.3 by binding their
  internal title callback without referencing its inaccessible descriptor type.
- Restored Unity 2021.3 compilation by no longer using the Console call-stack
  hiding attribute, which Unity added in 2022.2.
- Saved both Harmony importers in the older plugin format so Unity 2021.3 and
  2022.3 can read the Editor-only platform settings.
- Moved the repository to Martin Calander's personal GitHub account and adopted
  the package ID `com.martincalander.minimaleditor`.
- Renamed the GitHub repository from `com.martincalander.minimaleditor` to
  `MinimalUnityEditor` without changing the package ID or installation path.
- Vendored a uniquely named, package-owned Harmony binary for the Package Manager patches with Editor-only, explicit-reference importer settings.
- Updated the Package Manager assembly to reference `MinimalEditor.Harmony.dll`
  and removed the shared Harmony package dependency.
- Added focused user, compatibility, and troubleshooting guides, including the
  default menu behavior, menu-restoration caveat, and exact Unity 6 Package
  Manager duplicate rules.

### Removed

- **Breaking:** Removed the duplicate menu API from
  `MinimalEditor.Editor.EditorFeatureToggles` (`HideMenuItems`,
  `SetMenuItemPaths`, `SetMenuItemPathsInput`, and `SetMenuItemsInput`) and the
  `MenuItemFeatureToggles.HideMenuItems` alias. Use
  `MenuItemFeatureToggles.ModuleEnabled`, `SetMenuItemPaths`,
  `SetMenuItemPathsInput`, and `SetHideInput` instead.
- `MinimalEditor.Editor` no longer references `MinimalEditor.MenuItem.Editor`.
  `MINIMAL_EDITOR_DISABLE_MENU_ITEM` therefore no longer disables the short
  title and status-bar features.
- Removed unused internal helpers and test-only overloads.

## [0.1.0] - 2026-08-22

### Added

- Extracted the Console, Menu Item, Package Manager, optional FMOD menu, and
  Editor chrome modules into a standalone package.
- Included public editor APIs, settings, and stable Unity asset GUIDs.
- Added package-wide and module-specific compile opt-outs.
- Retained the Harmony integration used by the Package Manager module.

### Changed

- Limited Package Manager cleanup to Unity 6 or newer, where its reflected Package Manager API exists, and no longer reports success when no Harmony target was patched.
