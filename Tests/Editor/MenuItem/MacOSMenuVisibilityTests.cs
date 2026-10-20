#if UNITY_EDITOR_OSX
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace MinimalEditor.Tests.Editor.MenuItem
{
    public sealed class MacOSMenuVisibilityTests
    {
        const string TypeName = "MinimalEditor.MenuItem.Editor.MacOSMenuVisibility, MinimalEditor.MenuItem.Editor";
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        readonly List<IntPtr> items = new List<IntPtr>();
        Type visibilityType;
        object visibility;
        IntPtr menu;
        IntPtr applicationItem;

        [SetUp]
        public void SetUp()
        {
            visibilityType = Type.GetType(TypeName);
            Assert.That(visibilityType, Is.Not.Null);
            visibility = Activator.CreateInstance(visibilityType, true);
            menu = Native.CreateWithTitle("NSMenu", "Minimal Editor Tests");
            Assert.That(menu, Is.Not.EqualTo(IntPtr.Zero));
            applicationItem = AddRoot("Test Application");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                (visibility as IDisposable)?.Dispose();
            }
            finally
            {
                if (menu != IntPtr.Zero) Native.Release(menu);
                foreach (var item in items) Native.Release(item);
                items.Clear();
                visibility = null;
                menu = IntPtr.Zero;
            }
        }

        [TestCase("Jobs", "Jobs/*")]
        [TestCase("jobs", " Jobs/* ")]
        [TestCase("Tools", "Tools/*")]
        public void Apply_HidesExactRoot_AndPreservesOtherRoots(string title, string pattern)
        {
            var root = AddRoot(title);
            var prefix = AddRoot(title + " Extra");
            var unrelated = AddRoot("Window");
            Configure(pattern, "Test Application/*");

            Apply();

            Assert.That(Native.IsHidden(root), Is.True);
            Assert.That(Native.IsHidden(prefix), Is.False);
            Assert.That(Native.IsHidden(unrelated), Is.False);
            Assert.That(Native.IsHidden(applicationItem), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Jobs")]
        [TestCase("Jobs/Submenu/*")]
        [TestCase("Jobs/Command")]
        [TestCase("/*")]
        [TestCase("*/*")]
        [TestCase("Services/*")]
        [TestCase(" services/* ")]
        [TestCase("GameObject/*")]
        [TestCase("Job*/*")]
        [TestCase("Jobs*")]
        public void Apply_PartialOrProtectedPattern_KeepsRootsVisible(string pattern)
        {
            var roots = new[] { AddRoot("Jobs"), AddRoot("Services"), AddRoot("GameObject") };
            Configure(pattern);

            Apply();

            foreach (var root in roots) Assert.That(Native.IsHidden(root), Is.False);
        }

        [Test]
        public void Apply_DisablingPatterns_RestoresOnlyOwnedRoots()
        {
            var jobs = AddRoot("Jobs");
            var tools = AddRoot("Tools", hidden: true);
            Configure("Jobs/*", "Tools/*");
            Apply();
            Assert.That(Native.IsHidden(jobs), Is.True);

            Configure(null);
            Apply();

            Assert.That(Native.IsHidden(jobs), Is.False);
            Assert.That(Native.IsHidden(tools), Is.True);
        }

        [Test]
        public void Apply_RootMadeVisibleExternally_HidesItAgain()
        {
            var jobs = AddRoot("Jobs");
            Configure("Jobs/*");
            Apply();
            Native.SetHidden(jobs, false);
            Assert.That(Native.IsHidden(jobs), Is.False);

            Apply();

            Assert.That(Native.IsHidden(jobs), Is.True);
        }

        [Test]
        public void Apply_ReplacedRoot_RestoresAndReleasesOldRoot_AndHidesReplacement()
        {
            var oldRoot = AddRoot("Jobs");
            Configure("Jobs/*");
            Apply();
            Native.SendObject(menu, Native.Selector("removeItem:"), oldRoot);
            var replacement = AddRoot("Jobs");
            var retainedBeforeRetirement = Native.RetainCount(oldRoot);

            Apply();

            Assert.That(Native.IsHidden(oldRoot), Is.False);
            Assert.That(Native.IsHidden(replacement), Is.True);
            Assert.That(Native.RetainCount(oldRoot), Is.EqualTo(retainedBeforeRetirement - 1),
                "Retiring a root should release the retain held by the visibility helper.");
        }

        [Test]
        public void Apply_MissingMenu_RestoresOwnedRoots()
        {
            var jobs = AddRoot("Jobs");
            Configure("Jobs/*");
            Apply();

            Invoke("ApplyToMenu", IntPtr.Zero);

            Assert.That(Native.IsHidden(jobs), Is.False);
        }

        [Test]
        public void Dispose_RestoresAndReleasesOwnedRoots_AndPreservesPrehiddenRoots()
        {
            var jobs = AddRoot("Jobs");
            var tools = AddRoot("Tools", hidden: true);
            Configure("Jobs/*", "Tools/*");
            Apply();
            var retainedBeforeDisposal = Native.RetainCount(jobs);

            ((IDisposable)visibility).Dispose();
            ((IDisposable)visibility).Dispose();

            Assert.That(Native.IsHidden(jobs), Is.False);
            Assert.That(Native.IsHidden(tools), Is.True);
            Assert.That(Native.RetainCount(jobs), Is.EqualTo(retainedBeforeDisposal - 1),
                "Repeated disposal should release ownership exactly once.");
        }

        void Configure(params string[] patterns)
        {
            Invoke("Configure", (object)patterns);
        }

        void Apply()
        {
            Invoke("ApplyToMenu", menu);
        }

        void Invoke(string methodName, object argument)
        {
            var method = visibilityType.GetMethod(methodName, AnyInstance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(visibility, new[] { argument });
        }

        IntPtr AddRoot(string title, bool hidden = false)
        {
            var item = Native.CreateWithTitle("NSMenuItem", title);
            Assert.That(item, Is.Not.EqualTo(IntPtr.Zero));
            items.Add(item);
            Native.SetHidden(item, hidden);
            Native.SendObject(menu, Native.Selector("addItem:"), item);
            return item;
        }

        static class Native
        {
            const string Library = "/usr/lib/libobjc.A.dylib";

            internal static IntPtr CreateWithTitle(string className, string title)
            {
                var text = InitString(Get(GetClass("NSString"), Selector("alloc")), Selector("initWithUTF8String:"), title);
                try
                {
                    var instance = Get(GetClass(className), Selector("alloc"));
                    if (className == "NSMenu") return GetObject(instance, Selector("initWithTitle:"), text);
                    var keyEquivalent = Get(GetClass("NSString"), Selector("new"));
                    try
                    {
                        return InitMenuItem(instance, Selector("initWithTitle:action:keyEquivalent:"), text, IntPtr.Zero,
                            keyEquivalent);
                    }
                    finally
                    {
                        Release(keyEquivalent);
                    }
                }
                finally
                {
                    Release(text);
                }
            }

            internal static bool IsHidden(IntPtr item) => GetBool(item, Selector("isHidden")) != 0;
            internal static void SetHidden(IntPtr item, bool hidden) => SetBool(item, Selector("setHidden:"), hidden ? (byte)1 : (byte)0);
            internal static long RetainCount(IntPtr item) => Get(item, Selector("retainCount")).ToInt64();
            internal static void Release(IntPtr item) => Send(item, Selector("release"));

            [DllImport(Library, EntryPoint = "objc_getClass")] static extern IntPtr GetClass(string name);
            [DllImport(Library, EntryPoint = "sel_registerName")] internal static extern IntPtr Selector(string name);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern IntPtr Get(IntPtr receiver, IntPtr selector);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern IntPtr GetObject(IntPtr receiver, IntPtr selector, IntPtr value);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern IntPtr InitString(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern IntPtr InitMenuItem(IntPtr receiver, IntPtr selector, IntPtr title, IntPtr action, IntPtr keyEquivalent);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern byte GetBool(IntPtr receiver, IntPtr selector);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern void SetBool(IntPtr receiver, IntPtr selector, byte value);
            [DllImport(Library, EntryPoint = "objc_msgSend")] static extern void Send(IntPtr receiver, IntPtr selector);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern void SendObject(IntPtr receiver, IntPtr selector, IntPtr value);
        }
    }
}
#endif
