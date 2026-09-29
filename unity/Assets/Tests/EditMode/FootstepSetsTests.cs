using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 足音の組（tools/make-ambience.sh の 6 節と 11 節で切り出した単発。自室のラグの Rug は 11-4）と、場面 4 の場所ごとの既定の足音
    /// </summary>
    public class FootstepSetsTests
    {
        static void CheckSet(string name, int count)
        {
            for (var i = 1; i <= count; i++)
            {
                var path = "Assets/Audio/" + name + i + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, path);
                Assert.AreEqual(1, clip.channels, path + " はモノラル");
                Assert.AreEqual(44100, clip.frequency, path + " は 44.1kHz");
                // 一歩の単発で、余韻は次の一歩の手前まで。歩きの一歩（0.63 秒）を大きく越えない
                Assert.That(clip.length, Is.InRange(0.30f, 0.75f), path + " の長さ");
            }
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + (count + 1) + ".wav"),
                name + " は " + count + " 本");
        }

        [Test]
        public void TheHardFloorSetHasSevenSteps() { CheckSet("HardFloor", 7); }

        [Test]
        public void TheConcreteSetKeepsFourStepsForTheVillage() { CheckSet("Concrete", 4); }

        [Test]
        public void TheRoomSetHasSixSteps() { CheckSet("Room", 6); }

        [Test]
        public void TheRugSetHasSixSteps() { CheckSet("Rug", 6); }

        [Test]
        public void TheGravelSetKeepsSixStepsForTheVillage() { CheckSet("Gravel", 6); }

        [Test]
        public void EachPlaceFindsItsOwnSteps()
        {
            var a = AudioClip.Create("a", 441, 1, 44100, false);
            var b = AudioClip.Create("b", 441, 1, 44100, false);
            try
            {
                var table = new[]
                {
                    new DiveDirector.PlaceSteps { place = DiveIds.Estate, clips = new[] { a } },
                    new DiveDirector.PlaceSteps { place = DiveIds.Park, clips = new[] { b } },
                };
                Assert.AreSame(a, DiveDirector.StepsFor(DiveIds.Estate, table)[0]);
                Assert.AreSame(b, DiveDirector.StepsFor(DiveIds.Park, table)[0]);
                Assert.AreEqual(0, DiveDirector.StepsFor(DiveIds.Train, table).Length, "表に無い場所は空");
                Assert.AreEqual(0, DiveDirector.StepsFor(DiveIds.Train, null).Length);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
