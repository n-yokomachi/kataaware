using System;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の雑踏の音と、ヤードのラジオの曲の取り込みの設定。Web（WebGL）で鳴らす前提で決める。
    ///
    /// Web では Unity の音は FMOD ではなくブラウザの Web Audio で鳴り、取り込みは AAC になる（Unity のマニュアル「Audio in Web」）。
    /// - 読み方は Compressed In Memory か Decompress On Load の二つだけ。Streaming は使えない（ブラウザが逐次の読み込みを受け付けない）。
    ///   しかも Chromium の系統のブラウザでは、メモリの漏れを避けるため Compressed In Memory も Decompress On Load に替わる（マニュアル「Audio Clip」）。
    ///   展開した曲はブラウザの音の速さ（たいてい 48 kHz）の浮動小数で持つので、4 分のモノラルで 1 曲 45 MB ほどになる
    /// - だから曲は Preload Audio Data を切り、今の曲と次の曲だけを読む（<see cref="StallRadio"/> が LoadAudioData と UnloadAudioData で読み書きする）。
    ///   読み方は Compressed In Memory にする。Chromium の外では圧縮したまま持てて、iOS の消音モードでも鳴る
    /// - 雑踏の音は短い輪で最初から鳴るので、雨の音と同じく Decompress On Load で先に読む
    /// - どちらも元のファイルがモノラルの 22.05 kHz。音の速さも元のまま
    /// </summary>
    public sealed class AlleyAudioImport : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/Audio/Music/";
        public const string CrowdLoop = "Assets/Audio/CrowdLoop.wav";

        /// <summary>曲の並び。ラジオはこの順に流し、最後の曲の後は最初へ戻る</summary>
        public static readonly string[] Tracks =
        {
            MusicFolder + "SlowCountry.ogg",
            MusicFolder + "PeachLight.ogg",
            MusicFolder + "TheOnesWhoStayed.ogg",
            MusicFolder + "CopperHeart.ogg",
        };

        /// <summary>曲の圧縮の質（0〜1）。帯域を絞った安いスピーカーの音なので、高くしても聞き分けられない</summary>
        const float MusicQuality = 0.45f;
        const float CrowdQuality = 0.5f;

        /// <summary>取り込みの設定を替えたら数を上げる（替えた設定で取り込み直させるため）</summary>
        public override uint GetVersion() { return 2; }

        static bool IsMusic(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(MusicFolder, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessAudio()
        {
            var music = IsMusic(assetPath);
            var crowd = string.Equals(assetPath, CrowdLoop, StringComparison.OrdinalIgnoreCase);
            if (!music && !crowd) return;
            var ai = (AudioImporter)assetImporter;
            // 元のファイルがモノラルなので畳まない。Force To Mono で畳むと Normalize が掛かり、揃えてある音量（曲は −18 LUFS）が崩れる
            ai.forceToMono = false;
            ai.loadInBackground = true;
            ai.ambisonic = false;

            // 既定の設定だけを置く。Web には上書きを置かなくても、この設定の圧縮の形だけが AAC に替わって使われる
            // （上書きを持たない音の Web の設定を読むと、読み方・質・先読みは既定のまま、形が AAC になっている）。
            // エディタでも Web と同じ読み方で鳴るので、先読みを切った曲の読み書きをエディタで確かめられる
            var d = ai.defaultSampleSettings;
            d.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            d.compressionFormat = AudioCompressionFormat.Vorbis;
            d.quality = music ? MusicQuality : CrowdQuality;
            d.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            d.preloadAudioData = !music;
            ai.defaultSampleSettings = d;
        }
    }
}
