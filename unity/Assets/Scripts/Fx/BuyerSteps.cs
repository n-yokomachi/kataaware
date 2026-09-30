using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 売り買いで、買い手が卓の向こうへ歩いてくる足音（オーナー、2026-09-29「買い手が現れるときは足音と一緒に。女性の足音はヒールっぽくしてほしい」）。
    ///
    /// 一歩ずつ、その足の所で鳴らす。音は 3D で鳴らすので、近づくほど大きく、来る向きから聞こえる。
    /// 男は通りと同じ硬い靴の音（Concrete）、女はヒールの音（Heels）。
    /// いつ鳴らすかは歩く買い手（<see cref="BuyerWalk"/>）が決める。歩きの動きの足の着地に揃えて鳴らす
    /// （オーナー、2026-09-30「場面2の買い手も歩いて近づいてくるようにして」）
    /// </summary>
    public sealed class BuyerSteps : MonoBehaviour
    {
        [Tooltip("足音を鳴らす 3D の音源。一歩ごとにその足の所へ動かす")]
        [SerializeField] AudioSource source;
        [Tooltip("男の買い手の足音。硬い靴で石畳を歩く音（通りの足音と同じ組）")]
        [SerializeField] AudioClip[] shoes = new AudioClip[0];
        [Tooltip("女の買い手の足音。ヒール")]
        [SerializeField] AudioClip[] heels = new AudioClip[0];

        [Header("鳴らし方")]
        [Tooltip("音の大きさの振れ")]
        [SerializeField] float volumeJitter = 0.10f;
        [Tooltip("音の高さの振れ")]
        [SerializeField] float pitchJitter = 0.06f;

        int last = -1;

        /// <summary>鳴らした歩数。動作確認から読む</summary>
        public int Played { get; private set; }

        /// <summary>一歩を at で鳴らす。woman ならヒール。loud は大きさの倍（立ち止まる時の足を揃える一歩は小さく）</summary>
        public void Step(Vector3 at, bool woman, float loud = 1f)
        {
            Played++;
            var clips = woman ? heels : shoes;
            if (source == null || clips == null || clips.Length == 0) return;
            source.transform.position = at;
            // 直前と同じ音は避ける（Footsteps と同じ）
            var pick = clips.Length == 1 ? 0 : Random.Range(0, clips.Length);
            if (clips.Length > 1 && pick == last) pick = (pick + 1) % clips.Length;
            last = pick;
            if (clips[pick] == null) return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clips[pick], loud * (1f + Random.Range(-volumeJitter, volumeJitter)));
        }
    }
}
