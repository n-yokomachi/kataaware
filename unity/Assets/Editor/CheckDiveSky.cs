using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の空と遠景の書き割りを、絵を撮って測る道具。
    ///
    /// **測り終えたら必ず元へ戻す。** 同じエディタで別の担当が同時に測っているので、
    /// 場所・記憶・<see cref="RenderSettings"/>・体の当たりを起こしたままにすると、
    /// その担当の測りが狂う。<see cref="Stage"/> を using で囲めば、抜けるときに全部戻る。
    ///
    /// 撮るのは確認用のカメラ。ゲームのカメラと同じ fov 70・far 260 にして、
    /// 後処理は掛けない（記憶ごとの色味は Volume が持つので、測る絵には混ぜない）
    /// </summary>
    public static class CheckDiveSky
    {
        /// <summary>ゲームのカメラと揃える値</summary>
        public const float Fov = 70f;
        public const float Near = 0.08f;
        public const float Far = 260f;

        /// <summary>
        /// 場所を一つだけ起こし、記憶を全部伏せ、空の設定を丸ごと持っておく。
        /// Dispose で全部元へ戻す
        /// </summary>
        public sealed class Stage : IDisposable
        {
            readonly List<KeyValuePair<GameObject, bool>> kept = new List<KeyValuePair<GameObject, bool>>();
            readonly Sky sky = Sky.Read();
            readonly CharacterController hull;
            readonly bool hullOn;
            readonly bool wasDirty;

            /// <summary>起こした場所。無ければ null</summary>
            public readonly Transform Place;

            public Stage(string placeId, int take = -1)
            {
                wasDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
                var dive = GameObject.Find("Dive");
                if (dive == null) throw new InvalidOperationException("Dive が無い");
                var places = dive.transform.Find("Places");
                var takes = dive.transform.Find("Takes");
                if (places != null)
                    foreach (Transform p in places)
                    {
                        Keep(p.gameObject);
                        p.gameObject.SetActive(p.name == placeId);
                        if (p.name == placeId) Place = p;
                    }
                if (takes != null)
                    foreach (Transform t in takes)
                    {
                        Keep(t.gameObject);
                        t.gameObject.SetActive(t.name == take.ToString());
                    }
                var player = GameObject.Find("Player");
                hull = player != null ? player.GetComponent<CharacterController>() : null;
                if (hull != null) { hullOn = hull.enabled; hull.enabled = false; }
            }

            void Keep(GameObject go)
            {
                kept.Add(new KeyValuePair<GameObject, bool>(go, go.activeSelf));
            }

            public void Dispose()
            {
                for (var i = kept.Count - 1; i >= 0; i--)
                    if (kept[i].Key != null) kept[i].Key.SetActive(kept[i].Value);
                if (hull != null) hull.enabled = hullOn;
                sky.Write();
                // 起こして伏せただけなら、シーンの中身は元のまま。汚れた印だけ外す
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!wasDirty && scene.isDirty) EditorSceneManager_ClearDirty(scene);
            }
        }

        static void EditorSceneManager_ClearDirty(UnityEngine.SceneManagement.Scene scene)
        {
            // 公開の API は無いので、内部の口を探して呼ぶ。見つからなければ汚れたまま残す
            var m = typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod("ClearSceneDirtiness",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m != null) m.Invoke(null, new object[] { scene });
        }

        /// <summary><see cref="RenderSettings"/> の空まわりを丸ごと</summary>
        public sealed class Sky
        {
            Material skybox;
            AmbientMode mode;
            Color ambSky, ambEq, ambGround, ambLight;
            float ambIntensity;
            bool fog;
            FogMode fogMode;
            Color fogColor;
            float fogDensity, fogStart, fogEnd;
            Light sun;

            public static Sky Read()
            {
                return new Sky
                {
                    skybox = RenderSettings.skybox,
                    mode = RenderSettings.ambientMode,
                    ambSky = RenderSettings.ambientSkyColor,
                    ambEq = RenderSettings.ambientEquatorColor,
                    ambGround = RenderSettings.ambientGroundColor,
                    ambLight = RenderSettings.ambientLight,
                    ambIntensity = RenderSettings.ambientIntensity,
                    fog = RenderSettings.fog,
                    fogMode = RenderSettings.fogMode,
                    fogColor = RenderSettings.fogColor,
                    fogDensity = RenderSettings.fogDensity,
                    fogStart = RenderSettings.fogStartDistance,
                    fogEnd = RenderSettings.fogEndDistance,
                    sun = RenderSettings.sun,
                };
            }

            public void Write()
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientIntensity = ambIntensity;
                if (mode == AmbientMode.Flat) RenderSettings.ambientLight = ambLight;
                RenderSettings.ambientSkyColor = ambSky;
                RenderSettings.ambientEquatorColor = ambEq;
                RenderSettings.ambientGroundColor = ambGround;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.sun = sun;
            }
        }

        /// <summary>
        /// 確認用のカメラで一枚撮る。<paramref name="eye"/> と向きは world。
        /// <paramref name="yaw"/> は +z が 0 で東回り、<paramref name="pitch"/> は上が正
        /// </summary>
        public static Texture2D Shot(Vector3 eye, float yaw, float pitch, int w, int h,
            CameraClearFlags clear, Color bg, int mask = ~0, float fov = Fov)
        {
            var go = new GameObject("CheckDiveSkyEye");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            try
            {
                go.transform.position = eye;
                go.transform.rotation = Quaternion.Euler(-pitch, yaw, 0f);
                cam.fieldOfView = fov;
                cam.nearClipPlane = Near;
                cam.farClipPlane = Far;
                cam.clearFlags = clear;
                cam.backgroundColor = bg;
                cam.cullingMask = mask;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                return Grab(cam, w, h);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// カメラの絵を読み出す。sRGB のまま。
        /// 視錐台を自分で渡したカメラ（<see cref="Camera.projectionMatrix"/>）は、縦横比に触らない
        /// </summary>
        public static Texture2D Grab(Camera cam, int w, int h, bool fit = true)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var keep = RenderTexture.active;
            var target = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                if (fit) cam.aspect = w / (float)h;
                cam.Render();
                RenderTexture.active = rt;
                var shot = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                shot.Apply();
                return shot;
            }
            finally
            {
                cam.targetTexture = target;
                RenderTexture.active = keep;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// 組み立てが <see cref="DiveDirector"/> に持たせた、その場所の空・霞・環境光・日。
        /// 実行時に <c>Sky</c> が差し替えるのと同じ値
        /// </summary>
        public static PlaceSky SkyOf(string placeId)
        {
            var director = UnityEngine.Object.FindFirstObjectByType<DiveDirector>(FindObjectsInactive.Include);
            if (director == null) throw new InvalidOperationException("DiveDirector が無い");
            var field = typeof(DiveDirector).GetField("skies",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var all = (PlaceSky[])field.GetValue(director);
            var which = Array.IndexOf(DiveIds.Places, placeId);
            if (which < 0 || which >= all.Length) throw new InvalidOperationException("空が無い: " + placeId);
            return all[which];
        }

        /// <summary>
        /// その場所の空を当てて、ゲームと同じ塗り方で撮る。空を戻すのは <see cref="Stage"/> の Dispose
        /// </summary>
        public static void PairIn(string placeId, Vector3 eye, float yaw, float pitch, string path)
        {
            var sky = SkyOf(placeId);
            sky.Apply(null);
            Pair(eye, yaw, pitch, sky.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor, sky.flat, path);
        }

        public static void Save(Texture2D shot, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, shot.EncodeToPNG());
        }

        /// <summary>撮って書き出して捨てる。960×540 と、ゲームと同じ 320×180 の二枚</summary>
        public static void Pair(Vector3 eye, float yaw, float pitch, CameraClearFlags clear, Color bg, string path)
        {
            var big = Shot(eye, yaw, pitch, 960, 540, clear, bg);
            Save(big, path + ".png");
            UnityEngine.Object.DestroyImmediate(big);
            var small = Shot(eye, yaw, pitch, 320, 180, clear, bg);
            Save(small, path + "_game.png");
            UnityEngine.Object.DestroyImmediate(small);
        }

        /// <summary>二枚の絵の画素の差。0〜255 の平均と最大</summary>
        public static Vector2 Diff(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            if (pa.Length != pb.Length) return new Vector2(-1f, -1f);
            double sum = 0;
            var max = 0;
            for (var i = 0; i < pa.Length; i++)
            {
                var d = Mathf.Max(Mathf.Abs(pa[i].r - pb[i].r), Mathf.Max(Mathf.Abs(pa[i].g - pb[i].g), Mathf.Abs(pa[i].b - pb[i].b)));
                sum += d;
                if (d > max) max = d;
            }
            return new Vector2((float)(sum / pa.Length), max);
        }

        // ---- 団地の空と書き割りを測る ------------------------------------------------

        /// <summary>960×540 で 1 ラジアンが何画素になるか（縦の半分 ÷ tan(35 度)）。水平に構えたときの値</summary>
        public static readonly float Focal = 270f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);

        /// <summary>廊下（三階の廊下の真ん中、手すりの際）と庭（敷地の真ん中）の目</summary>
        public static readonly Vector3 Corridor = new Vector3(3.4f, BuildDive.EstateTop + 1.62f, -13.7f);
        public static readonly Vector3 Yard = new Vector3(5.5f, 1.62f, -4.0f);

        static readonly string[] Compass = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };

        /// <summary>
        /// 測れるものを全部測ってログに出す。場所・記憶・空は測り終えたら元へ戻る
        /// </summary>
        [MenuItem("HalfAware/Check the estate sky", false, 252)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) { Debug.LogError("再生中とコンパイル中は測らない"); return; }
            foreach (var line in Report()) Debug.Log(line);
        }

        public static List<string> Report()
        {
            var lines = new List<string>();
            lines.Add(SunLine());
            lines.AddRange(HazeLines());
            lines.AddRange(SeamLines());
            lines.Add(ReachLine());
            lines.AddRange(FarLines());
            return lines;
        }

        /// <summary>
        /// 1. 空の中の一番明るい所の方角と、Morning の光が来る方角の角度の差。
        /// 空だけを立方体の六面で撮り、明るさが頭打ちの画素の重心を日の中心とする
        /// </summary>
        public static string SunLine()
        {
            using (var stage = new Stage(DiveIds.Estate))
            {
                var sky = SkyOf(DiveIds.Estate);
                sky.Apply(null);
                if (sky.sun == null) return "日: Morning が繋がっていない";
                var faces = new[]
                {
                    Quaternion.Euler(0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 180f, 0f),
                    Quaternion.Euler(0f, 270f, 0f), Quaternion.Euler(-90f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f),
                };
                const int size = 384;
                var best = 0f;
                var shots = new List<KeyValuePair<Quaternion, Color32[]>>();
                foreach (var q in faces)
                {
                    var shot = Shot(Vector3.zero, q.eulerAngles.y, -q.eulerAngles.x, size, size, CameraClearFlags.Skybox, Color.black, 0, 90f);
                    var px = shot.GetPixels32();
                    UnityEngine.Object.DestroyImmediate(shot);
                    shots.Add(new KeyValuePair<Quaternion, Color32[]>(q, px));
                    foreach (var p in px) best = Mathf.Max(best, Luma(p));
                }
                var sum = Vector3.zero;
                var count = 0;
                var go = new GameObject("CheckDiveSkySun");
                go.hideFlags = HideFlags.HideAndDontSave;
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = 90f;
                cam.aspect = 1f;
                try
                {
                    foreach (var kv in shots)
                    {
                        go.transform.rotation = kv.Key;
                        for (var i = 0; i < kv.Value.Length; i++)
                        {
                            if (Luma(kv.Value[i]) < best * 0.995f) continue;
                            var x = i % size;
                            var y = i / size;
                            sum += cam.ViewportPointToRay(new Vector3((x + 0.5f) / size, (y + 0.5f) / size, 0f)).direction.normalized;
                            count++;
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                var found = sum.normalized;
                var toward = -sky.sun.transform.forward;
                return string.Format("1. 日の向き: 空の一番明るい所（{0} 画素の重心）と Morning の光が来る方角の差 {1:0.00} 度"
                    + "（空の日: 方位 {2:0.0} 度・仰角 {3:0.0} 度 / 灯り: 方位 {4:0.0} 度・仰角 {5:0.0} 度）",
                    count, Vector3.Angle(found, toward), Azimuth(found), Elevation(found), Azimuth(toward), Elevation(toward));
            }
        }

        static float Luma(Color32 c)
        {
            return 0.2126f * Mathf.GammaToLinearSpace(c.r / 255f) + 0.7152f * Mathf.GammaToLinearSpace(c.g / 255f)
                + 0.0722f * Mathf.GammaToLinearSpace(c.b / 255f);
        }

        static float Azimuth(Vector3 d)
        {
            var a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        static float Elevation(Vector3 d)
        {
            return Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 2. 霞の色と、空の地平の色の差。八方を水平に向いて空だけを撮り、画面の真ん中（地平）の
        /// 20×8 画素の平均を霞の色と比べる。差は sRGB の 0〜255 で、三色のうち一番大きい差
        /// </summary>
        public static List<string> HazeLines()
        {
            var lines = new List<string>();
            using (new Stage(DiveIds.Estate))
            {
                var sky = SkyOf(DiveIds.Estate);
                sky.Apply(null);
                var fog = (Color32)sky.hazeColor;
                var row = new System.Text.StringBuilder();
                var worst = 0f;
                var sunAz = sky.sun != null ? Azimuth(-sky.sun.transform.forward) : 0f;
                var away = 0f;
                for (var k = 0; k < 8; k++)
                {
                    var az = k * 45f;
                    var shot = Shot(Vector3.zero, az, 0f, 960, 540, CameraClearFlags.Skybox, Color.black, 0);
                    var px = shot.GetPixels32();
                    UnityEngine.Object.DestroyImmediate(shot);
                    float r = 0f, g = 0f, b = 0f;
                    var n = 0;
                    for (var y = 266; y < 274; y++)
                        for (var x = 470; x < 490; x++)
                        {
                            var c = px[y * 960 + x];
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                    r /= n; g /= n; b /= n;
                    var gap = Mathf.Max(Mathf.Abs(r - fog.r), Mathf.Max(Mathf.Abs(g - fog.g), Mathf.Abs(b - fog.b)));
                    worst = Mathf.Max(worst, gap);
                    if (Mathf.Abs(Mathf.DeltaAngle(az, sunAz + 180f)) <= 22.5f) away = gap;
                    row.AppendFormat("{0} ({1:0},{2:0},{3:0}) 差 {4:0.0} / ", Compass[k], r, g, b, gap);
                }
                lines.Add(string.Format("2. 霞の色 ({0},{1},{2}) と空の地平の色の差（0〜255）: 日の反対側 {3:0.0}、八方の最大 {4:0.0}（日の側の地平は暖めてあるので大きく出る）",
                    fog.r, fog.g, fog.b, away, worst));
                lines.Add("   " + row);
            }
            return lines;
        }

        /// <summary>
        /// 3. 書き割りの境目。廊下と庭から八方を水平に向き、画面の真ん中の縦一列で、
        /// 本物の地面の遠い縁の画素と、書き割りの中の地面の始まり（中心から 80 m の線）の画素の上下のずれを測る。
        ///
        /// 書き割りの中の地面の始まりは絵の上では見分けが付かないので、地面だけを
        /// 80 m の内と外で赤と緑に塗り分けた物を同じ輪の形で撮り直し、その絵を輪に一時だけ貼って測る。
        /// 正なら書き割りの線が上（隙間に書き割りの地面が見える）、負なら下（本物の地面が書き割りに被る）。
        /// 他の物に隠れて境目が見えない方角は「隠れる」とする
        /// </summary>
        public static List<string> SeamLines()
        {
            var lines = new List<string>();
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            var ring = place != null ? place.Find("EstateBackdrop") : null;
            var land = place != null ? place.Find("EstateLand") : null;
            if (ring == null || land == null) { lines.Add("3. 書き割りの境目: 輪か遠い地面が無い"); return lines; }

            var made = new List<UnityEngine.Object>();
            using (new Stage(DiveIds.Estate))
            {
                var sky = SkyOf(DiveIds.Estate);
                var plain = sky;
                plain.haze = false;
                Texture2D marks = null;
                var ringRenderer = ring.GetComponent<MeshRenderer>();
                var keepMat = ringRenderer.sharedMaterial;
                var renderers = place.GetComponentsInChildren<Renderer>(true);
                var keepOn = new bool[renderers.Length];
                for (var i = 0; i < renderers.Length; i++) keepOn[i] = renderers[i].enabled;
                try
                {
                    marks = BuildDive.EstateFarShoot(place, plain, (p, list) => MarkGround(p, list));
                    marks.wrapMode = TextureWrapMode.Clamp;
                    marks.filterMode = FilterMode.Point;
                    var debug = new Material(keepMat);
                    debug.hideFlags = HideFlags.HideAndDontSave;
                    debug.SetTexture("_BaseMap", marks);
                    made.Add(debug);
                    sky.Apply(null);

                    lines.Add("3. 書き割りの境目のずれ（画素、960×540、上が正）。測り / 式");
                    var spots = new List<KeyValuePair<string, Vector3>>
                    {
                        new KeyValuePair<string, Vector3>("廊下", Corridor),
                        new KeyValuePair<string, Vector3>("庭", Yard),
                    };
                    var headings = new List<float[]> { Eight(), Eight() };
                    // 八方がどれも棟に隠れる所があるので、境目が見える立ち位置を加えておく
                    spots.Add(new KeyValuePair<string, Vector3>("廊下の東端", new Vector3(7.4f, Corridor.y, -13.6f)));
                    headings.Add(new[] { 55f, 65f, 75f });
                    spots.Add(new KeyValuePair<string, Vector3>("廊下の西端", new Vector3(-0.6f, Corridor.y, -13.6f)));
                    headings.Add(new[] { 300f, 310f, 320f });
                    spots.Add(new KeyValuePair<string, Vector3>("踊り場", new Vector3(0f, BuildDive.EstateTop - BuildDive.Floor * 0.5f + 1.62f, -10.4f)));
                    headings.Add(new[] { 50f, 60f, 70f, 80f });
                    // 棟の裏へは回れなくなったので（見えない仕切りの南は z -15）、庭の東の塀の際から東と南東を見る
                    spots.Add(new KeyValuePair<string, Vector3>("庭の東の塀の際", new Vector3(23.9f, 1.62f, -8.0f)));
                    headings.Add(new[] { 45f, 90f, 135f, 180f });
                    for (var s = 0; s < spots.Count; s++)
                    {
                        var at = spots[s].Value;
                        var row = new System.Text.StringBuilder();
                        row.Append("   " + spots[s].Key + " ");
                        var worst = 0f;
                        foreach (var az in headings[s])
                        {
                            var k = Mathf.RoundToInt(az / 45f) % 8;
                            var label = Mathf.Approximately(az % 45f, 0f) ? Compass[k] : az.ToString("0") + "度";
                            var world = place.TransformPoint(at);
                            var yaw = place.eulerAngles.y + az;
                            // 本物の地面だけ
                            for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i].transform == land;
                            var a = Column(world, yaw);
                            // 輪だけ、塗り分けた絵で
                            for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i].transform == ring;
                            ringRenderer.sharedMaterial = debug;
                            var b = Column(world, yaw);
                            ringRenderer.sharedMaterial = keepMat;
                            // 全部を戻して、縁が他の物に隠れていないか
                            for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = keepOn[i];
                            var full = Column(world, yaw);
                            land.GetComponent<Renderer>().enabled = false;
                            var without = Column(world, yaw);
                            land.GetComponent<Renderer>().enabled = true;

                            var edge = TopOf(a);
                            var line = GreenFrom(b);
                            var expect = BackdropRing.Seam(at, az, BuildDive.EstateFarCentre, BuildDive.EstateFarEye,
                                BuildDive.EstateFarGround, BuildDive.EstateFarRadius, BuildDive.EstateFarPanels, Focal);
                            if (edge < 0 || line < 0) { row.AppendFormat("{0} 測れない / ", label); continue; }
                            var seen = !Same(full[edge], without[edge]);
                            var gap = line - (edge + 1);
                            if (seen) worst = Mathf.Max(worst, Mathf.Abs(gap));
                            row.AppendFormat("{0} {1}{2:+0;-0;0} / {3:+0.0;-0.0;0.0}  ", label, seen ? "" : "隠れる ", gap, expect);
                        }
                        row.AppendFormat(" … 見える方角の最悪 {0:0} 画素", worst);
                        lines.Add(row.ToString());
                    }
                }
                finally
                {
                    ringRenderer.sharedMaterial = keepMat;
                    for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = keepOn[i];
                    if (marks != null) UnityEngine.Object.DestroyImmediate(marks);
                    foreach (var m in made) if (m != null) UnityEngine.Object.DestroyImmediate(m);
                }
            }
            return lines;
        }

        static float[] Eight()
        {
            return new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        }

        /// <summary>画面の真ん中の縦一列。下から上へ</summary>
        static Color32[] Column(Vector3 eye, float yaw)
        {
            var shot = Shot(eye, yaw, 0f, 960, 540, CameraClearFlags.SolidColor, Color.black);
            var px = shot.GetPixels(480, 0, 1, 540);
            UnityEngine.Object.DestroyImmediate(shot);
            var col = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++) col[i] = px[i];
            return col;
        }

        /// <summary>黒でない一番上の画素（本物の地面の遠い縁）。見つからなければ -1</summary>
        static int TopOf(Color32[] col)
        {
            for (var y = col.Length - 1; y >= 0; y--)
                if (col[y].r + col[y].g + col[y].b > 12) return y;
            return -1;
        }

        /// <summary>下から見て最初に緑になる画素（書き割りの中の 80 m の線）。見つからなければ -1</summary>
        static int GreenFrom(Color32[] col)
        {
            for (var y = 0; y < col.Length; y++)
                if (col[y].g > 120 && col[y].r < 90) return y;
            return -1;
        }

        static bool Same(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 3;
        }

        /// <summary>
        /// 境目を測るためだけの地面。中心から辺まで 80 m の正十六角形の内を赤、外を緑に塗る。灯りも霞も受けない
        /// </summary>
        static GameObject MarkGround(Transform place, List<UnityEngine.Object> made)
        {
            var root = new GameObject("CheckDiveSkyMarks");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var c = BuildDive.EstateFarCentre;
            var n = BuildDive.EstateFarPanels;
            var inner = BackdropRing.Corner(BuildDive.EstateFarGround, n);
            var outer = BackdropRing.Corner(3000f, n);
            var red = new List<Vector3>();
            var green = new List<Vector3>();
            for (var k = 0; k < n; k++)
            {
                var a0 = BackdropRing.Azimuth(k, n) - 180f / n;
                var a1 = a0 + 360f / n;
                var p0 = c + BackdropRing.Heading(a0) * inner;
                var p1 = c + BackdropRing.Heading(a1) * inner;
                var q0 = c + BackdropRing.Heading(a0) * outer;
                var q1 = c + BackdropRing.Heading(a1) * outer;
                red.Add(c); red.Add(p0); red.Add(p1);
                green.Add(p0); green.Add(q0); green.Add(q1);
                green.Add(p0); green.Add(q1); green.Add(p1);
            }
            Flat(root.transform, "Red", red, new Color(1f, 0f, 0f), made);
            Flat(root.transform, "Green", green, new Color(0f, 1f, 0f), made);
            return root;
        }

        static void Flat(Transform parent, string name, List<Vector3> tris, Color col, List<UnityEngine.Object> made)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            for (var i = 0; i < tris.Count; i++) tris[i] = new Vector3(tris[i].x, -0.05f, tris[i].z);
            mesh.SetVertices(tris);
            var idx = new int[tris.Count];
            for (var i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            made.Add(mesh);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Cull", 0f);
            made.Add(m);
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>
        /// 4. 遠さの限り。歩ける所（見えない仕切りの内側、足元から三階の目の高さまで）の端から、
        /// 書き割りの輪の一番遠い頂点までの距離の最大
        /// </summary>
        public static string ReachLine()
        {
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            var ring = place != null ? place.Find("EstateBackdrop") : null;
            if (ring == null) return "4. 遠さの限り: 輪が無い";
            var walk = new Bounds();
            var first = true;
            foreach (var box in place.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.name.StartsWith("EstateFence")) continue;
                if (first) { walk = box.bounds; first = false; }
                else walk.Encapsulate(box.bounds);
            }
            // 仕切りの厚みの内側を歩ける所とする
            var inner = new Bounds(walk.center, walk.size - new Vector3(0.8f, 0f, 0.8f));
            var lowY = place.position.y;
            var highY = place.position.y + BuildDive.EstateTop + 1.7f;
            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            var best = 0f;
            var from = Vector3.zero;
            var to = Vector3.zero;
            foreach (var v in mesh.vertices)
            {
                var w = ring.TransformPoint(v);
                for (var cx = 0; cx < 2; cx++)
                    for (var cz = 0; cz < 2; cz++)
                        for (var cy = 0; cy < 2; cy++)
                        {
                            var p = new Vector3(cx == 0 ? inner.min.x : inner.max.x, cy == 0 ? lowY : highY, cz == 0 ? inner.min.z : inner.max.z);
                            var d = Vector3.Distance(p, w);
                            if (d > best) { best = d; from = p; to = w; }
                        }
            }
            return string.Format("4. 遠さの限り: 歩ける所の端 {0} から輪の角 {1} まで {2:0.0} m（250 以下、far は {3}）",
                from, to, best, Far);
        }

        /// <summary>
        /// 5・6. 場所の中の組んだ形。中心から一番遠い頂点までの距離と頂点の数。
        /// 60 m を超えてよいのは地面（EstateLand）と輪（EstateBackdrop）だけ
        /// </summary>
        public static List<string> FarLines()
        {
            var lines = new List<string>();
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            if (place == null) { lines.Add("5. 場所が無い"); return lines; }
            var centre = place.TransformPoint(BuildDive.EstateFarCentre);
            long verts = 0;
            var over = new List<string>();
            var row = new System.Text.StringBuilder();
            foreach (var mf in place.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                verts += mf.sharedMesh.vertexCount;
                var far = 0f;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    far = Mathf.Max(far, new Vector2(w.x - centre.x, w.z - centre.z).magnitude);
                }
                row.AppendFormat("{0} {1:0}m / ", mf.name, far);
                if (far > 60f && mf.name != "EstateLand" && mf.name != "EstateBackdrop") over.Add(mf.name);
            }
            lines.Add("5. 60 m より先の組んだ形（地面と輪を除く）: " + (over.Count == 0 ? "無し" : string.Join(", ", over.ToArray())));
            lines.Add("   中心から一番遠い頂点: " + row);
            lines.Add("6. 団地の場所ぜんたいの頂点 " + verts);
            return lines;
        }
    }
}
