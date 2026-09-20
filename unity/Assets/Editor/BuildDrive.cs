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
    /// 場面 8 の車内と道を組む。車は原点に置いたままで、道の方を手前へ送るので、
    /// ここで作るのは「原点の周りに畳んだ世界」であって、何 km もの実寸の道ではない。
    ///
    /// 道はタイルを環にして流す（<see cref="RoadRing"/>／<see cref="DriveWorld"/>）。
    /// 沿道は帯ごとの入れ物に分け、その下をタイルと同じ枚数の区切りに割る。
    /// 区切りを道と同じ環に乗せるので、沿道と道の継ぎ目は勝手に揃う。
    /// 対向車だけは同じ形の入れ物を別に持ち、道より速い環に乗せる。
    ///
    /// 帯はここでは 0 から数える。設計書が「帯 1」と呼ぶものが 0 番にあたる
    /// </summary>
    public static class BuildDrive
    {
        public const string Materials = "Assets/Materials/Drive/";
        public const string Generated = "Assets/Models/generated/drive/";
        public const string ScenePath = "Assets/Scenes/Drive.unity";

        // ---- 道の寸法。メートル --------------------------------------------

        /// <summary>タイル 1 枚の長さ。2 の冪と相性の良い数にする。20 なら計算に丸めが入らない</summary>
        public const float TileLength = 20f;
        /// <summary>前方にここまで途切れず敷く</summary>
        public const float Ahead = 140f;
        /// <summary>車の後ろにここまで残す。負の値</summary>
        public const float Behind = -30f;

        /// <summary>舗装の半幅。道幅 7.0</summary>
        public const float RoadHalf = 3.5f;
        /// <summary>未舗装の轍の半幅。道幅 4.6。帯 4 だけ道がここまで細る</summary>
        public const float DirtHalf = 2.3f;
        /// <summary>路肩。片側</summary>
        public const float Shoulder = 1.2f;
        /// <summary>
        /// 路肩の落ち込み。舗装と地続きに見せないための段。
        /// 座ったままの目線では舗装の面に隠れて段の立ち上がりそのものは見えないので、
        /// 縁を読ませているのは高さではなく色の違い（Verge）の方
        /// </summary>
        public const float ShoulderDrop = 0.05f;

        /// <summary>
        /// 道の中心の x。車は原点から動かせないので、走る車線の真ん中が原点に来るよう
        /// 道の方をずらす。英国なので左側通行。車線 1 本ぶん右へ寄せると、
        /// 中心線も対向車線も車の右側に来る。
        /// 右ハンドルの国を左ハンドルに変えるなら、この符号と車内の左右を返すだけで済む
        /// </summary>
        public const float LaneOffset = RoadHalf * 0.5f;

        /// <summary>対向車線の真ん中。世界の x</summary>
        public const float OncomingX = LaneOffset + RoadHalf * 0.5f;

        /// <summary>対向車が流れる速さ。道の何倍か。DriveWorld へそのまま渡す</summary>
        public const float OncomingRate = 2.2f;

        /// <summary>
        /// 車体の揺れの幅。m。粗さ 1（舗装）のときの値で、帯ごとの rough が掛かる。
        ///
        /// **これも組み立てが持つ。** DriveWorld は組み直しても作り直されない `Drive` の根に
        /// 付いているので、Inspector で触った値は帯の秒数と違って残る。残ること自体は
        /// 都合が良いが、残るか消えるかが付いている場所で決まるのは覚えていられない。
        /// ほかの値と同じく Wire が毎回書き戻すことにして、揃えてある。
        /// オーナーが実画面で決めたら、帯の秒数と同じくここへ書き写す
        /// </summary>
        public const float Shake = 0.004f;

        /// <summary>
        /// 揺れの速さ。走った距離に掛ける。1.0 で基本の波長がおよそ 6.3 m。
        /// 16 m/s で 2.5 Hz、28 m/s で 4.5 Hz にあたる。
        /// これより小さくすると、路面の凹凸ではなく船のような漂いに見える
        /// </summary>
        public const float ShakeRate = 1.0f;

        // ---- 路面に重ねる面の高さ ------------------------------------------
        //
        // 同じ平面に何枚も重ねるので、上下の順と間隔をここで一箇所に決める。
        // 近すぎると遠くで深度が潰れてちらつき、順を違えると下の絵が消える。
        // 手前 0.1 / 奥 1000 の深度では 100 m あたりで 6 mm ほどが限界なので、
        // 隣り合う面はどれも 8 mm 以上あける

        /// <summary>沿道より下に敷く地面。道だけが明るい帯に見えないように</summary>
        public const float VergeY = -0.12f;
        /// <summary>帯 0 の濡れた照り返し。白線の下に敷く</summary>
        public const float SheenY = 0.008f;
        /// <summary>白線。照り返しより上でないと、帯 0 だけ線が消える</summary>
        public const float PaintY = 0.020f;
        /// <summary>帯 4 の土。白線を覆い隠す高さが要る</summary>
        public const float EarthY = 0.036f;
        /// <summary>帯 4 の轍。土の上</summary>
        public const float RutY = 0.050f;
        /// <summary>帯 3 の牧草地。路肩より下げる。同じ高さだと面が重なってちらつく</summary>
        public const float PastureY = -0.06f;
        /// <summary>
        /// 帯 4 の畑の地。路肩の地面（<see cref="VergeY"/> -0.12）より 0.10 上で、
        /// 株の根（0）よりわずかに下。根を浮かせないためにここまで上げてある
        /// </summary>
        public const float FieldY = -0.02f;
        /// <summary>畑が持ち上がり始める距離。道の中心から、m。ここより内側は平ら</summary>
        public const float SwellFrom = 14f;
        /// <summary>畑の持ち上がり。距離の二乗に掛ける。58 m で 1.7 m ほど上がる</summary>
        public const float SwellRate = 0.00055f;
        /// <summary>
        /// 帯 4 の畑の広がり。道の中心から片側、m。
        /// 霧が畳む距離（帯 4 の density 0.017 でおよそ 100 m）より十分遠くまで敷く。
        /// 脇の窓から見たとき、畑の外の何かが見えてはいけない
        /// </summary>
        public const float FieldHalf = 150f;

        // ---- 帯ごとの空と灯り -------------------------------------------------
        //
        // **どれも仮置き。オーナーが実画面を見てから決める。**
        //
        // RenderSettings はシーンにひとつしか無いので、ここで一度置くだけでは
        // 全部の帯が同じ時間帯になる。値は帯ごとに持って、暗転の裏で
        // DriveDirector が差し替える（<see cref="DriveSky.Apply"/>）。
        //
        // 帯 0 から帯 4 へ向かって、倫敦の黒雲から抜けて空が開いていく。
        // 帯 3 は夜と朝の橋渡しで、空が開き始めたところであって朝ではない。
        // 日射しの向きは、帯 3 と帯 4 だけ前の右（東）から低く入れる。
        // 倫敦から北へ走っているので、日が昇るのは右手になる

        /// <summary>帯 0。倫敦の外れ。ナノマシンの黒雲とネオンの照り返し</summary>
        static readonly DriveSky Night = new DriveSky
        {
            sky = new Color(0.055f, 0.060f, 0.082f),
            haze = new Color(0.055f, 0.060f, 0.082f),
            density = 0.014f,
            sun = new Color(0.60f, 0.68f, 0.92f),
            power = 0.55f,
            aim = new Vector3(24f, 152f, 0f),
            lift = new Color(0.070f, 0.078f, 0.105f),
            ground = new Color(0.035f, 0.036f, 0.045f),
        };

        /// <summary>帯 1。夜の高速。街灯の橙が一定の間隔で流れる。倫敦の雲から抜けたので、霧は薄い</summary>
        static readonly DriveSky Lit = new DriveSky
        {
            sky = new Color(0.044f, 0.046f, 0.062f),
            haze = new Color(0.044f, 0.046f, 0.062f),
            density = 0.012f,
            sun = new Color(0.54f, 0.60f, 0.86f),
            power = 0.42f,
            aim = new Vector3(28f, 168f, 0f),
            // 街灯の橙が回り込む。上を少し暖色へ寄せる
            lift = new Color(0.064f, 0.060f, 0.072f),
            ground = new Color(0.030f, 0.028f, 0.030f),
        };

        /// <summary>
        /// 帯 2。深夜の幹線。**この場面で一番暗い。** 街灯が絶え、前照灯だけになる。
        /// 木立は空より暗く落として影絵にする。ここだけは、明るくすると幹線に見えなくなる
        /// </summary>
        static readonly DriveSky Deep = new DriveSky
        {
            sky = new Color(0.026f, 0.028f, 0.040f),
            haze = new Color(0.026f, 0.028f, 0.040f),
            density = 0.020f,
            sun = new Color(0.34f, 0.38f, 0.58f),
            power = 0.22f,
            aim = new Vector3(34f, 186f, 0f),
            lift = new Color(0.034f, 0.036f, 0.050f),
            ground = new Color(0.016f, 0.016f, 0.020f),
        };

        /// <summary>
        /// 帯 3。明け方の丘陵。薄明と霧。低い位置からの光。
        /// 空が開き始めたところで、まだ朝ではない。霧はこの帯が一番濃い
        /// </summary>
        static readonly DriveSky Dawn = new DriveSky
        {
            sky = new Color(0.150f, 0.152f, 0.185f),
            haze = new Color(0.150f, 0.152f, 0.185f),
            density = 0.026f,
            sun = new Color(0.80f, 0.56f, 0.44f),
            power = 0.62f,
            // 5 度。地平すれすれから薙ぐ。石垣の側面だけが赤く当たる。
            // 向きは帯 4 と揃える。日の昇る場所が帯を跨いで飛ぶと、夜明けが繋がらない
            aim = new Vector3(5f, 294f, 0f),
            lift = new Color(0.145f, 0.150f, 0.180f),
            ground = new Color(0.052f, 0.048f, 0.046f),
        };

        /// <summary>
        /// 帯 4。朝靄の未舗装路。原作の「真っ白な千切れ雲と、まだ薄青い高い空」。
        ///
        /// **霧の色は空と同じにしてある。** 離すと、畑が霧に溶け切ったところに
        /// 横一線の継ぎ目が出る。同じ色にしておけば、畑はそのまま空へ溶けて消える。
        /// 朝靄はその溶け方そのもので、別の色として描くものではない。
        /// 高さは雲の層（<see cref="Clouds"/>）が遠近で見せる
        /// </summary>
        static readonly DriveSky Morning = new DriveSky
        {
            sky = new Color(0.500f, 0.600f, 0.780f),
            haze = new Color(0.500f, 0.600f, 0.780f),
            // 0.017 だと 60 m で 6 割方溶けて、畑の広がりも丘の起伏も見えない。
            // 原作は「緩やかな湾曲を描いて広がる小麦畑」なので、遠くまで抜かす。
            // 0.008 なら 60 m で 8 割、150 m の畑の端でもまだ 2 割残る
            density = 0.008f,
            sun = new Color(1.00f, 0.88f, 0.70f),
            power = 1.15f,
            // 11 度。低いまま畑を薙ぐ。ここを 30 度に上げると株の天面しか当たらず、
            // 畑が真上から照らした平らな板になる。
            //
            // 298 度は**後ろ寄りの右**から差す向き。倫敦から北へ走っているので日は右手だが、
            // そこをさらに後ろへ回してある。前の右に置くと畑が逆光になり、
            // 前を向いたときに見えるのが株の陰の面ばかりになって、黄金色がどこにも出ない。
            // 後ろへ回すと、前を向いても脇の窓から見ても日の当たった面が手前を向く
            aim = new Vector3(11f, 298f, 0f),
            lift = new Color(0.40f, 0.46f, 0.56f),
            ground = new Color(0.26f, 0.22f, 0.16f),
        };

        /// <summary>
        /// 乗り込む前。ガレージの中。
        ///
        /// ガレージには天井の灯りが二つある。そこへ帯 4 のような朝の日射しを入れると
        /// 壁も床も白く飛ぶので、乗り込む前は夜のままにしておく。
        ///
        /// **帯 0 と同じ値にしてある。** 乗り込むところには暗転が無く、ドアを調べた
        /// その場でガレージが伏せて走り出す。ここで色が変われば、切り替わる瞬間が
        /// そのまま見える。帯を跨ぐときの差し替えが黒のあいだに隠れるのとは事情が違う。
        /// 帯 0 と別に名前を付けてあるのは、帯 0 を触ってもガレージが連れて動かないため
        /// </summary>
        public static readonly DriveSky GarageSky = Night;

        /// <summary>帯の数</summary>
        public const int Bands = 5;

        // ---- ガレージ。メートル ---------------------------------------------

        /// <summary>床の中心。車は原点にいるので、後ろへ寄せて歩く間を取る</summary>
        public static readonly Vector3 GarageAt = new Vector3(0f, 0f, -2f);
        /// <summary>床の広さ。x 方向</summary>
        public const float GarageWide = 14f;
        /// <summary>床の広さ。z 方向。柱の割り付けでいう長辺はこちら</summary>
        public const float GarageDeep = 16f;
        /// <summary>天井の高さ</summary>
        public const float GarageHigh = 2.9f;
        /// <summary>壁・床・天井の厚み</summary>
        public const float GarageThick = 0.25f;
        /// <summary>
        /// 床の面の高さ。
        ///
        /// 道のタイルは y 0 のまま z -30 から先へ敷いてあり、<see cref="PaintY"/> の
        /// 白線が 0.020 まで上がる。DriveWorld には道を伏せる手立てが無いので、
        /// ガレージの中にも舗装がそのまま重なっている。床をちょうど 0 に置くと
        /// 同じ高さの面が二枚重なってちらつくので、白線より上へ逃がす。
        /// 2 cm の段は歩いていても分からないし、壁の外からは見えない
        /// </summary>
        public const float GarageFloorY = 0.040f;
        /// <summary>柱の一辺</summary>
        public const float PillarSide = 0.35f;
        /// <summary>シャッターの幅</summary>
        public const float ShutterWide = 4.2f;
        /// <summary>シャッターの高さ</summary>
        public const float ShutterHigh = 2.6f;
        /// <summary>天井の灯りの色</summary>
        public static readonly Color GarageLamp = new Color(0.62f, 0.66f, 0.72f);

        // ---- ガレージの飾り -------------------------------------------------
        //
        // 床に貼る面は上下の順をここで決める。塗りの上に油、その上に排水口。
        // 実際にその順で汚れていくので、重なりの順と物の順が揃う。
        // 道の面ほど遠くを見ないので 4 mm ずつで足りる

        /// <summary>区画の線と番号</summary>
        public const float BayPaintY = GarageFloorY + 0.006f;
        /// <summary>油染み。塗りの上に落ちる</summary>
        public const float StainY = GarageFloorY + 0.010f;
        /// <summary>排水口の受け</summary>
        public const float DrainY = GarageFloorY + 0.014f;
        /// <summary>
        /// 排水口の格子。棒の中心の高さなので、受けとの間は棒の厚みの半分だけ縮む。
        /// 0.020 だと棒の底が受けとちょうど同じ面に乗って、両面を描く場面で取り合う
        /// </summary>
        public const float GrateY = GarageFloorY + 0.024f;

        /// <summary>区画 1 つの幅。車 1 台ぶん。14 m の床に 5 つ取れる</summary>
        public const float BayWide = 2.9f;
        /// <summary>区画の奥行き</summary>
        public const float BayDeep = 5.2f;
        /// <summary>区画の真ん中の z。車（原点）の入っている区画に合わせる</summary>
        public const float BayZ = 0.1f;
        /// <summary>塗りの線の幅</summary>
        public const float BayLine = 0.10f;
        /// <summary>覆いを掛けた隣の車を置く区画。車の左隣。歩く線には掛からない</summary>
        public const float CoveredBayX = -BayWide;

        /// <summary>
        /// 車体を塞ぐ箱の外形。世界の座標で、x は ±<see cref="BlockHalfX"/>。
        ///
        /// 車（<see cref="Car"/>）は運転席から見える面しか無く、当たりも持たない。
        /// 塞がないとガレージで車体をすり抜けられる。車高を上げた今は屋根の板（1.88〜1.94）が
        /// 立っている目線 1.60 より高いところにあるので、頭ごと車内へ入り込めてしまう。
        /// 下端をガレージの床の面に合わせるのは、隙間に足先を差し込ませないため。
        ///
        /// 前端はボンネットの先（2.48）まで伸ばす。屋根の前端で切ると、ボンネットの上を歩ける
        /// </summary>
        public const float BlockHalfX = 0.92f;
        public const float BlockTop = 1.94f;
        public const float BlockBack = -0.70f;
        public const float BlockFront = 2.50f;

        /// <summary>立ち位置。運転席のドア側から近づく。ここから車まで約 8 m</summary>
        public static readonly Vector3 StandAt = new Vector3(4.2f, 0f, -7f);
        /// <summary>立ち位置の向き。度</summary>
        public const float StandYaw = -28f;
        /// <summary>
        /// 運転席。ハンドルの真後ろ。x 0 に座るとハンドルが横に 44 度ずれて見える。
        /// 右ハンドルなので正の側で、<see cref="LaneOffset"/> と対になっている。
        ///
        /// 原作の「ボロのオフロード車」に合わせて、目は乗用車の 1.18 から 1.55 へ上げた。
        /// 車体がばねの上に高く載っているぶんがこの差にあたる。
        /// 車内の物はどれもこの目線から割り出してあるので、ここを動かすなら
        /// 計器盤・天井・ハンドル・座席・調べる対象までまとめて動かすことになる
        /// </summary>
        public static readonly Vector3 SeatAt = new Vector3(0.38f, 1.55f, 0f);

        /// <summary>
        /// ハンドルの中心。右ハンドルなので運転席と同じ x に来る。
        ///
        /// 高さは目線ではなく計器盤の天板から決める。輪の下の縁が天板より 0.09 沈んでいると、
        /// 輪が宙に浮かず柱から生えて見える。天板 1.28 に対してここは 1.25。
        /// 目線（1.55）との差は 0.30 で、乗用車だったときの 0.16 より開いた。
        /// 高い席から低い輪を見下ろす構えそのものが、オフロード車の座り方にあたる
        /// </summary>
        public static readonly Vector3 WheelAt = new Vector3(0.38f, 1.25f, 0.39f);
        /// <summary>輪の外径</summary>
        public const float WheelOuter = 0.36f;
        /// <summary>輪の太さ</summary>
        public const float WheelThick = 0.035f;
        /// <summary>輪を x 軸まわりに倒す角。度。オフロード車なのでバスに近いところまで寝ている</summary>
        public const float WheelLean = 68f;
        /// <summary>輪の芯までの半径。手を乗せる位置はここから出す</summary>
        public static float WheelRing { get { return WheelOuter * 0.5f - WheelThick * 0.5f; } }

        // ---- 車内の面。メートル ----------------------------------------------
        //
        // 計器盤も内張りも、箱ひとつではなく面と縁と部品の集まりとして組む。
        // 足す物はどれもこの四つの面から測る。天板と運転席側の面と腰の線が
        // 揃っていないと、車内がその場しのぎの寄せ集めに見える

        /// <summary>計器盤の天板。目線より 0.27 下</summary>
        public const float DashTop = 1.28f;
        /// <summary>計器盤の運転席を向いた面。物入れも吹き出し口もこの面に付く</summary>
        public const float DashFace = 0.51f;
        /// <summary>計器盤の前端。ここから先はボンネット</summary>
        public const float DashNose = 0.97f;
        /// <summary>内張りの上端。腰の線。左右と前で一本に通っている</summary>
        public const float WaistY = 1.30f;
        /// <summary>内張りの室内側の面。x の絶対値</summary>
        public const float CardIn = 0.82f;
        /// <summary>内張りの外側の面。x の絶対値。塞ぐ箱（<see cref="BlockHalfX"/> 0.92）より内</summary>
        public const float CardOut = 0.90f;
        /// <summary>天井の板の下端</summary>
        public const float RoofLow = 1.88f;

        /// <summary>
        /// 運転席から道が見え始める下の縁の高さ。z を渡すと、その位置での境目を返す。
        ///
        /// 目（<see cref="SeatAt"/> + <see cref="EyeLead"/>）からボンネットの天板の前の角
        /// （y 1.22 / z 2.42）へ引いた線。画面のここから上が道と空で、下はボンネットと計器盤になる。
        /// 車内の物がこの線より上へ出れば、出たぶんだけ道が削れる。
        ///
        /// 計器盤に庇や取っ手を足すたびにここが詰まる。数ミリの食い込みは実画面では
        /// 気づけないので、<see cref="CheckDrive"/> の見直し 14 が組むたびに測る
        /// </summary>
        public static float SightY(float z)
        {
            return SeatAt.y - 0.15f * (z - EyeLead);
        }

        /// <summary>メーターの絵を貼る板の大きさ。テクスチャの縦横比（512 × 128）と揃える</summary>
        public const float DialWide = 0.48f;
        public const float DialHigh = 0.12f;
        /// <summary>
        /// メーターの明るさ。Unlit なので、この値がそのまま画面に出る。
        /// 夜の車内はどこも 0.1 を下回るので、針と目盛りだけが浮かび上がる
        /// </summary>
        public const float DialGain = 0.62f;

        /// <summary>
        /// 腕組みの前腕の中心。胸の前。
        ///
        /// 計画は z 0.28 と置いていたが、その位置だと前腕が輪の下端（y 1.189 / z 0.239）を
        /// 貫く。輪は 68 度寝ていて下端がこちらへ張り出しているので、胸に引き寄せて
        /// 輪の手前へ収める。腕組みは元より胸に付く姿勢なので、寄せても不自然にはならない。
        ///
        /// **高さは <see cref="WheelAt"/> と揃えて上げ下げする。** 車高を上げたときも
        /// 輪と腕を一緒に 0.23 持ち上げたので、袖と輪の 34 mm の隙間はそのまま残っている。
        /// 片方だけ動かすと、この隙間が黙って詰まる
        /// </summary>
        public static readonly Vector3 FoldedAt = new Vector3(0.38f, 1.25f, 0.15f);
        /// <summary>肩。腕の付け根。上半身は作っていないので、視界の外の後ろへ逃がす</summary>
        public const float ShoulderY = 1.37f;
        public const float ShoulderZ = -0.06f;
        /// <summary>肩の左右の開き。体の中心から</summary>
        public const float ShoulderHalf = 0.19f;

        // ---- 調べる対象と繋ぎ先 ----------------------------------------------

        public const string ScriptPath = "Assets/Data/DriveScript.asset";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        public const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
        /// <summary>
        /// 暗転中に出る字だけ明朝。台詞はゴシック、という取り決めで、
        /// Alley.unity も Room.unity も Center 層だけこちらを使っている
        /// </summary>
        public const string MinchoPath = "Assets/Fonts/ShipporiMincho-Regular SDF.asset";

        /// <summary>車内の対象を拾える距離。座ったまま手の届く範囲</summary>
        const float ItemRadius = 1.4f;
        /// <summary>ガレージのドア。歩いて近づくので、車内の対象より少し遠くから拾える</summary>
        const float DoorRadius = 1.6f;
        /// <summary>目は顔にあるので体の前へ出す。PlayerController の eyeLead と同じ値</summary>
        public const float EyeLead = 0.22f;

        // ---- ピン。場面 1 で焼いたものを読んで使い回す ------------------------

        const string PinMesh = "Assets/Models/generated/Pin.asset";
        const string PinHoleMesh = "Assets/Models/generated/PinHole.asset";
        const string PinHeadMat = "Assets/Materials/Room/PinHead.mat";
        const string PinHoleMat = "Assets/Materials/Room/PinHole.mat";

        /// <summary>
        /// 帯の値。<see cref="DriveIds.Triggers"/> と同じ並び。
        /// **秒数も速さもすべて仮置きで、オーナーが実画面を見てから決める。**
        /// 帯 2 の黒と明けだけ長いのが仮眠にあたる
        /// </summary>
        static readonly DriveBand[] Route =
        {
            new DriveBand { name = "倫敦の外れ", trigger = DriveIds.Chips, speed = 16f, rough = 1.0f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f, sky = Night },
            new DriveBand { name = "夜の高速", trigger = DriveIds.Log, speed = 28f, rough = 1.0f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f, sky = Lit },
            new DriveBand { name = "深夜の幹線", trigger = DriveIds.Mirror, speed = 24f, rough = 1.0f, afterglow = 5f, black = 3.5f, fadeIn = 2.6f, sky = Deep },
            new DriveBand { name = "明け方の丘陵", trigger = DriveIds.Photo, speed = 20f, rough = 1.6f, afterglow = 5f, black = 0.8f, fadeIn = 1.6f, sky = Dawn },
            new DriveBand { name = "朝靄の未舗装路", trigger = DriveIds.Window, speed = 11f, rough = 4.5f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f, sky = Morning },
        };

        /// <summary>帯ごとのきっかけの対象。Items が立てて Wire が DriveDirector へ渡す</summary>
        static GameObject[] triggerItems = new GameObject[0];

        /// <summary>タイルの枚数</summary>
        public static int TileCount { get { return RoadRing.Needed(Ahead, TileLength, Behind); } }

        /// <summary>環が一周する長さ。沿道の間隔はこれを割り切ること</summary>
        public static float Span { get { return TileCount * TileLength; } }

        /// <summary>同じ形を何十も並べるので、mesh は名前で引いて使い回す</summary>
        static readonly Dictionary<string, Mesh> shapes = new Dictionary<string, Mesh>();

        // ---- 組み立て ------------------------------------------------------

        [MenuItem("HalfAware/Build the drive", false, 230)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Open()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/drive"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "drive");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Drive"))
                AssetDatabase.CreateFolder("Assets/Materials", "Drive");
            shapes.Clear();

            var root = Root("Drive");
            Prune(root, new[] { "Car", "Road", "Roadsides", "Oncoming", "Sky", "Garage", "Items" });
            // Stage より先に呼ぶ。Stage は Player/Main Camera があればそちらへ譲るので、
            // 後から rig を作ると札と AudioListener が二つずつになる
            Rig();
            Stage();
            Car(Child(root, "Car"));
            Road(Child(root, "Road"));
            Roadsides(Child(root, "Roadsides"));
            Traffic(Child(root, "Oncoming"));
            Clouds(root);
            Garage(root);
            Items(root);
            Wire(root);
            // 組み終えたら必ず見直す。目で気づくまで放っておかない
            CheckDrive.Run(root);

            Selection.activeGameObject = root.gameObject;
            Mark(root.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("車と道を組んだ。タイル {0} 枚 × {1} m（前 {2} / 後ろ {3}）、沿道と対向車が {4} 帯 × {0} 区切りずつ",
                TileCount, TileLength, Ahead, Behind, Bands));
            Debug.Log(string.Format("ガレージ {0} × {1} × 高さ {2}。立ち位置 {3} から運転席 {4} まで約 {5:F1} m、調べる対象 {6} 個",
                GarageWide, GarageDeep, GarageHigh, StandAt.ToString("F1"), SeatAt.ToString("F2"),
                Vector3.Distance(new Vector3(StandAt.x, 0f, StandAt.z), new Vector3(SeatAt.x, 0f, SeatAt.z)),
                Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length));
        }

        /// <summary>
        /// 場面 8 のシーンを開く。無ければ作る。
        /// 別のシーンに手を入れたまま呼ばれたら何もしない。ここで開き直すと黙って消える
        /// </summary>
        static bool Open()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (!other.isDirty) continue;
                Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + other.path);
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }
            var made = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(made, ScenePath);
            return true;
        }

        // ---- シーンの地 ----------------------------------------------------

        /// <summary>
        /// 日射し・霧と、カメラのレンダリングの設定。組み直すたびに結び直すので、手で触った値は残らない。
        /// カメラそのものは作らない。Rig が Player の下に必ず 1 つ作るので、ここで見るのは
        /// そのレンダリングの設定だけ。必ず Rig の後に呼ぶ。
        /// 道は z 160 で終わる。霧が無いと世界の端がそのまま見える。
        ///
        /// 空と灯りそのものは帯ごとに持つ（<see cref="DriveSky"/>）。ここで置くのは
        /// 乗り込む前のガレージのぶんで、再生を始めた最初のフレームに映るのもこれになる。
        /// 走り出したあとは DriveDirector が暗転の裏で差し替える
        /// </summary>
        static void Stage()
        {
            var cam = GameObject.Find("Player/Main Camera");
            var c = cam != null ? cam.GetComponent<Camera>() : null;
            if (c == null) Debug.LogWarning("Player/Main Camera が無い。Stage は Rig の後に呼ぶ");
            else
            {
                // 車内で一番近いのは天井の板の 0.33 m。手前を 0.1 まで引くと、
                // 路面に重ねた面の深度の余裕がそのぶん増える
                c.nearClipPlane = 0.1f;
                c.farClipPlane = 1000f;
                c.fieldOfView = 70f;
                EditorUtility.SetDirty(c);
            }

            var sun = Loose("Directional Light");
            sun.transform.position = new Vector3(0f, 8f, 0f);
            var l = sun.GetComponent<Light>();
            if (l == null) l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = LightShadows.Soft;
            // 色・強さ・向きは帯が持つ。ここではガレージのぶんを置くだけ
            GarageSky.Apply(l, c);
            EditorUtility.SetDirty(l);
        }

        // ---- 車内 ----------------------------------------------------------

        /// <summary>
        /// 運転席から見える範囲だけ組む。原点は車の中心で、視点は seat の (0.38, 1.55, 0)。
        /// 外形はボンネットだけ作る。運転席から見えるのはそこまでで、側面も車輪も画面に入らない。
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
            Bonnet(body);

            trim.Emit(parent, "CarTrim", Mat("CarTrim"), false, Generated);
            seat.Emit(parent, "CarSeat", Mat("CarSeat"), false, Generated);
            glass.Emit(parent, "CarGlass", Mat("CarGlass"), false, Generated);
            body.Emit(parent, "CarBody", Mat("CarBody"), false, Generated);
            steel.Emit(parent, "CarSteel", Mat("CarSteel"), false, Generated);
            gap.Emit(parent, "CarGap", Mat("CarGap"), false, Generated);
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
        static void Bonnet(Bank body)
        {
            // 天板。計器盤の前端（0.97）から継いで前へ 1.45
            body.Box(new Vector3(0f, 1.195f, 1.695f), new Vector3(1.52f, 0.05f, 1.45f));
            // フェンダー。天板の左右に一段高く載せる
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                body.Box(new Vector3(side * 0.78f, 1.235f, 1.710f), new Vector3(0.26f, 0.09f, 1.42f));
            }
            // 前端。運転席からは見えないが、ガレージで外から見たときに前が抜けていると板が浮く
            body.Box(new Vector3(0f, 1.085f, 2.450f), new Vector3(1.82f, 0.27f, 0.06f));
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

        // ---- 道 ------------------------------------------------------------

        /// <summary>
        /// タイルを環の枠の数だけ並べる。並べる z は DriveWorld が毎フレーム出すのと
        /// 同じ式から出す。組み立てと走り出しで並びが跳ばないように
        /// </summary>
        static void Road(Transform parent)
        {
            Clear(parent);
            var n = TileCount;
            var surface = TileMesh();
            var ground = GroundMesh();
            var paint = PaintMesh();
            for (var i = 0; i < n; i++)
            {
                var tile = Piece(parent, "Tile" + i, surface, Mat("Asphalt"));
                tile.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, n, TileLength, 0f, Behind));
                Piece(tile, "Ground", ground, Mat("Verge"));
                Piece(tile, "Line", paint, Mat("RoadLine"));
            }
        }

        /// <summary>
        /// タイル 1 枚の形。原点は手前（-z）の端で、z 方向にちょうど TileLength だけ伸びる。
        ///
        /// RoadRing.Slot が返すのは mesh の原点の z なので、この置き方から外れると
        /// 環は綺麗に並んだまま地平に穴が空く。中央に置けば前の端が半枚ぶん手前へ寄り、
        /// 奥（+z）端に置けば丸ごと一枚ぶん足りない。絶対の z を頂点に焼くのも同じで、
        /// localPosition と二重にずれる。
        ///
        /// Texel × TileLength が整数でないと継ぎ目で絵柄が途切れる。0.5 × 20 = 10
        /// </summary>
        static Mesh TileMesh()
        {
            var bank = new Bank { Texel = 0.5f };
            bank.FaceY(0f, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1);
            return Bake(bank, "RoadTile");
        }

        /// <summary>
        /// 舗装の外の地面と路肩。タイルと同じ形なので同じ環に乗る。
        ///
        /// 舗装しか敷かないと、その外は霧の色のまま抜ける。霧は路面より明るい色なので、
        /// 道だけが地平まで明るい帯として浮き、高架の脚も木立も何も無い所に立って見える。
        /// ここを暗く塞ぐのが目的で、帯 3 の牧草地（-0.06）と帯 4 の土の下に来る高さに置く。
        ///
        /// 舗装と別の mesh にしてあるのは、路肩と地面に舗装と違う色を持たせるため。
        /// 段の立ち上がりは外向きのままにする。運転席からは舗装の面に隠れて見えないが、
        /// 内向きに返すと道の塊が裏返り、ガレージから歩いて近づいたときに中身が見える
        /// </summary>
        static Mesh GroundMesh()
        {
            var bank = new Bank { Texel = 0.25f };
            bank.FaceY(VergeY, Lane(-24f), Lane(24f), 0f, TileLength, 1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var inner = Lane(RoadHalf * side);
                var outer = Lane((RoadHalf + Shoulder) * side);
                bank.FaceY(-ShoulderDrop, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, TileLength, 1);
                bank.FaceX(inner, 0f, TileLength, -ShoulderDrop, 0f, s == 0 ? -1 : 1);
            }
            return Bake(bank, "RoadGround");
        }

        /// <summary>
        /// 白線。一様な灰色の帯が流れても速さが読めないので、道が動いていることは
        /// これで見せる。破線の刻みは TileLength を割り切る数にする。
        /// 割り切らないと、タイルの継ぎ目のたびに破線が一箇所だけ詰まる
        /// </summary>
        static Mesh PaintMesh()
        {
            const float dash = 2f;
            const float step = 5f;
            var bank = new Bank { Texel = 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var x = Lane((RoadHalf - 0.18f) * (s == 0 ? -1f : 1f));
                bank.FaceY(PaintY, x - 0.05f, x + 0.05f, 0f, TileLength, 1);
            }
            for (var z = 0f; z + dash <= TileLength + 0.001f; z += step)
                bank.FaceY(PaintY, Lane(-0.06f), Lane(0.06f), z, z + dash, 1);
            return Bake(bank, "RoadLine");
        }

        // ---- 沿道 ----------------------------------------------------------

        /// <summary>
        /// 帯ごとの入れ物と、その下の区切り。
        ///
        /// 沿道の物をタイルの子にしてはいけない。子にすると Dress が空の入れ物を
        /// 切り替えるだけになり、5 帯ぶんが同時に出る。入れ物の直下へじかに並べると
        /// Dress は効くが、今度は沿道が一切動かない。
        ///
        /// Place は dressed.childCount を環の大きさに使うので、区切りの数はタイルの枚数と
        /// ぴったり同じにし、入れ物の直下には区切り以外を置かない。
        /// 帯ぜんたいを照らす灯りを 1 つ混ぜただけで、区切りが道の継ぎ目から全部ずれる
        /// </summary>
        static void Roadsides(Transform parent)
        {
            Clear(parent);
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                var slices = Slices(band);
                if (b == 0) Outskirts(slices);
                else if (b == 1) Motorway(slices);
                else if (b == 2) Trunk(slices);
                else if (b == 3) Downs(slices);
                else Furrows(slices);
                // 帯を出し分けるのは DriveWorld.Dress。組んだ直後は頭の帯だけ見せる
                band.gameObject.SetActive(b == 0);
            }
        }

        /// <summary>
        /// 対向車。沿道と同じ帯と区切りの形に割るが、入れ物は別に持つ。
        ///
        /// すれ違う車は自分の速さと相手の速さの和で近づいてくる。沿道と同じ環に乗せると
        /// 道と同じ速さでしか流れず、隣を並んで走っているようにしか見えない。
        /// DriveWorld が oncomingRate を掛けた距離でこちらだけ別に流す。
        ///
        /// 中身があるのは帯 1 だけだが、入れ物と区切りは 5 帯ぶん揃えて作る。
        /// Place は childCount を環の大きさに使うので、数が揃っていないと繋ぎ替えで狂う
        /// </summary>
        static void Traffic(Transform parent)
        {
            Clear(parent);
            var dots = Shape("Oncoming", 0.5f, b =>
            {
                b.Box(new Vector3(-0.76f, 0.72f, 0f), new Vector3(0.26f, 0.16f, 0.10f));
                b.Box(new Vector3(0.76f, 0.72f, 0f), new Vector3(0.26f, 0.16f, 0.10f));
            });
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                var slices = Slices(band);
                // 60 は 180 を割り切る。割り切らないと一周に一度だけ続けざまにすれ違う
                if (b == 1)
                    Along(slices, 60f, (slice, z, k) =>
                        Piece(slice, "Oncoming" + k, dots, Glow(new Color(0.92f, 0.94f, 1f), 3.4f))
                            .localPosition = new Vector3(OncomingX, 0f, z));
                band.gameObject.SetActive(b == 0);
            }
        }

        // ---- 空に浮かべる雲 ---------------------------------------------------

        /// <summary>
        /// 帯ごとの空の物。今は雲だけ。
        ///
        /// 帯 0 は原作の「クラッカーたちが撒き散らすクラック用のナノマシンの黒雲」、
        /// 帯 4 は「倫敦のナノマシンの黒雲とは違い、真っ白な千切れ雲」。
        /// **この二つは対になっている。** 片方だけ外すと、原作が対比で書いている
        /// 倫敦と田舎町の差が、色の違いだけになる。
        ///
        /// 沿道の入れ物には入れられない。あの下には区切りしか置けず（<see cref="CheckDrive"/>）、
        /// 区切りに入れれば道と同じ速さで手前へ流れて、雲まで時速 40 km で走ることになる。
        /// 動きは <see cref="CloudDrift"/> が絵のほうをずらして出す。
        ///
        /// **板の広さには上限がある。** カメラの奥は 1000 m なので、四隅までの距離が
        /// そこを越えると空が切り落とされて直線が出る。半幅は 700 m までに留めること
        /// </summary>
        static void Clouds(Transform root)
        {
            var parent = Child(root, "Sky");
            Clear(parent);
            var nano = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudLayer.png");
            var torn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudTorn.png");
            if (nano == null || torn == null) Debug.LogWarning("雲の絵が無い");
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                // ナノマシンの黒雲。低く垂れて、高架の上をそのまま塞ぐ
                if (b == 0 && nano != null)
                    Deck(band, "Nano", nano, 30f, 420f, 0.010f,
                        new Color(0.030f, 0.026f, 0.042f, 0.94f), 0.078f, 0.30f, new Vector2(0.0034f, 0.0012f));
                else if (b == Bands - 1 && torn != null)
                {
                    // 千切れ雲。二層に分けるのは、遠近だけでは高さが出ないため。
                    // 別々の速さで流れる二枚が重なって初めて「高い空」に見える。
                    // **絵の刻みは粗く取る。** 細かく繰り返すと、浅い角度で見たときに
                    // 雲ではなく空に散った点々になる。1 枚がおよそ 160 m になる刻みにしてある
                    Deck(band, "Torn0", torn, 88f, 680f, 0.0062f,
                        new Color(1f, 1f, 1f, 0.90f), 0.135f, 0.42f, new Vector2(0.0016f, 0.0006f));
                    Deck(band, "Torn1", torn, 155f, 680f, 0.0034f,
                        new Color(0.94f, 0.96f, 1f, 0.55f), 0.232f, 0.58f, new Vector2(0.0008f, 0.0003f));
                }
                // 出し分けるのは DriveDirector。組んだ直後は頭の帯だけ見せる
                band.gameObject.SetActive(b == 0);
            }
        }

        /// <summary>
        /// 雲の層 1 枚。下から見上げるので面は下へ向ける。
        /// low は絵が消え切る仰角の sin で、板の縁の仰角（high / 半幅）より上に取ること。
        /// 下回ると、空を横切る板の縁がそのまま線になって出る
        /// </summary>
        static void Deck(Transform parent, string name, Texture2D tex, float high, float half, float texel,
            Color tint, float low, float full, Vector2 drift)
        {
            var bank = new Bank { Texel = texel };
            bank.FaceY(high, -half, half, -half, half, -1);
            var go = bank.Emit(parent, name, CloudMat(name, tex, tint, low, full), false, Generated);
            if (go == null) return;
            var drifter = go.AddComponent<CloudDrift>();
            var so = new SerializedObject(drifter);
            so.FindProperty("speed").vector2Value = drift;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 雲のマテリアル。層ごとに 1 枚ずつ作る。CloudDrift が層ごとに別の絵をずらすので、
        /// 共有すると全部の層が一緒に動く
        /// </summary>
        static Material CloudMat(string name, Texture2D tex, Color tint, float low, float full)
        {
            var path = Materials + "Cloud" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("HalfAware/SkyCloud"));
                m.name = "Cloud" + name;
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。手で触った値は残らない
            m.shader = Shader.Find("HalfAware/SkyCloud");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_HazeLow", low);
            m.SetFloat("_HazeHigh", full);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 帯の入れ物の下に、タイルと同じ枚数の区切りを割る。
        /// 入れ物の直下には区切り以外を置かない。1 つ混ざるだけで環の大きさが変わる
        /// </summary>
        static Transform[] Slices(Transform band)
        {
            var n = TileCount;
            var all = new Transform[n];
            for (var i = 0; i < n; i++)
            {
                var slice = Child(band, "Slice" + i);
                slice.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, n, TileLength, 0f, Behind));
                all[i] = slice;
            }
            return all;
        }

        /// <summary>帯 0。倫敦の外れ。高架の脚とネオン、濡れた路面</summary>
        static void Outskirts(Transform[] slices)
        {
            var pier = Shape("Pier", 0.45f, b => b.Box(new Vector3(0f, 3.0f, 0f), new Vector3(1.10f, 6.0f, 1.10f)));
            var board = Shape("NeonBoard", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.12f, 1.10f, 2.60f)));
            var sheen = Shape("Sheen", 0.25f, b => b.FaceY(SheenY, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1));
            var hues = new[]
            {
                new Color(1f, 0.30f, 0.34f), new Color(0.36f, 0.72f, 1f), new Color(0.44f, 1f, 0.62f),
            };

            for (var i = 0; i < slices.Length; i++)
                Piece(slices[i], "Sheen", sheen, Mat("Sheen"));
            Along(slices, 45f, (slice, z, k) => Sides("Pier" + k, 6.5f, (at, side, name) =>
                Piece(slice, name, pier, Mat("Concrete")).localPosition = new Vector3(at, 0f, z)));
            Along(slices, 30f, (slice, z, k) => Sides("Neon" + k, 5.4f, (at, side, name) =>
                Piece(slice, name, board, Glow(hues[(k + side) % hues.Length], 2.4f))
                    .localPosition = new Vector3(at, 3.2f, z)));
        }

        /// <summary>帯 1。夜の高速。街灯だけ。対向車は別の環に乗るので Traffic が持つ</summary>
        static void Motorway(Transform[] slices)
        {
            var post = Shape("LampPost", 0.4f, b =>
            {
                b.Box(new Vector3(0f, 3.6f, 0f), new Vector3(0.18f, 7.2f, 0.18f));
                b.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.38f, 0.56f, 0.38f));
                // 道の上へ差し出す腕。真上にしか光らない街灯は道を照らさない
                b.Box(new Vector3(0.62f, 7.14f, 0f), new Vector3(1.30f, 0.12f, 0.12f));
            });
            var head = Shape("LampHead", 0.5f, b => b.Box(new Vector3(1.18f, 7.02f, 0f), new Vector3(0.62f, 0.12f, 0.26f)));

            Along(slices, 36f, (slice, z, k) => Sides("Lamp" + k, 4.6f, (at, side, name) =>
            {
                var lamp = Child(slice, name);
                lamp.localPosition = new Vector3(at, 0f, z);
                // 腕は mesh の +x へ伸ばしてあるので、右側は向きを返して道へ差し出す
                lamp.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                Piece(lamp, "Post", post, Mat("Metal"));
                Piece(lamp, "Head", head, Glow(new Color(1f, 0.72f, 0.36f), 3.0f));
            }));
        }

        /// <summary>帯 2。深夜の幹線。木立だけ</summary>
        static void Trunk(Transform[] slices)
        {
            var trees = new Mesh[3];
            for (var v = 0; v < trees.Length; v++) trees[v] = TreeMesh(v);
            // 種を決め打ちにして、組み直しても同じ画になるようにする
            var rnd = new System.Random(20260920);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var tag = s == 0 ? "L" : "R";
                // 右は刻みを半分ずらす。左右を同じ位置に立てると、道を挟んで木が対で並んで見える。
                // ばらつきも左右で別に引くので、揺らぎまで揃うことはない
                Scatter(slices, 12f, 3.6f, s * 6f, rnd, (slice, z, k) =>
                {
                    var t = Piece(slice, "Tree" + tag + k, trees[(k + s) % trees.Length], Mat("Tree"));
                    t.localPosition = new Vector3(Lane((5.8f + (float)rnd.NextDouble() * 1.4f) * side), 0f, z);
                    t.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    t.localScale = Vector3.one * (0.78f + (float)rnd.NextDouble() * 0.5f);
                });
            }
        }

        /// <summary>木 1 本。夜明け前の逆光では影にしかならないので、塊だけ作る</summary>
        static Mesh TreeMesh(int variant)
        {
            return Shape("Tree" + variant, 0.35f, b =>
            {
                var rnd = new System.Random(977 * variant + 31);
                b.Box(new Vector3(0f, 1.15f, 0f), new Vector3(0.34f, 2.30f, 0.34f));
                for (var i = 0; i < 6; i++)
                {
                    var y = 2.0f + i * 0.48f;
                    var wide = 2.5f - i * 0.30f;
                    var off = ((float)rnd.NextDouble() - 0.5f) * 0.7f;
                    b.Box(new Vector3(off, y, off * 0.6f), new Vector3(wide, 0.62f, wide),
                        Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f));
                }
            });
        }

        /// <summary>帯 3。明け方の丘陵。石垣と牧草地</summary>
        static void Downs(Transform[] slices)
        {
            var wall = Shape("StoneWall", 0.8f, b =>
            {
                for (var s = 0; s < 2; s++)
                {
                    // 路肩は道の中心から ±4.7 まで。石垣は道の縁より外に立つものなので、跨がせない
                    var x = Lane(s == 0 ? -5.2f : 5.2f);
                    b.Box(new Vector3(x, 0.40f, TileLength * 0.5f), new Vector3(0.46f, 0.80f, TileLength));
                    b.Box(new Vector3(x, 0.85f, TileLength * 0.5f), new Vector3(0.54f, 0.10f, TileLength));
                }
            });
            // 牧草地は路肩より下げる。同じ高さだと面が重なってちらつく
            var field = Shape("Pasture", 0.12f, b =>
            {
                b.FaceY(PastureY, Lane(-46f), Lane(-(RoadHalf + Shoulder)), 0f, TileLength, 1);
                b.FaceY(PastureY, Lane(RoadHalf + Shoulder), Lane(46f), 0f, TileLength, 1);
            });
            // 石垣は 20 m ずつの一様な押し出しなので、それだけでは何も流れて見えない。
            // 区切りに 1 つ門を入れると、区切りの長さがそのまま間隔になって必ず 180 を割り切る
            var gate = Shape("FieldGate", 0.5f, b =>
            {
                for (var i = 0; i < 2; i++)
                    b.Box(new Vector3(0f, 0.62f, i * 3.0f), new Vector3(0.16f, 1.24f, 0.16f));
                for (var i = 0; i < 3; i++)
                    b.Box(new Vector3(0f, 0.34f + i * 0.34f, 1.5f), new Vector3(0.07f, 0.07f, 3.0f));
            });
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Pasture", field, Mat("Grass"));
                Piece(slices[i], "Wall", wall, Mat("Stone"));
                // 運転席が右なので、近いのは左の石垣。門もそちら側に置く。
                // 石垣と同軸（-5.2）に置くと笠石より上の 0.34 m しか出ず、
                // 門ではなく壁に付いた金具に見える。牧草地の側へ出して独りで立たせる。
                // 柱の足は牧草地の面まで下ろす。石垣に隠れていたときは気づかないが、
                // 独りで立つと 6 cm 浮いているのがそのまま見える
                Piece(slices[i], "Gate", gate, Mat("Metal"))
                    .localPosition = new Vector3(Lane(-6.0f), PastureY, 8.5f);
            }
        }

        /// <summary>
        /// 帯 4。朝靄の未舗装路。黄金色の小麦畑と土の轍。
        ///
        /// 原作の「朝靄の中で、緩やかな湾曲を描いて広がる小麦畑が黄金色に微風になびいている」。
        /// **畑は視界を埋めるところまで広げる。** 道の脇に麦を数本立てただけでは、
        /// 窓を開けて息を吸い込む場面にならない。株は 40 m まで、地の面は 150 m まで敷いて、
        /// その先は霧が畳む。なびかせるのは株のマテリアル（HalfAware/Wheat）で、
        /// 株ごとに Transform を持たせる手は取れない。区切り 1 つに 200 近い株が
        /// 1 枚の mesh へ焼かれているため
        /// </summary>
        static void Furrows(Transform[] slices)
        {
            // 舗装のタイルはそのまま下に敷いてあるので、土の面で覆い隠す。
            // 帯 4 だけタイルを差し替える手は取らない。タイルは 1 種しか無い。
            // ±5.4 は路肩の段（±4.7）のすぐ外。ここより広げると土色が畑の下へ回り込み、
            // 黄金色が道の際で途切れて見える
            var earth = Shape("Earth", 0.2f, b => b.FaceY(EarthY, Lane(-5.4f), Lane(5.4f), 0f, TileLength, 1));
            var ruts = Shape("Ruts", 0.3f, b =>
            {
                b.FaceY(RutY, Lane(-DirtHalf), Lane(DirtHalf), 0f, TileLength, 1);
                b.Box(new Vector3(Lane(-0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
                b.Box(new Vector3(Lane(0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
            });
            // 畑の地。株のあいだから覗く面。
            // **株だけでは畑にならない。** 遠くの株は霧に畳まれて消えるので、地の面が無いと
            // 畑の向こうに路肩の地面（Verge）の暗い色がそのまま出て、刈り取った跡に見える
            var field = Shape("Field", 0.14f, b =>
            {
                b.FaceY(FieldY, Lane(-FieldHalf), Lane(-4.8f), 0f, TileLength, 1);
                b.FaceY(FieldY, Lane(4.8f), Lane(FieldHalf), 0f, TileLength, 1);
            });
            var wheat = Shape("Wheat", 0.4f, b =>
            {
                var rnd = new System.Random(4021);
                // 列ごとに、道からの距離・株の大きさ・列の中の刻みを持つ。
                //
                // **近くを詰めるのが要。** 疎に置くと株のあいだから地の面が覗いて、
                // 畑ではなく箱を並べた原っぱに見える。近い列は株を小さく刻みを細かく、
                // 遠い列は大きく粗くして、面として繋がっていればよいことにする。
                // 刻みはどれも 20 を割り切る数にして、区切りの継ぎ目で列が詰まらないようにする。
                //
                // 背は実物どおり 0.78〜1.04。目線（1.55）より低くして見下ろせるようにする。
                // 1.0〜1.5 まで上げると、一番内側の列の穂先がちょうど目の高さに来て、
                // 道の両脇に黄金の壁が立つ。原作は「広がる小麦畑」なので、それでは逆。
                //
                // 「低いと天面ばかり見えて箱の集まりになる」のは一番内側の数列だけで、
                // 20 m も離れれば見込む角は 2 度を切り、見えるのは株の側面になる。
                // 手前で天面が目立つぶんは、地面の起伏（Swell）と薄い霧で畑として繋げる。
                // WheatWind.High はこの上端と揃えること。
                //
                // 株は箱で、y まわりに回すと角が半幅の 1.414 倍まではみ出す。
                // 一番内側の列を 3.85 に置き、ばらつきを外向きだけにして、轍の側へ倒れ込ませない。
                // 風でさらに WheatWind.Reach だけ振れるぶんは見直しが見る
                var at = new[] { 3.75f, 4.5f, 5.4f, 6.5f, 7.8f, 9.4f, 11.4f, 14.0f, 17.5f, 22.0f, 28.0f, 36.0f, 46.0f, 58.0f };
                var wide = new[] { 0.45f, 0.50f, 0.55f, 0.65f, 0.80f, 0.95f, 1.20f, 1.50f, 1.90f, 2.45f, 3.10f, 4.00f, 5.00f, 6.20f };
                var step = new[] { 0.50f, 0.50f, 0.625f, 0.625f, 0.80f, 1.00f, 1.25f, 1.25f, 2.00f, 2.50f, 2.50f, 2.50f, 2.50f, 2.50f };
                for (var c = 0; c < at.Length; c++)
                    for (var z = step[c] * 0.5f; z < TileLength; z += step[c])
                        for (var s = 0; s < 2; s++)
                        {
                            var side = s == 0 ? -1f : 1f;
                            var high = 0.78f + (float)rnd.NextDouble() * 0.26f;
                            var from = at[c] + (float)rnd.NextDouble() * wide[c] * 0.9f;
                            b.Box(new Vector3(Lane(from * side), Swell(from) + high * 0.5f,
                                    z + ((float)rnd.NextDouble() - 0.5f) * step[c] * 0.7f),
                                new Vector3(wide[c], high, wide[c]),
                                Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f));
                        }
            });
            var crop = WheatMat();
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Earth", earth, Mat("Dirt"));
                Piece(slices[i], "Ruts", ruts, Mat("Rut"));
                Piece(slices[i], "Field", field, Mat("Field"));
                Piece(slices[i], "Wheat", wheat, crop);
            }
        }

        /// <summary>
        /// 畑のうねり。道から離れるほど、緩やかに持ち上がる。
        ///
        /// 原作の「緩やかな湾曲を描いて広がる小麦畑」。平らなままだと地平が定規を当てた
        /// ような直線になり、畑ではなく黄色い板が敷いてあるように見える。
        /// 二乗で効かせるので、近くはほとんど上がらず、遠くだけが背を持つ。
        ///
        /// **近い列（<see cref="SwellFrom"/> より内側）は上げない。** 株の揺れは根からの
        /// 高さで重みを付けているので、根そのものを持ち上げると株ぜんたいが 1 の重みになり、
        /// 撓むかわりに横へ滑る。目の届く手前の列だけは根を地面に置いておく。
        /// 遠い列は畑の面として見えればよく、40 m 先で 0.1 m 滑っても読めない
        /// </summary>
        static float Swell(float from)
        {
            if (from <= SwellFrom) return 0f;
            return SwellRate * (from * from - SwellFrom * SwellFrom);
        }

        // ---- 沿道の並べ方 --------------------------------------------------

        /// <summary>
        /// 環ぜんたいを spacing ごとに刻んで並べる。
        ///
        /// 環はこの長さで一周し、同じ並びを繰り返す。spacing が環の長さを割り切らないと
        /// 一周に一箇所だけ間隔が詰まり、そこだけ物が倍の速さで飛んでくるように見える
        /// </summary>
        static void Along(Transform[] slices, float spacing, System.Action<Transform, float, int> put)
        {
            if (slices.Length == 0 || spacing <= 0f) return;
            var count = Mathf.RoundToInt(Span / spacing);
            if (count <= 0 || Mathf.Abs(count * spacing - Span) > 0.001f)
            {
                Debug.LogWarning(string.Format("沿道の間隔 {0} m が環の長さ {1} m を割り切らない。並べずに飛ばす", spacing, Span));
                return;
            }
            for (var k = 0; k < count; k++) Drop(slices, k * spacing, k, put);
        }

        /// <summary>
        /// Along と同じだが、刻みから wobble まで前後にばらけさせる。
        /// 木立のように並んで見えては困るものに使う。環の上で畳むので、
        /// 一周したところで並びは繋がったままになる。
        /// phase は並び全体をずらす量で、左右で対にならないようにするのに使う
        /// </summary>
        static void Scatter(Transform[] slices, float spacing, float wobble, float phase, System.Random rnd,
            System.Action<Transform, float, int> put)
        {
            if (slices.Length == 0 || spacing <= 0f) return;
            var count = Mathf.RoundToInt(Span / spacing);
            if (count <= 0 || Mathf.Abs(count * spacing - Span) > 0.001f)
            {
                Debug.LogWarning(string.Format("沿道の間隔 {0} m が環の長さ {1} m を割り切らない。並べずに飛ばす", spacing, Span));
                return;
            }
            for (var k = 0; k < count; k++)
                Drop(slices, k * spacing + phase + ((float)rnd.NextDouble() - 0.5f) * 2f * wobble, k, put);
        }

        /// <summary>環の上の位置を、区切りの番号と区切りの中の z に割る</summary>
        static void Drop(Transform[] slices, float at, int k, System.Action<Transform, float, int> put)
        {
            var p = at % Span;
            if (p < 0f) p += Span;
            var i = Mathf.Clamp((int)(p / TileLength), 0, slices.Length - 1);
            put(slices[i], p - i * TileLength, k);
        }

        /// <summary>道の中心から測った x を、世界の x へ直す</summary>
        static float Lane(float x)
        {
            return LaneOffset + x;
        }

        /// <summary>道の中心から左右へ 1 つずつ。x は道の中心からの距離。side は 0 が左、1 が右</summary>
        static void Sides(string name, float x, System.Action<float, int, string> put)
        {
            put(Lane(-x), 0, name + "L");
            put(Lane(x), 1, name + "R");
        }

        // ---- ガレージ --------------------------------------------------------

        /// <summary>
        /// 車を入れてある共用のガレージ。プレイヤーは隅に立ち、歩いて運転席のドアまで来る。
        /// 乗り込んだ時点で DriveDirector が丸ごと伏せるので、入れ物はひとつにまとめる。
        ///
        /// **壁は省けない。** 道のタイルは y 0 のまま z -30 から 150 まで敷いてあり、
        /// DriveWorld には道を伏せる手立てが無い（Dress(-1) が消すのは沿道だけ）。
        /// 壁が無いと、ガレージに立っている間ずっと道が左右へ突き抜けて見える。
        ///
        /// 面はどれも Box で作る。板 1 枚で立てると内側から見た面が裏になり、
        /// 絵が出ないうえに MeshCollider の光線も素通りする
        /// </summary>
        static void Garage(Transform root)
        {
            var parent = Child(root, "Garage");
            Clear(parent);
            parent.gameObject.SetActive(true);
            parent.localPosition = Vector3.zero;

            var halfX = GarageWide * 0.5f;
            var halfZ = GarageDeep * 0.5f;
            var skin = GarageThick * 0.5f;
            // 壁は床の面から天井の面まで。中心と高さをここで一度だけ出す
            var midY = (GarageFloorY + GarageHigh) * 0.5f;
            var tall = GarageHigh - GarageFloorY;
            var front = GarageAt.z + halfZ + skin;

            var floor = new Bank { Texel = 0.5f };
            floor.Box(new Vector3(GarageAt.x, GarageFloorY - skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            var walls = new Bank { Texel = 0.5f };
            // 長辺の壁。四隅を覆うので、z 方向は厚みのぶん伸ばす
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (halfX + skin), midY, GarageAt.z),
                    new Vector3(GarageThick, tall, GarageDeep + GarageThick * 2f));
            }
            walls.Box(new Vector3(GarageAt.x, midY, GarageAt.z - halfZ - skin),
                new Vector3(GarageWide, tall, GarageThick));
            // 前の壁はシャッターの口を空けて、左右とまぐさだけ立てる
            var jamb = halfX - ShutterWide * 0.5f;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (ShutterWide * 0.5f + jamb * 0.5f), midY, front),
                    new Vector3(jamb, tall, GarageThick));
            }
            walls.Box(new Vector3(GarageAt.x, (ShutterHigh + GarageHigh) * 0.5f, front),
                new Vector3(ShutterWide, GarageHigh - ShutterHigh, GarageThick));

            var roof = new Bank { Texel = 0.5f };
            roof.Box(new Vector3(GarageAt.x, GarageHigh + skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            // 柱は四隅と長辺の中ほど。壁から部屋の側へ出して、面の中に埋もれないようにする
            var posts = new Bank { Texel = 0.5f };
            var inset = halfX - PillarSide * 0.5f;
            var rows = new[] { GarageAt.z - halfZ + PillarSide * 0.5f, GarageAt.z, GarageAt.z + halfZ - PillarSide * 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var i = 0; i < rows.Length; i++)
                    posts.Box(new Vector3(GarageAt.x + side * inset, midY, rows[i]),
                        new Vector3(PillarSide, tall, PillarSide));
            }

            // シャッターは壁の厚みの中へ収める。壁と同じ面に置くと、縁でちらつく
            var door = new Bank { Texel = 0.5f };
            door.Box(new Vector3(GarageAt.x, (GarageFloorY + ShutterHigh) * 0.5f, front),
                new Vector3(ShutterWide, ShutterHigh - GarageFloorY, GarageThick * 0.5f));
            Slats(door, front - GarageThick * 0.25f);

            // 当たりは壁と床に要る。歩いて出られては困るし、床が無いと落ちる
            floor.Emit(parent, "Floor", Mat("GarageFloor"), true, Generated);
            walls.Emit(parent, "Walls", Mat("GarageWall"), true, Generated);
            roof.Emit(parent, "Ceiling", Mat("Concrete"), true, Generated);
            posts.Emit(parent, "Pillars", Mat("Concrete"), true, Generated);
            door.Emit(parent, "Shutter", Mat("Shutter"), true, Generated);

            Blocker(parent);
            Bays(parent);
            Covered(parent);
            Lamps(parent);
        }

        /// <summary>
        /// シャッターの桟。歩く線の正面にあるのに、今までただの平らな面だった。
        /// inner は壁の厚みの中にある内側の面の z。ここから部屋の側へ出す。
        /// 巻き上げ式なので横桟だけ。真ん中に一本だけ太いのが持ち手にあたる
        /// </summary>
        static void Slats(Bank bank, float inner)
        {
            const float pitch = 0.235f;
            const float depth = 0.035f;
            var z = inner - depth * 0.5f;
            for (var y = GarageFloorY + 0.14f; y < ShutterHigh - 0.05f; y += pitch)
            {
                var grip = Mathf.Abs(y - 1.15f) < pitch * 0.5f;
                bank.Box(new Vector3(GarageAt.x, y, z),
                    new Vector3(ShutterWide - 0.10f, grip ? 0.105f : 0.060f, depth));
            }
            // 下端のレール。床との境に影が落ちて、閉まっていることが読める
            bank.Box(new Vector3(GarageAt.x, GarageFloorY + 0.045f, z),
                new Vector3(ShutterWide - 0.04f, 0.090f, depth * 1.4f));
        }

        /// <summary>
        /// 床の飾り。歩いているあいだ画面の下半分を埋めるのは床なので、
        /// ここが空だと 8 m のあいだ見るものが何も無い。
        /// 区画の線・番号・油染み・排水口の 4 つだけ置く。
        ///
        /// どれも当たりは持たせない。歩く線の真上を通るので、少しでも高さを持つと
        /// 足が引っかかったように見える。区画が 5 つ並ぶこと自体が、
        /// ここが自分ひとりの車庫ではなく共用だという説明になっている
        /// </summary>
        static void Bays(Transform parent)
        {
            var paint = new Bank { Texel = 0.5f };
            // 区画の仕切り。内側の 4 本だけ引く。外側の 2 区画は壁が境になる
            foreach (var k in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
                Stripe(paint, BayPaintY, k * BayWide, BayZ, BayLine, BayDeep);
            // 突き当たりの止め線。ここまで入れて停める
            Stripe(paint, BayPaintY, GarageAt.x, BayZ - BayDeep * 0.5f, GarageWide - 0.4f, BayLine);
            // 区画の番号。入口の側に描く。奥から歩いてくる人の正面に来るので、
            // 歩いているあいだずっと目に入る
            for (var i = 0; i < 5; i++)
                Numeral(paint, (i - 2) * BayWide, BayZ + BayDeep * 0.5f + 0.75f, i + 1, BayPaintY);
            paint.Emit(parent, "BayPaint", Mat("BayPaint"), false, Generated);

            // 油染み。自分の区画、歩く線の上、覆いを掛けた隣の区画に 1 つずつ
            var oil = new Bank { Texel = 0.35f };
            Stain(oil, 0.10f, BayZ - 0.45f, 0.64f, 7311);
            Stain(oil, 3.05f, -4.30f, 0.34f, 4127);
            // 覆いを掛けた車の下は見えないので、その手前の空いたところへ落とす
            Stain(oil, CoveredBayX + 0.28f, BayZ + 2.70f, 0.42f, 9043);
            oil.Emit(parent, "OilStains", Mat("OilStain"), false, Generated);

            // 排水口。歩く線がちょうど踏む場所に置く。足の下を過ぎていくのが分かる
            var pan = new Bank { Texel = 0.5f };
            var grate = new Bank { Texel = 1.0f };
            Drain(pan, grate, 3.45f, -5.20f);
            pan.Emit(parent, "DrainPan", Mat("Drain"), false, Generated);
            grate.Emit(parent, "DrainGrate", Mat("Metal"), false, Generated);
        }

        /// <summary>床に貼る帯。上を向いた面 1 枚だけ。裏は誰も見ないので作らない</summary>
        static void Stripe(Bank bank, float y, float x, float z, float wide, float deep)
        {
            bank.FaceY(y, x - wide * 0.5f, x + wide * 0.5f, z - deep * 0.5f, z + deep * 0.5f, 1);
        }

        /// <summary>数字の 7 本の棒の組み合わせ。0 から 9 まで。上から順に a b c d e f g の位</summary>
        static readonly int[] Segments = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };

        /// <summary>
        /// 床に描く区画の番号。字の形（フォント）を 3D に持ち込まずに済むよう、
        /// 7 本の棒で組む。型で抜いた塗りなので、角の丸みが無くても嘘にならない。
        /// 読む向きの上は +z。奥から入口へ歩く人がそのまま読める
        /// </summary>
        static void Numeral(Bank bank, float x, float z, int n, float y)
        {
            const float wide = 0.44f;
            const float high = 0.72f;
            const float thick = 0.085f;
            var bits = Segments[Mathf.Clamp(n, 0, 9)];
            if ((bits & 1) != 0) Stripe(bank, y, x, z + high * 0.5f, wide, thick);
            if ((bits & 64) != 0) Stripe(bank, y, x, z, wide, thick);
            if ((bits & 8) != 0) Stripe(bank, y, x, z - high * 0.5f, wide, thick);
            if ((bits & 32) != 0) Stripe(bank, y, x - wide * 0.5f, z + high * 0.25f, thick, high * 0.5f);
            if ((bits & 2) != 0) Stripe(bank, y, x + wide * 0.5f, z + high * 0.25f, thick, high * 0.5f);
            if ((bits & 16) != 0) Stripe(bank, y, x - wide * 0.5f, z - high * 0.25f, thick, high * 0.5f);
            if ((bits & 4) != 0) Stripe(bank, y, x + wide * 0.5f, z - high * 0.25f, thick, high * 0.5f);
        }

        /// <summary>
        /// 油染み。四角い面を置くと床にただの黒い四角が乗って見えるので、
        /// 輪郭を崩して扇に張る（BuildAlley の水たまりと同じ作り）
        /// </summary>
        static void Stain(Bank bank, float x, float z, float span, int seed)
        {
            const int n = 16;
            var rnd = new System.Random(seed);
            var rim = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.PI * 2f * i / n;
                var r = span * (0.55f + (float)rnd.NextDouble() * 0.70f);
                rim[i] = new Vector2(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r * 1.25f);
            }
            bank.FanY(new Vector3(x, StainY, z), rim);
        }

        /// <summary>
        /// 排水口。床を抜く代わりに、暗い受けの上へ格子を並べて開いているように見せる。
        /// 抜いてしまうと下に何も無いので、歩いて覗き込まれたときに床の裏が見える
        /// </summary>
        static void Drain(Bank pan, Bank grate, float x, float z)
        {
            const float side = 0.52f;
            Stripe(pan, DrainY, x, z, side, side);
            // 枠。受けの縁を隠す
            for (var s = 0; s < 2; s++)
            {
                var away = s == 0 ? -1f : 1f;
                grate.Box(new Vector3(x + away * side * 0.5f, GrateY, z), new Vector3(0.06f, 0.012f, side));
                grate.Box(new Vector3(x, GrateY, z + away * side * 0.5f), new Vector3(side, 0.012f, 0.06f));
            }
            for (var i = -2; i <= 2; i++)
                grate.Box(new Vector3(x, GrateY, z + i * 0.093f), new Vector3(side - 0.10f, 0.012f, 0.035f));
        }

        /// <summary>
        /// 隣の区画の、覆いを掛けたままの車。共用のガレージだと一目で分かるものがこれ。
        /// 形は覆いの下の塊だけで、車そのものは作らない。
        /// 歩く線は x 1.3〜4.2 を通るので、左隣の区画に置けば道を塞がない
        /// </summary>
        static void Covered(Transform parent)
        {
            var tarp = new Bank { Texel = 0.8f };
            var x = CoveredBayX;
            var z = BayZ;
            // 下から順に細くしていく。角の立った箱を重ねると、布を掛けた丸みに近づく
            tarp.Box(new Vector3(x, 0.26f, z), new Vector3(1.80f, 0.44f, 4.10f));
            tarp.Box(new Vector3(x, 0.60f, z - 0.05f), new Vector3(1.70f, 0.30f, 3.90f));
            tarp.Box(new Vector3(x, 0.86f, z - 0.30f), new Vector3(1.48f, 0.26f, 2.30f));
            tarp.Box(new Vector3(x, 1.02f, z - 0.35f), new Vector3(1.20f, 0.12f, 1.80f));
            // 掛けた布の皺。上を横切る紐のあたりが盛り上がる
            foreach (var at in new[] { -1.35f, 0.20f, 1.55f })
                tarp.Box(new Vector3(x, 0.52f, z + at), new Vector3(1.86f, 0.52f, 0.09f),
                    Quaternion.Euler(0f, 0f, 2.5f));
            // 当たりを入れる。壁と同じで、すり抜けられては困る。
            // Garage の下にあるので、乗り込んだ瞬間にこれも一緒に消える
            tarp.Emit(parent, "CoveredCar", Mat("Tarp"), true, Generated);
        }

        /// <summary>
        /// 車体を塞ぐ箱。見えないので絵は持たせない。
        ///
        /// 車ではなくガレージの下に置く。<see cref="DriveDirector"/> は乗り込んだ瞬間に
        /// Garage を丸ごと伏せるので、この当たりもそこで消える。車の下に置くと、
        /// 座ったあとの CharacterController が生きた当たりの中に座ることになる。
        /// 場面 1 の SceneFlow.chairBlocker と同じ考え方
        /// </summary>
        static void Blocker(Transform parent)
        {
            var go = new GameObject("Blocker");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, (GarageFloorY + BlockTop) * 0.5f, (BlockBack + BlockFront) * 0.5f);
            box.size = new Vector3(BlockHalfX * 2f, BlockTop - GarageFloorY, BlockFront - BlockBack);
        }

        /// <summary>
        /// 天井の灯り。2 本。強さと届く範囲は仮置きで、オーナーが実画面を見てから決める。
        /// 影を落とさせないのは、WebGL で影を持つ灯りを増やすと重くなるため
        /// </summary>
        static void Lamps(Transform parent)
        {
            var tube = Shape("GarageTube", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.18f, 0.08f, 2.40f)));
            var lit = Glow(GarageLamp, 1.6f);
            for (var s = 0; s < 2; s++)
            {
                var lamp = Child(parent, "Lamp" + s);
                lamp.localPosition = new Vector3(s == 0 ? -3.0f : 3.0f, 2.8f, -2.0f);
                Piece(lamp, "Tube", tube, lit);
                var bulb = new GameObject("Light");
                bulb.transform.SetParent(lamp, false);
                var l = bulb.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = GarageLamp;
                l.intensity = 2.6f;
                l.range = 16f;
                l.shadows = LightShadows.None;
            }
        }

        // ---- 調べる対象 ------------------------------------------------------

        /// <summary>
        /// 調べる対象。判定点は物の少し手前に置く。位置は右ハンドルの車のもので、
        /// 運転席のドアと窓は x の正の側、助手席と上着のポケットは負の側にある。
        /// <see cref="LaneOffset"/> を裏返して左ハンドルにするなら、ここの x もまとめて裏返す。
        ///
        /// **高さは車内と一緒に上げ下げする。** どれも掴まっている物（座面・計器盤・内張り・鏡）から
        /// 割り出した数で、車高を上げたときもその物との差を保ったまま持ち上げた。
        /// 置き直したら、座った目（<see cref="SeatAt"/> + <see cref="EyeLead"/>）からの距離が
        /// <see cref="ItemRadius"/> の内側に残っているか測り直すこと。
        ///
        /// **必須にするのは garage.door と drive.window の 2 つだけ。** 最後の帯で窓の独白を
        /// 送り切ったところで SceneFlow が場面を閉じる前提になっている。ほかに必須を足すと、
        /// 閉じられないまま BandClock が一巡して戻り、古い入れ替えの知らせで
        /// 範囲の外を並べにいって走りが止まる。
        ///
        /// **二択を出すのも garage.door だけ。** SceneFlow.CloseChoice は最後に
        /// player.CanMove = standUp == null || standUp.Standing と書く。standAfter が空の
        /// この場面では standUp が null なので、どの二択を閉じてもプレイヤーが歩けるようになる。
        /// ドアは直後に Board が false へ戻すので構わないが、ほかの対象に二択を足すと
        /// 走っている車から歩いて降りられる。DriveDirector は Board のあと二度と CanMove を書かない
        /// </summary>
        static void Items(Transform root)
        {
            var parent = Child(root, "Items");
            Clear(parent);
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
                Debug.LogWarning("場面 8 の文面が無い。先に HalfAware/Write the drive script を走らせる: " + ScriptPath);

            // ガレージのドアだけは歩いて近づくので、ほかより遠くから拾える。
            // 高さは内張りの真ん中。x と z は動かさない。見直し 12 が立ち位置からここまでを
            // 「歩く線」として引いていて、排水口がその線に乗っているかを見ている
            Put(parent, "Door", new Vector3(0.92f, 1.13f, 0.10f), script, DriveIds.Door, DoorRadius, true);

            triggerItems = new GameObject[Bands];
            triggerItems[0] = Put(parent, "Chips", new Vector3(-0.42f, 1.15f, -0.02f), script, DriveIds.Chips, ItemRadius, false);
            // 背もたれの前面は z 0.17。0.24 に置くと判定点も印も背もたれの中に入って、
            // 印が座席に食われる（CheckDrive の見直し 4 が拾う）。座面の上へ出す
            triggerItems[1] = Put(parent, "Log", new Vector3(-0.42f, 1.17f, 0.14f), script, DriveIds.Log, ItemRadius, false);
            // 鏡そのものは y 1.79〜1.87 / z 0.73〜0.75。印は 0.17 上に出るので、
            // 鏡に合わせて置くと天井の板（1.88〜1.94）に食われる。手前と下へ外してある
            triggerItems[2] = Put(parent, "Mirror", new Vector3(0f, 1.66f, 0.66f), script, DriveIds.Mirror, ItemRadius, false);
            triggerItems[3] = Put(parent, "Photo", new Vector3(0.16f, 1.23f, 0.62f), script, DriveIds.Photo, ItemRadius, false);
            // 窓だけ必須。帯 4 に入るまで伏せてあるので、それまで場面は閉じない。
            // ドアの内張りは x 0.82〜0.90。0.84 に置くと印が内張りの中に入る
            triggerItems[4] = Put(parent, "Window", new Vector3(0.80f, 1.26f, 0.10f), script, DriveIds.Window, ItemRadius, true);
            // きっかけはその帯に入るまで出さない。出し分けるのは DriveDirector.ShowTrigger
            for (var i = 0; i < triggerItems.Length; i++) triggerItems[i].SetActive(false);

            // 帯を問わず置く、読んでも帯が進まない対象。
            // ひとつの入れ物にまとめて、乗り込むまで DriveDirector に伏せさせる。
            // 塞ぐ箱があってもガレージの立てる位置から 1.1〜1.3 m しか離れず、
            // 拾える距離 1.4 の内側に入ってしまう。once: true なので、ここで読まれると
            // 走行中に二度と出ない
            var cabin = Child(parent, "Cabin");
            Put(cabin, "Radio", new Vector3(-0.02f, 1.19f, 0.70f), script, DriveIds.Radio, ItemRadius, false);
            Put(cabin, "Pocket", new Vector3(-0.10f, 1.07f, -0.30f), script, DriveIds.Pocket, ItemRadius, false);
            Put(cabin, "Fuel", new Vector3(0.46f, 1.25f, 0.58f), script, DriveIds.Fuel, ItemRadius, false);
            cabin.gameObject.SetActive(false);
        }

        /// <summary>
        /// 調べる対象をひとつ立てる。どれも一度調べたら終わりで、前提は持たない。
        /// 二択は文面の側（DriveScript）が持つので、ここでは何も決めない
        /// </summary>
        static GameObject Put(Transform parent, string name, Vector3 at, RoomScript script,
            string id, float radius, bool required)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            // Interactable の OnValidate は AddComponent の中で走るので、
            // ここで「id がない」と一度警告が出る。id はこの直後に入れている
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("required").boolValue = required;
            so.FindProperty("once").boolValue = true;
            so.FindProperty("after").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---- プレイヤーと画面と進行 -------------------------------------------

        /// <summary>
        /// プレイヤーの rig・画面・進行の入れ物。ほかの場面では手で置いてあるものを、
        /// 場面 8 は空のシーンから組み上げるのでここで作る。
        ///
        /// 組み直すたびに作り直す。手で触った値は残らないが、そのぶん
        /// どの端末で組んでも同じものが立つ。Stage より先に呼ぶこと
        /// </summary>
        static void Rig()
        {
            // Task 8 が間に合わせに置いた根のカメラを落とす。
            // 残したまま rig を足すと、MainCamera の札と AudioListener が二つずつになる
            Drop("Main Camera");
            Drop("Player");
            Drop("Hud");
            Drop("SceneFlow");
            Drop("Pins");

            var player = new GameObject("Player");
            var body = player.AddComponent<CharacterController>();
            body.height = 1.7f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.85f, 0f);
            body.slopeLimit = 45f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.08f;
            body.minMoveDistance = 0.001f;

            var eye = new GameObject("Main Camera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, PlayerController.StandingEyeHeight, EyeLead);
            eye.transform.localRotation = Quaternion.identity;
            eye.tag = "MainCamera";
            eye.AddComponent<Camera>();
            // 耳は場面にひとつだけ。カメラと同じ所へ付ける
            eye.AddComponent<AudioListener>();

            var walker = player.AddComponent<PlayerController>();
            var pso = new SerializedObject(walker);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (actions == null) Debug.LogWarning("入力の割り当てが無い: " + ActionsPath);
            pso.FindProperty("actions").objectReferenceValue = actions;
            pso.FindProperty("eye").objectReferenceValue = eye.transform;
            pso.FindProperty("eyeLead").floatValue = EyeLead;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // 当たりを入れたまま動かすと床や壁に押し出されて狙った場所に立たない（BuildAlley.Place と同じ）
            body.enabled = false;
            player.transform.position = new Vector3(StandAt.x, GarageFloorY + 0.06f, StandAt.z);
            player.transform.rotation = Quaternion.Euler(0f, StandYaw, 0f);
            body.enabled = true;

            Marks(Flow(walker, Screen()));
        }

        /// <summary>
        /// 調べられる物に立てるピン。場面 1 と場面 2 は BuildProps.BuildPins が立てていて、
        /// 場面 8 にだけ無かった。ピンが無いと、何を調べられるのかが画面のどこにも出ない。
        ///
        /// **BuildProps.BuildPins をそのまま呼ばない。** あちらは走るたびにマテリアルの色を
        /// 書き戻すので、オーナーが赤に直した PinHead.mat が琥珀色へ戻り、
        /// 場面 1 と場面 2 のピンまで一緒に変わる。ここでは同じ mesh と同じマテリアルを
        /// 読んで組み立てるだけにする。色を決めるのはあくまでオーナーで、
        /// 直せば三つの場面が揃って変わる。
        ///
        /// 場面の根に置く。BuildProps も根に置いているし、Player・Hud・SceneFlow も根にある。
        /// Prune が落とすのは Drive の下の子だけなので、根のこれには届かない。
        /// 組み直すたびに Rig の Drop("Pins") で作り直す
        /// </summary>
        static void Marks(SceneFlow flow)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PinMesh);
            var hole = AssetDatabase.LoadAssetAtPath<Mesh>(PinHoleMesh);
            var head = AssetDatabase.LoadAssetAtPath<Material>(PinHeadMat);
            var dark = AssetDatabase.LoadAssetAtPath<Material>(PinHoleMat);
            if (mesh == null || hole == null || head == null || dark == null)
            {
                Debug.LogWarning("ピンの形かマテリアルが無い。先に HalfAware/Build the interaction pins を走らせる");
                return;
            }
            var root = new GameObject("Pins");
            var source = new GameObject("PinSource");
            source.transform.SetParent(root.transform, false);
            source.AddComponent<MeshFilter>().sharedMesh = mesh;
            var lit = source.AddComponent<MeshRenderer>();
            lit.sharedMaterial = head;
            // ピンは影を落とさない。暗い車内で自分の影が対象に掛かる
            lit.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var eye = Piece(source.transform, "Hole", hole, dark);
            eye.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 複製のもと。切ったまま置いておく（PinMarkers.Start が複製する）
            source.SetActive(false);

            var markers = root.AddComponent<PinMarkers>();
            var so = new SerializedObject(markers);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pin").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>同じ名前の根を落とす</summary>
        static void Drop(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) Object.DestroyImmediate(go);
        }

        /// <summary>
        /// 字幕・印・暗転・幕・ログ。作りも値もほかの場面の Hud に揃えてある。
        /// 並び順がそのまま重なりの順になるので、暗転より後に中央の文字を置く
        /// </summary>
        static HudView Screen()
        {
            var go = new GameObject("Hud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudView));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning("字の形が無い: " + FontPath);
            var mincho = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MinchoPath);
            if (mincho == null) Debug.LogWarning("字の形が無い: " + MinchoPath);

            var band = Layer(go.transform, "SubtitleBand", new Color(0f, 0f, 0f, 0.75f), true);
            Frame(band, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(0f, 120f));
            var subtitle = Line(band, "Subtitle", font, 28f, Color.white, TextAlignmentOptions.Left);
            Frame(subtitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-160f, -24f));

            var prompt = Line(go.transform, "Prompt", font, 22f, Color.white, TextAlignmentOptions.Center);
            Frame(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(800f, 40f));

            var log = Layer(go.transform, "LogPanel", new Color(0.02f, 0.02f, 0.025f, 0.88f), true);
            Frame(log, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.93f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var title = Line(log, "Title", font, 22.4f, new Color(0.62f, 0.64f, 0.68f), TextAlignmentOptions.TopLeft);
            title.text = "ログ　　Tab で閉じる";
            Frame(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(-76f, 48f));
            var logText = Line(log, "Text", font, 24.08f, new Color(0.86f, 0.87f, 0.89f), TextAlignmentOptions.TopLeft);
            Frame(logText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -28f), new Vector2(-80f, -116f));
            log.gameObject.SetActive(false);

            var fade = Layer(go.transform, "Fade", new Color(0f, 0f, 0f, 0f), false);
            Frame(fade, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var curtain = Layer(go.transform, "Curtain", new Color(0f, 0f, 0f, 1f), false);
            Frame(curtain, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            curtain.gameObject.SetActive(false);

            // 「続く」もここに出る。暗転中の字だけ明朝にするので、この層だけ font を渡さない
            var centre = Line(go.transform, "Center", mincho, 40f, Color.white, TextAlignmentOptions.Center);
            Frame(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 80f));

            var hud = go.GetComponent<HudView>();
            var so = new SerializedObject(hud);
            so.FindProperty("subtitleBand").objectReferenceValue = band.gameObject;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("subtitleRowHeight").floatValue = 44f;
            so.FindProperty("subtitlePadding").floatValue = 34f;
            so.FindProperty("promptText").objectReferenceValue = prompt;
            so.FindProperty("centerText").objectReferenceValue = centre;
            so.FindProperty("fadeLayer").objectReferenceValue = fade.GetComponent<Image>();
            so.FindProperty("curtainLayer").objectReferenceValue = curtain.GetComponent<Image>();
            so.FindProperty("logPanel").objectReferenceValue = log.gameObject;
            so.FindProperty("logText").objectReferenceValue = logText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        /// <summary>塗り潰しの層</summary>
        static RectTransform Layer(Transform parent, string name, Color col, bool blocks)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = blocks;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>文字の層</summary>
        static TMP_Text Line(Transform parent, string name, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = col;
            text.alignment = align;
            text.text = "";
            return text;
        }

        /// <summary>層の位置と大きさ。アンカーとピボットをまとめて決める</summary>
        static void Frame(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 at, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// 場面の進行。SceneFlow と DriveDirector を同じ入れ物に置く（AlleyDirector と同じ構え）。
        ///
        /// **standAfter は空のままにする。** 何か入れると Stand が毎フレーム player.CanMove を
        /// 書くので、Board が false にしたものを歩ける側へ戻してしまう。
        /// 車内は座ったままなので、立ち上がりの段取りはそもそも要らない
        /// </summary>
        static SceneFlow Flow(PlayerController player, HudView hud)
        {
            var go = new GameObject("SceneFlow");
            var flow = go.AddComponent<SceneFlow>();
            go.AddComponent<DriveDirector>();
            var so = new SerializedObject(flow);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("hud").objectReferenceValue = hud;
            // 眩暈は場面 1 のもの。車内では使わない
            so.FindProperty("daze").objectReferenceValue = null;
            so.FindProperty("maxAngle").floatValue = InteractionPicker.MaxAngle;
            // 次の場面（場面 9）はまだ無い。必須を済ませたら「続く」で止まる
            so.FindProperty("nextScene").stringValue = "";
            so.FindProperty("openingCard").stringValue = "";
            so.FindProperty("standAfter").stringValue = "";
            so.FindProperty("standSpot").objectReferenceValue = null;
            so.FindProperty("chairBlocker").objectReferenceValue = null;
            so.FindProperty("chair").objectReferenceValue = null;
            so.FindProperty("body").objectReferenceValue = null;
            so.FindProperty("exitSound").objectReferenceValue = null;
            so.FindProperty("cutToBlack").boolValue = false;
            so.FindProperty("dazeUntil").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            return flow;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>
        /// DriveWorld にタイルと沿道と対向車を、DriveDirector にプレイヤー・画面・文面・
        /// ガレージ・運転席・帯を渡す。private な [SerializeField] なので
        /// SerializedObject 越しに書く
        /// </summary>
        static void Wire(Transform root)
        {
            var world = root.GetComponent<DriveWorld>();
            if (world == null) world = root.gameObject.AddComponent<DriveWorld>();
            var so = new SerializedObject(world);
            Fill(so.FindProperty("tiles"), Kids(root.Find("Road")));
            Fill(so.FindProperty("roadsides"), Kids(root.Find("Roadsides")));
            Fill(so.FindProperty("oncoming"), Kids(root.Find("Oncoming")));
            // 寸法は組み立てとひとつの数から出す。Inspector で別々に持つと片方だけ直して隙間が開く
            so.FindProperty("tileLength").floatValue = TileLength;
            so.FindProperty("behind").floatValue = Behind;
            so.FindProperty("oncomingRate").floatValue = OncomingRate;
            // 揺れは DriveWorld が PlayerController.EyeOffset へ渡す。
            // 場面 8 では EyeSway を付けないので、そこを書くのはここひとつだけ
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            so.FindProperty("shake").floatValue = Shake;
            so.FindProperty("shakeRate").floatValue = ShakeRate;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);

            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogWarning("SceneFlow が無い。DriveDirector を繋げない"); return; }
            var director = flow.GetComponent<DriveDirector>();
            if (director == null) director = flow.gameObject.AddComponent<DriveDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("flow").objectReferenceValue = flow;
            dso.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>();
            dso.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            dso.FindProperty("script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            dso.FindProperty("world").objectReferenceValue = world;
            // 乗り込んだら丸ごと伏せる。ガレージの中身はこの入れ物ひとつに収めてある
            var garage = Look(root, "Garage");
            dso.FindProperty("garage").objectReferenceValue = garage != null ? garage.gameObject : null;
            dso.FindProperty("seat").objectReferenceValue = Look(root, "Car/Seat");
            // 車内の対象は乗り込むまで伏せる。ガレージからでも拾える距離に入ってしまう
            var cabin = Look(root, "Items/Cabin");
            dso.FindProperty("cabin").objectReferenceValue = cabin != null ? cabin.gameObject : null;
            var folded = Look(root, "Car/ArmsFolded");
            var onWheel = Look(root, "Car/ArmsOnWheel");
            dso.FindProperty("folded").objectReferenceValue = folded != null ? folded.gameObject : null;
            dso.FindProperty("onWheel").objectReferenceValue = onWheel != null ? onWheel.gameObject : null;
            // 手動で運転するのは最後の帯だけ
            dso.FindProperty("drivenBand").intValue = Bands - 1;
            // 空と灯り。帯ごとの値は bands が持ち、差し替える先をここで渡す。
            // 日射しとカメラは Drive の下に無いので、名前で引く
            var lamp = GameObject.Find("Directional Light");
            dso.FindProperty("sun").objectReferenceValue = lamp != null ? lamp.GetComponent<Light>() : null;
            var view = GameObject.Find("Player/Main Camera");
            dso.FindProperty("eye").objectReferenceValue = view != null ? view.GetComponent<Camera>() : null;
            FillSky(dso.FindProperty("garageSky"), GarageSky);
            var overhead = Look(root, "Sky");
            Fill(dso.FindProperty("skies"), overhead != null ? Kids(overhead) : new Transform[0]);
            FillBands(dso.FindProperty("bands"));
            var picked = dso.FindProperty("triggers");
            picked.arraySize = triggerItems.Length;
            for (var i = 0; i < triggerItems.Length; i++)
                picked.GetArrayElementAtIndex(i).objectReferenceValue = triggerItems[i];
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(flow);
        }

        /// <summary>
        /// 帯の値を書き込む。DriveBand は struct なので、配列の要素ごとに中身を並べ直す。
        /// 秒数はすべて仮置きで、オーナーが再生しながら Inspector で決める
        /// </summary>
        static void FillBands(SerializedProperty row)
        {
            row.arraySize = Route.Length;
            for (var i = 0; i < Route.Length; i++)
            {
                var e = row.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = Route[i].name;
                e.FindPropertyRelative("trigger").stringValue = Route[i].trigger;
                e.FindPropertyRelative("speed").floatValue = Route[i].speed;
                e.FindPropertyRelative("rough").floatValue = Route[i].rough;
                e.FindPropertyRelative("afterglow").floatValue = Route[i].afterglow;
                e.FindPropertyRelative("black").floatValue = Route[i].black;
                e.FindPropertyRelative("fadeIn").floatValue = Route[i].fadeIn;
                FillSky(e.FindPropertyRelative("sky"), Route[i].sky);
            }
        }

        /// <summary>空と灯りを 1 つ書き込む。DriveSky も struct なので中身を並べ直す</summary>
        static void FillSky(SerializedProperty at, DriveSky from)
        {
            if (at == null) return;
            at.FindPropertyRelative("sky").colorValue = from.sky;
            at.FindPropertyRelative("haze").colorValue = from.haze;
            at.FindPropertyRelative("density").floatValue = from.density;
            at.FindPropertyRelative("sun").colorValue = from.sun;
            at.FindPropertyRelative("power").floatValue = from.power;
            at.FindPropertyRelative("aim").vector3Value = from.aim;
            at.FindPropertyRelative("lift").colorValue = from.lift;
            at.FindPropertyRelative("ground").colorValue = from.ground;
        }

        /// <summary>繋ぎ先を引く。黙って null を渡すと、再生して初めて気づくことになる</summary>
        static Transform Look(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null) Debug.LogWarning("繋ぎ先が見つからない: " + path);
            return t;
        }

        static Transform[] Kids(Transform parent)
        {
            var all = new Transform[parent.childCount];
            for (var i = 0; i < all.Length; i++) all[i] = parent.GetChild(i);
            return all;
        }

        static void Fill(SerializedProperty row, Transform[] all)
        {
            row.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }

        // ---- 道具 ----------------------------------------------------------

        /// <summary>
        /// 溜めた面を mesh のアセットにして返す。Bank.Emit は場面に物まで置くが、
        /// ここで要るのは形だけ。同じ形を何十も並べるので、mesh は 1 つを使い回す
        /// </summary>
        static Mesh Bake(Bank bank, string name)
        {
            var scratch = new GameObject("BakeScratch");
            var made = bank.Emit(scratch.transform, name, null, false, Generated);
            var mesh = made != null ? made.GetComponent<MeshFilter>().sharedMesh : null;
            Object.DestroyImmediate(scratch);
            return mesh;
        }

        /// <summary>名前で引ける形。同じ名前なら二度焼かない</summary>
        static Mesh Shape(string name, float texel, System.Action<Bank> draw)
        {
            Mesh mesh;
            if (shapes.TryGetValue(name, out mesh) && mesh != null) return mesh;
            var bank = new Bank { Texel = texel };
            draw(bank);
            mesh = Bake(bank, name);
            shapes[name] = mesh;
            return mesh;
        }

        /// <summary>形を 1 つ置く</summary>
        static Transform Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go.transform;
        }

        static Transform Root(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// シーンに直に置く物。カメラと日射しは Drive の下に入れない。
        /// 根だけを見るのは、GameObject.Find が Player の下の同じ名前を掴んでしまうため。
        /// 掴んだうえで根へ引き出すと、rig からカメラを抜き取ることになる
        /// </summary>
        static GameObject Loose(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) return go;
            return new GameObject(name);
        }

        /// <summary>知らない子を落とす。組み方を変えたときに前の束が残らないように</summary>
        static void Prune(Transform parent, string[] keep)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (System.Array.IndexOf(keep, c.name) >= 0) continue;
                Object.DestroyImmediate(c.gameObject);
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>
        /// 素材ごとのマテリアル。組み直すたびに色と艶を結び直すので、
        /// Inspector で触った値は残らない。詰めるならこの表を直す
        /// </summary>
        static Material Mat(string name)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            Color col;
            float smooth;
            Tone(name, out col, out smooth);
            // 絵があれば貼る。無ければ色だけ。組み直すたびに結び直すので、
            // Assets/Textures へ置くだけで差し替わる（BuildAlley と同じ構え）
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Drive" + name + ".png");
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, col.a));
            }
            else
            {
                m.SetTexture("_BaseMap", null);
                m.SetColor("_BaseColor", col);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", name == "Metal" ? 0.50f : 0f);
            if (col.a < 1f) SeeThrough(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// メーターの面のマテリアル。ほかの素材と違って Unlit で貼る。
        ///
        /// 灯りを一つも足さずに「夜に自分で光っているもの」を車内へ置ける。
        /// <see cref="DialGain"/> がそのまま画面の明るさになるので、
        /// 針と目盛りだけが浮いて、盤の地は夜の車内に沈んだままになる。
        ///
        /// 絵は板いっぱいに一度だけ貼る。Bank は実寸に Texel を掛けた uv を振るので、
        /// Texel 1 の板に対しては uv がそのまま長さ（m）になる。それを _BaseMap_ST で
        /// 0〜1 へ畳み直す。横の倍率が負なのは、Bank の -z 向きの面が u を
        /// 右から左へ流すため（<see cref="Panel"/>）
        /// </summary>
        static Material DialMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var path = Materials + "CarDials.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "CarDials";
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/DriveCarDials.png");
            if (tex == null) Debug.LogWarning("メーターの絵が無い: Assets/Textures/DriveCarDials.png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(DialGain, DialGain, DialGain, 1f));
            m.SetTextureScale("_BaseMap", new Vector2(-1f / DialWide, 1f / DialHigh));
            m.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 麦のマテリアル。ほかの素材と違って URP の Lit を使わない。
        ///
        /// 要るものが二つある。ひとつは頂点をずらして微風になびかせること。株は区切り 1 つに
        /// 200 近くを 1 枚の mesh へ焼いてあるので、Transform では動かせない。
        /// もうひとつは光の回り込みで、箱で作った株は面の向きが四方向しか無く、
        /// 素の Lambert だと日射しに背を向けた面が真っ黒に落ちて畑が市松模様に見える。
        ///
        /// 揺れの数は <see cref="WheatWind"/> から取る。見直しが同じ数を読んで、
        /// なびいた穂先が轍へ倒れ込まないかを測る
        /// </summary>
        static Material WheatMat()
        {
            var shader = Shader.Find("HalfAware/Wheat");
            if (shader == null)
            {
                Debug.LogWarning("HalfAware/Wheat が見つからない。麦はなびかない");
                return Mat("Field");
            }
            var path = Materials + "Wheat.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "Wheat";
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。前に URP の Lit で作ってあっても差し替わる
            m.shader = shader;
            // 黄金色。**この場面のほかの色（どれも 0.2 前後）から大きく外している。**
            // 朝の日射しを受けて初めて成り立つ色で、帯 4 のほかには出てこない
            m.SetColor("_BaseColor", new Color(0.550f, 0.410f, 0.145f));
            m.SetColor("_TipColor", new Color(0.870f, 0.720f, 0.330f));
            m.SetFloat("_Wrap", 0.60f);
            m.SetFloat("_Glow", 0.38f);
            m.SetFloat("_SwayAmp", WheatWind.Amp);
            m.SetFloat("_SwayFlutter", WheatWind.Flutter);
            m.SetFloat("_SwayAcross", WheatWind.Across);
            m.SetFloat("_SwayAlong", WheatWind.Along);
            m.SetFloat("_SwayRate", WheatWind.Rate);
            m.SetFloat("_SwayFlutterRate", WheatWind.FlutterRate);
            m.SetFloat("_SwayHigh", WheatWind.High);
            m.SetFloat("_SwaySide", WheatWind.Side);
            // 時刻はずらさない。絵を撮るときだけ外から動かす
            m.SetFloat("_SwayShift", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>透ける面にする。向こうが見えないと車内が箱にしか見えない</summary>
        static void SeeThrough(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>
        /// 素材ごとの色と艶。夜と明け方しか無い場面なので、地の色はどれも暗い。
        /// 明るさは帯ごとに Volume（Color Adjustments）で寄せる前提で、ここでは触らない
        /// </summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                // 内装。絵（DriveCarTrim.png）を貼るので、画面に出る明るさは絵の平均が持つ。
                // ここの色は絵が無いときの控えで、絵と同じ明るさに合わせてある。
                // 乗用車だった頃の 0.085 から上げたのは、暗いだけの面では夜に形が読めないため
                case "CarTrim": col = new Color(0.125f, 0.121f, 0.128f); smooth = 0.18f; break;
                case "CarSeat": col = new Color(0.140f, 0.121f, 0.106f); smooth = 0.10f; break;
                // 塗った鉄。輻・取っ手・摘み・止めねじ。**車内で唯一明るい素材。**
                // 内装の 2.3 倍あるので、夜の帯でも輪郭が残る
                case "CarSteel": col = new Color(0.288f, 0.262f, 0.208f); smooth = 0.28f; break;
                // 継ぎ目と窪み。絵を持たず、ただ暗い。板と板の境をこれで引く
                case "CarGap": col = new Color(0.020f, 0.019f, 0.021f); smooth = 0.05f; break;
                case "CarGlass": col = new Color(0.55f, 0.60f, 0.66f, 0.12f); smooth = 0.85f; break;
                // ボンネット。褪せて白茶けた塗りの鉄板。
                // **ここだけ夜の場面の中で浮くほど明るい。** 車内の色（0.09）で塗ると、
                // 実際に組んで測ったところ画面では路面と同じ 0.19 になり、ボンネットが丸ごと道に溶けた。
                // この場面は空も道も内装も 0.1 前後に固まっていて、形を読ませる手掛かりが明暗しか無い。
                // 0.47 まで上げて初めて路面の 0.19 に対して 0.22 へ離れ、前の縁が線として出る。
                // 古いオフロード車は石灰色や砂色に褪せているものなので、色の選び方としても外れていない
                case "CarBody": col = new Color(0.470f, 0.482f, 0.432f); smooth = 0.18f; break;
                // 前腕。革のライダースの袖。BuildProtagonist の上着と同じ色にしてある
                case "Sleeve": col = new Color(0.055f, 0.053f, 0.062f); smooth = 0.22f; break;
                // 手。夜の車内なので、肌も袖よりわずかに明るい程度に留める
                case "Skin": col = new Color(0.235f, 0.190f, 0.165f); smooth = 0.12f; break;
                case "Asphalt": col = new Color(0.115f, 0.118f, 0.132f); smooth = 0.16f; break;
                // 塗り直されていない白線。真っ白だと夜の道で浮く
                case "RoadLine": col = new Color(0.520f, 0.510f, 0.470f); smooth = 0.10f; break;
                // 舗装の外の地面と路肩。舗装より暗く、少し土を帯びた色にして道の縁を読ませる
                case "Verge": col = new Color(0.078f, 0.074f, 0.066f); smooth = 0.06f; break;
                case "Concrete": col = new Color(0.150f, 0.150f, 0.155f); smooth = 0.10f; break;
                // ガレージ。共用の車庫なので、床は油が染みて壁の塗りも褪せている
                case "GarageFloor": col = new Color(0.098f, 0.096f, 0.094f); smooth = 0.14f; break;
                case "GarageWall": col = new Color(0.128f, 0.128f, 0.133f); smooth = 0.07f; break;
                case "Shutter": col = new Color(0.155f, 0.150f, 0.140f); smooth = 0.24f; break;
                // 区画の線と番号。塗り直されていない白なので、床よりわずかに明るい程度
                case "BayPaint": col = new Color(0.310f, 0.300f, 0.262f); smooth = 0.08f; break;
                // 油染み。地の色は床より暗く、艶だけが残る
                case "OilStain": col = new Color(0.030f, 0.029f, 0.031f); smooth = 0.58f; break;
                // 排水口の受け。中は見えないので、ただ暗い
                case "Drain": col = new Color(0.040f, 0.039f, 0.038f); smooth = 0.14f; break;
                // 隣の車に掛かった覆い。埃をかぶった帆布
                case "Tarp": col = new Color(0.168f, 0.166f, 0.172f); smooth = 0.05f; break;
                // 濡れた路面。艶だけの面は映る物が無いと穴に見えるので、地の明るさを持たせる
                case "Sheen": col = new Color(0.100f, 0.108f, 0.128f); smooth = 0.86f; break;
                case "Metal": col = new Color(0.085f, 0.088f, 0.095f); smooth = 0.26f; break;
                case "Tree": col = new Color(0.045f, 0.042f, 0.040f); smooth = 0.08f; break;
                case "Stone": col = new Color(0.165f, 0.162f, 0.150f); smooth = 0.08f; break;
                case "Grass": col = new Color(0.062f, 0.085f, 0.052f); smooth = 0.08f; break;
                // 麦はここに無い。URP の Lit ではなく HalfAware/Wheat で塗るので、
                // 色は WheatMat が持っている
                //
                // 畑の地。株のあいだから覗く面なので、株より暗く沈んだ金色にする。
                // 株と同じ明るさにすると畑が一枚の板になり、立っている感じが消える
                case "Field": col = new Color(0.400f, 0.300f, 0.125f); smooth = 0.06f; break;
                // 土と轍は帯 4 でしか使わない。夜の帯の暗さに合わせる必要が無いので、
                // 朝日の下で土の色に見えるところまで上げてある。ここを 0.1 台へ戻すと、
                // 黄金色の畑のあいだを灰色の帯が抜けていくことになる
                case "Dirt": col = new Color(0.300f, 0.238f, 0.160f); smooth = 0.06f; break;
                // 踏み固められた轍。地の土より暗く湿っている
                case "Rut": col = new Color(0.215f, 0.168f, 0.118f); smooth = 0.14f; break;
                default: col = new Color(0.12f, 0.12f, 0.13f); smooth = 0.30f; break;
            }
        }

        /// <summary>
        /// 自分で光って見える面。灯りを何十も置くと WebGL では持たないので、
        /// ネオンも街灯の頭も明るい Unlit で済ませる
        /// </summary>
        static Material Glow(Color col, float gain)
        {
            var key = string.Format("Glow_{0:000}_{1:000}_{2:000}_{3:00}",
                (int)(col.r * 255), (int)(col.g * 255), (int)(col.b * 255), (int)(gain * 10));
            var path = Materials + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.name = key;
            m.SetColor("_BaseColor", new Color(col.r * gain, col.g * gain, col.b * gain, 1f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
