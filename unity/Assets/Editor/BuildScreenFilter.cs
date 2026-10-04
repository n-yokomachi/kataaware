using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 画面のフィルターの「減色＋ディザ」（<see cref="ScreenFilter"/>、設計書 2.5 節）が使う物を作る。
    ///
    /// - 点の模様: 64×64 の青色雑音の絵（<see cref="NoisePath"/>）。void-and-cluster（Ulichney）で、
    ///   近い画素どうしが同じ閾値の並びにならないよう 4096 の順位を振る。種は固定なので、何度作っても同じ絵になる
    /// - 色の組: <see cref="Palette"/> の sRGB の色を、シェーダーが比べる OKLab と出すリニアの RGB へ直したテクスチャ（<see cref="PalettePath"/>）
    /// - 近い二色の表: sRGB の 64³ の升ごとに、近い二色の番号を引いた絵（<see cref="LutPath"/>）。シェーダーは画素ごとに 52 色と比べずに、これを一度引く
    /// - マテリアル: Ps1.mat と、タイトルの画面の背景のマテリアル（<see cref="PicturePath"/>）に三つを繋ぐ
    ///
    /// **色の組を変えたら、これを走らせて作り直す**（HalfAware/Build the screen filter）。色の組のテクスチャと表が組と揃っているかは
    /// ScreenFilterTests が見る
    /// </summary>
    public static class BuildScreenFilter
    {
        public const string NoisePath = "Assets/Textures/Fx/BlueNoise64.png";
        public const string PalettePath = "Assets/Textures/Fx/ScreenFilterPalette.asset";
        public const string LutPath = "Assets/Textures/Fx/ScreenFilterLut.png";
        public const string Ps1Path = "Assets/Materials/Fx/Ps1.mat";
        public const string PicturePath = "Assets/Resources/" + ScreenFilter.PictureMaterial + ".mat";
        public const string NoiseProperty = "_HaNoise";
        public const string PaletteProperty = "_HaPalette";
        public const string LutProperty = "_HaLut";

        /// <summary>点の模様の一辺。シェーダーの <c>HA_NOISE_SIZE</c> と揃える</summary>
        public const int NoiseSize = 64;

        /// <summary>色の組のテクスチャの幅。色の数の上限</summary>
        public const int PaletteWidth = 64;

        /// <summary>近い二色の表の、sRGB の色ごとの段の数と、青の段を並べる一辺の数。シェーダーの <c>HA_LUT_STEPS</c>・<c>HA_LUT_TILES</c> と揃える</summary>
        public const int LutSteps = 64;
        public const int LutTiles = 8;

        /// <summary>void-and-cluster の重みの広がり（画素）。1.5 が元の論文の値</summary>
        const double Sigma = 1.5;
        const int Seed = 20261005;

        [MenuItem("HalfAware/Build the screen filter", false, 185)]
        public static void Menu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine(WriteNoise());
            sb.AppendLine(WritePalette());
            sb.AppendLine(WriteLut());
            sb.AppendLine(Wire());
            return sb.ToString().TrimEnd();
        }

        /// <summary>Ps1.mat とタイトルの画面の背景のマテリアルに、点の模様・色の組・近い二色の表を繋ぐ。背景のマテリアルが無ければ作る</summary>
        public static string Wire()
        {
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            if (noise == null) return "点の模様が無い: " + NoisePath;
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            if (palette == null) return "色の組が無い: " + PalettePath;
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath);
            if (lut == null) return "近い二色の表が無い: " + LutPath;
            var ps1 = AssetDatabase.LoadAssetAtPath<Material>(Ps1Path);
            if (ps1 == null) return "Ps1 のマテリアルが無い: " + Ps1Path;
            Give(ps1, noise, palette, lut);
            var shader = Shader.Find("HalfAware/FilteredPicture");
            if (shader == null) return "HalfAware/FilteredPicture が無い";
            var picture = AssetDatabase.LoadAssetAtPath<Material>(PicturePath);
            if (picture == null)
            {
                picture = new Material(shader);
                AssetDatabase.CreateAsset(picture, PicturePath);
            }
            picture.shader = shader;
            Give(picture, noise, palette, lut);
            AssetDatabase.SaveAssetIfDirty(ps1);
            AssetDatabase.SaveAssetIfDirty(picture);
            return "マテリアル → " + Ps1Path + "・" + PicturePath;
        }

        static void Give(Material m, Texture2D noise, Texture2D palette, Texture2D lut)
        {
            m.SetTexture(NoiseProperty, noise);
            m.SetTexture(PaletteProperty, palette);
            m.SetTexture(LutProperty, lut);
            EditorUtility.SetDirty(m);
        }

        // ---- 点の模様 ----------------------------------------------------------

        /// <summary>青色雑音を作って PNG で書き、取り込みを決める（最近傍・繰り返し・ミップ無し・リニア・一色）</summary>
        public static string WriteNoise()
        {
            var rank = BlueNoise(NoiseSize, Sigma, Seed);
            var n = NoiseSize * NoiseSize;
            var px = new Color32[n];
            for (var i = 0; i < n; i++)
            {
                // 順位 0〜4095 を 0〜255 へ。一つの値に 16 画素ずつ
                var v = (byte)(rank[i] * 256 / n);
                px[i] = new Color32(v, v, v, 255);
            }
            var tex = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, false, true);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(NoisePath));
                File.WriteAllBytes(NoisePath, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(NoisePath, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(NoisePath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.SingleChannel;
                imp.sRGBTexture = false;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = 64;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            return "点の模様 → " + NoisePath + "（" + NoiseSize + "×" + NoiseSize + "）";
        }

        /// <summary>
        /// void-and-cluster で size×size の画素に 0〜size²−1 の順位を振る（並びは y × size + x）。
        /// 重みは輪のように繋いだ（端が向こうの端へ回る）ガウス。敷き詰めても継ぎ目に模様が出ない
        /// </summary>
        public static int[] BlueNoise(int size, double sigma, int seed)
        {
            var n = size * size;
            var kernel = new double[n];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Math.Min(x, size - x);
                    var dy = Math.Min(y, size - y);
                    kernel[y * size + x] = Math.Exp(-(dx * dx + dy * dy) / (2.0 * sigma * sigma));
                }

            // 初めの粗い点。一割ほどを散らし、塊と空きを入れ替えて落ち着かせる
            var random = new System.Random(seed);
            var start = new bool[n];
            var ones = n / 10;
            for (var placed = 0; placed < ones;)
            {
                var i = random.Next(n);
                if (start[i]) continue;
                start[i] = true;
                placed++;
            }
            var energy = Energy(start, kernel, size);
            for (var guard = 0; guard < n * 4; guard++)
            {
                var cluster = Tightest(start, energy, true);
                Flip(start, energy, kernel, size, cluster);
                var voidAt = Tightest(start, energy, false);
                if (voidAt == cluster)
                {
                    Flip(start, energy, kernel, size, cluster);
                    break;
                }
                Flip(start, energy, kernel, size, voidAt);
            }

            var rank = new int[n];
            // 一: 初めの点から塊を一つずつ抜き、上から順位を振る
            var bits = (bool[])start.Clone();
            energy = Energy(bits, kernel, size);
            for (var r = ones - 1; r >= 0; r--)
            {
                var cluster = Tightest(bits, energy, true);
                Flip(bits, energy, kernel, size, cluster);
                rank[cluster] = r;
            }
            // 二: 初めの点から空きを一つずつ埋め、半分まで
            bits = (bool[])start.Clone();
            energy = Energy(bits, kernel, size);
            var count = ones;
            for (; count < n / 2; count++)
            {
                var voidAt = Tightest(bits, energy, false);
                Flip(bits, energy, kernel, size, voidAt);
                rank[voidAt] = count;
            }
            // 三: 残りは白黒を返して、残っている空き（返した側の塊）を詰まっている所から埋める
            var inverse = new bool[n];
            for (var i = 0; i < n; i++) inverse[i] = !bits[i];
            energy = Energy(inverse, kernel, size);
            for (; count < n; count++)
            {
                var cluster = Tightest(inverse, energy, true);
                Flip(inverse, energy, kernel, size, cluster);
                rank[cluster] = count;
            }
            return rank;
        }

        static double[] Energy(bool[] bits, double[] kernel, int size)
        {
            var e = new double[bits.Length];
            for (var i = 0; i < bits.Length; i++)
                if (bits[i]) Spread(e, kernel, size, i, 1.0);
            return e;
        }

        static void Spread(double[] e, double[] kernel, int size, int at, double sign)
        {
            var ax = at % size;
            var ay = at / size;
            for (var y = 0; y < size; y++)
            {
                var ky = (y - ay + size) % size;
                for (var x = 0; x < size; x++)
                {
                    var kx = (x - ax + size) % size;
                    e[y * size + x] += sign * kernel[ky * size + kx];
                }
            }
        }

        /// <summary>on なら点の中でいちばん詰まった所（重みが最も大きい）、そうでなければ空きの中でいちばん空いた所（最も小さい）</summary>
        static int Tightest(bool[] bits, double[] e, bool on)
        {
            var best = -1;
            var value = on ? double.MinValue : double.MaxValue;
            for (var i = 0; i < bits.Length; i++)
            {
                if (bits[i] != on) continue;
                if (on ? e[i] > value : e[i] < value)
                {
                    value = e[i];
                    best = i;
                }
            }
            return best;
        }

        static void Flip(bool[] bits, double[] e, double[] kernel, int size, int at)
        {
            bits[at] = !bits[at];
            Spread(e, kernel, size, at, bits[at] ? 1.0 : -1.0);
        }

        // ---- 色の組 ------------------------------------------------------------

        /// <summary>
        /// 色の組。sRGB の 16 進で 52 色。並びは表の並びになる（並びで見え方は変わらない）。
        ///
        /// 手本（オーナーが渡した動画）の色合いに寄せて、明るさの段ごとに色味をずらしてある。影は赤紫の黒、
        /// 中ほどの暗さは緑み（オリーブ）、明るい所は桃色寄りの藤色、いちばん上は白。灰色の壁は、同じ明るさの赤紫と緑の点が混ざって見える。
        /// ここまでで 25 色。残りは場面の色を潰さないための色: 自室の紫の空気（藤色の段）、夜の路地裏と車の中の暗い紺、
        /// 石と打ち放しの青みの灰、村の朝の空色、路地裏のネオン（青緑・緑・桃・赤・黄）、煉瓦と木の茶、電話ボックスの赤。
        /// 色は OKLCh（明るさ・彩度・色相）で決め、それを sRGB に直して書いてある（右の注）
        /// </summary>
        public static readonly string[] Palette =
        {
            // 影。赤紫の黒（0.06〜0.27）と、夜の紺（0.13・0.20）
            "010001", // L 0.06 C 0.015 h 330
            "0e040c", // L 0.13 C 0.03  h 335
            "030713", // L 0.13 C 0.03  h 265
            "220e1c", // L 0.20 C 0.04  h 340
            "0c1626", // L 0.20 C 0.035 h 260
            "361d2b", // L 0.27 C 0.045 h 345
            // 中ほどの暗さ。緑み（オリーブ）
            "171d11", // L 0.22 C 0.025 h 130
            "2c3420", // L 0.31 C 0.035 h 125
            "454b32", // L 0.40 C 0.04  h 120
            "5f6349", // L 0.49 C 0.04  h 115
            "7b7c65", // L 0.58 C 0.035 h 110
            // 明るい所。くすんだ桃色から、白に近い桃色まで
            "563a3b", // L 0.38 C 0.04  h 15
            "725150", // L 0.47 C 0.045 h 20
            "8d6b67", // L 0.56 C 0.045 h 25
            "a9857f", // L 0.65 C 0.045 h 30
            "c3a29a", // L 0.74 C 0.04  h 35
            "dcc0b7", // L 0.83 C 0.035 h 40
            "efddd2", // L 0.91 C 0.025 h 55
            "ffffff", // L 1.00
            // 藤色。自室の紫の空気
            "2b2243", // L 0.28 C 0.06  h 295
            "443760", // L 0.37 C 0.07  h 298
            "63507e", // L 0.47 C 0.075 h 302
            "85709c", // L 0.58 C 0.07  h 306
            "aa94bb", // L 0.70 C 0.06  h 310
            "ccb9d5", // L 0.81 C 0.045 h 315
            // 青みの灰。打ち放しと石、夜の舗装
            "273442", // L 0.32 C 0.03  h 250
            "425565", // L 0.44 C 0.035 h 245
            "627887", // L 0.56 C 0.035 h 240
            "c2cbd8", // L 0.84 C 0.02  h 255
            // 空色。灰から青への移りに段を足してある（記憶の団地の空が、灰の側と青の側で二つの面に割れた）
            "6d8aa3", // L 0.62 C 0.05  h 245
            "749dc1", // L 0.68 C 0.07  h 245
            "95bdda", // L 0.78 C 0.06  h 240
            "bedded", // L 0.88 C 0.04  h 230
            "dceaf4", // L 0.93 C 0.02  h 240
            // 青
            "162c55", // L 0.30 C 0.08  h 262
            "2b5591", // L 0.45 C 0.11  h 258
            // 青緑。ネオンと画面
            "17625e", // L 0.45 C 0.07  h 190
            "3fa0a0", // L 0.65 C 0.09  h 195
            "76e2e2", // L 0.85 C 0.10  h 195
            // 緑。草と生け垣、緑のネオン
            "305927", // L 0.42 C 0.09  h 140
            "518b44", // L 0.58 C 0.12  h 140
            "68cb6e", // L 0.76 C 0.16  h 145
            // 黄と橙。灯り
            "d09945", // L 0.72 C 0.12  h 75
            "f2e075", // L 0.90 C 0.13  h 100
            "c7692c", // L 0.62 C 0.14  h 50
            // 赤。電話ボックスと赤い灯り
            "5c1717", // L 0.32 C 0.10  h 25
            "7f2021", // L 0.40 C 0.13  h 25
            "c8393a", // L 0.56 C 0.18  h 25
            // 桃。ネオン
            "b22578", // L 0.52 C 0.19  h 350
            "e96fa3", // L 0.70 C 0.16  h 355
            // 茶。煉瓦の陰と木
            "492e1b", // L 0.33 C 0.05  h 55
            "754e2e", // L 0.46 C 0.07  h 60
        };

        /// <summary>
        /// 色の組をテクスチャに書く（<see cref="PalettePath"/>、<see cref="PaletteWidth"/>×2 の半精度）。
        /// 行 0 に OKLab（シェーダーが二色を結ぶ線へ色を落とすのに使う）、行 1 に出す色（リニアの RGB）。
        /// 有る物は中身だけ書き換える（guid を変えない）
        /// </summary>
        public static string WritePalette()
        {
            if (Palette.Length > PaletteWidth) return "色の組が " + PaletteWidth + " 色を超えている";
            var px = PalettePixels();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            if (tex == null || tex.width != PaletteWidth || tex.height != 2 || tex.format != TextureFormat.RGBAHalf)
            {
                if (tex != null) AssetDatabase.DeleteAsset(PalettePath);
                tex = new Texture2D(PaletteWidth, 2, TextureFormat.RGBAHalf, false, true);
                tex.SetPixels(px);
                tex.Apply(false, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 0;
                AssetDatabase.CreateAsset(tex, PalettePath);
            }
            else
            {
                tex.SetPixels(px);
                tex.Apply(false, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                EditorUtility.SetDirty(tex);
                AssetDatabase.SaveAssetIfDirty(tex);
            }
            return "色の組 → " + PalettePath + "（" + Palette.Length + " 色）";
        }

        /// <summary>色の組のテクスチャの中身。行 0 が OKLab、行 1 がリニアの RGB。組の外の升は黒</summary>
        public static Color[] PalettePixels()
        {
            var px = new Color[PaletteWidth * 2];
            for (var i = 0; i < PaletteWidth; i++)
            {
                if (i >= Palette.Length)
                {
                    px[i] = Color.black;
                    px[PaletteWidth + i] = Color.black;
                    continue;
                }
                var c = Hex(Palette[i]);
                var lab = OkLab(c);
                px[i] = new Color(lab.x, lab.y, lab.z, 1f);
                px[PaletteWidth + i] = new Color((float)Linear(c.x), (float)Linear(c.y), (float)Linear(c.z), 1f);
            }
            return px;
        }

        // ---- 近い二色の表 ------------------------------------------------------

        /// <summary>
        /// 近い二色の表を作って PNG で書く（<see cref="LutPath"/>）。sRGB の各色を <see cref="LutSteps"/> 段に分けた升ごとに、
        /// 升の真ん中の色からいちばん近い色と二番めに近い色の番号を r と g に入れる。青の段は 8×8 に並べて 512×512 に収める
        /// （x = 赤 + 64 ×（青 % 8）、y = 緑 + 64 ×（青 / 8））。取り込みは最近傍・ミップ無し・リニア・圧縮しない
        /// </summary>
        public static string WriteLut()
        {
            var side = LutSteps * LutTiles;
            var px = LutPixels();
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false, true);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(LutPath));
                File.WriteAllBytes(LutPath, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(LutPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(LutPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = side;
                imp.SaveAndReimport();
            }
            return "近い二色の表 → " + LutPath + "（" + side + "×" + side + "、" + LutSteps + "³ の升）";
        }

        /// <summary>近い二色の表の中身（行が下から、左から）</summary>
        public static Color32[] LutPixels()
        {
            var labs = new Vector3[Palette.Length];
            for (var i = 0; i < labs.Length; i++) labs[i] = OkLab(Hex(Palette[i]));
            var side = LutSteps * LutTiles;
            var px = new Color32[side * side];
            for (var b = 0; b < LutSteps; b++)
                for (var g = 0; g < LutSteps; g++)
                    for (var r = 0; r < LutSteps; r++)
                    {
                        var p = OkLab(new Vector3(r / (LutSteps - 1f), g / (LutSteps - 1f), b / (LutSteps - 1f)));
                        int first, second;
                        Nearest(p, labs, out first, out second);
                        var x = r + LutSteps * (b % LutTiles);
                        var y = g + LutSteps * (b / LutTiles);
                        px[y * side + x] = new Color32((byte)first, (byte)second, 0, 255);
                    }
            return px;
        }

        /// <summary>p にいちばん近い色と二番めに近い色の番号。近さは OKLab の距離</summary>
        public static void Nearest(Vector3 p, Vector3[] labs, out int first, out int second)
        {
            first = 0;
            second = 0;
            var d1 = float.MaxValue;
            var d2 = float.MaxValue;
            for (var i = 0; i < labs.Length; i++)
            {
                var d = (p - labs[i]).sqrMagnitude;
                if (d < d1)
                {
                    d2 = d1;
                    second = first;
                    d1 = d;
                    first = i;
                }
                else if (d < d2)
                {
                    d2 = d;
                    second = i;
                }
            }
        }

        /// <summary>16 進の sRGB を 0〜1 の三つに</summary>
        public static Vector3 Hex(string hex)
        {
            var v = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Vector3(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }

        /// <summary>sRGB（0〜1）を OKLab へ（Björn Ottosson の式。シェーダーの HaOkLab と同じ）</summary>
        public static Vector3 OkLab(Vector3 srgb)
        {
            double r = Linear(srgb.x), g = Linear(srgb.y), b = Linear(srgb.z);
            var l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
            var m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
            var s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;
            l = Math.Pow(l, 1.0 / 3.0);
            m = Math.Pow(m, 1.0 / 3.0);
            s = Math.Pow(s, 1.0 / 3.0);
            return new Vector3(
                (float)(0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s),
                (float)(1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s),
                (float)(0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s));
        }

        /// <summary>sRGB の一つの値（0〜1）をリニアへ</summary>
        public static double Linear(double c)
        {
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
