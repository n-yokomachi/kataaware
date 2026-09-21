using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// 等間隔に並ぶものを一つ入れると、その場所らしさがいちばん早く伝わる。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 団地 ------------------------------------------------------------
        //
        // 外階段を一本、まっすぐ上へ通す。折り返しにしないのは、記憶 0 の子どもが
        // 下から三階まで一息に駆け上がるのを、道筋を折らずに鍵打ちで書けるようにするため。
        // 三階の廊下に戸口が二つ並び、左が記憶 0・1 の家、右が記憶 8・15 の家

        /// <summary>階の高さ。m</summary>
        public const float Floor = 2.8f;
        /// <summary>一階ぶんの階段が z へ伸びる長さ。勾配は 34 度で、実際の外階段に近い</summary>
        const float Flight = 4.2f;
        /// <summary>踊り場の奥行き</summary>
        const float LandingDeep = 1.4f;
        /// <summary>階段の半幅。幅 1.2 m</summary>
        const float StairHalf = 0.6f;
        /// <summary>一階ぶんの段数</summary>
        const int StepCount = 16;

        /// <summary>階段の下端。ここから -z へ上がる</summary>
        public const float EstateFoot = 2.0f;
        /// <summary>一つ目の踊り場の真ん中</summary>
        public const float EstateLanding1 = EstateFoot - Flight - LandingDeep * 0.5f;      // -2.9
        /// <summary>二つ目の踊り場の真ん中</summary>
        public const float EstateLanding2 = EstateLanding1 - LandingDeep * 0.5f - Flight - LandingDeep * 0.5f; // -8.5
        /// <summary>三階の廊下の真ん中</summary>
        public const float EstateWalk = EstateLanding2 - LandingDeep * 0.5f - Flight - LandingDeep * 0.5f;     // -14.1
        /// <summary>建物の面。戸口はここに開く</summary>
        public const float EstateFace = EstateWalk - LandingDeep * 0.5f;                   // -14.8
        /// <summary>三階の高さ</summary>
        public const float EstateTop = Floor * 3f;                                         // 8.4

        /// <summary>左の戸口の真ん中。記憶 0 の母と、記憶 1 のハンナの家</summary>
        public const float DoorA = 1.6f;
        /// <summary>右の戸口の真ん中。記憶 8 のジョルジョと、記憶 15 のエレナの家</summary>
        public const float DoorB = 4.2f;
        const float DoorHalf = 0.45f;
        const float DoorHigh = 2.0f;

        /// <summary>右の家の中。テレビの前</summary>
        public static readonly Vector3 EstateTv = new Vector3(5.3f, EstateTop + 0.85f, -18.3f);


        // ---- 団地の外階段 ------------------------------------------------------

        /// <summary>
        /// 記憶 0・1・8・15 の舞台。外階段と三階の廊下、右の家の中まで。
        ///
        /// 左の家（戸口 A）は中まで作らず、戸口の奥に浅い窪みと灯りだけを置く。
        /// 記憶 0 が振り返ったときに母が逆光の影になるのは、この奥の灯りのため
        /// </summary>
        static void Estate(Transform place)
        {
            var slab = new Bank { Texel = 0.35f };
            // 地面。階段の下と建物の足元だけあればよい
            slab.FaceY(0f, -6f, 10f, -20f, 7f, 1);
            // 踊り場と三階の廊下
            slab.Box(new Vector3(0f, Floor - 0.09f, EstateLanding1), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3(0f, Floor * 2f - 0.09f, EstateLanding2), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3(3.2f, EstateTop - 0.09f, EstateWalk), new Vector3(7.6f, 0.18f, LandingDeep));
            Flightup(slab, EstateFoot, 0f);
            Flightup(slab, EstateLanding1 - LandingDeep * 0.5f, Floor);
            Flightup(slab, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            // 右の家の床
            slab.FaceY(EstateTop, 3.0f, 7.0f, -18.6f, EstateFace, 1);
            Emit(place, "EstateFloor", slab, "Floor", true);

            var wall = new Bank { Texel = 0.3f };
            // 階段が背にしている壁。ここが無いと、階段が虚空に掛かっているように見える
            wall.FaceX(-0.9f, EstateFace, 3f, 0f, 11.2f, 1);
            // 建物の面。三階に戸口を二つ開ける
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(DoorA - DoorHalf, DoorA + DoorHalf, EstateTop, EstateTop + DoorHigh),
                new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop, EstateTop + DoorHigh),
            };
            wall.FaceZHoles(EstateFace, -1.2f, 7.4f, 0f, 11.2f, 1, holes);
            // 左の戸口の奥の窪み
            wall.FaceX(DoorA - 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, 1);
            wall.FaceX(DoorA + 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, -1);
            wall.FaceY(EstateTop + 2.2f, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, -1);
            wall.FaceY(EstateTop, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, 1);
            // 右の家。四方と天井
            wall.FaceX(3.0f, -18.6f, EstateFace, EstateTop, EstateTop + 2.5f, 1);
            wall.FaceX(7.0f, -18.6f, EstateFace, EstateTop, EstateTop + 2.5f, -1);
            wall.FaceZ(-18.6f, 3.0f, 7.0f, EstateTop, EstateTop + 2.5f, 1);
            wall.FaceZ(EstateFace - 0.02f, 3.0f, 7.0f, EstateTop, EstateTop + 2.5f, -1);
            Emit(place, "EstateWall", wall, "Wall", true);

            var sky = new Bank { Texel = 0.3f };
            sky.FaceY(EstateTop + 2.5f, 3.0f, 7.0f, -18.6f, EstateFace, -1);
            sky.FaceY(EstateTop + 2.2f, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, -1);
            Emit(place, "EstateCeiling", sky, "Ceiling");

            var rail = new Bank { Texel = 0.5f };
            Handrail(rail, EstateFoot, 0f);
            Bar(rail, EstateLanding1 - LandingDeep * 0.5f, EstateLanding1 + LandingDeep * 0.5f, Floor);
            Handrail(rail, EstateLanding1 - LandingDeep * 0.5f, Floor);
            Bar(rail, EstateLanding2 - LandingDeep * 0.5f, EstateLanding2 + LandingDeep * 0.5f, Floor * 2f);
            Handrail(rail, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            // 三階の廊下の外側。**階段の口（x -0.6〜0.6）は空ける。**
            // 手すりに当たりを入れたので、廊下の長さぶん通すと階段を上がってきた先が塞がる。
            // 一度通した版では、上がり切ったところで壁に突き当たって廊下へ出られなかった
            for (var i = 0; i < 2; i++)
            {
                var y = EstateTop + 0.5f + i * 0.45f;
                rail.Box(new Vector3(3.8f, y, EstateWalk + LandingDeep * 0.5f), new Vector3(6.4f, 0.05f, 0.05f));
            }
            for (var i = 0; i <= 5; i++)
                rail.Box(new Vector3(StairHalf + i * 1.28f, EstateTop + 0.48f, EstateWalk + LandingDeep * 0.5f),
                    new Vector3(0.05f, 0.96f, 0.05f));
            Emit(place, "EstateRail", rail, "Rail", true);

            // 戸はどちらも開いている。記憶 0 は母が戸口に立ち、記憶 8 は新聞を持って中へ入り、
            // 記憶 15 は中から夫を迎える。閉めた戸を置くと、その三つが同じ場所で成り立たなくなる
            var leaf = new Bank { Texel = 0.4f };
            leaf.Box(new Vector3(DoorA + 0.92f, EstateTop + DoorHigh * 0.5f, EstateFace + 0.06f),
                new Vector3(0.86f, DoorHigh, 0.05f));
            leaf.Box(new Vector3(DoorB + 0.92f, EstateTop + DoorHigh * 0.5f, EstateFace + 0.06f),
                new Vector3(0.86f, DoorHigh, 0.05f));
            Emit(place, "EstateDoor", leaf, "Door");

            var soft = new Bank { Texel = 0.5f };
            soft.Box(new Vector3(4.9f, EstateTop + 0.22f, -16.6f), new Vector3(1.9f, 0.44f, 0.72f));
            soft.Box(new Vector3(4.9f, EstateTop + 0.52f, -16.28f), new Vector3(1.9f, 0.6f, 0.14f));
            Emit(place, "EstateSofa", soft, "Cloth");

            var set = new Bank { Texel = 0.5f };
            set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));
            Emit(place, "EstateTvSet", set, "Timber");

            // 戸口の奥とテレビ。灯りではなく光る面で済ませる。
            // 記憶 0 の母も記憶 15 の夫も、この光の手前に立つので影にしか見えない
            Pane(place, "EstateHallLight", new Vector3(DoorA, EstateTop + 1.1f, -16.35f),
                new Vector2(1.4f, 2.1f), Vector3.forward, Glow("GlowHall", new Color(1f, 0.90f, 0.74f), 0.55f));
            Pane(place, "EstateTv", EstateTv + new Vector3(0f, 0f, 0.06f),
                new Vector2(0.72f, 0.44f), Vector3.forward, Glow("GlowTv", new Color(0.72f, 0.82f, 1f), 1.35f));

            // 地面の端から先は虚空なので、見えない仕切りで囲う。
            // 建物の中まで歩ける必要は無いが、裏へ回れても困らないので地面ごと囲う
            Ring(place, "EstateFence", new Vector2(-6f, 10f), new Vector2(-20f, 7f), 3.5f);
            // 階段と三階の廊下の外側は手すりが受け持つ。EstateRail に当たりを入れてあるので、
            // ここへ仕切りを重ねると階段を上がってきた先が塞がる。
            // **廊下の両端だけは手すりが無い。** 歩いて確かめたら、廊下を +x へ歩き切って
            // 三階から地面へ落ちた。絵に手すりを増やさずに止めたいので、仕切りで塞ぐ
            Fence(place, "EstateWalkEndA", new Vector3(-0.7f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));
            Fence(place, "EstateWalkEndB", new Vector3(7.1f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));
            // 朝の低い日射し。階段の側から当てるので、三階の戸口に立つ人は逆光になる
            var sun = Lamp(place, "Morning", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(30f, -148f, 0f), new Color(1f, 0.95f, 0.86f), 1.7f, 10f);
            sun.shadows = LightShadows.Soft;
            // 右の家はテレビの光だけでは足りない。朝の窓明かりぶんをここで補う
            Lamp(place, "RoomB", LightType.Point, new Vector3(5.0f, EstateTop + 2.1f, -16.6f),
                Vector3.zero, new Color(0.80f, 0.84f, 0.95f), 1.5f, 6f);
        }

        /// <summary>一階ぶんの段。踏み面と蹴込みを一段ずつ張る</summary>
        static void Flightup(Bank b, float zFoot, float yFoot)
        {
            var d = Flight / StepCount;
            var r = Floor / StepCount;
            for (var i = 0; i < StepCount; i++)
            {
                b.FaceZ(zFoot - i * d, -StairHalf, StairHalf, yFoot + i * r, yFoot + (i + 1) * r, 1);
                b.FaceY(yFoot + (i + 1) * r, -StairHalf, StairHalf, zFoot - (i + 1) * d, zFoot - i * d, 1);
            }
            // 段の裏。下から見上げたときに段が宙に浮いて見えないように、斜めの板を一枚通す
            var dir = new Vector3(0f, Floor, -Flight).normalized;
            b.Box(new Vector3(0f, yFoot + Floor * 0.5f - 0.24f, zFoot - Flight * 0.5f),
                new Vector3(StairHalf * 2f, 0.3f, Mathf.Sqrt(Flight * Flight + Floor * Floor)),
                Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>斜めの手すり。段と同じ勾配で二本通し、柱を立てる</summary>
        static void Handrail(Bank b, float zFoot, float yFoot)
        {
            var dir = new Vector3(0f, Floor, -Flight).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var len = Mathf.Sqrt(Flight * Flight + Floor * Floor);
            for (var i = 0; i < 2; i++)
            {
                var lift = 0.55f + i * 0.45f;
                b.Box(new Vector3(StairHalf, yFoot + Floor * 0.5f + lift, zFoot - Flight * 0.5f),
                    new Vector3(0.05f, 0.05f, len), rot);
            }
            for (var i = 0; i <= 3; i++)
            {
                var k = i / 3f;
                b.Box(new Vector3(StairHalf, yFoot + Floor * k + 0.48f, zFoot - Flight * k),
                    new Vector3(0.05f, 0.96f, 0.05f));
            }
        }

        /// <summary>踊り場の手すり。水平に二本</summary>
        static void Bar(Bank b, float z0, float z1, float y)
        {
            for (var i = 0; i < 2; i++)
                b.Box(new Vector3(StairHalf, y + 0.5f + i * 0.45f, (z0 + z1) * 0.5f),
                    new Vector3(0.05f, 0.05f, z1 - z0));
            b.Box(new Vector3(StairHalf, y + 0.48f, z0 + 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
            b.Box(new Vector3(StairHalf, y + 0.48f, z1 - 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
        }
    }
}
