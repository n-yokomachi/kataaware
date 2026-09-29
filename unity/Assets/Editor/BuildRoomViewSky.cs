using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の窓の外の空の絵（<see cref="BuildRoomView"/>）。空の球（<see cref="RoomView.OnSky"/>）の、窓から見える向きの幅だけに貼る。
    /// 横は向き（<see cref="RoomView.SkyU"/>）、縦は仰角（<see cref="RoomView.SkyV"/>。地平の近くに画素を寄せる）。
    ///
    /// 空の色は仰角の移りに、日の沈んだ側（西北西）の暮れ残りと、市街の方角（北東を中心に三か所）の滲んだ光を足す。
    /// 地平の色は街並みの霞（<c>HalfAware/Townscape</c> の _Haze・_HazeWarm）と同じ式で出すので、遠い棟が空へ溶ける。
    ///
    /// **遠い街は絵に描く。** 地平のすぐ下から上へ、遠い屋根の帯（まばらな窓の灯り）と、市街の高い建物の群れ。
    /// 建物にはネオンの縦の管・冠の帯・看板・窓の粒・航空障害灯を描き、光る物は別の層に描いてぼかして重ね、暈にする。
    /// 夕暮れは空が明るいぶんネオンを控えめに、夜は空を落として灯りを増やす
    /// </summary>
    public static partial class BuildRoomView
    {
        /// <summary>日の沈んだ向き（西北西）。8 月の倫敦の 19 時台</summary>
        public const float SunAzimuth = -68f;
        /// <summary>日の沈んだ側へ霞の色が寄る絞り（向きの余弦の冪）</summary>
        public const float WarmWidth = 2f;

        /// <summary>時刻ごとの見え方。空の絵とマテリアルの値を一か所に持つ。色は sRGB</summary>
        sealed class Look
        {
            // 街並み（Townscape）
            public Color Tint;
            public Color GlowTint;
            public Color Haze;
            public Color HazeWarm;
            public float HazeMax;
            public float GlowHaze;
            /// <summary>街灯の暈の強さ</summary>
            public Color Halo;
            // 空
            public Color Low;
            public Color Mid;
            public Color High;
            public Color Zenith;
            public Color Afterglow;
            public float AfterglowAmount;
            public Color City;
            public float CityAmount;
            public Color CloudDark;
            public Color CloudLit;
            public float CloudCover;
            public Color Roofs;
            /// <summary>遠い屋根の帯の窓の灯りの密度</summary>
            public float RoofLights;
            /// <summary>高い建物の窓の灯りの密度</summary>
            public float TowerLights;
            /// <summary>ネオンの強さ</summary>
            public float Neon;
            public float Stars;
        }

        static Look LookOf(RoomView.Hour hour)
        {
            if (hour == RoomView.Hour.Night)
                return new Look
                {
                    Tint = new Color(0.34f, 0.34f, 0.48f),
                    GlowTint = new Color(1.05f, 1.05f, 1.05f),
                    Haze = new Color(0.13f, 0.11f, 0.19f),
                    HazeWarm = new Color(0.17f, 0.12f, 0.18f),
                    HazeMax = 0.78f,
                    GlowHaze = 0.4f,
                    Halo = new Color(1f, 1f, 1f),
                    Low = new Color(0.16f, 0.12f, 0.21f),
                    Mid = new Color(0.08f, 0.07f, 0.15f),
                    High = new Color(0.05f, 0.05f, 0.11f),
                    Zenith = new Color(0.03f, 0.03f, 0.07f),
                    Afterglow = new Color(0.20f, 0.12f, 0.18f),
                    AfterglowAmount = 0.25f,
                    City = new Color(0.62f, 0.26f, 0.40f),
                    CityAmount = 1.0f,
                    CloudDark = new Color(0.07f, 0.06f, 0.10f),
                    CloudLit = new Color(0.34f, 0.18f, 0.26f),
                    CloudCover = 0.45f,
                    Roofs = new Color(0.06f, 0.055f, 0.09f),
                    RoofLights = 0.05f,
                    TowerLights = 0.34f,
                    Neon = 1.25f,
                    Stars = 1f,
                };
            return new Look
            {
                Tint = new Color(0.72f, 0.66f, 0.86f),
                GlowTint = new Color(1f, 1f, 1f),
                Haze = new Color(0.46f, 0.38f, 0.55f),
                HazeWarm = new Color(0.78f, 0.52f, 0.52f),
                HazeMax = 0.8f,
                GlowHaze = 0.45f,
                Halo = new Color(0.6f, 0.6f, 0.6f),
                Low = new Color(0.52f, 0.40f, 0.58f),
                Mid = new Color(0.36f, 0.28f, 0.50f),
                High = new Color(0.22f, 0.18f, 0.40f),
                Zenith = new Color(0.12f, 0.10f, 0.25f),
                Afterglow = new Color(1.00f, 0.60f, 0.40f),
                AfterglowAmount = 0.8f,
                City = new Color(0.70f, 0.30f, 0.50f),
                CityAmount = 0.55f,
                CloudDark = new Color(0.28f, 0.21f, 0.34f),
                CloudLit = new Color(0.92f, 0.52f, 0.52f),
                CloudCover = 0.55f,
                Roofs = new Color(0.30f, 0.24f, 0.36f),
                RoofLights = 0.025f,
                TowerLights = 0.2f,
                Neon = 1.0f,
                Stars = 0f,
            };
        }

        /// <summary>市街の方角。中心の向き（度）・広がり（度）・建物の背の上限（仰角の度）・建物の数</summary>
        struct District
        {
            public float Centre;
            public float Spread;
            public float Tall;
            public int Count;
            public float Glow;

            public District(float centre, float spread, float tall, int count, float glow)
            {
                Centre = centre;
                Spread = spread;
                Tall = tall;
                Count = count;
                Glow = glow;
            }
        }

        /// <summary>
        /// 市街の群れ。シティ（ガーキンとウォーキートーキーの辺り。北の窓）とカナリー・ワーフ（東の窓）の二か所に寄せ、
        /// 名所の後ろで輝かせる。絵の塔は名所の形より低く抑え、名所の輪郭を塞がない。
        /// 西北西（日の沈んだ側）には置かない。暮れ残りはザ・シャードの向こう
        /// </summary>
        static readonly District[] Districts =
        {
            new District(-27f, 15f, 4.8f, 30, 1.2f),
            new District(80f, 5.5f, 4.5f, 14, 1.0f),
        };

        static readonly Color[] NeonColors =
        {
            new Color(1.00f, 0.20f, 0.62f),
            new Color(0.20f, 0.92f, 1.00f),
            new Color(1.00f, 0.62f, 0.16f),
            new Color(0.66f, 0.34f, 1.00f),
            new Color(0.36f, 1.00f, 0.56f),
            new Color(1.00f, 0.32f, 0.30f),
            new Color(1.00f, 0.20f, 0.62f),
            new Color(0.20f, 0.92f, 1.00f),
        };

        /// <summary>地平の色。街並みの霞と同じ式（linear で混ぜる）</summary>
        static Color HorizonAt(Look look, float azimuth)
        {
            var warm = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((azimuth - SunAzimuth) * Mathf.Deg2Rad)), WarmWidth);
            return Color.Lerp(look.Haze.linear, look.HazeWarm.linear, warm);
        }

        static float Px(float azimuth)
        {
            return RoomView.SkyU(azimuth) * SkyWide;
        }

        static float Py(float elevation)
        {
            return RoomView.SkyV(elevation) * SkyHigh;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var a = Mathf.Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), fx);
            var b = Mathf.Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float x, float y, int seed)
        {
            return Noise(x, y, seed) * 0.55f + Noise(x * 2.1f, y * 2.1f, seed + 7) * 0.3f + Noise(x * 4.3f, y * 4.3f, seed + 13) * 0.15f;
        }

        /// <summary>空の絵を一枚。色は linear で積み、最後に sRGB へ戻す</summary>
        static Texture2D PaintSky(RoomView.Hour hour)
        {
            var look = LookOf(hour);
            var w = SkyWide;
            var h = SkyHigh;
            var sky = new Color[w * h];
            var glow = new Color[w * h];

            var low = look.Low.linear;
            var mid = look.Mid.linear;
            var high = look.High.linear;
            var zenith = look.Zenith.linear;
            var after = look.Afterglow.linear;
            var city = look.City.linear;
            var cloudDark = look.CloudDark.linear;
            var cloudLit = look.CloudLit.linear;

            for (var y = 0; y < h; y++)
            {
                var e = RoomView.SkyElevation((y + 0.5f) / h);
                for (var x = 0; x < w; x++)
                {
                    var az = RoomView.ArcFrom + (x + 0.5f) / w * (RoomView.ArcTo - RoomView.ArcFrom);
                    var horizon = HorizonAt(look, az);
                    Color c;
                    var up = Mathf.Max(0f, e);
                    if (up < 6f) c = Color.Lerp(horizon, low, Smooth(up / 6f));
                    else if (up < 16f) c = Color.Lerp(low, mid, Smooth((up - 6f) / 10f));
                    else if (up < 36f) c = Color.Lerp(mid, high, Smooth((up - 16f) / 20f));
                    else c = Color.Lerp(high, zenith, Smooth(Mathf.Clamp01((up - 36f) / 34f)));

                    // 暮れ残り。日の沈んだ向きの地平に近いほど強い
                    var toSun = Mathf.Max(0f, Mathf.Cos((az - SunAzimuth) * Mathf.Deg2Rad));
                    var afterglow = Mathf.Pow(toSun, 5f) * Mathf.Exp(-up / 7f) * look.AfterglowAmount;
                    c += after * afterglow;

                    // 市街の滲んだ光。地平の上に低く広がる
                    var cityGlow = 0f;
                    foreach (var dist in Districts)
                    {
                        var da = Mathf.DeltaAngle(az, dist.Centre) / (dist.Spread * 1.6f);
                        cityGlow += Mathf.Exp(-da * da) * Mathf.Exp(-up / (3.5f + dist.Tall * 0.6f)) * dist.Glow;
                    }
                    c += city * cityGlow * look.CityAmount * 0.7f;

                    // 雲。横に長い筋を、地平から 22 度までに
                    if (up > 1.5f && up < 24f)
                    {
                        var band = Mathf.Clamp01((up - 1.5f) / 3f) * Mathf.Clamp01((24f - up) / 8f);
                        var n = Fbm(az * 0.045f, up * 0.42f, 91);
                        var streak = Mathf.Clamp01((n - (1f - look.CloudCover)) / 0.18f) * band;
                        if (streak > 0f)
                        {
                            var lit = Mathf.Clamp01(Mathf.Pow(toSun, 3f) * 1.2f + cityGlow * 0.5f) * (1f - Mathf.Clamp01((up - 4f) / 18f) * 0.4f);
                            var cloud = Color.Lerp(cloudDark, cloudLit, lit * (0.55f + 0.45f * Noise(az * 0.3f, up * 1.2f, 5)));
                            c = Color.Lerp(c, cloud, streak * 0.8f);
                        }
                    }

                    // 星。夜の、高い所に少しだけ
                    if (look.Stars > 0f && up > 14f && Hash(x, y, 3) < 0.0012f)
                        c += Color.white * (0.25f + 0.4f * Hash(x, y, 4)) * look.Stars;
                    sky[y * w + x] = c;
                }
            }

            // 種は時刻に依らない。夕暮れと夜で同じ建物が同じ所に立つ
            var r = new Random(Seed);
            Roofline(sky, glow, look, r);
            foreach (var dist in Districts) Towers(sky, glow, look, dist, r);
            Halo(sky, glow);

            var px = new Color32[w * h];
            for (var i = 0; i < px.Length; i++)
            {
                var g = sky[i].gamma;
                px[i] = new Color32(B(g.r), B(g.g), B(g.b), 255);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 遠い屋根の帯。地平のすぐ上に小さな凹凸（煙突と木）を立て、そこから下を塗る。
        /// 近くの街並み（230 m まで）の一番遠い棟の棟が仰角 -0.3 度ほどに来るので、それより上に 0.5 度ほど覗く
        /// </summary>
        static void Roofline(Color[] sky, Color[] glow, Look look, Random r)
        {
            var w = SkyWide;
            var roofs = look.Roofs.linear;
            for (var x = 0; x < w; x++)
            {
                var az = RoomView.ArcFrom + (x + 0.5f) / w * (RoomView.ArcTo - RoomView.ArcFrom);
                var top = -0.15f + 0.35f * Noise(az * 0.9f, 0.5f, 21) + (Hash(x, 0, 23) < 0.06f ? 0.25f : 0f);
                // 木の丸い塊
                top += 0.3f * Mathf.Clamp01((Noise(az * 0.35f, 2.5f, 29) - 0.62f) * 5f);
                var yTop = Mathf.RoundToInt(Py(top));
                var horizon = HorizonAt(look, az);
                for (var y = 0; y <= yTop && y < SkyHigh; y++)
                {
                    var e = RoomView.SkyElevation((y + 0.5f) / SkyHigh);
                    // 近いほど（仰角が下ほど）霞が抜けて暗い
                    var depth = Mathf.Clamp01(-e / 6f);
                    var c = Color.Lerp(Color.Lerp(horizon, roofs, 0.55f), roofs * 0.8f, depth);
                    var cityNear = 0f;
                    foreach (var dist in Districts)
                    {
                        var da = Mathf.DeltaAngle(az, dist.Centre) / (dist.Spread * 1.3f);
                        cityNear += Mathf.Exp(-da * da) * dist.Glow;
                    }
                    var i = y * w + x;
                    sky[i] = c;
                    // 窓の灯りの粒。市街の方角ほど多い
                    var density = look.RoofLights * (1f + cityNear * 2.5f) * (0.4f + 0.6f * (1f - depth));
                    if (Hash(x, y, 31) < density)
                    {
                        var warm = Hash(x, y, 37) < 0.75f;
                        var lamp = warm ? new Color(1.0f, 0.7f, 0.4f) : new Color(0.7f, 0.85f, 1.0f);
                        var k = 0.35f + 0.5f * Hash(x, y, 41);
                        sky[i] += lamp.linear * k;
                        glow[i] += lamp.linear * k * 0.3f;
                    }
                }
            }
        }

        /// <summary>市街の高い建物の群れを一つ。奥の低い物から手前の高い物へ重ねる</summary>
        static void Towers(Color[] sky, Color[] glow, Look look, District dist, Random r)
        {
            var list = new List<Vector4>();
            for (var i = 0; i < dist.Count; i++)
            {
                var az = dist.Centre + (F(r) * 2f - 1f) * dist.Spread;
                var near = 1f - Mathf.Abs(Mathf.DeltaAngle(az, dist.Centre)) / dist.Spread;
                var tall = Mathf.Lerp(0.8f, dist.Tall, Mathf.Pow(F(r), 1.6f) * (0.45f + 0.55f * near));
                var wide = Mathf.Lerp(0.35f, 1.5f, F(r)) * Mathf.Lerp(0.8f, 1.2f, tall / dist.Tall);
                list.Add(new Vector4(az, wide, tall, F(r)));
            }
            // 群れに二本、抜きん出た塔
            if (dist.Tall > 6f)
            {
                list.Add(new Vector4(dist.Centre - 4f, 0.9f, dist.Tall * 1.08f, 0.93f));
                list.Add(new Vector4(dist.Centre + 7f, 0.7f, dist.Tall * 0.92f, 0.97f));
            }
            list.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (var t in list) Tower(sky, glow, look, t.x, t.y, t.z, t.w, r);
        }

        /// <summary>
        /// 建物を一つ。向き az・幅 wide（度）・背 tall（仰角の度）。style の値で形と飾りを振る。
        /// 胴は空の色に沈んだ影、窓は粒、ネオンは縦の管・冠の帯・看板
        /// </summary>
        static void Tower(Color[] sky, Color[] glow, Look look, float az, float wide, float tall, float style, Random r)
        {
            var w = SkyWide;
            var x0 = Mathf.RoundToInt(Px(az - wide * 0.5f));
            var x1 = Mathf.RoundToInt(Px(az + wide * 0.5f));
            var yb = Mathf.RoundToInt(Py(-0.6f));
            var yt = Mathf.RoundToInt(Py(tall));
            if (x1 <= x0 + 1 || yt <= yb) return;
            var horizon = HorizonAt(look, az);
            var body = Color.Lerp(look.Roofs.linear, horizon, 0.25f) * (0.75f + 0.25f * style);
            // 頭の段（細る）
            var stepped = style > 0.55f;
            var stepAt = Mathf.RoundToInt(Mathf.Lerp(yb, yt, 0.78f));
            var inset = Mathf.Max(1, (x1 - x0) / 4);
            var neon = NeonColors[r.Next(NeonColors.Length)].linear;
            var neon2 = NeonColors[r.Next(NeonColors.Length)].linear;
            var windowCool = style > 0.4f;
            for (var y = yb; y <= yt && y < SkyHigh; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if (x < 0 || x >= w) continue;
                    if (stepped && y > stepAt && (x < x0 + inset || x > x1 - inset)) continue;
                    var i = y * w + x;
                    sky[i] = body;
                    // 窓の粒。2 画素おきの格子に、灯る物だけ
                    if ((x - x0) % 2 == 1 && (y - yb) % 3 == 1 && Hash(x, y, 57) < look.TowerLights)
                    {
                        var lamp = windowCool ? new Color(0.72f, 0.86f, 1.0f).linear : new Color(1.0f, 0.78f, 0.5f).linear;
                        var k = 0.3f + 0.35f * Hash(x, y, 59);
                        sky[i] += lamp * k;
                        glow[i] += lamp * k * 0.25f;
                    }
                }
            var n = look.Neon;
            // 縦の管を片側か両側に
            if (style < 0.75f)
            {
                Strip(sky, glow, x0, yb + (yt - yb) / 5, yt - 1, neon, n);
                if (style < 0.35f) Strip(sky, glow, x1, yb + (yt - yb) / 3, yt - 2, neon2, n);
            }
            // 冠の帯
            if (style > 0.3f)
            {
                var yc = stepped ? stepAt : yt - 1;
                for (var x = x0; x <= x1; x++) Emit(sky, glow, x, yc, neon2, n * 0.9f);
            }
            // 看板。胴の中ほどに明るい四角
            if (style > 0.15f && x1 - x0 >= 5 && F(r) < 0.7f)
            {
                var bw = Mathf.Max(3, (x1 - x0) * 2 / 3);
                var bh = Mathf.Max(3, Mathf.RoundToInt(bw * 0.7f));
                var bx = x0 + (x1 - x0 - bw) / 2;
                var by = Mathf.RoundToInt(Mathf.Lerp(yb, yt, 0.35f + 0.3f * F(r)));
                var colour = NeonColors[r.Next(NeonColors.Length)].linear;
                for (var y = by; y < by + bh; y++)
                    for (var x = bx; x < bx + bw; x++)
                    {
                        var edge = x == bx || x == bx + bw - 1 || y == by || y == by + bh - 1;
                        var fleck = Hash(x, y, 61) < 0.35f;
                        Emit(sky, glow, x, y, colour, n * (edge ? 1.0f : fleck ? 0.85f : 0.45f));
                    }
            }
            // 航空障害灯
            if (tall > 3f)
            {
                var red = new Color(1.0f, 0.12f, 0.08f).linear;
                var ax = (x0 + x1) / 2;
                for (var y = yt; y < yt + Mathf.RoundToInt(tall * 1.4f) && y < SkyHigh; y++) sky[y * w + ax] = body;
                Emit(sky, glow, ax, Mathf.Min(SkyHigh - 1, yt + Mathf.RoundToInt(tall * 1.4f)), red, 1.2f);
            }
        }

        static void Strip(Color[] sky, Color[] glow, int x, int y0, int y1, Color colour, float n)
        {
            for (var y = y0; y <= y1; y++) Emit(sky, glow, x, y, colour, n * (0.85f + 0.15f * Hash(x, y, 67)));
        }

        /// <summary>光る画素を一つ。芯は白へ寄せ、暈の層にも積む</summary>
        static void Emit(Color[] sky, Color[] glow, int x, int y, Color colour, float k)
        {
            if (x < 0 || y < 0 || x >= SkyWide || y >= SkyHigh) return;
            var i = y * SkyWide + x;
            var core = Color.Lerp(colour, Color.white, 0.25f) * k;
            sky[i] = new Color(Mathf.Max(sky[i].r, core.r), Mathf.Max(sky[i].g, core.g), Mathf.Max(sky[i].b, core.b));
            glow[i] += colour * k;
        }

        /// <summary>光る物の層をぼかして重ねる。横 3 回・縦 3 回の箱のぼかしで、釣鐘に近い暈になる</summary>
        static void Halo(Color[] sky, Color[] glow)
        {
            var w = SkyWide;
            var h = SkyHigh;
            var a = glow;
            var b = new Color[a.Length];
            for (var pass = 0; pass < 3; pass++)
            {
                BoxH(a, b, w, h, 4);
                BoxV(b, a, w, h, 3);
            }
            for (var i = 0; i < sky.Length; i++) sky[i] += a[i] * 1.4f;
        }

        static void BoxH(Color[] src, Color[] dst, int w, int h, int radius)
        {
            var n = radius * 2 + 1;
            for (var y = 0; y < h; y++)
            {
                var sum = Color.clear;
                for (var x = -radius; x <= radius; x++) sum += src[y * w + Mathf.Clamp(x, 0, w - 1)];
                for (var x = 0; x < w; x++)
                {
                    dst[y * w + x] = sum / n;
                    sum -= src[y * w + Mathf.Clamp(x - radius, 0, w - 1)];
                    sum += src[y * w + Mathf.Clamp(x + radius + 1, 0, w - 1)];
                }
            }
        }

        static void BoxV(Color[] src, Color[] dst, int w, int h, int radius)
        {
            var n = radius * 2 + 1;
            for (var x = 0; x < w; x++)
            {
                var sum = Color.clear;
                for (var y = -radius; y <= radius; y++) sum += src[Mathf.Clamp(y, 0, h - 1) * w + x];
                for (var y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum / n;
                    sum -= src[Mathf.Clamp(y - radius, 0, h - 1) * w + x];
                    sum += src[Mathf.Clamp(y + radius + 1, 0, h - 1) * w + x];
                }
            }
        }
    }
}
