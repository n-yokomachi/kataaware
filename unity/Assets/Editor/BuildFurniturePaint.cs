using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildFurniture
    {
        // ---- アトラス --------------------------------------------------------------
        //
        // 512×256 を点で引く一枚。左の半分が家具の絵（下の段に色の升、その上に繰り返す地と、札・画面・背表紙などの小さな絵）、
        // 右の半分が額の絵（128×128 の升を四つ）。α は艶（URP Lit の Smoothness を albedo の α から取る）。
        // 光る所は同じ升目の別の絵（Glow）に置く

        public const int AtlasW = 512;
        public const int AtlasH = 256;

        // 繰り返す地（32×32 と 64×64）
        static readonly RectInt FabricArea = new RectInt(0, 16, 32, 32);
        static readonly RectInt WoolArea = new RectInt(32, 16, 32, 32);
        static readonly RectInt CottonArea = new RectInt(64, 16, 32, 32);
        static readonly RectInt TopArea = new RectInt(96, 16, 32, 32);
        static readonly RectInt WalnutArea = new RectInt(128, 16, 32, 32);
        static readonly RectInt SteelArea = new RectInt(160, 16, 32, 32);
        static readonly RectInt LaminateArea = new RectInt(192, 16, 32, 32);
        static readonly RectInt LeafArea = new RectInt(224, 16, 32, 32);
        static readonly RectInt FurArea = new RectInt(0, 48, 64, 64);
        // 棚に差す機械の顔（下から UPS・二台の 2U・1U・スイッチ・パッチパネル・目隠し・ストレージ・側板の孔）
        static readonly RectInt RackArea = new RectInt(64, 48, 64, 64);
        // 背表紙（4×32 を横に 16、二段）
        static readonly RectInt SpineArea = new RectInt(128, 48, 64, 64);
        static readonly RectInt HobArea = new RectInt(192, 48, 32, 32);
        static readonly RectInt GrilleArea = new RectInt(224, 48, 32, 8);
        static readonly RectInt FilterArea = new RectInt(224, 56, 16, 12);
        static readonly RectInt FridgeDisplayArea = new RectInt(240, 56, 8, 4);
        static readonly RectInt KettleSwitchArea = new RectInt(248, 56, 6, 6);
        // 小さな絵
        static readonly RectInt MicrowaveDoorArea = new RectInt(0, 112, 24, 14);
        static readonly RectInt MicrowavePanelArea = new RectInt(24, 112, 8, 14);
        static readonly RectInt CoffeeFaceArea = new RectInt(32, 112, 12, 14);
        static readonly RectInt MemoArea = new RectInt(44, 112, 12, 16);
        static readonly RectInt MenuArea = new RectInt(56, 112, 12, 16);
        static readonly RectInt MagazineArea = new RectInt(68, 112, 24, 16);
        static readonly RectInt TeaBoxArea = new RectInt(92, 112, 16, 10);
        static readonly RectInt SachetArea = new RectInt(108, 112, 18, 8);
        static readonly RectInt AcFrontArea = new RectInt(128, 112, 40, 10);
        static readonly RectInt JarLabelArea = new RectInt(0, 128, 80, 8);
        static readonly RectInt TeaTinArea = new RectInt(80, 128, 16, 8);
        static readonly RectInt CoffeeTinArea = new RectInt(96, 128, 16, 8);
        static readonly RectInt ChipLabelArea = new RectInt(112, 128, 48, 6);
        static readonly RectInt CaseSpineArea = new RectInt(160, 128, 24, 12);
        static readonly RectInt DrawerLabelArea = new RectInt(184, 128, 24, 4);
        static readonly RectInt PagesArea = new RectInt(208, 128, 8, 8);
        static readonly RectInt BoxLabelArea = new RectInt(216, 128, 24, 8);
        // 仕事の机の周り（鍵盤の上の面、PC の前と横の硝子）
        static readonly RectInt KeysArea = new RectInt(0, 144, 32, 12);
        static readonly RectInt PcFrontArea = new RectInt(32, 144, 12, 28);
        static readonly RectInt PcSideArea = new RectInt(44, 144, 24, 28);
        // 額の絵の升（右の半分）
        static readonly RectInt[] PaintingSlots =
        {
            new RectInt(256, 0, 128, 128), new RectInt(384, 0, 128, 128), new RectInt(256, 128, 128, 128), new RectInt(384, 128, 128, 128),
        };

        /// <summary>色の升（4×4 画素。下の段に 64 個ずつ 4 段）</summary>
        public enum Hue
        {
            Black, Rubber, PlasticDark, PlasticGrey, PlasticWhite, Steel, SteelDark, Chrome, Brass, WoodFoot,
            Cotton, Paper, PageEdge, Cardboard, GlassDark, GlassCoffee, Coffee, Salt, Pepper, Ketchup,
            BrownSauce, Soy, Oil, Vinegar, Paprika, Turmeric, Herbs, Cumin, Sugar, LidBlack,
            LidRed, LidWhite, Cork, GlassClear, Soil, PotDark, Stem, LeatherBlack, LeatherBrown, SneakerWhite,
            Sole, UmbrellaNavy, UmbrellaBlack, CaseSmoke, CaseBlue, LabelWhite, CableBlue, CableYellow, CableGrey, CableRed,
            MagnetRed, MagnetYellow, MagnetBlue, MugTeal, PlateWhite, AcWhite, Slot, FrameBlack, FrameWood, Canvas,
            LampShade, Bulb, LedGreen, LedBlue, LedAmber, LedRed, LedCyan, TinNavy, TinGold, Terracotta,
            Fabric, FabricDark, Wool, Laminate, Oak, Walnut, RackBlack, Foam, Water, CableBlack,
            WireWhite, Ceramic, Enamel, PianoBlack, Diffuser, LedViolet,
        }

        struct Swatch
        {
            public Color Color;
            public float Smooth;
            public Color Glow;
        }

        static Swatch S(float r, float g, float b, float smooth, float glow = 0f)
        {
            var c = new Color(r, g, b);
            return new Swatch { Color = c, Smooth = smooth, Glow = glow > 0f ? c * glow : Color.black };
        }

        /// <summary>色の升の中身（<see cref="Hue"/> の順）</summary>
        static readonly Swatch[] Swatches =
        {
            S(0.035f, 0.035f, 0.040f, 0.30f), S(0.050f, 0.050f, 0.055f, 0.15f), S(0.090f, 0.090f, 0.100f, 0.35f), S(0.300f, 0.300f, 0.310f, 0.35f),
            S(0.600f, 0.590f, 0.560f, 0.35f), S(0.450f, 0.460f, 0.480f, 0.55f), S(0.180f, 0.180f, 0.200f, 0.50f), S(0.620f, 0.630f, 0.660f, 0.80f),
            S(0.550f, 0.420f, 0.200f, 0.60f), S(0.035f, 0.032f, 0.032f, 0.45f),
            S(0.560f, 0.550f, 0.520f, 0.10f), S(0.720f, 0.700f, 0.630f, 0.10f), S(0.640f, 0.610f, 0.530f, 0.10f), S(0.450f, 0.360f, 0.250f, 0.10f),
            S(0.030f, 0.035f, 0.040f, 0.85f), S(0.080f, 0.050f, 0.035f, 0.85f), S(0.100f, 0.060f, 0.030f, 0.70f), S(0.780f, 0.780f, 0.760f, 0.30f),
            S(0.120f, 0.100f, 0.090f, 0.20f), S(0.500f, 0.060f, 0.050f, 0.60f),
            S(0.220f, 0.100f, 0.050f, 0.60f), S(0.060f, 0.030f, 0.020f, 0.70f), S(0.550f, 0.400f, 0.100f, 0.70f), S(0.550f, 0.500f, 0.350f, 0.70f),
            S(0.550f, 0.150f, 0.070f, 0.20f), S(0.700f, 0.500f, 0.080f, 0.20f), S(0.250f, 0.320f, 0.120f, 0.20f), S(0.420f, 0.280f, 0.140f, 0.20f),
            S(0.800f, 0.780f, 0.720f, 0.30f), S(0.060f, 0.060f, 0.060f, 0.40f),
            S(0.500f, 0.080f, 0.070f, 0.40f), S(0.700f, 0.700f, 0.680f, 0.40f), S(0.520f, 0.400f, 0.260f, 0.10f), S(0.350f, 0.400f, 0.400f, 0.85f),
            S(0.100f, 0.070f, 0.050f, 0.05f), S(0.160f, 0.150f, 0.160f, 0.55f), S(0.220f, 0.180f, 0.100f, 0.20f), S(0.060f, 0.055f, 0.055f, 0.45f),
            S(0.260f, 0.140f, 0.070f, 0.45f), S(0.620f, 0.620f, 0.600f, 0.20f),
            S(0.080f, 0.080f, 0.080f, 0.15f), S(0.060f, 0.080f, 0.140f, 0.75f), S(0.040f, 0.040f, 0.045f, 0.75f), S(0.100f, 0.100f, 0.120f, 0.70f),
            S(0.100f, 0.160f, 0.280f, 0.60f), S(0.750f, 0.740f, 0.700f, 0.15f), S(0.100f, 0.250f, 0.550f, 0.40f), S(0.650f, 0.520f, 0.100f, 0.40f),
            S(0.400f, 0.400f, 0.420f, 0.40f), S(0.550f, 0.100f, 0.080f, 0.40f),
            S(0.600f, 0.100f, 0.080f, 0.50f), S(0.750f, 0.600f, 0.100f, 0.50f), S(0.100f, 0.250f, 0.600f, 0.50f), S(0.160f, 0.300f, 0.320f, 0.60f),
            S(0.700f, 0.700f, 0.680f, 0.60f), S(0.150f, 0.150f, 0.160f, 0.40f), S(0.020f, 0.020f, 0.020f, 0.10f), S(0.040f, 0.038f, 0.038f, 0.45f),
            S(0.040f, 0.038f, 0.038f, 0.45f), S(0.300f, 0.290f, 0.260f, 0.15f),
            S(0.780f, 0.700f, 0.550f, 0.10f, 0.55f), S(1.000f, 0.850f, 0.600f, 0.10f, 1.20f), S(0.400f, 1.000f, 0.550f, 0.50f, 1.0f), S(0.300f, 0.550f, 1.000f, 0.50f, 1.0f),
            S(1.000f, 0.620f, 0.180f, 0.50f, 1.0f), S(1.000f, 0.280f, 0.250f, 0.50f, 1.0f), S(0.300f, 0.900f, 1.000f, 0.50f, 1.0f), S(0.080f, 0.100f, 0.220f, 0.60f),
            S(0.550f, 0.420f, 0.150f, 0.60f), S(0.450f, 0.220f, 0.120f, 0.25f),
            S(0.075f, 0.070f, 0.072f, 0.42f), S(0.050f, 0.047f, 0.048f, 0.30f), S(0.340f, 0.130f, 0.140f, 0.05f), S(0.060f, 0.060f, 0.065f, 0.22f),
            S(0.035f, 0.035f, 0.040f, 0.80f), S(0.200f, 0.130f, 0.080f, 0.35f), S(0.045f, 0.045f, 0.050f, 0.35f), S(0.520f, 0.500f, 0.430f, 0.05f),
            S(0.200f, 0.240f, 0.260f, 0.90f), S(0.030f, 0.030f, 0.035f, 0.40f),
            S(0.700f, 0.700f, 0.680f, 0.30f), S(0.620f, 0.600f, 0.560f, 0.60f), S(0.045f, 0.045f, 0.050f, 0.75f),
            S(0.030f, 0.030f, 0.034f, 0.88f), S(0.860f, 0.850f, 0.820f, 0.20f, 0.95f), S(0.660f, 0.400f, 1.000f, 0.50f, 1.0f),
        };

        // ---- uv ----------------------------------------------------------------------

        /// <summary>画素の置き場を uv の矩形に。点で引くので、縁の画素の外を拾わないよう内へ少し寄せる</summary>
        static Rect Uv(RectInt r)
        {
            return new Rect((r.x + 0.05f) / AtlasW, (r.y + 0.05f) / AtlasH, (r.width - 0.1f) / AtlasW, (r.height - 0.1f) / AtlasH);
        }

        /// <summary>画素の置き場のうち、(x, y, w, h) の小さな矩形</summary>
        static Rect Uv(RectInt r, int x, int y, int w, int h)
        {
            return Uv(new RectInt(r.x + x, r.y + y, w, h));
        }

        static Vector2 SwatchUv(Hue h)
        {
            var i = (int)h;
            return new Vector2(((i % 64) * 4 + 2f) / AtlasW, ((i / 64) * 4 + 2f) / AtlasH);
        }

        /// <summary>一色の升</summary>
        public static Tile Sw(Hue h)
        {
            return new Tile(new Rect(SwatchUv(h), Vector2.zero), -1f);
        }

        static Tile Tiled(RectInt area, float metre) { return new Tile(Uv(area), metre); }
        static Tile Whole(Rect uv) { return new Tile(uv, 0f); }

        static Tile Fabric { get { return Tiled(FabricArea, 0.30f); } }
        static Tile Wool { get { return Tiled(WoolArea, 0.16f); } }
        static Tile Cotton { get { return Tiled(CottonArea, 0.22f); } }
        static Tile BlackTop { get { return Tiled(TopArea, 0.45f); } }
        static Tile Walnut { get { return Tiled(WalnutArea, 0.40f); } }
        static Tile SteelBrushed { get { return Tiled(SteelArea, 0.5f); } }
        static Tile Laminate { get { return Tiled(LaminateArea, 0.32f); } }
        static Tile Fur { get { return Tiled(FurArea, 0.45f); } }

        /// <summary>背表紙の絵（0〜31）の uv</summary>
        static Rect SpineUv(int k)
        {
            k = ((k % 32) + 32) % 32;
            return Uv(SpineArea, (k % 16) * 4, (k / 16) * 32, 4, 32);
        }

        /// <summary>背表紙の地の色の一点（表紙に使う）</summary>
        static Tile SpineCover(int k)
        {
            k = ((k % 32) + 32) % 32;
            var x = SpineArea.x + (k % 16) * 4 + 1.5f;
            var y = SpineArea.y + (k / 16) * 32 + 6.5f;
            return new Tile(new Rect(x / AtlasW, y / AtlasH, 0f, 0f), -1f);
        }

        // ---- 絵を描く入れ物 --------------------------------------------------------

        /// <summary>albedo（α は艶）と光る所を同じ升目で持つ</summary>
        sealed class Canvas
        {
            public readonly Color[] Albedo = new Color[AtlasW * AtlasH];
            public readonly Color[] Glow = new Color[AtlasW * AtlasH];

            public void Put(int x, int y, Color c, float smooth)
            {
                if (x < 0 || y < 0 || x >= AtlasW || y >= AtlasH) return;
                c.a = smooth;
                Albedo[y * AtlasW + x] = c;
                Glow[y * AtlasW + x] = Color.black;
            }

            /// <summary>光る画素。albedo には色を半分に落として置く</summary>
            public void Lit(int x, int y, Color c, float level = 1f)
            {
                if (x < 0 || y < 0 || x >= AtlasW || y >= AtlasH) return;
                var a = c * 0.5f;
                a.a = 0.5f;
                Albedo[y * AtlasW + x] = a;
                var g = c * level;
                g.a = 1f;
                Glow[y * AtlasW + x] = g;
            }

            public Color Get(int x, int y)
            {
                return Albedo[Mathf.Clamp(y, 0, AtlasH - 1) * AtlasW + Mathf.Clamp(x, 0, AtlasW - 1)];
            }

            public void Fill(RectInt r, Color c, float smooth)
            {
                for (var y = r.y; y < r.yMax; y++) for (var x = r.x; x < r.xMax; x++) Put(x, y, c, smooth);
            }

            public void Fill(int x, int y, int w, int h, Color c, float smooth)
            {
                Fill(new RectInt(x, y, w, h), c, smooth);
            }

            public void LitFill(int x, int y, int w, int h, Color c, float level = 1f)
            {
                for (var j = y; j < y + h; j++) for (var i = x; i < x + w; i++) Lit(i, j, c, level);
            }

            public void Edge(RectInt r, Color c, float smooth)
            {
                for (var x = r.x; x < r.xMax; x++) { Put(x, r.y, c, smooth); Put(x, r.yMax - 1, c, smooth); }
                for (var y = r.y; y < r.yMax; y++) { Put(r.x, y, c, smooth); Put(r.xMax - 1, y, c, smooth); }
            }

            /// <summary>走り書きの一行（途切れ途切れの 1 画素の線）</summary>
            public void Scribble(int x, int y, int len, Color c, float smooth, int seed)
            {
                for (var i = 0; i < len; i++)
                    if (Hash(x + i, y + seed * 17) > 0.22f) Put(x + i, y, c, smooth);
            }
        }

        /// <summary>乱れの代わりの決まった値（0〜1）。押すたびに同じ絵にする</summary>
        static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>繰り返す升の中で継ぎ目の出ない、なめらかな揺らぎ（0〜1）。size は升の画素、cell は揺らぎの粒の画素</summary>
        static float TileNoise(int x, int y, int size, int cell, int seed)
        {
            var n = Mathf.Max(1, size / cell);
            var fx = (float)x / cell;
            var fy = (float)y / cell;
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var tx = Mathf.SmoothStep(0f, 1f, fx - x0);
            var ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            System.Func<int, int, float> at = (i, j) => Hash(((i % n) + n) % n + seed * 131, ((j % n) + n) % n + seed * 71);
            var a = Mathf.Lerp(at(x0, y0), at(x0 + 1, y0), tx);
            var b = Mathf.Lerp(at(x0, y0 + 1), at(x0 + 1, y0 + 1), tx);
            return Mathf.Lerp(a, b, ty);
        }

        static Color C(Hue h) { return Swatches[(int)h].Color; }

        // ---- 描く --------------------------------------------------------------------

        /// <summary>アトラスと光る所の絵を描いて置く。額の絵は Paintings の縮めた絵から写す（無ければ下塗りの布）</summary>
        static Texture2D PaintAtlas(out Texture2D glow, List<string> notes)
        {
            var cv = new Canvas();
            for (var i = 0; i < cv.Albedo.Length; i++) { cv.Albedo[i] = new Color(0.1f, 0.1f, 0.1f, 0.2f); cv.Glow[i] = Color.black; }
            for (var i = 0; i < Swatches.Length; i++)
            {
                var s = Swatches[i];
                var x = (i % 64) * 4;
                var y = (i / 64) * 4;
                for (var j = 0; j < 4; j++)
                    for (var k = 0; k < 4; k++)
                    {
                        if (s.Glow.maxColorComponent > 0f) cv.Lit(x + k, y + j, s.Glow, 1f);
                        else cv.Put(x + k, y + j, s.Color, s.Smooth);
                        if (s.Glow.maxColorComponent > 0f) { var a = s.Color; a.a = s.Smooth; cv.Albedo[(y + j) * AtlasW + x + k] = a; }
                    }
            }
            PaintFabric(cv);
            PaintWool(cv);
            PaintCotton(cv);
            PaintTop(cv);
            PaintWood(cv, WalnutArea, C(Hue.Walnut), 0.35f, 7);
            PaintSteel(cv);
            PaintLaminate(cv);
            PaintLeaf(cv);
            PaintFur(cv);
            PaintRack(cv);
            PaintSpines(cv);
            PaintKitchenBits(cv);
            PaintLabels(cv);
            PaintDesk(cv);
            PaintPaintings(cv, notes);
            glow = SavePicture(cv.Glow, AtlasW, AtlasH, GlowTexture, TextureWrapMode.Clamp, false);
            return SavePicture(cv.Albedo, AtlasW, AtlasH, AtlasTexture, TextureWrapMode.Clamp, true);
        }

        /// <summary>
        /// ソファの張り地。黒い革（つや消しに近い、少し艶）。細かい粒と、ところどころの浅い皺の線（明るい筋と暗い筋が並ぶ）。
        /// 黒い部屋で形が消えないよう、地は真っ黒にせず、皺と粒の明るさで面の向きの違いが読めるようにする
        /// </summary>
        static void PaintFabric(Canvas cv)
        {
            var r = FabricArea;
            var baseC = C(Hue.Fabric);
            var px = new float[r.width * r.height];
            var gloss = new float[r.width * r.height];
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    px[y * r.width + x] = 0.92f + 0.14f * TileNoise(x, y, r.width, 8, 11) + 0.10f * (Hash(x, y + 3) - 0.5f);
                    gloss[y * r.width + x] = 0.42f;
                }
            // 浅い皺（2〜4 画素の斜めの線。明るい筋のすぐ脇に暗い筋）
            for (var k = 0; k < 26; k++)
            {
                var sx = Mathf.FloorToInt(Hash(k, 31) * r.width);
                var sy = Mathf.FloorToInt(Hash(k, 32) * r.height);
                var len = 3 + Mathf.FloorToInt(Hash(k, 33) * 4f);
                var dx = Hash(k, 34) > 0.5f ? 1 : -1;
                for (var i = 0; i < len; i++)
                {
                    var x = ((sx + i * dx) % r.width + r.width) % r.width;
                    var y = ((sy + i / 2) % r.height + r.height) % r.height;
                    var y2 = (y + 1) % r.height;
                    px[y * r.width + x] += 0.45f;
                    px[y2 * r.width + x] *= 0.7f;
                    gloss[y * r.width + x] = 0.55f;
                }
            }
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                    cv.Put(r.x + x, r.y + y, baseC * px[y * r.width + x], gloss[y * r.width + x]);
        }

        /// <summary>毛布。くすんだ葡萄色の編み目（縦に並ぶ V の目）</summary>
        static void PaintWool(Canvas cv)
        {
            var r = WoolArea;
            var baseC = C(Hue.Wool);
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var col = x % 4;
                    var row = y % 4;
                    // V の目: 列の真ん中へ向かって一段ずつ下がる
                    var v = Mathf.Abs(col - 1.5f) - (row * 0.5f);
                    var stitch = v < 0.6f && v > -0.9f ? 1.12f : 0.84f;
                    if (col == 0 && row == 3) stitch = 0.7f;
                    var n = 0.92f + 0.14f * TileNoise(x, y, r.width, 8, 23);
                    cv.Put(r.x + x, r.y + y, baseC * (stitch * n), 0.05f);
                }
        }

        /// <summary>枕の綿。くすんだ白に細い縞（刻み目）</summary>
        static void PaintCotton(Canvas cv)
        {
            var r = CottonArea;
            var baseC = C(Hue.Cotton);
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var stripe = x % 8 == 3 ? new Color(0.40f, 0.44f, 0.52f) : baseC;
                    var n = 0.92f + 0.10f * TileNoise(x, y, r.width, 8, 5) + 0.04f * Hash(x + 7, y);
                    cv.Put(r.x + x, r.y + y, stripe * n, 0.10f);
                }
        }

        /// <summary>天板。艶のある黒い石（人造の石英）。ごく細かい明るい粒をまばらに</summary>
        static void PaintTop(Canvas cv)
        {
            var r = TopArea;
            var baseC = C(Hue.Oak);
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var n = 0.9f + 0.2f * TileNoise(x, y, r.width, 16, 13);
                    var c = baseC * n;
                    var fleck = Hash(x + 5, y + 9);
                    if (fleck > 0.992f) c = new Color(0.13f, 0.13f, 0.14f);
                    else if (fleck > 0.975f) c = new Color(0.07f, 0.07f, 0.075f);
                    cv.Put(r.x + x, r.y + y, c, 0.80f);
                }
        }

        /// <summary>木目。u に沿って走る年輪の線と、ところどころの節</summary>
        static void PaintWood(Canvas cv, RectInt r, Color baseC, float smooth, int seed)
        {
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var warp = 3.5f * (TileNoise(x, y, r.width, 16, seed) - 0.5f);
                    var ring = Mathf.Repeat(y + warp + 0.3f * Mathf.Sin(x * Mathf.PI * 2f / r.width) * 2f, 5f);
                    var line = ring < 1f ? 0.78f : 1f;
                    var n = 0.93f + 0.10f * Hash(x / 3, y + seed);
                    cv.Put(r.x + x, r.y + y, baseC * (line * n), smooth);
                }
        }

        /// <summary>黒いステンレス（ヘアライン）。横に流れる筋と、指の跡のような曇り</summary>
        static void PaintSteel(Canvas cv)
        {
            var r = SteelArea;
            var baseC = new Color(0.105f, 0.105f, 0.115f);
            for (var y = 0; y < r.height; y++)
            {
                var row = 0.95f + 0.08f * Hash(3, y);
                for (var x = 0; x < r.width; x++)
                {
                    var streak = 0.97f + 0.05f * Hash(x / 6, y);
                    var smudge = TileNoise(x, y, r.width, 16, 41) > 0.72f ? 0.9f : 1f;
                    cv.Put(r.x + x, r.y + y, baseC * (row * streak * smudge * (smudge < 1f ? 1.25f : 1f)), smudge < 1f ? 0.45f : 0.62f);
                }
            }
        }

        /// <summary>戸と箱と棚。つや消しの黒の塗り（台所・作業台・本棚・靴置き・棚板）</summary>
        static void PaintLaminate(Canvas cv)
        {
            var r = LaminateArea;
            var baseC = C(Hue.Laminate);
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var n = 0.95f + 0.07f * TileNoise(x, y, r.width, 8, 57) + 0.03f * Hash(x, y + 9);
                    cv.Put(r.x + x, r.y + y, baseC * n, 0.22f);
                }
        }

        /// <summary>葉（u が横切り、真ん中が主脈。v が付け根から先）。濃い緑に明るい主脈と、先へ斜めに走る側脈、暗い縁</summary>
        static void PaintLeaf(Canvas cv)
        {
            var r = LeafArea;
            var leaf = new Color(0.085f, 0.20f, 0.075f);
            var vein = new Color(0.22f, 0.34f, 0.14f);
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var u = (x + 0.5f) / r.width - 0.5f;
                    var v = (y + 0.5f) / r.height;
                    var c = leaf * (0.9f + 0.2f * TileNoise(x, y, r.width, 8, 77));
                    // 付け根の方が明るく、先へ暗く
                    c *= 1.1f - 0.25f * v;
                    var side = Mathf.Abs(u) * 2f;
                    var lateral = Mathf.Repeat(v * 7f - side * 1.6f, 1f);
                    if (lateral < 0.12f && side > 0.08f && side < 0.85f) c = Color.Lerp(c, vein, 0.45f);
                    if (Mathf.Abs(u) < 0.04f) c = vein;
                    if (side > 0.9f) c *= 0.7f;
                    cv.Put(r.x + x, r.y + y, c, 0.45f);
                }
        }

        /// <summary>ラグの毛足。灰に藤の混じった地に、毛先の明るい房と、根元の暗い隙間。房の向きはばらばら</summary>
        static void PaintFur(Canvas cv)
        {
            var r = FurArea;
            var baseC = new Color(0.20f, 0.19f, 0.21f);
            var px = new float[r.width * r.height];
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var clump = TileNoise(x, y, r.width, 4, 3);
                    var big = TileNoise(x, y, r.width, 16, 9);
                    px[y * r.width + x] = 0.55f + 0.45f * clump + 0.25f * (big - 0.5f);
                }
            // 毛の筋。2〜3 画素の短い線を明るく足す（升の端は折り返す）
            for (var k = 0; k < 520; k++)
            {
                var sx = Mathf.FloorToInt(Hash(k, 1) * r.width);
                var sy = Mathf.FloorToInt(Hash(k, 2) * r.height);
                var dir = Mathf.FloorToInt(Hash(k, 3) * 4f);
                var len = 2 + Mathf.FloorToInt(Hash(k, 4) * 2f);
                var lift = 0.10f + 0.14f * Hash(k, 5);
                for (var i = 0; i < len; i++)
                {
                    var x = sx + (dir == 0 ? i : dir == 1 ? -i : dir == 2 ? i : 0);
                    var y = sy + (dir == 0 ? 0 : dir == 1 ? i : dir == 2 ? i : i);
                    x = ((x % r.width) + r.width) % r.width;
                    y = ((y % r.height) + r.height) % r.height;
                    px[y * r.width + x] += lift * (i == len - 1 ? 1.15f : 1f);
                }
            }
            for (var k = 0; k < 260; k++)
            {
                var x = Mathf.FloorToInt(Hash(k, 11) * r.width);
                var y = Mathf.FloorToInt(Hash(k, 12) * r.height);
                px[y * r.width + x] *= 0.7f;
            }
            for (var y = 0; y < r.height; y++)
                for (var x = 0; x < r.width; x++)
                {
                    var v = px[y * r.width + x];
                    var tint = Color.Lerp(baseC, new Color(0.27f, 0.25f, 0.29f), Mathf.Clamp01(v - 0.9f));
                    cv.Put(r.x + x, r.y + y, tint * v, 0.02f);
                }
        }

        /// <summary>
        /// 棚に差す機械の顔（横 64 画素が 19 インチの幅）。下から UPS（2U、液晶と釦）、サーバ A（2U、ドライブの差し口 8 つ）、サーバ B（2U、差し口 6 つと取っ手）、
        /// 1U のサーバ、スイッチ（口の列）、パッチパネル（色の付いた口）、目隠し、ストレージ（2U、差し口 12）、側板の通気の孔
        /// </summary>
        static void PaintRack(Canvas cv)
        {
            var r = RackArea;
            var black = C(Hue.RackBlack);
            var dark = new Color(0.08f, 0.08f, 0.09f);
            var grey = new Color(0.20f, 0.20f, 0.22f);
            var silver = new Color(0.36f, 0.37f, 0.40f);
            cv.Fill(r, black, 0.35f);
            // UPS（y 0〜7）
            cv.Fill(r.x, r.y, 64, 8, grey * 0.7f, 0.3f);
            for (var x = 4; x < 60; x += 2) cv.Put(r.x + x, r.y + 1, dark, 0.2f);
            cv.Fill(r.x + 40, r.y + 3, 14, 4, new Color(0.02f, 0.03f, 0.03f), 0.8f);
            cv.LitFill(r.x + 41, r.y + 4, 8, 1, new Color(0.35f, 0.85f, 0.75f), 0.7f);
            cv.LitFill(r.x + 41, r.y + 5, 12, 1, new Color(0.35f, 0.85f, 0.75f), 0.5f);
            cv.Lit(r.x + 56, r.y + 5, C(Hue.LedGreen));
            cv.Fill(r.x + 57, r.y + 3, 2, 1, silver, 0.4f);
            // サーバ A（y 8〜15）
            Server(cv, r.x, r.y + 8, 8, 8, 6, silver, dark);
            // サーバ B（y 16〜23）
            Server(cv, r.x, r.y + 16, 8, 6, 8, silver, dark);
            // 1U（y 24〜27）
            for (var x = 6; x < 30; x += 6) { cv.Fill(r.x + x, r.y + 25, 5, 2, dark, 0.3f); cv.Put(r.x + x + 4, r.y + 26, silver, 0.4f); }
            for (var x = 34; x < 60; x += 2) cv.Put(r.x + x, r.y + 25, dark, 0.2f);
            cv.Lit(r.x + 3, r.y + 26, C(Hue.LedBlue), 0.8f);
            // スイッチ（y 28〜31）。口の列（点滅する灯りは別の面）
            cv.Fill(r.x, r.y + 28, 64, 4, dark, 0.3f);
            for (var x = 4; x < 56; x += 2) { cv.Put(r.x + x, r.y + 29, black, 0.2f); cv.Put(r.x + x, r.y + 30, black, 0.2f); }
            cv.Lit(r.x + 59, r.y + 30, C(Hue.LedGreen), 0.6f);
            // パッチパネル（y 32〜35）。口に差した線の色
            var cables = new[] { Hue.CableBlue, Hue.CableYellow, Hue.CableGrey, Hue.CableBlue, Hue.CableRed, Hue.CableGrey, Hue.CableBlue };
            for (var k = 0; k < 24; k++)
            {
                var x = r.x + 4 + k * 2 + (k >= 12 ? 4 : 0);
                cv.Put(x, r.y + 33, black, 0.2f);
                if (Hash(k, 5) > 0.35f) cv.Put(x, r.y + 34, C(cables[k % cables.Length]), 0.4f);
                cv.Put(x, r.y + 32, Hash(k, 6) > 0.5f ? new Color(0.5f, 0.5f, 0.5f) : dark, 0.2f);
            }
            // 目隠し（y 36〜39）。刷毛の口
            cv.Fill(r.x, r.y + 36, 64, 4, black, 0.3f);
            for (var x = 6; x < 58; x++) cv.Put(r.x + x, r.y + 37, x % 2 == 0 ? dark : black, 0.1f);
            // ストレージ（y 40〜47）
            Server(cv, r.x, r.y + 40, 12, 4, 4, silver, dark);
            // 側板の孔（y 48〜63）
            for (var y = 48; y < 64; y++)
                for (var x = 0; x < 64; x++)
                    cv.Put(r.x + x, r.y + y, (x % 3 == 1 && y % 3 == 1) ? new Color(0.01f, 0.01f, 0.01f) : black * 1.4f, 0.35f);
        }

        /// <summary>2U の機械。bays 個の差し口（幅 bayW、高さ 3）に小さな灯り、左右の耳</summary>
        static void Server(Canvas cv, int x0, int y0, int bays, int bayW, int gap, Color silver, Color dark)
        {
            cv.Fill(x0, y0, 64, 8, new Color(0.13f, 0.13f, 0.15f), 0.35f);
            cv.Fill(x0, y0, 3, 8, silver * 0.8f, 0.45f);
            cv.Fill(x0 + 61, y0, 3, 8, silver * 0.8f, 0.45f);
            var span = bays * bayW;
            var start = x0 + 4 + Mathf.Max(0, (56 - span) / 2);
            for (var k = 0; k < bays; k++)
            {
                var bx = start + k * bayW;
                if (bx + bayW > x0 + 60) break;
                cv.Fill(bx, y0 + 2, bayW - 1, 4, dark, 0.3f);
                cv.Put(bx, y0 + 5, silver, 0.4f);
                if (Hash(bx, y0) > 0.3f) cv.Lit(bx + bayW - 2, y0 + 5, Hash(bx, y0 + 1) > 0.8f ? C(Hue.LedAmber) : C(Hue.LedGreen), 0.7f);
            }
            cv.Lit(x0 + 5, y0 + 6, C(Hue.LedBlue), 0.9f);
            if (gap > 0) for (var x = x0 + 4; x < x0 + 60; x += 2) cv.Put(x, y0 + 7, dark, 0.2f);
        }

        /// <summary>背表紙（4×32 を 32 種）。地の色、上と下の帯、真ん中の題の字の線、下の著者、版元の印</summary>
        static void PaintSpines(Canvas cv)
        {
            var hues = new[]
            {
                new Color(0.42f, 0.10f, 0.09f), new Color(0.09f, 0.13f, 0.28f), new Color(0.12f, 0.24f, 0.15f), new Color(0.55f, 0.42f, 0.18f),
                new Color(0.06f, 0.06f, 0.07f), new Color(0.66f, 0.62f, 0.52f), new Color(0.32f, 0.33f, 0.35f), new Color(0.30f, 0.18f, 0.10f),
                new Color(0.10f, 0.30f, 0.32f), new Color(0.35f, 0.08f, 0.16f), new Color(0.32f, 0.34f, 0.14f), new Color(0.72f, 0.70f, 0.64f),
                new Color(0.55f, 0.24f, 0.10f), new Color(0.18f, 0.12f, 0.26f), new Color(0.46f, 0.46f, 0.40f), new Color(0.14f, 0.18f, 0.20f),
            };
            var ink = new[] { new Color(0.80f, 0.74f, 0.52f), new Color(0.78f, 0.78f, 0.74f), new Color(0.05f, 0.05f, 0.05f) };
            for (var k = 0; k < 32; k++)
            {
                var x0 = SpineArea.x + (k % 16) * 4;
                var y0 = SpineArea.y + (k / 16) * 32;
                var baseC = hues[(k * 7 + k / 16 * 3) % hues.Length];
                var light = baseC.grayscale > 0.4f;
                var text = light ? ink[2] : ink[Hash(k, 3) > 0.5f ? 0 : 1];
                var gloss = Hash(k, 9) > 0.6f ? 0.45f : 0.15f;
                for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 4; x++)
                    {
                        var c = baseC * (0.93f + 0.1f * Hash(x0 + x, y0 + y));
                        // 縁の丸み（背の両脇を暗く）
                        if (x == 0 || x == 3) c *= 0.82f;
                        cv.Put(x0 + x, y0 + y, c, gloss);
                    }
                var style = k % 4;
                // 帯
                if (style == 0 || style == 2)
                {
                    for (var x = 0; x < 4; x++) { cv.Put(x0 + x, y0 + 28, ink[0], 0.5f); cv.Put(x0 + x, y0 + 3, ink[0], 0.5f); }
                    if (style == 2) for (var x = 0; x < 4; x++) cv.Put(x0 + x, y0 + 26, ink[0], 0.5f);
                }
                if (style == 1)
                {
                    var band = hues[(k * 5 + 3) % hues.Length];
                    for (var y = 22; y < 27; y++) for (var x = 0; x < 4; x++) cv.Put(x0 + x, y0 + y, band, 0.2f);
                }
                if (style == 3) for (var y = 0; y < 5; y++) for (var x = 0; x < 4; x++) cv.Put(x0 + x, y0 + y, ink[2] * 0.6f + baseC * 0.4f, 0.2f);
                // 題（縦の字の線）
                var top = 24 - Mathf.FloorToInt(Hash(k, 1) * 4f);
                var len = 7 + Mathf.FloorToInt(Hash(k, 2) * 8f);
                for (var y = top - len; y < top; y++)
                    if (Hash(k * 3, y) > 0.28f) cv.Put(x0 + 1 + (Hash(k, y) > 0.7f ? 1 : 0), y0 + y, text, 0.3f);
                // 著者と印
                for (var y = 6; y < 9; y++) cv.Put(x0 + 2, y0 + y, text * 0.8f + baseC * 0.2f, 0.3f);
                cv.Put(x0 + 1, y0 + 1, text, 0.3f);
            }
            // 頁の小口（紙の束の縞）
            var p = PagesArea;
            for (var y = 0; y < p.height; y++)
                for (var x = 0; x < p.width; x++)
                    cv.Put(p.x + x, p.y + y, C(Hue.PageEdge) * ((y % 2 == 0) ? 1.04f : 0.92f) * (0.96f + 0.06f * Hash(x, y)), 0.1f);
        }

        /// <summary>台所の顔: コンロの天板、換気扇の網、冷蔵庫の下の吸気の格子と表示、電子レンジの戸と操作の盤、コーヒーメーカーの顔、ケトルの釦</summary>
        static void PaintKitchenBits(Canvas cv)
        {
            // コンロ（IH の黒い硝子に、二つの輪と手前の操作の印）
            var h = HobArea;
            for (var y = 0; y < h.height; y++)
                for (var x = 0; x < h.width; x++)
                {
                    var c = C(Hue.GlassDark) * (0.9f + 0.2f * Hash(x, y + 5));
                    foreach (var ring in new[] { new Vector3(10.5f, 18.5f, 7.5f), new Vector3(22.5f, 14.5f, 6f) })
                    {
                        var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(ring.x, ring.y));
                        if (Mathf.Abs(d - ring.z) < 0.6f) c = new Color(0.20f, 0.20f, 0.22f);
                        if (Mathf.Abs(d - ring.z * 0.55f) < 0.5f) c = new Color(0.12f, 0.12f, 0.13f);
                    }
                    cv.Put(h.x + x, h.y + y, c, 0.85f);
                }
            // 操作の印は手前（天板に貼ると v が前へ増えるので、絵の上）
            for (var x = 8; x < 24; x += 3) cv.Put(h.x + x, h.y + 29, new Color(0.30f, 0.30f, 0.32f), 0.85f);
            cv.Lit(h.x + 25, h.y + 29, C(Hue.LedRed), 0.4f);
            // 吸気の格子
            var g = GrilleArea;
            for (var y = 0; y < g.height; y++)
                for (var x = 0; x < g.width; x++)
                    cv.Put(g.x + x, g.y + y, (y % 2 == 1 && x > 0 && x < g.width - 1) ? new Color(0.02f, 0.02f, 0.02f) : C(Hue.SteelDark), 0.3f);
            // 換気扇の網（真ん中に明かり）
            var f = FilterArea;
            for (var y = 0; y < f.height; y++)
                for (var x = 0; x < f.width; x++)
                    cv.Put(f.x + x, f.y + y, ((x + y) % 2 == 0) ? new Color(0.28f, 0.28f, 0.30f) : new Color(0.10f, 0.10f, 0.11f), 0.5f);
            cv.LitFill(f.x + 6, f.y + 1, 4, 2, new Color(1.0f, 0.86f, 0.62f), 0.55f);
            // 冷蔵庫の表示（黒い硝子に水色の 3 の字と棒）
            var d0 = FridgeDisplayArea;
            cv.Fill(d0, C(Hue.GlassDark), 0.85f);
            cv.LitFill(d0.x + 1, d0.y + 3, 2, 1, C(Hue.LedCyan), 0.9f);
            cv.Lit(d0.x + 2, d0.y + 2, C(Hue.LedCyan), 0.9f);
            cv.LitFill(d0.x + 1, d0.y + 1, 2, 1, C(Hue.LedCyan), 0.9f);
            cv.Lit(d0.x + 4, d0.y + 3, C(Hue.LedCyan), 0.6f);
            cv.LitFill(d0.x + 5, d0.y + 1, 2, 1, C(Hue.LedGreen), 0.4f);
            // ケトルの釦（青い灯り）
            var k0 = KettleSwitchArea;
            cv.Fill(k0, C(Hue.PlasticDark), 0.4f);
            cv.LitFill(k0.x + 1, k0.y + 1, 4, 4, C(Hue.LedBlue), 0.8f);
            // 電子レンジの戸（黒い硝子に点の網）
            var m = MicrowaveDoorArea;
            cv.Fill(m, C(Hue.PlasticDark), 0.4f);
            for (var y = 2; y < m.height - 2; y++)
                for (var x = 2; x < m.width - 2; x++)
                    cv.Put(m.x + x, m.y + y, ((x + y) % 2 == 0) ? new Color(0.07f, 0.07f, 0.08f) : C(Hue.GlassDark), 0.8f);
            // 電子レンジの操作の盤（緑の時計と釦）
            var mp = MicrowavePanelArea;
            cv.Fill(mp, C(Hue.PlasticDark), 0.35f);
            cv.Fill(mp.x + 1, mp.y + 10, 6, 3, C(Hue.GlassDark), 0.8f);
            foreach (var x in new[] { 1, 2, 4, 5 }) cv.Lit(mp.x + x, mp.y + 11, C(Hue.LedGreen), 0.8f);
            cv.Lit(mp.x + 3, mp.y + 12, C(Hue.LedGreen), 0.5f);
            for (var y = 2; y < 9; y += 2) for (var x = 1; x < 7; x += 2) cv.Put(mp.x + x, mp.y + y, new Color(0.26f, 0.26f, 0.28f), 0.3f);
            // コーヒーメーカーの顔（赤い電源の灯りと釦）
            var cf = CoffeeFaceArea;
            cv.Fill(cf, C(Hue.PlasticDark), 0.35f);
            cv.Fill(cf.x + 3, cf.y + 5, 6, 3, new Color(0.22f, 0.22f, 0.24f), 0.35f);
            cv.Lit(cf.x + 9, cf.y + 6, C(Hue.LedRed), 0.8f);
            cv.Fill(cf.x + 2, cf.y + 10, 8, 1, new Color(0.30f, 0.30f, 0.32f), 0.4f);
            // エアコンの顔（くすんだ白の板と継ぎ目、右に青い数字と緑の灯り）
            var ac = AcFrontArea;
            for (var y = 0; y < ac.height; y++)
                for (var x = 0; x < ac.width; x++)
                    cv.Put(ac.x + x, ac.y + y, C(Hue.AcWhite) * (0.95f + 0.06f * Hash(x / 2, y) - (y == 6 ? 0.1f : 0f)), 0.3f);
            cv.Fill(ac.x + 30, ac.y + 2, 7, 3, new Color(0.30f, 0.31f, 0.32f), 0.6f);
            foreach (var x in new[] { 31, 32, 34, 35 }) cv.Lit(ac.x + x, ac.y + 3, new Color(0.35f, 0.65f, 1.0f), 0.6f);
            cv.Lit(ac.x + 36, ac.y + 3, C(Hue.LedGreen), 0.6f);
        }

        /// <summary>札と紙: 冷蔵庫のメモとテイクアウトのメニュー、雑誌の表紙、紅茶の箱、小袋、瓶の札、茶と珈琲の缶、チップの札、ケースの背、抽斗の札、箱の札</summary>
        static void PaintLabels(Canvas cv)
        {
            var paper = C(Hue.Paper);
            var inkBlue = new Color(0.12f, 0.14f, 0.26f);
            var inkBlack = new Color(0.06f, 0.06f, 0.07f);
            var red = new Color(0.60f, 0.10f, 0.08f);
            // メモ（ちぎった上の縁、走り書き 6 行、赤の下線）
            var mo = MemoArea;
            for (var y = 0; y < mo.height; y++)
                for (var x = 0; x < mo.width; x++)
                {
                    var torn = y == mo.height - 1 && Hash(x, 7) > 0.5f;
                    cv.Put(mo.x + x, mo.y + y, torn ? C(Hue.SteelDark) : paper * (0.95f + 0.06f * Hash(x, y)), 0.1f);
                }
            for (var k = 0; k < 6; k++) cv.Scribble(mo.x + 1, mo.y + 13 - k * 2, 7 + Mathf.FloorToInt(Hash(k, 3) * 4f), inkBlue, 0.1f, k);
            cv.Fill(mo.x + 1, mo.y + 6, 6, 1, red, 0.1f);
            // テイクアウトのメニュー（赤い見出し、二列の品と値段）
            var me = MenuArea;
            cv.Fill(me, new Color(0.74f, 0.72f, 0.66f), 0.1f);
            cv.Fill(me.x, me.y + 12, me.width, 4, red, 0.15f);
            cv.Scribble(me.x + 2, me.y + 13, 8, new Color(0.9f, 0.85f, 0.6f), 0.15f, 21);
            for (var k = 0; k < 5; k++)
            {
                cv.Scribble(me.x + 1, me.y + 10 - k * 2, 6, inkBlack, 0.1f, 30 + k);
                cv.Put(me.x + 9, me.y + 10 - k * 2, inkBlack, 0.1f);
                cv.Put(me.x + 10, me.y + 10 - k * 2, inkBlack, 0.1f);
            }
            cv.Fill(me.x + 1, me.y, 3, 1, red, 0.1f);
            // 雑誌の表紙二枚（大きな写真の升と題字）
            var mg = MagazineArea;
            var photo = new[] { new Color(0.20f, 0.28f, 0.36f), new Color(0.40f, 0.22f, 0.16f) };
            for (var k = 0; k < 2; k++)
            {
                var x0 = mg.x + k * 12;
                cv.Fill(x0, mg.y, 12, 16, new Color(0.70f, 0.68f, 0.62f), 0.45f);
                for (var y = 2; y < 12; y++)
                    for (var x = 1; x < 11; x++)
                        cv.Put(x0 + x, mg.y + y, photo[k] * (0.7f + 0.6f * TileNoise(x + k * 5, y, 16, 4, 60 + k)), 0.45f);
                cv.Fill(x0 + 1, mg.y + 13, 10, 2, k == 0 ? red : inkBlack, 0.45f);
                cv.Scribble(x0 + 1, mg.y + 1, 8, inkBlack, 0.45f, 40 + k);
            }
            // 紅茶の箱（赤地に白の字と緑の帯）
            var tb = TeaBoxArea;
            cv.Fill(tb, new Color(0.46f, 0.08f, 0.07f), 0.3f);
            cv.Fill(tb.x, tb.y + 1, tb.width, 2, new Color(0.10f, 0.26f, 0.14f), 0.3f);
            cv.Scribble(tb.x + 2, tb.y + 6, 12, new Color(0.85f, 0.82f, 0.74f), 0.3f, 50);
            cv.Scribble(tb.x + 4, tb.y + 4, 8, new Color(0.85f, 0.82f, 0.74f), 0.3f, 51);
            // 小袋三つ（香辛料。色の違う印と、上の閉じ目）
            var sa = SachetArea;
            var sachet = new[] { new Color(0.55f, 0.18f, 0.08f), new Color(0.62f, 0.48f, 0.10f), new Color(0.22f, 0.34f, 0.14f) };
            for (var k = 0; k < 3; k++)
            {
                var x0 = sa.x + k * 6;
                cv.Fill(x0, sa.y, 6, 8, new Color(0.60f, 0.58f, 0.54f), 0.55f);
                cv.Fill(x0 + 1, sa.y + 2, 4, 3, sachet[k], 0.3f);
                cv.Fill(x0, sa.y + 7, 6, 1, new Color(0.40f, 0.40f, 0.42f), 0.55f);
                cv.Put(x0 + 2, sa.y + 5, inkBlack, 0.3f);
            }
            // 瓶の札（8×8 を 10 種: 塩・胡椒・パプリカ・ターメリック・香草・クミン・茶色のソース・ケチャップ・醤油・油）
            var jl = JarLabelArea;
            var labels = new[]
            {
                new[] { new Color(0.78f, 0.78f, 0.76f), new Color(0.12f, 0.24f, 0.52f) }, new[] { new Color(0.08f, 0.08f, 0.08f), new Color(0.78f, 0.78f, 0.74f) },
                new[] { new Color(0.70f, 0.66f, 0.56f), new Color(0.55f, 0.14f, 0.06f) }, new[] { new Color(0.70f, 0.66f, 0.56f), new Color(0.72f, 0.52f, 0.08f) },
                new[] { new Color(0.52f, 0.40f, 0.26f), new Color(0.22f, 0.34f, 0.12f) }, new[] { new Color(0.52f, 0.40f, 0.26f), new Color(0.40f, 0.24f, 0.10f) },
                new[] { new Color(0.10f, 0.14f, 0.30f), new Color(0.72f, 0.58f, 0.24f) }, new[] { new Color(0.74f, 0.72f, 0.66f), new Color(0.60f, 0.08f, 0.06f) },
                new[] { new Color(0.50f, 0.08f, 0.06f), new Color(0.05f, 0.05f, 0.05f) }, new[] { new Color(0.16f, 0.30f, 0.12f), new Color(0.72f, 0.66f, 0.30f) },
            };
            for (var k = 0; k < labels.Length; k++)
            {
                var x0 = jl.x + k * 8;
                cv.Fill(x0, jl.y, 8, 8, labels[k][0], 0.3f);
                cv.Fill(x0 + 2, jl.y + 3, 4, 3, labels[k][1], 0.3f);
                cv.Scribble(x0 + 1, jl.y + 1, 6, labels[k][1] * 0.8f, 0.3f, k + 70);
            }
            // 茶の缶（紺に金の帯と白い札）と珈琲の缶（黒に橙の帯）
            var tt = TeaTinArea;
            cv.Fill(tt, C(Hue.TinNavy), 0.6f);
            cv.Fill(tt.x, tt.y + 1, tt.width, 1, C(Hue.TinGold), 0.6f);
            cv.Fill(tt.x, tt.y + 6, tt.width, 1, C(Hue.TinGold), 0.6f);
            cv.Fill(tt.x + 5, tt.y + 3, 6, 2, new Color(0.78f, 0.76f, 0.70f), 0.3f);
            var ct = CoffeeTinArea;
            cv.Fill(ct, new Color(0.05f, 0.05f, 0.05f), 0.5f);
            cv.Fill(ct.x, ct.y + 3, ct.width, 2, new Color(0.62f, 0.30f, 0.08f), 0.5f);
            // チップの札（8×6 を 6 種。上に色の帯、字の線二本）
            var cl = ChipLabelArea;
            var tags = new[] { C(Hue.LedCyan) * 0.7f, new Color(0.45f, 0.30f, 0.65f), C(Hue.LedAmber) * 0.8f, C(Hue.LedGreen) * 0.6f, C(Hue.LedRed) * 0.6f, new Color(0.45f, 0.45f, 0.47f) };
            for (var k = 0; k < 6; k++)
            {
                var x0 = cl.x + k * 8;
                cv.Fill(x0, cl.y, 8, 6, C(Hue.LabelWhite), 0.15f);
                cv.Fill(x0, cl.y + 4, 8, 2, tags[k], 0.2f);
                cv.Scribble(x0 + 1, cl.y + 2, 6, inkBlack, 0.15f, 80 + k);
                cv.Scribble(x0 + 1, cl.y + 1, 4, inkBlue, 0.15f, 90 + k);
            }
            // ケースの背（4×12 を 6 種。煙色のケースに札の帯）
            var cs = CaseSpineArea;
            for (var k = 0; k < 6; k++)
            {
                var x0 = cs.x + k * 4;
                cv.Fill(x0, cs.y, 4, 12, C(Hue.CaseSmoke) * 1.2f, 0.7f);
                cv.Fill(x0 + 1, cs.y + 2, 2, 8, C(Hue.LabelWhite), 0.15f);
                cv.Fill(x0 + 1, cs.y + 9, 2, 1, tags[k], 0.2f);
                for (var y = 3; y < 8; y++) if (Hash(k, y) > 0.35f) cv.Put(x0 + 1 + (y & 1), cs.y + y, inkBlack, 0.15f);
            }
            // 抽斗の札（6×4 を 4 種）
            var dl = DrawerLabelArea;
            for (var k = 0; k < 4; k++)
            {
                var x0 = dl.x + k * 6;
                cv.Fill(x0, dl.y, 6, 4, new Color(0.10f, 0.10f, 0.11f), 0.4f);
                cv.Fill(x0 + 1, dl.y + 1, 4, 2, C(Hue.LabelWhite), 0.15f);
                cv.Put(x0 + 1 + k % 3, dl.y + 2, inkBlack, 0.15f);
            }
            // 箱の札（24×8。段ボールに白い札、BLANK と書いたような字と色の帯）
            var bl = BoxLabelArea;
            for (var y = 0; y < bl.height; y++)
                for (var x = 0; x < bl.width; x++)
                    cv.Put(bl.x + x, bl.y + y, C(Hue.Cardboard) * (0.94f + 0.08f * Hash(x, y + 3)), 0.1f);
            cv.Fill(bl.x + 6, bl.y + 2, 12, 4, C(Hue.LabelWhite), 0.15f);
            cv.Scribble(bl.x + 7, bl.y + 4, 10, inkBlack, 0.15f, 99);
            cv.Fill(bl.x + 7, bl.y + 3, 3, 1, C(Hue.LedCyan) * 0.6f, 0.2f);
        }

        /// <summary>仕事の机の周りの顔: 鍵盤（黒い鍵と薄い刻印の列、長い空白の鍵）、PC の前（縦の通気の溝・紫の灯りの線・電源の輪）、PC の横の硝子（中の紫の輪の扇と灯りの帯）</summary>
        static void PaintDesk(Canvas cv)
        {
            var k = KeysArea;
            cv.Fill(k, new Color(0.10f, 0.10f, 0.11f), 0.35f);
            for (var row = 0; row < 4; row++)
                for (var col = 0; col < 10; col++)
                {
                    var x = k.x + 1 + col * 3;
                    var y = k.y + 3 + row * 2;
                    if (row == 0 && col >= 3 && col <= 6) continue;
                    cv.Put(x, y, new Color(0.24f, 0.24f, 0.26f), 0.35f);
                    cv.Put(x + 1, y, new Color(0.20f, 0.20f, 0.22f), 0.35f);
                    if (Hash(col, row + 20) > 0.55f) cv.Lit(x, y, new Color(0.50f, 0.36f, 0.80f), 0.35f);
                }
            cv.Fill(k.x + 10, k.y + 1, 12, 1, new Color(0.24f, 0.24f, 0.26f), 0.35f);
            // PC の前（下が床、上が天板の側）
            var f = PcFrontArea;
            cv.Fill(f, new Color(0.035f, 0.035f, 0.04f), 0.4f);
            for (var y = f.y + 2; y < f.yMax - 8; y++)
                for (var x = f.x + 2; x < f.xMax - 2; x += 2)
                    cv.Put(x, y, new Color(0.012f, 0.012f, 0.014f), 0.2f);
            for (var y = f.y + 1; y < f.yMax - 1; y++) cv.Lit(f.x, y, C(Hue.LedViolet), 0.55f);
            var ring = new Vector2(f.x + 6f, f.yMax - 4f);
            for (var y = f.yMax - 7; y < f.yMax - 1; y++)
                for (var x = f.x + 3; x < f.x + 9; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), ring);
                    if (d > 1.2f && d < 2.3f) cv.Lit(x, y, C(Hue.LedCyan), 0.7f);
                }
            cv.Put(f.x + 3, f.yMax - 10, new Color(0.25f, 0.25f, 0.27f), 0.4f);
            cv.Put(f.x + 5, f.yMax - 10, new Color(0.25f, 0.25f, 0.27f), 0.4f);
            // PC の横の硝子。暗い硝子の奥に、紫に灯る扇の輪が二つと、下の灯りの帯
            var g = PcSideArea;
            for (var y = g.y; y < g.yMax; y++)
                for (var x = g.x; x < g.xMax; x++)
                    cv.Put(x, y, C(Hue.GlassDark) * (1f + 0.4f * TileNoise(x, y, 32, 8, 88)), 0.9f);
            foreach (var c in new[] { new Vector2(g.x + 16f, g.y + 21f), new Vector2(g.x + 16f, g.y + 12f) })
                for (var y = g.y; y < g.yMax; y++)
                    for (var x = g.x; x < g.xMax; x++)
                    {
                        var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        if (d > 3.0f && d < 4.2f) cv.Lit(x, y, C(Hue.LedViolet), 0.55f);
                        else if (d < 3.0f && (Mathf.FloorToInt(Mathf.Atan2(y + 0.5f - c.y, x + 0.5f - c.x) * 3f) & 1) == 0) cv.Put(x, y, new Color(0.08f, 0.07f, 0.10f), 0.6f);
                    }
            for (var x = g.x + 2; x < g.xMax - 2; x++) cv.Lit(x, g.y + 2, C(Hue.LedViolet), 0.35f);
            cv.Fill(g.x + 3, g.y + 5, 8, 3, new Color(0.10f, 0.10f, 0.11f), 0.5f);
        }

        // ---- 額の絵 ------------------------------------------------------------------

        /// <summary>額に入れる絵。名前（Paintings/ の下の絵の名）・縦横の比（絵が無い時の額の形）・長い辺の実寸（m）</summary>
        public struct Picture
        {
            public string Name;
            public float Aspect;
            public float Long;

            public Picture(string name, float aspect, float longSide)
            {
                Name = name;
                Aspect = aspect;
                Long = longSide;
            }
        }

        /// <summary>
        /// 四枚。ミレー『春』（オルセー美術館）、ターナー『戦艦テメレール号』（ナショナル・ギャラリー）、
        /// グリス『開いた窓の前の静物（ラヴィニャン広場）』（フィラデルフィア美術館）、ハマスホイ『陽光の中で踊る塵』（オードロップゴー美術館）。
        /// 比は元の絵の幅 ÷ 高さ（縮めた絵があればそちらの画素から取り直す）
        /// </summary>
        public static readonly Picture[] Pictures =
        {
            new Picture("MilletSpring", 1090f / 850f, 0.36f),
            new Picture("TurnerTemeraire", 2000f / 1413f, 0.64f),
            new Picture("GrisPlaceRavignan", 960f / 1267f, 0.46f),
            new Picture("HammershoiDustMotes", 1609f / 1890f, 0.46f),
        };

        public const string PaintingDir = "Assets/Textures/Furniture/Paintings/";

        /// <summary>絵ごとの uv（アトラスの中）と、縦横の比</summary>
        static readonly Rect[] PictureUv = new Rect[4];
        static readonly float[] PictureAspect = new float[4];

        /// <summary>縮めた絵をアトラスの右の半分の升へ写す。無い絵は下塗りの布（生成りの灰）にし、額は元の比で作っておく</summary>
        static void PaintPaintings(Canvas cv, List<string> notes)
        {
            for (var k = 0; k < Pictures.Length; k++)
            {
                var slot = PaintingSlots[k];
                var path = PaintingDir + Pictures[k].Name + ".png";
                var full = Path.Combine(Directory.GetCurrentDirectory(), path);
                Texture2D src = null;
                if (File.Exists(full))
                {
                    src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    if (!src.LoadImage(File.ReadAllBytes(full))) { Object.DestroyImmediate(src); src = null; }
                }
                int w, h;
                if (src != null)
                {
                    var scale = Mathf.Min((float)slot.width / src.width, (float)slot.height / src.height, 1f);
                    w = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
                    h = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var c = src.GetPixel(Mathf.Min(src.width - 1, Mathf.FloorToInt((x + 0.5f) * src.width / w)), Mathf.Min(src.height - 1, Mathf.FloorToInt((y + 0.5f) * src.height / h)));
                            cv.Put(slot.x + x, slot.y + y, c, 0.22f);
                        }
                    PictureAspect[k] = (float)src.width / src.height;
                    notes.Add(string.Format("絵 {0}: {1}×{2} を {3}×{4} で入れた", Pictures[k].Name, src.width, src.height, w, h));
                    Object.DestroyImmediate(src);
                }
                else
                {
                    var a = Pictures[k].Aspect;
                    w = a >= 1f ? slot.width : Mathf.RoundToInt(slot.height * a);
                    h = a >= 1f ? Mathf.RoundToInt(slot.width / a) : slot.height;
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                        {
                            var weave = ((x + y) % 2 == 0 ? 1.03f : 0.97f) * (0.95f + 0.08f * Hash(x, y + k * 50));
                            cv.Put(slot.x + x, slot.y + y, C(Hue.Canvas) * weave, 0.1f);
                        }
                    PictureAspect[k] = a;
                    notes.Add(string.Format("絵 {0}: 縮めた絵（{1}）が無いので、下塗りの布で額だけ作った", Pictures[k].Name, path));
                }
                PictureUv[k] = Uv(new RectInt(slot.x, slot.y, w, h));
            }
        }

        // ---- 点滅の絵 ------------------------------------------------------------------

        /// <summary>
        /// 棚の灯りの点滅の絵（16×16、繰り返す）。横が灯りの組（列）、縦が時の段（行）。灯りの面は列の真ん中の uv を持ち、
        /// <see cref="RackLights"/> が縦のずれを段ごとに送る。列 0〜5 は通信（ばらばらに瞬く緑と琥珀）、6〜9 はゆっくりの点滅、10〜15 は点いたまま
        /// </summary>
        static Texture2D PaintBlink()
        {
            const int n = BlinkSize;
            var px = new Color[n * n];
            var colours = new[] { C(Hue.LedGreen), C(Hue.LedGreen), C(Hue.LedAmber), C(Hue.LedGreen), C(Hue.LedCyan), C(Hue.LedGreen),
                C(Hue.LedBlue), C(Hue.LedGreen), C(Hue.LedAmber), C(Hue.LedBlue), C(Hue.LedGreen), C(Hue.LedGreen), C(Hue.LedBlue), C(Hue.LedGreen), C(Hue.LedAmber), C(Hue.LedCyan) };
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    bool on;
                    if (x < 6) on = Hash(x * 13, y * 7 + 3) > 0.45f;
                    else if (x < 10) on = ((y + x * 3) / 4) % 2 == 0;
                    else on = true;
                    var c = on ? colours[x] : colours[x] * 0.06f;
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            return SavePicture(px, n, n, BlinkTexture, TextureWrapMode.Repeat, false);
        }

        public const int BlinkSize = 16;

        /// <summary>点滅の組 k（0〜15）の uv（時の段は 0 の行）</summary>
        static Vector2 BlinkUv(int k)
        {
            return new Vector2((k % BlinkSize + 0.5f) / BlinkSize, 0.5f / BlinkSize);
        }

        // ---- 置く ----------------------------------------------------------------------

        /// <summary>絵を置き（中身が変わった時だけ書く）、点で引く・mipmap 無し・圧縮無しで取り込む。alpha なら α（艶）も持つ</summary>
        static Texture2D SavePicture(Color[] px, int w, int h, string path, TextureWrapMode wrap, bool alpha)
        {
            var tex = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, false);
            tex.SetPixels(px);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var full = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            var same = File.Exists(full) && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(full), bytes);
            if (!same) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var source = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (imp != null && (imp.mipmapEnabled || imp.filterMode != FilterMode.Point || imp.wrapMode != wrap || imp.alphaSource != source
                || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None || imp.alphaIsTransparency))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = wrap;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = source;
                imp.alphaIsTransparency = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
