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

A pattern takes effect when you press Return or leave the field, so a partly
typed pattern never removes Console rows.

**Warnings Only** is enabled by default. Turn it off when rules should also
apply to other Console message types. **Clean Stack Traces** removes the
filter's own helper frames from displayed stacks.

When active rules exist, the package revisits changed Console rows while the
Editor is open, skipping unchanged snapshots. A newly written matching row can
therefore appear briefly before it is removed. Rescans of a very large Console
wait longer between passes, so its matching rows can take a few seconds to
disappear. Invalid regular expressions are reported beside the rule
and are not applied. A regex that exceeds its 50 ms matching limit stops running
until the settings are saved again; the affected messages remain visible.

If another extension replaces Unity's log handler, Minimal Editor leaves that
handler in control. Filtering continues through Console rescans. A rescan that
cannot read every row leaves the Console unchanged. Rescans also pause when
Console search, severity toggles, or collapsed duplicates conceal rows. Clear
the search, show all severities, and turn off Collapse to resume them.

If Unity AI Assistant is installed and its Console integration is detected,
**Hide Assistant Toolbar Button** removes the **Add to Assistant** button. The
toggle is disabled when that optional integration is not present.

Console settings are stored in the user-global Editor preference key
`MinimalEditor.Console.HiddenLogFilters`. They carry across Unity projects opened
by the same operating-system user.

## Menu Item

Open **Preferences > Minimal Editor > Menu Item**.

The module starts enabled. Nearly every available built-in bundle also starts
enabled; **D3D12/Vulkan** and **Services** are the intentional exceptions. The
**Services** bundle is listed on macOS only and hides the Services heading in
the menu bar, including its sign-in commands. Unavailable optional
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
- Prefix a full line with `#` or `//` to make it a comment, including any
  semicolon-separated examples on that line.
- Matching surrounding single or double quotes are ignored. Incomplete or
  mismatched surrounding quotes leave that definition inactive.
- Matching is case-insensitive under standard roots such as `Tools` and
  `Window`. For a custom root menu, preserve the root's registered spelling so
  Unity can enumerate its commands; matching within that root is case-insensitive.
- A trailing `*` matches everything below a path prefix.
- Edits apply when you leave the text field, open another settings page, or
  select **Apply Now**. A partly typed path never removes a parent menu.

For example:

```text
# Hide one exact command
Assets/Import Package/Custom Package...

# Hide one submenu
Window/Analysis/*
```

On macOS, a whole-root rule such as `Jobs/*` also hides the native menu-bar
heading without needing to hover over it. Minimal Editor reapplies that
visibility when Unity recreates or unhides the heading. Turning off the rule
restores the heading; removed commands still return after a normal domain reload
or Editor reopen. This changes visibility after registration and does not
prevent Unity from discovering the commands during startup.

Unity does not expose every menu path safely. Bare root paths such as `Jobs`
are ignored, and known error-prone entries are reported in the settings page.
Unity regenerates Services commands, so they are never removed. On macOS,
`Services/*` or the **Services** bundle hides the Services heading in the menu
bar. Other `Services/` entries, and `Services/*` on other platforms, are
ignored. Use `Jobs/*` to hide the whole Jobs menu. Partial rules such as
`Tools/My Tool/*` do not hide the Tools heading.

Menu choices are user-global and apply to every project you open. They are
saved as `MinimalEditor/MenuItemSettings.json` in the Unity preferences folder
instead of `EditorPrefs`, so several open Editors do not overwrite each other's
changes. If that file does not exist yet, Minimal Editor copies the project's
older `ProjectSettings/MenuItemSettings.json` once. After that, the project file
is no longer read and can be deleted.

## Unity Editor Chrome

Open **Preferences > Minimal Editor > Unity Editor**.

- **Short Window Title** uses `PlayerSettings.productName` as the main Editor
  window title.
- **Hide Status Bar Extras** hides Unity's animation and memory statistics.
- **Hide Debugger Button** hides the debug mode button from the status bar.
  It starts disabled.

