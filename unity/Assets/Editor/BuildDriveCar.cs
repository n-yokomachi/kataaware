using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 の車。運転席から見える内張りと、ガレージで外から見える外装を作る。
    /// 寸法の定数と組み立ての入口は <see cref="BuildDrive"/> 本体にある
    /// </summary>
    public static partial class BuildDrive
    {
        // ---- 車内 ----------------------------------------------------------

        /// <summary>
        /// 車。原点は車の中心で、視点は seat の (0.38, 1.55, 0)。
        ///
        /// **もとは運転席から見える範囲だけを内側から組んでいた。** ボンネットのほかに外形は
        /// 無く、側面も車輪も尻も灯りも作っていない。走っているあいだはそれで足りるが、
        /// 場面はガレージから始まり、プレイヤーは 8 m 歩くあいだずっと車を外から見ている。
        /// そこに立っていたのは宙に浮いた板が数枚で、車には見えなかった。
        /// 外装は <see cref="Shell"/> が足す。
        ///
        /// **外装を足しても車内は一切動かさない。** 走行中の画面はすべて車内で、
        /// 目・計器盤・ハンドル・ガラス・ボンネットの寸法はどれも実画面で詰めた値になっている。
        /// 外装は内張りの外面（<see cref="CardOut"/>）より外だけを使い、
        /// 道の見える縁（<see cref="SightY"/>）へは一指も触れない。見直し 14 が毎回測る。
        ///
        /// 車は原作どおり「ボロのオフロード車」で、乗用車ではない。目線 1.55・立てたガラス・
        /// 計器盤の向こうに見えるボンネットの三つがその印で、どれか一つでも乗用車の値へ戻すと
        /// たちまちセダンに見える。とくにボンネットは、他の二つを合わせたより効く
        /// </summary>
        static void Car(Transform parent)
        {
            Clear(parent);
            var trim = new Bank { Texel = 1.2f };
            var seat = new Bank { Texel = 1.2f };
            var glass = new Bank { Texel = 0.8f };
            var body = new Bank { Texel = 0.9f };
            // 塗った鉄。輻・取っ手・摘み・止めねじ。地の色が内装の 3 倍近く明るいので、
            // 夜の帯でも形が残る。車内で唯一「明るい」素材として使う
            var steel = new Bank { Texel = 2.2f };
            // 継ぎ目と窪み。絵は持たず、ただ暗い。板と板の境をここで引く
            var gap = new Bank { Texel = 1.0f };
            // メーターの板。**Texel は 1 から動かさない。** DialMat が _BaseMap_ST で
            // uv を畳み直すときに、実寸がそのまま uv になっていることを当てにしている
            var dials = new Bank { Texel = 1f };

            // 計器盤。天板は 1.28 で、目線より 0.27 下。この差がそのままボンネットの見える量になる。
            // 上げればボンネットが隠れ、下げれば計器盤が薄くなって乗用車に戻る
            trim.Box(new Vector3(0f, 1.13f, 0.74f), new Vector3(1.72f, 0.30f, 0.46f));
            Dash(trim, steel, gap);
            // 英国なので右ハンドル。メーターは運転席の真上（LaneOffset と対）
            Binnacle(trim, dials);
            // メーターより 0.05 手前へ引く。前後を揃えると輪の向こう端が計器の面と擦れる
            Wheel(trim, steel, WheelAt, WheelOuter, WheelThick, WheelLean);
            // 上を後ろへ倒す。屋根が前へ被さる向きにすると、外が見えなくなる。
            // 古いオフロード車のガラスはほとんど立っているので、乗用車の 22 度から 10 度へ起こした。
            // 起こすと同じ間口でもガラスが縦に広がり、天井の縁が視界から退く。
            // 上の縁は天井の板の中へ差し込む。背を縮めずに下げると、下の縁が計器盤から離れて隙間が開く
            glass.Box(new Vector3(0f, 1.590f, 0.905f), new Vector3(1.66f, 0.63f, 0.02f), Quaternion.Euler(-10f, 0f, 0f));
            // ドアの内張り。上端 1.30 を計器盤の天板と揃える。腰の線が左右と前で一本に通ると箱に見える
            for (var s = 0; s < 2; s++) DoorCard(trim, steel, gap, s == 0 ? -1f : 1f);
            Pillars(trim);
            PassengerSeat(seat, steel);
            // 天井。目線との間を 0.33 取る。乗用車だったときの 0.31 より広い
            trim.Box(new Vector3(0f, 1.91f, 0.10f), new Vector3(1.72f, 0.06f, 1.60f));
            glass.Box(new Vector3(0f, 1.83f, 0.74f), new Vector3(0.28f, 0.08f, 0.02f));
            // 助手席の上の吊り手。オフロード車の助手席には必ず付いている
            steel.Box(new Vector3(-0.80f, 1.812f, 0.30f), new Vector3(0.036f, 0.036f, 0.26f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.83f, 1.846f, 0.30f + (i == 0 ? -0.13f : 0.13f)),
                    new Vector3(0.05f, 0.07f, 0.045f));
            Bonnet(body, gap);
            // ゴムと灯りは外装にしか出てこないので、ここで初めて入れ物を作る。
            // 緩衝器も牽引環も排気の先も steel に混ぜる。塗った鉄板の緩衝器は
            // このころの実用車そのもので、入れ物を分けるとレンダラーが 1 つ増えるだけ
            var rubber = new Bank { Texel = 3.0f };
            var lens = new Bank { Texel = 1.0f };
            var tail = new Bank { Texel = 1.0f };
            Shell(body, steel, gap, glass, rubber, lens, tail);
            // 車内の後ろ半分。外装の後に呼ぶのは、消えたままの室内灯に lens の入れ物が要るため。
            // 足すのは客室の後ろ（CabBack）から後ろと、床と、座面だけで、
            // 運転席まわりの寸法には一指も触れない
            Hold(trim, seat, steel, gap, body, lens);

            trim.Emit(parent, "CarTrim", Mat("CarTrim"), false, Generated);
            seat.Emit(parent, "CarSeat", Mat("CarSeat"), false, Generated);
            glass.Emit(parent, "CarGlass", Mat("CarGlass"), false, Generated);
            body.Emit(parent, "CarBody", Mat("CarBody"), false, Generated);
            steel.Emit(parent, "CarSteel", Mat("CarSteel"), false, Generated);
            gap.Emit(parent, "CarGap", Mat("CarGap"), false, Generated);
            rubber.Emit(parent, "CarTyre", Mat("CarTyre"), false, Generated);
            lens.Emit(parent, "CarLamp", Mat("CarLamp"), false, Generated);
            tail.Emit(parent, "CarTail", Mat("CarTail"), false, Generated);
            dials.Emit(parent, "CarDials", DialMat(), false, Generated);

            // 計器の裏の明かり。**この場面で車内に足す灯りはこれ一つだけ。**
            //
            // 帯 0〜2 の車内には明暗しか手掛かりが無く、素材の地を明るくしただけでは
            // ハンドルも計器盤も同じ値に並んで消える（実際に測って、輪と天板と内張りが
            // どれも 18 前後に並んだ）。実際の車と同じく、計器の裏の明かりが手元だけを
            // 照らすことにすれば、輪の上側と天板に段が付く。
            //
            // 届く範囲は 1.10 m。座席から先へは届かないので、道にも沿道にも掛からない。
            // 影は落とさせない。WebGL で影を持つ灯りを増やすと重くなる。
            //
            // **強さが極端に小さいのは書き間違いではない。** 灯りから輪までが 0.27 m しか
            // 離れておらず、距離の二乗で効くので 14 倍に増える。0.06 でも輪が白く飛んで、
            // 画面ぜんたいが滲んだ。実際に測って決めた値がこれで、
            // 帯 1 の輪の上側が 17 から 45 へ、内張り（19）と分かれるところ
            var backlight = Child(parent, "DialLamp");
            backlight.localPosition = new Vector3(WheelAt.x, 1.36f, 0.50f);
            var lit = backlight.GetComponent<Light>();
            if (lit == null) lit = backlight.gameObject.AddComponent<Light>();
            lit.type = LightType.Point;
            lit.color = new Color(1f, 0.74f, 0.42f);
            lit.intensity = 0.0025f;
            lit.range = 1.10f;
            lit.shadows = LightShadows.None;

            // 前照灯が路面を照らした跡。**灯りではなく、照らされた跡の方を置く。**
            // 本物の spot を前に据えると、路面を薙ぐ角度が浅すぎて（10 m 先で 6 度）
            // 面の向きとの積がほとんど残らない。道を明るくできるだけ強くすると、
            // 今度はすぐ脇の木や標識が真っ白に飛ぶ。板なら道だけを狙って照らせる。
            //
            // 車の子にする。沿道と違って環には乗らないので、走っても車の前に据わったまま。
            // 実際そう見える。前照灯の照らしは車と一緒に動くもので、流れて行くものではない。
            // 流れるのは帯 1 の街灯の溜まりの方（<see cref="Motorway"/>）。
            //
            // 強さは帯が持つ（<see cref="DriveSky.beam"/>）。ここで置くのは帯 0 のぶん
            var thrown = Piece(parent, "Beam", Card("Beam", BeamWide, BeamDeep),
                GlowMat("Beam", "Beam", Color.white, 0.26f, BeamWide, BeamDeep));
            thrown.localPosition = new Vector3(0f, 0f, BeamFrom + BeamDeep * 0.5f);

            // 視点の置き場。DriveDirector.seat へ繋ぐ。
            // ハンドルの真後ろに寄せてあるので、座ると輪が正面に来る
            var eye = Child(parent, "Seat");
            eye.localPosition = SeatAt;
            eye.localRotation = Quaternion.identity;

            Arms(parent);
        }

        /// <summary>
        /// 計器盤の作り込み。箱一つの天板と面を、部品の集まりに割る。
        ///
        /// 下を向いたときに画面を埋めるのは天板と運転席側の面の二つで、
        /// どちらも板一枚のままだと段ボールの箱に見える。天板には縁・物置き・曇り止め、
        /// 面には物入れ・吹き出し口・中央の操作盤・スイッチを置く。
        ///
        /// **手前へ出す量はどれも 1 cm 以上取る。** それより薄いと、この解像度では
        /// 面に描いた模様と見分けが付かない
        /// </summary>
        static void Dash(Bank trim, Bank steel, Bank gap)
        {
            // 天板の後ろの縁の玉縁。角のままだと、真上から見て一枚の板に潰れる
            trim.Box(new Vector3(0f, 1.285f, DashFace + 0.050f), new Vector3(1.72f, 0.030f, 0.100f));

            // 天板の物置き。助手席の前。暗い底を浮かせて、四辺を縁で囲う
            const float trayX = -0.47f;
            const float trayZ = 0.685f;
            const float trayW = 0.58f;
            const float trayD = 0.21f;
            gap.Box(new Vector3(trayX, DashTop + 0.004f, trayZ), new Vector3(trayW, 0.008f, trayD));
            for (var i = 0; i < 2; i++)
            {
                var d = i == 0 ? -1f : 1f;
                trim.Box(new Vector3(trayX + d * (trayW * 0.5f + 0.015f), DashTop + 0.010f, trayZ),
                    new Vector3(0.030f, 0.020f, trayD + 0.060f));
                trim.Box(new Vector3(trayX, DashTop + 0.010f, trayZ + d * (trayD * 0.5f + 0.015f)),
                    new Vector3(trayW + 0.060f, 0.020f, 0.030f));
            }

            // 曇り止めの吹き出し。ガラスの下の縁に沿って二列。
            // ガラスはこの位置で既に y 1.65 まで上がっているので、羽根と取り合わない
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -0.50f : 0.26f;
                gap.Box(new Vector3(x, DashTop + 0.003f, 0.895f), new Vector3(0.44f, 0.006f, 0.055f));
                for (var k = 0; k < 4; k++)
                    trim.Box(new Vector3(x - 0.165f + k * 0.110f, DashTop + 0.008f, 0.895f),
                        new Vector3(0.022f, 0.016f, 0.065f));
            }

            // 助手席の掴まり手。オフロード車の助手席にはこれが要る。
            // 高さ 1.365 は道の見える縁（SightY(0.62) = 1.49）の下
            steel.Box(new Vector3(-0.50f, 1.345f, 0.62f), new Vector3(0.30f, 0.040f, 0.040f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.50f + (i == 0 ? -0.130f : 0.130f), 1.308f, 0.62f),
                    new Vector3(0.050f, 0.075f, 0.050f));

            // 物入れの蓋。継ぎ目のぶん一回り小さく、掛け金を下に付ける
            trim.Box(new Vector3(-0.47f, 1.135f, DashFace - 0.016f), new Vector3(0.52f, 0.210f, 0.032f));
            steel.Box(new Vector3(-0.47f, 1.046f, DashFace - 0.030f), new Vector3(0.110f, 0.034f, 0.030f));

            // 吹き出し口。左右の端に一つずつ
            Vent(trim, steel, gap, -0.695f, 1.165f, 0.200f, 0.115f);
            Vent(trim, steel, gap, 0.695f, 1.165f, 0.200f, 0.115f);

            // 中央の操作盤。無線機の面と摘みと切り替え
            trim.Box(new Vector3(-0.01f, 1.130f, DashFace - 0.022f), new Vector3(0.320f, 0.260f, 0.044f));
            gap.Box(new Vector3(-0.01f, 1.196f, DashFace - 0.040f), new Vector3(0.250f, 0.075f, 0.020f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.01f + (i == 0 ? -0.105f : 0.105f), 1.100f, DashFace - 0.056f),
                    new Vector3(0.050f, 0.050f, 0.030f));
            for (var k = 0; k < 3; k++)
                steel.Box(new Vector3(-0.09f + k * 0.080f, 1.030f, DashFace - 0.052f),
                    new Vector3(0.056f, 0.036f, 0.022f));

            // 運転席側のスイッチの列。暗い座に鉄の摘みが四つ並ぶ
            gap.Box(new Vector3(0.325f, 1.075f, DashFace - 0.008f), new Vector3(0.300f, 0.062f, 0.016f));
            for (var k = 0; k < 4; k++)
                steel.Box(new Vector3(0.220f + k * 0.070f, 1.075f, DashFace - 0.024f),
                    new Vector3(0.052f, 0.044f, 0.026f));

            // 露わな止めねじ。板を継いであることを隠さないのが、この車の作りにあたる
            for (var i = 0; i < 2; i++)
                for (var k = 0; k < 2; k++)
                    steel.Box(new Vector3(i == 0 ? -0.820f : 0.820f, k == 0 ? 1.005f : 1.255f, DashFace - 0.008f),
                        new Vector3(0.020f, 0.020f, 0.014f));
        }

        /// <summary>
        /// 吹き出し口ひとつ。奥の暗がりを面より手前へ出し、周りを枠で囲って、
        /// 手前に羽根を渡す。窪みそのものを掘る代わりに、暗い面と枠で窪んで見せている
        /// </summary>
        static void Vent(Bank trim, Bank steel, Bank gap, float x, float y, float wide, float high)
        {
            gap.Box(new Vector3(x, y, DashFace - 0.008f), new Vector3(wide, high, 0.016f));
            for (var i = 0; i < 2; i++)
            {
                var d = i == 0 ? -1f : 1f;
                trim.Box(new Vector3(x + d * (wide * 0.5f + 0.012f), y, DashFace - 0.020f),
                    new Vector3(0.024f, high + 0.048f, 0.040f));
                trim.Box(new Vector3(x, y + d * (high * 0.5f + 0.012f), DashFace - 0.020f),
                    new Vector3(wide + 0.048f, 0.024f, 0.040f));
            }
            for (var k = 0; k < 3; k++)
                steel.Box(new Vector3(x, y - high * 0.5f + high * (k + 0.5f) / 3f, DashFace - 0.022f),
                    new Vector3(wide - 0.010f, 0.012f, 0.012f));
        }

        /// <summary>
        /// メーターの塊と庇。**夜の車内で自分から光っているのはここだけ。**
        ///
        /// 灯りを一つも足さずに、Unlit の絵一枚でそれを出す。帯 0〜2 の暗さでは
        /// 明暗しか手掛かりが無いので、針と目盛りが浮かぶだけで車内の向きが読める。
        ///
        /// 傾きも高さも、道の見える縁（<see cref="SightY"/>）と、調べる対象の印が
        /// 埋まらない隙間の二つで挟まれている。塊の上端は縁より 92 mm 下、
        /// 庇の上端は 30 mm 下で、印（drive.photo / drive.fuel）からは 9 mm 以上離してある。
        /// **動かすなら両方を測り直すこと。** どちらも組み立て時の見直しが拾う
        /// </summary>
        static void Binnacle(Bank trim, Bank dials)
        {
            var lean = Quaternion.Euler(14f, 0f, 0f);
            var pod = new Vector3(WheelAt.x, 1.320f, 0.615f);
            trim.Box(pod, new Vector3(0.560f, 0.140f, 0.160f), lean);
            trim.Box(new Vector3(pod.x, 1.4585f, 0.5195f), new Vector3(0.600f, 0.018f, 0.160f), lean);
            Panel(dials, pod + lean * new Vector3(0f, 0f, -0.0825f), DialWide, DialHigh, lean);
        }

        /// <summary>
        /// 絵を一度だけ貼る板。<see cref="Bank.Quad"/> をじかに呼ぶのは、Box にすると
        /// 横の小口にまで同じ絵が引き伸ばされるため。
        /// 角の順は Bank.FaceZ の裏向き（-z）と揃えてあり、u は右から左へ流れる。
        /// 裏返して戻すのは貼る側（<see cref="DialMat"/>）の役目
        /// </summary>
        static void Panel(Bank bank, Vector3 centre, float wide, float high, Quaternion rot)
        {
            var hw = wide * 0.5f;
            var hh = high * 0.5f;
            System.Func<float, float, Vector3> at =
                (sx, sy) => centre + rot * new Vector3(hw * sx, hh * sy, 0f);
            bank.Quad(at(1f, -1f), at(-1f, -1f), at(-1f, 1f), at(1f, 1f));
        }

        /// <summary>
        /// ドアの内張り。side は -1 が助手席側、1 が運転席側。
        ///
        /// **窓の下枠が要。** 板一枚だけだと脇が抜けたままで、車に乗っているのではなく
        /// 屋根の付いた台に座っているように見える。枠を回して初めて「窓」になる。
        /// 外へ出す量は塞ぐ箱（<see cref="BlockHalfX"/> 0.92）の内に収め、
        /// ガレージのドアの印（x 0.92）を食わない
        /// </summary>
        static void DoorCard(Bank trim, Bank steel, Bank gap, float side)
        {
            trim.Box(new Vector3(side * 0.86f, 1.02f, 0.10f), new Vector3(0.08f, 0.56f, 1.30f));
            trim.Box(new Vector3(side * 0.858f, 1.325f, 0.115f), new Vector3(0.094f, 0.050f, 1.37f));
            // 内張りの継ぎ目。上下二枚に割る
            gap.Box(new Vector3(side * 0.816f, 1.055f, 0.10f), new Vector3(0.020f, 0.014f, 1.28f));
            // 引き手。腰の線のすぐ下に前後へ渡す
            steel.Box(new Vector3(side * 0.797f, 1.190f, 0.28f), new Vector3(0.036f, 0.038f, 0.200f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(side * 0.808f, 1.190f, 0.28f + (i == 0 ? -0.115f : 0.115f)),
                    new Vector3(0.030f, 0.056f, 0.036f));
            // 窓の回し手。前寄りに付ける。電動の窓はこの年式の車には無い
            steel.Box(new Vector3(side * 0.802f, 1.120f, 0.52f), new Vector3(0.028f, 0.076f, 0.076f));
            steel.Box(new Vector3(side * 0.790f, 1.078f, 0.556f), new Vector3(0.024f, 0.030f, 0.088f));
            // 物入れ。下半分の窪みと、その下の棚
            gap.Box(new Vector3(side * 0.812f, 0.885f, -0.02f), new Vector3(0.020f, 0.150f, 0.62f));
            trim.Box(new Vector3(side * 0.800f, 0.805f, -0.02f), new Vector3(0.046f, 0.030f, 0.66f));
            // 止めねじ。四隅
            for (var i = 0; i < 2; i++)
                for (var k = 0; k < 2; k++)
                    steel.Box(new Vector3(side * 0.812f, i == 0 ? 0.790f : 1.268f, -0.42f + k * 0.94f),
                        new Vector3(0.016f, 0.020f, 0.020f));
        }

        /// <summary>
        /// 柱と窓の上枠。脇の抜けを前後と上から囲う。
        ///
        /// 前の柱はガラスの間口（|x| 0.83）の外に立てるので、正面の道は削らない。
        /// 目の右 35 度に来て画面の端に映るが、それは実際の車でもそう見えるもので、
        /// 削っているのは道ではなく脇の抜けの方
        /// </summary>
        static void Pillars(Bank trim)
        {
            var rake = Quaternion.Euler(-10f, 0f, 0f);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 前の柱。ガラスと同じ傾き。下は計器盤の角、上は天井の板へ差し込む
                trim.Box(new Vector3(side * 0.865f, 1.585f, 0.905f), new Vector3(0.085f, 0.640f, 0.085f), rake);
                // 後ろの柱。これが無いと、脇の窓が後ろへ抜けたまま終わる
                trim.Box(new Vector3(side * 0.865f, 1.590f, -0.615f), new Vector3(0.085f, 0.620f, 0.140f));
                // 窓の上枠。柱と柱を天井の下で繋ぐ
                trim.Box(new Vector3(side * 0.865f, 1.850f, 0.130f), new Vector3(0.085f, 0.075f, 1.400f));
            }
        }

        /// <summary>
        /// 助手席。座面・土手・背もたれ・枕・枠。
        ///
        /// **背もたれは座面の後ろに立てる。** これまで前（z 0.22）に立っていて、
        /// 座席が後ろを向いていた。脇を向いたときに真っ先に目に入るのがこの席なので、
        /// 向きが逆だと車内ぜんたいが読めなくなる
        /// </summary>
        static void PassengerSeat(Bank seat, Bank steel)
        {
            const float x = -0.42f;
            seat.Box(new Vector3(x, 0.985f, 0.06f), new Vector3(0.54f, 0.13f, 0.50f));
            for (var i = 0; i < 2; i++)
                seat.Box(new Vector3(x + (i == 0 ? -0.235f : 0.235f), 1.025f, 0.06f),
                    new Vector3(0.07f, 0.11f, 0.46f));
            seat.Box(new Vector3(x, 1.300f, -0.240f), new Vector3(0.50f, 0.58f, 0.13f));
            seat.Box(new Vector3(x, 1.675f, -0.265f), new Vector3(0.24f, 0.15f, 0.11f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(x + (i == 0 ? -0.07f : 0.07f), 1.605f, -0.258f),
                    new Vector3(0.018f, 0.060f, 0.018f));
            // 床の滑り台に載った枠
            steel.Box(new Vector3(x, 0.905f, 0.06f), new Vector3(0.46f, 0.055f, 0.44f));
        }

        // ---- 車内の後ろ半分と荷室 --------------------------------------------

        /// <summary>荷室の床板の上面。台枠の暗がりの天板（0.6225）の上に載る</summary>
        const float HoldFloorY = 0.685f;

        /// <summary>
        /// 振り返ったときに見える車内ぜんたい。床・タイヤハウス・側面の内張り・
        /// 後部座席・積んだ荷・天井・後ろの扉の内側。
        ///
        /// **運転席から後ろを向くと、そこには何も無かった。** 計器盤も内張りも助手席も
        /// 前しか作っておらず、客室の後ろ（<see cref="CabBack"/>）から先は外板の内面が
        /// 剥き出しで、床さえ張っていない。下を向けば車輪のあいだの地面が見えていた。
        /// 原作の主人公は荷を積んで倫敦からエディンバラ近郊へ走るので、後ろが空では困る。
        ///
        /// **運転席の背もたれだけは作らない。** 目は座席の (0.38, 1.55, 0) にあって、
        /// 後ろを向くと <see cref="EyeLead"/> のぶん z -0.22 へ下がる。背もたれは
        /// z -0.24 に立つので、作れば振り返った目の 2 cm 前に板が立って画面を塞ぐ。
        /// 座面と枠だけ置いて、背は座っている本人ということにしてある。
        ///
        /// 寸法はどれも既にある車体から割り出す。内張りの外面は外板の内面
        /// （<see cref="SkinIn"/> 0.93）、天井は荷室の蓋の下面（1.88）、
        /// 後ろは扉の内面（-2.19）、前は客室の後ろの柱（-0.55）。
        /// 角の立った板と箱だけで組むのは外装と同じ
        /// </summary>
        static void Hold(Bank trim, Bank seat, Bank steel, Bank gap, Bank body, Bank lens)
        {
            FloorPan(trim, seat, steel, gap);
            for (var s = 0; s < 2; s++) HoldSide(trim, seat, steel, body, s == 0 ? -1f : 1f);
            Load(trim, seat, steel);
            Lining(trim, steel, lens);
            TailInside(trim, steel, gap);
        }

        /// <summary>
        /// 床。**今まで一枚も無かった。** 客室の前から後ろの扉まで一枚の板で通す。
        /// 下端は敷居（<see cref="SillY"/>）のすぐ下に収めて、外から車体の下を覗いても
        /// 板の小口が出ないようにする。
        ///
        /// 真ん中の隆起は変速機の覆い。オフロード車の床はこれで左右に割れていて、
        /// 変速と副変速の把手が二本そこから生えている。振り返ったとき、
        /// 座席と座席のあいだに最初に目へ入るのがこれになる
        /// </summary>
        static void FloorPan(Bank trim, Bank seat, Bank steel, Bank gap)
        {
            // 床板。前は計器盤の下（0.95）、後ろは扉の内張りの中（-2.22）まで
            trim.Box(new Vector3(0f, HoldFloorY - 0.035f, -0.635f), new Vector3(1.88f, 0.070f, 3.17f));
            // 荷を滑らせないための当て木。塗った鉄で五本。荷室の床に前後の向きが出る
            for (var k = -2; k <= 2; k++)
                steel.Box(new Vector3(k * 0.26f, HoldFloorY + 0.007f, -1.42f),
                    new Vector3(0.055f, 0.014f, 1.52f));
            // 客室と荷室の境の継ぎ目
            gap.Box(new Vector3(0f, HoldFloorY + 0.004f, -0.62f), new Vector3(1.80f, 0.010f, 0.030f));

            // 変速機の覆い。天板は座面より少し低い
            trim.Box(new Vector3(0f, 0.805f, 0.20f), new Vector3(0.34f, 0.26f, 1.50f));
            steel.Box(new Vector3(0f, 0.940f, 0.20f), new Vector3(0.30f, 0.022f, 1.46f));
            // 把手の根の蛇腹
            gap.Box(new Vector3(0.02f, 0.952f, 0.16f), new Vector3(0.18f, 0.030f, 0.22f));
            // 変速の把手。倒れる向きに少しひねって立てる
            steel.Box(new Vector3(0.02f, 1.075f, 0.170f), new Vector3(0.034f, 0.280f, 0.034f),
                Quaternion.Euler(-7f, 0f, -5f));
            trim.Box(new Vector3(0.005f, 1.215f, 0.152f), new Vector3(0.080f, 0.080f, 0.080f));
            // 副変速の把手。四輪駆動の車にはこれが二本目として付いている
            steel.Box(new Vector3(-0.10f, 1.035f, 0.095f), new Vector3(0.030f, 0.200f, 0.030f),
                Quaternion.Euler(-5f, 0f, 7f));
            trim.Box(new Vector3(-0.118f, 1.140f, 0.088f), new Vector3(0.064f, 0.064f, 0.064f));
            // 手制動。覆いの上を後ろへ寝かせる
            steel.Box(new Vector3(0.055f, 1.010f, -0.10f), new Vector3(0.030f, 0.036f, 0.340f),
                Quaternion.Euler(22f, 0f, 0f));
            trim.Box(new Vector3(0.055f, 1.078f, -0.238f), new Vector3(0.048f, 0.048f, 0.110f),
                Quaternion.Euler(22f, 0f, 0f));

            // 運転席の座面。背もたれは作らない（<see cref="Hold"/> の但し書き）。
            // 助手席と同じ作りで、下を向いたときに座っているものが見える
            seat.Box(new Vector3(SeatAt.x, 0.985f, 0.06f), new Vector3(0.54f, 0.13f, 0.50f));
            for (var i = 0; i < 2; i++)
                seat.Box(new Vector3(SeatAt.x + (i == 0 ? -0.235f : 0.235f), 1.025f, 0.06f),
                    new Vector3(0.07f, 0.11f, 0.46f));
            steel.Box(new Vector3(SeatAt.x, 0.905f, 0.06f), new Vector3(0.46f, 0.055f, 0.44f));
        }

        /// <summary>
        /// 荷室の側面。タイヤハウスの張り出し・腰の内張り・窓の内枠・後部座席。
        ///
        /// **タイヤハウスが要る。** 後ろの車輪（<see cref="AxleRearZ"/>）は泥除けの抜きから
        /// 荷室の中へ入り込んでいて、箱で覆わないと床の上に車輪が生えて見える。
        /// 実際の車も同じ形で、この箱の上が後部座席の台になっている。
        ///
        /// 座席は左右で出し分ける。運転席側は下ろして座れる形、助手席側は畳んで
        /// 側面へ立ててある。荷を積むときに畳む席なので、畳んだままなのが
        /// 「荷を積んで出てきた」ことの説明になる
        /// </summary>
        static void HoldSide(Bank trim, Bank seat, Bank steel, Bank body, float side)
        {
            // タイヤハウス。抜きの頂き（0.89）より上へ出して、車輪を覆い切る
            body.Box(new Vector3(side * 0.775f, 0.805f, AxleRearZ), new Vector3(0.320f, 0.290f, 1.020f));
            // 天板の縁。塗った鉄なので、暗い荷室でここだけ形が残る
            steel.Box(new Vector3(side * 0.775f, 0.962f, AxleRearZ), new Vector3(0.345f, 0.030f, 1.050f));
            // 前後の小口。箱をそのまま切るより、一段落とした方が据わって見える
            for (var i = 0; i < 2; i++)
                body.Box(new Vector3(side * 0.775f, 0.740f, AxleRearZ + (i == 0 ? -0.565f : 0.565f)),
                    new Vector3(0.320f, 0.160f, 0.110f));

            // 腰の内張り。タイヤハウスの上から窓の下枠まで
            trim.Box(new Vector3(side * 0.8975f, 1.145f, -1.37f), new Vector3(0.085f, 0.390f, 1.46f));
            // 縦の骨。押した筋の代わりに、内張りの上へ細い板を三本
            foreach (var z in new[] { -0.80f, -1.37f, -1.94f })
                body.Box(new Vector3(side * 0.842f, 1.145f, z), new Vector3(0.040f, 0.370f, 0.075f));
            // 窓の下枠。暗い内張りと窓のあいだに明るい線が一本通る
            steel.Box(new Vector3(side * 0.9025f, 1.355f, -1.37f), new Vector3(0.075f, 0.050f, 1.420f));
            // 窓の内枠。上枠と前後の縦枠
            trim.Box(new Vector3(side * 0.9025f, 1.825f, -1.37f), new Vector3(0.075f, 0.050f, 1.420f));
            trim.Box(new Vector3(side * 0.9025f, 1.590f, -0.745f), new Vector3(0.075f, 0.420f, 0.090f));
            trim.Box(new Vector3(side * 0.9025f, 1.590f, -2.035f), new Vector3(0.075f, 0.420f, 0.090f));

            if (side > 0f)
            {
                // 運転席側。タイヤハウスの上に載る横向きの座席
                seat.Box(new Vector3(0.665f, 1.035f, AxleRearZ), new Vector3(0.380f, 0.115f, 0.980f));
                seat.Box(new Vector3(0.800f, 1.270f, AxleRearZ), new Vector3(0.110f, 0.360f, 0.960f));
                // 前の縁の枠と、床へ下ろした脚。座面が宙に浮かない
                steel.Box(new Vector3(0.490f, 0.985f, AxleRearZ), new Vector3(0.045f, 0.045f, 1.000f));
                for (var i = 0; i < 2; i++)
                    steel.Box(new Vector3(0.490f, 0.820f, AxleRearZ + (i == 0 ? -0.400f : 0.400f)),
                        new Vector3(0.036f, 0.290f, 0.036f));
            }
            else
            {
                // 助手席側。畳んで側面へ立ててある。背と座面の二枚が前後に重なる
                seat.Box(new Vector3(-0.800f, 1.330f, AxleRearZ), new Vector3(0.110f, 0.460f, 0.980f));
                seat.Box(new Vector3(-0.715f, 1.310f, AxleRearZ), new Vector3(0.095f, 0.420f, 0.940f),
                    Quaternion.Euler(0f, 0f, -4f));
                // 蝶番の側の枠。タイヤハウスの天板（0.977）の上に載る
                steel.Box(new Vector3(-0.760f, 1.000f, AxleRearZ), new Vector3(0.080f, 0.050f, 0.980f));
                // 吊り紐。畳んだ席の上を跨いで、前へ垂らす
                for (var i = 0; i < 2; i++)
                {
                    var z = AxleRearZ + (i == 0 ? -0.360f : 0.360f);
                    trim.Box(new Vector3(-0.790f, 1.575f, z), new Vector3(0.190f, 0.028f, 0.050f));
                    trim.Box(new Vector3(-0.655f, 1.455f, z), new Vector3(0.024f, 0.260f, 0.050f));
                }
            }
        }

        /// <summary>
        /// 積んである荷。木箱・布を掛けた荷・鞄・油の缶と、それを留める帯と床の留め具。
        ///
        /// 置く場所は左右のタイヤハウス（|x| 0.615 から外）のあいだと、その後ろの
        /// 一枚になった床。荷は床に直に置き、帯で床の留め具へ締める。
        /// **一つだけ塗った鉄の物を混ぜる。** 暗い荷室で角がいちばん先に光るので、
        /// 荷が積んであること自体がそこで読める
        /// </summary>
        static void Load(Bank trim, Bank seat, Bank steel)
        {
            var floor = HoldFloorY;
            // 奥は扉の内張りの面（-2.11）まで。手前は客室との境（-0.62）まで。
            // 左右はタイヤハウスの内の縁（|x| 0.615）まで

            // 木箱。奥の左に二つ積む。下の箱には鉄の箍を二本
            seat.Box(new Vector3(-0.32f, floor + 0.220f, -1.875f), new Vector3(0.620f, 0.440f, 0.450f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.32f + (i == 0 ? -0.180f : 0.180f), floor + 0.220f, -1.875f),
                    new Vector3(0.030f, 0.455f, 0.465f));
            trim.Box(new Vector3(-0.36f, floor + 0.545f, -1.900f), new Vector3(0.460f, 0.210f, 0.380f),
                Quaternion.Euler(0f, 9f, 0f));

            // 布を掛けた荷。中の塊の上へ、一回り大きい布を被せる
            trim.Box(new Vector3(0.10f, floor + 0.190f, -1.30f), new Vector3(0.560f, 0.380f, 0.600f));
            seat.Box(new Vector3(0.10f, floor + 0.205f, -1.30f), new Vector3(0.600f, 0.400f, 0.640f),
                Quaternion.Euler(0f, 3f, 0f));
            // 布の皺。垂れたところが二本
            foreach (var at in new[] { -0.22f, 0.20f })
                seat.Box(new Vector3(0.10f + at, floor + 0.190f, -1.30f), new Vector3(0.070f, 0.400f, 0.660f),
                    Quaternion.Euler(0f, 0f, 3f));

            // 荷締めの帯。荷の上を跨いで、床まで下ろす
            trim.Box(new Vector3(0.10f, floor + 0.415f, -1.30f), new Vector3(0.640f, 0.022f, 0.090f));
            for (var i = 0; i < 2; i++)
            {
                var x = 0.10f + (i == 0 ? -0.315f : 0.315f);
                trim.Box(new Vector3(x, floor + 0.210f, -1.30f), new Vector3(0.022f, 0.430f, 0.090f));
                steel.Box(new Vector3(x, floor + 0.030f, -1.30f), new Vector3(0.090f, 0.040f, 0.040f));
            }
            // 締め金。帯の上の一点だけ鉄
            steel.Box(new Vector3(0.24f, floor + 0.428f, -1.30f), new Vector3(0.100f, 0.055f, 0.100f));

            // 床の留め具。前は客室との境のそば、後ろは扉の手前
            foreach (var ring in new[] { new Vector2(0.545f, -0.68f), new Vector2(0.780f, -2.05f) })
                for (var s = 0; s < 2; s++)
                {
                    var x = (s == 0 ? -1f : 1f) * ring.x;
                    steel.Box(new Vector3(x, floor + 0.012f, ring.y), new Vector3(0.115f, 0.024f, 0.060f));
                    steel.Box(new Vector3(x, floor + 0.038f, ring.y), new Vector3(0.035f, 0.060f, 0.035f));
                }

            // 鞄。畳んだ席の下の床へ、タイヤハウスへ押し付けて寝かせる
            trim.Box(new Vector3(-0.34f, floor + 0.095f, -0.80f), new Vector3(0.520f, 0.190f, 0.340f),
                Quaternion.Euler(0f, -8f, 0f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.34f + (i == 0 ? -0.150f : 0.150f), floor + 0.128f, -0.655f),
                    new Vector3(0.070f, 0.055f, 0.040f), Quaternion.Euler(0f, -8f, 0f));

            // 油の缶。木箱の右。**塗った鉄はこの荷室でいちばん明るい素材で、
            // 暗い帯ではここの角が最初に光る。** 荷が積んであること自体がそこで読める
            steel.Box(new Vector3(0.40f, floor + 0.235f, -1.90f), new Vector3(0.190f, 0.470f, 0.360f));
            steel.Box(new Vector3(0.40f, floor + 0.495f, -1.90f), new Vector3(0.080f, 0.060f, 0.140f));
        }

        /// <summary>
        /// 天井と、そこに付く物。骨・手すり・消えたままの室内灯・助手席の背の裏の物入れ。
        ///
        /// 荷室の天井は内から蓋を一枚しただけで、上を向くと継ぎ目の無い板だった。
        /// 骨を三本渡せば、そこが天井だと読めるようになる
        /// </summary>
        static void Lining(Bank trim, Bank steel, Bank lens)
        {
            // 天井の骨。荷室の蓋（下面 1.88）へ 5 mm 差し込んで、面が並ばないようにする
            foreach (var z in new[] { -1.12f, -1.60f, -2.05f })
                steel.Box(new Vector3(0f, 1.865f, z), new Vector3(1.78f, 0.040f, 0.070f));
            // 手すり。後ろの柱の上。助手席の前のものと同じ作り
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                steel.Box(new Vector3(side * 0.845f, 1.812f, -0.86f), new Vector3(0.036f, 0.036f, 0.260f));
                for (var i = 0; i < 2; i++)
                    steel.Box(new Vector3(side * 0.872f, 1.846f, -0.86f + (i == 0 ? -0.130f : 0.130f)),
                        new Vector3(0.050f, 0.070f, 0.045f));
            }
            // 室内灯。**点けない。** 車内に足す灯りは計器の裏の一つだけという取り決めで
            // （<see cref="Car"/>）、ここに置くのは消えている灯りの笠だけ。
            // 淡い面が天井に一つあると、暗い荷室の奥行きがそこで測れる
            trim.Box(new Vector3(-0.56f, 1.862f, -1.30f), new Vector3(0.200f, 0.045f, 0.150f));
            lens.Box(new Vector3(-0.56f, 1.834f, -1.30f), new Vector3(0.150f, 0.022f, 0.105f));

            // 助手席の背もたれの裏の物入れ。振り返ると背の裏側が正面に来る
            trim.Box(new Vector3(-0.42f, 1.225f, -0.322f), new Vector3(0.420f, 0.320f, 0.036f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.42f + (i == 0 ? -0.190f : 0.190f), 1.300f, -0.318f),
                    new Vector3(0.028f, 0.520f, 0.024f));
        }

        /// <summary>
        /// 後ろの扉の内側。内張り・窓の内枠・掛け金・扉に留めた工具。
        ///
        /// 扉は横開きで、蝶番は外から見て左（x -0.915）に付いている。
        /// 掛け金は外の掛け金（x 0.860）と同じ側に置く。左右が食い違うと、
        /// 外から見たときと内から見たときで開く向きが逆になる
        /// </summary>
        static void TailInside(Bank trim, Bank steel, Bank gap)
        {
            // 内張り。扉の板（-2.25〜-2.19）へ 2 mm 差し込む
            const float panel = -2.155f;
            var face = panel + 0.0375f;
            trim.Box(new Vector3(0f, 0.980f, panel), new Vector3(1.780f, 0.700f, 0.075f));
            // 板を二枚継いである継ぎ目
            gap.Box(new Vector3(0f, 1.010f, face + 0.008f), new Vector3(1.700f, 0.018f, 0.020f));
            // 窓の内枠。扉の枠（厚み 0.06）の内側に合わせる
            trim.Box(new Vector3(0f, 1.355f, -2.160f), new Vector3(1.740f, 0.050f, 0.060f));
            trim.Box(new Vector3(0f, 1.825f, -2.160f), new Vector3(1.740f, 0.050f, 0.060f));
            for (var s = 0; s < 2; s++)
                trim.Box(new Vector3((s == 0 ? -1f : 1f) * 0.885f, 1.590f, -2.160f),
                    new Vector3(0.090f, 0.420f, 0.060f));
            // 引き手と掛け金
            steel.Box(new Vector3(0.700f, 1.120f, face + 0.030f), new Vector3(0.240f, 0.050f, 0.050f));
            steel.Box(new Vector3(0.815f, 1.035f, face + 0.025f), new Vector3(0.070f, 0.170f, 0.040f));
            // 蝶番の側の骨
            steel.Box(new Vector3(-0.830f, 0.980f, face + 0.018f), new Vector3(0.070f, 0.640f, 0.030f));
            // 車載の工具。帯で扉へ留めてある。x は積んだ木箱（-0.63 まで）と
            // 蝶番の側の骨（-0.795 から）のあいだ
            steel.Box(new Vector3(-0.700f, 1.060f, face + 0.050f), new Vector3(0.110f, 0.380f, 0.090f));
            trim.Box(new Vector3(-0.700f, 1.180f, face + 0.062f), new Vector3(0.150f, 0.030f, 0.115f));
        }

        /// <summary>
        /// ボンネットと前のフェンダー。車内から計器盤の向こうに見える、唯一の車体。
        ///
        /// **セダンとオフロード車を分けているのはこれ。** 目線を上げただけでは
        /// 「背の高い乗用車」にしか見えず、計器盤の先に平らな鉄板が寝ていて初めて
        /// 4x4 に座っていることが読める。
        ///
        /// 天板は 1.22。計器盤の天板（1.28）より 0.06 だけ低い。深く下げると計器盤の陰に
        /// 完全に隠れ、上げると道が見えなくなる。目（1.55 / z 0.22）から計器盤の前の角
        /// （1.28 / z 0.97）を掠める線が天板と交わるのが z 1.14 で、そこから先の 1.3 m が見えている。
        /// フェンダーは天板より 0.06 高くして左右に立てる。この二本の筋が遠近を作るので、
        /// 板一枚のときより前へ伸びて見える。
        /// 「ボロの」車なので曲面も飾りも付けない。角の立った板だけで組む
        /// </summary>
        static void Bonnet(Bank body, Bank gap)
        {
            // 天板。計器盤の前端（0.97）から継いで前へ 1.45
            body.Box(new Vector3(0f, 1.195f, 1.695f), new Vector3(1.52f, 0.05f, 1.45f));
            // 天板を割る絞りの筋。**帯 4 が朝になってから要るようになった。**
            //
            // 夜のあいだは天板が暗がりに沈んでいて、形を読ませていたのは前の縁と
            // フェンダーの二本だけで足りていた。朝日が当たるようになると、同じ天板が
            // 画面の下三分の一を埋める継ぎ目の無い一枚板になり、塗った鉄板ではなく
            // 下地のままの板に見える。絞りを二本通せば、板に前後の向きと厚みが出る。
            //
            // 暗い素材で引く。夜には縁が増えるだけなので、暗い帯での読みやすさを損なわない。
            //
            // **細く低く。** 幅 0.05・高さ 0.009 で置いたときは、朝日が 14 度から
            // 薙ぐせいで筋の三倍の影が伸び、絞りではなく天板に切った溝に見えた。
            // 影の長さは高さ ÷ tan(14 度) で決まるので、詰めるのは幅より高さの方
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                gap.Box(new Vector3(side * 0.46f, 1.2225f, 1.700f), new Vector3(0.028f, 0.006f, 1.40f));
            }
            // フェンダー。天板の左右に一段高く載せる。
            // **外の縁を車体の外板（0.97）まで広げてある。** 0.91 で切っていた頃は
            // 車内から見るぶんには足りたが、外装を付けたら天板と側面のあいだに
            // 幅 2 cm の隙が一本、鼻先まで通って開いた。内の縁（0.65）は動かさない。
            // 運転席から見えるのはそちらで、見える量が変わってしまう
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                body.Box(new Vector3(side * 0.81f, 1.235f, 1.710f), new Vector3(0.32f, 0.09f, 1.42f));
            }
            // 前端。運転席からは見えないが、ガレージで外から見たときに前が抜けていると板が浮く。
            // 幅もフェンダーと揃えて外板まで広げる
            body.Box(new Vector3(0f, 1.085f, 2.450f), new Vector3(BodyHalf * 2f, 0.27f, 0.06f));
        }

        // ---- 車の外装 --------------------------------------------------------

        /// <summary>
        /// 車の外装ぜんたい。側面・荷室・屋根・足回り・前後の顔。
        ///
        /// **車内へは一指も触れない。** 使うのは内張りの外面（<see cref="CardOut"/> 0.90）より
        /// 外と、天井の板（1.94）より上と、客室の後ろ（<see cref="CabBack"/>）より後ろだけ。
        /// 唯一の例外が風防の下の水切り板とワイパーで、そこは道の見える縁
        /// （<see cref="SightY"/>(0.94) = 1.442）より 0.17 下に収めてある。
        ///
        /// 側面のガラスだけは**片面**で張る。両面の板を入れると、走行中の車内の見え方が
        /// 変わる。この場面は走っている時間がほとんどで、脇の窓から見える沿道は
        /// 5 つの帯ぶん詰めた画のうちに入っている。外から見たときだけガラスが要るので、
        /// 外を向いた面 1 枚で足りる（床に貼る帯と同じ理屈で「裏は誰も見ない」）。
        /// 併せて、見直し 4 の巻き数の判定は開いた面を「中」と数えないので、
        /// 窓の判定点（x 0.80）がガラスに埋まったことにもならない
        /// </summary>
        static void Shell(Bank body, Bank steel, Bank gap, Bank glass, Bank tyre, Bank lens, Bank tail)
        {
            Flanks(body, steel, gap, glass);
            Boot(body, steel, gap, glass, tyre, tail, lens);
            Face(body, steel, gap, lens);
            Running(body, steel, gap, tyre);
        }

        /// <summary>
        /// 側面。外板・窓の枠・柱の外側・雨樋・ドアの継ぎ目と蝶番と取っ手。
        ///
        /// **雨樋が要。** 屋根と側面のあいだに樋を一本回すだけで、
        /// 塗った板を貼り合わせた車に見える。この年式の四輪駆動車の顔付きそのもの
        /// </summary>
        static void Flanks(Bank body, Bank steel, Bank gap, Bank glass)
        {
            var mid = (SkinIn + BodyHalf) * 0.5f;
            var skin = BodyHalf - SkinIn;
            // 泥除けの抜き。前は車軸の前後 ArchR、後ろも同じ
            var frontIn = AxleFrontZ - ArchR;
            var frontOut = AxleFrontZ + ArchR;
            var rearIn = AxleRearZ + ArchR;
            var rearOut = AxleRearZ - ArchR;
            // 抜きの頂き。ここから上が板で埋まる
            var crown = HubY + ArchR;

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * mid;

                // 前の翼板。抜きの前後は腰まで、抜きの上は頂きから腰まで
                Slab(body, x, skin, 0.94f, frontIn, SillY, 1.28f);
                Slab(body, x, skin, frontOut, 2.42f, SillY, 1.28f);
                Slab(body, x, skin, frontIn, frontOut, crown, 1.28f);
                // ドア。腰の線まで。継ぎ目で前後を切る
                Slab(body, x, skin, CabBack, 0.94f, SillY, BeltY);
                // 後ろの荷室の側面。抜きを避けて三つに割る
                Slab(body, x, skin, rearIn, CabBack, SillY, BeltY);
                Slab(body, x, skin, TailZ, rearOut, SillY, BeltY);
                Slab(body, x, skin, rearOut, rearIn, crown, BeltY);

                // 窓の枠。下枠・上枠・柱で、間を抜いてガラスを張る
                Slab(body, x, skin, TailZ, 0.94f, BeltY, PaneLow);
                Slab(body, x, skin, TailZ, 0.94f, PaneHigh, 1.94f);
                // 中柱（客室の後ろ）と後ろの隅の柱
                Slab(body, x, skin, CabBack, CabBack + 0.12f, PaneLow, PaneHigh);
                Slab(body, x, skin, TailZ, TailZ + 0.17f, PaneLow, PaneHigh);

                // 前の柱と中柱の外側。車内の柱（x 0.8225〜0.9075）の続き
                body.Box(new Vector3(x, 1.585f, 0.905f), new Vector3(skin, 0.640f, 0.085f),
                    Quaternion.Euler(-10f, 0f, 0f));
                body.Box(new Vector3(x, 1.590f, -0.615f), new Vector3(skin, 0.620f, 0.140f));

                // 屋根の縁。天井の板（|x| 0.86 まで）と窓の上枠（0.93 から）の隙を埋める。
                // 外の縁を 0.97 まで伸ばすと上枠と面がそろって、遠くでちらつく
                body.Box(new Vector3(side * 0.895f, 1.910f, 0.10f), new Vector3(0.070f, 0.060f, 1.60f));
                // 雨樋。屋根と側面の境を一本で通す
                steel.Box(new Vector3(side * (BodyHalf + 0.012f), 1.885f, -0.655f),
                    new Vector3(0.040f, 0.048f, 3.19f));

                // ドアの継ぎ目。前後の縁を暗く落とす
                foreach (var seam in new[] { CabBack, 0.94f })
                    gap.Box(new Vector3(x, (SillY + BeltY) * 0.5f, seam),
                        new Vector3(skin + 0.004f, BeltY - SillY, 0.018f));
                // 露わな蝶番。二枚。この車は継ぎ目を隠さない
                foreach (var y in new[] { 0.80f, 1.20f })
                    steel.Box(new Vector3(side * (BodyHalf + 0.010f), y, 0.900f),
                        new Vector3(0.030f, 0.085f, 0.110f));
                // 取っ手。押しボタン式の座と、引く爪
                steel.Box(new Vector3(side * (BodyHalf + 0.012f), 1.170f, 0.420f),
                    new Vector3(0.028f, 0.070f, 0.200f));
                steel.Box(new Vector3(side * (BodyHalf + 0.026f), 1.170f, 0.480f),
                    new Vector3(0.022f, 0.040f, 0.060f));
                // 鍵穴
                steel.Box(new Vector3(side * (BodyHalf + 0.008f), 1.090f, 0.420f),
                    new Vector3(0.018f, 0.032f, 0.032f));
                // 止めねじ。腰の線に沿って。露わなままなのがこの車の作り
                for (var k = 0; k < 6; k++)
                    steel.Box(new Vector3(side * (BodyHalf + 0.006f), BeltY - 0.030f, -2.05f + k * 0.52f),
                        new Vector3(0.014f, 0.020f, 0.020f));

                // 側面のガラス。外を向いた面 1 枚ずつ。ドアと荷室で 2 枚
                var pane = side * (BodyHalf - 0.012f);
                glass.FaceX(pane, -0.60f, 0.86f, PaneLow, PaneHigh, s == 0 ? -1 : 1);
                glass.FaceX(pane, TailZ + 0.17f, CabBack, PaneLow, PaneHigh, s == 0 ? -1 : 1);

                // 鏡。腕木と鏡面。**外へ出す量は塞ぐ箱（1.06）の内に収める。**
                // 歩いていて頭が鏡を通り抜けると、車体をすり抜けたのと同じに見える
                steel.Box(new Vector3(side * 1.005f, 1.470f, 0.880f), new Vector3(0.090f, 0.030f, 0.030f));
                body.Box(new Vector3(side * 1.040f, 1.490f, 0.860f), new Vector3(0.036f, 0.150f, 0.115f));
                glass.FaceZ(0.800f, side * 1.040f - 0.030f, side * 1.040f + 0.030f, 1.430f, 1.548f, -1);
            }

            // 屋根の外板。天井の板（1.94）の上へ薄く載せる。荷室の上まで一枚で通す。
            // **車内の天井の板には重ねない。** 1.88 まで下ろすと、板の下面（1.88）と
            // 外板の下面が同じ高さで向かい合い、車内の天井が走行中ずっとちらつく
            body.Box(new Vector3(0f, (1.94f + ShellTop) * 0.5f, (TailZ + 0.94f) * 0.5f),
                new Vector3(BodyHalf * 2f, ShellTop - 1.94f, 0.94f - TailZ));
            // 荷室の天井。客室の後ろは天井の板が無いので、ここだけ内から蓋をする。
            // 幅を窓の上枠の内面（0.93）へ 5 mm 差し込んで、面が並ばないようにする
            body.Box(new Vector3(0f, 1.910f, (TailZ + CabBack) * 0.5f),
                new Vector3(1.870f, 0.060f, CabBack - TailZ));
            // 屋根の骨。三本。板一枚のままだと、上から見たときに平らな蓋になる
            foreach (var z in new[] { -1.70f, -0.90f, -0.10f })
                steel.Box(new Vector3(0f, ShellTop + 0.010f, z), new Vector3(BodyHalf * 2f - 0.10f, 0.022f, 0.050f));

            // 風防の下の水切り板。ボンネットの後ろ端（0.97）と風防の下端をつなぐ。
            // 天板 1.30 は道の見える縁 1.442 の下。ここを上げると走行中の道が削れる
            body.Box(new Vector3(0f, 1.265f, 0.955f), new Vector3(1.80f, 0.070f, 0.070f));
            // ワイパー。二本を左下がりに寝かせる。水切り板の上に乗るだけの高さに留める
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                steel.Box(new Vector3(side * 0.34f, 1.302f, 0.940f), new Vector3(0.560f, 0.016f, 0.016f),
                    Quaternion.Euler(0f, 0f, side * 7f));
                steel.Box(new Vector3(side * 0.62f, 1.300f, 0.950f), new Vector3(0.055f, 0.030f, 0.050f));
            }
            // ボンネットの蝶番。後ろ端に二つ。開く向きが読める
            for (var s = 0; s < 2; s++)
                steel.Box(new Vector3((s == 0 ? -1f : 1f) * 0.52f, 1.235f, 0.995f),
                    new Vector3(0.110f, 0.040f, 0.075f));
        }

        /// <summary>x が一定の板 1 枚。厚みを持たせた箱として置く</summary>
        static void Slab(Bank bank, float x, float thick, float z0, float z1, float y0, float y1)
        {
            if (z1 <= z0 || y1 <= y0) return;
            bank.Box(new Vector3(x, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(thick, y1 - y0, z1 - z0));
        }

        /// <summary>
        /// 尻。後ろの扉・後ろのガラス・背負った予備輪・尾灯・番号板・緩衝器。
        ///
        /// **予備輪が決め手。** 尻に輪を一つ背負わせるだけで、乗用車の後ろ姿から離れる。
        /// 実際そこに積む物なので、飾りではなく道具として付いている
        /// </summary>
        static void Boot(Bank body, Bank steel, Bank gap, Bank glass, Bank tyre, Bank tail, Bank lens)
        {
            var back = TailZ + 0.03f;
            // 後ろの扉。腰の線まで一枚。横開きなので蝶番は片側だけ
            body.Box(new Vector3(0f, (SillY + BeltY) * 0.5f, back), new Vector3(BodyHalf * 2f, BeltY - SillY, 0.06f));
            // ガラスの枠。上枠と左右の柱
            body.Box(new Vector3(0f, (PaneHigh + 1.94f) * 0.5f, back), new Vector3(BodyHalf * 2f, 1.94f - PaneHigh, 0.06f));
            for (var s = 0; s < 2; s++)
                body.Box(new Vector3((s == 0 ? -1f : 1f) * 0.915f, (PaneLow + PaneHigh) * 0.5f, back),
                    new Vector3(0.110f, PaneHigh - PaneLow, 0.06f));
            // 後ろのガラス。外を向いた面 1 枚
            glass.FaceZ(TailZ - 0.005f, -0.86f, 0.86f, PaneLow, PaneHigh, -1);
            // 扉の継ぎ目と蝶番と掛け金
            gap.Box(new Vector3(0f, BeltY - 0.004f, TailZ - 0.004f), new Vector3(BodyHalf * 2f - 0.06f, 0.016f, 0.055f));
            for (var i = 0; i < 2; i++)
                steel.Box(new Vector3(-0.915f, 0.78f + i * 0.40f, TailZ - 0.030f), new Vector3(0.090f, 0.075f, 0.030f));
            steel.Box(new Vector3(0.860f, 0.98f, TailZ - 0.034f), new Vector3(0.070f, 0.170f, 0.030f));

            // 背負った予備輪。運転席の側へ寄せる。真ん中に据えると番号板が隠れる。
            // 緩衝器（y 0.63〜0.77）より上へ持ち上げて取り合わないようにする
            var spare = new Vector3(0.26f, 1.06f, TailZ - 0.175f);
            Hoop(tyre, spare, Vector3.forward, TyreR - 0.055f, TyreWide, 0.110f, 14, 0f, 360f);
            Wheel4(null, steel, gap, spare, Vector3.back, true);
            // 受けの腕。扉から生やす
            steel.Box(new Vector3(spare.x, spare.y, TailZ - 0.075f), new Vector3(0.080f, 0.080f, 0.150f));
            steel.Box(new Vector3(spare.x, spare.y - 0.24f, TailZ - 0.040f), new Vector3(0.060f, 0.300f, 0.055f));

            // 尾灯。丸を二つ。赤いのは外装で唯一の彩り
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                steel.Box(new Vector3(side * 0.78f, 0.95f, TailZ - 0.020f), new Vector3(0.190f, 0.190f, 0.045f));
                Disc(tail, new Vector3(side * 0.78f, 0.95f, TailZ - 0.046f), Vector3.back, 0.072f, 10);
            }
            // 番号板。字は持たせない。淡い板が一枚あるだけで後ろ姿が締まる
            lens.Box(new Vector3(-0.30f, 0.76f, TailZ - 0.028f), new Vector3(0.430f, 0.125f, 0.020f));

            // 緩衝器。塗った鉄の角材。牽引環と排気の先を添える
            steel.Box(new Vector3(0f, 0.700f, TailZ - 0.115f), new Vector3(BodyHalf * 2f + 0.02f, 0.135f, 0.155f));
            steel.Box(new Vector3(0f, 0.610f, TailZ - 0.160f), new Vector3(0.110f, 0.100f, 0.090f));
            steel.Box(new Vector3(-0.66f, 0.540f, TailZ - 0.060f), new Vector3(0.065f, 0.065f, 0.200f));
        }

        /// <summary>
        /// 顔。格子・前照灯・小灯・下の覆い・緩衝器。
        ///
        /// 前照灯は丸。この年式の実用車の顔はここで決まるので、四角い板では代わりにならない
        /// </summary>
        static void Face(Bank body, Bank steel, Bank gap, Bank lens)
        {
            // 格子。暗い奥を前へ出して、横桟を渡す
            gap.Box(new Vector3(0f, 1.085f, 2.495f), new Vector3(1.08f, 0.210f, 0.035f));
            for (var k = 0; k < 4; k++)
                steel.Box(new Vector3(0f, 1.000f + k * 0.057f, 2.512f), new Vector3(1.06f, 0.022f, 0.018f));
            for (var s = 0; s < 2; s++)
                steel.Box(new Vector3((s == 0 ? -1f : 1f) * 0.525f, 1.085f, 2.505f),
                    new Vector3(0.030f, 0.215f, 0.030f));

            // 前照灯。輪を金物で囲って、奥に淡い面を置く
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var at = new Vector3(side * 0.715f, 1.085f, NoseZ);
                Hoop(steel, at + new Vector3(0f, 0f, 0.020f), Vector3.forward, 0.108f, 0.036f, 0.030f, 10, 0f, 360f);
                Disc(lens, at + new Vector3(0f, 0f, 0.018f), Vector3.forward, 0.098f, 10);
                // 灯りの座。輪の裏の暗がり
                gap.Box(at + new Vector3(0f, 0f, -0.010f), new Vector3(0.210f, 0.210f, 0.030f));
            }
            // 小灯。翼板の前の角。丸ではなく角にして、前照灯と役を分ける
            for (var s = 0; s < 2; s++)
                lens.Box(new Vector3((s == 0 ? -1f : 1f) * 0.885f, 1.095f, NoseZ + 0.016f),
                    new Vector3(0.090f, 0.075f, 0.030f));

            // 下の覆い。前パネル（0.95）から下を塞ぐ。無いと鼻の下が抜けて板に見える
            body.Box(new Vector3(0f, 0.840f, 2.450f), new Vector3(1.74f, 0.230f, 0.055f));
            // 緩衝器。後ろと同じ角材。牽引環を二つ
            steel.Box(new Vector3(0f, 0.700f, NoseZ + 0.085f), new Vector3(BodyHalf * 2f + 0.02f, 0.140f, 0.160f));
            for (var s = 0; s < 2; s++)
                steel.Box(new Vector3((s == 0 ? -1f : 1f) * 0.42f, 0.700f, NoseZ + 0.180f),
                    new Vector3(0.075f, 0.105f, 0.045f));
            // ボンネットの留め金。二つとも外から掛ける。開けるたびに手で外す作り
            for (var s = 0; s < 2; s++)
                steel.Box(new Vector3((s == 0 ? -1f : 1f) * 0.50f, 1.215f, 2.455f),
                    new Vector3(0.090f, 0.060f, 0.060f));
        }

        /// <summary>
        /// 足回り。車輪 4 本・泥除けの庇・車軸・車台・敷居。
        ///
        /// **車台の暗がりが要る。** 敷居（0.62）から床までが抜けていると、
        /// 脇から見たときに車の下を向こうの区画まで見通せて、車体が浮いて見える
        /// </summary>
        static void Running(Bank body, Bank steel, Bank gap, Bank tyre)
        {
            foreach (var z in new[] { AxleFrontZ, AxleRearZ })
            {
                for (var s = 0; s < 2; s++)
                {
                    var side = s == 0 ? -1f : 1f;
                    var at = new Vector3(side * HubX, HubY, z);
                    Hoop(tyre, at, Vector3.right, TyreR - 0.055f, TyreWide, 0.110f, 16, 0f, 360f);
                    Wheel4(tyre, steel, gap, at, Vector3.right * side, false);
                    // 泥除けの庇。抜きの上半分に沿わせて、外板より 3 cm 外へ出す。
                    // 貼り付けの庇はこの手の車に必ず付いていて、輪郭を太く見せる。
                    // 角は真上を 0 として前後へ振るので、上半分は -82〜82 度
                    Hoop(body, new Vector3(side * (BodyHalf + 0.002f), HubY, z), Vector3.right,
                        ArchR + 0.030f, 0.060f, 0.055f, 7, -82f, 164f);
                    // 抜きの内側。庇の下の暗がり。無いと車輪が板に貼り付いて見える
                    Hoop(gap, new Vector3(side * (BodyHalf - 0.075f), HubY, z), Vector3.right,
                        ArchR - 0.015f, 0.150f, 0.045f, 7, -82f, 164f);
                }
                // 車軸。剛性軸なので左右が一本で繋がっている。脇から必ず見える
                steel.Box(new Vector3(0f, HubY, z), new Vector3(HubX * 2f - 0.10f, 0.105f, 0.120f));
                gap.Box(new Vector3(0f, HubY, z), new Vector3(0.34f, 0.230f, 0.230f));
            }
            // 車台。敷居より下の暗がり。前後の車軸のあいだを埋める
            gap.Box(new Vector3(0f, 0.500f, 0.10f), new Vector3(1.34f, 0.245f, 4.40f));
            // 敷居。踏み板の受けと、乗り降りの段
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                body.Box(new Vector3(side * 0.945f, 0.590f, -0.28f), new Vector3(0.075f, 0.075f, 3.10f));
                steel.Box(new Vector3(side * 1.000f, 0.470f, 0.170f), new Vector3(0.130f, 0.045f, 0.740f));
                for (var i = 0; i < 2; i++)
                    steel.Box(new Vector3(side * 0.975f, 0.530f, 0.170f + (i == 0 ? -0.30f : 0.30f)),
                        new Vector3(0.040f, 0.130f, 0.040f));
            }
        }

        /// <summary>
        /// 車輪の面。外側の皿・中心の窪み・止めねじ・内側の蓋。
        /// axis が向く側が外。tyre が null なら予備輪で、内側の蓋は要らない
        /// </summary>
        static void Wheel4(Bank tyre, Bank steel, Bank gap, Vector3 at, Vector3 axis, bool spare)
        {
            var n = axis.normalized;
            var face = at + n * (spare ? 0.030f : TyreWide * 0.5f - 0.035f);
            Disc(steel, face, n, 0.235f, 10);
            Disc(gap, face + n * 0.004f, n, 0.082f, 8);
            Disc(steel, face + n * 0.008f, n, 0.046f, 8);
            // 止めねじ。五本。輪が回る物だと読めるのはこれがあるから
            var u = Mathf.Abs(n.x) > 0.5f ? Vector3.up : Vector3.right;
            var v = Vector3.Cross(n, u);
            for (var i = 0; i < 5; i++)
            {
                var a = (i / 5f) * Mathf.PI * 2f;
                steel.Box(face + n * 0.006f + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * 0.135f,
                    new Vector3(0.036f, 0.036f, 0.036f));
            }
            if (spare || tyre == null) return;
            // 内側。抜けたままだと反対側から車の中が見通せる
            Disc(gap, at - n * (TyreWide * 0.5f - 0.020f), -n, 0.255f, 10);
        }

        /// <summary>
        /// 円板 1 枚。axis の向く側から見て表になる。
        ///
        /// <see cref="Bank"/> に丸い面は <see cref="Bank.FanY"/>（水平だけ）しか無いので、
        /// <see cref="Bank.Patch"/> を 2 枚ずつ使って扇に張る。Patch は a→b→c と a→c→d の
        /// 二つを出すので、(中心, 縁 i, 縁 i+1, 縁 i+2) と渡せば degenerate な三角を作らずに
        /// 扇が組める。seg は偶数でなければならない。
        ///
        /// uv は面の中の実寸に Texel を掛けて振る。ほかの面と同じ物差しなので、
        /// ゴムでも鉄でも絵の目の粗さが揃う
        /// </summary>
        static void Disc(Bank bank, Vector3 centre, Vector3 axis, float radius, int seg)
        {
            if (seg < 4) return;
            if (seg % 2 != 0) seg++;
            var n = axis.normalized;
            var u = Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up;
            u = (u - n * Vector3.Dot(u, n)).normalized;
            // u × v が axis になる組にする。FanY と同じ向きの決め方
            var v = Vector3.Cross(n, u);
            var rim = new Vector3[seg];
            var uv = new Vector2[seg];
            for (var i = 0; i < seg; i++)
            {
                var a = (float)i / seg * Mathf.PI * 2f;
                var c = Mathf.Cos(a) * radius;
                var d = Mathf.Sin(a) * radius;
                rim[i] = centre + u * c + v * d;
                uv[i] = new Vector2(c * bank.Texel, d * bank.Texel);
            }
            var mid = Vector2.zero;
            for (var i = 0; i < seg; i += 2)
                bank.Patch(centre, rim[i], rim[(i + 1) % seg], rim[(i + 2) % seg],
                    mid, uv[i], uv[(i + 1) % seg], uv[(i + 2) % seg]);
        }

        /// <summary>
        /// 輪。短い箱を円周に並べる（ハンドルの <see cref="Wheel"/> と同じ手）。
        /// axis が輪の軸、radius は箱の中心までの半径、wide が軸の向きの幅、thick が厚み。
        /// from と span は度。360 なら閉じた輪、それ未満なら泥除けの庇のような弧になる。
        /// 角度は axis から見て、軸に直交する最初の基準（上または右）から左回りに測る
        /// </summary>
        static void Hoop(Bank bank, Vector3 centre, Vector3 axis, float radius,
            float wide, float thick, int seg, float from, float span)
        {
            if (seg < 3 || radius <= 0f) return;
            var n = axis.normalized;
            var u = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            u = (u - n * Vector3.Dot(u, n)).normalized;
            var v = Vector3.Cross(n, u);
            var step = span / seg * Mathf.Deg2Rad;
            // 継ぎ目を少し重ねる。ぴったりだと角と角のあいだに隙間が見える
            var chord = radius * step * 1.10f;
            if (span >= 359.9f) chord = 2f * Mathf.PI * radius / seg * 1.10f;
            for (var i = 0; i < seg; i++)
            {
                var a = (from + (i + 0.5f) * span / seg) * Mathf.Deg2Rad;
                var radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                var along = -u * Mathf.Sin(a) + v * Mathf.Cos(a);
                bank.Box(centre + radial * radius, new Vector3(wide, thick, chord),
                    Quaternion.LookRotation(along, radial));
            }
        }

        /// <summary>
        /// 運転席の前腕。帯 0〜3 は腕組み、帯 4 だけハンドルに手を乗せる。
        /// 原作「自動運転に任せて私は腕組みをしながら考える」で、手動で運転するのは最後だけ。
        /// どちらも見た目だけで、操作には繋がらない。出し分けるのは <see cref="DriveDirector"/>。
        ///
        /// **カメラの子にはしない。** 座ったまま首だけ振るので、カメラに付けると
        /// 見回すたびに腕が画面に貼り付いたまま一緒に回る。車の持ち物として車内に置けば、
        /// 首を振っても腕は据わったまま残る。場面 1 の前腕（<see cref="Forearm"/>）が
        /// カメラの子なのは、あちらが下を向いたときだけ出す作りだから
        /// </summary>
        static void Arms(Transform parent)
        {
            Folded(Child(parent, "ArmsFolded"));
            var grip = Child(parent, "ArmsOnWheel");
            OnWheel(grip);
            // 組み立てた直後はエディタで腕組みの当たり具合を見られるよう、そちらだけ出しておく。
            // 再生すれば DriveDirector.Awake がどちらも伏せ、乗り込んでから帯に合わせて出す
            grip.gameObject.SetActive(false);
        }

        /// <summary>
        /// 腕組み。胸の前で前腕を上下に重ね、手は反対側の肘の下へ入れる。
        /// 二の腕は肩へ向けて後ろへ抜けさせる。上半身は作っていないので、
        /// 目（z 0.22）より後ろまで下がったところで視界から外れる
        /// </summary>
        static void Folded(Transform parent)
        {
            Clear(parent);
            var sleeve = new Bank { Texel = 1.6f };
            var skin = new Bank { Texel = 2.0f };
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var shoulder = new Vector3(FoldedAt.x + side * ShoulderHalf, ShoulderY, ShoulderZ);
                var elbow = new Vector3(FoldedAt.x + side * 0.21f, FoldedAt.y, FoldedAt.z - 0.02f);
                Limb(sleeve, shoulder, elbow, 0.095f);
                // 右腕を上、左腕を下に重ねる。前後にも少しずらして、二本が同じ面に潰れないようにする
                var y = FoldedAt.y + side * 0.034f;
                var z = FoldedAt.z + side * 0.012f;
                var wrist = new Vector3(FoldedAt.x - side * 0.15f, y, z);
                Limb(sleeve, new Vector3(elbow.x, y, z), wrist, 0.085f);
                // 手は反対側の肘の下。腕組みの形はここで決まる
                skin.Box(wrist - new Vector3(side * 0.055f, 0f, 0f), new Vector3(0.10f, 0.075f, 0.09f));
            }
            sleeve.Emit(parent, "FoldedSleeves", Mat("Sleeve"), false, Generated);
            skin.Emit(parent, "FoldedHands", Mat("Skin"), false, Generated);
        }

        /// <summary>
        /// ハンドルに乗せた手。輪の左右（三時と九時）を握る。
        /// 握りの位置は <see cref="WheelRing"/> から出すので、輪の寸法を変えても手が離れない
        /// </summary>
        static void OnWheel(Transform parent)
        {
            Clear(parent);
            var sleeve = new Bank { Texel = 1.6f };
            var skin = new Bank { Texel = 2.0f };
            var tilt = Quaternion.Euler(WheelLean, 0f, 0f);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var grip = WheelAt + new Vector3(side * WheelRing, 0f, 0f);
                // 肘は輪から引く。手で書いた高さのままだと、車高を上げたときに腕だけ床に取り残される
                var elbow = new Vector3(WheelAt.x + side * 0.21f, WheelAt.y - 0.14f, 0.10f);
                var shoulder = new Vector3(WheelAt.x + side * ShoulderHalf, ShoulderY, ShoulderZ);
                Limb(sleeve, shoulder, elbow, 0.095f);
                Limb(sleeve, elbow, grip, 0.085f);
                // 手は輪に沿わせて倒す。長い辺が輪の接線に乗る
                skin.Box(grip, new Vector3(0.055f, 0.125f, 0.085f), tilt);
            }
            sleeve.Emit(parent, "WheelSleeves", Mat("Sleeve"), false, Generated);
            skin.Emit(parent, "WheelHands", Mat("Skin"), false, Generated);
        }

        /// <summary>a から c へ伸びる 1 本。thick は断面の一辺</summary>
        static void Limb(Bank bank, Vector3 a, Vector3 c, float thick)
        {
            var span = c - a;
            var len = span.magnitude;
            if (len < 1e-4f) return;
            bank.Box((a + c) * 0.5f, new Vector3(thick, thick, len), Quaternion.LookRotation(span, Vector3.up));
        }

        /// <summary>
        /// ハンドルの輪。Bank に丸い形は無いので、短い箱を円周に並べて繋ぐ。
        /// lean は x 軸まわりに倒す角。オフロード車なので輪はバスに近いところまで寝ている。
        /// 立てるとメーターを真正面から塞ぐ。
        ///
        /// **輪と輻を別の素材に分ける。** 輪は黒い樹脂、輻と芯は塗った鉄で、
        /// 地の明るさが 3 倍近く違う。夜の帯では明暗しか手掛かりが無く、
        /// 全部を同じ黒で塗ると、手前に大きく映っているはずのハンドルが丸ごと消える
        /// </summary>
        static void Wheel(Bank rim, Bank spoke, Vector3 centre, float outer, float thick, float lean)
        {
            const int seg = 20;
            var tilt = Quaternion.Euler(lean, 0f, 0f);
            var ring = outer * 0.5f - thick * 0.5f;
            // 継ぎ目を少し重ねる。ぴったりだと角と角のあいだに隙間が見える
            var chord = 2f * Mathf.PI * ring / seg * 1.08f;
            for (var i = 0; i < seg; i++)
            {
                var a = (i + 0.5f) / seg * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a) * ring, Mathf.Sin(a) * ring, 0f);
                var rot = tilt * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                rim.Box(centre + tilt * at, new Vector3(chord, thick, thick), rot);
            }
            spoke.Box(centre, new Vector3(0.11f, 0.11f, 0.045f), tilt);
            // 警笛の押し。芯の面から手前へ出す。輪が寝ているので「手前」は上になる。
            // ここだけ輪と同じ黒い樹脂にする。芯まで鉄で塗ると、昼の帯で
            // 白い塊が手前に立っているようにしか見えない
            rim.Box(centre + tilt * new Vector3(0f, 0f, -0.028f),
                new Vector3(0.075f, 0.075f, 0.016f), tilt);
            // 輪だけだと宙に浮いた環にしか見えない
            for (var i = 0; i < 3; i++)
            {
                var a = (90f + i * 120f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ring * 0.5f;
                var rot = tilt * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                spoke.Box(centre + tilt * at, new Vector3(ring, 0.022f, 0.018f), rot);
            }
        }
    }
}
