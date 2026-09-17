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
        [Tooltip("ひと息で吐き出す粒の数")]
        [SerializeField] int blowCount = 34;

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
            var ahead = (face * 0.88f + Vector3.up * 0.34f).normalized;
            for (var i = 0; i < blowCount; i++)
            {
                var spread = Random.insideUnitSphere * 0.09f;
                var p = new ParticleSystem.EmitParams();
                p.position = transform.position + spread * 0.5f;
                p.velocity = ahead * Random.Range(0.28f, 0.52f) + spread;
                p.startSize = Random.Range(0.16f, 0.26f);
                p.startLifetime = Random.Range(3.4f, 5.2f);
                p.startColor = new Color(0.80f, 0.79f, 0.76f, Random.Range(0.52f, 0.68f));
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
