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
        // ---- 風防。メートル --------------------------------------------------
        //
        // ガラスと、その上に乗る水と、水を拭うワイパーの三つが同じ面に並ぶので、
        // 面の寸法はここ一箇所に置く。三つのどれか一つだけ動かすと、
        // 水が浮くかワイパーがガラスを突き抜ける

        /// <summary>風防の中心</summary>
        public static readonly Vector3 GlassAt = new Vector3(0f, 1.590f, 0.905f);
        /// <summary>風防の幅と丈と厚み</summary>
        public static readonly Vector3 GlassSize = new Vector3(1.66f, 0.63f, 0.02f);
        /// <summary>風防の傾き。上を後ろへ 10 度倒す</summary>
        public static readonly Quaternion GlassLean = Quaternion.Euler(-10f, 0f, 0f);

        // ---- 開くドア --------------------------------------------------------
        //
        // 運転席のドアだけは、車体に焼き込まずに別の入れ物へ分けて出す。
        // 乗り込むところでドアが開くので、板が蝶番を軸にして振れなければならない。
        // 助手席の側は最後まで閉じたままなので、これまでどおり車体に焼く

        /// <summary>開くドアの側。右ハンドルなので運転席は +x</summary>
        const float LeafSide = 1f;
        /// <summary>蝶番の軸。ドアの前の縁（継ぎ目 z 0.94）の、外板の面（x 0.97）</summary>
        static readonly Vector3 LeafPivot = new Vector3(LeafSide * BodyHalf, 0f, 0.94f);
        /// <summary>ドアの後ろの縁。客室の後ろの柱と継ぐところ</summary>
        const float LeafBack = CabBack;

        /// <summary>
        /// 開くドアが持って行く面の入れ物。素材ごとに 1 つずつ。
        ///
        /// 車体の入れ物（trim / body / …）と同じ Texel で持つ。同じ素材の面が
        /// 二つの mesh に分かれるので、絵の目の粗さが揃っていないと継ぎ目で段が出る。
        ///
        /// **閉じた姿勢では車体の座標そのままで組む。** 蝶番の入れ物の下に
        /// 「車の原点へ戻す」子を一枚挟んであるので（<see cref="DoorLeaf"/>）、
        /// ここへ置く面の座標は車体側と一切変わらない。閉じているあいだは
        /// 焼き込んでいたときと同じところに同じ面が出る
        /// </summary>
        sealed class Leaf
        {
            public readonly Bank Body = new Bank { Texel = 0.9f };
            public readonly Bank Steel = new Bank { Texel = 2.2f };
            public readonly Bank Gap = new Bank { Texel = 1.0f };
            public readonly Bank Glass = new Bank { Texel = 0.8f };
            public readonly Bank Trim = new Bank { Texel = 1.2f };
            public readonly Bank Plate = new Bank { Texel = 2.4f };

            /// <summary>その側の面をドアへ載せるか。開かない側は車体に焼く</summary>
            public static bool Mine(float side) { return side * LeafSide > 0f; }
        }

        /// <summary>
        /// 開くドアを吐く。蝶番の入れ物を車の下へ立て、その子に板の面を置く。
        ///
        /// **入れ物を二段にする。** 上（DriverDoor）は蝶番の軸のところに据えて、
        /// これを y 回りに回すとドアが前の縁を軸に振れる。下（Leaf）は軸のぶんを
        /// 打ち消して車の原点へ戻すので、面の座標は車体に焼いていたときのままでよい。
        /// 一段にすると、面を組むたびに軸からの差へ書き直すことになり、
        /// 車体側の寸法と突き合わせられなくなる。
        ///
        /// 回すのは <see cref="CarDoor"/>。段取り（DriveDirector）が開き具合を渡す
        /// </summary>
        static void DoorLeaf(Transform parent, Leaf leaf)
        {
            var hinge = Child(parent, "DriverDoor");
            hinge.localPosition = LeafPivot;
            hinge.localRotation = Quaternion.identity;
            var slab = Child(hinge, "Leaf");
            slab.localPosition = -LeafPivot;
            slab.localRotation = Quaternion.identity;

            leaf.Body.Emit(slab, "DoorBody", Mat("CarBody"), false, Generated);
            leaf.Steel.Emit(slab, "DoorSteel", Mat("CarSteel"), false, Generated);
            leaf.Gap.Emit(slab, "DoorGap", Mat("CarGap"), false, Generated);
            leaf.Glass.Emit(slab, "DoorGlass", Mat("CarGlass"), false, Generated);
            leaf.Trim.Emit(slab, "DoorTrim", Mat("CarTrim"), false, Generated);
            leaf.Plate.Emit(slab, "DoorChrome",
                ItemMat("CarChrome", new Color(0.455f, 0.460f, 0.470f), 0.62f), false, Generated);

            var hung = hinge.GetComponent<CarDoor>();
            if (hung == null) hung = hinge.gameObject.AddComponent<CarDoor>();
            var so = new SerializedObject(hung);
            so.FindProperty("swing").floatValue = CarDoor.Swing;
            so.FindProperty("side").floatValue = LeafSide;
            so.FindProperty("open").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 組み上がった場面はまだガレージの中で、誰も乗り込んでいない。閉じて置く
            hung.Set(0f);
        }

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
            // 運転席のドア。開くので車体には焼かず、蝶番の子へ分けて出す
            var leaf = new Leaf();

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
            glass.Box(GlassAt, GlassSize, GlassLean);
            // ドアの内張り。上端 1.30 を計器盤の天板と揃える。腰の線が左右と前で一本に通ると箱に見える
            for (var s = 0; s < 2; s++) DoorCard(trim, steel, gap, s == 0 ? -1f : 1f, leaf);
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
            Shell(body, steel, gap, glass, rubber, lens, tail, leaf);
            // 車内の後ろ半分。外装の後に呼ぶのは、消えたままの室内灯に lens の入れ物が要るため。
            // 足すのは客室の後ろ（CabBack）から後ろと、床と、座面だけで、
            // 運転席まわりの寸法には一指も触れない
            Hold(trim, seat, steel, gap, body, lens);
            // 調べられる物。判定点のところに、それと分かる形を置く。
            // 入れ物を分けて足すので、ここより上の寸法には一指も触れない
            Fittings(parent, trim, steel, gap, lens, tail, leaf);

            trim.Emit(parent, "CarTrim", Mat("CarTrim"), false, Generated);
            seat.Emit(parent, "CarSeat", Mat("CarSeat"), false, Generated);
            glass.Emit(parent, "CarGlass", PaneMat(), false, Generated);
            body.Emit(parent, "CarBody", Mat("CarBody"), false, Generated);
            steel.Emit(parent, "CarSteel", Mat("CarSteel"), false, Generated);
            gap.Emit(parent, "CarGap", Mat("CarGap"), false, Generated);
            rubber.Emit(parent, "CarTyre", Mat("CarTyre"), false, Generated);
            lens.Emit(parent, "CarLamp", Mat("CarLamp"), false, Generated);
            tail.Emit(parent, "CarTail", Mat("CarTail"), false, Generated);
            dials.Emit(parent, "CarDials", DialMat(), false, Generated);
            // 開くドア。車体を吐いた後に、蝶番の入れ物ごと立てる
            DoorLeaf(parent, leaf);

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

            // 前照灯の照らし。**灯りではなく、照らされたところの方を置く。**
            // 本物の spot を前に据えると、路面を薙ぐ角度が浅すぎて（10 m 先で 6 度）
            // 面の向きとの積がほとんど残らない。道を明るくできるだけ強くすると、
            // 今度はすぐ脇の木や標識が真っ白に飛ぶ。板なら道だけを狙って照らせる。
            //
            // 車の子にする。沿道と違って環には乗らないので、走っても車の前に据わったまま。
            // 実際そう見える。前照灯の照らしは車と一緒に動くもので、流れて行くものではない。
            // 流れるのは帯 1 の街灯の溜まりの方（<see cref="Motorway"/>）。
            //
            // **入れ物ごと車の原点に据える。** 板の物体座標がそのまま車の座標になるので、
            // シェーダーへ渡すレンズの座（<see cref="LampX"/> ほか）を組み立てと同じ数で書ける。
            //
            // 強さは帯が持つ（<see cref="DriveSky.beam"/>）。ここで置くのは帯 0 のぶん
            var thrown = Piece(parent, "Beam", Throw("Beam"), BeamMat());
            thrown.localPosition = Vector3.zero;

            // 視点の置き場。DriveDirector.seat へ繋ぐ。
            // ハンドルの真後ろに寄せてあるので、座ると輪が正面に来る
            var eye = Child(parent, "Seat");
            eye.localPosition = SeatAt;
            eye.localRotation = Quaternion.identity;

            Arms(parent);
            Rain(parent);
        }

        // ---- 前照灯の照らし。メートル ----------------------------------------
        //
        // **絵ではなく式で持つ。** 灯り一つを、レンズの座から前へ伸びる円錐として置き、
        // 板の画素ごとにその円錐へ当てて明るさを出す（<c>HalfAware/Headlamp</c>）。
        // 路面の板も空中の板も同じ式を引くので、路面の跡は「円錐が舗装を切ったところ」に
        // なり、空中の帯と自動で辻褄が合う。
        //
        // 前は路面に寝かせた板一枚に絵を貼っていた。光そのものが空中に無いので、
        // 夜の路面に橙色の染みが浮いているようにしか見えなかった。
        //
        // **BuildDrive の BeamWide / BeamDeep / BeamFrom はもう使わない。**
        // あちらは絵（DriveBeam.png）の寸法と対になっている数で、絵をやめた以上
        // ここの寸法と揃える相手がいない。板の寸法はこの節が持つ

        /// <summary>
        /// 前照灯のレンズの座。**<see cref="Face"/> が置く輪の中心と揃える。**
        /// 片方だけ動かすと、灯りの無いところから光が出る
        /// </summary>
        const float LampX = 0.715f;
        const float LampY = 1.085f;
        /// <summary>レンズの面。鼻先（<c>NoseZ</c> 2.48）の 4 cm 前</summary>
        const float LampZ = 2.52f;
        /// <summary>
        /// 光の軸を伏せる量。正接。0.050 で 2.9 度。
        ///
        /// 路面に落ちる跡がどこで終わるかは、これと <see cref="LampLid"/> の差で決まる。
        /// 終わりは <c>LampY / (LampDrop - LampLid)</c> ＝ 30 m。
        /// 運転席から道が見え始めるのは 10.5 m 先なので、見えている路面のほぼ全部が
        /// 照らしの中に入り、その先ですっぱり切れる
        /// </summary>
        const float LampDrop = 0.050f;
        /// <summary>
        /// 上端の切れ。軸からの上向き。正接。
        ///
        /// **実際の下向きの前照灯は上端がすっぱり切れている。** 対向車を眩ませないための
        /// 造りで、これが前照灯を前照灯に見せる。四角い板をぼかしただけでは出てこない
        /// </summary>
        const float LampLid = 0.014f;
        /// <summary>
        /// 切れ目のぼかし。**画素に載る太さまでは鈍らせる。**
        /// 切れるところは画面の 5 画素ぶんしか無いので、ここを 0 にすると
        /// 走るたびに階段が明滅する。0.015 で 4 画素かけて落ちる
        /// </summary>
        const float LampLidSoft = 0.015f;
        /// <summary>横の開き。正接。0.115 で片側 6.6 度</summary>
        const float LampSpread = 0.115f;
        /// <summary>軸より下への開き。正接。ここを詰めると足元が暗くなる</summary>
        const float LampDroop = 0.105f;
        /// <summary>下ほど横へ張り出す量。実際の前照灯も足元がいちばん広い</summary>
        const float LampFlare = 1.6f;
        /// <summary>届く距離。m。これを過ぎると二乗で弱る</summary>
        const float LampReach = 17f;
        /// <summary>芯の増し。灯り一つぶんの明るい真ん中。左右に二つ並ぶ山の高さ</summary>
        const float LampCore = 1.20f;
        /// <summary>
        /// 芯の広がり。正接。
        ///
        /// **横の開きに対する割合ではなく、角で持つ。** 割合で置いたときは、
        /// 芯の差し渡しが距離に連れてほとんど変わらず、二つの山が最後まで
        /// 溶けないか、でなければ最初から一つだった。角で持てば差し渡しは
        /// 距離に比例して広がり、灯りの間隔（1.43 m）は変わらないので、
        /// 13 m では二つの芯が並び、25 m を過ぎたあたりで一つに溶ける
        /// </summary>
        const float LampLobe = 0.040f;
        /// <summary>
        /// ぜんたいの強さ。**帯の <see cref="DriveSky.beam"/> に掛かる。**
        /// 帯の値は絵を貼っていた頃の板に合わせて決めてあるので、
        /// 式に移した明るさをここで元の桁へ戻す。
        /// 画面で測って、いちばん明るいところが前の 1.7 倍（線形）になる値
        /// </summary>
        const float LampGain = 1.10f;

        /// <summary>
        /// 空中の帯が切れ目の上へ漏れる量。正接。
        ///
        /// 路面の跡だけでは「路面に橙の染みがある」に留まる。雨や靄の中を通る光は
        /// 切れ目の上にも淡い暈を残すので、そこを漏らして初めて空中に光があると読める
        /// </summary>
        const float AirLeak = 0.045f;
        /// <summary>空中の下への開きの増し。散るぶんだけ路面より広い</summary>
        const float AirOpen = 1.7f;
        /// <summary>
        /// 空中の強さ。路面に対する割合。
        ///
        /// **境を潰さない量に留める。** 切れ目の上へ漏らすのは芯だけなので
        /// （シェーダーの側でやる）、ここを上げても上下が繋がることはないが、
        /// 上げすぎると空ぜんたいが白ちゃける
        /// </summary>
        const float AirGain = 0.30f;
        /// <summary>板の向きの効き。1 に近いほど、斜めから見たときに早く消える</summary>
        const float AirFace = 0.6f;

        /// <summary>路面に敷く板。幅と、手前の z と先の z</summary>
        const float ThrowWide = 17f;
        const float ThrowFrom = 2.6f;
        const float ThrowTo = 40f;
        /// <summary>空中の板の下の縁。路面の板（<c>GlowY</c>）のすぐ上</summary>
        const float AirFloor = 0.032f;
        /// <summary>
        /// 空中の板。(灯りからの距離 m, 重み)。
        ///
        /// **光の筋を横から切った面を、奥へ何枚か重ねる。** 運転席は光の軸のほぼ真上に
        /// 座っているので、軸に沿って立てた板（十字の構え）はどれも紙のように
        /// 横から見ることになって何も出ない。軸を横切る向きに寝かせれば、
        /// どの板も正面から見ることになる。
        ///
        /// 重みは板と板の間隔。光の中を見通した量を数えているので、
        /// 間隔の広い奥ほど一枚が受け持つぶんが多い。
        ///
        /// **枚数はこれで足りる。** 円錐の中の明るさは角で決まるので、どの板も
        /// 画面ではほぼ同じところに同じ大きさで重なる。六枚で段は出ない。
        /// 三角は路面と合わせて 14 枚
        /// </summary>
        static readonly Vector2[] AirCards =
        {
            new Vector2(4.5f, 0.60f), new Vector2(8f, 0.80f), new Vector2(12.5f, 1.00f),
            new Vector2(18f, 1.15f), new Vector2(24f, 1.20f), new Vector2(30f, 1.20f)
        };

        /// <summary>
        /// 前照灯の照らしの板。路面に寝かせた一枚と、空中に立てた六枚。
        ///
        /// **uv は絵の座標ではない。** x が板の種（0 が路面、1 が空中）、
        /// y が板ごとの重み。シェーダーはこの二つで振る舞いを分ける
        /// </summary>
        static Mesh Throw(string name)
        {
            return Shape(name, 1f, b =>
            {
                var road = new Vector2(0f, 1f);
                var hw = ThrowWide * 0.5f;
                b.Patch(
                    new Vector3(-hw, GlowY, ThrowFrom), new Vector3(hw, GlowY, ThrowFrom),
                    new Vector3(hw, GlowY, ThrowTo), new Vector3(-hw, GlowY, ThrowTo),
                    road, road, road, road);
                foreach (var card in AirCards)
                {
                    var f = card.x;
                    var z = LampZ + f;
                    // 板は光の広がりより一回り大きく取る。切り詰めると縁で光が切れる
                    var wide = LampX + 0.42f * f;
                    var top = 1.10f + 0.10f * f;
                    var tag = new Vector2(1f, card.y);
                    b.Patch(
                        new Vector3(-wide, AirFloor, z), new Vector3(wide, AirFloor, z),
                        new Vector3(wide, top, z), new Vector3(-wide, top, z),
                        tag, tag, tag, tag);
                }
            });
        }

        /// <summary>
        /// 前照灯の照らしのマテリアル。加算で重ねるので、暗いところは何もしない。
        ///
        /// **色は Vector で渡す。** 線形のまま渡したいので、色として宣言して
        /// SetColor の変換に晒さない（<see cref="Paint"/> と同じ扱い）。
        ///
        /// **橙に寄せない。** 実際の前照灯は白〜淡い黄で、橙に転ぶのは古い電球の車だけ。
        /// 原作は「ボロのオフロード車」なので少し暖かくてよいが、赤と青の差は 13% に留める。
        /// 芯はそこからさらに白へ抜ける（シェーダーの側でやる）。
        ///
        /// _BaseColor は景色ごとに DriveDirector が差し替える（<see cref="DriveSky.Apply"/>）。
        /// ここで置くのは帯 0 のぶん
        /// </summary>
        static Material BeamMat()
        {
            var shader = Shader.Find("HalfAware/Headlamp");
            if (shader == null) Debug.LogWarning("HalfAware/Headlamp が見つからない。前照灯が照らさない");
            var path = Materials + "Beam.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "Beam";
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", new Color(0.26f, 0.26f, 0.26f, 1f));
            m.SetVector("_Warm", new Vector4(1f, 0.955f, 0.875f, 1f));
            m.SetVector("_Lamp", new Vector4(LampX, LampY, LampZ, LampDrop));
            m.SetVector("_Cut", new Vector4(LampLid, LampLidSoft, LampSpread, LampDroop));
            m.SetVector("_Shape", new Vector4(LampFlare, LampReach, LampCore, LampLobe));
            m.SetVector("_Air", new Vector4(AirLeak, AirOpen, AirGain, AirFace));
            m.SetFloat("_Gain", LampGain);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 風防の雨 --------------------------------------------------------

        /// <summary>ワイパーの軸の高さ。風防の中心から下へ。下の縁の 2 cm 上</summary>
        const float WiperY = -0.295f;
        /// <summary>ワイパーの軸の x。運転席側と助手席側。二枚とも同じ向きに振る</summary>
        const float WiperNear = 0.36f;
        const float WiperFar = -0.30f;
        /// <summary>ガラスの外面から羽根までと、羽根から水の板まで。風防の面に立てた法線の向き</summary>
        const float BladeOut = 0.018f;
        const float FilmOut = 0.034f;

        // ---- 脇の窓の雨 ------------------------------------------------------

        /// <summary>脇の窓の水の板を張る x。側面のガラス（0.958）の 6 mm 外</summary>
        const float SideFilmX = 0.964f;
        /// <summary>脇の窓の間口。ドアのガラス（<c>Flanks</c> が張る面）と同じ</summary>
        const float SideFilmFront = 0.86f;
        const float SideFilmBack = -0.60f;
        /// <summary>
        /// 脇の窓の水が流れる向き。度。0 なら真後ろ、大きいほど後ろ下がりに寝る。
        ///
        /// 風防の水は上（＝走行風の下流）へ引かれるが、脇の窓では下流が真後ろになる。
        /// 板の uv の縦をそちらへ倒して張るので、同じシェーダーのまま筋が後ろへ走る。
        /// 実際の窓の筋も真横ではなく、重さのぶんだけ後ろ下がりに流れる
        /// </summary>
        const float SideFilmRake = 18f;

        /// <summary>
        /// 風防に付いた雨と、それを拭うワイパー。**帯 1（夜の高速）でだけ出す。**
        ///
        /// 入れ物ひとつ（Rain）に収めて伏せておく。出し入れは DriveDirector がやる。
        ///
        /// **車の外に粒を降らせない。** 運転席からの一人称なので、走行中の画面は
        /// ほとんど風防で埋まっている。目から 1 m 先を落ちる粒は 1 フレームで画面を
        /// 横切るから 427 × 240 では線にもならず、粒を増やすほど WebGL が重くなるだけになる。
        /// 雨が降っていると分かるのはガラスの側で、乗った水と羽根の二つで足りる。
        ///
        /// 水は板一枚（<see cref="Pane"/>）で、粒も筋も薄膜も画素ごとに起こす
        /// （<c>HalfAware/Screenwater</c>）。絵も持たないし、粒ひとつに mesh も立てない。
        /// 三角は水の板が 2 枚、羽根が 120 枚ずつで、合わせて 242 枚しかない。
        ///
        /// **羽根は伏せたところではボンネットの陰に隠れる。** 軸を下の縁へ置いてあるので、
        /// 伏せた角（-84 度）では道の見え始める縁（<see cref="SightY"/>）よりずっと下にあり、
        /// 振り上げるにつれてボンネットの向こうから現れる。実際の車もそう見える。
        /// 見直し 14 が測っているのはこの伏せた姿勢の方
        /// </summary>
        static void Rain(Transform parent)
        {
            var rain = Child(parent, "Rain");
            rain.localPosition = GlassAt;
            rain.localRotation = GlassLean;

            // 羽根。腕も羽根も 1 つの mesh に焼いて、2 本で使い回す。
            // レンダラーを増やさないよう、塗った鉄でまとめて塗る。夜の空（0.115）より
            // 暗く沈むので、空を横切っても路面を横切っても影として読める
            var shape = Shape("CarWiper", 2.2f, Blade);
            var arms = new Transform[2];
            for (var i = 0; i < arms.Length; i++)
            {
                var arm = Piece(rain, "Wiper" + i, shape, Mat("CarSteel"));
                arm.localPosition = new Vector3(i == 0 ? WiperNear : WiperFar, WiperY, BladeOut);
                arms[i] = arm;
            }

            // 水。ガラスと同じ間口に板を一枚。羽根より外へ置く。
            // 内へ入れると羽根が水に霞んで、ガラスの内側を拭いているように見える
            var film = Piece(rain, "Film", Pane("CarWater", GlassSize.x, GlassSize.y), WaterMat());
            film.localPosition = new Vector3(0f, 0f, FilmOut);

            // 脇の窓の水。**風防だけでは雨が降っていることが伝わらない。**
            //
            // 走行中に脇を向くと、そこにあるのは乾いたガラス一枚と流れる沿道だけで、
            // 前を向いたときだけ雨が降っていることになっていた。実際の車は
            // 脇の窓にも水が付くし、羽根が無いぶん拭われないまま溜まり続ける。
            //
            // 入れ物を一枚挟んで、風防の傾きを打ち消しておく。Rain は風防の面に
            // 合わせて据えてあるので、その子に置くと脇の窓まで 10 度傾く
            var sides = Child(rain, "Sides");
            sides.localRotation = Quaternion.Inverse(GlassLean);
            sides.localPosition = Quaternion.Inverse(GlassLean) * -GlassAt;
            var sideFilm = SideFilm("CarSideWater",
                SideFilmFront - SideFilmBack, PaneHigh - PaneLow, SideFilmRake);
            var sideMat = SideWaterMat();
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var pane = Piece(sides, "SideFilm" + s, sideFilm, sideMat);
                pane.localPosition = new Vector3(side * SideFilmX,
                    (PaneLow + PaneHigh) * 0.5f, (SideFilmFront + SideFilmBack) * 0.5f);
            }

            var run = rain.GetComponent<Wipers>();
            if (run == null) run = rain.gameObject.AddComponent<Wipers>();
            var so = new SerializedObject(run);
            Fill(so.FindProperty("blades"), arms);
            so.ApplyModifiedPropertiesWithoutUndo();
            // 伏せた角へ据えておく。組み上がった場面はまだガレージの中で、
            // 雨も降っていない。エディタで開いたときの見え方も組み立ての責任
            run.Set(-Mathf.PI * 0.5f, 0f);
            rain.gameObject.SetActive(false);
        }

        /// <summary>羽根の付け根。軸からの距離。m。ここから外が羽根、内が腕</summary>
        const float BladeFrom = 0.178f;
        /// <summary>羽根ぜんたいの幅（背骨とゴムの縁を合わせた差し渡し）。m</summary>
        const float BladeWide = 0.028f;
        /// <summary>腕の先の幅。m。羽根との差がそのまま段差になる</summary>
        const float ArmThin = 0.010f;

        /// <summary>
        /// ワイパー 1 本。軸を原点に、真上（+y）へ伸ばす。振るのは <see cref="Wipers"/>。
        ///
        /// **一本の棒にしない。** 前は軸の覆い・腕・羽根の三つに割ってあったが、
        /// 腕（幅 18 mm）と羽根（幅 26 mm）の差が実画面で 0.4 画素しかなく、しかも
        /// 二つが軸から先まで重なって伸びていたので、下から上まで同じ太さの暗い線が
        /// 一本通っているだけだった。風防に入った罅にしか見えない。
        ///
        /// **読ませるのは太さの段差ひとつ。** 運転席から羽根までは 0.8 m ほどで、
        /// 描画は 427 × 240・画角 70 度なので 1 mm が 0.21 画素にあたる。
        /// 腕を 10 mm（2.1 画素）まで細らせ、羽根を 28 mm（6.0 画素）に太らせると、
        /// 軸から途中までが細い線、そこから先が太い線になる。段差は 2.8 倍で、
        /// この解像度でも一目で分かる。**腕と羽根を重ねない。** 重ねると
        /// 太い方が影絵を決めてしまい、段差そのものが消える。
        ///
        /// 割り方は実物のとおり。
        ///   - 軸の覆い。軸の根を隠す低い塊
        ///   - 腕。軸から立ち上がり、先へ行くほど細る（16 mm → 10 mm）
        ///   - 爪。腕が羽根を掴むところ。腕より太い塊を一つ挟んで、継ぎ目を作る
        ///   - 背骨とゴムの縁。羽根を太さの違う二本の棒に割り、あいだに 7 mm の隙を空ける。
        ///     空を背にすると隙が明るく抜けて、中の空いた枠として読める。実物の羽根も
        ///     背骨の下にゴムが吊ってあって、あいだは抜けている
        ///   - 押さえ。背骨とゴムを繋ぐ橋を三つ。羽根の影絵に節が付いて、
        ///     滑らかな腕と質感が分かれる
        ///
        /// **ガラスに寝かせる。** ガラスに垂直な向きの差し渡しは 20 mm に収めてある
        /// （前は 29 mm あって角材に見えた）。ゴムの縁だけガラスの面（<see cref="BladeOut"/>
        /// から -8 mm）まで下ろして当て、背骨はその上に浮かせる。
        /// いちばん高い軸の覆いでも水の板（<see cref="FilmOut"/>）へは届かない。
        ///
        /// 羽根の長さは <see cref="Wipers.Reach"/> - <see cref="BladeFrom"/> で 0.32 m。
        /// 風防の丈（0.63）の半分強にあたり、払い終わりでも上の縁までは届かない
        /// </summary>
        static void Blade(Bank bank)
        {
            // 軸の覆い。羽根より低く、幅も羽根と同じに留める。
            // 前は 44 mm あって、ここだけ大きな塊が乗っているように見えた
            bank.Box(new Vector3(0f, 0.010f, 0.002f), new Vector3(BladeWide, 0.040f, 0.020f));
            // 腕。根は太く、先は細く。二段に割って細りを出す
            bank.Box(new Vector3(0f, 0.065f, 0.003f), new Vector3(0.016f, 0.086f, 0.011f));
            bank.Box(new Vector3(0f, 0.137f, 0.002f), new Vector3(ArmThin, 0.070f, 0.009f));
            // 爪。腕が羽根を掴むところ。ここで太さが変わる
            bank.Box(new Vector3(0f, BladeFrom - 0.001f, 0.001f),
                new Vector3(BladeWide, 0.030f, 0.016f));

            // 羽根。背骨とゴムの縁の二本。あいだの 7 mm は抜く
            var span = Wipers.Reach - BladeFrom;
            bank.Box(new Vector3(0.008f, BladeFrom + span * 0.5f - 0.003f, 0.004f),
                new Vector3(0.012f, span - 0.006f, 0.008f));
            bank.Box(new Vector3(-0.0095f, BladeFrom + span * 0.5f + 0.003f, -0.0025f),
                new Vector3(0.009f, span - 0.014f, 0.011f));
            // 押さえ。背骨とゴムを繋ぐ橋。羽根の丈を四つに割ったところへ
            for (var i = 1; i <= 3; i++)
                bank.Box(new Vector3(0f, BladeFrom + span * i / 4f, 0.001f),
                    new Vector3(BladeWide, 0.016f, 0.013f));
            // 羽根の先。少し細めて落とす
            bank.Box(new Vector3(0f, Wipers.Reach - 0.008f, 0.001f),
                new Vector3(0.018f, 0.016f, 0.011f));
        }

        /// <summary>
        /// 立てた板 1 枚。uv は板の中心を原点にしたメートルで振る。
        ///
        /// <see cref="Card"/> と同じ手だが、あちらは寝かせた板で、uv も左下を原点に取る。
        /// 風防の水は軸からの角と距離で拭い跡を出すので、原点が中心に来ていないと
        /// ワイパーの軸の位置を渡せない。
        ///
        /// **四隅だけで張る。** 中を割ると、割った先の頂点が道の見える縁
        /// （<see cref="SightY"/>）とガラスの上の縁のあいだに落ちて、見直し 14 が鳴る
        /// </summary>
        static Mesh Pane(string name, float wide, float high)
        {
            var hw = wide * 0.5f;
            var hh = high * 0.5f;
            return Shape(name, 1f, b => b.Patch(
                new Vector3(hw, -hh, 0f), new Vector3(-hw, -hh, 0f),
                new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f),
                new Vector2(hw, -hh), new Vector2(-hw, -hh),
                new Vector2(-hw, hh), new Vector2(hw, hh)));
        }

        /// <summary>
        /// 脇の窓に張る水の板 1 枚。x = 0 の面に立てる。置く側が左右へずらす。
        ///
        /// **uv は板の形とは別に振る。** シェーダーは uv の縦を「走行風の下流」として
        /// 水を流すので、真上へ振ると脇の窓でも水が上へ昇ってしまう。縦を後ろへ
        /// rake 度倒した向き（<see cref="SideFilmRake"/>）に取ると、同じシェーダーのまま
        /// 筋が後ろ下がりに走る。縦横とも長さ 1 の向きなので、uv の目盛りは
        /// 風防と同じくメートルのままになる。
        ///
        /// 四隅だけで張る。<see cref="Pane"/> と同じ理由で、中を割る必要が無い
        /// </summary>
        static Mesh SideFilm(string name, float deep, float high, float rake)
        {
            var hz = deep * 0.5f;
            var hy = high * 0.5f;
            var a = rake * Mathf.Deg2Rad;
            // uv の縦。後ろ（-z）へ、少し下（-y）へ
            var up = new Vector3(0f, -Mathf.Sin(a), -Mathf.Cos(a));
            // uv の横。縦と直交するもう一本
            var across = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
            var corner = new[]
            {
                new Vector3(0f, -hy, -hz), new Vector3(0f, -hy, hz),
                new Vector3(0f, hy, hz), new Vector3(0f, hy, -hz)
            };
            var uv = new Vector2[4];
            for (var i = 0; i < 4; i++)
                uv[i] = new Vector2(Vector3.Dot(corner[i], across), Vector3.Dot(corner[i], up));
            return Shape(name, 1f, b => b.Patch(
                corner[0], corner[1], corner[2], corner[3], uv[0], uv[1], uv[2], uv[3]));
        }

        /// <summary>
        /// 風防に乗った水のマテリアル。
        ///
        /// 色は線形で置く（<see cref="Paint"/> と同じ）。
        ///
        /// **地の色を夜の空（0.115）から離しすぎない。** 粒を空よりずっと明るくしていた頃は、
        /// 白くて丸いものが点々と浮いて、雨ではなく雪にしか読めなかった。
        /// 夜のガラスの水は後ろの灯りを歪めて見せるもので、自分で光るものではない。
        ///
        /// **ただし落としすぎると、今度は雨が降っていないように見える。** 0.175 まで下げた
        /// ときがそれで、水はガラスに在るのに土砂降りには読めなかった。地の色は 0.186 まで
        /// 戻し、足りないぶんは明るさではなく**量と動き**で出す。丸い塊に戻さない限り、
        /// 空の 1.6 倍でも雪には見えない。
        ///
        /// にじみ（_Smear）は後ろの明るさに掛かるので、暗い空の上では何も起きない。
        /// 街灯や対向車の前照灯を水が横切るところでだけ効く。
        ///
        /// 粒は筋に譲る。1 画素が 4.7 mm ぶんある解像度では、実物どおりの
        /// 2〜5 mm の水滴は円として描けないので、砂目（_Sand）として敷き、
        /// 筋（_Up / _Down）とちぎれて飛ぶ水（_Dart）を主役にする。
        ///
        /// **筋は細く、暗く。** 一度「流れが分からない」と言われて半幅を倍（6.2 mm）に
        /// 取り、水の色と _Smear を上げたが、今度は「気持ち悪い。なんでそんな白いの」と
        /// 差し戻された。_Smear は後ろの色に掛け算するので、路面のような中間の明るさの上では
        /// 筋がそのまま白へ飛ぶ。夜のガラスの水は自分で光らない。
        /// 半幅は 3 mm ほどへ戻し、水の色は空（0.115）より暗く、
        /// 筋の頭も空をわずかに上回る程度に留めてある。列の間隔も詰めて本数で見せる。
        ///
        /// ---- 三度目の詰め -----------------------------------------------------
        ///
        /// **足りなかったのは筋の太さだった。** 半幅 3.0 mm で置いていた頃、筋は
        /// いちばん太いところでも 4.2 mm しかなく、画素（3.8 mm）とほとんど同じ幅で、
        /// どの画素に載るかで出たり消えたりしていた。**動いてはいたが、動いているものが
        /// 画素の網を渡り歩くたびに明滅していたので、流れではなく雑音に見えていた。**
        /// 速さや量をいくら足しても、一本が画素に載り切らない限りここは変わらない。
        ///
        /// 半幅を倍に取ると、いちばん細い筋でも 8.7 mm ＝ 2.3 画素になり、
        /// 一本が隣の画素へ渡っていくところが追える。列の間隔も詰めて本数を増やし、
        /// 速さを 1.70 から 2.30 m/s（1 フレームで 20 画素）へ上げてある
        /// </summary>
        static Material WaterMat()
        {
            var m = WetGlass("CarWater");
            m.SetColor("_BaseColor", new Color(0.104f, 0.106f, 0.112f, 1f));
            m.SetColor("_Glint", new Color(0.126f, 0.128f, 0.136f, 1f));
            m.SetFloat("_Veil", 0.13f);
            m.SetFloat("_Sand", 0.36f);
            m.SetFloat("_Rill", 0.90f);
            m.SetFloat("_Spray", 0.46f);
            m.SetFloat("_Smear", 0.62f);
            m.SetFloat("_Grit", 150f);
            m.SetFloat("_Creep", 0.52f);
            // **走行風で外へ開く量を 0.22 から 0.30 へ。** 太らせた筋を真っ直ぐ上へ
            // 走らせると、間口を縦に抜ける平行線になってガラスの水ではなく
            // 外を落ちる雨に見える。上へ行くほど外へ逃がすと、筋が扇に開いて
            // 「風に引かれてガラスを伝っている」向きが出る
            m.SetFloat("_Fan", 0.30f);
            m.SetFloat("_Wiped", 1f);
            m.SetFloat("_Fall", 1f);
            // 列の間隔 / 一本の長さ / 流れる速さ / 半幅。どれもメートル。
            // 後ろへ引かれる筋は細く長く速く、落ちる筋は太く短く遅い。
            // ちぎれて飛ぶ水はいちばん細く、いちばん速い（間口を 0.20 秒で抜ける）。
            // **半幅はどれも 1 画素（3.8 mm）を超えるところまで太らせてある。**
            // 細いほど本物らしいが、載らない太さの筋は明滅にしかならない
            m.SetVector("_Up", new Vector4(0.112f, 0.50f, 2.45f, 0.0028f));
            m.SetVector("_Down", new Vector4(0.196f, 0.46f, 0.56f, 0.0036f));
            m.SetVector("_Dart", new Vector4(0.042f, 0.125f, 3.10f, 0.0022f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 脇の窓に乗った水のマテリアル。**風防とは三つのことが違う。**
        ///
        ///   - 拭われない（_Wiped 0）。脇の窓に羽根は無い。羽根の角は
        ///     Shader.SetGlobal で場面ぜんたいに置かれるので、切っておかないと
        ///     風防の扇がそのまま脇の窓にも出る
        ///   - 落ちる筋をほとんど出さない（_Fall）。板の uv の縦は後ろへ寝かせてあるので、
        ///     落ちる筋は前上がりに走ることになる。重さで垂れるぶんは砂目が受け持つ
        ///   - 溜まりきったまま拭われないので、風防より水が厚い。薄膜と砂目を増やし、
        ///     後ろへ引かれる筋（_Up）を長く速くする。走行風に持って行かれた水が
        ///     窓の後ろの端まで一本で抜けていくのが、脇の窓でいちばん目に入る
        ///
        /// 地の色は風防と揃える。同じ雨がどちらのガラスにも当たっている
        /// </summary>
        static Material SideWaterMat()
        {
            var m = WetGlass("CarSideWater");
            m.SetColor("_BaseColor", new Color(0.104f, 0.106f, 0.112f, 1f));
            m.SetColor("_Glint", new Color(0.126f, 0.128f, 0.136f, 1f));
            m.SetFloat("_Veil", 0.19f);
            m.SetFloat("_Sand", 0.44f);
            m.SetFloat("_Rill", 0.92f);
            m.SetFloat("_Spray", 0.50f);
            m.SetFloat("_Smear", 0.62f);
            m.SetFloat("_Grit", 150f);
            // 砂目も後ろへ引かれる。板の uv の縦がそちらを向いている
            m.SetFloat("_Creep", 0.70f);
            m.SetFloat("_Fan", 0.14f);
            m.SetFloat("_Wiped", 0f);
            m.SetFloat("_Fall", 0.18f);
            m.SetVector("_Up", new Vector4(0.098f, 0.78f, 3.20f, 0.0029f));
            m.SetVector("_Down", new Vector4(0.196f, 0.46f, 0.40f, 0.0032f));
            m.SetVector("_Dart", new Vector4(0.038f, 0.150f, 4.20f, 0.0023f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>水の乗ったガラス 1 枚ぶんのマテリアルの器。値は呼ぶ側が入れる</summary>
        static Material WetGlass(string name)
        {
            var shader = Shader.Find("HalfAware/Screenwater");
            if (shader == null) Debug.LogWarning("HalfAware/Screenwater が見つからない。ガラスに水が乗らない");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
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
        /// <summary>
        /// 風防のガラス。**ドアのガラスや隣の車と同じにしない。**
        ///
        /// 計器の裏の灯り（DialLamp）は運転席の目と x が同じ 0.38 にあり、
        /// 10 度寝た風防に映った像がちょうど視線の正面へ乗る。艶 0.85 の鏡面ローブが
        /// そこで丸い橙の塊になり、道の消失点に光源が浮いているように見えていた。
        /// オーナーの「前方にオレンジ色の光源が見える。丸いやつ」がこれ。
        ///
        /// 灯りの側は動かせない。あれはハンドルの上側に段を付けるために測って置いた
        /// ものなので（<see cref="Car"/> の長い説明）、ずらすと手元の見え方を測り直す
        /// ことになる。艶を落とす手もあるが、CarGlass はドアのガラスと隣の車でも
        /// 使い回しているので、そちらまで曇る。
        ///
        /// 風防だけ別のマテリアルにして、**灯りからの鏡面を切る**。地の色も透け方も
        /// CarGlass と同じなので、見た目で変わるのはあの塊が消えることだけ。
        /// 夜の場面なので、切って失うのは日射しのぎらつきくらいしかない
        /// </summary>
        static Material PaneMat()
        {
            var m = Mat("CarPane");
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

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
            trim.Box(PodAt, new Vector3(0.560f, 0.140f, 0.160f), PodLean);
            trim.Box(PodAt + new Vector3(0f, 0.1385f, -0.0955f),
                new Vector3(0.600f, 0.018f, 0.160f), PodLean);
            Panel(dials, PodFace, DialWide, DialHigh, PodLean);
        }

        /// <summary>
        /// メーターの塊の中心と傾き。**ここと <see cref="FuelDial"/> の二箇所で使うので
        /// 数を二重に持たない。** 片方だけ動かすと、立体の燃料計が絵の中の燃料計から外れる。
        ///
        /// **z 0.615 から 0.675 へ引いた。** 手前に置いていた頃は、ハンドルの輪の上側
        /// （68 度寝ているので上の縁がいちばん前へ出る。y 1.301 / z 0.564）が
        /// 計器の面（同じ高さで z 0.525）を 39 mm 貫いていて、輪の角が盤の絵の中から
        /// 生えていた。塊を 60 mm 奥へ引くと輪の上側との隙間が 22 mm 開く。
        ///
        /// **引けるのはここまで。** 庇の上端（1.4866）と道の見える縁
        /// （<see cref="SightY"/>）の隙間が、奥へ引くほど詰まる（縁は 1 m につき 0.15 下がる）。
        /// 0.675 で残りは 21 mm。見直し 14 が組むたびに測る。
        ///
        /// 読みやすさはむしろ良くなる。目から輪の上の縁を掠めた線が盤と交わる高さが
        /// 1.343（盤の真ん中、二つの計器の只中）から 1.310（計器の丸の下端）へ下がり、
        /// 輪が計器を横切らなくなる
        /// </summary>
        static readonly Vector3 PodAt = new Vector3(WheelAt.x, 1.320f, 0.675f);
        static readonly Quaternion PodLean = Quaternion.Euler(14f, 0f, 0f);
        /// <summary>計器の絵を貼る面の中心。塊の運転席を向いた側</summary>
        static Vector3 PodFace { get { return PodAt + PodLean * new Vector3(0f, 0f, -0.0825f); } }

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
        /// ガレージのドアの印（x 0.92）を食わない。
        ///
        /// **運転席の側は開くドアへ載せる。** 内張りは外板と一緒に振れるもので、
        /// 車体に残すと、開いたドアの跡に内張りだけが宙に立つ
        /// </summary>
        static void DoorCard(Bank trim, Bank steel, Bank gap, float side, Leaf leaf)
        {
            if (Leaf.Mine(side)) { trim = leaf.Trim; steel = leaf.Steel; gap = leaf.Gap; }
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

        // ---- 調べられる物 ----------------------------------------------------

        /// <summary>
        /// 調べる対象そのものの形。<see cref="BuildDrive.Items"/> が立てているのは
        /// 判定点と印だけで、そこに肝心の物が無かった。画面から何を調べているのかが
        /// 読めるよう、判定点の位置に物を置く。
        ///
        /// **判定点は動かさない。** どれも文面と帯の並びに結び付いていて、高さも
        /// 「判定点は物の少し手前に置く」取り決めで決まっている。合わせるのは物の側。
        ///
        /// **印を食わないこと。** 印は判定点の 0.17 m 上に立つ。物をそこへ被せると
        /// 印が埋まる（見直し 4）。物はどれも印の下か、印から横へ外したところに収めてある。
        ///
        /// **灯りは足さない。** 帯 0〜2 の車内にあるのは計器の裏の一つだけで、
        /// 形は明暗の段で見せる。暗い内装（0.125）の上に紙（0.33）・塗った鉄（0.216）・
        /// 端子（0.42）を段違いに置けば、灯りを増やさずに輪郭が残る。燃料計の針と目盛りだけは
        /// メーターの絵（<see cref="DialMat"/>）と同じ Unlit で、夜も自分で光る。
        ///
        /// **細かい造作は画素に届かない。** 描画は 427 × 240、画角は縦 70 度なので
        /// 1 ラジアンが 171 画素にしかならない。目から 0.9 m の助手席では 1 mm が 0.19 画素で、
        /// 5 mm を切る造作は明暗が混ざって消える。ここの寸法はどれもその物差しで決めてある
        /// </summary>
        static void Fittings(Transform parent, Bank trim, Bank steel, Bank gap, Bank lens, Bank tail, Leaf leaf)
        {
            // 基板と端子と紙と布。どれも車体の素材の表（Tone）に無いので、ここで色を結ぶ
            var board = new Bank { Texel = 2.6f };
            var gold = new Bank { Texel = 2.0f };
            var paper = new Bank { Texel = 1.6f };
            var cloth = new Bank { Texel = 1.8f };
            // 鍍金。窓の回し把手とポケットの口の縁
            var plate = new Bank { Texel = 2.4f };
            // 燃料計の目盛りと針。Unlit なので入れ物を分ける
            var scale = new Bank { Texel = 1f };
            var pointer = new Bank { Texel = 1f };

            ChipBundle(board, gold, steel);
            LogCopy(paper, gap);
            CoatOnSeat(cloth, gap, plate, paper);
            RearMirror(trim, steel, gap, lens);
            RadioSet(steel, gap, paper, tail);
            FuelDial(trim, gap, scale, pointer);
            CigarettePack(parent);
            WindowCrank(plate, steel, gap, leaf);

            // 基板。暗い緑。内装（0.125）より暗く落として、助手席の座面（0.140）から切る
            board.Emit(parent, "CarBoard", ItemMat("CarBoard", new Color(0.088f, 0.108f, 0.086f), 0.34f),
                false, Generated);
            // 端子。**車内でいちばん明るい面。**
            //
            // 助手席まで届く灯りが一つも無いので、実際に撮って測ると、
            // 内装（0.125）がそこでは 255 段階の 7 にしかならない。0.42 で置いた端子は 24 で、
            // 段は付くが「暗いところに暗い物がある」ところから出られなかった。
            // 塗った鉄（0.216）の 3.6 倍まで上げると 44 になり、夜の助手席で最初に目へ入る
            gold.Emit(parent, "CarGold", ItemMat("CarGold", new Color(0.780f, 0.600f, 0.250f), 0.55f),
                false, Generated);
            // 紙。褪せた写真とラジオの目盛りと煙草の箱も同じ素材で貼る。
            // 端子と同じ事情で 0.335 から上げた。実際の紙の反射率（0.7 前後）の内なので、
            // 朝の帯（実測 107 × 反射率）でも 66 までしか行かず、白く飛ばない
            paper.Emit(parent, "CarPaper", ItemMat("CarPaper", new Color(0.620f, 0.596f, 0.530f), 0.05f),
                false, Generated);
            // 上着。主人公の革の上着だが、袖（Sleeve 0.055）のままでは暗すぎて、
            // 撮ってみると畳みもポケットも一切見えない黒い塊になった。
            // 座面（0.140）より暗いところは変えずに、畳みが読める 0.078 まで持ち上げてある
            cloth.Emit(parent, "CarCloth", ItemMat("CarCloth", new Color(0.078f, 0.072f, 0.076f), 0.16f),
                false, Generated);
            // 鍍金。**塗った鉄（0.216）では足りない所にだけ使う。**
            // 運転席のドアには計器の裏の灯りが 0.62 m 先から斜めに届くだけで、
            // 実際に撮って測ると内張りが 0、塗った鉄の把手が 14 にしかならない。
            // 段は付いているが、夜の画面で 14 は「そこに何かある」までしか言えない。
            // この年式の車の回し把手は鍍金なので、0.46 で置き直すと 29 になる
            plate.Emit(parent, "CarChrome", ItemMat("CarChrome", new Color(0.455f, 0.460f, 0.470f), 0.62f),
                false, Generated);
            scale.Emit(parent, "CarGaugeMark", ItemLamp("CarGaugeMark", new Color(0.520f, 0.470f, 0.338f)),
                false, Generated);
            pointer.Emit(parent, "CarGaugeNeedle", ItemLamp("CarGaugeNeedle", new Color(0.593f, 0.215f, 0.024f)),
                false, Generated);
        }

        /// <summary>
        /// 調べる対象だけが使う素材。<see cref="Mat"/> を通さないのは、
        /// あちらが色の表（<see cref="Tone"/>）と絵の名前を BuildDrive 本体に持っているため。
        /// 絵は持たず、色と艶だけを結ぶ
        /// </summary>
        static Material ItemMat(string name, Color col, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 燃料計の目盛りと針の素材。メーターの絵（<see cref="DialMat"/>）と同じく Unlit で貼る。
        ///
        /// **灯りを増やさずに「夜に自分で光るもの」を置けるのはこの手だけ。** 色がそのまま
        /// 画面に出るので、絵の中の目盛り（sRGB 238,228,198）と針（252,160,56）に
        /// <see cref="DialGain"/> を掛けた値を線形で置いてある。立体の燃料計だけ明るさが
        /// 揃わないと、計器盤の中でそこだけ浮く
        /// </summary>
        static Material ItemLamp(string name, Color col)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 助手席のメモリーチップの束。判定点 (-0.42, 1.15, -0.02)。
        ///
        /// 文面は「売れ残りのメモリーチップの束が転がっている」なので、揃えて置かず、
        /// 座面の上へ斜めに投げ出す。座面の天板は 1.05 で、束はそこへ沈めてある。
        ///
        /// 束は板 3 枚。**4 枚 × 厚み 7 mm で組んだときは層が混ざって一つの塊に戻った。**
        /// 目から 0.93 m だと 1 mm が 0.19 画素で、7 mm の板は 1.3 画素にしかならない。
        /// 10 mm へ厚くして枚数を減らすと、1 枚が 2 画素になって層として残る。
        /// **それでも読ませているのは層ではなく、暗い板を横切る明るい帯と端子の列の方。**
        /// 帯（塗った鉄）は幅 20 mm で 3.7 画素、端子は 18 mm ごとで 3.4 画素ある
        /// </summary>
        static void ChipBundle(Bank board, Bank gold, Bank steel)
        {
            var at = new Vector3(-0.42f, 1.080f, -0.02f);
            var toss = Quaternion.Euler(0f, 18f, -8f);
            for (var i = 0; i < 3; i++)
                board.Box(at + toss * new Vector3(0f, (i - 1) * 0.0170f, 0f),
                    new Vector3(0.112f, 0.0100f, 0.068f), toss);
            // 金の端子。いちばん上の板の手前の縁に並べる。**この列が無いと板の束にしか見えない**
            for (var k = 0; k < 4; k++)
                gold.Box(at + toss * new Vector3(-0.045f + k * 0.030f, 0.0230f, -0.0245f),
                    new Vector3(0.018f, 0.0030f, 0.017f), toss);
            // 束を留めた帯。暗い板の上を明るい帯が一本横切る。
            // 真ん中ではなく奥へ 12 mm 寄せる。揃えて留めたのではなく、留めたまま転がっている
            steel.Box(at + toss * new Vector3(0f, 0.0225f, 0.012f), new Vector3(0.115f, 0.0035f, 0.020f), toss);
            steel.Box(at + toss * new Vector3(0f, -0.0225f, 0.012f), new Vector3(0.115f, 0.0035f, 0.020f), toss);
            for (var i = 0; i < 2; i++)
                steel.Box(at + toss * new Vector3(i == 0 ? -0.0580f : 0.0580f, 0f, 0.012f),
                    new Vector3(0.0035f, 0.048f, 0.020f), toss);
        }

        /// <summary>
        /// アクセスログの写し。判定点 (-0.42, 1.17, 0.14)。チップの束の 0.16 奥、同じ座面の上。
        ///
        /// **折り目で紙に見せる。** 平らな板を一枚置いても座面の染みと区別が付かない。
        /// 半分を 17 度起こすと、上を向いた面と起きた面に明暗の段が付いて、そこで紙になる。
        ///
        /// 字は一行ずつ引かない。目から 0.89 m では 1 行（3 mm）が 0.6 画素にしかならず、
        /// 引いても紙が灰色に濁るだけで終わる。太さ 8 mm・間 10 mm の細い帯を並べて、
        /// 一本を「町の名」として短く太くしてある
        /// </summary>
        static void LogCopy(Bank paper, Bank gap)
        {
            var at = new Vector3(-0.42f, 1.0525f, 0.14f);
            var lay = Quaternion.Euler(0f, -14f, 0f);
            var tilt = lay * Quaternion.Euler(17f, 0f, 0f);
            var top = at + lay * new Vector3(0f, 0.0082f, -0.0268f);
            // 下に重なったもう一枚。縁がずれて覗くだけで、束ねた写しに見える
            paper.Box(at + lay * new Vector3(0.010f, -0.0020f, 0.004f),
                new Vector3(0.146f, 0.0020f, 0.108f), lay * Quaternion.Euler(0f, 5f, 0f));
            // 手前の半分は座面に伏せたまま、奥の半分を折り目から起こす
            paper.Box(at + lay * new Vector3(0f, 0f, 0.028f), new Vector3(0.150f, 0.0025f, 0.056f), lay);
            paper.Box(top, new Vector3(0.150f, 0.0025f, 0.056f), tilt);
            // 伏せた側の文字。細い帯を三本
            for (var k = 0; k < 3; k++)
                gap.Box(at + lay * new Vector3(-0.004f, 0.0024f, 0.012f + k * 0.018f),
                    new Vector3(0.108f, 0.0010f, 0.008f), lay);
            // 起こした側。いちばん上だけ短く太い。ここが「エディンバラから少し離れた田舎町の名」
            gap.Box(top + tilt * new Vector3(-0.030f, 0.0024f, -0.019f),
                new Vector3(0.062f, 0.0010f, 0.012f), tilt);
            for (var k = 0; k < 2; k++)
                gap.Box(top + tilt * new Vector3(-0.004f, 0.0024f, 0.002f + k * 0.018f),
                    new Vector3(0.108f, 0.0010f, 0.008f), tilt);
        }

        /// <summary>
        /// 座席に置いた上着とそのポケット。判定点 (-0.10, 1.07, -0.30)。
        ///
        /// **判定点の真上には物を置けない。** 運転席の目から判定点へ線を引くと、
        /// 腕組みした左の二の腕（<see cref="Folded"/>）に当たる。実際に撮っても、
        /// 画面の真ん中は袖の黒い塊で埋まっていた。線が通るのを一点ずつ測ると、
        /// 抜けるのは x -0.35 から -0.21、z -0.17 から 0.05 の範囲で、
        /// そこは助手席の座面の右半分にあたる。上着の身頃とポケットはそこへ置き、
        /// 袖だけを判定点の方へ垂らして、上着が判定点まで届いていることにしてある。
        ///
        /// **チップと写しを食わない。** どちらも同じ座面の x -0.50 から -0.36 に置いてあるので、
        /// 上着は x -0.35 より右だけを使う。**背もたれにも入らない。** 背もたれの前の面は
        /// z -0.175 で、身頃はそれより手前に収める。
        ///
        /// 布は座面（0.140）より暗いので、明るい座面の上に暗い塊として抜ける。
        /// ポケットの口は、暗いままだと布に沈んで消えるので、口の縁に塗った鉄の線を
        /// 一本入れて段を作る。**煙草の箱は文面のもの**で、紙の面（0.62）が
        /// 8 画素ぶん覗くと、そこがポケットだと一目で読める
        /// </summary>
        static void CoatOnSeat(Bank cloth, Bank gap, Bank plate, Bank paper)
        {
            // 身頃。座面の右半分に丸めて置く。左の縁はチップの束（x -0.355 まで）に触れさせない
            cloth.Box(new Vector3(-0.225f, 1.088f, -0.055f), new Vector3(0.195f, 0.086f, 0.200f),
                Quaternion.Euler(0f, -12f, 4f));
            // 畳まれて盛り上がった襟のあたり。写し（x -0.342 まで）の右
            cloth.Box(new Vector3(-0.232f, 1.116f, 0.085f), new Vector3(0.140f, 0.060f, 0.135f),
                Quaternion.Euler(0f, 14f, -6f));
            // 袖。座面の右の縁から判定点の方へ垂らす。下端は変速機の覆いの天板（0.935）に着く
            cloth.Box(new Vector3(-0.115f, 1.020f, -0.215f), new Vector3(0.090f, 0.155f, 0.095f),
                Quaternion.Euler(22f, 0f, 26f));
            cloth.Box(new Vector3(-0.085f, 0.965f, -0.285f), new Vector3(0.085f, 0.075f, 0.105f),
                Quaternion.Euler(12f, -8f, 14f));

            // ポケット。身頃の上を向いた面に付ける。ここだけは線の通るところに置く
            var lean = Quaternion.Euler(-16f, -12f, 4f);
            var pocket = new Vector3(-0.243f, 1.100f, -0.130f);
            cloth.Box(pocket, new Vector3(0.150f, 0.055f, 0.120f), lean);
            // 口。暗い窪みを一本
            gap.Box(pocket + lean * new Vector3(0f, 0.031f, 0.006f),
                new Vector3(0.108f, 0.008f, 0.028f), lean);
            // 口の縁。ここだけ明るくして、布のどこがポケットなのかを出す
            plate.Box(pocket + lean * new Vector3(0f, 0.035f, 0.022f),
                new Vector3(0.104f, 0.006f, 0.008f), lean);
            // 蓋。口の向こうへ寝かせる
            cloth.Box(pocket + lean * new Vector3(0f, 0.031f, -0.026f),
                new Vector3(0.116f, 0.012f, 0.044f), lean);
            // 煙草の箱。口から覗かせる
            paper.Box(pocket + lean * new Vector3(-0.024f, 0.042f, 0.002f),
                new Vector3(0.046f, 0.026f, 0.030f), lean * Quaternion.Euler(-18f, 0f, 0f));
        }

        /// <summary>
        /// ルームミラー。判定点 (0, 1.66, 0.66)。
        ///
        /// 既にあるのは淡いガラスの板 1 枚（y 1.79〜1.87 / z 0.73〜0.75）だけで、
        /// 透けるので向こうの天井が見えていた。板は作り直さず、それを丸ごと包む筐で覆う。
        ///
        /// **鏡面は反射させない。** 映り込みを返す面をここに置くと、WebGL で映すものを
        /// 増やすことになるうえ、夜は映す物が無いので黒い穴が開く。暗い面（0.020）に
        /// 淡い筋（0.43）を二本入れて、「暗い鏡に何かが映っている」ところまでで止める。
        ///
        /// 面を 16 度下へ向けてある。文面の「角度が悪くて、自分の顔は映らない」がそれで、
        /// 目（1.55）より高い 1.82 にある鏡が下を向いていれば、映るのは座面だけになる。
        ///
        /// **印（0, 1.83, 0.66）を食わない。** 筐の手前の端は z 0.706 で、印の 0.046 奥にある。
        /// 高さは <see cref="CheckDrive"/> の SightLid（1.70）より上なので、道は削らない
        /// </summary>
        static void RearMirror(Bank trim, Bank steel, Bank gap, Bank lens)
        {
            var lean = Quaternion.Euler(-16f, 0f, 0f);
            var at = new Vector3(0f, 1.828f, 0.745f);
            // 筐。既にあるガラスの板をそのまま呑み込む大きさにする
            trim.Box(at, new Vector3(0.320f, 0.105f, 0.045f), lean);
            // 鏡面。筐より一回り小さく、縁が枠として残る
            var face = at + lean * new Vector3(0f, 0f, -0.0255f);
            gap.Box(face, new Vector3(0.286f, 0.082f, 0.008f), lean);
            // 映っているもの。細い筋を二本。長さを変えて並べると、模様ではなく映り込みに見える
            lens.Box(face + lean * new Vector3(-0.022f, 0.014f, -0.005f),
                new Vector3(0.170f, 0.009f, 0.004f), lean * Quaternion.Euler(0f, 0f, -3f));
            lens.Box(face + lean * new Vector3(0.062f, -0.019f, -0.005f),
                new Vector3(0.062f, 0.006f, 0.004f), lean * Quaternion.Euler(0f, 0f, -3f));
            // 天井からの腕。風防の上端（y 1.900 / z 0.850）へ向けて後ろ上がりに渡す
            Limb(steel, new Vector3(0f, 1.866f, 0.774f), new Vector3(0f, 1.884f, 0.846f), 0.022f);
            // 風防に留めた座
            trim.Box(new Vector3(0f, 1.886f, 0.856f), new Vector3(0.052f, 0.030f, 0.042f),
                Quaternion.Euler(-10f, 0f, 0f));
        }

        /// <summary>
        /// ラジオ。判定点 (-0.02, 1.19, 0.70)。
        ///
        /// 中央の操作盤（<see cref="Dash"/>）に、暗い面と摘み二つと押しボタンの列は既にある。
        /// 足りないのは目盛りで、それが無いあいだは無地の窪みにしか見えなかった。
        ///
        /// 目から 0.72 m なので、目盛りの帯は 206 mm で 49 画素、刻みは 30 mm ごとに 10 画素、
        /// 針は 5 mm で 1.7 画素になる。**刻みをこれ以上細かくすると帯が一色に潰れる。**
        /// 針だけ尾灯と同じ赤にしてあるのは、暗い車内で淡い帯の上の一点を切るため
        /// </summary>
        static void RadioSet(Bank steel, Bank gap, Bank paper, Bank tail)
        {
            const float x = -0.01f;
            const float y = 1.196f;
            // 枠。既にある暗い面（0.250 × 0.075）を囲う
            for (var i = 0; i < 2; i++)
            {
                var d = i == 0 ? -1f : 1f;
                steel.Box(new Vector3(x, y + d * 0.0425f, DashFace - 0.0515f),
                    new Vector3(0.266f, 0.010f, 0.014f));
                steel.Box(new Vector3(x + d * 0.130f, y, DashFace - 0.0515f),
                    new Vector3(0.010f, 0.095f, 0.014f));
            }
            // 目盛りの帯。紙の面（0.62）を面の上半分へ。
            // 運転席から見ると操作盤の面は 66 度の浅い角度で、帯が縦に潰れて見える。
            // そのぶん明るさで持たせる
            paper.Box(new Vector3(x, y + 0.016f, DashFace - 0.0545f), new Vector3(0.206f, 0.024f, 0.008f));
            // 刻み。端から端まで七つ。真ん中だけ長くして、帯に中心を与える
            for (var k = 0; k < 7; k++)
                gap.Box(new Vector3(x + (k - 3) * 0.030f, y + 0.016f, DashFace - 0.0585f),
                    new Vector3(0.0035f, k == 3 ? 0.020f : 0.013f, 0.004f));
            // 針。「雑音しか拾わない」ので、局と局のあいだに止めてある
            tail.Box(new Vector3(x + 0.052f, y + 0.016f, DashFace - 0.0605f),
                new Vector3(0.0050f, 0.028f, 0.005f));
            // 目盛りの下の格子。摘みと押しボタンのあいだの空きを埋める
            for (var k = 0; k < 5; k++)
                gap.Box(new Vector3(x + (k - 2) * 0.014f, y - 0.026f, DashFace - 0.0505f),
                    new Vector3(0.006f, 0.026f, 0.004f));
        }

        /// <summary>
        /// 燃料計。判定点 (0.46, 1.25, 0.58)。
        ///
        /// メーターの面は絵 1 枚（DriveCarDials.png 512 × 128）で、速度計・回転計と
        /// 小さい計器が二つ描いてある。そのうち右の小さい方（絵の px 366）を燃料計として
        /// 立体で作り直す。絵の上に地の面を被せてから、枠・目盛り・字・針を置き直す。
        ///
        /// **ここだけ立体にすると、計器盤の中で燃料計だけ段が付く。** ほかの計器は絵なので
        /// 平らなまま、これだけ枠が 10 mm 手前へ出て影を落とす。調べる対象がどれなのかが
        /// それで読める。
        ///
        /// 数字ではなく E と F の字を置くのが燃料計の印で、速度計・回転計と読み違えない。
        /// 目から 0.48 m なので字は 16 mm で 5.8 画素にしかならないが、読ませているのは
        /// 字そのものではなく「端に数字ではないものがある」ことの方。
        /// 針は文面どおり「半分を切っている」ところ（0.44）へ倒してある。
        ///
        /// **印（0.46, 1.42, 0.58）を食わない。** 枠の上端は 1.393 で、印の 27 mm 下に収まる
        /// </summary>
        static void FuelDial(Bank trim, Bank gap, Bank scale, Bank pointer)
        {
            var lean = PodLean;
            var right = lean * Vector3.right;
            var up = lean * Vector3.up;
            var outward = lean * Vector3.back;
            // 絵の中の計器の中心。左端（x 0.14）から px 366 ÷ 512 だけ右
            var at = PodFace + right * (0.14f + 366f / 512f * DialWide - WheelAt.x);
            // 絵の中の計器と同じ半径（px 34）
            const float r = 0.0319f;

            // 地の面。絵に描いてある目盛りと針を消す
            Disc(gap, at + outward * 0.004f, outward, r - 0.001f, 8);
            // 枠の輪。隣の計器（絵の縁は x 0.4458 まで）へ 0.6 mm しか掛からない太さに収める
            Hoop(trim, at + outward * 0.007f, outward, r + 0.002f, 0.014f, 0.008f, 10, 0f, 360f);

            // 目盛り。E から F までの 270 度を四つに割る。
            // 両端は字が兼ねるので引かない。重ねて置いたら字が潰れた
            for (var i = 1; i <= 3; i++)
            {
                var way = DialWay(right, up, i / 4f);
                scale.Box(at + outward * 0.006f + way * 0.0235f,
                    new Vector3(0.0042f, 0.0042f, i == 2 ? 0.0085f : 0.0060f),
                    Quaternion.LookRotation(way, outward));
            }
            DialLetter(scale, at, right, up, outward, 0f, true);
            DialLetter(scale, at, right, up, outward, 1f, false);

            // 針。0.44 は真ん中よりわずかに E の側
            var swing = DialWay(right, up, 0.44f);
            pointer.Box(at + outward * 0.009f + swing * 0.0135f, new Vector3(0.0050f, 0.0050f, 0.0245f),
                Quaternion.LookRotation(swing, outward));
            // 針の軸
            Disc(scale, at + outward * 0.011f, outward, 0.0055f, 6);
        }

        /// <summary>
        /// 煙草の箱。判定点は燃料計の隣の天板の上。
        ///
        /// **場面 1・2 に出てくる箱をそのまま使う。** 自室の卓にも小道の卓にも同じ箱が
        /// 置いてあり（<c>BuildAlleyItems.Pack</c>）、寸法も絵も
        /// <c>Assets/Materials/Room/CigarettePack.mat</c> の一枚で通っている。
        /// ここだけ別に紙の箱を組むと、同じ物が場面をまたいで別の形になる。
        /// 面の集まり（<see cref="Bank"/>）ではなく立方体ひとつで置くのは、
        /// 巻いてある絵が立方体の uv を当てにしているため。
        ///
        /// **立てずに寝かせる。** 立てた 88 mm の箱は、目（0.38, 1.55, 0.22）から
        /// 0.52 m しか離れていない天板の上でほとんど計器盤の高さまで伸びていて、
        /// 卓に置いた箱ではなく天板に立てた柱に見えた。寝かせれば背が 22 mm になり、
        /// 天板の上に転がしたものとして読める。
        ///
        /// **置ける場所は見た目より狭い。** 目は計器盤の塊（<see cref="PodAt"/>）より
        /// 0.14 高いだけなので、塊の右脇の天板は塊の右の壁の陰に入る。手前の玉縁
        /// （z 0.51〜0.61・上端 1.30）の上まで戻したところが、箱が丸ごと出る
        /// いちばん燃料計に近い置き場になる。塊を 60 mm 奥へ引いたぶん陰も退いたので、
        /// 立てていた頃と同じ x 0.722 のままで、箱の内側の角（x 0.680）まで見えている
        /// </summary>
        static void CigarettePack(Transform parent)
        {
            var pack = Child(parent, "CigarettePack");
            // **写真立てのあったところ。** 中央の操作盤の縁の上。
            //
            // 計器盤の天板には置けない。判定点そのものはメーターの塊
            // （x 0.10〜0.66 / y 1.247〜1.391）の中で、その前の天板は運転席の目から見ると
            // 塊と庇に丸ごと隠れる。庇の奥の下の縁（y 1.4304 / z 0.4439）を掠める線が
            // 天板と交わるのが z 0.725 で、塊の前の面（z 0.71）とのあいだに 15 mm しか残らない。
            // 見えるのは塊より下と手前、つまりここになる。
            //
            // **ただし操作盤の縁は奥行き 44 mm（z 0.466〜0.510）しかない。**
            // 箱は 86 mm あるので載り切らない。ここにあった写真立ては台紙が
            // 13 mm と薄く、立てていたから収まっていた。寝かせるなら受ける面が要るので、
            // 縁から手前へ小さな棚を出す。棚の天面が箱の座になる
            Tray(parent);

            // 少し振り、手前を持ち上げて傾ける。
            //
            // **平らに寝かせると読めない。** 天板と同じ向きの面は同じだけ灯りを受けるので、
            // 実測で箱 13.9 に対して周りの天板 12.2 ── 1.7 しか違わなかった。
            // ここにあった写真立てが読めていたのは、面を計器の裏の灯り（x 0.38 / y 1.36）へ
            // 向けていたからで、同じ手を当てる。手前を 16 度起こすと上の面が灯りを正面から
            // 受け、箱の縁にも影の線が出る
            var lean = Quaternion.Euler(-16f, -22f, 4f);
            pack.localRotation = lean;

            var size = new Vector3(0.056f, 0.022f, 0.086f);
            // **持ち上げる量は傾きから出す。** 勘で足していたときは、いちばん低い隅が
            // 天面より 5.3 mm 下へ潜って「埋まってる」と差し戻された。
            // 回した箱がどれだけ下へ張り出すかは、三辺それぞれの向きの縦成分で決まる
            var drop = Sink(lean, size);
            pack.localPosition = new Vector3(PackAt.x, TrayTop + drop + 0.0005f, PackAt.z);

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            box.transform.SetParent(pack, false);
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = PackMat();
            // 調べるのは判定点の側の仕事。箱そのものに当たりは要らない
            Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        /// <summary>箱の置き場。x と z だけ使う。y は棚の天面から出す</summary>
        static readonly Vector3 PackAt = new Vector3(0.140f, 0f, 0.545f);

        /// <summary>棚の天面。中央の操作盤の縁（1.260）に板の厚みを足した高さ</summary>
        const float TrayTop = 1.272f;

        /// <summary>
        /// 回した箱が中心からどれだけ下へ張り出すか。m。
        /// 三辺の向きを世界へ移し、その縦成分に半分の長さを掛けて足す
        /// </summary>
        static float Sink(Quaternion turn, Vector3 size)
        {
            return Mathf.Abs((turn * Vector3.right).y) * size.x * 0.5f
                + Mathf.Abs((turn * Vector3.up).y) * size.y * 0.5f
                + Mathf.Abs((turn * Vector3.forward).y) * size.z * 0.5f;
        }

        /// <summary>
        /// 箱を受ける棚。中央の操作盤の縁から手前へ出す。
        ///
        /// 道の見える縁（<see cref="SightY"/>）は z 0.57 で 1.498。棚の天面 1.272 は
        /// その 226 mm 下なので、前は塞がない。ハンドル（軸 x 0.38、外径の半分 0.19）へも
        /// 届かない。棚の右端 x 0.20 は軸から 0.18 離れていて、輪がそこまで来ない高さにある
        /// </summary>
        static void Tray(Transform parent)
        {
            var trim = new Bank { Texel = 1.2f };
            var steel = new Bank { Texel = 1.2f };
            var top = TrayTop;
            // 板。操作盤の面（z 0.510）から手前 0.585 まで
            trim.Box(new Vector3(PackAt.x, top - 0.006f, 0.5475f), new Vector3(0.132f, 0.012f, 0.150f));
            // 縁の立ち上がり。載せたものが転がり落ちない見た目にする
            steel.Box(new Vector3(PackAt.x, top + 0.004f, 0.6175f), new Vector3(0.132f, 0.020f, 0.010f));
            steel.Box(new Vector3(PackAt.x - 0.061f, top + 0.004f, 0.5475f), new Vector3(0.010f, 0.020f, 0.150f));
            trim.Emit(parent, "TrayTrim", Mat("CarTrim"), false, Generated);
            steel.Emit(parent, "TraySteel", Mat("CarSteel"), false, Generated);
        }

        /// <summary>
        /// 煙草の箱の素材。自室のものをそのまま借りる。
        /// 借り物が見つからないときだけ、同じ色の無地を車内の素材として作る
        /// </summary>
        static Material PackMat()
        {
            const string shared = "Assets/Materials/Room/CigarettePack.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(shared);
            if (m != null) return m;
            Debug.LogWarning("自室の煙草のマテリアルが無い: " + shared + "。無地で置く");
            return ItemMat("CarPack", new Color(0.320f, 0.180f, 0.155f), 0.24f);
        }

        /// <summary>
        /// 計器の盤の上の向き。part は 0 が E、1 が F。
        /// 絵（tools/make-drive.py の face）と同じく、左下の 135 度から右回りに 270 度を振る
        /// </summary>
        static Vector3 DialWay(Vector3 right, Vector3 up, float part)
        {
            var a = (135f + 270f * part) * Mathf.Deg2Rad;
            return right * Mathf.Cos(a) - up * Mathf.Sin(a);
        }

        /// <summary>
        /// 盤の上の E と F。縦の棒に横棒を三本（E）か二本（F）。
        /// 字の形を作れる大きさではないので、太さは画素に載る 3 mm で通す。
        /// **盤の縁へ寄せる。** 半径 14.5 mm に置いたときは二字が真ん中で寄り合って、
        /// 一つの塊に見えた。19 mm まで出すと左下と右下に分かれて、端の印として読める
        /// </summary>
        static void DialLetter(Bank bank, Vector3 at, Vector3 right, Vector3 up, Vector3 outward,
            float part, bool full)
        {
            var centre = at + outward * 0.006f + DialWay(right, up, part) * 0.0190f;
            var rot = Quaternion.LookRotation(outward, up);
            bank.Box(centre - right * 0.0028f, new Vector3(0.0028f, 0.0130f, 0.0040f), rot);
            bank.Box(centre + up * 0.0051f, new Vector3(0.0080f, 0.0028f, 0.0040f), rot);
            bank.Box(centre, new Vector3(0.0080f, 0.0028f, 0.0040f), rot);
            if (full) bank.Box(centre - up * 0.0051f, new Vector3(0.0080f, 0.0028f, 0.0040f), rot);
        }

        /// <summary>
        /// 運転席の窓と回し把手。判定点 (0.80, 1.26, 0.10)。
        ///
        /// <see cref="DoorCard"/> の回し手は前寄り（z 0.52）に付いていて、判定点からは 0.42 m 離れる。
        /// 印を向いた運転席の目からだと画面の中心から 42 度、つまり端に映るだけになるので、
        /// 判定点の指している方をここに作る。**運転席の側だけ。** 助手席の扉は調べる対象にならない。
        ///
        /// 併せて窓の下枠を通す。既にある内張りの上端（y 1.30〜1.35）の上へ、暗い溝と
        /// 明るい笠を重ねる。判定点の高さ（1.26）に「窓の開くところ」が無いと、
        /// 調べているのが把手なのか内張りなのか画面から決まらない。
        ///
        /// **窓のガラスは足さない。** 側面のガラスは外を向いた面 1 枚で張ってあり
        /// （<see cref="Shell"/>）、内向きの面を入れると走行中に見えている沿道の画が変わる。
        ///
        /// **把手の後ろに暗い座を敷く。** 把手だけを内張りの上へ置いたときは、
        /// 塗った鉄（0.216）と内装（0.125）の差が 1.7 倍しかなく、撮ってみると
        /// のっぺりした面の上の淡い染みにしか見えなかった。一回り大きい暗い板
        /// （<see cref="Mat"/> の CarGap 0.020）を後ろへ敷くと、明るい把手が
        /// 暗い輪に囲まれて、形が段として立つ
        /// </summary>
        static void WindowCrank(Bank plate, Bank steel, Bank gap, Leaf leaf)
        {
            // 窓の下枠。左右とも通す。溝は暗く、笠は明るく。
            // 運転席の側は窓ごとドアに付いているので、開くドアへ載せる
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var dark = Leaf.Mine(side) ? leaf.Gap : gap;
                var lit = Leaf.Mine(side) ? leaf.Steel : steel;
                dark.Box(new Vector3(side * 0.862f, 1.3555f, 0.115f), new Vector3(0.062f, 0.013f, 1.362f));
                lit.Box(new Vector3(side * 0.858f, 1.3475f, 0.115f), new Vector3(0.098f, 0.013f, 1.372f));
            }

            // 把手も運転席のドアに付いている。ここから先は開くドアの側へ置く
            plate = leaf.Plate;
            gap = leaf.Gap;

            // 暗い座。把手のぜんたいを後ろから囲う
            gap.Box(new Vector3(0.8175f, 1.140f, 0.110f), new Vector3(0.013f, 0.180f, 0.130f));
            // 回し把手の台座。内張りの室内側の面（x 0.82）から手前へ出す
            plate.Box(new Vector3(0.8090f, 1.162f, 0.100f), new Vector3(0.017f, 0.090f, 0.090f));
            // 軸
            plate.Box(new Vector3(0.7955f, 1.162f, 0.100f), new Vector3(0.028f, 0.030f, 0.030f));
            // 柄。下前がりに倒す
            plate.Box(new Vector3(0.7890f, 1.1260f, 0.1190f), new Vector3(0.018f, 0.0814f, 0.024f),
                Quaternion.Euler(-27.8f, 0f, 0f));
            // 握り。柄の先から室内へ出す
            plate.Box(new Vector3(0.7790f, 1.0880f, 0.1400f), new Vector3(0.036f, 0.030f, 0.030f));
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
        static void Shell(Bank body, Bank steel, Bank gap, Bank glass, Bank tyre, Bank lens, Bank tail,
            Leaf leaf)
        {
            Flanks(body, steel, gap, glass, leaf);
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
        static void Flanks(Bank body, Bank steel, Bank gap, Bank glass, Leaf leaf)
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
                // 開くドアが持って行く面。開かない側では車体の入れ物をそのまま指す
                var mine = Leaf.Mine(side);
                var dBody = mine ? leaf.Body : body;
                var dSteel = mine ? leaf.Steel : steel;
                var dGap = mine ? leaf.Gap : gap;
                var dGlass = mine ? leaf.Glass : glass;

                // 前の翼板。抜きの前後は腰まで、抜きの上は頂きから腰まで
                Slab(body, x, skin, 0.94f, frontIn, SillY, 1.28f);
                Slab(body, x, skin, frontOut, 2.42f, SillY, 1.28f);
                Slab(body, x, skin, frontIn, frontOut, crown, 1.28f);
                // ドア。腰の線まで。継ぎ目で前後を切る
                Slab(dBody, x, skin, LeafBack, 0.94f, SillY, BeltY);
                // 後ろの荷室の側面。抜きを避けて三つに割る
                Slab(body, x, skin, rearIn, CabBack, SillY, BeltY);
                Slab(body, x, skin, TailZ, rearOut, SillY, BeltY);
                Slab(body, x, skin, rearOut, rearIn, crown, BeltY);

                // 窓の枠。下枠・上枠・柱で、間を抜いてガラスを張る。
                // **ドアの間口ぶんはドアへ回す。** この車のドアは枠ごと一枚で、
                // 開けば下枠も上枠も一緒に振れる。車体に残すと、開いたドアの跡に
                // 枠だけが四角く残って窓が浮く
                Slab(body, x, skin, TailZ, LeafBack, BeltY, PaneLow);
                Slab(body, x, skin, TailZ, LeafBack, PaneHigh, 1.94f);
                Slab(dBody, x, skin, LeafBack, 0.94f, BeltY, PaneLow);
                Slab(dBody, x, skin, LeafBack, 0.94f, PaneHigh, 1.94f);
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

                // ドアの継ぎ目。前後の縁を暗く落とす。ドアの側の縁なので板と一緒に振れる
                foreach (var seam in new[] { LeafBack, 0.94f })
                    dGap.Box(new Vector3(x, (SillY + BeltY) * 0.5f, seam),
                        new Vector3(skin + 0.004f, BeltY - SillY, 0.018f));
                // 露わな蝶番。二枚。この車は継ぎ目を隠さない
                foreach (var y in new[] { 0.80f, 1.20f })
                    dSteel.Box(new Vector3(side * (BodyHalf + 0.010f), y, 0.900f),
                        new Vector3(0.030f, 0.085f, 0.110f));
                // 取っ手。押しボタン式の座と、引く爪
                dSteel.Box(new Vector3(side * (BodyHalf + 0.012f), 1.170f, 0.420f),
                    new Vector3(0.028f, 0.070f, 0.200f));
                dSteel.Box(new Vector3(side * (BodyHalf + 0.026f), 1.170f, 0.480f),
                    new Vector3(0.022f, 0.040f, 0.060f));
                // 鍵穴
                dSteel.Box(new Vector3(side * (BodyHalf + 0.008f), 1.090f, 0.420f),
                    new Vector3(0.018f, 0.032f, 0.032f));
                // 止めねじ。腰の線に沿って。露わなままなのがこの車の作り。
                // ドアの間口に落ちるものはドアの板へ打つ
                for (var k = 0; k < 6; k++)
                {
                    var z = -2.05f + k * 0.52f;
                    (z > LeafBack ? dSteel : steel).Box(
                        new Vector3(side * (BodyHalf + 0.006f), BeltY - 0.030f, z),
                        new Vector3(0.014f, 0.020f, 0.020f));
                }

                // 側面のガラス。外を向いた面 1 枚ずつ。ドアと荷室で 2 枚
                var pane = side * (BodyHalf - 0.012f);
                dGlass.FaceX(pane, -0.60f, 0.86f, PaneLow, PaneHigh, s == 0 ? -1 : 1);
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
