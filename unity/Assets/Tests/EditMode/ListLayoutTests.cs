using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>リストの枠の並べ方（ListLayout）と、枠に出す表の組み方（ListFormat.Compose の寄せない形）</summary>
    public class ListLayoutTests
    {
        const float Dot = ListLayout.Dot;

        /// <summary>英語に日本語訳のルビを振ったリストの見本（2026-09-28 の原稿の形。2026-09-29 にオーナーが日本語へ戻したが、ルビの付いた表の組み方を見るのに使う）</summary>
        const string Chips =
            "08/15  #1  Male<男>    41  32m40s  『Diego』\n" +
            "08/15  #2  Female<女>  23  16m05s  『Mia』\n" +
            "08/15  #3  Male<男>     8  24m38s  『優斗』\n" +
            "08/15  #4  Female<女>  35  56m26s  『阿明』\n" +
            "08/15  #5  Male<男>    19   3m32s  『Liam』\n" +
            "08/15  #6  Female<女>  52  48m55s  『Mathilde』";

        /// <summary>英語にルビの走査条件の見本（2026-09-28 の原稿の形）</summary>
        const string ConditionsInEnglish =
            "Condition<条件>:  ｜Name-call time<名前を呼ばれた時刻>\n" +
            "Target<対象>:  ｜No firewall<防壁なし>\n" +
            "Range<距離>:  Random<ランダム>\n" +
            "Period<期間>:  2156/03/02 - 2156/03/03\n" +
            "｜Quick-filter hits<候補の簡易抽出結果>:  526,232,318";

        /// <summary>英語にルビの売り上げのメモの見本（2026-09-28 の原稿の形）。最後の行は日付だけ</summary>
        const string Memo =
            "2166/08/10  Bought<仕入>  30 pcs  Stock<在庫>  47 pcs\n" +
            "2166/08/11  Sold<売却>     5 pcs  Stock<在庫>  42 pcs\n" +
            "2166/08/13  Sold<売却>     4 pcs  Stock<在庫>  38 pcs\n" +
            "2166/08/15";

        /// <summary>場面 3 の走査条件（今は日本語。全角の空白で区切る）</summary>
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
        public void TheListRubyIsBigEnoughToReadOnTheCoarseScreen()
        {
            // 表の字 12 Dot に対してルビ 11 Dot。字の下限（10 画素ほど）を下回ると、画数の多い漢字が粗い画面で潰れる（2026-09-28）
            Assert.That(ListLayout.RubyFont / Dot, Is.GreaterThanOrEqualTo(10f));
            Assert.That(ListLayout.RubyScale, Is.LessThan(1f), "表の字より一回り小さい");
            Assert.That(ListLayout.RubyScale, Is.GreaterThan(Ruby.Scale), "字幕のルビより大きい");
        }

        [Test]
        public void TheListRowsOpenEnoughForTheBiggerRuby()
        {
            var scale = ListLayout.RubyScale;
            // ルビが親字にかぶらない
            Assert.That(Ruby.LiftFor(scale) - Ruby.RubyDip * scale, Is.GreaterThanOrEqualTo(Ruby.BaseTop + 0.05f));
            // 行を開けた後の行送りから、下の行のルビの上の端を引いても、上の行の親字の下の端より下にならない
            var advance = Ruby.Advance + Ruby.SpacingFor(scale) * 0.01f;
            var rubyTop = Ruby.LiftFor(scale) + Ruby.RubyTop * scale;
            Assert.That(advance - rubyTop, Is.GreaterThanOrEqualTo(Ruby.BaseBottom + 0.05f));
            Assert.That(Ruby.SpacingFor(Ruby.Scale), Is.EqualTo(Ruby.ExtraLineSpacing).Within(1e-3f), "字幕の行間と同じ式");
        }

        [Test]
        public void TheBiggerRubyWidensItsColumnWhenItIsWiderThanTheBase()
        {
            // 「名前を呼ばれた時刻」（9 字）は、字幕の大きさなら親字「Name-call time」（14）に収まるが、表の大きさでは親字より広い
            const string cell = "｜Name-call time<名前を呼ばれた時刻>";
            Assert.AreEqual(14, Ruby.Width(cell));
            Assert.AreEqual(17, Ruby.Width(cell, ListLayout.RubyScale));
            var made = ListFormat.Compose("a  " + cell + "  z\nb  c  z", 200f, false, null, ListLayout.RubyScale).Split('\n');
            // 3 列目の頭は 1 列目 0.5 em + 間 1 em + 2 列目 8.5 em（ルビの幅 17）+ 間 1 em = 11 em。字幕の大きさなら 2 列目は 7 em で 9.5 em
            StringAssert.Contains("<pos=11em>z", made[1]);
            StringAssert.Contains("<pos=9.5em>z", ListFormat.Compose("a  " + cell + "  z\nb  c  z", 200f, false).Split('\n')[1]);
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

        [Test]
        public void TheEnglishListsKeepEveryColumn()
        {
            // 原稿の英語のリストは、ルビの付いた列も含めて全部の列が枠に入る
            foreach (var line in ListFormat.Compose(ConditionsInEnglish, ListLayout.RoomEm, false).Split('\n'))
                Assert.AreEqual(1, Pos(line), line);
            var memo = ListFormat.Compose(Memo, ListLayout.RoomEm, false).Split('\n');
            for (var i = 0; i < 3; i++) Assert.AreEqual(4, Pos(memo[i]), memo[i]);
            Assert.AreEqual("2166/08/15", memo[3], "日付だけの行はそのまま");
            Assert.LessOrEqual(ListFormat.WidthEm(Chips, 6), ListLayout.RoomEm * 0.92f, "チップの 6 列が枠の幅に入る");
        }

        [Test]
        public void TheCountsLineUpAtTheirEnds()
        {
            // 「 5 pcs」は「30 pcs」と尻を揃える（原稿で空白を足して揃えてある）
            var memo = ListFormat.Compose(Memo, ListLayout.RoomEm, false).Split('\n');
            Assert.AreEqual(PosOf(memo[0], "30 pcs") + 0.5f, PosOf(memo[1], "5 pcs"), 1e-3f);
            var chips = ListFormat.Compose(Chips, ListLayout.RoomEm, false).Split('\n');
            Assert.AreEqual(PosOf(chips[0], "41") + 0.5f, PosOf(chips[2], "8"), 1e-3f);
            Assert.AreEqual(PosOf(chips[0], "32m40s") + 0.5f, PosOf(chips[4], "3m32s"), 1e-3f);
        }

        /// <summary>line の中で text の直前に置いた pos の値（em）</summary>
        static float PosOf(string line, string text)
        {
            var at = line.IndexOf("em>" + text);
            Assert.GreaterOrEqual(at, 0, text + " が無い: " + line);
            var from = line.LastIndexOf("<pos=", at) + 5;
            return float.Parse(line.Substring(from, at - from), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
