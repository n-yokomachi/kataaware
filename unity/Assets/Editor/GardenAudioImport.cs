using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 6（庭の記憶）の音の取り込みの設定。村の音（<see cref="VillageAudioImport"/>）と同じ考え方で、Web（WebGL）で鳴らす前提で決める。
    /// - どれも場面の頭から要る（水の音は頭から、途切れの音は名を呼ぶ口元に合わせて鳴る）ので、Decompress On Load で先に読む
    /// - 圧縮は Vorbis。水の輪は村の輪と同じ質、単発は倍音を崩さないよう高く取る
    /// - 元のファイルはどれもモノラル 44.1kHz なので畳まず、音の速さも元のまま（揃えてある実効値と頂点を崩さない）
    /// </summary>
    public sealed class GardenAudioImport : AssetPostprocessor
    {
        public const string HosePath = "Assets/Audio/GardenHose.wav";
        public const string HoseStopPath = "Assets/Audio/GardenHoseStop.wav";
        public const string GlitchPath = "Assets/Audio/SignalGlitch.wav";
        public const string CutPath = "Assets/Audio/SignalCut.wav";

        const float LoopQuality = 0.5f;
        const float ShotQuality = 0.8f;

        /// <summary>取り込みの設定を替えたら数を上げる</summary>
        public override uint GetVersion() { return 1; }

        static bool Is(string path, string target)
        {
            return string.Equals(path, target, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessAudio()
        {
            var loop = Is(assetPath, HosePath);
            var shot = Is(assetPath, HoseStopPath) || Is(assetPath, GlitchPath) || Is(assetPath, CutPath);
            if (!loop && !shot) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = false;
            ai.loadInBackground = loop;
            ai.ambisonic = false;
            var d = ai.defaultSampleSettings;
            d.loadType = AudioClipLoadType.DecompressOnLoad;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = loop ? LoopQuality : ShotQuality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = true;
            ai.defaultSampleSettings = d;
        }
    }
}
