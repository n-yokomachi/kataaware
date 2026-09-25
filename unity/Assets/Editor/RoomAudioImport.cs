using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の空気の音（<see cref="RoomTone"/>）の取り込みの設定。場面 2 の雑踏の輪（<see cref="AlleyAudioImport"/>）と同じ考え方で、
    /// Web（WebGL）で鳴らす前提で決める。
    /// - 短い輪で場面の頭から鳴るので、Decompress On Load で先に読む（Web では Streaming が使えず、
    ///   Chromium の系統では Compressed In Memory も Decompress On Load に替わる）
    /// - 圧縮は Vorbis。Web では形だけ AAC に替わって使われる
    /// - 元のファイルがモノラルの 22.05 kHz なので畳まず、音の速さも元のまま（揃えてある実効値 −24dBFS を崩さない）
    /// </summary>
    public sealed class RoomAudioImport : AssetPostprocessor
    {
        public const string TonePath = "Assets/Audio/RoomTone.wav";

        /// <summary>空気の音の圧縮の質（0〜1）。雑踏の輪と同じ</summary>
        const float Quality = 0.5f;

        /// <summary>取り込みの設定を替えたら数を上げる（替えた設定で取り込み直させるため）</summary>
        public override uint GetVersion() { return 1; }

        void OnPreprocessAudio()
        {
            if (!string.Equals(assetPath, TonePath, StringComparison.OrdinalIgnoreCase)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = false;
            ai.loadInBackground = true;
            ai.ambisonic = false;
            var d = ai.defaultSampleSettings;
            d.loadType = AudioClipLoadType.DecompressOnLoad;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = Quality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = true;
            ai.defaultSampleSettings = d;
        }
    }
}
