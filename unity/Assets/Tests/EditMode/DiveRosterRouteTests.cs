using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 4 の渡り歩きの道が詰まらないかを、書き出した一覧（<c>DiveRoster.asset</c>）で見る。
    /// 設計書 6 節の「どの記憶にも、別の場所の人が一人は見える」の四つと、場所を一方向に渡ること。
    ///
    /// **出る口は見える人しか無い。** 端末が勝手に次を選ぶ作りをやめたので、
    /// 見える人（<see cref="DiveEntry.seen"/>）の飛び先が道のすべてになる。
    /// 公営住宅の四人の間でしか行き来できず、エレナから先へ進めないと差し戻された
    /// </summary>
    public class DiveRosterRouteTests
    {
        const string Path = "Assets/Data/DiveRoster.asset";

        /// <summary>
        /// `切断` が `潜る` と同じ大きさになる人数。<c>DiveDirector.cutAfter</c> の既定値で、
        /// 設計書 12 節の仮の 8。場面の Director は組み立てが置くので、ここからは読めない。
        /// 変えるなら両方を直す
        /// </summary>
        const int CutAfter = 8;

        static DiveRoster Load()
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(Path);
            Assert.That(roster, Is.Not.Null, Path + " が無い。HalfAware/Write the dive roster を走らせる");
            return roster;
        }

        /// <summary>場所の並び。公営住宅 → 公園 → 電車 → 台所 → 教室。前の場所へは戻らない</summary>
        static int Order(string place)
        {
            return System.Array.IndexOf(DiveIds.Places, place);
        }

        /// <summary>
        /// from から辿り着ける記憶と、そこまでに渡る人数。
        /// <paramref name="within"/> を渡すと、その場所の中の記憶だけを通る
        /// </summary>
        static Dictionary<int, int> Reach(DiveRoster roster, int from, string within = null)
        {
            var hops = new Dictionary<int, int> { { from, 0 } };
            var next = new Queue<int>();
            next.Enqueue(from);
            while (next.Count > 0)
            {
                var at = next.Dequeue();
                var seen = roster[at].seen;
                if (seen == null) continue;
                foreach (var who in seen)
                {
                    var to = who.target;
                    if (to < 0 || to >= roster.Count || hops.ContainsKey(to)) continue;
                    if (within != null && roster[to].place != within) continue;
                    hops[to] = hops[at] + 1;
                    next.Enqueue(to);
                }
            }
            return hops;
        }

        static List<int> In(DiveRoster roster, string place)
        {
            var all = new List<int>();
            for (var i = 0; i < roster.Count; i++)
                if (roster[i].place == place) all.Add(i);
            return all;
        }

        // どの記憶も五つの場所のどれかにいる。並びに無い場所があると、以下の試験が素通りする
        [Test]
        public void EveryMemoryStandsInOneOfTheFivePlaces()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
                Assert.That(Order(roster[i].place), Is.GreaterThanOrEqualTo(0),
                    i + " 番の場所 " + roster[i].place + " が DiveIds.Places に無い");
        }

        // 場所の中では今までどおり互いに行き来できる。同じ場所の人を渡るだけで、
        // その場所のどの記憶へも、どの記憶からも辿り着ける
        [Test]
        public void MemoriesInAPlaceReachEachOther()
        {
            var roster = Load();
            foreach (var place in DiveIds.Places)
            {
                var members = In(roster, place);
                Assert.That(members, Is.Not.Empty, place + " に記憶が無い");
                foreach (var from in members)
                {
                    var reach = Reach(roster, from, place);
                    foreach (var to in members)
                        Assert.That(reach.ContainsKey(to), Is.True,
                            place + " の中で " + from + " 番から " + to + " 番へ辿り着けない");
                }
            }
        }

        // 最後の場所（教室）のほかは、どこか一つの記憶から次の場所の人が見える。
        // 無ければその場所で輪が閉じて、そこから先へ出られない
        [Test]
        public void EveryPlaceButTheLastSeesSomeoneFromTheNext()
        {
            var roster = Load();
            for (var k = 0; k < DiveIds.Places.Length - 1; k++)
            {
                var here = DiveIds.Places[k];
                var there = DiveIds.Places[k + 1];
                var found = false;
                foreach (var i in In(roster, here))
                    foreach (var who in roster[i].seen)
                        if (who.target >= 0 && who.target < roster.Count && roster[who.target].place == there)
                            found = true;
                Assert.That(found, Is.True, here + " のどの記憶からも " + there + " の人が見えない");
            }
        }

        // 最初の記憶（メイ）から、十六人全員に辿り着ける
        [Test]
        public void EveryoneIsReachableFromTheFirst()
        {
            var roster = Load();
            var reach = Reach(roster, 0);
            for (var i = 0; i < roster.Count; i++)
                Assert.That(reach.ContainsKey(i), Is.True, "0 番から " + i + " 番へ辿り着けない");
        }

        // 最初の記憶から最後の場所へ着くまでに、必ず `切断` が押せる人数を渡っている。
        // 最後の場所で行き止まっても、帰る口は開いている
        [Test]
        public void TheLastPlaceIsAtLeastCutAfterHopsAway()
        {
            var roster = Load();
            var reach = Reach(roster, 0);
            var last = DiveIds.Places[DiveIds.Places.Length - 1];
            var shortest = int.MaxValue;
            foreach (var i in In(roster, last))
            {
                int hops;
                if (reach.TryGetValue(i, out hops) && hops < shortest) shortest = hops;
            }
            Assert.That(shortest, Is.LessThan(int.MaxValue), "0 番から " + last + " へ辿り着けない");
            Assert.That(shortest, Is.GreaterThanOrEqualTo(CutAfter),
                "0 番から " + last + " まで " + shortest + " 人で着いてしまう。切断が押せるのは " + CutAfter + " 人から");
        }

        // 場所は一方向に渡る。後の場所から前の場所へ戻る道は無い
        // （「つまりプリヤからハンナへのルートは不要」）
        [Test]
        public void NoOneSeenLeadsBackToAnEarlierPlace()
        {
            var roster = Load();
            for (var i = 0; i < roster.Count; i++)
                foreach (var who in roster[i].seen)
                {
                    if (who.target < 0 || who.target >= roster.Count) continue;
                    Assert.That(Order(roster[who.target].place), Is.GreaterThanOrEqualTo(Order(roster[i].place)),
                        i + " 番（" + roster[i].place + "）の " + who.name + " が前の場所 " +
                        roster[who.target].place + " の " + who.target + " 番へ戻る");
                }
        }
    }
}
