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
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
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


        // ---- 台所と玄関と階段 ----------------------------------------------------

        /// <summary>
        /// 記憶 5・6・12 の舞台。台所と玄関を戸口で分け、玄関の脇から二階へ階段を上げる。
        /// 二階は作らない。階段の上は暗がりで、足音だけが降りてくる
        /// </summary>
        static void Kitchen(Transform place)
        {
            var deck = new Bank { Texel = 0.4f };
            deck.FaceY(0f, -2.5f, 2.5f, -5f, 3.2f, 1);
            KitchenSteps(deck);
            Emit(place, "KitchenFloor", deck, "Floor", true);

            var wall = new Bank { Texel = 0.35f };
            wall.FaceZ(3.2f, -2.5f, 2.5f, 0f, HouseHigh, -1);
            wall.FaceZ(-5f, -2.5f, 2.5f, 0f, 4.2f, 1);
            wall.FaceX(2.5f, -5f, 3.2f, 0f, 4.2f, -1);
            // 玄関のドアを -x の壁に開ける。外の光はこの向こうから来る
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-2.4f, -1.5f, 0f, DoorHigh),
            };
            wall.FaceXHoles(-2.5f, -5f, 3.2f, 0f, HouseHigh, 1, holes);
            // 台所と玄関を分ける壁。戸口が一つ
            var gap = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-0.55f, 0.55f, 0f, 2.05f),
            };
            wall.FaceZHoles(KitchenDoorZ, -2.5f, 2.5f, 0f, HouseHigh, 1, gap);
            wall.FaceZHoles(KitchenDoorZ - 0.08f, -2.5f, 2.5f, 0f, HouseHigh, -1, gap);
            // 階段の吹き抜け。玄関の +x 側だけ天井が抜ける
            wall.FaceX(1.35f, -5f, KitchenDoorZ, HouseHigh, 4.2f, 1);
            Emit(place, "KitchenWall", wall, "Wall", true);

            var roof = new Bank { Texel = 0.35f };
            roof.FaceY(HouseHigh, -2.5f, 2.5f, KitchenDoorZ, 3.2f, -1);
            roof.FaceY(HouseHigh, -2.5f, 1.35f, -5f, KitchenDoorZ, -1);
            roof.FaceY(4.2f, 1.35f, 2.5f, -5f, KitchenDoorZ, -1);
            Emit(place, "KitchenCeiling", roof, "Ceiling");

            var fit = new Bank { Texel = 0.5f };
            // 流しと吊り棚。記憶 5 はこの前で手を洗い、記憶 6 は棚を顎で指される
            fit.Box(new Vector3(-1.05f, 0.45f, 2.85f), new Vector3(2.5f, 0.9f, 0.62f));
            fit.Box(new Vector3(-1.05f, 1.9f, 3.0f), new Vector3(2.5f, 0.7f, 0.36f));
            fit.Box(new Vector3(1.3f, 0.45f, 2.85f), new Vector3(1.2f, 0.9f, 0.62f));
            fit.Box(new Vector3(-2.0f, 0.02f, -1.95f), new Vector3(0.72f, 0.04f, 0.46f));
            Emit(place, "KitchenFittings", fit, "Timber");

            var tap = new Bank { Texel = 0.5f };
            tap.Box(new Vector3(-1.05f, 0.88f, 2.85f), new Vector3(0.7f, 0.06f, 0.44f));
            tap.Box(new Vector3(-1.05f, 1.06f, 3.05f), new Vector3(0.04f, 0.32f, 0.04f));
            KitchenRail(tap);
            Emit(place, "KitchenMetal", tap, "Rail");

            // 開いたまま壁へ寄せた戸。三つの記憶がどれもここから外へ出るので、閉めた戸は置かない
            var leaf = new Bank { Texel = 0.4f };
            leaf.Box(new Vector3(-2.42f, DoorHigh * 0.5f, -0.95f), new Vector3(0.05f, DoorHigh, 0.86f));
            Emit(place, "KitchenDoor", leaf, "Door");

            // 玄関のドアの向こうは白い光の板だけで、床はそこで切れている。
            // 三つの記憶がどれもこの戸口から出ていくので、絵としては開けたまま残し、
            // 抜けられないように見えない仕切りだけを立てる
            Fence(place, "KitchenDoorway", new Vector3(-2.55f, DoorHigh * 0.5f, -1.95f),
                new Vector3(0.2f, DoorHigh, 1.1f));

            // ドアの外。開けると白く飛ぶ朝の光
            Pane(place, "KitchenOutside", new Vector3(-2.72f, 1.05f, -1.95f), new Vector2(1.1f, 2.1f),
                Vector3.right, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f));

            Lamp(place, "Bulb", LightType.Point, new Vector3(-0.3f, HouseHigh - 0.25f, 1.1f),
                Vector3.zero, new Color(0.86f, 0.90f, 1f), 6.5f, 14f);
            Pane(place, "KitchenBulb", new Vector3(-0.3f, HouseHigh - 0.04f, 1.1f), new Vector2(0.5f, 0.5f),
                Vector3.down, Glow("GlowBulb", new Color(1f, 0.97f, 0.90f), 1.5f));
        }

        /// <summary>二階へ上がる段。玄関の +x 側を -z へ上がる</summary>
        static void KitchenSteps(Bank b)
        {
            const int n = 15;
            var d = (StairFoot - StairHead) / n;
            var r = HouseHigh / n;
            for (var i = 0; i < n; i++)
            {
                b.FaceZ(StairFoot - i * d, 1.35f, 2.5f, i * r, (i + 1) * r, 1);
                b.FaceY((i + 1) * r, 1.35f, 2.5f, StairFoot - (i + 1) * d, StairFoot - i * d, 1);
            }
            b.FaceY(HouseHigh, 1.35f, 2.5f, -5f, StairHead, 1);
        }

        /// <summary>階段の手すり。段と同じ勾配で一本</summary>
        static void KitchenRail(Bank b)
        {
            var run = StairFoot - StairHead;
            var dir = new Vector3(0f, HouseHigh, -run).normalized;
            b.Box(new Vector3(1.42f, HouseHigh * 0.5f + 0.9f, (StairFoot + StairHead) * 0.5f),
                new Vector3(0.05f, 0.05f, Mathf.Sqrt(run * run + HouseHigh * HouseHigh)),
                Quaternion.LookRotation(dir, Vector3.up));
            for (var i = 0; i <= 3; i++)
            {
                var k = i / 3f;
                b.Box(new Vector3(1.42f, HouseHigh * k + 0.45f, StairFoot - run * k), new Vector3(0.05f, 0.9f, 0.05f));
            }
        }
    }
}
