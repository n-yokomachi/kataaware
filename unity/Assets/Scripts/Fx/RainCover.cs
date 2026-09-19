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

        [Header("屋根の下の響き")]
        [Tooltip("足音に掛ける反響。屋根の下でだけ効かせる")]
        [SerializeField] AudioReverbFilter echo;
        [Tooltip("響く範囲。空なら屋根の範囲をそのまま使う。" +
                 "帆布の屋根は音を吸うので、石の小道だけを入れる")]
        [SerializeField] Bounds[] echoWalls = new Bounds[0];
        [Tooltip("屋根の下での響きの強さ。dB。0 が最大、-10000 で無音")]
        [SerializeField] float echoRoom = -700f;
        [Tooltip("外での響きの強さ。dB")]
        [SerializeField] float echoOutside = -10000f;
        [Tooltip("響きの長さ。秒。低い屋根の下なので短く")]
        [SerializeField] float echoDecay = 0.85f;
        [Tooltip("高い方の残り具合。dB。石と金けの屋根なので明るめに残す")]
        [SerializeField] float echoBright = -300f;

        float volume = -1f;
        /// <summary>今どれだけ屋根の下に寄っているか。0 が外、1 が屋根の下</summary>
        float at;
        /// <summary>響く範囲にどれだけ寄っているか</summary>
        float ringing;

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

        /// <summary>
        /// 反響の強さ。外では無音にして、屋根の下でだけ響かせる。
        /// 境で急に切り替えると嘘に聞こえるので、雨音と同じだけ渡す
        /// </summary>
        public static float Room(float outside, float under, float at)
        {
            return Mathf.Lerp(outside, under, Mathf.Clamp01(at));
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
            if (echo == null) return;
            // 響くのは石に囲まれたところだけ。帆布の屋根の下では響かせない
            var walls = echoWalls != null && echoWalls.Length > 0 ? echoWalls : shelters;
            var ring = Covered(player.position, walls) ? 1f : 0f;
            ringing = blend <= 0f ? ring : Mathf.MoveTowards(ringing, ring, Time.deltaTime / blend);
            // 既製の設定を当てると個々の値を触れなくなるので User にしておく
            if (echo.reverbPreset != AudioReverbPreset.User) echo.reverbPreset = AudioReverbPreset.User;
            echo.room = Room(echoOutside, echoRoom, ringing);
            echo.roomHF = echoBright;
            echo.decayTime = echoDecay;
            // 外では切っておく。掛けっぱなしにすると通り全体が屋内に聞こえる
            var wanted = ringing > 0.002f;
            if (echo.enabled != wanted) echo.enabled = wanted;
        }
    }
}
