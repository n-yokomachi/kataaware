using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の電車の空・霞・環境光と、窓の外を流れる三つの層（設計書 9.1 節「電車の作り込み」の「時刻と光」「窓の外（動く）」）。
    /// 空は <c>BuildDiveSky.cs</c>、撮る街並みの部品は <c>BuildDiveTown.cs</c>、抜き方は <c>BuildDiveFar.cs</c> の <see cref="FarCut"/> を使う。
    ///
    /// **3 月の 18:20。日の入り（17:44）の 36 分後で、日は西の地平の 5 度下。** 西の地平が橙、上は濃い青。
    /// 日は空に見えないので、影を落とす日の灯りは置かない。
    ///
    /// **窓の外は三つの層を、速さを変えて流す。** 走っている車両の外は書き割りの輪では作れないので、
    /// 公営住宅の書き割りと同じく一度組んで撮り、その絵を車両の両脇に立てた帯に貼って、
    /// <see cref="WindowStream"/> で層ごとに横へ送る。帯は抜いた所から後ろの夕暮れの空が見える。
    /// <list type="table">
    /// <item><term>近く（4.5 m）</term><description>高架の煉瓦の腰壁と、線路脇の柵、架線の柱。等間隔で速く流れる</description></item>
    /// <item><term>中（24 m）</term><description>煉瓦の長屋の裏側と倉庫。灯りの点いた窓、横丁の街灯</description></item>
    /// <item><term>遠く（55 m）</term><description>屋根の海の縁、高層棟、シティの灯りとクレーン。ゆっくり流れる</description></item>
    /// </list>
    /// 近くと中は本当の距離に立てて、同じ速さ（m/秒）で送る。窓に近い物ほど速く流れて見える差は、距離が持つ。
    /// 遠くは 300 m〜3 km 先の物を 55 m 先へ縮めて描いたので、その縮めた分だけ遅く送る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 空と光 --------------------------------------------------------------

        const string TrainSkyPath = DiveTextures + "TrainSky.png";
        const string TrainSkyMatPath = Materials + "TrainSkybox.mat";

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。車内（13 m）で 0.3%。
        /// 窓の外の帯は霞を受けない（<c>HalfAware/Backdrop</c>）ので、遠さの白みは撮るときに色へ混ぜてある（<see cref="TrainLayer.Haze"/>）
        /// </summary>
        const float TrainHazeDensity = 0.004f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す
        static readonly Color TrainZenith = new Color(0.050f, 0.075f, 0.190f);
        static readonly Color TrainMiddle = new Color(0.140f, 0.185f, 0.360f);
        /// <summary>地平の色。霞の色もこれにする。日の反対（東）の地平の、灰がかった藤色</summary>
        static readonly Color TrainHorizon = new Color(0.300f, 0.290f, 0.380f);
        static readonly Color TrainWarm = new Color(0.980f, 0.500f, 0.180f);
        static readonly Color TrainGlowCol = new Color(1.000f, 0.600f, 0.280f);
        static readonly Color TrainCloudLit = new Color(0.820f, 0.420f, 0.330f);
        static readonly Color TrainCloudShade = new Color(0.110f, 0.120f, 0.210f);
        /// <summary>地面の照り返しの色。暮れた街。sRGB</summary>
        static readonly Color TrainBounce = new Color(0.10f, 0.10f, 0.11f);
        /// <summary>環境光の倍率。夕暮れの空は暗く、車内は蛍光灯が受け持つので、隅が真っ黒に落ちない所まで</summary>
        const float TrainAmbientGain = 1.8f;

        /// <summary>
        /// 日へ向かう向き。西（+x）のやや北寄り（+z）、地平の 5 度下。
        /// 3 月の頭のロンドンの日の入りは西南西だが、窓の正面に夕焼けを持ってくるために車両の向きの方を合わせてある
        /// </summary>
        static Vector3 TrainSunward()
        {
            return Quaternion.Euler(5f, 80f, 0f) * Vector3.forward;
        }

        /// <summary>電車の空の見え方。日は地平の下なので円は描かず、地平の暖かさと、その上の広い明るみだけ</summary>
        static SkyPaint.Look TrainSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = TrainZenith.linear,
                middle = TrainMiddle.linear,
                horizon = TrainHorizon.linear,
                whiteDepth = 0.10f,
                warm = TrainWarm.linear,
                warmth = 0.95f,
                warmDepth = 0.14f,
                warmFocus = 2.5f,
                sun = sun.normalized,
                glow = TrainGlowCol.linear,
                glowNear = 0.30f,
                glowNearWidth = 9f,
                glowWide = 0.16f,
                glowWideWidth = 40f,
                disc = TrainGlowCol.linear,
                discRadius = 0f,
                discSoft = 1e-3f,
                cloudLit = TrainCloudLit.linear,
                cloudShade = TrainCloudShade.linear,
                cloudCover = 0.55f,
                // 仰角 2 度から 16 度。夕焼けの下を染めた薄い筋雲
                cloudLow = Mathf.Sin(2f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(16f * Mathf.Deg2Rad),
                cloudSeed = 29,
            };
        }

        /// <summary>電車の空・霞・環境光を一揃いで。影を落とす日は無い（sun は null）。空の絵とマテリアルもここで焼き直す</summary>
        static PlaceSky TrainPlaceSky(Transform place)
        {
            return PaintedSky(place, "Dusk", TrainSkyLook(TrainSunward()), TrainHorizon, TrainHazeDensity,
                TrainBounce, TrainAmbientGain, TrainSkyPath, TrainSkyMatPath);
        }

        // ---- 窓の外の層 ------------------------------------------------------------

        const string TrainOutsidePath = DiveTextures + "TrainOutside.png";
        const string TrainOutsideMatPath = Materials + "TrainOutside.mat";

        /// <summary>三つの層を一枚の絵に縦に並べる。横 1024、一段 640（上下に 64 の余白）</summary>
        const int TrainSheetWide = 1024;
        const int TrainSheetHigh = 2048;
        const int TrainRowHigh = 640;
        const int TrainRowPad = 64;
        const int TrainRowBody = TrainRowHigh - TrainRowPad * 2;

        /// <summary>一つの層。寸法は場所のローカル。y は車両の床が 0</summary>
        sealed class TrainLayer
        {
            public string Name;
            /// <summary>絵の中の段</summary>
            public int Row;
            /// <summary>絵の横の一周。これだけ流れると同じ景色に戻る。m</summary>
            public float Period;
            /// <summary>帯の下端と上端</summary>
            public float Low, High;
            /// <summary>車両の中心から帯までの横の距離</summary>
            public float Reach;
            /// <summary>帯の長さ。車両の前後と、隣の車両の窓の斜めまで覆う</summary>
            public float Length;
            /// <summary>流れる速さ。帯の上の m/秒</summary>
            public float Pace;
            /// <summary>撮るときに地の色へ混ぜる霞の割合。灯りには混ぜない</summary>
            public float Haze;
            /// <summary>一周ぶんを組む手順。a は流れる向きの位置（0〜Period）、x は帯からの奥行き（負が車両の側）</summary>
            public System.Action<Town, float> Build;
        }

        /// <summary>
        /// 三つの層。近くと中は同じ速さで送る（本当の距離に立てたので、見かけの速さの差は距離が持つ）。
        /// 7 m/秒は駅に近づいて速度を落としているところ
        /// </summary>
        static readonly TrainLayer[] TrainLayers =
        {
            new TrainLayer { Name = "Near", Row = 0, Period = 24f, Low = -10f, High = 6f, Reach = 4.5f, Length = 64f, Pace = 7f, Haze = 0f, Build = TrainNearOnce },
            new TrainLayer { Name = "Mid", Row = 1, Period = 72f, Low = -12f, High = 14f, Reach = 24f, Length = 192f, Pace = 7f, Haze = 0.12f, Build = TrainMidOnce },
            new TrainLayer { Name = "Far", Row = 2, Period = 240f, Low = -8f, High = 22f, Reach = 55f, Length = 288f, Pace = 0.7f, Haze = 0.5f, Build = TrainFarOnce },
        };

        /// <summary>
        /// 窓の外の帯を三枚、車両の両脇に立て、層ごとに <see cref="WindowStream"/> を付ける。撮った絵が無ければ置かずに一行だけ知らせる。
        ///
        /// **帯の uv は、横を「流れる向きの位置 ÷ 一周」で振る。** 帯の上の 1 m がどの帯でも絵の中の同じ長さになり、
        /// 送る量（<see cref="WindowStream"/> の speed）を m/秒 ÷ 一周で渡せる。
        /// 横の uv は -z へ増やす。+x の窓からは絵がそのまま、-x の窓からは左右が返って見えるが、両側とも同じ向きへ流れる
        /// </summary>
        static void TrainOutside(Transform place)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TrainOutsidePath);
            if (tex == null)
            {
                Debug.Log("窓の外の絵がまだ無い: " + TrainOutsidePath + "。HalfAware/Shoot the train backdrop で撮る");
                return;
            }
            var mat = FarMat(new FarRing { Material = TrainOutsideMatPath }, tex);
            var root = Child(place, "Outside");
            foreach (var layer in TrainLayers)
            {
                var bank = new Bank { Texel = 1f };
                var v0 = (layer.Row * TrainRowHigh + TrainRowPad) / (float)TrainSheetHigh;
                var v1 = (layer.Row * TrainRowHigh + TrainRowPad + TrainRowBody) / (float)TrainSheetHigh;
                var h = layer.Length * 0.5f;
                for (var s = 0; s < 2; s++)
                {
                    var x = (s == 0 ? -1f : 1f) * layer.Reach;
                    // 車内から見て右下・左下・左上・右上。+x の側は右が -z、-x の側は右が +z
                    var right = s == 0 ? h : -h;
                    var ur = -right / layer.Period;
                    bank.Patch(new Vector3(x, layer.Low, right), new Vector3(x, layer.Low, -right),
                        new Vector3(x, layer.High, -right), new Vector3(x, layer.High, right),
                        new Vector2(ur, v0), new Vector2(-ur, v0), new Vector2(-ur, v1), new Vector2(ur, v1));
                }
                var made = bank.Emit(root, "Outside" + layer.Name, mat, false, Generated);
                if (made == null) continue;
                var r = made.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                var stream = made.AddComponent<WindowStream>();
                var so = new SerializedObject(stream);
                Fill(so.FindProperty("panes"), new Object[] { r });
                // 繰り返しは mesh の uv が持つ。ずらすだけ
                so.FindProperty("tiling").vector2Value = Vector2.one;
                // 車両は +z へ走る。外は -z へ流れる
                so.FindProperty("speed").floatValue = -layer.Pace / layer.Period;
                so.FindProperty("stagger").floatValue = 0f;
                so.FindProperty("sway").floatValue = 0.10f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 窓の外の三つの層を撮る。層ごとに撮るためだけの街並みを組み、真横から平行に（正射影で）一周ぶんを撮って、
        /// 空を抜き（黒と白の背景の差。<see cref="FarCut"/>）、一枚の絵に縦に並べて書き出す。組んだ物は全部捨ててから返る。
        /// 帯に貼るのは `HalfAware/Build the dive`。
        ///
        /// **一周の両端で絵が繋がるように組む。** 一周ぶんの物を、前後に一周ずつずらしてもう二回置いてから真ん中を撮る
        /// </summary>
        [MenuItem("HalfAware/Shoot the train backdrop", false, 256)]
        public static void ShootTrainBackdrop()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("再生中とコンパイル中は撮らない");
                return;
            }
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Train) : null;
            if (place == null)
            {
                Debug.LogError(DiveIds.Train + " がまだ組まれていない。先に HalfAware/Build the dive");
                return;
            }
            var sky = TrainPlaceSky(place);
            Texture2D sheet;
            using (new CheckDiveSky.Stage(DiveIds.Train))
                sheet = TrainShoot(place, sky);
            if (sheet == null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            File.WriteAllBytes(TrainOutsidePath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            AssetDatabase.ImportAsset(TrainOutsidePath);
            var importer = AssetImporter.GetAtPath(TrainOutsidePath) as TextureImporter;
            if (importer != null)
            {
                // 書き割りの輪の絵（ShootBackdrop）に揃える。違うのは横に繰り返すことだけ
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            Debug.Log("窓の外を撮った: " + TrainOutsidePath + "。HalfAware/Build the dive で帯に貼る");
        }

        /// <summary>
        /// 三つの層を撮って一枚に並べる。呼ぶ側が <see cref="CheckDiveSky.Stage"/> で場所と空を持っている間に呼ぶ。
        /// 車両の形と灯りは伏せ、撮る物は場所の 600 m 下に組む（車内の灯りが届かない）。
        /// 地の色は灯りを受けない色（Unlit）で塗るので、霧は切って撮る
        /// </summary>
        static Texture2D TrainShoot(Transform place, PlaceSky sky)
        {
            var hidden = new List<Renderer>();
            var made = new List<Object>();
            var keepBlur = Shader.GetGlobalFloat("_DazeBlur");
            var keepWobble = Shader.GetGlobalFloat("_DazeWobble");
            GameObject eyeGo = null;
            try
            {
                foreach (var r in place.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hidden.Add(r);
                }
                sky.Apply(null);
                RenderSettings.fog = false;
                Shader.SetGlobalFloat("_DazeBlur", 0f);
                Shader.SetGlobalFloat("_DazeWobble", 0f);

                eyeGo = new GameObject("TrainEye");
                eyeGo.hideFlags = HideFlags.HideAndDontSave;
                var cam = eyeGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.cullingMask = ~0;
                cam.orthographic = true;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 800f;

                var sheet = new Texture2D(TrainSheetWide, TrainSheetHigh, TextureFormat.RGBA32, false, false);
                var fill = sky.hazeColor;
                var blank = new Color32(Byte(fill.r), Byte(fill.g), Byte(fill.b), 0);
                var all = new Color32[TrainSheetWide * TrainSheetHigh];
                for (var i = 0; i < all.Length; i++) all[i] = blank;
                sheet.SetPixels32(all);

                var fw = TrainSheetWide * FarFine;
                var fh = TrainRowBody * FarFine;
                foreach (var layer in TrainLayers)
                {
                    var root = new GameObject("TrainTown" + layer.Name);
                    root.hideFlags = HideFlags.HideAndDontSave;
                    root.transform.SetPositionAndRotation(place.position + Vector3.down * 600f, Quaternion.identity);
                    made.Add(root);
                    var t = new Town(root.transform, made);
                    TrainInks(t, made, layer.Haze);
                    for (var k = -1; k <= 1; k++) layer.Build(t, k * layer.Period);
                    t.Emit();

                    // 帯の面（x 0）の 300 m 手前から +x を向いて撮る。絵の右が -z（流れる向きの位置 a が増える向き）
                    var mid = (layer.Low + layer.High) * 0.5f;
                    var half = (layer.High - layer.Low) * 0.5f;
                    eyeGo.transform.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(-300f, mid, -layer.Period * 0.5f)),
                        Quaternion.LookRotation(Vector3.right, Vector3.up));
                    cam.orthographicSize = half;
                    cam.aspect = layer.Period / (half * 2f);
                    cam.projectionMatrix = Matrix4x4.Ortho(-layer.Period * 0.5f, layer.Period * 0.5f, -half, half, cam.nearClipPlane, cam.farClipPlane);
                    cam.backgroundColor = Color.black;
                    var dark = CheckDiveSky.Grab(cam, fw, fh, false);
                    cam.backgroundColor = Color.white;
                    var light = CheckDiveSky.Grab(cam, fw, fh, false);
                    var tile = FarCut(dark, light, fw, fh, blank);
                    // 空の所に僅かに残る α（黒と白の差の丸め）を落とす。抜く境には届かないが、
                    // 縮めた段で掛かりの割合を保つときに（mipMapsPreserveCoverage）持ち上がって空に点が浮く
                    for (var i = 0; i < tile.Length; i++)
                        if (tile[i].a < 24) tile[i] = blank;
                    Object.DestroyImmediate(dark);
                    Object.DestroyImmediate(light);
                    var y0 = layer.Row * TrainRowHigh + TrainRowPad;
                    sheet.SetPixels32(0, y0, TrainSheetWide, TrainRowBody, tile);
                    // 下の余白は一番下の行で埋める。帯の下端で地が霞の色へ滲まないように
                    var bottom = new Color32[TrainSheetWide];
                    System.Array.Copy(tile, 0, bottom, 0, TrainSheetWide);
                    for (var p = 1; p <= TrainRowPad; p++) sheet.SetPixels32(0, y0 - p, TrainSheetWide, 1, bottom);

                    Object.DestroyImmediate(root);
                    made.Remove(root);
                }
                sheet.Apply();
                return sheet;
            }
            finally
            {
                if (eyeGo != null) Object.DestroyImmediate(eyeGo);
                for (var i = made.Count - 1; i >= 0; i--)
                    if (made[i] != null) Object.DestroyImmediate(made[i]);
                foreach (var r in hidden) if (r != null) r.enabled = true;
                Shader.SetGlobalFloat("_DazeBlur", keepBlur);
                Shader.SetGlobalFloat("_DazeWobble", keepWobble);
            }
        }

        /// <summary>
        /// 撮るときの地の色。暮れた街は灯りを受けないので、灯りを受けない色（Unlit）で直に塗る。
        /// 地の色には層の遠さの分だけ霞の色を混ぜ、灯りの点いた窓と街灯には混ぜない（灯りは霞を透して届く）。
        /// マテリアルはアセットにせず <paramref name="made"/> へ入れる
        /// </summary>
        static void TrainInks(Town t, List<Object> made, float haze)
        {
            System.Action<string, Color, bool> ink = (name, col, lamp) =>
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.hideFlags = HideFlags.HideAndDontSave;
                m.name = "TrainInk" + name;
                var c = lamp ? col : Color.Lerp(col, TrainHorizon, haze);
                m.SetColor("_BaseColor", c);
                made.Add(m);
                t.Use(name, m);
            };
            // 近く。高架の煉瓦と、柵と、架線の柱
            ink("parapet", new Color(0.190f, 0.135f, 0.115f), false);
            ink("pier", new Color(0.160f, 0.112f, 0.096f), false);
            ink("coping", new Color(0.300f, 0.295f, 0.285f), false);
            ink("post", new Color(0.260f, 0.260f, 0.255f), false);
            ink("wire", new Color(0.090f, 0.095f, 0.100f), false);
            ink("mast", new Color(0.150f, 0.170f, 0.185f), false);
            ink("cabinet", new Color(0.230f, 0.260f, 0.230f), false);
            ink("weed", new Color(0.075f, 0.085f, 0.060f), false);
            // 中。長屋の裏と倉庫
            ink("stock", new Color(0.200f, 0.170f, 0.140f), false);
            ink("brick", new Color(0.175f, 0.115f, 0.100f), false);
            ink("sooty", new Color(0.130f, 0.095f, 0.085f), false);
            ink("slate", new Color(0.115f, 0.125f, 0.150f), false);
            ink("trim", new Color(0.330f, 0.330f, 0.320f), false);
            ink("pot", new Color(0.210f, 0.120f, 0.090f), false);
            ink("glass", new Color(0.045f, 0.055f, 0.080f), false);
            ink("ground", new Color(0.055f, 0.055f, 0.065f), false);
            ink("twig", new Color(0.085f, 0.080f, 0.080f), false);
            ink("concrete", new Color(0.220f, 0.225f, 0.235f), false);
            // 遠く。霞に沈んだ棟
            ink("roofs", new Color(0.110f, 0.110f, 0.150f), false);
            ink("tower", new Color(0.170f, 0.180f, 0.215f), false);
            ink("towerDark", new Color(0.120f, 0.130f, 0.165f), false);
            ink("city", new Color(0.150f, 0.175f, 0.230f), false);
            ink("crane", new Color(0.140f, 0.140f, 0.150f), false);
            // 灯り
            ink("lit", new Color(1.000f, 0.780f, 0.460f), true);
            ink("litDim", new Color(0.760f, 0.520f, 0.300f), true);
            ink("litCool", new Color(0.760f, 0.860f, 1.000f), true);
            ink("sodium", new Color(1.000f, 0.560f, 0.180f), true);
            ink("red", new Color(1.000f, 0.180f, 0.120f), true);
        }

        /// <summary>
        /// 軸に沿った箱を、流れる向きの位置 a・高さ y・奥行き x の端の値で。
        /// 場所の z は -a（絵の右が -z）
        /// </summary>
        static void TrainBox(Town t, string ink, float a0, float a1, float y0, float y1, float x0, float x1)
        {
            t[ink].Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, -(a0 + a1) * 0.5f),
                new Vector3(x1 - x0, y1 - y0, a1 - a0), Quaternion.identity);
        }

        // ---- 近く ------------------------------------------------------------------

        /// <summary>
        /// 近くの層の一周（24 m）。高架の煉瓦の腰壁と笠石、6 m ごとの控え壁、2.4 m ごとの柵の柱と三本の針金、
        /// 一周に一本の架線の柱（上に腕と碍子、頭を通る架空地線）、線路脇の箱と草。
        /// 腰壁の上端は車両の床と同じ高さ（レールから 1.1 m）
        /// </summary>
        static void TrainNearOnce(Town t, float at)
        {
            const float p = 24f;
            const float top = -0.25f;
            TrainBox(t, "parapet", at, at + p, -10f, top, 0f, 0.45f);
            TrainBox(t, "coping", at, at + p, top, -0.05f, -0.08f, 0.53f);
            // 煉瓦の目地の帯。遠目には横縞にしか見えないので、0.6 m ごとに一本
            for (var y = -9.7f; y < top - 0.2f; y += 0.6f)
                TrainBox(t, "pier", at, at + p, y, y + 0.05f, -0.01f, 0.2f);
            for (var k = 0; k < 4; k++)
                TrainBox(t, "pier", at + 1f + k * 6f, at + 1.7f + k * 6f, -10f, top - 0.1f, -0.15f, 0.2f);
            // 柵
            for (var k = 0; k < 10; k++)
            {
                var a = at + 0.4f + k * 2.4f;
                TrainBox(t, "post", a, a + 0.11f, -0.05f, 1.35f, 0.14f, 0.26f);
            }
            foreach (var y in new[] { 0.35f, 0.75f, 1.15f })
                TrainBox(t, "wire", at, at + p, y, y + 0.035f, 0.18f, 0.22f);
            // 架線の柱。H 形の鋼の柱と、上の腕、二つの碍子
            var m = at + 12.6f;
            TrainBox(t, "coping", m - 0.4f, m + 0.7f, -0.3f, 0.25f, -0.1f, 0.6f);
            TrainBox(t, "mast", m, m + 0.28f, 0.2f, 5.9f, 0.1f, 0.4f);
            TrainBox(t, "mast", m - 0.05f, m + 0.33f, 0.2f, 5.9f, 0.08f, 0.12f);
            TrainBox(t, "mast", m - 0.9f, m + 0.28f, 5.25f, 5.4f, -0.4f, 0.1f);
            TrainBox(t, "mast", m - 0.9f, m - 0.8f, 4.3f, 5.3f, -0.3f, -0.2f);
            TrainBox(t, "wire", m - 0.55f, m - 0.45f, 4.9f, 5.25f, -0.5f, -0.4f);
            TrainBox(t, "wire", m - 1.0f, m - 0.9f, 4.1f, 4.4f, -0.5f, -0.4f);
            // 架空地線。柱の頭を結んで一周を通す
            TrainBox(t, "wire", at, at + p, 5.75f, 5.8f, 0.2f, 0.3f);
            // 線路脇の箱
            TrainBox(t, "cabinet", at + 4.0f, at + 5.1f, -0.05f, 1.1f, 0.05f, 0.45f);
            TrainBox(t, "coping", at + 3.95f, at + 5.15f, 1.1f, 1.16f, 0.03f, 0.47f);
            TrainBox(t, "cabinet", at + 19.2f, at + 19.8f, -0.05f, 0.7f, 0.08f, 0.42f);
            // 笠石の上の草。ロンドンの高架に生える藪の小さな塊
            TrainBox(t, "weed", at + 7.2f, at + 8.4f, -0.05f, 0.45f, 0.1f, 0.4f);
            TrainBox(t, "weed", at + 7.6f, at + 8.0f, 0.4f, 0.75f, 0.15f, 0.35f);
            TrainBox(t, "weed", at + 16.3f, at + 16.9f, -0.05f, 0.3f, 0.1f, 0.4f);
            TrainBox(t, "weed", at + 21.5f, at + 22.9f, -0.05f, 0.55f, 0.1f, 0.4f);
        }

        // ---- 中 ------------------------------------------------------------------

        /// <summary>中の層の地面（通りと裏庭）の高さ。高架の下の街。車両の床から 8.7 m 下</summary>
        const float TrainStreetY = -8.7f;

        /// <summary>
        /// 中の層の一周（72 m）。手前に裏庭の塀と小屋と葉の無い木、奥に長屋の裏（二階建て六軒と三階建て三軒）、
        /// その間に倉庫。長屋の裏は一軒ずつの張り出しと、棟の上の煙突の束が並ぶ。横丁の口に街灯。
        /// 窓はおよそ三つに一つに灯り
        /// </summary>
        static void TrainMidOnce(Town t, float at)
        {
            var rnd = new System.Random(1818);
            const float p = 72f;
            const float g = TrainStreetY;
            // 地と、裏庭の塀
            TrainBox(t, "ground", at, at + p, -12f, g + 0.2f, -6.5f, 20f);
            TrainBox(t, "sooty", at, at + p, g, g + 1.8f, -5.2f, -4.9f);
            TrainBox(t, "trim", at, at + p, g + 1.8f, g + 1.9f, -5.25f, -4.85f);
            // 長屋
            TrainTerraceBack(t, at + 1f, 6, 2, "stock", rnd);
            TrainTerraceBack(t, at + 57f, 3, 3, "brick", rnd);
            // 横丁の口の街灯。塀の切れ目の奥に、ナトリウムの橙
            TrainBox(t, "ground", at + 31.5f, at + 36.5f, g, g + 2f, -5.4f, -4.7f);
            TrainBox(t, "mast", at + 33.9f, at + 34.05f, g, g + 6.2f, 0f, 0.15f);
            TrainBox(t, "mast", at + 33.4f, at + 34.05f, g + 6.1f, g + 6.25f, -0.1f, 0.15f);
            TrainBox(t, "sodium", at + 33.3f, at + 33.75f, g + 5.9f, g + 6.12f, -0.12f, 0.1f);
            // 倉庫。四階建ての煉瓦の箱に、縦長の窓の列。上の階に灯りの点いた事務所
            const float wa = 37f;
            const float wl = 19.5f;
            var roof = g + 14.4f;
            TrainBox(t, "brick", at + wa, at + wa + wl, g, roof, 2f, 16f);
            TrainBox(t, "trim", at + wa - 0.1f, at + wa + wl + 0.1f, roof - 0.3f, roof, 1.9f, 16f);
            for (var f = 0; f < 4; f++)
            {
                var y = g + 1.2f + f * 3.4f;
                TrainBox(t, "sooty", at + wa, at + wa + wl, y - 0.25f, y - 0.1f, 1.95f, 2.05f);
                for (var c = 0; c < 8; c++)
                {
                    var a = at + wa + 1.2f + c * 2.35f;
                    var lit = f >= 1 && rnd.NextDouble() < 0.45;
                    TrainBox(t, lit ? (f == 3 ? "litCool" : "lit") : "glass", a, a + 1.2f, y, y + 2.2f, 1.9f, 1.98f);
                    TrainBox(t, "trim", a - 0.05f, a + 1.25f, y - 0.08f, y, 1.85f, 1.98f);
                }
            }
            // 屋上の水槽
            TrainBox(t, "sooty", at + wa + 12f, at + wa + 15f, roof, roof + 0.9f, 6f, 9f);
            TrainBox(t, "concrete", at + wa + 11.8f, at + wa + 15.2f, roof + 0.9f, roof + 2.6f, 5.8f, 9.2f);
            // 裏庭の木と小屋
            TownTree(t, new Vector3(-3.5f, g, -(at + 13f)), 9.5f, rnd);
            TownTree(t, new Vector3(-2.8f, g, -(at + 64f)), 11f, rnd);
            TrainBox(t, "sooty", at + 22f, at + 24.5f, g, g + 2.3f, -4.5f, -2.5f);
            TrainBox(t, "slate", at + 21.8f, at + 24.7f, g + 2.3f, g + 2.45f, -4.6f, -2.4f);
        }

        /// <summary>
        /// 長屋の裏を一列。a0 から <paramref name="houses"/> 軒、一軒 5 m。裏の壁は x 4（帯の 4 m 奥）で車両の側を向く。
        /// 裏の屋根の面と、戸境の棟の煙突の束。一軒ずつ裏へ張り出した二階建ての張り出し（片流れの屋根）と、その脇の窓
        /// </summary>
        static void TrainTerraceBack(Town t, float a0, int houses, int storeys, string skin, System.Random rnd)
        {
            const float house = 5f;
            const float back = 4f;
            var g = TrainStreetY;
            var eaves = g + storeys * 3f + 0.4f;
            var ridge = eaves + 2.8f;
            var len = houses * house;
            TrainBox(t, skin, a0, a0 + len, g, eaves, back, back + 9f);
            // 裏の屋根の面。正射影では軒から棟までの帯に見える
            TrainBox(t, "slate", a0 - 0.15f, a0 + len + 0.15f, eaves, ridge, back + 0.1f, back + 4.5f);
            TrainBox(t, "trim", a0, a0 + len, eaves - 0.15f, eaves, back - 0.05f, back);
            for (var h = 0; h <= houses; h++)
            {
                var a = a0 + h * house;
                t[skin].Box(new Vector3(back + 4.5f, ridge + 0.4f, -a), new Vector3(1.6f, 2.4f, 0.8f), Quaternion.identity);
                TrainBox(t, "trim", a - 0.45f, a + 0.45f, ridge + 1.55f, ridge + 1.7f, back + 3.65f, back + 5.35f);
                for (var i = 0; i < 4; i++)
                    TrainBox(t, "pot", a - 0.45f + i * 0.24f, a - 0.29f + i * 0.24f, ridge + 1.7f, ridge + 2.2f, back + 4.3f, back + 4.7f);
                if (h == houses) break;
                // 屋根裏の窓。二軒に一軒、屋根の面に天窓か屋根窓。暗い屋根の帯が一枚の黒に潰れないように
                if (h % 2 == 1)
                {
                    var da = a + house * 0.62f;
                    if (rnd.NextDouble() < 0.5)
                    {
                        TrainBox(t, "slate", da - 0.1f, da + 1.5f, eaves + 0.5f, eaves + 2.1f, back - 0.6f, back + 0.2f);
                        TrainWindow(t, da + 0.2f, da + 1.2f, eaves + 0.8f, eaves + 1.8f, back - 0.62f, rnd);
                    }
                    else
                        TrainBox(t, rnd.NextDouble() < 0.5 ? "lit" : "glass", da, da + 0.9f, eaves + 1.2f, eaves + 1.8f, back - 0.02f, back + 0.05f);
                }
                // 張り出し。戸境に寄せて、軒の間の半分
                var outHigh = Mathf.Min(eaves, g + 6.2f);
                var oa = a + 0.1f;
                TrainBox(t, skin, oa, oa + house * 0.46f, g, outHigh, back - 4.5f, back);
                TrainBox(t, "slate", oa - 0.1f, oa + house * 0.46f + 0.1f, outHigh, outHigh + 0.45f, back - 4.6f, back);
                for (var s = 0; s < 2; s++)
                {
                    var y = g + s * 3f + 1.1f;
                    TrainWindow(t, oa + 0.55f, oa + 1.55f, y, y + 1.4f, back - 4.53f, rnd);
                }
                // 裏の壁の窓。張り出しの脇に、階ごとに一つ
                for (var s = 0; s < storeys; s++)
                {
                    var y = g + s * 3f + 1.2f;
                    TrainWindow(t, a + house * 0.58f, a + house * 0.58f + 1.0f, y, y + 1.5f, back - 0.03f, rnd);
                }
                // 縦樋
                TrainBox(t, "sooty", a + house - 0.12f, a + house - 0.04f, g, eaves, back - 0.1f, back);
            }
        }

        /// <summary>上げ下げ窓を一つ。白い枠と桟、三つに一つくらいに灯り</summary>
        static void TrainWindow(Town t, float a0, float a1, float y0, float y1, float x, System.Random rnd)
        {
            var roll = rnd.NextDouble();
            var pane = roll < 0.30 ? "lit" : roll < 0.36 ? "litCool" : roll < 0.42 ? "litDim" : "glass";
            TrainBox(t, "trim", a0 - 0.06f, a1 + 0.06f, y0 - 0.1f, y1 + 0.06f, x - 0.02f, x + 0.02f);
            TrainBox(t, pane, a0, a1, y0, y1, x - 0.04f, x);
            TrainBox(t, "trim", a0, a1, (y0 + y1) * 0.5f - 0.03f, (y0 + y1) * 0.5f + 0.03f, x - 0.06f, x - 0.03f);
        }

        // ---- 遠く ------------------------------------------------------------------

        /// <summary>
        /// 遠くの層の一周（240 m）。300 m〜3 km 先の物を 55 m 先へ縮めて描く（高さも幅も距離の比で）。
        /// 地平の少し下まで屋根の海の縁、その上に高層棟が四本（窓の升目の三割ほどに灯り）、
        /// シティの高いビルの塊（階ごとの灯りの帯、先の尖った塔）とクレーン（頭に赤い灯）、教会の尖塔
        /// </summary>
        static void TrainFarOnce(Town t, float at)
        {
            var rnd = new System.Random(2018);
            const float p = 240f;
            // 屋根の海。上端をぎざぎざに、ところどころに窓の灯り
            TrainBox(t, "roofs", at, at + p, -8f, -0.4f, 2f, 3f);
            var a = at;
            while (a < at + p)
            {
                var w = 2f + (float)rnd.NextDouble() * 6f;
                var top = -0.4f + (float)rnd.NextDouble() * 0.7f;
                TrainBox(t, "roofs", a, Mathf.Min(a + w, at + p), -0.5f, top, 1.5f, 2.5f);
                if (rnd.NextDouble() < 0.5)
                    TrainBox(t, "roofs", a + w * 0.3f, a + w * 0.3f + 0.12f, top, top + 0.22f, 1.4f, 1.6f);
                a += w;
            }
            for (var k = 0; k < 90; k++)
            {
                var la = at + (float)rnd.NextDouble() * (p - 0.3f);
                var ly = -2.2f + (float)rnd.NextDouble() * 1.9f;
                TrainBox(t, rnd.NextDouble() < 0.8 ? "lit" : "litDim", la, la + 0.14f, ly, ly + 0.09f, 1.3f, 1.4f);
            }
            // 高層棟
            TrainFarTower(t, at + 18f, 3.2f, 8.5f, rnd);
            TrainFarTower(t, at + 64f, 2.6f, 6.2f, rnd);
            TrainFarTower(t, at + 72f, 2.6f, 6.6f, rnd);
            TrainFarTower(t, at + 205f, 3.0f, 7.4f, rnd);
            // 教会の尖塔
            TrainBox(t, "towerDark", at + 104f, at + 105.2f, -0.5f, 2.6f, 0.6f, 1.2f);
            for (var k = 0; k < 6; k++)
            {
                var w = Mathf.Lerp(1.0f, 0.1f, k / 5f);
                TrainBox(t, "towerDark", at + 104.6f - w * 0.5f, at + 104.6f + w * 0.5f, 2.6f + k * 0.45f, 3.05f + k * 0.45f, 0.6f, 1.2f);
            }
            // シティ
            var city = new[] { 128f, 133f, 137.5f, 142f, 147f, 151f, 156f, 161f, 166f, 171f };
            var high = new[] { 5.5f, 9.0f, 7.0f, 13.5f, 8.0f, 10.5f, 6.0f, 11.0f, 7.5f, 5.0f };
            var wide = new[] { 3.0f, 3.5f, 2.6f, 3.2f, 4.0f, 3.0f, 3.4f, 2.8f, 3.6f, 3.0f };
            for (var k = 0; k < city.Length; k++)
                TrainFarCity(t, at + city[k], wide[k], high[k], k == 3 ? 1 : k == 7 ? 2 : 0, k % 2 == 0 ? "city" : "towerDark", rnd);
            // クレーン
            foreach (var ca in new[] { 140f, 158f, 176f })
            {
                var ch = 8f + (float)rnd.NextDouble() * 3f;
                TrainBox(t, "crane", at + ca, at + ca + 0.18f, -0.4f, ch, 0.2f, 0.4f);
                TrainBox(t, "crane", at + ca - 1.2f, at + ca + 3.6f, ch - 0.2f, ch, 0.2f, 0.4f);
                TrainBox(t, "red", at + ca + 3.4f, at + ca + 3.6f, ch, ch + 0.15f, 0.15f, 0.2f);
                TrainBox(t, "red", at + ca, at + ca + 0.18f, ch + 0.4f, ch + 0.55f, 0.15f, 0.2f);
                TrainBox(t, "crane", at + ca, at + ca + 0.18f, ch, ch + 0.4f, 0.2f, 0.4f);
            }
        }

        /// <summary>高層棟を一本。コンクリートの箱に窓の升目、屋上の機械室と赤い灯</summary>
        static void TrainFarTower(Town t, float a0, float wide, float high, System.Random rnd)
        {
            TrainBox(t, "tower", a0, a0 + wide, -0.6f, high, 0.6f, 1.4f);
            TrainBox(t, "towerDark", a0 + wide * 0.3f, a0 + wide * 0.7f, high, high + 0.5f, 0.7f, 1.3f);
            TrainBox(t, "red", a0 + wide * 0.48f, a0 + wide * 0.52f, high + 0.5f, high + 0.6f, 0.55f, 0.6f);
            for (var y = 0.2f; y < high - 0.3f; y += 0.33f)
                for (var c = 0; c < 6; c++)
                {
                    var a = a0 + wide * (c + 0.25f) / 6f;
                    var roll = rnd.NextDouble();
                    var ink = roll < 0.28 ? "lit" : roll < 0.34 ? "litCool" : roll < 0.40 ? "litDim" : "towerDark";
                    TrainBox(t, ink, a, a + wide * 0.5f / 6f, y, y + 0.14f, 0.55f, 0.6f);
                }
        }

        /// <summary>シティのビルを一本。kind 0 は箱、1 は先の尖った塔、2 は上で細る塔。階ごとの灯りの帯を途切れ途切れに</summary>
        static void TrainFarCity(Town t, float a0, float wide, float high, int kind, string skin, System.Random rnd)
        {
            var body = kind == 0 ? high : high * 0.72f;
            TrainBox(t, skin, a0, a0 + wide, -0.4f, body, 1.0f, 2.0f);
            if (kind == 1)
                for (var k = 0; k < 8; k++)
                {
                    var w = Mathf.Lerp(wide * 0.9f, 0.1f, (k + 1) / 8f);
                    var mid = a0 + wide * 0.5f;
                    TrainBox(t, skin, mid - w * 0.5f, mid + w * 0.5f, body + (high - body) * k / 8f, body + (high - body) * (k + 1) / 8f, 1.0f, 2.0f);
                }
            else if (kind == 2)
                TrainBox(t, skin, a0 + wide * 0.2f, a0 + wide * 0.8f, body, high, 1.0f, 2.0f);
            for (var y = 0.5f; y < body - 0.2f; y += 0.26f)
            {
                var a = a0 + 0.1f;
                while (a < a0 + wide - 0.2f)
                {
                    var w = 0.2f + (float)rnd.NextDouble() * 0.9f;
                    var end = Mathf.Min(a + w, a0 + wide - 0.1f);
                    if (rnd.NextDouble() < 0.55) TrainBox(t, rnd.NextDouble() < 0.7 ? "litCool" : "lit", a, end, y, y + 0.08f, 0.95f, 1.0f);
                    a = end + 0.1f;
                }
            }
            TrainBox(t, "red", a0 + wide * 0.45f, a0 + wide * 0.55f, high, high + 0.12f, 0.9f, 1.0f);
        }
    }
}
