using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の教室。
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
        // ---- 教室 ------------------------------------------------------------

        /// <summary>黒板の面</summary>
        public const float BoardZ = 4.92f;
        /// <summary>教壇の前。記憶 7 のリーがチョークを持っている</summary>
        public static readonly Vector3 Chalk = new Vector3(0f, 0.15f, 4.0f);
        /// <summary>机の列の x と、行の z。記憶 13 と 14 はこの並びに座る</summary>
        public static readonly float[] DeskX = { -2.4f, -0.8f, 0.8f, 2.4f };
        public static readonly float[] DeskZ = { 2.0f, 0.4f, -1.2f };
        /// <summary>机の天板。座る人はここから 0.55 m 後ろ</summary>
        public const float DeskY = 0.72f;
        const float ClassHigh = 3.0f;


        // ---- 教室 --------------------------------------------------------------

        /// <summary>
        /// 記憶 7・13・14 の舞台。机の列が四つ三行、黒板と教壇、窓は白い板。
        /// 記憶 14 は遠くがぼやける体なので、黒板の字は初めから書かない
        /// </summary>
        static void Classroom(Transform place)
        {
            var deck = new Bank { Texel = 0.4f };
            deck.FaceY(0f, -4f, 4f, -4f, 5f, 1);
            deck.Box(new Vector3(0f, 0.075f, 4.05f), new Vector3(6f, 0.15f, 1.1f));
            Emit(place, "ClassFloor", deck, "Floor", true);

            var wall = new Bank { Texel = 0.35f };
            wall.FaceZ(5f, -4f, 4f, 0f, ClassHigh, -1);
            wall.FaceZ(-4f, -4f, 4f, 0f, ClassHigh, 1);
            wall.FaceX(4f, -4f, 5f, 0f, ClassHigh, -1);
            // 窓は三枚。抜いた先に光る板を置く
            var holes = new System.Collections.Generic.List<Vector4>();
            for (var i = 0; i < 3; i++)
                holes.Add(new Vector4(-2.6f + i * 2.3f, -2.6f + i * 2.3f + 1.8f, 1.0f, 2.5f));
            wall.FaceXHoles(-4f, -4f, 5f, 0f, ClassHigh, 1, holes);
            Emit(place, "ClassWall", wall, "Wall", true);

            var roof = new Bank { Texel = 0.35f };
            roof.FaceY(ClassHigh, -4f, 4f, -4f, 5f, -1);
            Emit(place, "ClassCeiling", roof, "Ceiling");

            var slate = new Bank { Texel = 0.4f };
            slate.Box(new Vector3(0f, 1.65f, BoardZ), new Vector3(5.2f, 1.2f, 0.08f));
            Emit(place, "ClassBoard", slate, "Board");

            // 黒板の縁とチョーク受け。板は壁とほとんど同じ暗さなので、
            // 明るい縁が無いと黒板がそこに在ることが読み取れない
            var trim = new Bank { Texel = 0.5f };
            trim.Box(new Vector3(0f, 1.04f, BoardZ - 0.06f), new Vector3(5.3f, 0.06f, 0.14f));
            trim.Box(new Vector3(0f, 2.28f, BoardZ - 0.02f), new Vector3(5.3f, 0.06f, 0.10f));
            Emit(place, "ClassTrim", trim, "Rail");

            var wood = new Bank { Texel = 0.5f };
            for (var c = 0; c < DeskX.Length; c++)
                for (var r = 0; r < DeskZ.Length; r++)
                    Desk(wood, new Vector3(DeskX[c], 0f, DeskZ[r]));
            Emit(place, "ClassDesk", wood, "Timber");

            var pane = Glow("GlowNoon", new Color(0.96f, 0.97f, 1f), 1.6f);
            for (var i = 0; i < 3; i++)
                Pane(place, "ClassWindow" + i, new Vector3(-4.06f, 1.75f, -2.6f + i * 2.3f + 0.9f),
                    new Vector2(1.8f, 1.5f), Vector3.right, pane);

            Lamp(place, "Noon", LightType.Point, new Vector3(-0.8f, ClassHigh - 0.2f, 1.8f),
                Vector3.zero, new Color(0.94f, 0.96f, 1f), 10f, 18f);
            Pane(place, "ClassLamp", new Vector3(-0.8f, ClassHigh - 0.04f, 1.8f), new Vector2(1.2f, 0.3f),
                Vector3.down, Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f));
        }

        static void Desk(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, DeskY, 0f), new Vector3(0.62f, 0.04f, 0.44f));
            for (var i = 0; i < 4; i++)
                b.Box(at + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.27f, DeskY * 0.5f, (i < 2 ? -1f : 1f) * 0.18f),
                    new Vector3(0.04f, DeskY, 0.04f));
            b.Box(at + new Vector3(0f, 0.44f, -0.56f), new Vector3(0.4f, 0.04f, 0.36f));
            b.Box(at + new Vector3(0f, 0.66f, -0.72f), new Vector3(0.4f, 0.4f, 0.04f));
            for (var i = 0; i < 4; i++)
                b.Box(at + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.17f, 0.22f, -0.56f + (i < 2 ? -1f : 1f) * 0.15f),
                    new Vector3(0.03f, 0.44f, 0.03f));
        }
    }
}
