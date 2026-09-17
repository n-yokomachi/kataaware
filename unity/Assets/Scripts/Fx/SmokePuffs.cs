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
