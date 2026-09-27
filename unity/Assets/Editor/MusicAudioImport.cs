using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面ごとの BGM（<see cref="MusicBed"/> が流す曲）の取り込みの設定。ヤードのラジオの曲（<see cref="AlleyAudioImport"/>）と同じく、
    /// Web（WebGL）で鳴らす前提で決める。
    /// - 読み方は Compressed In Memory。Web では Streaming が使えず、Chromium の系統ではこれも Decompress On Load に替わる。
    ///   展開した曲はブラウザの音の速さ（たいてい 48 kHz）の浮動小数で持つので、ステレオで 1 秒 384 KB（2 分で 46 MB）ほどになる
    /// - だから **Preload Audio Data を切る。** 場面の頭で全部を読まない。場面の演出が要る曲だけを先に読み
    ///   （<see cref="MusicBed.Warm"/>）、鳴らし終えた曲は捨てる（MusicBed が UnloadAudioData する）。
    ///   読み込みの途中は長さが 0 と読めるので、MusicBed は長さを使わず、展開が済んでから鳴らす（<see cref="SoundLoad"/>）
    /// - 読み込みは裏で（Load In Background）。鳴らしたい時に読みが済んでいなければ、MusicBed が済むまで待ってからフェードを始める
    /// - 元のファイルはステレオ 44.1 kHz。場の外で鳴る曲なので畳まず、音の速さも元のまま（−22 LUFS に揃えてある大きさを崩さない）
    /// - 圧縮は Vorbis（Web では形だけ AAC に替わる）。質は走行の輪と同じ所。ラジオの曲（0.45）は帯域を絞った安いスピーカーの音だが、
    ///   こちらは素の曲なので高めに取る
    /// </summary>
    public sealed class MusicAudioImport : AssetPostprocessor
    {
        public const string Folder = AlleyAudioImport.MusicFolder;

        /// <summary>場面ごとの BGM。どれも <c>tools/make-ambience.sh</c> の 9 節で作る</summary>
        public static readonly string[] Tracks =
        {
            Folder + "MelancholicAmbient.ogg",
            Folder + "Parkside.ogg",
            Folder + "MellowAmbient.ogg",
            Folder + "Remembrance.ogg",
            Folder + "Ambient580528.ogg",
            Folder + "Ambient578724.ogg",
            Folder + "Cinematic586317.ogg",
        };

        /// <summary>圧縮の質（0〜1）</summary>
        public const float Quality = 0.6f;

        /// <summary>取り込みの設定を替えたら数を上げる（替えた設定で取り込み直させるため）</summary>
        public override uint GetVersion() { return 1; }

        public static bool IsTrack(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var track in Tracks)
                if (string.Equals(path, track, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void OnPreprocessAudio()
        {
            if (!IsTrack(assetPath)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = false;
            ai.loadInBackground = true;
            ai.ambisonic = false;
            // 既定の設定だけを置く。Web には上書きを置かなくても、この設定の圧縮の形だけが AAC に替わって使われる
            var d = ai.defaultSampleSettings;
            d.loadType = AudioClipLoadType.CompressedInMemory;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = Quality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = false;
            ai.defaultSampleSettings = d;
        }
    }
}
