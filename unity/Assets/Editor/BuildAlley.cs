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
            Prune(root, new[] { "Shell", "Fixtures", "Lamps", "Puddles", "Neon", "Market", "Boards", "Litter", "Crowd", "Backdrop", "Sky", "Bounds" });
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
            YardFacades(brick, stone, metal, glass, warm, cold);
            SouthEnd(brick, stone, metal);
            Walkway(metal, stone, -13.5f, 6.4f);
            Walkway(metal, stone, 26.5f, 7.1f);

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

            if (u.ground == 0)
            {
                Shopfront(stone, metal, warm, wx, back, z0, z1, shopY0, shopY1, inward);
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

        /// <summary>ヤード。敷石を敷き、四方の囲いは別に組む</summary>
        static void YardShell(Bank brick, Bank stone, Bank paving)
        {
            paving.FaceY(0.02f, YardWest, LaneWest, YardSouth, YardNorth, 1);
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

            // 通り。立ち止まっている人を並べ、ときどき二人組で向かい合わせる
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1 : 1;
                for (var z = WalkSouth + 2f; z < StreetNorth - 3f; z += (float)(2.3 + rng.NextDouble() * 2.8))
                {
                    if (side < 0 && z > LaneZ - 2.5f && z < LaneZ + 2.5f) continue;
                    var x = side * (RoadHalf + 0.72f + (float)rng.NextDouble() * 0.75f);
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
                Put(spots, new Vector3(x, 0.02f, LaneZ + (float)(rng.NextDouble() - 0.5) * 1.2f),
                    rng.NextDouble() < 0.5 ? 90f : -90f, StandPose(rng), rng);

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
                    var back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 0.6f, 0f, 0.62f);
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

            Bake(parent, spots, rng);
        }

        /// <summary>立ち姿を 4 つから選ぶ。同じ形が並ばないように</summary>
        static int StandPose(System.Random rng)
        {
            var r = rng.NextDouble();
            return r < 0.30 ? 0 : r < 0.56 ? 1 : r < 0.78 ? 3 : 4;
        }

        /// <summary>空いていれば立たせる。塞がっていれば諦める</summary>
        static void Put(List<Spot> spots, Vector3 at, float yaw, int pose, System.Random rng, bool force = false)
        {
            if (!force && !Free(at.x, at.z, 0.36f)) return;
            if (!force) Occupy(at.x, at.z, 0.30f);
            spots.Add(Spot1(at, yaw, pose, rng));
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
            Debug.Log("人 " + spots.Count + " 体、" + (tris.Count / 3) + " ポリゴン");
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>姿勢ごとの腰の下がり。座りとしゃがみは骨を曲げるだけでは沈まない</summary>
        static float PoseDrop(int pose)
        {
            switch (pose)
            {
                case 5: return 0.60f;
                case 6: return 0.58f;
                case 7: return 0.52f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 骨を曲げて姿勢を作る。安静へ戻してから重ねるので、同じ体を何度でも使い回せる。
        /// 0 立つ／1 歩く／2 腕組み／3 しゃがむ／4 もたれる／5 座る／6 覗き込む
        /// </summary>
        static void Pose(Transform root, Dictionary<Transform, Quaternion> rest, int pose)
        {
            foreach (var pair in rest) pair.Key.localRotation = pair.Value;

            float thighL = 2f, thighR = -2f, kneeL = 0f, kneeR = 0f;
            float armL = 6f, armR = -6f, elbowL = 12f, elbowR = 12f;
            float outL = 4f, outR = -4f, spine = 2f, lean = 0f;

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
                case 5:  // 座る
                    thighL = 80f; thighR = 76f; kneeL = -96f; kneeR = -100f;
                    outL = 16f; outR = -16f; armL = 34f; armR = 30f; elbowL = 56f; elbowR = 52f;
                    spine = 6f;
                    break;
                case 6:  // 台に手をついて座る
                    thighL = 78f; thighR = 82f; kneeL = -100f; kneeR = -94f;
                    outL = 20f; outR = -12f; armL = 18f; armR = 52f; elbowL = 30f; elbowR = 72f;
                    spine = 10f;
                    break;
                case 7:  // 膝を立てて座る
                    thighL = 88f; thighR = 70f; kneeL = -128f; kneeR = -88f;
                    outL = 10f; outR = -18f; armL = 46f; armR = 26f; elbowL = 64f; elbowR = 44f;
                    spine = 12f;
                    break;
            }

            Turn(root, "Hips", lean, 0f);
            Turn(root, "Abdomen", spine * 0.45f, 0f);
            Turn(root, "Torso", spine * 0.35f, 0f);
            Turn(root, "Chest", spine * 0.20f, 0f);
            Turn(root, "Neck", -spine * 0.35f, 0f);
            Turn(root, "Head", -spine * 0.25f + (float)0f, 0f);

            Turn(root, "UpperLeg.L", thighL, outL * 0.25f);
            Turn(root, "UpperLeg.R", thighR, outR * 0.25f);
            Turn(root, "LowerLeg.L", kneeL, 0f);
            Turn(root, "LowerLeg.R", kneeR, 0f);

            Turn(root, "UpperArm.L", armL, outL);
            Turn(root, "UpperArm.R", armR, outR);
            Turn(root, "LowerArm.L", elbowL, 0f);
            Turn(root, "LowerArm.R", elbowR, 0f);
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
            Taken.Clear();
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
            // 大型の塵芥箱
            Skip(metal, new Vector3(StreetHalf - 1.5f, KerbRise, 24.5f), rng);
            Occupy(StreetHalf - 1.5f, 24.5f, 1.5f);
            Skip(metal, new Vector3(-StreetHalf + 1.6f, KerbRise, 9.0f), rng);
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

        /// <summary>遠景の板。撮った絵が無ければ置かない</summary>
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Backdrop";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, AnchorY, BackdropZ);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var m = BackdropMat(tex);
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
            t.position = new Vector3(0f, AnchorY, StreetNorth - 2.5f);
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
            Blocker(parent, "Wall.Head", new Vector3(0f, h * 0.5f, StreetNorth + 0.5f), new Vector3(StreetHalf * 2f + 2f, h, 1f));
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
            for (var s2 = 0; s2 < 2; s2++)
            {
                var sideZ = s2 == 0 ? -1 : 1;
                for (var row = 0; row < 2; row++)
                {
                    var z = LaneZ + sideZ * (AisleHalf + 1.35f + row * 3.1f);
                    for (var x = LaneWest - 4.0f; x > YardWest + 2.0f; x -= 3.2f)
                    {
                        var px = x + (float)(rng.NextDouble() - 0.5) * 0.5f;
                        // 自分の店の場所は空けておく
                        var gap = new Vector2(px - MyStallX, z - MyStallZ);
                        if (gap.magnitude < 3.2f) continue;
                        Stall(parent, "Stall" + n++, new Vector3(px, 0f, z), sideZ > 0 ? 180f : 0f, rng, false);
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
            var w = mine ? 2.6f : (float)(2.1 + rng.NextDouble() * 0.6);
            var d = mine ? 2.2f : (float)(1.7 + rng.NextDouble() * 0.5);
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


        /// <summary>
        /// ヤードを囲む建物の裏の顔。通りに面した表と違って店先は無く、
        /// 窓と樋と物干しと外づけの階段が雑然と並ぶ。中庭はこの壁で出来ている
        /// </summary>
        static void YardFacades(Bank brick, Bank stone, Bank metal, Bank glass, Bank warm, Bank cold)
        {
            var rng = new System.Random(6210);
            // 西の壁。奥に立ちはだかる
            YardWallX(brick, stone, metal, glass, warm, cold, YardWest, 1, YardSouth, YardNorth, rng);
            // 東の壁は小路の口ぶんだけ切れている
            YardWallX(brick, stone, metal, glass, warm, cold, LaneWest, -1, YardSouth, LaneZ - LaneHalf, rng);
            YardWallX(brick, stone, metal, glass, warm, cold, LaneWest, -1, LaneZ + LaneHalf, YardNorth, rng);
            // 南と北
            YardWallZ(brick, stone, metal, glass, warm, cold, YardSouth, 1, YardWest, LaneWest, rng);
            YardWallZ(brick, stone, metal, glass, warm, cold, YardNorth, -1, YardWest, LaneWest, rng);
        }

        /// <summary>x が一定の囲い壁。窓を彫り、樋と物干しを掛ける</summary>
        static void YardWallX(Bank brick, Bank stone, Bank metal, Bank glass, Bank warm, Bank cold,
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
                var lit = rng.NextDouble();
                var bank = lit < 0.34 ? warm : lit < 0.48 ? cold : glass;
                bank.FaceX(back, o.x, o.y, o.z, o.w, inward);
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
        static void YardWallZ(Bank brick, Bank stone, Bank metal, Bank glass, Bank warm, Bank cold,
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
                var lit = rng.NextDouble();
                var bank = lit < 0.34 ? warm : lit < 0.48 ? cold : glass;
                bank.FaceZ(back, o.x, o.y, o.z, o.w, inward);
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
        /// 出店に並ぶ品。何を売っているかは読ませない。
        /// 色の違う小物が雑多に載っていれば、市が立っていることは伝わる
        /// </summary>
        static void Goods(Transform parent)
        {
            Clear(parent);
            var banks = new Bank[GoodsTint.Length];
            for (var i = 0; i < banks.Length; i++) banks[i] = new Bank { Texel = 1.2f };
            var rng = new System.Random(8899);
            var market = GameObject.Find("Alley/Market");
            if (market == null) return;
            foreach (Transform stall in market.transform)
            {
                Transform body = stall.name.StartsWith("Stall") ? stall : stall.Find("Stall");
                if (body == null) continue;
                var rot = Quaternion.Euler(0f, body.localEulerAngles.y, 0f);
                var at = body.localPosition;
                var n = 5 + rng.Next(7);
                for (var i = 0; i < n; i++)
                {
                    var b = banks[rng.Next(banks.Length)];
                    var local = new Vector3((float)(rng.NextDouble() - 0.5) * 1.7f, 0f, (float)(rng.NextDouble() - 0.5) * 0.55f - 0.35f);
                    var kind = rng.NextDouble();
                    var p = at + rot * local;
                    if (kind < 0.42)
                    {
                        var w = (float)(0.09 + rng.NextDouble() * 0.13);
                        b.Box(p + new Vector3(0f, 0.81f + w * 0.5f, 0f), new Vector3(w, w * 0.8f, w * 0.75f),
                            rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 50.0 - 25.0), 0f));
                    }
                    else if (kind < 0.70)
                    {
                        var h = (float)(0.11 + rng.NextDouble() * 0.12);
                        b.Box(p + new Vector3(0f, 0.81f + h * 0.5f, 0f), new Vector3(0.07f, h, 0.07f),
                            rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 60.0), 0f));
                    }
                    else if (kind < 0.88)
                    {
                        b.Box(p + new Vector3(0f, 0.83f, 0f), new Vector3(0.20f, 0.04f, 0.15f),
                            rot * Quaternion.Euler(0f, (float)(rng.NextDouble() * 70.0 - 35.0), 0f));
                    }
                    else
                    {
                        // 吊るした布。タープの縁から下がる
                        var len = (float)(0.35 + rng.NextDouble() * 0.5);
                        b.Box(at + rot * new Vector3((float)(rng.NextDouble() - 0.5) * 2.0f, 2.05f - len * 0.5f, -1.05f),
                            new Vector3(0.24f, len, 0.04f), rot);
                    }
                }
            }
            for (var i = 0; i < banks.Length; i++)
                banks[i].Emit(parent, "Goods" + i, Tinted("Goods" + i, GoodsTint[i]), false, Generated);
        }

        /// <summary>品の色。極彩色の通りに合わせて、市も色で埋める</summary>
        static readonly Color[] GoodsTint =
        {
            new Color(0.44f, 0.12f, 0.14f), new Color(0.12f, 0.31f, 0.41f),
            new Color(0.48f, 0.37f, 0.12f), new Color(0.16f, 0.34f, 0.21f),
            new Color(0.33f, 0.16f, 0.38f), new Color(0.52f, 0.44f, 0.36f),
        };

        /// <summary>色だけ変えた艶消しのマテリアル</summary>
        static Material Tinted(string name, Color col)
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
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 表示板 --------------------------------------------------------

        /// <summary>小路の口の表示板と、自分の露店の看板。どちらも光らない板</summary>
        static void Boards(Transform parent)
        {
            Clear(parent);
            Board(parent, "SignYardName", "SignYardName", new Vector3(-StreetHalf + 0.10f, 2.9f, LaneZ - 2.4f),
                Quaternion.Euler(0f, 90f, 0f), new Vector2(1.9f, 0.6f));
            Board(parent, "SignYardName.Lane", "SignYardName", new Vector3(LaneWest + 0.6f, 2.9f, LaneZ - LaneHalf + 0.08f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(1.9f, 0.6f));
            var face = Quaternion.Euler(0f, MyStallYaw, 0f);
            var front = face * new Vector3(0f, 0f, -1f);
            Board(parent, "SignMemories", "SignMemories",
                new Vector3(MyStallX, 1.64f, MyStallZ) + front * 1.12f,
                Quaternion.Euler(0f, MyStallYaw, 0f), new Vector2(1.5f, 0.75f));
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
            // 3 軸とも同じ指定の仕方で揃える。片方だけ範囲指定だと弾かれる
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            // 屋根の下では降らせない。粒に当たりを持たせると重いので、覆いを箱で持つ
            var cover = go.AddComponent<RainCover>();
            var cso = new SerializedObject(cover);
            var boxes = cso.FindProperty("shelters");
            boxes.arraySize = 2;
            boxes.GetArrayElementAtIndex(0).boundsValue = new Bounds(
                new Vector3((LaneWest - StreetHalf) * 0.5f, 3f, LaneZ),
                new Vector3(-StreetHalf - LaneWest + 1.2f, 6f, LaneHalf * 2f + 0.6f));
            boxes.GetArrayElementAtIndex(1).boundsValue = new Bounds(
                new Vector3(MyStallX, 1.2f, MyStallZ), new Vector3(3.0f, 2.4f, 3.0f));
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
            a.volume = 0.16f;
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
                case "TarpRed": col = new Color(0.290f, 0.105f, 0.095f); smooth = 0.28f; break;
                case "TarpGreen": col = new Color(0.105f, 0.215f, 0.135f); smooth = 0.28f; break;
                case "TarpBlue": col = new Color(0.095f, 0.150f, 0.260f); smooth = 0.28f; break;
                case "TarpOchre": col = new Color(0.290f, 0.215f, 0.090f); smooth = 0.28f; break;
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
