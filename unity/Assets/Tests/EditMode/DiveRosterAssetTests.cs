using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    /// <summary>場面 4 の記憶の一覧が、設計書 6 節のとおりに書き出されているかを見る</summary>
    public class DiveRosterAssetTests
    {
        const string Path = "Assets/Data/DiveRoster.asset";
        const string ConnectPath = "Assets/Data/ConnectScript.asset";

        static DiveRoster Load()
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(Path);
            Assert.That(roster, Is.Not.Null, Path + " が無い。HalfAware/Write the dive roster を走らせる");
            return roster;
        }

        static DiveEntry[] All(DiveRoster roster)
        {
            var all = new DiveEntry[roster.Count];
            for (var i = 0; i < all.Length; i++) all[i] = roster[i];
            return all;
        }

        [Test]
        public void TheRosterAssetIsThere()
        {
            Assert.That(Load().Count, Is.GreaterThan(0), Path + " が空");
        }

        [Test]
        public void SixteenAreListed()
        {
            Assert.That(Load().Count, Is.EqualTo(16));
        }

        [Test]
        public void EveryHopLandsInsideTheRoster()
        {
            Assert.That(DiveRoster.Closed(All(Load())), Is.True, "飛び先が一覧の外を指している");
        }

        // 右上の行も板の一行目も、場面 3 の列の文字をそのまま出す。
        // 書式が一字でも違うと、列で見た人と潜った先の人が同じ人だと読み取れなくなる
        [Test]
        public void TheSixOnTheMonitorKeepTheirRow()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ConnectPath);
            Assert.That(script, Is.Not.Null, ConnectPath + " が無い。HalfAware/Write the connect script を走らせる");

            var monitor = script.Find(ConnectIds.Monitor);
            Assert.That(monitor.Lines.Count, Is.GreaterThanOrEqualTo(3), "monitor に三ページ目が無い");

            var rows = monitor.Lines[2].Split('\n');
            Assert.That(rows.Length, Is.EqualTo(DiveIds.Listed.Length), "列の行数と DiveIds.Listed の数が違う");

            var roster = Load();
            for (var i = 0; i < DiveIds.Listed.Length; i++)
            {
                Assert.That(roster[DiveIds.Listed[i]].row, Is.EqualTo(rows[i]),
                    "列の " + (i + 1) + " 行目と一覧の " + DiveIds.Listed[i] + " 番の行が違う");
            }
        }

        // 同じ場所を別の体で見る対。片方だけ場所を書き換えると、
        // 渡った先で同じ場所のはずの箱が入れ替わる
        [Test]
        public void ThePairsStandInTheSamePlace()
        {
            var roster = Load();
            Same(roster, DiveIds.Estate, 0, 1, 8, 15);
            Same(roster, DiveIds.Park, 2, 3, 9, 10);
            Same(roster, DiveIds.Train, 4, 11);
            Same(roster, DiveIds.Kitchen, 5, 6, 12);
            Same(roster, DiveIds.Classroom, 7, 13, 14);
        }

        static void Same(DiveRoster roster, string place, params int[] which)
        {
            for (var i = 0; i < which.Length; i++)
            {
                Assert.That(roster[which[i]].place, Is.EqualTo(place),
                    which[i] + " 番が " + place + " を指していない");
            }
        }
    }
}
