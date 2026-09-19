using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 屋根の下では雨を止める。粒に当たり判定を持たせると重いので、
    /// 覆いのある範囲を箱で持ち、そこへ入ったら降らせないようにする。
    /// 音も一緒に絞る。屋根の下で雨音がそのままだと外に居るように聞こえる
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class RainCover : MonoBehaviour
    {
        [Tooltip("屋根のある範囲。中心と大きさで指定する")]
        [SerializeField] Bounds[] shelters = new Bounds[0];
        [Tooltip("降りの量。屋根の下ではこの割合まで落とす")]
        [SerializeField] float under = 0f;
        [Tooltip("切り替えにかける秒数。境で急に止まると嘘に見える")]
        [SerializeField] float blend = 0.45f;
        [Tooltip("屋根の下での雨音の割合")]
        [SerializeField] float muffled = 0.35f;
        [SerializeField] Transform player;
        [SerializeField] AudioSource sound;

        ParticleSystem rain;
        float rate = -1f;
        float volume = -1f;
        float at = 1f;

        /// <summary>今このとき屋根の下か。動作確認から読む</summary>
        public bool Sheltered { get; private set; }

        /// <summary>at が屋根の下にあるか</summary>
        public static bool Covered(Vector3 at, Bounds[] shelters)
        {
            if (shelters == null) return false;
            for (var i = 0; i < shelters.Length; i++) if (shelters[i].Contains(at)) return true;
            return false;
        }

        void OnEnable()
        {
            rain = GetComponent<ParticleSystem>();
            if (rain != null && rate < 0f) rate = rain.emission.rateOverTime.constant;
            if (sound != null && volume < 0f) volume = sound.volume;
        }

        void Update()
        {
            if (rain == null || player == null) return;
            Sheltered = Covered(player.position, shelters);
            var want = Sheltered ? 0f : 1f;
            at = blend <= 0f ? want : Mathf.MoveTowards(at, want, Time.deltaTime / blend);
            var em = rain.emission;
            em.rateOverTime = Mathf.Lerp(rate * under, rate, at);
            if (sound != null) sound.volume = Mathf.Lerp(volume * muffled, volume, at);
        }
    }
}
