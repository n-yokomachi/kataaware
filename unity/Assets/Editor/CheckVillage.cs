using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 村と庭の確かめ（設計書 6 節）。ゲームと同じ見え方で撮り、一度に見える範囲の重さを量る。
    ///
    /// 撮り方は場面 4 の <c>CheckDivePeople.Game</c> に倣う。Player/Main Camera を写したカメラ
    /// （後処理を通す）で 960×540 に撮ると、パイプラインの render scale 1/3 で中は 320×180 になる。
    /// カメラは <c>enabled = false</c>・<c>HideAndDontSave</c> で作って <c>Render()</c> で撮り、撮り終えたら捨てる。
    ///
    /// 目の位置と向きは <see cref="Views"/> に持つ。設計書 6 節の 1〜9 と、路地から畑と丘を見た所
    /// </summary>
    public static class CheckVillage
    {
        /// <summary>撮る所。名前・目・向き（+z が 0 度）・俯き（下が正）</summary>
        public struct View
        {
            public string Name;
            public Vector3 Eye;
            public float Yaw;
            public float Pitch;

            public View(string name, Vector3 eye, float yaw, float pitch)
            {
                Name = name;
                Eye = eye;
                Yaw = yaw;
                Pitch = pitch;
            }
        }

        /// <summary>設計書 6 節の 1〜9 と、路地から畑と丘を見た所</summary>
        public static View[] Views()
        {
            return new[]
            {
                // 1. 車の着く所から、村と路地を（車を降りてすぐの Player の目）。
                // 1b は少し歩いて振り返り、路肩に寄せて停めた車と麦畑の未舗装路（場面 8 の最後の帯）を
                new View("1_arrive", BuildVillage.ArriveAt + new Vector3(0.22f, 1.6f, -0.03f), BuildVillage.ArriveYaw, 3f),
                new View("1b_back", new Vector3(-69.5f, 1.6f, -0.6f), 262f, 3f),
                // 2. 路地の途中。家 A の前から東へ。電話ボックスと家 B の茅葺きが見える
                new View("2_lane", new Vector3(-44f, 1.6f, 0.4f), 80f, 2f),
                // 3. 家 B と片割れの家のあいだ越しに、片割れの裏庭の白いパラソル
                new View("3_parasol", new Vector3(-12.5f, 1.6f, 1.6f), 34f, -1f),
                // 路地から畑と丘を。南は農場の門の向こうの菜園（2026-09-27 に麦畑から替えた）、北は車の着く所の塀の向こう
                new View("fields_south", new Vector3(-28.7f, 1.6f, -1.2f), 190f, 1f),
                new View("fields_north", new Vector3(-58f, 1.6f, 1.6f), 330f, 1f),
                // 公衆電話の後ろの菜園（BuildVillageAllotment）。電話ボックスの東の路肩から、ボックスを右に菜園を左に見る所と、
                // 電話ボックスの脇で低い生け垣越しに畝を見下ろす所
                new View("veg_phone", new Vector3(-31.0f, 1.6f, -1.6f), 225f, 8f),
                new View("veg_hedge", new Vector3(-33.6f, 1.6f, -2.9f), 200f, 10f),
                // 村の教会（設計書 7 節）。路地の途中、片割れの家の前、囲いの壁の手前（いちばん近づいた所）から
                new View("church", new Vector3(-30f, 1.6f, 0.3f), 88f, -4f),
                new View("church_front", new Vector3(2.5f, 1.6f, 1.6f), 90f, -8f),
                new View("church_near", new Vector3(17.2f, 1.6f, 0f), 90f, -12f),
                // 空。路地の真ん中から東を見上げる
                new View("sky", new Vector3(-30f, 1.6f, 0f), 60f, -30f),
                // 格子戸を内から見る所と、テラスの卓と椅子の寄り
                new View("gate", new Vector3(-4.2f, 1.6f, 9.4f), 0f, 8f),
                new View("table", new Vector3(-0.2f, 1.45f, 19.3f), 225f, 22f),
                // 4. 閉じた格子戸を、脇の小路の外から
                new View("4_gate", new Vector3(BuildVillage.SidePathX + 0.3f, 1.6f, 7.4f), -4f, 4f),
                // 5. アーチの下の煉瓦の小路。トンネルの南の口の手前から、くぐった先を見通す（2026-09-28 にトンネルを南へ一つ延ばしたので手前へ寄せた）
                new View("5_arch", new Vector3(-3.85f, 1.6f, 20.6f), 12f, 6f),
                // 6. 芝から、テラスの白いパラソルの卓と裏口を
                new View("6_terrace", new Vector3(1.9f, 1.6f, 25.2f), 190f, 7f),
                // 7. 東屋の角
                new View("7_gazebo", new Vector3(-1.3f, 1.6f, 27.4f), 322f, 5f),
                // 8. 場面 6 の目線。テラスの卓の椅子に座った目の高さで、夕方の庭を見渡す
                new View("8_seated", new Vector3(-1.66f, 1.25f, 17.95f), 18f, 2f),
                // 9. 全体を上から。片割れの敷地と、村全体（霞を切って撮る）
                new View("9_above", new Vector3(0f, 25f, 19.8f), 0f, 90f),
                new View("9_village", new Vector3(-31f, 95f, 6f), 0f, 90f),
            };
        }

        /// <summary>
        /// 片割れの裏庭の見る所（2026-09-27 の庭の仕上げ直し）。
        /// タイトルの背景（東屋の側からアーチを正面に）・格子戸をくぐった所・テラスから芝と奥・東屋の角・
        /// 場面 6 の座った目（テラスの卓）・夕日の庭の全体
        /// </summary>
        public static View[] GardenViews()
        {
            return new[]
            {
                // タイトルの背景。トンネルの北の端のアーチの芯から、軸（北から東へ 8.3 度）の上を北へ 2.5 m、目 1.6 m で軸を真っすぐ見る。
                // 2026-09-28 にトンネルを北へ一つ延ばした（北の端のアーチは (-3.25, 25.3) から軸の上を北へ 0.95 m）ので、目も同じだけ北へ
                new View("g1_title", new Vector3(-3.25f + 0.1444f * 3.45f, 1.6f, 25.30f + 0.9895f * 3.45f), 188.3f, 0f),
                // 格子戸をくぐった所。小路は低い生け垣の西をゆるく左へ寄り、右の垣越しにテラスの卓
                new View("g2_gate", new Vector3(-4.2f, 1.6f, 12.9f), 356f, 3f),
                new View("g3_terrace", new Vector3(1.2f, 1.7f, 17.8f), 352f, 6f),
                new View("g4_gazebo", new Vector3(-1.3f, 1.6f, 27.4f), 322f, 5f),
                new View("g5_seated", new Vector3(-1.66f, 1.25f, 17.95f), 18f, 2f),
                new View("g6_whole", new Vector3(3.4f, 1.7f, 19.6f), 325f, 6f),
                // 小路のトンネル（2026-09-27）。トンネルの南の口の内からテラスと卓の開ける所（2026-09-28 に、格子戸の側からトンネルの口を見た所と替えた）と、
                // トンネルの中を歩く目から上を見上げた所
                new View("g7_south_mouth", new Vector3(-3.75f, 1.6f, 21.9f), 170f, 6f),
                new View("g8_tunnel_up", new Vector3(-3.55f, 1.6f, 23.0f), 8f, -38f),
                // 格子戸から卓までの道（2026-09-28）。格子戸からの小路が塀に沿って北へ上がり、東屋の前で東へ折れる所と、東屋の前の踊り場からトンネルの北の口
                new View("g9_west_walk", new Vector3(-5.74f, 1.6f, 24.6f), 8f, 4f),
                new View("g10_north_mouth", new Vector3(-4.75f, 1.6f, 31.2f), 162f, 6f),
                // 格子戸からの小路（塀沿い）から、トンネルを西の脇から見た所（輪 6 つ、5 m ほどの長さ）
                new View("g11_tunnel_side", new Vector3(-5.74f, 1.6f, 20.2f), 32f, 5f),
            };
        }

        /// <summary>
        /// 庭の見る所を朝と夕方で撮る。only が空でなければ、名前にその字を含む所だけ。
        /// hours は "me"（朝と夕方）・"m"（朝だけ）・"e"（夕方だけ）。終わったら朝に戻す
        /// </summary>
        public static string ShootGarden(string dir, string only, string hours)
        {
            var sb = new StringBuilder();
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                foreach (var h in hours)
                {
                    BuildVillage.SetHour(h == 'e' ? VillageHour.Hour.Evening : VillageHour.Hour.Morning);
                    foreach (var v in GardenViews())
                    {
                        if (!string.IsNullOrEmpty(only) && !v.Name.Contains(only)) continue;
                        sb.AppendLine(Game(v, dir + "/" + v.Name + (h == 'e' ? "_evening" : "_morning") + ".png"));
                    }
                }
            }
            finally
            {
                BuildVillage.SetHour(VillageHour.Hour.Morning);
            }
            return sb.ToString();
        }

        /// <summary>ゲームの見え方で一枚。hour はそのときの時刻に切り替えてから撮る</summary>
        public static string Game(View v, string path)
        {
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";
            GameObject go = null;
            var fog = RenderSettings.fog;
            try
            {
                go = new GameObject("CheckVillageEye");
                go.hideFlags = HideFlags.HideAndDontSave;
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main.GetComponent<Camera>());
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                go.transform.position = v.Eye;
                go.transform.rotation = Quaternion.Euler(v.Pitch, v.Yaw, 0f);
                // 高い所からの見下ろしは霞を切る。95 m の上からでは村ぜんたいが霞の色に沈む
                if (v.Eye.y > 50f) RenderSettings.fog = false;
                var shot = CheckDiveSky.Grab(cam, 960, 540);
                CheckDiveSky.Save(shot, path);
                Object.DestroyImmediate(shot);
                return v.Name + " → " + path;
            }
            finally
            {
                RenderSettings.fog = fog;
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 朝と夕方の両方で <see cref="Views"/> を撮り、朝と夕方を左右に並べた一枚も作る。終わったら朝に戻す
        /// </summary>
        public static string ShootAll(string dir)
        {
            var sb = new StringBuilder();
            var views = Views();
            var hours = new[] { VillageHour.Hour.Morning, VillageHour.Hour.Evening };
            var paths = new string[views.Length, 2];
            try
            {
                for (var h = 0; h < 2; h++)
                {
                    BuildVillage.SetHour(hours[h]);
                    for (var i = 0; i < views.Length; i++)
                    {
                        paths[i, h] = dir + "/" + views[i].Name + (h == 0 ? "_morning" : "_evening") + ".png";
                        sb.AppendLine(Game(views[i], paths[i, h]));
                    }
                }
            }
            finally
            {
                BuildVillage.SetHour(VillageHour.Hour.Morning);
            }
            sb.AppendLine(Sheet(paths, dir + "/sheet_morning_evening.png", 480, 270));
            return sb.ToString();
        }

        /// <summary>撮った絵を縦に並べ、朝を左、夕方を右に置いた一枚。縮めて並べる</summary>
        public static string Sheet(string[,] paths, string outPath, int w, int h)
        {
            var rows = paths.GetLength(0);
            var cols = paths.GetLength(1);
            var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false, false);
            var loaded = new List<Texture2D>();
            try
            {
                for (var r = 0; r < rows; r++)
                    for (var c = 0; c < cols; c++)
                    {
                        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                        loaded.Add(src);
                        if (!src.LoadImage(System.IO.File.ReadAllBytes(paths[r, c]))) continue;
                        var px = new Color[w * h];
                        for (var y = 0; y < h; y++)
                            for (var x = 0; x < w; x++)
                                px[y * w + x] = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                        sheet.SetPixels(c * w, (rows - 1 - r) * h, w, h, px);
                    }
                sheet.Apply();
                CheckDiveSky.Save(sheet, outPath);
                return "並べた → " + outPath;
            }
            finally
            {
                foreach (var t in loaded) Object.DestroyImmediate(t);
                Object.DestroyImmediate(sheet);
            }
        }

        /// <summary>
        /// 一度に見える範囲の重さ。視錐台に掛かるレンダラーの数（描く回数の目安）・三角形・マテリアル・テクスチャ、
        /// 影を落とすものの数。影は日の側からもう一度描くので、描く回数はおおよそ「見える数 + 影を落とす数」になる
        /// </summary>
        public static string Weigh(View v)
        {
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";
            var src = main.GetComponent<Camera>();
            var go = new GameObject("CheckVillageWeigh");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(src);
                cam.aspect = 16f / 9f;
                go.transform.position = v.Eye;
                go.transform.rotation = Quaternion.Euler(v.Pitch, v.Yaw, 0f);
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                var draws = 0;
                var casters = 0;
                long tris = 0;
                var mats = new HashSet<Material>();
                var texs = new HashSet<Texture>();
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!r.enabled) continue;
                    var f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    var near = Vector3.Distance(r.bounds.ClosestPoint(v.Eye), v.Eye);
                    if (r.shadowCastingMode != ShadowCastingMode.Off && near < 50f) casters++;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    if (near > cam.farClipPlane) continue;
                    draws += r.sharedMaterials.Length;
                    for (var s = 0; s < f.sharedMesh.subMeshCount; s++) tris += f.sharedMesh.GetIndexCount(s) / 3;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        mats.Add(m);
                        foreach (var id in m.GetTexturePropertyNameIDs())
                        {
                            var t = m.GetTexture(id);
                            if (t != null) texs.Add(t);
                        }
                    }
                }
                if (RenderSettings.skybox != null) { mats.Add(RenderSettings.skybox); draws++; }
                return string.Format("{0}: 描く {1}（影を落とす {2}）、三角 {3}、マテリアル {4}、テクスチャ {5}",
                    v.Name, draws, casters, tris, mats.Count, texs.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- 歩ける所（2026-09-28、格子戸から卓までは必ずトンネルを通る） ------------------

        /// <summary>
        /// 片割れの敷地の歩ける所を、Player と同じカプセル（CharacterController の太さ・高さ・越えられる段）で 0.1 m 升に塗り、
        /// 格子戸をくぐった所から歩いて行ける所を数える。二度数える。一度目はそのまま、二度目はトンネルの中を塞いで。
        /// 格子戸から卓までトンネルを通る道しか無ければ、一度目は卓のまわりの区画（場面 9 の終わり）に届き、二度目は届かない。
        /// 芝（東屋の側から入れる）はどちらでも届く。
        /// png があれば、上から見た道の並びの絵を書く（塞いだ升は暗い灰、トンネルを通らずに行ける所は青、トンネルを通って初めて行ける所は橙、
        /// トンネルの中は緑、行けない空きは薄い灰。白い線は小路の芯、黄の線は低い生け垣の芯、赤は卓の区画の縁）
        /// </summary>
        public static string Reach(string png)
        {
            var player = GameObject.Find("Player");
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            if (cc == null) return "Player の CharacterController が無い";
            var r = cc.radius;
            var high = cc.height;
            var climb = cc.stepOffset;
            const float cell = 0.1f;
            const float x0 = BuildVillage.PlotWest - 0.4f, z0 = BuildVillage.GateZ - 0.2f;
            const int nx = 148, nz = 240;
            var floor = new float[nx, nz];
            var free = new bool[nx, nz];
            Physics.SyncTransforms();
            for (var i = 0; i < nx; i++)
                for (var k = 0; k < nz; k++)
                {
                    var p = new Vector3(x0 + (i + 0.5f) * cell, 0f, z0 + (k + 0.5f) * cell);
                    var best = float.NegativeInfinity;
                    foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.45f, Vector3.down, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.transform.IsChildOf(player.transform)) continue;
                        if (h.point.y > best) best = h.point.y;
                    }
                    floor[i, k] = best;
                    if (float.IsNegativeInfinity(best)) continue;
                    var foot = new Vector3(p.x, best, p.z);
                    free[i, k] = !Blocked(foot + Vector3.up * (climb + r), foot + Vector3.up * (high - r), r, player.transform);
                }
            var start = new Vector2(BuildVillage.SidePathX, BuildVillage.GateZ + 0.6f);
            var table = new Vector2(BuildVillage.TableAt.x, BuildVillage.TableAt.z);
            var lawn = new Vector2(1.0f, 25.0f);
            var open = Flood(free, floor, climb, x0, z0, cell, start, false);
            var shut = Flood(free, floor, climb, x0, z0, cell, start, true);
            System.Func<bool[,], Vector2, float, bool> near = (seen, at, radius) =>
            {
                for (var i = 0; i < nx; i++)
                    for (var k = 0; k < nz; k++)
                        if (seen[i, k] && Vector2.Distance(new Vector2(x0 + (i + 0.5f) * cell, z0 + (k + 0.5f) * cell), at) <= radius) return true;
                return false;
            };
            var sb = new StringBuilder();
            sb.AppendFormat("カプセル 半径 {0:0.00}・高さ {1:0.00}・越えられる段 {2:0.00}", r, high, climb).AppendLine();
            sb.AppendFormat("そのまま: 卓の区画 {0}、芝 {1}", near(open, table, BuildVillage.ReunionZone) ? "届く" : "届かない", near(open, lawn, 0.3f) ? "届く" : "届かない").AppendLine();
            sb.AppendFormat("トンネルを塞ぐ: 卓の区画 {0}、芝 {1}", near(shut, table, BuildVillage.ReunionZone) ? "届く" : "届かない", near(shut, lawn, 0.3f) ? "届く" : "届かない").AppendLine();
            if (!string.IsNullOrEmpty(png)) sb.AppendLine(Plan(png, free, open, shut, x0, z0, cell, table));
            return sb.ToString();
        }

        /// <summary>
        /// 片割れの裏庭を真上から、影の向きもゲームと同じ光で撮る（正射影、北を右、西を上）。粗い画面の 3 倍（2880×1620、中は 960×540）で、
        /// 道と生け垣と花の縁の並びが読めるようにする。霞は切る
        /// </summary>
        public static string Aerial(string path)
        {
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";
            GameObject go = null;
            var fog = RenderSettings.fog;
            try
            {
                go = new GameObject("CheckVillageAerial") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main.GetComponent<Camera>());
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                cam.orthographic = true;
                cam.orthographicSize = 7.8f;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 80f;
                go.transform.position = new Vector3(0f, 40f, 23.4f);
                go.transform.rotation = Quaternion.Euler(90f, -90f, 0f);
                RenderSettings.fog = false;
                var shot = CheckDiveSky.Grab(cam, 2880, 1620);
                CheckDiveSky.Save(shot, path);
                Object.DestroyImmediate(shot);
                return "真上から → " + path;
            }
            finally
            {
                RenderSettings.fog = fog;
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>カプセルが Player の外の当たりに掛かるか</summary>
        static bool Blocked(Vector3 a, Vector3 b, float r, Transform player)
        {
            foreach (var c in Physics.OverlapCapsule(a, b, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(player)) return true;
            return false;
        }

        /// <summary>start から、空いた升を越えられる段の内で辿れる所。shutTunnel ならトンネルの中を塞ぐ</summary>
        static bool[,] Flood(bool[,] free, float[,] floor, float climb, float x0, float z0, float cell, Vector2 start, bool shutTunnel)
        {
            var nx = free.GetLength(0);
            var nz = free.GetLength(1);
            var seen = new bool[nx, nz];
            var si = Mathf.FloorToInt((start.x - x0) / cell);
            var sk = Mathf.FloorToInt((start.y - z0) / cell);
            if (!free[si, sk]) return seen;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(si, sk));
            seen[si, sk] = true;
            var steps = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var s in steps)
                {
                    var n = at + s;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y] || !free[n.x, n.y]) continue;
                    if (Mathf.Abs(floor[n.x, n.y] - floor[at.x, at.y]) > climb) continue;
                    if (shutTunnel && BuildVillage.InTunnel(new Vector2(x0 + (n.x + 0.5f) * cell, z0 + (n.y + 0.5f) * cell))) continue;
                    seen[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>上から見た道の並びの絵（升ひとつを 4 画素に）</summary>
        static string Plan(string png, bool[,] free, bool[,] open, bool[,] shut, float x0, float z0, float cell, Vector2 table)
        {
            const int px = 4;
            var nx = free.GetLength(0);
            var nz = free.GetLength(1);
            var tex = new Texture2D(nx * px, nz * px, TextureFormat.RGB24, false, false);
            try
            {
                var colors = new Color32[nx * px * nz * px];
                for (var i = 0; i < nx; i++)
                    for (var k = 0; k < nz; k++)
                    {
                        var p = new Vector2(x0 + (i + 0.5f) * cell, z0 + (k + 0.5f) * cell);
                        Color32 c;
                        if (!free[i, k]) c = new Color32(60, 60, 64, 255);
                        else if (BuildVillage.InTunnel(p) && open[i, k]) c = new Color32(70, 170, 80, 255);
                        else if (shut[i, k]) c = new Color32(90, 140, 220, 255);
                        else if (open[i, k]) c = new Color32(235, 150, 60, 255);
                        else c = new Color32(185, 185, 185, 255);
                        for (var a = 0; a < px; a++)
                            for (var b = 0; b < px; b++)
                                colors[(k * px + b) * nx * px + i * px + a] = c;
                    }
                System.Action<Vector2, Color32> dot = (p, c) =>
                {
                    var u = Mathf.RoundToInt((p.x - x0) / cell * px);
                    var v = Mathf.RoundToInt((p.y - z0) / cell * px);
                    for (var a = -1; a <= 0; a++)
                        for (var b = -1; b <= 0; b++)
                        {
                            var uu = u + a;
                            var vv = v + b;
                            if (uu >= 0 && vv >= 0 && uu < nx * px && vv < nz * px) colors[vv * nx * px + uu] = c;
                        }
                };
                var lines = BuildVillage.PlanLines();
                for (var l = 0; l < lines.Count; l++)
                {
                    var line = lines[l];
                    var col = l == 2 ? new Color32(250, 230, 60, 255) : new Color32(255, 255, 255, 255);
                    for (var j = 0; j + 1 < line.Length; j++)
                        for (var t = 0f; t <= 1f; t += 0.02f) dot(Vector2.Lerp(line[j], line[j + 1], t), col);
                }
                for (var a = 0; a < 360; a++)
                    dot(table + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * BuildVillage.ReunionZone, new Color32(230, 40, 40, 255));
                tex.SetPixels32(colors);
                tex.Apply();
                CheckDiveSky.Save(tex, png);
                return "道の並び → " + png;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 格子戸をくぐった所から卓の区画まで、Player と同じ CharacterController を道の芯に沿って歩かせる（毎秒 1.4 m、1/60 秒ずつ）。
        /// 当たりに止められて 1 秒進めなければ、そこで止める。途切れずに歩けたか、どこで止まったかを返す。
        /// 歩かせるのは写し（HideAndDontSave）で、同じ呼び出しの中で捨てる
        /// </summary>
        public static string Walk()
        {
            var player = GameObject.Find("Player");
            var src = player != null ? player.GetComponent<CharacterController>() : null;
            if (src == null) return "Player の CharacterController が無い";
            var route = BuildVillage.RouteToTable();
            var go = new GameObject("CheckVillageWalker") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cc = go.AddComponent<CharacterController>();
                cc.radius = src.radius;
                cc.height = src.height;
                cc.center = src.center;
                cc.stepOffset = src.stepOffset;
                cc.slopeLimit = src.slopeLimit;
                cc.skinWidth = src.skinWidth;
                cc.enabled = false;
                go.transform.position = new Vector3(route[0].x, 0.1f, route[0].y);
                cc.enabled = true;
                Physics.SyncTransforms();
                const float dt = 1f / 60f;
                const float speed = 1.4f;
                var stuck = 0;
                var frames = 0;
                var worst = 0f;
                var target = 1;
                while (target < route.Count && frames < 60 * 120)
                {
                    var at = go.transform.position;
                    var to = new Vector3(route[target].x, at.y, route[target].y);
                    var d = to - at;
                    if (d.magnitude < 0.2f) { target++; continue; }
                    var before = at;
                    cc.Move(d.normalized * speed * dt + Vector3.down * 2f * dt);
                    frames++;
                    var moved = new Vector2(go.transform.position.x - before.x, go.transform.position.z - before.z).magnitude;
                    stuck = moved < speed * dt * 0.25f ? stuck + 1 : 0;
                    worst = Mathf.Max(worst, Off(route, new Vector2(go.transform.position.x, go.transform.position.z)));
                    if (stuck > 60)
                        return string.Format("止まった: {0}（道の {1} 番目の点 {2} の手前、{3:0.0} 秒）", go.transform.position.ToString("F2"), target, route[target].ToString("F2"), frames * dt);
                }
                var end = go.transform.position;
                var inZone = ReunionDirector.Within(end, BuildVillage.TableAt, BuildVillage.ReunionZone);
                return string.Format("歩けた: {0} 秒で {1} に着いた（卓の区画 {2}）。芯からのずれは最大 {3:0.00} m",
                    (frames * dt).ToString("0.0"), end.ToString("F2"), inZone ? "の内" : "の外", worst);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>点 p から道の芯（折れ線）までの隔たり</summary>
        static float Off(List<Vector2> line, Vector2 p)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var ab = b - a;
                var t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>どのアセットにも属さない RenderTexture の数。撮影の片づけ漏れを見る</summary>
        public static int LooseRenderTextures()
        {
            var n = 0;
            foreach (var rt in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(rt))) n++;
            return n;
        }
    }
}
