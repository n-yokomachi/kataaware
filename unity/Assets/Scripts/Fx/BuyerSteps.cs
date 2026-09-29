using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 売り買いで、買い手が卓の向こうへ歩いてくる足音（オーナー、2026-09-29「買い手が現れるときは足音と一緒に。女性の足音はヒールっぽくしてほしい」）。
    ///
    /// 買い手の立つ所から離れた所で一歩目を鳴らし、一歩ごとに近づいて、立つ所で止まる。音は 3D で鳴らすので、
    /// 近づくほど大きく、来る向きから聞こえる。男は通りと同じ硬い靴の音（Concrete）、女はヒールの音（Heels）。
    /// 何歩・何秒おきに鳴らすかは Inspector で変える。いつ鳴らすかは演出（<see cref="AlleyDirector"/>）が決め、
    /// 足音の終わりに合わせて買い手を浮かび上がらせる（<see cref="Seconds"/>）
    /// </summary>
    public sealed class BuyerSteps : MonoBehaviour
    {
        [Tooltip("足音を鳴らす 3D の音源。一歩ごとにその足の所へ動かす")]
        [SerializeField] AudioSource source;
        [Tooltip("男の買い手の足音。硬い靴で石畳を歩く音（通りの足音と同じ組）")]
        [SerializeField] AudioClip[] shoes = new AudioClip[0];
        [Tooltip("女の買い手の足音。ヒール")]
        [SerializeField] AudioClip[] heels = new AudioClip[0];

        [Header("歩き方")]
        [Tooltip("男の歩数")]
        [SerializeField] int shoeSteps = 5;
        [Tooltip("男の一歩の間。秒")]
        [SerializeField] float shoeInterval = 0.56f;
        [Tooltip("女（ヒール）の歩数。歩幅が狭いぶん多い")]
        [SerializeField] int heelSteps = 6;
        [Tooltip("女（ヒール）の一歩の間。秒")]
        [SerializeField] float heelInterval = 0.48f;
        [Tooltip("一歩目を鳴らす所の、立つ所からの遠さ。m")]
        [SerializeField] float from = 3.4f;
        [Tooltip("左右の足の開き。m。一歩ごとに立つ所の線の左右へ振る")]
        [SerializeField] float gait = 0.11f;

        [Header("鳴らし方")]
        [Tooltip("音の大きさの振れ")]
        [SerializeField] float volumeJitter = 0.10f;
        [Tooltip("音の高さの振れ")]
        [SerializeField] float pitchJitter = 0.06f;

        int last = -1;

        /// <summary>鳴らした歩数。動作確認から読む</summary>
        public int Played { get; private set; }

        /// <summary>歩数</summary>
        public int Count(bool woman)
        {
            return Mathf.Max(1, woman ? heelSteps : shoeSteps);
        }

        /// <summary>一歩の間。秒</summary>
        public float Interval(bool woman)
        {
            return Mathf.Max(0.05f, woman ? heelInterval : shoeInterval);
        }

        /// <summary>一歩目から止まる一歩まで。秒</summary>
        public float Seconds(bool woman)
        {
            return (Count(woman) - 1) * Interval(woman);
        }

        /// <summary>
        /// k 歩目（0 から、count 歩のうち）の足の所。立つ所 to から away の向きへ reach 離れた所で一歩目を鳴らし、
        /// 同じ歩幅で近づいて、最後の一歩で to に止まる。左右の足は歩く線の左右へ gait ずつ振る（最後の一歩は線の上）
        /// </summary>
        public static Vector3 At(Vector3 to, Vector3 away, float reach, float gait, int k, int count)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.forward;
            away.Normalize();
            var left = count <= 1 ? 0f : 1f - k / (float)(count - 1);
            var side = Vector3.Cross(Vector3.up, away).normalized;
            var foot = k == count - 1 ? 0f : (k % 2 == 0 ? gait : -gait);
            return to + away * (reach * left) + side * foot;
        }

        /// <summary>
        /// 立つ所 to へ、away の向きから歩いてくる。woman ならヒール。<see cref="Seconds"/> で最後の一歩を鳴らして終わる
        /// </summary>
        public IEnumerator Walk(Vector3 to, Vector3 away, bool woman)
        {
            var clips = woman ? heels : shoes;
            var count = Count(woman);
            var gap = Interval(woman);
            last = -1;
            for (var k = 0; k < count; k++)
            {
                Step(At(to, away, from, gait, k, count), clips);
                if (k < count - 1) yield return new WaitForSeconds(gap);
            }
        }

        void Step(Vector3 at, AudioClip[] clips)
        {
            Played++;
            if (source == null || clips == null || clips.Length == 0) return;
            source.transform.position = at;
            // 直前と同じ音は避ける（Footsteps と同じ）
            var pick = clips.Length == 1 ? 0 : Random.Range(0, clips.Length);
            if (clips.Length > 1 && pick == last) pick = (pick + 1) % clips.Length;
            last = pick;
            if (clips[pick] == null) return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clips[pick], 1f + Random.Range(-volumeJitter, volumeJitter));
        }
    }
}
