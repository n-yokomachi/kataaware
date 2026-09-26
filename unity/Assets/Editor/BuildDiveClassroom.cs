using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の教室（設計書 9.1 節「classroom（教室）」「教室の作り込み」）。
    /// ロンドンの中等学校の、1960〜70 年代の校舎の一階の教室。3 月の 11:38〜11:39、昼前。
    ///
    /// **一目で分かる形は三つ。窓の列・机の列・ホワイトボード。**
    /// 等間隔に並ぶものとして、1.6 m ごとの窓と、その上半分に下ろしたブラインドの羽根、
    /// 格子の天井板と、升目に埋めた蛍光灯を置く。小物（筆記用具・ノート・教科書・先生の机のパソコンと書類・
    /// 掲示・時計・本棚・ごみ箱）はそのあと。
    /// ロッカーは廊下にあるので置かない。教壇と黒板も置かない（教卓は床と同じ高さ）。日本の学校に固有の物も置かない。
    ///
    /// **向き。** 場所のローカルで +z が前（ホワイトボード）、-x が窓の壁で南、+x が廊下。
    /// 生徒は前を向いて座り、窓は左手にある。
    ///
    /// **昼の光は本物の日の灯りで落とす**（台所と同じ作り）。日（Noon）は南のやや東、仰角 32 度から差す、影を落とす Directional。
    /// 教室の外側を影だけ落とす殻（<see cref="ClassShadowShell"/>）で囲い、窓の壁に窓の穴だけを開けておくと、
    /// 床と机に落ちる窓の四角・桟の影・ブラインドの羽根の縞が、空の絵の日と同じ向きで出る。
    /// 前は光る板を床に四枚寝かせて描いていた。
    ///
    /// **公営住宅と同じ三段で距離を作る。**
    /// <list type="table">
    /// <item><term>近く（このファイル）</term><description>教室の中</description></item>
    /// <item><term>中（<see cref="ClassYard"/>）</term><description>窓の外の舗装と、校庭の白線、葉の無いプラタナス</description></item>
    /// <item><term>遠く（<c>BuildDiveClassroomFar.cs</c>）</term><description>昼の空と、撮って貼る校庭の柵・向かいの校舎・木</description></item>
    /// </list>
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）と座る姿勢（<c>BuildDivePeople.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 目印（鍵打ちが見る） ------------------------------------------------

        /// <summary>ホワイトボードの面</summary>
        public const float BoardZ = 4.97f;
        /// <summary>
        /// 机の列の x と、行の z。記憶 14 と 15 はこの並びに座る。
        /// **前からある四列三行は動かせない。** 鍵打ちが添字で席を指しているので、
        /// 増やすぶんは末尾へ並べる。
        /// 二人掛けの机は 0 と 1、2 と 3 の列を一台で跨ぐ（<see cref="ClassDesks"/>）
        /// </summary>
        public static readonly float[] DeskX = { -2.4f, -0.8f, 0.8f, 2.4f, 4.0f };
        public static readonly float[] DeskZ = { 2.0f, 0.4f, -1.2f, -2.8f };
        /// <summary>机の天板。座る人はここから 0.55 m 後ろ</summary>
        public const float DeskY = 0.72f;

        // ---- 寸法 ----------------------------------------------------------------

        /// <summary>吊った天井の高さ</summary>
        const float ClassHigh = 2.85f;
        // 箱の四隅。窓側は動かさない。窓を動かすと記憶 8・14・15 が「窓の外が白い」と振り向く先がずれる
        const float ClassWest = -4f;
        const float ClassEast = 4.8f;
        const float ClassBack = -4f;
        const float ClassFore = 5f;
        /// <summary>窓の壁の厚み。窓の抱きと、日の影を落とす殻の厚み</summary>
        const float ClassSkin = 0.25f;

        // 窓の抜き。**1.6 m の繰り返しがこの場所をいちばん早く言う**ので、
        // 幅より間隔を先に決めて、余りを桟の太さにしてある
        const float ClassPaneStep = 1.6f;
        const float ClassPaneWide = 1.35f;
        const float ClassPaneLow = 0.95f;
        const float ClassPaneTop = 2.45f;
        /// <summary>腰の塗り分けの高さ。窓台と揃える</summary>
        const float ClassDadoY = ClassPaneLow;
        /// <summary>半分下ろしたブラインドの下端</summary>
        const float ClassBlindY = 1.72f;

        // ホワイトボード
        const float BoardX0 = -1.8f;
        const float BoardX1 = 1.8f;
        const float BoardY0 = 0.95f;
        const float BoardY1 = 2.15f;

        // 時計は前の壁の廊下側。ホワイトボードの上の腕（プロジェクター）から離す
        const float ClockX = 3.1f;
        const float ClockY = 2.30f;

        // 廊下側の戸
        const float ClassDoorZ0 = 2.52f;
        const float ClassDoorZ1 = 3.58f;
        const float ClassDoorHigh = 2.05f;

        /// <summary>
        /// 先生の机の真ん中。前の窓側。記憶 8 のリーは x=0 に立ち、記憶 14 の先生は x=0.45 から降りてくるので、
        /// 二人の通り道から外して窓の方へ寄せる
        /// </summary>
        static readonly Vector3 ClassTeacherDesk = new Vector3(-2.55f, 0f, 3.55f);

        /// <summary>
        /// 昼の日の向き（Euler）。**空の絵の日もこの向きに描く**（<see cref="ClassroomSunward"/>）。
        /// 3 月の頭のロンドンの 11:38 は、日が南から 8 度ほど東、仰角 31 度。
        /// 窓台（0.95）の光は窓から 1.5 m、窓の上端（2.45）の光は 3.9 m の所に落ちるが、
        /// 上半分はブラインドの羽根が縞に刻む
        /// </summary>
        static readonly Vector3 ClassNoonAim = new Vector3(32f, 82f, 0f);

        /// <summary>i 枚目の窓の中心。四枚を壁の真ん中に揃える</summary>
        static float ClassPaneAt(int i)
        {
            return (ClassBack + ClassFore) * 0.5f + (i - 1.5f) * ClassPaneStep;
        }

        /// <summary>素材ごとの入れ物。台所と同じく束ねて持ち回る（<see cref="KitchenBanks"/>）</summary>
        sealed class ClassBanks
        {
            // 当たりを入れる面
            /// <summary>床のビニル</summary>
            public readonly Bank Lino = new Bank { Texel = 0.5f };
            /// <summary>腰から上の壁。明るいクリーム</summary>
            public readonly Bank Paint = new Bank { Texel = 0.35f };
            /// <summary>腰から下の壁。濃い青緑</summary>
            public readonly Bank Dado = new Bank { Texel = 0.35f };
            // 当たりを入れない面
            /// <summary>天井板</summary>
            public readonly Bank Tile = new Bank { Texel = 0.35f };
            /// <summary>白。ホワイトボード・窓の枠・窓台・ラジエーター・ブラインド・プロジェクター・時計の文字盤・天井の格子</summary>
            public readonly Bank White = new Bank { Texel = 0.5f };
            /// <summary>金物。机と椅子の脚・ホワイトボードの縁とマーカーの受け・戸の金物・ラジエーターの溝</summary>
            public readonly Bank Metal = new Bank { Texel = 0.5f };
            /// <summary>生徒の机の天板。明るいブナの化粧板</summary>
            public readonly Bank Beech = new Bank { Texel = 0.5f };
            /// <summary>木。先生の机・本棚・戸・掲示板の枠</summary>
            public readonly Bank Oak = new Bank { Texel = 0.5f };
            /// <summary>青。樹脂の椅子・青のマーカーの字・教科書・掲示板の布</summary>
            public readonly Bank Blue = new Bank { Texel = 0.5f };
            /// <summary>紙。ノート・書類・掲示</summary>
            public readonly Bank Paper = new Bank { Texel = 0.5f };
            /// <summary>黒。マーカーの字・時計の縁と針・パソコン・先生の椅子・ごみ箱</summary>
            public readonly Bank Black = new Bank { Texel = 0.5f };
            public readonly Bank Red = new Bank { Texel = 0.5f };
            public readonly Bank Yellow = new Bank { Texel = 0.5f };
            /// <summary>緑。後ろの掲示板の布・非常口の札・教科書</summary>
            public readonly Bank Moss = new Bank { Texel = 0.5f };
            /// <summary>自分で光る面。天井の蛍光灯</summary>
            public readonly Bank Glow = new Bank { Texel = 0.5f };
            /// <summary>影だけを落とす殻（<see cref="ClassShadowShell"/>）</summary>
            public readonly Bank Shade = new Bank { Texel = 0.2f };
        }

        // ---- 組み立て --------------------------------------------------------------

        /// <summary>記憶 8・14・15 の舞台</summary>
        static void Classroom(Transform place)
        {
            var b = new ClassBanks();
            // 近く
            ClassShell(b);
            ClassWindows(b);
            ClassCeilingGrid(b);
            ClassWhiteboard(b);
            ClassDesks(b);
            ClassTeacher(b);
            ClassDoor(b);
            ClassBoards(b);
            ClassClock(b);
            ClassShelf(b);
            ClassBins(b);
            ClassShadowShell(b);

            // 地の色。教室にしか無い色（床・壁・腰・机の天板）だけ新しく持ち、あとは公営住宅と台所と分け合う
            var lino = EstatePaint("ClassLino", new Color(0.440f, 0.455f, 0.440f), 0.30f);
            var paint = EstatePaint("ClassWall", new Color(0.640f, 0.620f, 0.550f), 0.05f);
            var dado = EstatePaint("ClassDado", new Color(0.120f, 0.215f, 0.245f), 0.15f);
            var beech = EstatePaint("ClassBeech", new Color(0.620f, 0.500f, 0.350f), 0.25f);

            EstateEmit(place, "ClassFloor", b.Lino, lino, true);
            EstateEmit(place, "ClassWall", b.Paint, paint, true);
            EstateEmit(place, "ClassDado", b.Dado, dado, true);
            EstateEmit(place, "ClassCeiling", b.Tile, KitchenShared("EstateCeil"), false);
            EstateEmit(place, "ClassWhite", b.White, KitchenShared("EstateFrame"), false);
            EstateEmit(place, "ClassMetal", b.Metal, Mat("Rail"), false);
            EstateEmit(place, "ClassDesk", b.Beech, beech, false);
            EstateEmit(place, "ClassOak", b.Oak, KitchenShared("KitchenOak"), false);
            EstateEmit(place, "ClassBlue", b.Blue, KitchenShared("EstateBlue"), false);
            EstateEmit(place, "ClassPaper", b.Paper, KitchenShared("EstatePaper"), false);
            EstateEmit(place, "ClassBlack", b.Black, Mat("Ceiling"), false);
            EstateEmit(place, "ClassRed", b.Red, KitchenShared("EstateRed"), false);
            EstateEmit(place, "ClassYellow", b.Yellow, KitchenShared("EstateYellow"), false);
            EstateEmit(place, "ClassMoss", b.Moss, KitchenShared("EstateMoss"), false);
            NoShadow(EstateEmit(place, "ClassGlow", b.Glow, Glow("GlowClassTube", new Color(0.95f, 0.97f, 1f), 2.0f), false));
            var shell = EstateEmit(place, "ClassShade", b.Shade, Mat("Wall"), false);
            if (shell != null) shell.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // 戸の縦長の窓の向こうの廊下。廊下は組まず、明るい面を一枚だけ
            Pane(place, "ClassDoorGlass", new Vector3(ClassEast - 0.058f, 1.45f, 3.27f), new Vector2(0.16f, 0.72f), Vector3.left,
                Glow("GlowCorridor", new Color(0.92f, 0.94f, 1f), 0.6f));

            // 中（窓の外の舗装と校庭）
            var y = new YardBanks();
            ClassYard(y);
            y.Emit(place, "Class");

            // 遠く（本物の地面の縁と、撮って貼った書き割りの輪）
            FarLand(place, ClassroomRing, "ClassroomLand", ClassYardMat());
            Backdrop(place, ClassroomRing, "HalfAware/Shoot the classroom backdrop");

            ClassBlocks(place);
            ClassLights(place);
        }

        /// <summary>校庭の舗装。晴れた昼の日に白く照り返す、乾いた明るいアスファルト</summary>
        static Material ClassYardMat()
        {
            return EstatePaint("ClassYard", new Color(0.400f, 0.400f, 0.390f), 0.06f);
        }

        /// <summary>
        /// 灯り。昼の日と、空の青い光の代わりと、天井の蛍光灯と、床の照り返し。
        ///
        /// 環境光は空の絵から取った三色（<see cref="ClassroomPlaceSky"/>）が受け持つ。
        /// 蛍光灯は点けたまま。昼でも教室の奥（廊下側）は窓から遠く、天井の灯りが無いと沈む
        /// </summary>
        static void ClassLights(Transform place)
        {
            var sun = Lamp(place, "Noon", LightType.Directional, new Vector3(0f, 12f, 0f),
                ClassNoonAim, new Color(1f, 0.96f, 0.88f), 2.8f, 10f);
            sun.shadows = LightShadows.Soft;

            // 窓から入る空の光は灯りにしない。窓の側から spot で入れると、円錐の縁が天井に斜めの帯を描いた。
            // 空の光は環境光（空の絵から取った三色）と、下の ClassFill が受け持つ

            // 日と反対の空（北の高い所）から来る青い光の代わり。影は落とさない。
            // 日を背にした外の物（木の幹・向こうを向いた面）が、窓から黒い影だけに見えないように
            Lamp(place, "ClassFill", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(40f, 262f, 0f), new Color(0.70f, 0.78f, 0.94f), 0.45f, 10f);

            // 天井の蛍光灯。升目の三列を一つずつ受け持つ。点の灯りにすると天井が白く飛ぶので、下へ向けた広い spot
            for (var i = 0; i < 3; i++)
            {
                var tube = Lamp(place, "Tube" + i, LightType.Spot, new Vector3(0.5f, ClassHigh - 0.1f, ClassTroffZ[i]),
                    new Vector3(90f, 0f, 0f), new Color(0.94f, 0.96f, 1f), 3.6f, 7f);
                tube.spotAngle = 150f;
                tube.innerSpotAngle = 70f;
            }

            // 床の照り返し。日の四角と明るい床から天井と壁の上の方へ返る、少し暖かい光。
            // 窓の際に置くと、円錐の縁が窓の壁の上を斜めの帯になって這ったので、部屋の真ん中から上へ向ける
            var bounce = Lamp(place, "SunBounce", LightType.Spot, new Vector3(-0.2f, 0.1f, 0.5f),
                new Vector3(-90f, 0f, 0f), new Color(1f, 0.96f, 0.90f), 2.2f, 6.0f);
            bounce.spotAngle = 170f;
            bounce.innerSpotAngle = 110f;
        }

        // ---- 殻 ------------------------------------------------------------------

        /// <summary>
        /// 床・壁・天井。腰から下は濃い色、上はクリームに塗り分ける。
        /// 壁の厚みは窓の壁だけに持たせ、あとは内側の面を一枚張る。外から見る所は無い
        /// </summary>
        static void ClassShell(ClassBanks b)
        {
            b.Lino.FaceY(0f, ClassWest, ClassEast, ClassBack, ClassFore, 1);

            // 前・後ろ・廊下側。廊下側の戸は抜かずに壁へ重ねる（抜くと向こうに廊下を組むことになる）
            b.Dado.FaceZ(ClassFore, ClassWest, ClassEast, 0f, ClassDadoY, -1);
            b.Paint.FaceZ(ClassFore, ClassWest, ClassEast, ClassDadoY, ClassHigh, -1);
            b.Dado.FaceZ(ClassBack, ClassWest, ClassEast, 0f, ClassDadoY, 1);
            b.Paint.FaceZ(ClassBack, ClassWest, ClassEast, ClassDadoY, ClassHigh, 1);
            b.Dado.FaceX(ClassEast, ClassBack, ClassFore, 0f, ClassDadoY, -1);
            b.Paint.FaceX(ClassEast, ClassBack, ClassFore, ClassDadoY, ClassHigh, -1);

            // 窓の壁。腰は窓台の高さまで、その上に窓の穴を抜く
            var holes = new List<Vector4>();
            for (var i = 0; i < 4; i++)
            {
                var c = ClassPaneAt(i);
                holes.Add(new Vector4(c - ClassPaneWide * 0.5f, c + ClassPaneWide * 0.5f, ClassPaneLow, ClassPaneTop));
            }
            b.Dado.FaceX(ClassWest, ClassBack, ClassFore, 0f, ClassDadoY, 1);
            b.Paint.FaceXHoles(ClassWest, ClassBack, ClassFore, ClassDadoY, ClassHigh, 1, holes);

            // 腰の見切り。塗り分けの線を細い白い帯で押さえる。窓の壁は窓台が兼ねる。
            // **壁の面とぴったり同じ高さで終わらせない。** 同じ平面に二枚あると破線になって現れる
            const float rail = 0.04f;
            b.White.Box(new Vector3((ClassWest + ClassEast) * 0.5f, ClassDadoY, ClassFore - 0.012f), new Vector3(ClassEast - ClassWest, rail, 0.024f));
            // 後ろの壁だけは表の面一枚にする。日は南のやや東から来て後ろの壁をかすめるので、
            // 箱の上の面が殻の影の境に掛かり、影の地図の目の粗さで点々と光った
            b.White.FaceZ(ClassBack + 0.012f, ClassWest, ClassEast, ClassDadoY - rail * 0.5f, ClassDadoY + rail * 0.5f, 1);
            b.White.Box(new Vector3(ClassEast - 0.012f, ClassDadoY, (ClassBack + ClassDoorZ0 - 0.07f) * 0.5f), new Vector3(0.024f, rail, ClassDoorZ0 - 0.07f - ClassBack));
            b.White.Box(new Vector3(ClassEast - 0.012f, ClassDadoY, (ClassDoorZ1 + 0.07f + ClassFore) * 0.5f), new Vector3(0.024f, rail, ClassFore - ClassDoorZ1 - 0.07f));
            // 幅木。床のビニルを壁へ巻き上げた黒い帯
            const float skirt = 0.08f;
            b.Black.FaceZ(ClassFore - 0.004f, ClassWest, ClassEast, 0f, skirt, -1);
            b.Black.FaceZ(ClassBack + 0.004f, ClassWest, ClassEast, 0f, skirt, 1);
            b.Black.FaceX(ClassEast - 0.004f, ClassBack, ClassDoorZ0 - 0.07f, 0f, skirt, -1);
            b.Black.FaceX(ClassEast - 0.004f, ClassDoorZ1 + 0.07f, ClassFore, 0f, skirt, -1);
            b.Black.FaceX(ClassWest + 0.004f, ClassBack, ClassFore, 0f, skirt, 1);

            b.Tile.FaceY(ClassHigh, ClassWest, ClassEast, ClassBack, ClassFore, -1);
        }

        /// <summary>
        /// 窓を四枚。抱きと上枠、外寄りに白い金属の枠（中の縦桟と、上の突き出し窓の横桟）、
        /// 通しの窓台、下に長いラジエーター、上半分に下ろしたブラインド。
        /// **硝子は入れない**（公営住宅・台所と同じ）。昼の光と外の景色をそのまま通す
        /// </summary>
        static void ClassWindows(ClassBanks b)
        {
            const float x0 = ClassWest - ClassSkin;
            const float x1 = ClassWest;
            const float fx = ClassWest - 0.16f;
            const float w = 0.05f;
            const float mid = (ClassPaneLow + ClassPaneTop) * 0.5f;
            const float high = ClassPaneTop - ClassPaneLow;
            const float vent = 2.02f;
            for (var i = 0; i < 4; i++)
            {
                var c = ClassPaneAt(i);
                var z0 = c - ClassPaneWide * 0.5f;
                var z1 = c + ClassPaneWide * 0.5f;
                // 抱きと上枠と下の水切り
                b.Paint.FaceZ(z0, x0, x1, ClassPaneLow, ClassPaneTop, 1);
                b.Paint.FaceZ(z1, x0, x1, ClassPaneLow, ClassPaneTop, -1);
                b.Paint.FaceY(ClassPaneTop, x0, x1, z0, z1, -1);
                b.White.FaceY(ClassPaneLow, x0, x1, z0, z1, 1);
                // 枠。外周・中の縦桟・突き出し窓の横桟
                b.White.Box(new Vector3(fx, ClassPaneTop - w * 0.5f, c), new Vector3(0.06f, w, ClassPaneWide));
                b.White.Box(new Vector3(fx, ClassPaneLow + w * 0.5f, c), new Vector3(0.06f, w, ClassPaneWide));
                b.White.Box(new Vector3(fx, mid, z0 + w * 0.5f), new Vector3(0.06f, high, w));
                b.White.Box(new Vector3(fx, mid, z1 - w * 0.5f), new Vector3(0.06f, high, w));
                b.White.Box(new Vector3(fx, mid, c), new Vector3(0.06f, high, 0.06f));
                b.White.Box(new Vector3(fx, vent, c), new Vector3(0.06f, 0.05f, ClassPaneWide));
                b.Metal.Box(new Vector3(fx + 0.05f, vent - 0.08f, c - 0.3f), new Vector3(0.03f, 0.02f, 0.10f));
                ClassBlind(b, c);
            }
            // 通しの窓台
            var from = ClassPaneAt(0) - ClassPaneWide * 0.5f - 0.35f;
            var to = ClassPaneAt(3) + ClassPaneWide * 0.5f + 0.35f;
            b.White.Box(new Vector3(ClassWest + 0.09f, ClassPaneLow - 0.015f, (from + to) * 0.5f), new Vector3(0.20f, 0.03f, to - from));
            // 長いラジエーター。窓の列の下いっぱいに一本
            ClassRadiator(b, ClassWest + 0.05f, ClassPaneAt(0) - 0.55f, ClassPaneAt(3) + 0.55f);
        }

        /// <summary>
        /// 半分下ろしたブラインド。羽根を 4.5 cm ごとに、少し開けて傾ける。
        /// 日は羽根の間を抜けて、床の四角の奥の半分を縞に刻む
        /// </summary>
        static void ClassBlind(ClassBanks b, float c)
        {
            const float x = ClassWest + 0.05f;
            var wide = ClassPaneWide + 0.08f;
            b.White.Box(new Vector3(x, ClassPaneTop + 0.035f, c), new Vector3(0.05f, 0.05f, wide + 0.02f));
            // 羽根は 5 cm 幅を 4.5 cm ごと。細くすると 1/3 の解像度で途切れた点線になる
            var tilt = Quaternion.Euler(0f, 0f, -40f);
            for (var y = ClassPaneTop - 0.03f; y > ClassBlindY + 0.03f; y -= 0.045f)
                b.White.Box(new Vector3(x, y, c), new Vector3(0.05f, 0.003f, wide), tilt);
            b.White.Box(new Vector3(x, ClassBlindY, c), new Vector3(0.03f, 0.018f, wide));
            // 紐と引き手
            b.Metal.Box(new Vector3(x + 0.01f, (ClassPaneTop + ClassBlindY) * 0.5f - 0.1f, c + wide * 0.5f - 0.04f),
                new Vector3(0.004f, ClassPaneTop - ClassBlindY + 0.2f, 0.004f));
            b.White.Box(new Vector3(x + 0.01f, ClassBlindY - 0.22f, c + wide * 0.5f - 0.04f), new Vector3(0.018f, 0.05f, 0.018f));
        }

        /// <summary>パネルのラジエーター。x が一定の窓の壁の前。縦の溝（地と近い色にする。濃いと縞が揺れて見える）、両脇に床へ下りる管</summary>
        static void ClassRadiator(ClassBanks b, float x, float z0, float z1)
        {
            b.White.Box(new Vector3(x + 0.03f, 0.45f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.56f, z1 - z0));
            for (var z = z0 + 0.06f; z < z1 - 0.03f; z += 0.06f)
                b.Paper.FaceX(x + 0.061f, z - 0.004f, z + 0.004f, 0.22f, 0.68f, 1);
            b.Metal.Box(new Vector3(x + 0.04f, 0.09f, z0 + 0.04f), new Vector3(0.025f, 0.18f, 0.025f));
            b.Metal.Box(new Vector3(x + 0.04f, 0.09f, z1 - 0.04f), new Vector3(0.025f, 0.18f, 0.025f));
        }

        /// <summary>蛍光灯の升の行（z）。三行を等間隔に</summary>
        static readonly float[] ClassTroffZ = { 3.2f, 0.8f, -1.6f };
        /// <summary>蛍光灯の升の列（x）。三列を等間隔に</summary>
        static readonly float[] ClassTroffX = { -1.9f, 0.5f, 2.9f };

        /// <summary>
        /// 格子の天井。60 cm 角の天井板の目地を白い T の桟で引き、升目に埋めた蛍光灯（60×120 cm）を三列三行。
        /// **等間隔に並ぶ灯りの升が、窓の列と並んでこの場所を言う。**
        /// 升の中にルーバーの桟を二本通して、光る板が一枚の白い四角にならないようにする
        /// </summary>
        static void ClassCeilingGrid(ClassBanks b)
        {
            const float y = ClassHigh - 0.004f;
            const float t = 0.012f;
            for (var x = ClassWest + 0.6f; x < ClassEast - 0.05f; x += 0.6f)
                b.White.FaceY(y, x - t, x + t, ClassBack, ClassFore, -1);
            for (var z = ClassBack + 0.6f; z < ClassFore - 0.05f; z += 0.6f)
                b.White.FaceY(y, ClassWest, ClassEast, z - t, z + t, -1);
            foreach (var cx in ClassTroffX)
                foreach (var cz in ClassTroffZ)
                {
                    b.Glow.FaceY(ClassHigh - 0.008f, cx - 0.28f, cx + 0.28f, cz - 0.58f, cz + 0.58f, -1);
                    b.White.Box(new Vector3(cx, ClassHigh - 0.02f, cz), new Vector3(0.012f, 0.03f, 1.16f));
                    b.White.Box(new Vector3(cx, ClassHigh - 0.02f, cz - 0.29f), new Vector3(0.56f, 0.03f, 0.012f));
                    b.White.Box(new Vector3(cx, ClassHigh - 0.02f, cz + 0.29f), new Vector3(0.56f, 0.03f, 0.012f));
                    b.White.Box(new Vector3(cx, ClassHigh - 0.02f, cz), new Vector3(0.56f, 0.03f, 0.012f));
                }
        }

        // ---- 前 ------------------------------------------------------------------

        /// <summary>
        /// ホワイトボード。白い板・金物の縁・マーカーの受けと三本のマーカーと黒板消し、
        /// 書いてある字と図、上の壁から腕で吊ったプロジェクター。
        /// 記憶 8 のリーは板の真ん中（x=0）の前で書いていて、最後にマーカーを取り直すので、
        /// マーカーはその手元の受けに置く
        /// </summary>
        static void ClassWhiteboard(ClassBanks b)
        {
            var cy = (BoardY0 + BoardY1) * 0.5f;
            var wide = BoardX1 - BoardX0;
            var high = BoardY1 - BoardY0;
            b.White.Box(new Vector3(0f, cy, (BoardZ + ClassFore) * 0.5f), new Vector3(wide, high, ClassFore - BoardZ));
            // 縁とマーカーの受け
            b.Metal.Box(new Vector3(0f, BoardY1 + 0.015f, BoardZ + 0.01f), new Vector3(wide + 0.06f, 0.03f, 0.04f));
            b.Metal.Box(new Vector3(BoardX0 - 0.015f, cy, BoardZ + 0.01f), new Vector3(0.03f, high + 0.06f, 0.04f));
            b.Metal.Box(new Vector3(BoardX1 + 0.015f, cy, BoardZ + 0.01f), new Vector3(0.03f, high + 0.06f, 0.04f));
            b.Metal.Box(new Vector3(0f, BoardY0 - 0.015f, BoardZ - 0.035f), new Vector3(wide + 0.06f, 0.02f, 0.11f));
            b.Metal.Box(new Vector3(0f, BoardY0 + 0.0f, BoardZ - 0.087f), new Vector3(wide + 0.06f, 0.03f, 0.008f));
            // マーカー三本（黒・青・赤）と黒板消し
            ClassMarker(b.Black, new Vector3(0.12f, BoardY0 - 0.006f, BoardZ - 0.05f), 4f);
            ClassMarker(b.Blue, new Vector3(0.30f, BoardY0 - 0.006f, BoardZ - 0.045f), -6f);
            ClassMarker(b.Red, new Vector3(0.46f, BoardY0 - 0.006f, BoardZ - 0.05f), 2f);
            b.Black.Box(new Vector3(0.72f, BoardY0 + 0.012f, BoardZ - 0.045f), new Vector3(0.13f, 0.03f, 0.05f));
            b.Blue.Box(new Vector3(0.72f, BoardY0 + 0.032f, BoardZ - 0.045f), new Vector3(0.13f, 0.012f, 0.05f));

            ClassBoardInk(b);

            // プロジェクター。壁の上から腕を出し、板の手前の上に吊る
            b.Metal.Box(new Vector3(0f, 2.60f, ClassFore - 0.01f), new Vector3(0.16f, 0.22f, 0.02f));
            b.Metal.Box(new Vector3(0f, 2.60f, 4.62f), new Vector3(0.05f, 0.05f, 0.74f));
            b.Metal.Box(new Vector3(0f, 2.555f, 4.26f), new Vector3(0.05f, 0.06f, 0.05f));
            b.White.Box(new Vector3(0f, 2.47f, 4.26f), new Vector3(0.36f, 0.11f, 0.30f));
            b.Black.Box(new Vector3(0.09f, 2.46f, 4.415f), new Vector3(0.09f, 0.07f, 0.015f));
            b.Black.Box(new Vector3(-0.08f, 2.47f, 4.414f), new Vector3(0.10f, 0.04f, 0.012f));
        }

        static void ClassMarker(Bank cap, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            cap.Box(at + Vector3.up * 0.01f, new Vector3(0.13f, 0.018f, 0.018f), rot);
        }

        /// <summary>
        /// 板に書いてある字と図。左は青の字の行（見出しと、赤の下線）、
        /// 真ん中は黒の座標の軸に赤の山なりの線、右は三角形と角の印。
        /// 字は読めなくてよい。語の長さの違う短い線を行に並べ、手で書いた行の揺れを少し入れる。
        /// 記憶 15 のマテオには遠くがぼやけて読めない
        /// </summary>
        static void ClassBoardInk(ClassBanks b)
        {
            const float z = BoardZ - 0.0015f;
            var rnd = new System.Random(38);
            // 見出しと下線
            ClassWords(b.Blue, rnd, -1.66f, -0.72f, 2.02f, 0.050f, z);
            ClassStroke(b.Red, new Vector2(-1.66f, 1.975f), new Vector2(-0.78f, 1.972f), 0.014f, z);
            // 字の行
            var rows = new[] { 1.88f, 1.79f, 1.70f, 1.61f, 1.46f, 1.37f, 1.28f, 1.19f };
            for (var i = 0; i < rows.Length; i++)
            {
                var end = -0.30f - (float)rnd.NextDouble() * 0.45f;
                ClassWords(b.Blue, rnd, -1.62f + (i == 4 ? 0.0f : 0.04f), end, rows[i], 0.038f, z);
            }
            // リーが書きかけていた行。板の真ん中の手元まで
            ClassWords(b.Blue, rnd, -0.20f, 0.02f, 1.10f, 0.032f, z);

            // 座標の軸と山なりの線
            var o = new Vector2(0.28f, 1.14f);
            ClassStroke(b.Black, o, o + new Vector2(0.86f, 0f), 0.014f, z);
            ClassStroke(b.Black, o, o + new Vector2(0f, 0.86f), 0.014f, z);
            ClassStroke(b.Black, o + new Vector2(0.86f, 0f), o + new Vector2(0.83f, 0.02f), 0.012f, z);
            ClassStroke(b.Black, o + new Vector2(0.86f, 0f), o + new Vector2(0.83f, -0.02f), 0.012f, z);
            ClassStroke(b.Black, o + new Vector2(0f, 0.86f), o + new Vector2(-0.02f, 0.83f), 0.012f, z);
            ClassStroke(b.Black, o + new Vector2(0f, 0.86f), o + new Vector2(0.02f, 0.83f), 0.012f, z);
            for (var k = 1; k <= 4; k++)
            {
                ClassStroke(b.Black, o + new Vector2(k * 0.18f, -0.018f), o + new Vector2(k * 0.18f, 0.018f), 0.010f, z);
                ClassStroke(b.Black, o + new Vector2(-0.018f, k * 0.18f), o + new Vector2(0.018f, k * 0.18f), 0.010f, z);
            }
            var last = o + new Vector2(0.05f, 0.08f);
            for (var k = 1; k <= 12; k++)
            {
                var t = k / 12f;
                var p = o + new Vector2(0.05f + t * 0.74f, 0.08f + 0.66f * (1f - (2f * t - 1f) * (2f * t - 1f)));
                ClassStroke(b.Red, last, p, 0.014f, z);
                last = p;
            }
            ClassWords(b.Blue, rnd, 0.30f, 0.62f, 2.04f, 0.032f, z);

            // 三角形と角の印、その下の短い式
            var a = new Vector2(1.20f, 1.30f);
            var c = new Vector2(1.70f, 1.30f);
            var d = new Vector2(1.20f, 1.86f);
            ClassStroke(b.Black, a, c, 0.014f, z);
            ClassStroke(b.Black, a, d, 0.014f, z);
            ClassStroke(b.Black, d, c, 0.014f, z);
            ClassStroke(b.Red, a + new Vector2(0.07f, 0f), a + new Vector2(0.07f, 0.07f), 0.010f, z);
            ClassStroke(b.Red, a + new Vector2(0f, 0.07f), a + new Vector2(0.07f, 0.07f), 0.010f, z);
            ClassWords(b.Blue, rnd, 1.22f, 1.72f, 1.18f, 0.032f, z);
            ClassWords(b.Blue, rnd, 1.22f, 1.60f, 1.09f, 0.032f, z);
        }

        /// <summary>語の長さの違う短い線を一行。x0 から x1 まで、高さ y の真ん中に</summary>
        static void ClassWords(Bank ink, System.Random rnd, float x0, float x1, float y, float high, float z)
        {
            var x = x0;
            while (x < x1)
            {
                var len = Mathf.Min(0.04f + (float)rnd.NextDouble() * 0.13f, x1 - x);
                if (len < 0.02f) break;
                var dy = ((float)rnd.NextDouble() - 0.5f) * 0.006f;
                ink.FaceZ(z, x, x + len, y - high * 0.5f + dy, y + high * 0.5f + dy, -1);
                x += len + 0.035f + (float)rnd.NextDouble() * 0.02f;
            }
        }

        /// <summary>板の面に一画。a から c まで太さ w</summary>
        static void ClassStroke(Bank ink, Vector2 a, Vector2 c, float w, float z)
        {
            var d = c - a;
            var len = d.magnitude;
            if (len < 1e-4f) return;
            var ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var mid = (a + c) * 0.5f;
            ink.Box(new Vector3(mid.x, mid.y, z), new Vector3(len + w, w, 0.002f), Quaternion.Euler(0f, 0f, ang));
        }

        // ---- 机の列 --------------------------------------------------------------

        /// <summary>
        /// 二人掛けの机と樹脂の椅子。0 と 1、2 と 3 の列を一台の机で跨ぎ、4 の列は廊下の壁際に短い二人掛けを置く。
        /// 席（椅子）は <see cref="DeskX"/> の列の上から動かさない。記憶 14・15 の二人（2 と 3 の列の二行目）は
        /// 同じ机に並ぶ隣どうし。
        /// 机の上に筆記用具・ノート・教科書。手を挙げる生徒（アイシャ）の机には開いたノート、
        /// 伏せて寝る隣（マテオ）の机は、伏せた腕に掛からないよう奥の縁に教科書と筆箱だけ
        /// </summary>
        static void ClassDesks(ClassBanks b)
        {
            var rnd = new System.Random(1138);
            for (var r = 0; r < DeskZ.Length; r++)
            {
                var z = DeskZ[r];
                ClassTable(b, DeskX[0] - 0.32f, DeskX[1] + 0.32f, z);
                ClassTable(b, DeskX[2] - 0.32f, DeskX[3] + 0.32f, z);
                ClassTable(b, DeskX[4] - 0.60f, DeskX[4] + 0.60f, z);
                for (var c = 0; c < 4; c++)
                {
                    ClassChair(b, new Vector3(DeskX[c], 0f, z - 0.56f));
                    if (r == 1 && (c == 2 || c == 3)) continue;
                    ClassDeskThings(b, new Vector3(DeskX[c], 0f, z), rnd);
                }
                for (var s = 0; s < 2; s++)
                {
                    var x = DeskX[4] + (s == 0 ? -0.29f : 0.29f);
                    ClassChair(b, new Vector3(x, 0f, z - 0.56f));
                    if (rnd.NextDouble() < 0.6) ClassDeskThings(b, new Vector3(x, 0f, z), rnd);
                }
            }

            // アイシャの机。開いたノートに字の行、その脇にペンと閉じた教科書
            var aisha = new Vector3(DeskX[2], 0f, DeskZ[1]);
            ClassOpenBook(b, aisha + new Vector3(0.02f, 0f, 0.02f), 4f, true);
            b.Black.Box(aisha + new Vector3(0.24f, DeskY + 0.026f, 0.02f), new Vector3(0.14f, 0.01f, 0.01f), Quaternion.Euler(0f, 28f, 0f));
            b.Moss.Box(aisha + new Vector3(-0.26f, DeskY + 0.033f, 0.10f), new Vector3(0.19f, 0.026f, 0.25f), Quaternion.Euler(0f, -8f, 0f));
            // マテオの机。奥の縁に寄せる
            var mateo = new Vector3(DeskX[3], 0f, DeskZ[1]);
            b.Red.Box(mateo + new Vector3(0.18f, DeskY + 0.033f, 0.12f), new Vector3(0.19f, 0.026f, 0.25f), Quaternion.Euler(0f, 12f, 0f));
            b.Blue.Box(mateo + new Vector3(-0.16f, DeskY + 0.045f, 0.17f), new Vector3(0.20f, 0.05f, 0.07f), Quaternion.Euler(0f, -5f, 0f));
        }

        /// <summary>机を一台。天板は x0 から x1、奥行き 0.46。灰色の鋼管の脚と、奥の幕板</summary>
        static void ClassTable(ClassBanks b, float x0, float x1, float z)
        {
            var cx = (x0 + x1) * 0.5f;
            var wide = x1 - x0;
            b.Beech.Box(new Vector3(cx, DeskY, z), new Vector3(wide, 0.04f, 0.46f));
            b.Metal.Box(new Vector3(cx, DeskY - 0.022f, z - 0.232f), new Vector3(wide, 0.006f, 0.006f));
            const float leg = DeskY - 0.02f;
            for (var i = 0; i < 4; i++)
                b.Metal.Box(new Vector3(i % 2 == 0 ? x0 + 0.04f : x1 - 0.04f, leg * 0.5f, z + (i < 2 ? -0.19f : 0.19f)),
                    new Vector3(0.03f, leg, 0.03f));
            // 脚の間の貫と、奥の幕板
            b.Metal.Box(new Vector3(x0 + 0.04f, 0.62f, z), new Vector3(0.025f, 0.03f, 0.38f));
            b.Metal.Box(new Vector3(x1 - 0.04f, 0.62f, z), new Vector3(0.025f, 0.03f, 0.38f));
            b.Metal.Box(new Vector3(cx, 0.60f, z + 0.19f), new Vector3(wide - 0.08f, 0.14f, 0.012f));
        }

        /// <summary>
        /// 樹脂の椅子を一脚。座と背を一続きの青い殻、鋼管の四本脚。
        /// 座と背の位置は前の椅子と同じにしてある（座る姿勢が見ている）
        /// </summary>
        static void ClassChair(ClassBanks b, Vector3 seat)
        {
            b.Blue.Box(seat + new Vector3(0f, 0.44f, 0f), new Vector3(0.40f, 0.04f, 0.40f));
            b.Blue.Box(seat + new Vector3(0f, 0.68f, -0.17f), new Vector3(0.40f, 0.34f, 0.025f), Quaternion.Euler(-8f, 0f, 0f));
            b.Blue.Box(seat + new Vector3(0f, 0.50f, -0.19f), new Vector3(0.36f, 0.10f, 0.02f), Quaternion.Euler(-30f, 0f, 0f));
            for (var i = 0; i < 4; i++)
                b.Metal.Box(seat + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.17f, 0.21f, (i < 2 ? -1f : 1f) * 0.16f),
                    new Vector3(0.022f, 0.42f, 0.022f));
        }

        /// <summary>席一つぶんの机の上。開いたノートか閉じた練習帳、教科書、筆箱、ペン。席ごとに揺らす</summary>
        static void ClassDeskThings(ClassBanks b, Vector3 desk, System.Random rnd)
        {
            var yaw = ((float)rnd.NextDouble() - 0.5f) * 16f;
            var roll = rnd.NextDouble();
            if (roll < 0.55)
                ClassOpenBook(b, desk + new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.1f, 0f, -0.02f), yaw, false);
            else
                b.Blue.Box(desk + new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.1f, DeskY + 0.025f, 0f),
                    new Vector3(0.17f, 0.008f, 0.23f), Quaternion.Euler(0f, yaw, 0f));
            if (rnd.NextDouble() < 0.7)
            {
                var cover = rnd.Next(4);
                var bank = cover == 0 ? b.Red : cover == 1 ? b.Moss : cover == 2 ? b.Yellow : b.Blue;
                var side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                bank.Box(desk + new Vector3(side * 0.24f, DeskY + 0.033f, 0.08f), new Vector3(0.19f, 0.026f, 0.25f),
                    Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 20f, 0f));
            }
            if (rnd.NextDouble() < 0.6)
            {
                var bank = rnd.NextDouble() < 0.5 ? b.Black : b.Red;
                bank.Box(desk + new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.3f, DeskY + 0.045f, 0.17f), new Vector3(0.20f, 0.05f, 0.07f),
                    Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 20f, 0f));
            }
            b.Black.Box(desk + new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.3f, DeskY + 0.026f, -0.12f), new Vector3(0.14f, 0.01f, 0.01f),
                Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f));
        }

        /// <summary>開いたノート。見開きの白い紙と、真ん中の綴じ目。<paramref name="lines"/> なら字の行を書き込む</summary>
        static void ClassOpenBook(ClassBanks b, Vector3 desk, float yaw, bool lines)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var at = desk + new Vector3(0f, DeskY + 0.024f, 0f);
            b.Blue.Box(at + Vector3.down * 0.002f, new Vector3(0.35f, 0.004f, 0.24f), rot);
            b.Paper.Box(at + Vector3.up * 0.002f, new Vector3(0.33f, 0.004f, 0.22f), rot);
            b.Metal.Box(at + Vector3.up * 0.0045f, new Vector3(0.004f, 0.001f, 0.22f), rot);
            if (!lines) return;
            for (var i = 0; i < 7; i++)
            {
                var zz = 0.08f - i * 0.026f;
                var len = i == 6 ? 0.07f : 0.12f;
                b.Blue.Box(at + rot * new Vector3(-0.155f + len * 0.5f + 0.01f, 0.0046f, zz), new Vector3(len, 0.001f, 0.006f), rot);
                if (i < 4) b.Blue.Box(at + rot * new Vector3(0.02f + 0.06f, 0.0046f, zz), new Vector3(0.12f, 0.001f, 0.006f), rot);
            }
        }

        // ---- 先生の机 ------------------------------------------------------------

        /// <summary>
        /// 先生の机と椅子。床と同じ高さ（教壇は置かない）。机は生徒の側に幕板、先生の側に引き出しの袖。
        /// 上にパソコンの画面とキーボード、書類の束、ファイル、マグカップとペン立て
        /// </summary>
        static void ClassTeacher(ClassBanks b)
        {
            var o = ClassTeacherDesk;
            const float wide = 1.40f;
            const float deep = 0.70f;
            b.Oak.Box(o + new Vector3(0f, DeskY, 0f), new Vector3(wide, 0.04f, deep));
            b.Oak.Box(o + new Vector3(0f, 0.46f, -deep * 0.5f + 0.02f), new Vector3(wide - 0.04f, 0.48f, 0.02f));
            b.Oak.Box(o + new Vector3(-wide * 0.5f + 0.02f, 0.35f, 0f), new Vector3(0.03f, 0.70f, deep - 0.02f));
            // 袖の引き出し
            const float px = 0.44f;
            b.Oak.Box(o + new Vector3(px, 0.35f, 0f), new Vector3(0.44f, 0.70f, deep - 0.04f));
            for (var k = 0; k < 3; k++)
            {
                var y = 0.14f + k * 0.21f;
                b.Metal.Box(o + new Vector3(px, y + 0.06f, deep * 0.5f - 0.015f), new Vector3(0.12f, 0.015f, 0.015f));
                b.Black.FaceZ(o.z + deep * 0.5f - 0.019f, o.x + px - 0.22f, o.x + px + 0.22f, y - 0.012f, y - 0.004f, 1);
            }
            // 画面は先生の側を向く。生徒からは黒い背中
            var top = DeskY + 0.02f;
            b.Black.Box(o + new Vector3(-0.15f, top + 0.006f, -0.12f), new Vector3(0.22f, 0.012f, 0.16f));
            b.Black.Box(o + new Vector3(-0.15f, top + 0.12f, -0.14f), new Vector3(0.04f, 0.22f, 0.03f));
            b.Black.Box(o + new Vector3(-0.15f, top + 0.33f, -0.12f), new Vector3(0.54f, 0.33f, 0.035f));
            b.Blue.FaceZ(o.z - 0.12f + 0.0176f, o.x - 0.15f - 0.25f, o.x - 0.15f + 0.25f, top + 0.185f, top + 0.475f, 1);
            b.Black.Box(o + new Vector3(-0.15f, top + 0.01f, 0.14f), new Vector3(0.44f, 0.02f, 0.14f));
            b.Black.Box(o + new Vector3(0.14f, top + 0.012f, 0.16f), new Vector3(0.06f, 0.024f, 0.10f));
            // 書類の束とファイル
            for (var k = 0; k < 6; k++)
                b.Paper.Box(o + new Vector3(0.42f + (k % 2) * 0.01f, top + 0.004f + k * 0.008f, 0.02f - (k % 3) * 0.01f),
                    new Vector3(0.21f, 0.006f, 0.297f), Quaternion.Euler(0f, (k * 37 % 11) - 5f, 0f));
            b.Red.Box(o + new Vector3(0.40f, top + 0.07f, -0.22f), new Vector3(0.24f, 0.03f, 0.31f), Quaternion.Euler(0f, 6f, 0f));
            b.Paper.Box(o + new Vector3(0.13f, top + 0.004f, -0.05f), new Vector3(0.297f, 0.006f, 0.21f), Quaternion.Euler(0f, -12f, 0f));
            // マグカップとペン立て
            b.Yellow.Box(o + new Vector3(0.58f, top + 0.05f, 0.24f), new Vector3(0.08f, 0.10f, 0.08f));
            b.Black.Box(o + new Vector3(-0.56f, top + 0.05f, 0.20f), new Vector3(0.07f, 0.10f, 0.07f));
            b.Red.Box(o + new Vector3(-0.56f, top + 0.13f, 0.20f), new Vector3(0.008f, 0.08f, 0.008f), Quaternion.Euler(0f, 0f, 10f));
            b.Blue.Box(o + new Vector3(-0.55f, top + 0.13f, 0.21f), new Vector3(0.008f, 0.08f, 0.008f), Quaternion.Euler(8f, 0f, -6f));

            // 先生の椅子。机の窓側の奥、黒い布張りの回る椅子
            var seat = o + new Vector3(-0.15f, 0f, deep * 0.5f + 0.42f);
            b.Black.Box(seat + new Vector3(0f, 0.47f, 0f), new Vector3(0.48f, 0.07f, 0.46f));
            b.Black.Box(seat + new Vector3(0f, 0.80f, 0.24f), new Vector3(0.44f, 0.50f, 0.06f), Quaternion.Euler(-6f, 0f, 0f));
            b.Metal.Box(seat + new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.40f, 0.05f));
            for (var k = 0; k < 5; k++)
            {
                var dir = Quaternion.Euler(0f, k * 72f + 10f, 0f) * Vector3.forward;
                b.Black.Box(seat + dir * 0.17f + Vector3.up * 0.05f, new Vector3(0.04f, 0.03f, 0.34f), Quaternion.LookRotation(dir));
            }
        }

        // ---- 壁 ------------------------------------------------------------------

        /// <summary>
        /// 廊下側の窓付きの戸。壁へ重ねた木の戸に、縦長の覗き窓（向こうの明かりは <c>ClassDoorGlass</c>）、
        /// 白い枠、レバーの取っ手と蹴り板。戸の上に緑の非常口の札
        /// </summary>
        static void ClassDoor(ClassBanks b)
        {
            const float x = ClassEast;
            var mid = (ClassDoorZ0 + ClassDoorZ1) * 0.5f;
            var wide = ClassDoorZ1 - ClassDoorZ0;
            b.Oak.Box(new Vector3(x - 0.02f, ClassDoorHigh * 0.5f, mid), new Vector3(0.04f, ClassDoorHigh, wide - 0.08f));
            // 覗き窓の枠
            b.Metal.Box(new Vector3(x - 0.045f, 1.45f, 3.27f - 0.095f), new Vector3(0.02f, 0.78f, 0.03f));
            b.Metal.Box(new Vector3(x - 0.045f, 1.45f, 3.27f + 0.095f), new Vector3(0.02f, 0.78f, 0.03f));
            b.Metal.Box(new Vector3(x - 0.045f, 1.825f, 3.27f), new Vector3(0.02f, 0.03f, 0.22f));
            b.Metal.Box(new Vector3(x - 0.045f, 1.075f, 3.27f), new Vector3(0.02f, 0.03f, 0.22f));
            // 枠
            b.White.Box(new Vector3(x - 0.025f, ClassDoorHigh + 0.04f, mid), new Vector3(0.05f, 0.08f, wide + 0.06f));
            b.White.Box(new Vector3(x - 0.025f, (ClassDoorHigh + 0.08f) * 0.5f, ClassDoorZ0 - 0.005f), new Vector3(0.05f, ClassDoorHigh + 0.08f, 0.07f));
            b.White.Box(new Vector3(x - 0.025f, (ClassDoorHigh + 0.08f) * 0.5f, ClassDoorZ1 + 0.005f), new Vector3(0.05f, ClassDoorHigh + 0.08f, 0.07f));
            // 取っ手と蹴り板
            b.Metal.Box(new Vector3(x - 0.06f, 1.0f, ClassDoorZ0 + 0.16f), new Vector3(0.05f, 0.05f, 0.05f));
            b.Metal.Box(new Vector3(x - 0.085f, 1.0f, ClassDoorZ0 + 0.22f), new Vector3(0.02f, 0.022f, 0.14f));
            b.Metal.Box(new Vector3(x - 0.043f, 0.13f, mid), new Vector3(0.004f, 0.24f, wide - 0.14f));
            // 非常口の札
            b.Moss.Box(new Vector3(x - 0.012f, 2.30f, mid), new Vector3(0.02f, 0.14f, 0.38f));
            b.Paper.FaceX(x - 0.0225f, mid - 0.12f, mid - 0.02f, 2.25f, 2.35f, -1);
            b.Paper.FaceX(x - 0.0225f, mid + 0.02f, mid + 0.14f, 2.29f, 2.31f, -1);
        }

        /// <summary>
        /// 掲示。後ろの壁に布を張った掲示板を二枚（生徒の作品）、廊下側の壁に一枚（時間割と注意書き）。
        /// 紙は高さと幅を少しずつ違え、色の違う絵と字の行を入れる。揃えると戸棚の扉に見える
        /// </summary>
        static void ClassBoards(ClassBanks b)
        {
            var rnd = new System.Random(316);
            // 後ろの壁。窓側の板は美術の絵、廊下側の板は作文と地図
            ClassPinBoardZ(b, b.Moss, ClassBack, -3.40f, -0.90f, 1.10f, 2.25f);
            ClassPinBoardZ(b, b.Blue, ClassBack, -0.40f, 2.40f, 1.10f, 2.25f);
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 6; c++)
                {
                    var x = -3.22f + c * 0.40f + ((float)rnd.NextDouble() - 0.5f) * 0.05f;
                    var y = 1.95f - r * 0.36f + ((float)rnd.NextDouble() - 0.5f) * 0.04f;
                    if (x > -1.05f) continue;
                    ClassPictureZ(b, rnd, x, y, 0.26f, 0.30f);
                }
            for (var r = 0; r < 2; r++)
                for (var c = 0; c < 6; c++)
                {
                    var x = -0.16f + c * 0.44f + ((float)rnd.NextDouble() - 0.5f) * 0.04f;
                    var y = 1.87f - r * 0.46f + ((float)rnd.NextDouble() - 0.5f) * 0.04f;
                    ClassEssayZ(b, rnd, x, y);
                }
            // 見出しの帯
            b.Yellow.FaceZ(ClassBack + 0.022f, -2.80f, -1.50f, 2.12f, 2.20f, 1);
            b.Paper.FaceZ(ClassBack + 0.022f, 0.35f, 1.65f, 2.12f, 2.20f, 1);

            // 廊下側の壁。時間割と注意書き
            const float wx = ClassEast;
            ClassPinBoardX(b, b.Red, wx, -2.40f, 0.60f, 1.10f, 2.10f);
            // 時間割。紙に升目
            b.Paper.FaceX(wx - 0.022f, -2.20f, -1.10f, 1.30f, 1.95f, -1);
            for (var k = 0; k <= 5; k++)
                b.Blue.FaceX(wx - 0.023f, -2.20f + k * 0.22f - 0.003f, -2.20f + k * 0.22f + 0.003f, 1.30f, 1.95f, -1);
            for (var k = 0; k <= 6; k++)
                b.Blue.FaceX(wx - 0.023f, -2.20f, -1.10f, 1.30f + k * 0.108f - 0.003f, 1.30f + k * 0.108f + 0.003f, -1);
            for (var k = 0; k < 5; k++)
                for (var j = 0; j < 6; j++)
                    if (rnd.NextDouble() < 0.7)
                    {
                        var zc = -2.20f + k * 0.22f + 0.11f;
                        var yc = 1.30f + j * 0.108f + 0.054f;
                        var bank = rnd.NextDouble() < 0.3 ? b.Red : b.Black;
                        bank.FaceX(wx - 0.024f, zc - 0.07f, zc + 0.05f, yc - 0.01f, yc + 0.01f, -1);
                    }
            // 注意書き。火災の時の手順（青の札）と、教室の決まり（赤の見出し）
            b.Blue.FaceX(wx - 0.022f, -0.90f, -0.60f, 1.45f, 1.87f, -1);
            for (var k = 0; k < 6; k++)
                b.Paper.FaceX(wx - 0.023f, -0.87f, -0.63f - (k % 3) * 0.04f, 1.78f - k * 0.05f, 1.795f - k * 0.05f, -1);
            b.Paper.FaceX(wx - 0.022f, -0.40f, 0.40f, 1.25f, 1.95f, -1);
            b.Red.FaceX(wx - 0.023f, -0.36f, 0.36f, 1.80f, 1.90f, -1);
            for (var k = 0; k < 7; k++)
                b.Black.FaceX(wx - 0.023f, -0.34f, 0.10f + (float)rnd.NextDouble() * 0.24f, 1.70f - k * 0.06f, 1.715f - k * 0.06f, -1);
        }

        /// <summary>z が一定の壁の掲示板。布と木の枠</summary>
        static void ClassPinBoardZ(ClassBanks b, Bank felt, float z, float x0, float x1, float y0, float y1)
        {
            var s = z < 0f ? 1f : -1f;
            var sign = z < 0f ? 1 : -1;
            felt.FaceZ(z + s * 0.012f, x0, x1, y0, y1, sign);
            b.Oak.Box(new Vector3((x0 + x1) * 0.5f, y1 + 0.02f, z + s * 0.015f), new Vector3(x1 - x0 + 0.08f, 0.04f, 0.03f));
            b.Oak.Box(new Vector3((x0 + x1) * 0.5f, y0 - 0.02f, z + s * 0.015f), new Vector3(x1 - x0 + 0.08f, 0.04f, 0.03f));
            b.Oak.Box(new Vector3(x0 - 0.02f, (y0 + y1) * 0.5f, z + s * 0.015f), new Vector3(0.04f, y1 - y0, 0.03f));
            b.Oak.Box(new Vector3(x1 + 0.02f, (y0 + y1) * 0.5f, z + s * 0.015f), new Vector3(0.04f, y1 - y0, 0.03f));
        }

        /// <summary>x が一定の壁の掲示板。布と木の枠</summary>
        static void ClassPinBoardX(ClassBanks b, Bank felt, float x, float z0, float z1, float y0, float y1)
        {
            felt.FaceX(x - 0.012f, z0, z1, y0, y1, -1);
            b.Oak.Box(new Vector3(x - 0.015f, y1 + 0.02f, (z0 + z1) * 0.5f), new Vector3(0.03f, 0.04f, z1 - z0 + 0.08f));
            b.Oak.Box(new Vector3(x - 0.015f, y0 - 0.02f, (z0 + z1) * 0.5f), new Vector3(0.03f, 0.04f, z1 - z0 + 0.08f));
            b.Oak.Box(new Vector3(x - 0.015f, (y0 + y1) * 0.5f, z0 - 0.02f), new Vector3(0.03f, y1 - y0, 0.04f));
            b.Oak.Box(new Vector3(x - 0.015f, (y0 + y1) * 0.5f, z1 + 0.02f), new Vector3(0.03f, y1 - y0, 0.04f));
        }

        /// <summary>生徒の絵を一枚。白い紙に、色の違う形を二つ三つ</summary>
        static void ClassPictureZ(ClassBanks b, System.Random rnd, float x, float y, float w, float h)
        {
            const float z = ClassBack + 0.020f;
            b.Paper.FaceZ(z, x - w * 0.5f, x + w * 0.5f, y - h * 0.5f, y + h * 0.5f, 1);
            var banks = new[] { b.Red, b.Yellow, b.Blue, b.Moss, b.Black };
            var n = 2 + rnd.Next(2);
            for (var k = 0; k < n; k++)
            {
                var pw = w * (0.25f + (float)rnd.NextDouble() * 0.45f);
                var ph = h * (0.20f + (float)rnd.NextDouble() * 0.40f);
                var px = x + ((float)rnd.NextDouble() - 0.5f) * (w - pw) * 0.9f;
                var py = y + ((float)rnd.NextDouble() - 0.5f) * (h - ph) * 0.9f;
                banks[rnd.Next(banks.Length)].FaceZ(z + 0.001f + k * 0.0005f, px - pw * 0.5f, px + pw * 0.5f, py - ph * 0.5f, py + ph * 0.5f, 1);
            }
        }

        /// <summary>作文を一枚。白い紙に黒の字の行と、色の見出し</summary>
        static void ClassEssayZ(ClassBanks b, System.Random rnd, float x, float y)
        {
            const float z = ClassBack + 0.020f;
            const float w = 0.21f;
            const float h = 0.297f;
            b.Paper.FaceZ(z, x - w * 0.5f, x + w * 0.5f, y - h * 0.5f, y + h * 0.5f, 1);
            var head = rnd.Next(3) == 0 ? b.Red : b.Blue;
            head.FaceZ(z + 0.001f, x - w * 0.4f, x + w * 0.2f, y + h * 0.36f, y + h * 0.42f, 1);
            for (var k = 0; k < 9; k++)
            {
                var end = x + w * (0.25f + (float)rnd.NextDouble() * 0.15f);
                b.Black.FaceZ(z + 0.001f, x - w * 0.4f, end, y + h * 0.26f - k * 0.027f, y + h * 0.26f - k * 0.027f + 0.006f, 1);
            }
        }

        /// <summary>
        /// 時計。前の壁の廊下側。**11 時 38 分**（記憶 8・14 の時刻）。
        /// 黒い縁の丸い文字盤に、十二の目盛りと二本の針
        /// </summary>
        static void ClassClock(ClassBanks b)
        {
            var c = new Vector3(ClockX, ClockY, ClassFore);
            KitchenRound(b.Black, c + Vector3.back * 0.030f, Vector3.right, Vector3.up, 0.18f, 24, Vector3.back);
            KitchenRound(b.White, c + Vector3.back * 0.034f, Vector3.right, Vector3.up, 0.155f, 24, Vector3.back);
            for (var k = 0; k < 24; k++)
            {
                var a0 = k * Mathf.PI * 2f / 24f;
                var a1 = (k + 1) * Mathf.PI * 2f / 24f;
                // 縁の側面。壁から浮いた円盤に見せる
                var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.18f;
                var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.18f;
                b.Black.Quad(p1, p0, p0 + Vector3.back * 0.03f, p1 + Vector3.back * 0.03f);
            }
            const float face = ClassFore - 0.035f;
            for (var k = 0; k < 12; k++)
            {
                var th = k * 30f;
                var dir = new Vector3(Mathf.Sin(th * Mathf.Deg2Rad), Mathf.Cos(th * Mathf.Deg2Rad), 0f);
                var len = k % 3 == 0 ? 0.035f : 0.02f;
                b.Black.Box(new Vector3(c.x, c.y, face) + dir * (0.135f - len * 0.5f), new Vector3(0.008f, len, 0.002f), Quaternion.Euler(0f, 0f, -th));
            }
            // 針。前から見て右回り（+x が右）。短い針は 11 時 38 分で 349 度、長い針は 38 分で 228 度
            ClassHand(b.Black, new Vector3(c.x, c.y, face - 0.003f), 349f, 0.085f, 0.012f);
            ClassHand(b.Black, new Vector3(c.x, c.y, face - 0.005f), 228f, 0.125f, 0.008f);
            b.Red.Box(new Vector3(c.x, c.y, face - 0.007f), new Vector3(0.015f, 0.015f, 0.002f));
        }

        static void ClassHand(Bank bank, Vector3 centre, float clockwise, float len, float wide)
        {
            var dir = new Vector3(Mathf.Sin(clockwise * Mathf.Deg2Rad), Mathf.Cos(clockwise * Mathf.Deg2Rad), 0f);
            bank.Box(centre + dir * (len * 0.5f - 0.015f), new Vector3(wide, len + 0.03f, 0.002f), Quaternion.Euler(0f, 0f, -clockwise));
        }

        /// <summary>本棚。後ろの壁の廊下側の隅。三段に教科書と辞書、段ごとに背の高さと色を揃えずに</summary>
        static void ClassShelf(ClassBanks b)
        {
            const float x0 = 2.75f;
            const float x1 = 4.55f;
            const float z0 = ClassBack;
            const float z1 = ClassBack + 0.32f;
            const float top = 1.20f;
            var cx = (x0 + x1) * 0.5f;
            var cz = (z0 + z1) * 0.5f;
            b.Oak.Box(new Vector3(x0 + 0.01f, top * 0.5f, cz), new Vector3(0.02f, top, z1 - z0));
            b.Oak.Box(new Vector3(x1 - 0.01f, top * 0.5f, cz), new Vector3(0.02f, top, z1 - z0));
            b.Oak.Box(new Vector3(cx, top - 0.01f, cz), new Vector3(x1 - x0, 0.02f, z1 - z0));
            b.Oak.Box(new Vector3(cx, 0.05f, cz), new Vector3(x1 - x0, 0.10f, z1 - z0));
            b.Oak.Box(new Vector3(cx, top * 0.5f, z0 + 0.005f), new Vector3(x1 - x0, top, 0.01f));
            var shelves = new[] { 0.10f, 0.47f, 0.84f };
            var rnd = new System.Random(77);
            var banks = new[] { b.Red, b.Blue, b.Yellow, b.Moss, b.Paper, b.Black, b.Blue, b.Red };
            for (var s = 0; s < shelves.Length; s++)
            {
                if (s > 0) b.Oak.Box(new Vector3(cx, shelves[s] - 0.01f, cz), new Vector3(x1 - x0 - 0.04f, 0.02f, z1 - z0 - 0.01f));
                var x = x0 + 0.03f;
                while (x < x1 - 0.08f)
                {
                    var w = 0.022f + (float)rnd.NextDouble() * 0.035f;
                    var h = 0.20f + (float)rnd.NextDouble() * 0.10f;
                    var d = 0.17f + (float)rnd.NextDouble() * 0.06f;
                    if (rnd.NextDouble() < 0.06) { x += 0.08f; continue; }
                    banks[rnd.Next(banks.Length)].Box(new Vector3(x + w * 0.5f, shelves[s] + h * 0.5f, z1 - 0.02f - d * 0.5f), new Vector3(w, h, d));
                    x += w + 0.002f;
                }
            }
            // 天板の上に積んだ教科書と、地球儀の代わりの箱の束は置かない。目の高さの上は掲示板に譲る
            b.Blue.Box(new Vector3(x0 + 0.35f, top + 0.03f, cz), new Vector3(0.22f, 0.04f, 0.28f));
            b.Red.Box(new Vector3(x0 + 0.35f, top + 0.07f, cz), new Vector3(0.20f, 0.04f, 0.26f), Quaternion.Euler(0f, 8f, 0f));
        }

        /// <summary>ごみ箱と、青い資源ごみの箱。前の廊下側の隅</summary>
        static void ClassBins(ClassBanks b)
        {
            b.Black.Box(new Vector3(4.45f, 0.20f, 4.55f), new Vector3(0.30f, 0.40f, 0.30f));
            b.Metal.Box(new Vector3(4.45f, 0.41f, 4.55f), new Vector3(0.32f, 0.02f, 0.32f));
            b.Blue.Box(new Vector3(4.05f, 0.25f, 4.62f), new Vector3(0.32f, 0.50f, 0.28f));
            b.Paper.Box(new Vector3(4.05f, 0.505f, 4.62f), new Vector3(0.22f, 0.01f, 0.18f), Quaternion.Euler(0f, 14f, 0f));
        }

        // ---- 影の殻 ----------------------------------------------------------------

        /// <summary>
        /// 影だけを落とす殻。窓の壁（窓の穴だけ開ける）・後ろの壁・前の壁・屋根を厚い箱で囲う。
        ///
        /// 部屋の壁は内側の面を一枚張っているだけなので、外から来る日の影を落とせない（裏を向いた面は影の勘定から外れる）。
        /// 描かない殻を外に被せて、日の光が教室に入るのは窓からだけにする（台所の <see cref="KitchenShadowShell"/> と同じ）。
        /// 日は南（-x）のやや東（-z）の上から来るので、廊下側（+x）の面は要らない
        /// </summary>
        static void ClassShadowShell(ClassBanks b)
        {
            const float w0 = ClassWest - ClassSkin - 0.05f;
            const float w1 = ClassWest - ClassSkin;
            const float s0 = ClassBack - 0.3f;
            const float n0 = ClassFore + 0.3f;
            const float top = ClassHigh + 0.4f;
            // 窓の壁の外。窓の穴の上下と、穴の間
            KitchenSolid(b.Shade, w0 - 0.25f, w1, -0.1f, ClassPaneLow, s0, n0);
            KitchenSolid(b.Shade, w0 - 0.25f, w1, ClassPaneTop, top, s0, n0);
            var edge = s0;
            for (var i = 0; i < 4; i++)
            {
                var c = ClassPaneAt(i);
                KitchenSolid(b.Shade, w0 - 0.25f, w1, ClassPaneLow, ClassPaneTop, edge, c - ClassPaneWide * 0.5f);
                edge = c + ClassPaneWide * 0.5f;
            }
            KitchenSolid(b.Shade, w0 - 0.25f, w1, ClassPaneLow, ClassPaneTop, edge, n0);
            // 後ろ・前の壁と屋根
            KitchenSolid(b.Shade, w0 - 0.25f, ClassEast + 0.3f, -0.1f, top, s0, ClassBack);
            KitchenSolid(b.Shade, w0 - 0.25f, ClassEast + 0.3f, -0.1f, top, ClassFore, n0);
            KitchenSolid(b.Shade, w0 - 0.25f, ClassEast + 0.3f, ClassHigh, top, s0, n0);
        }

        // ---- 当たり ----------------------------------------------------------------

        /// <summary>
        /// 大きな家具の当たり。机は一台ずつ箱で囲い、椅子には入れない（座った人の足と重なる）。
        /// 座る席（机の手前 0.56 m）と、記憶 8 のリーが机の向こうで屈む所（机の奥の縁から 0.12 m）には掛からない奥行きにしてある
        /// </summary>
        static void ClassBlocks(Transform place)
        {
            for (var r = 0; r < DeskZ.Length; r++)
            {
                var z = DeskZ[r];
                Fence(place, "ClassTableA" + r, new Vector3((DeskX[0] + DeskX[1]) * 0.5f, DeskY * 0.5f, z), new Vector3(DeskX[1] - DeskX[0] + 0.64f, DeskY + 0.02f, 0.40f));
                Fence(place, "ClassTableB" + r, new Vector3((DeskX[2] + DeskX[3]) * 0.5f, DeskY * 0.5f, z), new Vector3(DeskX[3] - DeskX[2] + 0.64f, DeskY + 0.02f, 0.40f));
                Fence(place, "ClassTableC" + r, new Vector3(DeskX[4], DeskY * 0.5f, z), new Vector3(1.2f, DeskY + 0.02f, 0.40f));
            }
            Fence(place, "ClassTeacherBlock", ClassTeacherDesk + new Vector3(0f, DeskY * 0.5f, 0f), new Vector3(1.4f, DeskY + 0.02f, 0.70f));
            Fence(place, "ClassShelfBlock", new Vector3(3.65f, 0.62f, ClassBack + 0.16f), new Vector3(1.8f, 1.24f, 0.32f));
            Fence(place, "ClassBinBlock", new Vector3(4.25f, 0.25f, 4.58f), new Vector3(0.75f, 0.5f, 0.4f));
            // 窓の列の下のラジエーターと窓の穴。窓台は腰より高いが、走り込んだときに外へ抜けないように
            var from = ClassPaneAt(0) - ClassPaneWide * 0.5f - 0.35f;
            var to = ClassPaneAt(3) + ClassPaneWide * 0.5f + 0.35f;
            Fence(place, "ClassRadiatorBlock", new Vector3(ClassWest + 0.06f, 0.48f, (from + to) * 0.5f), new Vector3(0.12f, 0.96f, to - from));
            for (var i = 0; i < 4; i++)
                Fence(place, "ClassSashBlock" + i, new Vector3(ClassWest - ClassSkin * 0.5f, (ClassPaneLow + ClassPaneTop) * 0.5f, ClassPaneAt(i)),
                    new Vector3(ClassSkin, ClassPaneTop - ClassPaneLow, ClassPaneWide));
        }

        // ---- 中の層（窓の外） ------------------------------------------------------

        /// <summary>
        /// 窓の外。校舎の際の敷石の帯、その先の校庭の白線（ネットボールのコート）と黄色の石蹴り、
        /// 校庭の脇に葉の無いプラタナス。地面そのものは遠い地面（<see cref="FarLand"/>）が兼ねる。
        /// 日は窓の向こうの正面にあるので、外は逆光の明るさで白く飛び、木は輪郭だけが残る
        /// </summary>
        static void ClassYard(YardBanks y)
        {
            const float wall = ClassWest - ClassSkin;
            const float pave = -6.6f;
            const float g = -0.045f;
            // 敷石の帯と目地
            y.Kerb.FaceY(-0.01f, pave, wall, -14f, 15f, 1);
            for (var z = -14f + 0.9f; z < 15f; z += 0.9f)
                y.Tarmac.FaceY(-0.008f, pave, wall, z - 0.012f, z + 0.012f, 1);
            for (var x = wall - 0.9f; x > pave; x -= 0.9f)
                y.Tarmac.FaceY(-0.008f, x - 0.012f, x + 0.012f, -14f, 15f, 1);
            y.Kerb.Box(new Vector3(pave, -0.02f, 0.5f), new Vector3(0.12f, 0.06f, 29f));

            // ネットボールのコート。15.25 × 30.5 m を窓と平行に
            const float cx0 = -9.2f;
            const float cx1 = cx0 - 15.25f;
            const float cz0 = -14.75f;
            const float cz1 = 15.75f;
            // 線は本当は 5 cm だが、10〜25 m 先の 5 cm は 1/3 の解像度で点に割れる
            const float lw = 0.10f;
            y.Line.FaceY(g, cx1, cx0, cz0, cz0 + lw, 1);
            y.Line.FaceY(g, cx1, cx0, cz1 - lw, cz1, 1);
            y.Line.FaceY(g, cx0 - lw, cx0, cz0, cz1, 1);
            y.Line.FaceY(g, cx1, cx1 + lw, cz0, cz1, 1);
            var third = (cz1 - cz0) / 3f;
            y.Line.FaceY(g, cx1, cx0, cz0 + third - lw * 0.5f, cz0 + third + lw * 0.5f, 1);
            y.Line.FaceY(g, cx1, cx0, cz1 - third - lw * 0.5f, cz1 - third + lw * 0.5f, 1);
            var mx = (cx0 + cx1) * 0.5f;
            var mz = (cz0 + cz1) * 0.5f;
            ClassYardArc(y.Line, new Vector3(mx, g, mz), 0.45f, 0f, 360f, lw, 16);
            // ゴールの半円。ゴールの線（両端）から 4.9 m
            ClassYardArc(y.Line, new Vector3(mx, g, cz0), 4.9f, -90f, 90f, lw, 20);
            ClassYardArc(y.Line, new Vector3(mx, g, cz1), 4.9f, 90f, 270f, lw, 20);

            // 石蹴り。校舎の際の近く、黄色の升を十
            var hop = new[] { 0, 1, 0, 1, 0, 1, 0, 1 };
            for (var k = 0; k < 8; k++)
            {
                var z0 = -4.5f + k * 0.5f;
                if (hop[k] == 0)
                    ClassYardSquare(y.Yellow, -7.6f, z0, 0.5f, g);
                else
                {
                    ClassYardSquare(y.Yellow, -7.35f, z0, 0.5f, g);
                    ClassYardSquare(y.Yellow, -7.85f, z0, 0.5f, g);
                }
            }

            // 校庭の脇のプラタナス。窓の正面は空けて、両脇と奥の角に
            YardPlane(y, new Vector3(-7.8f, 0f, -11.0f), 10.5f, 431);
            YardPlane(y, new Vector3(-8.2f, 0f, 12.5f), 11.5f, 433);
            YardPlane(y, new Vector3(-27.0f, 0f, -9.5f), 12.0f, 437);
            YardPlane(y, new Vector3(-27.5f, 0f, 8.0f), 12.5f, 439);
        }

        /// <summary>地面に引く線の弧。a0 から a1 まで（度、+z が 0 で東回り）</summary>
        static void ClassYardArc(Bank bank, Vector3 centre, float radius, float a0, float a1, float wide, int steps)
        {
            for (var k = 0; k < steps; k++)
            {
                var t0 = Mathf.Lerp(a0, a1, k / (float)steps) * Mathf.Deg2Rad;
                var t1 = Mathf.Lerp(a0, a1, (k + 1) / (float)steps) * Mathf.Deg2Rad;
                var p0 = centre + new Vector3(Mathf.Sin(t0), 0f, Mathf.Cos(t0)) * radius;
                var p1 = centre + new Vector3(Mathf.Sin(t1), 0f, Mathf.Cos(t1)) * radius;
                var d = p1 - p0;
                bank.Box((p0 + p1) * 0.5f, new Vector3(wide, 0.002f, d.magnitude + wide), Quaternion.LookRotation(d, Vector3.up));
            }
        }

        /// <summary>地面に引く四角の枠</summary>
        static void ClassYardSquare(Bank bank, float cx, float z0, float size, float y)
        {
            const float w = 0.04f;
            var h = size * 0.5f;
            bank.FaceY(y, cx - h, cx + h, z0, z0 + w, 1);
            bank.FaceY(y, cx - h, cx + h, z0 + size - w, z0 + size, 1);
            bank.FaceY(y, cx - h, cx - h + w, z0, z0 + size, 1);
            bank.FaceY(y, cx + h - w, cx + h, z0, z0 + size, 1);
        }
    }
}
