using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        // 会話は設計書 7 節の写し。どの記憶も名前を呼ばれた瞬間から始まる。
        // 一行目は記憶に入った瞬間に勝手に出る声なので、相手を持たない
        [Test]
        public void EveryMemoryOpensWithSomeoneCallingTheName()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                Assert.That(said, Is.Not.Null.And.Not.Empty, i + " 番に会話が無い");
                Assert.That(said[0].Partnered, Is.False, i + " 番の一行目に相手が付いている");
            }
        }

        // 会話の相手は、その記憶で板の出る人（seen）のどれか。
        // 名前が一字でも違えば、目を留めても `E　話す` が出ず、会話が始まらないまま板も出ない
        [Test]
        public void EveryPartnerIsSomeoneSeenInTheMemory()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var seen in roster[i].seen) names.Add(seen.name);
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                {
                    if (!said[k].Partnered) continue;
                    Assert.That(names, Does.Contain(said[k].partner),
                        i + " 番の " + k + " 行目の相手 " + said[k].partner + " がその記憶にいない");
                }
            }
        }

        // 団地の四本（設計書 7 節の 1・2・9・16）は、二行目から全部に相手を持つ。
        // オーナーが「まずはマンションのシーンを作りこんで」と決めた四本
        [Test]
        public void TheEstateFourTalkFromTheSecondLine()
        {
            var roster = Load();
            foreach (var i in new[] { 0, 1, 8, 15 })
            {
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                    Assert.That(said[k].Partnered, Is.True, i + " 番の " + k + " 行目に相手が無い");
            }
        }

        // メイは母と、ジョルジョは妻と、エレナは夫と、一つの会話だけを交わす
        [Test]
        public void MeiGiorgioAndElenaEachHaveOneTalk()
        {
            var roster = Load();
            One(roster, 0, "Mother");
            One(roster, 8, "Wife");
            One(roster, 15, "Husband");
        }

        // ハンナは隣の老人と三行、次に娘と四行。娘は老人との最後の行
        // （四行目、ハンナ「ええ、午後からで」）が出たら階段を上がり始める（Mover の合図 4）。
        // 並びが入れ替わると、まだ上がってきていない娘に話しかけることになる
        [Test]
        public void HannaTalksToTheNeighbourAndThenToTheDaughter()
        {
            var talks = DiveEntry.Exchanges(Load()[1].said);
            Assert.That(talks.Length, Is.EqualTo(2));
            Assert.That(talks[0].partner, Is.EqualTo("Neighbour"));
            Assert.That(talks[0].lines, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(talks[1].partner, Is.EqualTo("Daughter"));
            Assert.That(talks[1].lines, Is.EqualTo(new[] { 4, 5, 6, 7 }));
        }

        // 残りの十二本はまだ相手を付けていない。一行目だけ出て、板はすぐ出る
        [Test]
        public void TheOtherTwelveHaveNoTalkYet()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                if (i == 0 || i == 1 || i == 8 || i == 15) continue;
                var talks = DiveEntry.Exchanges(roster[i].said);
                Assert.That(DiveEntry.AllDone(talks, 0), Is.True, i + " 番に会話が付いている");
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

        static void One(DiveRoster roster, int i, string partner)
        {
            var talks = DiveEntry.Exchanges(roster[i].said);
            Assert.That(talks.Length, Is.EqualTo(1), i + " 番の会話の数");
            Assert.That(talks[0].partner, Is.EqualTo(partner), i + " 番の会話の相手");
            Assert.That(talks[0].lines.Length, Is.EqualTo(roster[i].said.Length - 1), i + " 番の会話の行数");
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
