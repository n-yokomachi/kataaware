using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの音と煙。SmokeBeats の時刻表どおりに、
    /// ジッポ（開く・点く・閉じる）→ 点いた瞬間に煙草に火が移った音 → 吸う息 → 吐く息、を決めた回数だけ並べる。
    /// ジッポの音と火が移った音が鳴りきってから最初の吸う息までには、向き直す間（turn）を挟める（場面 1）。
    /// 煙は火が点いたところから立ちはじめ、吐く息に合わせてひと吹き足す。
    /// 場面 1・5・8 が同じ仕組みを使う（同じ主人公の同じジッポ）
    /// </summary>
    [DefaultExecutionOrder(15)]
    public sealed class Cigarette : MonoBehaviour
    {
        [SerializeField] SmokePuffs puffs;
        [Tooltip("ジッポと息を鳴らす。口元に置く")]
        [SerializeField] AudioSource voice;
        [Tooltip("ジッポの音（Zippo.wav）。開く・点く・閉じるが一つに入っている。点く時刻は SmokeBeats.StrikeInZippo")]
        [SerializeField] AudioClip zippo;
        [Tooltip("煙草に火が移った音（CigaretteLit.wav）。ジッポが点いた瞬間から鳴らす")]
        [SerializeField] AudioClip lit;
        [SerializeField] AudioClip drag;
        [SerializeField] AudioClip blow;

        float elapsed = -1f;
        int drags;
        /// <summary>ジッポの音と火が移った音が鳴りきってから吸い始めるまでに挟む、向き直す間。秒</summary>
        float turn;
        int nextDrag;
        int nextBlow;
        bool opened;
        bool caught;

        /// <summary>吐き始めるたびに知らせる。何服目かを渡す。0 から数える</summary>
        public event Action<int> Blew;

        /// <summary>吸っている最中か。動作確認から読む</summary>
        public bool Smoking { get { return elapsed >= 0f; } }

        /// <summary>これまでに吐いた回数。動作確認から読む</summary>
        public int Blows { get { return nextBlow; } }

        /// <summary>火を点けてからの秒。吸っていなければ -1。動作確認から読む</summary>
        public float Elapsed { get { return elapsed; } }

        void Awake()
        {
            // Web: どの音も先読みしない設定。展開を始めておく。
            // 火を点けてから最初の音（ジッポ）まで 0.3 秒しかなく、鳴らす時に読み込むと鳴り出しが遅れる（SoundLoad）
            SoundLoad.Warm(zippo, lit, drag, blow);
        }

        /// <summary>火を点ける。drags 服ぶん吸う。turn はジッポの音と火が移った音が鳴りきってから吸い始めるまでに挟む、向き直す間（秒）</summary>
        public void Light(int drags, float turn = 0f)
        {
            this.drags = Mathf.Max(0, drags);
            this.turn = Mathf.Max(0f, turn);
            elapsed = 0f;
            nextDrag = 0;
            nextBlow = 0;
            opened = false;
            caught = false;
        }

        /// <summary>途中で止める</summary>
        public void Stop()
        {
            elapsed = -1f;
            if (puffs != null) puffs.Cancel();
        }

        void Update()
        {
            if (elapsed < 0f) return;
            elapsed += Time.deltaTime;
            if (!opened && elapsed >= SmokeBeats.ZippoAt)
            {
                opened = true;
                Play(zippo);
            }
            // ジッポに火が点いた瞬間に、煙草に火が移る。煙もここから
            if (!caught && elapsed >= SmokeBeats.LitAt)
            {
                caught = true;
                Play(lit);
                if (puffs != null) puffs.Begin(SmokeBeats.Total(drags, turn));
            }
            if (nextDrag < drags && elapsed >= SmokeBeats.DragAt(nextDrag, drags, turn))
            {
                nextDrag++;
                Play(drag);
            }
            if (nextBlow < drags && elapsed >= SmokeBeats.BlowAt(nextBlow, drags, turn))
            {
                var i = nextBlow;
                nextBlow++;
                Play(blow);
                if (puffs != null) puffs.Blow();
                if (Blew != null) Blew(i);
            }
            if (elapsed < SmokeBeats.Total(drags, turn)) return;
            elapsed = -1f;
        }

        void Play(AudioClip clip)
        {
            if (voice == null || clip == null) return;
            voice.PlayOneShot(clip);
        }
    }
}
