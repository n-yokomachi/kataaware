using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 足音。進んだ距離を測って、一歩ぶん進むたびに鳴らす。
    /// 動作の再生位置ではなく距離で数えるので、速さを変えても歩幅どおりに鳴る
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

        float walked;
        int last = -1;

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

        /// <summary>直前と同じ音は避ける。繰り返しが耳につくので</summary>
        void Play()
        {
            Steps++;
            if (source == null || clips.Length == 0) return;
            var pick = clips.Length == 1 ? 0 : Random.Range(0, clips.Length);
            if (clips.Length > 1 && pick == last) pick = (pick + 1) % clips.Length;
            last = pick;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clips[pick], 1f + Random.Range(-volumeJitter, volumeJitter));
        }
    }
}
