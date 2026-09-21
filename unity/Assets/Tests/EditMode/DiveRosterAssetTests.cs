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

        // 会話は設計書 7 節の写し。どの記憶も名前を呼ばれた瞬間から始まるので、
        // 一行目は必ず 0 秒にある
        [Test]
        public void EveryMemoryOpensWithSomeoneCallingTheName()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                Assert.That(said, Is.Not.Null.And.Not.Empty, i + " 番に会話が無い");
                Assert.That(said[0].at, Is.EqualTo(0f).Within(1e-3f), i + " 番の一行目が 0 秒にない");
            }
        }

        // 秒が前後していると、後の行を出した後で前の行が出ることになる。
        // 記憶が尽きた後に置かれた行はそのまま出ずに終わる
        [Test]
        public void TheLinesRunInOrderAndFitInsideTheMemory()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                    Assert.That(said[k].at, Is.GreaterThanOrEqualTo(said[k - 1].at),
                        i + " 番の " + k + " 行目が前の行より前にある");
                Assert.That(said[said.Length - 1].at, Is.LessThan(roster[i].length),
                    i + " 番の終いの行が記憶の長さ " + roster[i].length + " 秒を越えている");
            }
        }

        // 顔は見せないので、誰が喋っているかは話者の名前と鉤括弧でしか伝わらない。
        // 独白は入れない決まりなので、鉤括弧の無い行があってはいけない
        [Test]
        public void EveryLineNamesWhoIsSpeaking()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                for (var k = 0; k < said.Length; k++)
                {
                    var line = said[k].line;
                    Assert.That(line, Is.Not.Null.And.Not.Empty, i + " 番の " + k + " 行目が空");
                    var open = line.IndexOf('「');
                    Assert.That(open, Is.GreaterThan(0), i + " 番の " + k + " 行目に話者が無い: " + line);
                    Assert.That(line.EndsWith("」"), Is.True,
                        i + " 番の " + k + " 行目が鉤括弧で閉じていない: " + line);
                }
            }
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
