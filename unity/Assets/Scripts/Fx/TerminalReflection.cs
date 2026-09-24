using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の机のモニターの黒い画面に映る、主人公の映り込み。
    ///
    /// 鏡の物理どおりには撮らない。画面ごとに決めた向き（<see cref="Pane.yaw"/>・<see cref="Pane.pitch"/>）から、
    /// 主人公の「鼻の頭から胸の上まで」（<see cref="rangeTop"/>〜<see cref="rangeBottom"/>）が画面いっぱいに収まるように、
    /// 映り込みのカメラ（<see cref="Pane.camera"/>）で撮り、画面の面に貼った板（<see cref="Pane.face"/>）へ、
    /// 左右を返して、明るい所だけを薄く重ねる（加算。HalfAware/ScreenReflection）。
    /// 正面の画面には正面の、左右の画面には横顔寄りの、上の段には見上げた顎と首の線が映り、どの画面にも自分がいるように見える。
    ///
    /// 主人公の性別は対面まで見せない（シナリオ設計 1 節）。鼻から上と胸から下は画面の縁の外に置き、
    /// 縁のきわも少し暗く沈める。唇の色はほぼ抜く。
    ///
    /// 一人称のカメラは頭を映さない（体のレンダラーの頭の面は何も描かない素材）。映り込みのカメラが撮る間だけ、
    /// 頭を描く写し（<see cref="head"/>）と灯り（<see cref="lamps"/>）を点け、撮り終えたら消す。
    /// 映り込みの板は、ほかの映り込みのカメラに撮られないよう、その間は伏せる。
    ///
    /// 端末を調べた独白の 3 行目（「こうして反射で自分の顔が見られるからだ」）から、独白を読み終えるまでだけ浮かべる。
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
            [System.NonSerialized] public RenderTexture target;
        }

        [SerializeField] SceneFlow flow;
        [Tooltip("独白を持つ調べる対象（端末）")]
        [SerializeField] Interactable source;
        [Tooltip("独白の何行目から映すか。0 から数える")]
        [SerializeField] int fromLine = 2;
        [Tooltip("浮かべるのと消すのにかける秒数")]
        [SerializeField] float fadeSeconds = 0.8f;
        [Tooltip("映り込みを出す画面")]
        [SerializeField] Pane[] panes = new Pane[0];
        [Tooltip("主人公の体。向きの基準")]
        [SerializeField] Transform body;
        [Tooltip("映り込みのカメラが撮る間だけ点ける、頭の写し")]
        [SerializeField] Renderer head;
        [Tooltip("映り込みのカメラが撮る間だけ点ける灯り（口元を照らす灯りと、頭の後ろの壁を照らす灯り）")]
        [SerializeField] Light[] lamps = new Light[0];
        [Tooltip("映り込みのカメラの絵の大きさ（px）。画面の幅 1 m あたり")]
        [SerializeField] float pixelsPerMetre = 320f;
        [Tooltip("画面の上の縁に来る所の、目からの下がり（m）。鼻の下の方")]
        [SerializeField] float rangeTop = 0.035f;
        [Tooltip("画面の下の縁に来る所の、目からの下がり（m）。胸の上（鎖骨のあたり）")]
        [SerializeField] float rangeBottom = 0.23f;
        [Tooltip("映り込みのカメラの、映す範囲の真ん中からの離れ（m）")]
        [SerializeField] float distance = 0.6f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int TexelId = Shader.PropertyToID("_Texel");
        MaterialPropertyBlock block;
        float level;

        /// <summary>今の濃さ（0〜1）。動作確認から読む</summary>
        public float Level => level;

        /// <summary>映り込みを出す画面（動作確認から読む）</summary>
        public IReadOnlyList<Pane> Panes => panes;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Show(false);
            if (head != null) head.enabled = false;
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
            if (level <= 0f) return;
            var eye = flow.Player != null && flow.Player.Eye != null ? flow.Player.Eye.position : Camera.main.transform.position;
            Aim(eye, level);
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
            var centre = eye + Vector3.down * ((rangeTop + rangeBottom) * 0.5f);
            var height = rangeBottom - rangeTop;
            foreach (var p in panes)
            {
                if (p == null || p.screen == null || p.face == null || p.camera == null) continue;
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

        /// <summary>映り込みのカメラが撮る間の支度。頭の写しと灯りを点け、映り込みの板を伏せる。off で元へ戻す</summary>
        void Shoot(bool on)
        {
            if (head != null) head.enabled = on;
            Lamps(on);
            foreach (var p in panes)
                if (p != null && p.face != null) p.face.enabled = !on && level > 0f;
        }

        /// <summary>映り込みのカメラで一こまずつ撮る（エディタで確かめるとき）。撮った後は、板を show のとおりに点けておく</summary>
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
