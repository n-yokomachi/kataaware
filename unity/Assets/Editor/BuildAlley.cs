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
        public const float StreetSouth = -6f;
        public const float StreetNorth = 42f;

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
            Prune(root, new[] { "Shell", "Fixtures", "Lamps", "Puddles", "Neon", "Market", "Boards", "Litter", "Crowd", "Bounds" });
            Shell(Child(root, "Shell"));
            Fixtures(Child(root, "Fixtures"));
            Lamps(Child(root, "Lamps"));
            Puddles(Child(root, "Puddles"));
            Neon(Child(root, "Neon"));
            Market(Child(root, "Market"));
            Boards(Child(root, "Boards"));
            Litter(Child(root, "Litter"));
            Crowd(Child(root, "Crowd"));
            Bounds(Child(root, "Bounds"));
            Rain();
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
            var warm = new Bank { Texel = 0.50f };
            var cold = new Bank { Texel = 0.50f };

            Ground(road, paving, stone);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                Facades(brick, stone, metal, glass, warm, cold, side);
            }
            LaneShell(brick, stone, paving);
            YardShell(brick, stone, paving);

            road.Emit(parent, "Road", Mat("Asphalt"), false, Generated);
            paving.Emit(parent, "Paving", Mat("Cobble"), false, Generated);
            brick.Emit(parent, "Brick", Mat("Brick"), false, Generated);
            stone.Emit(parent, "Stone", Mat("Concrete"), false, Generated);
            metal.Emit(parent, "MetalShell", Mat("Metal"), false, Generated);
            glass.Emit(parent, "Glass", Mat("Glass"), false, Generated);
            warm.Emit(parent, "WindowWarm", GlowMat(new Color(1.00f, 0.74f, 0.42f), 1.35f), false, Generated);
            cold.Emit(parent, "WindowCold", GlowMat(new Color(0.48f, 0.68f, 1.00f), 1.15f), false, Generated);
        }

        /// <summary>路面と歩道。車道は縁石ぶん低くして、排水口を等間隔に落とす</summary>
        static void Ground(Bank road, Bank paving, Bank stone)
        {
            road.FaceY(0f, -RoadHalf, RoadHalf, StreetSouth, StreetNorth, 1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                var inner = side * RoadHalf;
                var outer = side * StreetHalf;
                var x0 = Mathf.Min(inner, outer);
                var x1 = Mathf.Max(inner, outer);
                paving.FaceY(KerbRise, x0, x1, StreetSouth, StreetNorth, 1);
                stone.FaceX(inner, StreetSouth, StreetNorth, 0f, KerbRise, -side);
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
        static void Facades(Bank brick, Bank stone, Bank metal, Bank glass, Bank warm, Bank cold, int side)
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
                    Facade(brick, stone, metal, glass, warm, cold, side, wx, inward, u, rng);
        }

        static void Facade(Bank brick, Bank stone, Bank metal, Bank glass, Bank warm, Bank cold,
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
                var lit = rng.NextDouble();
                var bank = lit < 0.42 ? warm : lit < 0.60 ? cold : glass;
                bank.FaceX(back, o.x, o.y, o.z, o.w, inward);
                stone.Box(new Vector3(wx + inward * 0.06f, o.z - 0.07f, (o.x + o.y) * 0.5f),
                    new Vector3(0.13f, 0.09f, o.y - o.x + 0.24f));
                stone.Box(new Vector3(wx + inward * 0.04f, o.w + 0.09f, (o.x + o.y) * 0.5f),
                    new Vector3(0.10f, 0.12f, o.y - o.x + 0.30f));
            }

            if (u.ground == 0) Shopfront(stone, metal, warm, wx, back, z0, z1, shopY0, shopY1, inward);
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
        static void Shopfront(Bank stone, Bank metal, Bank warm, float wx, float back,
            float z0, float z1, float y0, float y1, int inward)
        {
            var gz0 = z0 + 0.35f;
            var gz1 = z1 - 1.55f;
            Reveal(stone, wx, back, new Vector4(gz0, gz1, y0, y1));
            warm.FaceX(back, gz0, gz1, y0, y1, inward);
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
            paving.FaceY(0.02f, west, east, LaneZ - LaneHalf, LaneZ + LaneHalf, 1);
            brick.FaceZ(LaneZ - LaneHalf, west, east, 0f, WallHeight, 1);
            brick.FaceZ(LaneZ + LaneHalf, west, east, 0f, WallHeight, -1);
            for (var i = 0; i < 4; i++)
            {
                var x = west + (east - west) * (0.18f + i * 0.2f);
                stone.Box(new Vector3(x, 4.4f, LaneZ), new Vector3(0.42f, 0.5f, LaneHalf * 2f + 0.2f));
            }
            brick.Box(new Vector3((west + east) * 0.5f, 6.6f, LaneZ), new Vector3(east - west, 4.2f, LaneHalf * 2f + 0.3f));
        }

        /// <summary>ヤード。四方を囲む面と、敷石と、見下ろす窓</summary>
        static void YardShell(Bank brick, Bank stone, Bank paving)
        {
            paving.FaceY(0.02f, YardWest, LaneWest, YardSouth, YardNorth, 1);
            brick.FaceX(YardWest, YardSouth, YardNorth, 0f, WallHeight, 1);
            brick.FaceZ(YardSouth, YardWest, LaneWest, 0f, WallHeight, 1);
            brick.FaceZ(YardNorth, YardWest, LaneWest, 0f, WallHeight, -1);
            brick.FaceX(LaneWest, YardSouth, LaneZ - LaneHalf, 0f, WallHeight, -1);
            brick.FaceX(LaneWest, LaneZ + LaneHalf, YardNorth, 0f, WallHeight, -1);
            var rng = new System.Random(5150);
            for (var f = 0; f < 4; f++)
            {
                var y = 3.2f + f * 2.8f;
                for (var z = YardSouth + 2f; z < YardNorth - 1.5f; z += 2.4f)
                {
                    if (rng.NextDouble() < 0.45) continue;
                    stone.Box(new Vector3(YardWest + 0.12f, y, z), new Vector3(0.24f, 1.3f, 0.95f));
                }
            }
            for (var f = 0; f < 3; f++)
            {
                var y = 3.4f + f * 2.8f;
                for (var x = YardWest + 2f; x < LaneWest - 1.5f; x += 2.6f)
                {
                    if (rng.NextDouble() < 0.5) continue;
                    stone.Box(new Vector3(x, y, YardSouth + 0.12f), new Vector3(0.95f, 1.3f, 0.24f));
                    if (rng.NextDouble() < 0.5)
                        stone.Box(new Vector3(x, y, YardNorth - 0.12f), new Vector3(0.95f, 1.3f, 0.24f));
                }
            }
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

        /// <summary>非常階段。2 層の踊り場と手摺、斜めの段</summary>
        static void FireEscape(Bank b, float wx, float z, int inward)
        {
            var from = 4.2f;
            for (var f = 0; f < 2; f++)
            {
                var y = from + f * UpperFloor;
                var x = wx + inward * 0.75f;
                b.Box(new Vector3(x, y, z), new Vector3(1.5f, 0.08f, 2.6f));
                b.Box(new Vector3(x + inward * 0.70f, y + 0.52f, z), new Vector3(0.06f, 1.04f, 2.6f));
                for (var i = -1; i <= 1; i += 2)
                    b.Box(new Vector3(x, y + 0.52f, z + i * 1.28f), new Vector3(1.5f, 1.04f, 0.06f));
                b.Box(new Vector3(x + inward * 0.70f, y + 1.02f, z), new Vector3(0.1f, 0.07f, 2.6f));
                var step = 8;
                for (var i = 0; i < step; i++)
                {
                    var t = (i + 0.5f) / step;
                    b.Box(new Vector3(x + inward * (0.1f + t * 1.1f), y + 0.12f + t * (UpperFloor - 0.3f), z + 1.0f),
                        new Vector3(0.26f, 0.05f, 0.7f));
                }
            }
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
                    b.Box(new Vector3(x, KerbRise + 0.42f, z), new Vector3(0.16f, 0.84f, 0.16f));
                    b.Box(new Vector3(x, KerbRise + 0.86f, z), new Vector3(0.22f, 0.07f, 0.22f));
                }
                for (var z = StreetSouth + 6f; z < StreetNorth - 4f; z += (float)(7.0 + rng.NextDouble() * 5.0))
                {
                    if (side < 0 && z > LaneZ - 3f && z < LaneZ + 3f) continue;
                    var wx = side * (StreetHalf - 0.55f);
                    var pile = 1 + rng.Next(3);
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
            for (var z = StreetSouth + 5f; z < StreetNorth - 3f; z += 11f)
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
            var bank = new Bank { Texel = 0.35f };
            var rng = new System.Random(7720);
            for (var z = StreetSouth + 3f; z < StreetNorth - 2f; z += (float)(2.6 + rng.NextDouble() * 3.4))
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0) * (StreetHalf - 0.9f);
                var w = (float)(0.9 + rng.NextDouble() * 2.6);
                var d = (float)(0.7 + rng.NextDouble() * 2.2);
                var y = Mathf.Abs(x) > RoadHalf ? KerbRise + 0.008f : 0.008f;
                bank.FaceY(y, x - w * 0.5f, x + w * 0.5f, z - d * 0.5f, z + d * 0.5f, 1);
            }
            for (var x = YardWest + 1.5f; x < LaneWest - 1f; x += (float)(2.0 + rng.NextDouble() * 2.5))
            {
                var z = LaneZ + (float)(rng.NextDouble() * 2.0 - 1.0) * 1.3f;
                var w = (float)(0.8 + rng.NextDouble() * 1.8);
                var d = (float)(0.6 + rng.NextDouble() * 1.4);
                bank.FaceY(0.028f, x - w * 0.5f, x + w * 0.5f, z - d * 0.5f, z + d * 0.5f, 1);
            }
            bank.Emit(parent, "Puddles", Mat("Puddle"), false, Generated);
        }


        // ---- 人 ------------------------------------------------------------

        /// <summary>
        /// 通りとヤードの人。顔は作らない。仮置きなので、灰色ひと色の半透明で置く。
        /// 全員ぶんを 1 枚の mesh へ焼くので、何十人立てても描画は 1 回で済む
        /// </summary>
        static void Crowd(Transform parent)
        {
            Clear(parent);
            var bank = new Bank { Texel = 0.5f };
            var rng = new System.Random(4820);

            // 通り。歩道を行き来する人と、店先で立ち止まっている人
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                for (var z = StreetSouth + 3f; z < StreetNorth - 3f; z += (float)(1.1 + rng.NextDouble() * 1.7))
                {
                    if (side < 0 && z > LaneZ - 2.5f && z < LaneZ + 2.5f) continue;
                    var x = side * (RoadHalf + 0.55f + (float)rng.NextDouble() * 1.1f);
                    var roll = rng.NextDouble();
                    int pose;
                    float yaw;
                    if (roll < 0.52) { pose = 1; yaw = rng.NextDouble() < 0.5 ? 0f : 180f; }      // 歩く
                    else if (roll < 0.70) { pose = 0; yaw = (float)(rng.NextDouble() * 360.0); }  // 立つ
                    else if (roll < 0.84) { pose = 2; yaw = side > 0 ? 90f : -90f; }              // 店先を向いて腕組み
                    else if (roll < 0.93) { pose = 4; yaw = side > 0 ? 90f : -90f; }              // 壁にもたれる
                    else { pose = 3; yaw = (float)(rng.NextDouble() * 360.0); }                   // しゃがむ
                    yaw += (float)(rng.NextDouble() * 30.0 - 15.0);
                    Figure(bank, new Vector3(x, KerbRise, z), yaw, pose, rng);
                }
            }
            // 車道を横切る人も少し入れる。歩道だけだと行列に見える
            for (var z = StreetSouth + 6f; z < StreetNorth - 5f; z += (float)(3.4 + rng.NextDouble() * 4.0))
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0) * (RoadHalf - 0.6f);
                Figure(bank, new Vector3(x, 0f, z), (float)(rng.NextDouble() * 360.0), 1, rng);
            }

            // 小路
            for (var x = LaneWest + 2f; x < -StreetHalf - 1.5f; x += (float)(2.5 + rng.NextDouble() * 2.5))
                Figure(bank, new Vector3(x, 0.02f, LaneZ + (float)(rng.NextDouble() - 0.5) * 1.4f),
                    rng.NextDouble() < 0.5 ? 90f : -90f, 1, rng);

            // ヤード。売り手は出店の奥に座り、買い手は筋を歩く
            var market = GameObject.Find("Alley/Market");
            if (market != null)
            {
                foreach (Transform stall in market.transform)
                {
                    if (!stall.name.StartsWith("Stall")) continue;
                    var p = stall.localPosition;
                    var yaw = stall.localEulerAngles.y;
                    var back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.95f);
                    Figure(bank, new Vector3(p.x + back.x, 0.02f, p.z + back.z), yaw + 180f, 5, rng);
                }
            }
            for (var x = LaneWest - 1.2f; x > MyStallX + 2.2f; x -= (float)(0.9 + rng.NextDouble() * 1.1))
            {
                var lanes = 1 + rng.Next(3);
                for (var i = 0; i < lanes; i++)
                {
                    var z = LaneZ + (float)(rng.NextDouble() * 2.0 - 1.0) * (AisleHalf - 0.35f);
                    var roll = rng.NextDouble();
                    var pose = roll < 0.55 ? 1 : roll < 0.78 ? 0 : roll < 0.9 ? 6 : 3;
                    Figure(bank, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.7f, 0.02f, z),
                        (float)(rng.NextDouble() * 360.0), pose, rng);
                }
            }
            bank.Emit(parent, "Crowd", CrowdMat(), false, Generated);
        }

        /// <summary>
        /// 人ひとり。胴と頭に手足を繋いだだけの形。
        /// pose で関節の角度を変える。0 立つ／1 歩く／2 腕組み／3 しゃがむ／4 もたれる／5 座る／6 覗き込む
        /// </summary>
        static void Figure(Bank b, Vector3 at, float yaw, int pose, System.Random rng)
        {
            var scale = (float)(0.94 + rng.NextDouble() * 0.14);       // 背丈を少し振る
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var lean = 0f;
            var hipY = 0.92f;
            var torsoPitch = 4f;

            float thighL = 2f, thighR = -2f, kneeL = 0f, kneeR = 0f;
            float armL = 8f, armR = -8f, elbowL = 10f, elbowR = 10f;
            float armOutL = 4f, armOutR = -4f;

            switch (pose)
            {
                case 1:  // 歩く
                    thighL = 26f; kneeL = -24f; thighR = -20f; kneeR = 34f;
                    armL = -22f; armR = 24f; torsoPitch = 6f;
                    break;
                case 2:  // 腕組みで立つ
                    armL = 62f; armR = -62f; elbowL = 96f; elbowR = 96f;
                    armOutL = 22f; armOutR = -22f; torsoPitch = 2f;
                    break;
                case 3:  // しゃがむ
                    hipY = 0.54f; thighL = 88f; thighR = 84f; kneeL = -104f; kneeR = -100f;
                    torsoPitch = 22f; armL = 30f; armR = 26f; elbowL = 60f; elbowR = 58f;
                    break;
                case 4:  // 壁にもたれる
                    lean = 11f; thighL = -8f; thighR = -12f; armL = 14f; armR = -16f;
                    torsoPitch = -6f;
                    break;
                case 5:  // 座る（あぐら）
                    hipY = 0.34f; thighL = 86f; thighR = 82f; kneeL = -86f; kneeR = -92f;
                    armOutL = 30f; armOutR = -30f; armL = 44f; armR = 40f; elbowL = 74f; elbowR = 70f;
                    torsoPitch = 8f;
                    break;
                case 6:  // 台を覗き込む
                    hipY = 0.88f; torsoPitch = 38f; thighL = -6f; thighR = 8f;
                    armL = 46f; armR = 42f; elbowL = 34f; elbowR = 30f;
                    break;
            }

            var hip = at + rot * new Vector3(0f, hipY * scale, 0f);
            var tilt = rot * Quaternion.Euler(lean, 0f, 0f);

            // 脚
            var kneeLpos = Limb(b, hip + tilt * new Vector3(-0.10f * scale, 0f, 0f), tilt, thighL, 0f, 0.42f * scale, 0.155f * scale);
            var kneeRpos = Limb(b, hip + tilt * new Vector3(0.10f * scale, 0f, 0f), tilt, thighR, 0f, 0.42f * scale, 0.155f * scale);
            var footL = Limb(b, kneeLpos, tilt, thighL + kneeL, 0f, 0.44f * scale, 0.125f * scale);
            var footR = Limb(b, kneeRpos, tilt, thighR + kneeR, 0f, 0.44f * scale, 0.125f * scale);
            b.Box(footL + new Vector3(0f, 0.035f, 0f) + rot * new Vector3(0f, 0f, 0.07f),
                new Vector3(0.12f * scale, 0.07f * scale, 0.26f * scale), rot);
            b.Box(footR + new Vector3(0f, 0.035f, 0f) + rot * new Vector3(0f, 0f, 0.07f),
                new Vector3(0.12f * scale, 0.07f * scale, 0.26f * scale), rot);

            // 胴と頭
            var body = tilt * Quaternion.Euler(torsoPitch, 0f, 0f);
            var chest = hip + body * new Vector3(0f, 0.34f * scale, 0f);
            b.Box(hip + body * new Vector3(0f, 0.09f * scale, 0f),
                new Vector3(0.33f * scale, 0.20f * scale, 0.22f * scale), body);
            b.Box(hip + body * new Vector3(0f, 0.36f * scale, 0f),
                new Vector3(0.37f * scale, 0.42f * scale, 0.23f * scale), body);
            var neck = hip + body * new Vector3(0f, 0.62f * scale, 0f);
            b.Box(neck, new Vector3(0.11f * scale, 0.09f * scale, 0.11f * scale), body);
            b.Box(neck + body * new Vector3(0f, 0.14f * scale, 0f),
                new Vector3(0.19f * scale, 0.22f * scale, 0.20f * scale),
                body * Quaternion.Euler(0f, (float)(rng.NextDouble() * 36.0 - 18.0), 0f));

            // 腕
            var shL = chest + body * new Vector3(-0.23f * scale, 0.14f * scale, 0f);
            var shR = chest + body * new Vector3(0.23f * scale, 0.14f * scale, 0f);
            var elL = Limb(b, shL, body, armL, armOutL, 0.30f * scale, 0.105f * scale);
            var elR = Limb(b, shR, body, armR, armOutR, 0.30f * scale, 0.105f * scale);
            Limb(b, elL, body, armL + elbowL, armOutL, 0.28f * scale, 0.085f * scale);
            Limb(b, elR, body, armR + elbowR, armOutR, 0.28f * scale, 0.085f * scale);
        }

        /// <summary>手足を 1 本。真下を 0 度として、pitch で前へ、roll で外へ振る</summary>
        static Vector3 Limb(Bank b, Vector3 from, Quaternion frame, float pitch, float roll, float len, float thick)
        {
            var dir = frame * (Quaternion.Euler(pitch, 0f, roll) * Vector3.down);
            var to = from + dir * len;
            b.Box((from + to) * 0.5f, new Vector3(thick, len, thick), Quaternion.FromToRotation(Vector3.down, dir));
            return to;
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
            m.SetColor("_BaseColor", new Color(0.26f, 0.27f, 0.30f, 0.82f));
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
                    if (roll < 0.30) Sacks(sack, new Vector3(x, KerbRise, z), rng);
                    else if (roll < 0.46) Carton(board, new Vector3(x, KerbRise, z), side, rng);
                    else if (roll < 0.60) Pallets(board, new Vector3(x, KerbRise, z), rng);
                    else if (roll < 0.72) Bin(metal, sack, new Vector3(x, KerbRise, z), rng);
                    else if (roll < 0.86) Scatter(board, new Vector3(x, KerbRise, z), 1.5f, rng);
                    else Pipes(metal, new Vector3(x, KerbRise, z), side, rng);
                }
                // 縁石沿いの小物
                for (var z = StreetSouth + 2f; z < StreetNorth - 2f; z += (float)(0.8 + rng.NextDouble() * 1.6))
                    Scatter(board, new Vector3(side * (RoadHalf + 0.18f), KerbRise, z), 0.7f, rng);
            }
            // 大型の塵芥箱
            Skip(metal, new Vector3(StreetHalf - 1.5f, KerbRise, 24.5f), rng);
            Skip(metal, new Vector3(-StreetHalf + 1.6f, KerbRise, 9.0f), rng);

            // 小路とヤード
            for (var x = LaneWest + 1f; x < -StreetHalf - 1f; x += (float)(1.4 + rng.NextDouble() * 1.6))
                Scatter(board, new Vector3(x, 0.02f, LaneZ + (float)(rng.NextDouble() - 0.5) * 2.4f), 1.1f, rng);
            // 入口のすぐ脇には積まない。入った瞬間に奥まで目が通るように
            for (var x = YardWest + 1.2f; x < LaneWest - 3.4f; x += (float)(1.1 + rng.NextDouble() * 1.5))
            {
                for (var i = 0; i < 2; i++)
                {
                    var edge = rng.NextDouble() < 0.5 ? YardSouth + 1.0f : YardNorth - 1.0f;
                    var z = Mathf.Lerp(edge, LaneZ, (float)rng.NextDouble() * 0.55f);
                    if (Mathf.Abs(z - LaneZ) < AisleHalf + 0.75f) continue;
                    var roll = rng.NextDouble();
                    if (roll < 0.34) Sacks(sack, new Vector3(x, 0.02f, z), rng);
                    else if (roll < 0.62) Pallets(board, new Vector3(x, 0.02f, z), rng);
                    else Scatter(board, new Vector3(x, 0.02f, z), 1.3f, rng);
                }
            }
            sack.Emit(parent, "Sacks", Mat("Tarp"), false, Generated);
            board.Emit(parent, "Cartons", Mat("Timber"), false, Generated);
            metal.Emit(parent, "Bins", Mat("Metal"), false, Generated);
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

        /// <summary>蓋つきの塵芥箱</summary>
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

        /// <summary>大型の塵芥箱。通りに 1 つ 2 つあると縮尺が伝わる</summary>
        static void Skip(Bank b, Vector3 at, System.Random rng)
        {
            var yaw = (float)(rng.NextDouble() * 20.0 - 10.0);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            b.Box(at + new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 1.1f, 1.25f), rot);
            b.Box(at + new Vector3(0f, 1.14f, 0f), new Vector3(1.95f, 0.08f, 1.3f), rot);
            for (var i = -1; i <= 1; i += 2)
                b.Box(at + rot * new Vector3(i * 0.95f, 0.55f, 0f), new Vector3(0.09f, 1.1f, 1.28f), rot);
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
            Blocker(parent, "Wall.Head", new Vector3(0f, h * 0.5f, StreetNorth + 0.5f), new Vector3(StreetHalf * 2f + 2f, h, 1f));
            Blocker(parent, "Wall.Back", new Vector3(0f, h * 0.5f, StreetSouth - 0.5f), new Vector3(StreetHalf * 2f + 2f, h, 1f));

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
                case "NeonNerve": return new Color(1.00f, 0.16f, 0.67f);
                case "NeonChiba": return new Color(0.16f, 0.92f, 1.00f);
                case "NeonMemory": return new Color(1.00f, 0.59f, 0.12f);
                case "NeonJack": return new Color(0.27f, 1.00f, 0.47f);
                case "NeonRafu": return new Color(1.00f, 0.24f, 0.24f);
                case "NeonBar": return new Color(0.67f, 0.43f, 1.00f);
                case "NeonNoodle": return new Color(1.00f, 0.86f, 0.24f);
                case "NeonClinic": return new Color(0.35f, 0.78f, 1.00f);
                default: return Color.white;
            }
        }

        /// <summary>縦長の絵か。縦なら高さが幅の倍になる</summary>
        static bool Tall(string texture)
        {
            return texture == "NeonNerve" || texture == "NeonMemory"
                || texture == "NeonRafu" || texture == "NeonBar";
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
                new Plate("NeonNerve",  -1,  1.5f,  4.7f, true,  1.55f),
                new Plate("NeonChiba",   1,  2.4f,  5.6f, false, 1.75f),
                new Plate("NeonRafu",    1,  3.6f, 10.4f, false, 2.35f),
                new Plate("NeonNoodle",  1,  6.2f,  4.0f, true,  1.45f),
                new Plate("NeonBar",    -1,  6.8f,  9.6f, false, 2.10f),
                new Plate("NeonJack",   -1,  8.6f,  4.2f, true,  1.60f),
                new Plate("NeonClinic",  1,  9.8f,  6.4f, false, 1.70f),
                new Plate("NeonMemory", -1, 11.4f,  7.2f, false, 1.55f),
                new Plate("NeonChiba",  -1, 12.6f,  4.1f, true,  1.50f),
                new Plate("NeonBar",     1, 13.4f,  4.5f, true,  1.45f),
                new Plate("NeonNerve",   1, 15.2f,  8.6f, false, 2.20f),
                new Plate("NeonRafu",   -1, 16.6f,  4.4f, true,  1.50f),
                new Plate("NeonNoodle", -1, 18.2f,  7.4f, false, 1.60f),
                new Plate("NeonMemory",  1, 19.0f,  4.2f, true,  1.55f),
                new Plate("NeonJack",    1, 21.6f,  6.8f, false, 1.75f),
                new Plate("NeonClinic", -1, 22.4f,  4.3f, true,  1.55f),
                new Plate("NeonBar",    -1, 24.0f, 10.2f, false, 2.30f),
                new Plate("NeonChiba",   1, 25.2f,  4.1f, true,  1.50f),
                new Plate("NeonNerve",  -1, 27.0f,  4.6f, true,  1.50f),
                new Plate("NeonNoodle",  1, 28.2f,  7.8f, false, 1.65f),
                new Plate("NeonMemory", -1, 29.6f,  9.4f, false, 2.15f),
                new Plate("NeonJack",    1, 30.4f,  4.4f, true,  1.55f),
                new Plate("NeonRafu",    1, 33.0f,  6.2f, false, 1.60f),
                new Plate("NeonClinic",  1, 34.6f,  4.2f, true,  1.60f),
                new Plate("NeonChiba",  -1, 37.2f,  8.2f, false, 1.70f),
                new Plate("NeonBar",     1, 37.8f,  4.5f, true,  1.45f),
                new Plate("NeonNerve",  -1, 39.4f,  4.3f, true,  1.50f),
                new Plate("NeonMemory",  1, 40.6f,  9.0f, false, 2.05f),
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
            go.transform.localRotation = p.blade
                ? Quaternion.identity
                : Quaternion.Euler(0f, p.side > 0 ? 90f : -90f, 0f);
            go.transform.localScale = new Vector3(wide, high, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = NeonMat(p.texture, tex);
            Object.DestroyImmediate(go.GetComponent<Collider>());

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
        /// <summary>自分の露店の場所。いちばん奥</summary>
        public const float MyStallX = -29.3f;

        /// <summary>
        /// ヤードの中身。両脇に出店を詰め、真ん中に人ひとりぶんの筋だけ残す。
        /// 足の置き場もない、という文に合わせて隙間は詰める
        /// </summary>
        static void Market(Transform parent)
        {
            Clear(parent);
            var rng = new System.Random(6100);
            var n = 0;
            for (var s2 = 0; s2 < 2; s2++)
            {
                var sideZ = s2 == 0 ? -1 : 1;
                for (var row = 0; row < 2; row++)
                {
                    var z = LaneZ + sideZ * (AisleHalf + 1.35f + row * 3.1f);
                    for (var x = LaneWest - 4.0f; x > MyStallX + 1.6f; x -= 3.2f)
                    {
                        Stall(parent, "Stall" + n++, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.5f, 0f, z),
                            sideZ > 0 ? 180f : 0f, rng, false);
                    }
                }
            }
            MyStall(Child(parent, "MyStall"));
            Bulbs(Child(parent, "Bulbs"));
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
            var w = mine ? 2.6f : (float)(2.1 + rng.NextDouble() * 0.6);
            var d = mine ? 2.2f : (float)(1.7 + rng.NextDouble() * 0.5);
            var high = mine ? 2.40f : (float)(2.32 + rng.NextDouble() * 0.26);

            for (var i = 0; i < 4; i++)
            {
                var px = (i % 2 == 0 ? -1 : 1) * w * 0.5f;
                var pz = (i < 2 ? -1 : 1) * d * 0.5f;
                Box(t, "Pole" + i, new Vector3(px, high * 0.5f, pz), new Vector3(0.06f, high, 0.06f), "Pole");
            }

            var tarp = mine ? "TarpMine" : "Tarp";
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
            Box(t, "Skirt", new Vector3(0f, th * 0.5f, -d * 0.18f - d * 0.24f), new Vector3(w * 0.88f, th, 0.05f), "Timber");

            var boxes = mine ? 2 : 1 + rng.Next(3);
            for (var i = 0; i < boxes; i++)
            {
                var bw = (float)(0.35 + rng.NextDouble() * 0.2);
                Box(t, "Crate" + i, new Vector3((float)(rng.NextDouble() - 0.5) * w * 0.7f, bw * 0.5f,
                        d * 0.30f + (float)(rng.NextDouble() - 0.5) * 0.3f),
                    new Vector3(bw, bw, bw), "Crate");
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
            Stall(parent, "Stall", new Vector3(MyStallX, 0f, LaneZ), 90f, rng, true);
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
                new Vector3(MyStallX + 2.4f, 3.1f, LaneZ - 0.6f),
                new Vector3(MyStallX + 0.2f, 2.5f, LaneZ + 0.2f),
                new Vector3(LaneWest - 5.4f, 3.2f, LaneZ + 0.8f),
                new Vector3(LaneWest - 11.2f, 3.3f, LaneZ - 1.4f),
                new Vector3(LaneWest - 12.8f, 3.1f, LaneZ + 2.2f),
                new Vector3(MyStallX + 5.0f, 3.2f, LaneZ + 0.4f),
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
                ball.GetComponent<MeshRenderer>().sharedMaterial = GlowMat(new Color(1.00f, 0.80f, 0.52f), 2.2f);
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.00f, 0.79f, 0.52f);
                l.range = 11f;
                l.intensity = 30f;
                l.shadows = LightShadows.None;
            }
        }

        // ---- 表示板 --------------------------------------------------------

        /// <summary>小路の口の表示板と、自分の露店の看板。どちらも光らない板</summary>
        static void Boards(Transform parent)
        {
            Clear(parent);
            Board(parent, "SignYardName", "SignYardName", new Vector3(-StreetHalf + 0.10f, 2.9f, LaneZ - 2.4f),
                Quaternion.Euler(0f, -90f, 0f), new Vector2(1.9f, 0.6f));
            Board(parent, "SignYardName.Lane", "SignYardName", new Vector3(LaneWest + 0.6f, 2.9f, LaneZ - LaneHalf + 0.08f),
                Quaternion.identity, new Vector2(1.9f, 0.6f));
            Board(parent, "SignMemories", "SignMemories", new Vector3(MyStallX - 0.55f, 1.62f, LaneZ),
                Quaternion.Euler(0f, -90f, 0f), new Vector2(1.5f, 0.75f));
        }

        /// <summary>板を 1 枚立てる。読ませるためではなく、そこに何があるかを示すために置く</summary>
        static void Board(Transform parent, string name, string texture, Vector3 at, Quaternion rot, Vector2 size)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + texture + ".png");
            if (tex == null) { Debug.LogWarning("テクスチャが無い: " + texture); return; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = BoardMat(texture, tex);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// 板のマテリアル。灯りを受ける Lit だと絵が出なかったので unlit で貼り、
        /// 明るさは色で落としてある。暗い路地で読める程度に留める
        /// </summary>
        static Material BoardMat(string texture, Texture2D tex)
        {
            var path = Materials + "Board_" + texture + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Unlit")
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Alley"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Alley");
                if (m != null) AssetDatabase.DeleteAsset(path);
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = "Board_" + texture;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
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
            main.maxParticles = 900;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.35f);

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 620f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(17f, 17f, 0.2f);

            // 風。まっすぐ落ちる雨は書き割りに見える
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.55f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

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
            var player = GameObject.Find("Player");
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0f, 0.1f, StreetSouth + 3f);
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

        /// <summary>素材ごとの色と艶。雨に濡れているので路面と敷石だけ強く光らせる</summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                case "Asphalt": col = new Color(0.125f, 0.132f, 0.155f); smooth = 0.72f; break;
                case "Cobble": col = new Color(0.135f, 0.138f, 0.150f); smooth = 0.58f; break;
                case "Brick": col = new Color(0.165f, 0.130f, 0.118f); smooth = 0.18f; break;
                case "Concrete": col = new Color(0.185f, 0.186f, 0.192f); smooth = 0.24f; break;
                case "Metal": col = new Color(0.095f, 0.098f, 0.105f); smooth = 0.46f; break;
                case "Glass": col = new Color(0.030f, 0.036f, 0.048f); smooth = 0.86f; break;
                case "Puddle": col = new Color(0.045f, 0.050f, 0.062f); smooth = 0.96f; break;
                case "Tarp": col = new Color(0.150f, 0.145f, 0.130f); smooth = 0.30f; break;
                case "TarpMine": col = new Color(0.135f, 0.115f, 0.100f); smooth = 0.30f; break;
                case "Timber": col = new Color(0.130f, 0.105f, 0.080f); smooth = 0.15f; break;
                case "Pole": col = new Color(0.090f, 0.090f, 0.095f); smooth = 0.42f; break;
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