The first two options start enabled when no earlier preference exists. Their
user-global `EditorPrefs` values carry across Unity projects. If a Unity
release changes the internal status-bar contract, that refinement does not
apply.
Turning off **Hide Status Bar Extras** restores only bars changed by Minimal
Editor, preserving extras that Unity had already hidden.

The **Inspector Header** section hides buttons from the right side of each
Inspector header. Use either option alone or both together:

- **Hide Help Button** hides the **?** button that opens the reference page.
- **Hide Preset Button** hides the preset button.

Both options start disabled. Turning one off puts its button back in Unity's
usual place. If a Unity release removes or renames a button, its option is
shown disabled and that button is left alone.

The **Inspector Footer** section has two options:

- **Hide Asset Labels** hides the Asset Labels and AssetBundle fields that
  Unity shows at the bottom of every asset Inspector. Asset previews stay
  visible.
- **Hide Asset PostProcessors** hides the Asset PostProcessors list that
  Unity shows at the bottom of importer Inspectors. It starts disabled.

**Hide Asset Labels** starts enabled when the Addressables package is
installed, because Addressables replaces AssetBundle names, and disabled
otherwise. A choice made here is stored and wins over that default.

These options and **Hide Debugger Button** use the bundled Harmony and patch
the Editor only while they are enabled. If a Unity release changes the
internal method an option relies on, that option is shown disabled and its
element is left alone.

The **Automated Editor** section has one option, for Unity 7 and newer:

- **Quit Automated Editor** lets a person quit an Editor that was launched
  with the `-automated` argument, as command-line tools and coding agents
  often do.

Unity cancels the Save Scene prompt in such an Editor because nobody is
expected to answer it. When an untitled scene has changes, the cancelled prompt
also cancels the quit, so Command+Q and the window close button do nothing.
With this option, a quit request saves every changed scene that already has a
file, saves an open prefab that has changes, saves assets, and exits. Changes
in a scene that was never saved to a file are dropped, with a warning in the
Console. If something cannot be saved, the quit is cancelled and the Console
lists it.

The option starts enabled. It uses the bundled Harmony and patches Unity's
quit request only in an Editor launched with `-automated`. It does nothing in
an Editor launched without that argument or in batch mode. It is shown disabled
on Editors older than Unity 7, and when a Unity release changes the internal
quit contract.

## Project Folders

This module requires Unity 7 with the CoreCLR Editor runtime. Open
**Preferences > Minimal Editor > Project Folders**.

A checked folder is hidden while **Hide selected folders** is enabled. The
built-in choices are:

| Folder | Initial visibility |
| --- | --- |
| `Assets/AddressableAssetsData` | Hidden |
| `Assets/StreamingAssets` | Visible |
| `Assets/Plugins` | Visible |
| `Assets/Gizmos` | Visible |
| `Assets/Settings` | Visible |

Use **Add folder** to include another folder inside Assets. Custom folders can
be removed from the list; built-in entries stay available for toggling.

The Project window toolbar has a folder visibility toggle and a dropdown for
individual folders and Preferences. Turn the toggle off to reveal every folder
without losing your choices. The same toggle is available under
**Tools > Minimal Editor > Project Folders > Hide Selected Folders**.

Filtering covers the folder tree, single-column layout, list/grid view, and
Project-window search results. Changes refresh every open Project window. If a
newly hidden folder is open, that browser returns to Assets. A hidden folder
that does not exist in the project costs nothing; filtering starts once the
folder is created.

Files remain in place and stay available to imports, builds, AssetDatabase
queries, and object pickers. Addressables does not need to be installed.
Preferences are saved locally for this user and project and survive Editor
restarts. When upgrading from the standalone Project Folder Visibility package,
configure your earlier choices again using the current Minimal Editor settings.

## Folder Icons

