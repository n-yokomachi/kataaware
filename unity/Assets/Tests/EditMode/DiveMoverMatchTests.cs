using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 思い出した時に、動く物の合図の秒を名前で当てる（DiveDirector.Match）。
    /// 記憶を組み直して動く物の数が変わっても（公園の記憶に鳩を足した、2026-09-27）、合う物には当たる
    /// </summary>
    public class DiveMoverMatchTests
    {
        [Test]
        public void CuesFollowTheirNamesWhenTheCountChanges()
        {
            var savedNames = new[] { "Grandfather", "Grandmother", "Doves/Pigeon0" };
            var saved = new[] { 12f, -1f, 20f };
            var nowNames = new[] { "Grandfather", "Doves/Pigeon0", "Doves/Pigeon1", "Grandmother", "Passerby" };
            var into = new[] { -1f, -1f, -1f, -1f, -1f };
            DiveDirector.Match(savedNames, saved, nowNames, into);
            CollectionAssert.AreEqual(new[] { 12f, 20f, -1f, -1f, -1f }, into, "合う物だけに当たり、新しい物は合図の前のまま");
        }

        [Test]
        public void AnOldCopyWithoutNamesOnlyFitsTheSameCount()
        {
            var same = new[] { -1f, -1f };
            DiveDirector.Match(null, new[] { 3f, 4f }, new[] { "a", "b" }, same);
            CollectionAssert.AreEqual(new[] { 3f, 4f }, same, "名前の無い写しは、数が同じなら並びのまま");
            var other = new[] { -1f, -1f, -1f };
            DiveDirector.Match(new string[0], new[] { 3f, 4f }, new[] { "a", "b", "c" }, other);
            CollectionAssert.AreEqual(new[] { -1f, -1f, -1f }, other, "数が違えば当てない");
        }

        [Test]
        public void NamesAreThePathUnderTheMemoryAndRepeatsAreCounted()
        {
            var root = new GameObject("DiveMoverMatchTests.Take");
            try
            {
                var doves = new GameObject("Doves").transform;
                doves.SetParent(root.transform, false);
                var a = new GameObject("Pigeon").AddComponent<Mover>();
                a.transform.SetParent(doves, false);
                var b = new GameObject("Pigeon").AddComponent<Mover>();
                b.transform.SetParent(doves, false);
                var man = new GameObject("Grandfather").AddComponent<Mover>();
                man.transform.SetParent(root.transform, false);
                var names = DiveDirector.Names(root.transform, new[] { man, a, b });
                CollectionAssert.AreEqual(new[] { "Grandfather", "Doves/Pigeon", "Doves/Pigeon#1" }, names);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
