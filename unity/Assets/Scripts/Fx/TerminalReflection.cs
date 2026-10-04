using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の机のモニターの黒い画面に映る、主人公の映り込み。
    ///
    /// 鏡の物理どおりには撮らない。画面ごとに決めた向き（<see cref="Pane.yaw"/>・<see cref="Pane.pitch"/>）から、
    /// 主人公の決めた範囲（<see cref="extent"/>。鼻から胸の上まで、または髪から胸の上まで）が画面いっぱいに収まるように、
    /// 映り込みのカメラ（<see cref="Pane.camera"/>）で撮り、画面の面に貼った板（<see cref="Pane.face"/>）へ、
    /// 左右を返して、明るい所だけを薄く重ねる（加算。HalfAware/ScreenReflection）。
    ///
    /// **映っている主人公は煙草を吸っている最中**（<see cref="MirrorSmoking"/>。オーナー、2026-10-05）。右手を口元に添え、
    /// 人差し指と中指で挟んだ煙草を唇に当てては脇へ離し、口から吐いた煙と煙草の先から立つ煙が、顔と髪の前を流れる。
    /// 手は口元の右に置き、口元の左のほくろは見える。
    ///
    /// 主人公の性別は対面まで見せない（シナリオ設計 1 節）。範囲の外は画面の縁の外に置き、縁のきわも少し暗く沈める。
    /// 唇の色はほぼ抜く。髪は映り込みの写しだけ後ろへなでつけ、顔と髪は煙と手越しにぼんやりとしか見えない。
    ///
    /// 一人称のカメラは頭を映さない（体のレンダラーの頭の面は何も描かない素材）。映り込みのカメラが撮る間だけ、
    /// 主人公の写し（<see cref="mirror"/>。髪をなでつけた頭と、煙草を口元へ運んだ右腕を含む体）と灯り（<see cref="lamps"/>）と煙草と煙を点け、
    /// 場面の主人公の体（<see cref="original"/>。右腕は肘掛けに置いたまま）を伏せ、撮り終えたら戻す。
    /// 上げた腕と煙は一人称の視界には入らない。映り込みの板は、ほかの映り込みのカメラに撮られないよう、その間は伏せる。
    /// 映り込みのカメラは <see cref="MirrorLayer"/> の物（写し・煙草・煙）だけを撮り、人の周りは画面の黒のまま（組み立ての設定。部屋も撮る形に戻せる）。
    ///
    /// 端末を調べた独白の 2 ページ目（「こうして画面の反射で自分の顔が見られるからだ。」。原稿 docs/scenario/01-room.md の注記）から、独白を読み終えるまでだけ浮かべる。
    /// ほかの時は映り込みのカメラを止め、画面は黒のまま
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class TerminalReflection : MonoBehaviour
    {
        /// <summary>映り込みを出す画面一枚</summary>
        [System.Serializable]
        public sealed class Pane
        {
            [Tooltip("画面の板。前（+z）が画面の奥、上（+y）が画面の上")]
            public Transform screen;
            [Tooltip("画面の板の大きさ（m）")]
            public Vector2 size = new Vector2(0.81f, 0.48f);
            [Tooltip("画面の板の厚み（m）。映り込みの板は表の面のすぐ手前に置く")]
            public float thick = 0.012f;
            [Tooltip("映り込みの板（画面の面に貼る）")]
            public Renderer face;
            [Tooltip("映り込みのカメラ。出している間だけ動かす")]
            public Camera camera;
            [Tooltip("映す向き（度）。体の正面から、体の右へ回すと正。左右の画面の横顔寄りの角度")]
            public float yaw;
            [Tooltip("映す高さの角度（度）。上から見下ろすと正、下から見上げると負")]
            public float pitch;
            [Tooltip("この画面だけ、下の縁に来る所の目からの下がり（m）を変える。0 なら共通の値。" +
                "見下ろす画面では胸元が入りやすいので、鎖骨のあたりまで上げる")]
            public float bottom;
            [System.NonSerialized] public RenderTexture target;
        }

        /// <summary>
        /// 独白の何ページ目から映すか（0 から数える）。原稿の注記「2 ページ目と同時に、モニターに顔が映る」（オーナー、2026-09-28）。
        /// 前は 3 ページ目（「というのもほら、」を 1 ページに分けていた頃）
        /// </summary>
        public const int FromPage = 1;

        /// <summary>
        /// 映り込みの写し・煙草・煙を置く層。名の無い 30 番（ほかの物は置かない）。映り込みのカメラはこの層だけを撮り、人の周り（椅子・部屋）は映さない。
        /// 一人称のカメラは全ての層を撮るが、この層の物は映り込みのカメラが撮る間しか点いていない
        /// </summary>
        public const int MirrorLayer = 30;

        [SerializeField] SceneFlow flow;
        [Tooltip("独白を持つ調べる対象（端末）")]
        [SerializeField] Interactable source;
        [Tooltip("独白の何ページ目から映すか。0 から数える")]
        [SerializeField] int fromLine = FromPage;
        [Tooltip("浮かべるのと消すのにかける秒数")]
        [SerializeField] float fadeSeconds = 0.8f;
        [Tooltip("映り込みを出す画面")]
        [SerializeField] Pane[] panes = new Pane[0];
        [Tooltip("主人公の体。向きの基準")]
        [SerializeField] Transform body;
        [Tooltip("映り込みのカメラが撮る間だけ点ける、主人公の写し（髪をなでつけた頭と、煙草を口元へ運んだ右腕を含む体）")]
        [FormerlySerializedAs("head")]
        [SerializeField] Renderer mirror;
        [Tooltip("映り込みのカメラが撮る間だけ伏せる、場面の主人公の体（右腕は肘掛けに置いたまま）")]
        [SerializeField] Renderer original;
        [Tooltip("映り込みの中の煙草と煙。映っている間だけ動かす")]
        [SerializeField] MirrorSmoking smoking;
        [Tooltip("映り込みのカメラが撮る間だけ点ける灯り（口元を照らす灯りと、頭の後ろの壁を照らす灯り）")]
        [SerializeField] Light[] lamps = new Light[0];
        [Tooltip("映り込みのカメラの絵の大きさ（px）。画面の幅 1 m あたり。画面に貼る大きさの 2 倍ほどで撮り、" +
            "ぼかして重ねる（肩の輪郭の段と、タンクトップの紐の粒をならす。URP の設定で MSAA は効かない）")]
        [SerializeField] float pixelsPerMetre = 560f;
        [Tooltip("写す範囲。鼻から胸の上まで（Mouth）か、髪から胸の上まで（Face）")]
        [SerializeField] Extent extent = Extent.Mouth;
        [Tooltip("映り込みのカメラの、映す範囲の真ん中からの離れ（m）")]
        [SerializeField] float distance = 0.6f;

        /// <summary>写す範囲</summary>
        public enum Extent
        {
            /// <summary>鼻の頭から胸の上まで。目と髪は画面の上の縁の外</summary>
            Mouth,
            /// <summary>髪の上から胸の上まで。目と髪も枠に入れ、手と煙で覆う</summary>
            Face,
        }

        /// <summary>
        /// 範囲 e の、画面の上の縁と下の縁に来る所の、目からの下がり（m。上は負）。
        /// Mouth は鼻の下の方（3.5 cm）から胸の上（鎖骨のあたり、23 cm）。Face は髪の上の縁の少し上（14 cm 上）から胸の上（21 cm）
        /// </summary>
        public static Vector2 Span(Extent e)
        {
            return e == Extent.Face ? new Vector2(-0.14f, 0.21f) : new Vector2(0.035f, 0.23f);
        }

        /// <summary>範囲 e の、上の縁と下の縁から沈める幅（ScreenReflection の _Edge。uv）。Face は髪の上の縁を広めに沈める</summary>
        public static Vector4 Edge(Extent e)
        {
            return e == Extent.Face ? new Vector4(0.10f, 0.06f, 0f, 0f) : new Vector4(0.06f, 0.05f, 0f, 0f);
        }

        /// <summary>範囲 e の、主役にする所（ScreenReflection の _Focus。真ん中の uv と半径）。Mouth は顎と首、Face は口元から目のあたり</summary>
        public static Vector4 Focus(Extent e)
        {
            return e == Extent.Face ? new Vector4(0.5f, 0.55f, 0.75f, 0.85f) : new Vector4(0.5f, 0.65f, 0.7f, 0.8f);
        }

        /// <summary>写す範囲。撮り比べるときに切り替える</summary>
        public Extent Range
        {
            get { return extent; }
            set { extent = value; }
        }

        /// <summary>映り込みの中の煙草と煙（動作確認から読む）</summary>
        public MirrorSmoking Smoking => smoking;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int TexelId = Shader.PropertyToID("_Texel");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int FocusId = Shader.PropertyToID("_Focus");
        MaterialPropertyBlock block;
        float level;
        /// <summary>煙草と煙を動かしているか（映っている間）</summary>
        bool smoked;
        /// <summary>撮る支度の中か</summary>
        bool shooting;
        /// <summary>撮る間に伏せた、場面の主人公の体が点いていたか</summary>
        bool originalWas;

        /// <summary>今の濃さ（0〜1）。動作確認から読む</summary>
        public float Level => level;

        /// <summary>映り込みを出す画面（動作確認から読む）</summary>
        public IReadOnlyList<Pane> Panes => panes;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Show(false);
            if (mirror != null) mirror.enabled = false;
            Lamps(false);
        }

        void Lamps(bool on)
        {
            foreach (var l in lamps)
                if (l != null) l.enabled = on;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            Show(false);
        }

        void OnDestroy()
        {
            foreach (var p in panes)
                if (p != null && p.target != null) Destroy(p.target);
        }

        void LateUpdate()
        {
            if (flow == null || source == null) return;
            var want = Showing(source.Lines, flow.CurrentLine, fromLine) ? 1f : 0f;
            level = Mathf.MoveTowards(level, want, fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f);
            Show(level > 0f);
            Smoke(level > 0f);
            if (level <= 0f) return;
            var eye = flow.Player != null && flow.Player.Eye != null ? flow.Player.Eye.position : Camera.main.transform.position;
            Aim(eye, level);
        }

        /// <summary>映っている間だけ煙草と煙を動かす。出始めで始め（煙はもう漂っている形から）、消えきったら止める</summary>
        void Smoke(bool on)
        {
            if (smoking == null) return;
            if (on && !smoked) smoking.Begin();
            else if (!on && smoked) smoking.End();
            smoked = on;
            if (on) smoking.Tick(Time.deltaTime);
        }

        /// <summary>映り込みの板とカメラを点ける・消す</summary>
        void Show(bool on)
        {
            foreach (var p in panes)
            {
                if (p == null) continue;
                if (p.face != null) p.face.enabled = on;
                if (p.camera != null) p.camera.enabled = on;
            }
        }

        /// <summary>
        /// 目 eye の主人公を、画面ごとの向きから撮るように映り込みのカメラを置き、板を画面に貼る。濃さは alpha（0〜1）。
        /// 再生中は毎こま呼ぶ。エディタで撮るときは、これを呼んでから <see cref="RenderNow"/> で撮る
        /// </summary>
        public void Aim(Vector3 eye, float alpha)
        {
            if (block == null) block = new MaterialPropertyBlock();
            var facing = body != null ? Quaternion.Euler(0f, body.eulerAngles.y, 0f) : Quaternion.identity;
            var span = Span(extent);
            foreach (var p in panes)
            {
                if (p == null || p.screen == null || p.face == null || p.camera == null) continue;
                var bottom = p.bottom > 0f ? p.bottom : span.y;
                var centre = eye + Vector3.down * ((span.x + bottom) * 0.5f);
                var height = bottom - span.x;
                if (p.target == null)
                {
                    var w = Mathf.Max(16, Mathf.RoundToInt(p.size.x * pixelsPerMetre));
                    var h = Mathf.Max(16, Mathf.RoundToInt(p.size.y * pixelsPerMetre));
                    p.target = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "TerminalReflection", hideFlags = HideFlags.DontSave };
                    p.target.Create();
                }
                p.camera.targetTexture = p.target;

                // 映す範囲の真ん中を、画面ごとの向きから見る。範囲の高さが画面の高さいっぱいに収まる視野
                var pose = Framing(centre, facing, p.yaw, p.pitch, distance);
                p.camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                p.camera.ResetProjectionMatrix();
                p.camera.nearClipPlane = 0.05f;
                p.camera.fieldOfView = 2f * Mathf.Atan(height * 0.5f / distance) * Mathf.Rad2Deg;
                p.camera.aspect = p.size.x / p.size.y;

                // 板は画面の表の 2 mm 手前に、画面と同じ大きさで貼る
                var outward = -p.screen.forward;
                var front = p.screen.position + outward * (p.thick * 0.5f + 0.002f);
                p.face.transform.SetPositionAndRotation(front, p.screen.rotation);
                var parent = p.face.transform.parent != null ? p.face.transform.parent.lossyScale : Vector3.one;
                p.face.transform.localScale = new Vector3(p.size.x / parent.x, p.size.y / parent.y, 1f);

                p.face.GetPropertyBlock(block);
                block.SetTexture(BaseMap, p.target);
                block.SetColor(BaseColor, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, alpha)));
                block.SetVector(TexelId, new Vector4(1f / p.target.width, 1f / p.target.height, 0f, 0f));
                block.SetVector(EdgeId, Edge(extent));
                block.SetVector(FocusId, Focus(extent));
                p.face.SetPropertyBlock(block);
            }
        }

        bool Ours(Camera cam)
        {
            if (cam == null) return false;
            foreach (var p in panes)
                if (p != null && p.camera == cam) return true;
            return false;
        }

        void Begin(ScriptableRenderContext context, Camera cam)
        {
            if (!Ours(cam)) return;
            Shoot(true);
        }

        void End(ScriptableRenderContext context, Camera cam)
        {
            if (!Ours(cam)) return;
            Shoot(false);
        }

        /// <summary>
        /// 映り込みのカメラが撮る間の支度。主人公の写しと灯りと煙草と煙を点け、場面の主人公の体と映り込みの板を伏せる。off で元へ戻す。
        /// 写しの右腕は撮る直前に今の形へ置き直す（根を重ねる場面の主人公の鎖骨のボーンは、座った形が毎こま当て直す）
        /// </summary>
        void Shoot(bool on)
        {
            if (on && !shooting)
            {
                if (original != null) originalWas = original.enabled;
                if (smoking != null) smoking.Pose(smoking.Clock);
            }
            if (mirror != null) mirror.enabled = on;
            if (original != null && on != shooting) original.enabled = on ? false : originalWas;
            if (smoking != null) smoking.Shoot(on);
            Lamps(on);
            shooting = on;
            foreach (var p in panes)
                if (p != null && p.face != null) p.face.enabled = !on && level > 0f;
        }

        /// <summary>映り込みのカメラで一こまずつ撮る（エディタで確認するとき）。撮った後は、板を show のとおりに点けておく</summary>
        public void RenderNow(bool show)
        {
            level = show ? 1f : 0f;
            foreach (var p in panes)
            {
                if (p == null || p.camera == null) continue;
                Shoot(true);
                try
                {
                    p.camera.Render();
                }
                finally
                {
                    Shoot(false);
                }
            }
            foreach (var p in panes)
                if (p != null && p.face != null) p.face.enabled = show;
        }

        /// <summary>今出している行 current が、独白 lines の from 行目から後ろにあるか</summary>
        public static bool Showing(IReadOnlyList<string> lines, string current, int from)
        {
            if (lines == null || current == null) return false;
            for (var i = Mathf.Max(0, from); i < lines.Count; i++)
                if (lines[i] == current) return true;
            return false;
        }

        /// <summary>
        /// 点 centre を、体の向き facing から yaw 度（体の右へ回すと正）・pitch 度（上から見下ろすと正）回した向きの、
        /// distance 離れた所から見るカメラの置き方。上はいつも世界の上
        /// </summary>
        public static Pose Framing(Vector3 centre, Quaternion facing, float yaw, float pitch, float distance)
        {
            var y = yaw * Mathf.Deg2Rad;
            var x = pitch * Mathf.Deg2Rad;
            var local = new Vector3(Mathf.Sin(y) * Mathf.Cos(x), Mathf.Sin(x), Mathf.Cos(y) * Mathf.Cos(x));
            var at = centre + facing * local * distance;
            return new Pose(at, Quaternion.LookRotation(centre - at, Vector3.up));
        }
    }
}
