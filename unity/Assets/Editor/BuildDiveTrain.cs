using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の電車。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// 等間隔に並ぶものを一つ入れると、その場所らしさがいちばん早く伝わる。
    ///
    /// 電車で等間隔に並ぶのは開口（<see cref="CarBayZ"/>）と吊り革で、この二つと
    /// ドアの二枚組が、車内だと分かる手掛かりのほとんどを持っている。
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
        /// 吊り革の棒の高さと、中心からの寄り。
        /// **棒は目より高く通す。** 1.95 に下げていたときは、輪がちょうど 1.58 の目の前に垂れて、
        /// 掴まっている人の視界を黒い四角が半分塞いでいた
        /// </summary>
        public const float StrapY = 2.05f;
        public const float StrapX = 0.55f;
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
        /// ドアのある壁。-x にしたのは、記憶 4 も 11 も「駅。ドアが開く」の鍵打ちで
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

        /// <summary>荷棚の板の高さ。窓の上端のすぐ上を通す</summary>
        const float CarRackY = 1.90f;

        /// <summary>吊り革の間隔</summary>
        const float CarStrapGap = 0.70f;

        /// <summary>座席の端の仕切り板の z。ドアの無い壁は通しの長椅子、ドアの壁はそこで切れる</summary>
        static readonly float[] CarSplitLong = { -3.55f, 3.55f };
        static readonly float[] CarSplitShort = { -2.02f, 2.02f };

        /// <summary>肘掛けの z。仕切り板ほど高くない横木で、長椅子の途中を区切る</summary>
        static readonly float[] CarRestLong = { -1.75f, 0f, 1.75f };
        static readonly float[] CarRestShort = { -1f, 1f };

        // ---- 電車 --------------------------------------------------------------

        /// <summary>
        /// 記憶 4・11 の舞台。一両ぶんの箱に、窓の列・吊り革の列・ドアの二枚組。
        /// 窓の外は帯のテクスチャで済ませ、流すのは <see cref="WindowStream"/> が受け持つ
        /// </summary>
        static void Train(Transform place)
        {
            // 車内は蛍光灯の下なので、Tone の表の暗がり前提の色では暗すぎる
            var skin = CarCoat("TrainSkin", new Color(0.315f, 0.322f, 0.332f), 0.12f);
            var deck = CarCoat("TrainDeck", new Color(0.392f, 0.382f, 0.360f), 0.16f);
            var mark = CarCoat("TrainLine", new Color(0.560f, 0.452f, 0.105f), 0.18f);

            TrainDeck(place, deck, mark);
            TrainShell(place, skin);
            TrainFit(place, skin);
            TrainSeat(place);
            TrainRail(place);
            TrainOutside(place);
            TrainLight(place);
        }

        /// <summary>
        /// 床と、ドアの足元の黄色い線。
        ///
        /// **床は他の場所より明るく置く。** 車内は上から蛍光灯が当たるだけなので、
        /// 床が暗いと立っている人の足元が沈んで、輪郭がどこで切れているか読めない
        /// </summary>
        static void TrainDeck(Transform place, Material deck, Material mark)
        {
            var floor = new Bank { Texel = 0.5f };
            floor.FaceY(0f, -CarHalf, CarHalf, -CarLong, CarLong, 1);
            CarBake(place, "TrainFloor", floor, deck, true);

            var line = new Bank { Texel = 0.5f };
            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                // 床から僅かに浮かせる。同じ高さに置くと二枚が取り合って線がちらつく
                line.FaceY(0.012f, Mathf.Min(CarDoorSide * 0.86f, CarDoorSide * 0.98f),
                    Mathf.Max(CarDoorSide * 0.86f, CarDoorSide * 0.98f),
                    z - CarDoorHalf - 0.14f, z + CarDoorHalf + 0.14f, 1);
            }
            CarBake(place, "TrainLine", line, mark, false);
        }

        /// <summary>
        /// 側壁・妻面と、座席の端の仕切り板。歩いて越えられない面はここへまとめる。
        ///
        /// ドアの開口は床まで抜けているので、二枚組の板だけでは外へ出られてしまう。
        /// 板は当たりを持たない形として組むので、開口は <see cref="Fence"/> で塞ぐ
        /// </summary>
        static void TrainShell(Transform place, Material skin)
        {
            var shell = new Bank { Texel = 0.4f };
            shell.FaceXHoles(-CarHalf, -CarLong, CarLong, 0f, CarHigh, 1, CarHoles(-1f));
            shell.FaceXHoles(CarHalf, -CarLong, CarLong, 0f, CarHigh, -1, CarHoles(1f));
            shell.FaceZ(-CarLong, -CarHalf, CarHalf, 0f, CarHigh, 1);
            shell.FaceZ(CarLong, -CarHalf, CarHalf, 0f, CarHigh, -1);

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                // 座席の下の箱。ここが抜けていると長椅子が宙に浮いて見える
                shell.Box(new Vector3(side * (SeatX + 0.06f), 0.185f, 0f), new Vector3(0.44f, 0.37f, to - from));
                var splits = CarDoorWall(side) ? CarSplitShort : CarSplitLong;
                for (var i = 0; i < splits.Length; i++)
                    shell.Box(new Vector3(side * SeatX, 0.62f, splits[i]), new Vector3(0.56f, 1.24f, 0.05f));
            }
            CarBake(place, "TrainShell", shell, skin, true);

            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                Fence(place, "TrainDoorStop" + i,
                    new Vector3(CarDoorSide * (CarHalf - 0.02f), CarDoorTop * 0.5f, z),
                    new Vector3(0.14f, CarDoorTop, CarDoorHalf * 2f));
            }
            // 長椅子は歩いて通り抜けられる高さなので、座面ごと塞ぐ
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                Fence(place, "TrainSeatStop" + s, new Vector3(side * (SeatX + 0.01f), 0.60f, 0f),
                    new Vector3(0.60f, 1.20f, to - from));
            }
        }

        /// <summary>ドアのある壁か。<c>side</c> は壁の x の符号</summary>
        static bool CarDoorWall(float side) { return side * CarDoorSide > 0f; }

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
                if (CarDoorWall(side) && System.Array.IndexOf(CarDoorBay, i) >= 0)
                    made.Add(new Vector4(z - CarDoorHalf, z + CarDoorHalf, 0f, CarDoorTop));
                else
                    made.Add(new Vector4(z - CarWinHalf, z + CarWinHalf, CarWinLow, CarWinTop));
            }
            return made;
        }

        /// <summary>
        /// 長椅子の一続き。ドアの無い壁は妻面から妻面まで通し、
        /// ドアの壁は開口の手前で切る。荷棚も肘掛けもこの長さに従う
        /// </summary>
        static void CarSeatRun(float side, out float from, out float to)
        {
            to = CarDoorWall(side) ? 1.98f : 3.5f;
            from = -to;
        }

        /// <summary>
        /// 天井と、開口の枠・荷棚の板・吊り広告。踏みも触りもしない面なので当たりは持たせない。
        ///
        /// 枠を入れるのは、抜いただけの窓が壁に開いた真っ黒な穴に見えるため。
        /// 縁が一段手前に出ていると、そこが窓だと分かる
        /// </summary>
        static void TrainFit(Transform place, Material skin)
        {
            var trim = new Bank { Texel = 0.4f };
            trim.FaceY(CarHigh, -CarHalf, CarHalf, -CarLong, CarLong, -1);

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var i = 0; i < CarBayZ.Length; i++)
                {
                    var z = CarBayZ[i];
                    var door = CarDoorWall(side) && System.Array.IndexOf(CarDoorBay, i) >= 0;
                    if (door)
                    {
                        var fx = side * (CarHalf - 0.05f);
                        for (var e = 0; e < 2; e++)
                        {
                            var jz = z + (e == 0 ? -1f : 1f) * (CarDoorHalf + 0.05f);
                            trim.Box(new Vector3(fx, CarDoorTop * 0.5f, jz), new Vector3(0.09f, CarDoorTop, 0.10f));
                        }
                        trim.Box(new Vector3(fx, CarDoorTop + 0.05f, z),
                            new Vector3(0.09f, 0.10f, CarDoorHalf * 2f + 0.20f));
                        continue;
                    }
                    var wx = side * (CarHalf - 0.035f);
                    trim.Box(new Vector3(wx, CarWinLow - 0.03f, z), new Vector3(0.07f, 0.06f, CarWinHalf * 2f + 0.12f));
                    trim.Box(new Vector3(wx, CarWinTop + 0.03f, z), new Vector3(0.07f, 0.06f, CarWinHalf * 2f + 0.12f));
                    for (var e = 0; e < 2; e++)
                    {
                        var jz = z + (e == 0 ? -1f : 1f) * (CarWinHalf + 0.03f);
                        trim.Box(new Vector3(wx, (CarWinLow + CarWinTop) * 0.5f, jz),
                            new Vector3(0.07f, CarWinTop - CarWinLow + 0.12f, 0.06f));
                    }
                }
                // 荷棚。網ではなく板にしたのは、遠目に網の目が潰れてただの灰色の帯になるため
                float from, to;
                CarSeatRun(side, out from, out to);
                trim.Box(new Vector3(side * 1.06f, CarRackY, 0f), new Vector3(0.46f, 0.035f, to - from));
            }

            // 吊り広告。中央の通路の上に、端から見て重なる向きで三枚。
            // z は灯り（TrainLight）の間へ落とす。灯りと同じ z に置くと、
            // 板の中に光源が入って一枚だけ白く飛ぶ
            for (var i = 0; i < 3; i++)
                trim.Box(new Vector3(0f, 2.07f, (i - 1) * 2f), new Vector3(0.62f, 0.40f, 0.014f));

            // 妻面の、次の車両へ続くドアの枠。奥の壁が真っ平らだと通路の突き当たりに見えない
            for (var s = 0; s < 2; s++)
            {
                var ez = (s == 0 ? -1f : 1f) * (CarLong - 0.035f);
                for (var e = 0; e < 2; e++)
                    trim.Box(new Vector3((e == 0 ? -1f : 1f) * 0.435f, 0.95f, ez),
                        new Vector3(0.07f, 1.90f, 0.07f));
                trim.Box(new Vector3(0f, 1.925f, ez), new Vector3(0.94f, 0.07f, 0.07f));
            }
            CarBake(place, "TrainRoof", trim, skin, false);
        }

        /// <summary>座席。座面と背もたれだけ。下の箱と仕切り板は側壁の側にある</summary>
        static void TrainSeat(Transform place)
        {
            var seat = new Bank { Texel = 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                float from, to;
                CarSeatRun(side, out from, out to);
                var len = to - from;
                seat.Box(new Vector3(side * SeatX, SeatY, 0f), new Vector3(0.56f, 0.10f, len));
                // 背は窓の下端で止める。上へ伸ばすと窓の列が座席に食われて短く見える
                seat.Box(new Vector3(side * (SeatX + 0.23f), 0.73f, 0f), new Vector3(0.09f, 0.56f, len));
            }
            CarBake(place, "TrainSeat", seat, Mat("Cloth"), false);
        }

        /// <summary>
        /// 金物。吊り革の棒と輪、縦の手すり、荷棚の縁と受け、ドアの二枚組、肘掛け。
        ///
        /// 輪は四本の細い棒で組む。塗り潰した四角だと、掴まるものではなく
        /// 天井から垂れた黒い板に見える
        /// </summary>
        static void TrainRail(Transform place)
        {
            var rail = new Bank { Texel = 0.5f };

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * StrapX;
                rail.Box(new Vector3(x, StrapY, 0f), new Vector3(0.04f, 0.04f, CarLong * 2f - 0.8f));
                var count = Mathf.FloorToInt((CarLong * 2f - 1.4f) / CarStrapGap) + 1;
                for (var k = 0; k < count; k++)
                {
                    var z = -(count - 1) * 0.5f * CarStrapGap + k * CarStrapGap;
                    rail.Box(new Vector3(x, StrapY - 0.12f, z), new Vector3(0.02f, 0.24f, 0.02f));
                    var ring = StrapY - 0.30f;
                    for (var e = 0; e < 2; e++)
                    {
                        var d = (e == 0 ? -1f : 1f) * 0.054f;
                        rail.Box(new Vector3(x + d, ring, z), new Vector3(0.022f, 0.13f, 0.022f));
                        rail.Box(new Vector3(x, ring + d, z), new Vector3(0.13f, 0.022f, 0.022f));
                    }
                }

                // 縦の手すり。仕切り板の通路側に立てると、座席の切れ目と手すりが同じ場所に来る
                var splits = CarDoorWall(side) ? CarSplitShort : CarSplitLong;
                for (var i = 0; i < splits.Length; i++)
                    rail.Box(new Vector3(side * 0.70f, StrapY * 0.5f, splits[i]),
                        new Vector3(0.055f, StrapY, 0.055f));

                var rests = CarDoorWall(side) ? CarRestShort : CarRestLong;
                for (var i = 0; i < rests.Length; i++)
                {
                    rail.Box(new Vector3(side * SeatX, 0.66f, rests[i]), new Vector3(0.52f, 0.06f, 0.06f));
                    // 通路側の端を座面まで下ろす。宙に浮いた横木は肘掛けに見えない
                    rail.Box(new Vector3(side * (SeatX - 0.23f), 0.55f, rests[i]),
                        new Vector3(0.06f, 0.28f, 0.06f));
                }

                float from, to;
                CarSeatRun(side, out from, out to);
                rail.Box(new Vector3(side * 0.845f, CarRackY + 0.055f, 0f),
                    new Vector3(0.035f, 0.035f, to - from));
                var arms = Mathf.RoundToInt((to - from) / 1.2f) + 1;
                for (var k = 0; k < arms; k++)
                {
                    var az = Mathf.Lerp(from + 0.1f, to - 0.1f, k / (float)(arms - 1));
                    rail.Box(new Vector3(side * 1.06f, CarRackY + 0.06f, az), new Vector3(0.46f, 0.045f, 0.045f));
                }
            }

            // 吊り広告の吊り具。天井との隙間が空いたままだと、板が宙に浮いて見える
            for (var i = 0; i < 3; i++)
                for (var e = 0; e < 2; e++)
                    rail.Box(new Vector3((e == 0 ? -1f : 1f) * 0.22f, 2.285f, (i - 1) * 2f),
                        new Vector3(0.03f, 0.04f, 0.016f));

            // ドアの二枚組。窓を残して縁だけ組み、開口の向こうの帯を透かす
            for (var i = 0; i < CarDoorBay.Length; i++)
            {
                var z = CarBayZ[CarDoorBay[i]];
                var x = CarDoorSide * (CarHalf - 0.045f);
                for (var k = 0; k < 2; k++)
                {
                    var lz = z + (k == 0 ? -1f : 1f) * CarDoorHalf * 0.5f;
                    rail.Box(new Vector3(x, 0.50f, lz), new Vector3(0.05f, 1.00f, CarDoorHalf));
                    rail.Box(new Vector3(x, 1.865f, lz), new Vector3(0.05f, 0.17f, CarDoorHalf));
                    for (var e = 0; e < 2; e++)
                    {
                        var sz = lz + (e == 0 ? -1f : 1f) * (CarDoorHalf - 0.07f) * 0.5f;
                        rail.Box(new Vector3(x, 1.39f, sz), new Vector3(0.05f, 0.78f, 0.07f));
                    }
                }
            }
            CarBake(place, "TrainPole", rail, Mat("Rail"), false);
        }

        /// <summary>
        /// 窓の外。開口ごとに帯を一枚ずつ置き、<see cref="WindowStream"/> がまとめて横へ送る。
        ///
        /// 一枚の長い帯で通していたときは、窓の列が出来たあとも外の絵が窓をまたいで繋がり、
        /// 壁の向こうに一枚の板が張ってあることが見えていた
        /// </summary>
        static void TrainOutside(Transform place)
        {
            var lights = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/WindowLights.png");
            if (lights == null) Debug.LogWarning("窓の外の帯が無い。tools/make-window.py を走らせる");
            var flow = CarFlow("GlowStream", lights, new Color(1f, 0.97f, 0.92f), 1.05f);

            var root = Child(place, "Windows");
            var faces = new List<Renderer>();
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var aim = side < 0f ? Vector3.right : Vector3.left;
                for (var i = 0; i < CarBayZ.Length; i++)
                {
                    var z = CarBayZ[i];
                    var door = CarDoorWall(side) && System.Array.IndexOf(CarDoorBay, i) >= 0;
                    var at = new Vector3(side * (CarHalf + 0.04f), door ? 1.39f : (CarWinLow + CarWinTop) * 0.5f, z);
                    var size = door
                        ? new Vector2(CarDoorHalf * 2f + 0.06f, 0.86f)
                        : new Vector2(CarWinHalf * 2f + 0.10f, CarWinTop - CarWinLow + 0.10f);
                    var pane = Pane(root, "Pane" + s + i, at, size, aim, flow);
                    faces.Add(pane.GetComponent<Renderer>());
                }
            }

            var stream = root.gameObject.AddComponent<WindowStream>();
            var so = new SerializedObject(stream);
            Fill(so.FindProperty("panes"), faces.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 車内の灯り。
        ///
        /// **一つでは足りない。** 8 m の車両を真ん中の一灯で照らすと、両端が闇に落ちて
        /// 人の輪郭がやっと見える絵になる。蛍光灯の筋を二本通し、灯りを四つに分けて、
        /// 端まで床が拾えるところまで上げる。
        ///
        /// 四つに留めたのは、URP が一つの物に当てる追加の灯りを 4 つまでにしているため。
        /// 五つ目からは、見る向きによって当たる灯りが入れ替わって明るさが跳ねる
        /// </summary>
        static void TrainLight(Transform place)
        {
            var tube = Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f);
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * 0.62f;
                Pane(place, "TrainTube" + i, new Vector3(x, CarHigh - 0.02f, 0f),
                    new Vector2(0.17f, CarLong * 2f - 1.2f), Vector3.down, tube);
            }
            for (var i = 0; i < 4; i++)
            {
                Lamp(place, "Fluorescent" + i, LightType.Point,
                    new Vector3(0f, CarHigh - 0.28f, (i * 2f) - 3f), Vector3.zero,
                    new Color(0.92f, 0.95f, 1f), 3.4f, 7.5f);
            }
        }

        // ---- 道具 --------------------------------------------------------------

        /// <summary>
        /// 車内の色。<see cref="Mat"/> の表はどこも暗がりを前提にしていて、
        /// 蛍光灯の下の車内には暗すぎる。ここだけ明るい地の色を持たせる
        /// </summary>
        static Material CarCoat(string name, Color col, float smooth)
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

        /// <summary>
        /// 窓の外の帯。<see cref="Glow"/> は色しか持たせないので、こちらで絵を貼る。
        /// ずれは <see cref="WindowStream"/> が窓ごとに渡すため、マテリアルは一枚で足りる
        /// </summary>
        static Material CarFlow(string name, Texture tex, Color col, float gain)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(col.r * gain, col.g * gain, col.b * gain, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 溜めた面を、渡したマテリアルで一枚に焼いて置く。
        /// <see cref="Emit"/> は素材の名前から <see cref="Tone"/> の表を辿るので、
        /// 表に無い色はこちらから直に渡す
        /// </summary>
        static Transform CarBake(Transform parent, string name, Bank bank, Material mat, bool collide)
        {
            var made = bank.Emit(parent, name, mat, collide, Generated);
            return made != null ? made.transform : null;
        }
    }
}
