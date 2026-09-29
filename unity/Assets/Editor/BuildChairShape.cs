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

        /// <summary>座面のクッションの半分の幅</summary>
        const float SeatHalf = 0.285f;
        /// <summary>座面の後ろの端と前の端（脇の盛り上がりの所）</summary>
        const float SeatBack = -0.215f;
        const float SeatFront = 0.29f;
        /// <summary>真ん中の畝（前後に走る 4 本）の範囲の半分と、1 本の幅</summary>
        const float ChannelHalf = 0.17f;
        const float ChannelWide = 0.085f;
        /// <summary>脇の盛り上がりが最も高い所（真ん中から）と、その高さ</summary>
        const float BolsterPeak = 0.235f;
        const float BolsterRise = 0.032f;
        /// <summary>クッションの下の面（座の皿の上）</summary>
        const float SeatFloor = 0.435f;
        /// <summary>
        /// 座面の前の真ん中の、ふくらはぎの逃げ。膝の裏から下がるふくらはぎ（床から 0.42〜0.47 m で前の端から 4 cm 内まで来る）に
        /// 前の椅子は 3 cm 食い込んでいた。前の真ん中だけクッションの下を上げて、ふくらはぎの上に浮かせる
        /// </summary>
        const float CalfRelief = 0.042f;
        /// <summary>脇の角の丸み（横から見た上の角と、上から見た前の角）</summary>
        const float SeatEdge = 0.035f;
        const float SeatCorner = 0.06f;

        /// <summary>背もたれの倒れ（度）。前の椅子は 24 度</summary>
        const float Recline = 20f;
        /// <summary>背もたれの前の基準の面の下の端（真ん中）。座面の後ろに 1.4 cm 沈めて載せる</summary>
        static readonly Vector3 BackBase = new Vector3(0f, 0.535f, -0.145f);
        /// <summary>背もたれの長さ（倒れた向きに沿って）と、前の基準の面から裏の殻までの厚み</summary>
        const float BackLength = 0.98f;
        const float BackThick = 0.095f;
        /// <summary>背もたれの真ん中の畝（横に走る 8 本）の範囲（倒れた向きに沿って）</summary>
        const float BackRibLow = 0.08f;
        const float BackRibHigh = 0.70f;
        const int BackRibs = 8;
        /// <summary>背もたれの脇の縁の丸み</summary>
        const float BackEdge = 0.03f;
        /// <summary>真ん中の畝の面と脇の張り出し（ウィング）の境。背もたれの半分の幅に対する割合</summary>
        const float BackPanel = 0.55f;

        /// <summary>肘掛けの上面の線。前の椅子と同じ高さと傾き（前へ 4 度下がる）。ジャックの置き場（z −0.07）で 0.6758</summary>
        static float PadTop(float z) { return 0.6758f - 0.0699f * (z + 0.07f); }
        const float PadInner = 0.245f;
        const float PadOuter = 0.335f;
        const float PadThick = 0.034f;
        const float PadBack = -0.175f;
        /// <summary>肘掛けの当て物の前の端。座って始めた形の左の指先（z 0.27〜0.29 で下へ垂れる）より手前で丸めて落とす</summary>
        const float PadFront = 0.255f;
        /// <summary>肘掛けの外の受け皿（当て物より 1〜3 cm 低い溝）の外の縁と前後の端</summary>
        const float DeckOuter = 0.412f;
        const float DeckFront = 0.195f;
        const float DeckBack = -0.06f;
        /// <summary>
        /// 肘掛けの前の端の操作盤（前へ高くなる斜めの面を、座った目の方へ向ける）。内の縁は座って始めた形の左の小指（x −0.31）より外。
        /// 座った目から肘掛けの上は真下に近く、下を向ける限り（40 度）では目に入らない。前へ出して高くし、首を振って見下ろすと画面の下に入る所に置く
        /// </summary>
        const float PodInner = 0.322f;
        const float PodOuter = 0.418f;
        const float PodFront = 0.335f;
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

        /// <summary>張り地の絵（64×32）。左半分が革の地、次の 16 画素が畝の帯、右の 16 画素が穴あきの畝の帯</summary>
        const int UpholsteryW = 64;
        const int UpholsteryH = 32;
        /// <summary>革の地の 1 枚の大きさ（m）と、畝の帯の縦の 1 枚の大きさ（m）</summary>
        const float GrainTile = 0.32f;
        const float RibTile = 0.16f;

        /// <summary>操作盤の絵（64×64）の置き場。画素で (x, y, 幅, 高さ)、y は下から</summary>
        const int PanelSize = 64;
        static readonly RectInt RightPod = new RectInt(0, 4, 16, 16);
        static readonly RectInt LeftPod = new RectInt(16, 4, 16, 16);
        static readonly RectInt RightDeck = new RectInt(0, 20, 12, 28);
        static readonly RectInt LeftDeck = new RectInt(12, 20, 12, 28);
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

        /// <summary>座面の上の面の高さ。尻の沈む所をわずかに窪ませ、脇を盛り上げ、真ん中に前後の畝を立てる</summary>
        static float SeatTop(float x, float z)
        {
            var ax = Mathf.Abs(x);
            var y = SeatLevel(z);
            y -= 0.005f * Mathf.Exp(-Mathf.Pow((z - 0.03f) / 0.12f, 2f)) * (1f - Smooth(0.12f, 0.20f, ax));
            y += BolsterRise * Smooth(ChannelHalf, BolsterPeak, ax);
            if (ax < ChannelHalf)
            {
                var f = (x + ChannelHalf) / ChannelWide;
                f -= Mathf.Floor(f);
                y += 0.007f * Mathf.Pow(Mathf.Sin(Mathf.PI * f), 1.5f);
            }
            return y;
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
            CushionGrid(shop, SeatShape, ChannelHalf, ChannelWide, 4, 4,
                new List<float> { 0.17f, 0.195f, 0.215f, 0.235f, 0.25f, 0.262f, 0.271f, 0.278f, 0.283f, SeatHalf });
            shop.End(Orient.Whole);
        }

        /// <summary>
        /// クッションの皮を張る。真ん中に前後の畝（ribs 本、幅 ribWide、畝ごとに segments に割る）、その外の脇（sides の |x| に輪を置き、最後に畳んで閉じる）。
        /// 畝ごとに uv を 0 から張るので、境の輪は二重に置く
        /// </summary>
        static void CushionGrid(Shop shop, Cushion c, float ribHalf, float ribWide, int ribs, int segments, List<float> sides)
        {
            for (var r = 0; r < ribs; r++)
            {
                var x0 = -ribHalf + r * ribWide;
                var xs = new List<float>();
                for (var i = 0; i <= segments; i++) xs.Add(x0 + ribWide * i / segments);
                RingGrid(shop, Shop.Upholstery, xs, x => CushionRing(c, x, false),
                    (x, arc) => RibUv((x - x0) / ribWide, arc / RibTile, false));
            }
            foreach (var side in new[] { 1f, -1f })
            {
                var rings = new List<List<Vector3>>();
                foreach (var ax in sides) rings.Add(CushionRing(c, side * ax, false));
                rings.Add(CushionRing(c, side * c.Half, true));
                var us = new List<float>(sides);
                us.Add(c.Half + 0.03f);
                // 輪はどの組も x の増える向きに並べる（左だけ逆に並べると、面の表裏が左だけ逆になる）
                if (side < 0f) { rings.Reverse(); us.Reverse(); }
                Rings(shop, Shop.Upholstery, rings, (i, arc) => GrainUv((us[i] - ribHalf) / GrainTile, arc / GrainTile));
            }
        }

        /// <summary>輪の並び（x ごとに輪郭）から格子を張る</summary>
        static void RingGrid(Shop shop, int sub, List<float> xs, Func<float, List<Vector3>> ring, Func<float, float, Vector2> uv)
        {
            var rings = new List<List<Vector3>>();
            foreach (var x in xs) rings.Add(ring(x));
            Rings(shop, sub, rings, (i, arc) => uv(xs[i], arc));
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

        /// <summary>革の地の uv。u は 0〜1 を地の置き場（絵の左半分）へ</summary>
        static Vector2 GrainUv(float u, float v)
        {
            return new Vector2(Mathf.Lerp(0.01f, 0.49f, Mathf.Clamp01(u)), v);
        }

        /// <summary>畝の uv。f は畝を横切る割合（0 と 1 が縫い目）。perforated なら穴あきの帯</summary>
        static Vector2 RibUv(float f, float v, bool perforated)
        {
            var x0 = perforated ? 48f : 32f;
            return new Vector2((x0 + 0.05f + 15.9f * Mathf.Clamp01(f)) / UpholsteryW, v);
        }

        // ---- 背もたれ ----------------------------------------------------------------

        static Vector3 BackUp { get { var r = Recline * Mathf.Deg2Rad; return new Vector3(0f, Mathf.Cos(r), -Mathf.Sin(r)); } }
        static Vector3 BackFwd { get { var r = Recline * Mathf.Deg2Rad; return new Vector3(0f, Mathf.Sin(r), Mathf.Cos(r)); } }

        /// <summary>背もたれの座標（x は横、s は倒れた向きに沿って下の端から、t は前の基準の面から前へ）を椅子の座標へ</summary>
        public static Vector3 Back(float x, float s, float t)
        {
            return BackBase + Vector3.right * x + BackUp * s + BackFwd * t;
        }

        static float Keyed(float[] keys, float s)
        {
            // keys は (s, 値) の並び。間はなめらかに繋ぐ
            if (s <= keys[0]) return keys[1];
            for (var i = 2; i < keys.Length; i += 2)
            {
                if (s > keys[i]) continue;
                var k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(keys[i - 2], keys[i], s));
                return Mathf.Lerp(keys[i - 1], keys[i + 1], k);
            }
            return keys[keys.Length - 1];
        }

        /// <summary>背もたれの半分の幅。腰から肩へ広がり、首で絞って頭の所は細い（レースの座席の形）</summary>
        static float BackHalf(float s)
        {
            return Keyed(new[] { 0f, 0.215f, 0.10f, 0.235f, 0.30f, 0.255f, 0.48f, 0.268f, 0.60f, 0.262f, 0.67f, 0.215f, 0.72f, 0.182f, 0.80f, 0.175f, 0.93f, 0.168f, BackLength, 0.15f }, s);
        }

        /// <summary>脇の張り出しの高さ（前へ）</summary>
        static float WingRise(float s)
        {
            return Keyed(new[] { 0f, 0.02f, 0.15f, 0.06f, 0.55f, 0.065f, 0.66f, 0.04f, 0.72f, 0.02f, BackLength, 0.02f }, s);
        }

        /// <summary>背もたれの前の面の、前の基準の面からの高さ。q は半分の幅に対する横の割合（−1〜1）</summary>
        static float BackFront(float q, float s)
        {
            var aq = Mathf.Abs(q);
            var t = WingRise(s) * Smooth(BackPanel, 0.9f, aq);
            if (s > BackRibLow && s < BackRibHigh)
            {
                var f = (BackRibHigh - s) / ((BackRibHigh - BackRibLow) / BackRibs);
                f -= Mathf.Floor(f);
                t += 0.008f * Mathf.Pow(Mathf.Sin(Mathf.PI * f), 1.5f) * (1f - Smooth(BackPanel - 0.1f, BackPanel, aq));
            }
            t += 0.012f * Smooth(0.68f, 0.74f, s);
            return t;
        }

        /// <summary>背もたれの輪郭の区切り（倒れた向きの s）。畝の縫い目の所で二重にして uv を切る</summary>
        struct BackRun
        {
            public int Sub;
            public int From;
            public int To;
            /// <summary>畝の帯で張る（真ん中の輪だけ）。何本目か。畝でなければ −1</summary>
            public int Rib;
        }

        /// <summary>
        /// 背もたれの、横の割合 q での輪郭（倒れた向きに沿った縦の断面）。裏の殻を下から上へ、上の丸み、前の面を上から下へ、下の端。
        /// 前の面は畝の縫い目ごとに点を二重にする（uv を畝ごとに張り直すため）。runs は区切り
        /// </summary>
        static List<Vector3> BackRing(float q, bool collapse, out List<BackRun> runs)
        {
            var aq = Mathf.Abs(q);
            var sign = q < 0f ? -1f : 1f;
            runs = new List<BackRun>();
            var pts = new List<Vector3>();
            // 縁の丸み。x は s ごとに q × 半分の幅なので、丸みの量も s ごとに出す
            Func<float, float> inset = s =>
            {
                var w = BackHalf(s);
                var x = aq * w;
                var edge = w - BackEdge;
                if (x <= edge) return 0f;
                return BackEdge - Mathf.Sqrt(Mathf.Max(0f, BackEdge * BackEdge - (x - edge) * (x - edge)));
            };
            Func<float, float> front = s => BackFront(q, s);
            Func<float, float> half = s => 0.5f * (BackFront(q, s) + BackThick);
            Func<float, float, Vector3> P = (s, t) => Back(sign * aq * BackHalf(s), s, t);
            Func<float, float> dd = s => collapse ? half(s) : inset(s);
            Func<float, float> backT = s => -BackThick + dd(s);
            Func<float, float> frontT = s => front(s) - dd(s);

            // 上の丸み。前と裏の真ん中を芯に
            var top = BackLength - dd(BackLength) * (collapse ? 0.3f : 1f);
            var rt = Mathf.Max(0.0005f, 0.5f * (frontT(top - 0.05f) - backT(top - 0.05f)));
            var sc = top - rt;
            var tc = 0.5f * (frontT(sc) + backT(sc));
            rt = Mathf.Max(0.0005f, 0.5f * (frontT(sc) - backT(sc)));

            // 裏の殻（下から上へ）
            var start = pts.Count;
            foreach (var k in new[] { 0f, 0.14f, 0.3f, 0.46f, 0.6f, 0.72f, 0.84f, 1f })
            {
                var s = Mathf.Lerp(0f, sc, k);
                pts.Add(P(s, backT(s)));
            }
            runs.Add(new BackRun { Sub = Shop.Shell, From = start, To = pts.Count - 1, Rib = -1 });
            // 上の丸み（裏から前へ）
            start = pts.Count - 1;
            for (var i = 1; i <= 4; i++)
            {
                var a = Mathf.Lerp(0f, 180f, i / 4f) * Mathf.Deg2Rad;
                pts.Add(P(sc + rt * Mathf.Sin(a), tc - rt * Mathf.Cos(a)));
            }
            // 頭の所の前の面（上から、畝の始まりまで）
            foreach (var s in new[] { Mathf.Lerp(sc, BackRibHigh, 0.35f), Mathf.Lerp(sc, BackRibHigh, 0.7f), BackRibHigh })
                pts.Add(P(s, frontT(s)));
            runs.Add(new BackRun { Sub = Shop.Upholstery, From = start, To = pts.Count - 1, Rib = -1 });
            // 畝（上から下へ）。縫い目の点は二重に置く
            var ribH = (BackRibHigh - BackRibLow) / BackRibs;
            for (var r = 0; r < BackRibs; r++)
            {
                var s0 = BackRibHigh - r * ribH;
                start = pts.Count;
                for (var i = 0; i <= 2; i++)
                {
                    var s = s0 - ribH * i / 2f;
                    pts.Add(P(s, frontT(s)));
                }
                runs.Add(new BackRun { Sub = Shop.Upholstery, From = start, To = pts.Count - 1, Rib = r });
            }
            // 畝の下から下の端、下の端の蓋（前から裏へ）
            start = pts.Count;
            foreach (var s in new[] { BackRibLow, BackRibLow * 0.5f, 0f })
                pts.Add(P(s, frontT(s)));
            pts.Add(P(0f, backT(0f)));
            runs.Add(new BackRun { Sub = Shop.Upholstery, From = start, To = pts.Count - 1, Rib = -1 });
            return pts;
        }

        static void Backrest(Shop shop)
        {
            shop.Begin("Backrest", 55f, true);
            // 真ん中（畝のある面）と左右の張り出し。輪は横の割合 q で並べる
            var centre = new List<float>();
            for (var i = 0; i <= 6; i++) centre.Add(Mathf.Lerp(-BackPanel, BackPanel, i / 6f));
            BackGroup(shop, centre, false, true);
            foreach (var side in new[] { 1f, -1f })
            {
                var qs = new List<float>();
                foreach (var q in new[] { BackPanel, 0.68f, 0.80f, 0.88f, 0.93f, 0.97f, 1f }) qs.Add(side * q);
                BackGroup(shop, qs, true, false);
            }
            shop.End(Orient.Whole);
        }

        /// <summary>背もたれの輪の組を張る。closeEnd なら最後に畳んだ輪を足して縁を閉じる</summary>
        static void BackGroup(Shop shop, List<float> qs, bool closeEnd, bool centre)
        {
            var rings = new List<List<Vector3>>();
            List<BackRun> runs = null;
            foreach (var q in qs)
            {
                List<BackRun> r;
                rings.Add(BackRing(q, false, out r));
                runs = r;
            }
            if (closeEnd)
            {
                List<BackRun> r;
                rings.Add(BackRing(qs[qs.Count - 1], true, out r));
            }
            // 輪はどの組も x の増える向きに並べる（左の張り出しだけ逆に並べると、面の表裏が左だけ逆になる）
            if (rings[0][0].x > rings[rings.Count - 1][0].x) rings.Reverse();
            var ribH = (BackRibHigh - BackRibLow) / BackRibs;
            foreach (var run in runs)
            {
                var sub = run.Sub;
                var n = run.To - run.From + 1;
                var idx = new int[rings.Count, n];
                for (var i = 0; i < rings.Count; i++)
                {
                    var arc = 0f;
                    for (var j = 0; j < n; j++)
                    {
                        var p = rings[i][run.From + j];
                        if (j > 0) arc += Vector3.Distance(rings[i][run.From + j - 1], p);
                        Vector2 uv;
                        if (sub != Shop.Upholstery) uv = Vector2.zero;
                        else if (centre && run.Rib >= 0)
                        {
                            // 畝を横切る向き（s）を絵の横に、横（x）を絵の縦に
                            var s0 = BackRibHigh - run.Rib * ribH;
                            var local = p - BackBase;
                            var s = Vector3.Dot(local, BackUp);
                            uv = RibUv((s0 - s) / ribH, p.x / RibTile, false);
                        }
                        else if (centre) uv = GrainUv((p.x + 0.16f) / GrainTile, arc / GrainTile + run.From * 0.013f);
                        else uv = GrainUv((Mathf.Abs(p.x) - 0.13f) / GrainTile, arc / GrainTile + run.From * 0.013f);
                        idx[i, j] = shop.V(p, uv);
                    }
                }
                for (var i = 0; i < rings.Count - 1; i++)
                    for (var j = 0; j < n - 1; j++)
                        shop.Q(sub, idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
            }
        }

        /// <summary>
        /// 背もたれの前に留めた当て物。腰の当て物（腰の骨の後ろ）と頭の当て物（頭の後ろ）。
        /// 穴あきの畝の帯を 1 枚張り、上と下の縁に縫い目が来る
        /// </summary>
        static void Pillows(Shop shop)
        {
            // 腰。座った体の腰の後ろ（s 0.10 で前へ 0.061、s 0.19 で 0.116）より 2 cm 以上手前に収める
            Pillow(shop, "LumbarPillow", 0.205f, 0.085f, 0.15f, 0.068f);
            // 頭。頭の後ろ（前へ 0.33 以上）までは 25 cm ほど空く。座ったまま背を預けて天井を仰ぐ姿勢の目もこれより前
            Pillow(shop, "HeadPillow", 0.848f, 0.088f, 0.125f, 0.07f);
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
                    var q = x / BackHalf(s);
                    var puff = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 4f))) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(v), 4f)));
                    var p = Back(x, s, BackFront(q, s) - 0.004f + thick * puff);
                    idx[i, j] = shop.V(p, RibUv((v + 1f) * 0.5f, x / RibTile, true));
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
            const float end = 0.02f;
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
            const float r = 0.012f;
            var rr = Mathf.Min(r, 0.5f * Mathf.Max(0.0005f, yt - yb), 0.5f * Mathf.Max(0.0005f, xo - xi));
            var pts = new List<Vector3>();
            pts.Add(new Vector3(xi, yb, z));
            pts.Add(new Vector3(xi, Mathf.Lerp(yb, yt - rr, 0.5f), z));
            for (var i = 0; i <= 3; i++)
            {
                var a = Mathf.Lerp(180f, 90f, i / 3f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(xi + rr + rr * Mathf.Cos(a), yt - rr + rr * Mathf.Sin(a), z));
            }
            pts.Add(new Vector3(0.5f * (xi + xo), yt + 0.0015f, z));
            for (var i = 0; i <= 3; i++)
            {
                var a = Mathf.Lerp(90f, 0f, i / 3f) * Mathf.Deg2Rad;
                pts.Add(new Vector3(xo - rr + rr * Mathf.Cos(a), yt - rr + rr * Mathf.Sin(a), z));
            }
            pts.Add(new Vector3(xo, yb, z));
            pts.Add(new Vector3(xi, yb, z));
            for (var i = 0; i < pts.Count; i++) pts[i] = new Vector3(sx * pts[i].x, pts[i].y, pts[i].z);
            return pts;
        }

        static void Arm(Shop shop, float sx)
        {
            var name = sx > 0f ? "R" : "L";
            // 当て物
            shop.Begin("ArmPad" + name, 55f, true);
            var zs = new[] { PadBack + 0.004f, PadBack + 0.011f, PadBack + 0.02f, -0.12f, -0.05f, 0.02f, 0.09f, 0.16f, 0.215f, PadFront - 0.02f, PadFront - 0.009f, PadFront - 0.003f };
            var rings = new List<List<Vector3>>();
            rings.Add(PadRing(sx, PadBack, true));
            foreach (var z in zs) rings.Add(PadRing(sx, z, false));
            rings.Add(PadRing(sx, PadFront, true));
            Rings(shop, Shop.Upholstery, rings, (i, arc) => GrainUv(arc / GrainTile, rings[i][0].z / GrainTile));
            shop.End(Orient.Whole);

            // 当て物の下の胴
            var body = new Vector3[8];
            var zb = PadBack + 0.005f;
            var zf = PadFront - 0.006f;
            var inner = sx * (PadInner + 0.006f);
            var outer = sx * PadOuter;
            var under = 0.598f;
            body[0] = new Vector3(inner, under, zb); body[1] = new Vector3(outer, under, zb);
            body[2] = new Vector3(outer, under, zf - 0.03f); body[3] = new Vector3(inner, under, zf - 0.03f);
            body[4] = new Vector3(inner, PadTop(zb) - PadThick + 0.004f, zb); body[5] = new Vector3(outer, PadTop(zb) - PadThick + 0.004f, zb);
            body[6] = new Vector3(outer, PadTop(zf) - PadThick + 0.004f, zf); body[7] = new Vector3(inner, PadTop(zf) - PadThick + 0.004f, zf);
            Hexa(shop, "ArmBody" + name, body, Shop.Shell, null, true);

            // 外の受け皿。当て物より 1〜3 cm 低い溝で、内（座った体の方）へ傾けて上を向く
            var deck = new Vector3[8];
            var di = sx * PadOuter;
            var dout = sx * (DeckOuter - 0.006f);
            Func<float, float> dIn = z => PadTop(z) - 0.030f;
            Func<float, float> dOut = z => PadTop(z) - 0.012f;
            deck[0] = new Vector3(di, under, DeckBack); deck[1] = new Vector3(dout, under, DeckBack);
            deck[2] = new Vector3(dout, under, DeckFront); deck[3] = new Vector3(di, under, DeckFront);
            deck[4] = new Vector3(di, dIn(DeckBack), DeckBack); deck[5] = new Vector3(dout, dOut(DeckBack), DeckBack);
            deck[6] = new Vector3(dout, dOut(DeckFront), DeckFront); deck[7] = new Vector3(di, dIn(DeckFront), DeckFront);
            var faces = new int[] { Shop.Shell, Shop.Panel, Shop.Shell, Shop.Shell, Shop.Shell, Shop.Shell };
            var uvs = new Rect?[] { null, PanelUv(sx > 0f ? RightDeck : LeftDeck), null, null, null, null };
            Hexa(shop, "ArmDeck" + name, deck, faces, uvs, true, sx < 0f);

            // 外の縁（受け皿の縁の立ち上がり）
            var rim = new Vector3[8];
            var r0 = sx * (DeckOuter - 0.012f);
            var r1 = sx * DeckOuter;
            rim[0] = new Vector3(r0, under, DeckBack - 0.004f); rim[1] = new Vector3(r1, under, DeckBack - 0.004f);
            rim[2] = new Vector3(r1, under, DeckFront); rim[3] = new Vector3(r0, under, DeckFront);
            rim[4] = new Vector3(r0, dOut(DeckBack) + 0.011f, DeckBack - 0.004f); rim[5] = new Vector3(r1, dOut(DeckBack) + 0.008f, DeckBack - 0.004f);
            rim[6] = new Vector3(r1, dOut(DeckFront) + 0.008f, DeckFront); rim[7] = new Vector3(r0, dOut(DeckFront) + 0.011f, DeckFront);
            Hexa(shop, "ArmRim" + name, rim, Shop.Shell, null, true);

            // 前の端の操作盤。後ろの低い縁から前の高い縁へ上がる斜めの面を、座った目の方（後ろ・上・内）へ向ける
            var pod = new Vector3[8];
            var pi = sx * PodInner;
            var po = sx * PodOuter;
            var pb = DeckFront;
            pod[0] = new Vector3(pi, under, pb); pod[1] = new Vector3(po, under, pb);
            pod[2] = new Vector3(po, under, PodFront - 0.03f); pod[3] = new Vector3(pi, under, PodFront - 0.03f);
            pod[4] = new Vector3(pi, 0.652f, pb); pod[5] = new Vector3(po, 0.668f, pb);
            pod[6] = new Vector3(po, 0.722f, PodFront); pod[7] = new Vector3(pi, 0.706f, PodFront);
            var podUvs = new Rect?[] { null, PanelUv(sx > 0f ? RightPod : LeftPod), null, null, null, null };
            Hexa(shop, "ArmPod" + name, pod, faces, podUvs, true, sx < 0f);

            // 支柱と、座の皿の下へ回る腕木
            Slab(shop, "ArmPost" + name, Shop.Shell, new Vector3(sx * 0.372f, 0.505f, -0.05f), Quaternion.identity, new Vector3(0.046f, 0.20f, 0.10f), 0.01f, true);
            Slab(shop, "ArmBracket" + name, Shop.Frame, new Vector3(sx * 0.30f, 0.408f, -0.05f), Quaternion.identity, new Vector3(0.20f, 0.026f, 0.08f), 0.006f, false);
            // 支柱の外の面の高さ調節の釦
            Decal(shop, "ArmButton" + name, new Vector3(sx * 0.3955f, 0.565f, -0.05f), new Vector3(0f, 0f, 0.012f), new Vector3(0f, 0.018f, 0f), new Vector3(sx, 0f, 0f), SwatchRect(Swatch.PlasticLight));
        }

        /// <summary>右の肘掛けの差込口。金属の縁、黒い穴、穴を囲む紫の灯り（ジャックを挿す所の合図）</summary>
        static void Port(Shop shop)
        {
            var c = PortAt;
            Tube(shop, "PortBezel", Shop.Chrome, new List<Vector3> { new Vector3(c.x, 0.648f, c.z), new Vector3(c.x, 0.6725f, c.z) }, 0.022f, 10, true, Vector2.zero, 70f);
            Ring(shop, "PortGlow", new Vector3(c.x, 0.6735f, c.z), Vector3.up, 0.0145f, 0.0195f, 12, SwatchUv(Swatch.VioletLit));
            Disc(shop, "PortHole", new Vector3(c.x, 0.6740f, c.z), Vector3.up, 0.0135f, 10, SwatchUv(Swatch.Rubber));
        }

        // ---- 座の下 ------------------------------------------------------------------

        static void Undercarriage(Shop shop)
        {
            // 座の皿
            Slab(shop, "SeatPan", Shop.Shell, new Vector3(0f, 0.4175f, -0.03f), Quaternion.identity, new Vector3(0.55f, 0.035f, 0.40f), 0.012f, true);
            // リクライニングの機構の箱
            Slab(shop, "Mechanism", Shop.Frame, new Vector3(0f, 0.372f, -0.02f), Quaternion.identity, new Vector3(0.24f, 0.055f, 0.25f), 0.01f, true);
            // 右はリクライニングのレバー、左は高さのレバー。先に黒い握り
            Tube(shop, "LeverR", Shop.Frame, new List<Vector3> { new Vector3(0.11f, 0.366f, 0.04f), new Vector3(0.19f, 0.360f, 0.058f), new Vector3(0.232f, 0.355f, 0.072f) }, 0.006f, 6, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripR", Shop.Panel, new Vector3(0.25f, 0.354f, 0.078f), Quaternion.Euler(0f, 18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0.005f, true, SwatchUv(Swatch.Rubber));
            Tube(shop, "LeverL", Shop.Frame, new List<Vector3> { new Vector3(-0.11f, 0.366f, 0.0f), new Vector3(-0.19f, 0.360f, 0.018f), new Vector3(-0.232f, 0.355f, 0.032f) }, 0.006f, 6, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripL", Shop.Panel, new Vector3(-0.25f, 0.354f, 0.038f), Quaternion.Euler(0f, -18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0.005f, true, SwatchUv(Swatch.Rubber));
            // 前の張りの摘み
            Tube(shop, "TensionKnob", Shop.Panel, new List<Vector3> { new Vector3(0f, 0.35f, 0.105f), new Vector3(0f, 0.35f, 0.135f) }, 0.026f, 10, true, SwatchUv(Swatch.Rubber), 70f);

            // 背もたれを支える左右の腕（座の下から、クッションの後ろを回って背の殻へ）と、倒れの軸の蓋
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.13f, 0.385f, -0.15f),
                    new Vector3(sx * 0.13f, 0.405f, -0.245f),
                    new Vector3(sx * 0.13f, 0.43f, -0.268f),
                    Back(sx * 0.13f, 0.13f, -BackThick + 0.004f),
                };
                Tube(shop, "ReclineArm" + (sx > 0f ? "R" : "L"), Shop.Frame, Spline(path, 10), 0.012f, 6, false, Vector2.zero, 70f);
                Tube(shop, "ReclineHub" + (sx > 0f ? "R" : "L"), Shop.Frame, new List<Vector3> { new Vector3(sx * 0.115f, 0.418f, -0.26f), new Vector3(sx * 0.152f, 0.418f, -0.26f) }, 0.022f, 10, true, Vector2.zero, 70f);
            }

            // ガスシリンダー。覆いと、磨いた芯
            TubeR(shop, "ColumnCover", Shop.Shell, new List<Vector3> { new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.235f, 0f) }, new List<float> { 0.046f, 0.038f }, 10, true, Vector2.zero, 70f);
            Tube(shop, "ColumnRod", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.225f, 0f), new Vector3(0f, 0.345f, 0f) }, 0.024f, 10, false, Vector2.zero, 70f);
            Tube(shop, "ColumnCollar", Shop.Frame, new List<Vector3> { new Vector3(0f, 0.328f, 0f), new Vector3(0f, 0.346f, 0f) }, 0.04f, 10, true, Vector2.zero, 70f);
        }

        // ---- 五本脚とキャスター --------------------------------------------------------

        static void Base(Shop shop)
        {
            Tube(shop, "Hub", Shop.Frame, new List<Vector3> { new Vector3(0f, 0.075f, 0f), new Vector3(0f, 0.132f, 0f) }, 0.056f, 10, true, Vector2.zero, 70f);
            for (var k = 0; k < 5; k++)
            {
                // 一本は真後ろ。前の二本は左右へ 36 度（机の側へ脚を突き出さない）
                var a = (180f + 72f * k) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var side = Vector3.Cross(Vector3.up, dir);
                shop.Begin("Leg" + k, 30f, true);
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
                Rings(shop, Shop.Frame, rings, (i, arc) => Vector2.zero);
                // 先の蓋
                var tip = rings[rings.Count - 1];
                var ci = shop.V((tip[0] + tip[1] + tip[2] + tip[3] + tip[4] + tip[5]) / 6f, Vector2.zero);
                var ids = new int[6];
                for (var i = 0; i < 6; i++) ids[i] = shop.V(tip[i], Vector2.zero);
                for (var i = 0; i < 6; i++) shop.T(Shop.Frame, ci, ids[i], ids[(i + 1) % 6]);
                shop.End(Orient.Each);

                // キャスター。覆いと、並んだ二つの車
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

        /// <summary>フットレストのパッドの半分の幅、前後の端、厚み、畝の範囲の半分と 1 本の幅（前後に 3 本）</summary>
        const float FootHalf = 0.20f;
        const float FootBack = 0.20f;
        const float FootFront = 0.445f;
        const float FootThick = 0.045f;
        const float FootRibHalf = 0.15f;
        const float FootRibWide = 0.10f;
        /// <summary>引き出す腕の左右の位置（脛と足の外の縁 |x| 0.178 より 2.5 cm 外）と、腕がパッドの脇に付く前後の位置</summary>
        const float FootArmX = 0.216f;
        const float FootPivot = 0.30f;
        /// <summary>引き出したレールの先（腕の上の軸）。座の皿の下の収め口から前へ出る</summary>
        static readonly Vector3 FootRailEnd = new Vector3(FootArmX, 0.391f, 0.19f);

        /// <summary>パッドの上の面。前後の畝をわずかに（3 mm）立てる</summary>
        static float FootPadTop(float x, float z)
        {
            var y = FootTop(z);
            if (Mathf.Abs(x) < FootRibHalf)
            {
                var f = (x + FootRibHalf) / FootRibWide;
                f -= Mathf.Floor(f);
                y += 0.003f * Mathf.Pow(Mathf.Sin(Mathf.PI * f), 1.5f);
            }
            return y;
        }

        static float FootPadUnder(float x, float z) { return FootTop(z) - FootThick; }

        static readonly Cushion FootShape = new Cushion
        {
            Top = FootPadTop, Under = FootPadUnder, Front = FootFront, Back = FootBack, Half = FootHalf, Edge = 0.02f, Corner = 0.03f, MidZ = 0.32f,
        };

        /// <summary>
        /// 座の前の下から引き出すフットレスト（ゲーミングチェアの引き出し式の脚置きの形）。座の皿の下の収め口から左右のレールが前へ出て、
        /// 先の軸から腕が下りてパッドの脇を支える。机との間が狭いので伸ばしきらず、座った形の足の裏が乗る所（床の近く）まで下ろした形。
        /// パッドは座面と同じ張り地で、前後に 3 本の畝
        /// </summary>
        static void Footrest(Shop shop)
        {
            shop.Begin("FootPad", 55f, true);
            CushionGrid(shop, FootShape, FootRibHalf, FootRibWide, 3, 3, new List<float> { 0.15f, 0.17f, 0.18f, 0.19f, 0.197f, FootHalf });
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
                Slab(shop, "FootArm" + n, Shop.Frame, 0.5f * (top + pad), Quaternion.LookRotation(seg.normalized, Vector3.forward), new Vector3(0.014f, 0.03f, seg.magnitude), 0.005f, true);
                Tube(shop, "FootPivot" + n, Shop.Frame, new List<Vector3> { new Vector3(sx * 0.197f, pad.y, pad.z), new Vector3(sx * 0.229f, pad.y, pad.z) }, 0.012f, 8, true, Vector2.zero, 50f);
            }
        }

        // ---- 頭の後ろの端末 ------------------------------------------------------------

        /// <summary>
        /// 潜行の装置。頭の当て物の左右の耳の所に小さな筐体（右は前を向いた小さな画面、左は灯りの輪）、
        /// 背もたれの頭の所の裏に箱（裏へ向いた画面・灯りの列・接続口と挿さった三本の端子）。
        /// どれも頭の後ろ（前へ 0.33 m より後ろ）にあり、座った目と、背を預けて天井を仰ぐ目の後ろに来る
        /// </summary>
        static void Crown(Shop shop)
        {
            var rot = Quaternion.LookRotation(BackFwd, BackUp);
            foreach (var sx in new[] { 1f, -1f })
            {
                var n = sx > 0f ? "R" : "L";
                var c = Back(sx * 0.203f, 0.855f, 0.0f);
                Slab(shop, "EarPod" + n, Shop.Shell, c, rot, new Vector3(0.058f, 0.15f, 0.10f), 0.012f, true);
                var face = Back(sx * 0.203f, 0.855f, 0.0505f);
                if (sx > 0f)
                    Decal(shop, "EarScreen", face, Vector3.right * 0.021f, BackUp * 0.056f, BackFwd, PanelUv(EarScreen));
                else
                    Decal(shop, "EarLamp", face + BackUp * 0.02f, Vector3.right * 0.021f, BackUp * 0.021f, BackFwd, PanelUv(EarLamp));
                // 外の面の灯りの列（縦）
                Decal(shop, "EarLeds" + n, Back(sx * (0.203f + 0.0295f), 0.855f, 0.0f), BackUp * 0.05f, BackFwd * 0.0075f, Vector3.right * sx, PanelUv(LedStrip));
            }

            // 裏の箱（角を面取りした箱。前の面は背の殻に 4 mm 埋める）
            const float tOut = -0.165f;
            Slab(shop, "CrownBox", Shop.Shell, Back(0f, 0.795f, 0.5f * (tOut - BackThick + 0.004f)), rot, new Vector3(0.25f, 0.27f, -BackThick + 0.004f - tOut), 0.016f, true);
            var rear = -BackFwd;
            var face2 = tOut - 0.0008f;
            Decal(shop, "CrownScreen", Back(0f, 0.838f, face2), Vector3.right * 0.075f, BackUp * 0.05f, rear, PanelUv(RearScreen));
            Decal(shop, "CrownLeds", Back(0f, 0.899f, face2), Vector3.right * 0.08f, BackUp * 0.007f, rear, PanelUv(LedStrip));
            Decal(shop, "CrownPorts", Back(0f, 0.72f, face2), Vector3.right * 0.085f, BackUp * 0.043f, rear, PanelUv(PortPlate));
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
            Slab(shop, "Junction", Shop.Shell, Back(0f, 0.23f, 0.5f * (jOut - BackThick + 0.004f)), rot, new Vector3(0.17f, 0.135f, -BackThick + 0.004f - jOut), 0.012f, true);
            Decal(shop, "JunctionFace", Back(0f, 0.231f, jOut - 0.0008f), Vector3.right * 0.07f, BackUp * 0.045f, rear, PanelUv(JunctionFace));
            // 背の半ばでケーブルを束ねる留め具
            Slab(shop, "CableClip", Shop.Shell, Back(0f, 0.452f, -0.114f), rot, new Vector3(0.13f, 0.024f, 0.036f), 0.006f, true);
        }

        /// <summary>
        /// 頭の後ろの端末から背もたれの裏を這うケーブル。裏の箱の端子三本は背の半ばの留め具を通って腰の分岐の箱へ、
        /// 耳の筐体の二本は背もたれの縁を回って裏の箱の脇へ。分岐の箱から太い一本が座の下の機構へ、もう一本が右の肘掛けの支柱へ
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
                    Back(sx * 0.212f, 0.83f, -0.04f),
                    Back(sx * 0.207f, 0.81f, -0.088f),
                    Back(sx * 0.186f, 0.79f, -0.122f),
                    Back(sx * 0.158f, 0.775f, -0.13f),
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
            // 分岐の箱から右の肘掛けの支柱へ（肘掛けの中を差込口まで通る）
            var arm = new List<Vector3>
            {
                Back(0.08f, 0.215f, -0.13f),
                Back(0.15f, 0.20f, -0.145f),
                new Vector3(0.27f, 0.56f, -0.30f),
                new Vector3(0.35f, 0.49f, -0.175f),
                new Vector3(0.372f, 0.48f, -0.095f),
            };
            Tube(shop, "ArmCable", Shop.Panel, Spline(arm, 14), 0.0065f, 6, false, SwatchUv(Swatch.Violet), 65f);
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
