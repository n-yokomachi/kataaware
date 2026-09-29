using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の家具（<see cref="BuildFurniture"/>）の確かめの撮影。再生せずに、Room を開いてプローブのカメラで撮る。
    ///
    /// 家具ごとに、前（保存してある Room の Kenney の物）と後（組んだ物）を同じ向きから撮って横に並べる（新しい物は後だけ）。
    /// 後はもう一枚、撮る間だけ目の脇に灯りを足した絵も撮る（形を読むための絵。ゲームの明るさではない）。
    /// 部屋ぜんたいは、前と後を三つの角と座った目（左と右）から。
    /// プローブのカメラは enabled=false・HideAndDontSave で作り、撮った同じ呼び出しの中で捨てる（RenderTexture は Grab が返す）。
    /// **組んだ Room は保存しない。** 撮り終えたら撮る前の場面へ戻す（組んだ場面は捨てる）。未保存の変更がある場面では撮らない
    /// </summary>
    public static class CheckFurniture
    {
        struct Subject
        {
            public string Name;
            public string[] Before;
            public string[] After;
            public Vector3 Dir;
            public float Fov;

            public Subject(string name, string[] before, string[] after, Vector3 dir, float fov = 46f)
            {
                Name = name;
                Before = before;
                After = after;
                Dir = dir;
                Fov = fov;
            }
        }

        static readonly Subject[] Subjects =
        {
            new Subject("01_sofa", new[] { "Room/Sofa", "Room/Pillow", "Room/Blanket", "Room/Binder" }, new[] { "Room/Sofa" }, new Vector3(1f, 0.55f, -0.3f)),
            new Subject("02_fridge", new[] { "Room/Fridge", "Room/Microwave" }, new[] { "Room/Fridge" }, new Vector3(0.35f, 0.3f, 1f), 40f),
            new Subject("03_kitchen", new[] { "Room/KitchenCabinet", "Room/KitchenSink", "Room/CoffeeMachine" }, new[] { "Room/Kitchen" }, new Vector3(0.1f, 0.55f, 1f), 56f),
            new Subject("04_bookcase", new[] { "Room/Bookcase" }, new[] { "Room/Bookcase" }, new Vector3(0.3f, 0.15f, -1f), 42f),
            new Subject("05_plant", new[] { "Room/PlantTall" }, new[] { "Room/Plant" }, new Vector3(-0.85f, 0.35f, 0.55f), 44f),
            new Subject("06_rug", new[] { "Room/Rug" }, new[] { "Room/Rug", "Room/LowTable" }, new Vector3(0.9f, 1.25f, -0.45f), 50f),
            new Subject("07_boxes_entrance", new[] { "Room/BoxA", "Room/BoxB", "Room/BoxC" }, new[] { "Room/ShoeRack", "Room/UmbrellaStand", "Room/Picture3" }, new Vector3(-0.35f, 0.5f, 1f), 50f),
            new Subject("08_lowtable", null, new[] { "Room/LowTable", "Room/Binder" }, new Vector3(0.8f, 0.9f, -0.5f), 40f),
            new Subject("09_floorlamp", null, new[] { "Room/FloorLamp" }, new Vector3(1f, 0.25f, 0.4f), 44f),
            new Subject("10_aircon", null, new[] { "Room/AirCon" }, new Vector3(1f, -0.25f, -0.35f), 40f),
            new Subject("11_workcounter", null, new[] { "Room/WorkCounter", "Room/Stool", "Room/Bin" }, new Vector3(0.5f, 0.6f, 1f), 56f),
            new Subject("12_serverrack", null, new[] { "Room/ServerRack" }, new Vector3(0.45f, 0.3f, -1f), 44f),
            new Subject("13_chipshelf", null, new[] { "Room/ChipShelf" }, new Vector3(-1f, 0.3f, -0.4f), 42f),
            new Subject("14_bin", null, new[] { "Room/Bin" }, new Vector3(0.3f, 0.8f, 1f), 40f),
            new Subject("15_pictures_sofa_wall", null, new[] { "Room/Picture0", "Room/Picture1", "Room/Picture2" }, new Vector3(1f, 0.05f, 0f), 44f),
            new Subject("16_picture_hammershoi", null, new[] { "Room/Picture3" }, new Vector3(-1f, 0.0f, 0.15f), 36f),
            new Subject("17_umbrella_shoes_close", null, new[] { "Room/UmbrellaStand", "Room/ShoeRack" }, new Vector3(-0.4f, 0.7f, 1f), 40f),
            new Subject("18_coat_rack", new[] { "Room/CoatRack" }, new[] { "Room/CoatRack" }, new Vector3(-1f, 0.2f, 0.6f), 44f),
            new Subject("19_desk", new[] { "Room/Desk", "Room/Keyboard", "Room/Mouse", "Room/Tower" }, new[] { "Room/Desk", "Room/Tower" }, new Vector3(-0.3f, 0.8f, -1f), 56f),
            new Subject("20_side_table", new[] { "Room/SideTable" }, new[] { "Room/SideTable" }, new Vector3(-1f, 0.5f, -0.3f), 44f),
            new Subject("21_kitchen_light", null, new[] { "Room/KitchenLight", "Room/Kitchen" }, new Vector3(0.2f, 0.2f, 1f), 60f),
        };

        /// <summary>部屋ぜんたい。名前・目・見る所・画角</summary>
        static readonly object[][] Overall =
        {
            new object[] { "o1_from_east_window", new Vector3(2.6f, 1.8f, -1.2f), new Vector3(-3.5f, 0.7f, 1.2f), 72f },
            new object[] { "o2_from_ne", new Vector3(2.4f, 1.9f, 1.4f), new Vector3(-3.5f, 0.6f, -0.6f), 72f },
            new object[] { "o3_from_nw", new Vector3(-4.6f, 1.8f, 2.6f), new Vector3(0.5f, 0.6f, -1.0f), 72f },
            new object[] { "o4_from_kitchen", new Vector3(-3.9f, 1.75f, -0.45f), new Vector3(-1.0f, 0.8f, 2.2f), 72f },
            new object[] { "o5_hall_to_entrance", new Vector3(0f, 1.6f, -1.8f), new Vector3(0.2f, 0.7f, -4.9f), 64f },
        };

        /// <summary>家具ひとつを撮る間は、ほかの家具（前の物も後の物も）を写さない。部屋の形・机・椅子などは残す</summary>
        static readonly string[] Furniture =
        {
            "Sofa", "Pillow", "Blanket", "Binder", "Rug", "LowTable", "FloorLamp", "AirCon", "Bookcase", "Books1", "Books2", "Books3", "Books4", "Books5", "Books6",
            "Fridge", "Microwave", "Kitchen", "KitchenCabinet", "KitchenSink", "CoffeeMachine", "Bin", "WorkCounter", "Stool", "ServerRack", "ChipShelf",
            "Plant", "PlantTall", "ShoeRack", "UmbrellaStand", "BoxA", "BoxB", "BoxC", "Picture0", "Picture1", "Picture2", "Picture3",
            "CoatRack", "Desk", "Keyboard", "Mouse", "Tower", "SideTable", "KitchenLight",
        };

        /// <summary>
        /// 保存してある Room（前）を撮り、同じ場面に家具を組んで（<see cref="BuildFurniture.Assemble"/>。保存しない）後を撮る。撮り終えたら組んだ場面は捨てる。
        /// 絵・mesh・マテリアルのアセットは焼き直されたまま残る
        /// </summary>
        public static string Preview(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            var before = new Dictionary<string, Texture2D>();
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var room = EditorSceneManager.OpenScene(BuildFurniture.RoomPath, OpenSceneMode.Single);
                Body(false);
                foreach (var s in Subjects)
                {
                    if (s.Before == null) continue;
                    var shot = Frame(s, s.Before, 0f);
                    if (shot != null) before[s.Name] = shot;
                }
                foreach (var o in Overall) Save(Shoot((Vector3)o[1], (Vector3)o[2], (float)o[3], 0f), Path.Combine(dir, "before_" + o[0] + ".png"));
                Seated(dir, "before", sb);

                sb.AppendLine(BuildFurniture.Assemble(room));
                Body(false);
                foreach (var s in Subjects)
                {
                    var after = Frame(s, s.After, 0f);
                    if (after == null) { sb.AppendLine(s.Name + ": 後の物が無い"); continue; }
                    Texture2D pair;
                    if (before.TryGetValue(s.Name, out pair))
                    {
                        Save(SideBySide(pair, after), Path.Combine(dir, s.Name + "_before_after.png"));
                        Object.DestroyImmediate(after);
                    }
                    else Save(after, Path.Combine(dir, s.Name + "_after.png"));
                    var fill = Frame(s, s.After, 1.4f);
                    if (fill != null) { Save(fill, Path.Combine(dir, s.Name + "_after_fill.png")); Object.DestroyImmediate(fill); }
                    sb.AppendLine("撮った " + s.Name);
                }
                foreach (var o in Overall) Save(Shoot((Vector3)o[1], (Vector3)o[2], (float)o[3], 0f), Path.Combine(dir, "after_" + o[0] + ".png"));
                Seated(dir, "after", sb);
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                foreach (var t in before.Values) if (t != null) Object.DestroyImmediate(t);
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
                // 組んで捨てた場面の一時的な物を片付ける（溜まるとエディタの読み込み直しが遅くなる）
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            File.WriteAllText(Path.Combine(dir, "preview_log.txt"), sb.ToString());
            return sb.ToString().TrimEnd();
        }

        // ---- 組み込んだ後 --------------------------------------------------------------

        /// <summary>組み込んで保存した後の撮る所。名前・目・見る所・画角（0 なら目のカメラのまま）</summary>
        static readonly object[][] FinalViews =
        {
            new object[] { "f01_hall_mouth_to_ldk", new Vector3(0.0f, 1.6f, -1.3f), new Vector3(-2.2f, 0.9f, 1.2f), 70f },
            new object[] { "f02_kitchen_side_to_living", new Vector3(-4.2f, 1.6f, -0.5f), new Vector3(-3.6f, 0.9f, 2.2f), 70f },
            new object[] { "f03_living_to_work", new Vector3(-3.8f, 1.6f, 2.2f), new Vector3(1.6f, 0.9f, 1.6f), 70f },
            new object[] { "f04_entrance_to_hall", new Vector3(-0.25f, 1.6f, -4.75f), new Vector3(0.4f, 1.2f, -3.2f), 64f },
            new object[] { "f04b_entrance_shoes_umbrella", new Vector3(-0.35f, 1.55f, -3.3f), new Vector3(0.35f, 0.5f, -4.6f), 64f },
            new object[] { "f06_sofa_wall_pictures", new Vector3(-2.3f, 1.45f, 1.7f), new Vector3(-4.9f, 1.45f, 1.7f), 60f },
            new object[] { "f07_kitchen_light_and_worktop", new Vector3(-1.3f, 1.7f, 0.9f), new Vector3(-2.6f, 1.1f, -1.1f), 64f },
            new object[] { "f07b_kitchen_ceiling_light", new Vector3(-0.9f, 1.5f, 1.3f), new Vector3(-2.6f, 2.2f, -0.8f), 70f },
            new object[] { "f08_black_furniture_overall", new Vector3(2.4f, 1.9f, 1.4f), new Vector3(-3.5f, 0.6f, -0.6f), 72f },
            new object[] { "f09_coat_rack", new Vector3(-0.3f, 1.5f, -0.2f), new Vector3(0.95f, 1.0f, -1.05f), 50f },
            new object[] { "f10a_desk_from_behind_chair", new Vector3(1.5f, 1.75f, 0.2f), new Vector3(1.7f, 0.6f, 2.2f), 62f },
            new object[] { "f10b_desk_and_side_table", new Vector3(0.3f, 1.5f, 1.0f), new Vector3(2.4f, 0.6f, 1.8f), 62f },
            new object[] { "f11_low_table", new Vector3(-2.4f, 1.3f, 0.9f), new Vector3(-3.3f, 0.4f, 1.6f), 56f },
        };

        /// <summary>
        /// 組み込んで保存した Room と Connect を撮る（場面は書き換えない。主人公の体は写さない）。Room は上の所と座った目、Connect はジャケットが掛かったコート掛け
        /// </summary>
        public static string Final(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(BuildFurniture.RoomPath, OpenSceneMode.Single);
                Seated(dir, "f05", sb);
                Body(false);
                foreach (var v in FinalViews)
                {
                    Save(Shoot((Vector3)v[1], (Vector3)v[2], (float)v[3], 0f), Path.Combine(dir, v[0] + ".png"));
                    sb.AppendLine("撮った " + v[0]);
                }
                EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
                Body(false);
                Save(Shoot(new Vector3(0f, 1.6f, -4.3f), new Vector3(0.6f, 1.1f, -1.2f), 64f, 0f), Path.Combine(dir, "f13_connect_start_to_hall_mouth.png"));
                // ジャケットがコート掛けに掛かっている場面（場面 3 では掛けた後、場面 5・7 は掛かったまま始まる）
                EditorSceneManager.OpenScene(BuildNotice.ScenePath, OpenSceneMode.Single);
                Body(false);
                Save(Shoot(new Vector3(-0.3f, 1.5f, -0.2f), new Vector3(0.95f, 1.0f, -1.05f), 50f, 0f), Path.Combine(dir, "f12_coat_rack_with_jacket.png"));
                sb.AppendLine("撮った 場面 3 の廊下と、場面 7 のジャケットの掛かったコート掛け");
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            return sb.ToString().TrimEnd();
        }

        // ---- 歩ける所 ------------------------------------------------------------------

        /// <summary>体の当たり（CharacterController と同じ半径と、段を越える高さ）</summary>
        const float BodyRadius = 0.3f;
        const float StepOver = 0.1f;
        const float Cell = 0.05f;

        /// <summary>
        /// 保存した場面（場面 1 と場面 3）で、体が歩ける所を升目で調べ、始まりの立ち位置から行ける升を塗り分けて、歩いて調べる物に届くかを数える。
        /// 場面 1 は立ち上がった後（椅子を押し下げた形）の立ち位置から。場面は書き換えて捨てる（保存しない）
        /// </summary>
        public static string Walk(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は調べない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(BuildFurniture.RoomPath, OpenSceneMode.Single);
                var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
                var fso = new SerializedObject(flow);
                var spot = (Transform)fso.FindProperty("standSpot").objectReferenceValue;
                var push = fso.FindProperty("chairPushBack").floatValue;
                var chair = GameObject.Find("Room/Chair");
                if (chair != null) chair.transform.position -= chair.transform.forward * push;
                sb.AppendLine(WalkOne(dir, "room", spot != null ? spot.position : new Vector3(1.5f, 0.05f, 1.68f)));
                EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
                sb.AppendLine(WalkOne(dir, "connect", new Vector3(RoomPlan.EntranceStand.x, 0.05f, RoomPlan.EntranceStand.y)));
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                TitleShots.Back(setup);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            File.WriteAllText(Path.Combine(dir, "walk_log.txt"), sb.ToString());
            return sb.ToString().TrimEnd();
        }

        static string WalkOne(string dir, string tag, Vector3 start)
        {
            Physics.SyncTransforms();
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            var root = player != null ? player.transform : null;
            float x0 = -5.2f, x1 = 3.2f, z0 = -5.3f, z1 = 3.2f;
            var nx = Mathf.CeilToInt((x1 - x0) / Cell);
            var nz = Mathf.CeilToInt((z1 - z0) / Cell);
            var free = new bool[nx, nz];
            var buffer = new Collider[16];
            for (var i = 0; i < nx; i++)
                for (var j = 0; j < nz; j++)
                {
                    var p = new Vector3(x0 + (i + 0.5f) * Cell, 0f, z0 + (j + 0.5f) * Cell);
                    var n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * (StepOver + BodyRadius), p + Vector3.up * 1.5f, BodyRadius, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    var hit = false;
                    for (var k = 0; k < n; k++)
                    {
                        if (root != null && buffer[k].transform.IsChildOf(root)) continue;
                        hit = true;
                        break;
                    }
                    // 床の上に居るか（壁の外や閉じた部屋の中を外す）
                    free[i, j] = !hit && RoomPlan.Open(new Vector2(p.x, p.z), 0f);
                }
            var si = Mathf.Clamp(Mathf.FloorToInt((start.x - x0) / Cell), 0, nx - 1);
            var sj = Mathf.Clamp(Mathf.FloorToInt((start.z - z0) / Cell), 0, nz - 1);
            var sb = new StringBuilder();
            if (!free[si, sj])
            {
                // 立ち位置の升が塞がっていれば、いちばん近い空いた升から（塞がっていることは書く）
                var best = float.MaxValue;
                int bi = si, bj = sj;
                for (var i = 0; i < nx; i++)
                    for (var j = 0; j < nz; j++)
                    {
                        if (!free[i, j]) continue;
                        var d = (i - si) * (i - si) + (j - sj) * (j - sj);
                        if (d < best) { best = d; bi = i; bj = j; }
                    }
                sb.AppendFormat("{0}: 始まりの立ち位置 {1} の升は体の当たりが掛かる（いちばん近い空いた升まで {2:0.00} m）", tag, start.ToString("F2"), Mathf.Sqrt(best) * Cell).AppendLine();
                si = bi; sj = bj;
            }
            var reach = new bool[nx, nz];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(si, sj));
            reach[si, sj] = true;
            var count = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                count++;
                foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                {
                    var q = c + d;
                    if (q.x < 0 || q.y < 0 || q.x >= nx || q.y >= nz || reach[q.x, q.y] || !free[q.x, q.y]) continue;
                    reach[q.x, q.y] = true;
                    queue.Enqueue(q);
                }
            }
            sb.AppendFormat("{0}: 行ける升 {1}（{2:0.0} m²）", tag, count, count * Cell * Cell).AppendLine();
            var targets = new List<Vector3>();
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root != null && it.transform.IsChildOf(root)) continue;
                var nearest = float.MaxValue;
                for (var i = 0; i < nx; i++)
                    for (var j = 0; j < nz; j++)
                    {
                        if (!reach[i, j]) continue;
                        var eye = new Vector3(x0 + (i + 0.5f) * Cell, 1.6f, z0 + (j + 0.5f) * Cell);
                        nearest = Mathf.Min(nearest, Vector3.Distance(eye, it.transform.position));
                    }
                sb.AppendFormat("  {0} {1}: 行ける所の目から最も近くて {2:0.00} m（拾える半径 {3:0.0}）{4}", it.Id, it.transform.position.ToString("F2"), nearest, it.Radius, nearest <= it.Radius * 0.9f ? "" : " ← 届かない").AppendLine();
                targets.Add(it.transform.position);
            }
            // 上から見た図（行ける所は緑、歩けるが行けない所は赤、当たりは暗い、対象は黄）
            const int scale = 4;
            var tex = new Texture2D(nx * scale, nz * scale, TextureFormat.RGBA32, false);
            var px = new Color32[tex.width * tex.height];
            for (var i = 0; i < nx; i++)
                for (var j = 0; j < nz; j++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) * Cell, z0 + (j + 0.5f) * Cell);
                    Color32 c = reach[i, j] ? new Color32(70, 150, 80, 255) : free[i, j] ? new Color32(170, 60, 60, 255)
                        : RoomPlan.Open(p, 0f) ? new Color32(40, 40, 48, 255) : new Color32(12, 12, 14, 255);
                    for (var a = 0; a < scale; a++)
                        for (var b = 0; b < scale; b++)
                            px[(j * scale + b) * tex.width + i * scale + a] = c;
                }
            foreach (var t in targets)
            {
                var i = Mathf.FloorToInt((t.x - x0) / Cell) * scale;
                var j = Mathf.FloorToInt((t.z - z0) / Cell) * scale;
                for (var a = -3; a <= 3; a++)
                    for (var b = -3; b <= 3; b++)
                    {
                        var x = i + a;
                        var y = j + b;
                        if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) px[y * tex.width + x] = new Color32(240, 210, 60, 255);
                    }
            }
            tex.SetPixels32(px);
            tex.Apply();
            CheckDiveSky.Save(tex, Path.Combine(dir, "walk_" + tag + ".png"));
            Object.DestroyImmediate(tex);
            return sb.ToString().TrimEnd();
        }

        /// <summary>座った目（場面 1 の始まりの所）から、左と右へ首を振った二枚</summary>
        static void Seated(string dir, string tag, StringBuilder log)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            var player = flow != null ? flow.Player : null;
            if (player == null) { log.AppendLine("主人公が無い"); return; }
            var eye = new SerializedObject(flow).FindProperty("seatEyeHeight").floatValue;
            var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
            var foot = player.transform.position;
            var body = player.transform.eulerAngles.y;
            foreach (var head in new[] { -60f, 60f })
            {
                var rot = Quaternion.Euler(0f, body, 0f);
                var at = foot + rot * new Vector3(0f, eye, lead);
                var look = at + rot * Quaternion.Euler(8f, head, 0f) * Vector3.forward;
                Save(Shoot(at, look, 0f, 0f), Path.Combine(dir, tag + "_seated_" + (head < 0f ? "left" : "right") + ".png"));
            }
        }

        /// <summary>物の並び（道筋）を囲む箱を、dir の向きから画角いっぱいに撮る。物が一つも無ければ null</summary>
        static Texture2D Frame(Subject s, string[] paths, float fill)
        {
            var hidden = new List<Renderer>();
            foreach (var name in Furniture)
            {
                if (Array.IndexOf(paths, "Room/" + name) >= 0) continue;
                var go = GameObject.Find("Room/" + name);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }
            try
            {
                return Frame(s, paths, fill, true);
            }
            finally
            {
                foreach (var r in hidden) r.enabled = true;
            }
        }

        static Texture2D Frame(Subject s, string[] paths, float fill, bool alone)
        {
            var any = false;
            var b = new Bounds();
            foreach (var p in paths)
            {
                var go = GameObject.Find(p);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            if (!any) return null;
            // 天井へ上る煙突などで箱が縦に伸びすぎないよう、撮る広さは床から 2.3 m までに
            if (b.max.y > 2.3f && b.min.y < 1.5f) b.SetMinMax(b.min, new Vector3(b.max.x, 2.3f, b.max.z));
            var radius = b.extents.magnitude;
            var dist = radius / Mathf.Sin(s.Fov * 0.5f * Mathf.Deg2Rad) * 0.92f;
            var eye = b.center + s.Dir.normalized * dist;
            // 部屋の中に収める（壁の外から撮らない）
            // 見る物の居る区画（LDK か廊下）の壁の面の内に
            var inHall = b.center.z < RoomPlan.Ldk.yMin;
            var area = inHall ? RoomPlan.Hall : RoomPlan.Ldk;
            var inset = RoomPlan.Wall * 0.5f + 0.1f;
            eye.x = Mathf.Clamp(eye.x, area.xMin + inset, area.xMax - inset);
            eye.z = Mathf.Clamp(eye.z, area.yMin + inset, area.yMax - inset);
            eye.y = Mathf.Clamp(eye.y, 0.15f, RoomPlan.Ceiling - 0.1f);
            return Shoot(eye, b.center, s.Fov, fill);
        }

        /// <summary>
        /// 主人公の目のカメラを写したプローブで、eye から look へ向けて撮る。fov が 0 なら目のカメラのまま。
        /// fill が 0 より大きければ、撮る間だけ目の脇に灯りを置く
        /// </summary>
        static Texture2D Shoot(Vector3 eye, Vector3 look, float fov, float fill)
        {
            var main = TitleShots.Main();
            if (main == null) return null;
            var go = new GameObject("FurnitureProbe") { hideFlags = HideFlags.HideAndDontSave };
            GameObject lamp = null;
            try
            {
                if (fill > 0f)
                {
                    lamp = new GameObject("FurnitureProbeFill") { hideFlags = HideFlags.HideAndDontSave };
                    var l = lamp.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 5f;
                    l.intensity = fill;
                    l.color = new Color(0.95f, 0.93f, 1f);
                    l.shadows = LightShadows.None;
                    lamp.transform.position = eye + Vector3.up * 0.4f + Vector3.Cross(Vector3.up, look - eye).normalized * 0.3f;
                }
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.nearClipPlane = 0.02f;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (fov > 0f) cam.fieldOfView = fov;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                return TitleShots.Steady(cam);
            }
            finally
            {
                if (lamp != null) Object.DestroyImmediate(lamp);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>二枚を横に並べる（左が前、右が後）</summary>
        static Texture2D SideBySide(Texture2D a, Texture2D b)
        {
            var o = new Texture2D(a.width + b.width + 8, Mathf.Max(a.height, b.height), TextureFormat.RGBA32, false, false);
            var px = new Color32[o.width * o.height];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            o.SetPixels32(px);
            o.SetPixels32(0, 0, a.width, a.height, a.GetPixels32());
            o.SetPixels32(a.width + 8, 0, b.width, b.height, b.GetPixels32());
            o.Apply();
            return o;
        }

        static void Save(Texture2D shot, string path)
        {
            if (shot == null) return;
            CheckDiveSky.Save(shot, path);
            Object.DestroyImmediate(shot);
        }

        /// <summary>主人公の体とジャックのケーブルを写すか（座った形のまま置いてあるので、外から撮る時は写さない）</summary>
        static void Body(bool on)
        {
            var pro = GameObject.Find("Player/Protagonist");
            if (pro != null) foreach (var r in pro.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            var cable = GameObject.Find("Room/Chair/Cable");
            if (cable != null) foreach (var r in cable.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }
    }
}
