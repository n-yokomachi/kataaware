using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の台所の中の層。窓の外の裏庭と、塀の向こうの隣の家の裏の張り出し
    /// （設計書 9.1 節「台所の作り込み」の「窓の外」の「組む」）。その先は書き割り（<c>BuildDiveKitchenFar.cs</c>）。
    ///
    /// **ロンドンのテラスハウスの裏庭。** 家の裏の壁から東へ 11 m ほどの細長い庭で、三方を木の塀が囲う。
    /// 家の際に敷石のテラス、その先に芝、塀の足元に植え込みの帯。奥の隅に物置の小屋、芝の真ん中に回る物干し。
    /// 北の塀の向こうに、隣の家の裏の張り出し（アウトリガー）が二階ぶんの煉瓦の壁と窓を見せる。
    /// 南の隣にも同じ張り出しがあり、窓から斜めに見える。
    ///
    /// **塀と小屋と物干しは影を落とす。** 朝日は東南東の低い所から差すので、塀と小屋の影が芝の上を家の方へ長く伸びる。
    /// 窓から見ると、どれも日を背にした影の形になる。
    ///
    /// **入れ物とマテリアルは公営住宅の敷地と分け合う**（<see cref="YardBanks"/>）。mesh の名前は <c>Kitchen</c> で始める。
    /// 塀の板は風雨で銀色に褪せた色（YardBark）、小屋は塗った濃い茶（YardTimber）
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>家の裏の壁の外の面。庭はここから東</summary>
        const float KitchenYardWest = KitchenEast + KitchenRearSkin;                      // 2.75
        /// <summary>奥の塀の線。庭の東の端</summary>
        const float KitchenYardEast = 14.2f;
        /// <summary>北と南の塀の線（隣との境）</summary>
        const float KitchenYardNorth = 3.45f;
        const float KitchenYardSouth = -5.2f;
        /// <summary>敷石のテラスの東の縁。ここから東が芝</summary>
        const float KitchenPatio = 5.2f;
        /// <summary>塀の高さ。目の高さ（1.58〜1.72）より高いので、塀の向こうの地面は窓から見えない</summary>
        const float KitchenFenceHigh = 1.8f;

        /// <summary>中の層をまとめて組む。出すのは呼ぶ側</summary>
        static void KitchenYard(YardBanks y)
        {
            KitchenYardGround(y);
            // 塀。北・南・奥。庭の側を表に
            KitchenFence(y, new Vector3(KitchenYardWest, 0f, KitchenYardNorth), new Vector3(KitchenYardEast, 0f, KitchenYardNorth), Vector3.back);
            KitchenFence(y, new Vector3(KitchenYardWest, 0f, KitchenYardSouth), new Vector3(KitchenYardEast, 0f, KitchenYardSouth), Vector3.forward);
            KitchenFence(y, new Vector3(KitchenYardEast, 0f, KitchenYardSouth), new Vector3(KitchenYardEast, 0f, KitchenYardNorth), Vector3.left);
            KitchenShed(y);
            KitchenRotary(y, new Vector3(8.4f, 0f, -0.9f));
            // 隣の家の裏の張り出し。北は窓を南へ、南は窓を北へ向ける
            KitchenOutrigger(y, KitchenYardNorth + 0.2f, 1f, 211);
            KitchenOutrigger(y, KitchenYardSouth - 0.2f, -1f, 223);
            KitchenYardThings(y);
        }

        /// <summary>
        /// 地面。家の際の敷石、その先の芝、塀の足元の植え込みの土、小屋へ渡る飛び石
        /// </summary>
        static void KitchenYardGround(YardBanks y)
        {
            const float bed = 0.75f;
            // 敷石のテラスと目地
            y.Kerb.FaceY(0.012f, KitchenYardWest, KitchenPatio, KitchenYardSouth + 0.05f, KitchenYardNorth - 0.05f, 1);
            for (var x = KitchenYardWest + 0.6f; x < KitchenPatio - 0.05f; x += 0.6f)
                y.Tarmac.FaceY(0.014f, x - 0.012f, x + 0.012f, KitchenYardSouth + 0.05f, KitchenYardNorth - 0.05f, 1);
            for (var z = KitchenYardSouth + 0.45f; z < KitchenYardNorth - 0.05f; z += 0.9f)
                y.Tarmac.FaceY(0.014f, KitchenYardWest, KitchenPatio, z - 0.012f, z + 0.012f, 1);
            // 芝。三方の塀の足元に植え込みの土の帯を残す
            y.Grass.FaceY(0.010f, KitchenPatio, KitchenYardEast - bed, KitchenYardSouth + bed, KitchenYardNorth - bed, 1);
            y.Timber.FaceY(0.008f, KitchenPatio, KitchenYardEast, KitchenYardNorth - bed, KitchenYardNorth, 1);
            y.Timber.FaceY(0.008f, KitchenPatio, KitchenYardEast, KitchenYardSouth, KitchenYardSouth + bed, 1);
            y.Timber.FaceY(0.008f, KitchenYardEast - bed, KitchenYardEast, KitchenYardSouth + bed, KitchenYardNorth - bed, 1);
            // テラスの縁の煉瓦の見切り
            y.Brick.Box(new Vector3(KitchenPatio + 0.05f, 0.03f, (KitchenYardSouth + KitchenYardNorth) * 0.5f), new Vector3(0.10f, 0.06f, KitchenYardNorth - KitchenYardSouth - 0.1f));
            // 飛び石。テラスから芝を斜めに小屋の戸へ
            for (var i = 0; i < 7; i++)
            {
                var k = (i + 0.5f) / 7f;
                var at = Vector3.Lerp(new Vector3(KitchenPatio + 0.4f, 0f, 0.6f), new Vector3(11.0f, 0f, 1.6f), k);
                y.Kerb.Box(at + new Vector3(0f, 0.015f, 0f), new Vector3(0.42f, 0.03f, 0.42f), Quaternion.Euler(0f, i * 17f, 0f));
            }
        }

        /// <summary>
        /// 木の塀を一辺。コンクリートの柱を 1.83 m ごとに立て、足元にコンクリートの板、
        /// その上に縦の羽目板を 10 cm ごとに重ねて、上に笠木。<paramref name="face"/> は庭の側（表）の向き
        /// </summary>
        static void KitchenFence(YardBanks y, Vector3 a, Vector3 b, Vector3 face)
        {
            var d = b - a;
            var len = d.magnitude;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (a + b) * 0.5f;
            const float board = 0.15f;
            // 板の地と、足元の板・笠木
            y.Bark.Box(mid + Vector3.up * ((board + KitchenFenceHigh) * 0.5f), new Vector3(0.02f, KitchenFenceHigh - board, len), rot);
            y.Kerb.Box(mid + Vector3.up * (board * 0.5f) + face * 0.01f, new Vector3(0.04f, board, len), rot);
            y.Bark.Box(mid + Vector3.up * (KitchenFenceHigh + 0.015f), new Vector3(0.07f, 0.03f, len), rot);
            // 羽目板の筋。表の側だけ
            var boards = Mathf.RoundToInt(len / 0.1f);
            for (var i = 1; i < boards; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)boards);
                y.Bark.Box(at + Vector3.up * ((board + KitchenFenceHigh) * 0.5f) + face * 0.014f, new Vector3(0.008f, KitchenFenceHigh - board - 0.02f, 0.012f), rot);
            }
            // 横木。表から二本見える
            for (var i = 0; i < 2; i++)
                y.Bark.Box(mid + Vector3.up * (i == 0 ? 0.45f : 1.45f) + face * 0.03f, new Vector3(0.04f, 0.08f, len), rot);
            var posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.83f));
            for (var i = 0; i <= posts; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)posts);
                y.Kerb.Box(at + Vector3.up * ((KitchenFenceHigh + 0.1f) * 0.5f) + face * 0.03f, new Vector3(0.10f, KitchenFenceHigh + 0.1f, 0.10f), rot);
            }
        }

        /// <summary>
        /// 物置の小屋。奥の北の隅に、濃い茶に塗った下見板の小屋。切妻の屋根の妻を家の方へ向け、戸と小さな窓。
        /// 屋根はフェルト（黒）
        /// </summary>
        static void KitchenShed(YardBanks y)
        {
            const float x0 = 11.3f;
            const float x1 = 13.7f;
            const float z0 = 0.6f;
            const float z1 = 2.9f;
            const float eave = 1.95f;
            const float ridge = 2.40f;
            var mid = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            y.Kerb.Box(mid + Vector3.up * 0.05f, new Vector3(x1 - x0 + 0.2f, 0.10f, z1 - z0 + 0.2f));
            y.Timber.Box(mid + Vector3.up * (0.1f + (eave - 0.1f) * 0.5f), new Vector3(x1 - x0, eave - 0.1f, z1 - z0));
            // 妻の三角。西と東
            foreach (var x in new[] { x0, x1 })
            {
                var n = x == x0 ? Vector3.left : Vector3.right;
                MidQuad(y.Timber, new Vector3(x, eave, z0), new Vector3(x, eave, z1), new Vector3(x, ridge, (z0 + z1) * 0.5f),
                    new Vector3(x, ridge, (z0 + z1) * 0.5f), n);
            }
            // 下見板の筋
            for (var h = 0.35f; h < eave; h += 0.22f)
            {
                y.Iron.FaceX(x0 - 0.003f, z0, z1, h, h + 0.012f, -1);
                y.Iron.FaceZ(z0 - 0.003f, x0, x1, h, h + 0.012f, -1);
                y.Iron.FaceZ(z1 + 0.003f, x0, x1, h, h + 0.012f, 1);
            }
            // 屋根。両の斜面を軒から 0.12 m 出す
            var r0 = new Vector3(x0 - 0.12f, ridge + 0.03f, (z0 + z1) * 0.5f);
            var r1 = new Vector3(x1 + 0.12f, ridge + 0.03f, (z0 + z1) * 0.5f);
            foreach (var side in new[] { -1f, 1f })
            {
                var e = side < 0f ? z0 - 0.15f : z1 + 0.15f;
                var e0 = new Vector3(x0 - 0.12f, eave - 0.06f, e);
                var e1 = new Vector3(x1 + 0.12f, eave - 0.06f, e);
                MidQuad(y.Iron, e0, e1, r1, r0, new Vector3(0f, 1f, side));
                MidQuad(y.Iron, e0 - Vector3.up * 0.04f, e1 - Vector3.up * 0.04f, r1 - Vector3.up * 0.04f, r0 - Vector3.up * 0.04f, new Vector3(0f, -1f, -side));
            }
            // 戸と窓。家の方（西の妻）
            y.Timber.Box(new Vector3(x0 - 0.02f, 0.95f, z0 + 0.62f), new Vector3(0.04f, 1.70f, 0.78f));
            for (var i = 0; i < 3; i++)
                y.Iron.Box(new Vector3(x0 - 0.045f, 0.4f + i * 0.55f, z0 + 0.62f), new Vector3(0.01f, 0.04f, 0.70f));
            y.Iron.Box(new Vector3(x0 - 0.05f, 1.0f, z0 + 0.92f), new Vector3(0.02f, 0.10f, 0.03f));
            y.Line.Box(new Vector3(x0 - 0.02f, 1.30f, z1 - 0.55f), new Vector3(0.03f, 0.56f, 0.66f));
            y.Glass.Box(new Vector3(x0 - 0.035f, 1.30f, z1 - 0.55f), new Vector3(0.01f, 0.48f, 0.58f));
            y.Line.Box(new Vector3(x0 - 0.04f, 1.30f, z1 - 0.55f), new Vector3(0.01f, 0.48f, 0.03f));
        }

        /// <summary>
        /// 回る物干し（ロータリー）。一本の柱の上から四本の腕を斜めに下ろし、腕のあいだに四重の四角い紐。
        /// 外の紐に手拭いと赤い服、洗濯ばさみ。芝の真ん中で、窓から見ると日を背にした骨組みの影
        /// </summary>
        static void KitchenRotary(YardBanks y, Vector3 foot)
        {
            const float high = 1.85f;
            const float reach = 1.35f;
            const float drop = 0.18f;
            y.Pole.Box(foot + Vector3.up * (high * 0.5f), new Vector3(0.045f, high, 0.045f));
            y.Pole.Box(foot + Vector3.up * 0.04f, new Vector3(0.12f, 0.08f, 0.12f));
            var top = foot + Vector3.up * high;
            var tips = new Vector3[4];
            for (var k = 0; k < 4; k++)
            {
                var a = (45f + k * 90f + 20f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                tips[k] = top + dir * reach - Vector3.up * drop;
                YardBeam(y.Pole, top, tips[k], 0.025f);
                YardBeam(y.Pole, foot + Vector3.up * (high - 0.45f), top + dir * 0.45f - Vector3.up * (drop * 0.33f), 0.015f);
            }
            foreach (var f in new[] { 0.35f, 0.6f, 0.82f, 1.0f })
                for (var k = 0; k < 4; k++)
                {
                    var p = Vector3.Lerp(top, tips[k], f);
                    var q = Vector3.Lerp(top, tips[(k + 1) % 4], f);
                    YardBeam(y.Line, p, q, 0.014f);
                }
            // 外の紐に干した物。紐の上から下へ垂らす
            var line0 = tips[1];
            var line1 = tips[2];
            KitchenHang(y.Line, Vector3.Lerp(line0, line1, 0.28f), line1 - line0, 0.50f, 0.62f);
            KitchenHang(y.Red, Vector3.Lerp(line0, line1, 0.66f), line1 - line0, 0.46f, 0.52f);
            KitchenHang(y.Yellow, Vector3.Lerp(tips[2], tips[3], 0.4f), tips[3] - tips[2], 0.16f, 0.34f);
            KitchenHang(y.Yellow, Vector3.Lerp(tips[2], tips[3], 0.52f), tips[3] - tips[2], 0.16f, 0.34f);
        }

        /// <summary>紐に掛けた布一枚。<paramref name="at"/> は紐の上の真ん中、<paramref name="along"/> は紐の向き</summary>
        static void KitchenHang(Bank bank, Vector3 at, Vector3 along, float wide, float high)
        {
            var dir = new Vector3(along.x, 0f, along.z).normalized;
            var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            bank.Box(at - Vector3.up * (high * 0.5f), new Vector3(wide, high, 0.012f), rot);
        }

        /// <summary>
        /// 隣の家の裏の張り出し（アウトリガー）。塀の向こうに、二階建ての煉瓦の箱とスレートの切妻屋根。
        /// 庭の側の面に一階と二階の上げ下げ窓、雨樋と縦樋、家の際に煙突。
        /// <paramref name="edge"/> は庭の側の面の z、<paramref name="away"/> は隣の家の側（+1 が北）
        /// </summary>
        static void KitchenOutrigger(YardBanks y, float edge, float away, int seed)
        {
            const float x0 = KitchenYardWest;
            const float x1 = 7.9f;
            const float deep = 2.7f;
            const float eave = 5.2f;
            const float ridge = 6.5f;
            var far = edge + away * deep;
            var zm = (edge + far) * 0.5f;
            var face = away > 0f ? Vector3.back : Vector3.forward;
            y.Stock.Box(new Vector3((x0 + x1) * 0.5f, eave * 0.5f, zm), new Vector3(x1 - x0, eave, deep));
            // 屋根。棟は x に沿う
            var r0 = new Vector3(x0, ridge, zm);
            var r1 = new Vector3(x1 + 0.15f, ridge, zm);
            MidQuad(y.Slate, new Vector3(x0, eave - 0.05f, edge - away * 0.15f), new Vector3(x1 + 0.15f, eave - 0.05f, edge - away * 0.15f), r1, r0, face + Vector3.up);
            MidQuad(y.Slate, new Vector3(x0, eave - 0.05f, far + away * 0.15f), new Vector3(x1 + 0.15f, eave - 0.05f, far + away * 0.15f), r1, r0, -face + Vector3.up);
            MidQuad(y.Stock, new Vector3(x1, eave, edge), new Vector3(x1, eave, far), new Vector3(x1, ridge, zm), new Vector3(x1, ridge, zm), Vector3.right);
            // 窓。一階と二階
            foreach (var sill in new[] { 0.95f, 3.45f })
            {
                const float wx0 = 5.25f;
                const float wx1 = 6.15f;
                var h = sill < 2f ? 1.35f : 1.25f;
                var z = edge + face.z * 0.02f;
                y.Line.Box(new Vector3((wx0 + wx1) * 0.5f, sill + h * 0.5f, z), new Vector3(wx1 - wx0 + 0.12f, h + 0.12f, 0.04f));
                y.Pane.Box(new Vector3((wx0 + wx1) * 0.5f, sill + h * 0.5f, z + face.z * 0.012f), new Vector3(wx1 - wx0, h, 0.02f));
                y.Line.Box(new Vector3((wx0 + wx1) * 0.5f, sill + h * 0.5f, z + face.z * 0.03f), new Vector3(wx1 - wx0, 0.05f, 0.02f));
                y.Line.Box(new Vector3((wx0 + wx1) * 0.5f, sill + h * 0.5f, z + face.z * 0.03f), new Vector3(0.04f, h, 0.02f));
                y.Kerb.Box(new Vector3((wx0 + wx1) * 0.5f, sill - 0.05f, z + face.z * 0.05f), new Vector3(wx1 - wx0 + 0.2f, 0.08f, 0.12f));
                // 窓の上の煉瓦の平アーチ
                y.Brick.Box(new Vector3((wx0 + wx1) * 0.5f, sill + h + 0.12f, z + face.z * 0.005f), new Vector3(wx1 - wx0 + 0.24f, 0.2f, 0.03f));
            }
            // 裏口の戸。一階の家の際
            y.Leaf.Box(new Vector3(3.6f, 1.02f, edge + face.z * 0.02f), new Vector3(0.84f, 2.04f, 0.04f));
            y.Pane.Box(new Vector3(3.6f, 1.55f, edge + face.z * 0.045f), new Vector3(0.56f, 0.70f, 0.01f));
            // 雨樋と縦樋
            y.Iron.Box(new Vector3((x0 + x1) * 0.5f + 0.05f, eave - 0.08f, edge + face.z * 0.20f), new Vector3(x1 - x0 + 0.2f, 0.10f, 0.10f));
            y.Iron.Box(new Vector3(x1 - 0.15f, eave * 0.5f, edge + face.z * 0.08f), new Vector3(0.07f, eave, 0.07f));
            y.Iron.Box(new Vector3(4.6f, 1.6f, edge + face.z * 0.06f), new Vector3(0.10f, 3.2f, 0.10f));
            // 家の際の煙突
            var stack = new Vector3(x0 + 0.5f, ridge, zm);
            y.Stock.Box(stack + Vector3.up * 0.2f, new Vector3(0.6f, 1.8f, 0.9f));
            y.Line.Box(stack + Vector3.up * 1.12f, new Vector3(0.7f, 0.1f, 1.0f));
            for (var i = 0; i < 2; i++)
                y.Rubber.Box(stack + new Vector3(0f, 1.38f, i == 0 ? -0.2f : 0.2f), new Vector3(0.2f, 0.42f, 0.2f));
            // 張り出しの向こうの庭の木。塀と屋根の上に枝を出す
            YardPlane(y, new Vector3(11.0f, 0f, edge + away * 3.6f), 7.5f, seed);
        }

        /// <summary>
        /// 庭の小物。テラスの鉢と小さな卓と椅子、植え込みの塊、餌台、奥の南の隅の若い木
        /// </summary>
        static void KitchenYardThings(YardBanks y)
        {
            // テラスの鉢。素焼きの鉢に常緑の葉
            var pots = new[] { new Vector3(3.10f, 0f, -0.55f), new Vector3(3.05f, 0f, 2.95f), new Vector3(3.20f, 0f, -3.65f), new Vector3(4.60f, 0f, 3.00f) };
            for (var i = 0; i < pots.Length; i++)
            {
                var s = 0.30f + (i % 2) * 0.12f;
                y.Brick.Box(pots[i] + Vector3.up * (s * 0.5f), new Vector3(s, s, s), Quaternion.Euler(0f, i * 20f, 0f));
                y.Leaf.Box(pots[i] + Vector3.up * (s + 0.18f), new Vector3(s * 1.1f, 0.40f, s * 1.1f), Quaternion.Euler(0f, i * 33f, 0f));
            }
            // 小さな鉄の卓と椅子二脚
            var t = new Vector3(4.0f, 0f, -2.7f);
            y.Iron.Box(t + Vector3.up * 0.71f, new Vector3(0.62f, 0.02f, 0.62f), Quaternion.Euler(0f, 45f, 0f));
            y.Iron.Box(t + Vector3.up * 0.36f, new Vector3(0.04f, 0.70f, 0.04f));
            y.Iron.Box(t + Vector3.up * 0.02f, new Vector3(0.36f, 0.04f, 0.36f));
            for (var i = 0; i < 2; i++)
            {
                var c = t + new Vector3(i == 0 ? -0.55f : 0.55f, 0f, i == 0 ? 0.2f : -0.25f);
                var yaw = i == 0 ? 90f : 270f;
                var rot = Quaternion.Euler(0f, yaw, 0f);
                y.Iron.Box(c + Vector3.up * 0.44f, new Vector3(0.40f, 0.02f, 0.40f), rot);
                y.Iron.Box(c + rot * new Vector3(0f, 0.68f, -0.19f), new Vector3(0.40f, 0.48f, 0.02f), rot);
                for (var k = 0; k < 4; k++)
                    y.Iron.Box(c + rot * new Vector3((k & 1) == 0 ? -0.17f : 0.17f, 0.22f, (k & 2) == 0 ? -0.17f : 0.17f), new Vector3(0.025f, 0.44f, 0.025f), rot);
            }
            // 植え込みの塊。塀の足元の帯に
            ParkShrub(y, new Vector3(6.4f, 0f, 3.0f), 0.9f, 1);
            ParkShrub(y, new Vector3(8.6f, 0f, 3.05f), 1.1f, 2);
            ParkShrub(y, new Vector3(10.2f, 0f, 3.0f), 0.8f, 3);
            ParkShrub(y, new Vector3(6.9f, 0f, -4.8f), 1.0f, 4);
            ParkShrub(y, new Vector3(9.4f, 0f, -4.75f), 1.2f, 5);
            ParkShrub(y, new Vector3(13.55f, 0f, -1.6f), 0.9f, 6);
            // 餌台。北の植え込みの前に、細い柱と小さな屋根
            var feeder = new Vector3(7.3f, 0f, 2.55f);
            y.Iron.Box(feeder + Vector3.up * 0.85f, new Vector3(0.03f, 1.7f, 0.03f));
            y.Glass.Box(feeder + new Vector3(0f, 1.45f, 0f), new Vector3(0.08f, 0.22f, 0.08f));
            y.Iron.Box(feeder + new Vector3(0f, 1.60f, 0f), new Vector3(0.16f, 0.03f, 0.16f), Quaternion.Euler(0f, 45f, 0f));
            // 奥の南の隅の若い木
            YardPlane(y, new Vector3(12.8f, 0f, -3.9f), 5.5f, 229);
        }
    }
}
