using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>リストの枠の並べ方（ListLayout）と、枠に出す表の組み方（ListFormat.Compose の寄せない形）</summary>
    public class ListLayoutTests
    {
        const float Dot = ListLayout.Dot;

        const string Chips =
            "08/15 #1 男 41 『ディエゴ』 32分40秒\n" +
            "08/15 #2 女 23 『ミア』 16分05秒\n" +
            "08/15 #3 男 8 『ゆうと』 24分38秒\n" +
            "08/15 #4 女 35 『阿明』 56分26秒\n" +
            "08/15 #5 男 19 『リアム』 3分32秒\n" +
            "08/15 #6 女 52 『マチルド』 48分55秒";

        const string Conditions =
            "条件　名前を呼ばれた時刻\n" +
            "対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日\n" +
            "候補の簡易抽出　56,232,318";

        static int Pos(string line)
        {
            var n = 0;
            for (var i = line.IndexOf("<pos="); i >= 0; i = line.IndexOf("<pos=", i + 1)) n++;
            return n;
        }

        [Test]
        public void TheTableSitsAtTheTopInsideThePanel()
        {
            var f = ListLayout.Lay(600f, 150f, 30f);
            Assert.GreaterOrEqual(f.table.xMin, -f.size.x * 0.5f + 20f * Dot, "左の縁から離れる");
            Assert.LessOrEqual(f.table.xMax, f.size.x * 0.5f - 20f * Dot, "右の縁から離れる");
            Assert.LessOrEqual(f.table.yMax, f.size.y * 0.5f - 8f * Dot, "上の縁から離れる");
            Assert.GreaterOrEqual(f.table.width, 600f - 1e-3f, "表の幅が入る");
            Assert.GreaterOrEqual(f.table.height, 150f - 1e-3f, "表の高さが入る");
        }

        [Test]
        public void ANarrowTableSitsInTheMiddleAsOneBlock()
        {
            // 売り上げのメモのような狭い表は、板の幅の下限（256 Dot）の真ん中へ、表の塊のまま寄せる
            var f = ListLayout.Lay(160f, 90f, 30f);
            Assert.AreEqual(256f * Dot, f.size.x, 1e-3f);
            Assert.AreEqual(0f, f.table.center.x, Dot, "表の塊が真ん中");
            var x = f.table.xMin / Dot;
            Assert.AreEqual(Mathf.Round(x), x, 1e-3f, "表の頭は Dot の境目");
        }

        [Test]
        public void TheAdvanceMarkSitsBottomRightBelowTheTable()
        {
            var f = ListLayout.Lay(300f, 150f, 30f);
            Assert.LessOrEqual(f.hint.yMax, f.table.yMin, "表の下");
            Assert.LessOrEqual(f.hint.xMax, f.size.x * 0.5f - 8f * Dot, "右の縁から離れる");
            Assert.Greater(f.hint.xMin, 0f, "右に寄る");
            Assert.GreaterOrEqual(f.hint.yMin, -f.size.y * 0.5f + 4f * Dot, "下の縁から離れる");
            Assert.GreaterOrEqual(f.hint.width, 30f - 1e-3f);
        }

        [Test]
        public void ThePanelFollowsTheTableWithinItsLimits()
        {
            var small = ListLayout.Lay(100f, 60f, 30f);
            var wide = ListLayout.Lay(800f, 60f, 30f);
            var huge = ListLayout.Lay(5000f, 60f, 30f);
            Assert.AreEqual(256f * Dot, small.size.x, 1e-3f, "狭い表でも二択の板の下限の幅");
            Assert.Greater(wide.size.x, small.size.x, "広い表ほど広い");
            Assert.AreEqual(ListLayout.Most * Dot, huge.size.x, 1e-3f, "上限を超えない");
            Assert.LessOrEqual(huge.size.x, 1280f * 0.85f, "画面からはみ出さない");
            Assert.Greater(ListLayout.Lay(100f, 200f, 30f).size.y, small.size.y, "行が多いほど高い");
        }

        [Test]
        public void TheEdgesLandOnWholeDots()
        {
            foreach (var f in new[] { ListLayout.Lay(333.3f, 111.1f, 27.7f), ListLayout.Lay(90f, 20f, 30f) })
            {
                var w = Mathf.RoundToInt(f.size.x / Dot);
                var h = Mathf.RoundToInt(f.size.y / Dot);
                Assert.AreEqual(w * Dot, f.size.x, 1e-3f);
                Assert.AreEqual(0, w % 2, "幅は偶数");
                Assert.AreEqual(1, h % 2, "高さは奇数（二択と同じく、画面の真ん中に置いても縁が画素の境目に乗る）");
            }
        }

        [Test]
        public void TheTextSizesMatchTheChoicePanelAndTheSubtitleMark()
        {
            Assert.AreEqual(ChoiceLayout.CardFont, ListLayout.RowFont, 1e-4f, "表の字は二択の札の字と同じ");
            Assert.AreEqual(11f, ListLayout.HintFont / Dot, 1e-3f, "送りの印は字幕の送りの印と同じ 11 Dot");
            Assert.AreEqual(ChoiceLayout.Lift, ListLayout.Lift, 1e-4f, "二択と同じ所に浮かぶ");
        }

        [Test]
        public void TheTableIsNotShiftedToTheMiddle()
        {
            var made = ListFormat.Compose(Chips, ListLayout.RoomEm, false);
            foreach (var line in made.Split('\n'))
                Assert.IsTrue(line.StartsWith("08/15"), "行の頭に寄せの pos を入れない: " + line);
        }

        [Test]
        public void TheColumnsFoldAsTheyDidInTheSubtitleWindow()
        {
            // チップのリストは 6 列のまま、走査条件は 2 列に畳む（字幕の窓で出していた頃と同じ）
            foreach (var line in ListFormat.Compose(Chips, ListLayout.RoomEm, false).Split('\n'))
                Assert.AreEqual(5, Pos(line), line);
            foreach (var line in ListFormat.Compose(Conditions, ListLayout.RoomEm, false).Split('\n'))
                Assert.AreEqual(1, Pos(line), line);
        }
    }
}
