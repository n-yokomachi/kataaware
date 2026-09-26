using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 村の環境音の輪（朝の村・麦の風）と格子戸の音の取り込みの設定。場面 2 の雑踏の輪（<see cref="AlleyAudioImport"/>）・
    /// 自室の空気の音（<see cref="RoomAudioImport"/>）と同じ考え方で、Web（WebGL）で鳴らす前提で決める。
    /// - 輪は村（<c>Village.unity</c>）と場面 8 の麦畑の頭から鳴るので、Decompress On Load で先に読む
    ///   （Web では Streaming が使えず、Chromium の系統では Compressed In Memory も Decompress On Load に替わる）
    /// - 格子戸の音は短い単発。調べた瞬間に鳴らしたいので、これも Decompress On Load で先に読む
    /// - 圧縮は Vorbis。Web では形だけ AAC に替わって使われる
    /// - 元のファイルがモノラル（輪は 22.05 kHz、格子戸は 44.1 kHz）なので畳まず、音の速さも元のまま
    ///   （揃えてある実効値 −24dBFS と、格子戸の頂点 −6dB を崩さない）
    /// </summary>
    public sealed class VillageAudioImport : AssetPostprocessor
    {
        public const string MorningPath = "Assets/Audio/VillageMorning.wav";
        public const string WheatPath = "Assets/Audio/WheatWind.wav";
        public const string GatePath = "Assets/Audio/GateCreak.wav";

        /// <summary>輪の圧縮の質（0〜1）。雑踏の輪・自室の空気の音と同じ</summary>
        const float LoopQuality = 0.5f;
        /// <summary>格子戸の圧縮の質。軋みの高い倍音を崩さないよう、輪より高く取る</summary>
        const float GateQuality = 0.8f;

        /// <summary>取り込みの設定を替えたら数を上げる（替えた設定で取り込み直させるため）</summary>
        public override uint GetVersion() { return 1; }

        static bool Is(string path, string target)
        {
            return string.Equals(path, target, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessAudio()
        {
            var loop = Is(assetPath, MorningPath) || Is(assetPath, WheatPath);
            var gate = Is(assetPath, GatePath);
            if (!loop && !gate) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = false;
            ai.loadInBackground = loop;
            ai.ambisonic = false;
            var d = ai.defaultSampleSettings;
            d.loadType = AudioClipLoadType.DecompressOnLoad;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = loop ? LoopQuality : GateQuality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = true;
            ai.defaultSampleSettings = d;
        }
    }
}
