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
    /// 目の位置と向きは <see cref="Views"/> に持つ。設計書 6 節の 4〜9
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

        /// <summary>設計書 6 節の 4〜9</summary>
        public static View[] Views()
        {
            return new[]
            {
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
                // 9. 全体を上から
                new View("9_above", new Vector3(0f, 25f, 19.8f), 0f, 90f),
            };
        }

        /// <summary>ゲームの見え方で一枚。hour はそのときの時刻に切り替えてから撮る</summary>
        public static string Game(View v, string path)
        {
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";
            GameObject go = null;
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
                var shot = CheckDiveSky.Grab(cam, 960, 540);
                CheckDiveSky.Save(shot, path);
                Object.DestroyImmediate(shot);
                return v.Name + " → " + path;
            }
            finally
            {
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
