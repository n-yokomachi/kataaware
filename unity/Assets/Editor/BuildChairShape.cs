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
        // 座った体の腰・背・脚（SeatedPose。PlaceProtagonist.RoomSit が座面・肘掛け・床から解いた形）を組み直さずに済むよう、
        // 体が触れる所（座面の高さの線・肘掛けの上面の線・ジャックの置き場）は前の椅子と同じ所に置く。
        // 肘掛けを外殻にして外と前へ広げたので、手を置く所と差込口は動いた（腕の形は PlaceProtagonist.Reseat で解き直す）

        /// <summary>座面の高さの線。前の椅子の座面の上面（背の側 0.549、前 0.50）と同じ傾き（前へ 6 度下がる）</summary>
        static float SeatLevel(float z) { return 0.527f - 0.105f * z; }

        /// <summary>
        /// 座面のクッションの半分の幅。外殻の内の面（<see cref="ArmIn"/>）の 1 mm 内まで張り、外殻の内側にぴったり収める（すき間も段も作らない）。
        /// 座った腰と腿の外（|x| 0.17〜0.18）から肘掛けの内の面まで 10 cm ある（前の椅子は当て物の内の縁まで 6 cm）
        /// </summary>
        const float SeatHalf = 0.274f;
        /// <summary>座面の後ろの端と前の端（前の椅子より 2 cm 深い。前の端は肘掛けの前の端と揃える）</summary>
        const float SeatBack = -0.215f;
        const float SeatFront = 0.31f;
        /// <summary>クッションの下の面（座の皿の上）</summary>
        const float SeatFloor = 0.435f;
        /// <summary>
        /// 座面の前の真ん中の、ふくらはぎの逃げ。膝の裏から下がるふくらはぎ（床から 0.42〜0.47 m で前の端から 4 cm 内まで来る）に
        /// 始めの椅子は 3 cm 食い込んでいた。前の真ん中だけクッションの下を上げて、ふくらはぎの上に浮かせる
        /// </summary>
        const float CalfRelief = 0.042f;
        /// <summary>縁の丸み（横から見た上の角と、上から見た前の角）。厚く柔らかい革のクッション</summary>
        const float SeatEdge = 0.03f;
        const float SeatCorner = 0.04f;

        /// <summary>背もたれの倒れ（度）</summary>
        const float Recline = 20f;
        /// <summary>背もたれの前の基準の面の下の端（真ん中）。座面の後ろに沈めて載せる</summary>
        static readonly Vector3 BackBase = new Vector3(0f, 0.535f, -0.145f);
        /// <summary>
        /// 背もたれの長さ（倒れた向きに沿って）と、前の基準の面から裏の面までの厚み（裏の端末の箱・分岐の箱・留め具はこの面に付く）。
        /// 厚い塊にするため、前の面は座った体の背に合わせたまま、裏を 8 cm 後ろへ出した（<see cref="RearShift"/>）
        /// </summary>
        const float BackLength = 0.95f;
        const float RearShift = 0.08f;
        const float BackThick = 0.095f + RearShift;
        /// <summary>
        /// 背もたれの半分の幅。下は外殻の外幅（<see cref="ArmOut"/>）と同じで、外殻の上の面の高さを抜ける所（倒れた向きに沿って 0.22）の少し上まで保ち、
        /// 肘掛けが背もたれの下の左右から延びて見えるようにする。そこから肩（BackShoulder）へ BackHalfW まで細くし、上の角と下の角は丸める
        /// </summary>
        const float BackHalfW = 0.30f;
        const float BackBaseW = ArmOut;
        const float BackJoin = 0.24f;
        const float BackShoulder = 0.55f;
        const float BackTopRound = 0.10f;
        const float BackBottomRound = 0.03f;
        /// <summary>
        /// 厚み。背もたれは前も裏も革で包んだ一つの厚い塊（腰で 22 cm、ボタン留めの膨らみを入れて 26 cm）。前と裏が合う真ん中の面（前の基準の面から前へ）、
        /// そこから前の面・裏の面までの厚み、縁の丸みの半径。座った体の背は前の面より前にある（腰で 6 cm、背の半ばで 15 cm 以上）
        /// </summary>
        const float BackMid = -0.025f;
        const float BackFrontH = 0.065f;
        const float BackRearH = 0.07f + RearShift;
        const float BackRound = 0.09f;
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

        /// <summary>
        /// 肘掛けの上面の線（上の面の膨らみのいちばん高い所）。前の椅子と同じ高さと傾き（前へ 4 度下がる）。ジャックの置き場（z −0.07）で 0.6758。
        /// 座った体の肩（1.074）から上腕（0.25 m）を下ろしても肘はこの面に届かないので、前腕は斜めに下ろし、手首から先をこの面に預ける（PlaceProtagonist.RoomSit）。
        /// 置き場より前は傾きを半分ほどへ緩める（前の端で 1.4 cm 高い）。右手を前の端まで出すと腕が伸びきり、前へ 4 度下がる面のままでは手の甲が 5〜10 mm 浮いた
        /// </summary>
        static float PadTop(float z) { return 0.6758f - 0.0699f * (z + 0.07f) + 0.0349f * Mathf.Max(0f, z + 0.07f) * Smooth(-0.07f, 0.33f, z); }
        /// <summary>
        /// 外殻（肘掛けと背の部分）。厚い背もたれの下の左右から、同じ断面のまま前へ延びる肘掛け（<see cref="Arm"/>）。内の面・外の面・下の面・前の端と、背の部分の内の面。
        /// 内の面は座面のクッションの脇にぴったり沿って立ち、外の面は床から 0.10 m までまっすぐ立つ（下へ細くしない）。前の端は座面の前の端と揃える。
        /// 背の部分の外の面は、厚い背もたれの裏が外殻の上の面の高さを抜ける所（z −0.385）と揃え、内の面はその 17.5 cm 前（<see cref="TubIn"/>）
        /// </summary>
        const float ArmIn = 0.275f;
        const float ArmOut = 0.45f;
        const float ArmFloor = 0.10f;
        const float TubIn = -0.21f;
        const float ArmFront = 0.31f;
        /// <summary>上の縁の巻きの半径（内と外）、前の端の丸み、上の面の膨らみ、外の面の膨らみ</summary>
        const float ArmRollIn = 0.032f;
        const float ArmRollOut = 0.048f;
        const float ArmEnd = 0.035f;
        const float ArmCrown = 0.006f;
        const float ArmBulge = 0.006f;
        /// <summary>
        /// 肘掛けの断面を回る割合の区切り。下の真ん中（0）から内の下の角（V1）、内の面を上って縫い目（V2）、内の巻き、上の面（V3〜V4）、
        /// 外の巻き、外の縫い目（V5）、外の面を下りて外の下の角（V6）、下の真ん中（1）へ戻る。絵もこの割合で写す（下の面は狭く）
        /// </summary>
        const float ArmV1 = 0.03f, ArmV2 = 0.36f, ArmV3 = 0.42f, ArmV4 = 0.57f, ArmV5 = 0.635f, ArmV6 = 0.97f;
        /// <summary>絵の上で数える断面の一周の長さ（m）。法線の絵の傾きを測る長さ</summary>
        const float ArmPerim = 0.90f;
        /// <summary>
        /// 肘掛けの前の面の外寄りに後から金具で留めた操作盤（前へ高くなる斜めの面を、座った目の方へ向ける）。肘掛けの前の面の板と、操作盤の下の棚の二つの金具で抱え、
        /// 外は肘掛けの外の面から 2 cm 張り出す。内寄りは、肘掛けの前の端に手の甲を預けた右手の指先と煙草が座った目から見えるように空ける
        /// （内寄りに置いていた時は、手が操作盤の手前で画面の外に出て、煙が操作盤の後ろから立って機械が煙を吐くように見えた）。
        /// 座った目から下を向ける限り（40 度）で、正面を向いて画面の下の隅に入る
        /// </summary>
        const float PodInner = 0.37f;
        const float PodOuter = 0.47f;
        const float PodBack = ArmFront + 0.003f;
        const float PodFront = ArmFront + 0.10f;
        const float PodBottom = 0.60f;
        /// <summary>
        /// 差込口（Room/Chair/PortHole。ケーブルの始まり）。右の肘掛けの上の面の外寄り。手首から先を預けた右手（内寄り）の後ろの外で、前腕で塞がない。
        /// 前の椅子は (0.286, 0.670, 0.150)（細い当て物の真ん中）
        /// </summary>
        public static readonly Vector3 PortAt = new Vector3(0.38f, PadTop(0.150f) + 0.0096f, 0.150f);

        /// <summary>台座（低く幅の広い円盤）の半径。外殻の下の縁（幅 0.90 m）の内に収まり、斜めや横からはほとんど隠れる</summary>
        const float PedestalReach = 0.31f;

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
        static readonly RectInt PodSide = new RectInt(44, 48, 12, 10);
        /// <summary>
        /// 革の面の絵の置き場。外殻は、横（u）が前の端から背の真ん中までの長さ（断面の高さごとに、その線の長さで割る）、
        /// 縦（v）が断面を回る割合（<see cref="ArmV1"/> ほか）で写し、左右で同じ絵を使う。肘掛けの前の面は、縁の内に玉縁を描いた板の絵。
        /// 背もたれの前と裏は、横を高さごとの幅で割って写す（下の幅が広い）。背もたれの裏は無地なので狭い置き場にする
        /// </summary>
        static readonly RectInt BackFrontArea = new RectInt(64, 64, 64, 64);
        static readonly RectInt BackRearArea = new RectInt(48, 64, 16, 64);
        static readonly RectInt ArmArea = new RectInt(0, 64, 48, 64);
        static readonly RectInt ArmCapArea = new RectInt(104, 40, 12, 24);
        static readonly RectInt SeatArea = new RectInt(64, 0, 64, 40);
        static readonly RectInt FootArea = new RectInt(64, 40, 40, 24);
        /// <summary>革の面の絵が覆う広さ（椅子の座標、m）。座面とフットレストは上から、背もたれは前の基準の面へ真っすぐに写す</summary>
        const float SeatAreaZ0 = -0.215f, SeatAreaZ1 = 0.31f;
        const float FootAreaZ0 = FootBack, FootAreaZ1 = FootFront;
        /// <summary>肘掛けの前の面の、絵に描く広さ（m。端の丸みの内の板）</summary>
        const float ArmCapW = ArmOut - ArmIn - 2f * ArmEnd;
        const float ArmCapH = 0.48f;

        /// <summary>
        /// 色の升（4×4 画素）。下の段に 16 個、左上の段（y 60）に金属と漆と革と黒い磨いた金属の 5 個。ケーブルや金具のような一色の物は升の真ん中を引く
        /// </summary>
        enum Swatch { Rubber, Plastic, Grey, Violet, Teal, Orange, Metal, Label, Cyan, VioletLit, Amber, Green, Brass, Glass, PlasticLight, Red, Chrome, Steel, Lacquer, Leather, BlackChrome }

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

        static Vector2 BackFrontUv(float x, float s) { return AreaUv(BackFrontArea, 0.5f * (x / BackW(s) + 1f), s / BackLength); }
        static Vector2 BackRearUv(float x, float s) { return AreaUv(BackRearArea, 0.5f * (x / BackW(s) + 1f), s / BackLength); }
        static Vector2 SeatUv(float x, float z) { return AreaUv(SeatArea, (x + SeatHalf) / (2f * SeatHalf), (z - SeatAreaZ0) / (SeatAreaZ1 - SeatAreaZ0)); }
        static Vector2 FootUv(float x, float z) { return AreaUv(FootArea, (x + FootHalf) / (2f * FootHalf), (z - FootAreaZ0) / (FootAreaZ1 - FootAreaZ0)); }

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
            SeatBase(shop);
            Backrest(shop);
            Arm(shop, 1f);
            Arm(shop, -1f);
            Port(shop);
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
        /// 縁の縫い目の線までの離れ（m）。上の平らな面と、脇と前の丸みの境。脇は |x| = weltX の線を前の縫い目まで、前は z = weltZ の線を脇の縫い目まで
        /// </summary>
        static float WeltDistance(float x, float z, float weltX, float weltZ)
        {
            var ax = Mathf.Abs(x);
            var corner = new Vector2(ax - weltX, z - weltZ).magnitude;
            var side = z <= weltZ ? Mathf.Abs(ax - weltX) : corner;
            var front = ax <= weltX ? Mathf.Abs(z - weltZ) : corner;
            return Mathf.Min(side, front);
        }

        /// <summary>玉縁（縫い目に沿った細い革の紐）の高さ（m、上へ正）。縫い目の線からの離れ d で、線の上で 3 mm 盛り上がり、両脇の縫い目で窪む</summary>
        static float Piping(float d)
        {
            return 0.003f * Mathf.Exp(-Mathf.Pow(d / 0.004f, 2f)) - 0.0025f * Mathf.Exp(-Mathf.Pow((Mathf.Abs(d) - 0.008f) / 0.003f, 2f));
        }

        /// <summary>
        /// クッションの上の面の細かい起伏（m、上へ正）。菱形のボタン留めの膨らみとボタンの窪み、縁の玉縁。mesh には持たせず、絵（陰と法線）に描く。
        /// 座面とフットレストは体の当たる面なので、上の面の高さ（mesh）は変えない
        /// </summary>
        static float CushionFine(Lattice l, float inside, float puff, float dip, float x, float z, float weltX, float weltZ)
        {
            var h = Piping(WeltDistance(x, z, weltX, weltZ));
            if (inside > 0f)
            {
                var near = Nearest(l, x, z);
                var hole = dip * Mathf.Exp(-Mathf.Pow((new Vector2(x, z) - near).magnitude / 0.012f, 2f));
                h += inside * (puff * Dome(l, x, z) - hole);
            }
            return h;
        }

        /// <summary>座面のボタン留めの面の内か（縁の縫い目の 1.5 cm 内から、背もたれの下に隠れる後ろの所まで）</summary>
        static float SeatTuftInside(float x, float z)
        {
            var ax = Mathf.Abs(x);
            return (1f - Smooth(SeatWeltX - 0.035f, SeatWeltX - 0.012f, ax)) * (1f - Smooth(SeatWeltZ - 0.035f, SeatWeltZ - 0.012f, z)) * Smooth(-0.16f, -0.11f, z);
        }

        /// <summary>座面の上の面の細かい起伏（菱形の膨らみ 2.2 cm、ボタンの窪み 1.4 cm、玉縁）</summary>
        static float SeatFine(float x, float z) { return CushionFine(SeatLattice, SeatTuftInside(x, z), 0.022f, 0.014f, x, z, SeatWeltX, SeatWeltZ); }

        /// <summary>座面の縫い目の所（脇の丸みの始まりのすぐ内と、前の丸みの始まりのすぐ後ろ）</summary>
        const float SeatWeltX = 0.207f;
        const float SeatWeltZ = 0.252f;

        /// <summary>座面の上の面の高さ（mesh）。尻の沈む所をわずかに窪ませる。ボタン留めと玉縁は絵に描く（<see cref="SeatFine"/>）</summary>
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
            CushionGrid(shop, SeatShape, new List<float> { 0f, 0.09f, 0.17f, SeatHalf - SeatEdge, 0.242f, 0.26f, 0.269f, SeatHalf });
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

        /// <summary>背もたれの、高さ s での半分の幅。下は外殻の外幅、肩から上は細い。上の角と下の角を丸める</summary>
        static float BackW(float s)
        {
            var w = Mathf.Lerp(BackBaseW, BackHalfW, Smooth(BackJoin, BackShoulder, s));
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
        /// <summary>
        /// ボタン留めの膨らみ（0〜1）。菱形ごとの丸い枕の形: 真ん中が高く、四つの辺（折り目）で 0、四つの角（ボタン）で 0。
        /// 菱形の中の位置 (fm, fn) の放物線の積を 0.7 乗して、頂をなだらかに、折り目の際を少し急にする
        /// </summary>
        static float TuftDome(float x, float s) { return Dome(BackLattice, x, s); }

        /// <summary>いちばん近いボタンの位置 (x, s)</summary>
        static Vector2 TuftNearest(float x, float s) { return Nearest(BackLattice, x, s); }

        /// <summary>ボタン留めの菱形の格子。同じ段のボタンの間の半分 Dx、段の間 Dy、ボタンの段の一つの位置 Y0。ボタンは (i·Dx, Y0 + j·Dy)、i + j が偶数の所</summary>
        struct Lattice
        {
            public float Dx, Dy, Y0;
            public Lattice(float dx, float dy, float y0) { Dx = dx; Dy = dy; Y0 = y0; }
        }

        static readonly Lattice BackLattice = new Lattice(TuftDx, TuftDs, TuftS0);
        /// <summary>座面の菱形（幅 14 cm・奥行き 20 cm）。前の縫い目の 3 cm 後ろにボタンの段。背もたれの菱形と同じ形の小さめ</summary>
        static readonly Lattice SeatLattice = new Lattice(0.07f, 0.10f, 0.22f);
        /// <summary>
        /// 外殻の内の面の菱形（横 12 cm・高さ 15 cm）。座面と同じ形の小さめ。横は背の真ん中から面に沿って、高さは床から数える
        /// </summary>
        static readonly Lattice ArmLattice = new Lattice(0.06f, 0.075f, 0.525f);
        /// <summary>フットレストの菱形（幅 12 cm・奥行き 18 cm）</summary>
        static readonly Lattice FootLattice = new Lattice(0.06f, 0.09f, 0.37f);

        /// <summary>
        /// ボタン留めの膨らみ（0〜1）。菱形ごとの丸い枕の形: 真ん中が高く、四つの辺（折り目）と四つの角（ボタン）で 0。
        /// 菱形の中の位置 (fm, fn) の放物線の積を 0.7 乗して、頂をなだらかに、折り目の際を少し急にする
        /// </summary>
        static float Dome(Lattice l, float x, float y)
        {
            var a = x / l.Dx;
            var b = (y - l.Y0) / l.Dy;
            var m = 0.5f * (a + b);
            var n = 0.5f * (b - a);
            var fm = m - Mathf.Floor(m);
            var fn = n - Mathf.Floor(n);
            return Mathf.Pow(Mathf.Max(0f, 16f * fm * (1f - fm) * fn * (1f - fn)), 0.7f);
        }

        /// <summary>いちばん近いボタンの位置</summary>
        static Vector2 Nearest(Lattice l, float x, float y)
        {
            var a = x / l.Dx;
            var b = (y - l.Y0) / l.Dy;
            var m = Mathf.Round(0.5f * (a + b));
            var n = Mathf.Round(0.5f * (b - a));
            return new Vector2((m - n) * l.Dx, l.Y0 + (m + n) * l.Dy);
        }

        /// <summary>菱形の折り目（ボタンを結ぶ斜めの線）への近さ（0〜1。線の上で 1、1.2 cm ほどで消える）。地の色の陰と艶に使う</summary>
        static float Crease(Lattice l, float x, float y)
        {
            var a = x / l.Dx;
            var b = (y - l.Y0) / l.Dy;
            var m = 0.5f * (a + b);
            var n = 0.5f * (b - a);
            // m・n の 1 あたりの長さ（m）
            var unit = 1f / (0.5f * Mathf.Sqrt(1f / (l.Dx * l.Dx) + 1f / (l.Dy * l.Dy)));
            var dm = Mathf.Abs(m - Mathf.Round(m)) * unit;
            var dn = Mathf.Abs(n - Mathf.Round(n)) * unit;
            return Mathf.Exp(-Mathf.Pow(Mathf.Min(dm, dn) / 0.012f, 2f));
        }

        /// <summary>ボタンの周り（0〜1。ボタンの上で 1、同じ段のボタンの間の 3 割ほどで消える）。地の色の陰と艶に使う</summary>
        static float ButtonRing(Lattice l, float x, float y)
        {
            var near = Nearest(l, x, y);
            return Mathf.Exp(-Mathf.Pow((new Vector2(x, y) - near).magnitude / (0.3f * l.Dx), 2f));
        }

        /// <summary>
        /// 背もたれの前のボタン留めの面の、高さ s での半分の幅。腰 0.262 から肩 0.30 へ（背もたれの下の、外殻の外幅まで広げた所は、ボタン留めの外の無地の厚い縁）。
        /// 腰の所まで菱形を広げると、ジャックを置いて指を抜く左手が、背もたれの下の菱形の膨らみに 1.3 cm 入った
        /// </summary>
        static float PanelW(float s)
        {
            return Mathf.Min(BackW(s), Mathf.Lerp(0.262f, BackHalfW, Smooth(0.05f, 0.32f, s)));
        }

        /// <summary>ボタン留めの面の内か（1 で内、縫い目と縁の巻きと腰の下の所で 0 へ）</summary>
        static float TuftInside(float x, float s)
        {
            var side = PanelW(s) - Mathf.Abs(x);
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
            // mesh の膨らみは本当の高さの 9 割（輪郭と、斜めから見た時の起伏）。膨らみの丸い陰影は法線の絵が持ち、絵の傾きは 8 割の高さで作る
            // （本当の高さのままだと斜面の大半が急な一つの向きになって、菱形が明るい半分と暗い半分に割れて見えた）
            var puff = detail == BackDetail.Full ? TuftPuff * 0.8f : TuftPuff * 0.9f;
            var tuft = inside > 0f ? inside * puff * TuftDome(x, s) : 0f;
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
            // 傾きは 60 度までに抑える。部屋の明かりでは椅子の明るさの多くが照り返し（斜めから見るほど強い）なので、
            // 折り目の際の急な画素が一画素の明るい筋になる（40 度まで抑えると、膨らみの斜面が平らな面になって、角錐を並べた筋に見えた）
            var tilt = new Vector2(ts.x, ts.y);
            var limit = Mathf.Tan(60f * Mathf.Deg2Rad) * ts.z;
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

        /// <summary>背もたれの菱形の折り目への近さ</summary>
        static float TuftCrease(float x, float s) { return Crease(BackLattice, x, s); }

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
            // 格子の列は真ん中から左右に 5 本ずつ（どの段も同じ数。肩の幅 0.30 m で菱形の格子にちょうど乗る）。下の広い所の外寄りは粗い列のまま、膨らみは絵で出す
            const int count = 5;
            var lat = Mathf.Min(1f, (w - 0.09f) / (count * TuftDx * 0.5f));
            var edge = new[] { 0f, 0.03f, 0.06f };
            var xs = new List<float>();
            foreach (var e in edge) xs.Add(-w + e);
            var last = count * TuftDx * 0.5f * lat;
            xs.Add(-0.5f * ((w - 0.06f) + last));
            for (var k = -count; k <= count; k++) xs.Add(k * TuftDx * 0.5f * lat);
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

        // ---- 外殻（肘掛けと背の部分） ------------------------------------------------------
        //
        // 肘掛けの断面（内の面・上の縁の巻き・上の面・外の面）を、右の肘掛けの前の端から後ろへ、後ろの角を回って背もたれの下を横切り、
        // 左の肘掛けの前の端まで、U の字に掃く。断面は床から 0.10 m まで下り、外の面はまっすぐ立つ。真上から見ると、肘掛けと背の部分が座面を囲む
        // 一つの外殻で、厚い背もたれは背の部分の上から立ち上がる（背もたれの下の幅は外殻の外幅と同じ、裏は背の部分の外の面と揃う。<see cref="BackW"/>）。
        // 道筋は断面の真ん中の線で持つ（σ は右の前の端からの長さ）。右と左は真ん中（背の部分の真ん中 x 0）で分けて、二つの部品にする（手を置く上の面を左右で測るため）

        /// <summary>断面の真ん中の線の、肘掛けの所の左右の位置</summary>
        const float TubMidX = 0.5f * (ArmIn + ArmOut);
        /// <summary>断面の半分の幅</summary>
        const float TubHalfW = 0.5f * (ArmOut - ArmIn);
        /// <summary>背の部分の真ん中の線の前後の位置（内の面は <see cref="TubIn"/>）</summary>
        const float TubMidZ = TubIn - TubHalfW;
        /// <summary>後ろの角の、真ん中の線の半径（内の面は 3.25 cm、外の面は 20.75 cm の半径で回る）</summary>
        const float TubCorner = 0.12f;
        /// <summary>道筋の区切り。肘掛けのまっすぐな所の長さ、角の長さ（真ん中の線で）、背の部分の半分の長さ、半分の全長</summary>
        const float TubStraight = ArmFront - (TubMidZ + TubCorner);
        const float TubCornerLen = 0.5f * Mathf.PI * TubCorner;
        const float TubBackHalf = TubMidX - TubCorner;
        const float TubHalfLen = TubStraight + TubCornerLen + TubBackHalf;

        /// <summary>道筋の σ での上の面の線。肘掛けのまっすぐな所は肘掛けの上面の線、角と背の部分は角の始まりの高さのまま</summary>
        static float TubTop(float sigma)
        {
            return PadTop(ArmFront - Mathf.Min(sigma, TubStraight));
        }

        /// <summary>道筋の σ での、真ん中の線の点（x, z）と、外への向き（真上から見て）</summary>
        static void TubFrame(float sigma, out Vector2 at, out Vector2 outward)
        {
            if (sigma <= TubStraight)
            {
                at = new Vector2(TubMidX, ArmFront - sigma);
                outward = new Vector2(1f, 0f);
                return;
            }
            if (sigma <= TubStraight + TubCornerLen)
            {
                var a = (sigma - TubStraight) / TubCorner;
                outward = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                at = new Vector2(TubMidX - TubCorner, TubMidZ + TubCorner) + TubCorner * outward;
                return;
            }
            at = new Vector2(TubMidX - TubCorner - (sigma - TubStraight - TubCornerLen), TubMidZ);
            outward = new Vector2(0f, -1f);
        }

        /// <summary>真ん中の線から外へ w 離れた線の、前の端から背の真ん中までの長さ（角の所だけ半径が変わる）</summary>
        static float TubLength(float w)
        {
            return TubStraight + 0.5f * Mathf.PI * (TubCorner + w) + TubBackHalf;
        }

        /// <summary>真ん中の線の σ に当たる、外へ w 離れた線の上の長さ</summary>
        static float TubArc(float sigma, float w)
        {
            if (sigma <= TubStraight) return sigma;
            if (sigma <= TubStraight + TubCornerLen) return TubStraight + (sigma - TubStraight) / TubCorner * (TubCorner + w);
            return TubStraight + 0.5f * Mathf.PI * (TubCorner + w) + (sigma - TubStraight - TubCornerLen);
        }

        /// <summary><see cref="TubArc"/> の逆。外へ w 離れた線の上の長さ s から、真ん中の線の σ</summary>
        static float TubSigma(float s, float w)
        {
            if (s <= TubStraight) return s;
            var corner = 0.5f * Mathf.PI * (TubCorner + w);
            if (s <= TubStraight + corner) return TubStraight + (s - TubStraight) / (TubCorner + w) * TubCorner;
            return s - corner + TubCornerLen;
        }

        /// <summary>前の端の丸みで、断面を内へ寄せる量（前の端から <see cref="ArmEnd"/> の内で、四分の一の円で寄る）</summary>
        static float ArmInset(float sigma)
        {
            if (sigma >= ArmEnd) return 0f;
            var k = ArmEnd - Mathf.Max(0f, sigma);
            return ArmEnd - Mathf.Sqrt(Mathf.Max(0f, ArmEnd * ArmEnd - k * k));
        }

        /// <summary>
        /// 断面の点（x は肘掛けの所の左右の位置で、内の面 <see cref="ArmIn"/>〜外の面 <see cref="ArmOut"/>、y は上）。上の面の線 top、端の丸みで内へ寄せる量 d、
        /// 断面を回る割合 v（<see cref="ArmV1"/> ほか）で。上の面は top から膨らみの分だけ下げた所から、真ん中で top まで膨らむ。
        /// 外の面はわずかに外へ膨らむ。内の面は座面のクッションの脇に沿うので膨らませない
        /// </summary>
        static Vector2 ArmSection(float top0, float v, float d)
        {
            var k = 1f - 0.8f * Mathf.Clamp01(d / ArmEnd);
            var top = top0 - ArmCrown - 0.9f * d;
            var bottom = ArmFloor + d;
            var xi = ArmIn + d;
            var xo = ArmOut - d;
            var ri = ArmRollIn * k;
            var ro = ArmRollOut * k;
            var xm = 0.5f * (xi + xo);
            if (v <= ArmV1) return Vector2.Lerp(new Vector2(xm, bottom), new Vector2(xi, bottom), v / ArmV1);
            if (v <= ArmV2) return Vector2.Lerp(new Vector2(xi, bottom), new Vector2(xi, top - ri), (v - ArmV1) / (ArmV2 - ArmV1));
            if (v <= ArmV3)
            {
                var a = Mathf.Lerp(180f, 90f, (v - ArmV2) / (ArmV3 - ArmV2)) * Mathf.Deg2Rad;
                return new Vector2(xi + ri + ri * Mathf.Cos(a), top - ri + ri * Mathf.Sin(a));
            }
            if (v <= ArmV4)
            {
                var t = (v - ArmV3) / (ArmV4 - ArmV3);
                return new Vector2(Mathf.Lerp(xi + ri, xo - ro, t), top + ArmCrown * k * Mathf.Sin(Mathf.PI * t));
            }
            if (v <= ArmV5)
            {
                var a = Mathf.Lerp(90f, 0f, (v - ArmV4) / (ArmV5 - ArmV4)) * Mathf.Deg2Rad;
                return new Vector2(xo - ro + ro * Mathf.Cos(a), top - ro + ro * Mathf.Sin(a));
            }
            if (v <= ArmV6)
            {
                var t = (v - ArmV5) / (ArmV6 - ArmV5);
                return new Vector2(xo + ArmBulge * k * Mathf.Sin(Mathf.PI * t), Mathf.Lerp(top - ro, bottom, t));
            }
            return Vector2.Lerp(new Vector2(xo, bottom), new Vector2(xm, bottom), (v - ArmV6) / (1f - ArmV6));
        }

        /// <summary>
        /// 断面の輪の、回る割合（14 点。下の真ん中で閉じる）。内の面は平らなので上と下の縁だけ、巻きは 30 度ごと、
        /// 上の面は真ん中の膨らみ、外の面は真ん中の膨らみに点を置く
        /// </summary>
        static readonly float[] ArmRingV =
        {
            0f, ArmV1, ArmV2, ArmV2 + (ArmV3 - ArmV2) / 3f, ArmV2 + 2f * (ArmV3 - ArmV2) / 3f, ArmV3, 0.5f * (ArmV3 + ArmV4), ArmV4,
            ArmV4 + (ArmV5 - ArmV4) / 3f, ArmV4 + 2f * (ArmV5 - ArmV4) / 3f, ArmV5, 0.5f * (ArmV5 + ArmV6), ArmV6, 1f,
        };

        /// <summary>
        /// 輪を置く σ（右の前の端から背の真ん中まで）。前の端は丸みに三つ、肘掛けのまっすぐな所は上面の線の傾きと絵の写りに足りる 9〜10 cm おき、
        /// 角は 22.5 度ごと、背の部分は真ん中までに二つ
        /// </summary>
        static readonly float[] TubRingSigma =
        {
            0f, 0.0105f, ArmEnd, 0.10f, 0.19f, 0.29f, 0.39f, 0.49f, TubStraight,
            TubStraight + 0.25f * TubCornerLen, TubStraight + 0.5f * TubCornerLen, TubStraight + 0.75f * TubCornerLen, TubStraight + TubCornerLen,
            TubStraight + TubCornerLen + 0.5f * TubBackHalf, TubHalfLen,
        };

        /// <summary>張りぐるみの輪の点（道筋の σ、断面の割合 v）を椅子の座標へ。sx は右（1）か左（−1）</summary>
        static Vector3 TubPoint(float sx, float sigma, float v, out float w)
        {
            Vector2 at, outward;
            TubFrame(sigma, out at, out outward);
            var p = ArmSection(TubTop(sigma), v, ArmInset(sigma));
            w = p.x - TubMidX;
            var plan = at + outward * w;
            return new Vector3(sx * plan.x, p.y, plan.y);
        }

        /// <summary>
        /// 外殻の片側（右か左）と、その肘掛けの前の面の操作盤。座面の脇から立ち上がる厚い革の塊（幅 17.5 cm、床から 0.10〜0.68 m）が、
        /// 前の端から後ろへ伸び、後ろの角を回って背もたれの下を横切り、背の真ん中で反対の側とつながる。
        /// 上の縁は内と外で太く巻いて縫い目に玉縁、内の面は座面と揃う菱形のボタン留め（絵。角と背の部分でも途切れない）、外の面は無地、前の端は丸めて、
        /// 縁の内に玉縁を回した板で閉じる。前の面の外寄りに、後から金具で留めた操作盤（黒い金属の箱、前の面に螺子で留めた板と、箱の下の棚）。
        /// 右は内の面の後ろ寄りに、抜いたジャックを置く金具の受け台（<see cref="BuildProps.JackRestAt"/> の下）
        /// </summary>
        static void Arm(Shop shop, float sx)
        {
            var name = sx > 0f ? "R" : "L";
            shop.Begin("ArmPad" + name, 50f, true);
            var nz = TubRingSigma.Length;
            var nv = ArmRingV.Length;
            var idx = new int[nz, nv];
            for (var i = 0; i < nz; i++)
            {
                var sigma = TubRingSigma[i];
                for (var j = 0; j < nv; j++)
                {
                    float w;
                    var p = TubPoint(sx, sigma, ArmRingV[j], out w);
                    idx[i, j] = shop.V(p, AreaUv(ArmArea, TubArc(sigma, w) / TubLength(w), ArmRingV[j]));
                }
            }
            for (var i = 0; i < nz - 1; i++)
            {
                // 外へ向ける芯。断面の真ん中（前の端の丸みの面も前へ向くよう、芯は前の端から 10 cm 内に置く）
                var sigma = Mathf.Max(0.1f, 0.5f * (TubRingSigma[i] + TubRingSigma[i + 1]));
                Vector2 at, outward;
                TubFrame(sigma, out at, out outward);
                var c = new Vector3(sx * at.x, 0.5f * (ArmFloor + TubTop(sigma)), at.y);
                for (var j = 0; j < nv - 1; j++)
                    shop.QOut(Shop.Upholstery, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], c);
            }
            ArmCap(shop, sx);
            shop.End(Orient.None);

            // 前の面の外寄りの操作盤。後ろの低い縁から前の高い縁へ上がる斜めの面を、座った目の方（後ろ・上・内）へ向ける。外の面は螺子二つの絵。
            // 内寄りは、肘掛けの前の端に手の甲を預けた右手の指先と煙草が、座った目から見えるように空ける
            var pod = new Vector3[8];
            var pi = sx * PodInner;
            var po = sx * PodOuter;
            var low = PadTop(ArmFront);
            pod[0] = new Vector3(pi, PodBottom, PodBack); pod[1] = new Vector3(po, PodBottom, PodBack);
            pod[2] = new Vector3(po, PodBottom, PodFront); pod[3] = new Vector3(pi, PodBottom, PodFront);
            pod[4] = new Vector3(pi, low + 0.004f, PodBack); pod[5] = new Vector3(po, low + 0.016f, PodBack);
            pod[6] = new Vector3(po, low + 0.064f, PodFront); pod[7] = new Vector3(pi, low + 0.05f, PodFront);
            var faces = new int[] { Shop.Frame, Shop.Panel, Shop.Frame, Shop.Frame, Shop.Frame, Shop.Panel };
            var podUvs = new Rect?[] { null, PanelUv(sx > 0f ? RightPod : LeftPod), null, null, null, PanelUv(PodSide) };
            Hexa(shop, "ArmPod" + name, pod, faces, podUvs, true, sx < 0f);
            // 留め具。肘掛けの前の面に螺子で留めた板と、操作盤の下の棚（L の形）で抱える
            var mid = sx * 0.5f * (PodInner + PodOuter);
            Slab(shop, "PodPlate" + name, Shop.Chrome, new Vector3(sx * (PodInner + 0.035f), PodBottom - 0.035f, ArmFront + 0.0015f), Quaternion.identity, new Vector3(0.07f, 0.07f, 0.004f), 0f, true);
            Slab(shop, "PodShelf" + name, Shop.Chrome, new Vector3(mid, PodBottom - 0.003f, 0.5f * (ArmFront + PodFront)), Quaternion.identity, new Vector3(PodOuter - PodInner + 0.01f, 0.006f, PodFront - ArmFront), 0f, true);

            if (sx < 0f) return;
            // 抜いたジャックを置く受け台。肘掛けの内の面に螺子で留めた板から、座面の上へ張り出した棚。棚の上の面に寝かせたジャックの座金が載る
            var rest = BuildProps.JackRestAt;
            var shelf = rest.y - BuildProps.JackWasherRadius;
            Slab(shop, "RestCradle", Shop.Chrome, new Vector3(rest.x + 0.003f, shelf - 0.003f, rest.z), Quaternion.identity, new Vector3(0.052f, 0.006f, 0.06f), 0f, true);
            Slab(shop, "RestBracket", Shop.Chrome, new Vector3(ArmIn + 0.0015f, shelf - 0.04f, rest.z), Quaternion.identity, new Vector3(0.004f, 0.08f, 0.05f), 0f, true);
        }

        /// <summary>肘掛けの前の端を閉じる板（端の丸みで寄せた断面の、真ん中からの扇）。前（+z）へ向ける</summary>
        static void ArmCap(Shop shop, float sx)
        {
            var pts = new List<Vector2>();
            for (var j = 0; j < ArmRingV.Length - 1; j++) pts.Add(ArmSection(TubTop(0f), ArmRingV[j], ArmEnd));
            var min = pts[0];
            var max = pts[0];
            foreach (var p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Func<Vector2, Vector2> uv = p => AreaUv(ArmCapArea, (p.x - min.x) / (max.x - min.x), (p.y - min.y) / (max.y - min.y));
            var centre = 0.5f * (min + max);
            var c = shop.V(new Vector3(sx * centre.x, centre.y, ArmFront), uv(centre));
            var ids = new int[pts.Count];
            for (var j = 0; j < pts.Count; j++) ids[j] = shop.V(new Vector3(sx * pts[j].x, pts[j].y, ArmFront), uv(pts[j]));
            for (var j = 0; j < pts.Count; j++) shop.TFacing(Shop.Upholstery, c, ids[j], ids[(j + 1) % pts.Count], Vector3.forward);
        }

        /// <summary>
        /// 外殻の絵の上の点（横は真ん中の線の長さ x＝0〜<see cref="TubHalfLen"/>、縦は断面を回る長さ p）が内の面なら、
        /// その面の上の高さ y と、背の真ん中からその面に沿って測った長さ along と、ボタン留めの面の内か（0〜1）を返す。内の面でなければ 0（外の面は無地）。
        /// ボタン留めは縫い目の 3.5 cm 下から、床の側の縁の 5 cm 上まで、前の端の丸みの 3 cm 内まで。角と背の部分では途切れない
        /// </summary>
        static float ArmFace(float x, float p, out float y, out float along)
        {
            var v = p / ArmPerim;
            y = 0f;
            along = 0f;
            float w;
            bool inner;
            if (v > ArmV1 && v < ArmV2) { w = -TubHalfW; inner = true; }
            else return 0f;
            var length = TubLength(w);
            var s = x / TubHalfLen * length;
            var top = TubTop(TubSigma(s, w));
            var welt = top - ArmCrown - (inner ? ArmRollIn : ArmRollOut);
            y = inner ? Mathf.Lerp(ArmFloor, welt, (v - ArmV1) / (ArmV2 - ArmV1)) : Mathf.Lerp(welt, ArmFloor, (v - ArmV5) / (ArmV6 - ArmV5));
            along = length - s;
            return Smooth(0.012f, 0.035f, welt - y) * Smooth(0.03f, 0.05f, y - ArmFloor) * Smooth(0.04f, 0.065f, s);
        }

        /// <summary>張りぐるみの縫い目（上の縁の巻きと、内と外の面の境）の玉縁の高さ</summary>
        static float ArmPiping(float p)
        {
            var v = p / ArmPerim;
            return Piping(Mathf.Min(Mathf.Abs(v - ArmV2), Mathf.Abs(v - ArmV5)) * ArmPerim);
        }

        /// <summary>
        /// 張りぐるみの面の細かい起伏（m、外へ正）。内と外の面の菱形の膨らみ（1.2 cm）とボタンの窪み（8 mm）、上の縁の縫い目の玉縁。
        /// mesh には持たせず、絵（陰と法線）に描く。上の面は手を預ける所なので玉縁だけ
        /// </summary>
        static float ArmFine(float x, float p)
        {
            float y, along;
            var inside = ArmFace(x, p, out y, out along);
            var h = ArmPiping(p);
            if (inside > 0f)
            {
                var near = Nearest(ArmLattice, along, y);
                var hole = 0.008f * Mathf.Exp(-Mathf.Pow((new Vector2(along, y) - near).magnitude / 0.010f, 2f));
                h += inside * (0.012f * Dome(ArmLattice, along, y) - hole);
            }
            return h;
        }

        /// <summary>張りぐるみの面の、向きの無い陰（ボタン留めの菱形を塊として読ませる）</summary>
        static float ArmShade(float x, float p)
        {
            float y, along;
            var inside = ArmFace(x, p, out y, out along);
            return TuftShade(ArmLattice, inside, along, y);
        }

        /// <summary>張りぐるみの面の艶（折り目とボタンの周りは消し、膨らみの頂と玉縁は少し照る）</summary>
        static float ArmGloss(float x, float p)
        {
            float y, along;
            var inside = ArmFace(x, p, out y, out along);
            return TuftGloss(ArmLattice, inside, 0f, along, y, ArmPiping(p));
        }

        /// <summary>肘掛けの前の板の縁からの離れ（m。絵の広さ <see cref="ArmCapW"/> × <see cref="ArmCapH"/> の中で）</summary>
        static float ArmCapBorder(float x, float y)
        {
            return Mathf.Min(Mathf.Min(x, ArmCapW - x), Mathf.Min(y, ArmCapH - y));
        }

        // ---- 座の台 ------------------------------------------------------------------
        //
        // 座面のクッションの下の、外殻の内側を埋める革の台（床から 0.10 m まで）。脇と後ろは外殻の内の面に隠れ、見えるのは前の面だけ。
        // 前の面は、座った脚のふくらはぎと踵の後ろを通って斜めに後ろへ下り（座面の前の縁と肘掛けの前の端は揃えたまま、その下だけを逃がす）、
        // 床の近くの足置きのクッションにつながる

        /// <summary>座の台の上と下の高さ、半分の幅（外殻の内の面の 2 mm 内）、上の縁と下の縁の前の端、後ろの端（外殻の背の部分の内の面の 5 mm 前）、角の丸み</summary>
        const float BaseTop = 0.43f;
        const float BaseBottom = ArmFloor;
        const float BaseHalf = ArmIn - 0.002f;
        const float BaseTopFront = 0.24f, BaseBottomFront = 0.09f, BaseBack = TubIn + 0.005f, BaseRound = 0.03f;

        /// <summary>座の台の輪を置く高さ（下の縁から上の縁へ）</summary>
        static readonly float[] BaseRings = { BaseBottom, BaseBottom + 0.02f, 0.5f * (BaseBottom + BaseTop), BaseTop - 0.02f, BaseTop };

        /// <summary>座の台の輪の、真上から見た半分（前の真ん中から右の脇を回って後ろの真ん中まで）を回る割合の点</summary>
        static readonly float[] BaseRingT = { 0f, 1f, 1.5f, 2f, 3f, 4f, 5f };

        /// <summary>
        /// 座の台の輪の点（右の半分。x, z）。t は 0〜5 で、前のまっすぐな所（0〜1）、前の角（1〜2）、脇（2〜3）、後ろの角（3〜4）、後ろのまっすぐな所（4〜5）
        /// </summary>
        static Vector2 BasePoint(float front, float t)
        {
            var half = BaseHalf;
            var back = BaseBack;
            var round = BaseRound;
            if (t <= 1f) return new Vector2(Mathf.Lerp(0f, half - round, t), front);
            if (t <= 2f)
            {
                var a = Mathf.Lerp(90f, 0f, t - 1f) * Mathf.Deg2Rad;
                return new Vector2(half - round + round * Mathf.Cos(a), front - round + round * Mathf.Sin(a));
            }
            if (t <= 3f) return new Vector2(half, Mathf.Lerp(front - round, back + round, t - 2f));
            if (t <= 4f)
            {
                var a = Mathf.Lerp(0f, -90f, t - 3f) * Mathf.Deg2Rad;
                return new Vector2(half - round + round * Mathf.Cos(a), back + round + round * Mathf.Sin(a));
            }
            return new Vector2(Mathf.Lerp(half - round, 0f, t - 4f), back);
        }

        /// <summary>座の台。脇の面は輪を高さごとに張り（前の面は高さで前の端を寄せて斜めにする）、上と下は扇で閉じる。絵は無地の革</summary>
        static void SeatBase(Shop shop)
        {
            shop.Begin("SeatBase", 50f, true);
            var nt = BaseRingT.Length;
            var uv = SwatchUv(Swatch.Leather);
            foreach (var sx in new[] { 1f, -1f })
            {
                var idx = new int[BaseRings.Length, nt];
                for (var i = 0; i < BaseRings.Length; i++)
                {
                    var front = Mathf.Lerp(BaseBottomFront, BaseTopFront, Mathf.InverseLerp(BaseBottom, BaseTop, BaseRings[i]));
                    for (var j = 0; j < nt; j++)
                    {
                        var p = BasePoint(front, BaseRingT[j]);
                        idx[i, j] = shop.V(new Vector3(sx * p.x, BaseRings[i], p.y), uv);
                    }
                }
                for (var i = 0; i < BaseRings.Length - 1; i++)
                {
                    var c = new Vector3(0f, 0.5f * (BaseRings[i] + BaseRings[i + 1]), 0.5f * (BaseBack + BaseBottomFront));
                    for (var j = 0; j < nt - 1; j++)
                        shop.QOut(Shop.Upholstery, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], c);
                }
            }
            foreach (var top in new[] { true, false })
            {
                var y = top ? BaseTop : BaseBottom;
                var front = top ? BaseTopFront : BaseBottomFront;
                var centre = shop.V(new Vector3(0f, y, 0.5f * (front + BaseBack)), uv);
                var ids = new List<int>();
                for (var j = 0; j < nt; j++) { var p = BasePoint(front, BaseRingT[j]); ids.Add(shop.V(new Vector3(p.x, y, p.y), uv)); }
                for (var j = nt - 2; j >= 1; j--) { var p = BasePoint(front, BaseRingT[j]); ids.Add(shop.V(new Vector3(-p.x, y, p.y), uv)); }
                for (var j = 0; j < ids.Count; j++) shop.TFacing(Shop.Upholstery, centre, ids[j], ids[(j + 1) % ids.Count], top ? Vector3.up : Vector3.down);
            }
            shop.End(Orient.None);
        }

        /// <summary>右の肘掛けの差込口。革に螺子で留めた金属の角の板に、金属の縁、黒い穴、穴を囲む紫の灯り（ジャックを挿す所の合図）</summary>
        static void Port(Shop shop)
        {
            var c = PortAt;
            Slab(shop, "PortPlate", Shop.Chrome, new Vector3(c.x, PadTop(c.z) + 0.0025f, c.z), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.056f, 0.004f, 0.056f), 0f, true);
            Tube(shop, "PortBezel", Shop.Chrome, new List<Vector3> { new Vector3(c.x, c.y - 0.022f, c.z), new Vector3(c.x, c.y + 0.0025f, c.z) }, 0.022f, 8, true, Vector2.zero, 70f);
            Ring(shop, "PortGlow", new Vector3(c.x, c.y + 0.0035f, c.z), Vector3.up, 0.0145f, 0.0195f, 8, SwatchUv(Swatch.VioletLit));
            Disc(shop, "PortHole", new Vector3(c.x, c.y + 0.004f, c.z), Vector3.up, 0.0145f, 8, SwatchUv(Swatch.Rubber));
        }

        // ---- 台座 ------------------------------------------------------------------

        /// <summary>台座の真ん中（座面の真ん中より少し後ろ。背もたれの重さの側）</summary>
        const float PedestalZ = -0.08f;
        /// <summary>台座の高さ（床から上の面まで）と、縁の光る面取りの幅</summary>
        const float PedestalHeight = 0.06f;
        const float PedestalBevel = 0.014f;

        /// <summary>
        /// 台座。外殻と座の台の下に置く、低く幅の広い黒く磨いた金属の円盤（半径 0.31 m、高さ 6 cm）。
        /// 上の縁に細く光る面取り（磨いた金属）を回す。台座と座の台のあいだは 4 cm の短い柱で、外殻の下の縁（床から 0.10 m）が柱と台座をほとんど隠す。
        /// 前の縁は足置きのクッションの下に入る（クッションの下の面は台座の上の面より上）
        /// </summary>
        static void Base(Shop shop)
        {
            var at = new Vector3(0f, 0f, PedestalZ);
            var r = PedestalReach;
            // 床に接する縁から脇の面
            Lathe(shop, "Pedestal", Shop.Panel, at, new[]
            {
                new Vector2(r - 0.012f, 0f), new Vector2(r, 0.008f), new Vector2(r, PedestalHeight - PedestalBevel),
            }, 24, 40f, SwatchUv(Swatch.BlackChrome));
            // 上の縁の光る面取り
            Lathe(shop, "PedestalBevel", Shop.Panel, at, new[]
            {
                new Vector2(r, PedestalHeight - PedestalBevel), new Vector2(r - PedestalBevel, PedestalHeight),
            }, 24, 30f, SwatchUv(Swatch.Chrome));
            // 上の面と柱
            Lathe(shop, "PedestalTop", Shop.Panel, at, new[]
            {
                new Vector2(r - PedestalBevel, PedestalHeight), new Vector2(0.075f, PedestalHeight + 0.002f),
            }, 24, 30f, SwatchUv(Swatch.BlackChrome));
            Tube(shop, "Column", Shop.Chrome, new List<Vector3> { new Vector3(0f, PedestalHeight, 0f), new Vector3(0f, BaseBottom + 0.01f, 0f) }, 0.05f, 12, false, Vector2.zero, 70f);
        }

        /// <summary>回し形。輪郭（半径, 高さ）を下から上へ並べ、at を通る y の軸のまわりに回した面。面は軸から外へ向ける</summary>
        static void Lathe(Shop shop, string name, int sub, Vector3 at, Vector2[] profile, int sides, float crease, Vector2 uv)
        {
            shop.Begin(name, crease, false);
            var idx = new int[profile.Length, sides];
            for (var i = 0; i < profile.Length; i++)
                for (var k = 0; k < sides; k++)
                {
                    var a = (float)k / sides * Mathf.PI * 2f;
                    idx[i, k] = shop.V(at + new Vector3(profile[i].x * Mathf.Cos(a), profile[i].y, profile[i].x * Mathf.Sin(a)), uv);
                }
            for (var i = 0; i < profile.Length - 1; i++)
                for (var k = 0; k < sides; k++)
                {
                    var k1 = (k + 1) % sides;
                    var axis = at + new Vector3(0f, 0.5f * (profile[i].y + profile[i + 1].y) - 0.05f, 0f);
                    shop.QOut(sub, idx[i, k], idx[i + 1, k], idx[i + 1, k1], idx[i, k1], axis);
                }
            shop.End(Orient.None);
        }

        // ---- 足置き ------------------------------------------------------------------

        /// <summary>
        /// 足置きのクッションの半分の幅、前後の端。後ろの端は座の台の前の面の斜めの所に深く入れる（座の台の前の面がクッションの平らな上の面へつながって見える。
        /// 浅く入れると、クッションの後ろの丸めた縁が斜めの面からはみ出して、部屋の明かりを白い一筋に返した）。
        /// 前の端は立ち上がって椅子を押し下げた時に、机との間の立ち位置の当たりの縁から 3 cm 手前に来る所（<see cref="PushBack"/>）
        /// </summary>
        const float FootHalf = 0.24f;
        const float FootBack = 0.05f;
        const float FootFront = 0.445f;
        /// <summary>クッションの下の面の高さ（後ろの端と前の端）。上の面は足の裏の線（<see cref="FootTop"/>）なので、後ろで 10 cm、前で 5 cm の厚み</summary>
        const float FootUnderBack = 0.08f;
        const float FootUnderFront = 0.02f;

        /// <summary>足置きのクッションの縫い目の所（脇の丸みのすぐ内と、前の丸みのすぐ後ろ。絵に玉縁を描く）</summary>
        const float FootWeltX = FootHalf - 0.032f;
        const float FootWeltZ = FootFront - 0.04f;

        /// <summary>足置きのボタン留めの面の内か（後ろは座の台の前の面に入る所の手前まで）</summary>
        static float FootTuftInside(float x, float z)
        {
            return (1f - Smooth(FootWeltX - 0.03f, FootWeltX - 0.01f, Mathf.Abs(x))) * (1f - Smooth(FootWeltZ - 0.03f, FootWeltZ - 0.01f, z)) * Smooth(FootBack + 0.04f, FootBack + 0.07f, z);
        }

        /// <summary>足置きの上の面の細かい起伏（菱形の膨らみ 1.6 cm、ボタンの窪み 1 cm、玉縁）。足の裏が乗る面の高さ（mesh）は変えない</summary>
        static float FootFine(float x, float z) { return CushionFine(FootLattice, FootTuftInside(x, z), 0.016f, 0.010f, x, z, FootWeltX, FootWeltZ); }

        static float FootPadTop(float x, float z) { return FootTop(z); }
        static float FootPadUnder(float x, float z) { return Mathf.Lerp(FootUnderBack, FootUnderFront, Mathf.InverseLerp(FootBack, FootFront, z)); }

        static readonly Cushion FootShape = new Cushion
        {
            Top = FootPadTop, Under = FootPadUnder, Front = FootFront, Back = FootBack, Half = FootHalf, Edge = 0.03f, Corner = 0.05f, MidZ = 0.30f, Uv = FootUv,
        };

        /// <summary>
        /// 足置き。座の台の前の面（座面の前の縁の下から斜めに後ろへ下りる革の面）が、床の近くの厚い革のクッションへつながる形
        /// （高い椅子の足置きの仕掛けを革で包んだ形。別に置く足台にはしない＝立つ時に椅子が床を滑って下がるので、別置きだと立ち位置に残る）。
        /// クッションの上の面は座った形の足の裏に沿わせて前へ 18 度下げ（<see cref="FootTop"/>）、座面と同じ菱形のボタン留めと玉縁。当たりは持たない
        /// </summary>
        static void Footrest(Shop shop)
        {
            shop.Begin("FootPad", 55f, true);
            CushionGrid(shop, FootShape, new List<float> { 0f, 0.12f, FootHalf - 0.03f, 0.225f, 0.235f, FootHalf });
            shop.End(Orient.Whole);
        }

        // ---- 頭の後ろの端末 ------------------------------------------------------------

        /// <summary>
        /// 腰の後ろの分岐の箱の高さ（倒れた向きに沿って、箱の真ん中）。外殻の背の部分の上の面（0.68 m）に埋もれないよう、箱の下の縁をその上に置く
        /// </summary>
        const float JunctionS = 0.33f;
        /// <summary>裏の端末の箱の高さの寄せ（背もたれの上の縁の巻きに掛からないよう、上の縁から 6.5 cm 下に箱の上の縁を置く）</summary>
        const float CrownDs = -0.045f;
        /// <summary>裏の端末の箱の裏の面（前の基準の面から前へ。負）</summary>
        const float CrownOut = -0.165f - RearShift;
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
                    Back(sx * 0.12f, EarS + 0.012f, -0.13f - RearShift),
                    Back(sx * 0.22f, EarS + 0.01f, -0.122f - RearShift),
                    Back(sx * 0.29f, EarS + 0.006f, -0.108f - 0.8f * RearShift),
                    Back(sx * (earX - 0.012f), EarS + 0.003f, -0.08f - 0.4f * RearShift),
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
                    Back(x, top + 0.004f, -0.105f - RearShift),
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
            const float jOut = -0.155f - RearShift;
            Slab(shop, "Junction", Shop.Frame, Back(0f, JunctionS, 0.5f * (jOut - BackThick + 0.004f)), rot, new Vector3(0.17f, 0.135f, -BackThick + 0.004f - jOut), 0f, true);
            Decal(shop, "JunctionFace", Back(0f, JunctionS + 0.001f, jOut - 0.0008f), Vector3.right * 0.07f, BackUp * 0.045f, -BackFwd, PanelUv(JunctionFace));
            // 背の半ばでケーブルを束ねる留め具
            Slab(shop, "CableClip", Shop.Frame, Back(0f, 0.452f, -0.114f - RearShift), rot, new Vector3(0.13f, 0.024f, 0.036f), 0f, true);
        }

        /// <summary>ケーブル。太い物は断面を四角、細い物は三角にする（粗い画面では丸く見える）</summary>
        static void Cable(Shop shop, string name, List<Vector3> path, int steps, float radius, Swatch colour)
        {
            Tube(shop, name, Shop.Panel, Spline(path, steps), radius, radius >= 0.008f ? 4 : 3, false, SwatchUv(colour), 65f);
        }

        /// <summary>
        /// 後から這わせたケーブル。裏の箱の端子三本は背の半ばの留め具を通って腰の分岐の箱へ、耳の筐体の二本は背もたれの縁を回って裏の箱の脇へ。
        /// 分岐の箱から太い一本と右の肘掛けへの一本が、背もたれの裏を下りて外殻の背の部分の外の面へ入る（中を通って座の下の機構と、右の肘掛けの差込口へ）。
        /// 左右の肘掛けの前の操作盤からも一本ずつ、肘掛けの前の面を下りて中へ入る
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
                    Back(p.x * 1.4f + sway * 0.3f, 0.62f + CrownDs, -0.19f - RearShift),
                    Back(p.x * 1.2f + sway, 0.53f, -0.155f - RearShift),
                    Back(p.x * 0.7f, 0.452f, -0.123f - RearShift),
                    Back(p.x * 0.75f, JunctionS + 0.065f, -0.130f - RearShift),
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
                    Back(sx * (earX + 0.004f), EarS - 0.04f, -0.085f - 0.5f * RearShift),
                    Back(sx * 0.29f, EarS - 0.055f, -0.115f - 0.8f * RearShift),
                    Back(sx * 0.20f, EarS - 0.07f, -0.128f - RearShift),
                    Back(sx * 0.125f, EarS - 0.085f, -0.13f - RearShift),
                };
                Cable(shop, "EarCable" + (sx > 0f ? "R" : "L"), path, 6, 0.0055f, sx > 0f ? Swatch.Grey : Swatch.Teal);
            }
            // 分岐の箱から、背もたれの裏を下りて外殻の背の部分の外の面へ入る（太い一本。中を通って座の下の機構へ）
            var top = TubTop(TubHalfLen);
            var outFace = TubIn - 2f * TubHalfW;
            var down = new List<Vector3>
            {
                Back(0f, JunctionS - 0.065f, -0.12f - RearShift),
                Back(0f, JunctionS - 0.10f, -0.13f - RearShift),
                new Vector3(0f, top - 0.02f, outFace - 0.012f),
                new Vector3(0f, top - 0.09f, outFace - 0.005f),
                new Vector3(0f, top - 0.13f, outFace + 0.004f),
            };
            Cable(shop, "TrunkCable", down, 6, 0.011f, Swatch.Rubber);
            // 分岐の箱から、背の部分の外の面を右へ下りて中へ入る（中を通って右の肘掛けの差込口へ）
            var arm = new List<Vector3>
            {
                Back(0.08f, JunctionS - 0.04f, -0.125f - RearShift),
                Back(0.15f, JunctionS - 0.07f, -0.135f - RearShift),
                new Vector3(0.22f, top - 0.02f, outFace - 0.012f),
                new Vector3(0.27f, top - 0.08f, outFace - 0.006f),
                new Vector3(0.29f, top - 0.12f, outFace + 0.004f),
            };
            Cable(shop, "ArmCable", arm, 7, 0.0065f, Swatch.Violet);
            // 肘掛けの操作盤から、肘掛けの前の面を下りて肘掛けの下へ
            foreach (var sx in new[] { 1f, -1f })
            {
                var path = new List<Vector3>
                {
                    new Vector3(sx * 0.40f, PodBottom - 0.008f, PodBack + 0.012f),
                    new Vector3(sx * 0.40f, 0.52f, ArmFront + 0.006f),
                    new Vector3(sx * 0.40f, 0.42f, ArmFront + 0.005f),
                    new Vector3(sx * 0.40f, 0.34f, ArmFront - 0.002f),
                };
                Cable(shop, "PodCable" + (sx > 0f ? "R" : "L"), path, 6, 0.005f, sx > 0f ? Swatch.Violet : Swatch.Grey);
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
