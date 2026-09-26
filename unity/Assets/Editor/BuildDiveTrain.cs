using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の電車（設計書 9.1 節「train（電車）」「電車の作り込み」）。
    /// ロンドンの地上を走る通勤の電車の一両。3 月の 18:20、日の入りの直後の夕暮れ。車内は白い蛍光灯で、窓の外より明るい。
    ///
    /// **一目で分かる形は三つ。窓の列・黄色い縦の手すりの列・両開きのドア。**
    /// 等間隔に並ぶものとして、1.8 m ごとの窓（<see cref="CarBayZ"/>）と、座席の端ごとの黄色い縦の手すり、
    /// 天井の照明の帯、窓の上の広告の額を置く。小物（優先席の札・非常通報の装置・防犯カメラの半球・路線図の帯と行き先の表示）はそのあと。
    /// 網棚と吊り革は置かない。日本の電車に固有の物（吊り広告など）も置かない。
    ///
    /// **向き。** 場所のローカルで +x が西（日の沈んだ側）、-x がドアの壁で東。+z と -z の妻面の先に隣の車両が続く。
    ///
    /// **公営住宅と同じ三段で距離を作る。** ただし窓の外は走って流れるので、三段とも撮って貼った帯を
    /// <see cref="WindowStream"/> で層ごとに違う速さで送る（<c>BuildDiveTrainFar.cs</c>）。
    /// <list type="table">
    /// <item><term>車内（このファイル）</term><description>この車両と、貫通の戸の先の隣の車両</description></item>
    /// <item><term>近く・中・遠く</term><description>線路脇の柵と架線の柱／煉瓦の長屋の裏と倉庫／高層棟とシティの灯り。後ろに夕暮れの空</description></item>
    /// </list>
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 両方から見る点は const にして揃える
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 電車 ------------------------------------------------------------

        /// <summary>車両の半幅</summary>
        const float CarHalf = 1.3f;
        /// <summary>車両の半分の長さ。一両ぶんで 8 m</summary>
        const float CarLong = 4f;
        /// <summary>天井</summary>
        const float CarHigh = 2.3f;
        /// <summary>
        /// 天井の横の手すりの高さと、中心からの寄り。座席の前の縁の真上を通す。
        /// **目より高く通す。** 低いと、立っている人の目の前を横切って視界を塞ぐ
        /// </summary>
        public const float CeilRailY = 1.98f;
        public const float CeilRailX = 0.70f;
        /// <summary>座席の座面</summary>
        public const float SeatY = 0.42f;
        /// <summary>座席の中心の x。両側にある</summary>
        public const float SeatX = 1.02f;

        // ---- 側壁の並び --------------------------------------------------------

        /// <summary>
        /// 開口の間隔。窓もドアも同じ間隔で並べるので、片側の二つがドアに替わっても
        /// 等間隔の列は途切れない。**この繰り返しだけで車両に見える**
        /// </summary>
        const float CarBayPitch = 1.8f;

        /// <summary>開口の中心の z。四つ並べて、両端に妻面までの余地を 0.6 m 残す</summary>
        static readonly float[] CarBayZ =
        {
            -1.5f * CarBayPitch, -0.5f * CarBayPitch, 0.5f * CarBayPitch, 1.5f * CarBayPitch,
        };

        /// <summary>
        /// ドアに替わる開口の番号。両端の二つだけがドアになる。
        /// 片側だけにしたのは、両側をドアにすると窓の列が二つずつに切れて、
        /// 等間隔に並ぶという手掛かりがどちらの壁にも残らないため
        /// </summary>
        static readonly int[] CarDoorBay = { 0, 3 };

        /// <summary>
        /// ドアのある壁。-x にしたのは、記憶 5 も 12 も「駅。ドアが開く」の鍵打ちで
        /// -x の壁へ向き直るため。逆にすると、その節で二人ともただの窓を見ていることになる
        /// </summary>
        const float CarDoorSide = -1f;

        /// <summary>窓の開口。半幅と、下端・上端</summary>
        const float CarWinHalf = 0.71f;
        const float CarWinLow = 1.00f;
        const float CarWinTop = 1.80f;

        /// <summary>ドアの開口。半幅と上端。二枚組なので、一枚の幅はちょうど半幅になる</summary>
        const float CarDoorHalf = 0.625f;
        const float CarDoorTop = 1.95f;

        /// <summary>
        /// 窓の上の斜めの面（広告の額を並べる所）。側壁の上の縁と、天井へ繋がる縁。
        /// 側壁の縁はドアの上枠（2.05）より上に取る。下げると上枠が斜めの面に食い込む
        /// </summary>
        const float CoveWallY = 2.06f;
        const float CoveRoofX = 0.95f;

        /// <summary>座席の端の仕切り板の z。ドアの無い壁は通しの長椅子、ドアの壁はそこで切れる</summary>
        static readonly float[] CarSplitLong = { -3.55f, 3.55f };
        static readonly float[] CarSplitShort = { -2.02f, 2.02f };

        /// <summary>肘掛けの z。仕切り板ほど高くない横木で、長椅子の途中を区切る。縦の手すりもここに立てる</summary>
        static readonly float[] CarRestLong = { -1.75f, 0f, 1.75f };
        static readonly float[] CarRestShort = { -1f, 1f };

        /// <summary>貫通路。妻面の開口の半幅と上端、隣の車両の妻面までの長さ</summary>
        const float GangHalf = 0.45f;
        const float GangTop = 1.95f;
        const float GangLong = 0.7f;

        /// <summary>素材ごとの入れ物。教室・台所と同じく束ねて持ち回る</summary>
        sealed class TrainBanks
        {
            // 当たりを入れる面
            /// <summary>床。滑り止めの粒の絵を貼る</summary>
            public readonly Bank Deck = new Bank { Texel = 1f / TrainFloorTile };
            /// <summary>側壁・妻面・座席の下の箱・仕切り板</summary>
            public readonly Bank Shell = new Bank { Texel = 0.4f };
            // 当たりを入れない面
            /// <summary>天井・窓の上の斜めの面・窓の枠・ドアの板・貫通の戸。側壁と同じ白っぽい灰色</summary>
            public readonly Bank Trim = new Bank { Texel = 0.4f };
            /// <summary>座席。柄物のモケットの絵を貼る</summary>
            public readonly Bank Seat = new Bank { Texel = 1f / TrainMoquetteTile };
            /// <summary>黄色。縦と横の手すり・ドアの縁・床の線・路線図の線</summary>
            public readonly Bank Yellow = new Bank { Texel = 0.5f };
            /// <summary>金物。手すりの受け・貫通路の踏み板・戸の押し板</summary>
            public readonly Bank Metal = new Bank { Texel = 0.5f };
            /// <summary>黒。ドアの戸当たりのゴム・窓の縁・貫通路の幌・防犯カメラ・表示の箱・広告の字</summary>
            public readonly Bank Dark = new Bank { Texel = 0.5f };
            /// <summary>紙。広告の地・路線図の帯・札</summary>
            public readonly Bank Paper = new Bank { Texel = 0.5f };
            public readonly Bank Red = new Bank { Texel = 0.5f };
            public readonly Bank Blue = new Bank { Texel = 0.5f };
            public readonly Bank Moss = new Bank { Texel = 0.5f };
            /// <summary>自分で光る面。天井の照明の帯</summary>
            public readonly Bank Glow = new Bank { Texel = 0.5f };
            /// <summary>行き先の表示の橙の字</summary>
            public readonly Bank Sign = new Bank { Texel = 0.5f };
            /// <summary>ドアを開けるボタンの緑の輪</summary>
            public readonly Bank Button = new Bank { Texel = 0.5f };
        }

        // ---- 組み立て --------------------------------------------------------------

        /// <summary>記憶 5・12 の舞台</summary>
        static void Train(Transform place)
        {
            var b = new TrainBanks();
            TrainShell(b);
            TrainCove(b);
            TrainCeiling(b);
            TrainSeat(b);
            TrainRail(b);
            TrainDoors(b);
            TrainNotices(b);
            TrainGangway(b);

            var skin = TrainSkinMat();
            EstateEmit(place, "TrainFloor", b.Deck, TrainDeckMat(), true);
            EstateEmit(place, "TrainShell", b.Shell, skin, true);
            EstateEmit(place, "TrainTrim", b.Trim, skin, false);
            EstateEmit(place, "TrainSeat", b.Seat, TrainSeatMat(), false);
            EstateEmit(place, "TrainYellow", b.Yellow, TrainYellowMat(), false);
            EstateEmit(place, "TrainMetal", b.Metal, Mat("Rail"), false);
            EstateEmit(place, "TrainDark", b.Dark, Mat("Ceiling"), false);
            EstateEmit(place, "TrainPaper", b.Paper, KitchenShared("EstatePaper"), false);
            EstateEmit(place, "TrainRed", b.Red, KitchenShared("EstateRed"), false);
            EstateEmit(place, "TrainBlue", b.Blue, KitchenShared("EstateBlue"), false);
            EstateEmit(place, "TrainMoss", b.Moss, KitchenShared("EstateMoss"), false);
            NoShadow(EstateEmit(place, "TrainGlow", b.Glow, TrainTubeMat(), false));
            NoShadow(EstateEmit(place, "TrainSign", b.Sign, Glow("GlowTrainSign", new Color(1f, 0.55f, 0.12f), 1.3f), false));
            NoShadow(EstateEmit(place, "TrainButton", b.Button, Glow("GlowTrainButton", new Color(0.35f, 1f, 0.45f), 1.1f), false));

            TrainNextCars(place);
            TrainBlocks(place);
            TrainLight(place);
            // 窓の外の三つの層と夕暮れの空（BuildDiveTrainFar.cs）
            TrainOutside(place);
        }

        // ---- 殻 ------------------------------------------------------------------

        /// <summary>
        /// 床・側壁・妻面と、座席の下の箱・仕切り板。歩いて越えられない面はここへまとめる。
        /// 妻面は貫通路の開口を抜く
        /// </summary>
        static void TrainShell(TrainBanks b)
        {
            b.Deck.FaceY(0f, -CarHalf, CarHalf, -CarLong, CarLong, 1);

            b.Shell.FaceXHoles(-CarHalf, -CarLong, CarLong, 0f, CoveWallY, 1, CarHoles(-1f));
            b.Shell.FaceXHoles(CarHalf, -CarLong, CarLong, 0f, CoveWallY, -1, CarHoles(1f));
            var gang = new List<Vector4> { new Vector4(-GangHalf, GangHalf, 0f, GangTop) };
            b.Shell.FaceZHoles(-CarLong, -CarHalf, CarHalf, 0f, CarHigh, 1, gang);
            b.Shell.FaceZHoles(CarLong, -CarHalf, CarHalf, 0f, CarHigh, -1, gang);

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                // 座席の下の箱。ここが抜けていると長椅子が宙に浮いて見える
                b.Shell.Box(new Vector3(side * (SeatX + 0.06f), 0.185f, 0f), new Vector3(0.44f, 0.37f, to - from));
                // 仕切り板。上の半分は透かしの板の縁だけ
                var splits = CarDoorWall(side) ? CarSplitShort : CarSplitLong;
                for (var i = 0; i < splits.Length; i++)
                    b.Shell.Box(new Vector3(side * SeatX, 0.55f, splits[i]), new Vector3(0.56f, 1.10f, 0.05f));
            }
            // 幅木。床と壁の境を黒い帯で押さえる。座席の下の箱の前にも
            b.Dark.FaceX(-CarHalf + 0.004f, -CarLong, CarLong, 0f, 0.07f, 1);
            b.Dark.FaceX(CarHalf - 0.004f, -CarLong, CarLong, 0f, 0.07f, -1);
            // 座席の下の箱の足元の黒い帯
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                var fx = side * (SeatX + 0.06f - 0.22f);
                b.Dark.FaceX(fx - side * 0.004f, from, to, 0f, 0.06f, side < 0f ? 1 : -1);
            }
        }

        /// <summary>ドアのある壁か。<c>side</c> は壁の x の符号</summary>
        static bool CarDoorWall(float side) { return side * CarDoorSide > 0f; }

        /// <summary>i 番の開口がドアか</summary>
        static bool CarIsDoor(float side, int i)
        {
            return CarDoorWall(side) && System.Array.IndexOf(CarDoorBay, i) >= 0;
        }

        /// <summary>
        /// 側壁の抜き。ドアの壁だけ両端が床まで抜ける。
        /// <see cref="Bank.FaceXHoles"/> の並びは (z0, z1, y0, y1)
        /// </summary>
        static List<Vector4> CarHoles(float side)
        {
            var made = new List<Vector4>();
            for (var i = 0; i < CarBayZ.Length; i++)
            {
                var z = CarBayZ[i];
                if (CarIsDoor(side, i))
                    made.Add(new Vector4(z - CarDoorHalf, z + CarDoorHalf, 0f, CarDoorTop));
                else
                    made.Add(new Vector4(z - CarWinHalf, z + CarWinHalf, CarWinLow, CarWinTop));
            }
            return made;
        }

        /// <summary>
        /// 長椅子の一続き。ドアの無い壁は妻面から妻面まで通し、
        /// ドアの壁は開口の手前で切る。肘掛けもこの長さに従う
        /// </summary>
        static void CarSeatRun(float side, out float from, out float to)
        {
            to = CarDoorWall(side) ? 1.98f : 3.5f;
            from = -to;
        }

        // ---- 窓の上 ----------------------------------------------------------------

        /// <summary>
        /// 窓の上の斜めの面と、そこに並べる広告の額。ドアの上は額の代わりに路線図の帯と行き先の表示。
        /// **額は等間隔に並べる。** 窓の列と同じく、繰り返しが車内だと言う。
        /// 中身は地の色・写真の四角・字の線だけで、読める字は入れない
        /// </summary>
        static void TrainCove(TrainBanks b)
        {
            var rnd = new System.Random(1820);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 斜めの面そのもの
                CoveRect(b.Trim, side, 0f, -CarLong, CarLong, 0f, 1f);
                // 窓の枠の上の縁と、斜めの面の下の見切り
                b.Trim.Box(new Vector3(side * (CarHalf - 0.02f), CoveWallY - 0.02f, 0f), new Vector3(0.04f, 0.04f, CarLong * 2f));

                if (CarDoorWall(side))
                {
                    // ドアの間の四枚
                    for (var k = 0; k < 4; k++)
                        TrainAd(b, side, -1.425f + k * 0.95f, 0.85f, rnd);
                    // ドアの外の端に一枚ずつ
                    TrainAd(b, side, -3.72f, 0.46f, rnd);
                    TrainAd(b, side, 3.72f, 0.46f, rnd);
                    for (var i = 0; i < CarDoorBay.Length; i++)
                        TrainDoorHead(b, side, CarBayZ[CarDoorBay[i]]);
                }
                else
                {
                    // 窓の列に揃えて八枚
                    for (var k = 0; k < 8; k++)
                        TrainAd(b, side, -3.5f + k * 1.0f, 0.88f, rnd);
                }
            }
        }

        /// <summary>
        /// 斜めの面の上の四角。<paramref name="s0"/>・<paramref name="s1"/> は壁の縁（0）から天井の縁（1）までの割合、
        /// <paramref name="lift"/> は面から車内の側へ浮かせる量
        /// </summary>
        static void CoveRect(Bank bank, float side, float lift, float z0, float z1, float s0, float s1)
        {
            var low = new Vector3(side * CarHalf, CoveWallY, 0f);
            var high = new Vector3(side * CoveRoofX, CarHigh, 0f);
            var up = (high - low).normalized;
            // 面の法線。車内の側（中心と下）を向く
            var n = new Vector3(-side * up.y, -Mathf.Abs(up.x), 0f).normalized;
            var p0 = Vector3.Lerp(low, high, s0) + n * lift;
            var p1 = Vector3.Lerp(low, high, s1) + n * lift;
            var mid = (p0 + p1) * 0.5f;
            mid.z = (z0 + z1) * 0.5f;
            TrainFace(bank, mid, n, up, z1 - z0, (p1 - p0).magnitude);
        }

        /// <summary>
        /// 向きを持つ四角を一枚。<paramref name="n"/> は表の向き、<paramref name="up"/> は面の上。
        /// 隅は <see cref="Bank.Quad"/> の決まり（表から見て右下・左下・左上・右上）で渡す
        /// </summary>
        static void TrainFace(Bank bank, Vector3 c, Vector3 n, Vector3 up, float w, float h)
        {
            var right = Vector3.Cross(up, -n).normalized * (w * 0.5f);
            var u = up.normalized * (h * 0.5f);
            bank.Quad(c + right - u, c - right - u, c - right + u, c + right + u);
        }

        /// <summary>広告の額を一枚。灰色の枠、地の色、写真の四角、字の線</summary>
        static void TrainAd(TrainBanks b, float side, float z, float wide, System.Random rnd)
        {
            var h = wide * 0.5f;
            CoveRect(b.Dark, side, 0.004f, z - h, z + h, 0.10f, 0.92f);
            var kind = rnd.Next(0, 4);
            var ground = kind == 0 ? b.Red : kind == 1 ? b.Blue : kind == 2 ? b.Moss : b.Paper;
            CoveRect(ground, side, 0.007f, z - h + 0.02f, z + h - 0.02f, 0.16f, 0.86f);
            // 写真の四角。地と違う色で、片側に寄せる
            var photo = kind == 3 ? b.Blue : b.Paper;
            var left = rnd.NextDouble() < 0.5;
            var pz0 = left ? z - h + 0.05f : z + h * 0.1f;
            var pz1 = left ? z - h * 0.1f : z + h - 0.05f;
            CoveRect(photo, side, 0.010f, pz0, pz1, 0.24f, 0.78f);
            // 字の線。写真の反対側に三本
            var tz0 = left ? z + h * 0.02f : z - h + 0.06f;
            var tz1 = left ? z + h - 0.06f : z - h * 0.02f;
            var ink = kind == 3 ? b.Dark : b.Paper;
            for (var k = 0; k < 3; k++)
            {
                var s = 0.62f - k * 0.14f;
                var cut = k == 2 ? 0.5f : 1f;
                CoveRect(ink, side, 0.010f, tz0, Mathf.Lerp(tz0, tz1, cut), s, s + 0.06f);
            }
        }

        /// <summary>
        /// ドアの上。下に路線図の帯（白い地に黄色の線と駅の刻み、今いる駅に赤い点）、
        /// 上に行き先の表示（黒い箱に橙の字）
        /// </summary>
        static void TrainDoorHead(TrainBanks b, float side, float z)
        {
            const float half = 0.66f;
            CoveRect(b.Paper, side, 0.005f, z - half, z + half, 0.06f, 0.42f);
            CoveRect(b.Yellow, side, 0.008f, z - half + 0.05f, z + half - 0.05f, 0.22f, 0.27f);
            for (var k = 0; k < 11; k++)
            {
                var tz = z - half + 0.07f + k * (half * 2f - 0.14f) / 10f;
                CoveRect(k == 6 ? b.Red : b.Dark, side, 0.010f, tz - 0.012f, tz + 0.012f, k == 6 ? 0.17f : 0.20f, k == 6 ? 0.32f : 0.29f);
            }
            // 駅の名前の代わりの細い線
            for (var k = 0; k < 11; k += 2)
            {
                var tz = z - half + 0.07f + k * (half * 2f - 0.14f) / 10f;
                CoveRect(b.Dark, side, 0.010f, tz - 0.035f, tz + 0.035f, 0.32f, 0.35f);
            }
            // 行き先の表示
            CoveRect(b.Dark, side, 0.006f, z - 0.42f, z + 0.42f, 0.50f, 0.92f);
            var rnd = new System.Random(Mathf.RoundToInt(z * 100f) + 7);
            var at = z - 0.36f;
            while (at < z + 0.34f)
            {
                var len = 0.03f + (float)rnd.NextDouble() * 0.09f;
                CoveRect(b.Sign, side, 0.010f, at, Mathf.Min(at + len, z + 0.36f), 0.63f, 0.79f);
                at += len + 0.025f + (rnd.NextDouble() < 0.25 ? 0.05f : 0f);
            }
        }

        // ---- 天井 ------------------------------------------------------------------

        /// <summary>
        /// 天井と照明の帯。帯は開口ごとに一枚、両側に。**照明も等間隔に並べる**。
        /// 間に吹き出し口の細い溝、真ん中に防犯カメラの半球を二つ
        /// </summary>
        static void TrainCeiling(TrainBanks b)
        {
            b.Trim.FaceY(CarHigh, -CoveRoofX, CoveRoofX, -CarLong, CarLong, -1);
            for (var s = 0; s < 2; s++)
            {
                var x = (s == 0 ? -1f : 1f) * 0.44f;
                for (var i = 0; i < CarBayZ.Length; i++)
                {
                    var z = CarBayZ[i];
                    // 枠を一段下げ、その下に光る面
                    b.Trim.Box(new Vector3(x, CarHigh - 0.02f, z), new Vector3(0.26f, 0.04f, 1.56f));
                    b.Glow.FaceY(CarHigh - 0.041f, x - 0.10f, x + 0.10f, z - 0.74f, z + 0.74f, -1);
                }
                // 吹き出し口
                b.Dark.FaceY(CarHigh - 0.002f, (s == 0 ? -1f : 1f) * 0.70f - 0.02f, (s == 0 ? -1f : 1f) * 0.70f + 0.02f, -CarLong + 0.3f, CarLong - 0.3f, -1);
            }
            // 防犯カメラの半球。台座と、向きを変えた箱を重ねて丸める
            for (var k = 0; k < 2; k++)
            {
                var at = new Vector3(0f, CarHigh, k == 0 ? -1.8f : 1.8f);
                b.Trim.Box(at + Vector3.down * 0.01f, new Vector3(0.16f, 0.02f, 0.16f));
                for (var r = 0; r < 3; r++)
                {
                    var w = 0.12f - r * 0.03f;
                    var y = 0.035f + r * 0.022f;
                    for (var q = 0; q < 2; q++)
                        b.Dark.Box(at + Vector3.down * y, new Vector3(w, 0.024f, w), Quaternion.Euler(0f, q * 45f, 0f));
                }
            }
        }

        // ---- 座席 ------------------------------------------------------------------

        /// <summary>
        /// 座席。座面と背もたれにモケットの絵を貼る。
        /// 背は窓の下端で止める。上へ伸ばすと窓の列が座席に食われて短く見える
        /// </summary>
        static void TrainSeat(TrainBanks b)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                var len = to - from;
                b.Seat.Box(new Vector3(side * SeatX, SeatY, 0f), new Vector3(0.56f, 0.10f, len));
                b.Seat.Box(new Vector3(side * (SeatX + 0.23f), 0.72f, 0f), new Vector3(0.09f, 0.54f, len));
            }
        }

        /// <summary>
        /// 手すり。座席の端ごとの黄色い縦の手すり（床から天井まで）、天井に沿った横の手すり二列、肘掛け。
        /// **縦の手すりは座席の端と肘掛けの所に立てる**。座席の切れ目と手すりが同じ所に来て、列が等間隔に揃う
        /// </summary>
        static void TrainRail(TrainBanks b)
        {
            const float pole = 0.036f;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * CeilRailX;
                var splits = CarDoorWall(side) ? CarSplitShort : CarSplitLong;
                var rests = CarDoorWall(side) ? CarRestShort : CarRestLong;
                var stands = new List<float>(splits);
                stands.AddRange(rests);
                foreach (var z in stands)
                {
                    b.Yellow.Box(new Vector3(x, CarHigh * 0.5f, z), new Vector3(pole, CarHigh, pole));
                    b.Metal.Box(new Vector3(x, 0.02f, z), new Vector3(0.08f, 0.04f, 0.08f));
                    b.Metal.Box(new Vector3(x, CarHigh - 0.02f, z), new Vector3(0.08f, 0.04f, 0.08f));
                }
                // 仕切り板の上から縦の手すりまでの横木
                foreach (var z in splits)
                    b.Yellow.Box(new Vector3(side * (SeatX - 0.02f + CeilRailX) * 0.5f + side * 0.02f, 1.10f, z),
                        new Vector3(Mathf.Abs(SeatX - CeilRailX) + 0.3f, 0.03f, 0.03f));

                // 天井の横の手すり。吊り具を 1.2 m ごと
                b.Yellow.Box(new Vector3(x, CeilRailY, 0f), new Vector3(0.032f, 0.032f, CarLong * 2f - 0.6f));
                for (var z = -CarLong + 0.5f; z < CarLong - 0.3f; z += 1.2f)
                    b.Metal.Box(new Vector3(x, (CeilRailY + CarHigh) * 0.5f, z), new Vector3(0.02f, CarHigh - CeilRailY, 0.02f));

                // 肘掛け。通路側の端を座面まで下ろす。宙に浮いた横木は肘掛けに見えない
                foreach (var z in rests)
                {
                    b.Metal.Box(new Vector3(side * SeatX, 0.66f, z), new Vector3(0.50f, 0.05f, 0.06f));
                    b.Metal.Box(new Vector3(side * (SeatX - 0.23f), 0.56f, z), new Vector3(0.05f, 0.22f, 0.05f));
                }
            }
        }

        // ---- ドア ------------------------------------------------------------------

        /// <summary>
        /// 両開きのドア。窓の枠と、ドアの板・縁の黄色・戸当たりのゴム・開けるボタン・床の黄色い線。
        /// 窓の開口の枠もここで組む。**硝子は入れない**（公営住宅・台所・教室と同じ）。窓の外の層をそのまま通す
        /// </summary>
        static void TrainDoors(TrainBanks b)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var i = 0; i < CarBayZ.Length; i++)
                {
                    var z = CarBayZ[i];
                    if (CarIsDoor(side, i)) continue;
                    // 窓の枠。灰色の縁が一段手前に出て、角に黒いゴム
                    var wx = side * (CarHalf - 0.035f);
                    b.Trim.Box(new Vector3(wx, CarWinLow - 0.035f, z), new Vector3(0.07f, 0.07f, CarWinHalf * 2f + 0.14f));
                    b.Trim.Box(new Vector3(wx, CarWinTop + 0.035f, z), new Vector3(0.07f, 0.07f, CarWinHalf * 2f + 0.14f));
                    for (var e = 0; e < 2; e++)
                    {
                        var jz = z + (e == 0 ? -1f : 1f) * (CarWinHalf + 0.035f);
                        b.Trim.Box(new Vector3(wx, (CarWinLow + CarWinTop) * 0.5f, jz), new Vector3(0.07f, CarWinTop - CarWinLow + 0.14f, 0.07f));
                    }
                    // 窓台。窓の下の縁を奥行きのある台にする
                    b.Trim.Box(new Vector3(side * (CarHalf - 0.06f), CarWinLow - 0.005f, z), new Vector3(0.12f, 0.01f, CarWinHalf * 2f));
                    // 真ん中の縦の桟。二枚の硝子の合わせ目
                    b.Dark.Box(new Vector3(side * (CarHalf + 0.01f), (CarWinLow + CarWinTop) * 0.5f, z), new Vector3(0.03f, CarWinTop - CarWinLow, 0.03f));
                }
            }

            const float leaf = 0.05f;
            var dx = CarDoorSide * (CarHalf - 0.045f);
            // 板の車内の面。ドアの壁は -x なので +x を向く
            var face = dx - CarDoorSide * leaf * 0.5f;
            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                // 開口の縁。両脇と上を黄色に
                var fx = CarDoorSide * (CarHalf - 0.05f);
                for (var e = 0; e < 2; e++)
                {
                    var jz = z + (e == 0 ? -1f : 1f) * (CarDoorHalf + 0.05f);
                    b.Yellow.Box(new Vector3(fx, CarDoorTop * 0.5f, jz), new Vector3(0.09f, CarDoorTop, 0.10f));
                }
                b.Yellow.Box(new Vector3(fx, CarDoorTop + 0.05f, z), new Vector3(0.09f, 0.10f, CarDoorHalf * 2f + 0.20f));
                // 二枚の板。窓を残して組む
                for (var k = 0; k < 2; k++)
                {
                    var sign = k == 0 ? -1f : 1f;
                    var lz = z + sign * CarDoorHalf * 0.5f;
                    b.Trim.Box(new Vector3(dx, 0.50f, lz), new Vector3(leaf, 1.00f, CarDoorHalf));
                    b.Trim.Box(new Vector3(dx, 1.865f, lz), new Vector3(leaf, 0.17f, CarDoorHalf));
                    for (var e = 0; e < 2; e++)
                    {
                        var sz = lz + (e == 0 ? -1f : 1f) * (CarDoorHalf - 0.09f) * 0.5f;
                        b.Trim.Box(new Vector3(dx, 1.39f, sz), new Vector3(leaf, 0.78f, 0.09f));
                    }
                    // 窓の縁のゴム
                    b.Dark.Box(new Vector3(face - CarDoorSide * 0.002f, 1.005f, lz), new Vector3(0.006f, 0.02f, CarDoorHalf - 0.16f));
                    b.Dark.Box(new Vector3(face - CarDoorSide * 0.002f, 1.775f, lz), new Vector3(0.006f, 0.02f, CarDoorHalf - 0.16f));
                    // 板の縁の黄色。戸先の反対（戸袋の側）と上
                    var outer = lz + sign * (CarDoorHalf * 0.5f - 0.02f);
                    b.Yellow.Box(new Vector3(face - CarDoorSide * 0.003f, CarDoorTop * 0.5f, outer), new Vector3(0.006f, CarDoorTop - 0.04f, 0.04f));
                    b.Yellow.Box(new Vector3(face - CarDoorSide * 0.003f, CarDoorTop - 0.03f, lz), new Vector3(0.006f, 0.04f, CarDoorHalf - 0.02f));
                    // 開けるボタン。戸先の寄りの腰の高さに、黒い台と緑の輪
                    var bz = z + sign * 0.13f;
                    b.Dark.Box(new Vector3(face - CarDoorSide * 0.006f, 1.12f, bz), new Vector3(0.012f, 0.09f, 0.09f));
                    b.Button.FaceX(face - CarDoorSide * 0.0125f, bz - 0.028f, bz + 0.028f, 1.092f, 1.148f, CarDoorSide < 0f ? 1 : -1);
                    b.Dark.Box(new Vector3(face - CarDoorSide * 0.014f, 1.12f, bz), new Vector3(0.004f, 0.034f, 0.034f));
                }
                // 戸当たりのゴム。二枚の合わせ目の黒い縦の帯
                b.Dark.Box(new Vector3(face - CarDoorSide * 0.004f, CarDoorTop * 0.5f, z), new Vector3(0.01f, CarDoorTop, 0.035f));
                // 床の黄色い線。開口の手前に、床から僅かに浮かせる（同じ高さに置くと二枚が取り合ってちらつく）
                b.Yellow.FaceY(0.004f, Mathf.Min(CarDoorSide * 0.86f, CarDoorSide * 0.98f),
                    Mathf.Max(CarDoorSide * 0.86f, CarDoorSide * 0.98f),
                    z - CarDoorHalf - 0.10f, z + CarDoorHalf + 0.10f, 1);
                // 敷居。金物の踏み板
                b.Metal.FaceY(0.006f, CarDoorSide * CarHalf < 0f ? -CarHalf : CarHalf - 0.12f,
                    CarDoorSide * CarHalf < 0f ? -CarHalf + 0.12f : CarHalf, z - CarDoorHalf, z + CarDoorHalf, 1);
            }
        }

        // ---- 札と装置 ----------------------------------------------------------------

        /// <summary>
        /// 優先席の札（ドアの脇の窓との間の柱に、青い札）と、非常通報の装置（ドアの外の端の柱に、黄色い縁の赤い箱）
        /// </summary>
        static void TrainNotices(TrainBanks b)
        {
            var wall = CarDoorSide * CarHalf;
            var n = CarDoorSide < 0f ? 1 : -1;
            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                var toward = z < 0f ? 1f : -1f;
                // 優先席の札。ドアと窓の間の柱
                var pz = z + toward * 0.84f;
                b.Blue.FaceX(wall - CarDoorSide * 0.006f, pz - 0.10f, pz + 0.10f, 1.30f, 1.56f, n);
                b.Paper.FaceX(wall - CarDoorSide * 0.008f, pz - 0.035f, pz + 0.035f, 1.42f, 1.50f, n);
                b.Paper.FaceX(wall - CarDoorSide * 0.008f, pz - 0.07f, pz + 0.07f, 1.34f, 1.37f, n);
                // 反対の壁の、同じ z の座席の上にも。ドアの前に座る人の向かい
                b.Blue.FaceX(-wall + CarDoorSide * 0.006f, pz - 0.10f, pz + 0.10f, 1.86f, 2.02f, -n);
                b.Paper.FaceX(-wall + CarDoorSide * 0.008f, pz - 0.035f, pz + 0.035f, 1.92f, 1.98f, -n);

                // 非常通報の装置。ドアの外の端の柱
                var az = z - toward * 0.95f;
                b.Yellow.Box(new Vector3(wall - CarDoorSide * 0.008f, 1.45f, az), new Vector3(0.016f, 0.24f, 0.18f));
                b.Red.Box(new Vector3(wall - CarDoorSide * 0.02f, 1.45f, az), new Vector3(0.02f, 0.18f, 0.12f));
                b.Dark.Box(new Vector3(wall - CarDoorSide * 0.035f, 1.43f, az), new Vector3(0.02f, 0.03f, 0.08f));
                b.Paper.FaceX(wall - CarDoorSide * 0.031f, az - 0.04f, az + 0.04f, 1.50f, 1.52f, n);
            }
        }

        // ---- 連結部 ------------------------------------------------------------------

        /// <summary>
        /// 両端の貫通路。妻面の開口に硝子の大きな窓の付いた貫通の戸を閉めて立て、
        /// その先に幌の通路と、隣の車両の妻面を置く。**通路の先に隣の車両が続くと、車内に奥行きが出る**
        /// </summary>
        static void TrainGangway(TrainBanks b)
        {
            for (var e = 0; e < 2; e++)
            {
                var sz = e == 0 ? -1f : 1f;
                var wall = sz * CarLong;
                // 開口の枠
                for (var k = 0; k < 2; k++)
                    b.Trim.Box(new Vector3((k == 0 ? -1f : 1f) * (GangHalf + 0.04f), GangTop * 0.5f, wall - sz * 0.02f), new Vector3(0.08f, GangTop, 0.06f));
                b.Trim.Box(new Vector3(0f, GangTop + 0.04f, wall - sz * 0.02f), new Vector3(GangHalf * 2f + 0.16f, 0.08f, 0.06f));
                // 貫通の戸。大きな窓を残して組み、押し板を付ける
                var dz = wall + sz * 0.02f;
                b.Trim.Box(new Vector3(0f, 0.475f, dz), new Vector3(GangHalf * 2f, 0.95f, 0.04f));
                b.Trim.Box(new Vector3(0f, 1.875f, dz), new Vector3(GangHalf * 2f, 0.15f, 0.04f));
                for (var k = 0; k < 2; k++)
                    b.Trim.Box(new Vector3((k == 0 ? -1f : 1f) * (GangHalf - 0.06f), 1.375f, dz), new Vector3(0.12f, 0.85f, 0.04f));
                b.Dark.Box(new Vector3(0f, 0.955f, dz - sz * 0.021f), new Vector3(GangHalf * 2f - 0.24f, 0.012f, 0.004f));
                b.Metal.Box(new Vector3(0.22f, 0.72f, dz - sz * 0.025f), new Vector3(0.10f, 0.28f, 0.01f));
                // 幌の通路。黒い蛇腹の襞を壁と天井に
                var z0 = wall + sz * 0.04f;
                var z1 = wall + sz * GangLong;
                b.Metal.FaceY(0.005f, -GangHalf, GangHalf, Mathf.Min(z0, z1), Mathf.Max(z0, z1), 1);
                b.Dark.FaceX(-GangHalf - 0.06f, Mathf.Min(z0, z1), Mathf.Max(z0, z1), 0f, GangTop + 0.1f, 1);
                b.Dark.FaceX(GangHalf + 0.06f, Mathf.Min(z0, z1), Mathf.Max(z0, z1), 0f, GangTop + 0.1f, -1);
                b.Dark.FaceY(GangTop + 0.1f, -GangHalf - 0.06f, GangHalf + 0.06f, Mathf.Min(z0, z1), Mathf.Max(z0, z1), -1);
                for (var k = 1; k < 6; k++)
                {
                    var fz = Mathf.Lerp(z0, z1, k / 6f);
                    b.Dark.Box(new Vector3(-GangHalf - 0.03f, (GangTop + 0.1f) * 0.5f, fz), new Vector3(0.06f, GangTop + 0.1f, 0.03f));
                    b.Dark.Box(new Vector3(GangHalf + 0.03f, (GangTop + 0.1f) * 0.5f, fz), new Vector3(0.06f, GangTop + 0.1f, 0.03f));
                    b.Dark.Box(new Vector3(0f, GangTop + 0.07f, fz), new Vector3(GangHalf * 2f + 0.12f, 0.06f, 0.03f));
                }
            }
        }

        /// <summary>隣の車両の妻面の z。貫通路の先</summary>
        const float NextCarStart = CarLong + GangLong;
        /// <summary>隣の車両の長さ。こちらと同じ</summary>
        const float NextCarLength = CarLong * 2f;

        /// <summary>
        /// 隣の車両。貫通の戸の窓越しと、窓の外の斜めにしか見えないので、床・壁・天井・長椅子・縦の手すり・照明の帯だけにする。
        /// +z の側に一両組み、-z の側へは同じ mesh を 180 度回して置く（mesh は一揃いで足りる）。
        /// 灯りを一つずつ持たせ、こちらの車両には届かない範囲に留める（URP が一つの物に当てる追加の灯りは 4 つまで）
        /// </summary>
        static void TrainNextCars(Transform place)
        {
            var fore = Child(place, "NextCarFore");
            fore.localPosition = new Vector3(0f, 0f, NextCarStart);
            var deck = new Bank { Texel = 1f / TrainFloorTile };
            var shell = new Bank { Texel = 0.4f };
            var seat = new Bank { Texel = 1f / TrainMoquetteTile };
            var yellow = new Bank { Texel = 0.5f };
            var glow = new Bank { Texel = 0.5f };
            const float len = NextCarLength;
            const float mid = len * 0.5f;
            deck.FaceY(0f, -CarHalf, CarHalf, 0f, len, 1);
            var holes = new List<Vector4>();
            for (var i = 0; i < CarBayZ.Length; i++)
                holes.Add(new Vector4(mid + CarBayZ[i] - CarWinHalf, mid + CarBayZ[i] + CarWinHalf, CarWinLow, CarWinTop));
            shell.FaceXHoles(-CarHalf, 0f, len, 0f, CoveWallY, 1, holes);
            shell.FaceXHoles(CarHalf, 0f, len, 0f, CoveWallY, -1, holes);
            shell.FaceZHoles(0f, -CarHalf, CarHalf, 0f, CarHigh, 1, new List<Vector4> { new Vector4(-GangHalf, GangHalf, 0f, GangTop) });
            shell.FaceZ(len, -CarHalf, CarHalf, 0f, CarHigh, -1);
            // 奥の妻面の貫通の戸。枠が一段手前に出ていると、そこで車両が切れて次へ続くと読める
            shell.Box(new Vector3(0f, GangTop * 0.5f, len - 0.02f), new Vector3(GangHalf * 2f + 0.16f, GangTop + 0.08f, 0.04f));
            shell.FaceY(CarHigh, -CarHalf, CarHalf, 0f, len, -1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 窓の上の斜めの面の代わりに、段を一つ
                shell.Box(new Vector3(side * (CarHalf - 0.17f), CarHigh - 0.12f, mid), new Vector3(0.34f, 0.24f, len));
                shell.Box(new Vector3(side * (SeatX + 0.06f), 0.185f, mid), new Vector3(0.44f, 0.37f, len - 1.0f));
                seat.Box(new Vector3(side * SeatX, SeatY, mid), new Vector3(0.56f, 0.10f, len - 1.0f));
                seat.Box(new Vector3(side * (SeatX + 0.23f), 0.72f, mid), new Vector3(0.09f, 0.54f, len - 1.0f));
                for (var k = 0; k < 5; k++)
                    yellow.Box(new Vector3(side * CeilRailX, CarHigh * 0.5f, 0.5f + k * (len - 1f) / 4f), new Vector3(0.036f, CarHigh, 0.036f));
                yellow.Box(new Vector3(side * CeilRailX, CeilRailY, mid), new Vector3(0.032f, 0.032f, len - 0.6f));
                for (var i = 0; i < CarBayZ.Length; i++)
                    glow.FaceY(CarHigh - 0.01f, side * 0.44f - 0.10f, side * 0.44f + 0.10f, mid + CarBayZ[i] - 0.74f, mid + CarBayZ[i] + 0.74f, -1);
            }
            EstateEmit(fore, "NextFloor", deck, TrainDeckMat(), false);
            EstateEmit(fore, "NextShell", shell, TrainSkinMat(), false);
            EstateEmit(fore, "NextSeat", seat, TrainSeatMat(), false);
            EstateEmit(fore, "NextYellow", yellow, TrainYellowMat(), false);
            NoShadow(EstateEmit(fore, "NextGlow", glow, TrainTubeMat(), false));
            // 灯りは二つ。一つでは奥の妻面まで届かず、貫通の戸の窓越しに奥が黒く抜けた
            Lamp(fore, "NextLamp0", LightType.Point, new Vector3(0f, CarHigh - 0.3f, 2.6f), Vector3.zero,
                new Color(0.92f, 0.95f, 1f), 2.8f, 2.9f);
            Lamp(fore, "NextLamp1", LightType.Point, new Vector3(0f, CarHigh - 0.3f, 6.0f), Vector3.zero,
                new Color(0.92f, 0.95f, 1f), 2.8f, 3.6f);

            var aft = Object.Instantiate(fore.gameObject, place).transform;
            aft.name = "NextCarAft";
            aft.localPosition = new Vector3(0f, 0f, -NextCarStart);
            aft.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // ---- 当たり ----------------------------------------------------------------

        /// <summary>
        /// ドアと貫通路の開口、長椅子。板は当たりを持たない形として組むので、開口は <see cref="Fence"/> で塞ぐ。
        /// 長椅子は歩いて通り抜けられる高さなので、座面ごと塞ぐ
        /// </summary>
        static void TrainBlocks(Transform place)
        {
            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                Fence(place, "TrainDoorStop" + i,
                    new Vector3(CarDoorSide * (CarHalf - 0.02f), CarDoorTop * 0.5f, z),
                    new Vector3(0.14f, CarDoorTop, CarDoorHalf * 2f));
            }
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                Fence(place, "TrainSeatStop" + s, new Vector3(side * (SeatX + 0.01f), 0.60f, 0f),
                    new Vector3(0.60f, 1.20f, to - from));
            }
            for (var e = 0; e < 2; e++)
                Fence(place, "TrainGangStop" + e, new Vector3(0f, GangTop * 0.5f, (e == 0 ? -1f : 1f) * (CarLong + 0.02f)),
                    new Vector3(GangHalf * 2f + 0.1f, GangTop, 0.1f));
        }

        // ---- 灯り ------------------------------------------------------------------

        /// <summary>
        /// 車内の灯り。**窓の外より明るい白い蛍光灯。**
        /// 8 m の車両を真ん中の一灯で照らすと両端が闇に落ちるので、灯りを四つに分けて端まで床が拾えるところまで上げる。
        ///
        /// 四つに留めたのは、URP が一つの物に当てる追加の灯りを 4 つまでにしているため。
        /// 五つ目からは、見る向きによって当たる灯りが入れ替わって明るさが跳ねる。
        /// 日は沈んでいるので、影を落とす日は置かない。夕暮れの空の光は環境光（<see cref="TrainPlaceSky"/>）が受け持つ
        /// </summary>
        static void TrainLight(Transform place)
        {
            for (var i = 0; i < 4; i++)
            {
                Lamp(place, "Fluorescent" + i, LightType.Point,
                    new Vector3(0f, CarHigh - 0.28f, (i * 2f) - 3f), Vector3.zero,
                    new Color(0.93f, 0.96f, 1f), 2.7f, 7.0f);
            }
        }

        // ---- 地の色 ------------------------------------------------------------------

        /// <summary>側壁と天井。白っぽい灰色の化粧板。蛍光灯の下で窓の外より明るく見える地</summary>
        static Material TrainSkinMat()
        {
            return TrainPaint("TrainSkin", new Color(0.500f, 0.505f, 0.500f), 0.22f, null);
        }

        /// <summary>手すりとドアの縁。艶のある黄色</summary>
        static Material TrainYellowMat()
        {
            return TrainPaint("TrainYellow", new Color(0.780f, 0.600f, 0.080f), 0.55f, null);
        }

        static Material TrainDeckMat()
        {
            return TrainPaint("TrainDeck", Color.white, 0.18f, TrainPicture(TrainFloorPath, TrainFloorSign, TrainFloorAt));
        }

        static Material TrainSeatMat()
        {
            return TrainPaint("TrainSeat", Color.white, 0.05f, TrainPicture(TrainMoquettePath, TrainMoquetteSign, TrainMoquetteAt));
        }

        static Material TrainTubeMat()
        {
            return Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f);
        }

        /// <summary>車内の地の色。<see cref="Mat"/> の表はどこも暗がりを前提にしていて、蛍光灯の下の車内には暗すぎる</summary>
        static Material TrainPaint(string name, Color col, float smooth, Texture2D tex)
        {
            var m = EstatePaint(name, col, smooth);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 床とモケットの絵 ----------------------------------------------------------

        /// <summary>床の絵の一枚が覆う長さ。m</summary>
        const float TrainFloorTile = 0.40f;
        const string TrainFloorPath = DiveTextures + "TrainFloor.png";
        const string TrainFloorSign = "floor1|256";
        /// <summary>座席の絵の一枚が覆う長さ。m</summary>
        const float TrainMoquetteTile = 0.24f;
        const string TrainMoquettePath = DiveTextures + "TrainMoquette.png";
        const string TrainMoquetteSign = "moquette2|256";
        const int TrainPictureSize = 256;

        /// <summary>
        /// 繰り返して貼る絵を描く。<paramref name="at"/> は一枚の中の位置（0〜1）の色（sRGB）。
        /// 取り込みの userData に版を持ち、版が同じなら描き直さない（公園の地面の絵と同じ作り）
        /// </summary>
        static Texture2D TrainPicture(string path, string sign, System.Func<float, float, Color> at)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.userData == sign && System.IO.File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int n = TrainPictureSize;
            var px = new Color32[n * n];
            for (var j = 0; j < n; j++)
                for (var i = 0; i < n; i++)
                {
                    var c = at((i + 0.5f) / n, (j + 0.5f) / n);
                    px[j * n + i] = new Color32(Byte(c.r), Byte(c.g), Byte(c.b), 255);
                }
            var pic = new Texture2D(n, n, TextureFormat.RGB24, false, false);
            pic.SetPixels32(px);
            pic.Apply();
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            System.IO.File.WriteAllBytes(path, pic.EncodeToPNG());
            Object.DestroyImmediate(pic);
            AssetDatabase.ImportAsset(path);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = n;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.userData = sign;
                importer.SaveAndReimport();
            }
            Debug.Log("電車の絵を描いた: " + path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// 床。灰青のゴムの床に、6 cm ごとの丸い粒の滑り止めと、明暗の細かな斑。
        /// 粒は升目を半分ずらした並び。遠くでは均されて、一枚の灰色になる
        /// </summary>
        static Color TrainFloorAt(float u, float v)
        {
            var baseCol = new Color(0.300f, 0.310f, 0.320f);
            var fleck = SkyPaint.Noise(new Vector3(u * 64f, 3.1f, v * 64f), 11);
            var c = baseCol * (0.90f + fleck * 0.2f);
            // 粒。0.4 m に 6.5 個ずつではなく、繰り返しが切れないように 7 個ずつ並べる
            const int count = 7;
            var gu = u * count;
            var gv = v * count;
            var row = Mathf.FloorToInt(gv);
            if (row % 2 == 1) gu += 0.5f;
            var cu = gu - Mathf.Floor(gu) - 0.5f;
            var cv = gv - Mathf.Floor(gv) - 0.5f;
            var r = Mathf.Sqrt(cu * cu + cv * cv);
            // 粒の上は明るく、縁は一段暗い
            if (r < 0.24f) c = Color.Lerp(c, baseCol * 1.22f, 1f - SkyPaint.Smooth(0.16f, 0.24f, r));
            else if (r < 0.31f) c *= 0.86f;
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// 座席のモケット。濃い紺の地に、橙と水色と白の短い四角を段違いに並べた柄。
        /// ロンドンの地上線の座席の色の取り合わせ
        /// </summary>
        static Color TrainMoquetteAt(float u, float v)
        {
            var navy = new Color(0.075f, 0.105f, 0.230f);
            var orange = new Color(0.640f, 0.300f, 0.080f);
            var sky = new Color(0.220f, 0.380f, 0.580f);
            var white = new Color(0.560f, 0.570f, 0.590f);
            var weave = SkyPaint.Noise(new Vector3(u * 90f, 1.3f, v * 90f), 5);
            var c = navy * (0.88f + weave * 0.24f);
            // 四角の升目。一枚に 5 × 5、段ごとに半分ずらす
            const int count = 5;
            var gv = v * count;
            var row = Mathf.FloorToInt(gv);
            var gu = u * count + (row % 2) * 0.5f;
            var col = Mathf.FloorToInt(gu);
            var fu = gu - col;
            var fv = gv - row;
            var pick = ((col * 7 + row * 3) % 5 + 5) % 5;
            if (fu > 0.15f && fu < 0.75f && fv > 0.30f && fv < 0.55f)
                c = pick == 0 || pick == 3 ? orange : pick == 1 ? sky : pick == 2 ? white : c;
            // 小さな点
            if (fu > 0.82f && fu < 0.92f && fv > 0.72f && fv < 0.84f) c = orange * 0.9f;
            c.a = 1f;
            return c;
        }
    }
}
