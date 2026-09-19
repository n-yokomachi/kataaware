using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の路地裏を組む。グレビル・ストリートの入口から北へ歩き、
    /// 途中の小路を西へ折れ、ブリーディング・ハート・ヤードへ入る一本道。
    /// 迷う余地は作らない。
    ///
    /// 面は素材ごとに 1 枚の mesh へ焼く（<see cref="Bank"/>）。箱を並べる作りだと
    /// 絵が寸法に合わせて伸びてしまい、壁が書き割りに見える。
    /// 当たり判定は見た目と分け、見えない箱で通りの輪郭だけ囲う
    /// </summary>
    public static class BuildAlley
    {
        public const string Materials = "Assets/Materials/Alley/";
        public const string Generated = "Assets/Models/generated/alley/";

        // ---- 一本道の寸法。メートル ----------------------------------------

        /// <summary>車道の半幅</summary>
        public const float RoadHalf = 3.2f;
        /// <summary>歩道を含めた通りの半幅。建物の面はここに来る</summary>
        public const float StreetHalf = 4.9f;
        public const float StreetSouth = -24f;
        /// <summary>ここより手前へは行かせない。見える街は先まで続く</summary>
        public const float WalkSouth = -4.5f;
        public const float StreetNorth = 56f;
        /// <summary>ここより奥へは行かせない。見える街は先まで続く</summary>
        public const float WalkNorth = 42f;

        public const float LaneZ = 35f;
        public const float LaneHalf = 1.6f;
        public const float LaneWest = -16f;

        public const float YardWest = -32f;
        public const float YardSouth = 26f;
        public const float YardNorth = 44f;

        public const float WallHeight = 16f;
        public const float WallThick = 1.4f;

        /// <summary>歩道の高さ</summary>
        public const float KerbRise = 0.16f;

        /// <summary>1 階の高さ。2 階から上はこれ</summary>
        public const float GroundFloor = 3.8f;
        public const float UpperFloor = 2.9f;

        // ---- 組み立て ------------------------------------------------------

        [MenuItem("HalfAware/Build the alley")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/alley"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "alley");

            var root = Root("Alley");
            // 前の作りで残っている束を落とす
            Prune(root, new[] { "Shell", "Fixtures", "Lamps", "Puddles", "Neon", "Market", "Boards", "Litter", "Crowd", "Backdrop", "Sky", "Bounds", "Roofs", "Items" });
            Shell(Child(root, "Shell"));
            Fixtures(Child(root, "Fixtures"));
            Lamps(Child(root, "Lamps"));
            Puddles(Child(root, "Puddles"));
            Neon(Child(root, "Neon"));
            Market(Child(root, "Market"));
            Boards(Child(root, "Boards"));
            Litter(Child(root, "Litter"));
            Crowd(Child(root, "Crowd"));
            Backdrop(Child(root, "Backdrop"));
            Sky(Child(root, "Sky"));
            Bounds(Child(root, "Bounds"));
            Roofs(Child(root, "Roofs"));
            NightSky();
            Mirrors(Child(root, "Mirrors"));
            BakeMirrors(root);
            Rain();
            // 調べる対象は、看板と露店が立ってから
            BuildAlleyItems.Build(root);
            var temp = GameObject.Find("TempGround");
            if (temp != null) Object.DestroyImmediate(temp);
            Place(root);
            Selection.activeGameObject = root.gameObject;
            Mark(root.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log("路地裏を組み直した");
        }

        // ---- 皮 ------------------------------------------------------------

        /// <summary>
        /// 通り・小路・ヤードの面をまとめて張る。素材ごとに溜めてから焼くので、
        /// 出来上がるのは素材の数だけの mesh になる
        /// </summary>
        static void Shell(Transform parent)
        {
            Clear(parent);
            var road = new Bank { Texel = 0.22f };
            var paving = new Bank { Texel = 0.42f };
            var brick = new Bank { Texel = 0.55f };
            var stone = new Bank { Texel = 0.40f };
            var metal = new Bank { Texel = 0.60f };
            var glass = new Bank { Texel = 0.50f };
            var timber = new Bank { Texel = 0.55f };
            // 窓の灯りは色ごとに分かれる。1 色だと通りが黄色く染まる
            var glow = new Glazing(glass);

            Ground(road, paving, stone);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                Facades(brick, stone, metal, glass, glow, side);
            }
            LaneShell(brick, stone, paving);
            YardShell(brick, stone, paving);
            YardFacades(brick, stone, metal, glass, glow);
            SouthEnd(brick, stone, metal);
            Heritage(Child(parent.parent, "Heritage"), brick, stone, metal, timber, glass, glow);
            Terraces(Child(parent.parent, "Terraces"), stone, metal, timber);
            Walkway(metal, stone, -13.5f, 6.4f);
            Walkway(metal, stone, 26.5f, 7.1f);
            Walkway(metal, stone, 47.5f, 6.8f);
            NorthEnd(brick, stone, metal);

            road.Emit(parent, "Road", Mat("Asphalt"), false, Generated);
            paving.Emit(parent, "Paving", Mat("Cobble"), false, Generated);
            brick.Emit(parent, "Brick", Mat("Brick"), false, Generated);
            stone.Emit(parent, "Stone", Mat("Concrete"), false, Generated);
            metal.Emit(parent, "MetalShell", Mat("Metal"), false, Generated);
            glass.Emit(parent, "Glass", Mat("Glass"), false, Generated);
            timber.Emit(parent, "Timberwork", Mat("Timber"), false, Generated);
            glow.Emit(parent);
        }

        /// <summary>
        /// 窓の灯り。色も明るさも一種類だと通りが一色に染まって見えるので、束で持って引く。
        /// 引いた先はそれぞれ別のマテリアルになるので、色の違いがそのまま画面に出る
        /// </summary>
        sealed class Glazing
        {
            /// <summary>灯りの色。タングステンに寄せつつ、蛍光灯・画面・色つきの灯りを混ぜる</summary>
            static readonly Color[] Hues =
            {
                new Color(1.00f, 0.78f, 0.50f),   // タングステン
                new Color(1.00f, 0.86f, 0.66f),   // 窓掛けごしの暖色
                new Color(0.97f, 0.94f, 0.86f),   // 白めの電球
                new Color(0.86f, 0.94f, 0.90f),   // 蛍光灯
                new Color(0.68f, 0.82f, 1.00f),   // 画面の青
                new Color(0.36f, 0.54f, 0.94f),   // 奥まった画面の青
                new Color(1.00f, 0.50f, 0.40f),   // 赤い灯り
                new Color(0.44f, 0.94f, 0.88f),   // 青緑
                new Color(0.78f, 0.58f, 1.00f),   // 藤
                new Color(1.00f, 0.62f, 0.24f),   // 深い橙
                new Color(1.00f, 0.80f, 0.56f),   // 店内の暖色。面が広いので暗く
                new Color(0.92f, 0.95f, 0.92f),   // 店内の白
                new Color(0.98f, 0.74f, 0.44f),   // 店の奥。いちばん暗い
            };

            /// <summary>色ごとの明るさ。ぜんぶ明るいと窓の並びが白く潰れて一色に見える</summary>
            static readonly float[] Glow =
            { 0.82f, 0.36f, 0.60f, 0.46f, 0.62f, 0.24f, 0.38f, 0.44f, 0.32f, 0.20f,
              0.46f, 0.40f, 0.29f };

            /// <summary>引く目。暖色をやや多めに、ほかも必ず混じるように並べる</summary>
            static readonly int[] Wheel =
            { 0, 4, 1, 2, 6, 0, 3, 8, 1, 5, 2, 7, 0, 9, 4, 1, 3, 6, 2, 5 };

            /// <summary>店先に使う色。暗い灯りや画面の青は引かない</summary>
            static readonly int[] Shopfronts = { 10, 11, 12, 10, 12 };

            readonly Bank[] lit = new Bank[Hues.Length];
            readonly Bank dark;

            public Glazing(Bank dark)
            {
                this.dark = dark;
                for (var i = 0; i < lit.Length; i++) lit[i] = new Bank { Texel = 0.50f };
            }

            /// <summary>窓を 1 枚引く。unlit は灯りの点いていない窓の割合</summary>
            public Bank Pick(System.Random rng, double unlit)
            {
                if (rng.NextDouble() < unlit) return dark;
                return lit[Wheel[rng.Next(Wheel.Length)]];
            }

            /// <summary>必ず灯りの点いた窓。店先のように暗くできないところで使う</summary>
            public Bank Shop(System.Random rng)
            {
                return lit[Shopfronts[rng.Next(Shopfronts.Length)]];
            }

            /// <summary>色を名指しで引く。同じ店の窓を揃えたいときに使う</summary>
            public Bank Lit(int i)
            {
                return lit[((i % lit.Length) + lit.Length) % lit.Length];
            }

            /// <summary>色ごとに 1 枚の mesh へ焼く</summary>
            public void Emit(Transform parent)
            {
                for (var i = 0; i < lit.Length; i++)
                    lit[i].Emit(parent, "Window" + i, GlowMat(Hues[i], Glow[i]), false, Generated);
            }
        }

        /// <summary>路面と歩道。車道は縁石ぶん低くして、排水口を等間隔に落とす</summary>
        static void Ground(Bank road, Bank paving, Bank stone)
        {
            // 抜けの先まで敷く。突き当たりの下に穴が空いて見えていた
            road.FaceY(0f, -RoadHalf, RoadHalf, BackdropZ - 1f, StreetNorth + 14f, 1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                var inner = side * RoadHalf;
                var outer = side * StreetHalf;
                var x0 = Mathf.Min(inner, outer);
                var x1 = Mathf.Max(inner, outer);
                paving.FaceY(KerbRise, x0, x1, BackdropZ - 1f, StreetNorth + 14f, 1);
                stone.FaceX(inner, BackdropZ - 1f, StreetNorth + 14f, 0f, KerbRise, -side);
                for (var z = StreetSouth + 5f; z < StreetNorth - 3f; z += 9f)
                    stone.Box(new Vector3(inner - side * 0.26f, 0.015f, z), new Vector3(0.44f, 0.03f, 0.7f));
            }
            // 車道の書き込み。真ん中の破線と、蓋と、点検口
            var rng = new System.Random(2400);
            for (var z = StreetSouth + 2f; z < StreetNorth - 2f; z += 3.2f)
                stone.Box(new Vector3(0f, 0.012f, z), new Vector3(0.16f, 0.024f, 1.5f));
            for (var z = StreetSouth + 7f; z < StreetNorth - 5f; z += (float)(10.0 + rng.NextDouble() * 6.0))
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0) * (RoadHalf - 1.0f);
                stone.Box(new Vector3(x, 0.014f, z), new Vector3(0.78f, 0.028f, 0.78f));
                stone.Box(new Vector3(x, 0.026f, z), new Vector3(0.58f, 0.026f, 0.58f));
            }
        }

        /// <summary>建物の割り</summary>
        struct Unit
        {
            public float z0;
            public float z1;
            public float height;
            public int ground;
            public int floors;
        }

        static List<Unit> Units(float from, float to, System.Random rng)
        {
            var list = new List<Unit>();
            var z = from;
            while (z < to - 0.5f)
            {
                var span = Mathf.Min((float)(6.0 + rng.NextDouble() * 4.0), to - z);
                if (to - (z + span) < 3.5f) span = to - z;
                var u = new Unit();
                u.z0 = z;
                u.z1 = z + span;
                u.floors = 3 + rng.Next(3);
                u.height = GroundFloor + UpperFloor * u.floors;
                u.ground = rng.Next(3);
                list.Add(u);
                z += span;
            }
            return list;
        }

        /// <summary>
        /// 片側の建物の面。1 階は店先かシャッターか戸口、2 階から上は窓を並べる。
        /// 面を平らのままにせず、付柱・胴蛇腹・窓台・軒で段を作る
        /// </summary>
        static void Facades(Bank brick, Bank stone, Bank metal, Bank glass, Glazing glow, int side)
        {
            var wx = side * StreetHalf;
            var inward = -side;
            var rng = new System.Random(side > 0 ? 8801 : 8802);
            var runs = new List<Vector2>();
            if (side < 0)
            {
                runs.Add(new Vector2(StreetSouth, LaneZ - LaneHalf));
                runs.Add(new Vector2(LaneZ + LaneHalf, StreetNorth));
            }
            else
            {
                runs.Add(new Vector2(StreetSouth, StreetNorth));
            }
            foreach (var run in runs)
                foreach (var u in Units(run.x, run.y, rng))
                    Facade(brick, stone, metal, glass, glow, side, wx, inward, u, rng);
        }

        static void Facade(Bank brick, Bank stone, Bank metal, Bank glass, Glazing glow,
            int side, float wx, int inward, Unit u, System.Random rng)
        {
            var pil = 0.34f;
            var z0 = u.z0 + pil;
            var z1 = u.z1 - pil;
            var holes = new List<Vector4>();

            var shopY0 = 0.42f;
            var shopY1 = GroundFloor - 0.75f;
            if (u.ground == 0)
            {
                holes.Add(new Vector4(z0 + 0.35f, z1 - 1.55f, shopY0, shopY1));
                holes.Add(new Vector4(z1 - 1.25f, z1 - 0.35f, 0f, shopY1 - 0.15f));
            }
            else if (u.ground == 2)
            {
                var c = (z0 + z1) * 0.5f;
                holes.Add(new Vector4(c - 0.55f, c + 0.55f, 0f, 2.25f));
            }

            var openings = new List<Vector4>();
            for (var f = 0; f < u.floors; f++)
            {
                var fy = GroundFloor + UpperFloor * f;
                var sill = fy + 0.80f;
                var head = sill + 1.55f;
                var span = z1 - z0;
                var count = Mathf.Max(1, Mathf.FloorToInt(span / 2.05f));
                var pitch = span / count;
                for (var i = 0; i < count; i++)
                {
                    var c = z0 + pitch * (i + 0.5f);
                    var w = Mathf.Min(1.10f, pitch * 0.55f);
                    openings.Add(new Vector4(c - w * 0.5f, c + w * 0.5f, sill, head));
                }
            }
            holes.AddRange(openings);

            brick.FaceXHoles(wx, u.z0, u.z1, 0f, u.height, inward, holes);

            var depth = 0.13f;
            var back = wx + side * depth;   // 掘り込みは建物の側へ
            foreach (var o in openings)
            {
                Reveal(stone, wx, back, o);
                glow.Pick(rng, 0.40).FaceX(back, o.x, o.y, o.z, o.w, inward);
                stone.Box(new Vector3(wx + inward * 0.06f, o.z - 0.07f, (o.x + o.y) * 0.5f),
                    new Vector3(0.13f, 0.09f, o.y - o.x + 0.24f));
                stone.Box(new Vector3(wx + inward * 0.04f, o.w + 0.09f, (o.x + o.y) * 0.5f),
                    new Vector3(0.10f, 0.12f, o.y - o.x + 0.30f));
            }

            if (u.ground == 0)
            {
                Shopfront(stone, metal, glow.Shop(rng), glass, wx, back, z0, z1, shopY0, shopY1, inward, rng);
                // 店の戸口。ここを塞がないと壁に穴が空いたままになる
                Doorway(stone, metal, wx, back, new Vector4(z1 - 1.25f, z1 - 0.35f, 0f, shopY1 - 0.15f), inward);
            }
            else if (u.ground == 1) Shutter(metal, wx, z0, z1, inward);
            else Doorway(stone, metal, wx, back, holes[0], inward);

            stone.Box(new Vector3(wx + inward * 0.11f, u.height * 0.5f, u.z0 + pil * 0.5f),
                new Vector3(0.22f, u.height, pil));
            stone.Box(new Vector3(wx + inward * 0.11f, u.height * 0.5f, u.z1 - pil * 0.5f),
                new Vector3(0.22f, u.height, pil));
            for (var f = 0; f <= u.floors; f++)
            {
                var y = f == 0 ? GroundFloor - 0.30f : GroundFloor + UpperFloor * f - 0.24f;
                if (y > u.height - 0.4f) break;
                stone.Box(new Vector3(wx + inward * 0.13f, y, (u.z0 + u.z1) * 0.5f),
                    new Vector3(0.26f, f == 0 ? 0.30f : 0.16f, u.z1 - u.z0));
            }
            stone.Box(new Vector3(wx + inward * 0.22f, u.height + 0.18f, (u.z0 + u.z1) * 0.5f),
                new Vector3(0.44f, 0.36f, u.z1 - u.z0 + 0.16f));
            brick.Box(new Vector3(wx + inward * 0.04f, u.height + 0.70f, (u.z0 + u.z1) * 0.5f),
                new Vector3(0.28f, 0.68f, u.z1 - u.z0));
        }

        /// <summary>
        /// 硝子の内側。灯りの面だけでは光る板にしか見えないので、
        /// 手前へ暗い影を並べて奥行きを作る。卓・棚・吊り灯り・客の影。
        /// axis は 0 なら面が x 一定（z 方向に広がる）、2 なら z 一定。
        /// towards は見る人が居る側。影はその手前へ出す
        /// </summary>
        static void Behind(Bank dark, int axis, float plane, int towards,
            float a0, float a1, float y0, float y1, System.Random rng)
        {
            var span = a1 - a0;
            if (span < 0.8f) return;

            // 面から手前へ出す距離。奥行きの違いで前後が読める
            var near = plane + towards * 0.05f;
            var mid = plane + towards * 0.13f;
            var far = plane + towards * 0.20f;

            // 客の影。座った高さと立った高さを混ぜる
            var n = Mathf.Max(1, Mathf.FloorToInt(span / 1.15f));
            for (var i = 0; i < n; i++)
            {
                var a = a0 + span * (i + 0.5f) / n + (float)(rng.NextDouble() - 0.5) * 0.3f;
                if (rng.NextDouble() < 0.28) continue;
                var stand = rng.NextDouble() < 0.34;
                var high = stand ? 1.72f : 1.28f;
                var foot = stand ? y0 : y0 + 0.42f;
                var d = rng.NextDouble() < 0.5 ? mid : far;
                Slab(dark, axis, d, a, (foot + high) * 0.5f, 0.40f, high - foot);
                Slab(dark, axis, d, a, high - 0.11f, 0.24f, 0.24f);
            }

            // 手前の卓と、奥の棚
            for (var a = a0 + 0.5f; a < a1 - 0.3f; a += (float)(1.5 + rng.NextDouble() * 1.1))
            {
                Slab(dark, axis, near, a, y0 + 0.40f, 1.05f, 0.09f);
                Slab(dark, axis, near, a, y0 + 0.20f, 0.12f, 0.40f);
            }
            var shelf = y0 + (y1 - y0) * 0.62f;
            Slab(dark, axis, far, (a0 + a1) * 0.5f, shelf, span * 0.78f, 0.07f);
            for (var a = a0 + 0.35f; a < a1 - 0.2f; a += 0.24f)
            {
                if (rng.NextDouble() < 0.35) continue;
                Slab(dark, axis, far, a, shelf + 0.16f, 0.09f, 0.26f);
            }

            // 吊り灯り。天井から 3 割ほど下がる
            for (var a = a0 + span * 0.25f; a < a1; a += span * 0.34f)
            {
                Slab(dark, axis, mid, a, y1 - 0.22f, 0.05f, 0.44f);
                Slab(dark, axis, mid, a, y1 - 0.48f, 0.30f, 0.16f);
            }
        }

        /// <summary>影ひとつ。面の向きに合わせて薄い板を立てる</summary>
        static void Slab(Bank dark, int axis, float depth, float along, float y, float wide, float high)
        {
            var size = axis == 0 ? new Vector3(0.03f, high, wide) : new Vector3(wide, high, 0.03f);
            var at = axis == 0 ? new Vector3(depth, y, along) : new Vector3(along, y, depth);
            dark.Box(at, size);
        }

        /// <summary>窓の抜けの内側。四方の返しを張って、壁に厚みを持たせる</summary>
        static void Reveal(Bank bank, float wx, float back, Vector4 o)
        {
            var x0 = Mathf.Min(wx, back);
            var x1 = Mathf.Max(wx, back);
            bank.FaceZ(o.x, x0, x1, o.z, o.w, 1);
            bank.FaceZ(o.y, x0, x1, o.z, o.w, -1);
            bank.FaceY(o.w, x0, x1, o.x, o.y, -1);
            bank.FaceY(o.z, x0, x1, o.x, o.y, 1);
        }

        /// <summary>店先。大きな硝子の奥に灯りを置き、上に庇を掛ける</summary>
        static void Shopfront(Bank stone, Bank metal, Bank warm, Bank dark, float wx, float back,
            float z0, float z1, float y0, float y1, int inward, System.Random rng)
        {
            var gz0 = z0 + 0.35f;
            var gz1 = z1 - 1.55f;
            Reveal(stone, wx, back, new Vector4(gz0, gz1, y0, y1));
            warm.FaceX(back, gz0, gz1, y0, y1, inward);
            Behind(dark, 0, back, inward, gz0 + 0.2f, gz1 - 0.2f, y0, y1, rng);
            for (var z = gz0 + 1.2f; z < gz1 - 0.2f; z += 1.2f)
                metal.Box(new Vector3(wx + inward * 0.12f, (y0 + y1) * 0.5f, z), new Vector3(0.12f, y1 - y0, 0.07f));
            metal.Box(new Vector3(wx + inward * 0.12f, y0, (gz0 + gz1) * 0.5f), new Vector3(0.16f, 0.12f, gz1 - gz0));
            var lip = 0.85f;
            metal.Box(new Vector3(wx + inward * (lip * 0.5f), y1 + 0.42f, (z0 + z1) * 0.5f),
                new Vector3(lip, 0.10f, z1 - z0 - 0.2f));
            metal.Box(new Vector3(wx + inward * lip, y1 + 0.20f, (z0 + z1) * 0.5f),
                new Vector3(0.08f, 0.44f, z1 - z0 - 0.2f));
            stone.Box(new Vector3(wx + inward * 0.30f, 0.09f, z1 - 0.8f), new Vector3(0.6f, 0.18f, 1.5f));
        }

        /// <summary>下ろしたシャッター。横の筋を刻んで波板にする</summary>
        static void Shutter(Bank metal, float wx, float z0, float z1, int inward)
        {
            var y1 = GroundFloor - 0.75f;
            metal.FaceX(wx + inward * 0.10f, z0 + 0.2f, z1 - 0.2f, 0.06f, y1, inward);
            for (var y = 0.20f; y < y1; y += 0.22f)
                metal.Box(new Vector3(wx + inward * 0.14f, y, (z0 + z1) * 0.5f), new Vector3(0.09f, 0.06f, z1 - z0 - 0.4f));
            metal.Box(new Vector3(wx + inward * 0.16f, y1 + 0.12f, (z0 + z1) * 0.5f), new Vector3(0.22f, 0.24f, z1 - z0 - 0.3f));
        }

        /// <summary>戸口。奥まった扉と把手</summary>
        static void Doorway(Bank stone, Bank metal, float wx, float back, Vector4 o, int inward)
        {
            Reveal(stone, wx, back, o);
            metal.FaceX(back, o.x, o.y, o.z, o.w, inward);
            metal.Box(new Vector3(back + inward * 0.06f, 1.05f, o.y - 0.14f), new Vector3(0.09f, 0.28f, 0.05f));
            stone.Box(new Vector3(wx + inward * 0.08f, o.w + 0.14f, (o.x + o.y) * 0.5f),
                new Vector3(0.18f, 0.18f, o.y - o.x + 0.4f));
        }

        /// <summary>小路。両側は煉瓦で、上に渡した梁と迫り出した 2 階で空を切る</summary>
        static void LaneShell(Bank brick, Bank stone, Bank paving)
        {
            var west = LaneWest;
            var east = -StreetHalf;
            // 西の端はちょうど LaneWest。ヤードの床と重ねない
            paving.FaceY(0.02f, west, east + 0.5f, LaneZ - LaneHalf - 0.3f, LaneZ + LaneHalf + 0.3f, 1);
            brick.FaceZ(LaneZ - LaneHalf, west, east, 0f, WallHeight, 1);
            brick.FaceZ(LaneZ + LaneHalf, west, east, 0f, WallHeight, -1);
            for (var i = 0; i < 4; i++)
            {
                var x = west + (east - west) * (0.18f + i * 0.2f);
                stone.Box(new Vector3(x, 4.4f, LaneZ), new Vector3(0.42f, 0.5f, LaneHalf * 2f + 0.2f));
            }
            brick.Box(new Vector3((west + east) * 0.5f, 6.6f, LaneZ), new Vector3(east - west, 4.2f, LaneHalf * 2f + 0.3f));
        }

        /// <summary>ヤード。敷石を敷き、四方の囲いは別に組む</summary>
        static void YardShell(Bank brick, Bank stone, Bank paving)
        {
            // 壁の内側へ少し潜らせる。継ぎ目で床が抜けて見えないように。
            // ただし小路の口だけは潜らせず、ちょうど LaneWest で止める。
            // 同じ高さの面が 2 枚重なると、歩くたびにどちらが手前か入れ替わって絵がちらつく
            var mouth0 = LaneZ - LaneHalf - 0.3f;
            var mouth1 = LaneZ + LaneHalf + 0.3f;
            paving.FaceY(0.02f, YardWest - 0.4f, LaneWest + 0.4f, YardSouth - 0.4f, mouth0, 1);
            paving.FaceY(0.02f, YardWest - 0.4f, LaneWest, mouth0, mouth1, 1);
            paving.FaceY(0.02f, YardWest - 0.4f, LaneWest + 0.4f, mouth1, YardNorth + 0.4f, 1);
        }

        // ---- 付属物 --------------------------------------------------------

        /// <summary>
        /// 面に付く物。樋、非常階段、室外機、そして通りを渡る電線。
        /// これが無いと、いくら窓を彫っても書き割りのままになる
        /// </summary>
        static void Fixtures(Transform parent)
        {
            Clear(parent);
            var metal = new Bank { Texel = 0.7f };
            var rng = new System.Random(3300);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                var wx = side * StreetHalf;
                var inward = -side;
                for (var z = StreetSouth + 3f; z < StreetNorth - 2f; z += (float)(4.0 + rng.NextDouble() * 3.0))
                {
                    if (side < 0 && z > LaneZ - LaneHalf - 1f && z < LaneZ + LaneHalf + 1f) continue;
                    var roll = rng.NextDouble();
                    if (roll < 0.34) Downpipe(metal, wx, z, inward, (float)(9.0 + rng.NextDouble() * 5.0));
                    else if (roll < 0.62) Aircon(metal, wx, z, inward, (float)(3.4 + rng.NextDouble() * 6.0));
                    else FireEscape(metal, wx, z, inward);
                }
            }
            Cables(metal, rng);
            Clutter(metal, rng);
            metal.Emit(parent, "Fixtures", Mat("Metal"), false, Generated);
            Lanterns(Child(parent, "Lanterns"));
        }

        /// <summary>雨樋。壁から少し浮かせ、受け金具を等間隔に打つ</summary>
        static void Downpipe(Bank b, float wx, float z, int inward, float height)
        {
            var x = wx + inward * 0.16f;
            b.Box(new Vector3(x, height * 0.5f, z), new Vector3(0.16f, height, 0.16f));
            for (var y = 1.2f; y < height; y += 1.8f)
                b.Box(new Vector3(wx + inward * 0.08f, y, z), new Vector3(0.16f, 0.07f, 0.26f));
            b.Box(new Vector3(x + inward * 0.12f, 0.22f, z), new Vector3(0.36f, 0.16f, 0.16f));
        }

        /// <summary>室外機。壁に据えた箱と、受けの腕</summary>
        static void Aircon(Bank b, float wx, float z, int inward, float y)
        {
            var x = wx + inward * 0.42f;
            b.Box(new Vector3(x, y, z), new Vector3(0.74f, 0.62f, 0.86f));
            b.Box(new Vector3(wx + inward * 0.20f, y - 0.36f, z), new Vector3(0.40f, 0.07f, 0.9f));
            for (var i = -2; i <= 2; i++)
                b.Box(new Vector3(x + inward * 0.36f, y + i * 0.11f, z), new Vector3(0.05f, 0.05f, 0.7f));
        }

        /// <summary>
        /// 非常階段。踊り場と手摺、斜めの段。
        /// 段だけを宙に並べると浮いて見えるので、桁を通し、壁へ受けを打ち、
        /// いちばん下には地面まで届く梯子を下ろす
        /// </summary>
        static void FireEscape(Bank b, float wx, float z, int inward)
        {
            var from = 4.2f;
            var lip = 0.75f;
            for (var f = 0; f < 2; f++)
            {
                var y = from + f * UpperFloor;
                var x = wx + inward * lip;
                b.Box(new Vector3(x, y, z), new Vector3(1.5f, 0.08f, 2.6f));                     // 踊り場
                // 壁へ打った受け。ここが無いと踊り場が宙に浮く
                for (var i = -1; i <= 1; i += 2)
                {
                    b.Box(new Vector3(wx + inward * (lip * 0.5f), y - 0.34f, z + i * 1.1f),
                        new Vector3(1.5f, 0.07f, 0.09f));
                    b.Box(new Vector3(wx + inward * (lip * 0.95f), y - 0.18f, z + i * 1.1f),
                        new Vector3(0.09f, 0.42f, 0.09f));
                }
                // 手摺
                b.Box(new Vector3(x + inward * 0.70f, y + 0.52f, z), new Vector3(0.06f, 1.04f, 2.6f));
                for (var i = -1; i <= 1; i += 2)
                    b.Box(new Vector3(x, y + 0.52f, z + i * 1.28f), new Vector3(1.5f, 1.04f, 0.06f));
                b.Box(new Vector3(x + inward * 0.70f, y + 1.02f, z), new Vector3(0.1f, 0.07f, 2.6f));
                // 斜めの段と、それを支える桁
                var rise = UpperFloor - 0.3f;
                var run = 1.1f;
                var mid = new Vector3(x + inward * (0.1f + run * 0.5f), y + 0.12f + rise * 0.5f, z + 1.0f);
                var tilt = Quaternion.AngleAxis(inward * Mathf.Atan2(rise, run) * Mathf.Rad2Deg,
                    Vector3.forward);
                var span = Mathf.Sqrt(rise * rise + run * run);
                for (var i = -1; i <= 1; i += 2)
                    b.Box(mid + new Vector3(0f, 0f, i * 0.34f), new Vector3(0.09f, span, 0.09f),
                        Quaternion.FromToRotation(Vector3.up, tilt * Vector3.up));
                var step = 8;
                for (var i = 0; i < step; i++)
                {
                    var t = (i + 0.5f) / step;
                    b.Box(new Vector3(x + inward * (0.1f + t * run - run * 0.5f), y + 0.12f + t * rise, z + 1.0f),
                        new Vector3(0.26f, 0.05f, 0.7f));
                }
            }
            // いちばん下の梯子。地面まで届かせる
            var bx = wx + inward * (lip + 0.55f);
            for (var i = -1; i <= 1; i += 2)
                b.Box(new Vector3(bx, from * 0.5f, z + i * 0.3f), new Vector3(0.07f, from, 0.07f));
            for (var y = 0.35f; y < from - 0.2f; y += 0.36f)
                b.Box(new Vector3(bx, y, z), new Vector3(0.06f, 0.05f, 0.6f));
        }

        /// <summary>通りを渡す電線。真ん中を少し垂らす</summary>
        static void Cables(Bank b, System.Random rng)
        {
            for (var z = StreetSouth + 6f; z < StreetNorth - 4f; z += (float)(6.5 + rng.NextDouble() * 3.0))
            {
                var y = (float)(6.4 + rng.NextDouble() * 2.6);
                var sag = (float)(0.35 + rng.NextDouble() * 0.5);
                var seg = 6;
                for (var i = 0; i < seg; i++)
                {
                    var t0 = (float)i / seg;
                    var t1 = (float)(i + 1) / seg;
                    var x0 = Mathf.Lerp(-StreetHalf, StreetHalf, t0);
                    var x1 = Mathf.Lerp(-StreetHalf, StreetHalf, t1);
                    var y0 = y - sag * 4f * t0 * (1f - t0);
                    var y1 = y - sag * 4f * t1 * (1f - t1);
                    b.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z),
                        new Vector3(x1 - x0, Mathf.Max(0.05f, Mathf.Abs(y1 - y0) + 0.05f), 0.05f));
                }
            }
        }

        /// <summary>路上の物。車止めと、積んだ木箱</summary>
        static void Clutter(Bank b, System.Random rng)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                var x = side * (RoadHalf + 0.45f);
                for (var z = StreetSouth + 4f; z < StreetNorth - 3f; z += 5.5f)
                {
                    // 小道の口には立てない。入口が塞がって見える
                    if (side < 0 && z > LaneZ - 3.2f && z < LaneZ + 3.2f) continue;
                    b.Box(new Vector3(x, KerbRise + 0.42f, z), new Vector3(0.16f, 0.84f, 0.16f));
                    b.Box(new Vector3(x, KerbRise + 0.86f, z), new Vector3(0.22f, 0.07f, 0.22f));
                    Occupy(x, z, 0.32f);
                }
                for (var z = StreetSouth + 6f; z < StreetNorth - 4f; z += (float)(7.0 + rng.NextDouble() * 5.0))
                {
                    if (side < 0 && z > LaneZ - 3f && z < LaneZ + 3f) continue;
                    var wx = side * (StreetHalf - 0.55f);
                    var pile = 1 + rng.Next(3);
                    Occupy(wx, z, 0.6f);
                    for (var i = 0; i < pile; i++)
                    {
                        var w = (float)(0.42 + rng.NextDouble() * 0.28);
                        b.Box(new Vector3(wx - side * (float)(rng.NextDouble() * 0.3), KerbRise + w * 0.5f + i * w * 0.9f,
                                z + (float)(rng.NextDouble() - 0.5) * 0.8f),
                            new Vector3(w, w, w));
                    }
                }
            }
        }

        /// <summary>電線から下がる提灯。小さいが、通りの奥行きを作る</summary>
        static void Lanterns(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(9110);
            var tint = new Color[]
            {
                new Color(1.00f, 0.42f, 0.30f), new Color(1.00f, 0.78f, 0.36f),
                new Color(0.42f, 0.86f, 1.00f), new Color(1.00f, 0.36f, 0.62f),
            };
            for (var z = StreetSouth + 6.5f; z < StreetNorth - 4f; z += (float)(3.0 + rng.NextDouble() * 2.0))
            {
                var x = (float)(rng.NextDouble() * 2f - 1f) * (StreetHalf - 1.2f);
                var y = (float)(5.4 + rng.NextDouble() * 1.4);
                var col = tint[rng.Next(tint.Length)];
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "Lantern";
                g.transform.SetParent(parent, false);
                g.transform.localPosition = new Vector3(x, y, z);
                g.transform.localScale = new Vector3(0.24f, 0.17f, 0.24f);
                g.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(col, 1.5f);
                Object.DestroyImmediate(g.GetComponent<Collider>());
            }
        }


        // ---- 街灯と水たまり ------------------------------------------------

        /// <summary>
        /// 壁付けの街灯。ネオンだけでは路面が沈むので、上から白い灯りを落とす。
        /// 雨に濡れた路面がここで初めて光る
        /// </summary>
        static void Lamps(Transform parent)
        {
            Clear(parent);
            var metal = new Bank { Texel = 0.7f };
            var n = 0;
            for (var z = StreetSouth + 6f; z < StreetNorth - 3f; z += 11f)
            {
                for (var s = 0; s < 2; s++)
                {
                    var side = s == 0 ? -1 : 1;
                    if (side < 0 && z > LaneZ - 3f && z < LaneZ + 3f) continue;
                    var wx = side * StreetHalf;
                    var inward = -side;
                    var y = 5.2f;
                    var arm = 1.35f;
                    metal.Box(new Vector3(wx + inward * arm * 0.5f, y, z), new Vector3(arm, 0.09f, 0.09f));
                    metal.Box(new Vector3(wx + inward * 0.25f, y - 0.42f, z), new Vector3(0.5f, 0.09f, 0.08f));
                    metal.Box(new Vector3(wx + inward * arm, y - 0.18f, z), new Vector3(0.44f, 0.30f, 0.44f));
                    var head = new GameObject("Lamp" + n++);
                    head.transform.SetParent(parent, false);
                    head.transform.localPosition = new Vector3(wx + inward * arm, y - 0.36f, z);
                    var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    glass.name = "Glass";
                    glass.transform.SetParent(head.transform, false);
                    glass.transform.localScale = new Vector3(0.34f, 0.06f, 0.34f);
                    glass.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(new Color(1.00f, 0.92f, 0.78f), 2.4f);
                    Object.DestroyImmediate(glass.GetComponent<Collider>());
                    var l = head.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1.00f, 0.90f, 0.76f);
                    l.range = 14f;
                    l.intensity = 26f;
                    l.shadows = LightShadows.None;
                }
            }
            metal.Emit(parent, "Brackets", Mat("Metal"), false, Generated);
        }

        /// <summary>
        /// 水たまり。路面より少しだけ上に、よく光る面を置く。
        /// 雨の夜は、路面そのものより水の照り返しで見えている
        /// </summary>
        static void Puddles(Transform parent)
        {
            Clear(parent);
            // 水は下の地面を透かして見せる。単色の板を置くと、暗い場では黒い穴になる。
            // 濡れた縁 → 水面 の 2 枚を重ねて、乾いた地面へなだらかに繋ぐ
            var roadDamp = new Bank { Texel = 0.22f };
            var roadWet = new Bank { Texel = 0.22f };
            var stoneDamp = new Bank { Texel = 0.42f };
            var stoneWet = new Bank { Texel = 0.42f };
            // 少し大きめに戻す。輪郭が自由になったので、広くても染みには見えない
            var rng = new System.Random(7720);

            // 車道と歩道は高さも絵も違う。縁石をまたがせると、
            // 歩道の絵が一段高いまま車道へ張り出して見える。どちらか片方へ必ず収める
            for (var z = StreetSouth + 3f; z < StreetNorth - 2f; z += (float)(2.6 + rng.NextDouble() * 3.4))
            {
                if (rng.NextDouble() < 0.34)
                {
                    // 歩道。幅 1.7 しかないので小ぶりに
                    var w = (float)(0.45 + rng.NextDouble() * 0.45);
                    var d = (float)(0.5 + rng.NextDouble() * 0.9);
                    var reach = Reach(w, d);
                    var lo = RoadHalf + reach;
                    var hi = StreetHalf - reach;
                    if (hi <= lo) continue;
                    var side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var x = side * Mathf.Lerp(lo, hi, (float)rng.NextDouble());
                    Pool(stoneDamp, stoneWet, KerbRise + 0.006f, x, z, w, d, rng);
                }
                else
                {
                    var w = (float)(0.9 + rng.NextDouble() * 1.9);
                    var d = (float)(0.7 + rng.NextDouble() * 1.5);
                    var span = RoadHalf - Reach(w, d);
                    if (span <= 0f) continue;
                    var x = (float)(rng.NextDouble() * 2.0 - 1.0) * span;
                    Pool(roadDamp, roadWet, 0.006f, x, z, w, d, rng);
                }
            }
            // 中庭。照り返しはここだけが持つので、筋のほかにも散らす。壁は越えさせない
            for (var x = YardWest + 1.5f; x < LaneWest - 1f; x += (float)(1.6 + rng.NextDouble() * 1.8))
            {
                var lanes = 1 + rng.Next(3);
                for (var i = 0; i < lanes; i++)
                {
                    var w = (float)(0.7 + rng.NextDouble() * 1.7);
                    var d = (float)(0.5 + rng.NextDouble() * 1.3);
                    var reach = Reach(w, d);
                    if (x - reach < YardWest || x + reach > LaneWest) continue;
                    var z = Mathf.Lerp(YardSouth + reach, YardNorth - reach, (float)rng.NextDouble());
                    Pool(stoneDamp, stoneWet, 0.026f, x, z, w, d, rng);
                }
            }
            // 小路。屋根があるので口の側だけ濡れている。壁の間に収める
            for (var x = LaneWest + 0.8f; x < -StreetHalf - 1f; x += (float)(2.2 + rng.NextDouble() * 2.0))
            {
                var w = (float)(0.6 + rng.NextDouble() * 1.0);
                var d = LaneHalf * 2f - 1.0f;
                var reach = Reach(w, d);
                if (reach > LaneHalf - 0.05f) continue;
                Pool(stoneDamp, stoneWet, 0.024f, x, LaneZ, w, d, rng);
            }

            // 濡れた縁は地面より少し暗いだけ。水面はさらに暗く、艶を上げる。
            // 暗くしすぎると黒い穴に見えるので、下の地面が透けて見える明るさに留める
            // 暗さで見せない。濡れているのは艶と映り込みで伝える
            roadDamp.Emit(parent, "DampRoad", WetMat("DampRoad", "AlleyAsphalt", 0.94f, 0.30f), false, Generated);
            stoneDamp.Emit(parent, "DampStone", WetMat("DampStone", "AlleyCobble", 0.94f, 0.30f), false, Generated);
            roadWet.Emit(parent, "WetRoad", WetMat("WetRoad", "AlleyAsphalt", 0.86f, 0.82f), false, Generated);
            stoneWet.Emit(parent, "WetStone", WetMat("WetStone", "AlleyCobble", 0.86f, 0.82f), false, Generated);
        }

        /// <summary>
        /// 水たまりが中心からどこまで届くか。うねりで膨らむぶんと、
        /// 外へ広げた濡れた縁ぶんを足した最大値。境をまたがせないための当たり
        /// </summary>
        static float Reach(float w, float d)
        {
            return Mathf.Max(w, d) * 0.5f * Swell + 0.20f;
        }

        /// <summary>うねりで半径が膨らむ上限。Pool のうねりの振れ幅と揃える</summary>
        const float Swell = 1.60f;

        /// <summary>
        /// 水たまり 1 つ。四角を並べると床に黒い四角が乗っているようにしか見えない。
        /// 中心から放射に縁を取り、うねりを 2 つ重ねて丸でも四角でもない輪郭にする。
        /// 濡れた縁を一回り外へ敷いて、乾いた地面へなだらかに繋ぐ
        /// </summary>
        static void Pool(Bank damp, Bank wet, float y, float cx, float cz, float w, float d, System.Random rng)
        {
            var n = 11 + rng.Next(6);
            var rim = new Vector2[n];
            var edge = new Vector2[n];
            var phase = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            // 2 つ足して Swell - 1 を超えないように。超えると境をまたぐ
            var wob1 = (float)(0.16 + rng.NextDouble() * 0.20);
            var wob2 = (float)(0.08 + rng.NextDouble() * 0.14);
            var lean = (float)(rng.NextDouble() * Mathf.PI);
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.PI * 2f * i / n;
                var r = 1f + Mathf.Sin(a * 2f + phase) * wob1 + Mathf.Sin(a * 3f - phase * 1.7f) * wob2;
                // 楕円を少し倒して、縦横のどちらにも寄らせない
                var ex = Mathf.Cos(a) * w * 0.5f * r;
                var ez = Mathf.Sin(a) * d * 0.5f * r;
                var px = cx + ex * Mathf.Cos(lean) - ez * Mathf.Sin(lean);
                var pz = cz + ex * Mathf.Sin(lean) + ez * Mathf.Cos(lean);
                rim[i] = new Vector2(px, pz);
                // 縁は外へ 0.18 ほど広げる
                var ox = px - cx; var oz = pz - cz;
                var len = Mathf.Max(0.001f, Mathf.Sqrt(ox * ox + oz * oz));
                edge[i] = new Vector2(px + ox / len * 0.18f, pz + oz / len * 0.18f);
            }
            damp.FanY(new Vector3(cx, y, cz), edge);
            wet.FanY(new Vector3(cx, y + 0.004f, cz), rim);
        }

        /// <summary>
        /// 濡れた地面。下の地面と同じ絵を貼って暗く沈め、艶だけ上げる。
        /// 絵を貼らずに色だけにすると、暗い場では黒い板にしか見えない
        /// </summary>
        static Material WetMat(string name, string skin, float dim, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + skin + ".png"));
            m.SetColor("_BaseColor", new Color(dim, dim, dim * 1.06f, 1f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 映り込みの下地になる空。既定のままだと昼の空なので、
        /// 濡れた面がそれを映して白く光る。夜の色まで落とす。
        /// 環境光は場面側で別に持っているので、ここでは触らない
        /// </summary>
        static void NightSky()
        {
            var path = Materials + "NightSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Skybox/Procedural"));
                m.name = "NightSky";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_SunDisk", 0f);
            m.SetFloat("_SunSize", 0f);
            m.SetFloat("_AtmosphereThickness", 0.40f);
            m.SetColor("_SkyTint", new Color(0.17f, 0.19f, 0.30f));
            m.SetColor("_GroundColor", new Color(0.035f, 0.036f, 0.045f));
            m.SetFloat("_Exposure", 0.22f);
            EditorUtility.SetDirty(m);
            RenderSettings.skybox = m;
            RenderSettings.reflectionIntensity = 1f;
        }

        /// <summary>映り込みを焼く。組み直すたびに焼き直さないと、前の絵が残る</summary>
        static void BakeMirrors(Transform root)
        {
            var probes = root.GetComponentsInChildren<ReflectionProbe>(true);
            for (var i = 0; i < probes.Length; i++)
            {
                var path = Generated + "Mirror" + i + ".exr";
                if (!Lightmapping.BakeReflectionProbe(probes[i], path))
                    Debug.LogWarning("映り込みを焼けなかった: " + path);
            }
        }

        /// <summary>
        /// 映り込みの下地。これが無いと、艶のある面は空だけを映して黒く沈む。
        /// ネオンと窓の灯りを拾わせたいので、通りと中庭に置いて焼く
        /// </summary>
        static void Mirrors(Transform parent)
        {
            Clear(parent);
            var at = new Vector3[]
            {
                new Vector3(0f, 2.6f, 2f),
                new Vector3(0f, 2.6f, 28f),
                new Vector3((YardWest + LaneWest) * 0.5f, 2.6f, LaneZ),
                new Vector3((LaneWest - StreetHalf) * 0.5f, 1.8f, LaneZ),
            };
            var size = new Vector3[]
            {
                new Vector3(StreetHalf * 2f, 9f, 34f),
                new Vector3(StreetHalf * 2f, 9f, 34f),
                new Vector3(LaneWest - YardWest, 9f, YardNorth - YardSouth),
                new Vector3(-StreetHalf - LaneWest, 5f, LaneHalf * 2f),
            };
            for (var i = 0; i < at.Length; i++)
            {
                var go = new GameObject("Mirror" + i);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = at[i];
                var pr = go.AddComponent<ReflectionProbe>();
                pr.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                pr.size = size[i];
                pr.resolution = 64;
                pr.hdr = true;
                pr.shadowDistance = 0f;
                pr.cullingMask = ~0;
                pr.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
                pr.importance = 1;
                pr.boxProjection = true;
            }
        }

        // ---- 人 ------------------------------------------------------------

        /// <summary>仮置きの人に使うモデル。自室の主人公と同じ Quaternius の一式</summary>
        static readonly string[] CrowdModels =
        {
            "W_Casual", "W_SciFi", "W_Formal", "W_Adventurer", "W_Suit",
        };

        /// <summary>置き場所ひとつ。どのモデルを、どの姿勢で、どこへ向けて立たせるか</summary>
        struct Spot
        {
            public Vector3 at;
            public float yaw;
            public int pose;
            public float scale;
        }

        /// <summary>
        /// 通りとヤードの人。モデルの骨を曲げて姿勢を作り、その形を焼いて 1 枚の mesh へ束ねる。
        /// 焼いてしまえば実行時に骨は動かないので、何十人立てても描画は 1 回で済む。
        /// 顔は作らない方針どおり、色は灰ひと色の半透明だけを当てる
        /// </summary>
        static void Crowd(Transform parent)
        {
            Clear(parent);
            var spots = new List<Spot>();
            var rng = new System.Random(4820);
            Skipped = 0;
            TouchShell();

            // 通り。立ち止まっている人を並べ、ときどき二人組で向かい合わせる
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                for (var z = WalkSouth + 2f; z < StreetNorth - 3f; z += (float)(2.3 + rng.NextDouble() * 2.8))
                {
                    if (side < 0 && z > LaneZ - 2.5f && z < LaneZ + 2.5f) continue;
                    // 壁の面は ±StreetHalf。飾りと腕の振りぶん、そこから 0.85 は空ける
                    var x = side * (RoadHalf + 0.42f + (float)rng.NextDouble() * 0.40f);
                    if (rng.NextDouble() < 0.28)
                    {
                        // 二人組。肩を寄せて向かい合う
                        var face = (float)(rng.NextDouble() * 360.0);
                        var gap = 0.78f;
                        var off = Quaternion.Euler(0f, face, 0f) * new Vector3(0f, 0f, gap * 0.5f);
                        Put(spots, new Vector3(x - off.x, KerbRise, z - off.z), face, StandPose(rng), rng);
                        Put(spots, new Vector3(x + off.x, KerbRise, z + off.z), face + 180f, StandPose(rng), rng);
                        continue;
                    }
                    var roll = rng.NextDouble();
                    var pose = roll < 0.30 ? 2 : roll < 0.58 ? 1 : roll < 0.80 ? 3 : 4;
                    var yaw = roll < 0.30 ? (side > 0 ? 90f : -90f) : (float)(rng.NextDouble() * 360.0);
                    Put(spots, new Vector3(x, KerbRise, z), yaw + (float)(rng.NextDouble() * 24.0 - 12.0), pose, rng);
                }
            }
            for (var z = WalkSouth + 6f; z < StreetNorth - 6f; z += (float)(8.0 + rng.NextDouble() * 6.0))
                Put(spots, new Vector3((float)(rng.NextDouble() * 2.0 - 1.0) * (RoadHalf - 0.6f), 0f, z),
                    (float)(rng.NextDouble() * 360.0), StandPose(rng), rng);

            // 小路
            for (var x = LaneWest + 2.5f; x < -StreetHalf - 2f; x += (float)(4.0 + rng.NextDouble() * 3.0))
            {
                // 狭いので壁に背をつけて立たせる。真ん中に立つと道を塞ぐ
                var wall = rng.NextDouble() < 0.5 ? -1 : 1;
                Put(spots, new Vector3(x, 0.02f, LaneZ + wall * (LaneHalf - 0.42f)),
                    wall > 0 ? 180f : 0f, 8, rng);
            }

            // ヤード。売り手は出店の奥に座る
            var market = GameObject.Find("Alley/Market");
            if (market != null)
            {
                foreach (Transform stall in market.transform)
                {
                    if (!stall.name.StartsWith("Stall")) continue;
                    if (rng.NextDouble() < 0.42) continue;          // 空の店もある
                    var p = stall.localPosition;
                    var yaw = stall.localEulerAngles.y;
                    // 座ると膝が 0.47 ほど前へ出る。台にぶつからないよう奥へ下げる
                    var back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 0.5f, 0f, 0.95f);
                    var seat = rng.Next(3);
                    Put(spots, new Vector3(p.x + back.x, 0.02f, p.z + back.z),
                        yaw + 180f + (float)(rng.NextDouble() * 40.0 - 20.0), seat == 0 ? 5 : seat == 1 ? 6 : 7, rng, true);
                }
            }
            // ヤードの買い手
            for (var x = LaneWest - 4.6f; x > YardWest + 2.4f; x -= (float)(1.0 + rng.NextDouble() * 1.2))
            {
                var lanes = 1 + rng.Next(3);
                for (var i = 0; i < lanes; i++)
                {
                    var z = LaneZ + (float)(rng.NextDouble() * 2.0 - 1.0) * (AisleHalf - 0.4f);
                    if (rng.NextDouble() < 0.30)
                    {
                        var face = (float)(rng.NextDouble() * 360.0);
                        var off = Quaternion.Euler(0f, face, 0f) * new Vector3(0f, 0f, 0.38f);
                        Put(spots, new Vector3(x - off.x, 0.02f, z - off.z), face, StandPose(rng), rng);
                        Put(spots, new Vector3(x + off.x, 0.02f, z + off.z), face + 180f, StandPose(rng), rng);
                        continue;
                    }
                    Put(spots, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.6f, 0.02f, z),
                        (float)(rng.NextDouble() * 360.0), StandPose(rng), rng);
                }
            }

            // 露店の前に立ち止まっている客。台帳に空きがあるところだけ
            var market2 = GameObject.Find("Alley/Market");
            if (market2 != null)
            {
                foreach (Transform stall in market2.transform)
                {
                    Transform bodyT = stall.name.StartsWith("Stall") ? stall : stall.Find("Stall");
                    if (bodyT == null) continue;
                    if (rng.NextDouble() < 0.38) continue;
                    var yaw2 = bodyT.localEulerAngles.y;
                    var front2 = Quaternion.Euler(0f, yaw2, 0f) * new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 1.1f, 0f, -1.35f);
                    var at2 = bodyT.localPosition + front2;
                    // 店の正面は店の -z 側。客はそこに立って、店のほうを向く
                    Put(spots, new Vector3(at2.x, 0.02f, at2.z), yaw2 + (float)(rng.NextDouble() * 30.0 - 15.0),
                        rng.NextDouble() < 0.4 ? 6 : StandPose(rng), rng);
                }
            }
            // 露天席。椅子の 7 割ほどが埋まっている。空席が混じるほうが店らしい
            for (var i = 0; i < ChairSpots.Count; i++)
            {
                var c = ChairSpots[i];
                if (rng.NextDouble() < 0.30) continue;
                var roll = rng.NextDouble();
                var seat = roll < 0.42 ? 6 : roll < 0.78 ? 5 : 7;
                // 椅子に深く腰掛けるので、座面より少し後ろへ置く
                var back = Quaternion.Euler(0f, c.w, 0f) * new Vector3(0f, 0f, -0.07f);
                Put(spots, new Vector3(c.x + back.x, c.y, c.z + back.z),
                    c.w + (float)(rng.NextDouble() * 14.0 - 7.0), seat, rng, true);
                Occupy(c.x, c.z, 0.34f);
            }

            // 樽の卓のまわり。立ったまま飲んでいる
            for (var i = 0; i < StandSpots.Count; i++)
            {
                var c = StandSpots[i];
                if (rng.NextDouble() < 0.34) continue;
                // 立ち飲みは場所に幅があるので、当たりを見てずらしてよい
                Put(spots, new Vector3(c.x, c.y, c.z), c.w + (float)(rng.NextDouble() * 24.0 - 12.0),
                    rng.NextDouble() < 0.35 ? 2 : StandPose(rng), rng);
            }

            // 四方の壁ぎわ。立ち話や雨宿り。露天席の区画は避ける
            for (var z = YardSouth + 2.2f; z < YardNorth - 2f; z += (float)(1.9 + rng.NextDouble() * 2.2))
            {
                if (z < TerraceNorth + 0.8f)
                    Put(spots, new Vector3(TerraceEast + 0.9f, 0.02f, z), 90f + (float)(rng.NextDouble() * 50.0 - 25.0), 8, rng);
                else
                    Put(spots, new Vector3(YardWest + 1.15f, 0.02f, z), 90f + (float)(rng.NextDouble() * 50.0 - 25.0), 8, rng);
                Put(spots, new Vector3(LaneWest - 1.15f, 0.02f, z), -90f + (float)(rng.NextDouble() * 50.0 - 25.0), 8, rng);
            }
            for (var x = YardWest + 2.2f; x < LaneWest - 2f; x += (float)(1.9 + rng.NextDouble() * 2.2))
            {
                Put(spots, new Vector3(x, 0.02f, TavernTerraceNorth + 0.75f), 0f + (float)(rng.NextDouble() * 50.0 - 25.0), 8, rng);
                Put(spots, new Vector3(x, 0.02f, YardNorth - 1.15f), 180f + (float)(rng.NextDouble() * 50.0 - 25.0), 8, rng);
            }

            UntouchShell();
            Bake(parent, spots, rng);
        }

        /// <summary>
        /// 見えている壁の面。(x0, z0) から (x1, z1) の線で持つ。
        /// 人をここへ近づけすぎると、肩や頭が壁へ食い込む
        /// </summary>
        static readonly Vector4[] Walls =
        {
            // 通りの東側
            new Vector4(StreetHalf, StreetSouth, StreetHalf, StreetNorth),
            // 通りの西側。小路の口だけ切れている
            new Vector4(-StreetHalf, StreetSouth, -StreetHalf, LaneZ - LaneHalf),
            new Vector4(-StreetHalf, LaneZ + LaneHalf, -StreetHalf, StreetNorth),
            // 小路の南北
            new Vector4(LaneWest, LaneZ - LaneHalf, -StreetHalf, LaneZ - LaneHalf),
            new Vector4(LaneWest, LaneZ + LaneHalf, -StreetHalf, LaneZ + LaneHalf),
            // 中庭の四方。東側は小路の口だけ切れている
            new Vector4(YardWest, YardSouth, YardWest, YardNorth),
            new Vector4(YardWest, YardSouth, LaneWest, YardSouth),
            new Vector4(YardWest, YardNorth, LaneWest, YardNorth),
            new Vector4(LaneWest, YardSouth, LaneWest, LaneZ - LaneHalf),
            new Vector4(LaneWest, LaneZ + LaneHalf, LaneWest, YardNorth),
        };

        /// <summary>壁の面までの近さ。一番近い一本との距離を返す</summary>
        static float ToWall(float x, float z)
        {
            var best = 9e9f;
            for (var i = 0; i < Walls.Length; i++)
            {
                var w = Walls[i];
                var ax = w.x; var az = w.y; var bx = w.z; var bz = w.w;
                var vx = bx - ax; var vz = bz - az;
                var len = vx * vx + vz * vz;
                var t = len < 1e-6f ? 0f : Mathf.Clamp01(((x - ax) * vx + (z - az) * vz) / len);
                var dx = x - (ax + vx * t);
                var dz = z - (az + vz * t);
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// そこに人を立たせられるか。壁の面までの間合いと、
        /// 実際に建っている物との当たりの両方で見る。
        /// 壁に寄りかかる姿勢だけは面へ近づけてよい
        /// </summary>
        static bool Standable(Vector3 at, float clearance, bool solid)
        {
            if (ToWall(at.x, at.z) < clearance) return false;
            if (!solid || Touched.Count == 0) return true;
            // 胴の高さだけを見る。足元まで見ると地面や縁石に当たってしまう
            var lo = at + new Vector3(0f, 0.42f, 0f);
            var hi = at + new Vector3(0f, 1.52f, 0f);
            return !Physics.CheckCapsule(lo, hi, 0.32f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>調べのあいだだけ付けた当たり</summary>
        static readonly List<MeshCollider> Touched = new List<MeshCollider>();

        /// <summary>
        /// 壁・柱・出店に当たりを付ける。見た目用の mesh は当たりを持たないので、
        /// 人を置くあいだだけここで付けて、済んだら外す
        /// </summary>
        static void TouchShell()
        {
            UntouchShell();
            var names = new[] { "Shell", "Heritage", "Terraces", "Fixtures", "Market", "Litter", "Lamps" };
            for (var n = 0; n < names.Length; n++)
            {
                var go = GameObject.Find("Alley/" + names[n]);
                if (go == null) continue;
                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                for (var i = 0; i < filters.Length; i++)
                {
                    if (filters[i].sharedMesh == null) continue;
                    if (filters[i].GetComponent<Collider>() != null) continue;
                    var mc = filters[i].gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = filters[i].sharedMesh;
                    Touched.Add(mc);
                }
            }
            Physics.SyncTransforms();
        }

        static void UntouchShell()
        {
            for (var i = 0; i < Touched.Count; i++)
                if (Touched[i] != null) Object.DestroyImmediate(Touched[i]);
            Touched.Clear();
        }

        /// <summary>立ち姿を 4 つから選ぶ。同じ形が並ばないように</summary>
        static int StandPose(System.Random rng)
        {
            var r = rng.NextDouble();
            return r < 0.30 ? 0 : r < 0.56 ? 1 : r < 0.78 ? 3 : 4;
        }

        /// <summary>
        /// 空いていれば立たせる。塞がっていれば諦める。
        /// force は台帳を飛ばす指定で、売り手や椅子のように場所が決まっているとき。
        /// 壁との間合いだけは force でも見る。ここを飛ばすと壁に埋まる
        /// </summary>
        static void Put(List<Spot> spots, Vector3 at, float yaw, int pose, System.Random rng, bool force = false)
        {
            // 壁に寄りかかる姿勢は背中をつけるので、近づいてよい。
            // 場所が決まっている置き方（売り手・椅子）は物との当たりを見ない。
            // そこに卓や樽があるのは承知の上で置いている
            // 腕を開いた姿勢は肩より 0.7 ほど外へ出る。壁の飾りぶんと足して見る
            var clearance = pose == 8 ? 0.40f : 0.85f;
            if (!Standable(at, clearance, !force))
            {
                // 少しずらせば立てることが多い。諦める前に周りを当たる
                if (force || !Shift(ref at, clearance)) { Skipped++; return; }
            }
            if (!force && !Free(at.x, at.z, 0.36f)) return;
            if (!force) Occupy(at.x, at.z, 0.30f);
            spots.Add(Spot1(at, yaw, pose, rng));
        }

        /// <summary>壁や物に当たって諦めた数。組み立ての最後に出す</summary>
        static int Skipped;

        /// <summary>
        /// 立てない場所を少しずらして直す。8 方向を 2 段階の距離で当たり、
        /// 最初に空いたところへ移す。どこも駄目なら false
        /// </summary>
        static bool Shift(ref Vector3 at, float clearance)
        {
            for (var step = 0; step < 3; step++)
            {
                var r = 0.26f + step * 0.28f;
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Mathf.PI * 0.25f;
                    var t = new Vector3(at.x + Mathf.Cos(a) * r, at.y, at.z + Mathf.Sin(a) * r);
                    if (!Standable(t, clearance, true)) continue;
                    at = t;
                    return true;
                }
            }
            return false;
        }

        static Spot Spot1(Vector3 at, float yaw, int pose, System.Random rng)
        {
            var s = new Spot();
            s.at = at;
            s.yaw = yaw;
            s.pose = pose;
            s.scale = (float)(0.95 + rng.NextDouble() * 0.12);
            return s;
        }

        /// <summary>
        /// 置き場所ぶんだけモデルを曲げて焼き、頂点をまとめて 1 枚の mesh にする。
        /// 焼いたあとの形は骨を持たないので、場面には静かな塊として残る
        /// </summary>
        static void Bake(Transform parent, List<Spot> spots, System.Random rng)
        {
            var stage = new GameObject("__crowd_stage");
            var insts = new GameObject[CrowdModels.Length];
            var rests = new List<Dictionary<Transform, Quaternion>>();
            for (var i = 0; i < CrowdModels.Length; i++)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/quaternius/" + CrowdModels[i] + ".fbx");
                if (src == null) { Debug.LogWarning("モデルが無い: " + CrowdModels[i]); rests.Add(null); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, stage.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                // プレハブの結びを解かないと骨は繋ぎ変えられない。解かないまま曲げると靴だけ残る
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // 足首が脛の子ではないので、膝を曲げても靴が置き去りになる。繋ぎ直す
                foreach (var pair in new[] { "L", "R" })
                {
                    var foot = Find(inst.transform, "Foot." + pair);
                    var shin = Find(inst.transform, "LowerLeg." + pair);
                    if (foot != null && shin != null && foot.parent != shin) foot.SetParent(shin, true);
                }
                var rest = new Dictionary<Transform, Quaternion>();
                foreach (var t in inst.GetComponentsInChildren<Transform>(true)) rest[t] = t.localRotation;
                insts[i] = inst;
                rests.Add(rest);
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var tmp = new Mesh();
            foreach (var spot in spots)
            {
                var k = rng.Next(CrowdModels.Length);
                if (insts[k] == null) continue;
                Pose(insts[k].transform, rests[k], spot.pose);
                var drop = PoseDrop(spot.pose);
                var trs = Matrix4x4.TRS(spot.at + new Vector3(0f, -drop * spot.scale, 0f),
                    Quaternion.Euler(0f, spot.yaw, 0f), Vector3.one * spot.scale);
                foreach (var smr in insts[k].GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    // 骨組みは 100 倍で作られているので、縮尺を掛けずに焼く。
                    // 焼いた形は取り込み時の向きのままなので、描画部の向きで起こし直す
                    smr.BakeMesh(tmp, false);
                    var frame = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    var place = trs * frame;
                    var v = tmp.vertices;
                    var n = tmp.normals;
                    var t2 = tmp.triangles;
                    var at = verts.Count;
                    for (var i = 0; i < v.Length; i++)
                    {
                        verts.Add(place.MultiplyPoint3x4(v[i]));
                        norms.Add(place.MultiplyVector(i < n.Length ? n[i] : Vector3.up).normalized);
                        uvs.Add(Vector2.zero);
                    }
                    for (var i = 0; i < t2.Length; i++) tris.Add(at + t2[i]);
                }
            }
            Object.DestroyImmediate(tmp);
            Object.DestroyImmediate(stage);
            if (tris.Count == 0) return;

            var mesh = new Mesh();
            mesh.name = "Crowd";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var path = Generated + "Crowd.asset";
            ProcMesh.Save(mesh, path);
            var go = new GameObject("Crowd");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            go.AddComponent<MeshRenderer>().sharedMaterial = CrowdMat();
            Debug.Log("人 " + spots.Count + " 体、" + (tris.Count / 3) + " ポリゴン。壁に近くて見送った場所 " + Skipped);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// 人をひとりだけ焼いて、自前の mesh を持った物として返す。
        ///
        /// 群衆は 62 体を 1 枚の mesh にまとめてしまうので、出したり消したりできない。
        /// 売り買いの買い手のように、場面の途中で現れて去る人はこちらで作る。
        /// build は体つき。この企画には女の模型しか無いので、
        /// 男は縦横を少し増して体格で見分けさせる
        /// </summary>
        public static GameObject BakeOne(Transform parent, string name, string model,
            Vector3 at, float yaw, int pose, Vector3 build, Material mat)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/quaternius/" + model + ".fbx");
            if (src == null) { Debug.LogWarning("モデルが無い: " + model); return null; }

            var stage = new GameObject("__one_stage");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, stage.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var pair in new[] { "L", "R" })
            {
                var foot = Find(inst.transform, "Foot." + pair);
                var shin = Find(inst.transform, "LowerLeg." + pair);
                if (foot != null && shin != null && foot.parent != shin) foot.SetParent(shin, true);
            }
            var rest = new Dictionary<Transform, Quaternion>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) rest[t] = t.localRotation;
            Pose(inst.transform, rest, pose);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var tmp = new Mesh();
            // 焼いた形は原点に置く。位置と向きは物の transform で持たせて、あとから動かせるようにする
            var trs = Matrix4x4.TRS(new Vector3(0f, -PoseDrop(pose) * build.y, 0f), Quaternion.identity, build);
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(tmp, false);
                var place = trs * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var v = tmp.vertices;
                var n = tmp.normals;
                var t2 = tmp.triangles;
                var head = verts.Count;
                for (var i = 0; i < v.Length; i++)
                {
                    verts.Add(place.MultiplyPoint3x4(v[i]));
                    norms.Add(place.MultiplyVector(i < n.Length ? n[i] : Vector3.up).normalized);
                    uvs.Add(Vector2.zero);
                }
                for (var i = 0; i < t2.Length; i++) tris.Add(head + t2[i]);
            }
            Object.DestroyImmediate(tmp);
            Object.DestroyImmediate(stage);
            if (tris.Count == 0) return null;

            var mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var path = Generated + name + ".asset";
            ProcMesh.Save(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// 買い手の色。群衆より濃くして、まわりの人だかりから浮かせる。
        /// 群衆は薄く透かしてあるが、こちらは芝居の相手なので透かさない
        /// </summary>
        public static Material BuyerMat()
        {
            var path = Materials + "Buyer.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Buyer";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", new Color(0.072f, 0.078f, 0.092f, 1f));
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Smoothness", 0.10f);
            m.SetFloat("_Metallic", 0f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 姿勢ごとの下げ量。骨を曲げても腰は動かないので、座った形のまま床へ落とす。
        /// 値は焼いた形の一番低い頂点を実測して決めてある。靴の裏が床に来る
        /// </summary>
        static float PoseDrop(int pose)
        {
            switch (pose)
            {
                case 5: return 0.422f;
                case 6: return 0.415f;
                case 7: return 0.432f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 骨を曲げて姿勢を作る。安静へ戻してから重ねるので、同じ体を何度でも使い回せる。
        /// 模型は +z を向いている。角度はどれも「正なら前」に揃えてある。
        /// 0 立つ／1 片脚に預ける／2 腕組み／3 手を後ろ／4 振り向く／
        /// 5 椅子に座る／6 卓に肘をつく／7 横を向いて座る／8 壁にもたれる
        /// </summary>
        static void Pose(Transform root, Dictionary<Transform, Quaternion> rest, int pose)
        {
            foreach (var pair in rest) pair.Key.localRotation = pair.Value;

            float thighL = 2f, thighR = -2f, kneeL = 0f, kneeR = 0f;
            float armL = 6f, armR = -6f, elbowL = 12f, elbowR = 12f;
            float outL = 4f, outR = -4f, spine = 2f, lean = 0f;
            // 足首。0 なら脛のまま。座ると爪先が下を向くので、起こして靴の裏を床へ向ける
            var level = false;

            switch (pose)
            {
                case 1:  // 重心を片脚に預けた立ち姿
                    thighL = 5f; thighR = -7f; kneeL = -4f; kneeR = 3f;
                    armL = 4f; armR = -9f; elbowL = 18f; elbowR = 10f;
                    outL = 7f; outR = -3f; spine = 1f;
                    break;
                case 2:  // 腕を組んで立つ
                    armL = 44f; armR = -44f; elbowL = 76f; elbowR = 76f;
                    outL = 12f; outR = -12f;
                    break;
                case 3:  // 手を後ろで組むように、腕を少し引いた立ち姿
                    armL = -12f; armR = -16f; elbowL = 26f; elbowR = 24f;
                    outL = 2f; outR = -6f; thighR = -4f; spine = 1f;
                    break;
                case 4:  // 少し振り向いた立ち姿
                    armL = 10f; armR = -4f; elbowL = 14f; elbowR = 20f;
                    outL = 9f; outR = -8f; spine = 3f; thighL = -3f; thighR = 4f;
                    break;
                // 座る 3 つは腿と膝をほぼ揃える。腰の高さが揃わないと、
                // 同じ椅子に座らせたときに浮いたり沈んだりする。
                // 腿を水平に、脛をほぼ垂直に落として、靴の裏が床へ着く角度を測ってある
                case 5:  // 椅子に座る
                    thighL = 85f; thighR = 84f; kneeL = -70f; kneeR = -72f;
                    outL = 12f; outR = -12f; armL = 26f; armR = 22f; elbowL = 48f; elbowR = 44f;
                    spine = 4f; level = true;
                    break;
                case 6:  // 卓に肘をついて座る
                    thighL = 84f; thighR = 86f; kneeL = -72f; kneeR = -68f;
                    outL = 16f; outR = -9f; armL = 14f; armR = 44f; elbowL = 26f; elbowR = 64f;
                    spine = 8f; level = true;
                    break;
                case 8:  // 壁に背をつけて立つ。足は体より前へ出る
                    lean = -9f; thighL = 12f; thighR = 8f; kneeL = -8f; kneeR = -5f;
                    armL = -16f; armR = 14f; elbowL = 30f; elbowR = 22f;
                    outL = 3f; outR = -8f; spine = -4f;
                    break;
                case 7:  // 膝を寄せて横を向いて座る
                    thighL = 86f; thighR = 83f; kneeL = -68f; kneeR = -74f;
                    outL = 3f; outR = -22f; armL = 38f; armR = 20f; elbowL = 56f; elbowR = 36f;
                    spine = 10f; level = true;
                    break;
            }

            Turn(root, "Hips", lean, 0f);
            Turn(root, "Abdomen", spine * 0.45f, 0f);
            Turn(root, "Torso", spine * 0.35f, 0f);
            Turn(root, "Chest", spine * 0.20f, 0f);
            Turn(root, "Neck", -spine * 0.35f, 0f);
            Turn(root, "Head", -spine * 0.25f + (float)0f, 0f);

            // 四肢は下を向いた骨なので、正の値が前へ出るよう符号を返す。
            // 背骨は上を向いているのでそのまま
            Turn(root, "UpperLeg.L", -thighL, outL * 0.25f);
            Turn(root, "UpperLeg.R", -thighR, outR * 0.25f);
            Turn(root, "LowerLeg.L", -kneeL, 0f);
            Turn(root, "LowerLeg.R", -kneeR, 0f);

            if (level)
            {
                Turn(root, "Foot.L", thighL + kneeL, 0f);
                Turn(root, "Foot.R", thighR + kneeR, 0f);
            }

            Turn(root, "UpperArm.L", -armL, outL);
            Turn(root, "UpperArm.R", -armR, outR);
            Turn(root, "LowerArm.L", -elbowL, 0f);
            Turn(root, "LowerArm.R", -elbowR, 0f);
        }

        /// <summary>骨ひとつを、体から見た軸で曲げる。左右で符号が揃う</summary>
        static void Turn(Transform root, string bone, float pitch, float roll)
        {
            if (Mathf.Approximately(pitch, 0f) && Mathf.Approximately(roll, 0f)) return;
            var b = Find(root, bone);
            if (b == null) return;
            b.rotation = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward) * b.rotation;
        }

        /// <summary>仮置きの人のマテリアル。灰色ひと色を少しだけ透かす</summary>
        static Material CrowdMat()
        {
            var path = Materials + "Crowd.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = "Crowd";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", new Color(0.175f, 0.182f, 0.205f, 0.86f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 場所取り ------------------------------------------------------

        /// <summary>
        /// もう何かが置いてある場所。中心（x, z）と半径で持つ。
        /// 人を立てるときにここを避ける。埋まった人が出るのはこの台帳が無かったため
        /// </summary>
        static readonly List<Vector3> Taken = new List<Vector3>();

        static void Occupy(float x, float z, float radius)
        {
            Taken.Add(new Vector3(x, z, radius));
        }

        /// <summary>半径 r の丸が、どの物とも重ならないか</summary>
        static bool Free(float x, float z, float r)
        {
            for (var i = 0; i < Taken.Count; i++)
            {
                var t = Taken[i];
                var dx = t.x - x;
                var dz = t.y - z;
                var reach = t.z + r;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }

        // ---- ごみ ----------------------------------------------------------

        /// <summary>
        /// 路上のごみ。壁際と縁石に寄せて溜め、筋の真ん中だけ空ける。
        /// 蚤の市と裏路地は、散らかっていないと嘘になる
        /// </summary>
        static void Litter(Transform parent)
        {
            Clear(parent);
            var sack = new Bank { Texel = 0.8f };
            var board = new Bank { Texel = 0.5f };
            var metal = new Bank { Texel = 0.7f };
            var paint = new Bank { Texel = 0.6f };
            var rng = new System.Random(6611);

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                var wall = side * (StreetHalf - 0.45f);
                for (var z = StreetSouth + 2.5f; z < StreetNorth - 2f; z += (float)(1.6 + rng.NextDouble() * 2.4))
                {
                    if (side < 0 && z > LaneZ - 2.2f && z < LaneZ + 2.2f) continue;
                    var roll = rng.NextDouble();
                    var x = wall - side * (float)(rng.NextDouble() * 0.7);
                    if (roll < 0.30) { Sacks(sack, new Vector3(x, KerbRise, z), rng); Occupy(x, z, 0.75f); }
                    else if (roll < 0.46) { Carton(board, new Vector3(x, KerbRise, z), side, rng); Occupy(x, z, 0.55f); }
                    else if (roll < 0.60) { Pallets(board, new Vector3(x, KerbRise, z), rng); Occupy(x, z, 0.6f); }
                    else if (roll < 0.72) { Bin(metal, sack, new Vector3(x, KerbRise, z), rng); Occupy(x, z, 0.85f); }
                    else if (roll < 0.86) Scatter(board, new Vector3(x, KerbRise, z), 1.5f, rng);
                    else { Pipes(metal, new Vector3(x, KerbRise, z), side, rng); Occupy(x, z, 0.45f); }
                }
                // 縁石沿いの小物
                for (var z = StreetSouth + 2f; z < StreetNorth - 2f; z += (float)(0.8 + rng.NextDouble() * 1.6))
                    Scatter(board, new Vector3(side * (RoadHalf + 0.18f), KerbRise, z), 0.7f, rng);
            }
            // 大型ゴミコンテナ
            Skip(paint, metal, board, sack, new Vector3(StreetHalf - 1.5f, KerbRise, 24.5f), rng);
            Occupy(StreetHalf - 1.5f, 24.5f, 1.5f);
            Skip(paint, metal, board, sack, new Vector3(-StreetHalf + 1.6f, KerbRise, 9.0f), rng);
            Occupy(-StreetHalf + 1.6f, 9.0f, 1.5f);

            // 小路とヤード
            for (var x = LaneWest + 1f; x < -StreetHalf - 1f; x += (float)(1.4 + rng.NextDouble() * 1.6))
                Scatter(board, new Vector3(x, 0.02f, LaneZ + (float)(rng.NextDouble() - 0.5) * 2.4f), 1.1f, rng);
            // 入口のすぐ脇には積まない。入った瞬間に奥まで目が通るように
            for (var x = YardWest + 1.2f; x < LaneWest - 2.6f; x += (float)(0.9 + rng.NextDouble() * 1.2))
            {
                for (var i = 0; i < 2; i++)
                {
                    var edge = rng.NextDouble() < 0.5 ? YardSouth + 1.0f : YardNorth - 1.0f;
                    var z = Mathf.Lerp(edge, LaneZ, (float)rng.NextDouble() * 0.55f);
                    if (Mathf.Abs(z - LaneZ) < AisleHalf + 0.30f) continue;
                    var roll = rng.NextDouble();
                    if (roll < 0.34) { Sacks(sack, new Vector3(x, 0.02f, z), rng); Occupy(x, z, 0.7f); }
                    else if (roll < 0.62) { Pallets(board, new Vector3(x, 0.02f, z), rng); Occupy(x, z, 0.6f); }
                    else Scatter(board, new Vector3(x, 0.02f, z), 1.3f, rng);
                }
            }
            sack.Emit(parent, "Sacks", Mat("Tarp"), false, Generated);
            board.Emit(parent, "Cartons", Mat("Timber"), false, Generated);
            metal.Emit(parent, "Bins", Mat("Metal"), false, Generated);
            paint.Emit(parent, "Skips", Mat("Skip"), false, Generated);
        }

        /// <summary>ごみ袋の山。潰れた丸みを箱の重なりで代える</summary>
        static void Sacks(Bank b, Vector3 at, System.Random rng)
        {
            var n = 2 + rng.Next(4);
            for (var i = 0; i < n; i++)
            {
                var w = (float)(0.42 + rng.NextDouble() * 0.26);
                var h = w * (float)(0.62 + rng.NextDouble() * 0.3);
                var p = at + new Vector3((float)(rng.NextDouble() - 0.5) * 0.7f, h * 0.5f + (float)(rng.NextDouble() * 0.12),
                    (float)(rng.NextDouble() - 0.5) * 0.9f);
                b.Box(p, new Vector3(w, h, w * 0.9f),
                    Quaternion.Euler((float)(rng.NextDouble() * 18.0 - 9.0), (float)(rng.NextDouble() * 360.0),
                        (float)(rng.NextDouble() * 18.0 - 9.0)));
            }
        }

        /// <summary>潰した段ボール。壁に立て掛ける</summary>
        static void Carton(Bank b, Vector3 at, int side, System.Random rng)
        {
            var n = 1 + rng.Next(3);
            for (var i = 0; i < n; i++)
            {
                var w = (float)(0.6 + rng.NextDouble() * 0.5);
                var h = (float)(0.7 + rng.NextDouble() * 0.6);
                b.Box(at + new Vector3(side * -0.12f * i, h * 0.45f, (float)(rng.NextDouble() - 0.5) * 0.5f),
                    new Vector3(0.05f, h, w),
                    Quaternion.Euler(0f, (float)(rng.NextDouble() * 16.0 - 8.0), side * (float)(9.0 + rng.NextDouble() * 8.0)));
            }
        }

        /// <summary>積んだ木箱と荷板</summary>
        static void Pallets(Bank b, Vector3 at, System.Random rng)
        {
            var n = 1 + rng.Next(4);
            var y = 0f;
            for (var i = 0; i < n; i++)
            {
                var w = (float)(0.44 + rng.NextDouble() * 0.28);
                var h = (float)(0.26 + rng.NextDouble() * 0.22);
                b.Box(at + new Vector3((float)(rng.NextDouble() - 0.5) * 0.18f, y + h * 0.5f, (float)(rng.NextDouble() - 0.5) * 0.18f),
                    new Vector3(w, h, w * (float)(0.8 + rng.NextDouble() * 0.4)),
                    Quaternion.Euler(0f, (float)(rng.NextDouble() * 40.0 - 20.0), 0f));
                y += h;
            }
        }

        /// <summary>蓋つきのゴミ箱</summary>
        static void Bin(Bank metal, Bank sack, Vector3 at, System.Random rng)
        {
            var yaw = (float)(rng.NextDouble() * 360.0);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            metal.Box(at + new Vector3(0f, 0.46f, 0f), new Vector3(0.56f, 0.92f, 0.48f), rot);
            metal.Box(at + new Vector3(0f, 0.95f, 0f), new Vector3(0.60f, 0.07f, 0.52f),
                rot * Quaternion.Euler((float)(rng.NextDouble() * 22.0), 0f, 0f));
            if (rng.NextDouble() < 0.5) Sacks(sack, at + new Vector3(0.5f, 0f, 0.2f), rng);
        }

        /// <summary>散らばった紙と空き瓶。薄い板と小さな棒で代える</summary>
        static void Scatter(Bank b, Vector3 at, float spread, System.Random rng)
        {
            var n = 2 + rng.Next(5);
            for (var i = 0; i < n; i++)
            {
                var p = at + new Vector3((float)(rng.NextDouble() - 0.5) * spread, 0.012f,
                    (float)(rng.NextDouble() - 0.5) * spread);
                if (rng.NextDouble() < 0.6)
                    b.Box(p, new Vector3((float)(0.14 + rng.NextDouble() * 0.2), 0.012f, (float)(0.1 + rng.NextDouble() * 0.16)),
                        Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));
                else
                    b.Box(p + new Vector3(0f, 0.03f, 0f), new Vector3(0.055f, 0.055f, (float)(0.12 + rng.NextDouble() * 0.1)),
                        Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 90f));
            }
        }

        /// <summary>壁に立て掛けた管</summary>
        static void Pipes(Bank b, Vector3 at, int side, System.Random rng)
        {
            var n = 2 + rng.Next(3);
            for (var i = 0; i < n; i++)
            {
                var len = (float)(1.1 + rng.NextDouble() * 0.9);
                b.Box(at + new Vector3(side * -0.1f, len * 0.42f, (float)(rng.NextDouble() - 0.5) * 0.5f),
                    new Vector3(0.07f, len, 0.07f),
                    Quaternion.Euler(0f, 0f, side * (float)(12.0 + rng.NextDouble() * 10.0)));
            }
        }

        /// <summary>
        /// 大型ゴミコンテナ。工事や引っ越しのゴミを放り込む、端が外へ開いた上開きのやつ。
        /// ロンドンの通りに 1 つ 2 つ置きっぱなしになっていて、縮尺も伝わる。
        ///
        /// 閉じた箱で作ると、暗い通りでは黒い塊にしか見えない。上を開けて中身を見せ、
        /// 端を斜めに広げ、口の縁と縦の桟と吊り金具を足して、形だけで分かるようにする。
        /// 塗りは褪せた黄土。周りが黒に近いので、ここだけ地の色を持たせて輪郭を立てる
        /// </summary>
        /// <param name="paint">箱そのもの。塗られた鉄板</param>
        /// <param name="metal">縁・桟・吊り金具。塗りの剥げた地金</param>
        /// <param name="board">中身の木材と瓦礫</param>
        /// <param name="sack">中身の袋</param>
        static void Skip(Bank paint, Bank metal, Bank board, Bank sack, Vector3 at, System.Random rng)
        {
            var rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 20.0 - 10.0), 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);

            const float hxb = 0.78f;    // 底の半分の長さ
            const float hxt = 0.95f;    // 口の半分の長さ。端が外へ広がる
            const float hz = 0.62f;     // 幅の半分。長辺は立てたまま
            const float h = 1.10f;      // 高さ
            const float t = 0.055f;     // 板の厚み

            // 長辺。下が狭く上が広い台形。外と内を張る
            paint.Quad(p(hxb, 0f, hz), p(-hxb, 0f, hz), p(-hxt, h, hz), p(hxt, h, hz));
            paint.Quad(p(-hxb, 0f, -hz), p(hxb, 0f, -hz), p(hxt, h, -hz), p(-hxt, h, -hz));
            paint.Quad(p(-hxb, 0f, hz - t), p(hxb, 0f, hz - t), p(hxt, h, hz - t), p(-hxt, h, hz - t));
            paint.Quad(p(hxb, 0f, -hz + t), p(-hxb, 0f, -hz + t), p(-hxt, h, -hz + t), p(hxt, h, -hz + t));

            // 端。外へ斜めに開く
            paint.Quad(p(hxb, 0f, hz), p(hxb, 0f, -hz), p(hxt, h, -hz), p(hxt, h, hz));
            paint.Quad(p(-hxb, 0f, -hz), p(-hxb, 0f, hz), p(-hxt, h, hz), p(-hxt, h, -hz));
            paint.Quad(p(hxb - t, 0f, -hz), p(hxb - t, 0f, hz), p(hxt - t, h, hz), p(hxt - t, h, -hz));
            paint.Quad(p(-hxb + t, 0f, hz), p(-hxb + t, 0f, -hz), p(-hxt + t, h, -hz), p(-hxt + t, h, hz));

            // 床と底
            paint.Quad(p(-hxb, t, hz), p(hxb, t, hz), p(hxb, t, -hz), p(-hxb, t, -hz));
            paint.Quad(p(-hxb, 0f, -hz), p(hxb, 0f, -hz), p(hxb, 0f, hz), p(-hxb, 0f, hz));

            // 口の縁。四方に回した角材
            for (var s = -1; s <= 1; s += 2)
            {
                metal.Box(p(0f, h + 0.02f, s * (hz + 0.015f)), new Vector3(2f * hxt + 0.10f, 0.08f, 0.09f), rot);
                metal.Box(p(s * (hxt + 0.02f), h + 0.02f, 0f), new Vector3(0.09f, 0.08f, 2f * hz + 0.12f), rot);
            }

            // 縦の桟。長辺に三本ずつ
            var ribs = new[] { -0.46f, 0.01f, 0.48f };
            foreach (var rx in ribs)
                for (var s = -1; s <= 1; s += 2)
                    metal.Box(p(rx, h * 0.5f - 0.02f, s * (hz + 0.035f)), new Vector3(0.09f, h - 0.10f, 0.07f), rot);

            // 吊り金具。鎖を掛ける立ち上がり
            for (var s = -1; s <= 1; s += 2)
                for (var e = -1; e <= 1; e += 2)
                    metal.Box(p(e * 0.58f, h + 0.13f, s * (hz - 0.02f)), new Vector3(0.11f, 0.20f, 0.06f), rot);

            // 中身。縁から覗いているだけでゴミコンテナと分かる。瓦礫を敷き、その上に木と袋
            board.Box(p(0f, 0.45f, 0f), new Vector3(1.48f, 0.78f, 1.06f), rot);
            for (var i = 0; i < 4; i++)
            {
                var spin = rot * Quaternion.Euler(
                    (float)(rng.NextDouble() * 26.0 - 13.0),
                    (float)(rng.NextDouble() * 60.0 - 30.0),
                    (float)(rng.NextDouble() * 18.0 - 9.0));
                var c = p((float)(rng.NextDouble() - 0.5) * 1.1f,
                    0.98f + (float)rng.NextDouble() * 0.30f,
                    (float)(rng.NextDouble() - 0.5) * 0.8f);
                board.Box(c, new Vector3(1.05f + (float)rng.NextDouble() * 0.55f, 0.045f, 0.16f), spin);
            }
            for (var i = 0; i < 2; i++)
            {
                var c = p((float)(rng.NextDouble() - 0.5) * 1.0f, 1.05f, (float)(rng.NextDouble() - 0.5) * 0.7f);
                sack.Box(c, new Vector3(0.44f, 0.30f, 0.38f),
                    rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 50.0), 0f));
            }
        }


        // ---- 空 ------------------------------------------------------------

        /// <summary>
        /// 低く垂れた黒い雲。板を 3 枚重ね、それぞれ別の速さで流す。
        /// 下から見上げる物なので面は下を向け、街の灯りを受けて底だけ少し色づかせる
        /// </summary>
        static void Sky(Transform parent)
        {
            Clear(parent);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudLayer.png");
            if (tex == null) { Debug.LogWarning("雲の絵が無い"); return; }

            var midX = (YardWest + StreetHalf) * 0.5f;
            var midZ = (StreetSouth + StreetNorth) * 0.5f;
            var wide = (StreetHalf - YardWest) + 120f;
            var deep = (StreetNorth - StreetSouth) + 120f;

            var layers = new float[] { 19f, 25f };
            var tile = new float[] { 0.052f, 0.031f };
            var tint = new Color[]
            {
                new Color(0.017f, 0.015f, 0.023f, 0.90f),
                new Color(0.048f, 0.038f, 0.056f, 0.58f),
            };
            var speed = new Vector2[]
            {
                new Vector2(0.0072f, 0.0021f),
                new Vector2(0.0036f, 0.0011f),
            };
            for (var i = 0; i < layers.Length; i++)
            {
                var bank = new Bank { Texel = tile[i] };
                bank.FaceY(layers[i], midX - wide * 0.5f, midX + wide * 0.5f,
                    midZ - deep * 0.5f, midZ + deep * 0.5f, -1);
                var go = bank.Emit(parent, "Cloud" + i, CloudMat(i, tex, tint[i]), false, Generated);
                if (go == null) continue;
                var drift = go.AddComponent<CloudDrift>();
                var so = new SerializedObject(drift);
                so.FindProperty("speed").vector2Value = speed[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>雲のマテリアル。層ごとに 1 枚ずつ作る。絵を別々にずらすため</summary>
        static Material CloudMat(int index, Texture2D tex, Color tint)
        {
            var path = Materials + "Cloud" + index + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Cloud" + index;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;
            EditorUtility.SetDirty(m);
            return m;
        }


        // ---- 手前の突き当たりと、その先の景色 --------------------------------

        /// <summary>遠景の板を置く位置。突き当たりの奥</summary>
        public const float BackdropZ = StreetSouth - 2.2f;
        /// <summary>突き当たりの抜け。ここから先の景色が見える</summary>
        public const float ArchHalfWidth = 4.2f;
        public const float ArchHigh = 7.2f;
        /// <summary>遠景を撮る位置。歩ける端に立ったときの目の高さ</summary>
        public const float AnchorZ = WalkSouth - 0.4f;
        public const float AnchorY = 1.6f;

        /// <summary>
        /// 手前の突き当たり。壁で塞がず、くぐり抜けの形に開けて、
        /// その奥に遠景の板を立てる。板は別に撮った通りの絵で、
        /// 立ち位置から見たときだけ先が続いて見える
        /// </summary>
        static void SouthEnd(Bank brick, Bank stone, Bank metal)
        {
            var holes = new List<Vector4>
            {
                new Vector4(-ArchHalfWidth, ArchHalfWidth, 0f, ArchHigh),
            };
            brick.FaceZHoles(StreetSouth, -StreetHalf - 1f, StreetHalf + 1f, 0f, WallHeight, 1, holes);
            // 抜けの縁。奥行きを見せる返し
            var depth = 1.6f;
            for (var i = -1; i <= 1; i += 2)
                stone.Box(new Vector3(i * (ArchHalfWidth + 0.18f), ArchHigh * 0.5f, StreetSouth - depth * 0.5f),
                    new Vector3(0.36f, ArchHigh + 0.7f, depth));
            stone.Box(new Vector3(0f, ArchHigh + 0.30f, StreetSouth - depth * 0.5f),
                new Vector3(ArchHalfWidth * 2f + 0.9f, 0.6f, depth));
            // くぐりの天井
            stone.FaceY(ArchHigh, -ArchHalfWidth, ArchHalfWidth, StreetSouth - depth, StreetSouth, -1);
            // 上の階。抜けの上にも建物が乗っている
            for (var f = 0; f < 3; f++)
            {
                var y = ArchHigh + 1.0f + f * 2.8f;
                for (var x = -StreetHalf + 0.9f; x < StreetHalf - 0.5f; x += 2.2f)
                    stone.Box(new Vector3(x, y, StreetSouth + 0.12f), new Vector3(0.95f, 1.25f, 0.24f));
            }
            metal.Box(new Vector3(0f, ArchHigh + 0.85f, StreetSouth - 0.35f), new Vector3(ArchHalfWidth * 1.4f, 0.10f, 0.10f));
        }

        /// <summary>
        /// 通りを渡す歩道橋。手前の区間に架けて、奥行きを作る。
        /// 桁と手摺と段だけの簡単な形だが、頭の上に物があると街が立体になる
        /// </summary>
        static void Walkway(Bank metal, Bank stone, float z, float height)
        {
            var wide = 1.9f;
            stone.Box(new Vector3(0f, height, z), new Vector3(StreetHalf * 2f, 0.22f, wide));
            metal.Box(new Vector3(0f, height - 0.18f, z - wide * 0.5f), new Vector3(StreetHalf * 2f, 0.16f, 0.12f));
            metal.Box(new Vector3(0f, height - 0.18f, z + wide * 0.5f), new Vector3(StreetHalf * 2f, 0.16f, 0.12f));
            for (var i = -1; i <= 1; i += 2)
            {
                var side = i * (wide * 0.5f);
                metal.Box(new Vector3(0f, height + 0.62f, z + side), new Vector3(StreetHalf * 2f, 0.07f, 0.07f));
                for (var x = -StreetHalf + 0.5f; x < StreetHalf; x += 1.1f)
                    metal.Box(new Vector3(x, height + 0.34f, z + side), new Vector3(0.06f, 0.62f, 0.06f));
            }
            // 支柱
            for (var i = -1; i <= 1; i += 2)
                metal.Box(new Vector3(i * (StreetHalf - 0.35f), height * 0.5f, z), new Vector3(0.24f, height, 0.24f));
        }

        /// <summary>
        /// 奥の突き当たり。手前と同じくくぐり抜けにして、その先に遠景の板を立てる
        /// </summary>
        static void NorthEnd(Bank brick, Bank stone, Bank metal)
        {
            var holes = new List<Vector4>
            {
                new Vector4(-ArchHalfWidth, ArchHalfWidth, 0f, ArchHigh),
            };
            brick.FaceZHoles(StreetNorth, -StreetHalf - 1f, StreetHalf + 1f, 0f, WallHeight, -1, holes);
            var depth = 1.6f;
            for (var i = -1; i <= 1; i += 2)
                stone.Box(new Vector3(i * (ArchHalfWidth + 0.18f), ArchHigh * 0.5f, StreetNorth + depth * 0.5f),
                    new Vector3(0.36f, ArchHigh + 0.7f, depth));
            stone.Box(new Vector3(0f, ArchHigh + 0.30f, StreetNorth + depth * 0.5f),
                new Vector3(ArchHalfWidth * 2f + 0.9f, 0.6f, depth));
            stone.FaceY(ArchHigh, -ArchHalfWidth, ArchHalfWidth, StreetNorth, StreetNorth + depth, -1);
            for (var f = 0; f < 3; f++)
            {
                var y = ArchHigh + 1.0f + f * 2.8f;
                for (var x = -StreetHalf + 0.9f; x < StreetHalf - 0.5f; x += 2.2f)
                    stone.Box(new Vector3(x, y, StreetNorth - 0.12f), new Vector3(0.95f, 1.25f, 0.24f));
            }
            metal.Box(new Vector3(0f, ArchHigh + 0.85f, StreetNorth + 0.35f), new Vector3(ArchHalfWidth * 1.4f, 0.10f, 0.10f));
        }

        /// <summary>遠景の板。両端に 1 枚ずつ。撮った絵が無ければ置かない</summary>
        static void Backdrop(Transform parent)
        {
            Clear(parent);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/StreetBackdrop.png");
            if (tex == null)
            {
                Debug.Log("遠景の絵がまだ無い。HalfAware/Shoot the street backdrop で撮る");
                return;
            }
            var size = BackdropSize();
            var m = BackdropMat(tex);
            Plate2(parent, "Backdrop.South", new Vector3(0f, AnchorY, BackdropZ), 180f, size, m, false);
            // 奥は同じ絵を左右で返して使う。並べて見ることは無い
            Plate2(parent, "Backdrop.North", new Vector3(0f, AnchorY, StreetNorth + 2.2f), 0f, size, m, true);
        }

        static void Plate2(Transform parent, string name, Vector3 at, float yaw, Vector2 size, Material m, bool mirror)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(mirror ? -size.x : size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// 板の大きさ。立ち位置から抜けを見込む角に、余裕を足して決める。
        /// ここと撮る側の画角が揃っていないと、絵が浮いて見える
        /// </summary>
        static Vector2 BackdropSize()
        {
            var reach = AnchorZ - BackdropZ;
            var toArch = AnchorZ - StreetSouth;
            var high = (ArchHigh - AnchorY) * reach / toArch * 2.2f;
            var wide = ArchHalfWidth * 2f * reach / toArch * 1.35f;
            return new Vector2(wide, high);
        }

        static Material BackdropMat(Texture2D tex)
        {
            var path = Materials + "Backdrop.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Backdrop";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.92f, 1f));
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 遠景を撮る。通りの北の端から南を向いて、板が見込む画角ぴったりで 1 枚。
        /// 撮った絵を板に貼ると、突き当たりの向こうに通りが続いて見える
        /// </summary>
        [MenuItem("HalfAware/Shoot the street backdrop")]
        public static void ShootBackdrop()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は撮らない"); return; }
            var root = GameObject.Find("Alley");
            if (root == null) { Debug.LogError("路地裏がまだ組まれていない"); return; }
            var old = root.transform.Find("Backdrop");
            if (old != null) old.gameObject.SetActive(false);        // 自分を撮らない
            var body = GameObject.Find("Player/Protagonist");
            var wasOn = body != null && body.activeSelf;
            if (body != null) body.SetActive(false);

            var size = BackdropSize();
            var reach = AnchorZ - BackdropZ;
            var fov = 2f * Mathf.Atan(size.y * 0.5f / reach) * Mathf.Rad2Deg;
            var aspect = size.x / size.y;
            var w = 1280;
            var h = Mathf.RoundToInt(w / aspect);

            var cam = Camera.main;
            var t = cam.transform;
            var keepPos = t.position;
            var keepRot = t.rotation;
            var keepFov = cam.fieldOfView;
            // 北の端から南を向く。ここからなら通りの長さがそのまま奥行きになる
            t.position = new Vector3(0f, AnchorY, WalkNorth - 2.5f);
            t.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            cam.fieldOfView = fov;
            cam.aspect = aspect;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var keepActive = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = keepActive;
            System.IO.File.WriteAllBytes(Application.dataPath + "/Textures/StreetBackdrop.png", shot.EncodeToPNG());
            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);

            t.position = keepPos;
            t.rotation = keepRot;
            cam.fieldOfView = keepFov;
            cam.ResetAspect();
            if (body != null) body.SetActive(wasOn);
            if (old != null) old.gameObject.SetActive(true);
            AssetDatabase.ImportAsset("Assets/Textures/StreetBackdrop.png");
            Debug.Log("遠景を撮った " + w + "x" + h + " 画角 " + fov.ToString("0.0"));
        }

        // ---- 当たり判定 ----------------------------------------------------

        /// <summary>
        /// 見えない箱で通りの輪郭を囲う。見た目の mesh には当たりを付けない。
        /// 面の彫りが細かいので、そのまま当てると引っ掛かって歩けなくなる
        /// </summary>
        static void Bounds(Transform parent)
        {
            Clear(parent);
            Blocker(parent, "Ground.Street", new Vector3(0f, -0.5f, (StreetSouth + StreetNorth) * 0.5f),
                new Vector3(StreetHalf * 2f, 1f, StreetNorth - StreetSouth));
            Blocker(parent, "Ground.Lane", new Vector3((LaneWest - StreetHalf) * 0.5f, -0.48f, LaneZ),
                new Vector3(-StreetHalf - LaneWest, 1f, LaneHalf * 2f));
            Blocker(parent, "Ground.Yard", new Vector3((YardWest + LaneWest) * 0.5f, -0.48f, (YardSouth + YardNorth) * 0.5f),
                new Vector3(LaneWest - YardWest, 1f, YardNorth - YardSouth));

            var h = WallHeight;
            Blocker(parent, "Wall.E", new Vector3(StreetHalf + 0.5f, h * 0.5f, (StreetSouth + StreetNorth) * 0.5f),
                new Vector3(1f, h, StreetNorth - StreetSouth));
            Blocker(parent, "Wall.W.S", new Vector3(-StreetHalf - 0.5f, h * 0.5f, (StreetSouth + LaneZ - LaneHalf) * 0.5f),
                new Vector3(1f, h, LaneZ - LaneHalf - StreetSouth));
            Blocker(parent, "Wall.W.N", new Vector3(-StreetHalf - 0.5f, h * 0.5f, (LaneZ + LaneHalf + StreetNorth) * 0.5f),
                new Vector3(1f, h, StreetNorth - LaneZ - LaneHalf));
            Blocker(parent, "Wall.Head", new Vector3(0f, h * 0.5f, WalkNorth + 0.5f), new Vector3(StreetHalf * 2f + 2f, h, 1f));
            Blocker(parent, "Ground.North", new Vector3(0f, -0.5f, StreetNorth + 3f),
                new Vector3(StreetHalf * 2f, 1f, 10f));
            Blocker(parent, "Wall.Back", new Vector3(0f, h * 0.5f, WalkSouth - 0.5f), new Vector3(StreetHalf * 2f + 2f, h, 1f));
            // 見えている先まで床は続く。落ちないように床だけ伸ばす
            Blocker(parent, "Ground.South", new Vector3(0f, -0.5f, StreetSouth - 3f),
                new Vector3(StreetHalf * 2f, 1f, 8f));

            Blocker(parent, "Lane.S", new Vector3((LaneWest - StreetHalf) * 0.5f, h * 0.5f, LaneZ - LaneHalf - 0.5f),
                new Vector3(-StreetHalf - LaneWest, h, 1f));
            Blocker(parent, "Lane.N", new Vector3((LaneWest - StreetHalf) * 0.5f, h * 0.5f, LaneZ + LaneHalf + 0.5f),
                new Vector3(-StreetHalf - LaneWest, h, 1f));

            Blocker(parent, "Yard.W", new Vector3(YardWest - 0.5f, h * 0.5f, (YardSouth + YardNorth) * 0.5f),
                new Vector3(1f, h, YardNorth - YardSouth + 2f));
            Blocker(parent, "Yard.S", new Vector3((YardWest + LaneWest) * 0.5f, h * 0.5f, YardSouth - 0.5f),
                new Vector3(LaneWest - YardWest, h, 1f));
            Blocker(parent, "Yard.N", new Vector3((YardWest + LaneWest) * 0.5f, h * 0.5f, YardNorth + 0.5f),
                new Vector3(LaneWest - YardWest, h, 1f));
            Blocker(parent, "Yard.E.S", new Vector3(LaneWest + 0.5f, h * 0.5f, (YardSouth + LaneZ - LaneHalf) * 0.5f),
                new Vector3(1f, h, LaneZ - LaneHalf - YardSouth));
            Blocker(parent, "Yard.E.N", new Vector3(LaneWest + 0.5f, h * 0.5f, (LaneZ + LaneHalf + YardNorth) * 0.5f),
                new Vector3(1f, h, YardNorth - LaneZ - LaneHalf));
        }

        /// <summary>雨を受け止める層。粒の当たりはこの層だけを見る</summary>
        public const int RoofLayer = 8;

        /// <summary>
        /// 屋根に当たりを付ける。雨の粒はここへ当たって消える。
        /// 人が屋根の下へ入ったときだけ降りを止める作りだと、
        /// 外から眺めたときに屋根を貫いて降っているのが見えてしまう。
        /// 見えない箱なので、絵には出ない
        /// </summary>
        static void Roofs(Transform parent)
        {
            Clear(parent);
            // 小路の屋根。躯体の下面は 4.5
            Roof(parent, "Roof.Lane", new Vector3((LaneWest - StreetHalf) * 0.5f, 4.45f, LaneZ),
                new Vector3(-StreetHalf - LaneWest, 0.2f, LaneHalf * 2f + 0.4f));
            // 露天席の天蓋
            Roof(parent, "Roof.Bistro",
                new Vector3((TerraceWest + TerraceEast) * 0.5f, 2.78f, (TerraceSouth + TerraceNorth) * 0.5f),
                new Vector3(TerraceEast - TerraceWest + 0.4f, 0.14f, TerraceNorth - TerraceSouth + 0.4f));
            Roof(parent, "Roof.Tavern",
                new Vector3((YardWest + 4.6f + LaneWest - 2.6f) * 0.5f, 2.60f,
                    (YardSouth + 0.65f + TavernTerraceNorth) * 0.5f),
                new Vector3(LaneWest - 2.6f - YardWest - 4.6f, 0.14f, TavernTerraceNorth - YardSouth - 0.65f + 0.4f));
            Roof(parent, "Roof.Restaurant",
                new Vector3((RestaurantWest + RestaurantEast) * 0.5f, 3.32f, YardNorth - 1.05f),
                new Vector3(RestaurantEast - RestaurantWest, 0.14f, 1.6f));
            // ビストロの 2 階の露台。下は雨をしのげる
            Roof(parent, "Roof.Balcony", new Vector3(YardWest + 1.15f, 3.88f, LaneZ - 2.5f),
                new Vector3(2.3f, 0.24f, 8f));
            // 通りを渡る歩道橋
            Roof(parent, "Roof.Walk0", new Vector3(0f, 6.38f, -13.5f), new Vector3(StreetHalf * 2f, 0.24f, 1.9f));
            Roof(parent, "Roof.Walk1", new Vector3(0f, 7.08f, 26.5f), new Vector3(StreetHalf * 2f, 0.24f, 1.9f));
            Roof(parent, "Roof.Walk2", new Vector3(0f, 6.78f, 47.5f), new Vector3(StreetHalf * 2f, 0.24f, 1.9f));

            // 出店のタープ。すでに当たりを持っているので、層だけ移す。
            // 自分の店のタープは破れているので、破れ目からは降り込む
            var market = GameObject.Find("Alley/Market");
            if (market == null) return;
            var all = market.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
                if (all[i].name.StartsWith("Tarp")) all[i].gameObject.layer = RoofLayer;
        }

        /// <summary>屋根ひとつ。見えない箱を置いて、雨の層へ移す</summary>
        static void Roof(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.layer = RoofLayer;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
        }

        static void Blocker(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
        }

        // ---- ネオン --------------------------------------------------------

        /// <summary>看板のひと枚。どちら側の壁に、どの高さで、面をどちらへ向けるか</summary>
        struct Plate
        {
            public string texture;
            public int side;      // 1 が東の壁、-1 が西の壁
            public float z;
            public float y;
            public bool blade;    // true なら壁から突き出して通りの上下を向く
            public float scale;   // 1 で幅 1.1 メートル

            public Plate(string texture, int side, float z, float y, bool blade, float scale)
            {
                this.texture = texture;
                this.side = side;
                this.z = z;
                this.y = y;
                this.blade = blade;
                this.scale = scale;
            }
        }

        /// <summary>看板の地の色。光の色をここから採る</summary>
        static Color NeonTint(string texture)
        {
            switch (texture)
            {
                case "NeonNerve": return new Color(1.00f, 0.18f, 0.66f);
                case "NeonOpen": return new Color(0.22f, 0.93f, 1.00f);
                case "NeonBar": return new Color(0.69f, 0.45f, 1.00f);
                case "NeonHotel": return new Color(1.00f, 0.24f, 0.24f);
                case "NeonArrow": return new Color(1.00f, 0.62f, 0.12f);
                case "NeonEye": return new Color(0.22f, 0.89f, 0.84f);
                case "NeonLive": return new Color(1.00f, 0.89f, 0.25f);
                case "NeonCross": return new Color(0.34f, 1.00f, 0.54f);
                case "NeonClub": return new Color(1.00f, 0.29f, 0.54f);
                case "NeonRings": return new Color(0.36f, 0.66f, 1.00f);
                case "NeonSleep": return new Color(1.00f, 0.47f, 0.82f);
                case "NeonNoodle": return new Color(1.00f, 0.78f, 0.28f);
                default: return Color.white;
            }
        }

        /// <summary>縦長の絵か。縦なら高さが幅の倍になる</summary>
        static bool Tall(string texture)
        {
            return texture == "NeonBar" || texture == "NeonHotel"
                || texture == "NeonArrow" || texture == "NeonCross" || texture == "NeonRings";
        }

        /// <summary>
        /// 通りのネオン。壁に貼る物と、突き出して通りの上下を向く物を混ぜる。
        /// 突き出した物には灯りを付ける。濡れた路面に色が落ちて、通りが極彩色になる
        /// </summary>
        static void Neon(Transform parent)
        {
            Clear(parent);
            var plates = new List<Plate>
            {
                new Plate("NeonSleep",  -1,-21.0f,  6.2f, false, 1.70f),
                new Plate("NeonBar",     1,-19.5f,  4.3f, true,  1.35f),
                new Plate("NeonClub",    1,-16.4f,  8.4f, false, 1.85f),
                new Plate("NeonArrow",  -1,-14.8f,  4.4f, true,  1.25f),
                new Plate("NeonNoodle", -1,-11.6f,  7.0f, false, 1.65f),
                new Plate("NeonCross",   1,-10.2f,  4.2f, true,  1.30f),
                new Plate("NeonEye",     1, -7.0f,  6.6f, false, 1.75f),
                new Plate("NeonRings",  -1, -5.6f,  4.5f, true,  1.30f),
                new Plate("NeonOpen",   -1,  1.5f,  4.6f, true,  1.55f),
                new Plate("NeonNerve",   1,  2.4f,  5.6f, false, 1.80f),
                new Plate("NeonHotel",   1,  3.8f, 10.2f, false, 2.30f),
                new Plate("NeonBar",     1,  6.2f,  4.1f, true,  1.35f),
                new Plate("NeonSleep",  -1,  6.8f,  9.4f, false, 2.05f),
                new Plate("NeonArrow",  -1,  8.6f,  4.3f, true,  1.25f),
                new Plate("NeonEye",     1,  9.8f,  6.4f, false, 1.75f),
                new Plate("NeonClub",   -1, 11.4f,  7.2f, false, 1.70f),
                new Plate("NeonCross",  -1, 12.6f,  4.2f, true,  1.30f),
                new Plate("NeonRings",   1, 13.4f,  4.6f, true,  1.30f),
                new Plate("NeonNoodle",  1, 15.2f,  8.6f, false, 2.15f),
                new Plate("NeonHotel",  -1, 16.6f,  4.4f, true,  1.35f),
                new Plate("NeonLive",   -1, 18.2f,  7.4f, false, 1.70f),
                new Plate("NeonBar",     1, 19.0f,  4.2f, true,  1.35f),
                new Plate("NeonOpen",    1, 21.6f,  6.8f, false, 1.70f),
                new Plate("NeonArrow",  -1, 22.4f,  4.3f, true,  1.25f),
                new Plate("NeonNerve",  -1, 24.0f, 10.0f, false, 2.25f),
                new Plate("NeonCross",   1, 25.2f,  4.1f, true,  1.30f),
                new Plate("NeonRings",  -1, 27.0f,  4.5f, true,  1.30f),
                new Plate("NeonSleep",   1, 28.2f,  7.8f, false, 1.70f),
                new Plate("NeonEye",    -1, 29.6f,  9.2f, false, 2.10f),
                new Plate("NeonBar",     1, 30.4f,  4.4f, true,  1.35f),
                new Plate("NeonClub",    1, 33.0f,  6.2f, false, 1.65f),
                new Plate("NeonArrow",   1, 34.6f,  4.2f, true,  1.25f),
                new Plate("NeonNoodle", -1, 37.2f,  8.2f, false, 1.75f),
                new Plate("NeonHotel",   1, 37.8f,  4.5f, true,  1.35f),
                new Plate("NeonCross",  -1, 39.4f,  4.3f, true,  1.30f),
                new Plate("NeonLive",    1, 40.6f,  8.8f, false, 2.00f),
            };
            for (var i = 0; i < plates.Count; i++) Sign(parent, "Sign" + i, plates[i]);
            Tubes(Child(parent, "Tubes"));
        }

        /// <summary>
        /// 面に這わせる管。看板のあいだを繋いで、壁そのものを光らせる。
        /// 「熱帯植物のように絡みついている」のはこちらの仕事で、看板だけでは足りない
        /// </summary>
        static void Tubes(Transform parent)
        {
            Clear(parent);
            var tint = new Color[]
            {
                new Color(1.00f, 0.16f, 0.67f), new Color(0.16f, 0.92f, 1.00f),
                new Color(1.00f, 0.59f, 0.12f), new Color(0.27f, 1.00f, 0.47f),
                new Color(0.67f, 0.43f, 1.00f), new Color(1.00f, 0.86f, 0.24f),
            };
            var rng = new System.Random(20260919);
            var n = 0;
            for (var s2 = 0; s2 < 2; s2++)
            {
                var side = s2 == 0 ? -1 : 1;
                var x = side * (StreetHalf - 0.06f);
                var z = StreetSouth + 1f;
                while (z < StreetNorth - 2f)
                {
                    var span = (float)(3.0 + rng.NextDouble() * 5.0);
                    // 小道の口には掛けない。入口が配管で塞がって見える
                    if (side < 0 && z + span > LaneZ - 3.6f && z < LaneZ + 3.6f)
                    {
                        z = LaneZ + 3.6f;
                        continue;
                    }
                    var y = (float)(2.2 + rng.NextDouble() * 8.0);
                    var col = tint[rng.Next(tint.Length)];
                    // 横に長く這う管
                    Strip(parent, "Tube" + n++, new Vector3(x, y, z + span * 0.5f),
                        new Vector3(0.06f, 0.09f, span), col);
                    // ときどき縦へ折れる
                    if (rng.NextDouble() < 0.45)
                    {
                        var drop = (float)(1.5 + rng.NextDouble() * 3.5);
                        Strip(parent, "Tube" + n++, new Vector3(x, y - drop * 0.5f, z + span),
                            new Vector3(0.06f, drop, 0.09f), col);
                    }
                    z += span + (float)(0.8 + rng.NextDouble() * 2.5);
                }
            }
        }

        /// <summary>光る帯をひとつ。細い箱に自発光のマテリアルを貼るだけ</summary>
        static void Strip(Transform parent, string name, Vector3 centre, Vector3 size, Color col)
        {
            var go = Box(parent, name, centre, size, "Metal");
            go.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(col, 1.9f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>色だけの自発光マテリアル。同じ色は使い回す</summary>
        static Material GlowMat(Color col, float glow)
        {
            var key = string.Format("Glow_{0:000}_{1:000}_{2:000}_{3:00}",
                (int)(col.r * 255), (int)(col.g * 255), (int)(col.b * 255), (int)(glow * 10));
            var path = Materials + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                AssetDatabase.CreateFolder("Assets/Materials", "Alley");
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.name = key;
            m.SetColor("_BaseColor", new Color(col.r * glow, col.g * glow, col.b * glow, 1f));
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>看板を 1 枚立てる。突き出す物には壁までの腕と灯りを足す</summary>
        static void Sign(Transform parent, string name, Plate p)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + p.texture + ".png");
            if (tex == null) { Debug.LogWarning("テクスチャが無い: " + p.texture); return; }
            var wide = Tall(p.texture) ? p.scale : p.scale * 2f;
            var high = Tall(p.texture) ? p.scale * 2f : p.scale;
            var wallX = p.side * StreetHalf;
            var reach = p.blade ? 0.95f : 0.12f;
            var x = wallX - p.side * reach;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, p.y, p.z);
            // 突き出す物は通りの上下を向き、貼る物は通りの中央を向く
            // 絵が乗るのは板の裏側。壁に貼る物は向きを返さないと字が反転する
            go.transform.localRotation = p.blade
                ? Quaternion.identity
                : Quaternion.Euler(0f, p.side > 0 ? 90f : -90f, 0f);
            go.transform.localScale = new Vector3(wide, high, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = NeonMat(p.texture, tex);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            if (p.blade)
            {
                // 突き出した看板は両面から読まれる。裏にもう 1 枚、左右を返して貼る
                var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
                back.name = name + ".Back";
                back.transform.SetParent(parent, false);
                back.transform.localPosition = new Vector3(x, p.y, p.z - 0.03f);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                back.transform.localScale = new Vector3(-wide, high, 1f);
                back.GetComponent<MeshRenderer>().sharedMaterial = NeonMat(p.texture, tex);
                Object.DestroyImmediate(back.GetComponent<Collider>());
            }

            if (!p.blade) return;
            // 壁まで繋ぐ腕
            Box(parent, name + ".Arm", new Vector3(wallX - p.side * reach * 0.5f, p.y + high * 0.5f - 0.1f, p.z),
                new Vector3(reach, 0.08f, 0.08f), "Metal");
            // 通りへ落ちる色。突き出した物にだけ付ける
            var lamp = new GameObject(name + ".Lamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(x - p.side * 0.5f, p.y, p.z);
            var l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = NeonTint(p.texture);
            l.range = 12f;
            l.intensity = 18f;
            l.shadows = LightShadows.None;
        }

        /// <summary>
        /// 自分で光る看板のマテリアル。足し算で重ねるので、滲みが背景へそのまま乗る。
        /// 裏からも見えるように面の切り落としは切ってある
        /// </summary>
        static Material NeonMat(string texture, Texture2D tex)
        {
            var path = Materials + "Neon_" + texture + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Neon_" + texture;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.7f, 1.7f, 1.7f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }


        // ---- 蚤の市 --------------------------------------------------------

        /// <summary>歩ける筋の半幅。ここだけは何も置かない</summary>
        public const float AisleHalf = 1.5f;
        /// <summary>自分の露店の場所。いちばん奥の、入って右手</summary>
        public const float MyStallX = -28.8f;
        public const float MyStallZ = 41.0f;
        /// <summary>入口の方を向く角度</summary>
        public const float MyStallYaw = -65f;

        /// <summary>
        /// ヤードの中身。両脇に出店を詰め、真ん中に人ひとりぶんの筋だけ残す。
        /// 足の置き場もない、という文に合わせて隙間は詰める
        /// </summary>
        static void Market(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(6100);
            var n = 0;
            // 出店どうしもぶつからないよう、ごみと同じ台帳を使う
            Taken.Clear();
            for (var s2 = 0; s2 < 2; s2++)
            {
                var sideZ = s2 == 0 ? -1 : 1;
                for (var row = 0; row < 2; row++)
                {
                    var z = LaneZ + sideZ * (AisleHalf + 1.35f + row * 3.1f);
                    for (var x = LaneWest - 4.0f; x > YardWest + 2.0f; x -= (float)(2.6 + rng.NextDouble() * 1.5))
                    {
                        // 整然と並べない。露店は思い思いの場所に出ている
                        var px = x + (float)(rng.NextDouble() - 0.5) * 0.9f;
                        var pz = z + (float)(rng.NextDouble() - 0.5) * 1.3f;
                        var gap = new Vector2(px - MyStallX, pz - MyStallZ);
                        if (gap.magnitude < 3.2f) continue;
                        if (Mathf.Abs(pz - LaneZ) < AisleHalf + 0.9f) continue;
                        // 露天席の区画には出さない。店と席がぶつかる
                        if (px < TerraceEast + 1.2f && pz < TerraceNorth + 1.2f) continue;
                        if (pz < TavernTerraceNorth + 1.2f) continue;
                        if (px > RestaurantWest - 1.2f && pz > RestaurantTerraceSouth - 1.2f) continue;
                        if (!Free(px, pz, 1.4f)) continue;
                        // 正面は筋の側。北の列は南を向き、南の列は北を向く
                        var face = (sideZ > 0 ? 0f : 180f) + (float)(rng.NextDouble() * 40.0 - 20.0);
                        Stall(parent, "Stall" + n++, new Vector3(px, 0f, pz), face, rng, false);
                    }
                }
            }
            MyStall(Child(parent, "MyStall"));
            Bulbs(Child(parent, "Bulbs"));
            Goods(Child(parent, "Goods"));
        }

        /// <summary>
        /// 出店ひとつ。4 本の柱にタープを張り、テーブルと木箱を置く。
        /// yaw は売り手が向く向きで、テーブルは筋の側へ出る
        /// </summary>
        static void Stall(Transform parent, string name, Vector3 at, float yaw, System.Random rng, bool mine)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = g.transform;
            Occupy(at.x, at.z, mine ? 1.9f : 1.55f);
            var w = mine ? 2.6f : (float)(1.5 + rng.NextDouble() * 1.5);
            var d = mine ? 2.2f : (float)(1.3 + rng.NextDouble() * 1.1);
            var high = mine ? 2.40f : (float)(2.32 + rng.NextDouble() * 0.26);

            for (var i = 0; i < 4; i++)
            {
                var px = (i % 2 == 0 ? -1 : 1) * w * 0.5f;
                var pz = (i < 2 ? -1 : 1) * d * 0.5f;
                Box(t, "Pole" + i, new Vector3(px, high * 0.5f, pz), new Vector3(0.06f, high, 0.06f), "Pole");
            }

            var tarp = mine ? "TarpMine" : TarpName(rng.Next(5));
            if (mine)
            {
                // 自分のぶんは穴が開いている。3 枚に割って、真ん中を空ける
                Sheet(t, "Tarp.N", new Vector3(0f, high, -d * 0.28f), new Vector2(w + 0.3f, d * 0.40f), tarp);
                Sheet(t, "Tarp.S", new Vector3(0f, high, d * 0.30f), new Vector2(w + 0.3f, d * 0.36f), tarp);
                Sheet(t, "Tarp.W", new Vector3(-w * 0.34f, high, 0f), new Vector2(w * 0.30f, d * 0.30f), tarp);
            }
            else
            {
                Sheet(t, "Tarp", new Vector3(0f, high, 0f), new Vector2(w + 0.3f, d + 0.3f), tarp);
            }

            var th = 0.78f;
            Box(t, "Table", new Vector3(0f, th, -d * 0.18f), new Vector3(w * 0.88f, 0.06f, d * 0.52f), "Timber");
            // 前垂れはタープと同じ布。板を張ると白い衝立に見える
            Box(t, "Skirt", new Vector3(0f, th * 0.5f, -d * 0.18f - d * 0.24f), new Vector3(w * 0.88f, th, 0.05f), tarp);

            var boxes = mine ? 2 : 1 + rng.Next(3);
            for (var i = 0; i < boxes; i++)
            {
                var bw = (float)(0.35 + rng.NextDouble() * 0.2);
                // 売り手は台の真後ろに座る。木箱はその左右へ寄せる
                var side2 = rng.NextDouble() < 0.5 ? -1f : 1f;
                Box(t, "Crate" + i, new Vector3(side2 * (w * 0.30f + 0.14f), bw * 0.5f,
                        d * 0.30f + (float)(rng.NextDouble() - 0.5) * 0.3f),
                    new Vector3(bw, bw, bw), "Crate");
            }
        }

        /// <summary>タープの色。市が並ぶと、この色の違いが一番効く</summary>
        static string TarpName(int i)
        {
            switch (i)
            {
                case 0: return "TarpRed";
                case 1: return "TarpGreen";
                case 2: return "TarpBlue";
                case 3: return "TarpOchre";
                default: return "Tarp";
            }
        }

        /// <summary>タープの一枚。水平に張った薄い板</summary>
        static void Sheet(Transform parent, string name, Vector3 centre, Vector2 size, string material)
        {
            Box(parent, name, centre, new Vector3(size.x, 0.04f, size.y), material);
        }

        /// <summary>
        /// 自分の露店。穴の開いたタープの下に、テーブルと椅子と看板がひとつ。
        /// いちばん奥に、入口を向いて構える
        /// </summary>
        static void MyStall(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(77);
            Stall(parent, "Stall", new Vector3(MyStallX, 0f, MyStallZ), MyStallYaw, rng, true);
            var t = parent.Find("Stall");
            Box(t, "Chair.Seat", new Vector3(0f, 0.44f, 0.72f), new Vector3(0.44f, 0.06f, 0.44f), "Timber");
            Box(t, "Chair.Back", new Vector3(0f, 0.70f, 0.94f), new Vector3(0.44f, 0.52f, 0.05f), "Timber");
            for (var i = 0; i < 4; i++)
            {
                var px = (i % 2 == 0 ? -1 : 1) * 0.18f;
                var pz = 0.72f + (i < 2 ? -0.18f : 0.18f);
                Box(t, "Chair.Leg" + i, new Vector3(px, 0.22f, pz), new Vector3(0.04f, 0.44f, 0.04f), "Pole");
            }
        }

        /// <summary>ヤードの裸電球。出店ごとには置かず、まばらに吊る</summary>
        static void Bulbs(Transform parent)
        {
            Clear(parent);
            var at = new Vector3[]
            {
                new Vector3(LaneWest - 3.2f, 3.1f, LaneZ + 0.4f),
                new Vector3(LaneWest - 8.0f, 3.3f, LaneZ - 2.6f),
                new Vector3(LaneWest - 8.6f, 3.2f, LaneZ + 3.0f),
                new Vector3(MyStallX + 2.6f, 3.1f, LaneZ - 0.6f),
                new Vector3(MyStallX + 0.3f, 2.6f, MyStallZ - 0.4f),
                new Vector3(MyStallX - 1.4f, 3.0f, MyStallZ + 0.8f),
                new Vector3(LaneWest - 5.4f, 3.2f, LaneZ + 0.8f),
                new Vector3(LaneWest - 11.2f, 3.3f, LaneZ - 1.4f),
                new Vector3(LaneWest - 12.8f, 3.1f, LaneZ + 2.2f),
                new Vector3(MyStallX + 5.0f, 3.2f, LaneZ + 0.4f),
                new Vector3(MyStallX + 2.0f, 3.1f, LaneZ + 4.2f),
            };
            for (var i = 0; i < at.Length; i++)
            {
                var g = new GameObject("Bulb" + i);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = at[i];
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Glass";
                ball.transform.SetParent(g.transform, false);
                ball.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
                // 3 つに 1 つは新しい白い玉。橙ばかりだと市が黄色一色になる
                var cold = i % 3 == 1;
                var hue = cold ? new Color(0.76f, 0.86f, 1.00f) : new Color(1.00f, 0.87f, 0.68f);
                ball.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(hue, 2.2f);
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = hue;
                l.range = 8.5f;
                l.intensity = 17f;
                l.shadows = LightShadows.None;
            }
        }


        /// <summary>
        /// ヤードを囲む建物の裏の顔。通りに面した表と違って店先は無く、
        /// 窓と樋と物干しと外づけの階段が雑然と並ぶ。中庭はこの壁で出来ている
        /// </summary>
        static void YardFacades(Bank brick, Bank stone, Bank metal, Bank glass, Glazing glow)
        {
            var rng = new System.Random(6210);
            // 西の壁。奥に立ちはだかる
            YardWallX(brick, stone, metal, glass, glow, YardWest, 1, YardSouth, YardNorth, rng);
            // 東の壁は小路の口ぶんだけ切れている
            YardWallX(brick, stone, metal, glass, glow, LaneWest, -1, YardSouth, LaneZ - LaneHalf, rng);
            YardWallX(brick, stone, metal, glass, glow, LaneWest, -1, LaneZ + LaneHalf, YardNorth, rng);
            // 南と北
            YardWallZ(brick, stone, metal, glass, glow, YardSouth, 1, YardWest, LaneWest, rng);
            YardWallZ(brick, stone, metal, glass, glow, YardNorth, -1, YardWest, LaneWest, rng);
        }

        /// <summary>x が一定の囲い壁。窓を彫り、樋と物干しを掛ける</summary>
        static void YardWallX(Bank brick, Bank stone, Bank metal, Bank glass, Glazing glow,
            float wx, int inward, float z0, float z1, System.Random rng)
        {
            var holes = new List<Vector4>();
            var floors = 5;
            for (var f = 0; f < floors; f++)
            {
                var sill = 3.0f + f * 2.75f;
                for (var z = z0 + 1.4f; z < z1 - 1.0f; z += 2.15f)
                {
                    if (rng.NextDouble() < 0.18) continue;
                    var w = (float)(0.85 + rng.NextDouble() * 0.30);
                    var h = (float)(1.25 + rng.NextDouble() * 0.25);
                    holes.Add(new Vector4(z, z + w, sill, sill + h));
                }
            }
            brick.FaceXHoles(wx, z0, z1, 0f, WallHeight, inward, holes);
            var back = wx - inward * 0.18f;
            foreach (var o in holes)
            {
                Reveal(stone, wx, back, o);
                glow.Pick(rng, 0.34).FaceX(back, o.x, o.y, o.z, o.w, inward);
                stone.Box(new Vector3(wx + inward * 0.07f, o.z - 0.08f, (o.x + o.y) * 0.5f),
                    new Vector3(0.16f, 0.10f, o.y - o.x + 0.26f));
                // 物干し。窓から窓へ渡した紐と布
                if (rng.NextDouble() < 0.34)
                {
                    var len = (float)(1.4 + rng.NextDouble() * 1.4);
                    metal.Box(new Vector3(wx + inward * 0.55f, o.w + 0.05f, (o.x + o.y) * 0.5f + len * 0.5f),
                        new Vector3(0.05f, 0.05f, len));
                }
            }
            // 樋と外づけの階段
            for (var z = z0 + 2.5f; z < z1 - 2f; z += (float)(4.5 + rng.NextDouble() * 3.5))
            {
                if (rng.NextDouble() < 0.5) Downpipe(metal, wx, z, inward, (float)(10.0 + rng.NextDouble() * 4.0));
                else Aircon(metal, wx, z, inward, (float)(3.6 + rng.NextDouble() * 5.0));
            }
            // 胴蛇腹
            for (var f = 1; f < 5; f++)
                stone.Box(new Vector3(wx + inward * 0.11f, 2.6f + f * 2.75f, (z0 + z1) * 0.5f),
                    new Vector3(0.22f, 0.13f, z1 - z0));
            stone.Box(new Vector3(wx + inward * 0.20f, WallHeight - 0.5f, (z0 + z1) * 0.5f),
                new Vector3(0.40f, 0.34f, z1 - z0));
        }

        /// <summary>z が一定の囲い壁</summary>
        static void YardWallZ(Bank brick, Bank stone, Bank metal, Bank glass, Glazing glow,
            float wz, int inward, float x0, float x1, System.Random rng)
        {
            var holes = new List<Vector4>();
            for (var f = 0; f < 5; f++)
            {
                var sill = 3.2f + f * 2.75f;
                for (var x = x0 + 1.4f; x < x1 - 1.0f; x += 2.25f)
                {
                    if (rng.NextDouble() < 0.20) continue;
                    var w = (float)(0.85 + rng.NextDouble() * 0.30);
                    var h = (float)(1.25 + rng.NextDouble() * 0.25);
                    holes.Add(new Vector4(x, x + w, sill, sill + h));
                }
            }
            brick.FaceZHoles(wz, x0, x1, 0f, WallHeight, inward, holes);
            var back = wz - inward * 0.18f;
            foreach (var o in holes)
            {
                var y0 = Mathf.Min(wz, back);
                var y1 = Mathf.Max(wz, back);
                stone.FaceX(o.x, y0, y1, o.z, o.w, -1);
                stone.FaceX(o.y, y0, y1, o.z, o.w, 1);
                stone.FaceY(o.w, o.x, o.y, y0, y1, -1);
                stone.FaceY(o.z, o.x, o.y, y0, y1, 1);
                glow.Pick(rng, 0.34).FaceZ(back, o.x, o.y, o.z, o.w, inward);
                stone.Box(new Vector3((o.x + o.y) * 0.5f, o.z - 0.08f, wz + inward * 0.07f),
                    new Vector3(o.y - o.x + 0.26f, 0.10f, 0.16f));
            }
            for (var x = x0 + 2.5f; x < x1 - 2f; x += (float)(4.0 + rng.NextDouble() * 3.0))
            {
                var px = x;
                var py = (float)(3.6 + rng.NextDouble() * 5.0);
                metal.Box(new Vector3(px, py * 0.5f, wz + inward * 0.16f), new Vector3(0.15f, py, 0.15f));
                if (rng.NextDouble() < 0.4)
                    metal.Box(new Vector3(px + 0.9f, py, wz + inward * 0.42f), new Vector3(0.7f, 0.58f, 0.8f));
            }
            for (var f = 1; f < 5; f++)
                stone.Box(new Vector3((x0 + x1) * 0.5f, 2.8f + f * 2.75f, wz + inward * 0.11f),
                    new Vector3(x1 - x0, 0.13f, 0.22f));
            stone.Box(new Vector3((x0 + x1) * 0.5f, WallHeight - 0.5f, wz + inward * 0.20f),
                new Vector3(x1 - x0, 0.34f, 0.40f));
        }

        /// <summary>
        /// 出店に並ぶ品。瓶・椀・木箱・巻いた布・積んだ板・吊るした束を作り分ける。
        /// 何を売っているかは読ませないが、形が違えば市らしくなる
        /// </summary>
        static void Goods(Transform parent)
        {
            Clear(parent);
            var banks = new Bank[GoodsTint.Length];
            for (var i = 0; i < banks.Length; i++) banks[i] = new Bank { Texel = 6f };
            var rng = new System.Random(8899);
            var market = GameObject.Find("Alley/Market");
            if (market == null) return;
            foreach (Transform stall in market.transform)
            {
                // 自分の露店には市の品を置かない。ここに並ぶのはメモリーチップだけで、
                // 瓶や鉢を積むとチップが埋もれて、置いたことが分からなくなる
                if (stall.name == "MyStall") continue;
                Transform body = stall.name.StartsWith("Stall") ? stall : stall.Find("Stall");
                if (body == null) continue;
                var rot = Quaternion.Euler(0f, body.localEulerAngles.y, 0f);
                var at = body.localPosition;
                var top = 0.81f;
                var n = 6 + rng.Next(8);
                for (var i = 0; i < n; i++)
                {
                    var b = banks[rng.Next(banks.Length)];
                    var local = new Vector3((float)(rng.NextDouble() - 0.5) * 1.5f, 0f,
                        (float)(rng.NextDouble() - 0.5) * 0.5f - 0.32f);
                    var p = at + rot * local;
                    var spin = rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    var kind = rng.Next(6);
                    if (kind == 0) Bottle(b, p, top, spin, rng);
                    else if (kind == 1) Bowl(b, p, top, spin, rng);
                    else if (kind == 2) Crate(b, p, top, spin, rng);
                    else if (kind == 3) Roll(b, p, top, spin, rng);
                    else if (kind == 4) Pile(b, p, top, spin, rng);
                    else Jar(b, p, top, spin, rng);
                }
                // 天井から吊るした束。市は上にも物を下げる
                var hang = rng.Next(4);
                for (var i = 0; i < hang; i++)
                {
                    var b = banks[rng.Next(banks.Length)];
                    var len = (float)(0.28 + rng.NextDouble() * 0.45);
                    var p = at + rot * new Vector3((float)(rng.NextDouble() - 0.5) * 1.8f, 0f, -0.95f);
                    b.Box(p + new Vector3(0f, 2.0f - len * 0.5f, 0f), new Vector3(0.11f, len, 0.11f),
                        rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 60.0), 0f));
                    b.Box(p + new Vector3(0f, 2.0f - len, 0f), new Vector3(0.16f, 0.09f, 0.16f), rot);
                }
            }
            for (var i = 0; i < banks.Length; i++)
                banks[i].Emit(parent, "Goods" + i, Tinted("Goods" + i, GoodsTint[i], GoodsSkin[i]), false, Generated);
        }

        /// <summary>瓶。胴と肩と首</summary>
        static void Bottle(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var h = (float)(0.16 + rng.NextDouble() * 0.10);
            var w = (float)(0.055 + rng.NextDouble() * 0.025);
            b.Box(p + new Vector3(0f, top + h * 0.45f, 0f), new Vector3(w, h * 0.9f, w), rot);
            b.Box(p + new Vector3(0f, top + h * 0.95f, 0f), new Vector3(w * 0.6f, h * 0.22f, w * 0.6f), rot);
            b.Box(p + new Vector3(0f, top + h * 1.12f, 0f), new Vector3(w * 0.32f, h * 0.30f, w * 0.32f), rot);
        }

        /// <summary>椀。浅い器に縁を付ける</summary>
        static void Bowl(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var r = (float)(0.10 + rng.NextDouble() * 0.07);
            b.Box(p + new Vector3(0f, top + 0.025f, 0f), new Vector3(r * 1.5f, 0.05f, r * 1.5f), rot);
            b.Box(p + new Vector3(0f, top + 0.065f, 0f), new Vector3(r * 1.9f, 0.035f, r * 1.9f), rot);
            if (rng.NextDouble() < 0.5)
                b.Box(p + new Vector3(0f, top + 0.105f, 0f), new Vector3(r * 1.2f, 0.05f, r * 1.2f), rot);
        }

        /// <summary>木箱。蓋を少しずらして載せる</summary>
        static void Crate(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var w = (float)(0.16 + rng.NextDouble() * 0.12);
            var h = (float)(0.10 + rng.NextDouble() * 0.08);
            b.Box(p + new Vector3(0f, top + h * 0.5f, 0f), new Vector3(w, h, w * 0.8f), rot);
            b.Box(p + new Vector3(0f, top + h + 0.015f, 0f), new Vector3(w * 1.06f, 0.03f, w * 0.86f),
                rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 16.0 - 8.0), 0f));
        }

        /// <summary>巻いた布。横に寝かせて積む</summary>
        static void Roll(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var len = (float)(0.22 + rng.NextDouble() * 0.16);
            var thick = (float)(0.055 + rng.NextDouble() * 0.03);
            var rows = 1 + rng.Next(3);
            for (var i = 0; i < rows; i++)
                b.Box(p + new Vector3(0f, top + thick * (0.5f + i * 0.92f), 0f),
                    new Vector3(len, thick, thick), rot * Quaternion.Euler(0f, i * 7f, 0f));
        }

        /// <summary>積んだ板。薄い物を重ねる</summary>
        static void Pile(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var w = (float)(0.13 + rng.NextDouble() * 0.09);
            var rows = 2 + rng.Next(5);
            for (var i = 0; i < rows; i++)
                b.Box(p + new Vector3(0f, top + 0.014f + i * 0.026f, 0f), new Vector3(w, 0.022f, w * 0.75f),
                    rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 24.0 - 12.0), 0f));
        }

        /// <summary>壺。胴が張って口がすぼまる</summary>
        static void Jar(Bank b, Vector3 p, float top, Quaternion rot, System.Random rng)
        {
            var r = (float)(0.07 + rng.NextDouble() * 0.05);
            b.Box(p + new Vector3(0f, top + r * 0.55f, 0f), new Vector3(r * 1.7f, r * 1.1f, r * 1.7f), rot);
            b.Box(p + new Vector3(0f, top + r * 1.20f, 0f), new Vector3(r * 1.1f, r * 0.35f, r * 1.1f), rot);
            b.Box(p + new Vector3(0f, top + r * 1.45f, 0f), new Vector3(r * 1.3f, r * 0.18f, r * 1.3f), rot);
        }

        /// <summary>品の色。極彩色の通りに合わせて、市も色で埋める</summary>
        /// <summary>品ごとの地。素材の名。空なら色だけ</summary>
        static readonly string[] GoodsSkin =
        {
            "GoodCloth", "GoodCeramic", "GoodPaper", "AlleyTimber", "GoodCloth",
            "GoodCeramic", "AlleyTimber", "GoodPaper", "GoodCeramic", "GoodCloth",
        };

        static readonly Color[] GoodsTint =
        {
            // 地に色を乗せるので、彩度は低く。染めた布や釉の色くらいに留める
            new Color(0.52f, 0.30f, 0.26f), new Color(0.34f, 0.44f, 0.50f),
            new Color(0.62f, 0.56f, 0.44f), new Color(0.34f, 0.42f, 0.33f),
            new Color(0.42f, 0.32f, 0.44f), new Color(0.60f, 0.52f, 0.42f),
            new Color(0.36f, 0.46f, 0.46f), new Color(0.48f, 0.36f, 0.24f),
            new Color(0.66f, 0.60f, 0.50f), new Color(0.38f, 0.36f, 0.40f),
        };

        /// <summary>地を貼って色を乗せた艶消しのマテリアル。skin が空なら色だけ</summary>
        static Material Tinted(string name, Color col, string skin = null)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            var tex = string.IsNullOrEmpty(skin)
                ? null : AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + skin + ".png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }


        // ---- ヤードに残る店の名残 --------------------------------------------

        /// <summary>
        /// ブリーディング・ハート・ヤードは実在する。16 世紀からの敷石の中庭で、
        /// 1983 年からは仏料理の店が三軒（ビストロ・タヴァーン・レストラン）入っていた。
        /// ビストロは元は倉庫で、剥き出しの木の梁と鉄の柱、19 世紀の仏ワインの刷り物が貼ってあった。
        /// 2166 年のここには、その名残だけが板で塞がれて残っている
        /// </summary>
        static void Heritage(Transform parent, Bank brick, Bank stone, Bank metal, Bank timber, Bank glass, Glazing glow)
        {
            // --- 西側。ビストロ。今も開いている。2 階は中庭を見下ろす露台 ---
            var wx = YardWest;
            var tz0 = LaneZ - 6.5f;
            var tz1 = LaneZ + 1.5f;
            var ty = 3.9f;
            stone.Box(new Vector3(wx + 1.15f, ty, (tz0 + tz1) * 0.5f), new Vector3(2.3f, 0.22f, tz1 - tz0));
            for (var z = tz0 + 0.8f; z < tz1; z += 2.4f)
            {
                metal.Box(new Vector3(wx + 2.15f, ty * 0.5f, z), new Vector3(0.16f, ty, 0.16f));
                metal.Box(new Vector3(wx + 1.2f, ty - 0.35f, z), new Vector3(2.0f, 0.10f, 0.10f));
            }
            metal.Box(new Vector3(wx + 2.25f, ty + 0.52f, (tz0 + tz1) * 0.5f), new Vector3(0.07f, 0.07f, tz1 - tz0));
            metal.Box(new Vector3(wx + 2.25f, ty + 0.96f, (tz0 + tz1) * 0.5f), new Vector3(0.09f, 0.08f, tz1 - tz0));
            for (var z = tz0 + 0.22f; z < tz1; z += 0.28f)
                metal.Box(new Vector3(wx + 2.25f, ty + 0.62f, z), new Vector3(0.045f, 0.9f, 0.045f));
            // 露台の卓と椅子。客が出ている想定
            for (var z = tz0 + 1.3f; z < tz1 - 0.6f; z += 2.3f)
            {
                timber.Box(new Vector3(wx + 1.25f, ty + 0.75f, z), new Vector3(0.9f, 0.07f, 0.9f));
                metal.Box(new Vector3(wx + 1.25f, ty + 0.38f, z), new Vector3(0.09f, 0.72f, 0.09f));
                for (var i = -1; i <= 1; i += 2)
                {
                    timber.Box(new Vector3(wx + 1.25f + i * 0.72f, ty + 0.44f, z), new Vector3(0.42f, 0.05f, 0.42f));
                    timber.Box(new Vector3(wx + 1.25f + i * 0.92f, ty + 0.68f, z), new Vector3(0.05f, 0.5f, 0.42f));
                }
            }
            // 1 階。大きな硝子と、中の灯り
            var gz0 = tz0 + 0.5f;
            var gz1 = tz1 - 0.5f;
            var inside = new System.Random(4141);
            var bistro = glow.Lit(10);         // ビストロの中はタングステン。面が広いので暗め
            bistro.FaceX(wx + 0.30f, gz0, gz1, 0.55f, 2.85f, 1);
            Behind(glass, 0, wx + 0.30f, 1, gz0 + 0.25f, gz1 - 0.25f, 0.55f, 2.85f, inside);
            // 桟。縦だけだと硝子が一枚板に見えるので、横も入れる
            for (var y = 1.35f; y < 2.8f; y += 0.72f)
                metal.Box(new Vector3(wx + 0.26f, y, (gz0 + gz1) * 0.5f), new Vector3(0.11f, 0.07f, gz1 - gz0));
            for (var z = gz0 + 1.15f; z < gz1 - 0.2f; z += 1.15f)
                metal.Box(new Vector3(wx + 0.26f, 1.7f, z), new Vector3(0.13f, 2.3f, 0.09f));
            metal.Box(new Vector3(wx + 0.26f, 0.52f, (gz0 + gz1) * 0.5f), new Vector3(0.18f, 0.16f, gz1 - gz0));
            metal.Box(new Vector3(wx + 0.26f, 2.92f, (gz0 + gz1) * 0.5f), new Vector3(0.18f, 0.16f, gz1 - gz0));
            // 日除け。中庭側へ張り出す
            var lip = 1.25f;
            timber.Box(new Vector3(wx + lip * 0.5f, 3.30f, (gz0 + gz1) * 0.5f),
                new Vector3(lip, 0.10f, gz1 - gz0 + 0.5f), Quaternion.Euler(0f, 0f, -7f));
            for (var z = gz0; z < gz1 + 0.3f; z += 1.6f)
                metal.Box(new Vector3(wx + lip * 0.5f, 3.05f, z), new Vector3(lip, 0.07f, 0.07f));
            // 扉と、その上の灯り
            timber.Box(new Vector3(wx + 0.16f, 1.10f, tz1 + 1.35f), new Vector3(0.20f, 2.2f, 1.1f));
            metal.Box(new Vector3(wx + 0.30f, 1.05f, tz1 + 1.05f), new Vector3(0.08f, 0.3f, 0.06f));
            bistro.FaceX(wx + 0.30f, tz1 + 2.1f, tz1 + 2.9f, 0.9f, 2.4f, 1);
            Behind(glass, 0, wx + 0.30f, 1, tz1 + 2.2f, tz1 + 2.8f, 0.9f, 2.4f, inside);
            // 品書きの黒板。扉の脇に立てかける
            timber.Box(new Vector3(wx + 0.72f, 0.62f, tz1 + 2.25f), new Vector3(0.09f, 1.2f, 0.7f),
                Quaternion.Euler(0f, 0f, 9f));
            // 地下蔵への両開き
            metal.Box(new Vector3(wx + 1.35f, 0.06f, tz0 - 1.8f), new Vector3(2.1f, 0.10f, 1.5f),
                Quaternion.Euler(0f, 8f, 0f));
            metal.Box(new Vector3(wx + 1.35f, 0.13f, tz0 - 1.8f), new Vector3(0.10f, 0.06f, 1.5f),
                Quaternion.Euler(0f, 8f, 0f));

            // --- 南側。タヴァーン。迫持ちの口に硝子を入れ、灯りを点ける ---
            var sz = YardSouth;
            for (var x = YardWest + 3.2f; x < LaneWest - 3f; x += 4.3f)
            {
                stone.Box(new Vector3(x, 1.4f, sz + 0.24f), new Vector3(2.0f, 0.26f, 0.48f));
                stone.Box(new Vector3(x, 2.85f, sz + 0.26f), new Vector3(2.3f, 0.30f, 0.52f));
                for (var i = -1; i <= 1; i += 2)
                    stone.Box(new Vector3(x + i * 1.05f, 1.45f, sz + 0.24f), new Vector3(0.28f, 2.9f, 0.48f));
                glow.Lit(12).FaceZ(sz + 0.40f, x - 0.85f, x + 0.85f, 0.55f, 2.7f, 1);
                Behind(glass, 2, sz + 0.40f, 1, x - 0.78f, x + 0.78f, 0.55f, 2.7f, inside);
                for (var i = -1; i <= 1; i += 2)
                    metal.Box(new Vector3(x + i * 0.42f, 1.62f, sz + 0.36f), new Vector3(0.09f, 2.15f, 0.13f));
                metal.Box(new Vector3(x, 1.62f, sz + 0.36f), new Vector3(1.7f, 0.09f, 0.13f));
            }
            // 入口。庇と、外に出した樽と卓
            var dx = YardWest + 7.4f;
            timber.Box(new Vector3(dx, 1.10f, sz + 0.30f), new Vector3(1.2f, 2.2f, 0.22f));
            timber.Box(new Vector3(dx, 2.55f, sz + 0.85f), new Vector3(2.2f, 0.10f, 1.3f),
                Quaternion.Euler(6f, 0f, 0f));
            for (var i = -1; i <= 1; i += 2)
                metal.Box(new Vector3(dx + i * 0.95f, 1.85f, sz + 1.4f), new Vector3(0.07f, 1.4f, 0.07f));
            for (var i = 0; i < 3; i++)
            {
                var bx = dx + 2.1f + i * 1.15f;
                timber.Box(new Vector3(bx, 0.42f, sz + 1.25f), new Vector3(0.62f, 0.84f, 0.62f));
                timber.Box(new Vector3(bx, 0.86f, sz + 1.25f), new Vector3(0.70f, 0.06f, 0.70f));
            }

            // --- 北側。西半分は元倉庫のまま。荷揚げの梁と滑車、鎧戸 ---
            var nz = YardNorth;
            for (var x = YardWest + 4.0f; x < RestaurantWest - 1f; x += 5.4f)
            {
                metal.Box(new Vector3(x, 8.6f, nz - 0.85f), new Vector3(0.22f, 0.22f, 1.8f));
                metal.Box(new Vector3(x, 8.35f, nz - 1.55f), new Vector3(0.14f, 0.36f, 0.14f));
                metal.Box(new Vector3(x, 8.05f, nz - 1.55f), new Vector3(0.26f, 0.26f, 0.10f));
                // 吊り下がった鎖
                for (var y = 6.2f; y < 8.0f; y += 0.3f)
                    metal.Box(new Vector3(x, y, nz - 1.55f), new Vector3(0.05f, 0.22f, 0.05f));
                // 荷揚げ口。木の扉を二枚
                timber.Box(new Vector3(x - 0.55f, 7.0f, nz - 0.18f), new Vector3(1.0f, 2.1f, 0.14f));
                timber.Box(new Vector3(x + 0.55f, 7.0f, nz - 0.18f), new Vector3(1.0f, 2.1f, 0.14f));
                metal.Box(new Vector3(x, 7.0f, nz - 0.26f), new Vector3(0.09f, 2.1f, 0.09f));
            }

            // --- 北側の東半分。三軒目。倉庫の一階をぶち抜いた店 ---
            var inn = glow.Lit(11);            // 中は白めの電球。面が広いので暗め
            var rx0 = RestaurantWest;
            var rx1 = RestaurantEast;
            var rc = (rx0 + rx1) * 0.5f;
            inn.FaceZ(nz - 0.34f, rx0 + 0.4f, rx1 - 1.9f, 0.62f, 2.95f, -1);
            Behind(glass, 2, nz - 0.34f, -1, rx0 + 0.6f, rx1 - 2.1f, 0.62f, 2.95f, inside);
            // 桟の横木
            for (var y = 1.35f; y < 2.9f; y += 0.74f)
                metal.Box(new Vector3((rx0 + rx1 - 1.5f) * 0.5f, y, nz - 0.30f),
                    new Vector3(rx1 - 1.9f - rx0 - 0.4f, 0.07f, 0.11f));
            for (var x = rx0 + 1.5f; x < rx1 - 2.0f; x += 1.1f)
                metal.Box(new Vector3(x, 1.8f, nz - 0.30f), new Vector3(0.10f, 2.4f, 0.13f));
            metal.Box(new Vector3((rx0 + rx1 - 1.5f) * 0.5f, 0.58f, nz - 0.30f),
                new Vector3(rx1 - 1.9f - rx0 - 0.4f, 0.16f, 0.18f));
            metal.Box(new Vector3((rx0 + rx1 - 1.5f) * 0.5f, 3.02f, nz - 0.30f),
                new Vector3(rx1 - 1.9f - rx0 - 0.4f, 0.16f, 0.18f));
            // 扉。東の端に寄せる
            timber.Box(new Vector3(rx1 - 1.15f, 1.12f, nz - 0.20f), new Vector3(1.15f, 2.24f, 0.18f));
            metal.Box(new Vector3(rx1 - 1.55f, 1.05f, nz - 0.32f), new Vector3(0.07f, 0.34f, 0.07f));
            for (var x = rx0; x < rx1 + 0.1f; x += 1.7f)
                metal.Box(new Vector3(x, 3.10f, nz - 1.05f), new Vector3(0.07f, 0.07f, 1.5f));
            // 古い倉庫の梁は残っている。日除けの上に一本
            metal.Box(new Vector3(rc, 5.6f, nz - 0.85f), new Vector3(0.24f, 0.24f, 1.8f));
            metal.Box(new Vector3(rc, 5.35f, nz - 1.55f), new Vector3(0.14f, 0.36f, 0.14f));

            // --- 吊り看板と刷り物 ---
            var signs = Child(parent, "Heritage");
            Clear(signs);
            // タヴァーンの吊り看板。南の入口の上、腕木から下げる。両面に絵を貼る
            var hx = YardWest + 7.4f;
            metal.Box(new Vector3(hx, 3.55f, YardSouth + 0.75f), new Vector3(0.10f, 0.10f, 1.5f));
            metal.Box(new Vector3(hx, 3.85f, YardSouth + 0.14f), new Vector3(0.14f, 0.7f, 0.14f));
            for (var i = -1; i <= 1; i += 2)
                metal.Box(new Vector3(hx + i * 0.55f, 3.15f, YardSouth + 1.35f), new Vector3(0.05f, 0.8f, 0.05f));
            Board(signs, "SignTavern", "SignTavern",
                new Vector3(hx, 2.48f, YardSouth + 1.35f), Vector3.forward, new Vector2(1.6f, 1.0f));
            Board(signs, "SignTavern.B", "SignTavern",
                new Vector3(hx, 2.48f, YardSouth + 1.29f), Vector3.back, new Vector2(1.6f, 1.0f));
            // レストランの看板。日除けの上、中庭を向く
            Board(signs, "SignRestaurant", "SignRestaurant",
                new Vector3((RestaurantWest + RestaurantEast) * 0.5f, 3.92f, YardNorth - 0.42f),
                Vector3.back, new Vector2(2.4f, 0.72f), true);
            // ビストロの窓脇に貼った刷り物
            Board(signs, "PosterWine", "PosterWine",
                new Vector3(YardWest + 0.42f, 2.05f, LaneZ + 2.6f), Vector3.right, new Vector2(0.78f, 1.04f));
            Board(signs, "PosterWine.2", "PosterWine",
                new Vector3(LaneWest - 0.42f, 2.35f, YardSouth + 6.4f), Vector3.left, new Vector2(0.72f, 0.96f));

            // --- ガス灯にならった中庭の灯り ---
            var lamps = Child(parent, "YardLamps");
            Clear(lamps);
            var at = new Vector3[]
            {
                new Vector3(YardWest + 0.55f, 3.3f, LaneZ + 6.6f),
                new Vector3(YardWest + 0.55f, 3.3f, LaneZ - 6.2f),
                new Vector3(LaneWest - 0.55f, 3.3f, YardSouth + 3.6f),
                new Vector3(LaneWest - 0.55f, 3.3f, YardNorth - 3.6f),
                new Vector3(YardWest + 6.5f, 3.3f, YardSouth + 0.55f),
            };
            for (var i = 0; i < at.Length; i++)
            {
                var g = new GameObject("GasLamp" + i);
                g.transform.SetParent(lamps, false);
                g.transform.localPosition = at[i];
                var glassBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glassBox.name = "Glass";
                glassBox.transform.SetParent(g.transform, false);
                glassBox.transform.localScale = new Vector3(0.22f, 0.30f, 0.22f);
                // 1 本だけ新しい灯りに替わっている。全部が橙だと中庭が一色になる
                var hue = i == 2 ? new Color(0.80f, 0.88f, 1.00f) : new Color(1.00f, 0.90f, 0.74f);
                glassBox.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(hue, 2.0f);
                Object.DestroyImmediate(glassBox.GetComponent<Collider>());
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = hue;
                l.range = 7.5f;
                l.intensity = 10f;
                l.shadows = LightShadows.None;
            }

            // --- 由来の石板。心臓の話はここから来ている ---
            stone.Box(new Vector3(LaneWest - 0.14f, 2.1f, LaneZ - 3.2f), new Vector3(0.14f, 0.62f, 0.9f));
            metal.Box(new Vector3(LaneWest - 0.24f, 2.1f, LaneZ - 3.2f), new Vector3(0.05f, 0.5f, 0.78f));
        }

        // ---- 露天席 --------------------------------------------------------

        /// <summary>ビストロの露天席。天蓋の下。市との境はここで切れる</summary>
        public const float TerraceWest = -31.45f;
        public const float TerraceEast = -29.25f;
        public const float TerraceSouth = 27.30f;
        public const float TerraceNorth = 33.10f;

        /// <summary>タヴァーンの露天席。南の壁づたい</summary>
        public const float TavernTerraceNorth = 28.55f;

        /// <summary>レストラン。北の壁の東半分。自分の露店とは離す</summary>
        public const float RestaurantWest = -24.6f;
        public const float RestaurantEast = -17.4f;
        public const float RestaurantTerraceSouth = 41.15f;

        /// <summary>
        /// 椅子の座面の高さ。座り姿勢で尻が 0.47 に来るのを測ってあるので、
        /// 板の天面がその少し下、0.46 に来るところへ置く
        /// </summary>
        public const float SeatHigh = 0.435f;

        /// <summary>
        /// 椅子の場所と向き。(x, y, z) と w に yaw。
        /// 組み立ての順は 外形 → 人 なので、ここへ並べておけば人を座らせられる
        /// </summary>
        static readonly List<Vector4> ChairSpots = new List<Vector4>();

        /// <summary>立ち飲みの場所と向き。樽の卓のまわりはここから埋める</summary>
        static readonly List<Vector4> StandSpots = new List<Vector4>();

        /// <summary>植木の葉を溜めるところ。露天席を組んでいる間だけ生きる</summary>
        static Bank LeafBank;

        /// <summary>刈り込んだ葉。箱を 3 つずらして重ね、輪郭を崩す</summary>
        static void Leaves(Bank leaf, Vector3 at, float size, System.Random rng)
        {
            leaf.Box(at, new Vector3(size, size * 0.86f, size),
                Quaternion.Euler(0f, (float)(rng.NextDouble() * 45.0), 0f));
            leaf.Box(at + new Vector3(0f, size * 0.34f, 0f), new Vector3(size * 0.78f, size * 0.62f, size * 0.78f),
                Quaternion.Euler(0f, (float)(rng.NextDouble() * 45.0), 0f));
            leaf.Box(at + new Vector3((float)(rng.NextDouble() - 0.5) * 0.14f, -size * 0.3f,
                    (float)(rng.NextDouble() - 0.5) * 0.14f),
                new Vector3(size * 0.72f, size * 0.5f, size * 0.72f),
                Quaternion.Euler(0f, (float)(rng.NextDouble() * 45.0), 0f));
        }

        /// <summary>
        /// 中庭の露天席。ブリーディング・ハート・ヤードの実物は、
        /// 敷石の上に卓を出して天蓋を張り、暖房と豆電球を点けている。
        /// 2166 年でもそこは変わっていない、という作りにする
        /// </summary>
        static void Terraces(Transform parent, Bank stone, Bank metal, Bank timber)
        {
            ChairSpots.Clear();
            StandSpots.Clear();
            Clear(parent);
            var rng = new System.Random(3311);
            // 日除けは帆布。木の板で張ると店先が一色の木箱に見える
            var awning = new Bank { Texel = 0.55f };
            LeafBank = new Bank { Texel = 1.4f };

            BistroTerrace(parent, stone, metal, timber, awning, rng);
            TavernTerrace(parent, stone, metal, timber, awning, rng);
            RestaurantTerrace(parent, stone, metal, timber, awning, rng);

            awning.Emit(parent, "Awnings", Mat("Awning"), false, Generated);
            LeafBank.Emit(parent, "Leaves", Mat("Foliage"), false, Generated);
        }

        /// <summary>ビストロの露天席。中庭の南西へ張り出す</summary>
        static void BistroTerrace(Transform parent, Bank stone, Bank metal, Bank timber, Bank awning, System.Random rng)
        {
            var leaf = LeafBank;
            var cx = (TerraceWest + TerraceEast) * 0.5f;
            var cz = (TerraceSouth + TerraceNorth) * 0.5f;
            var wide = TerraceEast - TerraceWest;
            var deep = TerraceNorth - TerraceSouth;
            var roof = 2.80f;

            // 天蓋。西は建物へ、東は柱で受ける。雨が落ちるよう東へ傾ける
            awning.Box(new Vector3(cx, roof, cz), new Vector3(wide + 0.35f, 0.07f, deep + 0.30f),
                Quaternion.Euler(0f, 0f, 4.5f));
            awning.Box(new Vector3(TerraceEast + 0.12f, roof - 0.20f, cz), new Vector3(0.10f, 0.26f, deep + 0.30f));
            for (var z = TerraceSouth; z <= TerraceNorth + 0.1f; z += deep * 0.5f)
            {
                metal.Box(new Vector3(TerraceEast, roof * 0.5f, z), new Vector3(0.11f, roof, 0.11f));
                metal.Box(new Vector3(cx, roof - 0.16f, z), new Vector3(wide, 0.07f, 0.07f));
            }
            // 天蓋を吊る筋交い
            for (var z = TerraceSouth + 0.6f; z < TerraceNorth; z += deep * 0.5f)
                metal.Box(new Vector3(TerraceEast - 0.45f, roof - 0.45f, z), new Vector3(1.0f, 0.06f, 0.06f),
                    Quaternion.Euler(0f, 0f, 38f));

            // 市との境の柵。切れ目をひとつ空けて、そこから入る
            for (var z = TerraceSouth; z < TerraceNorth; z += 0.42f)
            {
                if (z > cz - 0.75f && z < cz + 0.75f) continue;
                metal.Box(new Vector3(TerraceEast - 0.06f, 0.44f, z), new Vector3(0.04f, 0.88f, 0.04f));
            }
            metal.Box(new Vector3(TerraceEast - 0.06f, 0.88f, TerraceSouth + (cz - 0.75f - TerraceSouth) * 0.5f),
                new Vector3(0.07f, 0.07f, cz - 0.75f - TerraceSouth));
            metal.Box(new Vector3(TerraceEast - 0.06f, 0.88f, cz + 0.75f + (TerraceNorth - cz - 0.75f) * 0.5f),
                new Vector3(0.07f, 0.07f, TerraceNorth - cz - 0.75f));

            // 敷石の上に石板を敷いて、露天席だけ一段上げる
            stone.Box(new Vector3(cx, 0.035f, cz), new Vector3(wide + 0.2f, 0.07f, deep + 0.1f));

            // 卓と椅子。向かい合わせに 2 脚、ときどき 3 脚目
            var tables = new float[] { TerraceSouth + 1.05f, cz, TerraceNorth - 1.05f };
            for (var i = 0; i < tables.Length; i++)
            {
                var tz = tables[i];
                var tx = cx - 0.05f;
                // 石板の天面は 0.07。卓も椅子もそこへ置く
                Table(parent, "BistroTable" + i, new Vector3(tx, 0.07f, tz), rng);
                Chair(parent, "BistroChair" + i + "a", new Vector3(tx - 0.72f, 0.07f, tz + (float)(rng.NextDouble() - 0.5) * 0.2f), 90f, rng);
                Chair(parent, "BistroChair" + i + "b", new Vector3(tx + 0.72f, 0.07f, tz + (float)(rng.NextDouble() - 0.5) * 0.2f), -90f, rng);
                if (rng.NextDouble() < 0.5)
                    Chair(parent, "BistroChair" + i + "c", new Vector3(tx + 0.1f, 0.07f, tz - 0.74f), 0f, rng);
            }

            // 豆電球。天蓋の下に一列
            var wire = Child(parent, "BistroBulbs");
            var b = 0;
            for (var z = TerraceSouth + 0.35f; z < TerraceNorth; z += 0.52f, b++)
                // 玉ひとつずつに灯りを足すと数が増えすぎる。8 つに 1 つだけ光源を持たせる
                Bulb(wire, new Vector3(cx + Mathf.Sin(z * 1.7f) * 0.35f, roof - 0.42f, z), 0.055f, 1.9f,
                    null, 3.0f, b % 8 == 0 ? 2.4f : 0f);
            metal.Box(new Vector3(cx, roof - 0.33f, cz), new Vector3(0.035f, 0.035f, deep));

            // 暖房。傘つきの柱。雨の中で外に座れるのはこれのおかげ
            for (var i = 0; i < 2; i++)
            {
                var hz = TerraceSouth + 0.7f + i * (deep - 1.4f);
                var hx = TerraceEast - 0.42f;
                metal.Box(new Vector3(hx, 0.05f, hz), new Vector3(0.44f, 0.10f, 0.44f));
                metal.Box(new Vector3(hx, 0.95f, hz), new Vector3(0.12f, 1.8f, 0.12f));
                metal.Box(new Vector3(hx, 2.02f, hz), new Vector3(0.34f, 0.42f, 0.34f));
                metal.Box(new Vector3(hx, 2.30f, hz), new Vector3(0.72f, 0.07f, 0.72f));
                Bulb(Child(parent, "Heater" + i), new Vector3(hx, 2.02f, hz), 0.17f, 2.0f,
                    new Color(1.00f, 0.44f, 0.20f), 3.4f, 2.6f);
            }

            // 植木。柵ぎわ、卓と卓のあいだへ。椅子と場所を取り合わせない
            for (var i = 0; i < 4; i++)
            {
                var pz = TerraceSouth + 0.25f + i * 1.80f;
                var px = TerraceEast - 0.24f;
                stone.Box(new Vector3(px, 0.26f, pz), new Vector3(0.40f, 0.52f, 0.40f));
                stone.Box(new Vector3(px, 0.53f, pz), new Vector3(0.46f, 0.06f, 0.46f));
                timber.Box(new Vector3(px, 0.82f, pz), new Vector3(0.08f, 0.58f, 0.08f));
                Leaves(leaf, new Vector3(px, 1.28f, pz), 0.54f, rng);
            }
        }

        /// <summary>タヴァーンの露天席。樽を卓に立てて、南の壁づたいに並べる</summary>
        static void TavernTerrace(Transform parent, Bank stone, Bank metal, Bank timber, Bank awning, System.Random rng)
        {
            var z0 = YardSouth + 0.65f;
            var z1 = TavernTerraceNorth;
            var cz = (z0 + z1) * 0.5f;
            var x0 = YardWest + 4.6f;
            var x1 = LaneWest - 2.6f;
            var roof = 2.62f;
            var doorX = YardWest + 7.4f;

            // 日除け。壁から中庭へ張り出す
            awning.Box(new Vector3((x0 + x1) * 0.5f, roof, cz), new Vector3(x1 - x0, 0.06f, z1 - z0 + 0.4f),
                Quaternion.Euler(-5f, 0f, 0f));
            for (var x = x0; x <= x1 + 0.1f; x += (x1 - x0) * 0.25f)
            {
                metal.Box(new Vector3(x, roof * 0.5f, z1 - 0.06f), new Vector3(0.10f, roof, 0.10f));
                metal.Box(new Vector3(x, roof - 0.22f, cz), new Vector3(0.06f, 0.06f, z1 - z0));
            }

            // 樽の卓と丸椅子。入口の前だけ空ける
            var n = 0;
            for (var x = x0 + 0.9f; x < x1 - 0.6f; x += (float)(1.9 + rng.NextDouble() * 0.7))
            {
                if (Mathf.Abs(x - doorX) < 1.5f) continue;
                var bz = cz + (float)(rng.NextDouble() - 0.5) * 0.35f;
                // 樽。上面を卓にする
                timber.Box(new Vector3(x, 0.44f, bz), new Vector3(0.60f, 0.88f, 0.60f));
                metal.Box(new Vector3(x, 0.30f, bz), new Vector3(0.64f, 0.06f, 0.64f));
                metal.Box(new Vector3(x, 0.66f, bz), new Vector3(0.64f, 0.06f, 0.64f));
                timber.Box(new Vector3(x, 0.90f, bz), new Vector3(0.74f, 0.05f, 0.74f));
                // 樽の上に硝子を 2 つ 3 つ。誰かが置いていったもの
                var top = Child(parent, "TavernTop" + n);
                top.localPosition = new Vector3(x, 0f, bz);
                var glasses = 2 + rng.Next(2);
                for (var i = 0; i < glasses; i++)
                    Box(top, "Glass" + i, new Vector3((float)(rng.NextDouble() - 0.5) * 0.44f, 0.98f,
                            (float)(rng.NextDouble() - 0.5) * 0.44f),
                        new Vector3(0.06f, 0.12f, 0.06f), "Glass");
                // 樽は立ったまま飲む卓。腰掛けずに囲む
                for (var i = -1; i <= 1; i += 2)
                {
                    // 樽の天板は 0.74。腹がめり込まない距離まで離す
                    var sx = x + i * 0.98f;
                    if (Mathf.Abs(sx - doorX) < 1.2f) continue;
                    StandSpots.Add(new Vector4(sx, 0.02f, bz, i > 0 ? -90f : 90f));
                }
                n++;
            }

            // 灰皿がわりの砂の桶と、積んだ空樽
            metal.Box(new Vector3(x1 - 1.1f, 0.22f, z0 + 0.25f), new Vector3(0.34f, 0.44f, 0.34f));
            for (var i = 0; i < 3; i++)
                timber.Box(new Vector3(x0 - 0.6f, 0.30f + i * 0.60f, z0 + 0.35f + (i % 2) * 0.1f),
                    new Vector3(0.56f, 0.58f, 0.56f), Quaternion.Euler(0f, i * 17f, 0f));

            // 豆電球
            var wire = Child(parent, "TavernBulbs");
            var b = 0;
            for (var x = x0 + 0.4f; x < x1; x += 0.62f, b++)
                Bulb(wire, new Vector3(x, roof - 0.36f + Mathf.Sin(x * 1.3f) * 0.06f, cz), 0.05f, 1.7f,
                    null, 3.0f, b % 8 == 0 ? 2.4f : 0f);
        }

        /// <summary>レストランの歩道席。北の壁の前。日除けの下に卓を並べる</summary>
        static void RestaurantTerrace(Transform parent, Bank stone, Bank metal, Bank timber, Bank awning, System.Random rng)
        {
            var z = RestaurantTerraceSouth + 1.35f;
            // 日除け。北の壁から中庭へ張り出す
            awning.Box(new Vector3((RestaurantWest + RestaurantEast) * 0.5f, 3.34f, YardNorth - 1.05f),
                new Vector3(RestaurantEast - RestaurantWest, 0.07f, 1.5f), Quaternion.Euler(6f, 0f, 0f));
            // 敷居。歩道席と市の境に鉢を並べる
            for (var x = RestaurantWest; x < RestaurantEast + 0.1f; x += 1.15f)
            {
                stone.Box(new Vector3(x, 0.26f, RestaurantTerraceSouth), new Vector3(0.40f, 0.52f, 0.40f));
                stone.Box(new Vector3(x, 0.53f, RestaurantTerraceSouth), new Vector3(0.46f, 0.06f, 0.46f));
                Leaves(LeafBank, new Vector3(x, 0.74f, RestaurantTerraceSouth), 0.40f, rng);
            }
            // 卓は 3 つ。壁を背にする側と、中庭を向く側
            for (var i = 0; i < 3; i++)
            {
                var x = RestaurantWest + 1.5f + i * 2.2f;
                Table(parent, "InnTable" + i, new Vector3(x, 0f, z), rng);
                Chair(parent, "InnChair" + i + "a", new Vector3(x, 0f, z + 0.74f), 180f, rng);
                Chair(parent, "InnChair" + i + "b", new Vector3(x, 0f, z - 0.74f), 0f, rng);
                if (rng.NextDouble() < 0.4)
                    Chair(parent, "InnChair" + i + "c", new Vector3(x - 0.76f, 0f, z), 90f, rng);
            }
            // 豆電球。日除けの下
            var wire = Child(parent, "InnBulbs");
            var b = 0;
            for (var x = RestaurantWest + 0.4f; x < RestaurantEast; x += 0.58f, b++)
                Bulb(wire, new Vector3(x, 2.92f, YardNorth - 1.35f), 0.05f, 1.8f,
                    null, 3.0f, b % 8 == 0 ? 2.4f : 0f);
        }

        /// <summary>丸い卓。脚は一本柱。上に硝子と燭台を載せる</summary>
        static void Table(Transform parent, string name, Vector3 at, System.Random rng)
        {
            var t = Child(parent, name);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 24.0 - 12.0), 0f);
            Box(t, "Foot", new Vector3(0f, 0.03f, 0f), new Vector3(0.42f, 0.06f, 0.42f), "Furniture");
            Box(t, "Stem", new Vector3(0f, 0.36f, 0f), new Vector3(0.09f, 0.66f, 0.09f), "Furniture");
            Box(t, "Top", new Vector3(0f, 0.72f, 0f), new Vector3(0.76f, 0.05f, 0.76f), "Timber");
            Box(t, "Rim", new Vector3(0f, 0.68f, 0f), new Vector3(0.80f, 0.04f, 0.80f), "Furniture");
            // 卓の上。燭台と、硝子を 2 つ
            Bulb(t, new Vector3(0.06f, 0.83f, 0.04f), 0.045f, 2.4f, new Color(1.00f, 0.66f, 0.30f), 2.6f, 0f);
            Box(t, "Candle", new Vector3(0.06f, 0.78f, 0.04f), new Vector3(0.09f, 0.11f, 0.09f), "Metal");
            for (var i = 0; i < 2; i++)
                Box(t, "Glass" + i, new Vector3((float)(rng.NextDouble() - 0.5) * 0.42f, 0.80f,
                        (float)(rng.NextDouble() - 0.5) * 0.42f),
                    new Vector3(0.06f, 0.11f, 0.06f), "Glass");
        }

        /// <summary>椅子ひとつ。座面の高さは人形の座り姿勢に合わせる。yaw は座る人が向く向き</summary>
        static void Chair(Transform parent, string name, Vector3 at, float yaw, System.Random rng)
        {
            var spin = yaw + (float)(rng.NextDouble() * 24.0 - 12.0);
            var t = Child(parent, name);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, spin, 0f);
            Box(t, "Seat", new Vector3(0f, SeatHigh, 0f), new Vector3(0.44f, 0.05f, 0.44f), "Timber");
            for (var i = 0; i < 4; i++)
                Box(t, "Leg" + i, new Vector3((i % 2 == 0 ? -1 : 1) * 0.18f, SeatHigh * 0.5f, (i < 2 ? -1 : 1) * 0.18f),
                    new Vector3(0.04f, SeatHigh, 0.04f), "Furniture");
            // 背もたれは座る人の後ろ。この場では -z が後ろ。
            // 1 枚板だと白い衝立に見えるので、縦框と横桟で組む
            for (var i = -1; i <= 1; i += 2)
                Box(t, "Stile" + i, new Vector3(i * 0.20f, SeatHigh + 0.28f, -0.20f),
                    new Vector3(0.04f, 0.56f, 0.04f), "Furniture");
            Box(t, "Rail", new Vector3(0f, SeatHigh + 0.54f, -0.20f), new Vector3(0.44f, 0.07f, 0.045f), "Furniture");
            Box(t, "Slat", new Vector3(0f, SeatHigh + 0.30f, -0.20f), new Vector3(0.40f, 0.08f, 0.035f), "Furniture");
            Box(t, "Slat2", new Vector3(0f, SeatHigh + 0.14f, -0.20f), new Vector3(0.40f, 0.07f, 0.035f), "Furniture");
            ChairSpots.Add(new Vector4(at.x, at.y, at.z, spin));
        }

        /// <summary>豆電球ひとつ。光る玉と、そこから漏れる灯り</summary>
        static void Bulb(Transform parent, Vector3 at, float size, float glow,
            Color? tint = null, float range = 3.0f, float power = 4f)
        {
            var col = tint.HasValue ? tint.Value : new Color(1.00f, 0.89f, 0.74f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Bulb";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(size, size, size);
            go.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(col, glow);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            // 光源まで足すと数が増えすぎる。光る玉だけで済むところは power を 0 にする
            if (power <= 0f) return;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.range = range;
            l.intensity = power;
            l.shadows = LightShadows.None;
        }

        // ---- 表示板 --------------------------------------------------------

        /// <summary>小路の口の表示板と、自分の露店の看板。どちらも光らない板</summary>
        static void Boards(Transform parent)
        {
            Clear(parent);
            // 案内板を吊る腕木と灯り
            var arm = new Bank { Texel = 0.7f };
            arm.Box(new Vector3(-StreetHalf + 0.8f, 3.78f, LaneZ - 2.6f), new Vector3(1.8f, 0.11f, 0.11f));
            arm.Box(new Vector3(-StreetHalf + 0.16f, 3.35f, LaneZ - 2.6f), new Vector3(0.12f, 0.9f, 0.12f));
            for (var i = -1; i <= 1; i += 2)
                arm.Box(new Vector3(-StreetHalf + 1.45f + i * 1.15f, 3.50f, LaneZ - 2.6f), new Vector3(0.05f, 0.5f, 0.05f));
            arm.Emit(parent, "SignArm", Mat("Metal"), false, Generated);
            var lamp = new GameObject("ArrowLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(-StreetHalf + 1.25f, 4.0f, LaneZ - 2.6f);
            var al = lamp.AddComponent<Light>();
            al.type = LightType.Point;
            al.color = new Color(1.00f, 0.94f, 0.82f);
            al.range = 6f;
            al.intensity = 14f;
            al.shadows = LightShadows.None;
            // 大通りから小道への案内。歩道の上へ突き出して、南から歩いてくる目に入れる
            var armZ = LaneZ - 2.6f;
            Board(parent, "SignYardArrow", "SignYardArrow",
                new Vector3(-StreetHalf + 1.45f, 3.05f, armZ), Vector3.back, new Vector2(2.9f, 1.27f), true, false);
            Board(parent, "SignYardArrow.N", "SignYardArrow",
                new Vector3(-StreetHalf + 1.45f, 3.05f, armZ + 0.07f), Vector3.forward, new Vector2(2.9f, 1.27f), true, false);
            // 小道の口の脇。通りを歩きながら読める高さに
            Board(parent, "SignYardName", "SignYardName",
                new Vector3(-StreetHalf + 0.10f, 2.35f, LaneZ - 2.45f), Vector3.right, new Vector2(1.9f, 0.6f));
            // 小道を抜けた先。ヤードから振り返ったときに読める
            Board(parent, "SignYardName.Lane", "SignYardName",
                new Vector3(LaneWest + 0.7f, 2.9f, LaneZ - LaneHalf + 0.06f), Vector3.forward, new Vector2(1.9f, 0.6f));
            // 露店の看板。1.6 くらいに下げると、売り手の側に回ったときに
            // 板の背が目の高さに来て、買い手も卓も見えなくなる。頭の上へ吊る
            var front = Quaternion.Euler(0f, MyStallYaw, 0f) * new Vector3(0f, 0f, -1f);
            Board(parent, "SignMemories", "SignMemories",
                new Vector3(MyStallX, 2.14f, MyStallZ) + front * 1.12f, front, new Vector2(1.5f, 0.75f));

            StreetSigns(parent);
        }

        /// <summary>通りで読む板の数。BuildAlleyItems が調べる対象を立てる数と揃える</summary>
        public const int StreetSignCount = 5;

        /// <summary>
        /// 通りの壁に掛かっている、立ち止まって読む板。調べる対象はこの 5 枚。
        ///
        /// ネオンは頭上の店の灯りで、近づいて読む物ではない。こちらは目の高さに掛け、
        /// 左右に振り分けて、歩いていれば必ずどれかが目に入るようにする。
        /// 管が前を横切らないよう、壁から少しだけ手前へ出す
        /// </summary>
        static void StreetSigns(Transform parent)
        {
            // 名前、絵、南から数えた z、側（1 で東の壁）、高さ、大きさ
            var plates = new[]
            {
                new { tex = "SignStreetName", z = 2.6f,  side = 1,  y = 2.62f, size = new Vector2(2.00f, 0.625f) },
                new { tex = "SignFitting",    z = 9.2f,  side = -1, y = 2.22f, size = new Vector2(1.86f, 0.93f) },
                new { tex = "SignChemist",    z = 14.6f, side = 1,  y = 2.22f, size = new Vector2(1.86f, 0.93f) },
                new { tex = "SignPawn",       z = 20.8f, side = -1, y = 2.22f, size = new Vector2(1.86f, 0.93f) },
                new { tex = "SignNotice",     z = 27.6f, side = 1,  y = 2.26f, size = new Vector2(1.86f, 0.93f) },
            };
            for (var i = 0; i < plates.Length; i++)
            {
                var p = plates[i];
                // 読む人は通りの真ん中に立つので、板は通りの中央を向く
                var face = new Vector3(-p.side, 0f, 0f);
                Board(parent, "StreetSign" + i, p.tex,
                    new Vector3(p.side * (StreetHalf - 0.14f), p.y, p.z), face, p.size);
            }
        }

        /// <summary>
        /// 板を 1 枚立てる。face は「読む人が居る側」の向き。
        /// 絵は板の裏側に乗るので、向きはここで一度だけ決める。
        /// 呼ぶ側が角度を書くと必ずどれかが裏返るので、角度は受け取らない
        /// </summary>
        static void Board(Transform parent, string name, string texture, Vector3 at, Vector3 face, Vector2 size,
            bool glowing = false, bool backing = true)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + texture + ".png");
            if (tex == null) { Debug.LogWarning("テクスチャが無い: " + texture); return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.LookRotation(-face.normalized, Vector3.up);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = BoardMat(texture, tex, glowing);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            if (!backing) return;
            // 板の背。1 枚だけだと裏から絵が透けて、しかも左右が返って見える。
            // 表裏で 2 枚使う物（案内の矢）は呼ぶ側で backing を切る
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = name + ".Back";
            back.transform.SetParent(parent, false);
            back.transform.localPosition = at - face.normalized * 0.03f;
            back.transform.localRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            back.transform.localScale = new Vector3(size.x, size.y, 1f);
            back.GetComponent<MeshRenderer>().sharedMaterial = Mat("Metal");
            Object.DestroyImmediate(back.GetComponent<Collider>());
        }

        /// <summary>
        /// 板のマテリアル。灯りを受ける Lit だと絵が出なかったので unlit で貼り、
        /// 明るさは色で落としてある。暗い路地で読める程度に留める
        /// </summary>
        static Material BoardMat(string texture, Texture2D tex, bool glowing = false)
        {
            var path = Materials + "Board_" + texture + (glowing ? "_lit" : "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit")
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                if (m != null) AssetDatabase.DeleteAsset(path);
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Board_" + texture + (glowing ? "_lit" : "");
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", glowing ? new Color(1.45f, 1.45f, 1.50f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f));
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }


        // ---- 雨 ------------------------------------------------------------

        /// <summary>
        /// 雨。歩く先へ付いてくるよう、粒はプレイヤーの上から降らせる。
        /// 粒そのものは世界の座標で動かすので、走っても雨が斜めに固まらない。
        /// 屋根の下でも降り込むが、1/3 の解像度では気にならない
        /// </summary>
        static void Rain()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            var old = player.transform.Find("Rain");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("Rain");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 9f, 2.5f);
            // 箱の面から真下へ吐かせる
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8.5f, 11.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.020f, 0.038f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.70f, 0.82f, 0.22f), new Color(0.78f, 0.84f, 0.95f, 0.42f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 760;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.35f);

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 560f;

            // 屋根で止める。人が屋根の下へ入ったかどうかではなく、
            // 粒が屋根に当たって消える。外から眺めても屋根を貫かない
            var hit = ps.collision;
            hit.enabled = true;
            hit.type = ParticleSystemCollisionType.World;
            hit.mode = ParticleSystemCollisionMode.Collision3D;
            hit.quality = ParticleSystemCollisionQuality.High;
            hit.collidesWith = 1 << RoofLayer;
            hit.enableDynamicColliders = false;
            hit.sendCollisionMessages = false;
            hit.lifetimeLoss = 1f;      // 当たったら消える。跳ねさせない
            hit.bounce = 0f;
            hit.dampen = 1f;
            hit.radiusScale = 0.2f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 17f, 0.2f);

            // 風。まっすぐ落ちる雨は書き割りに見える
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.55f, -0.15f);
            // 3 軸とも同じ指定の仕方で揃える。片方だけ範囲指定だと弾かれる
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            // 屋根の下では降らせない。粒に当たりを持たせると重いので、覆いを箱で持つ
            var cover = go.AddComponent<RainCover>();
            var cso = new SerializedObject(cover);
            var boxes = cso.FindProperty("shelters");
            boxes.arraySize = 5;
            boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(
                new Vector3((LaneWest - StreetHalf) * 0.5f, 3f, LaneZ),
                new Vector3(-StreetHalf - LaneWest + 1.2f, 6f, LaneHalf * 2f + 0.6f));
            boxes.GetArrayElementAtIndex(1).boundsValue = new Bounds(
                new Vector3(MyStallX, 1.2f, MyStallZ), new Vector3(3.0f, 2.4f, 3.0f));
            // 露天席の天蓋の下。ここに入れば雨に濡れない
            boxes.GetArrayElementAtIndex(2).boundsValue = new Bounds(
                new Vector3((TerraceWest + TerraceEast) * 0.5f, 1.4f, (TerraceSouth + TerraceNorth) * 0.5f),
                new Vector3(TerraceEast - TerraceWest + 0.3f, 2.8f, TerraceNorth - TerraceSouth + 0.3f));
            boxes.GetArrayElementAtIndex(3).boundsValue = new Bounds(
                new Vector3((YardWest + 4.6f + LaneWest - 2.6f) * 0.5f, 1.3f,
                    (YardSouth + 0.65f + TavernTerraceNorth) * 0.5f),
                new Vector3(LaneWest - 2.6f - YardWest - 4.6f, 2.6f, TavernTerraceNorth - YardSouth - 0.65f + 0.4f));
            boxes.GetArrayElementAtIndex(4).boundsValue = new Bounds(
                new Vector3((RestaurantWest + RestaurantEast) * 0.5f, 1.5f, YardNorth - 1.05f),
                new Vector3(RestaurantEast - RestaurantWest, 3.0f, 2.2f));
            cso.FindProperty("player").objectReferenceValue = player.transform;
            cso.FindProperty("sound").objectReferenceValue = RainSound(player.transform);
            cso.ApplyModifiedPropertiesWithoutUndo();

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = RainMat();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3.6f;
            r.velocityScale = 0.06f;
            r.cameraVelocityScale = 0f;
            r.sortMode = ParticleSystemSortMode.None;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>
        /// 雨の音。頭上に置いて輪で流す。台詞の邪魔をしない音量に絞る
        /// </summary>
        static AudioSource RainSound(Transform player)
        {
            var t = player.Find("RainSound");
            var go = t != null ? t.gameObject : new GameObject("RainSound");
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var a = go.GetComponent<AudioSource>();
            if (a == null) a = go.AddComponent<AudioSource>();
            a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/RainLoop.wav");
            a.loop = true;
            a.playOnAwake = true;
            a.spatialBlend = 0f;
            a.volume = 0.30f;
            a.priority = 200;
            EditorUtility.SetDirty(a);
            return a;
        }

        /// <summary>雨粒のマテリアル。煙と同じ柔らかい絵を、細く引き伸ばして筋にする</summary>
        static Material RainMat()
        {
            var path = Materials + "Rain.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                AssetDatabase.CreateFolder("Assets/Materials", "Alley");
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.name = "Rain";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SmokePuff.png");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(1.1f, 1.15f, 1.25f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ---- 置き方 --------------------------------------------------------

        /// <summary>立ち位置。通りの入口に、北を向いて立たせる</summary>
        static void Place(Transform root)
        {
            var cam = GameObject.Find("Player/Main Camera");
            if (cam != null)
            {
                var c = cam.GetComponent<Camera>();
                // 空が真っ黒だと雲の形が出ない。街明かりを受けた夜空の色を置く
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.270f, 0.186f, 0.196f);
                EditorUtility.SetDirty(c);
            }
            var player = GameObject.Find("Player");
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0f, 0.1f, WalkSouth + 1.6f);
            player.transform.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }

        // ---- 道具 ----------------------------------------------------------

        static Transform Root(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>知らない子を落とす。組み方を変えたときに前の束が残らないように</summary>
        static void Prune(Transform parent, string[] keep)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (System.Array.IndexOf(keep, c.name) >= 0) continue;
                Object.DestroyImmediate(c.gameObject);
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>箱をひとつ置く。露店のような細かい物はこれで足りる</summary>
        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, string material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(material);
            return go;
        }

        /// <summary>
        /// 素材ごとのマテリアル。同じ名前の絵があれば貼り、無ければ色だけで作る。
        /// 絵は後から差し替えられるよう、組み直すたびに結び直す
        /// </summary>
        static Material Mat(string name)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            Color col;
            float smooth;
            Tone(name, out col, out smooth);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Alley" + name + ".png");
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
            }
            else
            {
                m.SetTexture("_BaseMap", null);
                m.SetColor("_BaseColor", col);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", name == "Metal" ? 0.55f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 素材ごとの色と艶。面という面が光ると画面が白く滑るので、地の艶は低く抑える。
        /// 濡れて強く照り返すのは水たまりだけ
        /// </summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                case "Asphalt": col = new Color(0.125f, 0.132f, 0.155f); smooth = 0.14f; break;
                case "Cobble": col = new Color(0.135f, 0.138f, 0.150f); smooth = 0.12f; break;
                case "Brick": col = new Color(0.165f, 0.130f, 0.118f); smooth = 0.18f; break;
                case "Concrete": col = new Color(0.185f, 0.186f, 0.192f); smooth = 0.10f; break;
                case "Metal": col = new Color(0.095f, 0.098f, 0.105f); smooth = 0.26f; break;
                case "Glass": col = new Color(0.030f, 0.036f, 0.048f); smooth = 0.52f; break;
                // 真っ黒で艶だけの面は、映り込む物が無いと穴に見える。地の明るさを持たせる
                case "Puddle": col = new Color(0.100f, 0.108f, 0.128f); smooth = 0.84f; break;
                case "Tarp": col = new Color(0.150f, 0.145f, 0.130f); smooth = 0.30f; break;
                // 店の日除け。ロンドンの店先にある濃い帆布
                case "Awning": col = new Color(0.052f, 0.108f, 0.082f); smooth = 0.22f; break;
                // 植木の葉。中庭で唯一の緑
                case "Foliage": col = new Color(0.042f, 0.082f, 0.046f); smooth = 0.12f; break;
                // 露天席の卓と椅子。塗った鉄と木。灯りの前で影になる濃さ
                case "Furniture": col = new Color(0.062f, 0.058f, 0.052f); smooth = 0.28f; break;
                case "TarpRed": col = new Color(0.290f, 0.105f, 0.095f); smooth = 0.28f; break;
                case "TarpGreen": col = new Color(0.105f, 0.215f, 0.135f); smooth = 0.28f; break;
                case "TarpBlue": col = new Color(0.095f, 0.150f, 0.260f); smooth = 0.28f; break;
                case "TarpOchre": col = new Color(0.290f, 0.215f, 0.090f); smooth = 0.28f; break;
                case "TarpMine": col = new Color(0.135f, 0.115f, 0.100f); smooth = 0.30f; break;
                case "Timber": col = new Color(0.130f, 0.105f, 0.080f); smooth = 0.15f; break;
                // 大型ゴミコンテナの塗り。褪せた黄土。この通りで唯一、地の色で輪郭が出る物
                case "Skip": col = new Color(0.225f, 0.160f, 0.055f); smooth = 0.20f; break;
                case "Pole": col = new Color(0.090f, 0.090f, 0.095f); smooth = 0.22f; break;
                case "Crate": col = new Color(0.105f, 0.090f, 0.072f); smooth = 0.12f; break;
                default: col = new Color(0.12f, 0.12f, 0.13f); smooth = 0.3f; break;
            }
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
