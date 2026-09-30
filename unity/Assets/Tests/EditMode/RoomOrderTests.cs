using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 1 の調べる順（<see cref="RoomIds.After"/>）と、吸い終えた後に立ち続けた煙を止める時（<see cref="RoomIntroDirector.SnuffNow"/>）。
    /// 2026-09-29 にモニターをジャケットの前に移した（オーナー「モニターへのインタラクトを必須にし、モニターへのインタラクトが終わったら
    /// 煙草の煙を止め、ジャケットへのインタラクトを有効化、という順にしよう」）
    /// </summary>
    public class RoomOrderTests
    {
        [Test]
        public void TheMonitorComesAfterTheCigaretteAndBeforeTheJacket()
        {
            CollectionAssert.AreEqual(new[] { RoomIds.Jack }, RoomIds.After(RoomIds.Cigarette));
            CollectionAssert.AreEqual(new[] { RoomIds.Cigarette }, RoomIds.After(RoomIds.Terminal));
            CollectionAssert.AreEqual(new[] { RoomIds.Terminal }, RoomIds.After(RoomIds.Jacket));
        }

        [Test]
        public void TheWalkingItemsComeAfterTheJacketAndTheDoorLast()
        {
            CollectionAssert.AreEqual(new[] { RoomIds.Jacket }, RoomIds.After(RoomIds.Chips));
            CollectionAssert.AreEqual(new[] { RoomIds.Jacket }, RoomIds.After(RoomIds.Clipboard));
            // ドアは前提の先頭に、文を持たないジャケットを置く（着るまで印を出さない）
            CollectionAssert.AreEqual(new[] { RoomIds.Jacket, RoomIds.Chips, RoomIds.Terminal }, RoomIds.After(RoomIds.Door));
            CollectionAssert.AreEqual(new[] { RoomIds.Cigarette }, RoomIds.After(RoomIds.Ashtray));
            CollectionAssert.AreEqual(new[] { RoomIds.Cigarette }, RoomIds.After(RoomIds.CigaretteBox));
            Assert.IsEmpty(RoomIds.After(RoomIds.Jack), "始めはジャックだけ");
        }

        [Test]
        public void EveryItemCanBeReachedInOrder()
        {
            // 前提は文面のアセットにある id だけで、輪になっていない
            var done = new HashSet<string>();
            for (var round = 0; round < RoomIds.All.Length && done.Count < RoomIds.All.Length; round++)
                foreach (var id in RoomIds.All)
                {
                    if (done.Contains(id)) continue;
                    var ready = true;
                    foreach (var a in RoomIds.After(id))
                    {
                        CollectionAssert.Contains(RoomIds.All, a, id + " の前提");
                        if (!done.Contains(a)) ready = false;
                    }
                    if (ready) done.Add(id);
                }
            Assert.AreEqual(RoomIds.All.Length, done.Count, "どれも順に済ませられる");
        }

        [Test]
        public void SeatedAfterSmokingTheMonitorIsThereButTheJacketIsNot()
        {
            // 煙草を吸い終えた座ったままの所。モニターは狙えて、ジャケットはモニターの後まで印が出ない
            var eye = new Vector3(1.5f, 1.309f, 1.42f);
            var monitor = new FakeItem(RoomIds.Terminal, new Vector3(1.5f, 1.1f, 2.42f)) { After = RoomIds.After(RoomIds.Terminal) };
            var jacket = new FakeItem(RoomIds.Jacket, new Vector3(2.43f, 0.8f, 1.42f)) { After = RoomIds.After(RoomIds.Jacket), Radius = 1.5f };
            var items = new IInteractable[] { monitor, jacket };
            var smoked = new HashSet<string> { RoomIds.Jack, RoomIds.Cigarette };
            Assert.AreSame(monitor, InteractionPicker.Select(eye, (monitor.Position - eye).normalized, items, smoked));
            Assert.IsNull(InteractionPicker.Select(eye, (jacket.Position - eye).normalized, items, smoked), "モニターの前はジャケットを出さない");
            var before = new HashSet<string> { RoomIds.Jack };
            Assert.IsNull(InteractionPicker.Select(eye, (monitor.Position - eye).normalized, items, before), "吸う前はモニターを出さない");
            smoked.Add(RoomIds.Terminal);
            Assert.AreSame(jacket, InteractionPicker.Select(eye, (jacket.Position - eye).normalized, items, smoked));
        }

        [Test]
        public void TheSmokeStopsOnlyAfterTheMonitorIsDoneAndRead()
        {
            Assert.IsTrue(RoomIntroDirector.SnuffNow(true, false, true, false, false));
            Assert.IsFalse(RoomIntroDirector.SnuffNow(true, false, false, false, false), "モニターがまだ（「いいえ」を含む）");
            Assert.IsFalse(RoomIntroDirector.SnuffNow(true, false, true, true, false), "「はい」の後の文を読んでいる間");
            Assert.IsFalse(RoomIntroDirector.SnuffNow(true, false, true, false, true), "二択を出している間");
            Assert.IsFalse(RoomIntroDirector.SnuffNow(true, true, true, false, false), "吸っている間");
            Assert.IsFalse(RoomIntroDirector.SnuffNow(false, false, true, false, false), "立っていない煙は止めない");
        }
    }
}
