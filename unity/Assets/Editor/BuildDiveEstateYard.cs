using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地。手すりの外の景色と、隣の棟から中の棟まで。その先は書き割り。
    ///
    /// **手すりの向こうは背景ではなく距離の作り。** 棟を一つ立てただけでは、
    /// 廊下を歩いても景色が動かず、一枚の書き割りになる。
    /// 高さと細かさの違う物を、三つの層に分けて重ねる。
    ///
    /// <list type="table">
    /// <item><term>手前 (z -13 〜 0)</term><description>
    /// 敷地の中。舗装の継ぎ目、白線の駐車場と車、ゴミ集積所、遊具、街灯、植え込み。
    /// 形が細かく、廊下を歩くと画面の上でいちばん大きく動く</description></item>
    /// <item><term>中 (z 2 〜 35)</term><description>
    /// 道路と歩道、隣の棟、同じ団地の別の棟を三つ。
    /// 窓の四角が等間隔に並ぶだけの書き割りで、棟の端の陰から次の棟が出入りする</description></item>
    /// <item><term>奥 (中心から 80 m より先)</term><description>
    /// 組まずに撮って貼る書き割り（<c>BuildDiveEstateFar.cs</c>）。本物の地面は中心から 80 m の
    /// 正十六角形で切れ、そこから先の地面も街並みも、板の輪に貼った絵が持つ。
    /// 前はここにビル十二棟と鉄塔二基と堤防を組んでいたが、書き割りへ置き換えて外した</description></item>
    /// </list>
    ///
    /// **高さは <see cref="EstateTop"/> と <see cref="Floor"/> から導く。**
    /// 階数が変われば廊下の目の高さも動くので、目の高さとの関係で決まる物
    /// （街灯の笠）を直値で書くと、階数を直した途端に関係が崩れる。
    /// 実寸で決まっている物（生垣 0.9 m、車 4 m、ゴミ小屋 1.6 m）はそのまま書く
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 敷地の寸法 --------------------------------------------------------

        /// <summary>敷地の西の端。地面（<see cref="EstateShell"/>）の端の内側</summary>
        const float YardWest = -13.4f;
        /// <summary>敷地の東の端</summary>
        const float YardEast = 24.4f;
        /// <summary>敷地と道路の境。生垣がここに回る</summary>
        const float YardEdge = -0.9f;
        /// <summary>道路の手前の縁</summary>
        const float RoadNear = 2.4f;
        /// <summary>道路の向こうの縁。ここから隣の棟の足元まで歩道</summary>
        const float RoadFar = 7.2f;

        // ---- 手すりの外・手前の層 ----------------------------------------------

        /// <summary>
        /// 敷地の中。舗装から街灯まで。
        ///
        /// ここに置く物はすべて素材ごとの入れ物（<see cref="EstateBanks"/>）へ溜める。
        /// 自前の地の色が要る物（植え込み・中の棟）は
        /// 場所の Transform を受け取る <see cref="EstateBlockWall"/> の側に置いてある
        /// </summary>
        static void EstateYard(EstateBanks b)
        {
            EstateYardPave(b);
            EstateYardLot(b);
            EstateYardLife(b);
            EstateYardPlay(b);
            EstateYardRoad(b);
        }

        /// <summary>
        /// 舗装の継ぎ目。等間隔の線が地面に入ると、敷地の広さが目で測れる。
        /// 廊下を歩くと、足元に近い線ほど速く流れる
        /// </summary>
        static void EstateYardPave(EstateBanks b)
        {
            for (var i = 0; i < 5; i++)
            {
                var z = -12.2f + i * 3.1f;
                b.Shade.FaceY(0.035f, YardWest, YardEast, z - 0.04f, z + 0.04f, 1);
            }
            for (var i = 0; i < 8; i++)
            {
                var x = -10.8f + i * 4.6f;
                b.Shade.FaceY(0.035f, x - 0.04f, x + 0.04f, -13.2f, YardEdge, 1);
            }
        }

        /// <summary>
        /// 駐車場と車。
        ///
        /// **暗い舗装に白線が等間隔で並ぶ形を、細かい作り込みより先に置く。**
        /// 車は箱を四つ重ねただけだが、白線の升目に収まっていれば車に見える
        /// </summary>
        static void EstateYardLot(EstateBanks b)
        {
            const float lotX0 = -13.2f;
            const float lotX1 = -2.4f;
            const float bay = 2.55f;
            b.Shade.FaceY(0.02f, lotX0, lotX1, -12.8f, -3.0f, 1);
            for (var row = 0; row < 2; row++)
            {
                var z0 = row == 0 ? -12.6f : -7.4f;
                var z1 = z0 + 4.6f;
                for (var i = 0; i < 5; i++)
                {
                    var x = lotX0 + 0.3f + i * bay;
                    b.Bright.FaceY(0.06f, x - 0.06f, x + 0.06f, z0, z1, 1);
                }
                // 車止めの線。列の奥を閉じる
                var stop = row == 0 ? z0 : z1;
                b.Bright.FaceY(0.06f, lotX0 + 0.3f, lotX0 + 0.3f + 4f * bay, stop - 0.06f, stop + 0.06f, 1);
            }
            // 六台。地の色を四つに分けるのは、同じ色が並ぶと一つの塊に見えるため
            EstateCar(b, b.Red, lotX0 + 0.3f + bay * 0.5f, -10.3f);
            EstateCar(b, b.Bright, lotX0 + 0.3f + bay * 1.5f, -10.3f);
            EstateCar(b, b.Gear, lotX0 + 0.3f + bay * 3.5f, -10.3f);
            EstateCar(b, b.Soft, lotX0 + 0.3f + bay * 0.5f, -5.1f);
            EstateCar(b, b.Bright, lotX0 + 0.3f + bay * 2.5f, -5.1f);
            EstateCar(b, b.Red, lotX0 + 0.3f + bay * 3.5f, -5.1f);
        }

        /// <summary>
        /// 車一台。地と客室の箱に、窓の帯と車輪の暗い箱を重ねる。
        /// 窓の帯が無いと、白い箱が冷蔵庫に見える
        /// </summary>
        static void EstateCar(EstateBanks b, Bank paint, float x, float z)
        {
            paint.Box(new Vector3(x, 0.56f, z), new Vector3(1.66f, 0.60f, 4.00f));
            paint.Box(new Vector3(x, 1.06f, z + 0.15f), new Vector3(1.50f, 0.54f, 1.90f));
            b.Shade.Box(new Vector3(x, 1.12f, z + 0.15f), new Vector3(1.54f, 0.32f, 1.94f));
            b.Shade.Box(new Vector3(x, 0.30f, z - 1.32f), new Vector3(1.74f, 0.52f, 0.54f));
            b.Shade.Box(new Vector3(x, 0.30f, z + 1.32f), new Vector3(1.74f, 0.52f, 0.54f));
        }

        /// <summary>
        /// 暮らしの道具。ゴミ集積所。
        /// 駐輪場・物干し台・低い棟・室外機・集合ポスト・掲示板は日本の団地の物なので外した。
        ///
        /// 地面の上に人の背丈ほどの塊がいくつも散っていると、
        /// 手前の層と隣の棟のあいだに段が生まれて、距離が読めるようになる
        /// </summary>
        static void EstateYardLife(EstateBanks b)
        {
            // ゴミ集積所。三方を囲った小屋。敷地の隅にこれがあると団地の裏に見える
            b.Gear.Box(new Vector3(21.8f, 0.80f, -11.0f), new Vector3(3.40f, 1.60f, 0.10f));
            b.Gear.Box(new Vector3(20.15f, 0.80f, -10.0f), new Vector3(0.10f, 1.60f, 2.10f));
            b.Gear.Box(new Vector3(23.45f, 0.80f, -10.0f), new Vector3(0.10f, 1.60f, 2.10f));
            b.Gear.Box(new Vector3(21.8f, 1.68f, -10.0f), new Vector3(3.70f, 0.08f, 2.40f));
            for (var i = 0; i < 3; i++)
                b.Red.Box(new Vector3(20.7f + i * 1.1f, 0.45f, -10.3f), new Vector3(0.80f, 0.90f, 0.70f));
        }

        /// <summary>
        /// 遊具。滑り台とベンチ。砂場は日本の公園の形なので外した。
        ///
        /// 敷地の東の隅に、地面から 1.5 m ほどの高さの形をひとかたまり置く。
        /// 手前の層が駐車場と駐輪場だけだと、高さが二種類しか無くて平らに見える
        /// </summary>
        static void EstateYardPlay(EstateBanks b)
        {
            const float slideX = 16.6f;
            const float slideZ = -5.6f;
            b.Gear.Box(new Vector3(slideX, 1.46f, slideZ), new Vector3(1.20f, 0.08f, 1.20f));
            b.Gear.Box(new Vector3(slideX - 0.50f, 0.73f, slideZ), new Vector3(0.09f, 1.46f, 0.09f));
            b.Gear.Box(new Vector3(slideX + 0.50f, 0.73f, slideZ), new Vector3(0.09f, 1.46f, 0.09f));
            // 斜面。明るい帯にすると、朝日の当たらない側でも段が読める
            b.Bright.Box(new Vector3(slideX, 0.92f, slideZ + 1.70f), new Vector3(0.80f, 0.07f, 3.10f),
                Quaternion.Euler(22f, 0f, 0f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(slideX - 0.42f + i * 0.84f, 0.92f, slideZ - 0.80f),
                    new Vector3(0.07f, 2.00f, 0.07f), Quaternion.Euler(-18f, 0f, 0f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(slideX, 0.52f + i * 0.44f, slideZ - 1.02f + i * 0.14f),
                    new Vector3(0.84f, 0.05f, 0.05f));

            // ベンチ二脚。滑り台の側へ向ける
            for (var i = 0; i < 2; i++)
            {
                var x = 18.4f + i * 3.6f;
                b.Set.Box(new Vector3(x, 0.42f, -2.6f), new Vector3(1.70f, 0.08f, 0.44f));
                b.Set.Box(new Vector3(x, 0.72f, -2.40f), new Vector3(1.70f, 0.36f, 0.06f));
                b.Gear.Box(new Vector3(x - 0.70f, 0.21f, -2.6f), new Vector3(0.08f, 0.42f, 0.40f));
                b.Gear.Box(new Vector3(x + 0.70f, 0.21f, -2.6f), new Vector3(0.08f, 0.42f, 0.40f));
            }
        }

        /// <summary>
        /// 道路とガードレールと街灯。手前の層と中の層のあいだを横切る帯。
        ///
        /// **等間隔の繰り返しを三つ重ねる。** 中央線の破線、ガードレールの支柱、街灯の柱。
        /// この三つが違う間隔で流れると、廊下を歩いたときの視差がいちばん強く出る
        /// </summary>
        static void EstateYardRoad(EstateBanks b)
        {
            // 廊下の目の高さ。街灯の笠はここより少し下に来ると、外を見下ろした絵に収まる
            const float eye = EstateTop + 1.62f;

            b.Shade.FaceY(0.02f, -14f, 25f, RoadNear, RoadFar, 1);
            b.Bright.FaceY(0.06f, -14f, 25f, RoadNear + 0.10f, RoadNear + 0.26f, 1);
            b.Bright.FaceY(0.06f, -14f, 25f, RoadFar - 0.26f, RoadFar - 0.10f, 1);
            // 中央線の破線
            for (var i = 0; i < 13; i++)
                b.Bright.FaceY(0.06f, -13.2f + i * 3.0f, -11.4f + i * 3.0f,
                    (RoadNear + RoadFar) * 0.5f - 0.08f, (RoadNear + RoadFar) * 0.5f + 0.08f, 1);

            // 街灯。8 m 間隔で五本。笠は道路の側へ張り出す。
            // 高さは目の高さから導く。柱の頭が廊下の目より下にあると、上から見下ろす形になる
            var lampHigh = Mathf.Min(4.6f, eye - 1.0f);
            for (var i = 0; i < 5; i++)
            {
                var x = -10.5f + i * 8.0f;
                b.Gear.Box(new Vector3(x, lampHigh * 0.5f, 1.10f), new Vector3(0.14f, lampHigh, 0.14f));
                b.Gear.Box(new Vector3(x, lampHigh - 0.06f, 1.52f), new Vector3(0.09f, 0.09f, 0.90f));
                b.Gear.Box(new Vector3(x, lampHigh - 0.18f, 1.92f), new Vector3(0.44f, 0.14f, 0.62f));
            }
        }

        // ---- 手すりの外・中と奥の層 --------------------------------------------

        /// <summary>
        /// 隣の棟と、中の棟と、敷地の植え込みと、奥の書き割り。
        ///
        /// <see cref="EstateYard"/> は場所の Transform を受け取らないので、
        /// 自前の地の色を持つ物はこちらへまとめる。
        /// 窓の黒い四角だけは棟ぜんぶで一つの入れ物を分け合う。マテリアルの枚数を増やさないため
        /// </summary>
        static void EstateBlockWall(Transform place, EstateBanks b)
        {
            var slabs = new Bank { Texel = 0.4f };
            var panes = new Bank { Texel = 0.4f };
            var mid = new Bank { Texel = 0.35f };
            var land = new Bank { Texel = 0.35f };

            EstateNextBlock(b, slabs, panes);
            EstateMidBlocks(mid, panes);
            // 遠い地面。敷地の地面は隣の棟の足元（z 9.5）で切れているので、その先を一枚で塞ぐ。
            // 塞がないと棟の脇に空の色がそのまま抜けて、棟が虚空に立って見える。
            // 敷地の地面より少し下へ置くので、手前では地面に隠れる。
            //
            // **縁は書き割りの輪と同じ中心・同じ向きの正多角形にする**（BuildDiveEstateFar.cs）。
            // そうすると撮った絵の中の地面の始まりが、どの板でも同じ高さの一本の線になり、
            // 中心から見て本物の地面の縁とちょうど繋がる
            land.FanY(new Vector3(EstateFarCentre.x, -0.05f, EstateFarCentre.z), EstateFarRim());

            // 遠いほど空の色へ寄せる。同じ色で並べると、どれが手前か分からなくなる
            NoShadow(EstateEmit(place, "EstateBlock", slabs,
                Glow("EstateBlock", new Color(0.50f, 0.53f, 0.60f), 0.40f), false));
            NoShadow(EstateEmit(place, "EstateBlockPane", panes, Mat("Ceiling"), false));
            NoShadow(EstateEmit(place, "EstateMid", mid,
                Glow("EstateMid", new Color(0.54f, 0.58f, 0.66f), 0.44f), false));
            // 遠い地面は中の棟と同じ青い灰色にしない。棟と同じ色だと、敷地の外が何も無い虚空に読める。
            // 枯れた芝と土の色にして、団地の外の空き地と畑に見せる。書き割りの地面も同じマテリアルで撮る
            NoShadow(EstateEmit(place, "EstateLand", land, EstateLandMat(), false));

            EstateYardGreen(place);
            // 60 m より先は組まない。撮った絵を板の輪に貼る
            EstateBackdrop(place);
        }

        /// <summary>隣の棟の間口の数。東を短くして、中の棟が端から顔を出せるようにした</summary>
        const int BlockBays = 9;
        /// <summary>隣の棟の間口。ここに窓が一つずつ並ぶ</summary>
        const float BlockBay = 2.6f;
        /// <summary>隣の棟の階数</summary>
        const int BlockFloors = 5;
        /// <summary>隣の棟の西の端</summary>
        const float BlockWest = -13.1f;
        /// <summary>隣の棟の東の端。ここから東は中の棟の受け持ち</summary>
        const float BlockEast = BlockBay * (BlockBays - 1) - 11.2f + 1.9f;             // 11.5
        /// <summary>隣の棟の高さ。階の高さから導くので、階数を直しても窓の列と屋上がずれない</summary>
        const float BlockHigh = 1.6f + BlockFloors * Floor;                            // 15.6

        /// <summary>
        /// 隣の棟。窓の四角が等間隔に並ぶだけの書き割り。
        ///
        /// **灯りを当てない。** 面を Unlit で持たせると、朝日が回らない側でも値が決まり、
        /// 夜明けの薄い空を背にした輪郭がそのまま出る。当たりも影も持たせない。
        /// バルコニーの帯と屋上の塔屋を重ねるのは、窓の列だけでは面が平らに沈むため。
        ///
        /// **東を 11.5 で切ってある。** 一棟で視界を端から端まで塞ぐと、
        /// 廊下のどこへ立っても同じ絵になって距離が出ない。切った先に中の棟を置く
        /// </summary>
        static void EstateNextBlock(EstateBanks b, Bank slabs, Bank panes)
        {
            slabs.FaceZ(BlockFace, BlockWest, BlockEast, 0f, BlockHigh, -1);

            // 階ごとの水平の帯。窓の列だけだと面が平らすぎて、棟の高さが出ない
            for (var j = 0; j < BlockFloors; j++)
                panes.FaceZ(BlockFace - 0.12f, BlockWest, BlockEast, 1.22f + j * Floor, 1.38f + j * Floor, -1);
            for (var i = 0; i < BlockBays; i++)
            {
                for (var j = 0; j < BlockFloors; j++)
                {
                    var x = -11.2f + i * BlockBay;
                    var y = 1.6f + j * Floor;
                    // 数枚だけ灯りが入っている。全部暗いと廃墟に、全部明るいと看板に見える
                    var into = (i * 5 + j * 3) % 13 == 0 ? b.Lit : panes;
                    into.FaceZ(BlockFace - 0.16f, x - 0.72f, x + 0.72f, y, y + 1.15f, -1);
                    // バルコニーの手すり。**窓より明るい側に置く。**
                    // 暗い帯にすると窓と繋がって、棟ぜんたいが黒い塊になる。
                    // 朝日の当たる手すりが明るく、その下の影が暗いと、階の帯が横へ通る
                    b.Far.Box(new Vector3(x, y - 0.42f, BlockFace - 0.42f), new Vector3(1.90f, 0.86f, 0.10f));
                    panes.Box(new Vector3(x, y - 0.84f, BlockFace - 0.22f), new Vector3(1.94f, 0.08f, 0.44f));
                }
            }
            // 屋上。立ち上がりと塔屋と水槽。棟の頭が切り落とされて見えないように
            panes.Box(new Vector3((BlockWest + BlockEast) * 0.5f, BlockHigh + 0.25f, BlockFace - 0.18f),
                new Vector3(BlockEast - BlockWest, 0.50f, 0.36f));
        }

        /// <summary>
        /// 中の層。同じ団地の別の棟を三つと、給水塔。棟の足元を受ける地面は <see cref="EstateBlockWall"/> が張る。
        ///
        /// **三つとも向きと距離を変える。** 東の棟は隣の棟の東の端から顔を出し、
        /// 西の棟は妻をこちらへ向け、高層棟は隣の棟の屋上の向こうに頭だけを出す。
        /// 廊下を東へ歩くと、隣の棟の端の陰から東の棟が出てくる幅が変わる。ここが視差の勘所
        /// </summary>
        static void EstateMidBlocks(Bank mid, Bank panes)
        {
            // 東の棟。隣の棟の東の端のすぐ向こう
            EstateMidBlock(mid, panes, 17.5f, 39.0f, 19.0f, 4, true);
            // 西の棟。妻を東へ向けて、廊下の西端から角が見えるように
            EstateMidBlock(mid, panes, -31.0f, -15.5f, 14.5f, 4, false);
            // 高層棟。隣の棟の屋上より高い形を一つ置くと、棟の列が平らに並ばなくなる
            EstateMidBlock(mid, panes, -2.0f, 12.0f, 33.0f, 10, true);

        }

        /// <summary>
        /// 中の棟を一つ。窓の列だけの書き割りに、妻を一枚添えて角を見せる。
        /// <paramref name="westEnd"/> が真なら西の妻、偽なら東の妻。
        /// 面が一枚きりだと、廊下を横へ歩いたときに紙が立っているように見える
        /// </summary>
        static void EstateMidBlock(Bank mid, Bank panes, float x0, float x1, float z, int floors, bool westEnd)
        {
            var high = 1.4f + floors * Floor;
            mid.FaceZ(z, x0, x1, 0f, high, -1);
            if (westEnd) mid.FaceX(x0, z, z + 9f, 0f, high, -1);
            else mid.FaceX(x1, z, z + 9f, 0f, high, 1);
            // 屋上の立ち上がり
            mid.FaceZ(z - 0.12f, x0 - 0.25f, x1 + 0.25f, high, high + 0.55f, -1);

            // 窓の列。階ごとの帯と、間口ごとの四角
            var bays = Mathf.Max(2, Mathf.FloorToInt((x1 - x0 - 1.6f) / BlockBay));
            var step = (x1 - x0 - 1.6f) / bays;
            for (var j = 0; j < floors; j++)
            {
                var y = 1.4f + j * Floor;
                panes.FaceZ(z - 0.10f, x0 + 0.4f, x1 - 0.4f, y - 0.30f, y - 0.16f, -1);
                for (var i = 0; i < bays; i++)
                {
                    var x = x0 + 0.8f + (i + 0.5f) * step;
                    panes.FaceZ(z - 0.14f, x - 0.62f, x + 0.62f, y, y + 1.10f, -1);
                }
            }
        }

        /// <summary>
        /// 敷地の植え込み。生垣を三方へ回し、低木と木を散らす。
        ///
        /// 緑は他の四つの場所と分け合う <see cref="Mat"/> の表に無いので、自前で一枚持つ。
        /// 生垣は敷地の境をなぞるので、地面の端の継ぎ目もここが隠す
        /// </summary>
        static void EstateYardGreen(Transform place)
        {
            var leaf = new Bank { Texel = 0.4f };

            // 道路側の生垣。3.9 m ごとに切れ目を入れる
            for (var i = 0; i < 10; i++)
                leaf.Box(new Vector3(-11.6f + i * 3.9f, 0.45f, YardEdge), new Vector3(3.50f, 0.90f, 0.80f));
            // 東西の境
            for (var i = 0; i < 3; i++)
            {
                leaf.Box(new Vector3(YardWest + 0.2f, 0.45f, -11.4f + i * 3.6f), new Vector3(0.80f, 0.90f, 3.20f));
                leaf.Box(new Vector3(YardEast - 0.2f, 0.45f, -11.4f + i * 3.6f), new Vector3(0.80f, 0.90f, 3.20f));
            }
            // 低木。生垣の列に高さの差が無いと、地面に緑の帯を引いただけに見える
            leaf.Box(new Vector3(-2.2f, 0.62f, -1.8f), new Vector3(1.40f, 1.24f, 1.30f));
            leaf.Box(new Vector3(9.4f, 0.55f, -1.8f), new Vector3(1.20f, 1.10f, 1.20f));
            leaf.Box(new Vector3(23.0f, 0.70f, -3.4f), new Vector3(1.50f, 1.40f, 1.40f));

            // 木を三本。幹と、大きさを変えた三段の葉。
            //
            // **葉を一枚の薄い板で済ませない。** 廊下から見ると木は手前の層でいちばん高く、
            // 板のままだと黒い長方形が景色の真ん中に立つ。段を付けて輪郭を崩す
            var trunks = new[] { 1.0f, 12.2f, 22.4f };
            for (var i = 0; i < trunks.Length; i++)
            {
                var x = trunks[i];
                var z = -2.5f + (i % 2) * 0.6f;
                var lift = 1f - (i % 3) * 0.08f;
                leaf.Box(new Vector3(x, 1.40f * lift, z), new Vector3(0.26f, 2.80f * lift, 0.26f));
                leaf.Box(new Vector3(x, 3.05f * lift, z), new Vector3(2.70f, 1.05f, 2.30f));
                leaf.Box(new Vector3(x - 0.2f, 3.85f * lift, z + 0.15f), new Vector3(2.00f, 0.95f, 1.75f));
                leaf.Box(new Vector3(x + 0.25f, 4.45f * lift, z - 0.10f), new Vector3(1.15f, 0.80f, 1.05f));
            }

            EstateEmit(place, "EstateLeaf", leaf,
                EstatePaint("EstateLeaf", new Color(0.118f, 0.152f, 0.098f), 0.05f), false);
        }
    }
}
