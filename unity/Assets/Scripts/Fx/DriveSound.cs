using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 8 の音。乗り込みの一連（ドア・イグニッション・動き出し）と、走行音の輪。
    ///
    /// **どれも 2D で鳴らす。** 耳はいつも運転席にあり、音源も同じ車の中にある。
    /// 距離で減らすと、視線を振っただけで走行音の大きさが変わる。
    ///
    /// 走行音は舗装と未舗装の二本を持ち、帯が変わるところで入れ替える。
    /// 入れ替えは暗転の黒のあいだに呼ばれるので、切り替わる瞬間は聞こえても見えない。
    ///
    /// 素材そのものの大きさは揃っていない。舗装の輪は実効値 -35dB、未舗装は -25dB で
    /// 10dB 開いている。素材の頂点は動かさない決まりなので（Audio/LICENSES.md）、
    /// 差は <see cref="pavedVolume"/> と <see cref="gravelVolume"/> で吸収する
    /// </summary>
    public sealed class DriveSound : MonoBehaviour
    {
        [Tooltip("単発。ドア・イグニッション・動き出し")]
        [SerializeField] AudioSource oneShot;
        [Tooltip("走行音。輪で鳴らし続ける")]
        [SerializeField] AudioSource road;

        [Header("単発")]
        [SerializeField] AudioClip doorOpen;
        [SerializeField] AudioClip doorShut;
        [SerializeField] AudioClip ignition;
        [Tooltip("暗転の黒のあいだに流す、車が動き出す音")]
        [SerializeField] AudioClip pullAway;

        [Header("走行音")]
        [Tooltip("舗装された道")]
        [SerializeField] AudioClip paved;
        [Tooltip("土と轍の道")]
        [SerializeField] AudioClip gravel;

        [Header("大きさ")]
        [SerializeField] float shotVolume = 0.85f;
        [Tooltip("舗装の輪。素材が 10dB 小さいぶんここで持ち上げる")]
        [SerializeField] float pavedVolume = 0.75f;
        [SerializeField] float gravelVolume = 0.30f;

        /// <summary>暗転の黒のあいだに流す音の長さ。秒。黒を何秒置くか決めるのに使う</summary>
        public float PullAwaySeconds { get { return pullAway != null ? pullAway.length : 0f; } }

        public void DoorOpen() { Shot(doorOpen); }
        public void DoorShut() { Shot(doorShut); }
        public void Ignition() { Shot(ignition); }
        public void PullAway() { Shot(pullAway); }

        /// <summary>走行音を鳴らし始める。同じ道が続くなら鳴らし直さない</summary>
        public void Road(bool rough)
        {
            if (road == null) return;
            var want = rough ? gravel : paved;
            if (want == null) return;
            road.volume = rough ? gravelVolume : pavedVolume;
            // **同じ音なら触らない。** 帯が変わっても舗装のままなら、
            // 鳴らし直すと輪の頭へ戻って継ぎ目が聞こえる
            if (road.clip == want && road.isPlaying) return;
            road.clip = want;
            road.loop = true;
            road.Play();
        }

        /// <summary>走行音を止める。場面が閉じるとき</summary>
        public void Hush()
        {
            if (road != null && road.isPlaying) road.Stop();
        }

        void Shot(AudioClip clip)
        {
            if (oneShot == null || clip == null) return;
            oneShot.PlayOneShot(clip, shotVolume);
        }
    }
}
