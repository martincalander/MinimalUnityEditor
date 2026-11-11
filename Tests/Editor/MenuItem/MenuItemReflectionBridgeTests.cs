using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.MenuItem
{
    public sealed class MenuItemReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type Hider => Type.GetType(
            "MinimalEditor.MenuItem.Editor.MenuItemHider, MinimalEditor.MenuItem.Editor", throwOnError: true);

        [SetUp, TearDown]
        public void ResetMenuItemHiderState()
        {
            Invoke("SuppressMutationForTeardown");
            SetBool("s_failSafeMode", false);
            SetBool("s_isApplying", false);
            Invoke("Initialize");
            EditorApplication.delayCall -= CreateDelayCall("FlushQueuedApply");
            SetBool("s_applyQueued", false);
            SetBool("s_forceApplyQueued", false);
        }

        [TestCase("Tools/Vendor", "Tools/*", true)]
        [TestCase("TOOLS/Vendor", " Tools/* ", true)]
        [TestCase("Tool", "Tools/*", false)]
        [TestCase("Tools", "*", false)]
        [TestCase("Tools/Ö😀", "tools/ö*", true)]
        [TestCase("Tools/Vendor", "Tools", false)]
        public void MenuPatterns_PreserveOrdinalPrefixAndLengthRules(string input, string pattern, bool expected)
        {
            var type = Type.GetType("MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher, MinimalEditor.MenuItem.Editor", true);
            Assert.That(type.GetMethod("IsMatch", AnyStatic).Invoke(null, new object[] { input, pattern }), Is.EqualTo(expected));
        }

        [Test]
        public void MenuItemSettingsProvider_IsUserScoped()
        {
            var type = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPreferencesProvider, MinimalEditor.MenuItem.Editor", throwOnError: true);
            var provider = (SettingsProvider)RequireMethod(type, "CreateProvider").Invoke(null, null);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Minimal Editor/Menu Item"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));

            var settings = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemModuleSettings, MinimalEditor.MenuItem.Editor", true);
            var storagePath = (string)settings.GetProperty("StoragePath", AnyStatic).GetValue(null);
            Assert.That(System.IO.Path.IsPathRooted(storagePath), Is.True);
            Assert.That(storagePath, Does.StartWith(UnityEditorInternal.InternalEditorUtility.unityPreferencesFolder));
        }

        [Test]
        public void CancelledQuitRequest_KeepsMenuHidingActive()
        {
            // Invoke only this module's handlers and a veto, without requesting a real Editor quit.
            var callbacks = QuitRequestCallbacks().Where(callback => callback.Method.DeclaringType == Hider)
                .Cast<Func<bool>>().Concat(new Func<bool>[] { () => false });
            Assert.That(callbacks.All(callback => callback()), Is.False);
            Assert.That(GetBool("s_mutationSuppressed"), Is.False);

            Invoke("QueueApply", true);

            Assert.That(GetBool("s_applyQueued"), Is.True);
        }

        [Test]
        public void SettingsData_NullBundleEntriesAndList_PreserveUsableSettings()
        {
            var type = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemSettingsData, MinimalEditor.MenuItem.Editor", throwOnError: true);
            var data = Activator.CreateInstance(type, true);
            JsonUtility.FromJsonOverwrite(
                "{\"customDefinitionsText\":\"keep\",\"bundles\":[{\"id\":\"jobs\",\"enabled\":false}]}", data);
            var bundles = type.GetField("bundles", AnyInstance);
            var get = type.GetMethod("GetBundleEnabled", AnyInstance);
            var set = type.GetMethod("SetBundleEnabled", AnyInstance);
            ((IList)bundles.GetValue(data)).Insert(0, null);

            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.False);
            set.Invoke(data, new object[] { "jobs", true });
            set.Invoke(data, new object[] { "tools", false });
            Assert.That(get.Invoke(data, new object[] { "jobs", false }), Is.True);
            Assert.That(get.Invoke(data, new object[] { "tools", true }), Is.False);

            bundles.SetValue(data, null);
            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.True);
            set.Invoke(data, new object[] { "jobs", false });
            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.False);
            Assert.That(type.GetField("customDefinitionsText", AnyInstance).GetValue(data), Is.EqualTo("keep"));
        }

        [Test]
        public void RepeatedInitialization_QueuesOnce_AndTeardownPreventsFurtherQueueing()
        {
            var callback = CreateDelayCall("FlushQueuedApply");
            Invoke("Initialize");
            Invoke("Initialize");
            Assert.That(EditorApplication.delayCall.GetInvocationList().Count(item => item.Equals(callback)), Is.EqualTo(1));
            Assert.That(GetBool("s_applyQueued"), Is.True);
            Assert.That(GetBool("s_forceApplyQueued"), Is.True);

            Invoke("SuppressMutationForTeardown");
            Invoke("SuppressMutationForTeardown");
            Assert.That(EditorApplication.delayCall?.GetInvocationList().Count(item => item.Equals(callback)) ?? 0, Is.Zero);
            Assert.That(GetBool("s_mutationSuppressed"), Is.True);
            Assert.That(GetBool("s_applyQueued"), Is.False);
            Assert.That(GetBool("s_forceApplyQueued"), Is.False);

            Invoke("QueueApply", true);
            Assert.That(GetBool("s_applyQueued"), Is.False);
            Assert.That(GetBool("s_forceApplyQueued"), Is.False);
        }

        [Test]
        public void MenuRemovalAndEnumeration_HaveExpectedSignatures()
        {
            Assert.That(RequireMethod(typeof(Menu), "RemoveMenuItem", typeof(string)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(RequireMethod(typeof(Menu), "ExtractSubmenus", typeof(string)).ReturnType, Is.EqualTo(typeof(string[])));
            var getItems = RequireMethod(typeof(Menu), "GetMenuItems", typeof(string), typeof(bool), typeof(bool));
            Assert.That(getItems.ReturnType.IsArray, Is.True);
            var path = getItems.ReturnType.GetElementType().GetProperty("path", AnyInstance);
            Assert.That(path, Is.Not.Null, "Menu items must expose their path.");
            Assert.That(path.PropertyType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void NativeMenuRules_MatchCaseInsensitivelyWithoutRemovingUnrelatedCommands()
        {
            var add = RequireMethod(typeof(Menu), "AddMenuItem", typeof(string), typeof(string), typeof(bool),
                typeof(int), typeof(Action), typeof(Func<bool>));
            var exists = RequireMethod(typeof(Menu), "MenuItemExists", typeof(string));
            var remove = RequireMethod(typeof(Menu), "RemoveMenuItem", typeof(string));
            var prefix = "Tools/MinimalEditorCaseTest" + Guid.NewGuid().ToString("N");
            var target = prefix + "/Command";
            var unrelated = prefix + "Sibling/Keep";
            var activeInput = RequireField("s_activeInput").GetValue(null);
            var pathsInput = RequireField("s_menuItemsInput").GetValue(null);
            try
            {
                RequireField("s_activeInput").SetValue(null, new Func<bool>(() => true));
                RequireField("s_menuItemsInput").SetValue(null, null);
                add.Invoke(null, new object[] { unrelated, "", false, 9999, (Action)(() => { }), (Func<bool>)(() => true) });
                foreach (var rule in new[] { target, target.ToLowerInvariant(), prefix.ToLowerInvariant(), prefix.ToLowerInvariant() + "/*" })
                {
                    add.Invoke(null, new object[] { target, "", false, 9999, (Action)(() => { }), (Func<bool>)(() => true) });
                    Assert.That(exists.Invoke(null, new object[] { target }), Is.True);
                    WithManualRules(_ =>
                    {
                        SetMenuItems(new[] { rule });
                        SetBool("s_mutationSuppressed", false);
                        Invoke("ApplyCore", true);
                        Invoke("SuppressMutationForTeardown");
                    });
                    Assert.That(exists.Invoke(null, new object[] { target }), Is.False, rule);
                    Assert.That(exists.Invoke(null, new object[] { unrelated }), Is.True, rule);
                }
            }
            finally
            {
                remove.Invoke(null, new object[] { prefix });
                remove.Invoke(null, new object[] { prefix + "Sibling" });
                RequireField("s_activeInput").SetValue(null, activeInput);
                RequireField("s_menuItemsInput").SetValue(null, pathsInput);
            }
        }

        [Test]
        public void MenuRootScan_ExactRules_DoNotEnumerateMenus()
        {
            Assert.That(MenuRoots("Assets/Create/Material", "Custom/Command", "Jobs"), Is.Empty);
        }

        [Test]
        public void MenuRootScan_OnlyIncludesRootsNeededByWildcards()
        {
            Assert.That(MenuRoots("Assets/Create/Material", "Window/Analysis/*", " window/UI* ", "Custom/Tools/*"),
                Is.EquivalentTo(new[] { "Window", "Custom" }));
        }

        [Test]
        public void MenuRootScan_RootPrefixesPreserveKnownCustomRoots()
        {
            Assert.That(MenuRoots("Tool*", "Custom/Command", "Custom*", "*", null, ""),
                Is.EquivalentTo(new[] { "Tools", "Custom" }));
        }

        [TestCase("GameObject/Center On Children")]
        [TestCase(" GameObject/Center On Children/ ")]
        [TestCase("GameObject/Center On Children/*")]
        [TestCase("GameObject/Center On Children//*")]
        [TestCase("GameObject/Center On*")]
        [TestCase("GameObject/*")]
        public void ProtectedCommand_CannotBeHiddenThroughEquivalentRules(string rule)
        {
            var matcher = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher, MinimalEditor.MenuItem.Editor", true);
            var paths = (IEnumerable<string>)RequireMethod(matcher, "SanitizeEntries", typeof(IEnumerable<string>))
                .Invoke(null, new object[] { new[] { rule, "Window/Analysis/*" } });

            Assert.That(paths, Is.EqualTo(new[] { "Window/Analysis/*" }));
        }

        [Test]
        public void ServicesRootRule_ReachesNativeRootsOnly()
        {
            var matcher = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher, MinimalEditor.MenuItem.Editor", true);
            var paths = (IReadOnlyList<string>)RequireMethod(matcher, "SanitizeEntries", typeof(IEnumerable<string>))
                .Invoke(null, new object[]
                {
                    new[] { "Services", " services/* ", "Services/Explore", "Services/Vivox/*", "Window/Analysis/*" }
                });

            Assert.That(paths, Is.EqualTo(new[] { "services/*", "Window/Analysis/*" }));

            var removable = (IEnumerable<string>)RequireMethod(
                    matcher, "ExcludeNativeRootOnlyEntries", typeof(IReadOnlyList<string>))
                .Invoke(null, new object[] { paths });

            Assert.That(removable, Is.EqualTo(new[] { "Window/Analysis/*" }));
        }

        [Test]
        public void ServicesBundle_IsMacOSOnly_AndOffByDefault()
        {
            var catalog = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemBundleCatalog, MinimalEditor.MenuItem.Editor", true);
            var bundle = RequireMethod(catalog, "Find", typeof(string)).Invoke(null, new object[] { "services" });
#if UNITY_EDITOR_OSX
            Assert.That(bundle, Is.Not.Null);
            var type = bundle.GetType();
            Assert.That(type.GetProperty("DefaultEnabled", AnyInstance).GetValue(bundle), Is.False);
            Assert.That(type.GetProperty("IsAvailable", AnyInstance).GetValue(bundle), Is.True);
            Assert.That((IEnumerable<string>)type.GetProperty("MenuPaths", AnyInstance).GetValue(bundle),
                Is.EqualTo(new[] { "Services/*" }));
#else
            Assert.That(bundle, Is.Null);
#endif
        }

        [Test]
        public void ManualRules_CanReuseCurrentList()
        {
            WithManualRules(list =>
            {
                SetMenuItems(list);
                Assert.That(list, Is.EqualTo(new[] { "Tools/Keep" }));
            });
        }

        [Test]
        public void ManualRules_FailedEnumerationPreservesPreviousConfiguration()
        {
            WithManualRules(list =>
            {
                SetBool("s_useManualMenuItems", false);
                var exception = Assert.Throws<TargetInvocationException>(() => SetMenuItems(ThrowingPaths()));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(list, Is.EqualTo(new[] { "Tools/Keep" }));
                Assert.That(GetBool("s_useManualMenuItems"), Is.False);
            });
        }

        [Test]
        public void CustomDefinitions_CommentsCannotActivateSemicolonSeparatedExamples()
        {
            Assert.That(ParseDefinitions("  # Examples; Tools/*; Jobs/*\r\n // Disabled; Window/*\nTools/Keep; 'Window/Analysis/*'"),
                Is.EqualTo(new[] { "Tools/Keep", "Window/Analysis/*" }));
        }

        [TestCase("'Tools/*\"")]
        [TestCase("\"Tools/*'")]
        [TestCase("'Tools/*")]
        [TestCase("Tools/*\"")]
        [TestCase("'")]
        [TestCase("\"\"")]
        public void CustomDefinitions_IncompleteOrMismatchedQuotesDoNotHideMenus(string definition)
        {
            Assert.That(ParseDefinitions(definition + "\nTools/Keep"), Is.EqualTo(new[] { "Tools/Keep" }));
        }

        static IEnumerable<string> ParseDefinitions(string text)
        {
            var settings = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemModuleSettings, MinimalEditor.MenuItem.Editor", true);
            return (IEnumerable<string>)RequireMethod(settings, "ParseDefinitionEntries", typeof(string))
                .Invoke(null, new object[] { text });
        }

        [Test]
        public void CustomDefinitionCache_RefreshesWhenSettingsTextChanges()
        {
            var settings = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemModuleSettings, MinimalEditor.MenuItem.Editor", true);
            var dataField = settings.GetField("s_data", AnyStatic);
            var originalData = dataField.GetValue(null);
            var data = Activator.CreateInstance(dataField.FieldType, true);
            var text = dataField.FieldType.GetField("customDefinitionsText", AnyInstance);
            var getEntries = RequireMethod(settings, "GetCustomDefinitionEntries");
            var warningKind = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemBlockedEntryKind, MinimalEditor.MenuItem.Editor", true);
            var getWarnings = RequireMethod(settings, "GetCustomDefinitionWarnings", warningKind);
            try
            {
                dataField.SetValue(null, data);
                text.SetValue(data, "Services/*\n# ignored\n'Window/Analysis/*'");
                var entries = getEntries.Invoke(null, null);
                Assert.That((IEnumerable<string>)entries, Is.EqualTo(new[] { "Services/*", "Window/Analysis/*" }));
                Assert.That(getEntries.Invoke(null, null), Is.SameAs(entries));
                var warnings = getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") });
                Assert.That(((IEnumerable<string>)warnings).Count(), Is.EqualTo(1));
                Assert.That(getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") }), Is.SameAs(warnings));

                text.SetValue(data, "Tools/Keep");
                Assert.That((IEnumerable<string>)getEntries.Invoke(null, null), Is.EqualTo(new[] { "Tools/Keep" }));
                Assert.That((IEnumerable<string>)getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") }),
                    Is.Empty);
            }
            finally
            {
                dataField.SetValue(null, originalData);
            }
        }

        [TestCase("Vendor/* => Tools/Vendor", "Vendor", "Tools/Vendor")]
        [TestCase("  Vendor => Tools/Vendor  ", "Vendor", "Tools/Vendor")]
        [TestCase("Vendor/* => Tools/", "Vendor", "Tools/Vendor")]
        [TestCase("Cave Expedition/* => Tools/", "Cave Expedition", "Tools/Cave Expedition")]
        [TestCase("Vendor/Audio/* => Tools/", "Vendor/Audio", "Tools/Audio")]
        [TestCase("Vendor/Open Settings => Tools/Vendor/", "Vendor/Open Settings", "Tools/Vendor/Open Settings")]
        [TestCase("Vendor/Help/ => Help/Vendor", "Vendor/Help", "Help/Vendor")]
        [TestCase("Vendor/Open Settings => Tools/Vendor Settings", "Vendor/Open Settings", "Tools/Vendor Settings")]
        public void MoveRules_ParseSourceAndDestination(string entry, string source, string destination)
        {
            var arguments = new object[] { entry, null, null };
            Assert.That(RequireMoveRuleMethod("TryParse").Invoke(null, arguments), Is.True);
            Assert.That(arguments[2], Is.Null);
            var type = arguments[1].GetType();
            Assert.That(type.GetProperty("Source", AnyInstance).GetValue(arguments[1]), Is.EqualTo(source));
            Assert.That(type.GetProperty("Destination", AnyInstance).GetValue(arguments[1]), Is.EqualTo(destination));
        }

        [TestCase("Vendor/* =>")]
        [TestCase("=> Tools/Vendor")]
        [TestCase("Vend* => Tools/Vendor")]
        [TestCase("Vendor/* => Tools/*")]
        [TestCase("Vendor/* => Tools => Other")]
        [TestCase("Vendor/* => Vendor/Old")]
        [TestCase("Vendor/* => vendor")]
        [TestCase("GameObject/Vendor/* => Tools/Vendor")]
        [TestCase("Assets/Vendor => Tools/Vendor")]
        [TestCase("CONTEXT/Transform/Reset => Tools/Reset")]
        [TestCase("Services/* => Tools/Services")]
        [TestCase("Vendor/* => Assets/Vendor")]
        [TestCase("Vendor/* => /Tools")]
        public void MoveRules_RejectUnsafeOrMalformedEntries(string entry)
        {
            var arguments = new object[] { entry, null, null };
            Assert.That(RequireMoveRuleMethod("TryParse").Invoke(null, arguments), Is.False);
            Assert.That(arguments[2], Is.Not.Null.And.Not.Empty);

            var matcher = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher, MinimalEditor.MenuItem.Editor", true);
            var paths = (IEnumerable<string>)RequireMethod(matcher, "SanitizeEntries", typeof(IEnumerable<string>))
                .Invoke(null, new object[] { new[] { entry, "Vendor/* => Tools/Vendor", "Window/Analysis/*" } });
            Assert.That(paths, Is.EqualTo(new[] { "Vendor/* => Tools/Vendor", "Window/Analysis/*" }));
        }

        [TestCase("Vendor/Help/About", "Tools/Vendor/Help/About")]
        [TestCase("vendor/Open", "Tools/Vendor/Open")]
        [TestCase("Vendor", "Tools/Vendor")]
        [TestCase("Vendors/Open", null)]
        [TestCase("Other/Vendor/Open", null)]
        public void MoveRules_MapOnlyCommandsBelowTheSource(string path, string expected)
        {
            var arguments = new object[] { "Vendor/* => Tools/Vendor", null, null };
            RequireMoveRuleMethod("TryParse").Invoke(null, arguments);
            var map = arguments[1].GetType().GetMethod("Map", AnyInstance);
            Assert.That(map.Invoke(arguments[1], new object[] { path }), Is.EqualTo(expected));
        }

        [Test]
        public void MoveRules_AreSplitFromHideRules()
        {
            var arguments = new object[] { new[] { "Jobs/*", "Vendor/* => Tools/Vendor", "Window/Analysis/*" }, null };
            var hidden = RequireMoveRuleMethod("Split").Invoke(null, arguments);
            Assert.That((IEnumerable<string>)hidden, Is.EqualTo(new[] { "Jobs/*", "Window/Analysis/*" }));
            Assert.That(((ICollection)arguments[1]).Count, Is.EqualTo(1));

            var hideOnly = new[] { "Jobs/*" };
            arguments = new object[] { hideOnly, null };
            Assert.That(RequireMoveRuleMethod("Split").Invoke(null, arguments), Is.SameAs(hideOnly));
            Assert.That(arguments[1], Is.Null);
        }

        [TestCase("Window/General/Console %#c", "Window/General/Console", "%#c")]
        [TestCase("Window/General/Undo History  %u", "Window/General/Undo History", "%u")]
        [TestCase("Tools/Run _F5", "Tools/Run", "_F5")]
        [TestCase("Tools/Open Settings", "Tools/Open Settings", "")]
        public void MenuItemNames_SplitIntoPathAndHotkey(string name, string path, string hotkey)
        {
            var arguments = new object[] { name, null, null };
            Hider.GetMethod("SplitMenuItemName", AnyStatic).Invoke(null, arguments);
            Assert.That(arguments[1], Is.EqualTo(path));
            Assert.That(arguments[2], Is.EqualTo(hotkey));
        }

        [TestCase("fmod", "FMOD/* => Tools/FMOD")]
        [TestCase("steam_audio", "Steam Audio/* => Tools/Steam Audio")]
        public void MoveBundles_AreAlwaysAvailable_AndOnByDefault(string id, string rule)
        {
            var catalog = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemBundleCatalog, MinimalEditor.MenuItem.Editor", true);
            var bundle = RequireMethod(catalog, "Find", typeof(string)).Invoke(null, new object[] { id });
            Assert.That(bundle, Is.Not.Null);
            var type = bundle.GetType();
            Assert.That(type.GetProperty("DefaultEnabled", AnyInstance).GetValue(bundle), Is.True);
            Assert.That(type.GetProperty("IsAvailable", AnyInstance).GetValue(bundle), Is.True);
            Assert.That((IEnumerable<string>)type.GetProperty("MenuPaths", AnyInstance).GetValue(bundle),
                Is.EqualTo(new[] { rule }));
        }

        [Test]
        public void MovedCommands_RunFromTheNewPath_AndReturnWhenTheRuleIsRemoved()
        {
            const string destinationRoot = "Tools/Minimal Editor Moved";
            const string movedCommand = destinationRoot + "/Group/Command";
            const string movedGuarded = destinationRoot + "/Guarded";
            Assert.That(MenuExists(MenuItemMoveTestCommands.Root), Is.False,
                "The fixture heading must stay out of the menu bar outside this test.");

            WithMoveFixture(() =>
            {
                AddPlaceholder(MenuItemMoveTestCommands.Command, 3);
                AddPlaceholder(MenuItemMoveTestCommands.Guarded, 40);
                AddPlaceholder(MenuItemMoveTestCommands.Root + "/Unregistered", 50);

                ApplyRules(MenuItemMoveTestCommands.Root + "/* => " + destinationRoot);
                Assert.That(MenuExists(movedCommand), Is.True);
                Assert.That(MenuExists(movedGuarded), Is.True);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Command), Is.False);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Root + "/Group"), Is.False);
                // A command without a MenuItem method cannot be called from a new path, so it stays.
                Assert.That(MenuExists(MenuItemMoveTestCommands.Root + "/Unregistered"), Is.True);
                Assert.That(MenuExists(destinationRoot + "/Unregistered"), Is.False);

                Assert.That(EditorApplication.ExecuteMenuItem(movedCommand), Is.True);
                Assert.That(MenuItemMoveTestCommands.Executed, Is.EqualTo(1));
                Assert.That(Menu.GetEnabled(movedGuarded), Is.False);
                MenuItemMoveTestCommands.GuardedEnabled = true;
                Assert.That(Menu.GetEnabled(movedGuarded), Is.True);
                Assert.That(EditorApplication.ExecuteMenuItem(movedGuarded), Is.True);
                Assert.That(MenuItemMoveTestCommands.Executed, Is.EqualTo(11));

                // Unity can register the original again. The next pass removes it without a second copy.
                AddPlaceholder(MenuItemMoveTestCommands.Command, 3);
                ApplyRules(MenuItemMoveTestCommands.Root + "/* => " + destinationRoot);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Command), Is.False);
                Assert.That(MenuPaths("Tools").Count(path => path == movedCommand), Is.EqualTo(1));

                RemoveMenu(MenuItemMoveTestCommands.Root + "/Unregistered");
                ApplyRules();
                Assert.That(MenuExists(destinationRoot), Is.False);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Command), Is.True);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Guarded), Is.True);
                Assert.That(EditorApplication.ExecuteMenuItem(MenuItemMoveTestCommands.Command), Is.True);
                Assert.That(MenuItemMoveTestCommands.Executed, Is.EqualTo(12));
            }, destinationRoot);
        }

        [Test]
        public void MovedCommands_EmptySourceHeadingIsRemoved_AndRestoredOnDeactivation()
        {
            const string destinationRoot = "Tools/Minimal Editor Moved";
            WithMoveFixture(() =>
            {
                AddPlaceholder(MenuItemMoveTestCommands.Command, 3);
                AddPlaceholder(MenuItemMoveTestCommands.Guarded, 40);

                ApplyRules(MenuItemMoveTestCommands.Root + " => " + destinationRoot);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Root), Is.False);
                Assert.That(MenuExists(destinationRoot + "/Group/Command"), Is.True);
                Assert.That(MenuExists("Tools"), Is.True);

                RequireField("s_activeInput").SetValue(null, new Func<bool>(() => false));
                SetBool("s_mutationSuppressed", false);
                Invoke("ApplyCore", true);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Command), Is.True);
                Assert.That(MenuExists(destinationRoot), Is.False);
            }, destinationRoot);
        }

        [Test]
        public void MovedCommands_NeverReplaceAnExistingCommand()
        {
            const string destinationRoot = "Tools/Minimal Editor Moved";
            WithMoveFixture(() =>
            {
                AddPlaceholder(MenuItemMoveTestCommands.Command, 3);
                AddPlaceholder(destinationRoot + "/Group/Command", 3);

                ApplyRules(MenuItemMoveTestCommands.Root + "/* => " + destinationRoot);
                Assert.That(MenuExists(MenuItemMoveTestCommands.Command), Is.True);
                Assert.That(EditorApplication.ExecuteMenuItem(destinationRoot + "/Group/Command"), Is.True);
                Assert.That(MenuItemMoveTestCommands.Executed, Is.Zero);
            }, destinationRoot);
        }

        static void WithMoveFixture(Action test, string destinationRoot)
        {
            var activeInput = RequireField("s_activeInput").GetValue(null);
            var pathsInput = RequireField("s_menuItemsInput").GetValue(null);
            MenuItemMoveTestCommands.Executed = 0;
            MenuItemMoveTestCommands.GuardedEnabled = false;
            try
            {
                RequireField("s_activeInput").SetValue(null, new Func<bool>(() => true));
                RequireField("s_menuItemsInput").SetValue(null, null);
                WithManualRules(_ => test());
            }
            finally
            {
                ((IDictionary)RequireField("s_movedItems").GetValue(null)).Clear();
                ((IDictionary)RequireField("s_moveDestinationOwners").GetValue(null)).Clear();
                RemoveMenu(destinationRoot);
                RemoveMenu(MenuItemMoveTestCommands.Root);
                RequireField("s_activeInput").SetValue(null, activeInput);
                RequireField("s_menuItemsInput").SetValue(null, pathsInput);
            }
        }

        static void ApplyRules(params string[] rules)
        {
            SetMenuItems(rules);
            SetBool("s_mutationSuppressed", false);
            Invoke("ApplyCore", true);
            Invoke("SuppressMutationForTeardown");
        }

        static void AddPlaceholder(string path, int priority) =>
            RequireMethod(typeof(Menu), "AddMenuItem", typeof(string), typeof(string), typeof(bool), typeof(int),
                    typeof(Action), typeof(Func<bool>))
                .Invoke(null, new object[] { path, "", false, priority, (Action)(() => { }), (Func<bool>)(() => true) });

        static bool MenuExists(string path) =>
            (bool)RequireMethod(typeof(Menu), "MenuItemExists", typeof(string)).Invoke(null, new object[] { path });

        static void RemoveMenu(string path) =>
            RequireMethod(typeof(Menu), "RemoveMenuItem", typeof(string)).Invoke(null, new object[] { path });

        static IEnumerable<string> MenuPaths(string root)
        {
            var items = (Array)RequireMethod(typeof(Menu), "GetMenuItems", typeof(string), typeof(bool), typeof(bool))
                .Invoke(null, new object[] { root, false, false });
            return items.Cast<object>().Select(item => (string)item.GetType().GetProperty("path", AnyInstance).GetValue(item));
        }

        static MethodInfo RequireMoveRuleMethod(string name)
        {
            var method = Type.GetType("MinimalEditor.MenuItem.Editor.MenuItemMoveRules, MinimalEditor.MenuItem.Editor", true)
                .GetMethod(name, AnyStatic);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

#if UNITY_EDITOR_OSX
        [Test]
        public void NativeRootPolling_OnlySubscribesOnceWhileWholeRootRulesExist()
        {
            var configure = RequireMethod(Hider, "ConfigureNativeRootVisibility", typeof(IReadOnlyList<string>));
            var update = typeof(EditorApplication).GetField("update", AnyStatic);
            Assert.That(update, Is.Not.Null);
            int SubscriptionCount() => (update.GetValue(null) as Delegate)?.GetInvocationList().Count(
                callback => callback.Method.DeclaringType == Hider &&
                            callback.Method.Name == "UpdateNativeRootVisibility") ?? 0;

            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            Assert.That(SubscriptionCount(), Is.EqualTo(1));

            configure.Invoke(null, new object[] { new[] { "Tools/Exact Command" } });
            Assert.That(SubscriptionCount(), Is.Zero);

            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            Invoke("SuppressMutationForTeardown");
            Assert.That(SubscriptionCount(), Is.Zero);
        }
#endif

        static IEnumerable<string> MenuRoots(params string[] rules) =>
            (IEnumerable<string>)RequireMethod(Hider, "BuildMenuRootSet", typeof(IReadOnlyList<string>))
                .Invoke(null, new object[] { rules });

        static void SetMenuItems(IEnumerable<string> items) =>
            RequireMethod(Hider, "SetMenuItems", typeof(IEnumerable<string>)).Invoke(null, new object[] { items });

        static IEnumerable<string> ThrowingPaths()
        {
            yield return "Tools/Partial";
            throw new InvalidOperationException("Incomplete input");
        }

        static void WithManualRules(Action<List<string>> assertion)
        {
            var list = (List<string>)RequireField("s_manualMenuItemsToHide").GetValue(null);
            var originalItems = list.ToArray();
            var originalManualState = GetBool("s_useManualMenuItems");
            Invoke("SuppressMutationForTeardown");
            try
            {
                SetMenuItems(new[] { "Tools/Keep" });
                assertion(list);
            }
            finally
            {
                list.Clear();
                list.AddRange(originalItems);
                SetBool("s_useManualMenuItems", originalManualState);
            }
        }

        static bool GetBool(string name) => (bool)RequireField(name).GetValue(null);
        static void SetBool(string name, bool value) => RequireField(name).SetValue(null, value);

        static IEnumerable<Delegate> QuitRequestCallbacks()
        {
            var field = typeof(EditorApplication).GetField("wantsToQuit", AnyStatic);
            if (field != null)
                return (field.GetValue(null) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();

            var tracker = typeof(EditorApplication).GetField("m_WantsToQuitEvent", AnyStatic)?.GetValue(null);
            Assert.That(tracker, Is.Not.Null);
            var enumerator = (IEnumerator)tracker.GetType().GetMethod("GetEnumerator").Invoke(tracker, null);
            var callbacks = new List<Delegate>();
            try
            {
                while (enumerator.MoveNext()) callbacks.Add((Delegate)enumerator.Current);
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
            return callbacks;
        }

        static void Invoke(string name, params object[] arguments)
        {
            var method = RequireMethod(Hider, name, arguments.Select(value => value.GetType()).ToArray());
            method.Invoke(null, arguments);
        }

        static EditorApplication.CallbackFunction CreateDelayCall(string name) =>
            (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), RequireMethod(Hider, name));

        static FieldInfo RequireField(string name)
        {
            var field = Hider.GetField(name, AnyStatic);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        static MethodInfo RequireMethod(Type type, string name, params Type[] arguments)
        {
            var method = type.GetMethod(name, AnyStatic, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            return method;
        }
    }

    // Commands the move tests look up by their MenuItem attributes. The heading is removed as
    // soon as code loads, so it only exists in the menu bar while a test adds it back.
    static partial class MenuItemMoveTestCommands
    {
        internal const string Root = "Minimal Editor Move Test";
        internal const string Command = Root + "/Group/Command";
        internal const string Guarded = Root + "/Guarded";

        internal static int Executed;
        internal static bool GuardedEnabled;

        [global::UnityEditor.MenuItem(Command, false, 3)]
        static void Execute() => Executed++;

        [global::UnityEditor.MenuItem(Guarded, false, 40)]
        static void ExecuteGuarded(MenuCommand command) => Executed += 10;

        [global::UnityEditor.MenuItem(Guarded, true)]
        static bool ValidateGuarded() => GuardedEnabled;

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void HideHeadingOutsideTests()
        {
            EditorApplication.delayCall -= RemoveHeading;
            EditorApplication.delayCall += RemoveHeading;
        }

        static void RemoveHeading()
        {
            typeof(Menu).GetMethod("RemoveMenuItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null)?.Invoke(null, new object[] { Root });
        }
    }
}
