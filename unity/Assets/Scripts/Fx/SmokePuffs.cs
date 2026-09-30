using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 煙草の煙。吸っている間だけ粒を出し、止めた後も出ている分は消えるまで流れる。
    /// 出どころは口元に置き、粒は世界の座標で動かす。首を振っても煙は置き去りになる。
    ///
    /// **場面 1 は吸い終えても煙を立て続ける**（<see cref="Linger"/>。オーナー、2026-09-29「タバコ吸った後だけど、しばらくは煙草の煙を出し続けるようにして」）。
    /// モニターを済ませたら、出す数と粒の大きさを <see cref="Fade"/> の秒で細くして止める（ふっと消さない。出ていた粒は寿命まで流れる）
    /// </summary>
    public sealed class SmokePuffs : MonoBehaviour
    {
        [SerializeField] ParticleSystem puffs;
        [Tooltip("ひと息で吐き出す粒の数。薄いものを数多く重ねて煙の塊にする")]
        [SerializeField] int blowCount = 90;
        [Tooltip("吐いた煙を撒く広さ。メートル")]
        [SerializeField] float blowSpread = 0.13f;

        float until = -1f;
        /// <summary>時刻が来ても止めずに立て続けるか</summary>
        bool lingering;
        /// <summary>細くしている途中の、始めた時刻。細くしていなければ負</summary>
        float fadeStart = -1f;
        float fadeSeconds;
        /// <summary>細くし始めた時の、出す数と粒の大きさの倍率。止めたら戻す</summary>
        float baseRate = -1f;
        float baseSize = -1f;

        /// <summary>今このとき煙を出しているか。動作確認から読む</summary>
        public bool Emitting { get { return until > 0f && (lingering || fadeStart >= 0f || Time.time < until); } }

        /// <summary>吸い終えた後も立て続けているか（細くしている途中は含まない）。動作確認から読む</summary>
        public bool Lingering { get { return lingering; } }

        /// <summary>細くしている途中か。動作確認から読む</summary>
        public bool Fading { get { return fadeStart >= 0f; } }

        /// <summary>
        /// 細くし始めてから elapsed 秒の、出す数の割合。1 から始めて seconds 秒で 0。両端をなだらかにする（ふっと消さない）
        /// </summary>
        public static float Thin(float elapsed, float seconds)
        {
            if (seconds <= 0f) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
        }

        /// <summary>細くする間の粒の大きさの倍率。出す数の割合 thin が 0 に近づくほど半分まで細る</summary>
        public static float Size(float thin)
        {
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(thin));
        }

        /// <summary>吸っている間の判定。始めた時刻から seconds 秒だけ出す</summary>
        public static bool Lit(float now, float startedAt, float seconds)
        {
            if (startedAt < 0f) return false;
            return now >= startedAt && now < startedAt + seconds;
        }

        /// <summary>seconds 秒のあいだ煙を立てる。<see cref="Linger"/> を掛けてあれば、時刻が来ても止めない</summary>
        public void Begin(float seconds)
        {
            Unfade();
            until = Time.time + seconds;
            if (puffs == null) return;
            puffs.Clear();
            puffs.Play();
        }

        /// <summary>時刻が来ても止めずに立て続ける（場面 1。モニターを済ませるまで）。まだ立てていなければ、立て始めた時から効く</summary>
        public void Linger()
        {
            lingering = true;
        }

        /// <summary>
        /// 吸い終えた後の、立ち続けている形で始める（場面 1 を思い出した時。煙草を吸い終えてモニターがまだの所）。
        /// 吐いた煙の塊は出さない
        /// </summary>
        public void Smolder()
        {
            Unfade();
            lingering = true;
            until = Mathf.Max(0.001f, Time.time);
            if (puffs == null) return;
            if (!puffs.isPlaying) puffs.Play();
        }

        /// <summary>立てている煙を、seconds 秒で出す数と粒の大きさを細くしてから止める。出ていた粒は寿命まで流れる</summary>
        public void Fade(float seconds)
        {
            if (until < 0f || fadeStart >= 0f) return;
            lingering = false;
            fadeStart = Time.time;
            fadeSeconds = Mathf.Max(0f, seconds);
            if (puffs == null) return;
            baseRate = puffs.emission.rateOverTimeMultiplier;
            baseSize = puffs.main.startSizeMultiplier;
        }

        /// <summary>立てている煙を seconds 秒だけ長くする。時刻表を止めている間（場面 1 の 1 ページを読んでいる間）に呼ぶ</summary>
        public void Extend(float seconds)
        {
            if (until < 0f || seconds <= 0f) return;
            until += seconds;
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
            lingering = false;
            if (puffs != null) puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Unfade();
        }

        /// <summary>細くしていたら、出す数と粒の大きさの倍率を戻す</summary>
        void Unfade()
        {
            if (fadeStart >= 0f && puffs != null && baseRate >= 0f)
            {
                var emission = puffs.emission;
                emission.rateOverTimeMultiplier = baseRate;
                var main = puffs.main;
                main.startSizeMultiplier = baseSize;
            }
            fadeStart = -1f;
            baseRate = -1f;
            baseSize = -1f;
        }

        void Update()
        {
            if (fadeStart >= 0f)
            {
                var thin = Thin(Time.time - fadeStart, fadeSeconds);
                if (puffs != null && baseRate >= 0f)
                {
                    var emission = puffs.emission;
                    emission.rateOverTimeMultiplier = baseRate * thin;
                    var main = puffs.main;
                    main.startSizeMultiplier = baseSize * Size(thin);
                }
                if (thin > 0f) return;
                Cancel();
                return;
            }
            if (until < 0f || lingering || Time.time < until) return;
            Cancel();
        }
    }
}
