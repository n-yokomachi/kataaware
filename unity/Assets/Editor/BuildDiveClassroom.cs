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
        /// <summary>
        /// 机の列の x と、行の z。記憶 13 と 14 はこの並びに座る。
        /// **前からある四列三行は動かせない。** 鍵打ちが添字で席を指しているので、
        /// 増やすぶんは末尾へ並べる
        /// </summary>
        public static readonly float[] DeskX = { -2.4f, -0.8f, 0.8f, 2.4f, 4.0f };
        public static readonly float[] DeskZ = { 2.0f, 0.4f, -1.2f, -2.8f };
        /// <summary>机の天板。座る人はここから 0.55 m 後ろ</summary>
        public const float DeskY = 0.72f;
        const float ClassHigh = 3.0f;

        // 箱の四隅。窓側は動かさず、列を一つ増やしたぶんだけ廊下側へ広げてある。
        // 窓を動かすと記憶 7・13・14 が「窓の外が白い」と振り向く先がずれる
        const float ClassWest = -4f;
        const float ClassEast = 4.8f;
        const float ClassBack = -4f;
        const float ClassFore = 5f;

        // 窓の抜き。**1.6 m の繰り返しがこの場所をいちばん早く言う**ので、
        // 幅より間隔を先に決めて、余りを桟の太さにしてある
        const float ClassPaneStep = 1.6f;
        const float ClassPaneWide = 1.35f;
        const float ClassPaneLow = 0.95f;
        const float ClassPaneTop = 2.45f;
        const float ClassBoardY = 1.70f;
        // 時計は黒板の脇。真上は黒板灯の器具が通り、座った目の高さからだと
        // 器具の陰へ隠れてしまう
        const float ClockX = 3.75f;
        const float ClockY = 2.05f;

        /// <summary>i 枚目の窓の中心。四枚を壁の真ん中に揃える</summary>
        static float ClassPaneAt(int i)
        {
            return (ClassBack + ClassFore) * 0.5f + (i - 1.5f) * ClassPaneStep;
        }

        // ---- 教室 --------------------------------------------------------------

        /// <summary>
        /// 記憶 7・13・14 の舞台。窓の列・机の列・黒板の三つを大きく置き、
        /// 掲示板と時計とロッカーは最後に添える。
        /// 記憶 14 は遠くがぼやける体なので、黒板の字は初めから書かない
        /// </summary>
        static void Classroom(Transform place)
        {
            var lino = ClassMat("ClassLino", new Color(0.392f, 0.384f, 0.360f), 0.22f);
            var sheet = ClassMat("ClassPaper", new Color(0.560f, 0.548f, 0.522f), 0.08f);

            var deck = new Bank { Texel = 0.4f };
            deck.FaceY(0f, ClassWest, ClassEast, ClassBack, ClassFore, 1);
            deck.Box(new Vector3(0f, 0.075f, 4.05f), new Vector3(6f, 0.15f, 1.1f));
            ClassEmit(place, "ClassFloor", deck, lino, true);

            var wall = new Bank { Texel = 0.35f };
            wall.FaceZ(ClassFore, ClassWest, ClassEast, 0f, ClassHigh, -1);
            wall.FaceZ(ClassBack, ClassWest, ClassEast, 0f, ClassHigh, 1);
            wall.FaceX(ClassEast, ClassBack, ClassFore, 0f, ClassHigh, -1);
            var holes = new System.Collections.Generic.List<Vector4>();
            for (var i = 0; i < 4; i++)
            {
                var c = ClassPaneAt(i);
                holes.Add(new Vector4(c - ClassPaneWide * 0.5f, c + ClassPaneWide * 0.5f,
                    ClassPaneLow, ClassPaneTop));
            }
            wall.FaceXHoles(ClassWest, ClassBack, ClassFore, 0f, ClassHigh, 1, holes);
            ClassEmit(place, "ClassWall", wall, Mat("Wall"), true);

            var roof = new Bank { Texel = 0.35f };
            roof.FaceY(ClassHigh, ClassWest, ClassEast, ClassBack, ClassFore, -1);
            ClassEmit(place, "ClassCeiling", roof, Mat("Ceiling"), false);

            var slate = new Bank { Texel = 0.4f };
            slate.Box(new Vector3(0f, ClassBoardY, BoardZ), new Vector3(6.0f, 1.4f, 0.08f));
            // 時計の針。黒板と同じ暗さの面が要るのはこの二本だけで、
            // これだけのために mesh をもう一枚焼くと素材の数が増える
            slate.Box(new Vector3(ClockX, ClockY + 0.075f, 4.806f), new Vector3(0.032f, 0.18f, 0.012f));
            slate.Box(new Vector3(ClockX + 0.07f, ClockY, 4.806f), new Vector3(0.15f, 0.030f, 0.012f));
            ClassEmit(place, "ClassBoard", slate, Mat("Board"), false);

            var trim = new Bank { Texel = 0.5f };
            ClassRails(trim);
            ClassEmit(place, "ClassTrim", trim, Mat("Rail"), false);

            var wood = new Bank { Texel = 0.5f };
            for (var c = 0; c < DeskX.Length; c++)
                for (var r = 0; r < DeskZ.Length; r++)
                    Desk(wood, new Vector3(DeskX[c], 0f, DeskZ[r]));
            ClassLectern(wood);
            ClassLockers(wood);
            ClassEmit(place, "ClassDesk", wood, Mat("Timber"), false);

            var leaf = new Bank { Texel = 0.5f };
            // 廊下側の戸。抜かずに壁へ重ねる。抜くと向こうは何も無い虚空で、
            // 廊下があるように見せるためだけに廊下を組むことになる
            leaf.Box(new Vector3(4.75f, 1.025f, 3.05f), new Vector3(0.09f, 2.05f, 0.98f));
            leaf.Box(new Vector3(-0.9f, 1.86f, -3.93f), new Vector3(2.8f, 1.1f, 0.05f));
            ClassEmit(place, "ClassDoor", leaf, Mat("Door"), false);

            var pin = new Bank { Texel = 0.5f };
            ClassNotices(pin);
            pin.Box(new Vector3(ClockX, ClockY, 4.826f), new Vector3(0.46f, 0.46f, 0.022f));
            ClassEmit(place, "ClassPaper", pin, sheet, false);

            ClassLight(place);
        }

        /// <summary>
        /// 金物をまとめて一枚に。黒板の縁・窓の桟・腰の帯・蛍光灯の器具・時計・戸枠は
        /// どれも同じ艶の細い線で、素材ごとに一枚へ焼く決まりに素直に従うとここへ集まる
        /// </summary>
        static void ClassRails(Bank b)
        {
            // 黒板の縁とチョーク受け。板は壁とほとんど同じ暗さなので、
            // 明るい縁が無いと黒板がそこに在ることが読み取れない
            b.Box(new Vector3(0f, 0.96f, BoardZ - 0.07f), new Vector3(6.1f, 0.06f, 0.16f));
            b.Box(new Vector3(0f, 2.44f, BoardZ - 0.02f), new Vector3(6.1f, 0.06f, 0.12f));

            var mid = (ClassPaneLow + ClassPaneTop) * 0.5f;
            var high = ClassPaneTop - ClassPaneLow;
            for (var i = 0; i < 4; i++)
            {
                var c = ClassPaneAt(i);
                var edge = ClassPaneWide * 0.5f - 0.035f;
                b.Box(new Vector3(-3.95f, ClassPaneLow + 0.035f, c), new Vector3(0.09f, 0.07f, ClassPaneWide));
                b.Box(new Vector3(-3.95f, ClassPaneTop - 0.035f, c), new Vector3(0.09f, 0.07f, ClassPaneWide));
                b.Box(new Vector3(-3.95f, mid, c - edge), new Vector3(0.09f, high, 0.07f));
                b.Box(new Vector3(-3.95f, mid, c + edge), new Vector3(0.09f, high, 0.07f));
                // 中桟。二枚引きに見せる一本で、窓が四つ並ぶ繰り返しを細かく刻む
                b.Box(new Vector3(-3.945f, mid, c), new Vector3(0.10f, high, 0.06f));
            }
            // 窓台と、それを他の壁へ回した腰の帯。高さの揃った水平線が一本あると、
            // 壁が暗いままでも箱の奥行きが読める。
            // **壁の面とぴったり同じ高さで終わらせない。** 同じ平面に二枚あると
            // どちらが手前か決まらず、艶のある金物なので破線になって現れる
            b.Box(new Vector3(-3.865f, ClassPaneLow - 0.045f, 0.5f), new Vector3(0.25f, 0.07f, 6.6f));
            b.Box(new Vector3(ClassEast - 0.05f, 0.92f, 0.5f), new Vector3(0.08f, 0.10f, 9f));
            b.Box(new Vector3(0.4f, 0.92f, ClassBack + 0.05f), new Vector3(8.8f, 0.10f, 0.08f));

            // 蛍光灯の器具を二本、天井の三等分の位置へ
            b.Box(new Vector3(0.4f, ClassHigh - 0.075f, 2.0f), new Vector3(6.0f, 0.13f, 0.28f));
            b.Box(new Vector3(0.4f, ClassHigh - 0.075f, -1.0f), new Vector3(6.0f, 0.13f, 0.28f));

            // 教壇の段鼻。床と同じ面で組んであるので、明るい線が一本無いと
            // 段が付いていることが分からず、人だけが浮いて見える
            b.Box(new Vector3(0f, 0.142f, 3.502f), new Vector3(6.02f, 0.035f, 0.055f));
            b.Box(new Vector3(-2.998f, 0.142f, 4.05f), new Vector3(0.055f, 0.035f, 1.1f));
            b.Box(new Vector3(2.998f, 0.142f, 4.05f), new Vector3(0.055f, 0.035f, 1.1f));

            // 黒板灯。黒板は板そのものが暗いのが正しいので、明るくするのではなく
            // 上から当てて、暗いまま輪郭が立つようにする
            b.Box(new Vector3(0f, 2.62f, 4.50f), new Vector3(6.0f, 0.10f, 0.24f));
            b.Box(new Vector3(-2.62f, 2.70f, 4.72f), new Vector3(0.06f, 0.16f, 0.46f));
            b.Box(new Vector3(2.62f, 2.70f, 4.72f), new Vector3(0.06f, 0.16f, 0.46f));

            b.Box(new Vector3(ClockX, ClockY, 4.875f), new Vector3(0.56f, 0.56f, 0.07f));

            b.Box(new Vector3(4.725f, 2.09f, 3.05f), new Vector3(0.14f, 0.08f, 1.14f));
            b.Box(new Vector3(4.725f, 1.03f, 2.52f), new Vector3(0.14f, 2.12f, 0.08f));
            b.Box(new Vector3(4.725f, 1.03f, 3.58f), new Vector3(0.14f, 2.12f, 0.08f));
            b.Box(new Vector3(4.69f, 0.98f, 3.38f), new Vector3(0.05f, 0.05f, 0.24f));
        }

        /// <summary>
        /// 教卓。記憶 7 のリーは x=0 に立ち、記憶 13 の先生は x=0.45 から降りてくるので、
        /// 教壇の窓側へ寄せて二人の通り道を空ける
        /// </summary>
        static void ClassLectern(Bank b)
        {
            b.Box(new Vector3(-1.9f, 0.96f, 3.95f), new Vector3(1.16f, 0.06f, 0.56f));
            b.Box(new Vector3(-1.9f, 0.53f, 3.95f), new Vector3(1.02f, 0.80f, 0.46f));
        }

        /// <summary>
        /// 後ろの壁のロッカー。**仕切りの間隔が机の列と別の刻みで並ぶ**ので、
        /// 後ろを向いたときに奥行きの手掛かりがもう一つ出る
        /// </summary>
        static void ClassLockers(Bank b)
        {
            const float x0 = -3.5f;
            const float x1 = 1.6f;
            const float z = -3.835f;
            var wide = x1 - x0;
            var midx = (x0 + x1) * 0.5f;
            b.Box(new Vector3(midx, 0.525f, z), new Vector3(wide, 1.05f, 0.31f));
            b.Box(new Vector3(midx, 0.52f, z + 0.175f), new Vector3(wide, 0.05f, 0.06f));
            b.Box(new Vector3(midx, 1.075f, z), new Vector3(wide + 0.1f, 0.07f, 0.35f));
            for (var i = 0; i <= 10; i++)
                b.Box(new Vector3(x0 + i * (wide / 10f), 0.525f, z + 0.175f), new Vector3(0.05f, 1.05f, 0.06f));

            // 掲示板の枠。ロッカーの天板の上に載せて、後ろの壁に二段の帯を作る
            b.Box(new Vector3(-0.9f, 2.44f, -3.92f), new Vector3(2.92f, 0.08f, 0.07f));
            b.Box(new Vector3(-0.9f, 1.28f, -3.92f), new Vector3(2.92f, 0.08f, 0.07f));
            b.Box(new Vector3(-2.32f, 1.86f, -3.92f), new Vector3(0.08f, 1.24f, 0.07f));
            b.Box(new Vector3(0.52f, 1.86f, -3.92f), new Vector3(0.08f, 1.24f, 0.07f));
        }

        /// <summary>掲示板に留めた紙。高さと幅を少しずつ違えないと戸棚の扉に見える</summary>
        static void ClassNotices(Bank b)
        {
            var x = new[] { -2.12f, -1.68f, -1.16f, -0.62f, -0.06f, 0.34f };
            var y = new[] { 1.92f, 1.78f, 1.95f, 1.84f, 1.90f, 1.76f };
            var w = new[] { 0.34f, 0.38f, 0.30f, 0.40f, 0.32f, 0.26f };
            var h = new[] { 0.46f, 0.38f, 0.50f, 0.42f, 0.44f, 0.36f };
            for (var i = 0; i < x.Length; i++)
                b.Box(new Vector3(x[i], y[i], -3.897f), new Vector3(w[i], h[i], 0.012f));
        }

        /// <summary>
        /// 昼の光。
        ///
        /// **影を落とす灯りは使わない。** 壁も天井も内側だけを向いた一枚の面で、
        /// 影を落とさせても外から入る光を止められず、窓の四角も出ない。
        /// 床に落ちる四角は光る板で直に描き、灯りは面の陰影だけを作らせる
        /// </summary>
        static void ClassLight(Transform place)
        {
            var sky = Glow("GlowNoon", new Color(0.97f, 0.98f, 1f), 2.8f);
            for (var i = 0; i < 4; i++)
                Pane(place, "ClassWindow" + i,
                    new Vector3(ClassWest - 0.06f, (ClassPaneLow + ClassPaneTop) * 0.5f, ClassPaneAt(i)),
                    new Vector2(ClassPaneWide, ClassPaneTop - ClassPaneLow), Vector3.right, sky);

            // 窓の抜きを 42 度の日射しで床へ伸ばした先。ここが明るいと、
            // 机の脚と座った人の輪郭が影の底から出てくる
            var spill = Glow("GlowSpill", new Color(1f, 0.99f, 0.95f), 0.85f);
            for (var i = 0; i < 4; i++)
            {
                var z0 = ClassPaneAt(i) - 0.34f;
                // 教壇へ乗り上げるぶんは切る。板は床の高さにあるので、
                // はみ出したところは教壇の中へ潜って見えなくなる
                var z1 = Mathf.Min(ClassPaneAt(i) + 1.19f, 3.48f);
                Pane(place, "ClassSpill" + i, new Vector3(-2.2f, 0.012f, (z0 + z1) * 0.5f),
                    new Vector2(1.6f, z1 - z0), Vector3.up, spill);
            }

            // 蛍光灯は電車と同じ形だが、教室は倍の明るさで飛ばしたい。
            // GlowTube を共有すると電車の車内まで変わるので、こちらで一枚持つ
            var tube = Glow("GlowClassTube", new Color(0.95f, 0.97f, 1f), 2.2f);
            Pane(place, "ClassLamp0", new Vector3(0.4f, ClassHigh - 0.145f, 2.0f),
                new Vector2(5.9f, 0.24f), Vector3.down, tube);
            Pane(place, "ClassLamp1", new Vector3(0.4f, ClassHigh - 0.145f, -1.0f),
                new Vector2(5.9f, 0.24f), Vector3.down, tube);

            Pane(place, "ClassBoardLamp", new Vector3(0f, 2.565f, 4.50f), new Vector2(5.8f, 0.24f),
                new Vector3(0f, -0.72f, 0.70f), tube);

            var glass = Glow("GlowCorridor", new Color(0.92f, 0.94f, 1f), 0.5f);
            Pane(place, "ClassDoorGlass", new Vector3(4.695f, 1.58f, 3.05f),
                new Vector2(0.84f, 0.5f), Vector3.left, glass);

            Lamp(place, "Noon", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(42f, 72f, 0f), new Color(1f, 0.98f, 0.94f), 1.5f, 10f);
            Lamp(place, "Tube0", LightType.Point, new Vector3(0.4f, ClassHigh - 0.35f, 2.0f),
                Vector3.zero, new Color(0.93f, 0.96f, 1f), 6f, 12f);
            Lamp(place, "Tube1", LightType.Point, new Vector3(0.4f, ClassHigh - 0.35f, -1.0f),
                Vector3.zero, new Color(0.93f, 0.96f, 1f), 6f, 12f);
            Lamp(place, "Boardlamp", LightType.Point, new Vector3(0f, 2.5f, 4.3f),
                Vector3.zero, new Color(1f, 0.97f, 0.92f), 2.0f, 6f);
            // 後ろの壁は日射しの裏側になり、ロッカーと掲示板が影の底へ沈む。
            // 記憶 7 は四秒目にそちらを振り向くので、そこだけ起こす一つ
            Lamp(place, "Backwall", LightType.Point, new Vector3(-0.9f, 2.2f, -2.5f),
                Vector3.zero, new Color(0.96f, 0.95f, 0.92f), 3.5f, 7f);
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

        // ---- 教室だけの道具 ----------------------------------------------------

        /// <summary>
        /// 教室だけの面。<c>Tone</c> の表はどの場所も暗い前提で組んであり、
        /// あれを明るくすると他の四つまで持っていかれる。
        /// 昼の教室は床の照り返しで輪郭を拾わせる場所なので、床と紙はここで持つ
        /// </summary>
        static Material ClassMat(string name, Color col, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>マテリアルを名前ではなく現物で渡す <c>Emit</c>。上のマテリアルを通すため</summary>
        static Transform ClassEmit(Transform parent, string name, Bank bank, Material mat, bool collide)
        {
            var made = bank.Emit(parent, name, mat, collide, Generated);
            return made != null ? made.transform : null;
        }
    }
}
