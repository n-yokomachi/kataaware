using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// エンディングの曲「HALF AWARE」の二つの版の取り込みの設定（<c>tools/make-ambience.sh</c> の 10 節で作る）。
    /// 場面ごとの BGM（<see cref="MusicAudioImport"/>）と同じく、Web で鳴らす前提で決める。
    /// - 読み方は Compressed In Memory。Web では Streaming が使えない
    /// - **Preload Audio Data を切る。** 場面の頭で読まず、EndingDirector が黒のあいだ（イグニッションの間）に読む。
    ///   二つとも展開が済んでから同じフレームに鳴らし始める（片方だけ遅れて鳴ると、入れ替えた所で曲がずれる）
    /// - 読み込みは裏で（Load In Background）
    /// - 素の版はステレオ 44.1 kHz のまま。車の版はモノラル 22.05 kHz（古い車のスピーカーの音なので、帯域も狭い）
    /// </summary>
    public sealed class EndingAudioImport : AssetPostprocessor
    {
        public const string Folder = AlleyAudioImport.MusicFolder;

        /// <summary>素の版。ドロップから後はこれだけが鳴る</summary>
        public const string OpenPath = Folder + "HalfAware.ogg";

        /// <summary>車の版。明けてからドロップの手前まで</summary>
        public const string CarPath = Folder + "HalfAwareCar.ogg";

        /// <summary>圧縮の質（0〜1）。素の版は場面ごとの BGM と同じ所、車の版はヤードのラジオの曲と同じ所</summary>
        public const float OpenQuality = 0.6f;
        public const float CarQuality = 0.45f;

        /// <summary>取り込みの設定を替えたら数を上げる</summary>
        public override uint GetVersion() { return 1; }

        public static bool IsSong(string path)
        {
            return string.Equals(path, OpenPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, CarPath, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessAudio()
        {
            if (!IsSong(assetPath)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = false;
            ai.loadInBackground = true;
            ai.ambisonic = false;
            var d = ai.defaultSampleSettings;
            d.loadType = AudioClipLoadType.CompressedInMemory;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = string.Equals(assetPath, CarPath, StringComparison.OrdinalIgnoreCase) ? CarQuality : OpenQuality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = false;
            ai.defaultSampleSettings = d;
        }
    }
}
