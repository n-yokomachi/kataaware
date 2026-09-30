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
        /// <summary>背もたれの長さ（倒れた向きに沿って）と、前の基準の面から裏の面までの厚み（裏の端末の箱・分岐の箱・留め具はこの面に付く）</summary>
        const float BackLength = 0.95f;
        const float BackThick = 0.095f;
        /// <summary>
        /// 背もたれの半分の幅。肩から上は広く、肘掛けの後ろの端が来る腰の高さは細く絞る（肘掛けの当て物の後ろの端に掛けない）。
        /// 腰の幅から肩の幅へ、倒れた向きに沿った高さ BackWaistTop から BackShoulder で広げる。上の角と下の角は丸める
        /// </summary>
        const float BackHalfW = 0.30f;
        const float BackWaistW = 0.245f;
        const float BackWaistTop = 0.05f;
        const float BackShoulder = 0.32f;
        const float BackTopRound = 0.10f;
        const float BackBottomRound = 0.03f;
        /// <summary>
        /// 厚み。背もたれは前も裏も革で包んだ一つの厚いクッション。前と裏が合う真ん中の面（前の基準の面から前へ）、そこから前の面・裏の面までの厚み、
        /// 縁の丸みの半径（横から見て 14 cm の厚みの縁が丸く巻いて見える）。座った体の背は前の面より前にある（腰で 6 cm、背の半ばで 15 cm 以上）
        /// </summary>
        const float BackMid = -0.025f;
        const float BackFrontH = 0.065f;
        const float BackRearH = 0.07f;
        const float BackRound = 0.07f;
        /// <summary>脇の縁の太い巻きの幅（縫い目までの幅）と、平らな面から盛り上がる高さ、縫い目の深さ</summary>
        const float BackBorder = 0.075f;
        const float BorderRise = 0.018f;
        const float SeamDeep = 0.012f;
        /// <summary>上の縁の巻き（前へ膨らむ頭の当て。下を縫い目で区切る）の幅と、膨らむ高さ</summary>
        const float TopBand = 0.16f;
        const float TopRise = 0.03f;
        /// <summary>
        /// ボタン留め（菱形）。同じ段のボタンの間の半分、段の間（倒れた向きに沿って）、一段目の高さ、菱形の膨らみの高さ、ボタンの窪みの深さ。
        /// 粗い画面でも菱形の膨らみが読めるよう、大きな菱形（幅 16 cm・高さ 24 cm）を深く（5 cm）膨らませ、ボタンの所を 1.8 cm 窪ませ、数は 13 個に減らした
        /// </summary>
        const float TuftDx = 0.08f;
        const float TuftDs = 0.12f;
        const float TuftS0 = 0.19f;
        const float TuftPuff = 0.05f;
        const float TuftDip = 0.018f;

        /// <summary>肘掛けの上面の線。前の椅子と同じ高さと傾き（前へ 4 度下がる）。ジャックの置き場（z −0.07）で 0.6758</summary>
        static float PadTop(float z) { return 0.6758f - 0.0699f * (z + 0.07f); }
        /// <summary>
        /// 肘掛けの当て物の内の縁と外の縁、厚み。肘を置く後ろ半分は厚く（7.2 cm）幅広く丸く、前の端へ向けて細くする
        /// （座って始めた形の左の指が当て物の前の端に掛かって垂れるので、前の端は始めの椅子と同じ 4.2 cm の厚みと内の縁のまま）
        /// </summary>
        const float PadInner = 0.245f;
        const float PadInnerBack = 0.232f;
        const float PadOuter = 0.345f;
        const float PadThick = 0.042f;
        const float PadThickBack = 0.072f;
        const float PadBack = -0.19f;
        static float PadIn(float z) { return Mathf.Lerp(PadInnerBack, PadInner, Smooth(0.08f, 0.18f, z)); }
        static float PadThickAt(float z) { return Mathf.Lerp(PadThickBack, PadThick, Smooth(0.06f, 0.20f, z)); }
        /// <summary>当て物の上の角の丸み。後ろ半分は太く丸め、前の端は細く</summary>
        static float PadRound(float z) { return Mathf.Lerp(0.034f, 0.018f, Smooth(0.08f, 0.20f, z)); }
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
        //
        // 椅子の絵は一枚（128×128、点で引く）に、地の色（α は艶）・法線・光る所の三つを同じ置き場で持つ。
        // 左下の 64×64 は操作盤の絵と色の升、ほかは革の面ごとの絵（背もたれの前・裏、肘掛け、座面、フットレスト）。
        // 革の面の細かい起伏（ボタンの窪み、菱形の折り目の鋭さ、縫い目、皺）は mesh に持たせず、地の色の陰と法線の絵に描く。
        // 輪郭を作る起伏（背もたれの厚みと縁の巻き、菱形の膨らみ、肘掛けと座面の厚み）は mesh に残す

        /// <summary>椅子の絵の一辺の画素</summary>
        const int AtlasSize = 128;
        /// <summary>操作盤の絵の置き場（画素で x, y, 幅, 高さ。y は下から）</summary>
        static readonly RectInt RightPod = new RectInt(0, 4, 16, 16);
        static readonly RectInt LeftPod = new RectInt(16, 4, 16, 16);
        static readonly RectInt EarScreen = new RectInt(32, 20, 12, 16);
        static readonly RectInt JunctionFace = new RectInt(44, 32, 20, 12);
        static readonly RectInt EarLamp = new RectInt(32, 36, 12, 12);
        static readonly RectInt LedStrip = new RectInt(44, 44, 20, 4);
        /// <summary>頭の後ろの箱の裏の面（画面・灯りの列・接続口・四隅の螺子を一枚に）と、操作盤の外の面（留めた螺子二つ）</summary>
        static readonly RectInt CrownFace = new RectInt(0, 20, 26, 28);
        static readonly RectInt PodSide = new RectInt(44, 48, 16, 6);
        /// <summary>革の面の絵の置き場</summary>
        static readonly RectInt BackFrontArea = new RectInt(64, 64, 64, 64);
        static readonly RectInt BackRearArea = new RectInt(32, 64, 32, 64);
        static readonly RectInt ArmArea = new RectInt(0, 64, 32, 64);
        static readonly RectInt SeatArea = new RectInt(64, 0, 64, 40);
        static readonly RectInt FootArea = new RectInt(64, 40, 40, 24);
        /// <summary>革の面の絵が覆う広さ（椅子の座標、m）。座面とフットレストと肘掛けは上から、背もたれは前の基準の面へ真っすぐに写す</summary>
        const float SeatAreaZ0 = -0.215f, SeatAreaZ1 = 0.29f;
        const float FootAreaZ0 = 0.20f, FootAreaZ1 = 0.445f;
        const float ArmAreaX0 = 0.20f, ArmAreaX1 = 0.37f;
        const float ArmAreaZ0 = -0.19f, ArmAreaZ1 = 0.255f;

        /// <summary>
        /// 色の升（4×4 画素）。下の段に 16 個、左上の段（y 60）に金属と漆と革の 4 個。ケーブルや金具のような一色の物は升の真ん中を引く
        /// </summary>
        enum Swatch { Rubber, Plastic, Grey, Violet, Teal, Orange, Metal, Label, Cyan, VioletLit, Amber, Green, Brass, Glass, PlasticLight, Red, Chrome, Steel, Lacquer, Leather }

        static Vector2 SwatchUv(Swatch s)
        {
            var i = (int)s;
            return new Vector2(((i % 16) * 4 + 2f) / AtlasSize, (i < 16 ? 2f : 62f) / AtlasSize);
        }

        /// <summary>画素の置き場を uv の矩形にする。点で引くので、縁の画素の外を拾わないよう内へ少し寄せる</summary>
        static Rect PanelUv(RectInt r)
        {
            return new Rect((r.x + 0.05f) / AtlasSize, (r.y + 0.05f) / AtlasSize, (r.width - 0.1f) / AtlasSize, (r.height - 0.1f) / AtlasSize);
        }

        /// <summary>置き場 r の中の (u, v)（0〜1）を uv に。縁の画素の外を拾わないよう、半画素内に留める</summary>
        static Vector2 AreaUv(RectInt r, float u, float v)
        {
            var px = Mathf.Clamp(u * r.width, 0.5f, r.width - 0.5f);
            var py = Mathf.Clamp(v * r.height, 0.5f, r.height - 0.5f);
            return new Vector2((r.x + px) / AtlasSize, (r.y + py) / AtlasSize);
        }

        static Vector2 BackFrontUv(float x, float s) { return AreaUv(BackFrontArea, (x + BackHalfW) / (2f * BackHalfW), s / BackLength); }
        static Vector2 BackRearUv(float x, float s) { return AreaUv(BackRearArea, (x + BackHalfW) / (2f * BackHalfW), s / BackLength); }
        static Vector2 SeatUv(float x, float z) { return AreaUv(SeatArea, (x + SeatHalf) / (2f * SeatHalf), (z - SeatAreaZ0) / (SeatAreaZ1 - SeatAreaZ0)); }
        static Vector2 FootUv(float x, float z) { return AreaUv(FootArea, (x + FootHalf) / (2f * FootHalf), (z - FootAreaZ0) / (FootAreaZ1 - FootAreaZ0)); }
        static Vector2 ArmUv(float x, float z) { return AreaUv(ArmArea, (Mathf.Abs(x) - ArmAreaX0) / (ArmAreaX1 - ArmAreaX0), (z - ArmAreaZ0) / (ArmAreaZ1 - ArmAreaZ0)); }

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

        /// <summary>
        /// 縫い目の溝（0〜1。上の平らな面と、脇と前の丸みの境）。脇は |x| = weltX の線を前の縫い目まで、前は z = weltZ の線を脇の縫い目まで。
        /// 平らな板に見えないよう、上の面をクッションの形に区切る。mesh には持たせず、絵（陰と法線）に描く
        /// </summary>
        static float Welt(float x, float z, float weltX, float weltZ)
        {
            var ax = Mathf.Abs(x);
            var side = Mathf.Exp(-Mathf.Pow((ax - weltX) / 0.006f, 2f)) * (1f - Smooth(weltZ - 0.006f, weltZ + 0.004f, z));
            var front = Mathf.Exp(-Mathf.Pow((z - weltZ) / 0.006f, 2f)) * (1f - Smooth(weltX - 0.006f, weltX + 0.004f, ax));
            return Mathf.Max(side, front);
        }

        /// <summary>座面の縫い目の所（脇の丸みの始まりのすぐ内と、前の丸みの始まりのすぐ後ろ）</summary>
        const float SeatWeltX = 0.205f;
        const float SeatWeltZ = 0.232f;

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
        /// 上から見た前の角の丸み、脇の端を畳む時に厚みを測る前後の位置、絵の uv（上から写す）
        /// </summary>
        sealed class Cushion
        {
            public Func<float, float, float> Top;
            public Func<float, float, float> Under;
            public float Front, Back, Half, Edge, Corner, MidZ;
            public Func<float, float, Vector2> Uv;
        }

        static readonly Cushion SeatShape = new Cushion
        {
            Top = SeatTop, Under = SeatUnder, Front = SeatFront, Back = SeatBack, Half = SeatHalf, Edge = SeatEdge, Corner = SeatCorner, MidZ = 0.03f, Uv = SeatUv,
        };

        /// <summary>
        /// クッションの、左右の位置 x での横から見た輪郭（後ろの下から、下の面を前へ、前の丸み、上の面を後ろへ、後ろの丸み。12 点で閉じる）。
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
            // 下の面（後ろから前へ。ふくらはぎの逃げの曲がりを真ん中の一点で）
            pts.Add(new Vector3(x, yk - rk, zk));
            var zm = Mathf.Lerp(zk, zc, 0.6f);
            pts.Add(new Vector3(x, under(zm), zm));
            pts.Add(new Vector3(x, yn - rn, zc));
            // 前の丸み（下から上へ）
            foreach (var deg in new[] { -45f, 0f, 45f, 90f })
            {
                var a = deg * Mathf.Deg2Rad;
                pts.Add(new Vector3(x, yn + rn * Mathf.Sin(a), zc + rn * Mathf.Cos(a)));
            }
            // 上の面（前から後ろへ）
            foreach (var k in new[] { 0.3f, 0.65f, 1f })
            {
                var z = Mathf.Lerp(zc, zk, k);
                pts.Add(new Vector3(x, k >= 1f ? yk + rk : top(z), z));
            }
            // 後ろの丸み（上から下へ。最後は始まりの点に戻る）
            foreach (var deg in new[] { 180f, 270f })
            {
                var a = deg * Mathf.Deg2Rad;
                pts.Add(new Vector3(x, yk + rk * Mathf.Sin(a), zk + rk * Mathf.Cos(a)));
            }
            return pts;
        }

        static void Seat(Shop shop)
        {
            shop.Begin("SeatCushion", 55f, true);
            CushionGrid(shop, SeatShape, new List<float> { 0f, 0.09f, 0.17f, SeatHalf - SeatEdge, 0.24f, 0.258f, 0.268f, SeatHalf });
            shop.End(Orient.Whole);
        }

        /// <summary>
        /// クッションの皮を張る。輪は左の端（畳んで閉じる）から右の端まで、x の増える向きに並べる（halves は真ん中から右の端までの |x|）。
        /// 絵は上から写す（脇と前の丸みは縫い目の外の無地の所を引く）
        /// </summary>
        static void CushionGrid(Shop shop, Cushion c, List<float> halves)
        {
            var xs = new List<float>();
            for (var i = halves.Count - 1; i >= 1; i--) xs.Add(-halves[i]);
            xs.AddRange(halves);
            var rings = new List<List<Vector3>> { CushionRing(c, -c.Half, true) };
            foreach (var x in xs) rings.Add(CushionRing(c, x, false));
            rings.Add(CushionRing(c, c.Half, true));
            Rings(shop, Shop.Upholstery, rings, p => c.Uv(p.x, p.z));
        }

        /// <summary>同じ点の数の輪を並べて格子を張る。uv は点の位置から</summary>
        static void Rings(Shop shop, int sub, List<List<Vector3>> rings, Func<Vector3, Vector2> uv)
        {
            var n = rings[0].Count;
            var idx = new int[rings.Count, n];
            for (var i = 0; i < rings.Count; i++)
                for (var j = 0; j < n; j++)
                    idx[i, j] = shop.V(rings[i][j], uv(rings[i][j]));
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

        /// <summary>背もたれの、高さ s での半分の幅。腰は細く、肩から上は広い。上の角と下の角を丸める</summary>
        static float BackW(float s)
        {
            var w = Mathf.Lerp(BackWaistW, BackHalfW, Smooth(BackWaistTop, BackShoulder, s));
            if (s > BackLength - BackTopRound)
            {
                var k = s - (BackLength - BackTopRound);
                return w - BackTopRound + Mathf.Sqrt(Mathf.Max(0f, BackTopRound * BackTopRound - k * k));
            }
            if (s < BackBottomRound)
            {
                var k = BackBottomRound - s;
                return w - BackBottomRound + Mathf.Sqrt(Mathf.Max(0f, BackBottomRound * BackBottomRound - k * k));
            }
            return w;
        }

        /// <summary>縁の丸み。縁からの離れ d で 0 から 1 へ、半径 r の四分の一の円で立ち上がる</summary>
        static float RollR(float d, float r)
        {
            var k = 1f - Mathf.Clamp01(d / r);
            return Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
        }

        /// <summary>縫い目の溝（縫い目からの離れ d で、幅 7 mm ほど）</summary>
        static float Groove(float d)
        {
            return Mathf.Exp(-Mathf.Pow(d / 0.007f, 2f));
        }

        /// <summary>
        /// ボタン留めの膨らみ（0〜1）。ボタンを結ぶ斜めの線（菱形の辺）で 0、菱形の真ん中で 1。ボタンは (i·TuftDx, TuftS0 + j·TuftDs)、i + j が偶数の所
        /// </summary>
        static float TuftDome(float x, float s) { return TuftDome(x, s, 0.8f); }

        /// <summary>ボタン留めの膨らみ。power が小さいほど頂が平らで折り目の際が急（法線の絵は 0.8 で枕の形に、mesh は 1.3 で折り目の際をなだらかに）</summary>
        static float TuftDome(float x, float s, float power)
        {
            var a = x / TuftDx;
            var b = (s - TuftS0) / TuftDs;
            var m = 0.5f * (a + b);
            var n = 0.5f * (b - a);
            var fm = m - Mathf.Floor(m);
            var fn = n - Mathf.Floor(n);
            return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * fm) * Mathf.Sin(Mathf.PI * fn)), power);
        }

        /// <summary>いちばん近いボタンの位置 (x, s)</summary>
        static Vector2 TuftNearest(float x, float s)
        {
            var a = x / TuftDx;
            var b = (s - TuftS0) / TuftDs;
            var m = Mathf.Round(0.5f * (a + b));
            var n = Mathf.Round(0.5f * (b - a));
            return new Vector2((m - n) * TuftDx, TuftS0 + (m + n) * TuftDs);
        }

        /// <summary>ボタン留めの面の内か（1 で内、縫い目と縁の巻きと腰の下の所で 0 へ）</summary>
        static float TuftInside(float x, float s)
        {
            var side = BackW(s) - Mathf.Abs(x);
            var top = BackLength - s;
            return Smooth(BackBorder, BackBorder + 0.03f, side) * Smooth(TopBand, TopBand + 0.03f, top) * Smooth(0.10f, 0.16f, s);
        }

        /// <summary>背もたれの前の面の細かさ。下の面（菱形の膨らみ無し）、mesh の形（菱形の膨らみまで）、本当の面（絵に描く窪みと溝まで）</summary>
        enum BackDetail { Base, Mesh, Full }

        /// <summary>
        /// 背もたれの前の面（前の基準の面から前へ）。縁は真ん中の面まで丸く巻いて下ろし（裏の面と合う）、脇は太い巻きを縫い目で区切り、
        /// 上は前へ膨らむ頭の当ての巻きを縫い目で区切り、内の面は菱形のボタン留めで膨らませる。detail で細かさを選ぶ
        /// </summary>
        static float BackFrontT(float x, float s, BackDetail detail)
        {
            var side = BackW(s) - Mathf.Abs(x);
            var top = BackLength - s;
            var h = BackFrontH * RollR(Mathf.Min(side, top), BackRound);
            // 脇の巻き（縫い目の手前で平らな面へ下りる）と、上の巻き（頭の当て）
            var sideRise = BorderRise * RollR(side, 0.04f) * (1f - Smooth(BackBorder - 0.028f, BackBorder - 0.004f, side));
            var topRise = top < TopBand ? TopRise * Mathf.Pow(Mathf.Sin(Mathf.PI * top / TopBand), 0.8f) : 0f;
            var rise = Mathf.Lerp(topRise, sideRise, Smooth(TopBand - 0.012f, TopBand + 0.012f, top));
            // ボタン留めの膨らみ
            var inside = detail == BackDetail.Base ? 0f : TuftInside(x, s);
            // mesh の膨らみは低め（6 割）。絵は上から真っすぐに写すので、mesh の膨らみを高くすると、折り目の際の急な斜面に一画素が引き伸ばされて明るい筋になる。
            // 膨らみの丸い陰影は法線の絵（本当の高さ）が持つ
            var puff = detail == BackDetail.Full ? TuftPuff : TuftPuff * 0.6f;
            var tuft = inside > 0f ? inside * puff * TuftDome(x, s, detail == BackDetail.Full ? 0.8f : 1.3f) : 0f;
            var fineDepth = detail == BackDetail.Full ? BackFine(x, s) : 0f;
            return BackMid + RollR(s, BackRound) * (h + rise + tuft - fineDepth);
        }

        static float BackFrontT(float x, float s) { return BackFrontT(x, s, BackDetail.Mesh); }

        /// <summary>背もたれの前の面の点（細かさ detail で）</summary>
        static Vector3 BackFrontP(float x, float s, BackDetail detail) { return Back(x, s, BackFrontT(x, s, detail)); }

        /// <summary>
        /// 背もたれの前の、下の面（菱形の膨らみ無し）の向き。接線（x の向き）・従接線（s の向き）・法線。
        /// ボタン留めの面の mesh の法線はこの法線にし、膨らみと窪みの陰影は法線の絵で出す（粗い mesh でも膨らみが丸く陰る）
        /// </summary>
        static void BackBaseFrame(float x, float s, out Vector3 tangent, out Vector3 bitangent, out Vector3 normal)
        {
            const float e = 0.004f;
            tangent = (BackFrontP(x + e, s, BackDetail.Base) - BackFrontP(x - e, s, BackDetail.Base)).normalized;
            var along = BackFrontP(x, s + e, BackDetail.Base) - BackFrontP(x, s - e, BackDetail.Base);
            normal = Vector3.Cross(along, tangent).normalized;
            if (Vector3.Dot(normal, BackFwd) < 0f) normal = -normal;
            bitangent = Vector3.Cross(normal, tangent).normalized;
            if (Vector3.Dot(bitangent, along) < 0f) bitangent = -bitangent;
        }

        /// <summary>
        /// 背もたれの前の面の、絵に描く法線（接線の座標。x は絵の右＝椅子の右、y は絵の上＝背もたれの上）。
        /// 本当の面（菱形の膨らみ・ボタンの窪み・折り目・縫い目）の法線を、下の面の向きで表す
        /// </summary>
        static Vector3 BackFrontNormal(float x, float s, float dx, float ds)
        {
            Vector3 t, b, n;
            BackBaseFrame(x, s, out t, out b, out n);
            var ux = BackFrontP(x + dx, s, BackDetail.Full) - BackFrontP(x - dx, s, BackDetail.Full);
            var us = BackFrontP(x, s + ds, BackDetail.Full) - BackFrontP(x, s - ds, BackDetail.Full);
            var real = Vector3.Cross(us, ux).normalized;
            if (Vector3.Dot(real, n) < 0f) real = -real;
            var ts = new Vector3(Vector3.Dot(real, t), Vector3.Dot(real, b), Vector3.Dot(real, n));
            // 傾きは 40 度までに抑える。部屋の明かりでは椅子の明るさの多くが照り返し（斜めから見るほど強い）なので、
            // 折り目の際の急な画素が一画素の明るい筋になる
            var tilt = new Vector2(ts.x, ts.y);
            var limit = Mathf.Tan(40f * Mathf.Deg2Rad) * ts.z;
            if (tilt.magnitude > limit) tilt = tilt.normalized * limit;
            return new Vector3(tilt.x, tilt.y, ts.z).normalized;
        }

        /// <summary>
        /// 背もたれの前の面の細かい窪み（m、正で窪む）。ボタンの窪みと、脇と上の縫い目の溝。mesh には持たせず、絵に描く
        /// （菱形の折り目は膨らみの形そのものが持つ。溝を足すと、明かりを受けた片側が細い明るい線になって筋に見えた）
        /// </summary>
        static float BackFine(float x, float s)
        {
            var side = BackW(s) - Mathf.Abs(x);
            var top = BackLength - s;
            var seam = Mathf.Max(top > TopBand - 0.01f ? Groove(side - BackBorder) : 0f, Groove(top - TopBand) * Smooth(0.005f, 0.03f, side));
            var depth = SeamDeep * seam;
            var inside = TuftInside(x, s);
            if (inside > 0f)
            {
                var near = TuftNearest(x, s);
                var dip = TuftDip * Mathf.Exp(-Mathf.Pow((new Vector2(x, s) - near).magnitude / 0.015f, 2f));
                depth += inside * dip;
            }
            return depth;
        }

        /// <summary>菱形の折り目（ボタンを結ぶ斜めの線）への近さ（0〜1。線の上で 1、1.2 cm ほどで消える）。地の色の陰に使う</summary>
        static float TuftCrease(float x, float s)
        {
            var a = x / TuftDx;
            var b = (s - TuftS0) / TuftDs;
            var m = 0.5f * (a + b);
            var n = 0.5f * (b - a);
            // m・n の 1 あたりの長さ（m）
            var unit = 1f / (0.5f * Mathf.Sqrt(1f / (TuftDx * TuftDx) + 1f / (TuftDs * TuftDs)));
            var dm = Mathf.Abs(m - Mathf.Round(m)) * unit;
            var dn = Mathf.Abs(n - Mathf.Round(n)) * unit;
            return Mathf.Exp(-Mathf.Pow(Mathf.Min(dm, dn) / 0.012f, 2f));
        }

        /// <summary>背もたれの裏の面（前の基準の面から前へ。負）。平らな裏から、縁で真ん中の面まで丸く巻き上げる</summary>
        static float BackRearT(float x, float s)
        {
            var side = BackW(s) - Mathf.Abs(x);
            var d = Mathf.Min(side, Mathf.Min(BackLength - s, s));
            return BackMid - BackRearH * RollR(d, BackRound);
        }

        /// <summary>ボタンの位置 (x, s)。ボタン留めの面の内（縫い目から 3 cm 以上内）の格子の点。5 段で 3・2・3・2・3 個</summary>
        static List<Vector2> Buttons()
        {
            var list = new List<Vector2>();
            for (var j = 0; j < 8; j++)
                for (var i = -4; i <= 4; i++)
                {
                    if (((i + j) & 1) != 0) continue;
                    var p = new Vector2(i * TuftDx, TuftS0 + j * TuftDs);
                    if (TuftInside(p.x, p.y) > 0.99f) list.Add(p);
                }
            return list;
        }

        /// <summary>
        /// 背もたれの格子の段（倒れた向きに沿った高さ s）。下の縁の巻き、ボタン留めの格子の段（ボタンと菱形の真ん中と折り目の中ほどに乗る、6 cm おき）、
        /// 頭の当ての巻き
        /// </summary>
        static List<float> BackRows()
        {
            var ss = new List<float> { 0f, 0.035f, 0.07f };
            for (var s = TuftS0 - TuftDs * 0.5f; s <= BackLength - TopBand + 0.001f; s += TuftDs * 0.5f) ss.Add(s);
            foreach (var s in new[] { 0.83f, 0.87f, 0.90f, 0.925f, 0.942f, BackLength }) ss.Add(s);
            return ss;
        }

        /// <summary>
        /// 段 s の格子の横の位置（前）。縁の巻き（縁から 0・3・6 cm）と、縫い目の内の一列、ボタン留めの格子の列（4 cm おきに 9 列）。
        /// 上と下の縁で幅が狭くなる所は、格子の列を幅に合わせて詰める
        /// </summary>
        static List<float> BackFrontColumns(float s)
        {
            var w = BackW(s);
            var lat = Mathf.Min(1f, (w - 0.09f) / (4f * TuftDx * 0.5f));
            var edge = new[] { 0f, 0.03f, 0.06f };
            var xs = new List<float>();
            foreach (var e in edge) xs.Add(-w + e);
            var last = 4f * TuftDx * 0.5f * lat;
            xs.Add(-0.5f * ((w - 0.06f) + last));
            for (var k = -4; k <= 4; k++) xs.Add(k * TuftDx * 0.5f * lat);
            xs.Add(0.5f * ((w - 0.06f) + last));
            for (var k = edge.Length - 1; k >= 0; k--) xs.Add(w - edge[k]);
            return xs;
        }

        /// <summary>段 s の格子の横の位置（裏）。縁の巻きと真ん中だけ</summary>
        static List<float> BackRearColumns(float s)
        {
            var w = BackW(s);
            return new List<float> { -w, -w + 0.025f, -w + 0.06f, 0f, w - 0.06f, w - 0.025f, w };
        }

        /// <summary>
        /// 背もたれ。前も裏も黒革で包んだ厚いクッション（横から見て縁が 14 cm の厚みで丸く巻く）。脇は太い巻きを縫い目で区切り、
        /// 上は前へ膨らむ頭の当ての巻き、内は大きな菱形のボタン留め（13 個）。肩から上を広く、腰を細く絞る。
        /// mesh はボタン留めの格子に合わせた粗い格子で、菱形の膨らみ（真ん中が高く、折り目が低い）までを持つ。
        /// ボタンの窪み・折り目の鋭さ・縫い目は絵（陰と法線）に描く
        /// </summary>
        static void Backrest(Shop shop)
        {
            var ss = BackRows();
            // 前。四角は、四隅の真ん中の本当の高さに近い方の対角で割る（菱形の折り目は面の辺に、膨らみの頂は尾根に乗る）
            shop.Begin("BackCushion", 50f, true);
            var front = new List<List<int>>();
            var at = new List<List<Vector2>>();
            foreach (var s in ss)
            {
                var row = new List<int>();
                var xs = BackFrontColumns(s);
                var rowAt = new List<Vector2>();
                foreach (var x in xs)
                {
                    row.Add(shop.V(Back(x, s, BackFrontT(x, s)), BackFrontUv(x, s)));
                    rowAt.Add(new Vector2(x, s));
                }
                front.Add(row);
                at.Add(rowAt);
            }
            for (var j = 0; j < front.Count - 1; j++)
                for (var i = 0; i < front[j].Count - 1; i++)
                {
                    var mid = 0.25f * (at[j][i] + at[j + 1][i] + at[j + 1][i + 1] + at[j][i + 1]);
                    Split(shop, Shop.Upholstery, front[j][i], front[j + 1][i], front[j + 1][i + 1], front[j][i + 1], Back(mid.x, mid.y, BackFrontT(mid.x, mid.y)), BackFwd);
                }
            shop.End(Orient.Toward, BackFwd);
            // ボタン留めの面の法線は下の面の法線にする（膨らみの陰影は法線の絵が持つ）
            for (var j = 0; j < front.Count; j++)
                for (var i = 0; i < front[j].Count; i++)
                {
                    var p = at[j][i];
                    if (TuftInside(p.x, p.y) <= 0f) continue;
                    Vector3 t, b, n;
                    BackBaseFrame(p.x, p.y, out t, out b, out n);
                    shop.SetNormal(front[j][i], n);
                }

            // 裏（前と同じ段で、横は縁の巻きと真ん中だけ。脇の縁は前と同じ点に乗る）。体の側から見て裏の面の向こうは中なので、食い込みの確かめには数えない
            shop.Begin("BackRear", 50f, false);
            var rear = new List<List<int>>();
            foreach (var s in ss)
            {
                var row = new List<int>();
                foreach (var x in BackRearColumns(s)) row.Add(shop.V(Back(x, s, BackRearT(x, s)), BackRearUv(x, s)));
                rear.Add(row);
            }
            for (var j = 0; j < rear.Count - 1; j++)
                for (var i = 0; i < rear[j].Count - 1; i++)
                    shop.Q(Shop.Upholstery, rear[j][i], rear[j + 1][i], rear[j + 1][i + 1], rear[j][i + 1]);
            shop.End(Orient.Toward, -BackFwd);
        }

        /// <summary>四角 a-b-c-d を、二つの対角のうち、真ん中の高さ（dir の向き）が本当の面の真ん中 centre に近い方で割る</summary>
        static void Split(Shop shop, int sub, int a, int b, int c, int d, Vector3 centre, Vector3 dir)
        {
            var h = Vector3.Dot(centre, dir);
            var ac = Mathf.Abs(0.5f * Vector3.Dot(shop.Pos(a) + shop.Pos(c), dir) - h);
            var bd = Mathf.Abs(0.5f * Vector3.Dot(shop.Pos(b) + shop.Pos(d), dir) - h);
            if (ac <= bd) shop.Q(sub, a, b, c, d);
            else
            {
                shop.T(sub, a, b, d);
                shop.T(sub, b, c, d);
            }
        }

        /// <summary>
        /// 背もたれの上の縁を越える道筋（左右の位置 x で、裏の面の s0 から上の縁を回って前の面の s1 まで）。面から gap 離す
        /// </summary>
        static List<Vector3> OverTop(float x, float s0, float s1, float gap)
        {
            var sec = new List<Vector2>();
            foreach (var d in new[] { 0.06f, 0.03f, 0.012f, 0.003f })
            {
                var s = BackLength - d;
                if (s >= s0) sec.Add(new Vector2(s, BackRearT(x, s)));
            }
            sec.Add(new Vector2(BackLength, BackMid));
            foreach (var d in new[] { 0.003f, 0.012f, 0.03f, 0.06f })
            {
                var s = BackLength - d;
                if (s >= s1) sec.Add(new Vector2(s, BackFrontT(x, s)));
            }
            var pts = new List<Vector3>();
            for (var i = 0; i < sec.Count; i++)
            {
                var t = sec[Mathf.Min(i + 1, sec.Count - 1)] - sec[Mathf.Max(i - 1, 0)];
                var n = new Vector2(t.y, -t.x).normalized;
                var p = sec[i] + n * gap;
                pts.Add(Back(x, p.x, p.y));
            }
            return pts;
        }

        // ---- 肘掛け ----------------------------------------------------------------

        /// <summary>
        /// 肘掛けの当て物の断面（前後の位置 z。11 点で閉じる）。内の下、内の脇（わずかに膨らむ）、内の上の角の太い丸み、上、外の上の角、外の脇、
        /// 外の下。上の面の一番高い所が肘掛けの上面の線（PadTop）
        /// </summary>
        static List<Vector3> PadRing(float sx, float z, bool collapse)
        {
            var endBack = 0.04f;
            var endFront = 0.03f;
            var d = 0f;
            if (z < PadBack + endBack) d = endBack - Mathf.Sqrt(Mathf.Max(0f, endBack * endBack - Mathf.Pow(PadBack + endBack - z, 2f)));
            if (z > PadFront - endFront) d = endFront - Mathf.Sqrt(Mathf.Max(0f, endFront * endFront - Mathf.Pow(z - (PadFront - endFront), 2f)));
            var top = PadTop(z);
            var thick = PadThickAt(z);
            var bottom = top - thick;
            var inner = PadIn(z);
            if (collapse) d = 0.5f * Mathf.Min(thick, PadOuter - inner);
            var xi = inner + d;
            var xo = PadOuter - d;
            var yt = top - d * 0.9f;
            var yb = bottom + d;
            if (collapse) { xi = xo = 0.5f * (inner + PadOuter); yt = yb = 0.5f * (top + bottom); }
            var rt = Mathf.Min(PadRound(z), 0.5f * Mathf.Max(0.0005f, yt - yb), 0.5f * Mathf.Max(0.0005f, xo - xi));
            var bulge = collapse ? 0f : 0.004f * Mathf.Clamp01((yt - yb) / 0.05f);
            var mid = Mathf.Lerp(yb, yt - rt, 0.5f);
            // 下の角の寄せ（畳んだ端では 0。端の点を一つに集める）
            var foot = collapse ? 0f : 0.006f;
            var pts = new List<Vector3>
            {
                new Vector3(xi + foot, yb, z),
                new Vector3(xi - bulge, mid, z),
                new Vector3(xi, yt - rt, z),
                new Vector3(xi + rt * (1f - 0.7071f), yt - rt * (1f - 0.7071f), z),
                new Vector3(xi + rt, yt, z),
                new Vector3(xo - rt, yt, z),
                new Vector3(xo - rt * (1f - 0.7071f), yt - rt * (1f - 0.7071f), z),
                new Vector3(xo, yt - rt, z),
                new Vector3(xo + bulge, mid, z),
                new Vector3(xo - foot, yb, z),
            };
            pts.Add(pts[0]);
            for (var i = 0; i < pts.Count; i++) pts[i] = new Vector3(sx * pts[i].x, pts[i].y, pts[i].z);
            return pts;
        }

        /// <summary>
        /// 肘掛け。厚く丸い革の当て物（肘を置く後ろ半分は 7.2 cm の厚みで上の角を太く丸め、前の端へ細くする）を、黒い漆の台に載せ、
        /// 磨いた金属の腕が座の皿の下から支える。前の端の外の脇に、後から金具で留めた操作盤（黒い金属の箱と、当て物の下から回り込む金属の留め具。
        /// 外の面の螺子は絵）。操作盤のケーブルは腕に沿って座の下へ
        /// </summary>
        static void Arm(Shop shop, float sx)
        {
            var name = sx > 0f ? "R" : "L";
            // 当て物
            shop.Begin("ArmPad" + name, 55f, true);
            var zs = new[] { PadBack + 0.012f, PadBack + 0.04f, -0.08f, 0.04f, 0.12f, 0.19f, PadFront - 0.022f, PadFront - 0.008f, PadFront - 0.002f };
            var rings = new List<List<Vector3>>();
            rings.Add(PadRing(sx, PadBack, true));
            foreach (var z in zs) rings.Add(PadRing(sx, z, false));
            rings.Add(PadRing(sx, PadFront, true));
            Rings(shop, Shop.Upholstery, rings, p => ArmUv(p.x, p.z));
            shop.End(Orient.Whole);

            // 当て物の下の黒い漆の台（厚い後ろ半分の下）
            var body = new Vector3[8];
            var zb = PadBack + 0.03f;
            var zf = 0.08f;
            Func<float, float> under = z => PadTop(z) - PadThickAt(z) - 0.012f;
            Func<float, float> over = z => PadTop(z) - PadThickAt(z) + 0.004f;
            var ib = sx * (PadIn(zb) + 0.014f);
            var iF = sx * (PadIn(zf) + 0.014f);
            var outer = sx * (PadOuter - 0.01f);
            body[0] = new Vector3(ib, under(zb), zb); body[1] = new Vector3(outer, under(zb), zb);
            body[2] = new Vector3(outer, under(zf), zf); body[3] = new Vector3(iF, under(zf), zf);
            body[4] = new Vector3(ib, over(zb), zb); body[5] = new Vector3(outer, over(zb), zb);
            body[6] = new Vector3(outer, over(zf), zf); body[7] = new Vector3(iF, over(zf), zf);
            Hexa(shop, "ArmBase" + name, body, Shop.Shell, null, true);

            // 磨いた金属の腕（台の下から後ろへ下り、座の皿の下へ入る）
            var support = new List<Vector3>
            {
                new Vector3(sx * 0.30f, under(0.06f) + 0.002f, 0.06f),
                new Vector3(sx * 0.305f, 0.545f, -0.01f),
                new Vector3(sx * 0.30f, 0.49f, -0.075f),
                new Vector3(sx * 0.27f, 0.435f, -0.11f),
                new Vector3(sx * 0.20f, 0.418f, -0.11f),
            };
            Tube(shop, "ArmSupport" + name, Shop.Chrome, Spline(support, 6), 0.014f, 4, false, Vector2.zero, 65f);

            // 前の操作盤。後ろの低い縁から前の高い縁へ上がる斜めの面を、座った目の方（後ろ・上・内）へ向ける。外の面は螺子二つの絵
            var pod = new Vector3[8];
            var pi = sx * PodInner;
            var po = sx * PodOuter;
            const float podUnder = 0.607f;
            pod[0] = new Vector3(pi, podUnder, PodBack); pod[1] = new Vector3(po, podUnder, PodBack);
            pod[2] = new Vector3(po, podUnder, PodFront - 0.03f); pod[3] = new Vector3(pi, podUnder, PodFront - 0.03f);
            pod[4] = new Vector3(pi, 0.645f, PodBack); pod[5] = new Vector3(po, 0.662f, PodBack);
            pod[6] = new Vector3(po, 0.716f, PodFront); pod[7] = new Vector3(pi, 0.70f, PodFront);
            var faces = new int[] { Shop.Frame, Shop.Panel, Shop.Frame, Shop.Frame, Shop.Frame, Shop.Panel };
            var podUvs = new Rect?[] { null, PanelUv(sx > 0f ? RightPod : LeftPod), null, null, null, PanelUv(PodSide) };
            Hexa(shop, "ArmPod" + name, pod, faces, podUvs, true, sx < 0f);
            // 留め具。当て物の下を横切る板と、当て物の外の脇に立つ板（L の形）で操作盤を抱える
            Slab(shop, "PodClampUnder" + name, Shop.Chrome, new Vector3(sx * 0.355f, 0.603f, 0.23f), Quaternion.identity, new Vector3(0.15f, 0.006f, 0.06f), 0f, true);
            Slab(shop, "PodClampSide" + name, Shop.Chrome, new Vector3(sx * 0.3485f, 0.629f, 0.23f), Quaternion.identity, new Vector3(0.005f, 0.052f, 0.05f), 0f, true);
        }

        /// <summary>右の肘掛けの差込口。革に螺子で留めた金属の角の板に、金属の縁、黒い穴、穴を囲む紫の灯り（ジャックを挿す所の合図）</summary>
        static void Port(Shop shop)
        {
            var c = PortAt;
            Slab(shop, "PortPlate", Shop.Chrome, new Vector3(c.x, PadTop(c.z) + 0.0025f, c.z), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.056f, 0.004f, 0.056f), 0f, true);
            Tube(shop, "PortBezel", Shop.Chrome, new List<Vector3> { new Vector3(c.x, 0.648f, c.z), new Vector3(c.x, 0.6725f, c.z) }, 0.022f, 8, true, Vector2.zero, 70f);
            Ring(shop, "PortGlow", new Vector3(c.x, 0.6735f, c.z), Vector3.up, 0.0145f, 0.0195f, 8, SwatchUv(Swatch.VioletLit));
            Disc(shop, "PortHole", new Vector3(c.x, 0.6740f, c.z), Vector3.up, 0.0145f, 8, SwatchUv(Swatch.Rubber));
        }

        // ---- 座の下 ------------------------------------------------------------------

        static void Undercarriage(Shop shop)
        {
            // 座の皿（黒い漆）
            Slab(shop, "SeatPan", Shop.Shell, new Vector3(0f, 0.4175f, -0.03f), Quaternion.identity, new Vector3(0.53f, 0.035f, 0.40f), 0f, true);
            // リクライニングの機構の箱
            Slab(shop, "Mechanism", Shop.Frame, new Vector3(0f, 0.372f, -0.02f), Quaternion.identity, new Vector3(0.24f, 0.055f, 0.25f), 0f, true);
            // 右はリクライニングのレバー、左は高さのレバー。先に黒い握り
            Tube(shop, "LeverR", Shop.Chrome, new List<Vector3> { new Vector3(0.11f, 0.366f, 0.04f), new Vector3(0.19f, 0.360f, 0.058f), new Vector3(0.232f, 0.355f, 0.072f) }, 0.006f, 4, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripR", Shop.Panel, new Vector3(0.25f, 0.354f, 0.078f), Quaternion.Euler(0f, 18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0f, true, SwatchUv(Swatch.Rubber));
            Tube(shop, "LeverL", Shop.Chrome, new List<Vector3> { new Vector3(-0.11f, 0.366f, 0.0f), new Vector3(-0.19f, 0.360f, 0.018f), new Vector3(-0.232f, 0.355f, 0.032f) }, 0.006f, 4, false, Vector2.zero, 70f);
            Slab(shop, "LeverGripL", Shop.Panel, new Vector3(-0.25f, 0.354f, 0.038f), Quaternion.Euler(0f, -18f, 0f), new Vector3(0.036f, 0.015f, 0.028f), 0f, true, SwatchUv(Swatch.Rubber));
            // 前の張りの摘み
            Tube(shop, "TensionKnob", Shop.Panel, new List<Vector3> { new Vector3(0f, 0.35f, 0.105f), new Vector3(0f, 0.35f, 0.135f) }, 0.026f, 6, true, SwatchUv(Swatch.Rubber), 70f);

            // 背もたれを支える左右の磨いた金属の腕（座の下から、座のクッションの後ろを回って背もたれの裏へ）と、倒れの軸の蓋
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.13f, 0.385f, -0.15f),
                    new Vector3(sx * 0.13f, 0.405f, -0.245f),
                    new Vector3(sx * 0.13f, 0.43f, -0.268f),
                    Back(sx * 0.13f, 0.13f, -BackThick + 0.004f),
                };
                Tube(shop, "ReclineArm" + (sx > 0f ? "R" : "L"), Shop.Chrome, Spline(path, 5), 0.012f, 4, false, Vector2.zero, 70f);
                Tube(shop, "ReclineHub" + (sx > 0f ? "R" : "L"), Shop.Chrome, new List<Vector3> { new Vector3(sx * 0.115f, 0.418f, -0.26f), new Vector3(sx * 0.152f, 0.418f, -0.26f) }, 0.022f, 6, true, Vector2.zero, 70f);
            }

            // ガスシリンダー。磨いた覆いと芯
            TubeR(shop, "ColumnCover", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.235f, 0f) }, new List<float> { 0.046f, 0.038f }, 8, true, Vector2.zero, 70f);
            Tube(shop, "ColumnRod", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.225f, 0f), new Vector3(0f, 0.345f, 0f) }, 0.024f, 6, false, Vector2.zero, 70f);
            Tube(shop, "ColumnCollar", Shop.Frame, new List<Vector3> { new Vector3(0f, 0.328f, 0f), new Vector3(0f, 0.346f, 0f) }, 0.04f, 6, true, Vector2.zero, 70f);
        }

        // ---- 五本脚とキャスター --------------------------------------------------------

        static void Base(Shop shop)
        {
            Tube(shop, "Hub", Shop.Chrome, new List<Vector3> { new Vector3(0f, 0.075f, 0f), new Vector3(0f, 0.132f, 0f) }, 0.056f, 8, true, Vector2.zero, 70f);
            for (var k = 0; k < 5; k++)
            {
                // 一本は真後ろ。前の二本は左右へ 36 度（机の側へ脚を突き出さない）。磨いた金属。断面は上の細い台形
                var a = (180f + 72f * k) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var side = Vector3.Cross(Vector3.up, dir);
                shop.Begin("Leg" + k, 40f, true);
                var rings = new List<List<Vector3>>();
                foreach (var r in new[] { 0.03f, 0.16f, LegReach })
                {
                    var k2 = Mathf.InverseLerp(0.03f, LegReach, r);
                    var w = Mathf.Lerp(0.026f, 0.017f, k2);
                    var yt = Mathf.Lerp(0.128f, 0.086f, k2);
                    var yb = Mathf.Lerp(0.084f, 0.062f, k2);
                    var c = dir * r;
                    var ring = new List<Vector3>
                    {
                        c - side * w + Vector3.up * yb,
                        c + side * w + Vector3.up * yb,
                        c + side * w * 0.65f + Vector3.up * yt,
                        c - side * w * 0.65f + Vector3.up * yt,
                    };
                    ring.Add(ring[0]);
                    rings.Add(ring);
                }
                Rings(shop, Shop.Chrome, rings, p => Vector2.zero);
                // 先の蓋
                var tip = rings[rings.Count - 1];
                shop.Q(Shop.Chrome, shop.V(tip[0], Vector2.zero), shop.V(tip[1], Vector2.zero), shop.V(tip[2], Vector2.zero), shop.V(tip[3], Vector2.zero));
                shop.End(Orient.Each);

                // キャスター。黒い覆いと、二つ並んだ車（一つの筒で見せる）
                var at = dir * LegReach;
                Slab(shop, "CasterHood" + k, Shop.Shell, at + Vector3.up * 0.046f, Quaternion.LookRotation(dir), new Vector3(0.034f, 0.03f, 0.05f), 0f, true);
                Tube(shop, "Wheel" + k, Shop.Panel, new List<Vector3> { at - side * 0.026f + Vector3.up * 0.026f, at + side * 0.026f + Vector3.up * 0.026f }, 0.026f, 6, true, SwatchUv(Swatch.Rubber), 50f);
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

        /// <summary>フットレストのパッドの縫い目の所（脇の丸みのすぐ内と、前の丸みのすぐ後ろ。絵に描く）</summary>
        const float FootWeltX = 0.168f;
        const float FootWeltZ = 0.405f;
        static float FootPadTop(float x, float z) { return FootTop(z); }
        static float FootPadUnder(float x, float z) { return FootTop(z) - FootThick; }

        static readonly Cushion FootShape = new Cushion
        {
            Top = FootPadTop, Under = FootPadUnder, Front = FootFront, Back = FootBack, Half = FootHalf, Edge = 0.02f, Corner = 0.03f, MidZ = 0.32f, Uv = FootUv,
        };

        /// <summary>
        /// 座の前の下から引き出すフットレスト（リクライニングの椅子の脚置きの形）。座の皿の下の収め口から左右の磨いた金属のレールが前へ出て、
        /// 先の軸から磨いた金属の腕が下りてパッドの脇を支える。机との間が狭いので伸ばしきらず、座った形の足の裏が乗る所（床の近く）まで下ろした形。
        /// パッドは座面と同じ厚い革のクッション
        /// </summary>
        static void Footrest(Shop shop)
        {
            shop.Begin("FootPad", 55f, true);
            CushionGrid(shop, FootShape, new List<float> { 0f, 0.12f, FootHalf - 0.02f, 0.194f, FootHalf });
            shop.End(Orient.Whole);
            // 座の皿の下の収め口
            Slab(shop, "FootHousing", Shop.Shell, new Vector3(0f, 0.391f, 0.095f), Quaternion.identity, new Vector3(0.47f, 0.018f, 0.15f), 0f, true);
            foreach (var sx in new[] { 1f, -1f })
            {
                var n = sx > 0f ? "R" : "L";
                var top = new Vector3(sx * FootRailEnd.x, FootRailEnd.y, FootRailEnd.z);
                // 引き出したレール
                Slab(shop, "FootRail" + n, Shop.Chrome, new Vector3(top.x, top.y, 0.14f), Quaternion.identity, new Vector3(0.022f, 0.016f, 0.10f), 0f, true);
                var pad = new Vector3(top.x, FootTop(FootPivot) - 0.5f * FootThick, FootPivot);
                var seg = pad - top;
                Tube(shop, "FootKnuckle" + n, Shop.Frame, new List<Vector3> { top - Vector3.right * 0.013f, top + Vector3.right * 0.013f }, 0.013f, 6, true, Vector2.zero, 50f);
                Slab(shop, "FootArm" + n, Shop.Chrome, 0.5f * (top + pad), Quaternion.LookRotation(seg.normalized, Vector3.forward), new Vector3(0.014f, 0.03f, seg.magnitude), 0f, true);
                Tube(shop, "FootPivot" + n, Shop.Frame, new List<Vector3> { new Vector3(sx * 0.197f, pad.y, pad.z), new Vector3(sx * 0.229f, pad.y, pad.z) }, 0.012f, 6, true, Vector2.zero, 50f);
            }
        }

        // ---- 頭の後ろの端末 ------------------------------------------------------------

        /// <summary>裏の端末の箱の高さの寄せ（背もたれの上の縁の巻きに掛からないよう、上の縁から 6.5 cm 下に箱の上の縁を置く）</summary>
        const float CrownDs = -0.045f;
        /// <summary>裏の端末の箱の裏の面（前の基準の面から前へ。負）</summary>
        const float CrownOut = -0.165f;
        /// <summary>耳の筐体の高さ（倒れた向きに沿って）と、左右の位置（背もたれの肩の縁の 3.6 cm 外）</summary>
        const float EarS = 0.83f;
        static float EarX { get { return BackW(EarS) + 0.036f; } }

        /// <summary>
        /// 後から取り付けた潜行の装置。背もたれの裏の頭の所に黒い金属の箱（裏の面に画面・灯りの列・接続口・四隅の螺子の絵、挿さった三本の端子）を、
        /// 背もたれの上の縁の巻きを越えて前へ掛かる二本の磨いた金属の留め具で留める。箱の脇から磨いた金属の腕を背もたれの縁の外へ回し、
        /// 背もたれの肩の外に耳の筐体（右は前を向いた小さな画面、左は灯りの輪）を出す。
        /// どれも頭の後ろ（前へ 0.33 m より後ろ）にあり、座った目と、背を預けて天井を仰ぐ目の後ろに来る
        /// </summary>
        static void Crown(Shop shop)
        {
            var rot = Quaternion.LookRotation(BackFwd, BackUp);
            const float tOut = CrownOut;
            var earX = EarX;
            foreach (var sx in new[] { 1f, -1f })
            {
                var n = sx > 0f ? "R" : "L";
                var c = Back(sx * earX, EarS, 0.0f);
                Slab(shop, "EarPod" + n, Shop.Frame, c, rot, new Vector3(0.058f, 0.15f, 0.10f), 0f, true);
                var face = Back(sx * earX, EarS, 0.0505f);
                if (sx > 0f)
                    Decal(shop, "EarScreen", face, Vector3.right * 0.021f, BackUp * 0.056f, BackFwd, PanelUv(EarScreen));
                else
                    Decal(shop, "EarLamp", face + BackUp * 0.02f, Vector3.right * 0.021f, BackUp * 0.021f, BackFwd, PanelUv(EarLamp));
                // 外の面の灯りの列（縦）
                Decal(shop, "EarLeds" + n, Back(sx * (earX + 0.0295f), EarS, 0.0f), BackUp * 0.05f, BackFwd * 0.0075f, Vector3.right * sx, PanelUv(LedStrip));
                // 箱の脇から背もたれの裏を横へ、肩の縁の外を回って耳の筐体の裏へ入る腕
                var arm = new List<Vector3>
                {
                    Back(sx * 0.12f, EarS + 0.012f, -0.13f),
                    Back(sx * 0.22f, EarS + 0.01f, -0.122f),
                    Back(sx * 0.29f, EarS + 0.006f, -0.108f),
                    Back(sx * (earX - 0.012f), EarS + 0.003f, -0.08f),
                    Back(sx * earX, EarS, -0.045f),
                };
                Tube(shop, "EarMount" + n, Shop.Chrome, Spline(arm, 5), 0.009f, 4, false, Vector2.zero, 60f);
            }

            // 裏の箱（黒い金属の箱。前の面は背の裏に 4 mm 埋める）と、裏の面の絵（画面・灯りの列・接続口・四隅の螺子）
            var boxS = 0.795f + CrownDs;
            Slab(shop, "CrownBox", Shop.Frame, Back(0f, boxS, 0.5f * (tOut - BackThick + 0.004f)), rot, new Vector3(0.25f, 0.27f, -BackThick + 0.004f - tOut), 0f, true);
            Decal(shop, "CrownFace", Back(0f, boxS, tOut - 0.0008f), Vector3.right * 0.125f, BackUp * 0.135f, -BackFwd, PanelUv(CrownFace));
            // 箱の上の縁から背もたれの上の縁の巻きを越えて前へ掛かる留め具（二本）
            foreach (var x in new[] { -0.08f, 0.08f })
            {
                var top = 0.93f + CrownDs;
                var strap = new List<Vector3>
                {
                    Back(x, top - 0.03f, tOut - 0.004f),
                    Back(x, top + 0.004f, tOut + 0.002f),
                    Back(x, top + 0.004f, -0.105f),
                };
                strap.AddRange(OverTop(x, top + 0.012f, BackLength - 0.065f, 0.006f));
                Tube(shop, "CrownStrap", Shop.Chrome, Spline(strap, 10), 0.006f, 3, false, Vector2.zero, 60f);
            }
            // 挿さった三本の端子（金属の胴と、色の付いた根元）
            var colours = new[] { Swatch.Grey, Swatch.Rubber, Swatch.Violet };
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * 0.05f;
                var s = 0.715f + CrownDs;
                Tube(shop, "Plug" + i, Shop.Panel, new List<Vector3> { Back(x, s, tOut + 0.004f), Back(x, s, tOut - 0.024f) }, 0.011f, 4, false, SwatchUv(Swatch.Metal), 50f);
                Tube(shop, "PlugBoot" + i, Shop.Panel, new List<Vector3> { Back(x, s, tOut - 0.024f), Back(x, s, tOut - 0.040f) }, 0.0085f, 4, true, SwatchUv(colours[i]), 50f);
            }

            // 腰の後ろの分岐の箱
            const float jOut = -0.155f;
            Slab(shop, "Junction", Shop.Frame, Back(0f, 0.23f, 0.5f * (jOut - BackThick + 0.004f)), rot, new Vector3(0.17f, 0.135f, -BackThick + 0.004f - jOut), 0f, true);
            Decal(shop, "JunctionFace", Back(0f, 0.231f, jOut - 0.0008f), Vector3.right * 0.07f, BackUp * 0.045f, -BackFwd, PanelUv(JunctionFace));
            // 背の半ばでケーブルを束ねる留め具
            Slab(shop, "CableClip", Shop.Frame, Back(0f, 0.452f, -0.114f), rot, new Vector3(0.13f, 0.024f, 0.036f), 0f, true);
        }

        /// <summary>ケーブル。太い物は断面を四角、細い物は三角にする（粗い画面では丸く見える）</summary>
        static void Cable(Shop shop, string name, List<Vector3> path, int steps, float radius, Swatch colour)
        {
            Tube(shop, name, Shop.Panel, Spline(path, steps), radius, radius >= 0.008f ? 4 : 3, false, SwatchUv(colour), 65f);
        }

        /// <summary>
        /// 後から這わせたケーブル。裏の箱の端子三本は背の半ばの留め具を通って腰の分岐の箱へ、耳の筐体の二本は背もたれの縁を回って裏の箱の脇へ。
        /// 分岐の箱から太い一本が座の下の機構へ、もう一本が右の肘掛けの金属の腕へ（腕に沿って差込口へ通る）。
        /// 左右の肘掛けの操作盤からも一本ずつ、当て物の下と金属の腕に沿って座の下へ
        /// </summary>
        static void Wiring(Shop shop)
        {
            const float tOut = CrownOut;
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
                    Back(p.x, 0.715f + CrownDs, tOut - 0.038f),
                    Back(p.x * 1.1f, 0.70f + CrownDs, tOut - 0.056f),
                    Back(p.x * 1.4f + sway * 0.3f, 0.62f + CrownDs, -0.19f),
                    Back(p.x * 1.2f + sway, 0.53f, -0.155f),
                    Back(p.x * 0.7f, 0.452f, -0.123f),
                    Back(p.x * 0.7f, 0.39f, -0.128f),
                    Back(p.x * 0.8f, 0.325f, -0.142f),
                    Back(p.x * 0.8f, 0.285f, -0.130f),
                };
                Cable(shop, "Cable" + i, path, 9, p.r, p.c);
            }
            // 耳の筐体から、背もたれの肩の縁の外を回って裏の箱の脇へ
            var earX = EarX;
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    Back(sx * (earX + 0.008f), EarS - 0.025f, -0.04f),
                    Back(sx * (earX + 0.004f), EarS - 0.04f, -0.085f),
                    Back(sx * 0.29f, EarS - 0.055f, -0.115f),
                    Back(sx * 0.20f, EarS - 0.07f, -0.128f),
                    Back(sx * 0.125f, EarS - 0.085f, -0.13f),
                };
                Cable(shop, "EarCable" + (sx > 0f ? "R" : "L"), path, 6, 0.0055f, sx > 0f ? Swatch.Grey : Swatch.Teal);
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
            Cable(shop, "TrunkCable", down, 8, 0.011f, Swatch.Rubber);
            // 分岐の箱から右の肘掛けの金属の腕へ（腕に沿って差込口まで通る）
            var arm = new List<Vector3>
            {
                Back(0.08f, 0.215f, -0.13f),
                Back(0.15f, 0.20f, -0.145f),
                new Vector3(0.27f, 0.56f, -0.30f),
                new Vector3(0.33f, 0.50f, -0.17f),
                new Vector3(0.318f, 0.475f, -0.085f),
            };
            Cable(shop, "ArmCable", arm, 7, 0.0065f, Swatch.Violet);
            // 肘掛けの操作盤から、当て物の下と金属の腕に沿って座の下へ
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.40f, 0.625f, PodBack + 0.002f),
                    new Vector3(sx * 0.395f, 0.598f, 0.16f),
                    new Vector3(sx * 0.34f, 0.574f, 0.06f),
                    new Vector3(sx * 0.325f, 0.548f, -0.02f),
                    new Vector3(sx * 0.318f, 0.49f, -0.08f),
                    new Vector3(sx * 0.285f, 0.44f, -0.12f),
                    new Vector3(sx * 0.22f, 0.42f, -0.13f),
                };
                Cable(shop, "PodCable" + (sx > 0f ? "R" : "L"), path, 7, 0.005f, sx > 0f ? Swatch.Violet : Swatch.Grey);
            }
        }

        // ---- 道具 ------------------------------------------------------------------

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

        /// <summary>角を面取りした箱（bevel が 0 なら面取り無しの 12 の三角の箱）。uv はどの面も swatch（色の升）か 0</summary>
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
            if (b <= 0f)
            {
                shop.End(Orient.Each);
                return;
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
        /// 面の組（張り地・漆・金具・磨いた金属・操作盤の絵）に分けて溜め、焼く時に一つの組（マテリアル一つ）にまとめる
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

            /// <summary>部品を閉じた後に、頂点の法線を決めた向きに置き換える</summary>
            public void SetNormal(int i, Vector3 n) { norms[i] = n.normalized; }

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

            /// <summary>
            /// 一つの mesh に焼く。面の組は一つにまとめ（マテリアル一つ・描く回数一つ）、uv を持たない漆・金具・磨いた金属の面は、
            /// 絵の色の升（<see cref="Swatch.Lacquer"/>・<see cref="Swatch.Steel"/>・<see cref="Swatch.Chrome"/>）を引く。法線の絵のために接線も焼く
            /// </summary>
            public Mesh Bake(string meshName)
            {
                var fallback = new Dictionary<int, Vector2>
                {
                    { Shell, SwatchUv(Swatch.Lacquer) },
                    { Frame, SwatchUv(Swatch.Steel) },
                    { Chrome, SwatchUv(Swatch.Chrome) },
                };
                var all = new List<int>();
                for (var i = 0; i < Count; i++)
                {
                    Vector2 swatch;
                    var has = fallback.TryGetValue(i, out swatch);
                    foreach (var v in tris[i])
                    {
                        if (has && uvs[v] == Vector2.zero) uvs[v] = swatch;
                        all.Add(v);
                    }
                }
                var mesh = new Mesh { name = meshName };
                if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 1;
                mesh.SetTriangles(all, 0, false);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
