using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地の空・日・霞・環境光（設計書 9.1 節「空と光」）。
    ///
    /// **一揃いで作る。** 一色の背景色で空を塗っていたのをやめ、
    /// 空の絵（<see cref="SkyPaint"/> で焼いた正距円筒の絵を <c>Skybox/Panoramic</c> に貼ったもの）、
    /// 絵の中の日と同じ向きの灯り、空の地平と同じ色の霞、空から取った環境光を、
    /// <see cref="PlaceSky"/> 一つにまとめて <see cref="DiveDirector"/> に持たせる。
    ///
    /// **空は絵に焼く。** <c>Skybox/Procedural</c> は上下の移りも日の周りも決まった式で、
    /// 雲を出す口が無い。絵なら WebGL でも同じに出て、遠さの限り（far 260）も受けず、
    /// 雲の帯と日の方角の暖かさを好きな形に決められる。
    ///
    /// **環境光は三色（Trilight）で渡す。** 空の絵から取る形（Skybox）は、差し替えのたびに
    /// 環境の光を焼き直さないと前の場所の光が残る。三色は空の絵から数えて出す（<see cref="EstateAmbient"/>）
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>空の絵の置き場</summary>
        const string DiveTextures = "Assets/Textures/Dive/";
        const string EstateSkyPath = DiveTextures + "EstateSky.png";
        /// <summary>空のマテリアル。前に遠景のビルに使っていた EstateSky.mat とは別の名前にする</summary>
        const string EstateSkyMatPath = Materials + "EstateSkybox.mat";

        /// <summary>空の絵の大きさ。横 2048 で一画素 0.18 度。960×540 で見ても日の円の縁が崩れない</summary>
        const int EstateSkyWide = 2048;
        const int EstateSkyHigh = 1024;

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。
        ///
        /// **近くはほとんど霞まず、遠くで急に白む形。** 隣の棟（25 m）で 0.6%、中の棟（50 m）で 2%、
        /// 書き割りの始まり（80 m）で 6%、板（200 m）で 30%、400 m で 76%、600 m で 96%。
        /// 部屋や廊下の中の色は変えず、書き割りの中の遠近だけを霞に持たせる
        /// </summary>
        public const float EstateHazeDensity = 0.003f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す
        static readonly Color EstateZenith = new Color(0.27f, 0.40f, 0.63f);
        static readonly Color EstateMiddle = new Color(0.47f, 0.59f, 0.76f);
        /// <summary>地平の色。霞の色もこれにする</summary>
        static readonly Color EstateHorizon = new Color(0.76f, 0.80f, 0.85f);
        static readonly Color EstateWarm = new Color(0.97f, 0.85f, 0.70f);
        static readonly Color EstateGlow = new Color(1.00f, 0.88f, 0.70f);
        static readonly Color EstateDisc = new Color(1.00f, 0.97f, 0.90f);
        static readonly Color EstateCloudLit = new Color(0.96f, 0.88f, 0.80f);
        static readonly Color EstateCloudShade = new Color(0.66f, 0.68f, 0.75f);

        /// <summary>地面の照り返しの色。乾いたコンクリートと土。sRGB</summary>
        static readonly Color EstateBounce = new Color(0.52f, 0.44f, 0.34f);

        /// <summary>
        /// 団地の空の見え方。<paramref name="sun"/> は日へ向かう向き（影を落とす灯りの逆）。
        ///
        /// 日の円は実際より大きく取る（半径 1.3 度）。ゲームは 320×180 で描くので、
        /// 実際の 0.27 度では一画素にも満たず、空のどこに日があるのか読めない
        /// </summary>
        static SkyPaint.Look EstateSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = EstateZenith.linear,
                middle = EstateMiddle.linear,
                horizon = EstateHorizon.linear,
                whiteDepth = 0.09f,
                warm = EstateWarm.linear,
                warmth = 0.75f,
                warmDepth = 0.10f,
                warmFocus = 4f,
                sun = sun.normalized,
                glow = EstateGlow.linear,
                glowNear = 0.45f,
                glowNearWidth = 4f,
                glowWide = 0.20f,
                glowWideWidth = 26f,
                disc = EstateDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = EstateCloudLit.linear,
                cloudShade = EstateCloudShade.linear,
                cloudCover = 0.55f,
                // 仰角 1.5 度から 15 度。地平の白みの上に薄く流れる
                cloudLow = Mathf.Sin(1.5f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(15f * Mathf.Deg2Rad),
                cloudSeed = 7,
            };
        }

        /// <summary>日へ向かう向き。団地の日射し（Morning）の逆。灯りがまだ無ければ組み立ての値から出す</summary>
        static Vector3 EstateSunward(Transform place)
        {
            var morning = place != null ? place.Find("Morning") : null;
            if (morning != null) return -morning.forward;
            return -(Quaternion.Euler(EstateMorningAim) * Vector3.forward);
        }

        /// <summary>
        /// 団地の空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す
        /// </summary>
        static PlaceSky EstatePlaceSky(Transform place)
        {
            var sun = EstateSunward(place);
            var look = EstateSkyLook(sun);
            var picture = EstateSkyPicture(look);
            var sky = new PlaceSky
            {
                skybox = EstateSkyMaterial(picture),
                flat = EstateHorizon,
                haze = true,
                hazeColor = EstateHorizon,
                hazeDensity = EstateHazeDensity,
                sun = place != null && place.Find("Morning") != null ? place.Find("Morning").GetComponent<Light>() : null,
            };
            EstateAmbient(look, out sky.ambientSky, out sky.ambientEquator, out sky.ambientGround);
            return sky;
        }

        /// <summary>
        /// 環境光を空から取る。上は天の半球の平均、横は地平の帯の平均、下は地面の照り返し。
        ///
        /// 空の明るさをそのまま使うと明るすぎる（室内まで昼の外の明るさになる）ので、
        /// 空の色の割合だけを持ってきて、明るさは部屋の中が沈みすぎない所へ寄せる。
        /// 日の円と周りの明るみは入れない。あれは Morning の灯りが受け持つ
        /// </summary>
        static void EstateAmbient(SkyPaint.Look look, out Color sky, out Color equator, out Color ground)
        {
            var calm = look;
            calm.glowNear = 0f;
            calm.glowWide = 0f;
            calm.discRadius = 0f;
            calm.discSoft = 1e-3f;
            var up = Color.black;
            var side = Color.black;
            var upWeight = 0f;
            var sideWeight = 0f;
            for (var e = 0; e < 18; e++)
            {
                var el = (e + 0.5f) * 5f;
                for (var a = 0; a < 36; a++)
                {
                    var d = Quaternion.Euler(-el, a * 10f, 0f) * Vector3.forward;
                    var c = SkyPaint.At(d, calm);
                    // 上向きの面が受ける光は、仰角の sin（面への入り方）と cos（その仰角の帯の広さ）で重みを付ける
                    var w = Mathf.Sin(el * Mathf.Deg2Rad) * Mathf.Cos(el * Mathf.Deg2Rad);
                    up += c * w;
                    upWeight += w;
                    if (el < 20f) { side += c; sideWeight += 1f; }
                }
            }
            up /= upWeight;
            side /= sideWeight;
            // 明るさの寄せ。linear の値に掛ける
            const float skyGain = 0.12f;
            const float sideGain = 0.06f;
            const float groundGain = 0.10f;
            sky = Gamma(up * skyGain);
            equator = Gamma(side * sideGain);
            ground = Gamma(EstateBounce.linear * groundGain);
        }

        static Color Gamma(Color linear)
        {
            var c = linear.gamma;
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// 空の絵を焼く。式と値が前回と同じなら焼き直さない（絵の取り込みの userData に印を持つ）。
        ///
        /// **mipmap は作らない。** Skybox/Panoramic は絵の左右の継ぎ目で uv が 1 から 0 へ跳ぶので、
        /// mipmap があると継ぎ目で一番小さい段が選ばれて、空に縦の線が出る
        /// </summary>
        static Texture2D EstateSkyPicture(SkyPaint.Look look)
        {
            var sign = EstateSkySign(look);
            var importer = AssetImporter.GetAtPath(EstateSkyPath) as TextureImporter;
            if (importer == null || importer.userData != sign || !File.Exists(EstateSkyPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                    AssetDatabase.CreateFolder("Assets/Textures", "Dive");
                var pic = new Texture2D(EstateSkyWide, EstateSkyHigh, TextureFormat.RGB24, false, false);
                var row = new Color32[EstateSkyWide];
                for (var y = 0; y < EstateSkyHigh; y++)
                {
                    var v = (y + 0.5f) / EstateSkyHigh;
                    for (var x = 0; x < EstateSkyWide; x++)
                    {
                        var u = (x + 0.5f) / EstateSkyWide;
                        var c = SkyPaint.At(SkyPaint.Direction(u, v), look).gamma;
                        row[x] = new Color32(Byte(c.r), Byte(c.g), Byte(c.b), 255);
                    }
                    pic.SetPixels32(0, y, EstateSkyWide, 1, row);
                }
                pic.Apply();
                File.WriteAllBytes(EstateSkyPath, pic.EncodeToPNG());
                Object.DestroyImmediate(pic);
                AssetDatabase.ImportAsset(EstateSkyPath);
                importer = AssetImporter.GetAtPath(EstateSkyPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = false;
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = EstateSkyWide;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.userData = sign;
                    importer.SaveAndReimport();
                }
                Debug.Log("団地の空の絵を焼いた: " + EstateSkyPath);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(EstateSkyPath);
        }

        static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        /// <summary>空の絵の印。式の版と見え方の値を並べた文字列</summary>
        static string EstateSkySign(SkyPaint.Look look)
        {
            return "sky1|" + EstateSkyWide + "x" + EstateSkyHigh + "|" + JsonUtility.ToJson(look);
        }

        /// <summary>
        /// 空のマテリアル。Skybox/Panoramic に絵を貼る。
        /// **_Tint は灰色 0.5 のままにしない。** linear の場では 0.5 が 0.214 に直り、それに 4.59 を掛けるので
        /// 絵の色がわずかに沈む（0.983 倍）。1 倍になる灰色を渡す
        /// </summary>
        static Material EstateSkyMaterial(Texture2D picture)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(EstateSkyMatPath);
            var shader = Shader.Find("Skybox/Panoramic");
            if (m == null)
            {
                m = new Material(shader);
                m.name = "EstateSkybox";
                AssetDatabase.CreateAsset(m, EstateSkyMatPath);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_MainTex", picture);
            // unity_ColorSpaceDouble（linear では 4.5948）の逆数を掛けて、絵の色がそのまま出る灰色
            var g = Mathf.LinearToGammaSpace(1f / 4.59479380f);
            m.SetColor("_Tint", new Color(g, g, g, 1f));
            m.SetFloat("_Exposure", 1f);
            m.SetFloat("_Rotation", 0f);
            m.SetFloat("_Mapping", 1f);
            m.SetFloat("_ImageType", 0f);
            m.SetFloat("_MirrorOnBack", 0f);
            m.SetFloat("_Layout", 0f);
            m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 空の絵を持たない場所。今までどおり一色の空で、霞は掛けない。
        /// 環境光は <see cref="Stage"/> がシーンに置く値と同じ
        /// </summary>
        static PlaceSky PlainSky(Color flat)
        {
            return PlaceSky.Plain(flat, StageAmbientSky, StageAmbientEquator, StageAmbientGround);
        }
    }
}
