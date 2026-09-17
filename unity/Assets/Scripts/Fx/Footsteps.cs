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

        void Update()
        {
            if (body == null) return;
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
