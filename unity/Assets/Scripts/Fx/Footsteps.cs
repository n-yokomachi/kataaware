using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 足音。進んだ距離を測って、一歩ぶん進むたびに鳴らす。
    /// 動作の再生位置ではなく距離で数えるので、速さを変えても歩幅どおりに鳴る。
    ///
    /// **一歩ごとに足元を見る。** 足元の当たりに <see cref="StepGround"/> が付いていれば、その地面の音で鳴らす
    /// （村の未舗装の路地は砂利、庭の煉瓦の小路とテラスは硬い音、場面 4 の住戸の中や公園の小径など）。付いていない床では
    /// <see cref="clips"/>（その場面の既定の音。場面 4 は <see cref="Use"/> で場所ごとに取り替える）で鳴らす。
    ///
    /// **一歩の音は余韻を次の一歩の手前まで持っている**（0.34〜0.68 秒）。歩く速さによっては次の一歩が前の余韻に重なるが、
    /// <c>PlayOneShot</c> は鳴っている音を切らずに別の声で重ねるので途切れない。高さの振れは入れ物（<see cref="source"/>）に一つなので、
    /// 次の一歩を鳴らすと前の余韻の高さも替わる。そのときの余韻は床の空気だけで頂点（5ms 窓の実効値）より 28〜50dB 下（自室の組が 28〜38dB でいちばん近い）にあり、新しい一歩の頭に隠れる
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class Footsteps : MonoBehaviour
    {
        /// <summary>一歩の歩幅。メートル。歩きの一巡（2 歩）が 1.76 m</summary>
        public const float Stride = 0.88f;

        [SerializeField] CharacterController body;
        [Tooltip("歩けるかどうかを見る。空なら親から拾う")]
        [SerializeField] PlayerController walker;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip[] clips = new AudioClip[0];
        [Tooltip("音の大きさの振れ。同じ音に聞こえないように")]
        [SerializeField] float volumeJitter = 0.12f;
        [Tooltip("音の高さの振れ")]
        [SerializeField] float pitchJitter = 0.09f;

        /// <summary>足元を探る線の始まりの高さ（体の足元から）と長さ。m。段（0.3 m）を上り下りしても床に届く</summary>
        const float ProbeFrom = 0.4f;
        const float ProbeReach = 1.0f;

        float walked;
        int last = -1;
        /// <summary>直前に鳴らした音の並び。地面が替わったら直前の番号を忘れる</summary>
        AudioClip[] playing;

        /// <summary>これまでに鳴らした歩数。動作確認から読む</summary>
        public int Steps { get; private set; }

        /// <summary>いま鳴らす音の数。動作確認から読む</summary>
        public int ClipCount { get { return clips == null ? 0 : clips.Length; } }

        /// <summary>
        /// 床の音を取り替える。場面 4 は一つのシーンに五つの場所が同居していて、
        /// 潜る先によってコンクリートと土を履き替える。
        /// 直前に鳴らした番号も忘れる。取り替えた先の並びでは別の音を指している
        /// </summary>
        public void Use(AudioClip[] next)
        {
            clips = next ?? new AudioClip[0];
            last = -1;
        }

        /// <summary>
        /// 積んだ距離に distance を足して、鳴らすべき歩数を返す。
        /// 残りは次に持ち越す。止まっているあいだは 0
        /// </summary>
        public static int Advance(ref float walked, float distance)
        {
            if (distance <= 0f) return 0;
            walked += distance;
            var steps = 0;
            while (walked >= Stride)
            {
                walked -= Stride;
                steps++;
            }
            return steps;
        }

        void Awake()
        {
            if (walker == null) walker = GetComponentInParent<PlayerController>();
        }

        void Update()
        {
            if (body == null) return;
            // **動けない間は鳴らさない。** CharacterController の velocity は
            // Move を呼ばなくなっても最後の値を持ち越す。歩いている途中で何かを調べて
            // 操作を取り上げると、その場に止まったまま足音だけが鳴り続ける。
            // 半端に積んだ距離も捨てる。次に歩き出したところから数え直す
            if (walker != null && !walker.CanMove) { walked = 0f; return; }
            var speed = BodyMotion.GroundSpeed(body.velocity);
            var steps = Advance(ref walked, speed * Time.deltaTime);
            for (var i = 0; i < steps; i++) Play();
        }

        /// <summary>
        /// 足元の地面の音。足元の当たりに <see cref="StepGround"/> が付いていて、その点の音が空でなければそれ。
        /// ほかは既定の音（<see cref="clips"/>）
        /// </summary>
        AudioClip[] Underfoot()
        {
            if (body == null) return clips;
            var foot = body.transform.position;
            // 始まりは体の当たりの中。中から出る線は自分の当たりを拾わない
            if (!Physics.Raycast(foot + Vector3.up * ProbeFrom, Vector3.down, out var hit, ProbeReach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return clips;
            var ground = hit.collider.GetComponent<StepGround>();
            if (ground == null) return clips;
            var own = ground.ClipsAt(hit.point);
            return own.Length > 0 ? own : clips;
        }

        /// <summary>直前と同じ音は避ける。繰り返しが耳につくので</summary>
        void Play()
        {
            Steps++;
            if (source == null) return;
            var set = Underfoot();
            if (set == null || set.Length == 0) return;
            // 地面が替わったら、直前の番号は別の音を指している
            if (set != playing) { playing = set; last = -1; }
            var pick = set.Length == 1 ? 0 : Random.Range(0, set.Length);
            if (set.Length > 1 && pick == last) pick = (pick + 1) % set.Length;
            last = pick;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(set[pick], 1f + Random.Range(-volumeJitter, volumeJitter));
        }
    }
}
