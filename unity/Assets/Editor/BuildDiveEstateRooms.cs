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
        // ---- 右の家 ------------------------------------------------------------

        /// <summary>
        /// 右の家（記憶 8・15）。玄関と居間。
        ///
        /// 座って見る絵なので、目の高さ 1.15 m から卓とテレビが同時に入るように寄せる
        /// </summary>
        static void EstateRoom(EstateBanks b)
        {
            EstateGenkan(b);

            // 床板。廊下と同じ土間の色では、戸を跨いで中へ入ったことが伝わらない。
            // 三和土の抜けたところを避けて二枚に分ける
            b.Board.FaceY(EstateTop, RoomX0, HallDoma, RoomBack, HallSill, 1);
            b.Board.FaceY(EstateTop, HallDoma, RoomX1, RoomBack, EstateFace, 1);

            b.Soft.Box(new Vector3(4.9f, EstateTop + 0.22f, -16.6f), new Vector3(1.9f, 0.44f, 0.72f));
            b.Soft.Box(new Vector3(4.9f, EstateTop + 0.52f, -16.28f), new Vector3(1.9f, 0.6f, 0.14f));

            b.Set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            b.Set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));

            // 卓。天板と脚
            b.Set.Box(new Vector3(4.95f, EstateTop + 0.37f, -17.62f), new Vector3(1.10f, 0.06f, 0.66f));
            for (var i = 0; i < 4; i++)
                b.Set.Box(new Vector3(4.95f + ((i & 1) == 0 ? -0.48f : 0.48f), EstateTop + 0.17f,
                    -17.62f + ((i & 2) == 0 ? -0.26f : 0.26f)), new Vector3(0.07f, 0.34f, 0.07f));

            // 座布団
            b.Soft.Box(new Vector3(4.05f, EstateTop + 0.05f, -17.62f), new Vector3(0.56f, 0.10f, 0.56f));
            b.Soft.Box(new Vector3(5.88f, EstateTop + 0.05f, -17.58f), new Vector3(0.56f, 0.10f, 0.56f));

            // カーテン。襞は板の前後をずらして出す。テレビの前だけ開けておく。
            // **廊下の洗濯物と同じ明るい布では駄目。** 戸口の正面から抜けを覗いたとき、
            // 奥のカーテンがいちばん明るい面になって、居間が真っ先に目へ入る
            b.Gear.Box(new Vector3(4.95f, EstateTop + 2.12f, RoomBack + 0.12f), new Vector3(3.9f, 0.05f, 0.05f));
            for (var i = 0; i < 5; i++)
                b.Soft.Box(new Vector3(3.15f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));
            for (var i = 0; i < 3; i++)
                b.Soft.Box(new Vector3(6.00f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));

            // 箪笥。**抜けを覗いた線の先へ置く。** 玄関から見通せる帯をここで塞ぐので、
            // 戸口の正面から見えるのは玄関と、その奥の家具の側面までになる
            b.Set.Box(new Vector3(3.30f, EstateTop + 0.75f, -17.30f), new Vector3(0.56f, 1.50f, 0.90f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(3.59f, EstateTop + 0.28f + i * 0.34f, -17.30f),
                    new Vector3(0.02f, 0.05f, 0.76f));
            b.Gear.Box(new Vector3(3.30f, EstateTop + 1.62f, -17.44f), new Vector3(0.20f, 0.24f, 0.16f));
            b.Soft.Box(new Vector3(3.30f, EstateTop + 1.60f, -17.10f), new Vector3(0.26f, 0.20f, 0.22f));

            // 幅木。壁の裾に線が一本通らないと、居間の壁が塗っただけの面に見える
            EstateSkirt(b, RoomX0 + 0.02f, RoomBack, HallSill, true);
            EstateSkirt(b, RoomX1 - 0.02f, RoomBack, EstateFace, true);
            EstateSkirt(b, RoomBack + 0.02f, RoomX0, RoomX1, false);
            EstateSkirt(b, HallWall - HallSkin - 0.02f, HallGap1, RoomX1, false);
            EstateSkirt(b, HallWall - HallSkin - 0.02f, RoomX0, HallGap0, false);

            // 蛍光灯の笠。光る面そのものは Estate が板で置く
            b.Shade.Box(new Vector3(5.0f, RoomRoof - 0.05f, -17.3f), new Vector3(1.3f, 0.1f, 0.32f));
        }

        /// <summary>
        /// 右の家の玄関。三和土・上がり框・下駄箱・短い廊下・居間への抜け。
        ///
        /// **抜けを戸口の正面から西へ外す。** 廊下に立って戸口の正面から覗いたとき、
        /// 目に入るのは三和土と框と壁で、居間は抜けの脇にしか見えない。
        /// 記憶 15 のエレナは居間に座って北西を向くので、その線だけが抜けを通って
        /// 三和土と下駄箱まで届く。仕切りを東へ寄せると、今度はエレナから玄関が見えなくなる
        /// </summary>
        static void EstateGenkan(EstateBanks b)
        {
            // 三和土と、框と東側の蹴上げ
            b.Tile.FaceY(EstateTop - EstateSunk, RoomX0, HallDoma, HallSill, EstateFace, 1);
            b.Tile.FaceZ(HallSill, RoomX0, HallDoma, EstateTop - EstateSunk, EstateTop, 1);
            b.Tile.FaceX(HallDoma, HallSill, EstateFace, EstateTop - EstateSunk, EstateTop, -1);
            // 上がり框。当たりは蹴上げの面が持つので、この縁には入れない
            b.Set.Box(new Vector3((RoomX0 + HallDoma) * 0.5f, EstateTop - 0.09f, HallSill + 0.035f),
                new Vector3(HallDoma - RoomX0, 0.18f, 0.07f));

            // 居間との仕切り。抜けの上は垂れ壁で閉じる
            var gap = new List<Vector4>
            {
                new Vector4(HallGap0, HallGap1, EstateTop, EstateTop + HallHead),
            };
            b.Wall.FaceZHoles(HallWall + HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, 1, gap);
            b.Wall.FaceZHoles(HallWall - HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, -1, gap);
            b.Wall.FaceX(HallGap0, HallWall - HallSkin, HallWall + HallSkin, EstateTop, EstateTop + HallHead, 1);
            b.Wall.FaceX(HallGap1, HallWall - HallSkin, HallWall + HallSkin, EstateTop, EstateTop + HallHead, -1);
            b.Wall.FaceY(EstateTop + HallHead, HallGap0, HallGap1, HallWall - HallSkin, HallWall + HallSkin, -1);
            // 玄関の東の壁。この奥は物入れで、中は作らない
            b.Wall.Box(new Vector3(HallEast, (EstateTop + RoomRoof) * 0.5f, (EstateFace + HallWall) * 0.5f),
                new Vector3(0.12f, RoomRoof - EstateTop, EstateFace - HallWall));
            // 抜けの縁。木の枠が回ると、壁に空いた穴ではなく建具の抜けに見える
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(i == 0 ? HallGap0 : HallGap1, EstateTop + HallHead * 0.5f, HallWall),
                    new Vector3(0.05f, HallHead, HallSkin * 2f + 0.03f));
            b.Set.Box(new Vector3((HallGap0 + HallGap1) * 0.5f, EstateTop + HallHead, HallWall),
                new Vector3(HallGap1 - HallGap0 + 0.1f, 0.05f, HallSkin * 2f + 0.03f));

            // 下駄箱。戸口の正面から見える東の箱と、エレナから見える西の箱
            b.Set.Box(new Vector3(4.92f, EstateTop + 0.47f, -15.17f), new Vector3(0.38f, 0.94f, 0.66f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(4.73f, EstateTop + 0.25f + i * 0.44f, -15.17f),
                    new Vector3(0.02f, 0.36f, 0.58f));
            b.Bright.Box(new Vector3(4.92f, EstateTop + 0.97f, -15.05f), new Vector3(0.20f, 0.03f, 0.14f));
            b.Gear.Box(new Vector3(4.92f, EstateTop + 1.06f, -15.32f), new Vector3(0.16f, 0.22f, 0.16f));
            b.Soft.Box(new Vector3(4.92f, EstateTop + 1.28f, -15.32f), new Vector3(0.26f, 0.28f, 0.24f));
            // **目の高さの物を仕切りの面へ集める。** 廊下に立って戸口の正面から覗くと、
            // 視界の下半分は戸枠に切られて三和土まで届かない。
            // 玄関だと分かる物は、奥の壁の目の高さに掛かっていないと絵に入らない。
            // 姿見は西の下駄箱の上、上着は東の壁際
            b.Set.Box(new Vector3(3.16f, EstateTop + 1.42f, HallWall + HallSkin + 0.02f),
                new Vector3(0.50f, 0.74f, 0.04f));
            b.Bright.Box(new Vector3(3.16f, EstateTop + 1.42f, HallWall + HallSkin + 0.05f),
                new Vector3(0.42f, 0.66f, 0.02f));
            b.Gear.Box(new Vector3(4.87f, EstateTop + 1.80f, HallWall + HallSkin + 0.06f),
                new Vector3(0.40f, 0.04f, 0.04f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(4.80f + i * 0.14f, EstateTop + 1.74f, HallWall + HallSkin + 0.06f),
                    new Vector3(0.03f, 0.10f, 0.03f));
            b.Soft.Box(new Vector3(4.80f, EstateTop + 1.30f, HallWall + HallSkin + 0.09f),
                new Vector3(0.34f, 0.86f, 0.14f));
            b.Soft.Box(new Vector3(4.94f, EstateTop + 1.36f, HallWall + HallSkin + 0.08f),
                new Vector3(0.28f, 0.74f, 0.12f));
            // 玄関マット。上から覗いたとき、三和土と框の境がここで一度切れる
            b.Soft.Box(new Vector3(4.15f, EstateTop - EstateSunk + 0.015f, -15.08f),
                new Vector3(0.78f, 0.03f, 0.42f));

            b.Set.Box(new Vector3(3.16f, EstateTop + 0.45f, -15.69f), new Vector3(0.28f, 0.90f, 0.22f));
            b.Red.Box(new Vector3(3.16f, EstateTop + 0.98f, -15.69f), new Vector3(0.16f, 0.16f, 0.16f));
            b.Soft.Box(new Vector3(3.16f, EstateTop + 1.16f, -15.69f), new Vector3(0.26f, 0.24f, 0.22f));

            // 靴。**主と夫の通り道を外して置く。** 通り道に置くと、二人が靴を踏んで歩く
            EstateShoes(b, 3.18f, -15.38f, 8f);
            EstateShoes(b, 3.60f, -15.41f, -6f);
            EstateShoes(b, 4.52f, -15.44f, 14f);

            // 仕切りの居間側。**居間から見ると、ここは幅 4 m の塗っただけの面になる。**
            // 記憶 15 は座って玄関の方を向くので、視界の半分をこの壁が占める
            // 掛ける高さは座った目から 30 度以内に収める。壁の高いところへ掛けると、
            // 立って歩く絵には入っても、座って撮る記憶 15 の絵には一つも入らない
            b.Gear.Box(new Vector3(4.90f, EstateTop + 1.48f, HallWall - HallSkin - 0.03f),
                new Vector3(0.28f, 0.28f, 0.05f));
            b.Bright.Box(new Vector3(4.90f, EstateTop + 1.48f, HallWall - HallSkin - 0.06f),
                new Vector3(0.22f, 0.22f, 0.02f));
            b.Set.Box(new Vector3(5.30f, EstateTop + 1.30f, HallWall - HallSkin - 0.03f),
                new Vector3(0.44f, 0.34f, 0.04f));
            b.Shade.Box(new Vector3(5.30f, EstateTop + 1.30f, HallWall - HallSkin - 0.06f),
                new Vector3(0.34f, 0.24f, 0.02f));
            b.Paper.Box(new Vector3(5.86f, EstateTop + 1.52f, HallWall - HallSkin - 0.03f),
                new Vector3(0.34f, 0.46f, 0.02f));
            b.Bright.Box(new Vector3(4.68f, EstateTop + 1.15f, HallWall - HallSkin - 0.03f),
                new Vector3(0.10f, 0.14f, 0.02f));

            // 傘立て。三和土の隅
            b.Gear.Box(new Vector3(3.14f, EstateTop + 0.07f, -14.98f), new Vector3(0.24f, 0.44f, 0.24f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(3.10f + i * 0.08f, EstateTop + 0.42f, -14.98f + i * 0.03f),
                    new Vector3(0.05f, 0.74f, 0.05f));
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
        static void EstateDoorway(EstateBanks b, float x, bool open)
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

            if (open)
            {
                // **開いた戸はメーターの箱より手前へ置く。** 面に貼り付けると、
                // 戸の板の中からメーターの角が生えてくる
                EstateLeaf(b, x + 0.92f, 0.43f, EstateFace + 0.26f);
                b.Gear.Box(new Vector3(x + 1.26f, EstateTop + 1.0f, EstateFace + 0.31f), new Vector3(0.05f, 0.05f, 0.12f));
                // 丁番。戸が壁から浮いて見えないように、枠との間を三つで繋ぐ
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
