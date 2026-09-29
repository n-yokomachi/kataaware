using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の窓の外の景色の絵（<see cref="BuildRoomView"/>）。街並みのアトラス（夕暮れと夜の二枚。升の並びは同じ）・
    /// 空の絵（夕暮れと夜）・街灯の暈の絵を、組み立てのたびに同じ種から描き直す。
    ///
    /// **アトラスの α は三つの値だけ。** 0 は抜く所（木の葉の隙間・柵の間）、128 はふつうの面、255 は自分で光る所。
    /// 光る所は <c>HalfAware/Townscape</c> が時刻の沈めを掛けない。夜のアトラスは夕暮れの灯った窓に灯る窓を足した物
    /// （同じ窓は夕暮れに灯っていれば夜も灯る）。
    ///
    /// **粗い画面に合わせて描く。** ゲームは 320×180 で描くので、窓際に立っても 1 度が 3 画素ほどにしかならない。
    /// 家の正面の升は 5 m × 10 m を 32×64 画素（1 m が 6.4 画素。向かいの家を窓際から見たときとほぼ 1 対 1）。
    /// 最近傍で引き、mipmap を持たない（隣の升が滲まない）
    /// </summary>
    public static partial class BuildRoomView
    {
        public const string TextureDir = "Assets/Textures/RoomView";
        const string TownDuskPath = TextureDir + "/Town.png";
        const string TownNightPath = TextureDir + "/TownNight.png";
        const string SkyDuskPath = TextureDir + "/Sky.png";
        const string SkyNightPath = TextureDir + "/SkyNight.png";
        const string GlowPath = TextureDir + "/Glow.png";

        const int AtlasSize = 512;
        const int SkyWide = 2048;
        const int SkyHigh = 512;
        const int Seed = 51166;

        const byte Cut = 0;
        const byte Solid = 128;
        const byte Lit = 255;

        // ---- アトラスの升 ----------------------------------------------------

        /// <summary>家の正面。4 つの壁（黄色い煉瓦・赤い煉瓦・漆喰・煤けた煉瓦）× 戸の左右 × 灯りの違いで 24 枚</summary>
        const int Fronts = 24;
        static RectInt FrontCell(int i) { return new RectInt((i % 16) * 32, (i / 16) * 64, 32, 64); }
        /// <summary>家の裏。8 枚</summary>
        const int Backs = 8;
        static RectInt BackCell(int i) { return new RectInt((8 + i) * 32, 64, 32, 64); }
        /// <summary>集合住宅の一区画（6 m × 4 階）。0・1 は赤煉瓦の古い集合住宅、2・3 はコンクリートの公営住宅</summary>
        static RectInt FlatsCell(int i) { return new RectInt(i * 32, 128, 32, 64); }
        /// <summary>家並みの端の妻壁。2 枚</summary>
        static RectInt GableCell(int i) { return new RectInt((4 + i) * 32, 128, 32, 64); }
        /// <summary>角のパブ。南の正面と西の側面（どちらも 10 m × 10 m）</summary>
        static readonly RectInt PubSouth = new RectInt(6 * 32, 128, 64, 64);
        static readonly RectInt PubWest = new RectInt(8 * 32, 128, 64, 64);
        /// <summary>教会の身廊の壁（6 m の区画）と塔の一面</summary>
        static readonly RectInt ChurchWall = new RectInt(10 * 32, 128, 32, 64);
        static readonly RectInt ChurchTower = new RectInt(11 * 32, 128, 32, 64);
        /// <summary>赤い電話ボックスの一面と、横断歩道の灯りの柱の縞</summary>
        static readonly RectInt Phone = new RectInt(12 * 32, 128, 16, 32);
        static readonly RectInt Stripe = new RectInt(12 * 32 + 16, 128, 8, 32);
        /// <summary>屋根。0〜2 は灰色のスレート、3 は赤い瓦、4 は屋根窓、5 は天窓</summary>
        const int Roofs = 6;
        static RectInt RoofCell(int i) { return new RectInt(i * 32, 192, 32, 64); }
        static readonly RectInt Spire = new RectInt(6 * 32, 192, 32, 64);
        /// <summary>木の樹冠。3 枚</summary>
        static RectInt CanopyCell(int i) { return new RectInt(i * 64, 256, 64, 64); }
        /// <summary>鉄の柵</summary>
        static readonly RectInt Railing = new RectInt(192, 256, 32, 16);

        // ---- 名所の升（BuildRoomViewLandmarks）。夜のアトラスでは投光で照らした石や灯った窓になる ----

        /// <summary>ホワイト・タワーの一面（円い頭の窓の三段、付け柱、頭の狭間）</summary>
        static readonly RectInt WhiteTowerFace = new RectInt(0, 320, 64, 64);
        /// <summary>ホワイト・タワーの角の小塔</summary>
        static readonly RectInt TurretFace = new RectInt(64, 320, 32, 64);
        /// <summary>ロンドン塔の城壁（頭の狭間を抜く）と、壁の円い塔</summary>
        static readonly RectInt CurtainWall = new RectInt(96, 320, 64, 32);
        static readonly RectInt DrumTower = new RectInt(160, 320, 32, 32);
        /// <summary>タワーブリッジの塔の一面と、上の歩道橋の格子（抜く）</summary>
        static readonly RectInt BridgeTower = new RectInt(192, 320, 32, 64);
        static readonly RectInt BridgeWalk = new RectInt(224, 320, 64, 16);
        /// <summary>ザ・シャード・ガーキン・ウォーキートーキーのガラス</summary>
        static readonly RectInt ShardFace = new RectInt(288, 320, 32, 64);
        static readonly RectInt GherkinFace = new RectInt(320, 320, 32, 64);
        static readonly RectInt WalkieFace = new RectInt(352, 320, 32, 64);
        /// <summary>セント・ポール大聖堂のドーム（鉛の肋）・ドラム（列柱）・壁</summary>
        static readonly RectInt DomeFace = new RectInt(384, 320, 32, 32);
        static readonly RectInt DrumFace = new RectInt(416, 320, 32, 32);
        static readonly RectInt CathedralFace = new RectInt(448, 320, 32, 32);
        /// <summary>川面と護岸</summary>
        static readonly RectInt WaterFace = new RectInt(480, 320, 32, 32);
        static readonly RectInt Embankment = new RectInt(480, 352, 32, 16);
        /// <summary>カナリー・ワーフのワン・カナダ・スクエア（ステンレス）とガラスの塔、O2 の膜</summary>
        static readonly RectInt CanadaFace = new RectInt(384, 352, 32, 64);
        static readonly RectInt GlassTower = new RectInt(416, 352, 32, 64);
        static readonly RectInt O2Face = new RectInt(448, 352, 32, 32);

        /// <summary>一色の升（8×8）。下の行に並べる</summary>
        enum Sw
        {
            Road, RoadFar, Pave, Kerb, White, Yellow, Lawn, Garden, Hedge, Gravel, Iron, LampLit, Trunk, Paint, CarGlass,
            Brick, Pot, BeaconLit, FlatRoof, Stone, Area, Lead, WarmLit, Path, PhoneRed, Black, Wall,
            Gold, BridgeBlue, BridgeLamp, FloodLamp, RedLamp, TipLamp, O2Yellow, NeonPink, NeonCyan, NeonViolet, Portland,
        }

        static RectInt Swatch(Sw s)
        {
            var i = (int)s;
            return new RectInt((i % 64) * 8, 448 + (i / 64) * 8, 8, 8);
        }

        /// <summary>升の uv。最近傍で引くので、縁は半画素より少し内へ寄せて隣を拾わない</summary>
        static Rect Uv(RectInt r)
        {
            const float e = 0.05f;
            return new Rect((r.x + e) / AtlasSize, (r.y + e) / AtlasSize, (r.width - 2f * e) / AtlasSize, (r.height - 2f * e) / AtlasSize);
        }

        /// <summary>一色の升の uv。真ん中の画素だけを使う</summary>
        static Rect Uv(Sw s)
        {
            var r = Swatch(s);
            return new Rect((r.x + 3.5f) / AtlasSize, (r.y + 3.5f) / AtlasSize, 1f / AtlasSize, 1f / AtlasSize);
        }

        // ---- 描く台 ----------------------------------------------------------

        /// <summary>色（sRGB）と α の組。α は Cut・Solid・Lit のどれか</summary>
        sealed class Sheet
        {
            public readonly int W;
            public readonly int H;
            public readonly Color[] C;
            public readonly byte[] A;

            public Sheet(int w, int h)
            {
                W = w;
                H = h;
                C = new Color[w * h];
                A = new byte[w * h];
            }

            public void Set(int x, int y, Color c, byte a)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                C[y * W + x] = c;
                A[y * W + x] = a;
            }

            public Color Get(int x, int y)
            {
                return C[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];
            }

            public void Fill(int x, int y, int w, int h, Color c, byte a)
            {
                for (var j = y; j < y + h; j++)
                    for (var i = x; i < x + w; i++)
                        Set(i, j, c, a);
            }

            public Texture2D Bake()
            {
                var px = new Color32[W * H];
                for (var i = 0; i < px.Length; i++)
                {
                    var c = C[i];
                    px[i] = new Color32(B(c.r), B(c.g), B(c.b), A[i]);
                }
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
                tex.SetPixels32(px);
                tex.Apply();
                return tex;
            }
        }

        static byte B(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        static float F(Random r)
        {
            return (float)r.NextDouble();
        }

        static Color Mul(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>二枚（夕暮れと夜）に同じ物を描く</summary>
        static void Both(Sheet d, Sheet n, int x, int y, Color c, byte a)
        {
            d.Set(x, y, c, a);
            n.Set(x, y, c, a);
        }

        static void BothFill(Sheet d, Sheet n, int x, int y, int w, int h, Color c, byte a)
        {
            d.Fill(x, y, w, h, c, a);
            n.Fill(x, y, w, h, c, a);
        }

        // ---- 色 --------------------------------------------------------------

        static readonly Color StockBrick = new Color(0.55f, 0.47f, 0.36f);
        static readonly Color RedBrick = new Color(0.50f, 0.28f, 0.22f);
        static readonly Color SootBrick = new Color(0.40f, 0.35f, 0.29f);
        static readonly Color Stucco = new Color(0.70f, 0.67f, 0.60f);
        static readonly Color StoneTrim = new Color(0.66f, 0.63f, 0.57f);
        static readonly Color SashFrame = new Color(0.72f, 0.70f, 0.66f);

        /// <summary>灯った窓の色。暖かい電球・白っぽい灯り・橙の笠・桃色のカーテン越し・たまにテレビの青</summary>
        static readonly Color[] WindowLights =
        {
            new Color(1.00f, 0.80f, 0.50f),
            new Color(1.00f, 0.88f, 0.64f),
            new Color(0.98f, 0.66f, 0.38f),
            new Color(0.98f, 0.72f, 0.58f),
            new Color(1.00f, 0.80f, 0.50f),
            new Color(0.62f, 0.72f, 1.00f),
        };

        static readonly Color[] DoorColors =
        {
            new Color(0.08f, 0.08f, 0.09f),
            new Color(0.10f, 0.13f, 0.26f),
            new Color(0.34f, 0.08f, 0.08f),
            new Color(0.08f, 0.22f, 0.15f),
            new Color(0.40f, 0.36f, 0.30f),
        };

        static readonly Color[] Pastels =
        {
            new Color(0.70f, 0.67f, 0.60f),
            new Color(0.66f, 0.68f, 0.64f),
            new Color(0.72f, 0.64f, 0.58f),
            new Color(0.62f, 0.66f, 0.70f),
            new Color(0.70f, 0.70f, 0.66f),
            new Color(0.70f, 0.62f, 0.64f),
        };

        /// <summary>灯っていない窓。夕暮れは空を映して青紫、夜はほとんど黒</summary>
        static Color GlassDusk(float up)
        {
            return Color.Lerp(new Color(0.15f, 0.14f, 0.23f), new Color(0.30f, 0.27f, 0.42f), up);
        }

        static Color GlassNight(float up)
        {
            return Color.Lerp(new Color(0.04f, 0.04f, 0.06f), new Color(0.08f, 0.08f, 0.12f), up);
        }

        // ---- 窓 --------------------------------------------------------------

        /// <summary>
        /// 上げ下げ窓を一つ。枠・ガラス・真ん中の桟。灯るかどうかは一度だけ振って、夕暮れで灯る窓は夜も灯る
        /// </summary>
        static void Window(Sheet d, Sheet n, int x0, int y0, int w, int h, float pDusk, float pNight, Random r, bool sash = true)
        {
            var roll = F(r);
            var litDusk = roll < pDusk;
            var litNight = roll < pNight;
            var warm = WindowLights[r.Next(WindowLights.Length)];
            var curtain = r.Next(3);
            // 楣石と枠
            BothFill(d, n, x0 - 1, y0 + h, w + 2, 1, StoneTrim, Solid);
            BothFill(d, n, x0, y0, w, h, SashFrame, Solid);
            for (var y = y0 + 1; y < y0 + h - 1; y++)
                for (var x = x0 + 1; x < x0 + w - 1; x++)
                {
                    var up = (y - y0) / (float)Mathf.Max(1, h - 1);
                    var edge = x == x0 + 1 || x == x0 + w - 2;
                    var top = y == y0 + h - 2;
                    // 灯った窓は真ん中が明るく、カーテンの寄った端と上の日除けが少し沈む
                    var lit = warm;
                    if (curtain == 1 && edge) lit = Mul(warm, 0.62f);
                    if (curtain == 2 && top) lit = Mul(warm, 0.55f);
                    lit = Mul(lit, 0.9f + 0.1f * F(r));
                    d.Set(x, y, litDusk ? lit : GlassDusk(up), litDusk ? Lit : Solid);
                    n.Set(x, y, litNight ? lit : GlassNight(up), litNight ? Lit : Solid);
                }
            if (sash && h >= 7) BothFill(d, n, x0 + 1, y0 + h / 2, w - 2, 1, SashFrame, Solid);
        }

        // ---- 家の正面 ----------------------------------------------------------

        static void PaintFront(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            var type = variant % 4;
            var doorLeft = (variant / 4) % 2 == 0;
            var wall = type == 0 ? StockBrick : type == 1 ? RedBrick : type == 2 ? Pastels[r.Next(Pastels.Length)] : SootBrick;
            var plaster = type == 2;
            var ox = cell.x;
            var oy = cell.y;
            // 壁。煉瓦は 6.4 画素 / m では目地まで描けないので、ざらつきと煤の縦の筋だけ
            var streaks = new float[32];
            for (var x = 0; x < 32; x++) streaks[x] = F(r) < 0.18f ? 0.86f + 0.08f * F(r) : 1f;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                {
                    var k = plaster ? 0.96f + 0.06f * F(r) : 0.88f + 0.2f * F(r);
                    var soot = Mathf.Lerp(1f, streaks[x], y / 63f);
                    Both(d, n, ox + x, oy + y, Mul(wall, k * soot), Solid);
                }
            // 一階は漆喰（黄色い煉瓦と赤い煉瓦の家）。横の目地を 3 画素おきに
            if (type == 0 || type == 1)
                for (var y = 1; y < 21; y++)
                    for (var x = 0; x < 32; x++)
                        Both(d, n, ox + x, oy + y, Mul(Stucco, (y % 3 == 0 ? 0.84f : 0.97f) + 0.04f * F(r)), Solid);
            // 足元の石、一階と二階の間の帯、軒の蛇腹と影
            BothFill(d, n, ox, oy, 32, 1, new Color(0.28f, 0.26f, 0.25f), Solid);
            BothFill(d, n, ox, oy + 21, 32, 1, StoneTrim, Solid);
            BothFill(d, n, ox, oy + 59, 32, 1, Mul(wall, 0.55f), Solid);
            BothFill(d, n, ox, oy + 60, 32, 4, StoneTrim, Solid);
            BothFill(d, n, ox, oy + 62, 32, 1, Mul(StoneTrim, 1.08f), Solid);

            // 戸。周りに石の縁、上に明かり取り
            var doorX = doorLeft ? 4 : 21;
            var door = DoorColors[r.Next(DoorColors.Length)];
            BothFill(d, n, ox + doorX - 1, oy + 1, 9, 18, StoneTrim, Solid);
            BothFill(d, n, ox + doorX, oy + 2, 7, 13, door, Solid);
            BothFill(d, n, ox + doorX + 5, oy + 8, 1, 1, new Color(0.6f, 0.5f, 0.25f), Solid);
            var fan = F(r);
            for (var x = 0; x < 7; x++)
            {
                var glowD = fan < 0.35f;
                var glowN = fan < 0.7f;
                var c = new Color(1.0f, 0.82f, 0.52f);
                d.Set(ox + doorX + x, oy + 16, glowD ? c : GlassDusk(0.5f), glowD ? Lit : Solid);
                d.Set(ox + doorX + x, oy + 17, glowD ? c : GlassDusk(0.8f), glowD ? Lit : Solid);
                n.Set(ox + doorX + x, oy + 16, glowN ? c : GlassNight(0.5f), glowN ? Lit : Solid);
                n.Set(ox + doorX + x, oy + 17, glowN ? c : GlassNight(0.8f), glowN ? Lit : Solid);
            }
            // 一階の窓（張り出し窓を思わせる幅の広い窓）
            Window(d, n, ox + (doorLeft ? 16 : 3), oy + 5, 11, 12, 0.18f, 0.42f, r);
            // 二階（背の高い窓）と三階
            Window(d, n, ox + 5, oy + 25, 7, 14, 0.2f, 0.45f, r);
            Window(d, n, ox + 20, oy + 25, 7, 14, 0.2f, 0.45f, r);
            Window(d, n, ox + 5, oy + 45, 7, 11, 0.16f, 0.4f, r);
            Window(d, n, ox + 20, oy + 45, 7, 11, 0.16f, 0.4f, r);
        }

        // ---- 家の裏 ------------------------------------------------------------

        static void PaintBack(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            var wall = variant % 3 == 0 ? StockBrick : variant % 3 == 1 ? SootBrick : RedBrick;
            var ox = cell.x;
            var oy = cell.y;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                    Both(d, n, ox + x, oy + y, Mul(wall, 0.82f + 0.18f * F(r)), Solid);
            // 裏の張り出し（台所）。少し明るい漆喰
            for (var y = 0; y < 22; y++)
                for (var x = 17; x < 32; x++)
                    Both(d, n, ox + x, oy + y, Mul(Stucco, 0.78f + 0.08f * F(r)), Solid);
            BothFill(d, n, ox + 17, oy + 22, 15, 1, Mul(wall, 0.5f), Solid);
            // 雨樋の縦管
            BothFill(d, n, ox + 14, oy, 1, 60, new Color(0.12f, 0.12f, 0.13f), Solid);
            BothFill(d, n, ox, oy + 60, 32, 4, Mul(wall, 0.7f), Solid);
            Window(d, n, ox + 4, oy + 3, 6, 12, 0.3f, 0.55f, r, false);
            Window(d, n, ox + 21, oy + 8, 6, 8, 0.35f, 0.6f, r);
            Window(d, n, ox + 5, oy + 27, 6, 10, 0.18f, 0.4f, r);
            Window(d, n, ox + 22, oy + 29, 5, 7, 0.1f, 0.25f, r);
            Window(d, n, ox + 5, oy + 45, 6, 10, 0.14f, 0.36f, r);
            Window(d, n, ox + 20, oy + 45, 6, 10, 0.14f, 0.36f, r);
        }

        // ---- 集合住宅 ------------------------------------------------------------

        static void PaintFlats(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            var council = variant >= 2;
            var wall = council ? new Color(0.50f, 0.50f, 0.48f) : new Color(0.50f, 0.30f, 0.24f);
            var ox = cell.x;
            var oy = cell.y;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                    Both(d, n, ox + x, oy + y, Mul(wall, (council ? 0.92f : 0.86f) + (council ? 0.08f : 0.18f) * F(r)), Solid);
            for (var f = 0; f < 4; f++)
            {
                var fy = oy + f * 16;
                if (council)
                {
                    // 各階の廊下とバルコニーの手すりの帯
                    BothFill(d, n, ox, fy + 1, 32, 3, new Color(0.30f, 0.30f, 0.30f), Solid);
                    BothFill(d, n, ox, fy + 3, 32, 1, new Color(0.62f, 0.62f, 0.60f), Solid);
                }
                else BothFill(d, n, ox, fy + 14, 32, 1, StoneTrim, Solid);
                Window(d, n, ox + 4, fy + 5, 9, 9, 0.24f, 0.5f, r, !council);
                Window(d, n, ox + 19, fy + 5, 9, 9, 0.24f, 0.5f, r, !council);
            }
        }

        static void PaintGable(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            var wall = variant == 0 ? StockBrick : SootBrick;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                {
                    var breast = x >= 12 && x < 20 ? 0.9f : 1f;
                    Both(d, n, cell.x + x, cell.y + y, Mul(wall, breast * (0.84f + 0.2f * F(r))), Solid);
                }
        }

        // ---- 角のパブ ------------------------------------------------------------

        static void PaintPub(Sheet d, Sheet n, RectInt cell, bool front, Random r)
        {
            var ox = cell.x;
            var oy = cell.y;
            var cream = new Color(0.70f, 0.66f, 0.56f);
            var green = new Color(0.06f, 0.18f, 0.12f);
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                    Both(d, n, ox + x, oy + y, Mul(cream, 0.94f + 0.08f * F(r)), Solid);
            // 一階は深い緑のタイルと大きな窓。窓は磨りガラス越しの暖かい灯りで、夕暮れから灯っている
            BothFill(d, n, ox, oy, 64, 22, green, Solid);
            var glass = new Color(1.0f, 0.76f, 0.46f);
            var panes = front ? new[] { 4, 38 } : new[] { 6, 40 };
            foreach (var px in panes)
                for (var y = 4; y < 17; y++)
                    for (var x = px; x < px + 22; x++)
                    {
                        var bar = (x - px) % 5 == 4 || y == 10;
                        var c = bar ? new Color(0.10f, 0.08f, 0.06f) : Mul(glass, 0.85f + 0.15f * F(r) + (y > 12 ? 0.05f : 0f));
                        Both(d, n, ox + x, oy + y, c, bar ? Solid : Lit);
                    }
            // 戸と、その上の明かり取り
            var doorX = front ? 28 : 30;
            BothFill(d, n, ox + doorX, oy + 1, 7, 13, new Color(0.05f, 0.07f, 0.06f), Solid);
            BothFill(d, n, ox + doorX, oy + 14, 7, 2, glass, Lit);
            // 看板の帯。金の字を点で
            BothFill(d, n, ox, oy + 18, 64, 4, new Color(0.04f, 0.10f, 0.07f), Solid);
            for (var x = 8; x < 56; x++)
                if (F(r) < 0.55f) Both(d, n, ox + x, oy + 19 + (F(r) < 0.5f ? 0 : 1), new Color(1.0f, 0.82f, 0.40f), Lit);
            BothFill(d, n, ox, oy + 22, 64, 1, StoneTrim, Solid);
            // 上の階。三つずつ
            for (var f = 0; f < 2; f++)
                for (var k = 0; k < 3; k++)
                    Window(d, n, ox + 8 + k * 20, oy + 27 + f * 17, 8, 12, 0.2f, 0.4f, r);
            BothFill(d, n, ox, oy + 59, 64, 1, Mul(cream, 0.55f), Solid);
            BothFill(d, n, ox, oy + 60, 64, 4, StoneTrim, Solid);
        }

        // ---- 教会 ------------------------------------------------------------

        static void PaintChurch(Sheet d, Sheet n, Random r)
        {
            var rag = new Color(0.44f, 0.43f, 0.41f);
            foreach (var cell in new[] { ChurchWall, ChurchTower })
                for (var y = 0; y < 64; y++)
                    for (var x = 0; x < 32; x++)
                        Both(d, n, cell.x + x, cell.y + y, Mul(rag, 0.84f + 0.2f * F(r)), Solid);
            // 身廊の尖頭窓。ステンドグラス越しの鈍い灯り
            var glassA = new Color(0.62f, 0.38f, 0.30f);
            var glassB = new Color(0.36f, 0.34f, 0.58f);
            for (var y = 12; y < 48; y++)
            {
                var half = y > 42 ? Mathf.Max(0, 3 - (y - 42) / 2) : 3;
                for (var x = 16 - half; x < 16 + half; x++)
                {
                    var lead = (x + y) % 4 == 0;
                    var c = lead ? new Color(0.08f, 0.07f, 0.07f) : ((x + y / 3) % 2 == 0 ? glassA : glassB);
                    Both(d, n, ChurchWall.x + x, ChurchWall.y + y, c, lead ? Solid : Lit);
                }
            }
            // 塔。鐘楼の鎧戸と、灯った時計の文字盤
            var t = ChurchTower;
            for (var k = 0; k < 2; k++)
                BothFill(d, n, t.x + 9 + k * 9, t.y + 30, 5, 10, new Color(0.10f, 0.10f, 0.11f), Solid);
            for (var y = 0; y < 11; y++)
                for (var x = 0; x < 11; x++)
                {
                    var dx = x - 5f;
                    var dy = y - 5f;
                    var rr = dx * dx + dy * dy;
                    if (rr > 30f) continue;
                    var hand = (x == 5 && y >= 5 && y <= 9) || (y == 5 && x >= 5 && x <= 8);
                    Both(d, n, t.x + 10 + x, t.y + 46 + y, hand ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.92f, 0.78f), hand ? Solid : Lit);
                }
            // 尖塔のスレート
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                {
                    var line = x % 4 == 0;
                    Both(d, n, Spire.x + x, Spire.y + y, Mul(new Color(0.30f, 0.31f, 0.34f), (line ? 0.75f : 0.95f) + 0.1f * F(r)), Solid);
                }
        }

        // ---- 屋根 ------------------------------------------------------------

        static void PaintRoof(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            // 夕暮れの空を映すスレートは、影の色より一段明るい
            var slate = variant == 3 ? new Color(0.46f, 0.28f, 0.23f)
                : variant == 1 ? new Color(0.31f, 0.33f, 0.39f)
                : new Color(0.35f, 0.35f, 0.40f);
            var ox = cell.x;
            var oy = cell.y;
            for (var y = 0; y < 64; y++)
            {
                var course = y / 3;
                var shift = course % 2 == 0 ? 0 : 1;
                for (var x = 0; x < 32; x++)
                {
                    var joint = y % 3 == 0 || (x + shift) % 3 == 0;
                    var k = joint ? 0.7f : 0.94f + 0.16f * F(r);
                    // 雨の筋と苔
                    if (F(r) < 0.02f) k *= 1.15f;
                    Both(d, n, ox + x, oy + y, Mul(slate, k), Solid);
                }
            }
            // 棟の鉛
            BothFill(d, n, ox, oy + 61, 32, 3, new Color(0.22f, 0.22f, 0.24f), Solid);
            if (variant == 4)
            {
                // 屋根窓。小さな屋根と、灯るかもしれない窓
                BothFill(d, n, ox + 10, oy + 14, 12, 18, new Color(0.62f, 0.60f, 0.56f), Solid);
                Window(d, n, ox + 12, oy + 16, 8, 12, 0.3f, 0.55f, r);
                BothFill(d, n, ox + 9, oy + 32, 14, 2, new Color(0.20f, 0.20f, 0.22f), Solid);
            }
            if (variant == 5)
            {
                // 天窓二つ。夕暮れは空を映して明るく、灯れば黄色い四角
                for (var k = 0; k < 2; k++)
                {
                    var x0 = ox + 6 + k * 14;
                    var roll = F(r);
                    for (var y = 0; y < 8; y++)
                        for (var x = 0; x < 6; x++)
                        {
                            var edge = x == 0 || y == 0 || x == 5 || y == 7;
                            var lit = new Color(1.0f, 0.84f, 0.58f);
                            var c = edge ? new Color(0.18f, 0.18f, 0.20f) : (roll < 0.3f ? lit : new Color(0.42f, 0.38f, 0.55f));
                            d.Set(x0 + x, oy + 30 + y, c, !edge && roll < 0.3f ? Lit : Solid);
                            var cn = edge ? new Color(0.18f, 0.18f, 0.20f) : (roll < 0.6f ? lit : GlassNight(0.5f));
                            n.Set(x0 + x, oy + 30 + y, cn, !edge && roll < 0.6f ? Lit : Solid);
                        }
                }
            }
        }

        // ---- 木 --------------------------------------------------------------

        /// <summary>樹冠。丸を幾つか重ねた塊を抜き、上の縁を明るく、下を暗く。葉の隙間を少し抜く</summary>
        static void PaintCanopy(Sheet d, Sheet n, RectInt cell, int variant, Random r)
        {
            var blobs = 9 + variant * 2;
            var cx = new float[blobs];
            var cy = new float[blobs];
            var cr = new float[blobs];
            for (var i = 0; i < blobs; i++)
            {
                var a = F(r) * Mathf.PI * 2f;
                var reach = F(r) * 16f;
                cx[i] = 32f + Mathf.Cos(a) * reach;
                cy[i] = 30f + Mathf.Sin(a) * reach * 0.8f;
                cr[i] = 9f + F(r) * 7f;
            }
            var leaf = variant == 2 ? new Color(0.20f, 0.26f, 0.14f) : new Color(0.16f, 0.24f, 0.15f);
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    var inside = false;
                    for (var i = 0; i < blobs && !inside; i++)
                    {
                        var dx = x - cx[i];
                        var dy = y - cy[i];
                        inside = dx * dx + dy * dy < cr[i] * cr[i];
                    }
                    if (!inside || F(r) < 0.05f)
                    {
                        Both(d, n, cell.x + x, cell.y + y, leaf, Cut);
                        continue;
                    }
                    var k = 0.7f + 0.45f * (y / 63f) + 0.25f * F(r);
                    if (F(r) < 0.08f) k *= 1.3f;
                    Both(d, n, cell.x + x, cell.y + y, Mul(leaf, k), Solid);
                }
        }

        static void PaintRailing(Sheet d, Sheet n)
        {
            var iron = new Color(0.07f, 0.07f, 0.08f);
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 32; x++)
                {
                    var bar = x % 2 == 0 && y < 14;
                    var rail = y == 1 || y == 11;
                    var finial = y == 14 && x % 2 == 0;
                    var on = bar || rail || finial;
                    Both(d, n, Railing.x + x, Railing.y + y, iron, on ? Solid : Cut);
                }
        }

        static void PaintPhone(Sheet d, Sheet n)
        {
            var red = new Color(0.62f, 0.08f, 0.07f);
            var pane = new Color(0.95f, 0.92f, 0.78f);
            BothFill(d, n, Phone.x, Phone.y, 16, 32, red, Solid);
            for (var row = 0; row < 6; row++)
                for (var col = 0; col < 3; col++)
                    BothFill(d, n, Phone.x + 3 + col * 4, Phone.y + 4 + row * 3, 3, 2, pane, Lit);
            BothFill(d, n, Phone.x + 2, Phone.y + 25, 12, 2, new Color(0.95f, 0.95f, 0.92f), Lit);
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 8; x++)
                    Both(d, n, Stripe.x + x, Stripe.y + y, (y / 4) % 2 == 0 ? new Color(0.06f, 0.06f, 0.06f) : new Color(0.78f, 0.78f, 0.76f), Solid);
        }

        static void PaintSwatches(Sheet d, Sheet n)
        {
            SwatchFill(d, n, Sw.Road, new Color(0.22f, 0.22f, 0.24f), Solid);
            SwatchFill(d, n, Sw.RoadFar, new Color(0.19f, 0.19f, 0.21f), Solid);
            SwatchFill(d, n, Sw.Pave, new Color(0.42f, 0.41f, 0.42f), Solid);
            SwatchFill(d, n, Sw.Kerb, new Color(0.55f, 0.54f, 0.52f), Solid);
            SwatchFill(d, n, Sw.White, new Color(0.78f, 0.78f, 0.76f), Solid);
            SwatchFill(d, n, Sw.Yellow, new Color(0.80f, 0.66f, 0.18f), Solid);
            SwatchFill(d, n, Sw.Lawn, new Color(0.17f, 0.25f, 0.14f), Solid);
            SwatchFill(d, n, Sw.Garden, new Color(0.18f, 0.19f, 0.14f), Solid);
            SwatchFill(d, n, Sw.Hedge, new Color(0.11f, 0.18f, 0.11f), Solid);
            SwatchFill(d, n, Sw.Gravel, new Color(0.47f, 0.44f, 0.39f), Solid);
            SwatchFill(d, n, Sw.Iron, new Color(0.07f, 0.07f, 0.08f), Solid);
            SwatchFill(d, n, Sw.LampLit, new Color(1.00f, 0.84f, 0.58f), Lit);
            SwatchFill(d, n, Sw.Trunk, new Color(0.22f, 0.19f, 0.16f), Solid);
            SwatchFill(d, n, Sw.Paint, new Color(0.78f, 0.78f, 0.78f), Solid);
            SwatchFill(d, n, Sw.CarGlass, new Color(0.12f, 0.13f, 0.17f), Solid);
            SwatchFill(d, n, Sw.Brick, new Color(0.46f, 0.34f, 0.27f), Solid);
            SwatchFill(d, n, Sw.Pot, new Color(0.52f, 0.32f, 0.22f), Solid);
            SwatchFill(d, n, Sw.BeaconLit, new Color(1.00f, 0.62f, 0.16f), Lit);
            SwatchFill(d, n, Sw.FlatRoof, new Color(0.24f, 0.24f, 0.26f), Solid);
            SwatchFill(d, n, Sw.Stone, new Color(0.62f, 0.59f, 0.54f), Solid);
            SwatchFill(d, n, Sw.Area, new Color(0.08f, 0.08f, 0.09f), Solid);
            SwatchFill(d, n, Sw.Lead, new Color(0.30f, 0.31f, 0.33f), Solid);
            SwatchFill(d, n, Sw.WarmLit, new Color(1.00f, 0.78f, 0.48f), Lit);
            SwatchFill(d, n, Sw.Path, new Color(0.30f, 0.29f, 0.28f), Solid);
            SwatchFill(d, n, Sw.PhoneRed, new Color(0.62f, 0.08f, 0.07f), Solid);
            SwatchFill(d, n, Sw.Black, new Color(0.02f, 0.02f, 0.02f), Solid);
            SwatchFill(d, n, Sw.Wall, new Color(0.40f, 0.36f, 0.31f), Solid);
            SwatchFill(d, n, Sw.Gold, new Color(0.80f, 0.64f, 0.26f), Solid);
            SwatchFill(d, n, Sw.BridgeBlue, new Color(0.34f, 0.52f, 0.72f), Solid);
            SwatchFill(d, n, Sw.BridgeLamp, new Color(0.80f, 0.86f, 1.00f), Lit);
            SwatchFill(d, n, Sw.FloodLamp, new Color(1.00f, 0.90f, 0.70f), Lit);
            SwatchFill(d, n, Sw.RedLamp, new Color(1.00f, 0.16f, 0.12f), Lit);
            SwatchFill(d, n, Sw.TipLamp, new Color(0.92f, 0.95f, 1.00f), Lit);
            SwatchFill(d, n, Sw.O2Yellow, new Color(0.86f, 0.70f, 0.14f), Solid);
            SwatchFill(d, n, Sw.NeonPink, new Color(1.00f, 0.24f, 0.66f), Lit);
            SwatchFill(d, n, Sw.NeonCyan, new Color(0.24f, 0.92f, 1.00f), Lit);
            SwatchFill(d, n, Sw.NeonViolet, new Color(0.66f, 0.40f, 1.00f), Lit);
            // 灯りの当たっていない石。夜のアトラスでは投光の灯り（名所の縁や屋根の端を照らす）
            var portland = Swatch(Sw.Portland);
            d.Fill(portland.x, portland.y, portland.width, portland.height, new Color(0.74f, 0.71f, 0.64f), Solid);
            n.Fill(portland.x, portland.y, portland.width, portland.height, Flooded(new Color(0.74f, 0.71f, 0.64f), 0.5f), Lit);
        }

        // ---- 名所 ------------------------------------------------------------

        static readonly Color Caen = new Color(0.76f, 0.72f, 0.62f);
        static readonly Color Ragstone = new Color(0.56f, 0.54f, 0.50f);
        static readonly Color PortlandStone = new Color(0.76f, 0.74f, 0.68f);
        static readonly Color FloodWarm = new Color(1.00f, 0.88f, 0.66f);
        static readonly Color FloodCool = new Color(0.84f, 0.88f, 1.00f);

        /// <summary>投光で照らした石の色。up は面の下（0）から上（1）。灯りは足元から当てるので下ほど明るい</summary>
        static Color Flooded(Color stone, float up, bool cool = false)
        {
            var lamp = cool ? FloodCool : FloodWarm;
            return Mul(Color.Lerp(lamp, stone, 0.4f), 0.78f - 0.3f * up);
        }

        /// <summary>
        /// 石の面を一画素。投光で照らした色（光る所）で、夕暮れは点いたばかりの控えめな灯り、夜は強い灯り。
        /// 夕暮れの空は明るいので、石の色のままでは空に溶けて形が読めなかった。
        /// dark なら窓や目地の暗がりで、照らさない
        /// </summary>
        static void Stone(Sheet d, Sheet n, int x, int y, Color stone, float up, float k, bool dark = false, bool cool = false)
        {
            if (dark)
            {
                d.Set(x, y, Mul(stone, 0.3f * k), Solid);
                n.Set(x, y, Mul(stone, 0.18f * k), Solid);
                return;
            }
            d.Set(x, y, Mul(Flooded(stone, up, cool), 0.72f * k), Lit);
            n.Set(x, y, Mul(Flooded(stone, up, cool), k), Lit);
        }

        /// <summary>
        /// ホワイト・タワーの一面。付け柱で四つの間に分け、間ごとに円い頭の小さな窓を三段。
        /// 頭の 3 画素は狭間（凸凹）で、凹の所を抜く
        /// </summary>
        static void PaintWhiteTower(Sheet d, Sheet n, Random r)
        {
            var c = WhiteTowerFace;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    if (y >= 61 && (x / 3) % 2 == 1)
                    {
                        Both(d, n, c.x + x, c.y + y, Caen, Cut);
                        continue;
                    }
                    var pilaster = x % 16 < 2;
                    var course = y == 22 || y == 42;
                    var k = (pilaster || course ? 1.06f : 0.94f) + 0.08f * F(r);
                    // 窓。間ごとに二つ、三段
                    var bx = x % 16;
                    var window = false;
                    foreach (var wy in new[] { 8, 28, 47 })
                    {
                        var h = wy == 28 ? 9 : 7;
                        if ((bx == 5 || bx == 6 || bx == 10 || bx == 11) && y >= wy && y < wy + h) window = true;
                        if ((bx == 5 || bx == 11) && y == wy + h - 1) window = false;
                    }
                    Stone(d, n, c.x + x, c.y + y, Caen, y / 63f, k, window);
                }
        }

        static void PaintTurret(Sheet d, Sheet n, Random r)
        {
            var c = TurretFace;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                {
                    var slit = (x == 15 || x == 16) && (y % 14 >= 5 && y % 14 < 10);
                    Stone(d, n, c.x + x, c.y + y, Caen, y / 63f, 0.92f + 0.1f * F(r), slit);
                }
        }

        /// <summary>城壁と円い塔。ケントの石の灰色に矢狭間、頭を狭間に抜く</summary>
        static void PaintCurtain(Sheet d, Sheet n, Random r)
        {
            foreach (var c in new[] { CurtainWall, DrumTower })
                for (var y = 0; y < c.height; y++)
                    for (var x = 0; x < c.width; x++)
                    {
                        if (y >= c.height - 3 && (x / 3) % 2 == 1)
                        {
                            Both(d, n, c.x + x, c.y + y, Ragstone, Cut);
                            continue;
                        }
                        var slit = x % 11 == 5 && y > 10 && y < 16;
                        var k = 0.88f + 0.18f * F(r) - (y < 3 ? 0.12f : 0f);
                        Stone(d, n, c.x + x, c.y + y, Ragstone, y / (float)(c.height - 1), k * 0.95f, slit);
                    }
        }

        /// <summary>
        /// タワーブリッジの塔。灰白の石張りに縦長の尖頭窓を二列、角の控え壁。夜は青みの投光と窓の灯り
        /// </summary>
        static void PaintBridge(Sheet d, Sheet n, Random r)
        {
            var c = BridgeTower;
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 32; x++)
                {
                    var buttress = x < 3 || x > 28;
                    var lancet = (x >= 9 && x <= 12 || x >= 19 && x <= 22) && y >= 18 && y < 50 && (y < 46 || x == 10 || x == 11 || x == 20 || x == 21);
                    var band = y == 16 || y == 52;
                    var k = (buttress || band ? 1.05f : 0.95f) + 0.07f * F(r);
                    if (lancet)
                    {
                        // 窓は夕暮れに灯り始め、夜は全部灯る
                        var lit = new Color(0.95f, 0.86f, 0.62f);
                        var on = F(r) < 0.35f;
                        d.Set(c.x + x, c.y + y, on ? lit : new Color(0.20f, 0.20f, 0.28f), on ? Lit : Solid);
                        n.Set(c.x + x, c.y + y, Mul(lit, 0.9f), Lit);
                        continue;
                    }
                    Stone(d, n, c.x + x, c.y + y, PortlandStone, y / 63f, k, false, true);
                }
            // 上の歩道橋の格子。青い鋼の上下の弦と斜めの格子、その間は抜く。弦に灯りの粒
            var w = BridgeWalk;
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 64; x++)
                {
                    var chord = y < 2 || y > 13;
                    var lattice = (x + y) % 8 == 0 || (x - y + 64) % 8 == 0;
                    var blue = new Color(0.34f, 0.52f, 0.72f);
                    if (chord && x % 6 == 3 && (y == 1 || y == 14))
                    {
                        d.Set(w.x + x, w.y + y, new Color(0.85f, 0.88f, 1.0f), Lit);
                        n.Set(w.x + x, w.y + y, new Color(0.80f, 0.70f, 1.0f), Lit);
                        continue;
                    }
                    Both(d, n, w.x + x, w.y + y, Mul(blue, 0.9f + 0.15f * F(r)), chord || lattice ? Solid : Cut);
                }
        }

        /// <summary>
        /// 高いガラスの塔。夕暮れは空と暮れ残りを映した色、夜は暗いガラスに灯った窓。
        /// top 行から上は頂（シャードの頂の光・ウォーキートーキーの空中庭園）で、どちらの時刻も灯る
        /// </summary>
        static void PaintGlass(Sheet d, Sheet n, RectInt c, Color low, Color high, Color night, float pDusk, float pNight, int top, Color crown, System.Func<int, int, float> pattern, Random r)
        {
            for (var y = 0; y < c.height; y++)
                for (var x = 0; x < c.width; x++)
                {
                    var up = y / (float)(c.height - 1);
                    var p = pattern(x, y);
                    if (y >= top)
                    {
                        var on = (x + y) % 3 != 0;
                        Both(d, n, c.x + x, c.y + y, on ? Mul(crown, 0.85f + 0.15f * F(r)) : Mul(high, 0.6f), on ? Lit : Solid);
                        continue;
                    }
                    var glass = Mul(Color.Lerp(low, high, up), p * (0.94f + 0.08f * F(r)));
                    var roll = F(r);
                    var windowRow = y % 3 == 1 && x % 2 == 0;
                    var lamp = F(r) < 0.8f ? new Color(0.86f, 0.92f, 1.0f) : new Color(1.0f, 0.82f, 0.56f);
                    d.Set(c.x + x, c.y + y, windowRow && roll < pDusk ? Mul(lamp, 0.8f) : glass, windowRow && roll < pDusk ? Lit : Solid);
                    n.Set(c.x + x, c.y + y, windowRow && roll < pNight ? Mul(lamp, 0.75f) : Mul(night, p), windowRow && roll < pNight ? Lit : Solid);
                }
        }

        /// <summary>セント・ポール大聖堂。鉛のドーム（縦の肋）、列柱のドラム、ポートランドの石の壁</summary>
        static void PaintStPaul(Sheet d, Sheet n, Random r)
        {
            var lead = new Color(0.46f, 0.48f, 0.52f);
            var c = DomeFace;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    Stone(d, n, c.x + x, c.y + y, lead, 1f - y / 31f, (x % 4 == 0 ? 1.12f : 0.95f) + 0.05f * F(r), false, true);
            c = DrumFace;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var column = x % 4 < 2;
                    var entablature = y < 4 || y > 26;
                    Stone(d, n, c.x + x, c.y + y, PortlandStone, y / 31f, 0.95f + 0.08f * F(r), !column && !entablature);
                }
            c = CathedralFace;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var window = x % 8 >= 3 && x % 8 < 5 && (y % 16 >= 5 && y % 16 < 12);
                    var cornice = y % 16 == 14;
                    Stone(d, n, c.x + x, c.y + y, PortlandStone, (y % 16) / 15f, (cornice ? 1.1f : 0.95f) + 0.06f * F(r), window);
                }
        }

        static void PaintRiver(Sheet d, Sheet n, Random r)
        {
            var c = WaterFace;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var streak = F(r) < 0.12f ? 1.25f : 1f;
                    d.Set(c.x + x, c.y + y, Mul(new Color(0.34f, 0.29f, 0.44f), (0.9f + 0.1f * F(r)) * streak), Solid);
                    var glint = F(r) < 0.02f;
                    n.Set(c.x + x, c.y + y, glint ? new Color(0.9f, 0.75f, 0.5f) * 0.6f : Mul(new Color(0.06f, 0.06f, 0.10f), streak), glint ? Lit : Solid);
                }
            c = Embankment;
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 32; x++)
                {
                    var joint = y % 4 == 0 || (x + (y / 4) * 4) % 8 == 0;
                    Both(d, n, c.x + x, c.y + y, Mul(new Color(0.46f, 0.45f, 0.43f), joint ? 0.7f : 0.92f + 0.1f * F(r)), Solid);
                }
        }

        /// <summary>カナリー・ワーフの塔と O2</summary>
        static void PaintDocklands(Sheet d, Sheet n, Random r)
        {
            // ワン・カナダ・スクエア。ステンレスの格子に窓
            PaintGlass(d, n, CanadaFace, new Color(0.50f, 0.52f, 0.58f), new Color(0.66f, 0.66f, 0.72f), new Color(0.16f, 0.17f, 0.20f),
                0.22f, 0.55f, 64, Color.white, (x, y) => x % 2 == 1 ? 1.1f : 0.85f, r);
            PaintGlass(d, n, GlassTower, new Color(0.30f, 0.38f, 0.52f), new Color(0.46f, 0.50f, 0.66f), new Color(0.07f, 0.09f, 0.14f),
                0.18f, 0.5f, 64, Color.white, (x, y) => y % 6 == 0 ? 1.15f : 1f, r);
            // O2 の白い膜。放射状の縫い目。夜は青紫に照らす
            var c = O2Face;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var seam = x % 4 == 0;
                    var white = Mul(new Color(0.84f, 0.84f, 0.82f), (seam ? 0.85f : 1f) - 0.2f * (1f - y / 31f));
                    d.Set(c.x + x, c.y + y, white, Solid);
                    n.Set(c.x + x, c.y + y, Mul(new Color(0.58f, 0.46f, 1.0f), (seam ? 0.7f : 0.85f) * (0.6f + 0.4f * y / 31f)), Lit);
                }
        }

        /// <summary>名所の升を全部。種は街並みの升とは別に取る（前からの升の絵を変えない）</summary>
        static void PaintLandmarks(Sheet d, Sheet n)
        {
            var r = new Random(Seed + 17);
            PaintWhiteTower(d, n, r);
            PaintTurret(d, n, r);
            PaintCurtain(d, n, r);
            PaintBridge(d, n, r);
            // ザ・シャード。尖った面の縦の縁と、頂の開いた骨組みの光
            PaintGlass(d, n, ShardFace, new Color(0.42f, 0.40f, 0.54f), new Color(0.70f, 0.56f, 0.66f), new Color(0.09f, 0.10f, 0.15f),
                0.06f, 0.28f, 56, new Color(0.92f, 0.95f, 1.0f), (x, y) => x == 0 || x == 16 ? 1.25f : 1f, r);
            // ガーキン。菱形の格子と、螺旋に巻く暗い帯
            PaintGlass(d, n, GherkinFace, new Color(0.26f, 0.34f, 0.40f), new Color(0.40f, 0.44f, 0.54f), new Color(0.06f, 0.09f, 0.10f),
                0.1f, 0.3f, 60, new Color(0.7f, 0.95f, 1.0f),
                (x, y) => ((x + y / 2) % 16 < 5 ? 0.62f : 1f) * ((x + y) % 4 == 0 || (x - y + 64) % 4 == 0 ? 1.18f : 1f), r);
            // ウォーキートーキー。縦の方立てと、頂の空中庭園
            PaintGlass(d, n, WalkieFace, new Color(0.38f, 0.42f, 0.52f), new Color(0.54f, 0.56f, 0.68f), new Color(0.08f, 0.09f, 0.13f),
                0.12f, 0.35f, 57, new Color(0.72f, 1.0f, 0.78f), (x, y) => x % 2 == 0 ? 1.1f : 0.9f, r);
            PaintStPaul(d, n, r);
            PaintRiver(d, n, r);
            PaintDocklands(d, n, r);
        }

        static void SwatchFill(Sheet d, Sheet n, Sw s, Color c, byte a)
        {
            var r = Swatch(s);
            BothFill(d, n, r.x, r.y, r.width, r.height, c, a);
        }

        /// <summary>街並みのアトラスを夕暮れと夜の二枚で描く</summary>
        static void PaintTown(out Texture2D dusk, out Texture2D night)
        {
            var r = new Random(Seed);
            var d = new Sheet(AtlasSize, AtlasSize);
            var n = new Sheet(AtlasSize, AtlasSize);
            for (var i = 0; i < Fronts; i++) PaintFront(d, n, FrontCell(i), i, r);
            for (var i = 0; i < Backs; i++) PaintBack(d, n, BackCell(i), i, r);
            for (var i = 0; i < 4; i++) PaintFlats(d, n, FlatsCell(i), i, r);
            for (var i = 0; i < 2; i++) PaintGable(d, n, GableCell(i), i, r);
            PaintPub(d, n, PubSouth, true, r);
            PaintPub(d, n, PubWest, false, r);
            PaintChurch(d, n, r);
            PaintPhone(d, n);
            for (var i = 0; i < Roofs; i++) PaintRoof(d, n, RoofCell(i), i, r);
            for (var i = 0; i < 3; i++) PaintCanopy(d, n, CanopyCell(i), i, r);
            PaintRailing(d, n);
            PaintLandmarks(d, n);
            PaintSwatches(d, n);
            dusk = d.Bake();
            night = n.Bake();
        }

        // ---- 暈 --------------------------------------------------------------

        /// <summary>街灯の暈と路面の溜まり。白く、α が真ん中から縁へ落ちる。色は頂点色が持つ</summary>
        static Texture2D PaintGlow()
        {
            const int size = 64;
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var rr = dx * dx + dy * dy;
                    var a = Mathf.Exp(-rr / (2f * 0.16f)) * 0.8f + Mathf.Exp(-rr / (2f * 0.02f)) * 0.2f;
                    a = Mathf.Clamp01((a - 0.02f) / 0.98f);
                    px[y * size + x] = new Color32(255, 255, 255, B(a));
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---- 書き出し ----------------------------------------------------------

        /// <summary>絵を書き出して取り込む。pixel なら最近傍・mipmap 無し・圧縮しない（アトラス）、そうでなければ滑らかに（空と暈）</summary>
        static void Write(Texture2D tex, string path, bool pixel, bool alpha)
        {
            if (!AssetDatabase.IsValidFolder(TextureDir)) AssetDatabase.CreateFolder("Assets/Textures", "RoomView");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.wrapMode = TextureWrapMode.Clamp;
            if (pixel)
            {
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = AtlasSize;
            }
            else
            {
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.maxTextureSize = SkyWide;
            }
            imp.SaveAndReimport();
        }

        /// <summary>絵を全部描き直して取り込む</summary>
        static void PaintAll()
        {
            Texture2D dusk, night;
            PaintTown(out dusk, out night);
            Write(dusk, TownDuskPath, true, true);
            Write(night, TownNightPath, true, true);
            Write(PaintSky(RoomView.Hour.Dusk), SkyDuskPath, false, false);
            Write(PaintSky(RoomView.Hour.Night), SkyNightPath, false, false);
            Write(PaintGlow(), GlowPath, false, true);
        }
    }
}
