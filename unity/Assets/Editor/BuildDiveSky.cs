using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の場所ごとの空の、場所をまたいで使う道具（設計書 9.1 節「空と光」）。
    ///
    /// 空・日・霞・環境光を一揃いで作る仕組みは公営住宅で決めた（<c>BuildDiveEstateSky.cs</c>）。
    /// 公園も同じ仕組みで作るので、空の絵を焼く・空のマテリアルを作る・環境光を空から取るの三つをここへ出してある。
    /// 見え方の値（<see cref="SkyPaint.Look"/>）と日の向きは、場所ごとのファイルが持つ
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>空の絵と遠景の書き割りの絵の置き場</summary>
        const string DiveTextures = "Assets/Textures/Dive/";

        /// <summary>空の絵の大きさ。横 2048 で一画素 0.18 度。960×540 で見ても日の円の縁が崩れない</summary>
        const int SkyWide = 2048;
        const int SkyHigh = 1024;

        /// <summary>
        /// 環境光を空から取る。上は天の半球の平均、横は地平の帯の平均、下は地面の照り返し（<paramref name="bounce"/>、sRGB）。
        ///
        /// 空の明るさをそのまま使うと明るすぎる（室内まで昼の外の明るさになる）ので、
        /// 空の色の割合だけを持ってきて、明るさは部屋の中が沈みすぎない所へ寄せる。
        /// 日の円と周りの明るみは入れない。あれは影を落とす日の灯りが受け持つ。
        /// <paramref name="gain"/> は寄せた明るさにさらに掛ける倍率。部屋を持たない場所（公園）は 1 より上げてよい
        /// </summary>
        static void SkyAmbient(SkyPaint.Look look, Color bounce, float gain, out Color sky, out Color equator, out Color ground)
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
            sky = Gamma(up * (skyGain * gain));
            equator = Gamma(side * (sideGain * gain));
            ground = Gamma(bounce.linear * (groundGain * gain));
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
        static Texture2D SkyPicture(string path, SkyPaint.Look look)
        {
            var sign = SkySign(look);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.userData != sign || !File.Exists(path))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                    AssetDatabase.CreateFolder("Assets/Textures", "Dive");
                var pic = new Texture2D(SkyWide, SkyHigh, TextureFormat.RGB24, false, false);
                var row = new Color32[SkyWide];
                for (var y = 0; y < SkyHigh; y++)
                {
                    var v = (y + 0.5f) / SkyHigh;
                    for (var x = 0; x < SkyWide; x++)
                    {
                        var u = (x + 0.5f) / SkyWide;
                        var c = SkyPaint.At(SkyPaint.Direction(u, v), look).gamma;
                        row[x] = new Color32(Byte(c.r), Byte(c.g), Byte(c.b), 255);
                    }
                    pic.SetPixels32(0, y, SkyWide, 1, row);
                }
                pic.Apply();
                File.WriteAllBytes(path, pic.EncodeToPNG());
                Object.DestroyImmediate(pic);
                AssetDatabase.ImportAsset(path);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = false;
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = SkyWide;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.userData = sign;
                    importer.SaveAndReimport();
                }
                Debug.Log("空の絵を焼いた: " + path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        /// <summary>空の絵の印。式の版と見え方の値を並べた文字列</summary>
        static string SkySign(SkyPaint.Look look)
        {
            return "sky1|" + SkyWide + "x" + SkyHigh + "|" + JsonUtility.ToJson(look);
        }

        /// <summary>
        /// 空のマテリアル。Skybox/Panoramic に絵を貼る。
        /// **_Tint は灰色 0.5 のままにしない。** linear の場では 0.5 が 0.214 に直り、それに 4.59 を掛けるので
        /// 絵の色がわずかに沈む（0.983 倍）。1 倍になる灰色を渡す
        /// </summary>
        static Material SkyMaterial(string path, Texture2D picture)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Panoramic");
            if (m == null)
            {
                m = new Material(shader);
                m.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(m, path);
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

        /// <summary>日へ向かう向き。影を落とす灯り（場所の子の <paramref name="sunName"/>）の逆。灯りがまだ無ければ組み立ての値から出す</summary>
        static Vector3 Sunward(Transform place, string sunName, Vector3 aim)
        {
            var sun = place != null ? place.Find(sunName) : null;
            if (sun != null) return -sun.forward;
            return -(Quaternion.Euler(aim) * Vector3.forward);
        }

        /// <summary>
        /// 空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す。
        /// 霞の色は空の地平の色（<see cref="SkyPaint.Look.horizon"/>）に揃える。
        /// <paramref name="ambientGain"/> は環境光の倍率（<see cref="SkyAmbient"/>）
        /// </summary>
        internal static PlaceSky PaintedSky(Transform place, string sunName, SkyPaint.Look look, Color horizon, float haze,
            Color bounce, float ambientGain, string picturePath, string materialPath)
        {
            var picture = SkyPicture(picturePath, look);
            var sun = place != null ? place.Find(sunName) : null;
            var sky = new PlaceSky
            {
                skybox = SkyMaterial(materialPath, picture),
                flat = horizon,
                haze = true,
                hazeColor = horizon,
                hazeDensity = haze,
                sun = sun != null ? sun.GetComponent<Light>() : null,
            };
            SkyAmbient(look, bounce, ambientGain, out sky.ambientSky, out sky.ambientEquator, out sky.ambientGround);
            return sky;
        }
    }
}
