using System;
using System.Linq;
using System.Reflection;
using MinimalEditor.PackageManager.Editor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace MinimalEditor.Tests.Editor.PackageManager
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
        public void NativeToolbar_LocalPackageButtonUsesActionRowAndSurvivesRefreshWithoutDuplicates()
        {
            var toolbarType = typeof(global::UnityEditor.Editor).Assembly.GetType(
                PackageManagerLocalPackageButton.ToolbarTypeName, true);
            var constructor = toolbarType.GetConstructor(Type.EmptyTypes);
            VisualElement toolbar;
            if (constructor != null)
                toolbar = (VisualElement)constructor.Invoke(null);
            else
            {
                var serializedType = toolbarType.GetNestedType("UxmlSerializedData",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(serializedType, Is.Not.Null, "The native toolbar needs its UXML construction factory.");
                var serialized = (VisualElement.UxmlSerializedData)Activator.CreateInstance(serializedType, true);
                toolbar = (VisualElement)serialized.CreateInstance();
            }
            var actions = toolbar.Q(PackageManagerLocalPackageButton.BuiltInActionsName);
            var nativeAction = toolbar.Q(PackageManagerLocalPackageButton.LegacyActionName);
            Assert.That(actions, Is.Not.Null, "The native toolbar must expose its action row.");
            Assert.That(nativeAction?.parent, Is.SameAs(actions));
            nativeAction.Query<TextElement>().ForEach(element => element.text = "Localized action");
            var package = new { name = "com.martincalander.minimaleditor", product = new object() };

            PackageManagerLocalPackageButton.Refresh(toolbar, package);
            PackageManagerLocalPackageButton.Refresh(toolbar, package);

            var buttons = toolbar.Query<Button>(PackageManagerLocalPackageButton.ButtonName).ToList();
            Assert.That(buttons, Has.Count.EqualTo(1));
            Assert.That(buttons[0].parent, Is.SameAs(actions));
            Assert.That(actions.IndexOf(buttons[0]), Is.EqualTo(actions.IndexOf(nativeAction) + 1));
            Assert.That(buttons[0].text, Is.EqualTo(PackageManagerLocalPackageButton.ButtonText));

            PackageManagerLocalPackageButton.Refresh(toolbar, null);
            Assert.That(toolbar.Q<Button>(PackageManagerLocalPackageButton.ButtonName), Is.Null,
                "Changing to a package without a local match must remove the previous action.");
        }

        [Test]
        public void TryInsertButtonAfterToolbarAction_NestedActionRowKeepsButtonInsideMainContainer()
        {
            var toolbar = new VisualElement();
            var main = new VisualElement { name = "toolbarMainContainer" };
            var actions = new VisualElement { name = PackageManagerLocalPackageButton.BuiltInActionsName };
            var nativeAction = new VisualElement { name = PackageManagerLocalPackageButton.LegacyActionName };
            nativeAction.Add(new Label("Localized action"));
            actions.Add(nativeAction);
            main.Add(actions);
            toolbar.Add(main);
            var progress = new VisualElement { name = "toolbarProgressContainer" };
            toolbar.Add(progress);
            var button = new Button();

            Assert.That(PackageManagerLocalPackageButton.TryInsertButtonAfterToolbarAction(
                toolbar, button, PackageManagerLocalPackageButton.InProjectActionText), Is.True);
            Assert.That(button.parent, Is.SameAs(actions));
            Assert.That(actions.IndexOf(button), Is.EqualTo(1));
            Assert.That(toolbar.Children().ToArray(), Is.EqualTo(new[] { main, progress }));
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
    }
}
