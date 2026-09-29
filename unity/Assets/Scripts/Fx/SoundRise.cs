using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面の頭の明けに合わせて、環境音と曲をフェードで入れる（オーナー、2026-09-29「暗転明けから音を再生するときはフェードで」）。
    /// 場面 2 は場面 1 のドアから暗転を挟んで明ける。雨・雑踏・ヤードのラジオがいきなり鳴り出さないよう、
    /// 場面を読んでから <see cref="seconds"/> かけて大きさを 0 から上げる。思い出した時（記憶から読んだ時）も場面を読み直すので同じ。
    ///
    /// 大きさを決めるのは、それぞれの音の持ち主（<see cref="RainCover"/>・<see cref="CrowdNoise"/>・<see cref="StallRadio"/>）のまま。
    /// ここはその後（実行順が後）に、決まった大きさへ明けの割合（<see cref="Level"/>）を掛けるだけ。持ち主は自分の覚えた大きさから毎フレーム書き直すので、掛けた値が次へ持ち越されることはない
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class SoundRise : MonoBehaviour
    {
        [Tooltip("明けに合わせて上げる音。雨・雑踏・ラジオ")]
        [SerializeField] AudioSource[] sounds = new AudioSource[0];
        [Tooltip("0 から元の大きさまで上げる秒数。黒からの明け（SceneFlow.FadeInSeconds、1.2 秒）に揃えるのが目安")]
        [SerializeField] float seconds = SceneFlow.FadeInSeconds;

        float elapsed;

        /// <summary>いまの明けの割合。0〜1。動作確認から読む</summary>
        public float Current { get; private set; }

        /// <summary>
        /// 場面を読んでから elapsed 秒の、明けの割合。0〜1。seconds が 0 以下なら 1（フェードを挟まない）。
        /// 滑らかに立ち上げて滑らかに寝かす（smoothstep）。直線だと、はじめの 1 割で −20dB まで上がって「鳴り出した」と聞こえる
        /// </summary>
        public static float Level(float elapsed, float seconds)
        {
            if (seconds <= 0f) return 1f;
            var t = Mathf.Clamp01(elapsed / seconds);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 最初のフレームは 0 から。Awake では掛けない（雨の持ち主 <see cref="RainCover"/> は OnEnable で組み立てた大きさを覚えるので、
        /// その前に 0 を掛けると雨が鳴らなくなる）。最初の Update は音が鳴り出す前に来る
        /// </summary>
        void Update()
        {
            if (Current >= 1f && elapsed > 0f) return;
            Current = Level(elapsed, seconds);
            elapsed += Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            if (sounds == null) return;
            for (var i = 0; i < sounds.Length; i++)
                if (sounds[i] != null) sounds[i].volume *= Current;
        }
    }
}
