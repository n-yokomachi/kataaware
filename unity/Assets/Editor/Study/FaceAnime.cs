using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HalfAware.EditorTools.Rocketbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 主人公と片割れの顔を「リアル系のアニメ寄り」にした変更（<see cref="RocketboxAnimeFace"/>）の撮り比べ。
    /// 検証（2026-10-05、オーナー「見た目はもうちょっと美人というかアニメ寄りというかリアル系アニメ寄りにできないものかと思っている」）で作り、
    /// 採用の後は、ゲームへ入れた物（Painted/ のマテリアルと頭のメッシュ）を「後」、前の作り（Lit のマテリアルと、顔の比率を焼かない頭のメッシュ）を
    /// その場で組み直した物を「前」として撮る。
    ///
    /// 撮るのはプレビューの場面の中だけで、場面のファイルとアセットは変えない。組み立て（<see cref="BuildRocketboxProtagonist.Assemble"/>）は
    /// 差込口の mesh を書き直すので呼ばず、マテリアルを着せる所と骨の手入れだけを同じ関数で行う（<see cref="Put"/>）。
    /// 場面の一枚（自室の一人称、村の対面）は、場面をプレビューの場面に開いて撮る（<see cref="EditorSceneManager.OpenPreviewScene(string)"/>。保存しない）
    /// </summary>
    public static class FaceAnime
    {
        public const string OutDir = @"C:\Users\PC_User\AppData\Local\Temp\claude\D--work-kataaware\3eb6fc68-a1f9-4751-bb31-73277ee27f6d\scratchpad\face_anime\final";

        static RocketboxPerson Who(bool twin)
        {
            var self = BuildRocketboxProtagonist.Chosen;
            return twin && self.TwinPerson != null ? self.TwinPerson : self;
        }

        // ---- 前と後の組 --------------------------------------------------------

        static readonly Type Builder = typeof(BuildRocketboxProtagonist);

        static object Call(string name, params object[] args)
        {
            var m = Builder.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null) throw new InvalidOperationException("組み立ての関数が無い: " + name);
            return m.Invoke(null, args);
        }

        /// <summary>今のゲームの組（Painted/ のマテリアルのアセット。読むだけ）</summary>
        public static BuildRocketboxProtagonist.Skin Current(RocketboxPerson who)
        {
            var skin = (BuildRocketboxProtagonist.Skin)Call("LoadPainted", who, false);
            if (skin == null) throw new InvalidOperationException("マテリアルが無い: " + who.Painted);
            return skin;
        }

        /// <summary>
        /// 前の組。Painted/ の元の絵（Head_self.png など）から、前の描き直し（<see cref="BuildRocketboxProtagonist.Paint"/>）と同じ Lit のマテリアルをその場で作る。
        /// 服（ワンピース・シャツ）は今のアセットのまま。使い終えたら skin.Destroy()
        /// </summary>
        public static BuildRocketboxProtagonist.Skin Legacy(RocketboxPerson who)
        {
            var cur = Current(who);
            var dir = who.Painted;
            Func<string, Texture2D> load = n =>
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + n);
                if (t == null) throw new InvalidOperationException("絵が無い: " + dir + n);
                return t;
            };
            var skin = new BuildRocketboxProtagonist.Skin(who) { SkipAnime = true, Dress = cur.Dress };
            skin.Head = BuildRocketboxProtagonist.Keep(skin, BuildRocketboxProtagonist.LitHead("Head_self", load("Head_self.png"), load("Head_self_spec.png")));
            skin.Hair = BuildRocketboxProtagonist.Keep(skin, BuildRocketboxProtagonist.Lit("Hair", load("Hair.png"), 0.34f, true));
            skin.Body = BuildRocketboxProtagonist.Keep(skin, BuildRocketboxProtagonist.Lit("Body", load("Body.png"), 0.12f, false));
            if (cur.Chest != null) skin.Chest = BuildRocketboxProtagonist.Keep(skin, BuildRocketboxProtagonist.Lit("Chest", load("Chest.png"), who.Look().skinSmoothness, false));
            if (cur.Legs != null) skin.Legs = BuildRocketboxProtagonist.Keep(skin, BuildRocketboxProtagonist.Lit("Legs", load("Legs.png"), 0.12f, false));
            return skin;
        }

        /// <summary>今のマテリアルのアセットを、前の組の物へ差し替える表（場面の一枚で使う）</summary>
        static Dictionary<Material, Material> Swap(BuildRocketboxProtagonist.Skin from, BuildRocketboxProtagonist.Skin to)
        {
            var d = new Dictionary<Material, Material>();
            Action<Material, Material> add = (a, b) => { if (a != null && b != null) d[a] = b; };
            add(from.Head, to.Head);
            add(from.Hair, to.Hair);
            add(from.Body, to.Body);
            add(from.Chest, to.Chest);
            add(from.Legs, to.Legs);
            return d;
        }

        /// <summary>
        /// 人の写しを、プレビューの場面に組む。legacy なら前の組（顔の比率を焼かない頭のメッシュを、その場で鼻の手入れから作る）、
        /// でなければ今のゲームの組（頭のメッシュのアセット）。片割れは模型の根を裏返す。立ちの動きの初めのこまで立たせる
        /// </summary>
        public static GameObject Put(Scene scene, RocketboxPerson who, BuildRocketboxProtagonist.Skin skin, bool twin, bool legacy)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            var her = (GameObject)PrefabUtility.InstantiatePrefab(src, scene);
            PrefabUtility.UnpackPrefabInstance(her, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            her.name = "FaceAnimeSubject";
            her.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            her.transform.localScale = twin ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
            var look = who.Look();
            skin.Look = look;
            smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(legacy ? who.CompositeMesh : who.Dir + who.Name + "_nose_mesh.asset");
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(who.SlimAvatar);
            var an = her.GetComponent<Animator>();
            if (avatar != null && an != null) an.avatar = avatar;
            Call("Dress", her, skin, false);
            Call("Shape", her, look.JawScale, look.jawClose);
            Call("ShapeFace", her, look);
            // 前の組は鼻の手入れだけをその場のメッシュに掛ける（マテリアルがアセットでないので、アセットは書かない）
            if (legacy) Call("ShapeNose", her, skin);
            Call("AddAnimator", her);
            Call("PlaceSkeleton", her);
            RocketboxStudy.PoseIdle(her);
            foreach (var r in her.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.updateWhenOffscreen = true;
                r.forceMatrixRecalculationPerRender = true;
            }
            return her;
        }

        // ---- 撮影台 ------------------------------------------------------------

        public enum Lighting { Soft, Village, VillageSide, Alley }

        /// <summary>プレビューの場面（新しく作るか、場面のファイルを開く）と灯りとカメラ。using で囲む。作ると眩暈の値を 0 にし、Dispose で戻して場面を閉じる</summary>
        public sealed class Stage : IDisposable
        {
            public readonly Scene Scene;
            public readonly Camera Cam;
            readonly float keepBlur, keepWobble;
            SphericalHarmonicsL2 ambient;
            readonly List<Object> made = new List<Object>();

            public Stage(Lighting light, string scenePath = null)
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生中は撮らない");
                if (EditorApplication.isCompiling) throw new InvalidOperationException("コンパイル中は撮らない");
                // 画面のフィルターは標準（0）で撮る。別の撮影が一時的に書き換えている間は撮らない（名は ScreenFilter.GlobalName と同じ）
                var filter = Shader.GetGlobalFloat("_HaFilter");
                if (filter != 0f) throw new InvalidOperationException("画面のフィルターが標準でない（_HaFilter = " + filter + "）。戻ってから撮る");
                keepBlur = Shader.GetGlobalFloat("_DazeBlur");
                keepWobble = Shader.GetGlobalFloat("_DazeWobble");
                Scene = scenePath != null ? EditorSceneManager.OpenPreviewScene(scenePath) : EditorSceneManager.NewPreviewScene();
                try
                {
                    Shader.SetGlobalFloat("_DazeBlur", 0f);
                    Shader.SetGlobalFloat("_DazeWobble", 0f);
                    var go = new GameObject("FaceAnimeEye");
                    SceneManager.MoveGameObjectToScene(go, Scene);
                    Cam = go.AddComponent<Camera>();
                    Cam.enabled = false;
                    Cam.scene = Scene;
                    Cam.fieldOfView = 70f;
                    Cam.nearClipPlane = 0.08f;
                    Cam.farClipPlane = 200f;
                    Cam.clearFlags = CameraClearFlags.SolidColor;
                    Cam.backgroundColor = new Color(0.10f, 0.10f, 0.11f);
                    Cam.allowMSAA = false;
                    Cam.allowHDR = true;
                    var data = Cam.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    data.volumeLayerMask = 0;
                    data.antialiasing = AntialiasingMode.None;
                    data.renderShadows = true;
                    data.dithering = false;
                    if (scenePath == null) SetUp(light);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            void SetUp(Lighting light)
            {
                var sh = new SphericalHarmonicsL2();
                sh.Clear();
                switch (light)
                {
                    case Lighting.Village:
                    case Lighting.VillageSide:
                        // 村の朝（Village.unity の Morning）。低い日（仰角 15 度）を、本人の左前（Village）か真横（VillageSide）から。空の補いを右上から
                        Trilight(new Color(0.167f, 0.253f, 0.422f), new Color(0.22f, 0.261f, 0.329f), new Color(0.198f, 0.208f, 0.113f));
                        Directional("Sun", light == Lighting.VillageSide ? -90f : -50f, 15f, new Color(1.0f, 0.93f, 0.8f), 2.1f, LightShadows.Soft, 1f);
                        Directional("Fill", 110f, 40f, new Color(0.7f, 0.78f, 0.95f), 0.55f, LightShadows.None, 0f);
                        return;
                    case Lighting.Alley:
                        // 路地裏の夜（Alley.unity）。暖かい街灯を右前の上に、赤いネオンを左、水色のネオンを右後ろに。弱い青い月
                        Trilight(new Color(0.075f, 0.07f, 0.105f), new Color(0.06f, 0.062f, 0.085f), new Color(0.04f, 0.042f, 0.055f));
                        Directional("Moon", 150f, 50f, new Color(0.52f, 0.58f, 0.85f), 0.16f, LightShadows.Soft, 1f);
                        var head = new Vector3(0f, 1.6f, 0f);
                        Point("Lamp", head + new Vector3(1.6f, 2.2f, 2.4f), new Color(1.0f, 0.9f, 0.76f), 26f, 14f);
                        Point("NeonRed", head + new Vector3(-2.2f, 0.6f, 0.8f), new Color(1.0f, 0.24f, 0.24f), 18f, 12f);
                        Point("NeonCyan", head + new Vector3(2.0f, 0.8f, -1.2f), new Color(0.22f, 0.93f, 1.0f), 18f, 12f);
                        return;
                }
                // 顔の検証（FaceStudy）と同じ柔らかい暖かい光
                sh.AddAmbientLight(new Color(0.30f, 0.26f, 0.22f));
                Directional("Key", -35f, 30f, new Color(1.00f, 0.86f, 0.70f), 0.85f, LightShadows.Soft, 0.55f);
                Directional("Fill", 50f, 10f, new Color(0.95f, 0.88f, 0.80f), 0.30f, LightShadows.None, 0f);
                ambient = sh;
            }

            void Point(string name, Vector3 at, Color col, float intensity, float range)
            {
                var go = new GameObject("FaceAnime" + name);
                SceneManager.MoveGameObjectToScene(go, Scene);
                go.transform.position = at;
                var l = go.AddComponent<UnityEngine.Light>();
                l.type = LightType.Point;
                l.color = col;
                l.intensity = intensity;
                l.range = range;
                l.shadows = LightShadows.None;
            }

            /// <summary>場面のファイルの環境光（三色）を、球面調和として面ごとに渡す</summary>
            public void Trilight(Color sky, Color equator, Color ground)
            {
                const int n = 400;
                var probe = new SphericalHarmonicsL2();
                probe.Clear();
                var dirs = new Vector3[n];
                for (var i = 0; i < n; i++)
                {
                    var y = 1f - 2f * (i + 0.5f) / n;
                    var r = Mathf.Sqrt(1f - y * y);
                    var phi = i * 2.399963f;
                    dirs[i] = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
                    probe.AddDirectionalLight(dirs[i], Color.white, 1f);
                }
                var res = new Color[1];
                probe.Evaluate(new[] { Vector3.up }, res);
                var k = 1f / Mathf.Max(1e-5f, res[0].r);
                var sh = new SphericalHarmonicsL2();
                sh.Clear();
                foreach (var d in dirs) sh.AddDirectionalLight(d, d.y > 0f ? Color.Lerp(equator, sky, d.y) : Color.Lerp(equator, ground, -d.y), k);
                ambient = sh;
            }

            public void Flat(Color c)
            {
                var sh = new SphericalHarmonicsL2();
                sh.Clear();
                sh.AddAmbientLight(c);
                ambient = sh;
            }

            void Directional(string name, float az, float el, Color col, float intensity, LightShadows shadows, float strength)
            {
                var go = new GameObject("FaceAnime" + name);
                SceneManager.MoveGameObjectToScene(go, Scene);
                var from = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
                go.transform.rotation = Quaternion.LookRotation(-from, Vector3.up);
                go.transform.position = new Vector3(0f, 1.6f, 0f) + from * 3f;
                var l = go.AddComponent<UnityEngine.Light>();
                l.type = LightType.Directional;
                l.color = col;
                l.intensity = intensity;
                l.shadows = shadows;
                l.shadowStrength = strength;
                l.shadowBias = 0.02f;
                l.shadowNormalBias = 0.2f;
            }

            /// <summary>場面の中のすべてのレンダラーに環境光を渡す（エディタで撮るカメラには、RenderSettings の環境光が届かない）</summary>
            public void LightAll()
            {
                var block = new MaterialPropertyBlock();
                var arr = new[] { ambient };
                foreach (var root in Scene.GetRootGameObjects())
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        r.lightProbeUsage = LightProbeUsage.CustomProvided;
                        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        r.GetPropertyBlock(block);
                        block.CopySHCoefficientArraysFrom(arr);
                        r.SetPropertyBlock(block);
                    }
            }

            public T Own<T>(T o) where T : Object
            {
                made.Add(o);
                return o;
            }

            public void Place(Vector3 eye, Vector3 target)
            {
                Cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            }

            public Texture2D Game(int w = 320, int h = 180)
            {
                Cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                return FaceStudy.Grab(Cam, w, h);
            }

            public Texture2D Clean(int w, int h)
            {
                var data = Cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                try { return FaceStudy.Grab(Cam, w, h); }
                finally { data.renderPostProcessing = true; }
            }

            public void Dispose()
            {
                Shader.SetGlobalFloat("_DazeBlur", keepBlur);
                Shader.SetGlobalFloat("_DazeWobble", keepWobble);
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
                if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }

        // ---- 顔の位置 ----------------------------------------------------------

        public struct Face
        {
            public Vector3 centre, forward, eyes;
        }

        public static Face FaceOf(GameObject her)
        {
            Transform l = null, r = null, nose = null, lip = null;
            foreach (var t in her.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bip01 LEye") l = t;
                else if (t.name == "Bip01 REye") r = t;
                else if (t.name == "Bip01 MNose") nose = t;
                else if (t.name == "Bip01 MUpperLip") lip = t;
            }
            // 片割れは根を裏返しているので、顔の正面は根の前（+z）のまま
            var fwd = Vector3.ProjectOnPlane(her.transform.rotation * Vector3.forward, Vector3.up).normalized;
            var eyes = (l.position + r.position) * 0.5f;
            var c = nose.position - fwd * 0.012f;
            c.y = (eyes.y + lip.position.y) * 0.5f;
            return new Face { centre = c, forward = fwd, eyes = eyes };
        }

        // ---- 撮る -------------------------------------------------------------

        static void Save(Texture2D t, string name)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static Texture2D Crop(Texture2D src, int w, int h)
        {
            var x = (src.width - w) / 2;
            var y = (src.height - h) / 2;
            var px = src.GetPixels(x, y, w, h);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            t.SetPixels(px);
            t.Apply();
            Object.DestroyImmediate(src);
            return t;
        }

        /// <summary>
        /// 顔の寄り。0.4 m、柔らかい光、正面と本人の左 30 度。細かい絵（中 1920×1080 の顔の周り）とゲームの見え方（320×180）を、前と後で。
        /// 名前は face_{self|twin}_{before|after}_{front|L30}_{clean|game}
        /// </summary>
        public static string ShootFace(bool twin)
        {
            var sb = new StringBuilder();
            var who = Who(twin);
            var tag = twin ? "twin" : "self";
            foreach (var legacy in new[] { true, false })
            {
                var skin = legacy ? Legacy(who) : Current(who);
                try
                {
                    using (var stage = new Stage(Lighting.Soft))
                    {
                        var her = Put(stage.Scene, who, skin, twin, legacy);
                        stage.LightAll();
                        var f = FaceOf(her);
                        foreach (var yaw in new[] { 0f, -30f })
                        {
                            // 本人の左へ回る（片割れは裏返した模型なので、根の左が本人の右）。どちらも黒子の側から撮る
                            var turn = twin ? -yaw : yaw;
                            var dir = Quaternion.AngleAxis(turn, Vector3.up) * f.forward;
                            stage.Place(f.centre + dir * 0.4f, f.centre);
                            var name = string.Format("face_{0}_{1}_{2}", tag, legacy ? "before" : "after", yaw == 0f ? "front" : "30");
                            Save(Crop(stage.Clean(1920, 1080), 720, 600), name + "_clean");
                            Save(stage.Game(), name + "_game");
                            sb.AppendLine(name);
                        }
                    }
                }
                finally
                {
                    if (legacy) skin.Destroy();
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 灯りを替えた寄り（村の朝・村の朝で日を真横から・路地裏の夜）。0.4 m の正面と 30 度、1 m の正面を、ゲームの見え方と細かい絵で、前と後。
        /// 名前は lit_{self|twin}_{village|villageside|alley}_{before|after}_{0.4m_front|0.4m_30|1.0m_front}
        /// </summary>
        public static string ShootLit(bool twin, Lighting light)
        {
            var sb = new StringBuilder();
            var who = Who(twin);
            foreach (var legacy in new[] { true, false })
            {
                var skin = legacy ? Legacy(who) : Current(who);
                try
                {
                    using (var stage = new Stage(light))
                    {
                        var her = Put(stage.Scene, who, skin, twin, legacy);
                        stage.LightAll();
                        var f = FaceOf(her);
                        foreach (var shot in new[] { new Vector2(0.4f, 0f), new Vector2(0.4f, -30f), new Vector2(1.0f, 0f) })
                        {
                            var turn = twin ? -shot.y : shot.y;
                            var dir = Quaternion.AngleAxis(turn, Vector3.up) * f.forward;
                            stage.Place(f.centre + dir * shot.x, f.centre);
                            var name = string.Format(CultureInfo.InvariantCulture, "lit_{0}_{1}_{2}_{3:0.0}m_{4}", twin ? "twin" : "self", light.ToString().ToLowerInvariant(), legacy ? "before" : "after", shot.x, shot.y == 0f ? "front" : "30");
                            Save(stage.Game(), name + "_game");
                            if (shot.x < 0.5f) Save(Crop(stage.Clean(1920, 1080), 720, 600), name + "_clean");
                            sb.AppendLine(name);
                        }
                    }
                }
                finally
                {
                    if (legacy) skin.Destroy();
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 場面 1（自室）の一人称。座った目から、右の手首（差込口）と前腕を見下ろす。前は体のマテリアルを前の組へ差し替える。
        /// 場面のファイルはプレビューの場面に開くだけで、保存しない
        /// </summary>
        public static string ShootRoomArms()
        {
            var sb = new StringBuilder();
            var who = Who(false);
            foreach (var legacy in new[] { true, false })
            {
                var cur = Current(who);
                var old = legacy ? Legacy(who) : null;
                try
                {
                    using (var stage = new Stage(Lighting.Soft, "Assets/Scenes/Room.unity"))
                    {
                        // 自室の環境光（平ら）
                        stage.Flat(new Color(0.05f, 0.055f, 0.065f));
                        stage.LightAll();
                        Animator body = null;
                        foreach (var root in stage.Scene.GetRootGameObjects())
                            foreach (var an in root.GetComponentsInChildren<Animator>(true))
                                if (an.gameObject.name == "Protagonist" && an.isHuman) body = an;
                        if (body == null) throw new InvalidOperationException("自室に主人公が無い");
                        if (legacy) Replace(stage.Scene, Swap(cur, old));
                        // 座った形（再生中は SeatedPose が毎こま当てる。エディタでは一度当てる）
                        var seat = body.GetComponent<SeatedPose>();
                        if (seat != null) { seat.Bind(); seat.Apply(); }
                        Transform player = null;
                        foreach (var root in stage.Scene.GetRootGameObjects()) if (root.name == "Player") player = root.transform;
                        if (player == null) throw new InvalidOperationException("自室に Player が無い");
                        // 座った目（SceneFlow の seatEyeHeight 1.259 m、体の前へ 0.1 m）から、右の前腕と手首を見る。下を向くのは 40 度まで（シナリオ設計 1 節）
                        var eye = player.position + Vector3.up * 1.259f + player.forward * 0.10f;
                        var hand = body.GetBoneTransform(HumanBodyBones.RightHand);
                        var elbow = body.GetBoneTransform(HumanBodyBones.RightLowerArm);
                        var target = Vector3.Lerp(hand.position, elbow.position, 0.4f);
                        var dir = target - eye;
                        var flat = new Vector3(dir.x, 0f, dir.z);
                        var pitch = Mathf.Min(40f, Mathf.Atan2(-dir.y, flat.magnitude) * Mathf.Rad2Deg);
                        var look = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
                        stage.Place(eye, eye + look);
                        var name = "room_arm_" + (legacy ? "before" : "after");
                        Save(stage.Game(), name + "_game");
                        Save(stage.Clean(960, 540), name + "_clean");
                        sb.AppendLine(name);
                    }
                }
                finally
                {
                    if (old != null) old.Destroy();
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 場面 10（村の対面）の片割れを、裏口から出た所で、正面 1.5 m と 0.6 m から、目の高さで。前は片割れのマテリアルを前の組へ差し替える。
        /// 場面のファイルはプレビューの場面に開くだけで、保存しない（伏せてある片割れは、撮る間だけ見せる）
        /// </summary>
        public static string ShootVillageTwin()
        {
            var sb = new StringBuilder();
            var who = Who(true);
            foreach (var legacy in new[] { true, false })
            {
                var cur = Current(who);
                var old = legacy ? Legacy(who) : null;
                try
                {
                    using (var stage = new Stage(Lighting.Village, "Assets/Scenes/Village.unity"))
                    {
                        // 村の環境光（Village.unity の三色）
                        stage.Trilight(new Color(0.167f, 0.253f, 0.422f), new Color(0.22f, 0.261f, 0.329f), new Color(0.198f, 0.208f, 0.113f));
                        GameObject twin = null;
                        foreach (var root in stage.Scene.GetRootGameObjects())
                            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                                if (t.name == "Twin" && t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) twin = t.gameObject;
                        if (twin == null) throw new InvalidOperationException("村に片割れが無い");
                        for (var t = twin.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                        // 場面のファイルの片割れは裏口の中に立っている（出てきて歩くのは再生中）。撮る間だけ、戸口から 1.8 m 外へ出す
                        twin.transform.position += twin.transform.forward * 1.8f;
                        // 夕方の灯りは消す（朝で撮る）
                        foreach (var l in Object.FindObjectsByType<UnityEngine.Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (l.gameObject.scene == stage.Scene && l.transform.parent != null && l.transform.parent.name == "Evening") l.enabled = false;
                        stage.LightAll();
                        if (legacy) Replace(stage.Scene, Swap(cur, old));
                        var f = FaceOf(twin);
                        foreach (var d in new[] { 1.5f, 0.6f })
                        {
                            stage.Place(f.eyes + f.forward * d + Vector3.down * 0.02f, f.centre);
                            var name = string.Format(CultureInfo.InvariantCulture, "village_twin_{0}_{1:0.0}m", legacy ? "before" : "after", d);
                            Save(stage.Game(), name + "_game");
                            sb.AppendLine(name);
                        }
                    }
                }
                finally
                {
                    if (old != null) old.Destroy();
                }
            }
            return sb.ToString();
        }

        static void Replace(Scene scene, Dictionary<Material, Material> map)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < ms.Length; i++)
                    {
                        Material to;
                        if (ms[i] != null && map.TryGetValue(ms[i], out to)) { ms[i] = to; changed = true; }
                    }
                    if (changed) r.sharedMaterials = ms;
                }
        }
    }
}
