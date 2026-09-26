using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 調べられる物の案内と送りの印の書式（HudView.Prompt・HudView.Advance）。
    /// 案内は「E/」＋左クリックのアイコン＋全角の空白＋対象の名、送りの印は「E/」＋アイコンだけ
    /// </summary>
    public class PromptFormatTests
    {
        [Test]
        public void ThePromptIsKeysIconSpaceAndName()
        {
            Assert.AreEqual("E/" + HudView.ClickLarge + "　机", HudView.Prompt("机"));
        }

        [Test]
        public void TheAdvanceMarkIsJustKeysAndIcon()
        {
            Assert.AreEqual("E/" + HudView.ClickSmall, HudView.Advance);
            StringAssert.DoesNotContain("送る", HudView.Advance);
            StringAssert.DoesNotContain("▼", HudView.Advance);
        }

        [Test]
        public void TheIconsNameTheSpriteAssetAndTakeTheTextColour()
        {
            foreach (var icon in new[] { HudView.ClickLarge, HudView.ClickSmall })
            {
                StringAssert.StartsWith("<sprite=\"" + HudView.ClickAsset + "\"", icon);
                StringAssert.Contains("tint=1", icon, "字の色で塗る");
            }
            StringAssert.Contains("name=\"large\"", HudView.ClickLarge);
            StringAssert.Contains("name=\"small\"", HudView.ClickSmall);
        }

        [Test]
        public void TheSpriteAssetIsWhereTmpLooksForIt()
        {
            var asset = Resources.Load<TMP_SpriteAsset>(TMP_Settings.defaultSpriteAssetPath + HudView.ClickAsset);
            Assert.That(asset, Is.Not.Null, "Resources/Sprite Assets/MouseLeft が無い。HalfAware/Make the click icon を走らせる");
            Assert.That(asset.GetSpriteIndexFromName("large"), Is.GreaterThanOrEqualTo(0));
            Assert.That(asset.GetSpriteIndexFromName("small"), Is.GreaterThanOrEqualTo(0));
            Assert.That(asset.spriteSheet, Is.Not.Null);
            Assert.AreEqual(FilterMode.Point, asset.spriteSheet.filterMode, "画素絵はぼかさない");
        }

        [Test]
        public void BothIconsStandAsTallAsTheText()
        {
            var asset = Resources.Load<TMP_SpriteAsset>(TMP_Settings.defaultSpriteAssetPath + HudView.ClickAsset);
            Assume.That(asset, Is.Not.Null);
            var point = asset.faceInfo.pointSize;
            foreach (var name in new[] { "large", "small" })
            {
                var c = asset.spriteCharacterTable[asset.GetSpriteIndexFromName(name)];
                var glyph = (TMP_SpriteGlyph)c.glyph;
                var em = glyph.metrics.height * glyph.scale * c.scale / point;
                Assert.AreEqual(0.91f, em, 0.01f, name + " の高さ（em）");
            }
        }
    }
}
