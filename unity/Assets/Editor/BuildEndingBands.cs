using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// エンディングの景色の帯の中身（<see cref="BuildEndingLand.Route"/> の各行）。区切り 20 m 一枚ぶんを組む。
    /// 右（+x）が運転席の側、左（-x）が助手席の側。起伏は環の一周で閉じる形（区切りの継ぎ目と、環の継ぎ目で段が出ない）
    /// </summary>
    public static partial class BuildEndingLand
    {
        const float VergeY = -0.015f;
        const float FloorY = -0.03f;

        // ---- 道 --------------------------------------------------------------------------

        /// <summary>村へ続く土の道（村の路面の絵）。区切りに 3 回繰り返す</summary>
        static void DirtRoad(Slice s)
        {
            const float r = 3f;
            s.road.Patch(new Vector3(-RoadHalf, 0f, TileLength), new Vector3(RoadHalf, 0f, TileLength),
                new Vector3(RoadHalf, 0f, 0f), new Vector3(-RoadHalf, 0f, 0f),
                new Vector2(0f, r), new Vector2(1f, r), new Vector2(1f, 0f), new Vector2(0f, 0f));
        }

        /// <summary>舗装の道。真ん中に擦れた白線の破線</summary>
        static void PavedRoad(Slice s, float half)
        {
            s.paint.Flat(-half, half, 0f, TileLength, 0f, Swatch.Asphalt);
            for (var z = 1f; z < TileLength; z += 5f)
                s.paint.Flat(-0.06f, 0.06f, z, z + 2.2f, 0.006f, Swatch.Line);
        }

        // ---- 夜の湖の助手席の側の森（帯 7。帯 1・2 は BuildEndingWild の作り込んだ林） -------------

        /// <summary>道の脇の花の段。道の真ん中からの隔たり・丈の倍率・種類と重み・まとまり・間隔</summary>
        struct Row
        {
            public float from, to, scale, step;
            public int drift;
            public Wild[] kinds;
            public float[] weights;
        }

        /// <summary>
        /// 8 月のイングランドの道端の野の花。手前の低い株（青紫のゼラニウム・キャットミント、黄緑のレディースマントル）が草の縁へこぼれ、
        /// 中ほどに白い花（エキナセアの白をフランスギクに見立てる）を主に紫とピンクと黄、奥にピンクの穂（タチアオイのピンクをヤナギランに見立てる）。
        /// 白いタチアオイは入れない（奥の段に並ぶと、白い柵のように見えた）
        /// </summary>
        static readonly Row[] ForestVerge =
        {
            new Row { from = RoadHalf + 0.05f, to = RoadHalf + 0.9f, scale = 1.1f, drift = 4, step = 0.55f,
                kinds = new[] { Wild.Geranium, Wild.Catmint, Wild.Mantle, Wild.Filler, Wild.Potato }, weights = new[] { 1.2f, 1.0f, 0.8f, 0.9f, 0.5f } },
            new Row { from = RoadHalf + 0.7f, to = RoadHalf + 1.9f, scale = 1.05f, drift = 5, step = 0.45f,
                kinds = new[] { Wild.EchWhite, Wild.Aster, Wild.EchPink, Wild.Rudbeckia, Wild.Filler }, weights = new[] { 1.6f, 1.1f, 0.8f, 0.6f, 0.5f } },
            new Row { from = RoadHalf + 1.6f, to = 4.9f, scale = 0.95f, drift = 5, step = 0.6f,
                kinds = new[] { Wild.HollyPink, Wild.DahliaPink, Wild.Aster, Wild.EchWhite }, weights = new[] { 1.6f, 0.4f, 0.6f, 0.6f } },
        };

        /// <summary>草の縁の外の端。ここから外は森の地</summary>
        const float VergeTo = 4.6f;

        /// <summary>森の片側。flowers は花の多さの割合（夜の湖は少なく）</summary>
        static void ForestSide(Slice s, int side, float flowers)
        {
            Strip(s, side, RoadHalf - 0.05f, VergeTo, VergeY, Swatch.Grass);
            Strip(s, side, VergeTo - 0.05f, 48f, FloorY, Swatch.Floor);
            if (flowers > 0f) Flowers(s, side, ForestVerge, flowers, h => VergeY);
            Thicket(s, side);
            Trees(s, side);
            TreeWall(s, side);
        }

        /// <summary>道の片側の、z に沿った平らな帯</summary>
        static void Strip(Slice s, int side, float near, float far, float y, Swatch col)
        {
            s.paint.Flat(Mathf.Min(side * near, side * far), Mathf.Max(side * near, side * far), 0f, TileLength, y, col);
        }

        /// <summary>花の段を植える。段ごとに、同じ物のまとまりを道に沿って並べ、隣のまとまりと端を重ねる。height は隔たりから足元の高さ</summary>
        static void Flowers(Slice s, int side, Row[] rows, float density, System.Func<float, float> height)
        {
            foreach (var row in rows)
            {
                var z = s.Next() * row.step;
                while (z < TileLength)
                {
                    var kind = Pick(s.rnd, row.kinds, row.weights);
                    var count = 2 + s.rnd.Next(row.drift);
                    for (var k = 0; k < count && z < TileLength; k++)
                    {
                        var d = s.Range(row.from, row.to);
                        var scale = row.scale * s.Range(0.8f, 1.2f);
                        var lean = row.from < RoadHalf + 0.5f ? new Vector3(-side * 0.18f, 0f, 0f) : Vector3.zero;
                        if (s.Next() <= density) Clump(s.flora, kind, new Vector3(side * d, height(d), z), scale, s.Next() * 180f, lean);
                        z += row.step * s.Range(0.6f, 1.3f) / Mathf.Max(0.2f, density);
                    }
                }
            }
        }

        /// <summary>草の縁の奥の藪（野バラとスイカズラと下草）と、森の中の下草</summary>
        static void Thicket(Slice s, int side)
        {
            var z = s.Next() * 2f;
            while (z < TileLength)
            {
                var x = side * s.Range(VergeTo + 0.2f, VergeTo + 3.5f);
                var pick = s.Next();
                var at = new Vector3(x, FloorY, z);
                if (pick < 0.30f) Clump(s.flora, Wild.Roses, at, s.Range(1.3f, 1.9f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.50f) Clump(s.flora, Wild.Honeysuckle, at, s.Range(1.2f, 1.7f), s.Next() * 180f, Vector3.zero);
                else if (pick < 0.80f) Clump(s.flora, Wild.Oak, at, s.Range(0.28f, 0.42f), s.Next() * 180f, Vector3.zero);
                else Clump(s.flora, Wild.Ivy, at, s.Range(1.6f, 2.4f), s.Next() * 180f, Vector3.zero);
                z += s.Range(1.4f, 2.6f);
            }
            for (var i = 0; i < 8; i++)
            {
                var at = new Vector3(side * s.Range(VergeTo + 3f, 24f), FloorY, s.Next() * TileLength);
                Clump(s.flora, s.Next() < 0.6f ? Wild.Oak : Wild.Yew, at, s.Range(0.3f, 0.5f), s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>
        /// 森の木。道の近くの列（樹冠を道の上へ張り出し、木漏れ日を路面へ落とす）と、奥の疎らな木
        /// </summary>
        static void Trees(Slice s, int side)
        {
            var z = s.Next() * 5f;
            while (z < TileLength)
            {
                var foot = new Vector3(side * s.Range(VergeTo + 0.6f, VergeTo + 4f), FloorY, z);
                Tree(s, foot, s.Range(6.5f, 9f), s.Range(1.5f, 2.0f), -side * s.Range(1.6f, 2.8f), Swatch.Bark);
                z += s.Range(6f, 9f);
            }
            for (var i = 0; i < 5; i++)
            {
                var foot = new Vector3(side * s.Range(VergeTo + 5f, 28f), FloorY, s.Next() * TileLength);
                Tree(s, foot, s.Range(5f, 8f), s.Range(1.3f, 1.9f), 0f, Swatch.Bark);
            }
        }

        /// <summary>木を一本。幹の足元・幹の丈・樹冠の倍率・樹冠を道の側へずらす量（x）</summary>
        static void Tree(Slice s, Vector3 foot, float trunk, float crown, float reach, Swatch bark)
        {
            s.paint.Prism(foot, s.Range(0.16f, 0.30f), s.Range(0.10f, 0.18f), trunk + 1.5f, 7, s.Next() * 60f, bark, null);
            var kind = s.Next() < 0.75f ? Wild.Oak : Wild.Yew;
            var high = Sizes[(int)kind].x * crown;
            var bottom = foot + new Vector3(reach * 0.5f, trunk * 0.7f, 0f);
            Clump(s.flora, kind, bottom, crown, s.Next() * 180f, Vector3.zero);
            if (Mathf.Abs(reach) > 0.01f)
                Clump(s.flora, Wild.Oak, bottom + new Vector3(reach, high * 0.25f, s.Range(-1f, 1f)), crown * 0.75f, s.Next() * 180f, Vector3.zero);
        }

        /// <summary>いちばん奥の暗い樹冠の壁。霞に溶けて、森の外が見えないように</summary>
        static void TreeWall(Slice s, int side)
        {
            for (var z = s.Next() * 3f; z < TileLength; z += s.Range(3f, 5f))
                Clump(s.flora, s.Next() < 0.5f ? Wild.Yew : Wild.Oak, new Vector3(side * s.Range(28f, 36f), FloorY - 0.5f, z),
                    s.Range(1.8f, 2.6f), s.Next() * 180f, Vector3.zero);
        }

        // ---- 海辺の崖（帯 2） ---------------------------------------------------------------

        /// <summary>海の面の高さ。崖の上の道から 11 m 下</summary>
        const float SeaY = -11f;

        /// <summary>崖の縁の、道の真ん中からの隔たり（環の中の z で決まる。環の一周で閉じる）</summary>
        static float CliffEdge(Slice s, float zRing)
        {
            var w = 2f * Mathf.PI * zRing / s.Span;
            return 6.0f + 1.0f * Mathf.Sin(w * 3f + 0.7f) + 0.5f * Mathf.Sin(w * 7f + 2.1f);
        }

        /// <summary>水の面の上の、ぎざぎざの輪の泡</summary>
        static void Foam(Slice s, Vector3 centre, float inner, float outer)
        {
            const int n = 12;
            for (var i = 0; i < n; i++)
            {
                var a0 = 2f * Mathf.PI * i / n;
                var a1 = 2f * Mathf.PI * (i + 1) / n;
                var o0 = outer * s.Range(0.8f, 1.15f);
                var o1 = outer * s.Range(0.8f, 1.15f);
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                // 上を向く巻き（上から見て右回り）
                s.paint.Quad(centre + d0 * inner, centre + d1 * inner, centre + d1 * o1, centre + d0 * o0, Swatch.Foam);
            }
        }

        // ---- 起伏のある地面（帯 4） -------------------------------------------------------

        /// <summary>
        /// 丘の高さ。道の近く（near まで）は平ら、そこから high m へなだらかに上がり、環に沿って波打つ。
        /// 波の周期は環の一周を割り切る（継ぎ目で段が出ない）
        /// </summary>
        static float Hill(Slice s, float d, float zRing, float near, float high, float wide, float roll)
        {
            if (d <= near) return VergeY;
            var t = d - near;
            var rise = high * (1f - Mathf.Exp(-(t / wide) * (t / wide)));
            var w = 2f * Mathf.PI * zRing / s.Span;
            var wave = roll * Mathf.Clamp01(t / 12f) * (Mathf.Sin(w * 2f + d * 0.045f) + 0.5f * Mathf.Sin(w * 5f - d * 0.08f + 1.3f));
            return VergeY + rise + wave;
        }

        /// <summary>麦の株の背。場面 8 の麦と同じくらい</summary>
        const float WheatHigh = 0.95f;

        /// <summary>羊。胴は白い毛、face は顔と脚の色</summary>
        static void Sheep(Slice s, Vector3 foot, float yaw, Swatch face)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            s.paint.Box(foot + new Vector3(0f, 0.62f, 0f), new Vector3(0.62f, 0.55f, 1.05f), yaw, Swatch.Wool, Swatch.Wool);
            s.paint.Box(foot + r * new Vector3(0f, 0.72f, 0.62f), new Vector3(0.26f, 0.30f, 0.34f), yaw, face, face);
            for (var i = 0; i < 4; i++)
            {
                var x = (i % 2 == 0 ? -0.2f : 0.2f);
                var z = (i < 2 ? -0.35f : 0.35f);
                s.paint.Box(foot + r * new Vector3(x, 0.18f, z), new Vector3(0.09f, 0.36f, 0.09f), yaw, face, face);
            }
        }

        // ---- ブナの並木（帯 6） -------------------------------------------------------------

        /// <summary>
        /// ブナの並木のトンネル。舗装の道の両脇に、灰色の滑らかな幹を 7 m おきに並べ、樹冠を道の上で重ねる。
        /// 夕暮れの低い橙の日が前の左から幹のあいだを抜け、道と車に縞の影を落とす
        /// </summary>
        static void Beech(Slice s)
        {
            PavedRoad(s, 2.3f);
            for (var side = -1; side <= 1; side += 2)
            {
                Strip(s, side, 2.25f, 3.0f, VergeY, Swatch.Grass);
                Strip(s, side, 2.95f, 6f, FloorY, Swatch.Litter);
                Strip(s, side, 5.95f, 60f, FloorY, Swatch.Grass);
                for (var z = side > 0 ? 1.5f : 5f; z < TileLength; z += 7f)
                {
                    var foot = new Vector3(side * s.Range(3.4f, 3.9f), FloorY, z + s.Range(-0.4f, 0.4f));
                    s.paint.Prism(foot, s.Range(0.30f, 0.40f), 0.22f, 12f, 8, s.Next() * 45f, Swatch.Bark, null);
                    // 樹冠は道の上へ寄せて重ねる。二段にして天井を厚く
                    Clump(s.flora, Wild.Oak, foot + new Vector3(-side * 1.2f, 6.2f, 0f), s.Range(1.9f, 2.3f), s.Next() * 180f, Vector3.zero);
                    Clump(s.flora, Wild.Oak, foot + new Vector3(-side * 2.6f, 8.5f, s.Range(-2f, 2f)), s.Range(1.4f, 1.8f), s.Next() * 180f, Vector3.zero);
                }
                // 並木の外の畑の縁の生け垣
                for (var z = s.Next() * 3f; z < TileLength; z += s.Range(2.5f, 4f))
                    Clump(s.flora, Wild.Yew, new Vector3(side * s.Range(7f, 9f), FloorY, z), s.Range(0.35f, 0.5f), s.Next() * 180f, Vector3.zero);
                TreeWall(s, side);
            }
        }

        // ---- 夜の湖（帯 7） ------------------------------------------------------------------

        /// <summary>湖の面の高さ</summary>
        const float LakeY = -0.6f;
        /// <summary>湖の岸（道の真ん中から）</summary>
        const float Shore = 5.2f;

        /// <summary>
        /// 湖沿いの道。運転席の側（右）が湖、助手席の側（左）は暗い森。舗装の道を前照灯が照らし、湖に月の光の道が伸びる（月と光の道は遠景）
        /// </summary>
        static void Lake(Slice s)
        {
            PavedRoad(s, 2.2f);
            ForestSide(s, -1, 0.35f);
            Strip(s, 1, 2.15f, 4.2f, VergeY, Swatch.Grass);
            s.paint.Quad(new Vector3(4.15f, VergeY, TileLength), new Vector3(Shore + 0.4f, LakeY - 0.1f, TileLength),
                new Vector3(Shore + 0.4f, LakeY - 0.1f, 0f), new Vector3(4.15f, VergeY, 0f), Swatch.Sand);
            s.water.Patch(new Vector3(Shore, LakeY, TileLength), new Vector3(900f, LakeY, TileLength), new Vector3(900f, LakeY, 0f), new Vector3(Shore, LakeY, 0f),
                new Vector2(Shore / 8f, 5f), new Vector2(900f / 8f, 5f), new Vector2(900f / 8f, 0f), new Vector2(Shore / 8f, 0f));
            // 岸の葦と、水際の石
            for (var z = s.Next(); z < TileLength; z += s.Range(0.6f, 1.4f))
                Clump(s.flora, s.Next() < 0.7f ? Wild.Filler : Wild.Ivy, new Vector3(s.Range(Shore - 0.4f, Shore + 1.2f), LakeY, z), s.Range(1.4f, 2.2f), s.Next() * 180f, Vector3.zero);
            for (var i = 0; i < 3; i++)
            {
                var r = s.Range(0.3f, 0.9f);
                s.paint.Prism(new Vector3(s.Range(Shore, Shore + 3f), LakeY - 0.3f, s.Next() * TileLength), r, r * 0.6f, r * 0.9f, 6, s.Next() * 60f,
                    Swatch.RockDark, Swatch.RockDark, s.rnd, 0.4f);
            }
        }

        // ---- 遠景（動かない） ------------------------------------------------------------

        /// <summary>
        /// 帯ごとの遠景。環に乗せず、場面に据えたまま置く（遠い物ほど車が進んでも向きが変わらないので、動かないのが正しい）。
        /// 霞に溶ける所まで離すので、色は暗めでよい
        /// </summary>
        static void Backdrop(Scene scene, Transform parent, Stats stats)
        {
            var p = new Paint();
            var glow = new Paint();
            var trees = new Bank { Texel = 1f, Rooted = true, CardLift = 1.4f };
            var rnd = new System.Random(77 + (int)scene * 13);
            switch (scene)
            {
                case Scene.Coast:
                {
                    // 入り江の向こうの岬。海から立つ崖の面と、上の芝と、根元の白い波。岬のいちばん高い所に錫の鉱山の機関場
                    Ridge(p, rnd, 380f, 22f, 78f, 18f, 42f, Swatch.Rock, SeaY - 1f);
                    var headDeg = 30f;
                    for (var d = 30f; d <= 60f; d += 1f)
                        if (RidgeTop(d, 22f, 78f, 18f, 42f) > RidgeTop(headDeg, 22f, 78f, 18f, 42f)) headDeg = d;
                    var head = RidgeTop(headDeg, 22f, 78f, 18f, 42f);
                    // 芝は厚めに（薄いと霞に消えて、岬が灰色の板に見えた）
                    Ridge(p, rnd, 379f, 22f, 78f, 3f, 7f, Swatch.Turf, 0f, true);
                    Surf(p, 380f, 22f, 78f);
                    EngineHouse(p, headDeg, 392f, head + 4f);
                    break;
                }
                case Scene.Moor:
                case Scene.Wheat:
                case Scene.Pasture:
                    FarmBackdrop(scene, p, trees, rnd);
                    break;
                case Scene.Lake:
                    // 向こう岸の丘の影と、ぽつぽつと灯る窓。月と、湖に伸びる月の光の道。星
                    Ridge(p, rnd, 330f, 5f, 160f, 12f, 45f, Swatch.HillNight);
                    for (var i = 0; i < 9; i++)
                    {
                        var a = Mathf.Lerp(20f, 140f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                        var at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 318f + Vector3.up * Mathf.Lerp(1f, 6f, (float)rnd.NextDouble());
                        Face(glow, at, 0.9f, 0.7f, Swatch.Lamp);
                    }
                    Moon(glow, rnd);
                    Stars(glow, rnd);
                    break;
            }
            stats.far = p.Count + glow.Count + trees.Count;
            var dir = BuildEnding.Generated + "Far" + scene + "_";
            var m = Materials();
            var land = p.Bake(dir + "Land.asset");
            if (land != null) Put(parent, "Land", land, m.swatch);
            var lit = glow.Bake(dir + "Glow.asset");
            if (lit != null) Put(parent, "Glow", lit, m.glow);
            var grove = trees.Emit(parent, "Trees", m.farm, false, dir);
            if (grove != null) grove.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (scene == Scene.Coast) stats.far += Gulls(parent) * 2;
            if (scene == Scene.Moor || scene == Scene.Wheat || scene == Scene.Pasture)
            {
                Clouds(parent);
                stats.far += 4;
            }
            // 遠景の子はどれも一つのレンダラー
            stats.farCalls = parent.childCount;
        }

        /// <summary>直前に立てた稜線（<see cref="Ridge"/>）の、方位 deg での高さ</summary>
        static float RidgeTop(float deg, float from, float to, float low, float high)
        {
            var t = Mathf.InverseLerp(from, to, deg);
            return Mathf.Lerp(low, high, 0.5f + 0.35f * Mathf.Sin(t * 9f + lastPhase) + 0.15f * Mathf.Sin(t * 23f + lastPhase * 2f));
        }

        static void Put(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 遠い丘の稜線。原点の周りの円弧（度。+z が 0 で右回り）に、半径 radius、高さ low〜high で波打つ帯を立てる。
        /// 足元は footY。cap なら、同じ波で上へ少しだけ立てる（崖の上の芝の縁）
        /// </summary>
        static void Ridge(Paint p, System.Random rnd, float radius, float from, float to, float low, float high, Swatch col, float footY = -20f, bool cap = false)
        {
            const int n = 48;
            var prev = Vector3.zero;
            var prevTop = Vector3.zero;
            var phase = cap ? lastPhase : (float)rnd.NextDouble() * 6f;
            lastPhase = phase;
            for (var i = 0; i <= n; i++)
            {
                var deg = Mathf.Lerp(from, to, i / (float)n);
                var a = deg * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var t = i / (float)n;
                var hgt = Mathf.Lerp(cap ? lastLow : low, cap ? lastHigh : high, 0.5f + 0.35f * Mathf.Sin(t * 9f + phase) + 0.15f * Mathf.Sin(t * 23f + phase * 2f));
                var foot = d * radius + Vector3.up * (cap ? hgt - 0.2f : footY);
                var top = d * radius + Vector3.up * (cap ? hgt + Mathf.Lerp(low, high, 0.5f) : hgt);
                if (i > 0) p.Quad(prevTop, top, foot, prev, col);
                prev = foot;
                prevTop = top;
            }
            if (!cap)
            {
                lastLow = low;
                lastHigh = high;
            }
        }

        /// <summary>直前の稜線の波（cap の芝を同じ稜線に載せるため）</summary>
        static float lastPhase, lastLow, lastHigh;

        /// <summary>遠い崖の根元の白い波。海の面の上に、円弧に沿った帯</summary>
        static void Surf(Paint p, float radius, float from, float to)
        {
            const int n = 48;
            for (var i = 0; i < n; i++)
            {
                var a0 = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                var a1 = Mathf.Lerp(from, to, (i + 1) / (float)n) * Mathf.Deg2Rad;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                var y = Vector3.up * (SeaY + 0.3f);
                var near = radius - 6f - 3f * Mathf.Abs(Mathf.Sin(i * 1.7f));
                // 上を向く巻き（上から見て右回り）。角が増すほど右へ回るので、外 a0 → 外 a1 → 内 a1 → 内 a0
                p.Quad(d0 * radius + y, d1 * radius + y, d1 * near + y, d0 * near + y, Swatch.Foam);
            }
        }

        /// <summary>月。前の右の低い所に、平らな円（原点を向く）</summary>
        static void Moon(Paint glow, System.Random rnd)
        {
            // 風防の右寄りに見える所（前の右 22 度、11 度の高さ）。38 度では右の柱の陰に入った。光の道は湖の上（岸は道から 5.2 m）
            const float azimuth = 22f, elevation = 11f, dist = 200f, radius = 3.4f;
            var dir = Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward;
            var centre = dir * dist;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            var up = Vector3.Cross(dir, right).normalized;
            const int n = 20;
            for (var i = 0; i < n; i++)
            {
                var a0 = 2f * Mathf.PI * i / n;
                var a1 = 2f * Mathf.PI * (i + 1) / n;
                glow.Tri(centre, centre + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius, centre + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * radius, Swatch.Moon);
            }
            // 湖に伸びる光の道。月の方位の線に沿って、切れ切れの光の板を並べる（遠いほど幅が広い）。
            // 低い目から見ると水の面は寝ているので、板を奥行きに長く取らないと画素に届かない（奥行き 0.1 m では見えなかった）
            var flat = new Vector3(dir.x, 0f, dir.z).normalized;
            var side = Vector3.Cross(Vector3.up, flat).normalized;
            for (var d = 16f; d < 190f; d *= 1.12f)
            {
                var n2 = 1 + (int)(d / 70f);
                for (var k = 0; k < n2; k++)
                {
                    var at = flat * (d + (float)rnd.NextDouble() * d * 0.05f) + side * ((float)rnd.NextDouble() - 0.5f) * d * 0.04f;
                    at.y = LakeY + 0.04f;
                    var half = (0.4f + d * 0.025f) * (0.6f + (float)rnd.NextDouble() * 0.8f);
                    var deep = d * 0.035f;
                    // 上を向く巻き
                    glow.Quad(at - side * half + flat * deep, at + side * half + flat * deep, at + side * half - flat * deep, at - side * half - flat * deep, Swatch.Moon);
                }
            }
        }

        /// <summary>星。原点を向く小さな四角を、空の上の半分に散らす</summary>
        static void Stars(Paint glow, System.Random rnd)
        {
            for (var i = 0; i < 160; i++)
            {
                var az = (float)rnd.NextDouble() * 360f;
                var el = Mathf.Lerp(10f, 75f, Mathf.Sqrt((float)rnd.NextDouble()));
                var dir = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
                var size = Mathf.Lerp(0.8f, 1.6f, (float)rnd.NextDouble());
                Face(glow, dir * 230f, size, size, Swatch.Star);
            }
        }

        /// <summary>原点を向く四角</summary>
        static void Face(Paint p, Vector3 at, float wide, float high, Swatch col)
        {
            var dir = (-at).normalized;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            var up = Vector3.Cross(dir, right).normalized;
            var r = right * wide * 0.5f;
            var u = up * high * 0.5f;
            // 原点から見て右回り（right は原点から見ると左を指す）
            p.Quad(at + r + u, at - r + u, at - r - u, at + r - u, col);
        }
    }
}
