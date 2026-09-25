using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 4 の記憶に出る人の一覧（<see cref="DiveCast"/>）が、設計書 6 節と 9.5 節に沿っているかを見る
    /// </summary>
    public sealed class DiveCastTests
    {
        const string RosterPath = "Assets/Data/DiveRoster.asset";

        static DiveRoster Roster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(RosterPath);
            Assert.That(roster, Is.Not.Null, RosterPath + " が無い。HalfAware/Write the dive roster を走らせる");
            return roster;
        }

        static Person Of(string id)
        {
            Person p;
            Assert.That(DiveCast.TryById(id, out p), Is.True, id + " が一覧に無い");
            return p;
        }

        [Test]
        public void SixteenPeopleOneForEachMemory()
        {
            var all = DiveCast.People;
            Assert.That(all.Length, Is.EqualTo(16));
            var entries = new HashSet<int>();
            var ids = new HashSet<string>();
            foreach (var p in all)
            {
                Assert.That(entries.Add(p.entry), Is.True, "記憶 " + p.entry + " の人が二人いる");
                Assert.That(ids.Add(p.id), Is.True, p.id + " が二人いる");
                Assert.That(p.entry, Is.InRange(0, 15));
            }
        }

        // 名前・年齢・性別は一覧の右上の行（場面 3 の列と同じ文字）から取る。
        // 食い違えば、潜った先の人と外から見た人が別人に見える
        [Test]
        public void EveryPersonMatchesTheirRow()
        {
            var roster = Roster();
            foreach (var p in DiveCast.People)
            {
                var row = roster[p.entry].row;
                StringAssert.Contains("『" + p.name + "』", row, p.id);
                StringAssert.Contains("　" + p.age + "　", row, p.id + " の年齢");
                StringAssert.StartsWith(p.female ? "女" : "男", row, p.id + " の性別");
            }
        }

        // 板の相手は Seen の飛び先で誰かが決まる。飛び先の人が一覧に無ければ、組み立てで人が立たない
        [Test]
        public void EverySeenPersonIsInTheCast()
        {
            var roster = Roster();
            for (var i = 0; i < roster.Count; i++)
            {
                var seen = roster[i].seen ?? new Seen[0];
                foreach (var s in seen)
                {
                    Person p;
                    Assert.That(DiveCast.TryByEntry(s.target, out p), Is.True,
                        "記憶 " + i + " の " + s.name + " の飛び先 " + s.target + " の人がいない");
                }
            }
        }

        [Test]
        public void OnlyEverydayClothes()
        {
            foreach (var p in DiveCast.People)
            {
                Assert.That(DiveCast.Models, Has.Member(p.model), p.id);
                Assert.That(DiveCast.Unfit, Has.No.Member(p.model), p.id);
            }
        }

        // 模型の部位はどれも役を持つ。持たない部位は肌の色で塗られ、服に穴が空いたように見える
        [Test]
        public void EveryModelPartHasARole()
        {
            foreach (var model in DiveCast.Models)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/quaternius/" + model + ".fbx");
                Assert.That(src, Is.Not.Null, model);
                foreach (var smr in src.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    for (var i = 0; i < smr.sharedMaterials.Length; i++)
                    {
                        Part part;
                        Assert.That(DiveCast.TryPartOf(model, smr.name, i, out part), Is.True,
                            model + "/" + smr.name + "[" + i + "] " + smr.sharedMaterials[i].name);
                    }
                }
            }
        }

        [Test]
        public void EveryPartHasAColour()
        {
            foreach (var p in DiveCast.People)
            {
                Assert.That(p.colors, Is.Not.Null, p.id);
                Assert.That(p.colors.Length, Is.EqualTo(DiveCast.PartCount), p.id);
                foreach (var c in p.colors) Assert.That(c.a, Is.EqualTo(1f), p.id + " の色が透けている");
            }
        }

        // 設計書 6 節の年齢から区分を取る。ジョルジョ 66・エレナ 63・アルベルト 78・ローザ 72 は年寄り。
        // ルーカスは 11 歳（3 歳から上げた。設計メモ 9 節の 8）なので子ども。幼児は一人もいない
        [TestCase("Lucas", AgeBand.Child)]
        [TestCase("Mei", AgeBand.Child)]
        [TestCase("Sofia", AgeBand.Child)]
        [TestCase("Daniel", AgeBand.Teen)]
        [TestCase("Aisha", AgeBand.Teen)]
        [TestCase("Mateo", AgeBand.Teen)]
        [TestCase("Priya", AgeBand.Teen)]
        [TestCase("Emily", AgeBand.Teen)]
        [TestCase("Hanna", AgeBand.Adult)]
        [TestCase("Lee", AgeBand.Adult)]
        [TestCase("Linda", AgeBand.Adult)]
        [TestCase("Mark", AgeBand.Adult)]
        [TestCase("Giorgio", AgeBand.Elder)]
        [TestCase("Elena", AgeBand.Elder)]
        [TestCase("Albert", AgeBand.Elder)]
        [TestCase("Rosa", AgeBand.Elder)]
        public void AgeBandFollowsTheDesign(string id, AgeBand band)
        {
            Assert.That(Of(id).Band, Is.EqualTo(band));
        }

        // 子どもは頭を大きく手足を短く、幼児はさらに寸詰まり。十代は細め、年寄りは背を丸める
        [Test]
        public void BuildFollowsAge()
        {
            var toddler = DiveCast.ProportionOf(AgeBand.Toddler);
            var child = DiveCast.ProportionOf(AgeBand.Child);
            var teen = DiveCast.ProportionOf(AgeBand.Teen);
            var adult = DiveCast.ProportionOf(AgeBand.Adult);
            Assert.That(toddler.head, Is.GreaterThan(child.head));
            Assert.That(child.head, Is.GreaterThan(teen.head));
            Assert.That(teen.head, Is.GreaterThanOrEqualTo(adult.head));
            Assert.That(toddler.leg, Is.LessThan(child.leg));
            Assert.That(child.leg, Is.LessThan(adult.leg));
            Assert.That(toddler.arm, Is.LessThan(child.arm));
            Assert.That(child.arm, Is.LessThan(adult.arm));

            foreach (var p in DiveCast.People)
            {
                switch (p.Band)
                {
                    case AgeBand.Toddler: Assert.Fail(p.id + " が幼児になっている。十六人に幼児はいない"); break;
                    case AgeBand.Child: Assert.That(p.height, Is.InRange(1.05f, 1.45f), p.id); break;
                    case AgeBand.Teen:
                        Assert.That(p.height, Is.InRange(1.50f, 1.85f), p.id);
                        Assert.That(p.girth, Is.LessThan(1f), p.id + " は十代なので細め");
                        break;
                    default: Assert.That(p.height, Is.InRange(1.50f, 1.90f), p.id); break;
                }
                if (p.Band == AgeBand.Elder) Assert.That(p.curl, Is.GreaterThan(0f), p.id + " は背を丸める");
                else Assert.That(p.curl, Is.EqualTo(0f), p.id + " は背を丸めない");
                Assert.That(p.girth, Is.InRange(0.85f, 1.15f), p.id);
            }
            // 公園の兄妹は兄（ルーカス 11）の方が背が高い
            Assert.That(Of("Mei").height, Is.LessThan(Of("Sofia").height));
            Assert.That(Of("Sofia").height, Is.LessThan(Of("Lucas").height));
        }

        // 倫敦の網に繋がった人々は世界に散っている。肌と髪の色もそれに合わせて散らす
        [Test]
        public void SkinAndHairAreSpread()
        {
            var skins = new HashSet<string>();
            var hairs = new HashSet<string>();
            var darkest = 1f;
            var lightest = 0f;
            foreach (var p in DiveCast.People)
            {
                var s = p[Part.Skin];
                skins.Add(Key(s));
                hairs.Add(Key(p[Part.Hair]));
                var l = s.grayscale;
                darkest = Mathf.Min(darkest, l);
                lightest = Mathf.Max(lightest, l);
            }
            Assert.That(skins.Count, Is.GreaterThanOrEqualTo(10), "肌の色が揃いすぎている");
            Assert.That(hairs.Count, Is.GreaterThanOrEqualTo(10), "髪の色が揃いすぎている");
            Assert.That(lightest - darkest, Is.GreaterThan(0.45f), "肌の明るさの幅が狭い");
            var origins = new HashSet<string>();
            foreach (var p in DiveCast.People) origins.Add(p.from);
            Assert.That(origins.Count, Is.GreaterThanOrEqualTo(6), "出身が偏っている");
        }

        static string Key(Color c)
        {
            return Mathf.RoundToInt(c.r * 40f) + "/" + Mathf.RoundToInt(c.g * 40f) + "/" + Mathf.RoundToInt(c.b * 40f);
        }

        // 同じ人は同じ見た目。一覧から二度引いても同じ値が返る
        [Test]
        public void SamePersonSameLook()
        {
            foreach (var p in DiveCast.People)
            {
                Person again;
                Assert.That(DiveCast.TryByEntry(p.entry, out again), Is.True);
                Assert.That(again.model, Is.EqualTo(p.model));
                Assert.That(again.height, Is.EqualTo(p.height));
                Assert.That(again.girth, Is.EqualTo(p.girth));
                CollectionAssert.AreEqual(p.colors, again.colors, p.id);
            }
        }
    }
}
