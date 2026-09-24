using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の机のモニターの黒い画面に映る、主人公の映り込み。
    ///
    /// モニターの画面を一枚ずつ鏡として、目をその画面の面で折り返した所に置いた映り込みのカメラ（<see cref="Pane.camera"/>）で
    /// 部屋と主人公の体を撮り、画面の面に貼った板（<see cref="Pane.face"/>）へ、明るい所だけを薄く重ねる（加算）。
    /// 画面ごとに向きが違うので、正面の画面には正面の、横や上の画面には斜めの頭と肩が映り、端の画面では見切れる。
    ///
    /// 主人公の性別は対面まで見せない（シナリオ設計 1 節）。目のあたりは影に沈め、口元と顎だけをうっすら明るくし、
    /// 胸元の高さから下は消す（板の色の出し方で決める。HalfAware/ScreenReflection）。
    ///
    /// 一人称のカメラは頭を映さない（体のレンダラーの頭の面は何も描かない素材）。映り込みのカメラが撮る間だけ、
    /// 頭を描く写し（<see cref="head"/>）を点け、撮り終えたら消す。映り込みの板は、ほかの映り込みのカメラに撮られないよう、その間は伏せる。
    ///
    /// 端末を調べた独白の 3 行目（「こうして反射で自分の顔が見られるからだ」）から、独白を読み終えるまでだけ浮かべる。
    /// ほかの時は映り込みのカメラを止め、画面は黒のまま
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class TerminalReflection : MonoBehaviour
    {
        /// <summary>鏡にする画面一枚</summary>
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
            [System.NonSerialized] public RenderTexture target;
        }

        [SerializeField] SceneFlow flow;
        [Tooltip("独白を持つ調べる対象（端末）")]
        [SerializeField] Interactable source;
        [Tooltip("独白の何行目から映すか。0 から数える")]
        [SerializeField] int fromLine = 2;
        [Tooltip("浮かべるのと消すのにかける秒数")]
        [SerializeField] float fadeSeconds = 0.8f;
        [Tooltip("鏡にする画面")]
        [SerializeField] Pane[] panes = new Pane[0];
        [Tooltip("映り込みのカメラが撮る間だけ点ける、頭の写し")]
        [SerializeField] Renderer head;
        [Tooltip("映り込みのカメラが撮る間だけ点ける灯り（顔の下半分を照らす）。無くてもよい")]
        [SerializeField] Light lamp;
        [Tooltip("映り込みのカメラの絵の大きさ（px）。画面の幅 1 m あたり。320×180 の画面の上で要る分だけ")]
        [SerializeField] float pixelsPerMetre = 200f;
        [Tooltip("目から顎の先までの下がり（m）")]
        [SerializeField] float chinDrop = 0.12f;
        [Tooltip("顔の幅の半分（m）")]
        [SerializeField] float faceHalf = 0.075f;
        [Tooltip("目から、胸元を消し始める高さと消し終える高さまでの下がり（m）")]
        [SerializeField] Vector2 chestDrop = new Vector2(0.27f, 0.34f);
        [Tooltip("映り込みを撮るときだけ目を下げる量（m）。座った目は下の段と上の段の画面の境の高さにあり、" +
            "そのままでは顔が下の段の画面の上の縁で切れる。下げると、下の段の画面に顔と肩が収まる")]
        [SerializeField] float eyeDrop = 0.18f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int EyeId = Shader.PropertyToID("_Eye");
        static readonly int ChestId = Shader.PropertyToID("_Chest");
        MaterialPropertyBlock block;
        float level;

        /// <summary>今の濃さ（0〜1）。動作確認から読む</summary>
        public float Level => level;

        /// <summary>鏡にする画面（動作確認から読む）</summary>
        public IReadOnlyList<Pane> Panes => panes;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Show(false);
            if (head != null) head.enabled = false;
            if (lamp != null) lamp.enabled = false;
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
        /// 目 eye から見た映り込みに合わせて、画面ごとに映り込みのカメラと板を置き、板の色の出し方を決める。濃さは alpha（0〜1）。
        /// 再生中は毎こま呼ぶ。エディタで撮るときは、これを呼んでから <see cref="RenderNow"/> で撮る
        /// </summary>
        public void Aim(Vector3 eye, float alpha)
        {
            if (block == null) block = new MaterialPropertyBlock();
            eye += Vector3.down * eyeDrop;
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

                // 画面の表の面（部屋の側）。外向きの向きは画面の手前（-z）
                var outward = -p.screen.forward;
                var front = p.screen.position + outward * (p.thick * 0.5f);
                var from = Mirror(eye, front, outward);
                var rotation = Quaternion.LookRotation(outward, p.screen.up);
                p.camera.transform.SetPositionAndRotation(from, rotation);
                var near = Mathf.Max(0.01f, Vector3.Dot(front - from, outward));
                var window = Window(from, rotation, front, p.size);
                p.camera.nearClipPlane = near;
                p.camera.projectionMatrix = Matrix4x4.Frustum(window.x, window.y, window.z, window.w, near, p.camera.farClipPlane);

                // 板は画面の表の 2 mm 手前に、画面と同じ大きさで貼る
                p.face.transform.SetPositionAndRotation(front + outward * 0.002f, p.screen.rotation);
                var parent = p.face.transform.parent != null ? p.face.transform.parent.lossyScale : Vector3.one;
                p.face.transform.localScale = new Vector3(p.size.x / parent.x, p.size.y / parent.y, 1f);

                // 目の映る所は、目からこの画面の面へ下ろした点。映り込みの顔は本物の半分の大きさで画面に載る
                var foot = eye - outward * Vector3.Dot(eye - front, outward);
                var u = Vector3.Dot(foot - front, p.screen.right) / p.size.x + 0.5f;
                var v = Vector3.Dot(foot - front, p.screen.up) / p.size.y + 0.5f;
                p.face.GetPropertyBlock(block);
                block.SetTexture(BaseMap, p.target);
                block.SetColor(BaseColor, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, alpha)));
                block.SetVector(EyeId, new Vector4(u, v, faceHalf * 0.5f / p.size.x, chinDrop * 0.5f / p.size.y));
                block.SetVector(ChestId, new Vector4(v - chestDrop.x * 0.5f / p.size.y, v - chestDrop.y * 0.5f / p.size.y, 0f, 0f));
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
            if (lamp != null) lamp.enabled = on;
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

        /// <summary>点 p を、点 planePoint を通り normal に直交する面で折り返した点</summary>
        public static Vector3 Mirror(Vector3 p, Vector3 planePoint, Vector3 normal)
        {
            normal = normal.normalized;
            return p - 2f * Vector3.Dot(p - planePoint, normal) * normal;
        }

        /// <summary>
        /// 位置 at・向き rotation のカメラから見た、真ん中 centre・大きさ size の四角（カメラの前の向きに直交する面の上）の、
        /// 左・右・下・上の縁（その面までの距離の所での、カメラの右と上の向きの m）。Matrix4x4.Frustum にそのまま渡す
        /// </summary>
        public static Vector4 Window(Vector3 at, Quaternion rotation, Vector3 centre, Vector2 size)
        {
            var d = centre - at;
            var x = Vector3.Dot(d, rotation * Vector3.right);
            var y = Vector3.Dot(d, rotation * Vector3.up);
            return new Vector4(x - size.x * 0.5f, x + size.x * 0.5f, y - size.y * 0.5f, y + size.y * 0.5f);
        }
    }
}
