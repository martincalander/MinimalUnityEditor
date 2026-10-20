using System;
using System.IO;
using MinimalEditor.Editor;
using NUnit.Framework;

namespace MinimalEditor.Tests.Editor.PackageManager
{
    public sealed class ResolvePackagesMenuItemTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ResolveRunsWithOrWithoutALockfile(bool hasLockfile)
        {
            string directory = Path.Combine(Path.GetTempPath(), "MinimalEditorResolve-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string lockPath = Path.Combine(directory, "packages-lock.json");
            try
            {
                if (hasLockfile)
                    File.WriteAllText(lockPath, "{}");
                int calls = 0;

                ResolvePackagesMenuItem.ResolveAllPackages(lockPath, () =>
                {
                    Assert.That(File.Exists(lockPath), Is.False);
                    calls++;
                });

                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
