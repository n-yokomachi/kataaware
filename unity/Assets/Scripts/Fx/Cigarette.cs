using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの音と煙。SmokeBeats の時刻表どおりに、
    /// 蓋を開ける金属音 → 火が点く音 → 吸う息 → 吐く息、を決めた回数だけ並べる。
    /// 煙は火が点いてから立ちはじめ、吐く息に合わせてひと吹き足す
    /// </summary>
    [DefaultExecutionOrder(15)]
    public sealed class Cigarette : MonoBehaviour
    {
        [SerializeField] SmokePuffs puffs;
        [Tooltip("ライターと息を鳴らす。口元に置く")]
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip lighterClick;
        [SerializeField] AudioClip lighterFlame;
        [SerializeField] AudioClip drag;
        [SerializeField] AudioClip blow;

        float elapsed = -1f;
        int drags;
        int nextDrag;
        int nextBlow;
        bool clicked;
        bool flamed;

        /// <summary>吐き始めるたびに知らせる。何服目かを渡す。0 から数える</summary>
        public event Action<int> Blew;

        /// <summary>吸っている最中か。動作確認から読む</summary>
        public bool Smoking { get { return elapsed >= 0f; } }

        /// <summary>これまでに吐いた回数。動作確認から読む</summary>
        public int Blows { get { return nextBlow; } }

        /// <summary>火を点ける。drags 服ぶん吸う</summary>
        public void Light(int drags)
        {
            this.drags = Mathf.Max(0, drags);
            elapsed = 0f;
            nextDrag = 0;
            nextBlow = 0;
            clicked = false;
            flamed = false;
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
            if (!clicked && elapsed >= SmokeBeats.ClickAt)
            {
                clicked = true;
                Play(lighterClick);
            }
            if (!flamed && elapsed >= SmokeBeats.FlameAt)
            {
                flamed = true;
                Play(lighterFlame);
                // 火が点いてから煙が立ちはじめる
                if (puffs != null) puffs.Begin(SmokeBeats.Total(drags));
            }
            if (nextDrag < drags && elapsed >= SmokeBeats.DragAt(nextDrag))
            {
                nextDrag++;
                Play(drag);
            }
            if (nextBlow < drags && elapsed >= SmokeBeats.BlowAt(nextBlow))
            {
                var i = nextBlow;
                nextBlow++;
                Play(blow);
                if (puffs != null) puffs.Blow();
                if (Blew != null) Blew(i);
            }
            if (elapsed < SmokeBeats.Total(drags)) return;
            elapsed = -1f;
        }

        void Play(AudioClip clip)
        {
            if (voice == null || clip == null) return;
            voice.PlayOneShot(clip);
        }
    }
}
