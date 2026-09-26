using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// UI を粗い画素で見せる。HUD（字幕・印・暗転）とコンソールの Canvas を、画面より小さい
    /// RenderTexture へ描き、最近傍で画面いっぱいに引き伸ばして重ねる。3D の絵が 1/3 の解像度で
    /// 描かれているのに UI だけくっきりしていると、HTML の画面を上に貼ったように浮く。
    ///
    /// **粗さは <see cref="UiLook"/> の一か所の値。** 既定は 1/2（960×540 で中が 480×270）。
    /// 字はいちばん小さいものでも、この中で縦 10 画素ほど取れる大きさにしてある。
    ///
    /// UI を描くカメラには、全画面の後処理（Ps1・Daze）を持たないレンダラー（<see cref="UiLook.renderer"/>）を使わせ、
    /// パイプラインの解像度の縮小（renderScale、3D を 1/3 にしている値）はこのカメラを描く間だけ 1 に戻す。
    /// どちらも掛かると、UI が 1/3 のさらに半分に潰れたり、後処理で透けた地が塗られたりする。
    /// カメラは切っておき、毎フレームの最後に自分で描く（Canvas の組み直しを済ませてから描くため）。
    ///
    /// 場面の見出し（冒頭のカード・「続く」）は粗くしない。字幕でない表示なので、HudView が別の Canvas に分けてくっきり描く
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class UiLens : MonoBehaviour
    {
        public const int UiLayer = 5;

        /// <summary>粗く描いた UI を画面へ重ねる順。場面の見出しはこの一つ上に、くっきり描く</summary>
        public const int ShowOrder = 100;

        /// <summary>UI を描くカメラの置き場。3D の物が何も無い所</summary>
        static readonly Vector3 Away = new Vector3(0f, -5000f, 0f);

        static UiLens instance;
        static bool overridden;
        static float scaleOverride;

        UiLook look;
        Camera lens;
        float keptRenderScale = -1f;
        RenderTexture target;
        RawImage show;
        Material showMaterial;

        /// <summary>
        /// いまの粗さ。UI を画面の何分の一で描くか。
        /// 書き込むと <see cref="UiLook"/> の値より優先する（設定の画面から変えるとき）
        /// </summary>
        public static float Scale
        {
            get
            {
                if (overridden) return scaleOverride;
                var look = instance != null ? instance.look : null;
                return look != null ? look.scale : UiLook.DefaultScale;
            }
            set
            {
                overridden = true;
                scaleOverride = Mathf.Clamp(value, 0.25f, 1f);
            }
        }

        /// <summary>設定の画面などで上書きした粗さを捨てて、<see cref="UiLook"/> の値へ戻す</summary>
        public static void ResetScale()
        {
            overridden = false;
        }

        /// <summary>UI を描くカメラ。無ければ null</summary>
        public static Camera Lens { get { return instance != null ? instance.lens : null; } }

        /// <summary>いま描いている RenderTexture。無ければ null</summary>
        public RenderTexture Target { get { return target; } }

        public Camera Eye { get { return lens; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            instance = null;
            overridden = false;
        }

        /// <summary>遊んでいる間の一つを返す。無ければ作って場面をまたいで残す</summary>
        public static UiLens Ensure()
        {
            if (instance != null) return instance;
            var made = Make();
            DontDestroyOnLoad(made.gameObject);
            made.Stage();
            return made;
        }

        /// <summary>カメラだけ組む。画面へ重ねる層は付けない。エディタで撮るときはこれで作って、撮り終えたら消す</summary>
        public static UiLens Make()
        {
            var go = new GameObject("UiLens");
            var made = go.AddComponent<UiLens>();
            made.Build();
            if (instance == null) instance = made;
            return made;
        }

        /// <summary>
        /// canvas を粗い画面で描かせる。order は重なりの順で、大きいほど上。
        /// Canvas の配下はまとめて UI のレイヤーへ移す（UI を描くカメラはそれしか写さない）
        /// </summary>
        public static void Adopt(Canvas canvas, int order)
        {
            if (canvas == null) return;
            Ensure().Take(canvas, order);
        }

        public void Take(Canvas canvas, int order)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = lens;
            canvas.sortingOrder = order;
            // 上に重ねる物ほど手前に置く。並びの順と距離の両方で揃えておく
            canvas.planeDistance = Mathf.Max(1f, 10f - order * 0.01f);
            Layer(canvas.transform);
            // 当たりは画面の座標で来るので、粗い画面の座標へ直してから調べる
            var ray = canvas.GetComponent<GraphicRaycaster>();
            if (ray != null && !(ray is LensRaycaster))
            {
                var blocking = ray.blockingObjects;
                var reversed = ray.ignoreReversedGraphics;
                DestroyImmediate(ray);
                var lensRay = canvas.gameObject.AddComponent<LensRaycaster>();
                lensRay.blockingObjects = blocking;
                lensRay.ignoreReversedGraphics = reversed;
            }
        }

        static void Layer(Transform t)
        {
            t.gameObject.layer = UiLayer;
            for (var i = 0; i < t.childCount; i++) Layer(t.GetChild(i));
        }

        /// <summary>画面の座標を、粗い画面の座標へ直す</summary>
        public static Vector2 ToLens(Vector2 screen)
        {
            if (instance == null || instance.target == null || Screen.width <= 0 || Screen.height <= 0) return screen;
            return new Vector2(screen.x * instance.target.width / Screen.width, screen.y * instance.target.height / Screen.height);
        }

        void Build()
        {
            look = Resources.Load<UiLook>(UiLook.Path);
            var eye = new GameObject("Lens");
            eye.transform.SetParent(transform, false);
            eye.transform.position = Away;
            lens = eye.AddComponent<Camera>();
            lens.enabled = false;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(0f, 0f, 0f, 0f);
            lens.cullingMask = 1 << UiLayer;
            lens.orthographic = true;
            lens.orthographicSize = 1f;
            lens.nearClipPlane = 0.1f;
            lens.farClipPlane = 20f;
            lens.allowHDR = false;
            lens.allowMSAA = false;
            lens.useOcclusionCulling = false;
            var data = lens.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            var index = RendererIndex(look != null ? look.renderer : null);
            if (index >= 0) data.SetRenderer(index);
            else Debug.LogWarning("UiLens: UI のレンダラーがパイプラインの一覧に無い。後処理が UI にも掛かる", this);
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        /// <summary>いまのパイプラインのレンダラーの一覧で、renderer が何番か。無ければ -1</summary>
        static int RendererIndex(ScriptableRendererData renderer)
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null || renderer == null) return -1;
            var list = asset.rendererDataList;
            for (var i = 0; i < list.Length; i++) if (list[i] == renderer) return i;
            return -1;
        }

        /// <summary>UI を描く間だけ、解像度の縮小を外す。粗さは描く先の大きさ（<see cref="Scale"/>）だけで決める</summary>
        void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (camera != lens) return;
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return;
            keptRenderScale = asset.renderScale;
            asset.renderScale = 1f;
        }

        void End(ScriptableRenderContext context, Camera camera)
        {
            if (camera != lens || keptRenderScale < 0f) return;
            var asset = UniversalRenderPipeline.asset;
            if (asset != null) asset.renderScale = keptRenderScale;
            keptRenderScale = -1f;
        }

        /// <summary>粗く描いた絵を、画面いっぱいに最近傍で引き伸ばして重ねる層</summary>
        void Stage()
        {
            var go = new GameObject("Show", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ShowOrder;
            var img = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            img.transform.SetParent(go.transform, false);
            show = img.GetComponent<RawImage>();
            show.raycastTarget = false;
            var r = show.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            if (look != null && look.show != null) show.material = look.show;
            else
            {
                var shader = Shader.Find("HalfAware/UiLens");
                if (shader != null)
                {
                    showMaterial = new Material(shader);
                    showMaterial.hideFlags = HideFlags.DontSave;
                    show.material = showMaterial;
                }
                else Debug.LogWarning("UiLens: 重ねるシェーダーが無い。UI の縁が暗く見える", this);
            }
        }

        /// <summary>画面の大きさと粗さから、描く絵の大きさを決める。変わったときだけ作り直す</summary>
        public void Fit(int screenWidth, int screenHeight)
        {
            var s = Scale;
            var w = Mathf.Max(1, Mathf.RoundToInt(screenWidth * s));
            var h = Mathf.Max(1, Mathf.RoundToInt(screenHeight * s));
            if (target != null && target.width == w && target.height == h) return;
            Drop();
            // 深度は使わないが、URP の render graph はカメラの出力に深度を求める
            target = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.name = "UiLens";
            target.hideFlags = HideFlags.DontSave;
            target.filterMode = FilterMode.Point;
            target.wrapMode = TextureWrapMode.Clamp;
            target.useMipMap = false;
            target.antiAliasing = 1;
            target.Create();
            lens.targetTexture = target;
            if (show != null) show.texture = target;
        }

        /// <summary>組み直した Canvas を、粗い画面へ描く</summary>
        public void Draw()
        {
            if (lens == null || target == null) return;
            Canvas.ForceUpdateCanvases();
            lens.Render();
        }

        void LateUpdate()
        {
            Fit(Screen.width, Screen.height);
            Draw();
        }

        void Drop()
        {
            if (lens != null) lens.targetTexture = null;
            if (target == null) return;
            target.Release();
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
            target = null;
        }

        void OnDestroy()
        {
            Release();
        }

        /// <summary>
        /// 作った RenderTexture とマテリアルを捨てる。エディタでは OnDestroy が呼ばれないので、
        /// 撮り終えて消す前にこれを呼ぶ
        /// </summary>
        public void Release()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            if (keptRenderScale >= 0f)
            {
                var asset = UniversalRenderPipeline.asset;
                if (asset != null) asset.renderScale = keptRenderScale;
                keptRenderScale = -1f;
            }
            Drop();
            if (showMaterial != null)
            {
                if (Application.isPlaying) Destroy(showMaterial);
                else DestroyImmediate(showMaterial);
                showMaterial = null;
            }
            if (instance == this) instance = null;
        }
    }
}
