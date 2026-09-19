using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 屋根の下での雨音。降り自体は粒が屋根に当たって消えるので、ここでは止めない。
    /// 屋根の下で雨音がそのままだと、外に立っているように聞こえる。
    /// 覆いのある範囲を箱で持ち、そこへ入ったら音だけ絞る
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class RainCover : MonoBehaviour
    {
        [Tooltip("屋根のある範囲。中心と大きさで指定する")]
        [SerializeField] Bounds[] shelters = new Bounds[0];
        [Tooltip("切り替えにかける秒数。境で急に変わると嘘に聞こえる")]
        [SerializeField] float blend = 0.45f;
        [Tooltip("屋根の下での雨音の割合")]
        [SerializeField] float muffled = 0.35f;
        [SerializeField] Transform player;
        [SerializeField] AudioSource sound;

        float volume = -1f;
        /// <summary>今どれだけ屋根の下に寄っているか。0 が外、1 が屋根の下</summary>
        float at;

        /// <summary>今このとき屋根の下か。動作確認から読む</summary>
        public bool Sheltered { get; private set; }

        /// <summary>at が屋根の下にあるか</summary>
        public static bool Covered(Vector3 at, Bounds[] shelters)
        {
            if (shelters == null) return false;
            for (var i = 0; i < shelters.Length; i++) if (shelters[i].Contains(at)) return true;
            return false;
        }

        /// <summary>屋根の下でどれだけ音を絞るか。0 が外、1 が屋根の下</summary>
        public static float Volume(float outside, float muffled, float under)
        {
            return Mathf.Lerp(outside, outside * muffled, Mathf.Clamp01(under));
        }

        void OnEnable()
        {
            if (sound != null && volume < 0f) volume = sound.volume;
        }

        void Update()
        {
            if (player == null) return;
            Sheltered = Covered(player.position, shelters);
            var want = Sheltered ? 1f : 0f;
            at = blend <= 0f ? want : Mathf.MoveTowards(at, want, Time.deltaTime / blend);
            if (sound != null) sound.volume = Volume(volume, muffled, at);
        }
    }
}
