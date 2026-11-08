using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MinimalEditor.Console.Editor;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.Console
{
    // Invoked explicitly through the Editor. Benchmarks never clear the live Console or save preferences.
    public static partial class MinimalEditorBenchmarks
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type Installer = typeof(ConsoleLogFilterInstaller);
        static object sink;

        [Serializable]
        public sealed class Measurement
        {
            public string name;
            public int iterations;
            public double medianMicroseconds;
            public double minMicroseconds;
            public double maxMicroseconds;
            public double bytesPerOperation;
            public double[] samplesMicroseconds;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc;
            public string unity;
            public string runtime;
            public string os;
            public string cpu;
            public bool batchmode;
            public List<Measurement> measurements = new List<Measurement>();
        }

        public static string Run(string outputPath)
        {
            var report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, batchmode = Application.isBatchMode
            };
            var active = Installer.GetField("s_activeFilteringLogHandler", Static);
            var clean = Installer.GetField("s_cleanStackTracesEnabled", Static);
            var originalHandler = active.GetValue(null);
            var originalClean = clean.GetValue(null);
            try
            {
                Measure(report, "harness/empty", () => { });
                BenchmarkConsole(report, active, clean);
                BenchmarkOtherModules(report);
            }
            finally
            {
                active.SetValue(null, originalHandler);
                clean.SetValue(null, originalClean);
                sink = null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            return outputPath;
        }

        static void BenchmarkConsole(Report report, FieldInfo active, FieldInfo clean)
        {
            var handlerType = Installer.GetNestedType("FilterLogHandler", BindingFlags.NonPublic);
            var handler = Activator.CreateInstance(handlerType, true);
            handlerType.GetMethod("SetInner").Invoke(handler, new object[] { new NullLogHandler() });
            var logger = (ILogHandler)handler;
            active.SetValue(null, handler);
            var configure = handlerType.GetMethod("Configure");
            var build = Installer.GetMethod("BuildCompiledRules", Static);
            void Configure(int count, bool regex = false, bool cleanStack = true)
            {
                var rules = new List<ConsoleLogFilterRuleData>();
                for (var i = 0; i < count; i++)
                    rules.Add(new ConsoleLogFilterRuleData
                    {
                        pattern = "hidden-warning-" + i + ":",
                        matchType = regex ? ConsoleLogFilterMatchType.Regex : ConsoleLogFilterMatchType.Contains
                    });
                configure.Invoke(handler, new[] { (object)true, true, build.Invoke(null, new object[] { rules }) });
                clean.SetValue(null, cleanStack);
            }
            var noArgs = Array.Empty<object>();
            var formatArgs = new object[] { 42 };
            foreach (var count in new[] { 0, 1, 10, 100 })
            {
                Configure(count);
                Measure(report, "logger/contains/" + count + "/miss", () => logger.LogFormat(LogType.Warning, null, "ordinary warning text", noArgs));
                Measure(report, "logger/contains/" + count + "/log-bypass", () => logger.LogFormat(LogType.Log, null, "ordinary log {0}", formatArgs));
                if (count == 0) continue;
                Measure(report, "logger/contains/" + count + "/first-hit", () => logger.LogFormat(LogType.Warning, null, "hidden-warning-0: value", noArgs));
                var last = "hidden-warning-" + (count - 1) + ": value";
                Measure(report, "logger/contains/" + count + "/last-hit", () => logger.LogFormat(LogType.Warning, null, last, noArgs));
                Configure(count, regex: true);
                Measure(report, "logger/regex/" + count + "/miss", () => logger.LogFormat(LogType.Warning, null, "ordinary warning text", noArgs));
            }
            Configure(1);
            Measure(report, "logger/formatted-warning", () => logger.LogFormat(LogType.Warning, null, "ordinary warning {0}", formatArgs));
            var cleaner = Bind<Func<string, string>>(Installer, "CleanStackTrace");
            var normal = string.Join("\n", Enumerable.Repeat("Game.Player:Update() (at Assets/Player.cs:12)", 20));
            var dirty = normal + "\nUnityEngine.DebugLogHandler:LogFormat()\n" + normal;
            Measure(report, "stack/clean-20-lines", () => sink = cleaner(normal));
            Measure(report, "stack/dirty-41-lines", () => sink = cleaner(dirty));
            Measure(report, "regex/build-compiled", () => sink = new Regex("hidden-warning-[0-9]+:", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)), 1);
            Measure(report, "regex/build-interpreted", () => sink = new Regex("hidden-warning-[0-9]+:", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)), 1);
            var compiled = new Regex("hidden-warning-[0-9]+:", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            var interpreted = new Regex("hidden-warning-[0-9]+:", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            Measure(report, "regex/compiled-match", () => { if (!compiled.IsMatch("hidden-warning-42: text")) throw new Exception(); });
            Measure(report, "regex/interpreted-match", () => { if (!interpreted.IsMatch("hidden-warning-42: text")) throw new Exception(); });

            foreach (var count in new[] { 100, 1000, 10000 })
            {
                Configure(1);
                var entries = new MemoryEntries(count, "ordinary warning text", false);
                Measure(report, "console/" + count + "/unchanged", () => Scan(entries, false));
                Measure(report, "console/" + count + "/changed-no-match", () => Scan(entries, true), 1);
                var dirtyEntries = new MemoryEntries(count, "ordinary warning text", true);
                Measure(report, "console/" + count + "/stack-cleanup", () => Scan(dirtyEntries, true), 1);
                var hiddenEntries = new MemoryEntries(count, "hidden-warning-0: value", false);
                Measure(report, "console/" + count + "/all-hidden", () => Scan(hiddenEntries, true), 1);
                Configure(1, cleanStack: false);
                Measure(report, "console/" + count + "/cleanup-disabled", () => Scan(entries, true), 1);
            }
            Configure(0);
            var update = Bind<Action>(Installer, "OnEditorUpdate");
            Measure(report, "idle/console-update-no-rules", update);
            var access = Installer.GetField("s_consoleEntryAccess", Static).GetValue(null);
            var countMethod = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), access, access.GetType().GetMethod("GetTotalCount", Instance));
            Measure(report, "console/native-count", () => { if (countMethod() < 0) throw new Exception(); });
            var rowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry", true);
            var row = Activator.CreateInstance(rowType);
            var fields = rowType.GetFields(Instance);
            var shallowCopy = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), typeof(object).GetMethod("MemberwiseClone", Instance));
            Measure(report, "candidate/snapshot-reflected-copy", () =>
            {
                var clone = Activator.CreateInstance(rowType);
                foreach (var field in fields) field.SetValue(clone, field.GetValue(row));
                sink = clone;
            });
            Measure(report, "candidate/snapshot-memberwise-copy", () => sink = shallowCopy(row));
            var modeField = rowType.GetField("mode", Instance);
            var input = System.Linq.Expressions.Expression.Parameter(typeof(object));
            var modeGetter = System.Linq.Expressions.Expression.Lambda<Func<object, int>>(
                System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Convert(input, rowType), modeField), input).Compile();
            Measure(report, "candidate/mode-reflection", () => { if ((int)modeField.GetValue(row) != 0) throw new Exception(); });
            Measure(report, "candidate/mode-compiled-getter", () => { if (modeGetter(row) != 0) throw new Exception(); });
        }

        static void Scan(MemoryEntries entries, bool force)
        {
            if (!ConsoleLogFilterInstaller.TryApplyFiltersToConsoleEntries(entries, force, out _, out var message))
                throw new InvalidOperationException(message);
        }

        static void BenchmarkOtherModules(Report report)
        {
            var packageType = Find("MinimalEditor.PackageManager.Editor.PackageManagerAssetStoreDuplicateFilter");
            if (packageType != null)
            {
                var method = packageType.GetMethod("ShouldHideAssetStoreDuplicate", Static, null,
                    new[] { typeof(object), typeof(Func<string, string>), typeof(string) }, null);
                var filter = (Func<object, Func<string, string>, string, bool>)Delegate.CreateDelegate(typeof(Func<object, Func<string, string>, string, bool>), method);
                var root = Path.GetFullPath(".");
                var cache = Path.Combine(root, "Library/PackageCache/com.vendor.tool");
                Func<string, string> resolve = _ => cache;
                foreach (var count in new[] { 0, 100, 1000 })
                {
                    var assets = Enumerable.Range(0, count).Select(i => new Asset { importedPath = "Packages/com.vendor.tool/File" + i + ".cs" }).ToArray();
                    var version = new Version { importedAssets = assets };
                    var package = new Package { name = "com.vendor.tool", product = new object(), versions = new Versions { imported = version, primary = version } };
                    Measure(report, "packages/duplicate/" + count + "-assets", () => { if (!filter(package, resolve, root)) throw new Exception(); });
                }
                var registry = new Package { name = "com.vendor.tool" };
                Measure(report, "packages/non-store", () => { if (filter(registry, resolve, root)) throw new Exception(); });
            }
            var folder = Find("MinimalEditor.ProjectFolderVisibility.Editor.FolderVisibilitySettings");
            if (folder != null)
            {
                var filter = Bind<Func<string, bool>>(folder, "IsHiddenPath");
                Measure(report, "folders/path-check-current-settings", () => { if (filter("Assets/Game/Player.cs")) throw new Exception(); });
            }
            var status = Find("MinimalEditor.Editor.StatusBarExtrasHider");
            if (status != null) Measure(report, "idle/status-bar-poll", Bind<Action>(status, "PollFallback"));
            var menu = Find("MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher");
            if (menu != null)
            {
                var match = Bind<Func<string, string, bool>>(menu, "IsMatch");
                Measure(report, "menus/wildcard-match", () => { if (!match("Tools/Vendor/Settings", "Tools/*")) throw new Exception(); });
            }
            var toolbar = typeof(ConsoleAssistantToolbarButtonController).GetProperty("IsAssistantConsoleToolbarAvailable", Static).GetGetMethod(true);
            var available = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), toolbar);
            Measure(report, "console/optional-toolbar-cached", () => { available(); });
        }

        static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);
        static T Bind<T>(Type type, string method) where T : Delegate =>
            (T)Delegate.CreateDelegate(typeof(T), type.GetMethod(method, Static));

        static void Measure(Report report, string name, Action action, int initialIterations = 64)
        {
            for (var i = 0; i < 4; i++) action();
            var iterations = initialIterations;
            var watch = new Stopwatch();
            do
            {
                watch.Restart();
                for (var i = 0; i < iterations; i++) action();
                watch.Stop();
                if (watch.Elapsed.TotalMilliseconds >= 12 || iterations >= 262144) break;
                iterations *= 2;
            } while (true);
            var samples = new double[9];
            var allocations = new double[9];
            for (var sample = 0; sample < samples.Length; sample++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                watch.Restart();
                for (var i = 0; i < iterations; i++) action();
                watch.Stop();
                allocations[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
                samples[sample] = watch.Elapsed.TotalMilliseconds * 1000 / iterations;
            }
            var sorted = samples.OrderBy(v => v).ToArray();
            report.measurements.Add(new Measurement
            {
                name = name, iterations = iterations, samplesMicroseconds = samples,
                medianMicroseconds = sorted[4], minMicroseconds = sorted[0], maxMicroseconds = sorted[8],
                bytesPerOperation = allocations.OrderBy(v => v).ElementAt(4)
            });
        }

        sealed class NullLogHandler : ILogHandler
        {
            public void LogException(Exception exception, UnityEngine.Object context) { }
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) { }
        }

        sealed class MemoryEntries : ConsoleLogFilterInstaller.ConsoleEntryAccess
        {
            readonly object[] rows;
            internal MemoryEntries(int count, string text, bool dirty)
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry", true);
                var modeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow+Mode", true);
                var mode = Convert.ToInt32(Enum.Parse(modeType, "ScriptingWarning"));
                rows = new object[count];
                for (var i = 0; i < count; i++)
                {
                    var row = Activator.CreateInstance(type);
                    foreach (var field in type.GetFields(Instance))
                        if (field.FieldType == typeof(string)) field.SetValue(row, string.Empty);
                    type.GetField("mode", Instance).SetValue(row, mode);
                    type.GetField("message", Instance).SetValue(row, dirty ? text + "\nUnityEngine.DebugLogHandler:LogFormat()\nGame.Player:Update()" : text);
                    if (dirty) type.GetField("callstackTextStartUTF16", Instance).SetValue(row, text.Length + 1);
                    rows[i] = row;
                }
            }
            internal override int? GetChangeVersion() => 1;
            internal override int GetTotalCount() => rows.Length;
            internal override int BeginRead() => rows.Length;
            internal override object Read(int index) => rows[index];
            internal override void EndRead() { }
            // Replays the same immutable source for each iteration, excluding native Console I/O.
            internal override void Clear() { }
            internal override void Add(object entry) { sink = entry; }
        }

        sealed class Package { public string name; public object product; public Versions versions; }
        sealed class Versions { public Version imported; public Version primary; }
        sealed class Version { public Asset[] importedAssets; }
        sealed class Asset { public string importedPath; }
    }
}
