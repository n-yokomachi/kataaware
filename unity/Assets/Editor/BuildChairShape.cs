using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    public static partial class BuildChair
    {
        // ---- 寸法 ------------------------------------------------------------------
        //
        // どれも椅子（Room/Chair）から見た値。x は右、y は上、z は前（机の方）。原点は床の、ガスシリンダーの真下。
        // 座った体の形（SeatedPose。PlaceProtagonist.RoomSit が座面・肘掛け・床から解いた形）を組み直さずに済むよう、
        // 体が触れる所（座面の高さの線・肘掛けの上面・差込口・ジャックの置き場）は前の椅子と同じ所に置く

        /// <summary>座面の高さの線。前の椅子の座面の上面（背の側 0.549、前 0.50）と同じ傾き（前へ 6 度下がる）</summary>
        static float SeatLevel(float z) { return 0.527f - 0.105f * z; }

        /// <summary>座面のクッションの半分の幅（肘掛けを支える磨いた金属の腕の内）</summary>
        const float SeatHalf = 0.27f;
        /// <summary>座面の後ろの端と前の端</summary>
        const float SeatBack = -0.215f;
        const float SeatFront = 0.29f;
        /// <summary>クッションの下の面（座の皿の上）</summary>
        const float SeatFloor = 0.435f;
        /// <summary>
        /// 座面の前の真ん中の、ふくらはぎの逃げ。膝の裏から下がるふくらはぎ（床から 0.42〜0.47 m で前の端から 4 cm 内まで来る）に
        /// 始めの椅子は 3 cm 食い込んでいた。前の真ん中だけクッションの下を上げて、ふくらはぎの上に浮かせる
        /// </summary>
        const float CalfRelief = 0.042f;
        /// <summary>縁の丸み（横から見た上の角と、上から見た前の角）。厚く柔らかい革のクッション</summary>
        const float SeatEdge = 0.055f;
        const float SeatCorner = 0.07f;

        /// <summary>背もたれの倒れ（度）</summary>
        const float Recline = 20f;
        /// <summary>背もたれの前の基準の面の下の端（真ん中）。座面の後ろに沈めて載せる</summary>
        static readonly Vector3 BackBase = new Vector3(0f, 0.535f, -0.145f);
        /// <summary>背もたれの長さ（倒れた向きに沿って）と、前の基準の面から裏の殻の裏までの厚み</summary>
        const float BackLength = 0.95f;
        const float BackThick = 0.095f;
        /// <summary>背もたれのクッションの半分の幅（まっすぐの脇）と、上の角・下の角の丸み</summary>
        const float BackHalfW = 0.24f;
        const float BackTopRound = 0.12f;
        const float BackBottomRound = 0.03f;
        /// <summary>クッションの裏（殻の前の面に収まる所）と前の面の高さ、縁の丸み（前の基準の面から前へ）</summary>
        const float CushionBack = -0.06f;
        const float CushionFront = 0.03f;
        const float CushionRoll = 0.045f;
        /// <summary>殻がクッションの縁から外へ出る幅（黒い漆の縁が見える）</summary>
        const float ShellLip = 0.015f;
        /// <summary>ボタン留め（ダイヤ形）。ボタンの横の間と、段の間（倒れた向きに沿って）、一段目の高さと段の数</summary>
        const float TuftStep = 0.0535f;
        const float TuftRow = 0.075f;
        const float TuftLow = 0.16f;
        const int TuftRows = 7;

        /// <summary>肘掛けの上面の線。前の椅子と同じ高さと傾き（前へ 4 度下がる）。ジャックの置き場（z −0.07）で 0.6758</summary>
        static float PadTop(float z) { return 0.6758f - 0.0699f * (z + 0.07f); }
        const float PadInner = 0.245f;
        const float PadOuter = 0.345f;
        const float PadThick = 0.042f;
        const float PadBack = -0.19f;
        /// <summary>肘掛けの当て物の前の端。座って始めた形の左の指先（z 0.27〜0.29 で下へ垂れる）より手前で丸めて落とす</summary>
        const float PadFront = 0.255f;
        /// <summary>
        /// 肘掛けの前の端に後から金具で留めた操作盤（前へ高くなる斜めの面を、座った目の方へ向ける）。当て物の外の脇に留め、
        /// 内の縁は座って始めた形の左の小指（x −0.31）と当て物（0.345）より外。
        /// 座った目から肘掛けの上は真下に近く、下を向ける限り（40 度）では目に入らない。前へ出して高くし、首を振って見下ろすと画面の下に入る所に置く
        /// </summary>
        const float PodInner = 0.352f;
        const float PodOuter = 0.442f;
        const float PodBack = 0.19f;
        const float PodFront = 0.33f;
        /// <summary>差込口（Room/Chair/PortHole。ケーブルの始まり）。前の椅子と同じ所</summary>
        public static readonly Vector3 PortAt = new Vector3(0.286f, 0.670f, 0.150f);

        /// <summary>五本脚の脚の先（キャスターの芯）までの半径</summary>
        const float LegReach = 0.30f;

        /// <summary>
        /// フットレストのパッドの上の面の線。座った形の足の裏（踵 z 0.18 で 0.169、土踏まず z 0.30 で 0.122、指の付け根 z 0.38〜0.40 で 0.096〜0.098）に
        /// ±5 mm で沿わせて、前へ 18 度下げる（パッドは柔らかいので、足の裏がわずかに沈む所がある）
        /// </summary>
        static float FootTop(float z) { return 0.161f - 0.33f * (z - 0.18f); }

        // ---- 絵の中の置き場 --------------------------------------------------------

        /// <summary>張り地の絵（64×64、縦横とも繰り返す黒革の地）と、その 1 枚の大きさ（m）</summary>
        const int UpholsteryW = 64;
        const int UpholsteryH = 64;
        const float GrainTile = 0.32f;

        /// <summary>操作盤の絵（64×64）の置き場。画素で (x, y, 幅, 高さ)、y は下から</summary>
        const int PanelSize = 64;
        static readonly RectInt RightPod = new RectInt(0, 4, 16, 16);
        static readonly RectInt LeftPod = new RectInt(16, 4, 16, 16);
        static readonly RectInt RearScreen = new RectInt(32, 4, 24, 16);
        static readonly RectInt EarScreen = new RectInt(32, 20, 12, 16);
        static readonly RectInt PortPlate = new RectInt(44, 20, 20, 12);
        static readonly RectInt JunctionFace = new RectInt(44, 32, 20, 12);
        static readonly RectInt EarLamp = new RectInt(32, 36, 12, 12);
        static readonly RectInt LedStrip = new RectInt(44, 44, 20, 4);

        /// <summary>色の升（4×4 画素、下の段に 16 個）。ケーブルやキャスターの一色の物は升の真ん中を引く</summary>
        enum Swatch { Rubber, Plastic, Grey, Violet, Teal, Orange, Metal, Label, Cyan, VioletLit, Amber, Green, Brass, Glass, PlasticLight, Red }

        static Vector2 SwatchUv(Swatch s)
        {
            return new Vector2(((int)s * 4 + 2f) / PanelSize, 2f / PanelSize);
        }

        /// <summary>画素の置き場を uv の矩形にする。点で引くので、縁の画素の外を拾わないよう内へ少し寄せる</summary>
        static Rect Uv(RectInt r, int w, int h)
        {
            return new Rect((r.x + 0.05f) / w, (r.y + 0.05f) / h, (r.width - 0.1f) / w, (r.height - 0.1f) / h);
        }

        static Rect PanelUv(RectInt r) { return Uv(r, PanelSize, PanelSize); }

        /// <summary>革の地の uv。m で渡し、1 枚の大きさで割る（絵は縦横とも繰り返す）</summary>
        static Vector2 GrainUv(float u, float v) { return new Vector2(u / GrainTile, v / GrainTile); }

        // ---- 組み立て ----------------------------------------------------------------

        /// <summary>組んだ形。mesh と、確かめ（体が食い込んでいないか）に使う部品ごとの三角</summary>
        public sealed class Made
        {
            public Mesh Mesh;
            public List<Piece> Pieces;
            public int Triangles;
            public int Vertices;
        }

        /// <summary>部品ひとつ。solid は中身の詰まった形（体が入り込んではいけない物）</summary>
        public sealed class Piece
        {
            public string Name;
            public bool Solid;
            public Bounds Bounds;
            public int[] Triangles;
        }

        /// <summary>椅子の形を組む。押すたびに同じ物になる（乱れは使わない）</summary>
        public static Made Shape()
        {
            var shop = new Shop();
            Seat(shop);
            Backrest(shop);
            Pillows(shop);
            Arm(shop, 1f);
            Arm(shop, -1f);
            Port(shop);
            Undercarriage(shop);
            Base(shop);
            Footrest(shop);
            Crown(shop);
            Wiring(shop);
            var mesh = shop.Bake("Chair");
            return new Made { Mesh = mesh, Pieces = shop.Pieces, Triangles = shop.TriangleCount, Vertices = mesh.vertexCount };
        }

        // ---- 座面 ------------------------------------------------------------------

        static float Smooth(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

        /// <summary>座面の上の面の高さ。尻の沈む所をわずかに窪ませる</summary>
        static float SeatTop(float x, float z)
        {
            return SeatLevel(z) - 0.005f * Mathf.Exp(-Mathf.Pow((z - 0.03f) / 0.12f, 2f)) * (1f - Smooth(0.12f, 0.20f, Mathf.Abs(x)));
        }

        /// <summary>クッションの下の面。前の真ん中だけ、ふくらはぎの上へ上げる</summary>
        static float SeatUnder(float x, float z)
        {
            return SeatFloor + CalfRelief * Smooth(0.10f, 0.25f, z) * (1f - Smooth(0.19f, 0.235f, Mathf.Abs(x)));
        }

        /// <summary>
        /// クッションの形（座面と、フットレストのパッド）。上の面と下の面の高さ（x, z から）、前と後ろの端、半分の幅、脇の縁の丸み、
        /// 上から見た前の角の丸み、脇の端を畳む時に厚みを測る前後の位置
        /// </summary>
        sealed class Cushion
        {
            public Func<float, float, float> Top;
            public Func<float, float, float> Under;
            public float Front, Back, Half, Edge, Corner, MidZ;
        }

        static readonly Cushion SeatShape = new Cushion
        {
            Top = SeatTop, Under = SeatUnder, Front = SeatFront, Back = SeatBack, Half = SeatHalf, Edge = SeatEdge, Corner = SeatCorner, MidZ = 0.03f,
        };

        /// <summary>
        /// クッションの、左右の位置 x での横から見た輪郭（後ろの下から、下の面を前へ、前の丸み、上の面を後ろへ、後ろの丸み）。
        /// 脇の縁では輪郭を内へ寄せて丸め（inset）、最後は線に畳んで閉じる（collapse）
        /// </summary>
        static List<Vector3> CushionRing(Cushion c, float x, bool collapse)
        {
            var ax = Mathf.Abs(x);
            var d = 0f;
            var edge = c.Half - c.Edge;
            if (ax > edge) d = c.Edge - Mathf.Sqrt(Mathf.Max(0f, c.Edge * c.Edge - (ax - edge) * (ax - edge)));
            var corner = c.Half - c.Corner;
            var plan = ax > corner ? c.Corner - Mathf.Sqrt(Mathf.Max(0f, c.Corner * c.Corner - (ax - corner) * (ax - corner))) : 0f;
            Func<float, float> top = z => c.Top(x, z) - d;
            Func<float, float> under = z => c.Under(x, z) + d;
            if (collapse)
            {
                // 厚みの半分まで寄せて、上と下を真ん中の線で合わせる
                var mid = 0.5f * (c.Top(x, c.MidZ) + c.Under(x, c.MidZ));
                top = z => mid;
                under = z => mid;
                d = 0.5f * (c.Top(x, c.MidZ) - c.Under(x, c.MidZ));
            }
            var zf = c.Front - d - plan;
            var zb = c.Back + d + plan * 0.5f;
            // 前の丸み。上と下の真ん中を芯に、厚みの半分を半径にする
            var rn = Mathf.Max(0.0005f, 0.5f * (top(zf - 0.02f) - under(zf - 0.02f)));
            var zc = zf - rn;
            rn = Mathf.Max(0.0005f, 0.5f * (top(zc) - under(zc)));
            var yn = 0.5f * (top(zc) + under(zc));
            var rk = Mathf.Max(0.0005f, 0.5f * (top(zb + 0.03f) - under(zb + 0.03f)));
            var zk = zb + rk;
            rk = Mathf.Max(0.0005f, 0.5f * (top(zk) - under(zk)));
            var yk = 0.5f * (top(zk) + under(zk));

            var pts = new List<Vector3>();
            // 下の面（後ろから前へ）
            for (var i = 0; i <= 3; i++)
            {
                var z = Mathf.Lerp(zk, zc, i / 3f);
                pts.Add(new Vector3(x, i == 0 ? yk - rk : i == 3 ? yn - rn : under(z), z));
            }
            // 前の丸み（下から上へ）
            for (var i = 1; i <= 4; i++)
            {
                var a = Mathf.Lerp(-90f, 90f, i / 4f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(x, yn + rn * Mathf.Sin(a), zc + rn * Mathf.Cos(a)));
            }
            // 上の面（前から後ろへ）
            var tops = new[] { 0.12f, 0.25f, 0.40f, 0.55f, 0.70f, 0.85f, 1f };
            foreach (var k in tops)
            {
                var z = Mathf.Lerp(zc, zk, k);
                pts.Add(new Vector3(x, k >= 1f ? yk + rk : top(z), z));
            }
            // 後ろの丸み（上から下へ）
            for (var i = 1; i <= 2; i++)
            {
                var a = Mathf.Lerp(90f, 270f, i / 2f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(x, yk + rk * Mathf.Sin(a), zk + rk * Mathf.Cos(a)));
            }
            return pts;
        }

        static void Seat(Shop shop)
        {
            shop.Begin("SeatCushion", 55f, true);
            CushionGrid(shop, SeatShape, new List<float> { 0f, 0.07f, 0.14f, 0.19f, SeatHalf - SeatEdge, 0.232f, 0.246f, 0.257f, 0.265f, SeatHalf });
            shop.End(Orient.Whole);
        }

        /// <summary>
        /// クッションの皮を張る。輪は左の端（畳んで閉じる）から右の端まで、x の増える向きに並べる（halves は真ん中から右の端までの |x|）。
        /// 革の地は縦横とも繰り返すので、uv は x と輪郭に沿った長さから振る
        /// </summary>
        static void CushionGrid(Shop shop, Cushion c, List<float> halves)
        {
            var xs = new List<float>();
            for (var i = halves.Count - 1; i >= 1; i--) xs.Add(-halves[i]);
            xs.AddRange(halves);
            var rings = new List<List<Vector3>> { CushionRing(c, -c.Half, true) };
            foreach (var x in xs) rings.Add(CushionRing(c, x, false));
            rings.Add(CushionRing(c, c.Half, true));
            Rings(shop, Shop.Upholstery, rings, (i, arc) => GrainUv(rings[i][0].x, arc));
        }

        /// <summary>同じ点の数の輪を並べて格子を張る。uv は（輪の番号, 輪郭に沿った長さ）から</summary>
        static void Rings(Shop shop, int sub, List<List<Vector3>> rings, Func<int, float, Vector2> uv)
        {
            var n = rings[0].Count;
            var idx = new int[rings.Count, n];
            for (var i = 0; i < rings.Count; i++)
            {
                var arc = 0f;
                for (var j = 0; j < n; j++)
                {
                    if (j > 0) arc += Vector3.Distance(rings[i][j - 1], rings[i][j]);
                    idx[i, j] = shop.V(rings[i][j], uv(i, arc));
                }
            }
            for (var i = 0; i < rings.Count - 1; i++)
                for (var j = 0; j < n - 1; j++)
                    shop.Q(sub, idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
        }

        // ---- 背もたれ ----------------------------------------------------------------

        static Vector3 BackUp { get { var r = Recline * Mathf.Deg2Rad; return new Vector3(0f, Mathf.Cos(r), -Mathf.Sin(r)); } }
        static Vector3 BackFwd { get { var r = Recline * Mathf.Deg2Rad; return new Vector3(0f, Mathf.Sin(r), Mathf.Cos(r)); } }

        /// <summary>背もたれの座標（x は横、s は倒れた向きに沿って下の端から、t は前の基準の面から前へ）を椅子の座標へ</summary>
        public static Vector3 Back(float x, float s, float t)
        {
            return BackBase + Vector3.right * x + BackUp * s + BackFwd * t;
        }

        /// <summary>背もたれのクッションの、高さ s での半分の幅。脇はまっすぐで、上の角と下の角を丸める</summary>
        static float BackW(float s)
        {
            if (s > BackLength - BackTopRound)
            {
                var k = s - (BackLength - BackTopRound);
                return BackHalfW - BackTopRound + Mathf.Sqrt(Mathf.Max(0f, BackTopRound * BackTopRound - k * k));
            }
            if (s < BackBottomRound)
            {
                var k = BackBottomRound - s;
                return BackHalfW - BackBottomRound + Mathf.Sqrt(Mathf.Max(0f, BackBottomRound * BackBottomRound - k * k));
            }
            return BackHalfW;
        }

        /// <summary>縁の丸み。縁からの離れ d で 0 から 1 へ、四分の一の円で立ち上がる</summary>
        static float Roll(float d)
        {
            var k = 1f - Mathf.Clamp01(d / CushionRoll);
            return Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
        }

        /// <summary>ボタンの位置（x, s）。段ごとに半分ずらしたダイヤ形（偶数の段は 4 つ、奇数の段は 3 つ）</summary>
        static List<Vector2> Buttons()
        {
            var list = new List<Vector2>();
            for (var r = 0; r < TuftRows; r++)
            {
                var s = TuftLow + r * TuftRow;
                if (r % 2 == 0) foreach (var k in new[] { -3, -1, 1, 3 }) list.Add(new Vector2(k * TuftStep, s));
                else foreach (var k in new[] { -2, 0, 2 }) list.Add(new Vector2(k * TuftStep, s));
            }
            return list;
        }

        static List<Vector2> tuftButtons;

        /// <summary>ボタン留めの窪み。ボタンの所を深く、隣り合う段のボタンを結ぶ斜めの折り目を浅く沈める</summary>
        static float Tuft(float x, float s)
        {
            if (tuftButtons == null) tuftButtons = Buttons();
            var p = new Vector2(x, s);
            var deep = 0f;
            foreach (var b in tuftButtons)
            {
                var d = (p - b).magnitude;
                deep = Mathf.Max(deep, 0.02f * Mathf.Exp(-Mathf.Pow(d / 0.016f, 2f)));
            }
            foreach (var a in tuftButtons)
                foreach (var b in tuftButtons)
                {
                    if (b.y - a.y < TuftRow * 0.5f || b.y - a.y > TuftRow * 1.5f || Mathf.Abs(Mathf.Abs(b.x - a.x) - TuftStep) > 0.001f) continue;
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    var d = (p - (a + ab * t)).magnitude;
                    deep = Mathf.Max(deep, 0.009f * Mathf.Exp(-Mathf.Pow(d / 0.009f, 2f)));
                }
            return deep;
        }

        /// <summary>背もたれのクッションの前の面の高さ（前の基準の面から前へ）。縁で殻の前の面まで丸めて下ろし、腰の所を少し膨らませ、ボタン留めで窪ませる</summary>
        static float BackFrontT(float x, float s)
        {
            var edge = Roll(BackW(s) - Mathf.Abs(x)) * Roll(s) * Roll(BackLength - s);
            var lumbar = 0.022f * Mathf.Exp(-Mathf.Pow((s - 0.21f) / 0.08f, 2f));
            return CushionBack + (CushionFront - CushionBack + lumbar - Tuft(x, s)) * edge;
        }

        /// <summary>
        /// 背もたれ。黒い漆の殻（縁がクッションの外へ 1.5 cm 出る）に、厚く柔らかい革のクッションを収めた重役の椅子の形。
        /// クッションの前はダイヤ形のボタン留め（7 段 25 個）。格子の線はボタンと折り目の中ほどを通るように置く（窪みが粗い画面でも読める）
        /// </summary>
        static void Backrest(Shop shop)
        {
            shop.Begin("BackCushion", 55f, true);
            var us = new List<float>();
            foreach (var e in new[] { -1f, -0.96f, -0.9f, -(BackHalfW - CushionRoll) / BackHalfW }) us.Add(e);
            for (var k = -6; k <= 6; k++) us.Add(k * TuftStep * 0.5f / BackHalfW);
            foreach (var e in new[] { (BackHalfW - CushionRoll) / BackHalfW, 0.9f, 0.96f, 1f }) us.Add(e);
            var ss = new List<float> { 0f, 0.012f, 0.03f, 0.055f, 0.085f };
            for (var j = 0; j <= 2 * (TuftRows - 1) + 2; j++) ss.Add(TuftLow - TuftRow * 0.5f + j * TuftRow * 0.5f);
            foreach (var s in new[] { 0.69f, 0.735f, 0.78f, BackLength - BackTopRound, 0.87f, 0.90f, 0.925f, 0.94f, BackLength }) ss.Add(s);
            var idx = new int[us.Count, ss.Count];
            for (var i = 0; i < us.Count; i++)
                for (var j = 0; j < ss.Count; j++)
                {
                    var s = ss[j];
                    var x = us[i] * BackW(s);
                    idx[i, j] = shop.V(Back(x, s, BackFrontT(x, s)), GrainUv(x, s));
                }
            for (var i = 0; i < us.Count - 1; i++)
                for (var j = 0; j < ss.Count - 1; j++)
                    shop.Q(Shop.Upholstery, idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
            shop.End(Orient.Toward, BackFwd);

            // ボタン（革を包んだ小さな釦）
            foreach (var b in Buttons())
                Disc(shop, "Button", Back(b.x, b.y, BackFrontT(b.x, b.y) + 0.0015f), BackFwd, 0.008f, 6, SwatchUv(Swatch.Rubber));

            // 殻。クッションの外形を縁の分だけ広げた角の丸い板。前の面（クッションの裏）から裏の面まで、縁を丸める
            shop.Begin("BackShell", 35f, true);
            var outline = ShellOutline();
            var layers = new[] { new Vector2(-0.012f, -0.055f), new Vector2(0f, -0.06f), new Vector2(0f, -0.088f), new Vector2(-0.008f, -BackThick) };
            var rings = new List<List<Vector3>>();
            foreach (var layer in layers)
            {
                var ring = new List<Vector3>();
                foreach (var o in outline)
                {
                    var n = o.Normal;
                    ring.Add(Back(o.P.x + n.x * layer.x, o.P.y + n.y * layer.x, layer.y));
                }
                rings.Add(ring);
            }
            var ids = new int[layers.Length, outline.Count];
            for (var i = 0; i < layers.Length; i++)
                for (var j = 0; j < outline.Count; j++)
                    ids[i, j] = shop.V(rings[i][j], Vector2.zero);
            for (var i = 0; i < layers.Length - 1; i++)
                for (var j = 0; j < outline.Count; j++)
                {
                    var j1 = (j + 1) % outline.Count;
                    shop.Q(Shop.Shell, ids[i, j], ids[i, j1], ids[i + 1, j1], ids[i + 1, j]);
                }
            // 前と裏の蓋（真ん中から扇に）
            foreach (var cap in new[] { 0, layers.Length - 1 })
            {
                var c = shop.V(Back(0f, 0.5f * BackLength, layers[cap].y), Vector2.zero);
                var capIds = new int[outline.Count];
                for (var j = 0; j < outline.Count; j++) capIds[j] = shop.V(rings[cap][j], Vector2.zero);
                for (var j = 0; j < outline.Count; j++) shop.T(Shop.Shell, c, capIds[j], capIds[(j + 1) % outline.Count]);
            }
            shop.End(Orient.Each);
        }

        struct OutlinePoint
        {
            /// <summary>(x, s)</summary>
            public Vector2 P;
            /// <summary>外向きの向き（x, s）</summary>
            public Vector2 Normal;
        }

        /// <summary>殻の外形（x, s。左回り）。クッションの外形を ShellLip だけ広げた角の丸い四角</summary>
        static List<OutlinePoint> ShellOutline()
        {
            var w = BackHalfW + ShellLip;
            var lo = -ShellLip;
            var hi = BackLength + ShellLip;
            var rt = BackTopRound + ShellLip;
            var rb = BackBottomRound + ShellLip;
            var list = new List<OutlinePoint>();
            Action<Vector2, float, float, float, int> arc = (c, r, a0, a1, n) =>
            {
                for (var i = 0; i < n; i++)
                {
                    var a = Mathf.Lerp(a0, a1, (float)i / n) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    list.Add(new OutlinePoint { P = c + d * r, Normal = d });
                }
            };
            arc(new Vector2(w - rb, lo + rb), rb, -90f, 0f, 3);
            list.Add(new OutlinePoint { P = new Vector2(w, 0.35f), Normal = Vector2.right });
            arc(new Vector2(w - rt, hi - rt), rt, 0f, 90f, 6);
            arc(new Vector2(-(w - rt), hi - rt), rt, 90f, 180f, 6);
            list.Add(new OutlinePoint { P = new Vector2(-w, 0.35f), Normal = Vector2.left });
            arc(new Vector2(-(w - rb), lo + rb), rb, 180f, 270f, 3);
            return list;
        }

        /// <summary>背もたれの頭の所に留めた、柔らかい革の頭の当て物</summary>
        static void Pillows(Shop shop)
        {
            // 頭の後ろ（前へ 0.33 以上）までは 25 cm ほど空く。座ったまま背を預けて天井を仰ぐ姿勢の目もこれより前
            Pillow(shop, "HeadPillow", 0.835f, 0.09f, 0.17f, 0.055f);
        }

        static void Pillow(Shop shop, string name, float sMid, float sHalf, float xHalf, float thick)
        {
            shop.Begin(name, 60f, true);
            const int nx = 8, ns = 6;
            var idx = new int[nx + 1, ns + 1];
            for (var i = 0; i <= nx; i++)
                for (var j = 0; j <= ns; j++)
                {
                    var u = Mathf.Lerp(-1f, 1f, (float)i / nx);
                    var v = Mathf.Lerp(-1f, 1f, (float)j / ns);
                    var s = sMid + v * sHalf;
                    var x = u * xHalf;
                    var puff = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 4f))) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(v), 4f)));
                    idx[i, j] = shop.V(Back(x, s, BackFrontT(x, s) - 0.004f + thick * puff), GrainUv(x, s));
                }
            for (var i = 0; i < nx; i++)
                for (var j = 0; j < ns; j++)
                    shop.Q(Shop.Upholstery, idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
            // 裏は背もたれに当たって見えないので張らない。向きは前（BackFwd）
            shop.End(Orient.Toward, BackFwd);
        }

        // ---- 肘掛け ----------------------------------------------------------------

        /// <summary>肘掛けの当て物の断面（前後の位置 z）。内の下から、内の脇、上の角の丸み、外の脇、外の下、下の面</summary>
        static List<Vector3> PadRing(float sx, float z, bool collapse)
        {
            const float end = 0.03f;
            var d = 0f;
            if (z < PadBack + end) d = end - Mathf.Sqrt(Mathf.Max(0f, end * end - Mathf.Pow(PadBack + end - z, 2f)));
            if (z > PadFront - end) d = end - Mathf.Sqrt(Mathf.Max(0f, end * end - Mathf.Pow(z - (PadFront - end), 2f)));
            var top = PadTop(z);
            var bottom = top - PadThick;
            if (collapse) d = 0.5f * PadThick;
            var xi = PadInner + d;
            var xo = PadOuter - d;
            var yt = top - d * 0.9f;
            var yb = bottom + d;
            if (collapse) { xi = xo = 0.5f * (PadInner + PadOuter); yt = yb = 0.5f * (top + bottom); }
            const float r = 0.018f;
            var rr = Mathf.Min(r, 0.5f * Mathf.Max(0.0005f, yt - yb), 0.5f * Mathf.Max(0.0005f, xo - xi));
            var pts = new List<Vector3>();
            pts.Add(new Vector3(xi + 0.004f, yb, z));
            pts.Add(new Vector3(xi, Mathf.Lerp(yb, yt - rr, 0.5f), z));
            for (var i = 0; i <= 3; i++)
            {
                var a = Mathf.Lerp(180f, 90f, i / 3f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(xi + rr + rr * Mathf.Cos(a), yt - rr + rr * Mathf.Sin(a), z));
            }
            pts.Add(new Vector3(0.5f * (xi + xo), yt + 0.002f, z));
            for (var i = 0; i <= 3; i++)
            {
                var a = Mathf.Lerp(90f, 0f, i / 3f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(xo - rr + rr * Mathf.Cos(a), yt - rr + rr * Mathf.Sin(a), z));
            }
            pts.Add(new Vector3(xo, Mathf.Lerp(yb, yt - rr, 0.5f), z));
            pts.Add(new Vector3(xo - 0.004f, yb, z));
            pts.Add(new Vector3(xi + 0.004f, yb, z));
            for (var i = 0; i < pts.Count; i++) pts[i] = new Vector3(sx * pts[i].x, pts[i].y, pts[i].z);
            return pts;
        }

        /// <summary>
        /// 肘掛け。丸みのある革の当て物を、黒い漆の台に載せ、磨いた金属の腕が座の皿の下から支える。
        /// 前の端の外の脇に、後から金具で留めた操作盤（黒い金属の箱と、当て物の下から回り込む金属の留め具）。操作盤のケーブルは腕に沿って座の下へ
        /// </summary>
        static void Arm(Shop shop, float sx)
        {
            var name = sx > 0f ? "R" : "L";
            // 当て物
            shop.Begin("ArmPad" + name, 55f, true);
            var zs = new[] { PadBack + 0.006f, PadBack + 0.016f, PadBack + 0.03f, -0.12f, -0.05f, 0.02f, 0.09f, 0.16f, PadFront - 0.03f, PadFront - 0.014f, PadFront - 0.005f };
            var rings = new List<List<Vector3>>();
            rings.Add(PadRing(sx, PadBack, true));
            foreach (var z in zs) rings.Add(PadRing(sx, z, false));
            rings.Add(PadRing(sx, PadFront, true));
            Rings(shop, Shop.Upholstery, rings, (i, arc) => GrainUv(arc, rings[i][0].z));
            shop.End(Orient.Whole);

            // 当て物の下の黒い漆の台
            var body = new Vector3[8];
            var zb = PadBack + 0.012f;
            var zf = PadFront - 0.02f;
            var inner = sx * (PadInner + 0.008f);
            var outer = sx * (PadOuter - 0.004f);
            Func<float, float> under = z => PadTop(z) - PadThick - 0.012f;
            Func<float, float> over = z => PadTop(z) - PadThick + 0.004f;
            body[0] = new Vector3(inner, under(zb), zb); body[1] = new Vector3(outer, under(zb), zb);
            body[2] = new Vector3(outer, under(zf), zf); body[3] = new Vector3(inner, under(zf), zf);
            body[4] = new Vector3(inner, over(zb), zb); body[5] = new Vector3(outer, over(zb), zb);
            body[6] = new Vector3(outer, over(zf), zf); body[7] = new Vector3(inner, over(zf), zf);
            Hexa(shop, "ArmBase" + name, body, Shop.Shell, null, true);

            // 磨いた金属の腕（当て物の下から後ろへ下り、座の皿の下へ入る）
            var support = new List<Vector3>
            {
                new Vector3(sx * 0.30f, 0.605f, 0.07f),
                new Vector3(sx * 0.305f, 0.565f, 0.0f),
                new Vector3(sx * 0.30f, 0.49f, -0.075f),
                new Vector3(sx * 0.27f, 0.435f, -0.11f),
                new Vector3(sx * 0.20f, 0.418f, -0.11f),
            };
            Tube(shop, "ArmSupport" + name, Shop.Chrome, Spline(support, 8), 0.014f, 6, false, Vector2.zero, 65f);

            // 前の操作盤。後ろの低い縁から前の高い縁へ上がる斜めの面を、座った目の方（後ろ・上・内）へ向ける
            var pod = new Vector3[8];
            var pi = sx * PodInner;
            var po = sx * PodOuter;
            const float podUnder = 0.607f;
            pod[0] = new Vector3(pi, podUnder, PodBack); pod[1] = new Vector3(po, podUnder, PodBack);
            pod[2] = new Vector3(po, podUnder, PodFront - 0.03f); pod[3] = new Vector3(pi, podUnder, PodFront - 0.03f);
            pod[4] = new Vector3(pi, 0.645f, PodBack); pod[5] = new Vector3(po, 0.662f, PodBack);
            pod[6] = new Vector3(po, 0.716f, PodFront); pod[7] = new Vector3(pi, 0.70f, PodFront);
            var faces = new int[] { Shop.Frame, Shop.Panel, Shop.Frame, Shop.Frame, Shop.Frame, Shop.Frame };
            var podUvs = new Rect?[] { null, PanelUv(sx > 0f ? RightPod : LeftPod), null, null, null, null };
            Hexa(shop, "ArmPod" + name, pod, faces, podUvs, true, sx < 0f);
            // 留め具。当て物の下を横切る板と、当て物の外の脇に立つ板（L の形）で操作盤を抱える
            Slab(shop, "PodClampUnder" + name, Shop.Chrome, new Vector3(sx * 0.355f, 0.603f, 0.23f), Quaternion.identity, new Vector3(0.15f, 0.006f, 0.06f), 0.002f, true);
            Slab(shop, "PodClampSide" + name, Shop.Chrome, new Vector3(sx * 0.3485f, 0.629f, 0.23f), Quaternion.identity, new Vector3(0.005f, 0.052f, 0.05f), 0.0015f, true);
            // 留めた螺子（操作盤の外の面に二つ）
            foreach (var z in new[] { 0.215f, 0.27f })
                Disc(shop, "PodScrew" + name, new Vector3(sx * (PodOuter + 0.0008f), 0.63f, z), new Vector3(sx, 0f, 0f), 0.004f, 6, SwatchUv(Swatch.Metal));
        }

        /// <summary>右の肘掛けの差込口。革に螺子で留めた金属の角の板に、金属の縁、黒い穴、穴を囲む紫の灯り（ジャックを挿す所の合図）</summary>
        static void Port(Shop shop)
        {
            var c = PortAt;
            Slab(shop, "PortPlate", Shop.Chrome, new Vector3(c.x, PadTop(c.z) + 0.0025f, c.z), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.056f, 0.004f, 0.056f), 0.0015f, true);
            Tube(shop, "PortBezel", Shop.Chrome, new List<Vector3> { new Vector3(c.x, 0.648f, c.z), new Vector3(c.x, 0.6725f, c.z) }, 0.022f, 10, true, Vector2.zero, 70f);
            Ring(shop, "PortGlow", new Vector3(c.x, 0.6735f, c.z), Vector3.up, 0.0145f, 0.0195f, 12, SwatchUv(Swatch.VioletLit));
            Disc(shop, "PortHole", new Vector3(c.x, 0.6740f, c.z), Vector3.up, 0.0135f, 10, SwatchUv(Swatch.Rubber));
        }

        // ---- 座の下 ------------------------------------------------------------------

        static void Undercarriage(Shop shop)
        {
            // 座の皿（黒い漆）
            Slab(shop, "SeatPan", Shop.Shell, new Vector3(0f, 0.4175f, -0.03f), Quaternion.identity, new Vector3(0.53f, 0.035f, 0.40f), 0.012f, true);
            // リクライニングの機構の箱
            Slab(shop, "Mechanism", Shop.Frame, new Vector3(0f, 0.372f, -0.02f), Quaternion.identity, new Vector3(0.24f, 0.055f, 0.25f), 0.01f, true);
            // 右はリクライニングのレバー、左は高さのレバー。先に黒い握り
            Tube(shop, "LeverR", Shop.Chrome, new List<Vector3> { new Vector3(0.11f, 0.366f, 0.04f), new Vector3(0.19f, 0.360f, 0.058f), new Vector3(0.232f, 0.355f, 0.072f) }, 0.006f, 6, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripR", Shop.Panel, new Vector3(0.25f, 0.354f, 0.078f), Quaternion.Euler(0f, 18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0.005f, true, SwatchUv(Swatch.Rubber));
            Tube(shop, "LeverL", Shop.Chrome, new List<Vector3> { new Vector3(-0.11f, 0.366f, 0.0f), new Vector3(-0.19f, 0.360f, 0.018f), new Vector3(-0.232f, 0.355f, 0.032f) }, 0.006f, 6, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripL", Shop.Panel, new Vector3(-0.25f, 0.354f, 0.038f), Quaternion.Euler(0f, -18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0.005f, true, SwatchUv(Swatch.Rubber));
            // 前の張りの摘み
            Tube(shop, "TensionKnob", Shop.Panel, new List<Vector3> { new Vector3(0f, 0.35f, 0.105f), new Vector3(0f, 0.35f, 0.135f) }, 0.026f, 10, true, SwatchUv(Swatch.Rubber), 70f);

            // 背もたれを支える左右の磨いた金属の腕（座の下から、クッションの後ろを回って背の殻へ）と、倒れの軸の蓋
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.13f, 0.385f, -0.15f),
                    new Vector3(sx * 0.13f, 0.405f, -0.245f),
                    new Vector3(sx * 0.13f, 0.43f, -0.268f),
                    Back(sx * 0.13f, 0.13f, -BackThick + 0.004f),
                };
                Tube(shop, "ReclineArm" + (sx > 0f ? "R" : "L"), Shop.Chrome, Spline(path, 8), 0.012f, 6, false, Vector2.zero, 70f);
                Tube(shop, "ReclineHub" + (sx > 0f ? "R" : "L"), Shop.Chrome, new List<Vector3> { new Vector3(sx * 0.115f, 0.418f, -0.26f), new Vector3(sx * 0.152f, 0.418f, -0.26f) }, 0.022f, 10, true, Vector2.zero, 70f);
            }

            // ガスシリンダー。磨いた覆いと芯
            TubeR(shop, "ColumnCover", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.235f, 0f) }, new List<float> { 0.046f, 0.038f }, 12, true, Vector2.zero, 70f);
            Tube(shop, "ColumnRod", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.225f, 0f), new Vector3(0f, 0.345f, 0f) }, 0.024f, 10, false, Vector2.zero, 70f);
            Tube(shop, "ColumnCollar", Shop.Frame, new List<Vector3> { new Vector3(0f, 0.328f, 0f), new Vector3(0f, 0.346f, 0f) }, 0.04f, 10, true, Vector2.zero, 70f);
        }

        // ---- 五本脚とキャスター --------------------------------------------------------

        static void Base(Shop shop)
        {
            Tube(shop, "Hub", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.075f, 0f), new Vector3(0f, 0.132f, 0f) }, 0.056f, 12, true, Vector2.zero, 70f);
            for (var k = 0; k < 5; k++)
            {
                // 一本は真後ろ。前の二本は左右へ 36 度（机の側へ脚を突き出さない）。磨いた金属
                var a = (180f + 72f * k) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var side = Vector3.Cross(Vector3.up, dir);
                shop.Begin("Leg" + k, 40f, true);
                var rings = new List<List<Vector3>>();
                foreach (var r in new[] { 0.03f, 0.13f, 0.22f, LegReach })
                {
                    var k2 = Mathf.InverseLerp(0.03f, LegReach, r);
                    var w = Mathf.Lerp(0.026f, 0.017f, k2);
                    var yt = Mathf.Lerp(0.128f, 0.086f, k2);
                    var yb = Mathf.Lerp(0.084f, 0.062f, k2);
                    var c = dir * r;
                    var ring = new List<Vector3>
                    {
                        c - side * w * 0.8f + Vector3.up * yb,
                        c + side * w * 0.8f + Vector3.up * yb,
                        c + side * w + Vector3.up * Mathf.Lerp(yb, yt, 0.55f),
                        c + side * w * 0.6f + Vector3.up * yt,
                        c - side * w * 0.6f + Vector3.up * yt,
                        c - side * w + Vector3.up * Mathf.Lerp(yb, yt, 0.55f),
                    };
                    ring.Add(ring[0]);
                    rings.Add(ring);
                }
                Rings(shop, Shop.Chrome, rings, (i, arc) => Vector2.zero);
                // 先の蓋
                var tip = rings[rings.Count - 1];
                var ci = shop.V((tip[0] + tip[1] + tip[2] + tip[3] + tip[4] + tip[5]) / 6f, Vector2.zero);
                var ids = new int[6];
                for (var i = 0; i < 6; i++) ids[i] = shop.V(tip[i], Vector2.zero);
                for (var i = 0; i < 6; i++) shop.T(Shop.Chrome, ci, ids[i], ids[(i + 1) % 6]);
                shop.End(Orient.Each);

                // キャスター。黒い覆いと、並んだ二つの車
                var at = dir * LegReach;
                Slab(shop, "CasterHood" + k, Shop.Shell, at + Vector3.up * 0.046f, Quaternion.LookRotation(dir), new Vector3(0.034f, 0.03f, 0.05f), 0.008f, true);
                foreach (var s in new[] { -1f, 1f })
                {
                    var hub = at + side * (0.017f * s) + Vector3.up * 0.026f;
                    Tube(shop, "Wheel" + k + (s > 0 ? "a" : "b"), Shop.Panel, new List<Vector3> { hub - side * 0.007f, hub + side * 0.007f }, 0.026f, 8, true, SwatchUv(Swatch.Rubber), 50f);
                }
            }
        }

        // ---- フットレスト ------------------------------------------------------------

        /// <summary>フットレストのパッドの半分の幅、前後の端、厚み</summary>
        const float FootHalf = 0.20f;
        const float FootBack = 0.20f;
        const float FootFront = 0.445f;
        const float FootThick = 0.045f;
        /// <summary>引き出す腕の左右の位置（脛と足の外の縁 |x| 0.178 より 2.5 cm 外）と、腕がパッドの脇に付く前後の位置</summary>
        const float FootArmX = 0.216f;
        const float FootPivot = 0.30f;
        /// <summary>引き出したレールの先（腕の上の軸）。座の皿の下の収め口から前へ出る</summary>
        static readonly Vector3 FootRailEnd = new Vector3(FootArmX, 0.391f, 0.19f);

        static float FootPadTop(float x, float z) { return FootTop(z); }
        static float FootPadUnder(float x, float z) { return FootTop(z) - FootThick; }

        static readonly Cushion FootShape = new Cushion
        {
            Top = FootPadTop, Under = FootPadUnder, Front = FootFront, Back = FootBack, Half = FootHalf, Edge = 0.02f, Corner = 0.03f, MidZ = 0.32f,
        };

        /// <summary>
        /// 座の前の下から引き出すフットレスト（リクライニングの椅子の脚置きの形）。座の皿の下の収め口から左右の磨いた金属のレールが前へ出て、
        /// 先の軸から磨いた金属の腕が下りてパッドの脇を支える。机との間が狭いので伸ばしきらず、座った形の足の裏が乗る所（床の近く）まで下ろした形。
        /// パッドは座面と同じ厚い革のクッション
        /// </summary>
        static void Footrest(Shop shop)
        {
            shop.Begin("FootPad", 55f, true);
            CushionGrid(shop, FootShape, new List<float> { 0f, 0.07f, 0.13f, FootHalf - 0.02f, 0.19f, 0.197f, FootHalf });
            shop.End(Orient.Whole);
            // 座の皿の下の収め口
            Slab(shop, "FootHousing", Shop.Shell, new Vector3(0f, 0.391f, 0.095f), Quaternion.identity, new Vector3(0.47f, 0.018f, 0.15f), 0.005f, true);
            foreach (var sx in new[] { 1f, -1f })
            {
                var n = sx > 0f ? "R" : "L";
                var top = new Vector3(sx * FootRailEnd.x, FootRailEnd.y, FootRailEnd.z);
                // 引き出したレール
                Slab(shop, "FootRail" + n, Shop.Chrome, new Vector3(top.x, top.y, 0.14f), Quaternion.identity, new Vector3(0.022f, 0.016f, 0.10f), 0.004f, true);
                var pad = new Vector3(top.x, FootTop(FootPivot) - 0.5f * FootThick, FootPivot);
                var seg = pad - top;
                Tube(shop, "FootKnuckle" + n, Shop.Frame, new List<Vector3> { top - Vector3.right * 0.013f, top + Vector3.right * 0.013f }, 0.013f, 8, true, Vector2.zero, 50f);
                Slab(shop, "FootArm" + n, Shop.Chrome, 0.5f * (top + pad), Quaternion.LookRotation(seg.normalized, Vector3.forward), new Vector3(0.014f, 0.03f, seg.magnitude), 0.005f, true);
                Tube(shop, "FootPivot" + n, Shop.Frame, new List<Vector3> { new Vector3(sx * 0.197f, pad.y, pad.z), new Vector3(sx * 0.229f, pad.y, pad.z) }, 0.012f, 8, true, Vector2.zero, 50f);
            }
        }

        // ---- 頭の後ろの端末 ------------------------------------------------------------

        /// <summary>
        /// 後から取り付けた潜行の装置。背もたれの裏の頭の所に黒い金属の箱（裏へ向いた画面・灯りの列・接続口と挿さった三本の端子）を、
        /// 背もたれの上の縁を越えて前へ掛かる二本の磨いた金属の留め具で留める。箱の脇から磨いた金属の腕を背もたれの縁の外へ回し、
        /// 頭の当て物の左右に耳の筐体（右は前を向いた小さな画面、左は灯りの輪）を出す。
        /// どれも頭の後ろ（前へ 0.33 m より後ろ）にあり、座った目と、背を預けて天井を仰ぐ目の後ろに来る
        /// </summary>
        static void Crown(Shop shop)
        {
            var rot = Quaternion.LookRotation(BackFwd, BackUp);
            const float tOut = -0.165f;
            const float earX = 0.275f;
            foreach (var sx in new[] { 1f, -1f })
            {
                var n = sx > 0f ? "R" : "L";
                var c = Back(sx * earX, 0.855f, 0.0f);
                Slab(shop, "EarPod" + n, Shop.Frame, c, rot, new Vector3(0.058f, 0.15f, 0.10f), 0.012f, true);
                var face = Back(sx * earX, 0.855f, 0.0505f);
                if (sx > 0f)
                    Decal(shop, "EarScreen", face, Vector3.right * 0.021f, BackUp * 0.056f, BackFwd, PanelUv(EarScreen));
                else
                    Decal(shop, "EarLamp", face + BackUp * 0.02f, Vector3.right * 0.021f, BackUp * 0.021f, BackFwd, PanelUv(EarLamp));
                // 外の面の灯りの列（縦）
                Decal(shop, "EarLeds" + n, Back(sx * (earX + 0.0295f), 0.855f, 0.0f), BackUp * 0.05f, BackFwd * 0.0075f, Vector3.right * sx, PanelUv(LedStrip));
                // 箱の脇から背もたれの縁の外を回って耳の筐体の裏へ入る腕
                var arm = new List<Vector3>
                {
                    Back(sx * 0.12f, 0.87f, -0.13f),
                    Back(sx * 0.215f, 0.868f, -0.118f),
                    Back(sx * 0.262f, 0.864f, -0.098f),
                    Back(sx * 0.273f, 0.86f, -0.07f),
                    Back(sx * earX, 0.858f, -0.045f),
                };
                Tube(shop, "EarMount" + n, Shop.Chrome, Spline(arm, 8), 0.009f, 6, false, Vector2.zero, 60f);
            }

            // 裏の箱（角を面取りした黒い金属の箱。前の面は背の殻に 4 mm 埋める）
            Slab(shop, "CrownBox", Shop.Frame, Back(0f, 0.795f, 0.5f * (tOut - BackThick + 0.004f)), rot, new Vector3(0.25f, 0.27f, -BackThick + 0.004f - tOut), 0.016f, true);
            // 背もたれの上の縁を越えて前へ掛かる留め具（二本）
            foreach (var x in new[] { -0.08f, 0.08f })
            {
                // 殻の上の縁（s 0.965）と、クッションの丸めた上の縁に沿わせる
                var strap = new List<Vector3>
                {
                    Back(x, 0.928f, tOut - 0.002f),
                    Back(x, 0.955f, -0.14f),
                    Back(x, 0.972f, -0.10f),
                    Back(x, 0.973f, -0.06f),
                    Back(x, 0.957f, -0.028f),
                    Back(x, 0.941f, 0.005f),
                    Back(x, 0.927f, 0.022f),
                };
                Tube(shop, "CrownStrap", Shop.Chrome, Spline(strap, 8), 0.006f, 6, false, Vector2.zero, 60f);
            }
            var rear = -BackFwd;
            var face2 = tOut - 0.0008f;
            Decal(shop, "CrownScreen", Back(0f, 0.838f, face2), Vector3.right * 0.075f, BackUp * 0.05f, rear, PanelUv(RearScreen));
            Decal(shop, "CrownLeds", Back(0f, 0.899f, face2), Vector3.right * 0.08f, BackUp * 0.007f, rear, PanelUv(LedStrip));
            Decal(shop, "CrownPorts", Back(0f, 0.72f, face2), Vector3.right * 0.085f, BackUp * 0.043f, rear, PanelUv(PortPlate));
            // 四隅の螺子
            foreach (var x in new[] { -0.105f, 0.105f })
                foreach (var s in new[] { 0.685f, 0.905f })
                    Disc(shop, "CrownScrew", Back(x, s, face2 - 0.0002f), rear, 0.005f, 6, SwatchUv(Swatch.Metal));
            // 挿さった三本の端子（金属の胴と、色の付いた根元）
            var colours = new[] { Swatch.Grey, Swatch.Rubber, Swatch.Violet };
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * 0.05f;
                Tube(shop, "Plug" + i, Shop.Panel, new List<Vector3> { Back(x, 0.715f, tOut + 0.004f), Back(x, 0.715f, tOut - 0.024f) }, 0.011f, 8, true, SwatchUv(Swatch.Metal), 50f);
                Tube(shop, "PlugBoot" + i, Shop.Panel, new List<Vector3> { Back(x, 0.715f, tOut - 0.024f), Back(x, 0.715f, tOut - 0.040f) }, 0.0085f, 8, true, SwatchUv(colours[i]), 50f);
            }

            // 腰の後ろの分岐の箱
            const float jOut = -0.155f;
            Slab(shop, "Junction", Shop.Frame, Back(0f, 0.23f, 0.5f * (jOut - BackThick + 0.004f)), rot, new Vector3(0.17f, 0.135f, -BackThick + 0.004f - jOut), 0.012f, true);
            Decal(shop, "JunctionFace", Back(0f, 0.231f, jOut - 0.0008f), Vector3.right * 0.07f, BackUp * 0.045f, rear, PanelUv(JunctionFace));
            // 背の半ばでケーブルを束ねる留め具
            Slab(shop, "CableClip", Shop.Frame, Back(0f, 0.452f, -0.114f), rot, new Vector3(0.13f, 0.024f, 0.036f), 0.006f, true);
        }

        /// <summary>
        /// 後から這わせたケーブル。裏の箱の端子三本は背の半ばの留め具を通って腰の分岐の箱へ、耳の筐体の二本は背もたれの縁を回って裏の箱の脇へ。
        /// 分岐の箱から太い一本が座の下の機構へ、もう一本が右の肘掛けの金属の腕へ（腕に沿って差込口へ通る）。
        /// 左右の肘掛けの操作盤からも一本ずつ、当て物の下と金属の腕に沿って座の下へ
        /// </summary>
        static void Wiring(Shop shop)
        {
            const float tOut = -0.165f;
            var plugs = new[]
            {
                new { x = -0.05f, r = 0.006f, c = Swatch.Grey },
                new { x = 0f, r = 0.0085f, c = Swatch.Rubber },
                new { x = 0.05f, r = 0.0065f, c = Swatch.Violet },
            };
            for (var i = 0; i < 3; i++)
            {
                var p = plugs[i];
                var sway = (i - 1) * 0.028f;
                var path = new List<Vector3>
                {
                    Back(p.x, 0.715f, tOut - 0.038f),
                    Back(p.x * 1.1f, 0.70f, tOut - 0.056f),
                    Back(p.x * 1.4f + sway * 0.3f, 0.62f, -0.19f),
                    Back(p.x * 1.2f + sway, 0.53f, -0.155f),
                    Back(p.x * 0.7f, 0.452f, -0.123f),
                    Back(p.x * 0.7f, 0.39f, -0.128f),
                    Back(p.x * 0.8f, 0.325f, -0.142f),
                    Back(p.x * 0.8f, 0.285f, -0.130f),
                };
                Tube(shop, "Cable" + i, Shop.Panel, Spline(path, 16), p.r, 6, false, SwatchUv(p.c), 65f);
            }
            // 耳の筐体から裏の箱の脇へ
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    Back(sx * 0.284f, 0.83f, -0.04f),
                    Back(sx * 0.281f, 0.815f, -0.085f),
                    Back(sx * 0.262f, 0.80f, -0.112f),
                    Back(sx * 0.20f, 0.785f, -0.128f),
                    Back(sx * 0.125f, 0.77f, -0.13f),
                };
                Tube(shop, "EarCable" + (sx > 0f ? "R" : "L"), Shop.Panel, Spline(path, 10), 0.0055f, 6, false, SwatchUv(sx > 0f ? Swatch.Grey : Swatch.Teal), 65f);
            }
            // 分岐の箱から座の下の機構へ（太い一本）
            var down = new List<Vector3>
            {
                Back(0f, 0.18f, -0.14f),
                Back(0f, 0.12f, -0.162f),
                new Vector3(0f, 0.50f, -0.34f),
                new Vector3(0f, 0.42f, -0.30f),
                new Vector3(0f, 0.385f, -0.21f),
                new Vector3(0f, 0.378f, -0.13f),
            };
            Tube(shop, "TrunkCable", Shop.Panel, Spline(down, 16), 0.011f, 6, false, SwatchUv(Swatch.Rubber), 65f);
            // 分岐の箱から右の肘掛けの金属の腕へ（腕に沿って差込口まで通る）
            var arm = new List<Vector3>
            {
                Back(0.08f, 0.215f, -0.13f),
                Back(0.15f, 0.20f, -0.145f),
                new Vector3(0.27f, 0.56f, -0.30f),
                new Vector3(0.33f, 0.50f, -0.17f),
                new Vector3(0.318f, 0.475f, -0.085f),
            };
            Tube(shop, "ArmCable", Shop.Panel, Spline(arm, 14), 0.0065f, 6, false, SwatchUv(Swatch.Violet), 65f);
            // 肘掛けの操作盤から、当て物の下と金属の腕に沿って座の下へ
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.40f, 0.625f, PodBack + 0.002f),
                    new Vector3(sx * 0.395f, 0.598f, 0.16f),
                    new Vector3(sx * 0.33f, 0.588f, 0.06f),
                    new Vector3(sx * 0.322f, 0.56f, -0.02f),
                    new Vector3(sx * 0.318f, 0.49f, -0.08f),
                    new Vector3(sx * 0.285f, 0.44f, -0.12f),
                    new Vector3(sx * 0.22f, 0.42f, -0.13f),
                };
                Tube(shop, "PodCable" + (sx > 0f ? "R" : "L"), Shop.Panel, Spline(path, 14), 0.005f, 6, false, SwatchUv(sx > 0f ? Swatch.Violet : Swatch.Grey), 65f);
            }
        }

        // ---- 道具 ------------------------------------------------------------------

        static Rect SwatchRect(Swatch s)
        {
            var uv = SwatchUv(s);
            var e = 0.5f / PanelSize;
            return new Rect(uv.x - e, uv.y - e, 2f * e, 2f * e);
        }

        /// <summary>Catmull-Rom で点の間を刻む。端の点は通る</summary>
        static List<Vector3> Spline(List<Vector3> pts, int steps)
        {
            var o = new List<Vector3>();
            var n = pts.Count;
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps * (n - 1);
                var k = Mathf.Min(Mathf.FloorToInt(t), n - 2);
                var f = t - k;
                var p0 = pts[Mathf.Max(0, k - 1)];
                var p1 = pts[k];
                var p2 = pts[k + 1];
                var p3 = pts[Mathf.Min(n - 1, k + 2)];
                var f2 = f * f;
                var f3 = f2 * f;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f2 + (-p0 + 3f * p1 - 3f * p2 + p3) * f3));
            }
            return o;
        }

        /// <summary>筒。path に沿って輪を張る（輪の向きは前の輪から滑らかに運ぶ）。uv はどの点も同じ（色の升）</summary>
        static void Tube(Shop shop, string name, int sub, List<Vector3> path, float radius, int sides, bool caps, Vector2 uv, float crease)
        {
            var radii = new List<float>();
            foreach (var p in path) radii.Add(radius);
            TubeR(shop, name, sub, path, radii, sides, caps, uv, crease);
        }

        static void TubeR(Shop shop, string name, int sub, List<Vector3> path, List<float> radii, int sides, bool caps, Vector2 uv, float crease)
        {
            shop.Begin(name, crease, false);
            var n = path.Count;
            var tangents = new Vector3[n];
            for (var i = 0; i < n; i++)
                tangents[i] = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            var normal = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var idx = new int[n, sides];
            for (var i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    normal = (normal - Vector3.Dot(normal, tangents[i]) * tangents[i]).normalized;
                }
                var bin = Vector3.Cross(tangents[i], normal);
                for (var s = 0; s < sides; s++)
                {
                    var a = (float)s / sides * Mathf.PI * 2f;
                    idx[i, s] = shop.V(path[i] + (normal * Mathf.Cos(a) + bin * Mathf.Sin(a)) * radii[i], uv);
                }
            }
            for (var i = 0; i < n - 1; i++)
                for (var s = 0; s < sides; s++)
                {
                    var s1 = (s + 1) % sides;
                    var centre = 0.5f * (path[i] + path[i + 1]);
                    shop.QOut(sub, idx[i, s], idx[i + 1, s], idx[i + 1, s1], idx[i, s1], centre);
                }
            if (caps)
            {
                foreach (var end in new[] { 0, n - 1 })
                {
                    var ids = new int[sides];
                    for (var s = 0; s < sides; s++) ids[s] = shop.V(shop.Pos(idx[end, s]), uv);
                    var c = shop.V(path[end], uv);
                    var outward = end == 0 ? -tangents[0] : tangents[n - 1];
                    for (var s = 0; s < sides; s++) shop.TFacing(sub, c, ids[s], ids[(s + 1) % sides], outward);
                }
            }
            shop.End(Orient.None);
        }

        /// <summary>丸い板（片面）。normal の向きに見える</summary>
        static void Disc(Shop shop, string name, Vector3 centre, Vector3 normal, float radius, int sides, Vector2 uv)
        {
            shop.Begin(name, 0f, false);
            var a = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var b = Vector3.Cross(normal, a);
            var c = shop.V(centre, uv);
            var ids = new int[sides];
            for (var s = 0; s < sides; s++)
            {
                var t = (float)s / sides * Mathf.PI * 2f;
                ids[s] = shop.V(centre + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * radius, uv);
            }
            for (var s = 0; s < sides; s++) shop.TFacing(Shop.Panel, c, ids[s], ids[(s + 1) % sides], normal);
            shop.End(Orient.None);
        }

        /// <summary>輪の板（片面）</summary>
        static void Ring(Shop shop, string name, Vector3 centre, Vector3 normal, float inner, float outer, int sides, Vector2 uv)
        {
            shop.Begin(name, 0f, false);
            var a = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var b = Vector3.Cross(normal, a);
            var ins = new int[sides];
            var outs = new int[sides];
            for (var s = 0; s < sides; s++)
            {
                var t = (float)s / sides * Mathf.PI * 2f;
                var d = a * Mathf.Cos(t) + b * Mathf.Sin(t);
                ins[s] = shop.V(centre + d * inner, uv);
                outs[s] = shop.V(centre + d * outer, uv);
            }
            for (var s = 0; s < sides; s++)
            {
                var s1 = (s + 1) % sides;
                shop.TFacing(Shop.Panel, ins[s], outs[s], outs[s1], normal);
                shop.TFacing(Shop.Panel, ins[s], outs[s1], ins[s1], normal);
            }
            shop.End(Orient.None);
        }

        /// <summary>絵を貼る板（片面）。halfRight は絵の横（u が増える向き）の半分、halfUp は縦の半分</summary>
        static void Decal(Shop shop, string name, Vector3 centre, Vector3 halfRight, Vector3 halfUp, Vector3 normal, Rect uv)
        {
            shop.Begin(name, 0f, false);
            var a = shop.V(centre - halfRight - halfUp, new Vector2(uv.xMin, uv.yMin));
            var b = shop.V(centre + halfRight - halfUp, new Vector2(uv.xMax, uv.yMin));
            var c = shop.V(centre + halfRight + halfUp, new Vector2(uv.xMax, uv.yMax));
            var d = shop.V(centre - halfRight + halfUp, new Vector2(uv.xMin, uv.yMax));
            shop.TFacing(Shop.Panel, a, b, c, normal);
            shop.TFacing(Shop.Panel, a, c, d, normal);
            shop.End(Orient.None);
        }

        /// <summary>角を面取りした箱。uv はどの面も swatch（色の升）か 0</summary>
        static void Slab(Shop shop, string name, int sub, Vector3 centre, Quaternion rot, Vector3 size, float bevel, bool solid, Vector2 uv = default(Vector2))
        {
            shop.Begin(name, 20f, solid);
            var h = size * 0.5f;
            var b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            Func<float, float, float, Vector3> P = (x, y, z) => centre + rot * new Vector3(x, y, z);
            var hs = new[] { h.x, h.y, h.z };
            Func<int, float, int, float, int, float, Vector3> A = (a0, v0, a1, v1, a2, v2) =>
            {
                var v = new float[3];
                v[a0] = v0; v[a1] = v1; v[a2] = v2;
                return P(v[0], v[1], v[2]);
            };
            // 6 つの面
            for (var ax = 0; ax < 3; ax++)
                foreach (var sg in new[] { -1f, 1f })
                {
                    var u = (ax + 1) % 3;
                    var w = (ax + 2) % 3;
                    var p0 = A(ax, sg * hs[ax], u, -(hs[u] - b), w, -(hs[w] - b));
                    var p1 = A(ax, sg * hs[ax], u, hs[u] - b, w, -(hs[w] - b));
                    var p2 = A(ax, sg * hs[ax], u, hs[u] - b, w, hs[w] - b);
                    var p3 = A(ax, sg * hs[ax], u, -(hs[u] - b), w, hs[w] - b);
                    shop.Q(sub, shop.V(p0, uv), shop.V(p1, uv), shop.V(p2, uv), shop.V(p3, uv));
                }
            // 12 の稜の面取り
            for (var a0 = 0; a0 < 3; a0++)
                for (var a1 = a0 + 1; a1 < 3; a1++)
                {
                    var w = 3 - a0 - a1;
                    foreach (var s0 in new[] { -1f, 1f })
                        foreach (var s1 in new[] { -1f, 1f })
                        {
                            var p0 = A(a0, s0 * hs[a0], a1, s1 * (hs[a1] - b), w, -(hs[w] - b));
                            var p1 = A(a0, s0 * hs[a0], a1, s1 * (hs[a1] - b), w, hs[w] - b);
                            var p2 = A(a0, s0 * (hs[a0] - b), a1, s1 * hs[a1], w, hs[w] - b);
                            var p3 = A(a0, s0 * (hs[a0] - b), a1, s1 * hs[a1], w, -(hs[w] - b));
                            shop.Q(sub, shop.V(p0, uv), shop.V(p1, uv), shop.V(p2, uv), shop.V(p3, uv));
                        }
                }
            // 8 つの角
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                    {
                        var p0 = P(sx * h.x, sy * (h.y - b), sz * (h.z - b));
                        var p1 = P(sx * (h.x - b), sy * h.y, sz * (h.z - b));
                        var p2 = P(sx * (h.x - b), sy * (h.y - b), sz * h.z);
                        shop.T(sub, shop.V(p0, uv), shop.V(p1, uv), shop.V(p2, uv));
                    }
            shop.End(Orient.Each);
        }

        static void Hexa(Shop shop, string name, Vector3[] c, int sub, Rect?[] uvs, bool solid)
        {
            Hexa(shop, name, c, new[] { sub, sub, sub, sub, sub, sub }, uvs, solid, false);
        }

        /// <summary>
        /// 八つの角の箱（平らな六つの面）。角は下の 0〜3（−x−z, +x−z, +x+z, −x+z）と上の 4〜7（同じ並び）。
        /// 面は 下・上・後ろ・前・左・右 の順に subs と uvs を当てる。上の面の絵は u が +x、v が +z。flipU なら u を逆に
        /// </summary>
        static void Hexa(Shop shop, string name, Vector3[] c, int[] subs, Rect?[] uvs, bool solid, bool flipU = false)
        {
            shop.Begin(name, 20f, solid);
            var faces = new[]
            {
                new[] { 0, 1, 2, 3 },
                new[] { 4, 5, 6, 7 },
                new[] { 0, 1, 5, 4 },
                new[] { 3, 2, 6, 7 },
                new[] { 0, 3, 7, 4 },
                new[] { 1, 2, 6, 5 },
            };
            for (var f = 0; f < 6; f++)
            {
                var r = uvs != null && uvs[f].HasValue ? uvs[f].Value : new Rect(0f, 0f, 0f, 0f);
                var q = faces[f];
                var u0 = flipU ? r.xMax : r.xMin;
                var u1 = flipU ? r.xMin : r.xMax;
                var ids = new[]
                {
                    shop.V(c[q[0]], new Vector2(u0, r.yMin)),
                    shop.V(c[q[1]], new Vector2(u1, r.yMin)),
                    shop.V(c[q[2]], new Vector2(u1, r.yMax)),
                    shop.V(c[q[3]], new Vector2(u0, r.yMax)),
                };
                shop.Q(subs[f], ids[0], ids[1], ids[2], ids[3]);
            }
            shop.End(Orient.Each);
        }

        // ---- 面を溜める入れ物 --------------------------------------------------------

        /// <summary>部品の面の向きの揃え方</summary>
        enum Orient
        {
            /// <summary>作った向きのまま</summary>
            None,
            /// <summary>部品の真ん中から外へ向く面が多くなるよう、部品ごと裏返す（丸く閉じた形）</summary>
            Whole,
            /// <summary>面ごとに、部品の真ん中から外へ向ける（凸の形）</summary>
            Each,
            /// <summary>部品ごと、決めた向き（dir）へ向ける</summary>
            Toward,
        }

        /// <summary>
        /// 椅子の面を溜める入れ物。部品（Begin〜End）ごとに、同じ位置の頂点の法線を、折れの角（crease）より緩い面どうしだけ均す。
        /// マテリアルごとの面の組（張り地・殻・金具・磨いた金属・操作盤の絵）に分けて一つの mesh に焼く
        /// </summary>
        sealed class Shop
        {
            public const int Upholstery = 0, Shell = 1, Frame = 2, Chrome = 3, Panel = 4, Count = 5;

            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> norms = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int>[] tris = new List<int>[Count];
            readonly List<int> part = new List<int>();
            int start;
            float crease;
            string name;
            bool solid;

            public readonly List<Piece> Pieces = new List<Piece>();
            public int TriangleCount { get; private set; }

            public Shop()
            {
                for (var i = 0; i < Count; i++) tris[i] = new List<int>();
            }

            public void Begin(string name, float crease, bool solid)
            {
                this.name = name;
                this.crease = crease;
                this.solid = solid;
                start = verts.Count;
                part.Clear();
            }

            public int V(Vector3 p, Vector2 uv)
            {
                verts.Add(p);
                uvs.Add(uv);
                norms.Add(Vector3.zero);
                return verts.Count - 1;
            }

            public Vector3 Pos(int i) { return verts[i]; }

            public void T(int sub, int a, int b, int c)
            {
                part.Add(sub); part.Add(a); part.Add(b); part.Add(c);
            }

            public void Q(int sub, int a, int b, int c, int d)
            {
                T(sub, a, b, c);
                T(sub, a, c, d);
            }

            /// <summary>面の表が want の向きになるよう、並びを揃えて置く</summary>
            public void TFacing(int sub, int a, int b, int c, Vector3 want)
            {
                var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(n, want) < 0f) T(sub, a, c, b);
                else T(sub, a, b, c);
            }

            /// <summary>四角を、centre から外へ向けて置く（筒の皮）</summary>
            public void QOut(int sub, int a, int b, int c, int d, Vector3 centre)
            {
                var mid = (verts[a] + verts[b] + verts[c] + verts[d]) * 0.25f;
                var want = mid - centre;
                TFacing(sub, a, b, c, want);
                TFacing(sub, a, c, d, want);
            }

            public void End(Orient orient, Vector3 dir = default(Vector3))
            {
                var count = part.Count / 4;
                var centroid = Vector3.zero;
                for (var i = start; i < verts.Count; i++) centroid += verts[i];
                if (verts.Count > start) centroid /= verts.Count - start;
                if (orient == Orient.Whole || orient == Orient.Toward)
                {
                    var sum = 0f;
                    for (var f = 0; f < count; f++)
                    {
                        var a = verts[part[f * 4 + 1]];
                        var b = verts[part[f * 4 + 2]];
                        var c = verts[part[f * 4 + 3]];
                        var n = Vector3.Cross(b - a, c - a);
                        sum += orient == Orient.Whole ? Vector3.Dot(n, (a + b + c) / 3f - centroid) : Vector3.Dot(n, dir);
                    }
                    if (sum < 0f) for (var f = 0; f < count; f++) Swap(f);
                }
                else if (orient == Orient.Each)
                {
                    for (var f = 0; f < count; f++)
                    {
                        var a = verts[part[f * 4 + 1]];
                        var b = verts[part[f * 4 + 2]];
                        var c = verts[part[f * 4 + 3]];
                        if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centroid) < 0f) Swap(f);
                    }
                }

                // 面の法線（面積の重み付き）
                var faceN = new Vector3[count];
                var byKey = new Dictionary<Vector3Int, List<int>>();
                var own = new Dictionary<int, List<int>>();
                for (var f = 0; f < count; f++)
                {
                    var ia = part[f * 4 + 1];
                    var ib = part[f * 4 + 2];
                    var ic = part[f * 4 + 3];
                    faceN[f] = Vector3.Cross(verts[ib] - verts[ia], verts[ic] - verts[ia]);
                    foreach (var v in new[] { ia, ib, ic })
                    {
                        var key = Key(verts[v]);
                        List<int> list;
                        if (!byKey.TryGetValue(key, out list)) byKey[key] = list = new List<int>();
                        list.Add(f);
                        if (!own.TryGetValue(v, out list)) own[v] = list = new List<int>();
                        list.Add(f);
                    }
                }
                var cos = Mathf.Cos(crease * Mathf.Deg2Rad);
                foreach (var pair in own)
                {
                    var mine = Vector3.zero;
                    foreach (var f in pair.Value) mine += faceN[f];
                    if (mine.sqrMagnitude < 1e-16f)
                        foreach (var f in byKey[Key(verts[pair.Key])]) mine += faceN[f];
                    var m = mine.normalized;
                    var sum = Vector3.zero;
                    foreach (var f in byKey[Key(verts[pair.Key])])
                    {
                        if (faceN[f].sqrMagnitude < 1e-16f) continue;
                        if (Vector3.Dot(faceN[f].normalized, m) >= cos - 1e-4f) sum += faceN[f];
                    }
                    norms[pair.Key] = sum.sqrMagnitude > 1e-16f ? sum.normalized : (m.sqrMagnitude > 0f ? m : Vector3.up);
                }

                var piece = new Piece { Name = name, Solid = solid, Triangles = new int[count * 3] };
                var bounds = new Bounds();
                var first = true;
                for (var f = 0; f < count; f++)
                {
                    var sub = part[f * 4];
                    for (var k = 1; k <= 3; k++)
                    {
                        var v = part[f * 4 + k];
                        tris[sub].Add(v);
                        piece.Triangles[f * 3 + k - 1] = v;
                        if (first) { bounds = new Bounds(verts[v], Vector3.zero); first = false; }
                        else bounds.Encapsulate(verts[v]);
                    }
                }
                piece.Bounds = bounds;
                Pieces.Add(piece);
                TriangleCount += count;
                part.Clear();
            }

            void Swap(int f)
            {
                var t = part[f * 4 + 2];
                part[f * 4 + 2] = part[f * 4 + 3];
                part[f * 4 + 3] = t;
            }

            static Vector3Int Key(Vector3 p)
            {
                return new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
            }

            public Mesh Bake(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = Count;
                for (var i = 0; i < Count; i++) mesh.SetTriangles(tris[i], i, false);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
