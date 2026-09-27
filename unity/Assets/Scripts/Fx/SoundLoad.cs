using System;
using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 音の長さを読む所と、読み込んですぐ鳴らす所の、Web（WebGL）での手当て。
    ///
    /// Web では音の中身をブラウザが非同期で展開する（Web Audio の decodeAudioData）。書き出した物で確かめると、
    /// - 読み込む前（<see cref="AudioDataLoadState.Unloaded"/>）と展開の後（Loaded）は、<see cref="AudioClip.length"/> は正しい
    /// - **読み込みを始めてから展開が済むまで（Loading）の間だけ、length が 0 を返す。**
    ///   ブラウザのコンソールに「Trying to get length of sound which is not loaded yet.」が出る
    /// - その間に鳴らすと、鳴り出しが展開の後へ延びる。鳴らしてすぐ <see cref="AudioSource.time"/> で位置を決めても効かず、頭から鳴る
    ///
    /// 先読み（Preload Audio Data）を切った音は、鳴らした時に読み込みが始まる。だから「鳴らしてから長さを読む」と 0 になる。
    /// ここでは、長さを読み込みの前に読んで持っておく（<see cref="Seconds"/>）、展開を待ってから鳴らす（<see cref="Ready"/>・<see cref="Wait"/>）、
    /// 先に展開を始めておく（<see cref="Warm"/>）の三つを用意する。
    ///
    /// なお、Unity は音を読み込むその場で自分でも長さを問うので、読み込み一回につき一件は同じ行が出る。これはゲームの側では消せない。
    ///
    /// **エディタとスタンドアロンの動きは変えない。** そちらでは読み込みが鳴らす時にその場で済むので、
    /// <see cref="Ready"/> はいつも真、<see cref="Warm"/> は何もせず、<see cref="Seconds"/> は length をそのまま返す
    /// </summary>
    public static class SoundLoad
    {
        /// <summary>展開を待つ上限。実時間の秒。読めないまま待ち続けないように</summary>
        public const float WaitLimit = 10f;

        /// <summary>
        /// 鳴らしてよいか・長さを読んでよいか。Web では展開が済んだか、読めないと決まった時だけ真。
        /// まだ読み始めていなければ読み始める。Web のほかではいつも真
        /// </summary>
        public static bool Ready(AudioClip clip)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (clip == null) return true;
            switch (clip.loadState)
            {
                case AudioDataLoadState.Unloaded:
                    clip.LoadAudioData();
                    return false;
                case AudioDataLoadState.Loading:
                    return false;
                default:
                    return true;
            }
#else
            return true;
#endif
        }

        /// <summary>
        /// 展開を始めておく。場面の頭で呼び、鳴らす時には展開が済んでいるようにする。
        /// 長さを <see cref="Seconds"/> で持っておく音は、先に長さを読んでから呼ぶ（展開の途中は長さが読めない）。
        /// Web のほかでは何もしない
        /// </summary>
        public static void Warm(params AudioClip[] clips)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (clips == null) return;
            foreach (var clip in clips)
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
#endif
        }

        /// <summary>
        /// 長さ。秒。known には読めた値を入れておき、Web で展開の途中なら読まずにそれを返す（まだ一度も読めていなければ 0）。
        /// Web のほかでは length をそのまま返す
        /// </summary>
        public static float Seconds(AudioClip clip, ref float known)
        {
            if (clip == null) return 0f;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (clip.loadState == AudioDataLoadState.Loading) return known;
            known = clip.length;
            return known;
#else
            return clip.length;
#endif
        }

        /// <summary>
        /// 展開が済むまで待つ。上限は <see cref="WaitLimit"/> 秒（実時間）。eachFrame は待つ間の毎フレームに呼ぶ（止めを掛け続けるなど）。
        /// **呼ぶ側は <c>if (!SoundLoad.Ready(clip)) yield return SoundLoad.Wait(clip)</c> の形で呼ぶ。**
        /// 入れ子のコルーチンは、中で一度も止まらなくても抜けるのが次のフレームになることがあり、エディタの時刻がずれる
        /// </summary>
        public static IEnumerator Wait(AudioClip clip, Action eachFrame = null)
        {
            var until = Time.realtimeSinceStartup + WaitLimit;
            while (!Ready(clip) && Time.realtimeSinceStartup < until)
            {
                if (eachFrame != null) eachFrame();
                yield return null;
            }
        }
    }
}
