using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>二択の札の並べ方と当たり（ChoiceLayout・ChoiceView）、マウスの出入り（ChoicePointer）</summary>
    public class ChoiceLayoutTests
    {
        const float Dot = ChoiceLayout.Dot;

        /// <summary>全角 n 字の札の字の幅。12 Dot の字に字間 0.15 em</summary>
        static float Label(int n)
        {
            return n * ChoiceLayout.CardFont * 1.15f;
        }

        static ChoiceLayout.Frame Lay(params int[] chars)
        {
            var widths = new float[chars.Length];
            for (var i = 0; i < chars.Length; i++) widths[i] = Label(chars[i]);
            return ChoiceLayout.Lay(Label(6), widths);
        }

        [Test]
        public void TheCardsLineUpLeftToRightWithoutOverlap()
        {
            foreach (var f in new[] { Lay(2, 3), Lay(2, 3, 4), Lay(1, 2, 3, 4, 5) })
            {
                for (var i = 1; i < f.cards.Length; i++)
                {
                    Assert.Greater(f.cards[i].xMin, f.cards[i - 1].xMax, "札 " + i + " は前の札の右に離れて並ぶ");
                    Assert.AreEqual(f.cards[0].y, f.cards[i].y, 1e-3f, "札は一列");
                }
            }
        }

        [Test]
        public void TheRowSitsInTheMiddleInsideThePanel()
        {
            foreach (var f in new[] { Lay(2, 3), Lay(2, 3, 4), Lay(3, 3, 3, 3, 3) })
            {
                var left = f.cards[0].xMin;
                var right = f.cards[f.cards.Length - 1].xMax;
                Assert.AreEqual(0f, (left + right) * 0.5f, Dot, "札の列は板の真ん中");
                Assert.GreaterOrEqual(left, -f.size.x * 0.5f + 8f * Dot, "左の縁から離れる");
                Assert.LessOrEqual(right, f.size.x * 0.5f - 8f * Dot, "右の縁から離れる");
                foreach (var c in f.cards)
                {
                    Assert.GreaterOrEqual(c.yMin, -f.size.y * 0.5f + 8f * Dot, "下の縁から離れる");
                    Assert.Greater(c.width, Label(1), "札は字より広い");
                }
            }
        }

        [Test]
        public void ThePanelWidensForMoreCards()
        {
            var two = Lay(2, 3);
            var five = Lay(3, 3, 3, 3, 3);
            Assert.Greater(five.size.x, two.size.x);
            Assert.AreEqual(two.size.y, five.size.y, 1e-3f, "高さは札の数で変わらない");
            Assert.LessOrEqual(five.size.x, 1280f * 0.75f, "多くても画面からはみ出さない");
        }

        [Test]
        public void TheQuestionSitsAboveTheLineAboveTheCards()
        {
            var f = Lay(2, 3);
            Assert.Greater(f.question.yMin, f.line.yMax, "問いの下に線");
            Assert.Greater(f.line.yMin, f.cards[0].yMax, "線の下に札");
            Assert.LessOrEqual(f.question.yMax, f.size.y * 0.5f - 8f * Dot, "問いは上の縁から離れる");
            Assert.AreEqual(0f, f.line.center.x, Dot, "線は真ん中");
            Assert.Greater(f.line.width, Label(6) * 0.9f, "線は問いの下に渡る");
        }

        [Test]
        public void TheSmallestTextKeepsTenPixels()
        {
            // 既定の粗さ（0.75）で、1 Dot が粗い画面の 1 画素
            Assert.GreaterOrEqual(ChoiceLayout.QuestionFont / Dot, 11f - 1e-3f);
            Assert.GreaterOrEqual(ChoiceLayout.CardFont / Dot, 11f - 1e-3f);
        }

        [Test]
        public void TheHitFindsTheCardUnderThePoint()
        {
            var f = Lay(2, 3, 4);
            for (var i = 0; i < f.cards.Length; i++)
            {
                Assert.AreEqual(i, ChoiceLayout.Hit(f, f.cards[i].center), "札 " + i + " の真ん中");
                Assert.AreEqual(i, ChoiceLayout.Hit(f, new Vector2(f.cards[i].xMin + 1f, f.cards[i].yMax - 1f)), "札 " + i + " の左上の角");
            }
            var gap = new Vector2((f.cards[0].xMax + f.cards[1].xMin) * 0.5f, f.cards[0].center.y);
            Assert.AreEqual(-1, ChoiceLayout.Hit(f, gap), "札のあいだ");
            Assert.AreEqual(-1, ChoiceLayout.Hit(f, f.question.center), "問いの上");
            Assert.AreEqual(-1, ChoiceLayout.Hit(f, new Vector2(0f, f.size.y)), "板の外");
        }

        // ---- 組んだ札の当たり ----------------------------------------------

        GameObject canvasObject;

        [TearDown]
        public void Clean()
        {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            canvasObject = null;
        }

        [Test]
        public void TheBuiltCardsAnswerAtTheirPlaceOnScreen()
        {
            canvasObject = new GameObject("ChoiceTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var view = ChoiceView.Build((RectTransform)canvasObject.transform);
            var choice = new Choice("チップを抜く", new[] { Choice.Yes, Choice.No, "あとで" });
            view.Show(choice);
            Assert.AreEqual(-1, view.At(Vector2.zero), "伏せている間は当たらない");
            view.Visible = true;
            var corners = new Vector3[4];
            for (var i = 0; i < choice.Count; i++)
            {
                var card = view.Card(i);
                Assert.IsNotNull(card);
                Assert.AreEqual(view.Frame.cards[i].size.x, card.rect.width, 1e-2f, "札の幅は並べたとおり");
                // 重ねる Canvas では、世界の座標がそのまま画面の座標
                card.GetWorldCorners(corners);
                var middle = (corners[0] + corners[2]) * 0.5f;
                Assert.AreEqual(i, view.At(middle), "札 " + i + " の真ん中");
            }
            view.Card(0).GetWorldCorners(corners);
            var right = corners[2];
            view.Card(1).GetWorldCorners(corners);
            var between = new Vector2((right.x + corners[0].x) * 0.5f, (corners[0].y + corners[2].y) * 0.5f);
            Assert.AreEqual(-1, view.At(between), "札のあいだ");
        }

        // ---- マウスの出入り ----------------------------------------------------

        [Test]
        public void RestingOnACardWhenItOpensDoesNotChoose()
        {
            var p = new ChoicePointer();
            p.Step(1, false, false);
            Assert.AreEqual(-1, p.Entered);
            p.Step(1, false, false);
            Assert.AreEqual(-1, p.Entered);
        }

        [Test]
        public void EnteringACardChoosesItOnce()
        {
            var p = new ChoicePointer();
            p.Step(-1, false, false);
            p.Step(0, false, false);
            Assert.AreEqual(0, p.Entered);
            p.Step(0, false, false);
            Assert.AreEqual(-1, p.Entered, "上にいるだけでは選び直さない");
            p.Step(-1, false, false);
            Assert.AreEqual(-1, p.Entered, "外へ出ても選ばない");
            p.Step(1, false, false);
            Assert.AreEqual(1, p.Entered);
        }

        [Test]
        public void PressAndReleaseOnTheSameCardPicksIt()
        {
            var p = new ChoicePointer();
            p.Step(1, false, false);
            p.Step(1, true, false);
            Assert.AreEqual(-1, p.Picked, "押しただけでは決めない");
            p.Step(1, false, true);
            Assert.AreEqual(1, p.Picked);
            p.Step(0, true, true);
            Assert.AreEqual(0, p.Picked, "一フレームのうちに押して離しても決める");
        }

        [Test]
        public void SlidingOffOrReleasingAnOldPressDoesNotPick()
        {
            var p = new ChoicePointer();
            p.Step(-1, false, false);
            p.Step(0, false, true);
            Assert.AreEqual(-1, p.Picked, "出す前から押していたボタン（字幕を送ったクリック）を離しても決めない");
            p.Step(0, true, false);
            p.Step(1, false, true);
            Assert.AreEqual(-1, p.Picked, "押した札と違う札で離しても決めない");
            p.Step(-1, true, false);
            p.Step(-1, false, true);
            Assert.AreEqual(-1, p.Picked, "札の外");
        }
    }
}
