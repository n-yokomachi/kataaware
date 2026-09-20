using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 風防のワイパー。羽根を左右に振り、拭った跡をガラスの水（HalfAware/Screenwater）へ渡す。
    ///
    /// 羽根の角度は 中心 + 振り幅 × sin(位相) で、位相だけを進める。端で緩んで折り返すので、
    /// 一定の速さで往復させるより本物に近い。**水の側は状態を持たない。** ある画素の角を
    /// 羽根が最後に通ったのがいつかは sin を逆に解けば出るので、拭った跡を焼き付けておく
    /// 板（RenderTexture）は要らない。427 × 240・WebGL でそこまで抱えたくない。
    ///
    /// 渡す値は Shader.SetGlobal で置く。風防の水は場面にひとつしか無いので、
    /// マテリアルを複製するより素直で、SRP Batcher からも落ちない。
    /// 置かれていなければ振り幅も速さも 0 のままで、水は全面が溜まりきったままになる。
    ///
    /// 軸の位置は羽根の localPosition から読む。羽根と水の板は同じ親（Rain）の子で、
    /// 板の中心が親の原点、板の uv が親の xy 平面のメートルであることを当てにしている。
    /// 数を二重に持たずに済むかわり、羽根を別の親へ移すとここが狂う
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public sealed class Wipers : MonoBehaviour
    {
        // 既定値。ここが唯一の出どころで、組み立て（BuildDrive）は羽根の長さと
        // 伏せたときの角をここから引く。実測して決めた値なので、
        // 動かすなら運転席から撮り直すこと

        /// <summary>羽根が水を拭う内端。軸からの距離。m</summary>
        public const float Hub = 0.08f;
        /// <summary>羽根の先。軸からの距離。m。風防の丈 0.63 の 8 割を掃く</summary>
        public const float Reach = 0.50f;
        /// <summary>振りの中心。度。真上が 0、運転席の側（+x）へ回る向きが正</summary>
        public const float Centre = -35f;
        /// <summary>
        /// 振り幅。度。片側の値。
        ///
        /// 伏せた側（中心 - 振り幅 = -84 度）ではほとんど寝ていて、ボンネットの陰に隠れる。
        /// 起こした側（+14 度）で運転席の正面を横切る。運転席から風防を真っ直ぐ見た点は
        /// 軸から 4.5 度のところにあるので、ここを詰めると正面が拭われないまま残る
        /// </summary>
        public const float Sweep = 49f;
        /// <summary>
        /// 一往復にかける秒数。
        ///
        /// **音の輪がこの値で切ってある。** 雨とワイパーの音（<see cref="DriveSound"/>）は
        /// 包絡線の自己相関で出した 0.9485 秒の 14 倍を一周として繋いであるので、
        /// ここを動かすと羽根の払いと音が少しずつずれていく
        /// </summary>
        public const float Period = 0.9485f;
        /// <summary>
        /// 拭った跡が溜まりきるまでの秒数。
        ///
        /// **一往復より短く取る。** 振りの真ん中は半周ごと（0.47 秒）に拭われるので、
        /// ここを往復より長くすると真ん中がいつまでも溜まりきらず、
        /// 拭っているのかどうか画面から読めない。0.8 なら真ん中は 6 割まで戻り、
        /// 振りの端（ほぼ一往復ぶん待つ）は溜まりきる。
        /// 見ている正面ほど澄んで、隅ほど水が残る
        /// </summary>
        public const float Dry = 0.8f;

        [Tooltip("羽根。軸は localPosition から読むので、水の板と同じ親に同じ向きで置く")]
        [SerializeField] Transform[] blades = new Transform[0];
        [Tooltip("一往復にかける秒数")]
        [SerializeField] float period = Period;
        [Tooltip("振りの中心。度。真上が 0、運転席の側（+x）へ回る向きが正")]
        [SerializeField] float centre = Centre;
        [Tooltip("振り幅。度。片側の値。中心 ± この角のあいだを往復する")]
        [SerializeField] float sweep = Sweep;
        [Tooltip("羽根が水を拭う内端。軸からの距離。m")]
        [SerializeField] float inner = Hub;
        [Tooltip("羽根の先。軸からの距離。m")]
        [SerializeField] float outer = Reach;
        [Tooltip("拭った跡が溜まりきるまでの秒数。往復より短いと、戻る頃には溜まっている")]
        [SerializeField] float dry = Dry;

        static readonly int PivotId = Shader.PropertyToID("_WipePivot");
        static readonly int AimId = Shader.PropertyToID("_WipeAim");
        static readonly int SpanId = Shader.PropertyToID("_WipeSpan");
        static readonly int WashId = Shader.PropertyToID("_WipeWash");

        /// <summary>一周</summary>
        public const float Turn = Mathf.PI * 2f;

        /// <summary>
        /// 水の流れる時計を畳む長さ。秒。
        ///
        /// 水は升目を刻んで粒を起こすので、時計が大きくなると frac が桁落ちして
        /// 粒が階段状に飛ぶ。帯ひとつはこれより短いうえ、出すたびに 0 から数え直すので、
        /// 畳むところまで回ることはまず無い
        /// </summary>
        public const float Loop = 600f;

        float phase;
        float wash;

        /// <summary>今の位相。0 から一周まで。動作確認から読む</summary>
        public float Phase { get { return phase; } }

        /// <summary>水の流れる時計。秒</summary>
        public float Wash { get { return wash; } }

        /// <summary>位相の進む速さ。ラジアン毎秒</summary>
        public float Rate { get { return period > 0.0001f ? Turn / period : 0f; } }

        /// <summary>
        /// 位相 at のときの羽根の角。真上を 0、+x へ回る向きを正に取ったラジアン。
        /// シェーダーはこれを逆に解いて、画素ごとに最後に拭われた時刻を出す
        /// </summary>
        public static float Aim(float centre, float amp, float at)
        {
            return centre + amp * Mathf.Sin(at);
        }

        /// <summary>位相を一周の中へ収める</summary>
        public static float Wrap(float at)
        {
            return Mathf.Repeat(at, Turn);
        }

        /// <summary>
        /// 羽根の角（ラジアン）から、羽根の localRotation に入れる z の角（度）。
        ///
        /// Unity の z 回りの回転は +y を -x へ倒すので、こちらの取り方とは向きが逆になる
        /// </summary>
        public static float Lean(float aim)
        {
            return -aim * Mathf.Rad2Deg;
        }

        void OnEnable()
        {
            // 水は出すたびに 0 から溜め直す。伏せているあいだも時計を進めると、
            // 次に出したとき粒が別のところに湧いているように見える
            Set(phase, 0f);
        }

        void Update()
        {
            Set(phase + Time.deltaTime * Rate, wash + Time.deltaTime);
        }

        /// <summary>羽根だけを動かす。水の流れはそのまま</summary>
        public void Set(float at)
        {
            Set(at, wash);
        }

        /// <summary>
        /// 羽根を位相 at のところへ据え、水を flowing 秒ぶん流して、水の側へ値を渡す。
        /// 再生していなくても効くので、絵を撮るときはここを直に呼べばよい
        /// </summary>
        public void Set(float at, float flowing)
        {
            phase = Wrap(at);
            wash = Mathf.Repeat(flowing, Loop);
            var mid = centre * Mathf.Deg2Rad;
            var amp = sweep * Mathf.Deg2Rad;
            var lean = Lean(Aim(mid, amp, phase));
            var live = 0;
            var pivot = Vector4.zero;
            for (var i = 0; i < blades.Length; i++)
            {
                if (blades[i] == null) continue;
                blades[i].localRotation = Quaternion.Euler(0f, 0f, lean);
                var at2 = blades[i].localPosition;
                if (live == 0) { pivot.x = at2.x; pivot.y = at2.y; }
                else if (live == 1) { pivot.z = at2.x; pivot.w = at2.y; }
                live++;
            }
            if (live == 0) return;
            Shader.SetGlobalVector(PivotId, pivot);
            Shader.SetGlobalVector(AimId, new Vector4(mid, amp, Rate, phase));
            Shader.SetGlobalVector(SpanId, new Vector4(inner, outer, dry, Mathf.Min(live, 2)));
            Shader.SetGlobalFloat(WashId, wash);
        }
    }
}
