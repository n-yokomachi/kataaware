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
        [Tooltip("運転席の窓を下ろす音")]
        [SerializeField] AudioClip windowDown;
        [Tooltip("煙を吐く息。**Cigarette が持つのと同じ素材。**" +
            "あちらは時刻表どおりに吸ってすぐ吐くが、ここでは窓を下ろし終えてから吐かせたいので、" +
            "吐く息だけこちらから鳴らす")]
        [SerializeField] AudioClip exhale;

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
        // **どれも 1.00。** 二度「もう少し大きめに」と差し戻された末の指示がこれ。
        // AudioSource.volume の上限が 1 なので、ここから先は上げられない。
        // 窓を開けたときの「さらに大きく」は、音量ではなくこもりの取れ方で出す
        // （<see cref="Open"/>）。素材の実効値が舗装 -35dB、未舗装 -25dB と
        // 10dB 開いているので、同じ 1.00 でも未舗装の方がはっきり大きく鳴る
        [Tooltip("舗装の輪")]
        [SerializeField] float pavedVolume = 1.00f;
        [Tooltip("土と轍の輪")]
        [SerializeField] float gravelVolume = 1.00f;
        [Tooltip("雨とワイパー。走行音に重ねる")]
        [SerializeField] float rainVolume = 1.00f;
        [Tooltip("エンジンだけ掛かっている音")]
        [SerializeField] float idleVolume = 0.60f;
        [Tooltip("吐く息。口元なので単発より控えめに。場面 1 の Cigarette は 0.40")]
        [SerializeField] float breathVolume = 0.40f;
        [Tooltip("窓を下ろす音。走行の輪の上で鳴るので単発の既定より大きく")]
        [SerializeField] float windowVolume = 1.00f;

        [Header("窓")]
        [Tooltip("窓を閉めているときに走行音と雨から上を削る高さ。Hz")]
        [SerializeField] float shutCut = 1500f;
        [Tooltip("窓を開けたときの高さ。Hz。22000 で実質こもりなし")]
        [SerializeField] float openCut = 22000f;
        [Tooltip("こもりが取れるまでの秒数。窓が下りる速さに合わせる")]
        [SerializeField] float openSeconds = 1.6f;

        AudioLowPassFilter roadMuffle;
        AudioLowPassFilter weatherMuffle;
        float cut = -1f;
        float want;

        /// <summary>イグニッションの長さ。秒。鳴らし終えてから震え出すのに使う</summary>
        public float IgnitionSeconds { get { return ignition != null ? ignition.length : 0f; } }

        /// <summary>窓を下ろす音の長さ。秒。下ろし終えてから独白を出すのに使う</summary>
        public float WindowSeconds { get { return windowDown != null ? windowDown.length : 0f; } }

        /// <summary>
        /// 窓を開けた／閉めた。**音量では上げない。**
        ///
        /// 走行音も雨も既に 1.00 で、AudioSource.volume の上限に張り付いている。
        /// 窓を開けて大きく聞こえるのは、実際には音量より「こもりが取れる」こと
        /// なので、そちらで出す。閉まっているあいだは 1.5kHz から上を削り、
        /// 開けるとその蓋が外れる。路面の擦れも雨の粒も高い方に居るので、
        /// 蓋が外れた瞬間にどちらもぐっと前へ出る
        /// </summary>
        public void Open(bool open)
        {
            want = open ? openCut : shutCut;
        }

        /// <summary>
        /// 窓の開け閉めを、寄せずにその場で決める。景色が変わるときに使う。
        ///
        /// **景色の頭で寄せてはいけない。** 前の景色で窓を開けていると、
        /// 次の景色に入っても 1.6 秒かけて閉まっていく途中の音から始まる。
        /// 暗転を無くしたので、その 1.6 秒がそのまま聞こえる
        /// </summary>
        public void Shut()
        {
            want = shutCut;
            cut = shutCut;
            Apply(shutCut);
        }

        void Awake()
        {
            roadMuffle = Muffle(road);
            weatherMuffle = Muffle(weather);
            want = shutCut;
            cut = shutCut;
            Apply(shutCut);
        }

        void Update()
        {
            if (Mathf.Approximately(cut, want)) return;
            // 対数で寄せる。Hz を線形で動かすと、聞こえ方は終わり際にしか変わらない
            var from = Mathf.Log(Mathf.Max(20f, cut));
            var to = Mathf.Log(Mathf.Max(20f, want));
            var step = Mathf.Abs(Mathf.Log(Mathf.Max(20f, openCut)) - Mathf.Log(Mathf.Max(20f, shutCut)));
            var rate = openSeconds > 0.0001f ? step / openSeconds : step;
            cut = Mathf.Exp(Mathf.MoveTowards(from, to, rate * Time.deltaTime));
            if (Mathf.Abs(cut - want) / Mathf.Max(1f, want) < 0.01f) cut = want;
            Apply(cut);
        }

        void Apply(float hz)
        {
            if (roadMuffle != null) roadMuffle.cutoffFrequency = hz;
            if (weatherMuffle != null) weatherMuffle.cutoffFrequency = hz;
        }

        static AudioLowPassFilter Muffle(AudioSource on)
        {
            if (on == null) return null;
            var had = on.GetComponent<AudioLowPassFilter>();
            if (had == null) had = on.gameObject.AddComponent<AudioLowPassFilter>();
            had.lowpassResonanceQ = 1f;
            return had;
        }

        public void DoorOpen() { Shot(doorOpen); }
        public void DoorShut() { Shot(doorShut); }
        public void Ignition() { Shot(ignition); }
        /// <summary>
        /// 窓を下ろす音。**単発の既定より大きく鳴らす。**
        /// 走行の輪を素材ごと 16dB 持ち上げたので、既定の音量では埋もれて
        /// 「消えてる？」と差し戻された
        /// </summary>
        public void WindowDown()
        {
            if (oneShot == null || windowDown == null) return;
            oneShot.PlayOneShot(windowDown, windowVolume);
        }

        /// <summary>煙を吐く息。窓を下ろし終えてから鳴らす</summary>
        public void Exhale()
        {
            if (oneShot == null || exhale == null) return;
            oneShot.PlayOneShot(exhale, breathVolume);
        }

        /// <summary>吐く息の長さ。秒</summary>
        public float ExhaleSeconds { get { return exhale != null ? exhale.length : 0f; } }

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