A folder in the Project window shows an image as its icon when the image sits
directly inside that folder and its file name, without the extension, ends in
`Icon`. Matching ignores case. `Characters/CharactersIcon.png`,
`Audio/Icon.jpg`, and `UI/menuicon.psd` all qualify. `IconSet.png` and
`Iconic.png` do not, and an image in a subfolder does not apply to the folders
above it. Any file that Unity imports as a `Texture2D` can be used, including
package folders under `Packages/`.

When a folder contains several matching images, the first rule that matches
wins:

1. a file named exactly `Icon`;
2. a file named after the folder followed by `Icon`, such as `AudioIcon`;
3. the first remaining file in ordinal path order.

The image file itself still appears in the folder. Adding, renaming, moving, or
deleting an image updates the icon on the next repaint without restarting the
Editor.

Open **Preferences > Minimal Editor > Folder Icons** to turn the feature off
or to choose an **Icon Style**:

- **Full Size**, the default, replaces the folder icon with the image.
- **Badge** keeps Unity's folder icon and draws the image small in its
  bottom-right corner.

The feature starts enabled when no earlier preference exists. Both choices use
user-global `EditorPrefs` values. Define `MINIMAL_EDITOR_DISABLE_FOLDER_ICONS`
to compile the module out.

The module uses Unity's public Project window item callback and does not patch
the Project window. In **Full Size**, it draws the image over Unity's folder
icon after covering that icon with the row's background color. **Badge** draws
over the folder icon without covering it, so the limits below about background
colors do not apply to it. Known limits:

- In **Full Size**, transparent images can show a small square in a different shade where the
  background color is not matched. This can happen while a row is hovered, in
  the light skin, or on Unity versions before Unity 7, whose colors have not
  been sampled.
- In **Full Size** and the two-column layout, the folder tree highlights the
  folder shown on the right. When that highlight was set without selecting
  anything, for example right after the Editor starts, the tree row is drawn
  without the highlight behind a transparent image.
- In **Full Size**, grid tiles draw the image at the full tile width, slightly
  larger than the native folder icon.

## Package Manager

This module compiles only on Unity 6 or newer.

On Package Manager's **In Project** page, an Asset Store-backed row is hidden
when its matching read-only UPM package already resolves outside the project's
`Packages/` folder. A matching embedded package or submodule keeps its Asset
Store row; that row's details toolbar receives **Go To Local Package** instead.

**Window > Package Management > Re-resolve All Packages** deletes
`Packages/packages-lock.json` when present and asks Unity Package Manager to
resolve the project again, including projects with lockfiles disabled.
There is no confirmation dialog. Commit or otherwise preserve
intentional lockfile changes before using it, then review the regenerated file.

The Package Manager integration patches exact Unity internals when a Package
Manager window is first open or focused after a code load, not at every code
load. If those internals differ, the affected convenience is left inactive
rather than changing unrelated Package Manager pages.

## FMOD

When `com.fmod.fmod-for-unity-2-03` is installed, equivalent FMOD commands are
registered under **Tools > FMOD** and the original root entries are removed on
a best-effort basis. If a matching FMOD API is unavailable, its original command
stays in place and the replacement is disabled or absent.

There is no FMOD settings page in Minimal Editor.

## Scripting

The package keeps small public Editor APIs for projects that need to configure
the same behavior from code:

- `MinimalEditor.Console.Editor.ConsoleLogFilters`;
- `MinimalEditor.Console.Editor.ConsoleHyperlinks`;
- `MinimalEditor.MenuItem.Editor.MenuItemFeatureToggles` for menu hiding,
  bundles, custom definitions, and path inputs;
- `MinimalEditor.Editor.EditorFeatureToggles` for the short title and status-bar
  extras.

Folder Icons has no public scripting API.

Production asmdefs are not auto-referenced. Add the matching `MinimalEditor.*`
assembly reference to the consuming Editor asmdef. Programmatic path and
delegate inputs are session state and must be registered again after a domain
reload. See [Compatibility and scripting](compatibility.md) for an overview.
