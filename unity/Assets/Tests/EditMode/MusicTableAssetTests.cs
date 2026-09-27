using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>BGM の割り当てのアセット（Resources/MusicTable）と、曲の取り込みの設定を確かめる</summary>
    public class MusicTableAssetTests
    {
        /// <summary>場面ごとの BGM。取り込みの設定（Editor の MusicAudioImport）と同じ並び。テストの組からは Editor の組が見えないので写す</summary>
        static readonly string[] Tracks =
        {
            "Assets/Audio/Music/MelancholicAmbient.ogg",
            "Assets/Audio/Music/Parkside.ogg",
            "Assets/Audio/Music/MellowAmbient.ogg",
            "Assets/Audio/Music/Remembrance.ogg",
            "Assets/Audio/Music/Ambient580528.ogg",
            "Assets/Audio/Music/Ambient578724.ogg",
            "Assets/Audio/Music/Cinematic586317.ogg",
        };

        /// <summary>ヤードのラジオの 4 曲（AlleyAudioImport）。同じ置き場にある</summary>
        static readonly string[] Radio =
        {
            "Assets/Audio/Music/SlowCountry.ogg",
            "Assets/Audio/Music/PeachLight.ogg",
            "Assets/Audio/Music/TheOnesWhoStayed.ogg",
            "Assets/Audio/Music/CopperHeart.ogg",
        };

        static MusicTable Load()
        {
            var table = Resources.Load<MusicTable>(MusicTable.Path);
            Assert.IsNotNull(table, "Resources/MusicTable が無い");
            return table;
        }

        [TestCase(MusicCue.Dive, "MelancholicAmbient", 95.94f)]
        [TestCase(MusicCue.DiveLate, "Parkside", 221.2f)]
        [TestCase(MusicCue.Garden, "MellowAmbient", 89.2f)]
        [TestCase(MusicCue.Notice, "Cinematic586317", 145.25f)]
        [TestCase(MusicCue.Drive, "Remembrance", 32.73f)]
        [TestCase(MusicCue.Village, "Ambient580528", 138.4f)]
        [TestCase(MusicCue.Reunion, "Ambient578724", 106.75f)]
        public void EachSceneHasItsTrack(MusicCue cue, string file, float seconds)
        {
            var track = Load().Find(cue);
            Assert.IsNotNull(track, cue + " の曲が当たっていない");
            Assert.AreEqual(file, track.clip.name);
            Assert.IsTrue(track.loop, "どの曲も輪にしてある");
            Assert.That(track.volume, Is.InRange(0.05f, 1f));
            // 長さは読み込む前に読む（Web では読み込みの途中だけ 0 と読める。エディタでは読める）
            Assert.AreEqual(seconds, track.clip.length, 0.02f, "make-ambience.sh の 9 節の輪の長さ");
        }

        [Test]
        public void TheEndingHasNoTrackYet()
        {
            Assert.IsNull(Load().Find(MusicCue.Ending), "エンディングの曲はまだ当てていない。当てたら、この確かめを曲の行へ移す");
        }

        [Test]
        public void TheVillageSceneNameMatchesTheScene()
        {
            var name = Load().villageScene;
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity"), "村の入口の名がシーンの名と合っていない: " + name);
        }

        [Test]
        public void TracksAreImportedForTheWeb()
        {
            foreach (var path in Tracks)
            {
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.IsNotNull(ai, path);
                var d = ai.defaultSampleSettings;
                Assert.IsFalse(d.preloadAudioData, path + " は先読みしない（Web で展開すると大きい）");
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, d.loadType, path);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, d.compressionFormat, path);
                Assert.IsTrue(ai.loadInBackground, path);
                Assert.IsFalse(ai.forceToMono, path + " はステレオのまま");
                Assert.AreEqual(0.6f, d.quality, 1e-4f, path + " はラジオ（0.45）より高く取る");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.AreEqual(2, clip.channels, path);
            }
        }

        [Test]
        public void TheYardRadioKeepsItsOwnSettings()
        {
            foreach (var path in Radio)
            {
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.IsNotNull(ai, path);
                Assert.AreEqual(0.45f, ai.defaultSampleSettings.quality, 1e-4f, path + " はラジオの質のまま（場面の BGM の設定が掛かっていない）");
            }
        }
    }
}
