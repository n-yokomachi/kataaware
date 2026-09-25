using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅の中の層。道路の向こうから、中心（<see cref="EstateFarCentre"/>）から 60 m まで。
    /// その先は書き割り（<c>BuildDiveEstateFar.cs</c>）。
    ///
    /// 一目で分かる形を三つ置く（設計書 9.1 節「敷地と遠景の中身（ロンドン）」）。
    /// <list type="table">
    /// <item><term>隣の棟</term><description>道路の向かいに、同じ型のデッキアクセスのメゾネット棟を向かい合わせに。
    /// 三階の高さを走るコンクリートのデッキの腰壁、その奥の色の違う玄関と台所の窓、デッキの上に張り出す四階。
    /// 自分たちの棟と同じ形が向かいに見えると、ここが一つの団地だと読める</description></item>
    /// <item><term>塔状の高層棟</term><description>北西に二十階。廊下から見上げても頭が画面に収まらない</description></item>
    /// <item><term>煙突の並ぶ煉瓦の長屋</term><description>北東の角から横丁に沿って、ヴィクトリア朝の二〜三階建ての長屋。
    /// 黄色い煉瓦、白い窓枠、一階の張り出し窓、スレートの屋根、戸境ごとに素焼きの煙突の束。
    /// 屋根の上に等間隔に並ぶ煙突が、朝日の空を背にいちばんロンドンらしい輪郭になる</description></item>
    /// </list>
    ///
    /// **中の層の地の色は、灯りを受けたうえで少し自分で光らせる**（<see cref="MidPaint"/>）。
    /// 朝日は北北東の低い所から来るので、廊下から見える面（南を向く面）はどれも日陰になる。
    /// 灯りを受けるだけの面は環境光しか拾わず黒く沈み、灯りを受けない面は日の当たる妻も同じ明るさになる。
    /// 両方を足すと、日陰の面は煉瓦の色が読める暗さで止まり、日の当たる妻と屋根だけが明るくなる
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 隣の棟の寸法 --------------------------------------------------------

        /// <summary>隣の棟のデッキの縁。歩道の奥から 0.5 m</summary>
        const float NextDeck = BlockFace + 0.5f;                                           // 10.0
        /// <summary>デッキの奥の壁（下のメゾネットの表の壁）</summary>
        const float NextFace = NextDeck + 2.0f;                                            // 12.0
        /// <summary>隣の棟の裏の面。奥行きは自分たちの棟と同じ 8.6 m</summary>
        const float NextBack = NextFace + 8.6f;                                            // 20.6
        /// <summary>住戸の間口。自分たちの棟と同じ</summary>
        const float NextBay = 5.4f;
        /// <summary>住戸の数。東の妻が横丁の角に来る</summary>
        const int NextBays = 5;
        /// <summary>隣の棟の東の妻</summary>
        const float NextEast = 11.5f;
        /// <summary>隣の棟の西の妻。西には階段の塔が付く</summary>
        const float NextWest = NextEast - NextBay * NextBays;                              // -15.5
        /// <summary>四階の床。デッキの天井</summary>
        const float NextUpper = EstateTop + Floor;                                         // 8.4
        /// <summary>陸屋根。四階建て</summary>
        const float NextRoof = EstateTop + Floor * 2f;                                     // 11.2

        // ---- 横丁と長屋の寸法 ----------------------------------------------------

        /// <summary>北へ入る横丁の車道の西と東</summary>
        const float SideWest = 14.0f;
        const float SideEast = 18.2f;
        /// <summary>横丁の歩道の外の縁</summary>
        const float SidePaveWest = SideWest - 1.2f;
        const float SidePaveEast = SideEast + 1.4f;
        /// <summary>横丁の北の端。中心から 60 m に収まる所まで</summary>
        const float SideNorth = 43f;
        /// <summary>長屋の一軒の間口</summary>
        const float HouseWide = 5.0f;
        /// <summary>敷地の東を南へ下る通りの車道の西と東、東の歩道の外の縁</summary>
        const float EastWest = 25.4f;
        const float EastEast = 28.6f;
        const float EastPave = EastEast + 1.2f;

        // ---- 組む ----------------------------------------------------------------

        /// <summary>中の層をまとめて組む。敷地と同じ入れ物に溜め、出すのは呼ぶ側</summary>
        static void EstateMid(YardBanks y)
        {
            MidStreets(y);
            MidNext(y);
            MidTower(y, new Vector3(-24f, 0f, 24f), 16f, 20);
            MidBlockWest(y);
            // 北東の角。大通りに表を向けた二階建ての長屋。西の端から東へ五軒
            MidTerrace(y, new Vector3(SidePaveEast + 0.2f, 0f, BlockFace + 1.6f), 180f, 5, 2, 3);
            // 横丁の東側。西に表を向けた三階建て。北の端から南へ三軒
            MidTerrace(y, new Vector3(SidePaveEast + 1.6f, 0f, 39.0f), 270f, 3, 3, 5);
            // 敷地の東の通りの向こう。西に表を向けた二階建て。庭から東を見ると正面に来る
            MidTerrace(y, new Vector3(EastPave + 1.6f, 0f, YardEdge - 1.1f), 270f, 4, 2, 9);
            MidTrees(y);
        }

        /// <summary>
        /// 中の層の地の色。灯りを受ける Lit に、地の色の <paramref name="fill"/> 倍を自分で光らせる。
        /// 日陰の面が黒く沈まないための底上げ（このファイルの冒頭を見る）
        /// </summary>
        static Material MidPaint(string name, Color col, float fill, float smooth)
        {
            var m = EstatePaint(name, col, smooth);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", new Color(col.r * fill, col.g * fill, col.b * fill, 1f));
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 面を一枚。四隅は縁を回る順に渡し、<paramref name="outward"/> の側を表にする。
        /// 屋根の斜面のように、軸に揃わない面に使う
        /// </summary>
        static void MidQuad(Bank b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f) b.Quad(p0, p1, p2, p3);
            else b.Quad(p3, p2, p1, p0);
        }

        // ---- 道路 ----------------------------------------------------------------

        /// <summary>
        /// 大通りの続きと、北へ入る横丁と、敷地の東の通り。敷地の地面の外は遠い地面（EstateLand）の上に重ねる。
        /// 横丁の入口には止まれの白い破線、縁石沿いは入口から先も黄色い二重線、その先は住人の駐車の白線と車
        /// </summary>
        static void MidStreets(YardBanks y)
        {
            // 大通りの続き。西と東へ、中心から 55 m ほどまで。東の手前の歩道は東の通りの口で切る
            foreach (var span in new[] { new Vector2(-45f, -14f), new Vector2(25f, 50f) })
            {
                var near0 = span.x > 0f ? EastEast + 0.2f : span.x;
                y.Tarmac.FaceY(0.012f, span.x, span.y, RoadNear, RoadFar, 1);
                y.Kerb.FaceY(0.012f, near0, span.y, YardEdge, RoadNear - 0.2f, 1);
                y.Kerb.FaceY(0.012f, span.x, span.y, RoadFar + 0.2f, BlockFace, 1);
                y.Kerb.Box(new Vector3((near0 + span.y) * 0.5f, 0.06f, RoadNear - 0.1f), new Vector3(span.y - near0, 0.12f, 0.2f));
                y.Kerb.Box(new Vector3((span.x + span.y) * 0.5f, 0.06f, RoadFar + 0.1f), new Vector3(span.y - span.x, 0.12f, 0.2f));
                for (var x = span.x + 0.5f; x < span.y; x += 9f)
                    y.Line.FaceY(0.02f, x, Mathf.Min(x + 4f, span.y), RoadMid - 0.05f, RoadMid + 0.05f, 1);
                YardDoubleYellow(y, span.x, span.y, RoadNear + 0.25f);
                YardDoubleYellow(y, span.x, span.y, RoadFar - 0.25f - 0.28f);
            }

            // 横丁。大通りの向こうの歩道を切って北へ
            y.Tarmac.FaceY(0.013f, SideWest, SideEast, RoadFar, SideNorth, 1);
            y.Kerb.FaceY(0.012f, SidePaveWest, SideWest - 0.2f, BlockFace, SideNorth, 1);
            y.Kerb.FaceY(0.012f, SideEast + 0.2f, SidePaveEast, RoadFar + 0.2f, SideNorth, 1);
            y.Kerb.Box(new Vector3(SideWest - 0.1f, 0.06f, (RoadFar + SideNorth) * 0.5f + 1f), new Vector3(0.2f, 0.12f, SideNorth - RoadFar - 2f));
            y.Kerb.Box(new Vector3(SideEast + 0.1f, 0.06f, (RoadFar + SideNorth) * 0.5f + 1f), new Vector3(0.2f, 0.12f, SideNorth - RoadFar - 2f));
            // 入口の止まれ（譲れ）の線。二本の白い破線と、その手前の三角
            for (var x = SideWest + 0.1f; x < SideEast - 0.2f; x += 0.9f)
            {
                y.Line.FaceY(0.022f, x, x + 0.6f, RoadFar + 0.35f, RoadFar + 0.5f, 1);
                y.Line.FaceY(0.022f, x, x + 0.6f, RoadFar + 0.75f, RoadFar + 0.9f, 1);
            }
            MidQuad(y.Line, new Vector3(SideWest + 1.6f, 0.022f, RoadFar + 3.2f), new Vector3(SideWest + 2.6f, 0.022f, RoadFar + 3.2f),
                new Vector3(SideWest + 2.1f, 0.022f, RoadFar + 1.6f), new Vector3(SideWest + 2.1f, 0.022f, RoadFar + 1.6f), Vector3.up);
            // 縁石沿いの黄色い二重線。入口から 12 m
            YardDoubleYellowZ(y, SideWest + 0.25f, RoadFar + 1f, RoadFar + 12f);
            YardDoubleYellowZ(y, SideEast - 0.25f - 0.28f, RoadFar + 1f, RoadFar + 12f);
            // その先は住人の駐車。東の縁石沿いに白い破線の枠と、止めた車を二台
            y.Line.FaceY(0.022f, SideEast - 2.2f, SideEast - 2.1f, RoadFar + 13f, SideNorth - 2f, 1);
            for (var z = RoadFar + 13f; z < SideNorth - 2f; z += 5.5f)
                y.Line.FaceY(0.022f, SideEast - 2.2f, SideEast, z, z + 0.1f, 1);
            YardCar(y, y.Line, SideEast - 1.1f, RoadFar + 16f);
            YardCar(y, y.Red, SideEast - 1.1f, RoadFar + 27f);
            // 横丁の街灯。東の歩道に二本、腕は車道へ
            MidLamp(y, new Vector3(SideEast + 0.5f, 0f, 20f), Vector3.left);
            MidLamp(y, new Vector3(SideEast + 0.5f, 0f, 34f), Vector3.left);

            // 敷地の東の通り。大通りから南へ。東の長屋の前を通る
            y.Tarmac.FaceY(0.012f, EastWest, EastEast, -24f, RoadNear, 1);
            y.Kerb.FaceY(0.012f, EastEast + 0.2f, EastPave, -24f, RoadNear - 0.2f, 1);
            y.Kerb.Box(new Vector3(EastWest - 0.1f, 0.06f, -11.5f), new Vector3(0.2f, 0.12f, 24f));
            y.Kerb.Box(new Vector3(EastEast + 0.1f, 0.06f, -11.5f), new Vector3(0.2f, 0.12f, 24f));
            YardDoubleYellowZ(y, EastWest + 0.25f, -24f, YardEdge);
            YardDoubleYellowZ(y, EastEast - 0.25f - 0.28f, -24f, YardEdge);

            // 西の芝地。敷地の塀の外から西の棟まで。斜めに歩道を一本
            y.Grass.FaceY(0.01f, -21.2f, -13.8f, -24f, YardEdge, 1);
            y.Kerb.FaceY(0.015f, -18.0f, -16.8f, -24f, YardEdge, 1);
            // 高層棟の足元。芝と、入口へ向かう舗装
            y.Grass.FaceY(0.01f, -32f, -15f, BlockFace + 0.3f, 33.5f, 1);
            y.Kerb.FaceY(0.015f, -25f, -23f, BlockFace, 16f, 1);
        }

        /// <summary>黄色い二重線を、z に沿って一組。x0 は西の線の西の縁</summary>
        static void YardDoubleYellowZ(YardBanks y, float x0, float z0, float z1)
        {
            y.Yellow.FaceY(0.022f, x0, x0 + 0.08f, z0, z1, 1);
            y.Yellow.FaceY(0.022f, x0 + 0.20f, x0 + 0.28f, z0, z1, 1);
        }

        /// <summary>背の高い街灯を、腕の向き <paramref name="arm"/>（水平）を渡して立てる</summary>
        static void MidLamp(YardBanks y, Vector3 foot, Vector3 arm)
        {
            const float h = 8f;
            var rot = Quaternion.LookRotation(arm, Vector3.up);
            y.Pole.Box(foot + new Vector3(0f, 0.4f, 0f), new Vector3(0.24f, 0.8f, 0.24f));
            y.Pole.Box(foot + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.13f, h, 0.13f));
            y.Pole.Box(foot + Vector3.up * (h - 0.05f) + arm * 0.45f, new Vector3(0.07f, 0.07f, 0.9f), rot);
            y.Iron.Box(foot + Vector3.up * (h - 0.10f) + arm * 1.15f, new Vector3(0.30f, 0.11f, 0.75f), rot);
        }

        // ---- 隣の棟 --------------------------------------------------------------

        /// <summary>
        /// 隣の棟。自分たちの棟と同じ型のデッキアクセスのメゾネット棟を、道路を挟んで向かい合わせに建てる。
        ///
        /// 下の二層は地面から入る下のメゾネット（前庭の塀と、表の戸と窓）、三階の高さにデッキが走り、
        /// その奥に上のメゾネットの玄関と台所の窓、デッキの上に四階が張り出す。
        /// **デッキの腰壁の明るい帯**と、**その上の暗い奥まり**が横に通るのが、この型の顔。
        /// 戸は一戸ずつ色を変える。西の妻には階段の塔
        /// </summary>
        static void MidNext(YardBanks y)
        {
            const float deck = EstateTop;
            const float mid = (NextWest + NextEast) * 0.5f;
            const float span = NextEast - NextWest;

            // 塊。表の壁から裏まで、地面から屋根まで
            y.NextBrick.Box(new Vector3(mid, NextRoof * 0.5f, (NextFace + NextBack) * 0.5f), new Vector3(span, NextRoof, NextBack - NextFace));
            // 四階の張り出し。デッキの上を縁まで覆う
            y.NextBrick.Box(new Vector3(mid, (NextUpper + NextRoof) * 0.5f, (NextDeck + NextFace) * 0.5f), new Vector3(span, NextRoof - NextUpper, NextFace - NextDeck));
            // 張り出しの床の縁の帯
            y.NextCrete.Box(new Vector3(mid, NextUpper - 0.1f, NextDeck + 0.95f), new Vector3(span + 0.1f, 0.3f, 2.0f));
            // 屋上の立ち上がり
            y.NextCrete.Box(new Vector3(mid, NextRoof + 0.25f, NextDeck + 0.12f), new Vector3(span + 0.2f, 0.5f, 0.24f));
            y.NextCrete.Box(new Vector3(mid, NextRoof + 0.25f, NextBack - 0.12f), new Vector3(span + 0.2f, 0.5f, 0.24f));
            // デッキの床と腰壁。腰壁の上に鉄の手すり
            y.NextCrete.Box(new Vector3(mid, deck - 0.15f, (NextDeck + NextFace) * 0.5f), new Vector3(span, 0.3f, NextFace - NextDeck));
            y.NextCrete.Box(new Vector3(mid, deck + 0.5f, NextDeck + 0.1f), new Vector3(span, 1.0f, 0.2f));
            y.Iron.Box(new Vector3(mid, deck + 1.08f, NextDeck + 0.1f), new Vector3(span, 0.05f, 0.06f));
            for (var x = NextWest + 0.3f; x < NextEast; x += 1.8f)
                y.Iron.Box(new Vector3(x, deck + 1.04f, NextDeck + 0.1f), new Vector3(0.04f, 0.08f, 0.04f));
            // デッキの縁の柱。戸境ごとに地面から四階の床まで
            for (var k = 0; k <= NextBays; k++)
                y.NextCrete.Box(new Vector3(NextWest + k * NextBay, NextUpper * 0.5f, NextDeck + 0.15f), new Vector3(0.3f, NextUpper, 0.3f));

            var doors = new[] { y.Red, y.Line, y.Yellow, y.Leaf, y.Iron };
            for (var k = 0; k < NextBays; k++)
            {
                var x0 = NextWest + k * NextBay;
                // 下のメゾネット。一階に戸と窓、二階に窓を二つ。表は南（-z）
                MidDoor(y, doors[(k + 2) % doors.Length], x0 + 1.0f, 0f, NextFace);
                MidWindow(y, x0 + 3.4f, 0.9f, NextFace, 1.9f, 1.3f, (k * 7) % 5 == 1);
                MidWindow(y, x0 + 1.4f, 3.7f, NextFace, 1.4f, 1.3f, false);
                MidWindow(y, x0 + 3.9f, 3.7f, NextFace, 1.4f, 1.3f, (k * 3) % 5 == 2);
                // 前庭の塀と門の柱
                y.NextBrick.Box(new Vector3(x0 + 3.2f, 0.45f, BlockFace + 0.12f), new Vector3(NextBay - 1.6f, 0.9f, 0.25f));
                y.Leaf.Box(new Vector3(x0 + 3.4f, 0.55f, BlockFace + 0.9f), new Vector3(2.4f, 1.1f, 0.8f));
                // 上のメゾネット。デッキの奥に玄関と、その脇の台所の窓、メーターの物入れ
                MidDoor(y, doors[k % doors.Length], x0 + 1.2f, deck, NextFace);
                MidWindow(y, x0 + 3.3f, deck + 1.0f, NextFace, 1.5f, 1.0f, (k * 5) % 4 == 1);
                y.Line.Box(new Vector3(x0 + 2.15f, deck + 1.1f, NextFace - 0.03f), new Vector3(0.5f, 0.7f, 0.06f));
                // 四階。張り出しの面に寝室の窓を二つ
                MidWindow(y, x0 + 1.5f, NextUpper + 0.9f, NextDeck, 1.5f, 1.2f, false);
                MidWindow(y, x0 + 3.9f, NextUpper + 0.9f, NextDeck, 1.5f, 1.2f, k == 3);
            }
            // 裏の面。窓の列だけ
            for (var k = 0; k < NextBays; k++)
                for (var f = 0; f < 4; f++)
                    for (var i = 0; i < 2; i++)
                        y.Pane.FaceZ(NextBack + 0.02f, NextWest + k * NextBay + 1.0f + i * 2.6f, NextWest + k * NextBay + 2.4f + i * 2.6f,
                            f * Floor + 1.0f, f * Floor + 2.2f, 1);

            // 西の妻の階段の塔。縦に細い窓が階段の折り返しごとに並ぶ
            const float towerW = 2.8f;
            y.NextCrete.Box(new Vector3(NextWest - towerW * 0.5f, (NextRoof + 1.2f) * 0.5f, NextDeck + 2.2f), new Vector3(towerW, NextRoof + 1.2f, 4.4f));
            for (var h = 0; h < 8; h++)
                y.Pane.FaceZ(NextDeck - 0.02f, NextWest - towerW * 0.5f - 0.3f, NextWest - towerW * 0.5f + 0.3f,
                    h * Floor * 0.5f + 0.9f, h * Floor * 0.5f + 1.9f, -1);
            // 東の妻。横丁から見える。上の方に小さな窓を二つだけ
            for (var f = 1; f < 4; f += 2)
                y.Pane.FaceX(NextEast + 0.02f, NextFace + 3.5f, NextFace + 4.5f, f * Floor + 1.0f, f * Floor + 2.0f, 1);
        }

        /// <summary>戸を一枚。表は -z。色の付いた板と、上の明かり取りと、郵便受けの口</summary>
        static void MidDoor(YardBanks y, Bank paint, float x, float floor, float face)
        {
            paint.FaceZ(face - 0.03f, x - 0.47f, x + 0.47f, floor, floor + 2.05f, -1);
            y.Pane.FaceZ(face - 0.03f, x - 0.47f, x + 0.47f, floor + 2.12f, floor + 2.45f, -1);
            y.Iron.FaceZ(face - 0.04f, x - 0.14f, x + 0.14f, floor + 1.05f, floor + 1.11f, -1);
        }

        /// <summary>窓を一枚。表は -z（<paramref name="face"/> の手前）。下に明るい窓台。<paramref name="lit"/> なら灯りが入っている</summary>
        static void MidWindow(YardBanks y, float x, float sill, float face, float wide, float high, bool lit)
        {
            (lit ? y.Lit : y.Pane).FaceZ(face - 0.03f, x - wide * 0.5f, x + wide * 0.5f, sill, sill + high, -1);
            y.NextCrete.Box(new Vector3(x, sill - 0.05f, face - 0.08f), new Vector3(wide + 0.2f, 0.1f, 0.16f));
            // 縦の桟を一本。窓の四角が板に見えないように
            y.Iron.FaceZ(face - 0.04f, x - 0.03f, x + 0.03f, sill, sill + high, -1);
        }

        // ---- 高層棟 --------------------------------------------------------------

        /// <summary>
        /// 塔状の高層棟。正方形の平面に、階ごとのコンクリートの床の帯と、各面四つずつの窓。
        /// 屋上にエレベーターの機械室と、アンテナを数本。足元に入口の庇
        /// </summary>
        static void MidTower(YardBanks y, Vector3 centre, float wide, int floors)
        {
            const float storey = 2.7f;
            const float plinth = 1.2f;
            var high = plinth + floors * storey;
            var half = wide * 0.5f;
            y.NextCrete.Box(centre + Vector3.up * (high * 0.5f), new Vector3(wide, high, wide));
            var faces = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            for (var f = 0; f < floors; f++)
            {
                var y0 = plinth + f * storey;
                // 床の帯。面から少し出す
                y.NextCrete.Box(centre + Vector3.up * (y0 - 0.1f), new Vector3(wide + 0.16f, 0.22f, wide + 0.16f));
                foreach (var n in faces)
                {
                    var across = Vector3.Cross(Vector3.up, n);
                    for (var i = 0; i < 4; i++)
                    {
                        var along = -half + wide * (i + 0.5f) / 4f;
                        var at = centre + n * (half + 0.02f) + across * along + Vector3.up * (y0 + 0.8f);
                        var lit = (f * 7 + i * 3 + (int)(n.x * 2f + n.z * 5f)) % 17 == 0;
                        MidPane(lit ? y.Lit : y.Pane, at, n, across, 2.4f, 1.3f);
                    }
                }
            }
            y.NextCrete.Box(centre + Vector3.up * (high + 0.3f), new Vector3(wide + 0.2f, 0.6f, wide + 0.2f));
            y.NextCrete.Box(centre + new Vector3(-2f, high + 2.0f, 1f), new Vector3(6f, 3.4f, 5f));
            for (var i = 0; i < 3; i++)
                y.Iron.Box(centre + new Vector3(3f + i * 1.4f, high + 2.6f, -3f + i), new Vector3(0.08f, 5.2f, 0.08f));
            // 入口の庇。南の面
            y.Kerb.Box(centre + new Vector3(0f, 2.8f, -half - 1.0f), new Vector3(4f, 0.2f, 2.0f));
            y.Glass.FaceZ(centre.z - half - 0.03f, centre.x - 1.2f, centre.x + 1.2f, 0f, 2.4f, -1);
        }

        /// <summary>
        /// 向きを持つ面に窓を一枚。<paramref name="at"/> は下の辺の真ん中、<paramref name="n"/> は表の向き、
        /// <paramref name="across"/> は面に沿った横の向き
        /// </summary>
        static void MidPane(Bank b, Vector3 at, Vector3 n, Vector3 across, float wide, float high)
        {
            var r = across * (wide * 0.5f);
            var u = Vector3.up * high;
            MidQuad(b, at - r, at + r, at + r + u, at - r + u, n);
        }

        // ---- 西の棟 --------------------------------------------------------------

        /// <summary>
        /// 西の棟。もう一棟のデッキアクセスの棟を、敷地の西の芝地の向こうに南北に長く。
        /// デッキは東（敷地の側）を向く。庭から西を見ると、駐車場の向こうに腰壁の帯が横に通る
        /// </summary>
        static void MidBlockWest(YardBanks y)
        {
            const float face = -21.4f;          // デッキの奥の壁
            const float deckEdge = face + 2.0f; // デッキの縁
            const float back = face - 8.6f;
            const float south = -24f;
            const float north = -3f;
            const float deck = EstateTop;
            const float midZ = (south + north) * 0.5f;
            const float len = north - south;
            y.NextBrick.Box(new Vector3((face + back) * 0.5f, NextRoof * 0.5f, midZ), new Vector3(face - back, NextRoof, len));
            y.NextBrick.Box(new Vector3((face + deckEdge) * 0.5f, (NextUpper + NextRoof) * 0.5f, midZ), new Vector3(deckEdge - face, NextRoof - NextUpper, len));
            y.NextCrete.Box(new Vector3(deckEdge - 0.95f, NextUpper - 0.1f, midZ), new Vector3(2.0f, 0.3f, len + 0.1f));
            y.NextCrete.Box(new Vector3((face + deckEdge) * 0.5f, deck - 0.15f, midZ), new Vector3(deckEdge - face, 0.3f, len));
            y.NextCrete.Box(new Vector3(deckEdge - 0.1f, deck + 0.5f, midZ), new Vector3(0.2f, 1.0f, len));
            y.Iron.Box(new Vector3(deckEdge - 0.1f, deck + 1.08f, midZ), new Vector3(0.06f, 0.05f, len));
            y.NextCrete.Box(new Vector3(deckEdge - 0.12f, NextRoof + 0.25f, midZ), new Vector3(0.24f, 0.5f, len + 0.2f));
            var doors = new[] { y.Leaf, y.Red, y.Iron, y.Yellow };
            var bays = Mathf.FloorToInt(len / NextBay);
            for (var k = 0; k <= bays; k++)
                y.NextCrete.Box(new Vector3(deckEdge - 0.15f, NextUpper * 0.5f, south + 0.5f + k * NextBay), new Vector3(0.3f, NextUpper, 0.3f));
            for (var k = 0; k < bays; k++)
            {
                var z0 = south + 0.5f + k * NextBay;
                // 表は東（+x）。戸と窓は面の手前に薄い板で
                doors[k % doors.Length].FaceX(face + 0.03f, z0 + 3.9f, z0 + 4.8f, deck, deck + 2.05f, 1);
                doors[(k + 1) % doors.Length].FaceX(face + 0.03f, z0 + 3.9f, z0 + 4.8f, 0f, 2.05f, 1);
                (k == 2 ? y.Lit : y.Pane).FaceX(face + 0.03f, z0 + 0.8f, z0 + 2.4f, deck + 1.0f, deck + 2.0f, 1);
                y.Pane.FaceX(face + 0.03f, z0 + 0.8f, z0 + 2.9f, 0.9f, 2.2f, 1);
                y.Pane.FaceX(face + 0.03f, z0 + 0.8f, z0 + 2.4f, 3.7f, 5.0f, 1);
                y.Pane.FaceX(face + 0.03f, z0 + 3.6f, z0 + 5.0f, 3.7f, 5.0f, 1);
                y.Pane.FaceX(deckEdge + 0.03f, z0 + 0.8f, z0 + 2.3f, NextUpper + 0.9f, NextUpper + 2.1f, 1);
                (k == 1 ? y.Lit : y.Pane).FaceX(deckEdge + 0.03f, z0 + 3.2f, z0 + 4.7f, NextUpper + 0.9f, NextUpper + 2.1f, 1);
            }
        }

        // ---- 煙突の並ぶ煉瓦の長屋 ------------------------------------------------

        /// <summary>
        /// ヴィクトリア朝の長屋を一列。<paramref name="start"/> は列の一方の端の、表の壁の足元。
        /// <paramref name="yaw"/> は表の向き（+z が 0、東回り）で、家は表から見て右へ並ぶ。
        ///
        /// 黄色い煉瓦（ロンドンのストック煉瓦）の箱にスレートの切妻屋根を一本通し、
        /// 戸境ごとに煙突の束を棟の上へ突き出す。**煙突は等間隔に並べる。** 屋根の上の煙突の列が、
        /// 遠目にはいちばんロンドンの長屋らしい輪郭になる。
        /// 表は一階に張り出し窓と色の付いた戸、上の階に白い枠の上げ下げ窓、階の境に白い帯。
        ///
        /// <paramref name="lamps"/> が偽なら、上の階の窓に灯りを入れない。**灯りは朝の七時の見え方。**
        /// 公園の午後 3 時台に灯った窓が並ぶと、夕方に見える。
        /// 偽でも乱数は同じだけ引くので、戸の色と生垣の並びは灯りの有無で変わらない
        /// </summary>
        static void MidTerrace(YardBanks y, Vector3 start, float yaw, int houses, int storeys, int seed, bool lamps = true)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // 表から見て右が +x、奥が -z
            System.Func<float, float, float, Vector3> p = (a, b, c) => start + rot * new Vector3(-a, b, c);
            var front = rot * Vector3.forward;
            var right = rot * Vector3.left;
            const float deep = 8.5f;
            var eaves = storeys * 3.0f + 0.4f;
            var ridge = eaves + 2.8f;
            var len = houses * HouseWide;

            // 壁の塊
            y.Stock.Box(p(len * 0.5f, eaves * 0.5f, -deep * 0.5f), new Vector3(len, eaves, deep), rot);
            // 軒の白い帯と、階の境の帯
            y.Line.Box(p(len * 0.5f, eaves - 0.15f, 0.02f), new Vector3(len, 0.3f, 0.1f), rot);
            for (var s = 1; s < storeys; s++)
                y.Line.Box(p(len * 0.5f, s * 3.0f + 0.1f, 0.02f), new Vector3(len, 0.14f, 0.08f), rot);
            // 屋根。表と裏の斜面、両端の妻の三角
            var a0 = p(0f, eaves, 0.3f);
            var a1 = p(len, eaves, 0.3f);
            var r0 = p(0f, ridge, -deep * 0.5f);
            var r1 = p(len, ridge, -deep * 0.5f);
            var b0 = p(0f, eaves, -deep - 0.3f);
            var b1 = p(len, eaves, -deep - 0.3f);
            MidQuad(y.Slate, a0, a1, r1, r0, front + Vector3.up);
            MidQuad(y.Slate, b1, b0, r0, r1, -front + Vector3.up);
            MidQuad(y.Stock, p(0f, eaves, 0f), p(0f, eaves, -deep), r0, r0, -right);
            MidQuad(y.Stock, p(len, eaves, -deep), p(len, eaves, 0f), r1, r1, right);

            var rnd = new System.Random(seed);
            var doors = new[] { y.Red, y.Iron, y.Leaf, y.Yellow, y.Line };
            for (var h = 0; h < houses; h++)
            {
                var x0 = h * HouseWide;
                // 戸境の煙突。棟の上へ 1.5 m、上に素焼きの煙突を四本
                var stackX = x0;
                MidChimney(y, p(stackX, ridge, -deep * 0.5f), rot);
                if (h == houses - 1) MidChimney(y, p(len, ridge, -deep * 0.5f), rot);

                // 一階の張り出し窓。表から 0.7 m
                var bay = x0 + HouseWide * 0.36f;
                y.Stock.Box(p(bay, 0.4f, 0.35f), new Vector3(2.3f, 0.8f, 0.7f), rot);
                y.Pane.Box(p(bay, 1.7f, 0.33f), new Vector3(2.1f, 1.8f, 0.62f), rot);
                for (var i = 0; i < 3; i++)
                    y.Line.Box(p(bay - 1.05f + i * 1.05f, 1.7f, 0.66f), new Vector3(0.1f, 1.8f, 0.06f), rot);
                y.Line.Box(p(bay, 2.7f, 0.37f), new Vector3(2.4f, 0.2f, 0.78f), rot);
                // 戸。明かり取りと白い縁
                var doorX = x0 + HouseWide * 0.80f;
                y.Line.Box(p(doorX, 1.35f, 0.02f), new Vector3(1.3f, 2.7f, 0.06f), rot);
                doors[rnd.Next(doors.Length)].Box(p(doorX, 1.05f, 0.05f), new Vector3(0.95f, 2.1f, 0.04f), rot);
                y.Pane.Box(p(doorX, 2.35f, 0.05f), new Vector3(0.95f, 0.35f, 0.04f), rot);
                // 上の階の上げ下げ窓を二つずつ
                for (var s = 1; s < storeys; s++)
                    for (var i = 0; i < 2; i++)
                    {
                        var wx = x0 + HouseWide * (0.3f + i * 0.42f);
                        var sill = s * 3.0f + 0.7f;
                        var lit = rnd.NextDouble() < 0.12 && lamps;
                        y.Line.Box(p(wx, sill + 0.85f, 0.02f), new Vector3(1.05f, 1.8f, 0.05f), rot);
                        (lit ? y.Lit : y.Pane).Box(p(wx, sill + 0.85f, 0.05f), new Vector3(0.85f, 1.6f, 0.03f), rot);
                        y.Line.Box(p(wx, sill + 0.85f, 0.07f), new Vector3(0.85f, 0.06f, 0.02f), rot);
                    }
                // 前庭の低い塀と生垣
                y.Stock.Box(p(x0 + HouseWide * 0.4f, 0.4f, 1.4f), new Vector3(HouseWide * 0.8f - 0.2f, 0.8f, 0.24f), rot);
                if (rnd.NextDouble() < 0.5)
                    y.Leaf.Box(p(x0 + HouseWide * 0.3f, 0.6f, 1.0f), new Vector3(1.6f, 1.2f, 0.6f), rot);
            }
        }

        /// <summary>煙突の束。棟の上へ突き出す煉瓦の角柱に白い笠、上に素焼きの煙突を四本</summary>
        static void MidChimney(YardBanks y, Vector3 ridgeAt, Quaternion rot)
        {
            const float up = 1.5f;
            y.Stock.Box(ridgeAt + Vector3.up * (up * 0.5f - 0.6f), new Vector3(0.7f, up + 1.2f, 1.4f), rot);
            y.Line.Box(ridgeAt + Vector3.up * (up - 0.05f), new Vector3(0.8f, 0.12f, 1.5f), rot);
            for (var i = 0; i < 4; i++)
                y.Rubber.Box(ridgeAt + rot * new Vector3(0f, up + 0.25f, -0.51f + i * 0.34f), new Vector3(0.2f, 0.5f, 0.2f), rot);
        }

        /// <summary>中の層の木。横丁の角と、西の芝地と、高層棟の足元に葉の無いプラタナス</summary>
        static void MidTrees(YardBanks y)
        {
            YardPlane(y, new Vector3(SidePaveWest + 0.5f, 0f, 15f), 11f, 17);
            YardPlane(y, new Vector3(-15.4f, 0f, -9f), 12f, 19);
            YardPlane(y, new Vector3(-19.5f, 0f, 13.5f), 10f, 23);
            YardPlane(y, new Vector3(-31f, 0f, 12.5f), 11f, 29);
        }
    }
}
