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
        [Tooltip("雨とワイパー。走行音に重ねる輪")]
        [SerializeField] AudioSource weather;
        [Tooltip("エンジンだけ掛かっている音。止まっているあいだの輪")]
        [SerializeField] AudioSource motor;

        [Header("単発")]
        [SerializeField] AudioClip doorOpen;
        [SerializeField] AudioClip doorShut;
        [SerializeField] AudioClip ignition;
        [Tooltip("暗転の黒のあいだに流す、車が動き出す音")]
        [SerializeField] AudioClip pullAway;
        [Tooltip("運転席の窓を下ろす音")]
        [SerializeField] AudioClip windowDown;

        [Header("走行音")]
        [Tooltip("舗装された道")]
        [SerializeField] AudioClip paved;
        [Tooltip("土と轍の道")]
        [SerializeField] AudioClip gravel;
        [Tooltip("雨とワイパー。ワイパーの周期 0.9485 秒の 14 倍で輪にしてある")]
        [SerializeField] AudioClip rain;
        [Tooltip("エンジンだけ掛かっている音")]
        [SerializeField] AudioClip idle;

        [Header("大きさ")]
        [SerializeField] float shotVolume = 0.85f;
        // **走行音は一度「もう少し大きめに」と差し戻されている。**
        // 上げるときは二本の差を保つこと。素材の実効値が舗装 -35dB、未舗装 -25dB で
        // 10dB 開いているので、同じ値を入れると未舗装だけ飛び出す
        [Tooltip("舗装の輪。素材が 10dB 小さいぶんここで持ち上げる")]
        [SerializeField] float pavedVolume = 1.00f;
        [SerializeField] float gravelVolume = 0.42f;
        [Tooltip("雨とワイパー。走行音に重ねるので控えめに")]
        [SerializeField] float rainVolume = 0.55f;
        [Tooltip("エンジンだけ掛かっている音")]
        [SerializeField] float idleVolume = 0.60f;

        /// <summary>暗転の黒のあいだに流す音の長さ。秒。黒を何秒置くか決めるのに使う</summary>
        public float PullAwaySeconds { get { return pullAway != null ? pullAway.length : 0f; } }

        /// <summary>イグニッションの長さ。秒。鳴らし終えてから震え出すのに使う</summary>
        public float IgnitionSeconds { get { return ignition != null ? ignition.length : 0f; } }

        /// <summary>窓を下ろす音の長さ。秒。下ろし終えてから独白を出すのに使う</summary>
        public float WindowSeconds { get { return windowDown != null ? windowDown.length : 0f; } }

        public void DoorOpen() { Shot(doorOpen); }
        public void DoorShut() { Shot(doorShut); }
        public void Ignition() { Shot(ignition); }
        public void PullAway() { Shot(pullAway); }
        public void WindowDown() { Shot(windowDown); }

        /// <summary>
        /// エンジンだけ掛かっている音。イグニッションのあと、走り出すまでの間を埋める。
        /// **これが無いと音が切れる。** 鍵を回し終えたところで静かになり、
        /// 「音声止めた？」と差し戻された
        /// </summary>
        public void Idle(bool running)
        {
            if (motor == null) return;
            if (!running)
            {
                if (motor.isPlaying) motor.Stop();
                return;
            }
            if (idle == null) return;
            motor.volume = idleVolume;
            if (motor.clip == idle && motor.isPlaying) return;
            motor.clip = idle;
            motor.loop = true;
            motor.Play();
        }

        /// <summary>雨の輪。走行音に重ねる。降っていない景色では止める</summary>
        public void Weather(bool wet)
        {
            if (weather == null) return;
            if (!wet)
            {
                if (weather.isPlaying) weather.Stop();
                return;
            }
            if (rain == null) return;
            weather.volume = rainVolume;
            if (weather.clip == rain && weather.isPlaying) return;
            weather.clip = rain;
            weather.loop = true;
            weather.Play();
        }

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
            if (weather != null && weather.isPlaying) weather.Stop();
            if (motor != null && motor.isPlaying) motor.Stop();
        }

        void Shot(AudioClip clip)
        {
            if (oneShot == null || clip == null) return;
            oneShot.PlayOneShot(clip, shotVolume);
        }
    }
}
