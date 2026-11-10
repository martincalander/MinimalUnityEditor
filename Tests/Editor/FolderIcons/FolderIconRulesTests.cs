using MinimalEditor.FolderIcons.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.FolderIcons
{
    internal sealed class FolderIconRulesTests
    {
        [TestCase("Assets/Icon.png")]
        [TestCase("Assets/Characters/FooIcon.png")]
        [TestCase("Assets/Characters/fooicon.JPG")]
        [TestCase("Assets/Characters/ICON.tga")]
        [TestCase("Assets/Characters/Foo.Bar Icon.psd")]
        [TestCase("Assets/Characters/Icon")]
        public void IsIconAssetPath_AcceptsNamesEndingInIcon(string path)
        {
            Assert.That(FolderIconRules.IsIconAssetPath(path), Is.True);
        }

        [TestCase("Assets/Characters/IconFoo.png")]
        [TestCase("Assets/Characters/Iconic.png")]
        [TestCase("Assets/Characters/Foo.png")]
        [TestCase("Assets/Icon/Foo.png")]
        [TestCase("Assets/Characters/.png")]
        [TestCase("")]
        [TestCase(null)]
        public void IsIconAssetPath_RejectsOtherNames(string path)
        {
            Assert.That(FolderIconRules.IsIconAssetPath(path), Is.False);
        }

        [TestCase("Icon", true)]
        [TestCase("FooIcon", true)]
        [TestCase("fooicon", true)]
        [TestCase("IconFoo", false)]
        [TestCase("Iconic", false)]
        [TestCase("", false)]
        public void IsIconFileName_UsesCaseInsensitiveSuffix(string name, bool expected)
        {
            Assert.That(FolderIconRules.IsIconFileName(name), Is.EqualTo(expected));
        }

        [TestCase("Assets/A/B.png", "Assets/A")]
        [TestCase("Packages/com.example.tool/Icon.png", "Packages/com.example.tool")]
        [TestCase("Assets", null)]
        [TestCase("", null)]
        public void GetParentFolder_UsesLastSlash(string path, string expected)
        {
            Assert.That(FolderIconRules.GetParentFolder(path), Is.EqualTo(expected));
        }

        [Test]
        public void GetRank_PrefersExactIconThenFolderNameIcon()
        {
            Assert.That(FolderIconRules.GetRank("Assets/Audio/icon.png", "Audio"), Is.EqualTo(FolderIconRules.ExactIconRank));
            Assert.That(FolderIconRules.GetRank("Assets/Audio/AudioIcon.png", "Audio"), Is.EqualTo(FolderIconRules.FolderNameIconRank));
            Assert.That(FolderIconRules.GetRank("Assets/Audio/audioicon.png", "Audio"), Is.EqualTo(FolderIconRules.FolderNameIconRank));
            Assert.That(FolderIconRules.GetRank("Assets/Audio/MusicIcon.png", "Audio"), Is.EqualTo(FolderIconRules.OtherIconRank));
            Assert.That(FolderIconRules.GetRank("Assets/Audio/Music.png", "Audio"), Is.EqualTo(FolderIconRules.NotAnIconRank));
        }

        [Test]
        public void ChooseIcon_PrefersExactIcon()
        {
            string chosen = FolderIconRules.ChooseIcon("Assets/Audio", new[]
            {
                "Assets/Audio/AIcon.png",
                "Assets/Audio/AudioIcon.png",
                "Assets/Audio/Icon.png"
            });

            Assert.That(chosen, Is.EqualTo("Assets/Audio/Icon.png"));
        }

        [Test]
        public void ChooseIcon_PrefersFolderNameIconOverOtherMatches()
        {
            string chosen = FolderIconRules.ChooseIcon("Assets/Audio", new[]
            {
                "Assets/Audio/AIcon.png",
                "Assets/Audio/AudioIcon.png"
            });

            Assert.That(chosen, Is.EqualTo("Assets/Audio/AudioIcon.png"));
        }

        [Test]
        public void ChooseIcon_UsesOrdinalPathOrderForEqualRanks()
        {
            string[] candidates = { "Assets/Audio/bIcon.png", "Assets/Audio/BIcon.png", "Assets/Audio/AIcon.png" };
            string[] reversed = { candidates[2], candidates[1], candidates[0] };

            Assert.That(FolderIconRules.ChooseIcon("Assets/Audio", candidates), Is.EqualTo("Assets/Audio/AIcon.png"));
            Assert.That(FolderIconRules.ChooseIcon("Assets/Audio", reversed), Is.EqualTo("Assets/Audio/AIcon.png"));
            Assert.That(
                FolderIconRules.ChooseIcon("Assets/Audio", new[] { "Assets/Audio/Icon.png", "Assets/Audio/Icon.jpg" }),
                Is.EqualTo("Assets/Audio/Icon.jpg"));
        }

        [Test]
        public void ChooseIcon_IgnoresNestedAndNonMatchingFiles()
        {
            string chosen = FolderIconRules.ChooseIcon("Assets/Audio", new[]
            {
                "Assets/Audio/Music/Icon.png",
                "Assets/Audio/Iconic.png",
                "Assets/Other/Icon.png"
            });

            Assert.That(chosen, Is.Null);
        }

        [Test]
        public void GetIconRect_UsesRowHeightForListsAndWidthForGrid()
        {
            Assert.That(FolderIconDrawer.GetIconRect(new Rect(34, 10, 160, 16)), Is.EqualTo(new Rect(34, 10, 16, 16)));
            Assert.That(FolderIconDrawer.GetIconRect(new Rect(10, 20, 64, 80)), Is.EqualTo(new Rect(10, 20, 64, 64)));
        }

        [Test]
        public void GetBadgeRect_SitsInBottomRightCorner()
        {
            Assert.That(FolderIconDrawer.GetBadgeRect(new Rect(34, 10, 16, 16), grid: false), Is.EqualTo(new Rect(40, 16, 10, 10)));
            Assert.That(FolderIconDrawer.GetBadgeRect(new Rect(10, 20, 64, 64), grid: true), Is.EqualTo(new Rect(45, 55, 29, 29)));
        }

        [Test]
        public void Classify_SeparatesTreeContentListAndGrid()
        {
            var window = new Rect(100, 500, 1000, 300);

            Assert.That(FolderIconDrawer.Classify(new Rect(10, 0, 64, 80), 300, window, true), Is.EqualTo(FolderIconSurface.Grid));
            Assert.That(FolderIconDrawer.Classify(new Rect(34, 0, 160, 16), 100, window, true), Is.EqualTo(FolderIconSurface.Tree));
            Assert.That(FolderIconDrawer.Classify(new Rect(18, 0, 700, 16), 307, window, true), Is.EqualTo(FolderIconSurface.ContentList));
            Assert.That(FolderIconDrawer.Classify(new Rect(18, 0, 700, 16), 307, default, false), Is.EqualTo(FolderIconSurface.Tree));
        }

        [Test]
        public void IsTwoColumnTree_DetectsTreeThatStopsBeforeWindowEdge()
        {
            var window = new Rect(0, 500, 1152, 300);

            Assert.That(FolderIconDrawer.IsTwoColumnTree(new Rect(34, 0, 161, 16), 0, window), Is.True);
            Assert.That(FolderIconDrawer.IsTwoColumnTree(new Rect(34, 0, 1105, 16), 0, window), Is.False);
        }

        [Test]
        public void ContainsRow_AllowsVerticalToleranceOnly()
        {
            var window = new Rect(0, 557, 1152, 289);

            Assert.That(FolderIconDrawer.ContainsRow(window, new Vector2(114, 862)), Is.True);
            Assert.That(FolderIconDrawer.ContainsRow(window, new Vector2(114, 900)), Is.False);
            Assert.That(FolderIconDrawer.ContainsRow(window, new Vector2(1160, 700)), Is.False);
        }

        [TestCase(true, false, false, true, false, FolderIconRowState.SelectedFocused)]
        [TestCase(true, false, false, false, false, FolderIconRowState.SelectedUnfocused)]
        [TestCase(false, true, false, true, true, FolderIconRowState.Normal)]
        [TestCase(true, false, true, true, false, FolderIconRowState.SelectedFocused)]
        [TestCase(true, false, true, true, true, FolderIconRowState.Normal)]
        [TestCase(false, true, true, true, true, FolderIconRowState.SelectedUnfocused)]
        [TestCase(false, true, true, true, false, FolderIconRowState.Normal)]
        public void GetRowState_FollowsNativeHighlight(
            bool selected, bool parent, bool twoColumnTree, bool focused, bool inContent, FolderIconRowState expected)
        {
            Assert.That(FolderIconDrawer.GetRowState(selected, parent, twoColumnTree, focused, inContent), Is.EqualTo(expected));
        }

        [Test]
        public void GridSelection_TintsIconInsteadOfFillingBackground()
        {
            Assert.That(
                FolderIconColors.Background(FolderIconSurface.Grid, FolderIconRowState.SelectedFocused, true),
                Is.EqualTo(FolderIconColors.Background(FolderIconSurface.Grid, FolderIconRowState.Normal, true)));
            Assert.That(FolderIconColors.IconTint(FolderIconSurface.Grid, FolderIconRowState.SelectedFocused), Is.Not.EqualTo(Color.white));
            Assert.That(FolderIconColors.IconTint(FolderIconSurface.Tree, FolderIconRowState.SelectedFocused), Is.EqualTo(Color.white));
        }
    }
}
