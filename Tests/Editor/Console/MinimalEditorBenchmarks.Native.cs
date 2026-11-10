using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MinimalEditor.Console.Editor;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.Console
{
    public static partial class MinimalEditorBenchmarks
    {
        // Only use in a disposable project: this deliberately replaces its Console contents.
        public static string RunNativeConsole(string outputPath, bool allowClearingConsole = false)
        {
            if (!allowClearingConsole || Application.isBatchMode)
                throw new InvalidOperationException("Native Console benchmarks require an isolated visible Editor and explicit allowClearingConsole.");
            var report = NewReport();
            var active = Installer.GetField("s_activeFilteringLogHandler", Static);
            var clean = Installer.GetField("s_cleanStackTracesEnabled", Static);
            var originalHandler = active.GetValue(null);
            var originalClean = clean.GetValue(null);
            var originalWarningStack = Application.GetStackTraceLogType(LogType.Warning);
            var entries = (ConsoleLogFilterInstaller.ConsoleEntryAccess)Installer.GetField("s_consoleEntryAccess", Static).GetValue(null);
            var logEntries = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries", true);
            var flags = logEntries.GetProperty("consoleFlags", Static);
            var originalFlags = (int)flags.GetValue(null);
            var flagType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow+ConsoleFlags", true);
            int Flag(string name) => Convert.ToInt32(Enum.Parse(flagType, name));
            try
            {
                flags.SetValue(null, (originalFlags | Flag("LogLevelLog") | Flag("LogLevelWarning") | Flag("LogLevelError")) & ~Flag("Collapse"));
                var handlerType = Installer.GetNestedType("FilterLogHandler", BindingFlags.NonPublic);
                var handler = Activator.CreateInstance(handlerType, true);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
                var rules = new List<ConsoleLogFilterRuleData> { new ConsoleLogFilterRuleData { pattern = "hidden-warning-0:" } };
                var compiled = Installer.GetMethod("BuildCompiledRules", Static).Invoke(null, new object[] { rules });
                handlerType.GetMethod("Configure").Invoke(handler, new[] { (object)true, true, compiled });
                active.SetValue(null, handler);
                clean.SetValue(null, true);
                foreach (var count in new[] { 100, 1000, 10000 })
                {
                    foreach (var scenario in new[] { "changed-no-match", "stack-cleanup", "all-hidden" })
                    {
                        var row = new MemoryEntries(1, scenario == "all-hidden" ? "hidden-warning-0: value" : "ordinary warning text", scenario == "stack-cleanup").Read(0);
                        void Prepare()
                        {
                            entries.Clear();
                            entries.Invalidate();
                            for (var i = 0; i < count; i++)
                            {
                                // AddMessage does not preserve stack offsets. Capture a real
                                // logger stack so this case measures actual cleanup work.
                                if (scenario == "stack-cleanup") ConsoleLogFilterInstallerBenchmarkSource.LogWarning();
                                else entries.Add(row);
                            }
                            if (entries.GetTotalCount() != count) throw new InvalidOperationException("Unexpected native Console population.");
                            if (scenario == "stack-cleanup" && !FirstMessageHasHelper())
                                throw new InvalidOperationException("Native Console did not capture the helper frame for cleanup.");
                        }
                        void Scan()
                        {
                            if (!ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, true, out _, out var message))
                                throw new InvalidOperationException(message);
                        }
                        bool FirstMessageHasHelper()
                        {
                            entries.BeginRead();
                            try
                            {
                                var entry = entries.Read(0);
                                var text = (string)entry.GetType().GetField("message", Instance).GetValue(entry);
                                return text.Contains("MinimalEditor.Console.Editor.ConsoleLogFilterInstaller");
                            }
                            finally { entries.EndRead(); }
                        }
                        // Native population and result verification are outside the timed region.
                        Prepare();
                        Scan();
                        var samples = new double[9];
                        var allocations = new double[9];
                        var watch = new Stopwatch();
                        for (var sample = 0; sample < samples.Length; sample++)
                        {
                            Prepare();
                            var before = GC.GetAllocatedBytesForCurrentThread();
                            watch.Restart();
                            Scan();
                            watch.Stop();
                            allocations[sample] = GC.GetAllocatedBytesForCurrentThread() - before;
                            samples[sample] = watch.Elapsed.TotalMilliseconds * 1000;
                            if (entries.GetTotalCount() != (scenario == "all-hidden" ? 0 : count))
                                throw new InvalidOperationException("Native scan returned an unexpected row count.");
                            if (scenario == "stack-cleanup" && FirstMessageHasHelper())
                                throw new InvalidOperationException("Native scan did not clean the helper frame.");
                        }
                        var sorted = samples.OrderBy(v => v).ToArray();
                        report.measurements.Add(new Measurement
                        {
                            name = "native-console/" + count + "/" + scenario, iterations = 1,
                            medianMicroseconds = sorted[4], minMicroseconds = sorted[0], maxMicroseconds = sorted[8],
                            samplesMicroseconds = samples, bytesPerOperation = allocations.OrderBy(v => v).ElementAt(4)
                        });
                    }
                }
            }
            finally
            {
                entries.Clear();
                entries.Invalidate();
                active.SetValue(null, originalHandler);
                clean.SetValue(null, originalClean);
                Application.SetStackTraceLogType(LogType.Warning, originalWarningStack);
                flags.SetValue(null, originalFlags);
            }
            return SaveReport(report, outputPath);
        }

        public static string RunAdditional(string outputPath)
        {
            var report = NewReport();
            var folder = Find("MinimalEditor.ProjectFolderVisibility.Editor.FolderVisibilitySettings");
            if (folder != null)
            {
                var settingsField = folder.GetField("s_Settings", Static);
                var original = settingsField.GetValue(null);
                var ruleType = folder.Assembly.GetType("MinimalEditor.ProjectFolderVisibility.Editor.FolderVisibilityRule");
                var filter = Bind<Func<string, bool>>(folder, "IsHiddenPath");
                try
                {
                    foreach (var count in new[] { 5, 50, 500 })
                    {
                        var settings = Activator.CreateInstance(settingsField.FieldType, true);
                        settingsField.FieldType.GetField("enabled", Instance).SetValue(settings, true);
                        var folders = (IList)settingsField.FieldType.GetField("folders", Instance).GetValue(settings);
                        for (var i = 0; i < count; i++)
                            folders.Add(Activator.CreateInstance(ruleType, Instance, null, new object[] { "Assets/BenchmarkFolder" + i, true }, null));
                        settingsField.SetValue(null, settings);
                        var last = "Assets/BenchmarkFolder" + (count - 1) + "/Child.asset";
                        Measure(report, "folders/" + count + "-rules/miss", () => { if (filter("Assets/Other/Child.asset")) throw new Exception(); });
                        Measure(report, "folders/" + count + "-rules/last-hit", () => { if (!filter(last)) throw new Exception(); });
                    }
                }
                finally { settingsField.SetValue(null, original); }
            }
            var guid = AssetDatabase.AssetPathToGUID("Assets");
            Measure(report, "folders/native-guid-to-path", () => sink = AssetDatabase.GUIDToAssetPath(guid));
            Measure(report, "folders/native-is-valid-folder", () => sink = AssetDatabase.IsValidFolder("Assets/MinimalEditorBenchmarkMissing"));
            var folderFilter = Find("MinimalEditor.ProjectFolderVisibility.Editor.AddressablesFolderVisibility");
            if (folder != null && folderFilter != null)
                BenchmarkProjectListPasses(report, folder, folderFilter);
            var hider = Find("MinimalEditor.MenuItem.Editor.MenuItemHider");
            if (hider != null)
            {
                var applyCore = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), hider.GetMethod("ApplyCore", Static));
                // Applies the project's current menu settings. After the first pass every exact rule misses.
                Measure(report, "menus/apply/current-settings", () => applyCore(true), 1);
            }
            var status = Find("MinimalEditor.Editor.StatusBarExtrasHider");
            if (status != null) Measure(report, "status-bar/active-apply", Bind<Action>(status, "Apply"));
