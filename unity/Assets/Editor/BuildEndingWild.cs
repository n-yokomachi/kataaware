using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 作り込んだ景色の帯（2026-09-28、オーナー「エンディングの各背景の作りこみ」）。村の庭と同じ水準まで作る。
    /// お手本は 8 月のイングランドの実在の景色（下調べの要点はシナリオ設計 13.3）。
    /// 植物の絵は <c>tools/make-ending.py</c> が描く一枚のアトラス（<c>EndingWild.png</c>）で、村の庭の升のうちエンディングでも使う物も同じ一枚にまとめてある。
    ///
    /// - 帯 1 朝の森（<see cref="Woodland"/>）: 滑らかな灰色のブナの幹が高く並ぶ「木の大聖堂」と、道の上で重なる樹冠。
    ///   道端を 8 月の野の花が埋める（奥にヤナギランの赤紫の穂、中ほどにシモツケソウのクリームとヘンプ・アグリモニーのピンク、
    ///   手前にノップウィードの赤紫とマツムシソウの藤色、ハナウドの白い傘）。林の縁はキイチゴとワラビ。朝靄の中の光の筋
    /// - 帯 2 海辺の崖（<see cref="Cliffs"/>）: 右は崖の上のヒースとハリエニシダの紫と黄、杭と針金の柵、その向こうの青い海と沖の離れ岩の白い波。
    ///   左はコーンウォールの石垣（石を積んだ面に土を詰め、上に草とハリエニシダ）と、海風に曲げられたサンザシ、その奥の林
    /// </summary>
    public static partial class BuildEndingLand
    {
        // ---- アトラス（EndingWild.png。make-ending.py の CELLS と同じ並び） -------------------

        const int WildUnit = 128;
        const int WildWide = 1024;
        const int WildHigh = 1024;

        enum W
        {
            Willowherb, Hogweed, Meadowsweet, Knapweed, Scabious, Hemp, Ragwort, Grass,
            Bramble, Bracken, Gorse, Heather, Tuft, Thrift, Geranium,
            Beech, Hawthorn, Oak, Yew, Roses, Honeysuckle, Gull, GullUp, OxEye, Catmint, Mantle,
        }

        static readonly int[,] WildCells =
        {
            { 0, 0, 1, 3 }, { 1, 0, 1, 3 }, { 2, 0, 1, 2 }, { 3, 0, 1, 2 }, { 4, 0, 1, 2 }, { 5, 0, 1, 2 }, { 6, 0, 1, 2 }, { 7, 0, 1, 2 },
            { 2, 2, 2, 1 }, { 4, 2, 2, 1 }, { 6, 2, 2, 1 }, { 0, 3, 2, 1 }, { 2, 3, 2, 1 }, { 4, 3, 2, 1 }, { 6, 3, 2, 1 },
            { 0, 4, 2, 2 }, { 2, 4, 2, 2 }, { 4, 4, 2, 2 }, { 6, 4, 2, 2 }, { 0, 6, 2, 2 }, { 2, 6, 2, 2 },
            { 4, 6, 1, 1 }, { 4, 7, 1, 1 }, { 5, 6, 1, 2 }, { 6, 6, 2, 1 }, { 6, 7, 2, 1 },
        };

        /// <summary>
        /// 丈・幅（m）・札の枚数。丈は実物の草丈から（ヤナギラン 1.5 m、ハナウド 1.7 m、シモツケソウ 1.1 m、ノップウィード 0.75 m ほど）。
        /// 幅は絵の縦横の比に合わせる
        /// </summary>
        static readonly Vector3[] WildSizes =
        {
            new Vector3(1.50f, 0.62f, 2), new Vector3(1.70f, 0.75f, 2), new Vector3(1.15f, 0.60f, 2), new Vector3(0.75f, 0.48f, 2),
            new Vector3(0.85f, 0.50f, 2), new Vector3(1.20f, 0.62f, 2), new Vector3(0.85f, 0.50f, 2), new Vector3(0.95f, 0.55f, 2),
            new Vector3(1.00f, 2.00f, 2), new Vector3(0.90f, 1.80f, 3), new Vector3(0.85f, 1.60f, 3), new Vector3(0.38f, 0.80f, 3),
            new Vector3(0.45f, 0.90f, 3), new Vector3(0.28f, 0.60f, 3), new Vector3(0.42f, 0.85f, 3),
            new Vector3(8.0f, 9.0f, 3), new Vector3(4.2f, 4.6f, 2), new Vector3(6.0f, 7.0f, 3), new Vector3(5.6f, 4.4f, 3),
            new Vector3(1.00f, 1.00f, 2), new Vector3(1.00f, 1.00f, 2),
            new Vector3(1.30f, 1.30f, 1), new Vector3(1.30f, 1.30f, 1), new Vector3(0.90f, 0.48f, 2), new Vector3(0.48f, 0.95f, 3), new Vector3(0.36f, 0.75f, 3),
        };

        static void WildUv(W k, out Vector2 min, out Vector2 max)
        {
            var i = (int)k;
            const float insetU = 1.5f / WildWide;
            const float insetV = 1.5f / WildHigh;
            const float u = (float)WildUnit / WildWide;
            const float v = (float)WildUnit / WildHigh;
            var x0 = WildCells[i, 0] * u;
            var x1 = (WildCells[i, 0] + WildCells[i, 2]) * u;
            var y1 = 1f - WildCells[i, 1] * v;
            var y0 = 1f - (WildCells[i, 1] + WildCells[i, 3]) * v;
            min = new Vector2(x0 + insetU, y0 + insetV);
            max = new Vector2(x1 - insetU, y1 - insetV);
        }

        /// <summary>株を一つ（野の花と木のアトラス）。札を向きから等しい角度で回して交差させる</summary>
        static void Grow(Bank f, W k, Vector3 foot, float scale, float yaw, Vector3 lean)
        {
            var size = WildSizes[(int)k];
            var cards = Mathf.Max(1, (int)size.z);
            Grow(f, k, foot, scale, yaw, lean, cards, 180f / cards);
        }

        /// <summary>札の枚数と、札どうしの開き（度）を決めて植える。一枚目の札の横は yaw の向き</summary>
        static void Grow(Bank f, W k, Vector3 foot, float scale, float yaw, Vector3 lean, int cards, float spread)
        {
            Vector2 min, max;
            WildUv(k, out min, out max);
            var size = WildSizes[(int)k];
            var high = size.x * scale;
            var wide = size.y * scale;
            f.RootY = foot.y;
            f.RootHigh = high;
            var up = (Vector3.up + lean).normalized * high;
            for (var c = 0; c < cards; c++)
            {
                var a = (yaw + spread * c) * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f);
                f.AtlasCard(foot + Vector3.down * 0.03f, across, up, min, max);
            }
        }

        static W PickW(System.Random rnd, W[] kinds, float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights) sum += w;
            var r = (float)rnd.NextDouble() * sum;
            for (var i = 0; i < kinds.Length; i++)
            {
                r -= weights[i];
                if (r <= 0f) return kinds[i];
            }
            return kinds[kinds.Length - 1];
        }

        /// <summary>道端の花の段（野の花のアトラス）</summary>
        struct WildRow
        {
            public float from, to, scale, step;
            public int drift;
            public W[] kinds;
            public float[] weights;
        }

        /// <summary>段ごとに、同じ物のまとまりを道に沿って並べ、隣のまとまりと端を重ねる（色が帯状ににじむ）</summary>
        static void Meadow(Slice s, int side, WildRow[] rows, System.Func<float, float> height)
        {
            foreach (var row in rows)
            {
                var z = s.Next() * row.step;
                while (z < TileLength)
                {
                    var kind = PickW(s.rnd, row.kinds, row.weights);
                    var count = 2 + s.rnd.Next(row.drift);
                    for (var k = 0; k < count && z < TileLength; k++)
                    {
                        var d = s.Range(row.from, row.to);
                        var scale = row.scale * s.Range(0.8f, 1.2f);
                        var lean = d < RoadHalf + 0.6f ? new Vector3(-side * 0.2f, 0f, 0f) : Vector3.zero;
                        Grow(s.wild, kind, new Vector3(side * d, height(d), z), scale, s.Next() * 180f, lean);
                        z += row.step * s.Range(0.6f, 1.3f);
                    }
                }
            }
        }

        // ---- 帯 1 朝の森 ---------------------------------------------------------------

        /// <summary>
        /// 8 月の林の道端。お手本は南イングランドの道端の 8 月の花（ヤナギランの群れ・シモツケソウ・ノップウィード・マツムシソウ・
        /// ヘンプ・アグリモニー・ラグワート・ハナウド・背の高い草）。奥ほど背が高い三段にし、手前は道の縁へこぼれる
        /// </summary>
        static readonly WildRow[] WoodVerge =
        {
            // 道の縁の草。硬い縁を消す
            new WildRow { from = RoadHalf - 0.05f, to = RoadHalf + 0.35f, scale = 0.9f, drift = 3, step = 0.45f,
                kinds = new[] { W.Tuft, W.Tuft, W.Geranium }, weights = new[] { 1f, 1f, 0.3f } },
            // 手前。赤紫のノップウィード、藤色のマツムシソウ、白いフランスギク、黄のラグワート、青紫のゼラニウム
            new WildRow { from = RoadHalf + 0.2f, to = RoadHalf + 1.0f, scale = 1.0f, drift = 4, step = 0.40f,
                kinds = new[] { W.Knapweed, W.Scabious, W.OxEye, W.Ragwort, W.Geranium, W.Mantle, W.Tuft },
                weights = new[] { 1.3f, 1.1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.6f } },
            // 中ほど。クリームのシモツケソウ、ピンクのヘンプ・アグリモニー、背の高い草、ときどきハナウドの白い傘
            // （ハナウドは少しだけ。白い平らな傘がまとまると、低い目からは白い棚が並んだように見えた）
            new WildRow { from = RoadHalf + 0.8f, to = RoadHalf + 1.9f, scale = 1.0f, drift = 4, step = 0.45f,
                kinds = new[] { W.Meadowsweet, W.Hemp, W.Grass, W.Knapweed, W.Hogweed, W.OxEye },
                weights = new[] { 1.3f, 1.0f, 0.9f, 0.6f, 0.1f, 0.4f } },
            // 奥。ヤナギランの赤紫の穂の群れ（道端の 8 月の主役）。ハナウドとシモツケソウを混ぜる
            new WildRow { from = RoadHalf + 1.6f, to = 4.9f, scale = 1.0f, drift = 6, step = 0.48f,
                kinds = new[] { W.Willowherb, W.Hogweed, W.Meadowsweet, W.Grass },
                weights = new[] { 2.4f, 0.15f, 0.5f, 0.4f } },
        };

        /// <summary>ブナの林の縁（草の縁の外）。ここから外は林の地</summary>
        const float WoodEdge = 4.3f;

        static void Woodland(Slice s)
        {
            DirtRoad(s);
            for (var side = -1; side <= 1; side += 2)
            {
                Strip(s, side, RoadHalf - 0.05f, WoodEdge, VergeY, Swatch.Grass);
                Strip(s, side, WoodEdge - 0.05f, 90f, FloorY, Swatch.Floor);
                Meadow(s, side, WoodVerge, d => VergeY);
                Brambles(s, side, WoodEdge + 0.2f, WoodEdge + 2.4f);
                Beeches(s, side, true, 0.25f);
                Undergrowth(s, side);
                CanopyWall(s, side);
                DeepWood(s, side);
            }
            Shafts(s, MorningForest.aim);
            // 道しるべ（公の小径の指差しの杭）。環に一本
            if (s.index == 3) Fingerpost(s, -1, 6f);
        }

        /// <summary>林の縁の、キイチゴとワラビとスイカズラの茂み</summary>
        static void Brambles(Slice s, int side, float from, float to)
        {
            for (var z = s.Next() * 1.5f; z < TileLength; z += s.Range(1.1f, 2.0f))
            {
                var at = new Vector3(side * s.Range(from, to), FloorY, z);
                var pick = s.Next();
                if (pick < 0.45f) Grow(s.wild, W.Bramble, at, s.Range(0.9f, 1.3f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.75f) Grow(s.wild, W.Bracken, at, s.Range(0.9f, 1.3f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.88f) Grow(s.wild, W.Honeysuckle, at + Vector3.up * 0.2f, s.Range(1.0f, 1.4f), s.Next() * 180f, Vector3.zero);
                else Grow(s.wild, W.Roses, at + Vector3.up * 0.2f, s.Range(1.0f, 1.3f), s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>
        /// ブナ。滑らかな灰色の幹が高く真っ直ぐに立ち、樹冠は 8 m より上。道の近くの列は樹冠を道の上へ張り出して、道の上で左右が重なる
        /// （お手本はコッツウォルズのブナの林。「そびえる灰色の幹の下を歩くと、木の大聖堂に入ったよう」）。
        /// arch なら道の近くの列を置く。奥は疎らに、oaks の割合で楢を混ぜる
        /// </summary>
        static void Beeches(Slice s, int side, bool arch, float oaks)
        {
            if (arch)
            {
                var z = s.Next() * 4f;
                while (z < TileLength)
                {
                    var foot = new Vector3(side * s.Range(WoodEdge + 1.0f, WoodEdge + 4.2f), FloorY, z);
                    var trunk = s.Range(10f, 13f);
                    Trunk(s, foot, s.Range(0.30f, 0.44f), trunk);
                    var crown = s.Range(1.45f, 1.8f);
                    Grow(s.wild, W.Beech, foot + new Vector3(-side * s.Range(0.8f, 1.6f), trunk * 0.62f, 0f), crown, s.Next() * 180f, Vector3.zero);
                    // 道の上へ張り出した枝の葉。高く、道の側へ
                    Grow(s.wild, W.Beech, foot + new Vector3(-side * s.Range(3.4f, 4.6f), trunk * 0.80f, s.Range(-1.5f, 1.5f)), crown * 0.72f, s.Next() * 180f, Vector3.zero);
                    z += s.Range(7f, 10f);
                }
            }
            for (var i = 0; i < 6; i++)
            {
                var foot = new Vector3(side * s.Range(WoodEdge + 5f, 30f), FloorY, s.Next() * TileLength);
                var trunk = s.Range(8f, 12f);
                Trunk(s, foot, s.Range(0.22f, 0.40f), trunk);
                var oak = s.Next() < oaks;
                Grow(s.wild, oak ? W.Oak : W.Beech, foot + new Vector3(0f, trunk * (oak ? 0.55f : 0.62f), 0f), s.Range(1.3f, 1.7f), s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>ブナの幹。滑らかな灰色の、上へ少し細る九角柱。根元で少し張る</summary>
        static void Trunk(Slice s, Vector3 foot, float radius, float high)
        {
            var lean = new Vector3(s.Range(-0.3f, 0.3f), 0f, s.Range(-0.3f, 0.3f));
            s.paint.Prism(foot + Vector3.down * 0.1f, radius * 1.35f, radius, 0.7f, 9, s.Next() * 40f, Swatch.BeechBark, null);
            s.paint.Prism(foot + Vector3.up * 0.5f, radius, radius * 0.62f, high, 9, s.Next() * 40f, Swatch.BeechBark, null, null, 0f, lean);
        }

        /// <summary>林の地の下草。日の当たる所のワラビの群れ、ヒイラギのような暗い低木、倒れた幹</summary>
        static void Undergrowth(Slice s, int side)
        {
            for (var i = 0; i < 10; i++)
            {
                var at = new Vector3(side * s.Range(WoodEdge + 2.5f, 24f), FloorY, s.Next() * TileLength);
                Grow(s.wild, W.Bracken, at, s.Range(0.9f, 1.4f), s.Next() * 180f, Vector3.zero);
            }
            for (var i = 0; i < 3; i++)
            {
                var at = new Vector3(side * s.Range(WoodEdge + 3f, 22f), FloorY, s.Next() * TileLength);
                Grow(s.wild, W.Yew, at, s.Range(0.28f, 0.42f), s.Next() * 180f, Vector3.zero);
            }
            if (s.Next() < 0.35f)
            {
                var a = new Vector3(side * s.Range(WoodEdge + 3f, 12f), FloorY + 0.2f, s.Range(2f, 16f));
                var dir = Quaternion.Euler(0f, s.Range(0f, 180f), 0f) * Vector3.forward;
                var r = s.Range(0.22f, 0.34f);
                s.paint.Log(a, a + dir * s.Range(3f, 6f), r, 7, Swatch.Bark, Swatch.Moss);
            }
        }

        /// <summary>いちばん奥の樹冠の壁。霞に溶けて、林の外が見えないように</summary>
        static void CanopyWall(Slice s, int side)
        {
            for (var z = s.Next() * 3f; z < TileLength; z += s.Range(3f, 5f))
            {
                var pick = s.Next();
                var kind = pick < 0.5f ? W.Beech : pick < 0.8f ? W.Oak : W.Yew;
                Grow(s.wild, kind, new Vector3(side * s.Range(28f, 36f), FloorY - 0.5f, z), s.Range(1.3f, 1.9f), s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>
        /// 林のいちばん奥の、霞んだ幹と葉の暗い面。奥の樹冠の壁の札は下が抜けているので、無いと足元の隙間から空が覗いた
        /// </summary>
        static void DeepWood(Slice s, int side)
        {
            const float x = 42f, high = 16f;
            if (side < 0)
                s.paint.Quad(new Vector3(-x, FloorY - 1f, TileLength), new Vector3(-x, FloorY - 1f, 0f), new Vector3(-x, high, 0f), new Vector3(-x, high, TileLength), Swatch.HillFar);
            else
                s.paint.Quad(new Vector3(x, FloorY - 1f, 0f), new Vector3(x, FloorY - 1f, TileLength), new Vector3(x, high, TileLength), new Vector3(x, high, 0f), Swatch.HillFar);
        }

        /// <summary>
        /// 林の光の筋。樹冠の隙間から日の向き（aim）に沿って地面へ落ちる、加算の細長い板。
        /// 前から見える向きと、横から見える向きの二枚を交差させる。霞の中で光が見える、朝の林の形
        /// </summary>
        static void Shafts(Slice s, Vector3 aim)
        {
            var light = Quaternion.Euler(aim) * Vector3.forward;
            var across = Vector3.Cross(Vector3.up, light).normalized;
            var other = Vector3.Cross(light, across).normalized;
            for (var i = 0; i < 4; i++)
            {
                var top = new Vector3(s.Range(-11f, 11f), s.Range(10f, 13.5f), s.Next() * TileLength);
                var reach = (top.y - FloorY) / Mathf.Max(0.2f, -light.y);
                var bottom = top + light * reach;
                var half = s.Range(0.35f, 0.9f);
                foreach (var h in new[] { across, other })
                {
                    s.shafts.Patch(top - h * half, top + h * half, bottom + h * half, bottom - h * half,
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
                }
            }
        }

        /// <summary>公の小径の指差しの杭。木の杭に、小径の方を指す腕</summary>
        static void Fingerpost(Slice s, int side, float z)
        {
            var at = new Vector3(side * (RoadHalf + 0.55f), VergeY, z);
            s.paint.Box(at + Vector3.up * 0.7f, new Vector3(0.11f, 1.4f, 0.11f), 0f, Swatch.Wood, Swatch.Wood);
            s.paint.Box(at + new Vector3(side * 0.30f, 1.24f, 0f), new Vector3(0.62f, 0.13f, 0.04f), 0f, Swatch.Wood, Swatch.Wood);
            s.paint.Box(at + new Vector3(side * 0.30f, 1.24f, -0.026f), new Vector3(0.46f, 0.05f, 0.01f), 0f, Swatch.Line, Swatch.Line);
        }

        // ---- 帯 2 海辺の崖 ---------------------------------------------------------------

        /// <summary>
        /// 海辺の崖の道。お手本はコーンウォールの北の海岸（セント・アグネス岬など）の 8 月。
        /// - 崖の上をハリエニシダ（ウエスタン・ゴース）の黄とヒースの紫が絡んで覆い、その向こうに青い海と、沖の離れ岩に砕ける白い波
        /// - 道の内陸の側は、コーンウォールの石垣（下に大きな石、上へ揃った石を積んだ二枚の面に土を詰め、上に草とハリエニシダ）。
        ///   石垣の上に、海風で内陸へ曲げられたサンザシ
        /// - 繰り返す物は、崖の縁に沿った杭と針金の柵の杭。遠くの岬には錫の鉱山の機関場の煙突（遠景）、空にカモメ
        /// </summary>
        static void Cliffs(Slice s)
        {
            DirtRoad(s);
            // 左（内陸）。石垣と、その上の草とハリエニシダ、曲がったサンザシ、奥の林
            Strip(s, -1, RoadHalf - 0.05f, HedgeFoot + 0.02f, VergeY, Swatch.Grass);
            CornishHedge(s);
            Strip(s, -1, HedgeFoot + HedgeBase - 0.1f, 90f, FloorY, Swatch.Floor);
            Meadow(s, -1, new[]
            {
                new WildRow { from = RoadHalf - 0.05f, to = HedgeFoot - 0.05f, scale = 0.85f, drift = 3, step = 0.5f,
                    kinds = new[] { W.Tuft, W.Knapweed, W.Scabious, W.Thrift }, weights = new[] { 1.4f, 0.6f, 0.4f, 0.2f } },
            }, d => VergeY);
            Beeches(s, -1, false, 0.6f);
            CanopyWall(s, -1);
            DeepWood(s, -1);
            // 右（海）。草の縁と崖の上の芝、柵、ヒースとハリエニシダ、崖、海
            Strip(s, 1, RoadHalf - 0.05f, 3.0f, VergeY, Swatch.Grass);
            const int steps = 8;
            var dz = TileLength / steps;
            for (var j = 0; j < steps; j++)
            {
                float z0 = j * dz, z1 = z0 + dz;
                var e0 = CliffEdge(s, s.Offset + z0);
                var e1 = CliffEdge(s, s.Offset + z1);
                s.paint.Quad(new Vector3(2.95f, VergeY - 0.005f, z1), new Vector3(e1, -0.05f, z1), new Vector3(e0, -0.05f, z0),
                    new Vector3(2.95f, VergeY - 0.005f, z0), Swatch.Turf);
                CliffFace(s, j, z0, z1, e0, e1);
            }
            Fence(s, FenceX);
            Heathland(s);
            s.sea.Patch(new Vector3(6f, SeaY, TileLength), new Vector3(1500f, SeaY, TileLength), new Vector3(1500f, SeaY, 0f), new Vector3(6f, SeaY, 0f),
                new Vector2(6f / 8f, 5f), new Vector2(1500f / 8f, 5f), new Vector2(1500f / 8f, 0f), new Vector2(6f / 8f, 0f));
            Stacks(s);
            Whitecaps(s);
        }

        /// <summary>石垣の道の側の面の根元（道の真ん中から、左へ）</summary>
        const float HedgeFoot = 2.45f;
        /// <summary>石垣の高さ</summary>
        const float HedgeHigh = 1.35f;
        /// <summary>石垣の根元の厚み。上は 0.8 m（下に大きな石を置くので、上へ細る）</summary>
        const float HedgeBase = 1.5f;
        /// <summary>道の側の面の傾き。上へ行くほど道から離れる（m）</summary>
        const float HedgeBatter = 0.32f;

        /// <summary>
        /// コーンウォールの石垣。道の側の面に石を段に積む（下の段は大きな据え石、上へ行くほど揃った四角い石）。
        /// 石ごとに花崗岩・その陰・粘板岩・石灰岩の色を振り、面から少しずつ出し入れする。石のあいだの目地は奥の暗い面。
        /// 上は芝で、草とハリエニシダとヒースとハマカンザシが載り、ところどころに海風で曲げられたサンザシ
        /// </summary>
        static void CornishHedge(Slice s)
        {
            System.Func<float, float> faceX = y => -(HedgeFoot + HedgeBatter * y / HedgeHigh);
            // 目地（奥の暗い面）
            s.paint.Quad(new Vector3(faceX(0f) - 0.02f, 0f, TileLength), new Vector3(faceX(0f) - 0.02f, 0f, 0f),
                new Vector3(faceX(HedgeHigh) - 0.02f, HedgeHigh, 0f), new Vector3(faceX(HedgeHigh) - 0.02f, HedgeHigh, TileLength), Swatch.RockDark);
            var y0 = 0f;
            var course = 0;
            while (y0 < HedgeHigh - 0.05f)
            {
                var big = course == 0;
                var h = Mathf.Min(HedgeHigh - y0, big ? s.Range(0.28f, 0.34f) : s.Range(0.15f, 0.22f));
                var z = -s.Next() * 0.3f;
                while (z < TileLength)
                {
                    var len = big ? s.Range(0.5f, 0.9f) : s.Range(0.22f, 0.5f);
                    float za = Mathf.Max(0f, z + 0.02f), zb = Mathf.Min(TileLength, z + len - 0.02f);
                    if (zb > za + 0.05f)
                    {
                        float ya = y0 + 0.018f, yb = y0 + h - 0.018f;
                        var out0 = s.Range(0.01f, 0.05f);
                        var pick = s.Next();
                        var col = pick < 0.4f ? Swatch.Granite : pick < 0.65f ? Swatch.GraniteDark : pick < 0.85f ? Swatch.Slate : Swatch.Stone;
                        // 道の側（+x）を向く面（Bank.FaceX の sign 1 と同じ並び）
                        s.paint.Quad(new Vector3(faceX(ya) + out0, ya, zb), new Vector3(faceX(ya) + out0, ya, za),
                            new Vector3(faceX(yb) + out0, yb, za), new Vector3(faceX(yb) + out0, yb, zb), col);
                    }
                    z += len;
                }
                y0 += h;
                course++;
            }
            // 上の芝
            var topIn = faceX(HedgeHigh);
            s.paint.Flat(-(HedgeFoot + HedgeBase - 0.4f), topIn + 0.02f, 0f, TileLength, HedgeHigh + 0.02f, Swatch.Turf);
            // 上の草とハリエニシダ
            for (var z = s.Next() * 0.8f; z < TileLength; z += s.Range(0.5f, 1.0f))
            {
                var at = new Vector3(s.Range(topIn - 0.55f, topIn - 0.05f), HedgeHigh, z);
                var pick = s.Next();
                if (pick < 0.30f) Grow(s.wild, W.Gorse, at, s.Range(0.55f, 0.85f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.62f) Grow(s.wild, W.Tuft, at, s.Range(0.8f, 1.1f), s.Next() * 180f, new Vector3(0.15f, 0f, 0f));
                else if (pick < 0.78f) Grow(s.wild, W.Heather, at, s.Range(0.8f, 1.1f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.88f) Grow(s.wild, W.Thrift, at, s.Range(0.8f, 1.1f), s.Next() * 180f, Vector3.zero);
                else Grow(s.wild, W.Bramble, at, s.Range(0.6f, 0.8f), s.Next() * 180f, Vector3.zero);
            }
            // 海風で内陸（左）へ曲げられたサンザシ。石垣の上に、10 m ほどおき。樹冠は道から 2〜3.5 m の高さ
            // （背を 5 m ほどにすると、斜めの札が道に沿った長い屋根のように見えた）。
            // 一枚目の札は横が +x（絵の左、樹冠の流れる側が内陸の -x に来る）。前から見て傾きが読める
            for (var z = s.Range(1f, 8f); z < TileLength; z += s.Range(9f, 14f))
            {
                var at = new Vector3(-(HedgeFoot + HedgeBase * 0.72f), HedgeHigh - 0.1f, z);
                Grow(s.wild, W.Hawthorn, at, s.Range(0.7f, 0.9f), 0f, Vector3.zero, 2, 60f);
            }
        }

        /// <summary>崖の縁の杭と針金の柵の x。崖の縁（道から 5〜7 m）の手前</summary>
        const float FenceX = 4.1f;

        /// <summary>杭と針金の柵。杭は 2.4 m おきに少しずつ傾けて立て、針金を二本張る</summary>
        static void Fence(Slice s, float x)
        {
            for (var z = 0.6f; z < TileLength; z += 2.4f)
            {
                var tilt = s.Range(-3f, 3f);
                s.paint.Box(new Vector3(x + tilt * 0.004f, 0.5f, z), new Vector3(0.10f, 1.05f, 0.10f), s.Range(0f, 90f), Swatch.Wood, Swatch.Wood);
                foreach (var y in new[] { 0.52f, 0.88f })
                    s.paint.Box(new Vector3(x, y, z + 1.2f), new Vector3(0.02f, 0.02f, 2.4f), 0f, Swatch.Slate, Swatch.Slate);
            }
        }

        /// <summary>
        /// 崖の上の荒れ地。ヒースの紫の群れとハリエニシダの黄の丸い茂みを継ぎはぎに、崖の縁にはハマカンザシ。
        /// 窓から沖の岩の根元を見下ろす線（水平から 8〜11 度下）を塞がないよう、縁に近いほど低い株にする
        /// </summary>
        static void Heathland(Slice s)
        {
            // 道と柵のあいだ。ハリエニシダの丸い茂みを二つ三つずつ寄せ、あいだは草とヒースとマツムシソウ
            // （道に沿って切れ目なく並べると、黄色い生け垣の帯に見えた）
            var along = s.Next() * 3f;
            while (along < TileLength)
            {
                var gorse = s.Next() < 0.55f;
                var count = 1 + s.rnd.Next(3);
                for (var k = 0; k < count && along < TileLength; k++)
                {
                    var at = new Vector3(s.Range(RoadHalf + 0.5f, FenceX - 0.3f), VergeY, along);
                    if (gorse) Grow(s.wild, W.Gorse, at, s.Range(0.6f, 0.95f), s.Next() * 180f, Vector3.zero);
                    else
                    {
                        var pick = s.Next();
                        var kind = pick < 0.45f ? W.Heather : pick < 0.8f ? W.Tuft : W.Scabious;
                        Grow(s.wild, kind, at, kind == W.Scabious ? s.Range(0.7f, 0.9f) : s.Range(0.9f, 1.2f), s.Next() * 180f, Vector3.zero);
                    }
                    along += s.Range(0.9f, 1.5f);
                }
                along += s.Range(0.5f, 1.5f);
            }
            // ヒース。柵の外まで、群れにして
            for (var i = 0; i < 9; i++)
            {
                var cz = s.Next() * TileLength;
                var edge = CliffEdge(s, s.Offset + cz);
                var cx = s.Range(RoadHalf + 0.8f, edge - 0.4f);
                for (var k = 0; k < 4; k++)
                {
                    var z = Mathf.Clamp(cz + s.Range(-1.2f, 1.2f), 0f, TileLength);
                    var x = Mathf.Min(cx + s.Range(-0.8f, 0.8f), CliffEdge(s, s.Offset + z) - 0.3f);
                    Grow(s.wild, W.Heather, new Vector3(x, VergeY, z), s.Range(0.8f, 1.2f), s.Next() * 180f, Vector3.zero);
                }
            }
            // 柵の外の低いハリエニシダと、縁のハマカンザシと草
            for (var i = 0; i < 12; i++)
            {
                var z = s.Next() * TileLength;
                var edge = CliffEdge(s, s.Offset + z);
                var x = s.Range(FenceX + 0.2f, edge - 0.3f);
                var pick = s.Next();
                var kind = pick < 0.3f ? W.Gorse : pick < 0.65f ? W.Thrift : W.Tuft;
                Grow(s.wild, kind, new Vector3(x, VergeY, z), kind == W.Gorse ? s.Range(0.45f, 0.6f) : s.Range(0.8f, 1.1f), s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>崖の面の一段（縁 → 中ほど → 根元）。面ごとに明るい岩と陰の岩を混ぜて、角の立った岩肌にする</summary>
        static void CliffFace(Slice s, int j, float z0, float z1, float e0, float e1)
        {
            System.Func<float, float, Vector3> mid = (e, zr) =>
                new Vector3(e + 1.6f + 0.9f * Mathf.Sin(2f * Mathf.PI * zr / s.Span * 11f), -7f + 1.2f * Mathf.Sin(2f * Mathf.PI * zr / s.Span * 13f + 1f), zr - s.Offset);
            System.Func<float, float, Vector3> foot = (e, zr) =>
                new Vector3(e + 3.8f + 1.3f * Mathf.Sin(2f * Mathf.PI * zr / s.Span * 5f + 1f), SeaY - 0.8f, zr - s.Offset);
            var top0 = new Vector3(e0, -0.05f, z0);
            var top1 = new Vector3(e1, -0.05f, z1);
            var mid0 = mid(e0, s.Offset + z0);
            var mid1 = mid(e1, s.Offset + z1);
            var foot0 = foot(e0, s.Offset + z0);
            var foot1 = foot(e1, s.Offset + z1);
            s.paint.Quad(top1, mid1, mid0, top0, (j % 3 == 0) ? Swatch.RockDark : Swatch.Rock);
            s.paint.Quad(mid1, foot1, foot0, mid0, (j % 2 == 0) ? Swatch.Rock : Swatch.RockDark);
            s.paint.Quad(new Vector3(foot1.x - 0.3f, SeaY + 0.05f, z1), new Vector3(foot1.x + 1.4f + s.Next(), SeaY + 0.05f, z1),
                new Vector3(foot0.x + 1.4f + s.Next(), SeaY + 0.05f, z0), new Vector3(foot0.x - 0.3f, SeaY + 0.05f, z0), Swatch.Foam);
        }

        /// <summary>
        /// 沖の離れ岩と、根元の白い波。岩は上へ細る柱を三段に積み、段ごとに芯をずらして角を立てる
        /// （一本の多角柱では、灰色の塔か建物に見えた）。根元は波に濡れた暗い岩、上は明るい岩で、天に芝
        /// </summary>
        static void Stacks(Slice s)
        {
            var stacks = 1 + (s.Next() < 0.55f ? 1 : 0);
            for (var k = 0; k < stacks; k++)
            {
                var at = new Vector3(s.Range(30f, 90f), SeaY - 1f, s.Range(3f, 17f));
                var r = s.Range(2.5f, 6f);
                var high = s.Range(7f, 16f);
                Crag(s, at, r, high);
                // 脇の低い岩
                var side = at + new Vector3(r * s.Range(0.6f, 1.2f), 0f, r * s.Range(-1.2f, 1.2f));
                Crag(s, side, r * s.Range(0.35f, 0.55f), high * s.Range(0.3f, 0.5f));
                Foam(s, new Vector3(at.x, SeaY + 0.06f, at.z), r * 1.05f, r * 1.05f + s.Range(2f, 4f));
                // 根元の岩礁と、そこに砕ける泡
                for (var q = 0; q < 3; q++)
                {
                    var a = s.Range(0f, 6.28f);
                    var rr = r * s.Range(1.3f, 2.0f);
                    var rock = at + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                    var small = s.Range(0.6f, 1.4f);
                    s.paint.Prism(rock, small, small * 0.5f, s.Range(1.2f, 2.2f), 5, s.Next() * 60f, Swatch.Slate, Swatch.RockDark, s.rnd, 0.4f);
                    Foam(s, new Vector3(rock.x, SeaY + 0.07f, rock.z), small * 1.05f, small + s.Range(0.8f, 1.6f));
                }
            }
        }

        /// <summary>岩の柱を三段に積む。foot は根元（海の面の下）、r は根元の半径、high は丈</summary>
        static void Crag(Slice s, Vector3 foot, float r, float high)
        {
            var h0 = high * s.Range(0.4f, 0.5f);
            var h1 = high * s.Range(0.3f, 0.35f);
            var h2 = high - h0 - h1;
            var r1 = r * s.Range(0.7f, 0.85f);
            var r2 = r1 * s.Range(0.6f, 0.8f);
            var sides = 6 + s.rnd.Next(2);
            var lean0 = new Vector3(s.Range(-0.2f, 0.2f), 0f, s.Range(-0.2f, 0.2f)) * r;
            var lean1 = new Vector3(s.Range(-0.25f, 0.25f), 0f, s.Range(-0.25f, 0.25f)) * r1;
            s.paint.Prism(foot, r, r1 * 1.05f, h0, sides, s.Next() * 60f, Swatch.Slate, Swatch.RockDark, s.rnd, 0.35f, lean0);
            var mid = foot + lean0 + Vector3.up * (h0 - 0.2f);
            s.paint.Prism(mid, r1, r2 * 1.05f, h1, sides, s.Next() * 60f, Swatch.RockDark, Swatch.Rock, s.rnd, 0.35f, lean1);
            var top = mid + lean1 + Vector3.up * (h1 - 0.2f);
            s.paint.Prism(top, r2, r2 * s.Range(0.5f, 0.75f), h2, sides, s.Next() * 60f, Swatch.Rock, Swatch.Turf, s.rnd, 0.3f);
        }

        /// <summary>沖の白波。海の面の上の、細長い白い筋。風の向きに揃えて斜めに</summary>
        static void Whitecaps(Slice s)
        {
            for (var i = 0; i < 12; i++)
            {
                var at = new Vector3(s.Range(14f, 160f), SeaY + 0.04f, s.Next() * TileLength);
                var len = s.Range(1.5f, 4.5f) * (1f + at.x / 120f);
                var wide = s.Range(0.2f, 0.5f) * (1f + at.x / 80f);
                var dir = new Vector3(0.45f, 0f, 0.89f);
                var side = new Vector3(dir.z, 0f, -dir.x);
                s.paint.Quad(at - side * wide + dir * len, at + side * wide + dir * len, at + side * wide - dir * len, at - side * wide - dir * len, Swatch.Foam);
            }
        }

        // ---- 遠景の書き足し ------------------------------------------------------------

        /// <summary>
        /// 岬の上の、錫の鉱山の機関場（コーンウォールの海岸の目印）。背の高い石の箱の建物と、角に寄り添う細い煙突。
        /// 方位 deg、隔たり dist、足元の高さ ground。崖の面（半径 380 m）より奥に置き、足元は崖の縁に隠れる
        /// </summary>
        static void EngineHouse(Paint p, float deg, float dist, float ground)
        {
            var a = deg * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * dist + Vector3.up * ground;
            var across = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            // 建物（幅 8 m・奥行 11 m・高さ 15 m）。足元を 3 m 埋める
            p.Box(at + Vector3.up * 6f, new Vector3(8f, 18f, 11f), deg, Swatch.GraniteDark, Swatch.GraniteDark);
            // 破風の三角（道の側を向く）
            p.Prism(at + Vector3.up * 15f, 5.6f, 0.3f, 4.5f, 4, 45f - deg, Swatch.GraniteDark, null);
            // 煙突。建物の角に、上へ細る丸い柱。建物より 10 m ほど高く
            p.Prism(at + across * 5.2f + Vector3.down * 3f, 1.3f, 0.85f, 29f, 8, 0f, Swatch.Granite, Swatch.RockDark);
        }

        /// <summary>
        /// カモメ。崖に吹き上げる風に乗って、ほとんど羽ばたかずに同じ所に留まる。動かない遠景に置いて、<see cref="Hover"/> で揺らす。
        /// 車と同じ速さで並んで滑るように見える。札は運転席の目の方を向ける（窓からも前からも形が読める）。置いた羽数を返す
        /// </summary>
        static int Gulls(Transform parent)
        {
            var m = Materials();
            var eye = new Vector3(0.4f, 1.2f, 0f);
            var spots = new[] { new Vector3(9f, 5f, 7f), new Vector3(17f, 9f, 24f), new Vector3(30f, 4f, 42f) };
            var made = 0;
            for (var i = 0; i < spots.Length; i++)
            {
                var b = new Bank { Texel = 1f, Rooted = true, RootFixed = new Vector2(0f, 1f), CardLift = 0f };
                Vector2 min, max;
                WildUv(i == 1 ? W.GullUp : W.Gull, out min, out max);
                var size = WildSizes[(int)W.Gull] * 1.2f;
                b.AtlasCard(new Vector3(0f, -size.x * 0.5f, 0f), new Vector3(size.y * 0.5f, 0f, 0f), new Vector3(0f, size.x, 0f), min, max);
                var go = b.Emit(parent, "Gull" + i, m.wild, false, BuildEnding.Generated + "FarCoast_");
                if (go == null) continue;
                made++;
                go.transform.localPosition = spots[i];
                // 札の表（+z）を目へ向け、翼の先がどちらへ向くかを一羽ずつ変える
                go.transform.localRotation = Quaternion.LookRotation(eye - spots[i]) * Quaternion.Euler(0f, i == 2 ? 180f : 0f, 0f);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var hover = go.AddComponent<Hover>();
                var so = new SerializedObject(hover);
                so.FindProperty("phase").floatValue = i * 2.1f;
                so.FindProperty("rate").floatValue = 0.5f + i * 0.13f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return made;
        }

        // ---- マテリアル ------------------------------------------------------------------

        /// <summary>野の花と木の札のマテリアル（EndingWild.png、HalfAware/Foliage）。揺れは村の写しと同じく小さく</summary>
        static Material WildMat()
        {
            const string path = BuildEnding.Materials + "EndingWild.mat";
            var shader = Shader.Find("HalfAware/Foliage");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "EndingWild" };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", Atlas(BuildEnding.Textures + "EndingWild.png", true));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Wrap", 0.5f);
            m.SetFloat("_Glow", 0.32f);
            m.SetFloat("_Shade", 0.35f);
            m.SetFloat("_SkyLift", 0.25f);
            m.SetFloat("_Sway", 0.03f);
            m.SetFloat("_SwayRate", 1.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>林の光の筋（加算。霧を色として混ぜない HalfAware/RoadGlow）。朝の日の色を薄く</summary>
        static Material ShaftMat()
        {
            const string path = BuildEnding.Materials + "EndingShaft.mat";
            var shader = Shader.Find("HalfAware/RoadGlow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "EndingShaft" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", Atlas(BuildEnding.Textures + "EndingShaft.png", false));
            m.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.12f, 1f));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>海辺の帯の海。昼前の青。細かい波の明暗の絵を貼り、艶で日を拾う</summary>
        static Material SeaMat()
        {
            var m = Lit("EndingSea", new Color(0.05f, 0.26f, 0.44f), 0.86f);
            m.SetTexture("_BaseMap", WaterTexture());
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>make-ending.py の描いた絵の取り込み。cutout ならアトラス（α で抜く・ミップで被覆を保つ）、でなければ光の筋（切り詰め）</summary>
        static Texture2D Atlas(string path, bool cutout)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null)
            {
                Debug.LogWarning("絵が無い。python tools/make-ending.py を走らせる: " + path);
                return null;
            }
            var sign = cutout ? "wild1" : "shaft1";
            if (importer.userData != sign)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = cutout;
                importer.mipMapsPreserveCoverage = cutout;
                importer.alphaTestReferenceValue = 0.5f;
                importer.maxTextureSize = 1024;
                importer.userData = sign;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
