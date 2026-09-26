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
                // 路地から畑と丘を。南は農場の門の向こうの麦畑、北は車の着く所の塀の向こう
                new View("fields_south", new Vector3(-28.7f, 1.6f, -1.2f), 190f, 1f),
                new View("fields_north", new Vector3(-58f, 1.6f, 1.6f), 330f, 1f),
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
                // 5. アーチの下の煉瓦の小路。アーチの手前から、くぐった先を見通す
                new View("5_arch", new Vector3(-4.05f, 1.6f, 21.6f), 12f, 6f),
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
                new View("g1_title", new Vector3(-3.25f + 0.048f * 2.5f, 1.6f, 25.30f + 0.999f * 2.5f), 182.75f, 0f),
                new View("g2_gate", new Vector3(-4.2f, 1.6f, 12.9f), 4f, 3f),
                new View("g3_terrace", new Vector3(1.2f, 1.7f, 17.8f), 352f, 6f),
                new View("g4_gazebo", new Vector3(-1.3f, 1.6f, 27.4f), 322f, 5f),
                new View("g5_seated", new Vector3(-1.66f, 1.25f, 17.95f), 18f, 2f),
                new View("g6_whole", new Vector3(3.4f, 1.7f, 19.6f), 325f, 6f),
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
