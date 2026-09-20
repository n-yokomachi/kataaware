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
    /// 帯はここでは 0 から数える。設計書が「帯 1」と呼ぶものが 0 番にあたる。
    ///
    /// 大きいので partial に割ってある。ここには寸法の定数・組み立ての入口・シーンの地・
    /// 調べる対象・プレイヤーと画面・繋ぎ込み・道具を置く。形を作るところは
    /// <c>BuildDriveCar.cs</c>（車）・<c>BuildDriveLand.cs</c>（道と沿道と空）・
    /// <c>BuildDriveGarage.cs</c>（ガレージ）にある
    /// </summary>
    public static partial class BuildDrive
    {
        public const string Materials = "Assets/Materials/Drive/";
        public const string Generated = "Assets/Models/generated/drive/";
        public const string ScenePath = "Assets/Scenes/Drive.unity";

        // ---- 道の寸法。メートル --------------------------------------------
        //
        // **前と後ろの割り振りだけを動かす。** 環一周の長さ（<see cref="Span"/>）は
        // Ahead - Behind をタイルの長さで切り上げたものなので、前を縮めたぶんだけ
        // 後ろを伸ばせば 180 m のまま動かない。180 は沿道の間隔（60 / 45 / 36 / 30 /
        // 20 / 12）の最小公倍数で、ここを動かすと <c>BuildDriveLand.cs</c> の間隔を
        // 一つ残らず引き直すことになる。タイルの枚数も区切りの数も変わらないので、
        // レンダラーも三角も一つも増えない

        /// <summary>タイル 1 枚の長さ。2 の冪と相性の良い数にする。20 なら計算に丸めが入らない</summary>
        public const float TileLength = 20f;
        /// <summary>
        /// 前方にここまで途切れず敷く。
        ///
        /// **140 から縮めた。** 後ろを伸ばすぶんをここから取れば環一周の長さが変わらない。
        /// 縮めても絵がほとんど動かないのは、遠いところは霧が畳んでいるため。
        /// 一番薄い帯 1（夜の高速）と帯 4（朝靄）でも、ここまで来ると 58% 霞む。
        /// 道の先が地平の一点に集まるのも効いていて、110 m の路面は目線から
        /// 0.8 度しか下がっていない。140 m（0.6 度）との差は数画素しかない
        /// </summary>
        public const float Ahead = 110f;
        /// <summary>
        /// 車の後ろにここまで残す。負の値。
        ///
        /// **-30 では近すぎた。** 環の端はタイルの刻みに乗るので、走っているあいだ
        /// 後ろの端は Behind から Behind + TileLength までを行ったり来たりする。
        /// -30 だと位相によっては車の 10 m 後ろで世界が切れていて、脇の窓から
        /// 後ろを見ると小麦畑がそこで唐突に消えた。-70 なら悪いときでも 50 m、
        /// 良いときで 70 m 後ろまで残る
        /// </summary>
        public const float Behind = -70f;

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

        /// <summary>
        /// 止まったままエンジンだけ掛かっているときに、揺れの位相を進める速さ。m/s 相当。
        ///
        /// 揺れの位相は本来なら走った距離から取る（<see cref="RoadShake"/>）。
        /// 止まっているあいだは距離が進まないので、そのままでは震えが凍りつく。
        /// ここで時間から位相を作る。7 なら ShakeRate 1.0 で基本の波長が
        /// およそ 1.1 Hz。エンジンの回転そのものではなく、車体が揺すられる周期にあたる
        /// </summary>
        public const float IdleRate = 7f;

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
        /// <summary>
        /// 路面に落ちる灯りの板。街灯の溜まり・前照灯の照らし・ネオンの映り込み。
        /// 白線より上でないと、加算の板が深度で弾かれて何も乗らない。
        /// 帯 4 の土（0.036）とは帯が違うので画面で重なることは無いが、
        /// 梯子（<see cref="CheckDrive"/> 13）は 5 帯ぶんを一列に並べて見るので、
        /// 上下どちらへも 8 mm 空けたここに置く
        /// </summary>
        public const float GlowY = 0.028f;
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
        /// <summary>畑が平らでいる距離。道の中心から、m。ここより内側は起伏を付けない</summary>
        public const float SwellFrom = 12f;
        /// <summary>
        /// 起伏が立ち上がり切るまでの距離。m。SwellFrom のところで滑らかに 0 へ落とす。
        /// 段で繋ぐと、道の脇に土手が立ち上がったように見える
        /// </summary>
        public const float SwellEase = 16f;
        /// <summary>丘ぜんたいの持ち上がり。SwellFrom から先の距離の二乗に掛ける</summary>
        public const float SwellRate = 0.00042f;
        /// <summary>
        /// 主な背の高さ。m。**この場面の地平はこれが決める。**
        /// 座った目（1.55）から 78 m 先に 8 m ほどの背が来るので、稜線は水平から
        /// およそ 5 度上がったところに出る。ここより先の地面はこの背に隠れるので、
        /// 畑の外れ（<see cref="FieldHalf"/>）が絵に出ることは無い
        /// </summary>
        public const float CrestHigh = 6.6f;
        /// <summary>主な背までの距離。SwellFrom から、m</summary>
        public const float CrestAt = 66f;
        /// <summary>主な背の広がり。m。大きくするほど緩い斜面になる</summary>
        public const float CrestWide = 34f;
        /// <summary>
        /// 手前のうねりの高さ。m。背が一つだけだと斜面が一枚の板に見えるので、
        /// 途中にもう一段だけ入れて稜線を重ねる
        /// </summary>
        public const float FoldHigh = 1.2f;
        /// <summary>手前のうねりまでの距離。SwellFrom から、m</summary>
        public const float FoldAt = 35f;
        /// <summary>手前のうねりの広がり。m</summary>
        public const float FoldWide = 18f;
        /// <summary>
        /// 畝の深さ。その場の丘の高さに対する割合。道際では丘が 0 なので畝も出ない。
        /// これが無いと稜線が定規を当てた曲線になる
        /// </summary>
        public const float FurrowDeep = 0.20f;
        /// <summary>
        /// 畝の位相が道から遠ざかる向きに進む割合。rad/m。
        /// 0 にすると畝が道と平行な縞になるので、斜めに走らせる
        /// </summary>
        public const float FurrowSlant = 0.13f;
        /// <summary>
        /// 畑の地の裾が道へ寄る距離。m。ここから外は畑の地、内側は踏み固められた土の肩。
        ///
        /// 一番内側の株（3.30、札の半幅 0.34、風の振れ 0.25）が 2.71 まで寄るので、
        /// 裾はそれより内側に取る。轍（<see cref="DirtHalf"/> 2.3）との間に
        /// 0.5 m の土が残る
        /// </summary>
        public const float HemFrom = 2.8f;

        /// <summary>
        /// 畑の地を割る升目の、道からの距離。m。
        /// 起伏を追わせるので 1 枚の面では張れない。近くは細かく、遠くは粗く割る。
        /// <see cref="HemFrom"/> からここの頭までは、起伏の無い裾が別に埋める
        /// </summary>
        public static readonly float[] FieldRows =
        {
            4.8f, 6f, 7.5f, 9.5f, 12f, 15f, 18f, 22f, 26f, 31f, 37f,
            44f, 52f, 61f, 71f, 82f, 94f, 107f, 121f, 135f, FieldHalf,
        };
        /// <summary>畑の地の升目を z 方向に割る数。畝（20 m でひと回り）を追える細かさが要る</summary>
        public const int FieldSteps = 10;
        /// <summary>
        /// 畑の地の絵の刻み。1 m あたり。TileLength を掛けて整数にならないと、
        /// 区切りの継ぎ目で絵柄が途切れる。0.15 × 20 = 3
        /// </summary>
        public const float FieldTexel = 0.15f;
        /// <summary>
        /// 株の絵の刻み。1 m あたり。札の横幅だけがこれで決まる。
        /// 縦は必ず 0→1 で、絵が根から穂先までを 1 枚に収めている。
        /// 絵 1 枚に 13 本描いてあるので、1.25 なら 0.8 m にひと並び、およそ 1 m に 16 本。
        /// 札の幅が変わってもこの密度は変わらない。近くも遠くも同じ混み具合に見える。
        /// 1.5 から下げた。描画解像度が 1/3 なので、細かく刻むと穂が 1 画素に満たず、
        /// 麦ではなく刻んだ藁に見える
        /// </summary>
        public const float WheatTexel = 1.25f;
        /// <summary>
        /// 株ひと群につき札を何枚交差させるか。
        ///
        /// 1 枚だと、脇を通り過ぎるときに札が横を向いた瞬間だけ畑が消えて、
        /// 立てた板だと分かる。2 枚を直交させれば、どの向きから見ても必ず
        /// どちらかが正面を向く。3 枚にすると塗る画素が 1.5 倍になるわりに、
        /// 走り抜ける速さでは違いが出ない
        /// </summary>
        public const int WheatCross = 2;
        /// <summary>
        /// 札の法線を上へ倒す割合。<see cref="HalfAware.EditorTools.Bank.CardLift"/> へ渡す。
        /// 面の向きそのままだと上を向く成分が無く、低い朝日の下で畑が板を並べたように沈む
        /// </summary>
        public const float WheatLift = 1.0f;
        /// <summary>
        /// 札の α を抜く閾値。
        ///
        /// **低い描画解像度では、ここが株の太さそのものになる。** 絵の α は 0 か 255 しか
        /// 持たないが、mip と拡大の補間が中間を作るので、そこを切る高さで穂が太りも痩せもする。
        /// 実際に 0.26 / 0.34 / 0.42 / 0.58 を撮って見比べた。0.58 では畑が食い荒らされて
        /// 株がばらばらの塊になり、地が透けすぎる。0.26 では穂が繋がって輪郭が鈍る。
        /// 0.34 が、株の隙間を残したまま畑が面として繋がる高さだった。
        ///
        /// **絵の取り込み設定（DriveWheat.png.meta の alphaTestReferenceValue）と揃えること。**
        /// そちらは mip を作るときに被覆を保つ基準で、ずれると遠くの畑だけ痩せる
        /// </summary>
        public const float WheatCut = 0.34f;
        /// <summary>
        /// 朝靄が溜まる層の高さ。m。ここより上には靄が掛からない。
        ///
        /// **距離の霧（<see cref="DriveSky.density"/>）とは別物。** あちらは距離しか見ないので、
        /// どれだけ濃くしても空気が一様に霞むだけになる。原作の「朝靄の中で」はそうではなく、
        /// 靄は地面に溜まっていて、穂先や丘の背はそこから抜けて立っている。
        /// 株の背（0.72〜1.18）の三倍ほどに取ってあるので、手前の株は根元だけが
        /// わずかに沈み、遠くの窪みは丸ごと白く畳まれる。主な背（<see cref="CrestHigh"/>、
        /// 道から 78 m で 8.5 m）はこの層の上に出るので、稜線だけが靄から顔を出す
        /// </summary>
        public const float MistTop = 3.4f;
        /// <summary>
        /// 朝靄の溜まりの濃さ。1/m。**二乗で掛かる**（HalfAware/Wheat の Misting）。
        ///
        /// 地面すれすれで 20 m 先が 4%、30 m で 9%、手前のうねり（<see cref="FoldAt"/> の
        /// 47 m）で 22%、稜線の足元（78 m）で 49%、畑の端（150 m）で 92% 白くなる。
        /// 一乗で掛けていたときは 20 m でもう 20% 乗って、手前の畑から黄金色が抜けた。
        /// 原作は「朝靄の中で」と「黄金色に」を同じ一文で書いているので、
        /// 手前の株の色を靄に明け渡してはいけない
        /// </summary>
        public const float MistDeep = 0.0105f;

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
            // **0.055 から上げた。** 空の色はガンマのまま画面に出るので、0.055 は 14/255。
            // 高架の脚もネオンの看板も、その手前で霧に溶ける地平も、そこより明るくなって
            // 影絵にならない。倫敦の外れの空はネオンと街の照り返しで低いところが明るい。
            // 黒雲（Sky/Band0 の Nano）が上を塞いでいるので、上げても天頂は黒いまま
            sky = new Color(0.100f, 0.105f, 0.130f),
            haze = new Color(0.100f, 0.105f, 0.130f),
            density = 0.014f,
            sun = new Color(0.60f, 0.68f, 0.92f),
            power = 0.55f,
            aim = new Vector3(24f, 152f, 0f),
            // **環境光はここまで大きい数になる。** 舗装の地の色は 0.115 しかないので、
            // 掛かる光が 0.08 ほど無いと路面が画面に出ない。0.070 で足りて見えていたのは、
            // 既定の映り込み（組み込みの灰色の立方体）がどの面にも平らな艶を足していたため。
            // それを切った以上、明るさは帯の値として持つほかない
            lift = new Color(0.250f, 0.262f, 0.320f),
            ground = new Color(0.105f, 0.104f, 0.120f),
            // 前照灯。ネオンと濡れた舗装が明るいので、控えめに足すだけでよい
            beam = 0.26f,
        };

        /// <summary>帯 1。夜の高速。街灯の橙が一定の間隔で流れる。倫敦の雲から抜けたので、霧は薄い</summary>
        static readonly DriveSky Lit = new DriveSky
        {
            // 街灯の橙が低い雲へ照り返す。夜の高速の空は真上より地平が明るい。
            // 帯 0 より暖かく、わずかに明るい
            sky = new Color(0.115f, 0.104f, 0.108f),
            haze = new Color(0.115f, 0.104f, 0.108f),
            // **0.012 から下げた。** 表が謳うのは「街灯の列」で、0.012 では 36 m 先の
            // 一対しか残らず、その先は霧に呑まれて列にならない。0.0085 なら 110 m 先
            // （道の敷いてある端）でも 39% 抜けるので、三対が奥へ並ぶ。
            // 後ろを伸ばしたぶん前を 140 から縮めたときも、四対目は元から霧に呑まれていて
            // 見えていなかったので、値は動かしていない（絵で比べて差は 30 万画素中 9 個）
            density = 0.0085f,
            sun = new Color(0.54f, 0.60f, 0.86f),
            power = 0.42f,
            aim = new Vector3(28f, 168f, 0f),
            // 街灯の橙が回り込む。上を少し暖色へ寄せる。
            // 帯 0 より高いのは、片側 5 本ずつの街灯が空気ごと照らしているため。
            // 溜まりの外の路面が読めるのはこの環境光の側で、溜まりはその上に乗る
            lift = new Color(0.278f, 0.258f, 0.262f),
            ground = new Color(0.116f, 0.106f, 0.102f),
            // 前照灯。街灯の溜まりのあいだを埋める。溜まりと同じだけ明るくすると
            // 一定の間隔で流れるはずの橙が、点きっぱなしの照らしに呑まれる
            beam = 0.24f,
        };

        /// <summary>
        /// 帯 2。深夜の幹線。**この場面で一番暗い。** 街灯が絶え、前照灯だけになる。
        /// 木立は空より暗く落として影絵にする。ここだけは、明るくすると幹線に見えなくなる
        /// </summary>
        static readonly DriveSky Deep = new DriveSky
        {
            // **0.026 から上げた。** 木立は空より暗いつもりで置いてあったが、実際に測ると
            // 木 (26,27,29) に対して地平の空が (23,22,22) で、木の方がわずかに明るかった。
            // 差は 1 段（8.2）に届かないので、影絵どころか木立がそこにあることが読めない。
            //
            // **空の色はガンマのまま画面に出る。** 0.026 は 14/255 ではなく 7/255 で、
            // そこへガラスの被り（+14）が乗って 23 になっていた。つまり空の側は
            // ほとんど何も決めておらず、動かしても絵が変わらない。0.125（32/255）まで
            // 上げて初めて、木（26 前後）との間に段（8.2）以上の差が付く。
            // 環境光は据え置く。上げると木まで一緒に明るくなって差が戻らない
            sky = new Color(0.125f, 0.130f, 0.160f),
            haze = new Color(0.125f, 0.130f, 0.160f),
            density = 0.020f,
            sun = new Color(0.34f, 0.38f, 0.58f),
            power = 0.22f,
            aim = new Vector3(34f, 186f, 0f),
            // この帯だけは環境光も落とす。街灯が絶えた幹線を照らすものは無い
            lift = new Color(0.215f, 0.222f, 0.268f),
            ground = new Color(0.082f, 0.082f, 0.098f),
            // **この帯で道を見せているのはこれだけ。** 街灯が絶えるので、
            // ほかの帯より強く投げる。木立が前照灯の縁を掠めて流れていく
            beam = 0.46f,
        };

        /// <summary>
        /// 帯 3。明け方の丘陵。薄明と霧。低い位置からの光。
        /// 空が開き始めたところで、まだ朝ではない。霧はこの帯が一番濃い。
        ///
        /// **塗り潰しは地平の色にする。** 帯 4 と同じ理由で、上の暗い側を板
        /// （<see cref="Clouds"/> の Deep）に乗せてある。薄明は空の色が一色なのではなく、
        /// 地平が明るく天頂が暗いという傾きそのものなので、一色で塗ると
        /// どこも同じ灰色になって「まだ夜の続き」に見える。実際そうなっていて、
        /// 空も道も石垣も 45〜50 に並んでいた
        /// </summary>
        static readonly DriveSky Dawn = new DriveSky
        {
            // 地平の薄明。天頂の青は板が乗せる
            sky = new Color(0.232f, 0.218f, 0.228f),
            haze = new Color(0.232f, 0.218f, 0.228f),
            density = 0.022f,
            // **一乗で掛ける。** 表が謳っているのは「薄明と霧」で、二乗の掛かりは
            // 近くを素通しにして遠くだけ一気に溶かす。それは向こうが見えない夜の掛かり方で、
            // 霧ではない。一乗なら近くの石垣にも薄く白が乗り、遠いほど濃くなる。
            // 帯 4 の朝靄と同じ掛け方で、濃さだけが違う
            mist = true,
            // 夜明けの色。**彩度は抑える。** (0.86, 0.58, 0.44) では、低い日射しが
            // 直に当たる柱と天井の内張りが木目に見えるほど赤茶けた
            sun = new Color(0.88f, 0.66f, 0.52f),
            power = 0.86f,
            // 5 度。地平すれすれから薙ぐ。石垣の側面だけが赤く当たる。
            // 向きは帯 4 と揃える。日の昇る場所が帯を跨いで飛ぶと、夜明けが繋がらない
            aim = new Vector3(5f, 294f, 0f),
            lift = new Color(0.340f, 0.346f, 0.390f),
            ground = new Color(0.132f, 0.124f, 0.118f),
            // 薄明が上がってきているので、前照灯はもう路面を支配しない。
            // 消してしまうと夜からの続きが切れるので、点けたまま落とす
            beam = 0.16f,
        };

        /// <summary>
        /// 帯 4。朝靄の未舗装路。原作の「真っ白な千切れ雲と、まだ薄青い高い空」。
        ///
        /// **霧の色は空と同じにしてある。** 離すと、畑が霧に溶け切ったところに
        /// 横一線の継ぎ目が出る。同じ色にしておけば、畑はそのまま空へ溶けて消える。
        /// 朝靄はその溶け方そのもので、別の色として描くものではない。
        /// 高い空の青と千切れ雲は、そのうえに敷く板（<see cref="Clouds"/>）が持つ
        /// </summary>
        static readonly DriveSky Morning = new DriveSky
        {
            // **地平の色。高い空の色ではない。** 高い空の青は板（Clouds の High）が
            // 乗せるので、塗り潰しは畑が溶けていく先の色――朝靄の白を置く。
            // 板には必ず縁があり、縁の外の仰角にはこの色がそのまま出る。
            // ここへ青を置いていたときは、道の先のように稜線が視界を塞がない向きで、
            // 地平の上に 1.6 度ぶんの青い帯が硬い縁を引いて残った
            sky = new Color(0.820f, 0.800f, 0.752f),
            // **空と同じ色。** 畑が溶け切ったところがそのまま塗り潰しに続くので、
            // 地平に継ぎ目が出ない。朝靄は青くなく、日の当たった水気の色をしている。
            // 0.700/0.665/0.615 からさらに白へ上げた。色味の残る靄は煙に見える。
            // ほぼ無彩の 0.800/0.792/0.770 からわずかに暖めてある。畑が溶けていく先が
            // 無彩だと、白く飛んだところが灰色に転んで、朝ではなく曇りの色になる
            haze = new Color(0.820f, 0.800f, 0.752f),
            // **ここは空気そのものの霞み。朝靄の溜まりではない。**
            // 靄が地面に溜まって穂先だけが抜けて立つところは、畑のシェーダーが
            // 別に持っている（<see cref="MistTop"/> / <see cref="MistDeep"/>）。
            // RenderSettings の霧は距離しか見ないので、いくら濃くしても
            // 空気が一様に霞むだけで、靄の層にはならない。
            //
            // 掛かり方は一乗（<see cref="DriveSky.mist"/>）。二乗は近くをまったく
            // 素通しにして、ある距離から一気に溶かす掛かり方で、夜の帯には合うが
            // 靄の朝には合わない。0.0095 から 0.0080 へ下げたのは、溜まりのぶんが
            // 別に乗るようになったため。20 m で 15%、稜線（78 m）で 47%、
            // 畑の端（150 m）で 70% 霞む。稜線を空へ溶かしているのはこちらで、
            // 稜線の天は靄の層（3.4 m）より高いところにある
            density = 0.0080f,
            mist = true,
            // **橙から引き戻した。** (1.00, 0.86, 0.64) では畑の色を掛けた先が
            // 夕日になる。朝の日射しは白に近く、色を付けているのは水気の方
            sun = new Color(1.00f, 0.94f, 0.80f),
            // **1.15 では足りなかった。** 日射しが 14 度から入るので、上を向いた面が
            // 直に受けるのは 0.24 ぶんしかない。残りは青い環境光が埋めることになり、
            // 実際に測ると未舗装路が (0.28, 0.26, 0.26) の無彩色、つまり灰色の舗装に見えた。
            // 1.40 まで上げて初めて土の色が青い環境光に勝つ。
            // 1.46 は、既定の映り込み（<see cref="DriveSky.Apply"/> で切った）が
            // 艶のある面へ足していたぶんの埋め合わせ。ボンネット (108,115,96) と
            // 轍 (136,106,85) を切る前の値へ戻すための 4% で、畑の側は動かない
            power = 1.46f,
            // 14 度。低いまま畑を薙ぐ。ここを 30 度に上げると株の天面しか当たらず、
            // 畑が真上から照らした平らな板になる。逆に 11 度まで下げると、
            // 水平な面（道と畑の地）に届く光が足りず、地面から色が抜ける。
            //
            // 298 度は**後ろ寄りの右**から差す向き。倫敦から北へ走っているので日は右手だが、
            // そこをさらに後ろへ回してある。前の右に置くと畑が逆光になり、
            // 前を向いたときに見えるのが株の陰の面ばかりになって、黄金色がどこにも出ない。
            // 後ろへ回すと、前を向いても脇の窓から見ても日の当たった面が手前を向く
            aim = new Vector3(14f, 298f, 0f),
            lift = new Color(0.38f, 0.42f, 0.50f),
            ground = new Color(0.32f, 0.29f, 0.24f),
            // 朝。前照灯は消す。点けたままにすると轍に白が乗り、
            // 土の暖色（139,107,82）が灰へ転ぶ
            beam = 0f,
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
        /// 帯 0 と別に名前を付けてあるのは、帯 0 を触ってもガレージが連れて動かないため。
        ///
        /// **前照灯だけは帯 0 と違う。** 停めてある車の灯りは消えている。
        /// ドアを調べて乗り込んだところで点くので、切り替わる瞬間が見えても構わない。
        /// むしろそこは「エンジンを掛けた」ところにあたる
        /// </summary>
        public static readonly DriveSky GarageSky = Parked(Night);

        /// <summary>前照灯だけ消した写し。DriveSky は struct なので、受けた時点で控えになる</summary>
        static DriveSky Parked(DriveSky from)
        {
            from.beam = 0f;
            return from;
        }

        /// <summary>帯の数</summary>
        /// <summary>
        /// 景色の数。<see cref="Route"/> と必ず同じにする。
        /// 設計書の改訂で 5 から 3 になった（2026-09-16-scenario-design.md 7 節）
        /// </summary>
        public const int Bands = 3;

        // ---- ガレージ。メートル ---------------------------------------------

        /// <summary>
        /// 床の中心。車は原点にいるので、後ろへ寄せて歩く間を取る。
        ///
        /// **区画を 2 列にしたときに後ろへ 2.5 動かした。** 前の壁（シャッター）の z は
        /// GarageAt.z + GarageDeep/2 + 厚みの半分で決まる。奥行きを 16 から 21 へ伸ばしても
        /// 中心を -2 のままにすると、シャッターが z 8.6 まで前へ出て、車の鼻先からの間合いが
        /// 倍に開く。中心を下げれば前の壁は 6.125 に据わったままで、伸びたぶんが
        /// そっくり後ろの列と通路になる
        /// </summary>
        public static readonly Vector3 GarageAt = new Vector3(0f, 0f, -4.5f);
        /// <summary>床の広さ。x 方向</summary>
        public const float GarageWide = 14f;
        /// <summary>
        /// 床の広さ。z 方向。柱の割り付けでいう長辺はこちら。
        ///
        /// **16 では 2 列が入らない。** 区画の奥行きが 5.2 で、車が区画へ切り返せる通路に
        /// 5.9 は要る。5.2 × 2 + 5.9 に、シャッターの前の 3.3 と後ろの壁際の 1.4 を足して 21
        /// </summary>
        public const float GarageDeep = 21f;
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
        /// <summary>
        /// シャッターの手前に吊る灯りの列の z。前の壁の内側の面から 1.7 m。
        ///
        /// 区画の列に合わせた二列（z -1.2 と -9.7）はどちらもシャッターから 7 m 以上
        /// 離れていて、前の壁も操作盤も光電管も闇に沈んでいた。ここは前の列の
        /// 止め線（<see cref="BayZ"/> + <see cref="BayDeep"/>/2 ＝ 2.7）より前なので、
        /// 停めてある車の屋根に光を止められることも無い
        /// </summary>
        public static float ShutterLampZ { get { return GarageAt.z + GarageDeep * 0.5f - 1.7f; } }

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
        /// <summary>前の列の真ん中の z。車（原点）の入っている区画に合わせる</summary>
        public const float BayZ = 0.1f;
        /// <summary>
        /// 後ろの列の真ん中の z。
        ///
        /// 二つの列は通路を挟んで向かい合う。どちらの列も通路から入るので、
        /// 前の列は +z へ、後ろの列は -z へ鼻を向けて停める。突き当たりの止め線は
        /// それぞれの奥（前の列は +z 側、後ろの列は -z 側）に引き、番号は
        /// どちらも通路に面した口に描く。通路を歩く人の足元に番号が来る
        /// </summary>
        public const float BackBayZ = -11f;
        /// <summary>通路。前の列の口から後ろの列の口まで</summary>
        public static float AisleFrom { get { return BackBayZ + BayDeep * 0.5f; } }
        public static float AisleTo { get { return BayZ - BayDeep * 0.5f; } }
        /// <summary>塗りの線の幅</summary>
        public const float BayLine = 0.10f;
        /// <summary>
        /// 隣の 2 番の区画。自分の車の左隣。歩く線（x 1.3〜4.2）には掛からない。
        ///
        /// 前は覆いを掛けた塊を置いていたが、乗り込むまでずっと横目に入る一台なので、
        /// 今はほかの隣と同じ外装のある車が停めてある（<see cref="Stalls"/>）
        /// </summary>
        public const float CoveredBayX = -BayWide;

        /// <summary>
        /// 車体を塞ぐ箱の外形。世界の座標で、x は ±<see cref="BlockHalfX"/>。
        ///
        /// 車（<see cref="Car"/>）は運転席から見える面しか無く、当たりも持たない。
        /// 塞がないとガレージで車体をすり抜けられる。車高を上げた今は屋根の板（1.88〜1.94）が
        /// 立っている目線 1.60 より高いところにあるので、頭ごと車内へ入り込めてしまう。
        /// 下端をガレージの床の面に合わせるのは、隙間に足先を差し込ませないため。
        ///
        /// 前端はボンネットの先（2.48）まで伸ばす。屋根の前端で切ると、ボンネットの上を歩ける。
        ///
        /// **外装を作ってから車ぜんたいを包む形へ広げた。** それまでは運転席の周りしか
        /// 無かったので、客室のぶんだけ塞げば足りていた。車輪も後ろの荷室も尻の予備輪も
        /// 無かったからで、今それらを付けた以上、同じ箱では後ろ半分をすり抜けられる。
        ///
        /// x は車体の外板（0.97）でも泥除け（1.00）でもなく、いちばん外へ出る鏡（1.055）に
        /// 合わせる。歩いていて頭が鏡を通り抜けるのは、車体をすり抜けるのと同じ見え方になる。
        /// <see cref="InteractionPicker"/> は光線を引かないので、ドアの判定点（x 0.92）が
        /// 箱の中に入っても拾えなくなりはしない
        /// </summary>
        public const float BlockHalfX = 1.06f;
        public const float BlockTop = 1.97f;
        public const float BlockBack = -2.50f;
        public const float BlockFront = 2.66f;

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

        // ---- 車の外装。メートル ----------------------------------------------
        //
        // **場面 8 を組み始めてから今まで、外装は一枚も無かった。** 運転席からしか
        // 車を見ない前提で、屋根の板・内張り 2 枚・ボンネット・ガラス・計器盤だけを
        // 内側から組んである。ガレージに立って眺めると、宙に浮いた板が数枚あるだけで、
        // 側面も車輪も尻も灯りも無い。
        //
        // ここの数はすべて、既にある車内の寸法から割り出す。勝手に置くと車内と噛み合わない。
        //   - 外板の外面（0.97）は内張りの外面（<see cref="CardOut"/> 0.90）の外
        //   - 外板の内面（0.93）はドアの判定点（x 0.92）の外。中へ入ると
        //     見直し 4（ピンが形に埋まっていないか）が鳴る
        //   - 窓の下枠（1.32）は内張りの上端（<see cref="WaistY"/> 1.30）に合わせる
        //   - 屋根（1.94）と鼻先（2.48）は天井の板とボンネットの前パネルがもう決めている
        //
        // 原作の「ボロのオフロード車」は角の立った実用車で、丸みも飾りも持たない。
        // 露わな蝶番と止めねじ、尻に背負った予備輪、平らな板だけで組む

        /// <summary>外板の外面。x の絶対値。全幅 1.94 m</summary>
        public const float BodyHalf = 0.97f;
        /// <summary>外板の内面。x の絶対値。ドアの判定点（0.92）より外に置く</summary>
        public const float SkinIn = 0.93f;
        /// <summary>車体の下端。ここから下は足回りと影</summary>
        public const float SillY = 0.62f;
        /// <summary>窓の下枠。外から見た腰の線</summary>
        public const float BeltY = 1.32f;
        /// <summary>側面のガラスの下端と上端</summary>
        public const float PaneLow = 1.38f;
        public const float PaneHigh = 1.80f;
        /// <summary>屋根の外板の上端。天井の板（1.94）の上へ 3 cm 載せる</summary>
        public const float ShellTop = 1.97f;
        /// <summary>尻。荷室の後端</summary>
        public const float TailZ = -2.25f;
        /// <summary>鼻先。前パネルの前面（既にある）</summary>
        public const float NoseZ = 2.48f;
        /// <summary>客室の後端。屋根の板と塞ぐ箱がここで切れていた</summary>
        public const float CabBack = -0.70f;
        /// <summary>車軸の z。軸間 2.82 m。110 型の車台にあたる</summary>
        public const float AxleFrontZ = 1.62f;
        public const float AxleRearZ = -1.20f;
        /// <summary>車輪の外径の半分と幅。床の面に接する</summary>
        public const float TyreR = 0.37f;
        public const float TyreWide = 0.24f;
        /// <summary>車輪の中心の x。輪距 1.69 m</summary>
        public const float HubX = 0.845f;
        /// <summary>泥除けの抜きの半径。車輪より 0.11 大きい</summary>
        public const float ArchR = 0.48f;
        /// <summary>車輪の中心の高さ。ガレージの床の面に接地させる</summary>
        public static float HubY { get { return GarageFloorY + TyreR; } }

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

        // ---- 前照灯の照らし。メートル ----------------------------------------
        //
        // 板の寸法。絵（tools/make-drive.py の beam）はこの寸法を前提に描いてある。
        // ここを動かすなら絵の方の wide / deep / near も揃えること

        /// <summary>照らしの板の幅。遠くで開くぶんまで含む</summary>
        public const float BeamWide = 16f;
        /// <summary>照らしの板の長さ</summary>
        public const float BeamDeep = 44f;
        /// <summary>
        /// 照らしの板の手前の端。z。ボンネットの鼻先（2.48）のすぐ先。
        /// ここより手前は前照灯より後ろなので、そもそも照らされない
        /// </summary>
        public const float BeamFrom = 2.6f;

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
        // **景色は 3 つ。** 設計書（2026-09-16-scenario-design.md 7 節）の改訂で、
        // 深夜の幹線と明け方の丘陵が落ちた。Deep と Dawn の空はもう誰も使わないが、
        // 戻すときのために残してある。
        //
        // **明けるのはフェードではなく切り替え。** 設計書に「各シーンの切り替えは
        // フェードではなく瞬間的な切り替えとする」とあるので fadeIn は 0。
        // 黒へ入るのは元から切り替えなので、出入りとも一瞬になる。
        //
        // 最後の景色の余韻が 10 秒なのは、独白のあと家々が通り過ぎるところを
        // 映してから暗転するため（設計書 7.2）
        static readonly DriveBand[] Route =
        {
            new DriveBand { name = "倫敦の市街", trigger = DriveIds.Chips, speed = 16f, rough = 1.0f, afterglow = 5f, black = 0.8f, fadeIn = 0f, sky = Night },
            new DriveBand { name = "夜の高速", trigger = DriveIds.Cigar, speed = 28f, rough = 1.0f, rain = true, afterglow = 5f, black = 0.8f, fadeIn = 0f, sky = Lit },
            new DriveBand { name = "朝靄の未舗装路", trigger = DriveIds.Window, speed = 11f, rough = 4.5f, gravel = true, afterglow = 10f, black = 0.8f, fadeIn = 0f, sky = Morning },
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
        /// 道は z 110〜130 で終わる。環の端はタイルの刻みに乗るので幅がある。霧が無いと世界の端がそのまま見える。
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
            // 煙草はダッシュボードの天板の上。計器の絵（y 1.28〜1.40 / z 0.52〜0.55）の
            // 手前に置く。印は 0.17 上に出るので、計器盤の庇へ入らない高さにしてある
            triggerItems[1] = Put(parent, "Cigar", new Vector3(0.06f, 1.30f, 0.50f), script, DriveIds.Cigar, ItemRadius, false);
            // 窓だけ必須。最後の景色に入るまで伏せてあるので、それまで場面は閉じない。
            // ドアの内張りは x 0.82〜0.90。0.84 に置くと印が内張りの中に入る
            triggerItems[2] = Put(parent, "Window", new Vector3(0.80f, 1.26f, 0.10f), script, DriveIds.Window, ItemRadius, true);
            // きっかけはその帯に入るまで出さない。出し分けるのは DriveDirector.ShowTrigger
            for (var i = 0; i < triggerItems.Length; i++) triggerItems[i].SetActive(false);

            // 帯を問わず置く、読んでも帯が進まない対象。
            // ひとつの入れ物にまとめて、乗り込むまで DriveDirector に伏せさせる。
            // 塞ぐ箱があってもガレージの立てる位置から 1.1〜1.3 m しか離れず、
            // 拾える距離 1.4 の内側に入ってしまう。once: true なので、ここで読まれると
            // 走行中に二度と出ない
            // ガレージでだけ調べられる対象。乗り込んだら DriveDirector が伏せる。
            // 伏せないと、走っている車の後ろ 5.9 m にピンが浮いたまま残る
            // （PinMarkers は Interactable.Active しか見ない）
            var garageOnly = Child(parent, "GarageOnly");
            Put(garageOnly, "Button", new Vector3(2.55f, 1.12f, 5.60f), script, DriveIds.Button, ItemRadius, false);

            var cabin = Child(parent, "Cabin");
            Put(cabin, "Radio", new Vector3(-0.02f, 1.19f, 0.70f), script, DriveIds.Radio, ItemRadius, false);
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

            Feet(player.transform, body);
            CarSound(player.transform);
            Smoke(player.transform, eye.transform);

            // 当たりを入れたまま動かすと床や壁に押し出されて狙った場所に立たない（BuildAlley.Place と同じ）
            body.enabled = false;
            player.transform.position = new Vector3(StandAt.x, GarageFloorY + 0.06f, StandAt.z);
            player.transform.rotation = Quaternion.Euler(0f, StandYaw, 0f);
            body.enabled = true;

            Marks(Flow(walker, Screen()));
        }

        // 響きの値。**「もっと空間に響いている感じ」と差し戻されている。**
        //
        // 強さだけ上げても広さは出ない。広い場所だと分かるのは、音が返ってくるまでの
        // 間と、高音が尾のあいだどれだけ残るかで、そこを触るのが要る。
        //
        //   - 返ってくるまでの間（EchoEarlyDelay / EchoLateDelay）。壁までの距離がこれで伝わる。
        //     14 × 21 の車庫なら、いちばん近い壁でも 5 m ほど離れている
        //   - 高音の残り（EchoDecayHF）。既定の 0.5 では高音が先に消えて、
        //     尾が籠もった唸りになる。裸のコンクリートは高音を返すので 1 に近づける
        //   - 尾の長さ（EchoDecay）。空の車庫は実際 2 秒を超えて鳴る
        //
        // 強すぎると言われたら EchoLevel と EchoDecay から下げる

        /// <summary>響きの広さ。ミリベル。場面 2 の小道は -700</summary>
        const float EchoRoom = -220f;
        /// <summary>高音の残り。コンクリートは高音を返すので小道の -600 より上げる</summary>
        const float EchoBright = -120f;
        /// <summary>尾を引く長さ。秒。小道は 0.75</summary>
        const float EchoDecay = 2.60f;
        /// <summary>
        /// 尾のあいだ高音がどれだけ残るか。低音の尾に対する割合。
        /// 既定の 0.5 では高音が先に落ちて、響きが壁の向こうの唸りに聞こえる
        /// </summary>
        const float EchoDecayHF = 0.92f;
        /// <summary>初期反射の強さ。ミリベル。小道は -900</summary>
        const float EchoReflect = -420f;
        /// <summary>
        /// 最初の反射が返ってくるまで。秒。壁までの距離がここに出る。
        /// 音は 1 秒で 340 m 進むので、0.020 秒は往復 6.8 m にあたる
        /// </summary>
        const float EchoEarlyDelay = 0.020f;
        /// <summary>残響そのものの強さ。ミリベル。小道は 60</summary>
        const float EchoLevel = 520f;
        /// <summary>初期反射のあと、尾が立ち上がるまで。秒。長いほど広い場所に聞こえる</summary>
        const float EchoLateDelay = 0.038f;
        /// <summary>
        /// 直の音の残し方。ミリベル。0 が素のまま。
        /// 少しだけ落として、響きの側を前に出す
        /// </summary>
        const float EchoDry = -140f;

        /// <summary>
        /// 場面 8 の足音の素材。**場面 1・2 の Step1〜5 とは別物。**
        ///
        /// あちらは Kenney の柔らかい足音で、濡れた石畳と土のためにある。測ると
        /// 2.5kHz 以上が 700Hz 以下より 11〜15dB 弱く、裸のコンクリートの上で鳴らすと
        /// 床が土に聞こえる。同じ素材（CC0）から唸りを抜いて打音を持ち上げ、
        /// 重心を下げたものが Concrete1〜4（`tools/make-steps.py`）。
        /// 出どころと加工は Assets/Audio/LICENSES.md に控えてある
        /// </summary>
        static readonly string[] ConcreteSteps =
        {
            "Assets/Audio/Concrete1.wav", "Assets/Audio/Concrete2.wav",
            "Assets/Audio/Concrete3.wav", "Assets/Audio/Concrete4.wav",
        };

        /// <summary>
        /// 足音。**場面 8 には今まで無かった。**
        ///
        /// プレイヤーはコンクリートの床を 8 m 歩いて車まで来る。そのあいだ無音で、
        /// 歩いているという手応えがどこにも無かった。場面 2 は Alley.unity に
        /// 手で置いた Feet が持っているが、場面 8 は空のシーンから組み上げるので、
        /// ここで作らないと誰も作らない。
        ///
        /// 反響は素材ではなく `AudioReverbFilter` に持たせる。
        /// コンクリートの箱の中なので、組み込みの `ParkingLot` がそのまま当たる。
        /// 場面 2 の `RainCover` のように屋根の下で切り替える必要は無い。
        /// ここは端から端までひと続きの箱で、響きの変わる場所が無い
        /// </summary>
        /// <summary>
        /// 火を点けて一服する一連。**場面 1 の仕組みをそのまま使い回す。**
        ///
        /// <see cref="Cigarette"/> が <see cref="SmokeBeats"/> の時刻表どおりに
        /// 蓋を開ける金属音・火・吸う息・吐く息を並べ、<see cref="SmokePuffs"/> が煙を出す。
        /// 素材も場面 1 と同じものを読む。
        ///
        /// 煙はカメラの子に置く。口元から立つものなので、視線を振れば一緒に動く
        /// </summary>
        static void Smoke(Transform player, Transform eye)
        {
            var puffs = BuildProps.BuildSmoke(eye).GetComponent<SmokePuffs>();

            var go = new GameObject("Cigarette");
            go.transform.SetParent(player, false);
            var voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            // 口元。耳と同じ体に付いているので距離で減らさない
            voice.spatialBlend = 0f;
            voice.volume = 0.40f;

            var smoke = go.AddComponent<Cigarette>();
            var so = new SerializedObject(smoke);
            so.FindProperty("puffs").objectReferenceValue = puffs;
            so.FindProperty("voice").objectReferenceValue = voice;
            for (var i = 0; i < SmokeClips.Length; i += 2)
                so.FindProperty(SmokeClips[i]).objectReferenceValue = Sound(SmokeClips[i + 1]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>一服の素材。場面 1 と同じものを読む。出どころは Assets/Audio/LICENSES.md</summary>
        static readonly string[] SmokeClips =
        {
            "lighterClick", "Assets/Audio/LighterClick.wav",
            "lighterFlame", "Assets/Audio/LighterFlame.wav",
            "drag", "Assets/Audio/Drag.wav",
            "blow", "Assets/Audio/Blow.wav",
        };

        /// <summary>
        /// 乗り込みと走行の音。**足音とは別の入れ物に置く。**
        ///
        /// 足音の入れ物には <see cref="AudioReverbFilter"/> が付いていて、
        /// 同じ入れ物の AudioSource は全部そこを通る。走行音まで通すと、
        /// 車の中にいるあいだずっとコンクリートの車庫の響きが乗る。
        ///
        /// どれも 2D。耳も音源も同じ運転席にあるので、距離で減らす意味が無い。
        /// 単発と輪で入れ物を分けるのは、輪を鳴らしたまま単発を重ねるため
        /// </summary>
        static void CarSound(Transform player)
        {
            var go = new GameObject("Motor");
            go.transform.SetParent(player, false);
            var shots = go.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.loop = false;
            shots.spatialBlend = 0f;

            var wheels = new GameObject("Road");
            wheels.transform.SetParent(go.transform, false);
            var road = wheels.AddComponent<AudioSource>();
            road.playOnAwake = false;
            road.loop = true;
            road.spatialBlend = 0f;

            // 雨は走行音に重ねるので、別の入れ物で同時に鳴らす
            var sky = new GameObject("Weather");
            sky.transform.SetParent(go.transform, false);
            var weather = sky.AddComponent<AudioSource>();
            weather.playOnAwake = false;
            weather.loop = true;
            weather.spatialBlend = 0f;

            var sound = go.AddComponent<DriveSound>();
            var so = new SerializedObject(sound);
            so.FindProperty("oneShot").objectReferenceValue = shots;
            so.FindProperty("road").objectReferenceValue = road;
            so.FindProperty("weather").objectReferenceValue = weather;
            for (var i = 0; i < DriveClips.Length; i += 2)
                so.FindProperty(DriveClips[i]).objectReferenceValue = Sound(DriveClips[i + 1]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 乗り込みと走行の音の素材。DriveSound の値の名と、その素材の組。
        ///
        /// 輪の二本は継ぎ目を消してある。頭 0.5〜0.75 秒を尻へ被せてあるので、
        /// そのまま loop に掛けて段が出ない。素材の切り出しと出どころは
        /// Assets/Audio/LICENSES.md に控えてある
        /// </summary>
        static readonly string[] DriveClips =
        {
            "doorOpen", "Assets/Audio/CarDoorOpen.wav",
            "doorShut", "Assets/Audio/CarDoorShut.wav",
            "ignition", "Assets/Audio/Ignition.wav",
            "pullAway", "Assets/Audio/PullAway.wav",
            "paved", "Assets/Audio/DriveSealed.wav",
            "gravel", "Assets/Audio/DriveGravel.wav",
            "rain", "Assets/Audio/RainWipers.wav",
            "windowDown", "Assets/Audio/WindowDown.wav",
        };

        static AudioClip Sound(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("音の素材が無い: " + path);
            return clip;
        }

        static void Feet(Transform player, CharacterController body)
        {
            var feet = new GameObject("Feet");
            feet.transform.SetParent(player, false);
            feet.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var src = feet.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            // 耳と同じ体に付いているので、距離で減らさない
            src.spatialBlend = 0f;
            // 場面 2 は 0.55。あちらは雨の音の上で鳴らすので大きく要る。
            // ここは無音の車庫なので一度 0.38 まで下げたが、「軽い」と差し戻された。
            // 素材そのものも重心を下げてある（tools/make-steps.py）
            src.volume = 0.52f;

            // **既製の ParkingLot を当てない。** あれは room も roomHF も -1000 で、
            // 高音が丸ごと落ちるので、無音のガレージではほとんど何も聞こえない。
            // 実際「反響が付いていない」と差し戻された。場面 2 と同じく User にして
            // 一つずつ置く（RainCover が小道でやっているのと同じ構え）。
            //
            // 小道より強く取る。あちらは石壁の細い通りで room -700 / decay 0.75 だが、
            // ここは 14 × 21 × 高さ 2.9 のコンクリートの箱で、実際よく響く。
            // 強すぎると言われたら decay と reverbLevel から下げる
            var echo = feet.AddComponent<AudioReverbFilter>();
            echo.reverbPreset = AudioReverbPreset.User;
            echo.dryLevel = EchoDry;
            echo.room = EchoRoom;
            echo.roomHF = EchoBright;
            echo.decayTime = EchoDecay;
            echo.decayHFRatio = EchoDecayHF;
            echo.reflectionsLevel = EchoReflect;
            echo.reflectionsDelay = EchoEarlyDelay;
            echo.reverbLevel = EchoLevel;
            echo.reverbDelay = EchoLateDelay;
            echo.diffusion = 100f;
            echo.density = 100f;

            var steps = feet.AddComponent<Footsteps>();
            var so = new SerializedObject(steps);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("source").objectReferenceValue = src;
            var clips = so.FindProperty("clips");
            clips.arraySize = ConcreteSteps.Length;
            for (var i = 0; i < ConcreteSteps.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ConcreteSteps[i]);
                if (clip == null) Debug.LogWarning("足音の素材が無い: " + ConcreteSteps[i]);
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
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
            so.FindProperty("idleRate").floatValue = IdleRate;
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
            var garageOnly = Look(root, "Items/GarageOnly");
            dso.FindProperty("garageOnly").objectReferenceValue = garageOnly != null ? garageOnly.gameObject : null;
            // 足音。乗り込んだら止める。Player の下にあるのでガレージと一緒には消えない
            dso.FindProperty("feet").objectReferenceValue = Object.FindFirstObjectByType<Footsteps>(FindObjectsInactive.Include);
            dso.FindProperty("sound").objectReferenceValue = Object.FindFirstObjectByType<DriveSound>(FindObjectsInactive.Include);
            dso.FindProperty("cigarette").objectReferenceValue = Object.FindFirstObjectByType<Cigarette>(FindObjectsInactive.Include);
            // 風防の水とワイパー。BuildDriveCar が Drive/Car/Rain として組む。
            // **無くても知らせない。** まだ作っていない段では毎回組むたびに知らせが出る。
            // DriveDirector は null を見て何もしないので、無いまま走っても止まらない
            var rain = root.Find("Car/Rain");
            dso.FindProperty("rainRig").objectReferenceValue = rain != null ? rain.gameObject : null;
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
            // 前照灯の照り返し。強さは帯が持ち、差し替える先をここで渡す
            var thrown = Look(root, "Car/Beam");
            var lamps = thrown != null ? thrown.GetComponent<Renderer>() : null;
            dso.FindProperty("beams").objectReferenceValue = lamps;
            // 組み上がった場面はまだガレージの中。前照灯は消えている。
            // 再生すれば DriveDirector が帯ごとに点け直すが、エディタで開いたときの
            // 見え方も組み立ての責任なので、ここで揃えておく
            if (lamps != null) lamps.enabled = GarageSky.beam > 0f;
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
                e.FindPropertyRelative("gravel").boolValue = Route[i].gravel;
                e.FindPropertyRelative("rain").boolValue = Route[i].rain;
                e.FindPropertyRelative("afterglow").floatValue = Route[i].afterglow;
                e.FindPropertyRelative("black").floatValue = Route[i].black;
                e.FindPropertyRelative("fadeIn").floatValue = Route[i].fadeIn;
                FillSky(e.FindPropertyRelative("sky"), Route[i].sky);
            }
        }

        /// <summary>
        /// 空と灯りを 1 つ書き込む。DriveSky も struct なので中身を並べ直す。
        ///
        /// **DriveSky に値を足したら、ここへも足すこと。** 並べ直す書き方なので、
        /// 足し忘れた値は既定のまま場面へ入り、組み立てのつもりと実画面が食い違う。
        /// 実際 mist を足したとき、ここを直し忘れて帯 4 だけ靄が掛からなかった
        /// </summary>
        static void FillSky(SerializedProperty at, DriveSky from)
        {
            if (at == null) return;
            at.FindPropertyRelative("sky").colorValue = from.sky;
            at.FindPropertyRelative("haze").colorValue = from.haze;
            at.FindPropertyRelative("density").floatValue = from.density;
            at.FindPropertyRelative("mist").boolValue = from.mist;
            at.FindPropertyRelative("sun").colorValue = from.sun;
            at.FindPropertyRelative("power").floatValue = from.power;
            at.FindPropertyRelative("aim").vector3Value = from.aim;
            at.FindPropertyRelative("lift").colorValue = from.lift;
            at.FindPropertyRelative("ground").colorValue = from.ground;
            at.FindPropertyRelative("beam").floatValue = from.beam;
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
        /// 隣の車の塗りの名と色。**色は線形で置く。**
        ///
        /// 絵（DriveCarPaint.png）は汚れと褪せだけを持ち、平均がほぼ白（sRGB 236 ＝ 線形 0.83）。
        /// URP の Lit は絵に _BaseColor を掛けるので、画面に出る明るさはここの色の 0.83 倍になる。
        ///
        /// **一台だけ明るい色を混ぜる。** この場面の色はどれも 0.1〜0.4 に固まっていて、
        /// 暗い車を五台並べると区画の境が読めない一つの塊になる。褪せた生成りが一台あると、
        /// そこを手掛かりに残りの四台の輪郭も見えてくる
        /// </summary>
        static readonly string[] PaintNames = { "Cream", "Maroon", "Navy", "Tan" };

        static readonly Color[] PaintHues =
        {
            new Color(0.560f, 0.540f, 0.478f),
            new Color(0.232f, 0.084f, 0.072f),
            new Color(0.086f, 0.112f, 0.170f),
            new Color(0.330f, 0.266f, 0.168f),
        };

        /// <summary>
        /// 隣の車の塗り。<see cref="Mat"/> を通さない。
        /// あちらは名前から絵を引くので、色ごとに絵を焼くことになる。
        /// ここでは絵 1 枚を全員で使い回し、色だけ _BaseColor で差し替える
        /// </summary>
        static Material Paint(int which)
        {
            var name = "Car" + PaintNames[which];
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/DriveCarPaint.png");
            if (tex == null) Debug.LogWarning("隣の車の塗りの絵が無い: Assets/Textures/DriveCarPaint.png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", PaintHues[which]);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.22f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
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
        /// 600 を越える札を 1 枚の mesh へ焼いてあるので、Transform では動かせない。
        /// もうひとつは α を閾値で抜くこと。株の形は mesh ではなく絵の α が持っている。
        ///
        /// 揺れの数は <see cref="WheatWind"/> から取る。見直しが同じ数を読んで、
        /// なびいた穂先が轍へ倒れ込まないかを測る
        /// </summary>
        static Material WheatMat()
        {
            // 黄金色。**この場面のほかの色（どれも 0.2 前後）から大きく外している。**
            // 朝の日射しを受けて初めて成り立つ色で、帯 4 のほかには出てこない。
            //
            // **青を持ち上げて橙から引き戻した。** 穂先が (0.96, 0.79, 0.35) だったときは
            // 日射しの色を掛けた先が (0.96, 0.68, 0.22) で、黄金色というより夕日の橙だった。
            // 黄金色は白を含んだ黄で、朝の色。青を 0.35 から 0.52 へ上げてある。
            // **そのぶん明るさは落とす。** 白を足しただけでは、日射しを掛けた先が
            // 振り切れて畑ぜんたいが生成りの布に見えた。色味を白へ寄せて値を下げると、
            // 熟れた麦の深い黄が残る。根元も同じ向きへ寄せた。遠くの株は絵が mip で
            // 平均へ潰れ、見えているのは根元色と穂先色の中ほどになるため
            return Crop("Wheat", "DriveWheat", true,
                new Color(0.500f, 0.400f, 0.210f), new Color(0.845f, 0.735f, 0.440f));
        }

        /// <summary>
        /// 畑の地のマテリアル。**株と同じシェーダーで塗る。**
        ///
        /// URP の Lit で塗っていたときは、上を向いた面が青い環境光ばかりを受けて
        /// 灰色に転び、株の黄金色との境が畑の中ほどに横線として出た。同じ灯りの式で
        /// 塗れば、株の切れる先から地へそのまま繋がる。
        /// 株ではないので uv1 を持たず、揺れもしない（_Rooted 0）
        /// </summary>
        static Material FieldMat()
        {
            // **株の陰になる面として塗る。** 株より一段どころではなく、七掛けまで落とす。
            //
            // ここは上を向いた面なので、株と同じ灯りの式で塗ると回り込みも穂群の天も
            // 満額で受けて、実際に測ると株 (177,145,75) より明るい (180,152,88) になった。
            // 株の隙間から覗くところが畑でいちばん明るいと、隙間のひとつひとつが
            // 光って見えて、畑が疎らに散った枯れ草になる。麦畑の地は株の下の日陰で、
            // そこがどれだけ暗いかが「詰まっている」という見え方そのものを作る。
            //
            // 穂群の天（_Canopy）と穂の透かし（_Glow）も落とす。どちらも立った穂の
            // ためにある値で、水平な地に満額で掛けるものではない。
            //
            // **落としすぎてもいけない。** 株の四割五分まで沈めたときは、隙間が
            // 穴になって、低い描画解像度では畑が胡麻塩に見えた。六割弱に置いてある
            //
            // 橙から引き戻す向きは株と揃える。片方だけ直すと、株の切れる先で色が変わる
            return Crop("FieldCrop", "DriveField", false,
                new Color(0.330f, 0.272f, 0.145f), new Color(0.520f, 0.446f, 0.264f),
                0.10f, 0.26f);
        }

        /// <summary>
        /// 畑を塗るマテリアル。ほかの素材と違って URP の Lit を使わない。
        ///
        /// canopy と glow は立った穂のための値なので、水平な地（<see cref="FieldMat"/>）は
        /// 落として呼ぶ。満額で掛けると地が株より明るくなる。
        ///
        /// 要るものが四つある。ひとつは頂点をずらして微風になびかせること。株は区切り 1 つに
        /// 600 を越える札を 1 枚の mesh へ焼いてあるので、Transform では動かせない。
        /// ひとつは α を閾値で抜くこと。株の形は mesh ではなく絵の α が持っている。
        /// ひとつは光の回り込みで、札は面が二方向しか無く、素の Lambert だと
        /// 日射しに背を向けた札が真っ黒に落ちて畑が市松模様に見える。
        /// もうひとつは穂が朝日を透かす分で、薄い穂は裏から照らされても光る。
        ///
        /// **絵は明暗だけを持ち、色はここが持つ。** 絵にも色を焼くと、黄金色を詰めるつまみが
        /// 二箇所に割れる。絵の平均は中庸（0.5）で、シェーダーが 2 倍して掛ける。
        ///
        /// 揺れの数は <see cref="WheatWind"/> から取る。見直しが同じ数を読んで、
        /// なびいた穂先が轍へ倒れ込まないかを測る
        /// </summary>
        static Material Crop(string name, string picture, bool rooted, Color root, Color tip,
            float canopy = 0.35f, float glow = 0.60f)
        {
            var shader = Shader.Find("HalfAware/Wheat");
            if (shader == null)
            {
                Debug.LogWarning("HalfAware/Wheat が見つからない。麦はなびかない");
                return Mat("Field");
            }
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。前に URP の Lit で作ってあっても差し替わる
            m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + picture + ".png");
            if (tex == null) Debug.LogWarning("畑の絵が無い: Assets/Textures/" + picture + ".png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", root);
            m.SetColor("_TipColor", tip);
            // 回り込みは 0.60 から下げた。強いほど畑が平らになり、日射しの向きが読めなくなる
            m.SetFloat("_Wrap", 0.52f);
            // 透かしは 0.48 から上げた。**日陰の面の色を決めているのはこれ。**
            // 低いと、日射しに背を向けた面が空の青い環境光だけになる。実際に測ったところ
            // 80 m 先の畑は (0.119, 0.084, 0.094) と青が緑を上回っていて、黄金色ではなかった。
            // 穂は薄く、朝の低い日射しを実際に透かすので、色の選び方としても外れていない。
            // 箱を札に替えてからは 0.70 だと日陰の面まで持ち上がりすぎたので 0.60 へ。
            // 箱と違って札は裏も表も同じ絵なので、透かしの効く面が倍に増えている
            m.SetFloat("_Glow", glow);
            m.SetFloat("_Canopy", canopy);
            // 畑の地の絵は α を持たない。閾値を 0 にして素通しにする。
            // 株の絵は α で形を抜くので、こちらだけ切る
            m.SetFloat("_Cutoff", rooted ? WheatCut : 0f);
            m.SetFloat("_Rooted", rooted ? 1f : 0f);
            m.SetFloat("_GroundHigh", 0.75f);
            m.SetFloat("_SwayAmp", rooted ? WheatWind.Amp : 0f);
            m.SetFloat("_SwayFlutter", WheatWind.Flutter);
            m.SetFloat("_SwayAcross", WheatWind.Across);
            m.SetFloat("_SwayAlong", WheatWind.Along);
            m.SetFloat("_SwayRate", WheatWind.Rate);
            m.SetFloat("_SwayFlutterRate", WheatWind.FlutterRate);
            m.SetFloat("_SwayHigh", WheatWind.High);
            m.SetFloat("_SwaySide", WheatWind.Side);
            m.SetFloat("_GustDeep", WheatWind.Gust);
            m.SetFloat("_GustAcross", WheatWind.GustAcross);
            m.SetFloat("_GustAlong", WheatWind.GustAlong);
            m.SetFloat("_GustRate", WheatWind.GustRate);
            m.SetFloat("_ShadeDeep", WheatWind.Shade);
            // 朝靄の溜まり。色は帯 4 の霧と同じものを渡す。
            // 距離の霧（DriveSky.density）と別に持つのは、あちらが距離しか見ないため
            m.SetColor("_MistColor", Morning.haze);
            m.SetFloat("_MistDeep", MistDeep);
            m.SetFloat("_MistTop", MistTop);
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
                case "CarSteel": col = new Color(0.216f, 0.223f, 0.238f); smooth = 0.28f; break;
                // 継ぎ目と窪み。絵を持たず、ただ暗い。板と板の境をこれで引く
                case "CarGap": col = new Color(0.020f, 0.019f, 0.021f); smooth = 0.05f; break;
                case "CarGlass": col = new Color(0.55f, 0.60f, 0.66f, 0.12f); smooth = 0.85f; break;
                // ボンネット。褪せた緑の塗りの鉄板。
                // **ここだけ夜の場面の中で浮くほど明るい。** 車内の色（0.09）で塗ると、
                // 実際に組んで測ったところ画面では路面と同じ明るさになり、ボンネットが丸ごと道に溶けた。
                // 帯 0〜2 は空も道も内装も 0.1 前後に固まっていて、形を読ませる手掛かりが明暗しか無い。
                //
                // **明るさは夜のために据え置き、彩りで昼を直してある。** 無彩の 0.47 で塗っていた
                // ときは、朝になった帯 4 で画面が (121,121,114)・彩度 5% の灰色の板になり、
                // 塗った面ではなく下地のままの鉄板に見えた。明るさを落とせば夜が潰れるので、
                // 落とす代わりに緑へ寄せてある。絵（DriveCarBody.png）が持つ斑と剥げも同じ理由で、
                // 平均を下げずに「平らな一枚板」から離すためにある
                case "CarBody": col = new Color(0.279f, 0.305f, 0.275f); smooth = 0.18f; break;
                // 車輪のゴム。絵（DriveCarTyre.png）が持つので、ここは絵が無いときの控え。
                // 実物のゴムの反射率は 0.03〜0.05 で、真っ黒に塗ると灯りの下でも穴に見える
                case "CarTyre": col = new Color(0.030f, 0.030f, 0.032f); smooth = 0.12f; break;
                // 前照灯のガラスと番号板。消えている灯りなので、光らせず淡い面として置く。
                // ガレージの天井の灯りを受けて、顔の中で二つだけ明るい丸になる
                case "CarLamp": col = new Color(0.430f, 0.425f, 0.400f); smooth = 0.72f; break;
                // 尾灯。**外装で唯一の彩り。** 暗い車体の尻に赤が二つあるだけで、
                // どちらが前でどちらが後ろかが一目で読める
                case "CarTail": col = new Color(0.300f, 0.042f, 0.036f); smooth = 0.66f; break;
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
                // 油溜まり。**α を 1 未満にするのはここだけの事情。**
                //
                // 濃さも色も虹も絵（DriveOilStain.png）が持っていて、ここの色は使われない。
                // ただし <see cref="Mat"/> は α が 1 を切ったときだけ <see cref="SeeThrough"/> を
                // 通すので、1 のままだと絵の α が捨てられて、縁の切り立った黒い塊に戻る。
                // 0.96 は「ほぼそのまま、でも透ける」ための値で、濃さを決めているのではない。
                //
                // **艶は濡れた舗装（Sheen）と同じところまで上げた。** 0.52 では映り込みの
                // 広がりが床（0.14）と大差なく、天井の灯りが溜まりの上で滲むだけで
                // 終わっていた。溜まりが溜まりに見えるのは、暗いからではなく、
                // そこだけ灯りを映すからで、映り込みの鋭さがそのまま水気に読める。
                // 映り込みは天井の 9 灯から直に来る（RenderSettings の映り込みは
                // <see cref="DriveSky.Apply"/> が切ってあるので、環境の側からは来ない）
                case "OilStain": col = new Color(0.030f, 0.029f, 0.031f, 0.96f); smooth = 0.86f; break;
                // 排水口の受け。中は見えないので、ただ暗い
                case "Drain": col = new Color(0.040f, 0.039f, 0.038f); smooth = 0.14f; break;
                // 濡れた路面。艶だけの面は映る物が無いと穴に見えるので、地の明るさを持たせる
                case "Sheen": col = new Color(0.100f, 0.108f, 0.128f); smooth = 0.86f; break;
                case "Metal": col = new Color(0.085f, 0.088f, 0.095f); smooth = 0.26f; break;
                case "Tree": col = new Color(0.045f, 0.042f, 0.040f); smooth = 0.08f; break;
                case "Stone": col = new Color(0.165f, 0.162f, 0.150f); smooth = 0.08f; break;
                case "Grass": col = new Color(0.062f, 0.085f, 0.052f); smooth = 0.08f; break;
                // 畑は株も地もここに無い。URP の Lit ではなく HalfAware/Wheat で塗るので、
                // 色は WheatMat と FieldMat が持っている。
                //
                // この "Field" は、そのシェーダーが見つからなかったときの控え。
                // 絵（DriveField.png）は明暗だけしか持たないので、ここへ落ちると
                // 畑が灰色になる。そうなっていたら、黄金色を詰める前にシェーダーを探すこと
                case "Field": col = new Color(0.400f, 0.300f, 0.125f); smooth = 0.06f; break;
                // 土と轍は帯 4 でしか使わない。どちらも絵（DriveDirt／DriveRut）を持つので、
                // 画面に出る明るさは絵の平均が持つ。ここの色は絵が無いときの控え。
                // 夜の帯の暗さに合わせる必要が無いので、朝日の下で土に見えるところまで上げてある
                case "Dirt": col = new Color(0.314f, 0.112f, 0.019f); smooth = 0.06f; break;
                // 轍。**地の土より明るい。** 夏の朝の乾いた農道で、車輪の通る筋は
                // 埃が磨かれて白茶ける。湿った暗い轍にすると、黄金色の畑のあいだを
                // 暗い溝が抜けることになり、道が水路に見える。
                //
                // **青を抜いて赤を上げてある。** 帯 4 の霧は一乗で掛かり、色を足し算で乗せる。
                // 運転席からいちばん近くに見える路面（ボンネットの先、およそ 10 m）でも
                // 8% の白が乗るので、絵を暗くしても色は戻らない。戻るのは赤を足したときだけ
                case "Rut": col = new Color(0.434f, 0.175f, 0.027f); smooth = 0.10f; break;
                default: col = new Color(0.12f, 0.12f, 0.13f); smooth = 0.30f; break;
            }
        }

        /// <summary>
        /// 路面に落ちる灯りの板 1 枚の形。原点を中心に、x へ wide、z へ deep。
        ///
        /// Texel は 1 のまま。Bank は実寸に Texel を掛けた uv を振るので、
        /// uv がそのまま長さ（m）になり、<see cref="GlowMat"/> が _BaseMap_ST で
        /// 0〜1 へ畳み直せる（<see cref="DialMat"/> と同じ手）。
        /// v は 0 が奥（+z）、1 が手前（-z）に来る
        /// </summary>
        static Mesh Card(string name, float wide, float deep)
        {
            return Shape(name, 1f, b => b.FaceY(GlowY, -wide * 0.5f, wide * 0.5f, -deep * 0.5f, deep * 0.5f, 1));
        }

        /// <summary>
        /// 路面に落ちる灯りのマテリアル。加算で重ねるので、暗いところは何もしない。
        ///
        /// URP の Unlit ではなく <c>HalfAware/RoadGlow</c> を使う。あちらは霧を色として
        /// 混ぜるので、加算の板では霧の色が板の形のまま路面に乗る
        /// </summary>
        static Material GlowMat(string name, string picture, Color col, float gain, float wide, float deep)
        {
            var shader = Shader.Find("HalfAware/RoadGlow");
            if (shader == null) Debug.LogWarning("HalfAware/RoadGlow が見つからない。路面の灯りが出ない");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Drive" + picture + ".png");
            if (tex == null) Debug.LogWarning("灯りの絵が無い: Assets/Textures/Drive" + picture + ".png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(col.r * gain, col.g * gain, col.b * gain, 1f));
            m.SetTextureScale("_BaseMap", new Vector2(1f / wide, 1f / deep));
            m.SetTextureOffset("_BaseMap", new Vector2(0.5f, -0.5f));
            EditorUtility.SetDirty(m);
            return m;
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