#if UNITY_2023_2_OR_NEWER
            var title = Find("MinimalEditor.Editor.WindowTitleShortener");
            if (title != null)
            {
                var callback = Bind<Action<ApplicationTitleDescriptor>>(title, "OnUpdateMainWindowTitle");
                var descriptor = new ApplicationTitleDescriptor("Benchmark", Application.unityVersion, "", "", false);
                Measure(report, "title/update-callback", () => callback(descriptor));
            }
#endif
#if UNITY_EDITOR_OSX
            var fixtureType = Find("MinimalEditor.Tests.Editor.MenuItem.MacOSMenuVisibilityTests");
            if (fixtureType != null)
            {
                var fixture = Activator.CreateInstance(fixtureType);
                try
                {
                    fixtureType.GetMethod("SetUp").Invoke(fixture, null);
                    for (var i = 0; i < 12; i++)
                        fixtureType.GetMethod("AddRoot", Instance).Invoke(fixture, new object[] { "Benchmark" + i, false });
                    fixtureType.GetMethod("Configure", Instance).Invoke(fixture, new object[] { new[] { "Benchmark1/*", "Benchmark5/*" } });
                    var visibility = fixtureType.GetField("visibility", Instance).GetValue(fixture);
                    var menu = (IntPtr)fixtureType.GetField("menu", Instance).GetValue(fixture);
                    var apply = (Action<IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr>), visibility, visibility.GetType().GetMethod("ApplyToMenu", Instance));
                    Measure(report, "menus/native-12-roots", () => apply(menu));
                }
                finally { fixtureType.GetMethod("TearDown").Invoke(fixture, null); }
            }
