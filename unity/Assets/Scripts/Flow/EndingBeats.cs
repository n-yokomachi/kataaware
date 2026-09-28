using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// エンディング（シナリオ設計 13 節）の段取りの秒。絵に依らない計算だけを持ち、EndingDirector が音と画面へ繋ぐ。
    ///
    /// 秒は二つの時計で持つ。
    /// <list type="bullet">
    /// <item>**場面の時計**: 場面を読んだ所から。黒のあいだ（鍵の音）だけ使う</item>
    /// <item>**曲の時計**: 曲「HALF AWARE」の再生の位置。元の曲の秒のまま（make-ambience.sh の 10 節）。
    /// 明けから後は、目・切り替え・クレジット・終わりのフェードをすべてこちらで数える。
    /// 曲とずれない（コンソールを開いて曲を止めても、戻れば同じ所から続く）</item>
    /// </list>
    /// **値はどれも仮置き。** 組み立て（BuildEnding）が書き、オーナーが Inspector で詰める
    /// </summary>
    [Serializable]
    public sealed class EndingBeats
    {
        [Header("黒のあいだ（場面の時計）")]
        [Tooltip("場面の頭から、イグニッションの音を鳴らすまで。秒。黒のまま音の無い間")]
        public float ignitionAt = 0.8f;
        [Tooltip("イグニッションを鳴らしてから明け始めるまで。秒。曲と走行音もここから鳴らし始める。" +
            "Ignition.wav は 4.0 秒でエンジンが掛かり、6.5 秒から先は掛かったままの繰り返し")]
        public float openAfter = 6.4f;
        [Tooltip("明けてから、イグニッションの残りを消しきるまで。秒")]
        public float ignitionFade = 1.5f;

        [Header("曲の時計（曲の秒）")]
        [Tooltip("黒から明けきるまで。秒。曲の頭から")]
        public float openFade = 2.0f;
        [Tooltip("目が助手席の片割れへ向き始める曲の秒（明けてから 5 秒）")]
        public float glanceAt = 5.0f;
        [Tooltip("目を回す秒。PlayerController.Face と同じ 1 秒のなめらかな回り")]
        public float glanceTurn = PlayerController.FaceSeconds;
        [Tooltip("片割れの方を向いたまま留まる秒")]
        public float glanceHold = 2.2f;
        [Tooltip("車の版から素の版へ入れ替え始める曲の秒。ドロップの前の溜め（35.18〜36.0 秒、低音が抜けている）の中")]
        public float switchFrom = 35.90f;
        [Tooltip("入れ替えと、走行音を消すのにかける秒。ドロップの頭の打ちより前に済ませる")]
        public float switchFade = 0.06f;
        [Tooltip("ドロップの頭の打ちの曲の秒（波形から。make-ambience.sh の 10 節）")]
        public float dropAt = 36.012f;
        [Tooltip("クレジットが画面の下の縁から入り始める曲の秒")]
        public float creditsFrom = 36.0f;
        [Tooltip("クレジットの最後の行が流す所の真ん中に着く曲の秒。そこで止まり、フェードアウトまで留まる")]
        public float creditsTo = 200.0f;
        [Tooltip("フェードアウトを始める曲の秒。最後の打ちは 207.4 秒")]
        public float fadeOutFrom = 207.5f;
        [Tooltip("黒になりきる曲の秒。曲の尾は 212.5 秒で −60dB")]
        public float fadeOutTo = 212.5f;
        [Tooltip("黒になりきってから、タイトルの画面へ戻るまで。秒")]
        public float titleAfter = 1.5f;

        /// <summary>明け始める場面の秒</summary>
        public float OpenAt { get { return ignitionAt + openAfter; } }

        /// <summary>素の版の大きさの割合。0 は車の版だけ、1 は素の版だけ。走行音は 1 から引いた値</summary>
        public float Open(float song)
        {
            if (switchFade <= 0f) return song >= switchFrom ? 1f : 0f;
            return Mathf.Clamp01((song - switchFrom) / switchFade);
        }

        /// <summary>走行音の大きさの割合。入れ替えと同じ時刻に消える</summary>
        public float Road(float song)
        {
            return 1f - Open(song);
        }

        /// <summary>
        /// 画面を覆う黒の濃さ。曲の頭より前は黒、頭から openFade 秒で明け、fadeOutFrom から fadeOutTo でまた黒へ
        /// </summary>
        public float Cover(float song)
        {
            if (song < 0f) return 1f;
            if (song >= fadeOutTo) return 1f;
            if (song >= fadeOutFrom)
                return fadeOutTo > fadeOutFrom ? Mathf.Clamp01((song - fadeOutFrom) / (fadeOutTo - fadeOutFrom)) : 1f;
            if (openFade <= 0f) return 0f;
            return 1f - Mathf.Clamp01(song / openFade);
        }

        /// <summary>
        /// 目が片割れへ向いている割合。0 は前、1 は片割れ。回るあいだは PlayerController.Face と同じなめらかさ（<see cref="Gaze.Ease"/>）
        /// </summary>
        public float Glance(float song)
        {
            var t = song - glanceAt;
            if (t <= 0f) return 0f;
            if (t < glanceTurn) return Gaze.Ease(t / glanceTurn);
            t -= glanceTurn;
            if (t < glanceHold) return 1f;
            t -= glanceHold;
            if (t < glanceTurn) return 1f - Gaze.Ease(t / glanceTurn);
            return 0f;
        }

        /// <summary>目が前へ戻りきる曲の秒</summary>
        public float GlanceEnd { get { return glanceAt + glanceTurn * 2f + glanceHold; } }

        /// <summary>クレジットの流れの割合。0 は最初の行が画面の下の縁、1 は最後の行が流す所の真ん中</summary>
        public float Roll(float song)
        {
            if (creditsTo <= creditsFrom) return song >= creditsFrom ? 1f : 0f;
            return Mathf.Clamp01((song - creditsFrom) / (creditsTo - creditsFrom));
        }

        /// <summary>クレジットを出しているか（入り始めてから、黒になりきるまで）</summary>
        public bool Rolling(float song)
        {
            return song >= creditsFrom && song < fadeOutTo;
        }

        /// <summary>タイトルの画面へ戻る曲の秒</summary>
        public float EndAt { get { return fadeOutTo + titleAfter; } }

        /// <summary>終わったか</summary>
        public bool Finished(float song)
        {
            return song >= EndAt;
        }

        /// <summary>イグニッションの残りの大きさの割合。明けてから ignitionFade 秒で消える（曲の秒で数える）</summary>
        public float IgnitionTail(float song)
        {
            if (song <= 0f) return 1f;
            if (ignitionFade <= 0f) return 0f;
            return 1f - Mathf.Clamp01(song / ignitionFade);
        }
    }
}
