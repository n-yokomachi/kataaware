using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 煙草の煙。吸っている間だけ粒を出し、止めた後も出ている分は消えるまで流れる。
    /// 出どころは口元に置き、粒は世界の座標で動かす。首を振っても煙は置き去りになる
    /// </summary>
    public sealed class SmokePuffs : MonoBehaviour
    {
        [SerializeField] ParticleSystem puffs;
        [Tooltip("ひと息で吐き出す粒の数。薄いものを数多く重ねて煙の塊にする")]
        [SerializeField] int blowCount = 90;
        [Tooltip("吐いた煙を撒く広さ。メートル")]
        [SerializeField] float blowSpread = 0.13f;

        float until = -1f;

        /// <summary>今このとき煙を出しているか。動作確認から読む</summary>
        public bool Emitting { get { return until > 0f && Time.time < until; } }

        /// <summary>吸っている間の判定。始めた時刻から seconds 秒だけ出す</summary>
        public static bool Lit(float now, float startedAt, float seconds)
        {
            if (startedAt < 0f) return false;
            return now >= startedAt && now < startedAt + seconds;
        }

        /// <summary>seconds 秒のあいだ煙を立てる</summary>
        public void Begin(float seconds)
        {
            until = Time.time + seconds;
            if (puffs == null) return;
            puffs.Clear();
            puffs.Play();
        }

        /// <summary>
        /// ひと息ぶんを吐き出す。細く燻る煙とは別に、前へ向けて大きな塊をまとめて出す
        /// </summary>
        public void Blow()
        {
            if (puffs == null) return;
            // 出どころは上を向けてあるので、吐く向きは親（カメラ）の正面から取る。
            // まっすぐ前へ出して、少し上へ抜けていく
            var face = transform.parent != null ? transform.parent.forward : transform.forward;
            // 口から前へ出るのは最初だけ。すぐ上へ向かうので、初速も上を強くする
            var ahead = (face * 0.80f + Vector3.up * 0.60f).normalized;
            for (var i = 0; i < blowCount; i++)
            {
                // 口から出るほど細く速く、先へ行くほど広がって遅い。一息の形にする
                var along = (float)i / Mathf.Max(1, blowCount - 1);
                var spread = Random.insideUnitSphere * blowSpread * (0.25f + along);
                var p = new ParticleSystem.EmitParams();
                p.position = transform.position + face * (along * 0.12f) + spread * 0.6f;
                p.velocity = ahead * Random.Range(0.34f, 0.62f) * (1.15f - along * 0.5f) + spread * 0.7f;
                p.startSize = Random.Range(0.13f, 0.24f) * (0.75f + along * 0.6f);
                p.startLifetime = Random.Range(3.8f, 6.0f);
                // 1 粒は薄く。重なったところだけ濃くなる
                p.startColor = new Color(0.82f, 0.81f, 0.78f, Random.Range(0.26f, 0.40f));
                p.rotation = Random.Range(0f, 360f);
                puffs.Emit(p, 1);
            }
        }

        /// <summary>止める。すでに出た粒はそのまま流れて消える</summary>
        public void Cancel()
        {
            until = -1f;
            if (puffs == null) return;
            puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update()
        {
            if (until < 0f || Time.time < until) return;
            Cancel();
        }
    }
}
