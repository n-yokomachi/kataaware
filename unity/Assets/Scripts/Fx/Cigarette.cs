using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの音と煙。SmokeBeats の時刻表どおりに、
    /// ジッポ（開く・点く・閉じる）→ 点いた瞬間に煙草に火が移った音 → 吸う息 → 吐く息、を決めた回数だけ並べる。
    /// ジッポの音と火が移った音が鳴りきってから最初の吸う息までには、向き直す間（turn）を挟める（場面 1）。
    /// 場面 1 は火を点けたまま、向き直す手前で時刻表を止めておける（hold。煙草を取った 1 ページを送るまで。<see cref="Release"/> で進める）。
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
        /// <summary>向き直す手前で時刻表を止めておくか</summary>
        bool held;

        /// <summary>吐き始めるたびに知らせる。何服目かを渡す。0 から数える</summary>
        public event Action<int> Blew;

        /// <summary>吸っている最中か。動作確認から読む</summary>
        public bool Smoking { get { return elapsed >= 0f; } }

        /// <summary>これまでに吐いた回数。動作確認から読む</summary>
        public int Blows { get { return nextBlow; } }

        /// <summary>火を点けてからの秒。吸っていなければ -1。動作確認から読む</summary>
        public float Elapsed { get { return elapsed; } }

        /// <summary>向き直す手前で時刻表を止めているか（止める所に届いていなくても、止める約束なら true）。動作確認から読む</summary>
        public bool Held { get { return held; } }

        void Awake()
        {
            // Web: どの音も先読みしない設定。展開を始めておく。
            // 火を点けてから最初の音（ジッポ）まで 0.3 秒しかなく、鳴らす時に読み込むと鳴り出しが遅れる（SoundLoad）
            SoundLoad.Warm(zippo, lit, drag, blow);
        }

        /// <summary>
        /// 火を点ける。drags 服ぶん吸う。turn はジッポの音と火が移った音が鳴りきってから吸い始めるまでに挟む、向き直す間（秒）。
        /// hold なら、向き直す手前（<see cref="SmokeBeats.TurnAt"/>）で時刻表を止め、<see cref="Release"/> を待つ
        /// </summary>
        public void Light(int drags, float turn = 0f, bool hold = false)
        {
            this.drags = Mathf.Max(0, drags);
            this.turn = Mathf.Max(0f, turn);
            held = hold;
            elapsed = 0f;
            nextDrag = 0;
            nextBlow = 0;
            opened = false;
            caught = false;
        }

        /// <summary>
        /// 吸い終えた後も煙を立て続ける（場面 1。モニターを済ませるまで）。<see cref="Light"/> の後に呼ぶ。
        /// 吸い終えたら、口元の煙から hand（右手の指先の骨）の一筋に替わる
        /// </summary>
        public void Linger(Transform hand = null)
        {
            if (puffs != null) puffs.Linger(hand);
        }

        /// <summary>吸い終えた後の、指先から一筋の煙が立っている形で始める（場面 1 を思い出した時）。音は鳴らさない</summary>
        public void Smolder(Transform hand = null)
        {
            if (puffs != null) puffs.Smolder(hand);
        }

        /// <summary>立ち続けていた煙を seconds 秒で細くして止める</summary>
        public void Snuff(float seconds)
        {
            if (puffs != null) puffs.Fade(seconds);
        }

        /// <summary>向き直す手前で止めていた時刻表を進める。止める所に届く前に呼べば、止めずに進む</summary>
        public void Release()
        {
            held = false;
        }

        /// <summary>途中で止める</summary>
        public void Stop()
        {
            elapsed = -1f;
            held = false;
            if (puffs != null) puffs.Cancel();
        }

        void Update()
        {
            if (elapsed < 0f) return;
            float stalled;
            elapsed = SmokeBeats.Advance(elapsed, Time.deltaTime, held, out stalled);
            // 止めた分だけ煙を延ばす。吸い終わる所で煙も尽きるように
            if (stalled > 0f && puffs != null) puffs.Extend(stalled);
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
