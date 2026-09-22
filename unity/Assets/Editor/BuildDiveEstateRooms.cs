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

        /// <summary>靴を一足。三和土の上に並ぶ小さな暗い塊が、土間を土間として読ませる</summary>
        static void EstateShoes(EstateBanks b, float x, float z, float turn)
        {
            var rot = Quaternion.Euler(0f, turn, 0f);
            for (var i = 0; i < 2; i++)
            {
                var at = new Vector3(x + (i == 0 ? -0.09f : 0.09f), EstateTop - EstateSunk + 0.05f, z);
                b.Shade.Box(at, new Vector3(0.11f, 0.10f, 0.26f), rot);
                b.Shade.Box(at + new Vector3(0f, 0.04f, -0.07f), new Vector3(0.11f, 0.08f, 0.10f), rot);
            }
        }

        /// <summary>
        /// 左の戸口の奥（記憶 0・1）。ここも玄関から始める。
        ///
        /// **母の立つところだけ床の高さのまま残す。** 記憶 0 は子どもの目から母の脚を見上げる
        /// 鍵打ちがあり、三和土をそこまで下げると母が 0.15 m 浮く。
        /// 玄関のすのこを敷いて、母はその上に立っていることにする
        /// </summary>
        static void EstatePorch(EstateBanks b)
        {
            b.Tile.FaceY(EstateTop - EstateSunk, PorchX0, PorchX1, PorchSill, EstateFace, 1);
            b.Tile.FaceZ(PorchSill, PorchX0, PorchX1, EstateTop - EstateSunk, EstateTop, 1);
            b.Wall.FaceY(EstateTop, PorchX0, PorchX1, PorchBack, PorchSill, 1);
            b.Wall.FaceX(PorchX0, PorchBack, EstateFace, EstateTop - EstateSunk, EstateTop + 2.2f, 1);
            b.Wall.FaceX(PorchX1, PorchBack, EstateFace, EstateTop - EstateSunk, EstateTop + 2.2f, -1);
            b.Wall.FaceZ(PorchBack, PorchX0, PorchX1, EstateTop, EstateTop + 2.2f, 1);
            b.Set.Box(new Vector3(DoorA, EstateTop - 0.09f, PorchSill + 0.035f),
                new Vector3(PorchX1 - PorchX0, 0.18f, 0.07f));

            // すのこ。板の隙間を暗い線で入れておかないと、ただの踏み台に見える
            b.Set.Box(new Vector3(1.70f, EstateTop - 0.075f, -15.12f), new Vector3(0.80f, 0.15f, 0.50f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(1.70f, EstateTop + 0.002f, -15.31f + i * 0.126f),
                    new Vector3(0.80f, 0.02f, 0.02f));

            // 下駄箱。戸口の抜けに掛からないよう西の壁へ寄せる
            b.Set.Box(new Vector3(1.00f, EstateTop + 0.325f, -15.18f), new Vector3(0.28f, 0.95f, 0.72f));
            b.Gear.Box(new Vector3(1.00f, EstateTop + 0.86f, -15.18f), new Vector3(0.18f, 0.14f, 0.20f));
            EstateShoes(b, 1.28f, -15.42f, -10f);
            EstateShoes(b, 2.10f, -15.38f, 16f);

            // 奥の物。光る面の手前へ影になる物を置く。
            // 何も無いと、記憶 1 が戸口へ向いたとき、視界の半分が真っ白な板になる
            b.Shade.Box(new Vector3(DoorA, EstateTop + 0.26f, -16.18f), new Vector3(1.20f, 0.52f, 0.30f));
            b.Shade.Box(new Vector3(DoorA, EstateTop + 1.86f, -16.24f), new Vector3(1.34f, 0.07f, 0.22f));
            b.Soft.Box(new Vector3(DoorA - 0.36f, EstateTop + 1.36f, -16.24f), new Vector3(0.36f, 0.92f, 0.12f));
            b.Soft.Box(new Vector3(DoorA + 0.40f, EstateTop + 1.44f, -16.24f), new Vector3(0.30f, 0.76f, 0.12f));
        }

        // ---- 戸口 --------------------------------------------------------------

        /// <summary>
        /// 戸口ひとつ。枠・番号板・郵便受け・メーター・表札・呼び鈴まで。
        ///
        /// 壁に開けた穴だけでは戸口に見えない。枠と、脇に並ぶ小物が付いて初めて
        /// 「人が出入りする戸」として読める。三つとも同じ組み合わせにして、並びを強める。
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
            // 表札と呼び鈴。番号板の下へ縦に重ねる。
            // 戸の右は開いた戸が倒れてくる側なので、住戸の札はすべて左へ寄せる
            b.Paper.Box(new Vector3(x - 0.75f, EstateTop + 1.64f, EstateFace + 0.05f),
                new Vector3(0.22f, 0.10f, 0.02f), Quaternion.Euler(0f, 0f, -3f));
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.46f, EstateFace + 0.06f), new Vector3(0.09f, 0.13f, 0.03f));
            b.Bright.Box(new Vector3(x - 0.75f, EstateTop + 1.47f, EstateFace + 0.08f), new Vector3(0.04f, 0.04f, 0.02f));
            // 郵便受け
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.28f, EstateFace + 0.06f), new Vector3(0.32f, 0.24f, 0.12f));
            b.Shade.Box(new Vector3(x - 0.75f, EstateTop + 1.34f, EstateFace + 0.125f), new Vector3(0.22f, 0.04f, 0.02f));
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
        /// 置く先は階段の口の側へ寄せる。記憶 0 の母子も記憶 1 のハンナも戸口の前で撮るので、
        /// そちらへ置くと人の足元が物で埋まる
        /// </summary>
        static void EstateCommon(EstateBanks b)
        {
            // 掲示板。硝子の入った枠に紙が何枚か。壁でいちばん明るい四角になる
            b.Gear.Box(new Vector3(StairWest + 0.03f, EstateTop + 1.38f, -14.05f), new Vector3(0.05f, 0.74f, 0.92f));
            b.Shade.Box(new Vector3(StairWest + 0.055f, EstateTop + 1.38f, -14.05f), new Vector3(0.02f, 0.64f, 0.82f));
            for (var i = 0; i < 4; i++)
                b.Paper.Box(new Vector3(StairWest + 0.065f, EstateTop + 1.24f + (i % 2) * 0.30f,
                        -13.80f - (i / 2) * 0.42f), new Vector3(0.01f, 0.24f, 0.32f));

            // 消火器。赤い縦長がひとつあるだけで、廊下が共用部だと分かる。
            // 置く先は井戸の東の隅。上がり切ったところは西の一本の出口なので、そちらは空けておく
            b.Red.Box(new Vector3(1.05f, EstateTop + 0.30f, -14.52f), new Vector3(0.19f, 0.54f, 0.19f));
            b.Gear.Box(new Vector3(1.05f, EstateTop + 0.60f, -14.52f), new Vector3(0.09f, 0.10f, 0.09f));
            b.Gear.Box(new Vector3(1.05f, EstateTop + 0.02f, -14.52f), new Vector3(0.30f, 0.04f, 0.30f));
            b.Paper.Box(new Vector3(1.16f, EstateTop + 0.34f, -14.52f), new Vector3(0.01f, 0.16f, 0.13f));

            // 配電盤と量水器の扉。面のいちばん端に寄せて、戸口の列を邪魔しない
            b.Gear.Box(new Vector3(0.38f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.52f, 0.72f, 0.16f));
            b.Shade.Box(new Vector3(0.38f, EstateTop + 1.42f, EstateFace + 0.18f), new Vector3(0.42f, 0.60f, 0.02f));
            b.Bright.Box(new Vector3(0.38f, EstateTop + 1.72f, EstateFace + 0.19f), new Vector3(0.20f, 0.09f, 0.02f));
            b.Gear.Box(new Vector3(0.38f, EstateTop + 0.36f, EstateFace + 0.11f), new Vector3(0.46f, 0.52f, 0.20f));

            // 非常灯。緑は場面 3 の端末と同じ色で、この場面でここにしか無い
            b.Gear.Box(new Vector3(0.90f, WalkRoof - 0.30f, EstateFace + 0.08f), new Vector3(0.34f, 0.20f, 0.10f));
            b.Green.Box(new Vector3(0.90f, WalkRoof - 0.30f, EstateFace + 0.14f), new Vector3(0.27f, 0.14f, 0.03f));

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
        /// **人の立ち位置を避けて戸と戸のあいだへ寄せる。** 記憶 0 の母子は戸口 A の前、
        /// 記憶 8 の主は戸口 B の前を通るので、そこへ物を置くと足元が埋まる
        /// </summary>
        static void EstateLives(EstateBanks b)
        {
            // 傘立てと傘。戸口 A と B のあいだ
            b.Gear.Box(new Vector3(2.60f, EstateTop + 0.24f, EstateFace + 0.20f), new Vector3(0.28f, 0.48f, 0.26f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(2.54f + i * 0.06f, EstateTop + 0.58f, EstateFace + 0.20f + i * 0.02f),
                    new Vector3(0.05f, 0.76f, 0.05f));

            // 植木鉢。高さを変えて三つ。同じ高さで並べると棚に見える
            var pots = new[] { 2.06f, 2.32f, 0.62f };
            var tall = new[] { 0.26f, 0.19f, 0.30f };
            for (var i = 0; i < pots.Length; i++)
            {
                b.Red.Box(new Vector3(pots[i], EstateTop + tall[i] * 0.5f, EstateFace + 0.22f),
                    new Vector3(0.24f, tall[i], 0.24f));
                b.Soft.Box(new Vector3(pots[i], EstateTop + tall[i] + 0.16f, EstateFace + 0.22f),
                    new Vector3(0.30f, 0.34f, 0.28f));
            }

            // ダンボールの山。戸口 C の脇。潰した板を一枚立て掛ける
            for (var i = 0; i < 3; i++)
                b.Set.Box(new Vector3(7.50f - i * 0.04f, EstateTop + 0.17f + i * 0.33f, EstateFace + 0.32f),
                    new Vector3(0.50f, 0.32f, 0.42f));
            b.Set.Box(new Vector3(7.10f, EstateTop + 0.46f, EstateFace + 0.30f),
                new Vector3(0.62f, 0.92f, 0.04f), Quaternion.Euler(12f, 0f, 0f));

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

            // 室外機。羽根の線を三本入れると、ただの箱と見分けが付く
            b.Gear.Box(new Vector3(3.3f, EstateTop + 0.30f, EstateFace + 0.25f), new Vector3(0.80f, 0.58f, 0.36f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(3.3f, EstateTop + 0.18f + i * 0.12f, EstateFace + 0.44f),
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
