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

        // 会話は設計書 7 節の写し。どの記憶も名前を呼ばれた瞬間から始まるので、
        // 一行目は点を持たない。点を付けると、記憶に入った瞬間の声がその点まで出なくなる
        [Test]
        public void EveryMemoryOpensWithSomeoneCallingTheName()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                Assert.That(said, Is.Not.Null.And.Not.Empty, i + " 番に会話が無い");
                Assert.That(said[0].Placed, Is.False, i + " 番の一行目に点が付いている");
            }
        }

        // 団地の四本（設計書 7 節の 1・2・9・16）は、二行目から先に点を持つ。
        // オーナーが「まずはマンションのシーンを作りこんで」と決めた四本で、
        // ここが空になっていると一行目だけ出て黙ったままになる
        [Test]
        public void TheEstateFourCarryASpotAfterTheFirstLine()
        {
            var roster = Load();
            foreach (var i in new[] { 0, 1, 8, 15 })
            {
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                    Assert.That(said[k].Placed, Is.True, i + " 番の " + k + " 行目に点が無い");
            }
        }

        // 点は途中で切らさない。一本の記憶の中で、点のある行と無い行が混じると、
        // 無い行で会話が止まり、その先の点へ入っても何も出なくなる
        [Test]
        public void ASpotIsEitherOnEveryLineOrOnNone()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                var placed = 0;
                for (var k = 1; k < said.Length; k++) if (said[k].Placed) placed++;
                Assert.That(placed == 0 || placed == said.Length - 1, Is.True,
                    i + " 番の点が " + placed + " 行ぶんしかない（会話は " + said.Length + " 行）");
            }
        }

        // 点は場所のローカルで置いてある。団地の箱の外へ出ていると、
        // 歩いて届かないところに置いたことになり、その行から先が出なくなる。
        // 定数は `BuildDive` の側にあるが、Editor の組み立てはここから見えないので写してある
        [Test]
        public void TheSpotsStandInsideTheEstate()
        {
            var roster = Load();
            // 階段の井戸の南の端（z -10.0）から居間の奥（z -18.6）まで、
            // 井戸の西（x -1.2）から廊下の東（x 7.8）まで、地面（y 0）から三階の床（y 8.4）まで
            var box = new Bounds();
            box.SetMinMax(new Vector3(-1.5f, -0.5f, -19.5f), new Vector3(8.5f, 9.5f, -9.0f));
            foreach (var i in new[] { 0, 1, 8, 15 })
            {
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                {
                    Assert.That(box.Contains(said[k].where), Is.True,
                        i + " 番の " + k + " 行目の点が団地の外にある " + said[k].where.ToString("F2"));
                    Assert.That(said[k].radius, Is.InRange(0.5f, 2.0f),
                        i + " 番の " + k + " 行目の点の半径が " + said[k].radius);
                }
            }
        }

        // 続く二つの点が重なっていると、歩き出した拍子に二行がまとめて出る。
        // 一歩ぶんは離しておく
        [Test]
        public void EachSpotStandsAStepFromTheOneBefore()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
            {
                var said = roster[i].said;
                for (var k = 2; k < said.Length; k++)
                {
                    if (!said[k].Placed || !said[k - 1].Placed) continue;
                    Assert.That(Vector3.Distance(said[k].where, said[k - 1].where),
                        Is.GreaterThan(0.6f),
                        i + " 番の " + k + " 行目の点が前の点に重なっている");
                }
            }
        }

        // 点は順に armed になる。この決まりが崩れると、置いた点の並びの意味が変わるので、
        // 一覧の点を見るこの組に並べて置いてある
        [Test]
        public void OnlyTheNextSpotIsArmed()
        {
            var said = new[]
            {
                new Said { line = "母「メイ！」" },
                new Said { line = "メイ「えー」", where = new Vector3(0f, 0f, 0f), radius = 1f },
                new Said { line = "母「はい、水筒」", where = new Vector3(10f, 0f, 0f), radius = 1f },
            };
            // 一行目は点を待たずに出る
            Assert.That(DiveEntry.Due(said, 0, new Vector3(50f, 0f, 0f)), Is.EqualTo(0));
            // 先の点の中にいても、手前の点を踏むまでは飛ばない
            Assert.That(DiveEntry.Due(said, 1, new Vector3(10f, 0f, 0f)), Is.EqualTo(-1));
            Assert.That(DiveEntry.Due(said, 1, new Vector3(0.5f, 0f, 0f)), Is.EqualTo(1));
            Assert.That(DiveEntry.Due(said, 2, new Vector3(10f, 0f, 0f)), Is.EqualTo(2));
            // 出し切ったら何も返さない
            Assert.That(DiveEntry.Due(said, 3, Vector3.zero), Is.EqualTo(-1));
        }

        // 点を置いていない行は出ない。団地の四本より先に点を置いていない十二本が、
        // 記憶に入った瞬間に会話を全部吐き出さないための決まり
        [Test]
        public void ALineWithNoSpotStaysUnsaid()
        {
            var said = new[]
            {
                new Said { line = "ローザ「アルベルト、帰りましょう」" },
                new Said { line = "ソフィア「おじいちゃん、手」" },
            };
            Assert.That(DiveEntry.Due(said, 0, Vector3.zero), Is.EqualTo(0));
            Assert.That(DiveEntry.Due(said, 1, Vector3.zero), Is.EqualTo(-1));
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