#endif
            sink = null;
            return SaveReport(report, outputPath);
        }

        // Measures the Project window list prefix on 1,000 results, as Unity calls it on every GUI event.
        static void BenchmarkProjectListPasses(Report report, Type settingsType, Type visibilityType)
        {
            if (!(bool)visibilityType.GetProperty("IsPatched", Static).GetValue(null))
                return;
            var filterList = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), visibilityType.GetMethod("FilterList", Static));
            var browserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser", true);
            var settingsField = settingsType.GetField("s_Settings", Static);
            var ruleType = settingsType.Assembly.GetType("MinimalEditor.ProjectFolderVisibility.Editor.FolderVisibilityRule");
            var original = settingsField.GetValue(null);
            const string existingFolder = "Assets/MinimalEditorBenchmarkHidden";
            var createdFolder = !AssetDatabase.IsValidFolder(existingFolder) &&
                !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "MinimalEditorBenchmarkHidden"));
            EditorWindow browser = null;
            void UseRule(string path, bool hidden)
            {
                var settings = Activator.CreateInstance(settingsField.FieldType, true);
                settingsField.FieldType.GetField("enabled", Instance).SetValue(settings, true);
                var folders = (IList)settingsField.FieldType.GetField("folders", Instance).GetValue(settings);
                folders.Add(Activator.CreateInstance(ruleType, Instance, null, new object[] { path, hidden }, null));
                settingsField.SetValue(null, settings);
            }
            try
            {
                // The filter hooks are added once a hidden rule points at an existing folder. Add them before
                // creating the benchmark browser, because the refresh rebuilds every Project browser's views.
                UseRule(existingFolder, true);
                visibilityType.GetMethod("RefreshProjectWindows", Static).Invoke(null, null);
                if (!(bool)visibilityType.GetProperty("FiltersPatched", Static).GetValue(null))
                    throw new InvalidOperationException("The Project Folders filter hooks were not added.");
                browser = (EditorWindow)ScriptableObject.CreateInstance(browserType);
                browser.position = new Rect(100, 100, 640, 480);
                Call(browser, "Init");
                Call(browser, "InitViewMode", Enum.Parse(browserType.GetNestedType("ViewMode", BindingFlags.NonPublic), "TwoColumns"));
                var group = Member(Member(browser, "m_ListArea"), "m_LocalAssets");
                var hierarchy = Member(group, "m_FilteredHierarchy");
                var resultsField = hierarchy.GetType().GetField("m_Results", Instance);
                var visibleField = hierarchy.GetType().GetField("m_VisibleItems", Instance);
                var resultType = resultsField.FieldType.GetElementType();
                var guidField = resultType.GetField("assetGUID", Instance);
                var guids = AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.StartsWith("Packages/", StringComparison.Ordinal))
                    .Select(AssetDatabase.AssetPathToGUID).Where(value => !string.IsNullOrEmpty(value)).ToArray();
                var results = Array.CreateInstance(resultType, 1000);
                for (var i = 0; i < results.Length; i++)
                {
                    var result = Activator.CreateInstance(resultType, true);
                    guidField.SetValue(result, new GUID(guids[i % guids.Length]));
                    results.SetValue(result, i);
                }
                resultsField.SetValue(hierarchy, results);
                visibleField.SetValue(hierarchy, results);
                UseRule(existingFolder, false);
                Measure(report, "folders/list-pass/1000/no-hidden-rules", () => filterList(group));
                UseRule("Assets/MinimalEditorBenchmarkMissing", true);
                Measure(report, "folders/list-pass/1000/missing-hidden-folder", () => filterList(group));
                UseRule(existingFolder, true);
                Measure(report, "folders/list-pass/1000/existing-hidden-folder", () => filterList(group));
                if (resultsField.GetValue(hierarchy) != results)
                    throw new InvalidOperationException("No benchmark result is inside the hidden folder.");
            }
            finally
            {
                settingsField.SetValue(null, original);
                if (browser != null)
                    UnityEngine.Object.DestroyImmediate(browser);
                if (createdFolder)
                    AssetDatabase.DeleteAsset(existingFolder);
            }
        }

        static object Member(object instance, string name)
        {
            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(instance);
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        static void Call(object instance, string name, params object[] arguments)
        {
            foreach (var method in instance.GetType().GetMethods(Instance))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                {
                    method.Invoke(instance, arguments);
                    return;
                }
            throw new MissingMethodException(instance.GetType().FullName, name);
        }

        static Report NewReport() => new Report
        {
            utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, batchmode = Application.isBatchMode
        };

        static string SaveReport(Report report, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            return outputPath;
        }
    }
}

namespace MinimalEditor.Console.Editor
{
    // The matching name gives the native fixture a removable frame even when
    // the runtime inlines the real log handler. This type exists only in tests.
    internal static class ConsoleLogFilterInstallerBenchmarkSource
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void LogWarning()
        {
            UnityEngine.Debug.LogWarning("ordinary warning text");
            GC.KeepAlive(typeof(ConsoleLogFilterInstallerBenchmarkSource));
        }
    }
}
