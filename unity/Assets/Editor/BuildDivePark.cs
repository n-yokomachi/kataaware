using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公園（設計書 9.1 節「park（公園）」「公園の作り込み」）。
    /// 住宅地の中の小さな公園で、四方を黒い鉄の柵で囲い、門から入る。3 月の午後 3 時台。
    ///
    /// **一目で分かる形は三つ。ベンチの列・街灯・明るい小径の帯。** 等間隔に並ぶものとして
    /// 柵の縦の桟と支柱、街灯の列を置く。ほかの物（植え込み・ごみ箱・水飲み場・池の柵）は、
    /// この三つが立ってから隙間を埋めるために置いてある。
    ///
    /// **公営住宅と同じ三段で距離を作る。**
    /// <list type="table">
    /// <item><term>近く（このファイル）</term><description>柵の内側。門と柵、プラタナス、小径と縁石、ベンチ、街灯、池、
    /// 植え込み、ごみ箱、水飲み場、掲示板。芝と土と落ち葉は地面の絵で塗り分ける（<see cref="ParkGroundPicture"/>）</description></item>
    /// <item><term>中（<c>BuildDiveParkMid.cs</c>）</term><description>柵の外の通りと、向かいの煉瓦のテラスハウス。
    /// 門の正面は横丁になっていて、奥まで家並みが続く</description></item>
    /// <item><term>遠く（<c>BuildDiveParkFar.cs</c>）</term><description>組んで撮って板に貼る書き割り。屋根の海と煙突、
    /// 教会の尖塔、高層棟、遠くの木々</description></item>
    /// </list>
    ///
    /// **人と鍵打ちは動かさない。** 記憶 2・3・9・10 の立ち位置・歩く道筋・板の出る所は <c>BuildDiveTakes.cs</c> の値のまま。
    /// 置いた物はそれを塞がない所に置き、出来事が場所の物を指している所（門の柱に手を置く）は、物の方を出来事に合わせる。
    ///
    /// **入れ物とマテリアルは公営住宅の敷地と分け合う**（<see cref="YardBanks"/>、<c>EstateYard*</c> のマテリアル）。
    /// 公園にしか無い色は地面の絵・小径・水だけ。mesh の名前は <c>Park</c> で始める。
    ///
    /// 寸法は場所のローカル。+z が門の側で、方角では西南西。午後の日は門の側の低い所から差す。
    /// 道具の名前は <c>Park</c> で始める。五つの場所が同じ partial class を分け合っていて、短い名前は衝突する
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 目印 ----------------------------------------------------------------

        /// <summary>左のベンチ。記憶 2 のアルベルトが掛けている</summary>
        public static readonly Vector3 BenchA = new Vector3(0f, 0f, 0f);
        /// <summary>右のベンチ。記憶 9 のローザが餌の袋を畳んでいた</summary>
        public static readonly Vector3 BenchB = new Vector3(2.6f, 0f, 0f);
        /// <summary>
        /// 三脚目。人は掛けない。
        /// 二脚では「たまたま二つ置いてある」にしか見えず、列として読めないので同じ間隔で伸ばす
        /// </summary>
        public static readonly Vector3 BenchC = new Vector3(5.2f, 0f, 0f);
        /// <summary>
        /// 門の側の柵の線。ベンチから真っ直ぐ +z。
        /// 記憶 9・10 で祖父が門柱の手前（z 8.7）に立つので、柱の台がその胸に触れない所まで下げてある
        /// </summary>
        public const float GateZ = 9.3f;
        /// <summary>
        /// 門柱の真ん中の x。**西の柱は記憶 2 のアルベルトが最後に手を置く柱。** 歩き終えた所（-0.3, 8.5）から
        /// 向いている 318 度の左前 1 m（手を伸ばして届く面まで 0.65 m）に来るよう、門の方を柱に合わせて据えた。
        /// 記憶 9 と 10 で祖父（-0.9, 8.7）が立つのはこの柱の手前
        /// </summary>
        const float GatePostWest = -0.85f;
        const float GatePostEast = 1.35f;
        /// <summary>門柱の太さ</summary>
        const float GatePost = 0.46f;
        /// <summary>
        /// 通り抜けの小さな門。記憶 3 のプリヤが通りの歩道（7.6, 10.5）から入ってきて、東の芝生を南へ横切る。
        /// 柵を閉じると歩く線が柵を抜けるので、線が柵を跨ぐ所に口を開ける
        /// </summary>
        const float SideGateX = 7.45f;
        const float SideGateHalf = 0.6f;
        /// <summary>柵の西・東・南の線</summary>
        const float ParkWest = -13f;
        const float ParkEast = 13f;
        const float ParkSouth = -12f;
        /// <summary>池の縁の中心と半径。鳩はこの手前に集まる</summary>
        public static readonly Vector3 Pond = new Vector3(-4.2f, 0f, 4.6f);
        const float PondR = 2.8f;
        /// <summary>
        /// 池の手前側の半径。<see cref="PondR"/> より狭い。
        /// 真円のままだと水際が主の歩く線（記憶 2 の -1.3、記憶 10 の -1.6）に重なって、
        /// 池の縁に立ったはずの人が水の上に立つ
        /// </summary>
        const float ParkPondNear = 1.6f;
        /// <summary>池の南北の半径</summary>
        const float ParkPondSide = 2.6f;
        /// <summary>池の柵を並べる角度の幅。小径から見える手前側だけ</summary>
        const float ParkHoopArc = 75f;

        // ---- 小径 ----------------------------------------------------------------
        //
        // 門からベンチの列まで真っ直ぐ一本（記憶 2・9・10 で人が歩く筋）。ベンチの前を東西に一本、
        // その西の端と東の端から南へ回る輪。池へは門からの小径の西へ広げた所から寄る。
        // ベンチ A が門の正面に来るので、門からの小径はベンチの列で終わる

        /// <summary>門からベンチまでの小径の西と東の縁</summary>
        const float WalkWest = -1.0f;
        const float WalkEast = 1.3f;
        /// <summary>ベンチの前を東西に通る小径の南と北の縁</summary>
        const float CrossSouth = -0.9f;
        const float CrossNorth = 0.6f;
        /// <summary>東西の小径の両端。ここから南へ回る</summary>
        const float CrossEnd = 10.2f;
        /// <summary>南へ回る小径の幅</summary>
        const float LoopWide = 1.0f;
        /// <summary>南の東西の小径の北の縁</summary>
        const float LoopNorth = -10.0f;
        /// <summary>ベンチの列から南の小径へ下りる細い小径の西と東の縁。ベンチ A の西の脇を通る</summary>
        const float LinkWest = -2.0f;
        const float LinkEast = -1.0f;

        // ---- 街灯 ----------------------------------------------------------------

        /// <summary>街灯の間隔。等間隔の繰り返しが、ここが公園だといちばん早く伝える</summary>
        const float ParkLampStep = 8f;
        /// <summary>
        /// 街灯の列の x と、一番北の一本の z。池への寄り付きの西の端から、ベンチの列の後ろ、南の小径の手前まで、
        /// 南へ下りる細い小径の西の肩に沿って一列に立てる
        /// </summary>
        const float ParkLampX = -2.35f;
        const float ParkLampZ = 6.7f;

        /// <summary>
        /// 午後の日の向き（Euler）。**空の絵の日もこの向きに描く**（<see cref="ParkSunward"/>）。
        /// 3 月の 15 時 47 分のロンドンは、日が南西の仰角 15〜20 度。ここでは 20 度に取り、門の側（+z）から
        /// 少し西へ振って差させる。門へ歩く人と門に立つ人はこれで逆光になり、日は門の正面の横丁の上に見える
        /// </summary>
        static readonly Vector3 ParkAfternoonAim = new Vector3(20f, 170f, 0f);

        // ---- 組み立て --------------------------------------------------------------

        /// <summary>
        /// 記憶 2・3・9・10 の舞台
        /// </summary>
        static void Park(Transform place)
        {
            var y = new YardBanks();
            // 近く
            var ground = new Bank { Texel = 1f };
            ground.Patch(new Vector3(ParkWest, 0f, GateZ), new Vector3(ParkEast, 0f, GateZ),
                new Vector3(ParkEast, 0f, ParkSouth), new Vector3(ParkWest, 0f, ParkSouth),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            EstateEmit(place, "ParkGround", ground, ParkGroundMat(), true);

            var walk = new Bank { Texel = 0.35f };
            ParkPaths(walk, y);
            EstateEmit(place, "ParkPath", walk, ParkMat("ParkPave", new Color(0.450f, 0.420f, 0.350f), 0.08f), false);

            ParkRailings(y);
            ParkGate(y);
            ParkPondEdge(y);
            var pool = new Bank { Texel = 0.2f };
            pool.FanY(new Vector3(Pond.x, 0.06f, Pond.z), ParkRim(0f));
            Emit(place, "ParkWater", pool, "Water");

            Bench(y, BenchA);
            Bench(y, BenchB);
            Bench(y, BenchC);
            for (var i = 0; i < 3; i++)
                ParkLamp(y, new Vector3(ParkLampX, 0f, ParkLampZ - i * ParkLampStep));
            ParkThings(y);
            ParkTrees(y);

            // 中（柵の外の通りと家並み）。同じ入れ物に溜めて、同じ色の物を一枚の mesh にまとめる
            ParkMid(y);
            y.Emit(place, "Park", ParkCrownMat());

            // 遠く（本物の地面の縁と、撮って貼った書き割りの輪）
            FarLand(place, ParkRing, "ParkLand", EstateLandMat());
            Backdrop(place, ParkRing, "HalfAware/Shoot the park backdrop");

            // 鳩は場所ではなく記憶の側に置く（BuildDiveTakes.Doves）。
            // 飛び立つ秒が記憶ごとに違うので、場所に置くと四つの記憶で同じ瞬間に飛ぶことになる

            ParkFences(place);
            ParkLights(place);
        }

        /// <summary>
        /// 灯り。午後の日と、日の当たらない側の下限。
        ///
        /// 環境光は空の絵から取った三色（<see cref="ParkPlaceSky"/>）が受け持つので、
        /// 前のように影を落とさない灯りを二つ入れて底を持ち上げることはしない。
        /// 一つだけ残すのは、日と反対の空（東の高い所）から来る青い光の代わり。
        /// これが無いと、逆光の人の顔と胸が環境光だけの色になって、輪郭が拾えない
        /// </summary>
        static void ParkLights(Transform place)
        {
            var sun = Lamp(place, "Afternoon", LightType.Directional, new Vector3(0f, 12f, 0f),
                ParkAfternoonAim, new Color(1f, 0.88f, 0.70f), 2.3f, 10f);
            sun.shadows = LightShadows.Soft;
            Lamp(place, "ParkFill", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(38f, -20f, 0f), new Color(0.70f, 0.76f, 0.92f), 0.55f, 10f);
        }

        // ---- 小径 ----------------------------------------------------------------

        /// <summary>
        /// 小径。砂利を固めた明るい帯を、地面から 2 cm 浮かせて重ねる。
        /// 門からの小径とベンチの前の小径は、両脇に低い石の縁を回す（<see cref="YardBanks.Kerb"/>）
        /// </summary>
        static void ParkPaths(Bank b, YardBanks y)
        {
            const float h = 0.02f;
            // 門からベンチまで。門の口の下まで伸ばして、外の歩道と繋ぐ
            b.FaceY(h, WalkWest, WalkEast, CrossNorth, GateZ + 0.16f, 1);
            // ベンチの前の東西
            b.FaceY(h, -CrossEnd, CrossEnd, CrossSouth, CrossNorth, 1);
            // 西と東の端から南へ、南の東西
            b.FaceY(h, -CrossEnd, -CrossEnd + LoopWide, LoopNorth, CrossSouth, 1);
            b.FaceY(h, CrossEnd - LoopWide, CrossEnd, LoopNorth, CrossSouth, 1);
            b.FaceY(h, -CrossEnd, CrossEnd, LoopNorth - LoopWide, LoopNorth, 1);
            // ベンチ A の西の脇から南へ
            b.FaceY(h, LinkWest, LinkEast, LoopNorth, CrossSouth, 1);
            // 池への寄り付き。門からの小径の西へ、池の縁の手前まで広げる
            b.FaceY(h, -2.3f, WalkWest, 2.2f, 7.2f, 1);

            // 縁石。門からの小径の東、西（池への寄り付きの所は開ける）、ベンチの前の小径の北と南
            const float k = 0.08f;
            ParkKerb(y, new Vector3(WalkEast + k * 0.5f, 0f, CrossNorth + k), new Vector3(WalkEast + k * 0.5f, 0f, GateZ - 0.2f));
            ParkKerb(y, new Vector3(WalkWest - k * 0.5f, 0f, CrossNorth + k), new Vector3(WalkWest - k * 0.5f, 0f, 2.2f));
            ParkKerb(y, new Vector3(WalkWest - k * 0.5f, 0f, 7.2f), new Vector3(WalkWest - k * 0.5f, 0f, GateZ - 0.2f));
            ParkKerb(y, new Vector3(-CrossEnd, 0f, CrossNorth + k * 0.5f), new Vector3(WalkWest - k, 0f, CrossNorth + k * 0.5f));
            ParkKerb(y, new Vector3(WalkEast + k, 0f, CrossNorth + k * 0.5f), new Vector3(CrossEnd, 0f, CrossNorth + k * 0.5f));
            ParkKerb(y, new Vector3(-CrossEnd + LoopWide + k, 0f, CrossSouth - k * 0.5f), new Vector3(LinkWest - k, 0f, CrossSouth - k * 0.5f));
            ParkKerb(y, new Vector3(LinkEast + k, 0f, CrossSouth - k * 0.5f), new Vector3(CrossEnd - LoopWide - k, 0f, CrossSouth - k * 0.5f));
        }

        /// <summary>小径の縁の低い石。地面から 6 cm</summary>
        static void ParkKerb(YardBanks y, Vector3 a, Vector3 b)
        {
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.05f) return;
            y.Kerb.Box((a + b) * 0.5f + Vector3.up * 0.03f, new Vector3(0.08f, 0.06f, len),
                Quaternion.LookRotation(d / len, Vector3.up));
        }

        // ---- 柵と門 --------------------------------------------------------------

        /// <summary>
        /// 四方の黒い鉄の柵。門の側は門と通り抜けの口を開ける
        /// </summary>
        static void ParkRailings(YardBanks y)
        {
            var nw = new Vector3(ParkWest, 0f, GateZ);
            var ne = new Vector3(ParkEast, 0f, GateZ);
            var sw = new Vector3(ParkWest, 0f, ParkSouth);
            var se = new Vector3(ParkEast, 0f, ParkSouth);
            ParkRailing(y, nw, new Vector3(GatePostWest - GatePost * 0.5f, 0f, GateZ));
            ParkRailing(y, new Vector3(GatePostEast + GatePost * 0.5f, 0f, GateZ), new Vector3(SideGateX - SideGateHalf - 0.06f, 0f, GateZ));
            ParkRailing(y, new Vector3(SideGateX + SideGateHalf + 0.06f, 0f, GateZ), ne);
            ParkRailing(y, ne, se);
            ParkRailing(y, se, sw);
            ParkRailing(y, sw, nw);
        }

        /// <summary>
        /// 黒い鉄の柵を一辺。低い石の縁に立て、上と下に横の桟、縦の桟を 0.16 m ごと、先に槍の頭。
        /// 支柱を 2.5 m ごとに少し太く立て、頭に小さな玉を載せる。
        ///
        /// **縦の桟は四面だけの細い柱にして頂点を抑える**（<see cref="YardRailing"/> と同じ作り）。
        /// 槍の頭は柵の面に沿った菱形を裏表の二枚で。320×180 では形より、等間隔に並ぶ先の点が柵を柵に見せる
        /// </summary>
        static void ParkRailing(YardBanks y, Vector3 a, Vector3 b)
        {
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.1f) return;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var side = Vector3.Cross(Vector3.up, dir);
            var mid = (a + b) * 0.5f;
            const float top = 1.14f;
            y.Kerb.Box(mid + Vector3.up * 0.09f, new Vector3(0.30f, 0.18f, len), rot);
            y.Iron.Box(mid + Vector3.up * 0.30f, new Vector3(0.035f, 0.035f, len), rot);
            y.Iron.Box(mid + Vector3.up * 1.02f, new Vector3(0.035f, 0.035f, len), rot);

            var bars = Mathf.Max(1, Mathf.RoundToInt(len / 0.16f));
            const float t = 0.011f;
            for (var i = 1; i < bars; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)bars);
                // 四面。上下の蓋は要らない
                var c0 = at - dir * t - side * t;
                var c1 = at + dir * t - side * t;
                var c2 = at + dir * t + side * t;
                var c3 = at - dir * t + side * t;
                var lo = Vector3.up * 0.18f;
                var hi = Vector3.up * top;
                MidQuad(y.Iron, c0 + lo, c1 + lo, c1 + hi, c0 + hi, -side);
                MidQuad(y.Iron, c1 + lo, c2 + lo, c2 + hi, c1 + hi, dir);
                MidQuad(y.Iron, c2 + lo, c3 + lo, c3 + hi, c2 + hi, side);
                MidQuad(y.Iron, c3 + lo, c0 + lo, c0 + hi, c3 + hi, -dir);
                // 槍の頭。柵の面に沿った菱形
                var tip = at + Vector3.up * (top + 0.10f);
                var w0 = at + Vector3.up * (top + 0.02f) - dir * 0.028f;
                var w1 = at + Vector3.up * (top + 0.02f) + dir * 0.028f;
                var root = at + Vector3.up * top;
                MidQuad(y.Iron, root, w1, tip, w0, side);
                MidQuad(y.Iron, root, w0, tip, w1, -side);
            }
            var posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.5f));
            for (var i = 0; i <= posts; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)posts);
                y.Iron.Box(at + Vector3.up * 0.70f, new Vector3(0.07f, 1.04f, 0.07f), rot);
                y.Iron.Box(at + Vector3.up * 1.24f, new Vector3(0.10f, 0.06f, 0.10f), rot);
                y.Iron.Box(at + Vector3.up * 1.31f, new Vector3(0.08f, 0.08f, 0.08f), rot * Quaternion.Euler(45f, 0f, 45f));
            }
        }

        /// <summary>
        /// 門。煉瓦の門柱二本に石の台と笠と頭の玉、黒い鉄の門扉を二枚、外（歩道の側）へ開け放してある。
        /// 西の柱の脇の柵に公園の名の札。通り抜けの口には細い鉄の柱と、開けた扉を一枚。
        ///
        /// **門扉は外へ開ける。** 内へ開けると、門の前に立つ祖父と孫（記憶 9・10）の脇を扉が塞ぐ
        /// </summary>
        static void ParkGate(YardBanks y)
        {
            const float high = 2.1f;
            foreach (var x in new[] { GatePostWest, GatePostEast })
            {
                var foot = new Vector3(x, 0f, GateZ);
                y.Kerb.Box(foot + Vector3.up * 0.15f, new Vector3(GatePost + 0.08f, 0.30f, GatePost + 0.08f));
                y.Brick.Box(foot + Vector3.up * (high * 0.5f), new Vector3(GatePost, high, GatePost));
                y.Kerb.Box(foot + Vector3.up * (high + 0.05f), new Vector3(GatePost + 0.12f, 0.10f, GatePost + 0.12f));
                y.Kerb.Box(foot + Vector3.up * (high + 0.16f), new Vector3(GatePost - 0.08f, 0.12f, GatePost - 0.08f));
                // 頭の玉。向きを変えた箱を二つ重ねて丸く
                y.Kerb.Box(foot + Vector3.up * (high + 0.36f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.Euler(0f, 45f, 0f));
                y.Kerb.Box(foot + Vector3.up * (high + 0.36f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.Euler(45f, 0f, 45f));
            }
            // 門扉。柱の外の面の丁番から、外へ 100 度ほど開けて歩道に立てる
            var clear = (GatePostEast - GatePostWest - GatePost) * 0.5f;
            ParkGateLeaf(y, new Vector3(GatePostWest + GatePost * 0.5f, 0f, GateZ + 0.05f), Vector3.right, clear, 80f);
            ParkGateLeaf(y, new Vector3(GatePostEast - GatePost * 0.5f, 0f, GateZ + 0.05f), Vector3.left, clear, 80f);

            // 通り抜けの口。細い柱と、開けた扉を一枚
            foreach (var x in new[] { SideGateX - SideGateHalf, SideGateX + SideGateHalf })
            {
                y.Iron.Box(new Vector3(x, 0.72f, GateZ), new Vector3(0.10f, 1.44f, 0.10f));
                y.Iron.Box(new Vector3(x, 1.50f, GateZ), new Vector3(0.14f, 0.12f, 0.14f));
            }
            ParkGateLeaf(y, new Vector3(SideGateX + SideGateHalf - 0.05f, 0f, GateZ + 0.05f), Vector3.left, SideGateHalf * 2f - 0.1f, 95f);

            // 公園の名の札。西の柱の脇の柵に、通りの側を向けて。字は読めなくてよいので白地に黒い帯を並べる
            const float sx = GatePostWest - GatePost * 0.5f - 1.05f;
            const float sz = GateZ + 0.06f;
            y.Iron.Box(new Vector3(sx, 1.28f, sz), new Vector3(1.60f, 0.52f, 0.03f));
            y.Line.Box(new Vector3(sx, 1.28f, sz + 0.02f), new Vector3(1.50f, 0.42f, 0.02f));
            y.Iron.Box(new Vector3(sx, 1.36f, sz + 0.035f), new Vector3(1.20f, 0.09f, 0.01f));
            y.Iron.Box(new Vector3(sx - 0.25f, 1.20f, sz + 0.035f), new Vector3(0.70f, 0.05f, 0.01f));
        }

        /// <summary>
        /// 門扉を一枚。<paramref name="hinge"/> は丁番の足元、<paramref name="shut"/> は閉めたときに丁番から伸びる向き
        /// （<see cref="Vector3.right"/> か <see cref="Vector3.left"/>）、<paramref name="wide"/> は扉の幅、
        /// <paramref name="swing"/> は閉めた所から外（+z、歩道の側）へ開けた角度
        /// </summary>
        static void ParkGateLeaf(YardBanks y, Vector3 hinge, Vector3 shut, float wide, float swing)
        {
            var along = Quaternion.Euler(0f, shut.x > 0f ? -swing : swing, 0f) * shut;
            var rot = Quaternion.LookRotation(along, Vector3.up);
            System.Func<float, float, Vector3> p = (s, h) => hinge + along * s + Vector3.up * h;
            y.Iron.Box(p(wide * 0.5f, 0.10f), new Vector3(0.04f, 0.04f, wide), rot);
            y.Iron.Box(p(wide * 0.5f, 1.12f), new Vector3(0.04f, 0.04f, wide), rot);
            y.Iron.Box(p(0.02f, 0.66f), new Vector3(0.05f, 1.32f, 0.05f), rot);
            y.Iron.Box(p(wide - 0.02f, 0.66f), new Vector3(0.05f, 1.26f, 0.05f), rot);
            var bars = Mathf.Max(2, Mathf.RoundToInt(wide / 0.13f));
            for (var i = 1; i < bars; i++)
            {
                var s = wide * i / bars;
                y.Iron.Box(p(s, 0.64f), new Vector3(0.022f, 1.08f, 0.022f), rot);
                y.Iron.Box(p(s, 1.20f), new Vector3(0.012f, 0.06f, 0.05f), rot * Quaternion.Euler(0f, 90f, 45f));
            }
        }

        // ---- 池 ------------------------------------------------------------------

        /// <summary>
        /// 池の縁石と、手前の低い柵。
        /// 対岸に葦の株を箱で立てていたが、水の上に黒い塊が並んで、池ではなく花壇に見えたので外した
        /// </summary>
        static void ParkPondEdge(YardBanks y)
        {
            ParkCoping(y.Kerb, ParkRim(0f));
            ParkHoops(y.Iron);
        }

        /// <summary>
        /// 池の縁の点。上から見て左回り。
        ///
        /// 手前（+x）側だけ半径を狭めてあるのと、三つ山の揺らぎを重ねてあるのは、
        /// 真円の水たまりが人の掘った池に見えないため。<paramref name="grow"/> は外へ広げる量で、
        /// 岸の土と柵を同じ形から取るのに使う
        /// </summary>
        static Vector2[] ParkRim(float grow)
        {
            const int n = 24;
            var rim = new Vector2[n];
            for (var i = 0; i < n; i++) rim[i] = ParkRimAt(Mathf.PI * 2f * i / n, grow);
            return rim;
        }

        static Vector2 ParkRimAt(float a, float grow)
        {
            var cos = Mathf.Cos(a);
            var wave = 1f + 0.06f * Mathf.Sin(a * 3f);
            var rx = (cos >= 0f ? ParkPondNear : PondR) * wave + grow;
            var rz = ParkPondSide * wave + grow;
            return new Vector2(Pond.x + cos * rx, Pond.z + Mathf.Sin(a) * rz);
        }

        /// <summary>
        /// 池の縁石。
        ///
        /// 水の面だけでは地面に置いた板にしか見えないので、縁を一段上げて窪みに見せる。
        /// 一周させてあるのは、対岸の縁が切れていると水が地面へ流れ出して見えるため
        /// </summary>
        static void ParkCoping(Bank b, Vector2[] rim)
        {
            var mid = new Vector2(Pond.x, Pond.z);
            for (var i = 0; i < rim.Length; i++)
            {
                var p = rim[i];
                var q = rim[(i + 1) % rim.Length];
                var at = (p + q) * 0.5f;
                var run = q - p;
                var away = (at - mid).normalized * 0.17f;
                b.Box(new Vector3(at.x + away.x, 0.15f, at.y + away.y),
                    new Vector3(0.34f, 0.30f, run.magnitude + 0.06f),
                    Quaternion.LookRotation(new Vector3(run.x, 0f, run.y).normalized, Vector3.up));
            }
        }

        /// <summary>
        /// 池の低い柵。輪を伏せて並べたもの。
        ///
        /// 小径の側にだけ並べるのは、対岸の分は水面越しに縁石と重なって潰れるため。
        /// またげる高さの柵だが、池へは入らせないので、縁石の線に見えない仕切りを別に回す（<see cref="ParkFences"/>）
        /// </summary>
        static void ParkHoops(Bank b)
        {
            var from = -ParkHoopArc * Mathf.Deg2Rad;
            var span = ParkHoopArc * 2f * Mathf.Deg2Rad;
            var last = ParkRimAt(from, 0.55f);
            var run = 0f;
            for (var i = 1; i <= 150; i++)
            {
                var p = ParkRimAt(from + span * i / 150f, 0.55f);
                run += Vector2.Distance(last, p);
                // 0.62 m は輪の差し渡し。隣どうしが触れる間隔にすると柵の線が切れない
                if (run >= 0.62f)
                {
                    var tan = (p - last).normalized;
                    ParkHoop(b, new Vector3(p.x, 0f, p.y), new Vector3(tan.x, 0f, tan.y));
                    run = 0f;
                }
                last = p;
            }
        }

        static void ParkHoop(Bank b, Vector3 at, Vector3 along)
        {
            const float r = 0.30f;
            const int n = 7;
            // 輪の面の法線。真上を向く辺があるので、ここを上向きに取ると回しようが無くなる
            var side = Vector3.Cross(Vector3.up, along).normalized;
            for (var i = 0; i < n; i++)
            {
                var a0 = Mathf.PI * i / n;
                var a1 = Mathf.PI * (i + 1) / n;
                var p0 = at + along * (Mathf.Cos(a0) * r) + Vector3.up * (Mathf.Sin(a0) * r);
                var p1 = at + along * (Mathf.Cos(a1) * r) + Vector3.up * (Mathf.Sin(a1) * r);
                var run = p1 - p0;
                b.Box((p0 + p1) * 0.5f, new Vector3(0.04f, 0.04f, run.magnitude + 0.02f),
                    Quaternion.LookRotation(run.normalized, side));
            }
        }

        // ---- ベンチと街灯 ----------------------------------------------------------

        /// <summary>
        /// ベンチ 1 脚。木の座面と背の桟、鋳鉄の両端（脚・肘掛け・背の支え）、背の真ん中に小さな記念の銘板。
        /// 座った人は +z（門の側）を向く。
        ///
        /// 座面と背もたれを桟に分けてあるのは、板一枚のままだと木の色をした箱にしか見えず、
        /// 三脚並べても列として読めないため。桟の隙間に日が抜けて初めてベンチになる。
        /// ロンドンの公園のベンチは、背に亡くなった人の名を刻んだ金属の板を留めてある
        /// </summary>
        static void Bench(YardBanks y, Vector3 at)
        {
            var lean = Quaternion.Euler(-12f, 0f, 0f);
            for (var i = 0; i < 3; i++)
                y.Timber.Box(at + new Vector3(0f, 0.45f, -0.15f + i * 0.15f), new Vector3(1.80f, 0.05f, 0.12f));
            for (var i = 0; i < 3; i++)
                y.Timber.Box(at + new Vector3(0f, 0.60f + i * 0.14f, -0.25f - i * 0.03f), new Vector3(1.80f, 0.10f, 0.035f), lean);
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * 0.80f;
                y.Iron.Box(at + new Vector3(x, 0.21f, 0.14f), new Vector3(0.06f, 0.42f, 0.06f));
                y.Iron.Box(at + new Vector3(x, 0.21f, -0.20f), new Vector3(0.06f, 0.42f, 0.06f), Quaternion.Euler(10f, 0f, 0f));
                y.Iron.Box(at + new Vector3(x, 0.41f, -0.03f), new Vector3(0.06f, 0.05f, 0.44f));
                y.Iron.Box(at + new Vector3(x, 0.66f, -0.28f), new Vector3(0.06f, 0.52f, 0.05f), lean);
                y.Iron.Box(at + new Vector3(x, 0.64f, -0.02f), new Vector3(0.07f, 0.04f, 0.44f));
                y.Iron.Box(at + new Vector3(x, 0.54f, 0.17f), new Vector3(0.05f, 0.20f, 0.05f));
            }
            // 記念の銘板。背の真ん中の桟に小さな金属の板。
            // 真鍮の黄色で塗ると、目の前のベンチでは黄色い札が貼ってあるように浮いたので、鈍い金属の色にする
            y.Pole.Box(at + new Vector3(0f, 0.74f, -0.25f), new Vector3(0.15f, 0.045f, 0.012f), lean);
        }

        /// <summary>
        /// 街灯 1 本。ヴィクトリア朝の黒い鋳鉄の柱に、灯籠の形の笠。
        /// 台座・細い柱・梯子を掛ける腕・硝子の灯籠・屋根と頭の飾り。
        ///
        /// **夕方なので灯は入れない。** 笠と柱の形だけで読ませる。灯りを点けると、
        /// 記憶ごとに色味を寄せる Volume の前で光る点が三つ増えて、絵の明るさがそちらに引かれる
        /// </summary>
        static void ParkLamp(YardBanks y, Vector3 at)
        {
            var turn = Quaternion.Euler(0f, 45f, 0f);
            y.Iron.Box(at + new Vector3(0f, 0.24f, 0f), new Vector3(0.34f, 0.48f, 0.34f));
            y.Iron.Box(at + new Vector3(0f, 0.24f, 0f), new Vector3(0.34f, 0.48f, 0.34f), turn);
            y.Iron.Box(at + new Vector3(0f, 0.53f, 0f), new Vector3(0.24f, 0.10f, 0.24f));
            y.Iron.Box(at + new Vector3(0f, 1.90f, 0f), new Vector3(0.11f, 2.66f, 0.11f));
            y.Iron.Box(at + new Vector3(0f, 1.90f, 0f), new Vector3(0.11f, 2.66f, 0.11f), turn);
            y.Iron.Box(at + new Vector3(0f, 3.05f, 0f), new Vector3(0.60f, 0.04f, 0.04f));
            y.Iron.Box(at + new Vector3(0f, 3.26f, 0f), new Vector3(0.16f, 0.12f, 0.16f));
            // 灯籠。下が細く上が広い硝子を、四隅の枠で挟む
            y.Iron.Box(at + new Vector3(0f, 3.36f, 0f), new Vector3(0.22f, 0.05f, 0.22f));
            y.Glass.Box(at + new Vector3(0f, 3.62f, 0f), new Vector3(0.28f, 0.46f, 0.28f));
            for (var i = 0; i < 4; i++)
            {
                var cx = (i % 2 == 0 ? -1f : 1f) * 0.15f;
                var cz = (i / 2 == 0 ? -1f : 1f) * 0.15f;
                y.Iron.Box(at + new Vector3(cx, 3.62f, cz), new Vector3(0.03f, 0.50f, 0.03f));
            }
            y.Iron.Box(at + new Vector3(0f, 3.89f, 0f), new Vector3(0.42f, 0.05f, 0.42f));
            y.Iron.Box(at + new Vector3(0f, 3.97f, 0f), new Vector3(0.26f, 0.10f, 0.26f), turn);
            y.Iron.Box(at + new Vector3(0f, 4.08f, 0f), new Vector3(0.10f, 0.10f, 0.10f));
            y.Iron.Box(at + new Vector3(0f, 4.19f, 0f), new Vector3(0.04f, 0.14f, 0.04f));
        }

        // ---- 小物と植え込み --------------------------------------------------------

        /// <summary>
        /// 植え込み・ごみ箱・水飲み場・掲示板。
        /// どれも人の歩く筋（門からベンチまでの x -1.3〜1.0、プリヤが芝生を横切る線）から外してある
        /// </summary>
        static void ParkThings(YardBanks y)
        {
            // 低い生け垣。ベンチの背の後ろと、門からの小径の東
            ParkHedge(y, new Vector3(-0.6f, 0f, -1.5f), new Vector3(6.7f, 0f, -1.5f));
            ParkHedge(y, new Vector3(1.95f, 0f, 3.7f), new Vector3(1.95f, 0f, 8.2f));
            // 柵の内側の植え込みの塊。南の隅と、西の柵沿い
            ParkShrub(y, new Vector3(-11.8f, 0f, -10.9f), 1.6f, 1);
            ParkShrub(y, new Vector3(11.8f, 0f, -10.9f), 1.5f, 2);
            ParkShrub(y, new Vector3(-12.0f, 0f, 7.9f), 1.4f, 3);
            ParkShrub(y, new Vector3(-12.1f, 0f, -4.0f), 1.2f, 4);

            // ごみ箱。ベンチの列の東の端と、門の内の東
            ParkBin(y, new Vector3(6.95f, 0f, 0.15f));
            ParkBin(y, new Vector3(1.95f, 0f, 8.75f));
            // 水飲み場。公園と学校の庭にしか無い物なので、一つあるだけで場所が絞れる
            ParkFount(y, new Vector3(2.15f, 0f, 2.6f));
            // 掲示板。門を入った東に、公園の側を向けて。決まりの札を貼ってある
            const float bx = 3.3f;
            const float bz = 8.75f;
            for (var i = 0; i < 2; i++)
                y.Iron.Box(new Vector3(bx - 0.55f + i * 1.1f, 0.75f, bz), new Vector3(0.07f, 1.50f, 0.07f));
            y.Iron.Box(new Vector3(bx, 1.30f, bz), new Vector3(1.26f, 0.78f, 0.05f));
            y.Line.Box(new Vector3(bx, 1.30f, bz - 0.03f), new Vector3(1.14f, 0.66f, 0.02f));
            for (var i = 0; i < 5; i++)
                y.Iron.Box(new Vector3(bx - 0.08f, 1.52f - i * 0.1f, bz - 0.045f), new Vector3(0.84f - (i % 2) * 0.2f, 0.03f, 0.01f));
            y.Iron.Box(new Vector3(bx, 1.73f, bz), new Vector3(1.34f, 0.06f, 0.12f));
        }

        /// <summary>
        /// 低い植え込みの列。
        ///
        /// 株を 0.8 m 間隔で並べて小径の縁をなぞる。等間隔の繰り返しが、
        /// 舗装の帯だけのときより強く「歩く筋」を示す。
        /// 色は芝の緑（<see cref="YardBanks.Grass"/>）。常緑の植え込みの暗い緑では、日の当たらない面が黒い箱に見えた。
        /// またげる高さなので当たりは入れない。引っ掛かる隅を増やすほうが困る
        /// </summary>
        static void ParkHedge(YardBanks y, Vector3 from, Vector3 to)
        {
            var run = to - from;
            var span = run.magnitude;
            if (span < 0.01f) return;
            var dir = run / span;
            var turn = Quaternion.LookRotation(dir, Vector3.up);
            var n = Mathf.Max(1, Mathf.RoundToInt(span / 0.8f));
            for (var i = 0; i <= n; i++)
            {
                var at = from + dir * (span * i / n);
                // 株ごとに背を振る。同じ高さで揃えると生垣ではなく低い塀に見える
                var tall = 0.44f + Mathf.Repeat(i * 0.618f, 1f) * 0.16f;
                y.Grass.Box(at + new Vector3(0f, tall * 0.5f, 0f), new Vector3(0.62f, tall, 0.78f), turn);
                y.Grass.Box(at + new Vector3(0f, tall + 0.08f, 0f), new Vector3(0.48f, 0.20f, 0.62f), turn);
            }
        }

        /// <summary>常緑の植え込みの塊。向きを変えた箱を三つ重ねて、角を丸く見せる</summary>
        static void ParkShrub(YardBanks y, Vector3 at, float size, int seed)
        {
            for (var k = 0; k < 3; k++)
            {
                var w = size * (1f - k * 0.18f);
                var h = size * (0.55f + k * 0.12f);
                y.Leaf.Box(at + new Vector3((k - 1) * 0.12f * size, h * 0.5f, ((k + seed) % 2 - 0.5f) * 0.2f * size),
                    new Vector3(w, h, w * 0.9f), Quaternion.Euler(0f, seed * 23f + k * 31f, 0f));
            }
        }

        /// <summary>ごみ箱。黒い鋳物の胴に金の帯、少し張り出した蓋</summary>
        static void ParkBin(YardBanks y, Vector3 at)
        {
            y.Iron.Box(at + new Vector3(0f, 0.05f, 0f), new Vector3(0.44f, 0.10f, 0.44f));
            y.Iron.Box(at + new Vector3(0f, 0.44f, 0f), new Vector3(0.40f, 0.70f, 0.40f));
            y.Iron.Box(at + new Vector3(0f, 0.44f, 0f), new Vector3(0.40f, 0.70f, 0.40f), Quaternion.Euler(0f, 45f, 0f));
            y.Yellow.Box(at + new Vector3(0f, 0.70f, 0f), new Vector3(0.42f, 0.04f, 0.42f), Quaternion.Euler(0f, 22.5f, 0f));
            y.Iron.Box(at + new Vector3(0f, 0.84f, 0f), new Vector3(0.48f, 0.06f, 0.48f));
            y.Iron.Box(at + new Vector3(0f, 0.92f, 0f), new Vector3(0.30f, 0.10f, 0.30f), Quaternion.Euler(0f, 45f, 0f));
        }

        /// <summary>水飲み場。花崗岩の台に、鉄の鉢と蛇口</summary>
        static void ParkFount(YardBanks y, Vector3 at)
        {
            y.Kerb.Box(at + new Vector3(0f, 0.04f, 0f), new Vector3(0.72f, 0.08f, 0.72f));
            y.Kerb.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.82f, 0.34f));
            y.Kerb.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.82f, 0.34f), Quaternion.Euler(0f, 45f, 0f));
            y.Iron.Box(at + new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.12f, 0.46f));
            y.Iron.Box(at + new Vector3(0f, 1.00f, -0.10f), new Vector3(0.06f, 0.14f, 0.06f));
        }

        /// <summary>
        /// プラタナス。柵の内側に六本。3 月なので葉は無く、枝ぶりが空に透ける（<see cref="YardPlane"/>）。
        /// 人の歩く筋と池から外し、門の側の二本は門を挟む形にする
        /// </summary>
        static void ParkTrees(YardBanks y)
        {
            YardPlane(y, new Vector3(-7.4f, 0f, 8.0f), 14f, 31);
            YardPlane(y, new Vector3(-11.6f, 0f, 2.6f), 16f, 37);
            YardPlane(y, new Vector3(-5.2f, 0f, -5.8f), 15f, 41);
            YardPlane(y, new Vector3(5.4f, 0f, -6.0f), 15.5f, 43);
            YardPlane(y, new Vector3(11.6f, 0f, -3.2f), 16f, 47);
            YardPlane(y, new Vector3(11.4f, 0f, 5.6f), 14f, 53);
        }

        // ---- 当たり ----------------------------------------------------------------

        /// <summary>
        /// 見えない当たり。柵の四辺（門と通り抜けの口は開ける）、門の外の歩道の縁、池の縁。
        ///
        /// **門の外の歩道までは歩ける。** 門は開いているので、出たところで止めると見えない壁に当たる。
        /// 歩道の先（縁石の線）と、柵の角の延長で止める。歩道の当たりの床も置く（地面の mesh は柵の内側だけ）
        /// </summary>
        static void ParkFences(Transform place)
        {
            const float high = 1.6f;
            const float thick = 0.3f;
            var gateOpen0 = GatePostWest + GatePost * 0.5f;
            var gateOpen1 = GatePostEast - GatePost * 0.5f;
            const float c = thick * 0.5f;
            ParkWall(place, "ParkFenceN0", new Vector3(ParkWest - c, 0f, GateZ), new Vector3(gateOpen0, 0f, GateZ), high, thick);
            ParkWall(place, "ParkFenceN1", new Vector3(gateOpen1, 0f, GateZ), new Vector3(SideGateX - SideGateHalf, 0f, GateZ), high, thick);
            ParkWall(place, "ParkFenceN2", new Vector3(SideGateX + SideGateHalf, 0f, GateZ), new Vector3(ParkEast + c, 0f, GateZ), high, thick);
            ParkWall(place, "ParkFenceE", new Vector3(ParkEast, 0f, ParkSouth - c), new Vector3(ParkEast, 0f, GateZ), high, thick);
            ParkWall(place, "ParkFenceW", new Vector3(ParkWest, 0f, ParkSouth - c), new Vector3(ParkWest, 0f, GateZ), high, thick);
            ParkWall(place, "ParkFenceS", new Vector3(ParkWest - c, 0f, ParkSouth), new Vector3(ParkEast + c, 0f, ParkSouth), high, thick);
            // 門の外の歩道。縁石の線と、両端
            ParkWall(place, "ParkFenceKerb", new Vector3(ParkWest, 0f, ParkKerbZ), new Vector3(ParkEast, 0f, ParkKerbZ), high, thick);
            ParkWall(place, "ParkFencePaveW", new Vector3(ParkWest, 0f, GateZ), new Vector3(ParkWest, 0f, ParkKerbZ), high, thick);
            ParkWall(place, "ParkFencePaveE", new Vector3(ParkEast, 0f, GateZ), new Vector3(ParkEast, 0f, ParkKerbZ), high, thick);
            // 門柱。柵の当たりより高く、頭の玉まで。人の脇に浮く板（HoloPanel）は当たりを見て柱を避けるので、
            // 柵の高さで切ると、柱の上半分の裏へ板が回り込んで字が柱に隠れる
            foreach (var x in new[] { GatePostWest, GatePostEast })
                Fence(place, x < 0f ? "ParkPostW" : "ParkPostE", new Vector3(x, 1.25f, GateZ),
                    new Vector3(GatePost + 0.08f, 2.5f, GatePost + 0.08f));
            var floor = new GameObject("ParkPaveFloor");
            floor.transform.SetParent(place, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, (GateZ + ParkKerbZ) * 0.5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(ParkEast - ParkWest, 0.1f, ParkKerbZ - GateZ + 0.4f);

            // 池の縁。縁石の外の面に沿って一周。膝の高さなので、人を選ぶ目の線は遮らない
            var rim = ParkRim(0.36f);
            for (var i = 0; i < rim.Length; i++)
            {
                var p = rim[i];
                var q = rim[(i + 1) % rim.Length];
                ParkWall(place, "ParkFencePond" + i, new Vector3(p.x, 0f, p.y), new Vector3(q.x, 0f, q.y), 0.6f, 0.12f);
            }
        }

        /// <summary>a から b へ伸びる見えない壁。高さと厚み</summary>
        static void ParkWall(Transform place, string name, Vector3 a, Vector3 b, float high, float thick)
        {
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.01f) return;
            var go = new GameObject(name);
            go.transform.SetParent(place, false);
            go.transform.localPosition = (a + b) * 0.5f + Vector3.up * (high * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(thick, high, len);
        }

        // ---- 地面の絵 --------------------------------------------------------------

        /// <summary>地面の絵の置き場と、描き方の版。版を変えると次の組み直しで描き直す</summary>
        const string ParkGroundPath = DiveTextures + "ParkGround.png";
        const string ParkGroundSign = "ground2|512";
        const int ParkGroundSize = 512;

        // 地面の色。sRGB。どれも実物より暗く置く（Tone と同じ考え）
        static readonly Color ParkGrassDark = new Color(0.150f, 0.205f, 0.095f);
        static readonly Color ParkGrassLight = new Color(0.225f, 0.275f, 0.120f);
        static readonly Color ParkSoilCol = new Color(0.265f, 0.205f, 0.145f);
        static readonly Color ParkLitterCol = new Color(0.360f, 0.240f, 0.120f);
        static readonly Color ParkLitterDark = new Color(0.270f, 0.170f, 0.090f);

        /// <summary>
        /// 地面のマテリアル。芝と土と落ち葉を一枚の絵で塗り分けて貼る（<see cref="ParkGroundPicture"/>）。
        ///
        /// **色ごとの面を重ねない。** 前は芝の上に土の短冊を 1 cm 浮かせて重ねていたが、
        /// 縁が定規で引いた線になり、踏まれて禿げた所に見えなかった。絵なら縁を滲ませられて、マテリアルも一枚で済む
        /// </summary>
        static Material ParkGroundMat()
        {
            var m = ParkMat("ParkGround", Color.white, 0.04f);
            m.SetTexture("_BaseMap", ParkGroundPicture());
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 柵の内側の地面の絵。512 画素四方で、柵の西から東（26 m）と南から門（21.3 m）を覆う。
        /// 柵や小径の寸法を変えたら <see cref="ParkGroundSign"/> の版を上げる。上げないと前の絵のまま貼られる。
        ///
        /// 地は芝。明るさを大きな斑と細かな斑で揺らす。そこへ、踏まれて禿げた土
        /// （小径の縁・ベンチの前・プリヤが芝生を横切る筋・生け垣の根元・池の岸）と、
        /// 冬のあいだに吹き溜まった落ち葉（木の下と柵の根元）を重ねる。
        /// 描いた絵は取り込みの userData に版を持ち、版が同じなら描き直さない（空の絵と同じ作り）
        /// </summary>
        static Texture2D ParkGroundPicture()
        {
            var importer = AssetImporter.GetAtPath(ParkGroundPath) as TextureImporter;
            if (importer != null && importer.userData == ParkGroundSign && System.IO.File.Exists(ParkGroundPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(ParkGroundPath);

            const int n = ParkGroundSize;
            var px = new Color32[n * n];
            for (var j = 0; j < n; j++)
            {
                var z = Mathf.Lerp(ParkSouth, GateZ, (j + 0.5f) / n);
                for (var i = 0; i < n; i++)
                {
                    var x = Mathf.Lerp(ParkWest, ParkEast, (i + 0.5f) / n);
                    var c = ParkGroundAt(new Vector2(x, z));
                    px[j * n + i] = new Color32(Byte(c.r), Byte(c.g), Byte(c.b), 255);
                }
            }
            var pic = new Texture2D(n, n, TextureFormat.RGB24, false, false);
            pic.SetPixels32(px);
            pic.Apply();
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            System.IO.File.WriteAllBytes(ParkGroundPath, pic.EncodeToPNG());
            Object.DestroyImmediate(pic);
            AssetDatabase.ImportAsset(ParkGroundPath);
            importer = AssetImporter.GetAtPath(ParkGroundPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = n;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.userData = ParkGroundSign;
                importer.SaveAndReimport();
            }
            Debug.Log("公園の地面の絵を描いた: " + ParkGroundPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ParkGroundPath);
        }

        /// <summary>地面の一点の色。sRGB</summary>
        static Color ParkGroundAt(Vector2 p)
        {
            var big = SkyPaint.Noise(new Vector3(p.x * 0.32f, 0.3f, p.y * 0.32f), 3);
            var fine = SkyPaint.Noise(new Vector3(p.x * 2.4f, 1.7f, p.y * 2.4f), 5);
            var grain = SkyPaint.Noise(new Vector3(p.x * 8.5f, 4.1f, p.y * 8.5f), 9);
            var c = Color.Lerp(ParkGrassDark, ParkGrassLight, Mathf.Clamp01(big * 0.85f + fine * 0.3f - 0.1f));
            c *= 0.93f + grain * 0.14f;

            // 踏まれて禿げた土。縁は細かな斑で揺らして滲ませる
            var wear = 0f;
            var edge = ParkPathGap(p);
            wear = Mathf.Max(wear, 1f - SkyPaint.Smooth(0f, 0.30f + fine * 0.25f, edge));
            // ベンチの前。足を置く所
            if (p.y > CrossNorth && p.y < 1.7f && p.x > -1.1f && p.x < 6.3f)
                wear = Mathf.Max(wear, (1f - SkyPaint.Smooth(CrossNorth, 1.6f, p.y)) * (0.45f + fine * 0.5f));
            // プリヤが通りから芝生を横切る筋。通り抜けの口ほど濃い
            var line = ParkSegGap(p, new Vector2(SideGateX, GateZ), new Vector2(6.4f, 2.4f));
            var along = Mathf.InverseLerp(2.4f, GateZ, p.y);
            wear = Mathf.Max(wear, (1f - SkyPaint.Smooth(0.12f, 0.45f + fine * 0.2f, line)) * (0.35f + along * 0.5f));
            // 生け垣と植え込みの根元
            wear = Mathf.Max(wear, 1f - SkyPaint.Smooth(0.25f, 0.6f, ParkSegGap(p, new Vector2(-0.6f, -1.5f), new Vector2(6.7f, -1.5f))));
            wear = Mathf.Max(wear, 1f - SkyPaint.Smooth(0.25f, 0.6f, ParkSegGap(p, new Vector2(1.95f, 3.7f), new Vector2(1.95f, 8.2f))));
            // 池の岸
            var shore = ParkPondGap(p);
            wear = Mathf.Max(wear, 1f - SkyPaint.Smooth(0.1f, 0.8f + fine * 0.4f, shore));
            c = Color.Lerp(c, ParkSoilCol * (0.9f + grain * 0.2f), Mathf.Clamp01(wear));

            // 落ち葉。木の下と、柵の根元の吹き溜まり
            var leaves = 0f;
            foreach (var t in ParkTreeFeet)
            {
                var d = Vector2.Distance(p, t);
                leaves = Mathf.Max(leaves, (1f - SkyPaint.Smooth(1.2f, 3.4f + big * 1.5f, d)) * 0.85f);
            }
            var rail = Mathf.Min(Mathf.Min(p.x - ParkWest, ParkEast - p.x), Mathf.Min(p.y - ParkSouth, GateZ - p.y));
            leaves = Mathf.Max(leaves, (1f - SkyPaint.Smooth(0.15f, 0.9f + fine * 0.6f, rail)) * 0.9f);
            // 小径の上には残らない
            if (edge <= 0f) leaves = 0f;
            leaves *= SkyPaint.Smooth(0.35f, 0.65f, fine * 0.6f + grain * 0.5f);
            var leaf = Color.Lerp(ParkLitterDark, ParkLitterCol, grain);
            c = Color.Lerp(c, leaf, Mathf.Clamp01(leaves));
            c.a = 1f;
            return c;
        }

        /// <summary>地面の絵の落ち葉の中心。<see cref="ParkTrees"/> と同じ根元</summary>
        static readonly Vector2[] ParkTreeFeet =
        {
            new Vector2(-7.4f, 8.0f), new Vector2(-11.6f, 2.6f), new Vector2(-5.2f, -5.8f),
            new Vector2(5.4f, -6.0f), new Vector2(11.6f, -3.2f), new Vector2(11.4f, 5.6f),
        };

        /// <summary>一番近い小径の縁までの距離。小径の上なら 0 以下</summary>
        static float ParkPathGap(Vector2 p)
        {
            var best = float.MaxValue;
            best = Mathf.Min(best, ParkRectGap(p, WalkWest, WalkEast, CrossNorth, GateZ + 0.16f));
            best = Mathf.Min(best, ParkRectGap(p, -CrossEnd, CrossEnd, CrossSouth, CrossNorth));
            best = Mathf.Min(best, ParkRectGap(p, -CrossEnd, -CrossEnd + LoopWide, LoopNorth, CrossSouth));
            best = Mathf.Min(best, ParkRectGap(p, CrossEnd - LoopWide, CrossEnd, LoopNorth, CrossSouth));
            best = Mathf.Min(best, ParkRectGap(p, -CrossEnd, CrossEnd, LoopNorth - LoopWide, LoopNorth));
            best = Mathf.Min(best, ParkRectGap(p, LinkWest, LinkEast, LoopNorth, CrossSouth));
            best = Mathf.Min(best, ParkRectGap(p, -2.3f, WalkWest, 2.2f, 7.2f));
            return best;
        }

        static float ParkRectGap(Vector2 p, float x0, float x1, float z0, float z1)
        {
            var dx = Mathf.Max(x0 - p.x, p.x - x1);
            var dz = Mathf.Max(z0 - p.y, p.y - z1);
            if (dx <= 0f && dz <= 0f) return Mathf.Max(dx, dz);
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
        }

        static float ParkSegGap(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>池の縁石の外の面から外への距離。内なら負</summary>
        static float ParkPondGap(Vector2 p)
        {
            var rel = p - new Vector2(Pond.x, Pond.z);
            var a = Mathf.Atan2(rel.y, rel.x);
            var edge = ParkRimAt(a, 0.34f) - new Vector2(Pond.x, Pond.z);
            return rel.magnitude - edge.magnitude;
        }

        /// <summary>
        /// プラタナスの樹冠の札のマテリアル。絵は公営住宅の敷地の木と同じ（<see cref="YardCrownPicture"/>）で、色だけ沈める。
        ///
        /// 札は灯りを受けない（<c>HalfAware/Backdrop</c>）。公園では木が大きく、しかも門の側の低い日を背にして
        /// 空に透けて見えることが多いので、絵の色のままだと逆光の枝が明るい段ボールの切り抜きに見える。
        /// 日陰の幹と同じくらいまで暗くして、空を背にした枝ぶりの影として読ませる
        /// </summary>
        static Material ParkCrownMat()
        {
            const string path = Materials + "ParkCrown.mat";
            var shader = Shader.Find("HalfAware/Backdrop");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "ParkCrown";
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", YardCrownPicture());
            m.SetColor("_BaseColor", new Color(0.58f, 0.56f, 0.55f, 1f));
            m.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 公園にだけ要る色。
        ///
        /// <see cref="Tone"/> の表はどの場所からも見るので、そこを明るくすると五つの場所が
        /// 揃って明るくなる。小径と地面のように公園でしか使わない色はここに持つ
        /// </summary>
        static Material ParkMat(string name, Color col, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
