using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地。戸の内側。玄関と居間。
    ///
    /// **戸を開けたらすぐ居間、では家に見えない。** 三和土で一段下げ、
    /// 上がり框で仕切り、その先に居間を置く。廊下から覗いたときに
    /// 奥行きが二段あることが、ここが誰かの家だと伝える。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 印になる点は <c>BuildDiveEstate.cs</c> の const に置いて両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 右の家（老夫婦ジョルジョとエレナ） --------------------------------
        //
        // 間取りは「三和土 → 仕切り → 居間（茶の間）」。仕切りは上がり框の内側へ
        // 半歩だけ引いて立て、抜けの東半分は腰板付きの硝子戸が開いたまま塞いでいる。
        // 戸を開けて正面に見えるのは仕切りの面と戸の腰板で、居間の床は抜けの西の
        // 幅ぶんしか見えない。**腰板より上は硝子なので、居間に立つ人の胸から上は通る。**
        // 記憶 8 でエレナが「新聞、来てる？」と声をかけるとき、姿がここから見える。
        //
        // **左の家のような南北の廊下は通せない。** 記憶 15 の夫は戸口から座る所まで
        // 一直線に歩き、その線は框の内側 0.3〜1.0 m を西へ斜めに横切る。記憶 8 の
        // 「抜け」の点と記憶 15 の「抜けの側」の点も、框から 0.4 m と 0.65 m の所にある。
        // 南北に壁を通すと夫が壁を抜け、点が壁の中へ入る。仕切りは一枚に留める

        /// <summary>右の家の仕切りの真ん中。上がり框の内側へ半歩</summary>
        const float RoomHall = HallSill - 0.31f;                                   // -15.86
        /// <summary>
        /// 居間への抜けの西の端。
        ///
        /// **戸口の真ん中（<see cref="DoorB"/>）より西へ置く。** 東へ寄せると、
        /// 記憶 15 の夫が戸口から座る所まで歩く線が、抜けではなく壁を通る
        /// </summary>
        const float RoomGap0 = 3.56f;
        /// <summary>抜けの東の端。玄関の東の壁と揃える</summary>
        const float RoomGap1 = HallEast;                                           // 5.15
        /// <summary>
        /// 開いたままの硝子戸の西の端。ここから東が腰板で塞がる。
        ///
        /// **これ以上西へ寄せられない。** 記憶 8 の鍵打ちは三和土（x 4.4）から
        /// 居間（x 4.4）へ真っ直ぐ入るので、仕切りの面の x 4.4 は開いていなければ、
        /// 主の視界を腰板が横切る
        /// </summary>
        const float RoomLeaf = 4.44f;
        /// <summary>
        /// 硝子戸の腰板の高さ。
        ///
        /// **ここがこの家の要。** 低くすると三和土から居間の床が奥まで見え、
        /// 高くすると居間に立つ妻（胸が 1.2 m）が腰板の陰に入る。
        /// 三和土から妻の胸へ引いた線は、戸の面で 1.36 m を通る
        /// </summary>
        const float RoomWaist = 1.12f;
        /// <summary>硝子戸の頭。鴨居（<see cref="HallHead"/>）より一枚ぶん低い</summary>
        const float RoomLeafTop = HallHead - 0.08f;

        /// <summary>
        /// 右の家（記憶 8・15）。玄関と居間。
        ///
        /// 座って見る絵なので、目の高さ 1.15 m から卓とテレビが同時に入るように寄せる
        /// </summary>
        static void EstateRoom(EstateBanks b)
        {
            EstateGenkan(b);

            // 床板。廊下と同じ土間の色では、戸を跨いで中へ入ったことが伝わらない。
            // 居間の一枚と、仕切りと框のあいだの踏み込みの一枚
            b.Board.FaceY(EstateTop, RoomX0, RoomX1, RoomBack, RoomHall - HallSkin, 1);
            b.Board.FaceY(EstateTop, RoomGap0, RoomGap1, RoomHall - HallSkin, HallSill, 1);

            EstateTea(b);
            EstateShrine(b);
            EstateCloset(b);

            // 幅木。壁の裾に線が一本通らないと、居間の壁が塗っただけの面に見える
            EstateSkirt(b, RoomX0 + 0.02f, RoomBack, RoomHall - HallSkin, true);
            EstateSkirt(b, RoomX1 - 0.02f, RoomBack, RoomHall - HallSkin, true);
            EstateSkirt(b, RoomBack + 0.02f, RoomX0, RoomX1, false);
            EstateSkirt(b, RoomHall - HallSkin - 0.02f, RoomX0, RoomGap0, false);
            EstateSkirt(b, RoomHall - HallSkin - 0.02f, RoomGap1, RoomX1, false);

            // 蛍光灯の笠。光る面そのものは Estate が板で置く
            b.Shade.Box(new Vector3(5.0f, RoomRoof - 0.05f, -17.3f), new Vector3(1.3f, 0.1f, 0.32f));
        }

        /// <summary>
        /// 茶の間の真ん中。窓・座卓・座布団・座椅子・テレビ。
        ///
        /// **左の家と暮らし方を分ける。** あちらは脚の高い卓と椅子で朝の途中、
        /// こちらは座卓と座椅子で、二人ぶんの湯呑が出たままになっている
        /// </summary>
        static void EstateTea(EstateBanks b)
        {
            // 奥の窓。居間でいちばん明るい面。硝子は光る面で済ませ、枠と桟を手前へ回す
            b.Lit.FaceZ(RoomBack + 0.02f, 3.00f, 3.80f, EstateTop + 1.00f, EstateTop + 1.90f, -1);
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(i == 0 ? 3.03f : 3.77f, EstateTop + 1.45f, RoomBack + 0.05f),
                    new Vector3(0.06f, 0.98f, 0.06f));
            b.Gear.Box(new Vector3(3.40f, EstateTop + 1.45f, RoomBack + 0.05f), new Vector3(0.05f, 0.94f, 0.04f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(3.40f, EstateTop + 0.97f + i * 0.96f, RoomBack + 0.05f),
                    new Vector3(0.86f, 0.06f, 0.06f));
            // 短いカーテン。襞は板の前後をずらして出す。窓の両端へ寄せて開けておく
            b.Gear.Box(new Vector3(3.40f, EstateTop + 2.00f, RoomBack + 0.13f), new Vector3(1.10f, 0.05f, 0.05f));
            for (var i = 0; i < 4; i++)
                b.Soft.Box(new Vector3((i < 2 ? 2.94f : 3.58f) + (i % 2) * 0.28f, EstateTop + 1.40f,
                        RoomBack + 0.11f + (i % 2) * 0.05f),
                    new Vector3(0.28f, 1.14f, 0.05f));
            // 窓の下の飾り棚。**抜けを覗いた線のいちばん奥がここに来る。**
            // 棚が無い版では、奥の壁の裾まで床が続いて居間の奥行きがそのまま絵に出た
            b.Set.Box(new Vector3(3.40f, EstateTop + 0.40f, RoomBack + 0.16f), new Vector3(0.80f, 0.80f, 0.30f));
            b.Shade.Box(new Vector3(3.40f, EstateTop + 0.42f, RoomBack + 0.32f), new Vector3(0.02f, 0.64f, 0.02f));
            b.Bright.Box(new Vector3(3.18f, EstateTop + 0.88f, RoomBack + 0.16f), new Vector3(0.14f, 0.12f, 0.10f));
            b.Red.Box(new Vector3(3.60f, EstateTop + 0.89f, RoomBack + 0.16f), new Vector3(0.13f, 0.14f, 0.13f));

            // テレビと台
            b.Set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            b.Set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));

            // 炬燵。天板と、掛けたままの布団。**老夫婦の茶の間はこれで言い切る。**
            // 布団が床まで垂れるので、抜けから覗いた線は炬燵で止まり、
            // その奥の床は絵に出ない。記憶 15 の「夫の座るところ」も「居間」も、
            // ちょうど炬燵の縁に当たる
            b.Soft.Box(new Vector3(3.96f, EstateTop + 0.17f, -17.15f), new Vector3(1.24f, 0.30f, 0.94f));
            b.Set.Box(new Vector3(3.96f, EstateTop + 0.34f, -17.15f), new Vector3(1.10f, 0.06f, 0.70f));
            // 卓の上。二人ぶんの湯呑と急須、畳んだ新聞と老眼鏡
            b.Bright.Box(new Vector3(3.72f, EstateTop + 0.42f, -17.03f), new Vector3(0.10f, 0.09f, 0.10f));
            b.Bright.Box(new Vector3(4.16f, EstateTop + 0.42f, -17.27f), new Vector3(0.10f, 0.09f, 0.10f));
            b.Gear.Box(new Vector3(3.94f, EstateTop + 0.45f, -16.95f), new Vector3(0.20f, 0.16f, 0.18f));
            b.Paper.Box(new Vector3(4.30f, EstateTop + 0.39f, -17.01f),
                new Vector3(0.30f, 0.03f, 0.22f), Quaternion.Euler(0f, 14f, 0f));
            b.Gear.Box(new Vector3(3.60f, EstateTop + 0.39f, -17.31f), new Vector3(0.14f, 0.02f, 0.05f));

            // 座布団と座椅子。夫と妻の座る所。記憶 15 の二人はここに座る
            EstateSeat(b, 3.45f, -16.45f);
            EstateSeat(b, 4.95f, -16.65f);

            // 電気ポットと薬の袋。老夫婦の卓の脇に一つずつ
            b.Linen.Box(new Vector3(4.62f, EstateTop + 0.14f, -17.56f), new Vector3(0.24f, 0.28f, 0.24f));
            b.Shade.Box(new Vector3(4.62f, EstateTop + 0.29f, -17.56f), new Vector3(0.20f, 0.03f, 0.20f));
            b.Paper.Box(new Vector3(4.44f, EstateTop + 0.05f, -16.86f),
                new Vector3(0.16f, 0.09f, 0.12f), Quaternion.Euler(0f, 22f, 0f));
        }

        /// <summary>
        /// 座布団と座椅子を一組。背の低い座椅子は、立つ人の胸より下に収まるので
        /// 三和土からエレナを見る線を遮らない
        /// </summary>
        static void EstateSeat(EstateBanks b, float x, float z)
        {
            b.Soft.Box(new Vector3(x, EstateTop + 0.05f, z), new Vector3(0.58f, 0.10f, 0.58f));
            b.Soft.Box(new Vector3(x, EstateTop + 0.28f, z + 0.28f), new Vector3(0.52f, 0.46f, 0.08f));
            b.Set.Box(new Vector3(x, EstateTop + 0.51f, z + 0.30f), new Vector3(0.54f, 0.05f, 0.05f));
        }

        /// <summary>
        /// 壁際の物。西の仏壇、東の整理箪笥と柱時計、仕切りの壁際の茶箪笥。
        ///
        /// **箪笥は壁際へ回す。** 抜けの正面へ置いていた版では、居間に立つ妻が
        /// 箪笥の陰に入って、玄関から姿が見えなかった（オーナーの差し戻し）。
        /// 西の壁は記憶 15 の点が三つ（-16.40 / -17.05 / -18.05）並んでいて、
        /// 置けるのは -17.35 から -17.80 の 0.45 m だけ。そこへ仏壇を入れる
        /// </summary>
        static void EstateShrine(EstateBanks b)
        {
            // 仏壇。西の壁。扉の中だけ明るい。この場面でいちばん小さい光。
            // **背の高い物を抜けの正面の奥へ一つ置く。** 戸口から覗いた線が
            // 居間の西の隅まで抜けるのを、ここで止める
            b.Set.Box(new Vector3(RoomX0 + 0.23f, EstateTop + 0.86f, -17.575f), new Vector3(0.46f, 1.60f, 0.45f));
            b.Shade.Box(new Vector3(RoomX0 + 0.47f, EstateTop + 1.02f, -17.575f), new Vector3(0.03f, 0.78f, 0.33f));
            b.Bright.Box(new Vector3(RoomX0 + 0.45f, EstateTop + 1.02f, -17.575f), new Vector3(0.02f, 0.58f, 0.25f));
            b.Gear.Box(new Vector3(RoomX0 + 0.42f, EstateTop + 0.76f, -17.42f), new Vector3(0.06f, 0.14f, 0.06f));
            b.Red.Box(new Vector3(RoomX0 + 0.42f, EstateTop + 0.76f, -17.73f), new Vector3(0.08f, 0.18f, 0.08f));

            // 整理箪笥。東の壁の南寄り。背の高い物はここへ集める
            b.Set.Box(new Vector3(RoomX1 - 0.28f, EstateTop + 0.76f, -17.75f), new Vector3(0.56f, 1.52f, 1.00f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(RoomX1 - 0.57f, EstateTop + 0.30f + i * 0.34f, -17.75f),
                    new Vector3(0.02f, 0.05f, 0.86f));
            b.Paper.Box(new Vector3(RoomX1 - 0.31f, EstateTop + 1.54f, -17.62f), new Vector3(0.22f, 0.03f, 0.16f));

            // 柱時計。二人きりの家で音のする物はこれとテレビだけ
            b.Set.Box(new Vector3(RoomX1 - 0.03f, EstateTop + 1.72f, -16.98f), new Vector3(0.05f, 0.34f, 0.26f));
            b.Bright.Box(new Vector3(RoomX1 - 0.07f, EstateTop + 1.72f, -16.98f), new Vector3(0.02f, 0.22f, 0.18f));

            // 茶箪笥。仕切りの居間側、抜けの東。**背は腰板と同じだけに抑える。**
            // これより高くすると、三和土から妻の胸を見る線に掛かる
            b.Set.Box(new Vector3(5.75f, EstateTop + 0.50f, -16.18f), new Vector3(1.00f, 1.00f, 0.44f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(5.75f, EstateTop + 0.34f + i * 0.30f, -15.95f),
                    new Vector3(0.86f, 0.03f, 0.02f));
            // 茶箪笥の上。夫婦の写真と、電話と、日めくり
            b.Gear.Box(new Vector3(5.38f, EstateTop + 1.08f, -16.18f), new Vector3(0.20f, 0.16f, 0.06f));
            b.Bright.Box(new Vector3(5.38f, EstateTop + 1.08f, -16.21f), new Vector3(0.15f, 0.12f, 0.02f));
            b.Shade.Box(new Vector3(5.86f, EstateTop + 1.07f, -16.18f), new Vector3(0.26f, 0.14f, 0.22f));
            b.Paper.Box(new Vector3(6.14f, EstateTop + 1.06f, -16.18f), new Vector3(0.16f, 0.12f, 0.14f));

            // 仕切りの居間側の壁。記憶 15 は座って北を向くので、掛ける高さは
            // 座った目（1.15 m）から 30 度以内に収める
            b.Paper.Box(new Vector3(3.24f, EstateTop + 1.34f, RoomHall - HallSkin - 0.02f),
                new Vector3(0.36f, 0.48f, 0.02f));
            b.Gear.Box(new Vector3(3.24f, EstateTop + 1.60f, RoomHall - HallSkin - 0.03f),
                new Vector3(0.40f, 0.04f, 0.03f));
        }

        /// <summary>
        /// 押し入れ。奥の壁の真ん中に襖二枚。布団はこの中なので、床には出さない。
        ///
        /// **戸口から見通した帯をここで塞ぐ。** 抜けを覗いた線の行き着く先が
        /// 空いた床のままだと、居間の奥行きがそのまま絵に出る
        /// </summary>
        static void EstateCloset(EstateBanks b)
        {
            b.Wall.Box(new Vector3(4.375f, (EstateTop + RoomRoof) * 0.5f, -18.25f),
                new Vector3(0.95f, RoomRoof - EstateTop, 0.70f));
            // 襖二枚と鴨居。合わせ目の線が一本入らないと、二枚が一枚の板に見える
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(4.135f + i * 0.48f, EstateTop + 0.90f, -17.88f),
                    new Vector3(0.47f, 1.76f, 0.05f));
            b.Set.Box(new Vector3(4.375f, EstateTop + 1.82f, -17.87f), new Vector3(1.01f, 0.07f, 0.07f));
            b.Shade.Box(new Vector3(4.375f, EstateTop + 0.90f, -17.85f), new Vector3(0.02f, 1.72f, 0.02f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(4.24f + i * 0.28f, EstateTop + 0.92f, -17.84f),
                    new Vector3(0.05f, 0.13f, 0.02f));
        }

        /// <summary>
        /// 右の家の玄関。三和土・上がり框・手すり・下駄箱・物入と、居間への仕切り。
        ///
        /// **仕切りを一枚立てて、抜けの東半分は開いた硝子戸で塞ぐ。**
        /// 戸口の正面から覗いて絵に入るのは、三和土と框と仕切りの面、
        /// それに硝子戸の腰板まで。居間の床は抜けの西の 0.88 m ぶんしか出ない
        /// </summary>
        static void EstateGenkan(EstateBanks b)
        {
            // 三和土と、框の蹴上げ
            b.Tile.FaceY(EstateTop - EstateSunk, RoomGap0, RoomGap1, HallSill, EstateFace, 1);
            b.Tile.FaceZ(HallSill, RoomGap0, RoomGap1, EstateTop - EstateSunk, EstateTop, 1);
            // 上がり框の板。当たりは蹴上げの面が持つので、この縁には入れない
            b.Set.Box(new Vector3((RoomGap0 + RoomGap1) * 0.5f, EstateTop - 0.09f, HallSill + 0.035f),
                new Vector3(RoomGap1 - RoomGap0, 0.18f, 0.07f));

            // 玄関の西と東の物入。中は作らないので、面だけ立てて閉じる
            b.Wall.Box(new Vector3(RoomGap0 - HallSkin, (EstateTop - EstateSunk + RoomRoof) * 0.5f,
                    (HallSill + EstateFace) * 0.5f),
                new Vector3(HallSkin * 2f, RoomRoof - EstateTop + EstateSunk, EstateFace - HallSill));
            b.Wall.Box(new Vector3(RoomGap1 + HallSkin, (EstateTop - EstateSunk + RoomRoof) * 0.5f,
                    (HallSill + EstateFace) * 0.5f),
                new Vector3(HallSkin * 2f, RoomRoof - EstateTop + EstateSunk, EstateFace - HallSill));
            // 西の物入の板戸。引手の線を二本入れて、壁ではなく建具に見せる
            b.Set.Box(new Vector3(RoomGap0 - 0.01f, EstateTop + 0.72f, -15.18f), new Vector3(0.03f, 1.74f, 0.66f));
            b.Shade.Box(new Vector3(RoomGap0 - 0.03f, EstateTop + 0.72f, -15.18f), new Vector3(0.01f, 1.70f, 0.02f));

            // 居間との仕切り。抜けの上は垂れ壁で閉じる
            var gap = new List<Vector4>
            {
                new Vector4(RoomGap0, RoomGap1, EstateTop, EstateTop + HallHead),
            };
            b.Wall.FaceZHoles(RoomHall + HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, 1, gap);
            b.Wall.FaceZHoles(RoomHall - HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, -1, gap);
            b.Wall.FaceX(RoomGap0, RoomHall - HallSkin, RoomHall + HallSkin, EstateTop, EstateTop + HallHead, 1);
            b.Wall.FaceX(RoomGap1, RoomHall - HallSkin, RoomHall + HallSkin, EstateTop, EstateTop + HallHead, -1);
            b.Wall.FaceY(EstateTop + HallHead, RoomGap0, RoomGap1, RoomHall - HallSkin, RoomHall + HallSkin, -1);
            // 抜けの縁と敷居。木の枠が回ると、壁に空いた穴ではなく建具の抜けに見える
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(i == 0 ? RoomGap0 : RoomGap1, EstateTop + HallHead * 0.5f, RoomHall),
                    new Vector3(0.05f, HallHead, HallSkin * 2f + 0.03f));
            b.Set.Box(new Vector3((RoomGap0 + RoomGap1) * 0.5f, EstateTop + HallHead, RoomHall),
                new Vector3(RoomGap1 - RoomGap0 + 0.1f, 0.05f, HallSkin * 2f + 0.03f));
            b.Set.Box(new Vector3((RoomGap0 + RoomGap1) * 0.5f, EstateTop + 0.02f, RoomHall),
                new Vector3(RoomGap1 - RoomGap0, 0.04f, HallSkin * 2f));

            // 引き違いの硝子戸。二枚とも東へ引いて重ねてある。
            // **腰板の高さがこの家の要。** 低すぎると居間の床が見え、高すぎると
            // 居間に立つ人が隠れる。1.02 m は、立つ人の胸（1.2 m）の下を通る
            EstateGlass(b, RoomLeaf, RoomGap1, RoomHall + 0.035f, true);
            EstateGlass(b, RoomLeaf + 0.06f, RoomGap1, RoomHall - 0.035f, false);

            // 下駄箱。三和土の東の端。硝子戸の腰板と背を揃える
            b.Set.Box(new Vector3(4.94f, EstateTop + 0.32f, -15.14f), new Vector3(0.36f, 0.94f, 0.62f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(4.76f, EstateTop + 0.10f + i * 0.44f, -15.14f),
                    new Vector3(0.02f, 0.36f, 0.54f));
            b.Gear.Box(new Vector3(4.94f, EstateTop + 0.90f, -15.30f), new Vector3(0.16f, 0.22f, 0.16f));
            b.Soft.Box(new Vector3(4.94f, EstateTop + 1.12f, -15.30f), new Vector3(0.26f, 0.28f, 0.24f));
            b.Bright.Box(new Vector3(4.94f, EstateTop + 0.81f, -14.98f), new Vector3(0.20f, 0.03f, 0.14f));

            // 框の手すり。**老夫婦の家だとここで言い切る。** 靴を履くのに掴む縦の棒と、
            // 框に沿った横の棒。戸口の正面から見える、いちばん低い所の物になる
            b.Gear.Box(new Vector3(4.66f, EstateTop + 0.30f, HallSill - 0.04f), new Vector3(0.05f, 1.20f, 0.05f));
            b.Gear.Box(new Vector3(4.66f, EstateTop + 0.88f, HallSill - 0.10f), new Vector3(0.05f, 0.05f, 0.18f));
            b.Gear.Box(new Vector3(4.66f, EstateTop + 0.06f, HallSill - 0.10f), new Vector3(0.05f, 0.05f, 0.18f));
            // 踏み台。膝が上がらないぶん、框の手前にもう一段置く
            b.Set.Box(new Vector3(4.28f, EstateTop - EstateSunk + 0.055f, -15.34f), new Vector3(0.52f, 0.11f, 0.26f));

            // 靴。**主と妻の通り道を外して置く。** 通り道に置くと、二人が靴を踏んで歩く
            EstateShoes(b, 3.76f, -15.36f, 8f);
            EstateShoes(b, 4.06f, -15.38f, -6f);

            // 傘立て。三和土の西の隅。長い傘と、杖が一本
            b.Gear.Box(new Vector3(3.72f, EstateTop - EstateSunk + 0.22f, -14.98f), new Vector3(0.24f, 0.44f, 0.24f));
            b.Shade.Box(new Vector3(3.68f, EstateTop - EstateSunk + 0.57f, -14.98f), new Vector3(0.05f, 0.74f, 0.05f));
            b.Set.Box(new Vector3(3.77f, EstateTop - EstateSunk + 0.52f, -15.01f), new Vector3(0.04f, 0.84f, 0.04f));
            b.Set.Box(new Vector3(3.77f, EstateTop - EstateSunk + 0.92f, -15.05f), new Vector3(0.04f, 0.04f, 0.13f));

            // 玄関マット。上から覗いたとき、三和土と框の境がここで一度切れる
            b.Soft.Box(new Vector3(4.22f, EstateTop - EstateSunk + 0.015f, -15.02f),
                new Vector3(0.80f, 0.03f, 0.40f));

            // **目の高さの物は玄関の左右の壁へ掛ける。** 仕切りの面は抜けで開いていて、
            // そこへ掛けると物が宙に浮く。掛ける先は西の物入の板戸と、東の物入の壁。
            // **掛ける先は三和土の北寄りに限る。** 東の壁の南寄りへ掛けると、
            // 廊下から妻を見る線がその物に当たる
            b.Gear.Box(new Vector3(RoomGap1 - 0.06f, EstateTop + 1.72f, -15.16f),
                new Vector3(0.04f, 0.04f, 0.34f));
            b.Soft.Box(new Vector3(RoomGap1 - 0.09f, EstateTop + 1.24f, -15.16f),
                new Vector3(0.14f, 0.84f, 0.30f));
            b.Paper.Box(new Vector3(RoomGap0 - 0.04f, EstateTop + 1.46f, -14.98f),
                new Vector3(0.02f, 0.34f, 0.26f));
            b.Bright.Box(new Vector3(RoomGap0 - 0.05f, EstateTop + 1.46f, -14.98f),
                new Vector3(0.01f, 0.28f, 0.20f));
        }

        /// <summary>
        /// 開いたままの硝子戸を一枚。腰板と框と、上の桟だけ。硝子は張らない。
        ///
        /// <paramref name="front"/> が真なら手前の一枚で、中桟が一本入る。
        /// 二枚が少しずれて重なっていることが、引き違いの戸だと伝える
        /// </summary>
        static void EstateGlass(EstateBanks b, float x0, float x1, float z, bool front)
        {
            var mid = (x0 + x1) * 0.5f;
            var run = x1 - x0;
            // 腰板
            b.Set.Box(new Vector3(mid, EstateTop + RoomWaist * 0.5f, z), new Vector3(run, RoomWaist, 0.04f));
            // 上下の框と、左右の縦框
            b.Set.Box(new Vector3(mid, EstateTop + RoomLeafTop, z), new Vector3(run, 0.08f, 0.05f));
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(i == 0 ? x0 + 0.03f : x1 - 0.03f, EstateTop + RoomLeafTop * 0.5f, z),
                    new Vector3(0.06f, RoomLeafTop, 0.05f));
            if (!front) return;
            // 中桟。硝子の面を上下に分ける線が一本入ると、そこが硝子だと分かる
            b.Set.Box(new Vector3(mid, EstateTop + (RoomWaist + RoomLeafTop) * 0.5f, z),
                new Vector3(run, 0.04f, 0.045f));
            // 引手。開け切った戸の端に来る
            b.Shade.Box(new Vector3(x0 + 0.10f, EstateTop + 0.86f, z + 0.025f), new Vector3(0.05f, 0.13f, 0.02f));
        }

        /// <summary>
        /// 幅木を一本。<paramref name="alongZ"/> が真なら x が一定の壁、偽なら z が一定の壁
        /// </summary>
        static void EstateSkirt(EstateBanks b, float at, float from, float to, bool alongZ)
        {
            var mid = (from + to) * 0.5f;
            var run = Mathf.Abs(to - from);
            b.Set.Box(alongZ ? new Vector3(at, EstateTop + 0.05f, mid) : new Vector3(mid, EstateTop + 0.05f, at),
                alongZ ? new Vector3(0.03f, 0.10f, run) : new Vector3(run, 0.10f, 0.03f));
        }

        /// <summary>
        /// 靴を一足。三和土の上に並ぶ小さな暗い塊が、土間を土間として読ませる。
        ///
        /// <paramref name="grow"/> を 1 より小さくすると子どもの靴になる。
        /// 大人の靴の隣に小さいのが一足あるだけで、誰と誰が住んでいるかが伝わる
        /// </summary>
        static void EstateShoes(EstateBanks b, float x, float z, float turn, float grow = 1f)
        {
            var rot = Quaternion.Euler(0f, turn, 0f);
            for (var i = 0; i < 2; i++)
            {
                var at = new Vector3(x + (i == 0 ? -0.09f : 0.09f) * grow,
                    EstateTop - EstateSunk + 0.05f * grow, z);
                b.Shade.Box(at, new Vector3(0.11f, 0.10f, 0.26f) * grow, rot);
                b.Shade.Box(at + new Vector3(0f, 0.04f, -0.07f) * grow,
                    new Vector3(0.11f, 0.08f, 0.10f) * grow, rot);
            }
        }

        // ---- 左の家（母ハンナと娘メイ） ----------------------------------------
        //
        // 間取りは L 字。三和土から上がった短い廊下の西が抜けていて、そこから台所へ入り、
        // さらに南へ回ると居間に出る。**廊下の正面は塞ぐ。** そこには EstateShell の
        // 光る面（EstateHallLight）が立っていて、抜けを開けると光る板の中を通ることになる。
        // 塞いだ面は曇り硝子の引き戸ということにして、枠と桟を手前へ回す。
        // 記憶 0 の母はこの硝子を背にするので、逆光の影のまま立つ

        /// <summary>左の家の西の壁の内側。建物の西の面から 0.15 m 入る</summary>
        const float FlatX0 = -1.05f;
        /// <summary>左の家の東の壁の内側。右の家との戸境</summary>
        const float FlatX1 = 2.88f;
        /// <summary>左の家の奥の壁の内側</summary>
        const float FlatBack = -18.30f;
        /// <summary>家の中の仕切りの厚みの半分</summary>
        const float FlatSkin = 0.06f;
        /// <summary>左の家の天井。玄関の天井（<c>EstateShell</c> が張る）と同じ高さで通す</summary>
        const float FlatRoof = EstateTop + 2.2f;

        /// <summary>
        /// 左の戸口の奥（記憶 0・1）。母ハンナ（34）と娘メイ（7）の住戸。
        ///
        /// **母の立つところだけ床の高さのまま残す。** 記憶 0 は子どもの目から母の脚を見上げる
        /// 鍵打ちがあり、三和土をそこまで下げると母が 0.15 m 浮く。
        /// 玄関のすのこを敷いて、母はその上に立っていることにする
        /// </summary>
        static void EstatePorch(EstateBanks b)
        {
            EstateFlatShell(b);
            EstateFlatGenkan(b);
            EstateFlatKitchen(b);
            EstateFlatRoom(b);
        }

        /// <summary>
        /// 左の家の躯体。三和土・床板・壁・天井・幅木。
        ///
        /// 建物の面は外を向いた一枚きりなので、**内側にもう一枚張らないと外が見通せる。**
        /// 戸口の分だけ抜いて、三和土の底から天井まで通す
        /// </summary>
        static void EstateFlatShell(EstateBanks b)
        {
            // 三和土と、框の蹴上げ
            b.Tile.FaceY(EstateTop - EstateSunk, PorchX0, PorchX1, PorchSill, EstateFace, 1);
            b.Tile.FaceZ(PorchSill, PorchX0, PorchX1, EstateTop - EstateSunk, EstateTop, 1);
            b.Set.Box(new Vector3(DoorA, EstateTop - 0.09f, PorchSill + 0.035f),
                new Vector3(PorchX1 - PorchX0, 0.18f, 0.07f));

            // 床板。玄関の廊下・台所・居間の三枚。**廊下と同じ土間の色では中へ入ったことが伝わらない**
            b.Board.FaceY(EstateTop, PorchX0, PorchX1, PorchBack, PorchSill, 1);
            b.Board.FaceY(EstateTop, FlatX0, PorchX0, FlatBack, EstateFace, 1);
            b.Board.FaceY(EstateTop, PorchX0, FlatX1, FlatBack, PorchBack, 1);

            // 外周の壁
            b.Wall.FaceX(FlatX0, FlatBack, EstateFace, EstateTop - EstateSunk, FlatRoof, 1);
            b.Wall.FaceX(FlatX1, FlatBack, PorchBack, EstateTop, FlatRoof, -1);
            b.Wall.FaceZ(FlatBack, FlatX0, FlatX1, EstateTop, FlatRoof, 1);
            b.Wall.FaceZHoles(EstateFace - 0.02f, FlatX0, PorchX1, EstateTop - EstateSunk, FlatRoof, -1,
                new List<Vector4>
                {
                    new Vector4(DoorA - DoorHalf, DoorA + DoorHalf, EstateTop - EstateSunk, EstateTop + DoorHigh),
                });

            // 三和土の西の仕切り。廊下の西は抜けているので、三和土の分だけ立てる
            b.Wall.Box(new Vector3(PorchX0 - FlatSkin, (EstateTop - EstateSunk + FlatRoof) * 0.5f,
                    (PorchSill + EstateFace) * 0.5f),
                new Vector3(FlatSkin * 2f, FlatRoof - EstateTop + EstateSunk, EstateFace - PorchSill));
            // 玄関の東の壁。この奥は物入れで、中は作らない
            b.Wall.Box(new Vector3(PorchX1 + FlatSkin, (EstateTop - EstateSunk + FlatRoof) * 0.5f,
                    (PorchBack + EstateFace) * 0.5f),
                new Vector3(FlatSkin * 2f, FlatRoof - EstateTop + EstateSunk, EstateFace - PorchBack));
            // 廊下の正面の壁と、居間の側から見た同じ壁。光る面はこの手前に立つ
            b.Wall.FaceZ(PorchBack, PorchX0, PorchX1, EstateTop, FlatRoof, 1);
            b.Wall.FaceZ(PorchBack - FlatSkin * 2f, PorchX0, FlatX1, EstateTop, FlatRoof, -1);
            // 抜けの垂れ壁。天井まで抜けていると、玄関と台所の切れ目が読めない
            b.Wall.Box(new Vector3(PorchX0 - FlatSkin, (EstateTop + HallHead + FlatRoof) * 0.5f,
                    (PorchBack + PorchSill) * 0.5f),
                new Vector3(FlatSkin * 2f, FlatRoof - EstateTop - HallHead, PorchSill - PorchBack));
            // 抜けの縁。木の枠が回ると、壁に空いた穴ではなく建具の抜けに見える
            b.Set.Box(new Vector3(PorchX0 - FlatSkin, EstateTop + HallHead, (PorchBack + PorchSill) * 0.5f),
                new Vector3(FlatSkin * 2f + 0.03f, 0.05f, PorchSill - PorchBack + 0.10f));

            // 天井。上を向いた面は日射しを受けないので、暗いままでよい
            b.Shade.FaceY(FlatRoof, FlatX0, PorchX0, FlatBack, EstateFace, -1);
            b.Shade.FaceY(FlatRoof, PorchX0, FlatX1, FlatBack, PorchBack, -1);

            // 幅木。壁の裾に線が一本通らないと、居間の壁が塗っただけの面に見える
            EstateSkirt(b, FlatX0 + 0.02f, FlatBack, EstateFace, true);
            EstateSkirt(b, FlatX1 - 0.02f, FlatBack, PorchBack, true);
            EstateSkirt(b, FlatBack + 0.02f, FlatX0, FlatX1, false);
            EstateSkirt(b, EstateFace - 0.04f, FlatX0, PorchX0, false);
            EstateSkirt(b, PorchBack - FlatSkin * 2f - 0.02f, PorchX0, FlatX1, false);
        }

        /// <summary>
        /// 左の家の玄関。すのこ・下駄箱・靴・傘立てと、正面の曇り硝子の引き戸。
        ///
        /// **母と娘の家だと三和土で言い切る。** 大人の靴の隣に小さい靴、傘立てに短い傘、
        /// 硝子に貼ったクレヨンの絵。戸口の正面から覗いて絵に入るのはここまでで、
        /// 台所と居間は中へ入った人にしか見えない
        /// </summary>
        static void EstateFlatGenkan(EstateBanks b)
        {
            // すのこ。板の隙間を暗い線で入れておかないと、ただの踏み台に見える
            b.Set.Box(new Vector3(1.70f, EstateTop - 0.075f, -15.12f), new Vector3(0.80f, 0.15f, 0.50f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(1.70f, EstateTop + 0.002f, -15.31f + i * 0.126f),
                    new Vector3(0.80f, 0.02f, 0.02f));

            // 下駄箱。戸口の抜けに掛からないよう西の壁へ寄せる
            b.Set.Box(new Vector3(1.00f, EstateTop + 0.325f, -15.18f), new Vector3(0.28f, 0.95f, 0.72f));
            b.Shade.Box(new Vector3(1.15f, EstateTop + 0.33f, -15.18f), new Vector3(0.02f, 0.80f, 0.60f));
            b.Red.Box(new Vector3(1.00f, EstateTop + 0.87f, -15.32f), new Vector3(0.16f, 0.14f, 0.16f));
            b.Soft.Box(new Vector3(1.00f, EstateTop + 1.02f, -15.32f), new Vector3(0.22f, 0.20f, 0.20f));
            b.Bright.Box(new Vector3(1.00f, EstateTop + 0.90f, -15.02f), new Vector3(0.14f, 0.20f, 0.03f));

            // 靴。大人の一足と、子どもの一足。**母の通り道（すのこ）を外して置く**
            EstateShoes(b, 1.30f, -15.46f, -10f);
            EstateShoes(b, 2.02f, -15.44f, 16f, 0.66f);

            // 傘立て。三和土の東の隅。長い傘と、子どもの短い傘
            b.Gear.Box(new Vector3(2.24f, EstateTop + 0.07f, -15.36f), new Vector3(0.24f, 0.44f, 0.24f));
            b.Shade.Box(new Vector3(2.20f, EstateTop + 0.32f, -15.34f), new Vector3(0.05f, 0.74f, 0.05f));
            b.Red.Box(new Vector3(2.29f, EstateTop + 0.16f, -15.39f), new Vector3(0.05f, 0.50f, 0.05f));

            // 曇り硝子の引き戸。**光る面がこの硝子。** 枠と桟を手前へ回して、
            // ただ光る板ではなく、灯りの点いた奥の部屋の戸として読ませる
            const float leaf = PorchBack + 0.10f;
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(i == 0 ? PorchX0 + 0.03f : PorchX1 - 0.03f, EstateTop + 1.10f, leaf),
                    new Vector3(0.06f, 2.20f, 0.11f));
            b.Set.Box(new Vector3(DoorA, EstateTop + 2.18f, leaf), new Vector3(1.56f, 0.08f, 0.11f));
            b.Set.Box(new Vector3(DoorA, EstateTop + 0.02f, leaf), new Vector3(1.56f, 0.05f, 0.11f));
            b.Set.Box(new Vector3(DoorA, EstateTop + 1.08f, leaf - 0.01f), new Vector3(0.05f, 2.12f, 0.05f));
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(DoorA, EstateTop + 0.72f + i * 0.76f, leaf - 0.01f),
                    new Vector3(1.44f, 0.04f, 0.05f));
            b.Shade.Box(new Vector3(1.44f, EstateTop + 1.06f, leaf + 0.01f), new Vector3(0.05f, 0.13f, 0.03f));
            // 硝子へ貼った子どもの絵。逆光で影になるが、戸口の正面から見える唯一の紙になる
            b.Shade.Box(new Vector3(1.16f, EstateTop + 1.24f, leaf + 0.01f),
                new Vector3(0.28f, 0.22f, 0.02f), Quaternion.Euler(0f, 0f, 6f));

            // 廊下の東の壁。クレヨンの絵と、掛けた通学帽と手提げ袋。
            // **貼る高さは子どもの目。** 大人の目の高さに掛けると、住んでいるのが誰か分からない
            for (var i = 0; i < 3; i++)
                b.Paper.Box(new Vector3(PorchX1 - 0.02f, EstateTop + 1.02f + (i % 2) * 0.22f,
                        -15.82f - i * 0.22f),
                    new Vector3(0.02f, 0.24f, 0.19f), Quaternion.Euler((i - 1) * 5f, 0f, 0f));
            b.Red.Box(new Vector3(PorchX1 - 0.04f, EstateTop + 1.24f, -15.82f), new Vector3(0.01f, 0.08f, 0.07f));
            b.Gear.Box(new Vector3(PorchX1 - 0.04f, EstateTop + 1.58f, -16.12f), new Vector3(0.04f, 0.04f, 0.42f));
            b.Red.Box(new Vector3(PorchX1 - 0.12f, EstateTop + 1.46f, -16.24f), new Vector3(0.20f, 0.10f, 0.22f));
            b.Linen.Box(new Vector3(PorchX1 - 0.10f, EstateTop + 1.28f, -15.98f), new Vector3(0.16f, 0.36f, 0.14f));
        }

        /// <summary>
        /// 台所。廊下の西の抜けを入ってすぐ。窓・流し台・吊り戸棚・冷蔵庫。
        ///
        /// **窓をこの家でいちばん明るい面にする。** 右の家をテレビの青が持たせているところを、
        /// こちらは朝の白い窓が持つ。抜けをくぐって最初に目へ入るのがこれになる
        /// </summary>
        static void EstateFlatKitchen(EstateBanks b)
        {
            // 窓。硝子は光る面で済ませる。枠と桟と、端へ寄せた短いカーテン
            b.Lit.FaceX(FlatX0 + 0.02f, -17.10f, -15.90f, EstateTop + 1.02f, EstateTop + 1.92f, 1);
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(FlatX0 + 0.05f, EstateTop + 1.47f, i == 0 ? -17.13f : -15.87f),
                    new Vector3(0.06f, 0.98f, 0.06f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(FlatX0 + 0.05f, EstateTop + 0.99f + i * 0.96f, -16.50f),
                    new Vector3(0.06f, 0.06f, 1.32f));
            b.Gear.Box(new Vector3(FlatX0 + 0.05f, EstateTop + 1.47f, -16.50f), new Vector3(0.05f, 0.94f, 0.04f));
            b.Gear.Box(new Vector3(FlatX0 + 0.12f, EstateTop + 2.00f, -16.50f), new Vector3(0.04f, 0.04f, 1.44f));
            for (var i = 0; i < 2; i++)
                b.Linen.Box(new Vector3(FlatX0 + 0.12f, EstateTop + 1.60f, i == 0 ? -17.14f : -15.86f),
                    new Vector3(0.05f, 0.76f, 0.26f));

            // 流し台。扉の並びと、天板の上のシンク・蛇口・水切り
            b.Set.Box(new Vector3(FlatX0 + 0.31f, EstateTop + 0.42f, -16.50f), new Vector3(0.62f, 0.84f, 1.60f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(FlatX0 + 0.62f, EstateTop + 0.44f, -16.92f + i * 0.84f),
                    new Vector3(0.02f, 0.66f, 0.38f));
            b.Gear.Box(new Vector3(FlatX0 + 0.32f, EstateTop + 0.86f, -16.50f), new Vector3(0.66f, 0.05f, 1.64f));
            b.Shade.Box(new Vector3(FlatX0 + 0.32f, EstateTop + 0.83f, -16.78f), new Vector3(0.46f, 0.10f, 0.54f));
            b.Gear.Box(new Vector3(FlatX0 + 0.08f, EstateTop + 1.02f, -16.78f), new Vector3(0.05f, 0.30f, 0.05f));
            b.Gear.Box(new Vector3(FlatX0 + 0.20f, EstateTop + 1.15f, -16.78f), new Vector3(0.28f, 0.05f, 0.05f));
            b.Gear.Box(new Vector3(FlatX0 + 0.32f, EstateTop + 0.95f, -16.06f), new Vector3(0.34f, 0.13f, 0.30f));
            for (var i = 0; i < 2; i++)
                b.Bright.Box(new Vector3(FlatX0 + 0.26f + i * 0.14f, EstateTop + 1.03f, -16.06f),
                    new Vector3(0.11f, 0.06f, 0.11f));

            // 吊り戸棚
            b.Set.Box(new Vector3(FlatX0 + 0.17f, EstateTop + 1.72f, -15.35f), new Vector3(0.34f, 0.62f, 0.72f));
            b.Shade.Box(new Vector3(FlatX0 + 0.35f, EstateTop + 1.72f, -15.35f), new Vector3(0.02f, 0.52f, 0.02f));

            // 冷蔵庫。**白い大きな面をひとつ立てる。** 右の家は暗い木ばかりで、これが無い。
            // 扉には子どもの紙が二枚貼ってある
            b.Linen.Box(new Vector3(0.40f, EstateTop + 0.75f, -15.12f), new Vector3(0.62f, 1.50f, 0.62f));
            b.Shade.Box(new Vector3(0.40f, EstateTop + 1.06f, -15.44f), new Vector3(0.58f, 0.02f, 0.02f));
            b.Gear.Box(new Vector3(0.15f, EstateTop + 1.00f, -15.45f), new Vector3(0.04f, 0.30f, 0.05f));
            b.Paper.Box(new Vector3(0.51f, EstateTop + 1.26f, -15.44f),
                new Vector3(0.20f, 0.16f, 0.01f), Quaternion.Euler(0f, 0f, -7f));
            b.Paper.Box(new Vector3(0.29f, EstateTop + 0.80f, -15.44f),
                new Vector3(0.14f, 0.18f, 0.01f), Quaternion.Euler(0f, 0f, 5f));

            // 洗濯物の籠と、畳みかけの山。右の家の座布団が揃っているのと対にする
            b.Gear.Box(new Vector3(0.42f, EstateTop + 0.18f, -17.96f), new Vector3(0.50f, 0.36f, 0.42f));
            for (var i = 0; i < 3; i++)
                b.Linen.Box(new Vector3(0.42f + (i % 2) * 0.05f, EstateTop + 0.40f + i * 0.09f, -17.96f),
                    new Vector3(0.42f, 0.09f, 0.34f));

            // 奥の壁へ貼った子どもの絵。**この壁がいちばん広く空いている。**
            // 何も無い版では、台所の南が塗っただけの面のまま四畳ぶん続いていた。
            // 貼る高さは子どもの手が届くところで揃える
            for (var i = 0; i < 3; i++)
                b.Paper.Box(new Vector3(-0.62f + i * 0.36f, EstateTop + 1.06f + (i % 2) * 0.16f, FlatBack + 0.03f),
                    new Vector3(0.26f, 0.21f, 0.02f), Quaternion.Euler(0f, 0f, (i - 1) * 4f));
            b.Red.Box(new Vector3(-0.26f, EstateTop + 1.22f, FlatBack + 0.05f), new Vector3(0.09f, 0.07f, 0.01f));

            // 台所の蛍光灯
            b.Shade.Box(new Vector3(FlatX0 + 0.62f, FlatRoof - 0.06f, -16.60f), new Vector3(0.90f, 0.10f, 0.26f));
            b.Lit.Box(new Vector3(FlatX0 + 0.62f, FlatRoof - 0.14f, -16.60f), new Vector3(0.78f, 0.05f, 0.17f));
        }

        /// <summary>
        /// 居間。台所から南へ回った先。卓と椅子・押し入れ・絵本の棚・時計。
        ///
        /// **座って暮らす右の家と作りを分ける。** あちらは低い卓と座布団、こちらは
        /// 脚の高い卓と椅子二脚。娘の椅子だけ引き出したままで、朝の途中だと伝える
        /// </summary>
        static void EstateFlatRoom(EstateBanks b)
        {
            // 卓。天板と脚
            b.Set.Box(new Vector3(1.95f, EstateTop + 0.68f, -17.25f), new Vector3(1.06f, 0.06f, 0.70f));
            for (var i = 0; i < 4; i++)
                b.Set.Box(new Vector3(1.95f + ((i & 1) == 0 ? -0.46f : 0.46f), EstateTop + 0.33f,
                    -17.25f + ((i & 2) == 0 ? -0.28f : 0.28f)), new Vector3(0.06f, 0.64f, 0.06f));
            // 椅子二脚。娘の側は引き出したまま
            EstateFlatChair(b, 1.95f, -16.80f, 180f, false);
            EstateFlatChair(b, 1.18f, -17.28f, 74f, true);

            // 卓の上。茶碗と牛乳のコップ、置いたままの皿。
            // 水筒は記憶 0 で母が娘へ渡すもので、まだ蓋が開いている
            b.Bright.Box(new Vector3(1.72f, EstateTop + 0.75f, -17.12f), new Vector3(0.13f, 0.08f, 0.13f));
            b.Bright.Box(new Vector3(2.22f, EstateTop + 0.77f, -17.36f), new Vector3(0.09f, 0.12f, 0.09f));
            b.Paper.Box(new Vector3(1.98f, EstateTop + 0.72f, -17.40f), new Vector3(0.22f, 0.02f, 0.22f));
            b.Red.Box(new Vector3(2.28f, EstateTop + 0.81f, -17.06f), new Vector3(0.10f, 0.20f, 0.10f));
            b.Gear.Box(new Vector3(2.28f, EstateTop + 0.94f, -17.06f), new Vector3(0.11f, 0.06f, 0.11f));

            // 押し入れ。襖二枚と鴨居。布団はこの中なので、床には出さない
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(FlatX1 - 0.03f, EstateTop + 0.92f, -16.98f - i * 0.46f),
                    new Vector3(0.05f, 1.76f, 0.45f));
            b.Set.Box(new Vector3(FlatX1 - 0.04f, EstateTop + 1.84f, -17.21f), new Vector3(0.07f, 0.07f, 1.00f));
            // 合わせ目。線が一本入らないと、襖二枚が一枚の板に見える
            b.Shade.Box(new Vector3(FlatX1 - 0.06f, EstateTop + 0.92f, -17.21f), new Vector3(0.02f, 1.72f, 0.02f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(FlatX1 - 0.07f, EstateTop + 0.92f, -16.83f - i * 0.76f),
                    new Vector3(0.02f, 0.13f, 0.05f));

            // 時計とカレンダー。右の家は掛け軸と写真で、こちらは日付の紙
            b.Bright.Box(new Vector3(FlatX1 - 0.02f, EstateTop + 1.76f, -16.62f), new Vector3(0.03f, 0.26f, 0.26f));
            b.Shade.Box(new Vector3(FlatX1 - 0.05f, EstateTop + 1.76f, -16.62f), new Vector3(0.01f, 0.13f, 0.03f));
            b.Paper.Box(new Vector3(FlatX1 - 0.02f, EstateTop + 1.42f, -18.02f), new Vector3(0.03f, 0.36f, 0.27f));

            // 絵本の低い棚。背の高さが揃った色の帯が、子どもの物だと伝える
            b.Set.Box(new Vector3(2.44f, EstateTop + 0.29f, -18.10f), new Vector3(0.72f, 0.58f, 0.32f));
            b.Shade.Box(new Vector3(2.44f, EstateTop + 0.30f, -17.95f), new Vector3(0.66f, 0.02f, 0.02f));
            for (var i = 0; i < 4; i++)
            {
                var into = (i % 2) == 0 ? b.Red : b.Bright;
                into.Box(new Vector3(2.16f + i * 0.09f, EstateTop + 0.46f, -18.06f),
                    new Vector3(0.07f, 0.26f, 0.22f), Quaternion.Euler(0f, 0f, i == 3 ? 11f : 0f));
            }
            // 床へ出たままの積み木
            b.Red.Box(new Vector3(1.02f, EstateTop + 0.05f, -18.06f), new Vector3(0.10f, 0.10f, 0.10f));
            b.Bright.Box(new Vector3(1.17f, EstateTop + 0.05f, -17.93f),
                new Vector3(0.10f, 0.10f, 0.10f), Quaternion.Euler(0f, 24f, 0f));

            // 居間の蛍光灯
            b.Shade.Box(new Vector3(1.95f, FlatRoof - 0.06f, -17.30f), new Vector3(0.30f, 0.10f, 0.92f));
            b.Lit.Box(new Vector3(1.95f, FlatRoof - 0.14f, -17.30f), new Vector3(0.20f, 0.05f, 0.80f));
        }

        /// <summary>椅子ひとつ。座と背と、左右の脚。<paramref name="turn"/> は背の向く向き</summary>
        static void EstateFlatChair(EstateBanks b, float x, float z, float turn, bool cushion)
        {
            var rot = Quaternion.Euler(0f, turn, 0f);
            var at = new Vector3(x, EstateTop, z);
            b.Set.Box(at + new Vector3(0f, 0.44f, 0f), new Vector3(0.40f, 0.05f, 0.40f), rot);
            b.Set.Box(at + rot * new Vector3(0f, 0.68f, -0.18f), new Vector3(0.40f, 0.44f, 0.05f), rot);
            for (var i = 0; i < 2; i++)
                b.Set.Box(at + rot * new Vector3(i == 0 ? -0.16f : 0.16f, 0.21f, 0f),
                    new Vector3(0.05f, 0.42f, 0.36f), rot);
            if (cushion)
                b.Red.Box(at + new Vector3(0f, 0.51f, 0f), new Vector3(0.34f, 0.10f, 0.34f), rot);
        }

        // ---- 戸口 --------------------------------------------------------------

        /// <summary>
        /// 戸口ひとつ。枠・番号板・呼び鈴・郵便受け・メーター。
        ///
        /// 壁に開けた穴だけでは戸口に見えない。枠と、脇に並ぶ小物が付いて初めて
        /// 「人が出入りする戸」として読める。三つとも同じ組み合わせ・同じ高さにして、
        /// 並びを強める。**戸の脇へ置くのはこの四つまで。** 表札まで重ねた版では、
        /// 戸の左に紙と板が五枚縦に並んで、どれも読めない塊になった。
        /// 幅も戸の真ん中から 0.6 m の内側へ入らないように詰めてある。
        ///
        /// 左と真ん中の戸は開いている。記憶 0 は母が戸口に立ち、記憶 8 は新聞を持って中へ入り、
        /// 記憶 15 は中から夫を迎える。閉めた戸を置くと、その三つが同じ場所で成り立たなくなる。
        /// 右は中を作っていないので閉めておく
        /// </summary>
        /// <summary>
        /// 戸口。枠と住戸の小物は場所が持ち、**戸の板そのものは記憶が持つ**。
        ///
        /// 記憶 0 では隣の老夫婦はまだ一度も出てきていないのに、その戸が開いた穴になっていた。
        /// 記憶ごとに開け閉めするには、板を記憶の子として置くしかない（場所は四つの記憶で
        /// 使い回すので、場所に置いた板は記憶ごとに消せない）。
        ///
        /// <paramref name="hung"/> が true の戸口は板を置かず、丁番だけ残す。
        /// <see cref="BuildDiveTakes"/> の <c>Shut</c> と <c>Ajar</c> がそこへ板を掛ける。
        /// false の戸口（<see cref="DoorC"/>）は中を作っていないので、場所が閉めたまま持つ
        /// </summary>
        static void EstateDoorway(EstateBanks b, float x, bool hung)
        {
            const float jamb = DoorHalf + 0.06f;
            b.Gear.Box(new Vector3(x - jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            b.Gear.Box(new Vector3(x + jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            b.Gear.Box(new Vector3(x, EstateTop + DoorHigh + 0.06f, EstateFace + 0.05f),
                new Vector3(DoorHalf * 2f + 0.24f, 0.12f, 0.1f));

            // 番号板。廊下でいちばん明るい小さな四角になるので、戸口の位置が遠くからでも拾える
            b.Bright.Box(new Vector3(x - 0.75f, EstateTop + 1.80f, EstateFace + 0.05f), new Vector3(0.24f, 0.15f, 0.02f));
            // 呼び鈴。番号板の下へ。
            // 戸の右は開いた戸が倒れてくる側なので、住戸の小物はすべて左へ寄せる
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.55f, EstateFace + 0.06f), new Vector3(0.09f, 0.13f, 0.03f));
            b.Bright.Box(new Vector3(x - 0.75f, EstateTop + 1.56f, EstateFace + 0.08f), new Vector3(0.04f, 0.04f, 0.02f));
            // 郵便受け
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.28f, EstateFace + 0.06f), new Vector3(0.28f, 0.24f, 0.12f));
            b.Shade.Box(new Vector3(x - 0.75f, EstateTop + 1.34f, EstateFace + 0.125f), new Vector3(0.20f, 0.04f, 0.02f));
            // メーターの箱。硝子の面だけ明るい
            b.Gear.Box(new Vector3(x + 0.80f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.36f, 0.46f, 0.18f));
            b.Bright.Box(new Vector3(x + 0.80f, EstateTop + 1.50f, EstateFace + 0.185f), new Vector3(0.18f, 0.18f, 0.02f));

            if (hung)
            {
                // 丁番だけ。板は記憶が掛けるので、ここには置かない。
                // 丁番が無いと、記憶が開いた板を置いたときに壁から浮いて見える
                for (var i = 0; i < 3; i++)
                    b.Gear.Box(new Vector3(x + 0.51f, EstateTop + 0.40f + i * 0.62f, EstateFace + 0.16f),
                        new Vector3(0.07f, 0.16f, 0.22f));
            }
            else
            {
                EstateLeaf(b, x, DoorHalf, EstateFace + 0.05f);
                b.Gear.Box(new Vector3(x + 0.32f, EstateTop + 1.0f, EstateFace + 0.1f), new Vector3(0.05f, 0.05f, 0.12f));
            }
        }

        /// <summary>
        /// 戸一枚。鉄扉として読ませる。
        ///
        /// 平らな板のままでは物置の戸に見えるので、上に換気口、下に新聞受け、
        /// 目の高さに覗き穴を入れる。どれも住戸の戸にしか付かない
        /// </summary>
        static void EstateLeaf(EstateBanks b, float cx, float half, float z)
        {
            b.Leaf.Box(new Vector3(cx, EstateTop + DoorHigh * 0.5f, z), new Vector3(half * 2f, DoorHigh, 0.05f));
            var face = z + 0.026f;
            // 面を囲う細い線。鉄扉の折り返しの縁
            b.Shade.Box(new Vector3(cx, EstateTop + 1.06f, face), new Vector3(half * 2f - 0.10f, 1.52f, 0.015f));
            // 換気口。細い羽根が三枚
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(cx, EstateTop + 1.80f + i * 0.07f, face + 0.01f),
                    new Vector3(half * 1.1f, 0.035f, 0.02f));
            // 新聞受けと、差さったままの新聞
            b.Shade.Box(new Vector3(cx, EstateTop + 0.34f, face + 0.01f), new Vector3(half * 0.86f, 0.05f, 0.02f));
            b.Gear.Box(new Vector3(cx, EstateTop + 0.34f, face), new Vector3(half * 1.0f, 0.13f, 0.015f));
            // 覗き穴。目の高さの黒い点ひとつで、そこが住戸の戸になる
            b.Shade.Box(new Vector3(cx, EstateTop + 1.52f, face + 0.01f), new Vector3(0.045f, 0.045f, 0.02f));
            b.Gear.Box(new Vector3(cx, EstateTop + 1.52f, face), new Vector3(0.07f, 0.07f, 0.015f));
        }

        // ---- 共用部 ------------------------------------------------------------

        /// <summary>
        /// 最上階の廊下の共用部。消火器・掲示板・配電盤・非常灯・点検口。
        ///
        /// **住戸の物ではなく建物の物を置く。** 傘立てや植木鉢は住んでいる人の物で、
        /// それだけでは長屋にも見える。誰の物でもない物が並んで初めて、
        /// 管理された集合住宅の共用廊下になる。
        /// **置く先は階段の口へまとめる。** 戸口の脇へ散らすと、住戸の番号板や郵便受けと
        /// 混ざって、どれも読めない塊になる。階段を上がり切ったところに建物の物が
        /// 固まっていて、そこから東は住戸の列だけ、という分かれ方にする
        /// </summary>
        static void EstateCommon(EstateBanks b)
        {
            // 掲示板。硝子の入った枠に紙が何枚か。壁でいちばん明るい四角になる。
            // **階数の札（EstateSign）を避けて手すり寄りへ。** 同じ壁の同じ高さに来る
            b.Gear.Box(new Vector3(StairWest + 0.03f, EstateTop + 1.38f, -13.78f), new Vector3(0.05f, 0.74f, 0.66f));
            b.Shade.Box(new Vector3(StairWest + 0.055f, EstateTop + 1.38f, -13.78f), new Vector3(0.02f, 0.64f, 0.58f));
            for (var i = 0; i < 4; i++)
                b.Paper.Box(new Vector3(StairWest + 0.065f, EstateTop + 1.24f + (i % 2) * 0.30f,
                        -13.62f - (i / 2) * 0.32f), new Vector3(0.01f, 0.24f, 0.24f));

            // 消火器。赤い縦長がひとつあるだけで、廊下が共用部だと分かる。
            // 置く先は階段の口と戸口 A のあいだ。上がり切ったところは西の一本の出口なので、
            // そこは空けたまま、面へ寄せて据える
            b.Red.Box(new Vector3(0.20f, EstateTop + 0.30f, -14.52f), new Vector3(0.19f, 0.54f, 0.19f));
            b.Gear.Box(new Vector3(0.20f, EstateTop + 0.60f, -14.52f), new Vector3(0.09f, 0.10f, 0.09f));
            b.Gear.Box(new Vector3(0.20f, EstateTop + 0.02f, -14.52f), new Vector3(0.30f, 0.04f, 0.30f));
            b.Paper.Box(new Vector3(0.31f, EstateTop + 0.34f, -14.52f), new Vector3(0.01f, 0.16f, 0.13f));

            // 配電盤と量水器の扉。階段の口の真上へ寄せて、戸口の列から外す
            b.Gear.Box(new Vector3(-0.70f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.52f, 0.72f, 0.16f));
            b.Shade.Box(new Vector3(-0.70f, EstateTop + 1.42f, EstateFace + 0.18f), new Vector3(0.42f, 0.60f, 0.02f));
            b.Bright.Box(new Vector3(-0.70f, EstateTop + 1.72f, EstateFace + 0.19f), new Vector3(0.20f, 0.09f, 0.02f));
            b.Gear.Box(new Vector3(-0.70f, EstateTop + 0.36f, EstateFace + 0.11f), new Vector3(0.46f, 0.52f, 0.20f));

            // 非常灯。緑は場面 3 の端末と同じ色で、この場面でここにしか無い。
            // 階段の口の真上が持ち場で、戸口の脇に点いていると何の灯りか読めない
            b.Gear.Box(new Vector3(-0.35f, WalkRoof - 0.30f, EstateFace + 0.08f), new Vector3(0.34f, 0.20f, 0.10f));
            b.Green.Box(new Vector3(-0.35f, WalkRoof - 0.30f, EstateFace + 0.14f), new Vector3(0.27f, 0.14f, 0.03f));

            // 天井の点検口。四本の細い棒で四角を描くだけ。
            // 天井が一枚の板のままだと、頭上に何も無い場所になる
            EstateHatch(b.Gear, 2.10f, WalkRoof - 0.015f, -14.25f, 0.56f);
            EstateHatch(b.Gear, 6.00f, WalkRoof - 0.015f, -14.25f, 0.56f);
            // 床の点検口。等間隔で三つ並ぶと、床の繰り返しがもう一段増える。
            // 手すり寄りに置くのは、面側は室外機と傘立てで埋まっているため
            for (var c = 0; c < 3; c++)
                EstateHatch(b.Gear, 1.0f + c * 2.6f, EstateTop + 0.012f, -13.72f, 0.44f);
        }

        /// <summary>点検口の枠。四辺を細い棒で囲うだけ</summary>
        static void EstateHatch(Bank b, float x, float y, float z, float side)
        {
            var half = side * 0.5f;
            b.Box(new Vector3(x, y, z - half), new Vector3(side, 0.03f, 0.04f));
            b.Box(new Vector3(x, y, z + half), new Vector3(side, 0.03f, 0.04f));
            b.Box(new Vector3(x - half, y, z), new Vector3(0.04f, 0.03f, side));
            b.Box(new Vector3(x + half, y, z), new Vector3(0.04f, 0.03f, side));
        }

        // ---- 暮らしの気配 ------------------------------------------------------

        /// <summary>
        /// 最上階の各戸の前に出ている物。傘立て・植木鉢・ダンボール・新聞。
        ///
        /// **戸口の前は空ける。** 戸の真ん中から左右 0.6 m、廊下の手前半分（面から 0.7 m）には
        /// 何も置かない。記憶 0 の母子は戸口 A の前、記憶 8 の主は戸口 B の前を通るので、
        /// そこへ物を置くと足元が埋まるうえ、戸口の列そのものが読めなくなる。
        /// 置く先は戸と戸のあいだか、手すり際のどちらかにまとめる
        /// </summary>
        static void EstateLives(EstateBanks b)
        {
            // 傘立てと傘。戸口 A と B のあいだ
            b.Gear.Box(new Vector3(2.42f, EstateTop + 0.24f, EstateFace + 0.20f), new Vector3(0.28f, 0.48f, 0.26f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(2.36f + i * 0.06f, EstateTop + 0.58f, EstateFace + 0.20f + i * 0.02f),
                    new Vector3(0.05f, 0.76f, 0.05f));

            // 植木鉢。**手すり際へ三つ、同じ鉢を等間隔で並べる。** 高さも間隔もばらばらに
            // 散らしていた版では、置き忘れた物が転がっているようにしか見えなかった。
            // 揃って並ぶと、そこだけ人が手を掛けている場所になる。草の背だけ変える
            var leaf = new[] { 0.34f, 0.22f, 0.40f };
            for (var i = 0; i < leaf.Length; i++)
            {
                var x = 2.52f + i * 0.32f;
                b.Red.Box(new Vector3(x, EstateTop + 0.12f, WalkFront - 0.26f), new Vector3(0.24f, 0.24f, 0.24f));
                b.Soft.Box(new Vector3(x, EstateTop + 0.24f + leaf[i] * 0.5f, WalkFront - 0.26f),
                    new Vector3(0.28f, leaf[i], 0.26f));
            }

            // ダンボールの山。戸口 B と C のあいだ。潰した板を一枚、山へ立て掛ける
            for (var i = 0; i < 3; i++)
                b.Set.Box(new Vector3(5.20f - i * 0.04f, EstateTop + 0.17f + i * 0.33f, EstateFace + 0.32f),
                    new Vector3(0.50f, 0.32f, 0.42f));
            b.Set.Box(new Vector3(5.18f, EstateTop + 0.46f, EstateFace + 0.62f),
                new Vector3(0.62f, 0.92f, 0.04f), Quaternion.Euler(-12f, 0f, 0f));

            // 新聞受けに差さったままの新聞。ここだけ白く、戸口の位置をもう一度拾わせる
            b.Paper.Box(new Vector3(DoorA - 0.75f, EstateTop + 1.40f, EstateFace + 0.14f),
                new Vector3(0.20f, 0.26f, 0.05f), Quaternion.Euler(0f, 0f, -14f));
            b.Paper.Box(new Vector3(DoorC - 0.75f, EstateTop + 1.39f, EstateFace + 0.13f),
                new Vector3(0.20f, 0.24f, 0.05f), Quaternion.Euler(0f, 0f, 9f));
        }

        /// <summary>
        /// 最上階の廊下に出ている大きな物。物干し竿と洗濯物、室外機、洗濯機、裸電球。
        ///
        /// 戸口と手すりだけの廊下は事務所の通路に見える。人が住んでいる証しをここで置く。
        /// 床に置く物は、開いた戸の前を避けて戸と戸のあいだへ寄せる
        /// </summary>
        static void EstateGear(EstateBanks b)
        {
            // 物干し竿。戸口 C の前に寄せて、廊下を +x へ見通したときの奥行きに掛ける
            b.Gear.Box(new Vector3(6.05f, EstateTop + 1.90f, WalkFront - 0.11f), new Vector3(2.5f, 0.045f, 0.045f));
            for (var i = 0; i < 4; i++)
            {
                var x = 5.0f + i * 0.6f;
                if (i == 2) b.Soft.Box(new Vector3(x, EstateTop + 1.62f, WalkFront - 0.11f), new Vector3(0.34f, 0.5f, 0.02f));
                else b.Linen.Box(new Vector3(x, EstateTop + 1.55f, WalkFront - 0.11f), new Vector3(0.46f, 0.66f, 0.02f));
            }

            // 室外機。羽根の線を三本入れると、ただの箱と見分けが付く。
            // 戸口 B の前を空けるぶん、戸口 A と B の真ん中へ寄せる
            b.Gear.Box(new Vector3(3.10f, EstateTop + 0.30f, EstateFace + 0.25f), new Vector3(0.80f, 0.58f, 0.36f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(3.10f, EstateTop + 0.18f + i * 0.12f, EstateFace + 0.44f),
                    new Vector3(0.62f, 0.05f, 0.02f));

            // 洗濯機
            b.Linen.Box(new Vector3(5.8f, EstateTop + 0.43f, EstateFace + 0.34f), new Vector3(0.60f, 0.86f, 0.56f));
            b.Shade.Box(new Vector3(5.8f, EstateTop + 0.87f, EstateFace + 0.34f), new Vector3(0.44f, 0.03f, 0.40f));
            b.Bright.Box(new Vector3(5.8f, EstateTop + 0.80f, EstateFace + 0.63f), new Vector3(0.36f, 0.06f, 0.02f));

            // 裸電球。コードは細く暗く、球だけ光らせる
            for (var i = 0; i < 3; i++)
            {
                var x = 0.9f + i * 3f;
                b.Gear.Box(new Vector3(x, EstateTop + 2.18f, EstateWalk), new Vector3(0.025f, 0.24f, 0.025f));
                b.Lit.Box(new Vector3(x, EstateTop + 1.98f, EstateWalk), new Vector3(0.12f, 0.16f, 0.12f));
            }
        }
    }
}
