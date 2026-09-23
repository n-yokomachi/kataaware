using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Rocketbox の元のテクスチャ（2048 の TGA、リポジトリの外の unity/RawAssets/rocketbox/ に置く）から、
    /// 512 の PNG の写しを作って Assets/Models/rocketbox/ に置く。リポジトリに入れるのはこの写しだけ。
    ///
    /// 縮め方は、線形の光に戻してから 4×4（256 なら 8×8）の平均。透けのある絵は α で重みを付けて平均し、
    /// 透けた地の色（髪の絵の地は肌色）が毛の縁へ滲まないようにする
    /// </summary>
    public static class RocketboxTextures
    {
        /// <summary>元の置き場。プロジェクトの根（Assets の一つ上）からの相対</summary>
        public const string RawDir = "RawAssets/rocketbox";

        public static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        [MenuItem("HalfAware/Rocketbox/Shrink the textures")]
        public static void ShrinkMenu()
        {
            foreach (var who in RocketboxPerson.All) Debug.Log(Shrink(who.Name, who.RawTextures, 512));
        }

        /// <summary>
        /// 一人分のテクスチャを縮めて書く。書き先は Assets/Models/rocketbox/{who}/{name}.png。
        /// 元が無ければ例外（落とすのはオーナーの許しが要る）
        /// </summary>
        public static string Shrink(string who, string[] names, int size)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in names)
            {
                var src = Path.Combine(ProjectRoot, RawDir, who, n + ".tga");
                if (!File.Exists(src)) throw new FileNotFoundException("元のテクスチャが無い（RawAssets に落としておく）: " + src);
                int w, h;
                bool alpha;
                var px = ReadTga(src, out w, out h, out alpha);
                var small = Downsample(px, w, h, size, alpha);
                var path = RocketboxImport.Root + who + "/" + n + ".png";
                WritePng(small, size, size, path, alpha);
                sb.AppendFormat("{0}: {1}×{2}{3} → {4}（{5}）\n", n, w, h, alpha ? " α" : "", size, path);
            }
            return sb.ToString();
        }

        /// <summary>PNG を書いて取り込む。α が無ければ RGB で書く。取り込みの設定は <see cref="RocketboxImport"/> が決める</summary>
        public static void WritePng(Color32[] px, int w, int h, string assetPath, bool alpha)
        {
            var t = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            try
            {
                t.SetPixels32(px);
                t.Apply();
                var full = Path.Combine(ProjectRoot, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, t.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Assets の中の PNG を、取り込みの設定（縮小・圧縮）を通さずにそのまま読む</summary>
        public static Color32[] ReadPng(string assetPath, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(Path.Combine(ProjectRoot, assetPath))))
                    throw new InvalidDataException("PNG を読めない: " + assetPath);
                w = t.width;
                h = t.height;
                return t.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }

        /// <summary>
        /// 圧縮していない TGA（型 2、24 か 32 bit）を読む。並びは Unity と同じく下の行から。
        /// Rocketbox の TGA はこの形（頭と体は 24 bit、透けの絵は 32 bit）
        /// </summary>
        public static Color32[] ReadTga(string path, out int w, out int h, out bool alpha)
        {
            var b = File.ReadAllBytes(path);
            int idLen = b[0], cmapType = b[1], type = b[2];
            if (cmapType != 0 || type != 2) throw new NotSupportedException("圧縮していない真色の TGA だけ読める（型 " + type + "）: " + path);
            w = b[12] | (b[13] << 8);
            h = b[14] | (b[15] << 8);
            int bpp = b[16], desc = b[17];
            if (bpp != 24 && bpp != 32) throw new NotSupportedException("24 か 32 bit の TGA だけ読める（" + bpp + "）: " + path);
            alpha = bpp == 32;
            var topDown = (desc & 0x20) != 0;
            var rightLeft = (desc & 0x10) != 0;
            var step = bpp / 8;
            var o = 18 + idLen;
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                var row = topDown ? h - 1 - y : y;
                for (var x = 0; x < w; x++)
                {
                    var col = rightLeft ? w - 1 - x : x;
                    var k = o + (y * w + x) * step;
                    px[row * w + col] = new Color32(b[k + 2], b[k + 1], b[k], alpha ? b[k + 3] : (byte)255);
                }
            }
            return px;
        }

        /// <summary>正方形の絵を size へ縮める。線形の光で平均し、α があれば α で重みを付ける</summary>
        public static Color32[] Downsample(Color32[] px, int w, int h, int size, bool alpha)
        {
            if (w != h || w % size != 0) throw new ArgumentException("正方形で、size で割り切れる大きさだけ縮められる");
            var f = w / size;
            var lin = new float[256];
            for (var i = 0; i < 256; i++) lin[i] = Mathf.GammaToLinearSpace(i / 255f);
            var o = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    double r = 0, g = 0, bl = 0, a = 0, pr = 0, pg = 0, pb = 0;
                    for (var dy = 0; dy < f; dy++)
                        for (var dx = 0; dx < f; dx++)
                        {
                            var c = px[(y * f + dy) * w + x * f + dx];
                            var ca = c.a / 255.0;
                            r += lin[c.r]; g += lin[c.g]; bl += lin[c.b]; a += ca;
                            pr += lin[c.r] * ca; pg += lin[c.g] * ca; pb += lin[c.b] * ca;
                        }
                    var n = (double)(f * f);
                    if (alpha && a > 1e-4)
                    {
                        r = pr / a; g = pg / a; bl = pb / a;
                    }
                    else
                    {
                        r /= n; g /= n; bl /= n;
                    }
                    o[y * size + x] = new Color32(ToByte(r), ToByte(g), ToByte(bl), alpha ? (byte)Mathf.RoundToInt((float)(a / n) * 255f) : (byte)255);
                }
            return o;
        }

        static byte ToByte(double linear)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace((float)linear) * 255f), 0, 255);
        }
    }
}
