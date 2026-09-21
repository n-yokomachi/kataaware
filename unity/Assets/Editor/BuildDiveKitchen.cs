using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の台所。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// 等間隔に並ぶものを一つ入れると、その場所らしさがいちばん早く伝わる。
    ///
    /// 台所を決めるのは三つ。**流し台と吊り戸棚**、**窓から床へ落ちる朝の四角い光**、
    /// **階段**。光の四角だけは他のどの場所にも無いので、これが場所の名札になる。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 手掛かりになる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 台所 ------------------------------------------------------------

        /// <summary>台所と玄関を分ける壁。戸口はここに開く</summary>
        public const float KitchenDoorZ = 0f;
        /// <summary>流しの前。記憶 5 のマークが皿を洗っている</summary>
        public static readonly Vector3 Sink = new Vector3(-1.1f, 0f, 2.35f);
        /// <summary>玄関のドアの前</summary>
        public static readonly Vector3 Entrance = new Vector3(-1.95f, 0f, -1.95f);
        /// <summary>階段の下端と上端。玄関の +x 側を上がる</summary>
        public const float StairX = 1.9f;
        public const float StairFoot = -0.6f;
        public const float StairHead = -4.4f;
        const float HouseHigh = 2.6f;

        /// <summary>
        /// 玄関の戸の高さ。団地の <c>DoorHigh</c> と同じ値だが、あちらは団地の戸の寸法で、
        /// 直されたときに台所の戸口と仕切りまで一緒に動くと困るので別に持つ
        /// </summary>
        const float HallDoorHigh = 2.0f;

        /// <summary>流しの背の壁と、天板の高さ・手前の面。流し台まわりはどれもここから測る</summary>
        const float KitchenBack = 3.2f;
        const float CounterHigh = 0.9f;
        const float CounterFace = 2.65f;
        const float CounterX0 = -2.4f;
        const float CounterX1 = 1.2f;

        /// <summary>東の壁の窓。朝日はここから入り、床の四角はこの寸法で決まる</summary>
        const float WindowX = 2.5f;
        const float WindowZ0 = 0.9f;
        const float WindowZ1 = 2.2f;
        const float WindowY0 = 0.95f;
        const float WindowY1 = 2.05f;

        /// <summary>玄関の外の踊り場。戸口の先が虚空だと戸を抜けられないので、ここまでは床がある</summary>
        const float PorchEdge = -3.65f;
        const float PorchZ0 = -2.75f;
        const float PorchZ1 = -1.15f;


        // ---- 台所と玄関と階段 ----------------------------------------------------

        /// <summary>
        /// 記憶 5・6・12 の舞台。台所と玄関を戸口で分け、玄関の脇から二階へ階段を上げる。
        /// 二階は作らない。階段の上は暗がりで、足音だけが降りてくる。
        ///
        /// 素材ごとの入れ物をここで一つずつ作り、以下の各段が同じ入れ物へ面を溜める。
        /// 家具を素材ごとに分けて焼くと、椅子や取っ手の数だけレンダラーが増える
        /// </summary>
        static void Kitchen(Transform place)
        {
            var deck = new Bank { Texel = 0.4f };
            var wall = new Bank { Texel = 0.35f };
            var roof = new Bank { Texel = 0.35f };
            var fit = new Bank { Texel = 0.5f };
            var tin = new Bank { Texel = 0.5f };
            var grit = new Bank { Texel = 0.5f };
            var leaf = new Bank { Texel = 0.4f };

            KitchenShell(deck, wall, roof);
            KitchenStairs(deck, tin);
            KitchenCounter(fit, tin, roof, wall);
            KitchenTable(fit);
            KitchenHall(place, grit, fit, leaf, wall);
            KitchenWindow(place, tin);

            Emit(place, "KitchenFloor", deck, "Floor", true);
            Emit(place, "KitchenWall", wall, "Wall", true);
            Emit(place, "KitchenCeiling", roof, "Ceiling");
            Emit(place, "KitchenGrit", grit, "Ground", true);
            Emit(place, "KitchenFittings", fit, "Timber");
            Emit(place, "KitchenMetal", tin, "Rail");
            Emit(place, "KitchenDoor", leaf, "Door");

            KitchenBlocks(place);
            KitchenLights(place);
        }

        /// <summary>床・壁・天井の箱。東の壁には窓が、玄関との仕切りには戸口が抜ける</summary>
        static void KitchenShell(Bank deck, Bank wall, Bank roof)
        {
            deck.FaceY(0f, -2.5f, 2.5f, -5f, KitchenBack, 1);

            wall.FaceZ(KitchenBack, -2.5f, 2.5f, 0f, HouseHigh, -1);
            wall.FaceZ(-5f, -2.5f, 2.5f, 0f, 4.2f, 1);
            // 東の壁。朝日の入り口なので、ここだけ穴を抜いて窓にする
            var sash = new List<Vector4>
            {
                new Vector4(WindowZ0, WindowZ1, WindowY0, WindowY1),
            };
            wall.FaceXHoles(WindowX, -5f, KitchenBack, 0f, 4.2f, -1, sash);
            // 玄関のドアを -x の壁に開ける。外の光はこの向こうから来る
            var holes = new List<Vector4>
            {
                new Vector4(-2.4f, -1.5f, 0f, HallDoorHigh),
            };
            wall.FaceXHoles(-2.5f, -5f, KitchenBack, 0f, HouseHigh, 1, holes);
            // 台所と玄関を分ける壁。戸口が一つ
            var gap = new List<Vector4>
            {
                new Vector4(-0.55f, 0.55f, 0f, 2.05f),
            };
            wall.FaceZHoles(KitchenDoorZ, -2.5f, 2.5f, 0f, HouseHigh, 1, gap);
            wall.FaceZHoles(KitchenDoorZ - 0.08f, -2.5f, 2.5f, 0f, HouseHigh, -1, gap);
            // 階段の吹き抜け。玄関の +x 側だけ天井が抜ける。
            // 台所側は天井の高さで切れているので、階段から前を向くと天井の上の虚空が見える。
            // 記憶 12 はそこから始まるので、吹き抜けの台所側も塞ぐ
            wall.FaceX(1.35f, -5f, KitchenDoorZ, HouseHigh, 4.2f, 1);
            wall.FaceZ(KitchenDoorZ, 1.35f, 2.5f, HouseHigh, 4.2f, -1);

            roof.FaceY(HouseHigh, -2.5f, 2.5f, KitchenDoorZ, KitchenBack, -1);
            roof.FaceY(HouseHigh, -2.5f, 1.35f, -5f, KitchenDoorZ, -1);
            roof.FaceY(4.2f, 1.35f, 2.5f, -5f, KitchenDoorZ, -1);
        }

        /// <summary>
        /// 二階へ上がる段と、その手すり・段鼻。玄関の +x 側を -z へ上がる。
        ///
        /// **段鼻を金物で入れる。** 段の面はどれも同じ暗さで、暗がりでは踏面と蹴上げの
        /// 境目が消えて斜面に見える。縁に艶のある細い帯を回すと、そこだけ灯りを返して
        /// 段が数えられるようになる
        /// </summary>
        static void KitchenStairs(Bank deck, Bank tin)
        {
            const int n = 15;
            var d = (StairFoot - StairHead) / n;
            var r = HouseHigh / n;
            for (var i = 0; i < n; i++)
            {
                deck.FaceZ(StairFoot - i * d, 1.35f, 2.5f, i * r, (i + 1) * r, 1);
                deck.FaceY((i + 1) * r, 1.35f, 2.5f, StairFoot - (i + 1) * d, StairFoot - i * d, 1);
                tin.Box(new Vector3(1.925f, (i + 1) * r + 0.015f, StairFoot - i * d - 0.045f),
                    new Vector3(1.13f, 0.03f, 0.09f));
            }
            deck.FaceY(HouseHigh, 1.35f, 2.5f, -5f, StairHead, 1);
            tin.Box(new Vector3(1.925f, HouseHigh + 0.015f, StairHead - 0.045f), new Vector3(1.13f, 0.03f, 0.09f));

            // 手すり。段と同じ勾配で一本、支柱は等間隔に六本
            var run = StairFoot - StairHead;
            var dir = new Vector3(0f, HouseHigh, -run).normalized;
            tin.Box(new Vector3(1.42f, HouseHigh * 0.5f + 0.9f, (StairFoot + StairHead) * 0.5f),
                new Vector3(0.05f, 0.05f, Mathf.Sqrt(run * run + HouseHigh * HouseHigh)),
                Quaternion.LookRotation(dir, Vector3.up));
            for (var i = 0; i <= 5; i++)
            {
                var k = i / 5f;
                tin.Box(new Vector3(1.42f, HouseHigh * k + 0.45f, StairFoot - run * k), new Vector3(0.05f, 0.9f, 0.05f));
            }
        }

        /// <summary>
        /// 流し台と吊り戸棚。北の壁に沿って一本、下は扉の列、上は吊り戸棚。
        ///
        /// **扉の継ぎ目が等間隔の繰り返しになる。** 台所を台所として読ませるのは、
        /// 蛇口の形よりもこの縦線の並びのほうが早い
        /// </summary>
        static void KitchenCounter(Bank fit, Bank tin, Bank roof, Bank wall)
        {
            var span = CounterX1 - CounterX0;
            var deep = KitchenBack - CounterFace;
            var deepMid = (CounterFace + KitchenBack) * 0.5f;
            // 流し鉢の下だけ胴を低く止める。通しの箱にすると、天板の穴から鉢ではなく
            // 胴の天面が見えて、窪みが埋まったままになる
            var bx0 = Sink.x - 0.37f;
            var bx1 = Sink.x + 0.37f;
            fit.Box(new Vector3((CounterX0 + bx0) * 0.5f, CounterHigh * 0.5f, deepMid),
                new Vector3(bx0 - CounterX0, CounterHigh, deep));
            fit.Box(new Vector3((bx1 + CounterX1) * 0.5f, CounterHigh * 0.5f, deepMid),
                new Vector3(CounterX1 - bx1, CounterHigh, deep));
            fit.Box(new Vector3(Sink.x, 0.35f, deepMid), new Vector3(bx1 - bx0, 0.7f, deep));

            // 下の扉。五枚を等間隔に並べ、合わせ目に取っ手を立てる
            const int doors = 5;
            var wide = (span - 0.06f * (doors + 1)) / doors;
            for (var i = 0; i < doors; i++)
            {
                var at = CounterX0 + 0.06f * (i + 1) + wide * (i + 0.5f);
                fit.Box(new Vector3(at, 0.46f, CounterFace - 0.018f), new Vector3(wide, 0.7f, 0.036f));
                tin.Box(new Vector3(at + wide * 0.5f - 0.05f, 0.72f, CounterFace - 0.05f),
                    new Vector3(0.03f, 0.16f, 0.03f));
            }

            // 天板。流し鉢の穴を残して四枚に分ける
            const float bz0 = 2.74f;
            const float bz1 = 3.12f;
            const float rim = CounterHigh + 0.02f;
            tin.Box(new Vector3((CounterX0 + bx0) * 0.5f, rim, (CounterFace + KitchenBack) * 0.5f),
                new Vector3(bx0 - CounterX0, 0.04f, deep + 0.04f));
            tin.Box(new Vector3((bx1 + CounterX1) * 0.5f, rim, (CounterFace + KitchenBack) * 0.5f),
                new Vector3(CounterX1 - bx1, 0.04f, deep + 0.04f));
            tin.Box(new Vector3(Sink.x, rim, (CounterFace + bz0) * 0.5f),
                new Vector3(bx1 - bx0, 0.04f, bz0 - CounterFace + 0.04f));
            tin.Box(new Vector3(Sink.x, rim, (bz1 + KitchenBack) * 0.5f),
                new Vector3(bx1 - bx0, 0.04f, KitchenBack - bz1 + 0.04f));
            // 流し鉢。底と四方の立ち上がりで窪みを作る
            tin.Box(new Vector3(Sink.x, 0.72f, (bz0 + bz1) * 0.5f), new Vector3(bx1 - bx0, 0.03f, bz1 - bz0));
            tin.Box(new Vector3(bx0, 0.81f, (bz0 + bz1) * 0.5f), new Vector3(0.03f, 0.2f, bz1 - bz0));
            tin.Box(new Vector3(bx1, 0.81f, (bz0 + bz1) * 0.5f), new Vector3(0.03f, 0.2f, bz1 - bz0));
            tin.Box(new Vector3(Sink.x, 0.81f, bz0), new Vector3(bx1 - bx0, 0.2f, 0.03f));
            tin.Box(new Vector3(Sink.x, 0.81f, bz1), new Vector3(bx1 - bx0, 0.2f, 0.03f));
            // 蛇口。首を鉢の上まで伸ばし、根本に湯と水の栓
            tin.Box(new Vector3(Sink.x, 1.06f, 3.06f), new Vector3(0.05f, 0.34f, 0.05f));
            tin.Box(new Vector3(Sink.x, 1.21f, 2.95f), new Vector3(0.04f, 0.04f, 0.26f));
            tin.Box(new Vector3(Sink.x - 0.14f, 0.97f, 3.06f), new Vector3(0.16f, 0.04f, 0.04f));
            tin.Box(new Vector3(Sink.x + 0.14f, 0.97f, 3.06f), new Vector3(0.16f, 0.04f, 0.04f));
            // 水切り。溝の入った板を鉢の +x 側へ
            for (var i = 0; i < 7; i++)
            {
                tin.Box(new Vector3(bx1 + 0.09f + i * 0.09f, rim + 0.025f, (bz0 + bz1) * 0.5f),
                    new Vector3(0.05f, 0.015f, bz1 - bz0 - 0.06f));
            }

            // 吊り戸棚。扉は四枚、取っ手は下の縁へ
            const float hangY0 = 1.5f;
            const float hangY1 = 2.25f;
            const float hangX1 = 0.3f;
            fit.Box(new Vector3((CounterX0 + hangX1) * 0.5f, (hangY0 + hangY1) * 0.5f, (2.84f + KitchenBack) * 0.5f),
                new Vector3(hangX1 - CounterX0, hangY1 - hangY0, KitchenBack - 2.84f));
            const int leaves = 4;
            var lw = (hangX1 - CounterX0 - 0.05f * (leaves + 1)) / leaves;
            for (var i = 0; i < leaves; i++)
            {
                var at = CounterX0 + 0.05f * (i + 1) + lw * (i + 0.5f);
                fit.Box(new Vector3(at, (hangY0 + hangY1) * 0.5f, 2.82f), new Vector3(lw, hangY1 - hangY0 - 0.08f, 0.04f));
                tin.Box(new Vector3(at, hangY0 + 0.12f, 2.78f), new Vector3(lw * 0.5f, 0.03f, 0.03f));
            }

            // 冷蔵庫。胴は壁と同じ暗さなので壁の一枚へ混ぜ、前の扉だけ金物で立てる
            const float coldX = 2.05f;
            wall.Box(new Vector3(coldX, 0.88f, 2.83f), new Vector3(0.7f, 1.76f, 0.7f));
            tin.Box(new Vector3(coldX, 0.56f, 2.46f), new Vector3(0.72f, 1.08f, 0.06f));
            tin.Box(new Vector3(coldX, 1.45f, 2.46f), new Vector3(0.72f, 0.62f, 0.06f));
            tin.Box(new Vector3(coldX - 0.3f, 0.95f, 2.41f), new Vector3(0.04f, 0.3f, 0.04f));
            tin.Box(new Vector3(coldX - 0.3f, 1.3f, 2.41f), new Vector3(0.04f, 0.26f, 0.04f));

            // 電子レンジ。台所でいちばん暗い面は窓の硝子なので、天井と同じ黒に混ぜる
            tin.Box(new Vector3(0.75f, 1.09f, 2.95f), new Vector3(0.52f, 0.34f, 0.4f));
            roof.Box(new Vector3(0.71f, 1.09f, 2.74f), new Vector3(0.34f, 0.22f, 0.02f));
        }

        /// <summary>
        /// 卓と椅子二脚。人の通る筋（記憶 5 と 6 が歩く台所の真ん中）を空けたいので、
        /// 西の壁へ寄せる
        /// </summary>
        static void KitchenTable(Bank fit)
        {
            const float tx = -1.7f;
            const float tz = 1.3f;
            fit.Box(new Vector3(tx, 0.72f, tz), new Vector3(1.0f, 0.05f, 0.7f));
            for (var i = 0; i < 4; i++)
            {
                var sx = (i % 2 == 0) ? -0.44f : 0.44f;
                var sz = (i < 2) ? -0.29f : 0.29f;
                fit.Box(new Vector3(tx + sx, 0.35f, tz + sz), new Vector3(0.06f, 0.7f, 0.06f));
            }
            KitchenChair(fit, tx, 0.62f, 1f);
            KitchenChair(fit, tx, 1.98f, -1f);
        }

        /// <summary>椅子一脚。背もたれは卓と反対側を向く</summary>
        static void KitchenChair(Bank fit, float x, float z, float face)
        {
            fit.Box(new Vector3(x, 0.43f, z), new Vector3(0.4f, 0.05f, 0.4f));
            fit.Box(new Vector3(x, 0.67f, z - 0.18f * face), new Vector3(0.4f, 0.44f, 0.04f));
            for (var i = 0; i < 4; i++)
            {
                var sx = (i % 2 == 0) ? -0.17f : 0.17f;
                var sz = (i < 2) ? -0.17f : 0.17f;
                fit.Box(new Vector3(x + sx, 0.21f, z + sz), new Vector3(0.04f, 0.42f, 0.04f));
            }
        }

        /// <summary>
        /// 玄関と、その外の踊り場。
        ///
        /// **戸口は歩いて抜けられる。** 以前は戸口ぜんたいが見えない仕切りで塞いであり、
        /// 三つの記憶がどれもここから外へ出るのに、主は敷居へ足も掛けられなかった。
        /// 外は白く飛んだ光しか無いので、戸の先へ一畳ぶんの踊り場を伸ばし、
        /// その奥の口を白い板で閉じて、仕切りはその手前へ下げる
        /// </summary>
        static void KitchenHall(Transform place, Bank grit, Bank fit, Bank leaf, Bank wall)
        {
            // 三和土。土間は板の間より暗く、上がり框の一段で分かれる
            grit.FaceY(0.02f, -2.5f, -1.72f, -2.65f, -1.15f, 1);
            grit.FaceZ(-2.65f, -2.5f, -1.72f, 0f, 0.02f, -1);
            grit.FaceZ(-1.15f, -2.5f, -1.72f, 0f, 0.02f, 1);
            fit.Box(new Vector3(-1.72f, 0.05f, -1.9f), new Vector3(0.07f, 0.1f, 1.5f));

            // 靴二足。爪先を戸へ向けて揃えてある
            for (var i = 0; i < 4; i++)
            {
                var pair = (i < 2) ? -2.28f : -1.56f;
                var side = (i % 2 == 0) ? -0.07f : 0.07f;
                leaf.Box(new Vector3(-2.16f, 0.07f, pair + side), new Vector3(0.26f, 0.09f, 0.11f));
            }

            // 開いたまま壁へ寄せた戸。三つの記憶がどれもここから外へ出るので、閉めた戸は置かない
            leaf.Box(new Vector3(-2.42f, HallDoorHigh * 0.5f, -0.95f), new Vector3(0.05f, HallDoorHigh, 0.86f));
            // 戸口の枠。白い外光との境が直に切れていると、壁に穴が開いているようにしか見えない
            fit.Box(new Vector3(-2.46f, HallDoorHigh * 0.5f, -2.45f), new Vector3(0.09f, HallDoorHigh + 0.1f, 0.1f));
            fit.Box(new Vector3(-2.46f, HallDoorHigh * 0.5f, -1.45f), new Vector3(0.09f, HallDoorHigh + 0.1f, 0.1f));
            fit.Box(new Vector3(-2.46f, HallDoorHigh + 0.05f, -1.95f), new Vector3(0.09f, 0.1f, 1.1f));

            // 踊り場。床・両脇・庇で囲い、外へ開くのは奥の一面だけ
            grit.FaceY(0f, PorchEdge, -2.5f, PorchZ0, PorchZ1, 1);
            wall.FaceZ(PorchZ0, PorchEdge, -2.5f, 0f, 2.4f, 1);
            wall.FaceZ(PorchZ1, PorchEdge, -2.5f, 0f, 2.4f, -1);
            wall.FaceY(2.4f, PorchEdge, -2.5f, PorchZ0, PorchZ1, -1);
            var holes = new List<Vector4>
            {
                new Vector4(-2.4f, -1.5f, 0f, HallDoorHigh),
            };
            wall.FaceXHoles(-2.5f, PorchZ0, PorchZ1, 0f, 2.4f, -1, holes);

            // 踊り場の奥。開けると白く飛ぶ朝の光で、その先は無い
            Pane(place, "KitchenOutside", new Vector3(PorchEdge + 0.02f, 1.2f, -1.95f), new Vector2(1.6f, 2.4f),
                Vector3.right, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f));
            Fence(place, "KitchenPorchEnd", new Vector3(PorchEdge + 0.12f, 1.2f, -1.95f), new Vector3(0.2f, 2.4f, 1.7f));
        }

        /// <summary>
        /// 窓と、床に落ちる朝の四角。
        ///
        /// **床の四角は光そのものを描く。** 灯りだけで窓の形を床へ落とすには影を落とす
        /// 板と高い解像度の影が要り、WebGL では持たない。窓の桟の割り付けで四枚に切った
        /// 明るい板を床へ寝かせれば、同じ絵が影の勘定なしに出る
        /// </summary>
        static void KitchenWindow(Transform place, Bank tin)
        {
            var midZ = (WindowZ0 + WindowZ1) * 0.5f;
            var midY = (WindowY0 + WindowY1) * 0.5f;
            var wide = WindowZ1 - WindowZ0;
            var high = WindowY1 - WindowY0;
            tin.Box(new Vector3(WindowX - 0.04f, WindowY0 - 0.04f, midZ), new Vector3(0.16f, 0.08f, wide + 0.12f));
            tin.Box(new Vector3(WindowX - 0.02f, WindowY1 + 0.03f, midZ), new Vector3(0.1f, 0.06f, wide + 0.12f));
            tin.Box(new Vector3(WindowX - 0.02f, midY, WindowZ0 - 0.03f), new Vector3(0.1f, high + 0.12f, 0.06f));
            tin.Box(new Vector3(WindowX - 0.02f, midY, WindowZ1 + 0.03f), new Vector3(0.1f, high + 0.12f, 0.06f));
            tin.Box(new Vector3(WindowX - 0.02f, midY, midZ), new Vector3(0.08f, high, 0.05f));
            tin.Box(new Vector3(WindowX - 0.02f, midY, midZ), new Vector3(0.08f, 0.05f, wide));

            Pane(place, "KitchenWindow", new Vector3(WindowX + 0.03f, midY, midZ), new Vector2(wide, high),
                Vector3.left, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f));

            // 朝の陽は低い。窓の上下の縁がそのまま床の四角の遠近の縁になる
            const float slope = 0.78f;
            var near = WindowX - WindowY0 / slope;
            var far = WindowX - WindowY1 / slope;
            var barX = WindowX - midY / slope;
            var floor = Glow("GlowSunFloor", new Color(1f, 0.98f, 0.93f), 0.95f);
            KitchenSun(place, "A", far, barX - 0.05f, WindowZ0, midZ - 0.04f, floor);
            KitchenSun(place, "B", barX + 0.05f, near, WindowZ0, midZ - 0.04f, floor);
            KitchenSun(place, "C", far, barX - 0.05f, midZ + 0.04f, WindowZ1, floor);
            KitchenSun(place, "D", barX + 0.05f, near, midZ + 0.04f, WindowZ1, floor);
        }

        /// <summary>床の光の一枚。桟で切れた四つのうちの一つ</summary>
        static void KitchenSun(Transform place, string tag, float x0, float x1, float z0, float z1, Material mat)
        {
            Pane(place, "KitchenSun" + tag, new Vector3((x0 + x1) * 0.5f, 0.012f, (z0 + z1) * 0.5f),
                new Vector2(x1 - x0, z1 - z0), Vector3.up, mat);
        }

        /// <summary>
        /// 大きな家具の当たり。
        ///
        /// mesh の当たりを家具ごとに入れると引っ掛かる隅が増えるので、抜けられると困る
        /// 二つだけを箱で囲う（冷蔵庫の胴は壁の一枚に入っていて、もう当たりを持つ）。
        /// 記憶の鍵打ちの立ち位置は、どれもこの箱の外に取ってある
        /// </summary>
        static void KitchenBlocks(Transform place)
        {
            var mid = (CounterX0 + CounterX1) * 0.5f;
            Fence(place, "KitchenCounterBlock", new Vector3(mid, CounterHigh * 0.5f, (CounterFace + KitchenBack) * 0.5f),
                new Vector3(CounterX1 - CounterX0, CounterHigh, KitchenBack - CounterFace));
            Fence(place, "KitchenTableBlock", new Vector3(-1.7f, 0.36f, 1.3f), new Vector3(1.0f, 0.72f, 0.7f));
        }

        /// <summary>
        /// 灯り。
        ///
        /// **暗すぎたので上げた。** 前の絵は人の輪郭がやっと見える程度で、流し台も階段も
        /// 暗がりに沈んでいた。台所は朝で、窓から入る光がいちばん強いのが正しいので、
        /// 窓に一つ大きな spot を置いて床を照らし、裸電球はその補いにする
        /// </summary>
        static void KitchenLights(Transform place)
        {
            var sun = Lamp(place, "Morning", LightType.Spot, new Vector3(2.3f, 1.85f, 1.55f),
                new Vector3(46f, 265f, 0f), new Color(0.96f, 0.97f, 1f), 18f, 11f);
            sun.spotAngle = 110f;
            sun.innerSpotAngle = 46f;

            Lamp(place, "Bulb", LightType.Point, new Vector3(-0.3f, HouseHigh - 0.25f, 1.1f),
                Vector3.zero, new Color(0.86f, 0.90f, 1f), 11f, 15f);
            Pane(place, "KitchenBulb", new Vector3(-0.3f, HouseHigh - 0.04f, 1.1f), new Vector2(0.5f, 0.5f),
                Vector3.down, Glow("GlowBulb", new Color(1f, 0.97f, 0.90f), 1.5f));

            // 玄関と踊り場。外の白がここまでは届いている
            Lamp(place, "HallGlow", LightType.Point, new Vector3(-2.6f, 1.35f, -1.95f),
                Vector3.zero, new Color(0.90f, 0.94f, 1f), 9f, 8f);
            // 階段。吹き抜けの壁より +x 側へ置く。壁の裏に置くと、記憶 12 が見下ろす
            // いちばん手前の面が真っ黒になる。上がるほど薄れて、二階は暗がりのまま残す
            Lamp(place, "StairGlow", LightType.Point, new Vector3(1.7f, 2.3f, -2.1f),
                Vector3.zero, new Color(0.88f, 0.90f, 1f), 5f, 9f);
        }
    }
}
