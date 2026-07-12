using Essentials.PackageManager.Editor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Essentials.Tests.Editor.PackageManager
{
    public sealed class PackageManagerLocalPackageButtonTests
    {
        [Test]
        public void TryInsertButtonAfterToolbarAction_InProjectActionExists_InsertsAfterActionContainer()
        {
            var toolbar = new VisualElement();
            var before = new VisualElement { name = "before" };
            var inProjectAction = new VisualElement { name = "inProjectAction" };
            var after = new VisualElement { name = "after" };
            var button = new Button { name = PackageManagerLocalPackageButton.ButtonName };

            inProjectAction.Add(new Label(PackageManagerLocalPackageButton.InProjectActionText));
            toolbar.Add(before);
            toolbar.Add(inProjectAction);
            toolbar.Add(after);

            Assert.That(
                PackageManagerLocalPackageButton.TryInsertButtonAfterToolbarAction(
                    toolbar,
                    button,
                    PackageManagerLocalPackageButton.InProjectActionText),
                Is.True);

            Assert.That(toolbar.IndexOf(button), Is.EqualTo(2));
            Assert.That(toolbar.IndexOf(after), Is.EqualTo(3));
        }

        [Test]
        public void TryInsertButtonAfterToolbarAction_InProjectActionMissing_ReturnsFalseAndDoesNotInsert()
        {
            var toolbar = new VisualElement();
            var existing = new VisualElement { name = "existing" };
            var button = new Button { name = PackageManagerLocalPackageButton.ButtonName };
            toolbar.Add(existing);

            Assert.That(
                PackageManagerLocalPackageButton.TryInsertButtonAfterToolbarAction(
                    toolbar,
                    button,
                    PackageManagerLocalPackageButton.InProjectActionText),
                Is.False);

            Assert.That(toolbar.Contains(button), Is.False);
            Assert.That(toolbar.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ButtonMetadata_UsesExpectedTextAndName()
        {
            Assert.That(PackageManagerLocalPackageButton.ButtonName, Is.EqualTo("essentials-go-to-local-package-button"));
            Assert.That(PackageManagerLocalPackageButton.ButtonText, Is.EqualTo("Go To Local Package"));
            Assert.That(PackageManagerLocalPackageButton.InProjectActionText, Is.EqualTo("In Project"));
        }
    }
}
