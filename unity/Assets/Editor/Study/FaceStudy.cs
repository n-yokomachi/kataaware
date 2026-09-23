using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 主人公と片割れの顔を、どこまで作り込めるかを確かめる撮影台。
    ///
    /// 場面の中身から遠く離れた所（<see cref="Origin"/>、y = -500）に頭を一つ置き、
    /// 自前の灯りを当て、背景はカメラの地の色で撮る。撮る頭は <see cref="FaceSubject"/> を満たせば何でもよい
    /// （今の模型の段 0〜3 も、あとで落とす別の模型も、<see cref="FaceStages"/> と <see cref="FaceHeadSpec"/> で同じ台に載る）。
    ///
    /// 守ること:
    /// - 作る物はすべて HideAndDontSave で、一回の呼び出しの中で作って、撮って、壊す
    /// - 場面の環境光には触らない（頭の面ごとに球面調和を渡す）。眩暈の値だけは一時的に 0 にし、Dispose で戻す
    /// - 場面に点いている平行光があれば撮らずに止める（頭が場面の日で照らされてしまう）
    /// - 再生モードでもコンパイル中でも撮らない
    /// </summary>
    public static class FaceStudy
    {
        public const string OutDir = @"C:\Users\PC_User\AppData\Local\Temp\claude\D--work-kataaware\3eb6fc68-a1f9-4751-bb31-73277ee27f6d\scratchpad\face";

        /// <summary>頭を置く所。場面の中身から遠く離す</summary>
        public static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

        /// <summary>ゲームの主観カメラと同じ縦の画角</summary>
        public const float Fov = 70f;
        public const float Near = 0.08f;
        public const float Far = 8f;

        public const int GameW = 320, GameH = 180, BigW = 960, BigH = 540;

        /// <summary>顔の表面からカメラまで。頬に触れられる近さ、1 m、2 m</summary>
        public static readonly float[] Distances = { 0.4f, 1.0f, 2.0f };

        /// <summary>
        /// 向き。正のときカメラは本人の右（+x）へ回り込む。
        /// -30 は本人の左の頬（主人公のほくろの側）がこちらへ向く
        /// </summary>
        public static readonly float[] Yaws = { -30f, 0f, 30f };

        public static readonly Color Backdrop = new Color(0.10f, 0.10f, 0.11f);

        /// <summary>端末の消えている画面の色（BuildConnect.ScreenOff と同じ）</summary>
        public static readonly Color ScreenOff = new Color(0.035f, 0.040f, 0.045f);

        /// <summary>
        /// 印の絵の閾値。印の白黒の境で線形の被りが半分になる所を、sRGB の出力で読むと 188。
        /// 印の地（本人の体）は灰 0.15 で塗るので、どの色も 20 未満なら背景
        /// </summary>
        public const byte MarkCut = 188;
        public const byte OtherCut = 100;
        public const byte BodyCut = 20;

        public enum Lighting { Soft, Side }

        // ---- 撮影台 ---------------------------------------------------------

        /// <summary>
        /// 灯りとカメラ。using で囲む。作ると眩暈の値を 0 にし、Dispose で戻す
        /// </summary>
        public sealed class Rig : IDisposable
        {
            readonly List<Object> made = new List<Object>();
            SphericalHarmonicsL2 ambient;
            readonly float keepBlur, keepWobble;
            readonly Camera cam;
            readonly Material bodyMask;
            readonly Dictionary<Texture, Material> markMats = new Dictionary<Texture, Material>();
            public readonly Lighting Setting;

            public Rig(Lighting light)
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("再生中は撮らない");
                if (EditorApplication.isCompiling)
                    throw new InvalidOperationException("コンパイル中は撮らない");
                var foreign = ForeignLights();
                if (foreign.Length > 0)
                    throw new InvalidOperationException("場面の灯りが点いている: " + foreign);

                Setting = light;
                keepBlur = Shader.GetGlobalFloat("_DazeBlur");
                keepWobble = Shader.GetGlobalFloat("_DazeWobble");
                try
                {
                    Shader.SetGlobalFloat("_DazeBlur", 0f);
                    Shader.SetGlobalFloat("_DazeWobble", 0f);
                    SetUpLights(light);

                    var go = Keep(new GameObject("FaceStudyEye"));
                    cam = go.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.fieldOfView = Fov;
                    cam.nearClipPlane = Near;
                    cam.farClipPlane = Far;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Backdrop;
                    cam.allowMSAA = false;
                    cam.allowHDR = true;
                    var data = cam.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    data.volumeLayerMask = 0;
                    data.antialiasing = AntialiasingMode.None;
                    data.renderShadows = true;
                    data.dithering = false;

                    bodyMask = Unlit(new Color(0.15f, 0.15f, 0.15f), null, false);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            /// <summary>場面の灯りのうち、頭まで届くもの（点いている平行光と、範囲に入る点光源・スポット）</summary>
            static string ForeignLights()
            {
                var names = new List<string>();
                foreach (var l in Object.FindObjectsByType<UnityEngine.Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!l.isActiveAndEnabled || (l.hideFlags & HideFlags.DontSave) != 0) continue;
                    if (l.type == LightType.Directional || Vector3.Distance(l.transform.position, Origin) < l.range + 3f)
                        names.Add(l.name);
                }
                return string.Join(", ", names.ToArray());
            }

            void SetUpLights(Lighting light)
            {
                var sh = new SphericalHarmonicsL2();
                sh.Clear();
                if (light == Lighting.Soft)
                {
                    // 柔らかい暖かい光。斜め前上から本人の左へ寄せた主光と、反対から弱い補い、暖かい環境光
                    sh.AddAmbientLight(new Color(0.30f, 0.26f, 0.22f));
                    Directional("Key", -35f, 30f, new Color(1.00f, 0.86f, 0.70f), 0.85f, LightShadows.Soft, 0.55f);
                    Directional("Fill", 50f, 10f, new Color(0.95f, 0.88f, 0.80f), 0.30f, LightShadows.None, 0f);
                }
                else
                {
                    // 横からの強い光。本人の右（髪の薄い側）の斜め横から、影は硬く、環境光はほとんど無し。
                    // 本人の左から当てると、左に垂れた髪に遮られて顔に光が届かない
                    sh.AddAmbientLight(new Color(0.05f, 0.05f, 0.06f));
                    Directional("Key", 65f, 15f, new Color(1.00f, 0.96f, 0.90f), 2.0f, LightShadows.Hard, 1f);
                }
                ambient = sh;
            }

            /// <summary>
            /// 頭に環境光を渡す。場面の環境光（RenderSettings）には触らず、面ごとに球面調和を持たせる。
            /// RenderSettings.ambientProbe を書き換えても、エディタで撮るカメラには届かなかった
            /// </summary>
            public void Light(FaceSubject who)
            {
                var block = new MaterialPropertyBlock();
                var arr = new[] { ambient };
                foreach (var r in who.Root.GetComponentsInChildren<Renderer>(true))
                {
                    r.lightProbeUsage = LightProbeUsage.CustomProvided;
                    r.GetPropertyBlock(block);
                    block.CopySHCoefficientArraysFrom(arr);
                    r.SetPropertyBlock(block);
                }
            }

            /// <summary>
            /// 平行光を一つ。az は顔の正面（+z）から本人の右（+x）へ回る角度、el は仰角。
            /// 光の来る向きを決め、光はその逆へ進む
            /// </summary>
            void Directional(string name, float az, float el, Color col, float intensity, LightShadows shadows, float strength)
            {
                var go = Keep(new GameObject("FaceStudy" + name));
                var from = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
                go.transform.rotation = Quaternion.LookRotation(-from, Vector3.up);
                go.transform.position = Origin + from * 3f;
                var l = go.AddComponent<UnityEngine.Light>();
                l.type = LightType.Directional;
                l.color = col;
                l.intensity = intensity;
                l.shadows = shadows;
                l.shadowStrength = strength;
                l.shadowBias = 0.02f;
                l.shadowNormalBias = 0.2f;
            }

            GameObject Keep(GameObject go)
            {
                go.hideFlags = HideFlags.HideAndDontSave;
                made.Add(go);
                return go;
            }

            public Material Unlit(Color col, Texture tex, bool clip)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.hideFlags = HideFlags.HideAndDontSave;
                m.SetColor("_BaseColor", col);
                if (tex != null) m.SetTexture("_BaseMap", tex);
                if (clip)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.35f);
                    m.EnableKeyword("_ALPHATEST_ON");
                }
                made.Add(m);
                return m;
            }

            /// <summary>暗い硝子。カメラと顔の間に置く半透明の板の材料</summary>
            public Material Glass(Color col, float alpha)
            {
                var m = Unlit(new Color(col.r, col.g, col.b, alpha), null, false);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                return m;
            }

            public GameObject Quad(string name, Material mat)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                Keep(go);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                return go;
            }

            public void Place(Vector3 eye, Vector3 target)
            {
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.LookRotation(target - eye, Vector3.up);
            }

            public Camera Camera { get { return cam; } }

            public Texture2D Render(int w, int h)
            {
                return Grab(cam, w, h);
            }

            /// <summary>
            /// 印の絵で撮る。頭のすべての面を灰 0.15 の無灯火にし、印を持つ面だけ印の絵に差し替える。
            /// 撮り終えたら元のマテリアルへ戻す
            /// </summary>
            public Texture2D RenderMarks(FaceSubject who, int w, int h, IList<GameObject> hide)
            {
                var keep = new Dictionary<Renderer, Material[]>();
                var bg = cam.backgroundColor;
                var data = cam.GetUniversalAdditionalCameraData();
                try
                {
                    foreach (var r in who.Root.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = r.sharedMaterials;
                        keep[r] = ms;
                        var next = new Material[ms.Length];
                        for (var i = 0; i < next.Length; i++) next[i] = bodyMask;
                        r.sharedMaterials = next;
                    }
                    foreach (var s in who.MaskSlots)
                    {
                        if (s.renderer == null) continue;
                        var ms = s.renderer.sharedMaterials;
                        if (s.slot < 0 || s.slot >= ms.Length) continue;
                        Material mm;
                        if (!markMats.TryGetValue(s.mask, out mm))
                        {
                            mm = Unlit(Color.white, s.mask, true);
                            markMats[s.mask] = mm;
                        }
                        ms[s.slot] = mm;
                        s.renderer.sharedMaterials = ms;
                    }
                    if (hide != null) foreach (var g in hide) if (g != null) g.SetActive(false);
                    cam.backgroundColor = Color.black;
                    // 印は後処理を通さない（トーンマップと周辺減光で白が落ちる）。
                    // 後処理を切ると PS1 の減色も掛からず、拡大は双線形になるが、塊の真ん中の画素は中の画素そのもの
                    data.renderPostProcessing = false;
                    return Grab(cam, w, h);
                }
                finally
                {
                    data.renderPostProcessing = true;
                    cam.backgroundColor = bg;
                    if (hide != null) foreach (var g in hide) if (g != null) g.SetActive(true);
                    foreach (var kv in keep) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
                }
            }

            public void Dispose()
            {
                Shader.SetGlobalFloat("_DazeBlur", keepBlur);
                Shader.SetGlobalFloat("_DazeWobble", keepWobble);
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                made.Clear();
            }
        }

        /// <summary>
        /// パイプラインの render scale の逆数。今の PC_RPAsset は 1/3 なので 3。
        /// render scale はテクスチャへ撮るカメラにも掛かる（960×540 へ撮ると中は 320×180 で、点で拡大される）
        /// </summary>
        public static int Factor
        {
            get
            {
                var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (rp == null || rp.renderScale >= 0.999f) return 1;
                return Mathf.Max(1, Mathf.RoundToInt(1f / rp.renderScale));
            }
        }

        /// <summary>
        /// カメラの絵を読み出す。sRGB のまま。中の解像度がちょうど w×h になるよう、
        /// w×h の <see cref="Factor"/> 倍の大きさへ撮り、拡大の塊の真ん中の画素を拾う。
        /// 後処理が点いていれば、URP の後処理と PS1 の減色・ディザを通った後の絵
        /// </summary>
        public static Texture2D Grab(Camera cam, int w, int h)
        {
            var f = Factor;
            int W = w * f, H = h * f;
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var keep = RenderTexture.active;
            var target = cam.targetTexture;
            Texture2D full = null;
            try
            {
                cam.targetTexture = rt;
                cam.aspect = w / (float)h;
                cam.Render();
                RenderTexture.active = rt;
                full = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
                full.hideFlags = HideFlags.HideAndDontSave;
                full.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                full.Apply();
                var src = full.GetPixels32();
                var dst = new Color32[w * h];
                var o = f / 2;
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                        dst[y * w + x] = src[(y * f + o) * W + x * f + o];
                var shot = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                shot.hideFlags = HideFlags.HideAndDontSave;
                shot.SetPixels32(dst);
                shot.Apply();
                return shot;
            }
            finally
            {
                cam.targetTexture = target;
                RenderTexture.active = keep;
                RenderTexture.ReleaseTemporary(rt);
                if (full != null) Object.DestroyImmediate(full);
            }
        }

        public static void Save(Texture2D shot, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, shot.EncodeToPNG());
        }

        // ---- 一揃いを撮る ----------------------------------------------------

        /// <summary>
        /// 一つの頭を、一つの光で、三つの近さ × 三つの向きで撮って測る。
        /// 絵は <see cref="OutDir"/> へ <c>{tag}_{光}_{d}m_{向き}.png</c>（320×180）と <c>..._960.png</c>。
        /// 測った数は measure.csv に一行ずつ書き、要約を返す
        /// </summary>
        public static string ShootSet(Func<FaceSubject> make, string tag, Lighting light)
        {
            var sb = new StringBuilder();
            using (var rig = new Rig(light))
            using (var who = make())
            {
                rig.Light(who);
                sb.AppendLine(tag + " " + light + ": " + who.Describe());
                foreach (var d in Distances)
                    foreach (var yaw in Yaws)
                    {
                        var name = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2:0.0}m_{3}", tag, LightName(light), d, YawName(yaw));
                        var m = ShootOne(rig, who, d, yaw, name, null);
                        Record(tag, light.ToString(), d, yaw, m);
                        sb.AppendLine(name + " " + m.Short());
                    }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 端末の黒い画面への映り込み。今の場面には映り込みの作りが無いので、「暗い硝子に映った顔」として撮る。
        /// 頭を左右に裏返し（鏡像）、画面から顔までの距離の倍だけ離れたカメラから、
        /// 間に消えた画面の色の半透明の板を挟む（明るさを落とし、コントラストを弱める）
        /// </summary>
        public static string ShootMirror(Func<FaceSubject> make, string tag, float glassAlpha = 0.72f)
        {
            var sb = new StringBuilder();
            using (var rig = new Rig(Lighting.Soft))
            using (var who = make())
            {
                rig.Light(who);
                var s = who.Root.transform.localScale;
                who.Root.transform.localScale = new Vector3(-s.x, s.y, s.z);
                var glass = rig.Quad("FaceStudyGlass", rig.Glass(ScreenOff, glassAlpha));
                sb.AppendLine(tag + " mirror: " + who.Describe());
                // 画面から目まで 0.6 m（座って覗く）と 0.4 m（身を乗り出す）
                foreach (var gap in new[] { 0.6f, 0.4f })
                {
                    var d = gap * 2f;
                    var name = string.Format(CultureInfo.InvariantCulture, "{0}_mirror_{1:0.0}m", tag, gap);
                    var m = ShootOne(rig, who, d, 0f, name, glass);
                    Record(tag, "Mirror", gap, 0f, m);
                    sb.AppendLine(name + " " + m.Short());
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 描くための寄りの一枚。画角を絞って顔だけを 960×540 で撮る。測らない。ゲームの見え方ではない
        /// </summary>
        public static string Peek(Func<FaceSubject> make, string name, Lighting light, float fov, float d, float yaw)
        {
            using (var rig = new Rig(light))
            using (var who = make())
            {
                rig.Light(who);
                var c = who.FaceCentre;
                rig.Place(c + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * d, c);
                rig.Camera.fieldOfView = fov;
                var shot = rig.Render(BigW, BigH);
                try { Save(shot, Path.Combine(OutDir, "peek", name + ".png")); }
                finally { Object.DestroyImmediate(shot); }
                return who.Describe();
            }
        }

        static Measure ShootOne(Rig rig, FaceSubject who, float d, float yaw, string name, GameObject glass)
        {
            var c = who.FaceCentre;
            var eye = c + Quaternion.AngleAxis(yaw, Vector3.up) * who.Forward * d;
            rig.Place(eye, c);
            if (glass != null)
            {
                glass.transform.position = eye + (c - eye).normalized * 0.12f;
                glass.transform.rotation = Quaternion.LookRotation(c - eye, Vector3.up);
                glass.transform.localScale = Vector3.one * 0.5f;
            }
            var hide = glass != null ? new List<GameObject> { glass } : null;

            var small = rig.Render(GameW, GameH);
            var big = rig.Render(BigW, BigH);
            var markSmall = rig.RenderMarks(who, GameW, GameH, hide);
            var markBig = rig.RenderMarks(who, BigW, BigH, hide);
            Texture2D bare = null;
            if (who.MoleOff.Count > 0)
            {
                var keep = who.HideMole();
                try { bare = rig.Render(GameW, GameH); }
                finally { FaceSubject.Restore(keep); }
            }
            try
            {
                Save(small, Path.Combine(OutDir, name + ".png"));
                Save(big, Path.Combine(OutDir, name + "_960.png"));
                var px = small.GetPixels32();
                var m = Measure.Take(px, markSmall.GetPixels32(), markBig.GetPixels32());
                m.faceW = ProjectedWidth(rig.Camera, who, GameW, GameH);
                if (bare != null) m.MoleChange(px, bare.GetPixels32());
                return m;
            }
            finally
            {
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(markSmall);
                Object.DestroyImmediate(markBig);
                if (bare != null) Object.DestroyImmediate(bare);
            }
        }

        /// <summary>顔の幅（目の高さの肌の左右の広がり）が画面上で何画素か。320×180 で</summary>
        static float ProjectedWidth(Camera cam, FaceSubject who, int w, int h)
        {
            cam.aspect = w / (float)h;
            var c = who.EyeLine;
            var r = who.Right * (who.FaceWidth * 0.5f);
            var a = cam.WorldToViewportPoint(c - r);
            var b = cam.WorldToViewportPoint(c + r);
            return Mathf.Abs(b.x - a.x) * w;
        }

        public static string LightName(Lighting l) { return l == Lighting.Soft ? "soft" : "side"; }

        public static string YawName(float yaw)
        {
            if (Mathf.Abs(yaw) < 0.5f) return "front";
            // 正はカメラが本人の右へ回る
            return (yaw > 0f ? "R" : "L") + Mathf.RoundToInt(Mathf.Abs(yaw));
        }

        // ---- 測る ------------------------------------------------------------

        public struct Measure
        {
            public float faceW;
            public float ipd;
            public int molePx;
            public bool moleSub;
            public bool moleSeen;
            public float moleLuma;
            public float ringLuma;
            public int ringPx;
            public int irisPx;
            public int amberPx;
            public float irisH, irisS, irisV;
            /// <summary>ほくろを消した絵と比べて、明るさが 4 以上変わった画素の数と、変わった量の最大。比べていなければ -1</summary>
            public int changePx;
            /// <summary>両目の虹彩の中心の画素（320×180）の平均の色相・彩度・明度</summary>
            public float eyeH, eyeS, eyeV;
            public float changeMax;

            /// <summary>
            /// ほくろ有りと無しの同じ構図（320×180）を比べる。PS1 の減色は 1 段が約 8 なので、
            /// 4 以上の差は少なくとも 1 段ずれた画素
            /// </summary>
            public void MoleChange(Color32[] with, Color32[] without)
            {
                changePx = 0;
                changeMax = 0f;
                for (var i = 0; i < with.Length; i++)
                {
                    var d = Mathf.Abs(Luma(with[i]) - Luma(without[i]));
                    if (d >= 4f) changePx++;
                    if (d > changeMax) changeMax = d;
                }
            }

            public float DeltaL { get { return ringLuma - moleLuma; } }

            public string Short()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "顔幅 {0:0.0}px 両目 {1:0.0}px ほくろ {2}{3}px ΔL {4:0.0} 変わる画素 {10}（最大 {11:0}）虹彩 {5}px（琥珀 {6}px）H {7:0} S {8:0.00} V {9:0.00} 目の中心 H {12:0} S {13:0.00} V {14:0.00}",
                    faceW, ipd, molePx, moleSub ? "(画素未満)" : "", moleSeen ? DeltaL : float.NaN, irisPx, amberPx, irisH, irisS, irisV, changePx, changeMax, eyeH, eyeS, eyeV);
            }

            /// <summary>
            /// img はふつうに撮った 320×180、mark は同じ構図の印の絵（320×180 と 960×540）。
            /// ほくろ = 印の R、虹彩 = 印の G、他の部品 = 印の B。
            /// ほくろの周りの肌は、ほくろの画素から 1〜3 画素の範囲で、どの印にも掛からない本人の画素
            /// </summary>
            public static Measure Take(Color32[] img, Color32[] mark, Color32[] markBig)
            {
                var m = new Measure();
                m.changePx = -1;
                const int w = GameW, h = GameH;
                var mole = new List<int>();
                for (var i = 0; i < mark.Length; i++)
                    if (mark[i].r >= MarkCut && mark[i].g < OtherCut && mark[i].b < OtherCut) mole.Add(i);

                // 320×180 で一画素も取れないときは、960×540 の印の重心の画素を見る
                if (mole.Count == 0)
                {
                    float sx = 0, sy = 0; var n = 0;
                    for (var i = 0; i < markBig.Length; i++)
                        if (markBig[i].r >= MarkCut && markBig[i].g < OtherCut && markBig[i].b < OtherCut)
                        {
                            sx += i % BigW; sy += i / BigW; n++;
                        }
                    if (n > 0)
                    {
                        var x = Mathf.Clamp(Mathf.FloorToInt(sx / n / 3f), 0, w - 1);
                        var y = Mathf.Clamp(Mathf.FloorToInt(sy / n / 3f), 0, h - 1);
                        mole.Add(y * w + x);
                        m.moleSub = true;
                    }
                }
                m.molePx = m.moleSub ? 0 : mole.Count;
                m.moleSeen = mole.Count > 0;

                if (m.moleSeen)
                {
                    var inMole = new HashSet<int>(mole);
                    var ring = new HashSet<int>();
                    foreach (var i in mole)
                    {
                        int x0 = i % w, y0 = i / w;
                        for (var dy = -3; dy <= 3; dy++)
                            for (var dx = -3; dx <= 3; dx++)
                            {
                                int x = x0 + dx, y = y0 + dy;
                                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                                var j = y * w + x;
                                if (inMole.Contains(j)) continue;
                                var k = mark[j];
                                var body = k.r >= BodyCut || k.g >= BodyCut || k.b >= BodyCut;
                                var other = k.r >= OtherCut || k.g >= OtherCut || k.b >= OtherCut;
                                if (body && !other) ring.Add(j);
                            }
                    }
                    m.moleLuma = MeanLuma(img, mole);
                    m.ringPx = ring.Count;
                    m.ringLuma = ring.Count > 0 ? MeanLuma(img, ring) : float.NaN;
                }

                // 虹彩
                float r = 0, g = 0, b = 0;
                for (var i = 0; i < mark.Length; i++)
                {
                    if (mark[i].g < MarkCut || mark[i].r >= OtherCut) continue;
                    m.irisPx++;
                    var c = img[i];
                    r += c.r; g += c.g; b += c.b;
                    float hh, ss, vv;
                    Color.RGBToHSV(c, out hh, out ss, out vv);
                    if (hh * 360f >= 15f && hh * 360f <= 50f && ss >= 0.35f && vv >= 0.15f) m.amberPx++;
                }
                if (m.irisPx > 0)
                {
                    var mean = new Color(r / m.irisPx / 255f, g / m.irisPx / 255f, b / m.irisPx / 255f);
                    Color.RGBToHSV(mean, out m.irisH, out m.irisS, out m.irisV);
                    m.irisH *= 360f;
                }
                else { m.irisH = m.irisS = m.irisV = float.NaN; }

                // 両目の間隔。960×540 の虹彩の印を左右に分け、重心の間を 1/3 する
                Vector2 ca, cb;
                m.ipd = IrisSpan(markBig, out ca, out cb);
                // 虹彩の中心の画素。虹彩が画素の半分を覆わない近さでも、目の所の色を見る
                m.eyeH = m.eyeS = m.eyeV = float.NaN;
                if (!float.IsNaN(m.ipd))
                {
                    var er = 0f; var eg = 0f; var eb = 0f;
                    foreach (var c in new[] { ca, cb })
                    {
                        var x = Mathf.Clamp(Mathf.FloorToInt(c.x / 3f), 0, w - 1);
                        var y = Mathf.Clamp(Mathf.FloorToInt(c.y / 3f), 0, h - 1);
                        var p = img[y * w + x];
                        er += p.r / 510f; eg += p.g / 510f; eb += p.b / 510f;
                    }
                    Color.RGBToHSV(new Color(er, eg, eb), out m.eyeH, out m.eyeS, out m.eyeV);
                    m.eyeH *= 360f;
                }
                return m;
            }

            static float IrisSpan(Color32[] big, out Vector2 a, out Vector2 bb)
            {
                a = bb = Vector2.zero;
                var xs = new List<Vector2>();
                for (var i = 0; i < big.Length; i++)
                    if (big[i].g >= MarkCut && big[i].r < OtherCut) xs.Add(new Vector2(i % BigW, i / BigW));
                if (xs.Count < 2) return float.NaN;
                // 二つに分ける。x の最小と最大を種に、二度だけ振り分け直す
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in xs) { lo = Mathf.Min(lo, p.x); hi = Mathf.Max(hi, p.x); }
                a = new Vector2(lo, 0); bb = new Vector2(hi, 0);
                for (var it = 0; it < 4; it++)
                {
                    Vector2 sa = Vector2.zero, sb = Vector2.zero; int na = 0, nb = 0;
                    foreach (var p in xs)
                    {
                        if (Mathf.Abs(p.x - a.x) <= Mathf.Abs(p.x - bb.x)) { sa += p; na++; } else { sb += p; nb++; }
                    }
                    if (na == 0 || nb == 0) return float.NaN;
                    a = sa / na; bb = sb / nb;
                }
                return Vector2.Distance(a, bb) / 3f;
            }

            static float MeanLuma(Color32[] img, IEnumerable<int> idx)
            {
                double s = 0; var n = 0;
                foreach (var i in idx) { s += Luma(img[i]); n++; }
                return n > 0 ? (float)(s / n) : float.NaN;
            }
        }

        public static float Luma(Color32 c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; }

        const string CsvHead = "tag,light,distance,yaw,face_px,ipd_px,mole_px,mole_subpixel,mole_luma,ring_luma,delta_luma,iris_px,amber_px,iris_h,iris_s,iris_v,mole_change_px,mole_change_max,eye_centre_h,eye_centre_s,eye_centre_v";

        static void Record(string tag, string light, float d, float yaw, Measure m)
        {
            Directory.CreateDirectory(OutDir);
            var path = Path.Combine(OutDir, "measure.csv");
            var fresh = !File.Exists(path);
            // 同じ tag・光・近さ・向きの古い行は消してから書く
            var lines = new List<string>();
            var key = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.0},{3:0},", tag, light, d, yaw);
            if (!fresh)
                foreach (var l in File.ReadAllLines(path))
                    if (!l.StartsWith(key, StringComparison.Ordinal)) lines.Add(l);
            if (lines.Count == 0 || lines[0] != CsvHead) lines.Insert(0, CsvHead);
            lines.Add(key + string.Format(CultureInfo.InvariantCulture,
                "{0:0.0},{1:0.0},{2},{3},{4:0.0},{5:0.0},{6:0.0},{7},{8},{9:0},{10:0.00},{11:0.00},{12},{13:0},{14:0},{15:0.00},{16:0.00}",
                m.faceW, m.ipd, m.molePx, m.moleSub ? 1 : 0, m.moleLuma, m.ringLuma, m.moleSeen ? m.DeltaL : float.NaN,
                m.irisPx, m.amberPx, m.irisH, m.irisS, m.irisV, m.changePx, m.changeMax, m.eyeH, m.eyeS, m.eyeV));
            File.WriteAllLines(path, lines.ToArray());
        }

        // ---- 一覧の絵 --------------------------------------------------------

        /// <summary>
        /// 段を横に並べた一覧。行は近さ × 向き（0.4 m の L30・正面・R30、1.0 m …、2.0 m …）、列は tags の順。
        /// 320×180 を画素のまま scale 倍に拡大し、4 画素の隙間を空けて並べる
        /// </summary>
        public static string Sheet(string[] tags, Lighting light, string outName, int scale = 2)
        {
            var rows = new List<string>();
            foreach (var d in Distances)
                foreach (var yaw in Yaws)
                    rows.Add(string.Format(CultureInfo.InvariantCulture, "{0}_{1:0.0}m_{2}", LightName(light), d, YawName(yaw)));
            return SheetOf(tags, rows, outName, scale);
        }

        public static string SheetOf(string[] tags, IList<string> rows, string outName, int scale = 2)
        {
            const int gap = 4;
            int cw = GameW * scale, ch = GameH * scale;
            var W = tags.Length * cw + (tags.Length + 1) * gap;
            var H = rows.Count * ch + (rows.Count + 1) * gap;
            var sheet = new Texture2D(W, H, TextureFormat.RGBA32, false);
            sheet.hideFlags = HideFlags.HideAndDontSave;
            var fill = new Color32[W * H];
            for (var i = 0; i < fill.Length; i++) fill[i] = new Color32(40, 40, 44, 255);
            var missing = new List<string>();
            for (var ri = 0; ri < rows.Count; ri++)
                for (var ti = 0; ti < tags.Length; ti++)
                {
                    var path = Path.Combine(OutDir, tags[ti] + "_" + rows[ri] + ".png");
                    if (!File.Exists(path)) { missing.Add(Path.GetFileName(path)); continue; }
                    var t = new Texture2D(2, 2);
                    t.hideFlags = HideFlags.HideAndDontSave;
                    t.LoadImage(File.ReadAllBytes(path));
                    var px = t.GetPixels32();
                    int ox = gap + ti * (cw + gap);
                    // 行は上から並べる。Texture2D の y は下から
                    int oy = H - (gap + ri * (ch + gap)) - ch;
                    for (var y = 0; y < ch; y++)
                        for (var x = 0; x < cw; x++)
                            fill[(oy + y) * W + ox + x] = px[(y / scale) * t.width + x / scale];
                    Object.DestroyImmediate(t);
                }
            sheet.SetPixels32(fill);
            sheet.Apply();
            Save(sheet, Path.Combine(OutDir, outName));
            Object.DestroyImmediate(sheet);
            return outName + (missing.Count > 0 ? "（無い絵: " + string.Join(", ", missing.ToArray()) + "）" : "");
        }
    }
}
