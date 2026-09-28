using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// エンディング（シナリオ設計 13 節）の場面 <c>Ending.unity</c> を組む。片割れを助手席に乗せて、村の近くの森の道を走り、
    /// 曲「HALF AWARE」に乗せてクレジットが縦に流れる。
    ///
    /// - 車は場面 8 の車をそのまま組む（<see cref="BuildDrive.CarInto"/>）。変えるのはこの場面の写しだけで、
    ///   助手席を運転席と同じ高さへ下ろし（片割れの頭が天井を抜けないように）、助手席に置いてあったチップの束とログの写しを片づけ、
    ///   前照灯の照らしと雨を伏せる（<see cref="Cabin"/>）
    /// - 景色は帯（森・海辺の崖・ヒースの荒野・麦畑の丘・石垣の丘・ブナの並木・夜の湖）に分け、場面 8 と同じく区切りの環にして流す
    ///   （<see cref="DriveWorld"/>）。帯の表と中身は <c>BuildEndingLand.cs</c>・<c>BuildEndingBands.cs</c>。曲の秒で暗転を挟まずに替わる
    /// - 片割れは助手席に座らせ、座った形のまま一枚のメッシュに焼く（場面 6 の主と同じやり方。<c>BuildEndingTwin.cs</c>）
    /// - クレジットの本文は <c>docs/release/credits.md</c>（オーナーが添削する）。組むたびに読み直して場面へ並べる（<see cref="Credits"/>）
    ///
    /// 組み直すたびに作り直すので、手で触った値は残らない。詰めた値はここへ書き写す
    /// </summary>
    public static partial class BuildEnding
    {
        public const string ScenePath = "Assets/Scenes/Ending.unity";
        public const string SceneName = "Ending";
        public const string Generated = "Assets/Models/generated/ending/";
        public const string Materials = "Assets/Materials/Ending/";
        public const string Textures = "Assets/Textures/Ending/";

        /// <summary>クレジットの本文（repo の根から）。オーナーが添削する</summary>
        public const string CreditsSource = "docs/release/credits.md";
        /// <summary>本文の写し。組み立てが md の「---」と「---」のあいだを書く。md を直したら組み直す</summary>
        public const string CreditsAsset = "Assets/Data/EndingCredits.txt";

        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string MinchoPath = "Assets/Fonts/ShipporiMincho-Regular SDF.asset";
        const string GothicPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

        // ---- 目 ------------------------------------------------------------------------
        //
        // 運転席の目（場面 8 の SeatAt + EyeLead、(0.38, 1.55, 0.22)）から、窓の側（+x）へ寄せる。寄せる量は EndingView.lean の一か所。
        // 2026-09-28 オーナー「カメラ位置自体をもう少し右にして。前の柱が画面中央になるイメージ」で 0.08 m から 0.24 m へ寄せた。
        // 既定の向きで、風防と横の窓のあいだの前の柱が画面の真ん中に来て、左半分が風防の向こうの道、右半分が横の窓の外になる

        /// <summary>Player の根。目はここから前へ <see cref="BuildDrive.EyeLead"/></summary>
        public static Vector3 SeatAt { get { return BuildDrive.SeatAt + EndingView.Default.lean; } }

        /// <summary>目の置き場（車の座標）</summary>
        public static Vector3 EyeAt { get { return SeatAt + new Vector3(0f, 0f, BuildDrive.EyeLead); } }

        /// <summary>
        /// 前を見ている向き。度（左右は +z が 0 で右回り、上下は下が正）。値は <see cref="EndingView.front"/> の一か所
        /// （2026-09-28 オーナー「カメラ位置自体をもう少し右にして。前の柱が画面中央になるイメージ」。寄せた目から見て前の柱が画面の真ん中に来る向き）
        /// </summary>
        public static Vector2 Front { get { return EndingView.Default.front; } }

        /// <summary>
        /// 片割れを見る所（車の座標）。腿の上に置いた手のあたり。ここを画面の真ん中へ持ってくると、
        /// 画面の上の縁が顎より下に来て、顔は入らない（<see cref="TwinLook"/>）
        /// </summary>
        public static readonly Vector3 TwinFocus = new Vector3(-0.44f, 0.98f, 0.28f);

        /// <summary>片割れを見る時の、下を向ける限り。度。主人公の決まり（1 節）と同じ 40 度まで</summary>
        public const float TwinPitchMost = PlayerController.PitchDownLimit;

        /// <summary>カメラの縦の画角。場面 8 と同じ</summary>
        public const float Fov = 70f;

        // ---- 走り ------------------------------------------------------------------------

        // 走る速さと路面の粗さは帯ごと（BuildEndingLand.Route）

        /// <summary>走行音。場面 8 の最後の景色と同じ未舗装の道の輪（村へ続く土の道）</summary>
        public const string RoadSound = "Assets/Audio/DriveGravel.wav";
        public const string IgnitionSound = "Assets/Audio/Ignition.wav";

        // ---- クレジット ------------------------------------------------------------------

        /// <summary>
        /// クレジットを流す所。画面の横の割合。**左右はこの一つの値で替わる**（13.2 節で後で決める）。
        /// 仮に右 1/3（運転席の窓の外の上）
        /// </summary>
        public static readonly Vector2 CreditsColumn = new Vector2(2f / 3f, 1f);

        /// <summary>クレジットの暗がりの濃さの上限（α）</summary>
        public const float CreditsShade = 0.5f;

        [MenuItem("HalfAware/Build the ending", false, 240)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>場面を組んで保存する。書いたことを返す</summary>
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "再生中は組み直さない。止めてからもう一度";
            if (!Open()) return "開いているシーンに未保存の変更がある。保存するか捨ててからもう一度";
            Folders();
            var note = new StringBuilder();

            var root = Root(SceneName);
            Prune(root, new[] { "Car", "Roadsides", "Backdrops", "Twin" });
            var car = Child(root, "Car");
            BuildDrive.CarInto(car);
            Cabin(car, note);
            Twin(Child(root, "Twin"), car, note);
            BuildEndingLand.Build(root, note);

            var rig = Rig(note);
            var sun = Sun();
            var cam = rig.Eye.GetComponent<Camera>();

            var screen = Screen();
            var roll = screen.GetComponent<CreditRoll>();
            note.AppendLine(Credits(roll));

            Wire(root, rig, sun, cam, screen, note);
            AddToBuild(note);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "エンディングを組んだ。\n" + note.ToString().TrimEnd();
        }

        /// <summary>
        /// クレジットだけを並べ直す（md を直した後に）。場面を開いて並べ、保存する
        /// </summary>
        [MenuItem("HalfAware/Write the ending credits", false, 241)]
        public static void CreditsMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は並べ直さない"); return; }
            if (!Open()) { Debug.LogError("開いているシーンに未保存の変更がある"); return; }
            var roll = Object.FindFirstObjectByType<CreditRoll>(FindObjectsInactive.Include);
            if (roll == null) { Debug.LogError("CreditRoll が無い。先に HalfAware/Build the ending を走らせる"); return; }
            var said = Credits(roll);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(said);
        }

        /// <summary>
        /// エンディングのシーンを開く。無ければ作る。別のシーンに手を入れたまま呼ばれたら何もしない（開き直すと黙って消える）
        /// </summary>
        static bool Open()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath && SceneManager.sceneCount == 1) return true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }
            var made = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(made, ScenePath);
            return true;
        }

        static void Folders()
        {
            Folder("Assets/Models/generated", "ending");
            Folder("Assets", "Materials");
            Folder("Assets/Materials", "Ending");
            Folder("Assets/Textures", "Ending");
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // ---- 車の中 ------------------------------------------------------------------------

        /// <summary>助手席の座面の上面。場面 8 の車（BuildDriveCar.PassengerSeat）の値</summary>
        const float PassengerSeatTop = 1.05f;

        /// <summary>
        /// 助手席を下ろす量。運転席（<see cref="BuildDrive.DriverSeatTop"/>）と同じ高さにする。
        /// 場面 8 の助手席は人を座らせない前提の高さ（座面 1.05）で、片割れを座らせると頭が天井（1.88）を 10 cm ほど抜ける。
        /// 運転席も同じ理由で 0.265 下げてある
        /// </summary>
        public static float PassengerDrop { get { return PassengerSeatTop - BuildDrive.DriverSeatTop; } }

        /// <summary>
        /// 助手席と、そこに置いてあった物の範囲（車の座標）。この中の頂点だけを下ろす。
        /// - 座面と土手と枠: 座面の真ん中（x -0.42）の左右 0.29、高さ 1.09 まで。変速の把手の頭（x -0.15 から、高さ 1.108 から）には届かない
        /// - 背もたれと枕と枕の脚: 座面の後ろ（z -0.16 より後ろ）
        /// - チップの束（-0.42, 1.08, -0.02）とログの写し（-0.42, 1.05, 0.14）: その周りの球
        /// </summary>
        static bool InPassenger(Vector3 p)
        {
            if (p.x < -0.73f || p.x > -0.145f) return false;
            if (p.y > 0.85f && p.y < 1.09f && p.z > -0.20f && p.z < 0.32f) return true;
            if (p.y > 0.85f && p.y < 1.77f && p.z > -0.36f && p.z <= -0.16f) return true;
            if ((p - new Vector3(-0.42f, 1.08f, -0.02f)).sqrMagnitude < 0.10f * 0.10f) return true;
            if ((p - new Vector3(-0.42f, 1.05f, 0.14f)).sqrMagnitude < 0.11f * 0.11f) return true;
            return false;
        }

        /// <summary>
        /// 場面 8 の車を、この場面の写しとして手直しする。場面 8 のメッシュは書き換えず、下ろした形を別のメッシュにして差し替える。
        /// - 助手席を運転席と同じ高さへ下ろす（<see cref="PassengerDrop"/>）
        /// - 助手席に置いてあったチップの束とログの写しも一緒に下ろして、片割れの腿の下に隠す（チップの基板と端子は伏せる）
        /// - 風防の雨（Rain）は伏せる。前照灯の照らし（Beam）は夜の帯だけ点く（帯の空の beam。EndingDirector が差し替える）
        /// </summary>
        static void Cabin(Transform car, StringBuilder note)
        {
            foreach (var name in new[] { "Rain", "CarBoard", "CarGold" })
            {
                var t = car.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }
            var moved = new StringBuilder();
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeSelf) continue;
                // 開くドアは助手席の反対の側なので触らない
                if (mf.transform.parent != car) continue;
                var src = mf.sharedMesh;
                var v = src.vertices;
                var count = 0;
                for (var i = 0; i < v.Length; i++)
                {
                    var p = car.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                    if (!InPassenger(p)) continue;
                    p.y -= PassengerDrop;
                    v[i] = mf.transform.InverseTransformPoint(car.TransformPoint(p));
                    count++;
                }
                if (count == 0) continue;
                var copy = Object.Instantiate(src);
                copy.name = src.name + "Ending";
                copy.vertices = v;
                copy.RecalculateBounds();
                var path = Generated + src.name + "Ending.asset";
                ProcMesh.Save(copy, path);
                Object.DestroyImmediate(copy);
                mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                moved.AppendFormat("{0} {1}、", src.name, count);
            }
            note.AppendFormat("車: 場面 8 の車を組み、助手席を {0:0.000} m 下ろした（頂点 {1}）。雨は伏せた。{2}", PassengerDrop,
                moved.Length > 0 ? moved.ToString().TrimEnd('、') : "無し", RoadBeam(car)).AppendLine();
        }

        /// <summary>
        /// 前照灯の照らしを、路面の板だけにした写しに替える（夜の湖の帯で点く）。
        /// 場面 8 の照らしは路面の板と、光の筋を横から切った空中の板でできている（uv の x が 0 なら路面、1 なら空中。BuildDriveCar.Throw）。
        /// この場面は目を窓の側へ寄せて右の窓の外を見るので、空中の板の横の縁が窓の中に明るい縦の帯として見えた
        /// </summary>
        static string RoadBeam(Transform car)
        {
            var beam = car.Find("Beam");
            var mf = beam != null ? beam.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return "前照灯の照らしが無い";
            var src = mf.sharedMesh;
            var uv = src.uv;
            var tris = src.triangles;
            var keep = new List<int>();
            for (var t = 0; t + 2 < tris.Length; t += 3)
                if (uv[tris[t]].x < 0.5f && uv[tris[t + 1]].x < 0.5f && uv[tris[t + 2]].x < 0.5f)
                {
                    keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]);
                }
            var copy = Object.Instantiate(src);
            copy.name = "BeamEnding";
            copy.triangles = keep.ToArray();
            copy.RecalculateBounds();
            var path = Generated + "BeamEnding.asset";
            ProcMesh.Save(copy, path);
            Object.DestroyImmediate(copy);
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            return string.Format("前照灯は路面の板だけ（三角 {0} → {1}）", tris.Length / 3, keep.Count / 3);
        }

        // ---- 目と音 ------------------------------------------------------------------------

        /// <summary>
        /// Player（見回しと歩きはさせない）、目のカメラ、耳、音の口。
        /// 目は場面 8 の乗り込んだ後と同じ作り: 根を目の置き場（<see cref="SeatAt"/>）に置き、目の高さは 0、前へ <see cref="BuildDrive.EyeLead"/>。
        /// 首だけで振り向く（根は前を向いたまま。目の位置は動かない）
        /// </summary>
        static PlayerController Rig(StringBuilder note)
        {
            Drop("Player");
            Drop("Main Camera");
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(SeatAt, Quaternion.identity);

            var eye = new GameObject("Main Camera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, BuildDrive.EyeLead);
            eye.transform.localRotation = Quaternion.Euler(Front.y, Front.x, 0f);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            // 車内でいちばん近いのは天井の板の 0.33 m。場面 8 と同じ
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = Fov;
            eye.AddComponent<AudioListener>();

            var walker = player.AddComponent<PlayerController>();
            var so = new SerializedObject(walker);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (actions == null) note.AppendLine("入力の割り当てが無い: " + ActionsPath);
            so.FindProperty("actions").objectReferenceValue = actions;
            so.FindProperty("eye").objectReferenceValue = eye.transform;
            so.FindProperty("eyeLead").floatValue = BuildDrive.EyeLead;
            so.ApplyModifiedPropertiesWithoutUndo();

            var sound = new GameObject("Sound");
            sound.transform.SetParent(player.transform, false);
            Voice(sound.transform, "Ignition", IgnitionSound, false, note);
            Voice(sound.transform, "Road", RoadSound, true, note);
            Voice(sound.transform, "SongCar", EndingAudioImport.CarPath, false, note);
            Voice(sound.transform, "SongOpen", EndingAudioImport.OpenPath, false, note);
            return walker;
        }

        /// <summary>音の口を一つ。どれも 2D（耳も音も運転席にある）</summary>
        static AudioSource Voice(Transform parent, string name, string clip, bool loop, StringBuilder note)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip);
            if (s.clip == null) note.AppendLine("音の素材が無い: " + clip);
            return s;
        }

        /// <summary>日射し。色と向きは光と空（<see cref="BuildEndingForest"/> の Sky）が持つ</summary>
        static Light Sun()
        {
            var go = Loose("Directional Light");
            var l = go.GetComponent<Light>();
            if (l == null) l = go.AddComponent<Light>();
            // AddComponent<Light> だけでは URP の付属データが付かない（場面 8 と同じ）
            UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(l);
            l.type = LightType.Directional;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            go.transform.position = new Vector3(0f, 8f, 0f);
            return l;
        }

        // ---- 画面 ------------------------------------------------------------------------

        /// <summary>
        /// 画面。二つの Canvas。
        /// - Hud: 粗い画面（UiLens）で描く。クレジットの暗がりと見出しと名前の行、いちばん上に黒（明けとフェードアウト）
        /// - HudCard: 画面の解像度でくっきり描く。クレジットの題と読みだけ（タイトルの画面と同じく粗くしない）
        /// どちらも 1280×720 の拡縮（ほかの場面の Hud と同じ）
        /// </summary>
        static GameObject Screen()
        {
            Drop("Hud");
            Drop("HudCard");
            var hud = Sheet("Hud", 0);
            var card = Sheet("HudCard", UiLens.ShowOrder + 1);

            var column = Panel(hud.transform, "Credits");
            var shade = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            shade.transform.SetParent(column, false);
            var raw = shade.GetComponent<RawImage>();
            raw.texture = ShadeTexture();
            raw.color = new Color(0f, 0f, 0f, 0f);
            raw.raycastTarget = false;
            Stretch(raw.rectTransform);
            var content = Panel(column, "Content");
            var crispColumn = Panel(card.transform, "Credits");
            var crispContent = Panel(crispColumn, "Content");

            // 黒。いちばん上（並びの最後）
            var cover = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cover.transform.SetParent(hud.transform, false);
            var img = cover.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            Stretch(img.rectTransform);

            var roll = hud.AddComponent<CreditRoll>();
            var so = new SerializedObject(roll);
            so.FindProperty("columnFrom").floatValue = CreditsColumn.x;
            so.FindProperty("columnTo").floatValue = CreditsColumn.y;
            so.FindProperty("column").objectReferenceValue = column;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("crispColumn").objectReferenceValue = crispColumn;
            so.FindProperty("crispContent").objectReferenceValue = crispContent;
            so.FindProperty("shade").objectReferenceValue = raw;
            so.FindProperty("shadeAlpha").floatValue = CreditsShade;
            so.ApplyModifiedPropertiesWithoutUndo();
            roll.Fit();
            return hud;
        }

        static GameObject Sheet(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.layer = UiLens.UiLayer;
            return go;
        }

        static RectTransform Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLens.UiLayer;
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            Stretch(r);
            return r;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.gameObject.layer = UiLens.UiLayer;
        }

        /// <summary>
        /// クレジットの暗がりの絵。左の縁から 3 割で濃くなりきる横の段。列の縁に硬い線を引かない。
        /// 色は黒で、濃さは RawImage の色の α が持つ（<see cref="CreditRoll"/> が上げ下げする）
        /// </summary>
        static Texture2D ShadeTexture()
        {
            const string path = Textures + "CreditShade.png";
            const int w = 64;
            var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false, true);
            var px = new Color32[w * 4];
            for (var x = 0; x < w; x++)
            {
                var k = Mathf.Clamp01(x / (w * 0.3f));
                var a = (byte)Mathf.RoundToInt(255f * k * k * (3f - 2f * k));
                for (var y = 0; y < 4; y++) px[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var full = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!File.Exists(full) || !SameBytes(File.ReadAllBytes(full), bytes)) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.mipmapEnabled || imp.wrapMode != TextureWrapMode.Clamp || imp.textureCompression != TextureImporterCompression.Uncompressed))
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // ---- クレジット ------------------------------------------------------------------

        /// <summary>
        /// md（<see cref="CreditsSource"/>）から本文を写し（<see cref="CreditsAsset"/>）、場面へ並べる。
        /// 見出しはしっぽり明朝、名前の行は Noto Sans JP、題と読みは明朝でくっきり
        /// </summary>
        static string Credits(CreditRoll roll)
        {
            var md = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", CreditsSource));
            if (!File.Exists(md)) return "クレジットの本文が無い: " + md;
            var body = CreditsText.Body(File.ReadAllText(md, Encoding.UTF8));
            if (body.Length == 0) return "クレジットの本文に「---」の柵が二つ揃っていない: " + md;
            var full = Path.Combine(Directory.GetCurrentDirectory(), CreditsAsset);
            var bytes = new UTF8Encoding(false).GetBytes(body);
            if (!File.Exists(full) || !SameBytes(File.ReadAllBytes(full), bytes)) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(CreditsAsset, ImportAssetOptions.ForceSynchronousImport);

            var rows = CreditsText.Parse(body);
            var look = CreditsLook();
            roll.Compose(rows, look);
            // 明朝の絵に無い字（焼いていない字）を数える。見出しと題は明朝なので、足りなければ HalfAware/Bake the font に足す
            var missing = new StringBuilder();
            foreach (var r in rows)
            {
                if (r.kind != CreditsText.Kind.Title && r.kind != CreditsText.Kind.Reading && r.kind != CreditsText.Kind.Heading) continue;
                foreach (var ch in r.text)
                    if (look.heading != null && !look.heading.HasCharacter(ch, false, false) && missing.ToString().IndexOf(ch) < 0) missing.Append(ch);
            }
            EditorUtility.SetDirty(roll);
            var headings = 0;
            foreach (var r in rows) if (r.kind == CreditsText.Kind.Heading) headings++;
            return string.Format("クレジット: {0} 行（見出し {1}）、中身の長さ {2:0} px（1280×720 の Canvas）、最後の行の真ん中 {3:0} px。明朝に無い字 {4}",
                rows.Count, headings, roll.Length, roll.LastCentre, missing.Length == 0 ? "無し" : "「" + missing + "」");
        }

        /// <summary>字の大きさと間合い。粗い画面（0.75）の中で、名前の行が縦 10 画素ほどになる大きさから</summary>
        static CreditRoll.Look CreditsLook()
        {
            return new CreditRoll.Look
            {
                heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MinchoPath),
                body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GothicPath),
                titleSize = 46f,
                readingSize = 24f,
                headingSize = 23f,
                lineSize = 19f,
                titleSpacing = 8f,
                readingSpacing = 14f,
                headingSpacing = 3f,
                gap = 46f,
                afterHeading = 10f,
                afterTitle = 10f,
                lineGap = 4f,
                margin = 26f,
                titleColor = new Color(1f, 1f, 1f, 1f),
                headingColor = new Color(0.96f, 0.92f, 0.84f, 1f),
                lineColor = new Color(0.90f, 0.90f, 0.88f, 1f),
            };
        }

        // ---- 繋ぎ込み ------------------------------------------------------------------

        static void Wire(Transform root, PlayerController player, Light sun, Camera cam, GameObject screen, StringBuilder note)
        {
            var world = root.GetComponent<DriveWorld>();
            if (world == null) world = root.gameObject.AddComponent<DriveWorld>();
            var so = new SerializedObject(world);
            // 道も帯の区切りの中に置くので、帯をまたぐタイルは無い
            Fill(so.FindProperty("tiles"), new Transform[0]);
            var bands = root.Find("Roadsides");
            Fill(so.FindProperty("roadsides"), bands != null ? Kids(bands) : new Transform[0]);
            Fill(so.FindProperty("oncoming"), new Transform[0]);
            so.FindProperty("tileLength").floatValue = BuildEndingLand.TileLength;
            so.FindProperty("behind").floatValue = BuildEndingLand.Behind;
            so.FindProperty("oncomingRate").floatValue = 1f;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("shake").floatValue = BuildDrive.Shake;
            so.FindProperty("shakeRate").floatValue = BuildDrive.ShakeRate;
            so.FindProperty("idleRate").floatValue = BuildDrive.IdleRate;
            so.ApplyModifiedPropertiesWithoutUndo();

            Drop("EndingDirector");
            var go = new GameObject("EndingDirector");
            var d = go.AddComponent<EndingDirector>();
            var dso = new SerializedObject(d);
            dso.FindProperty("world").objectReferenceValue = world;
            dso.FindProperty("sun").objectReferenceValue = sun;
            var legs = dso.FindProperty("bands");
            legs.arraySize = BuildEndingLand.Route.Length;
            for (var i = 0; i < BuildEndingLand.Route.Length; i++)
            {
                var leg = BuildEndingLand.Route[i];
                var e = legs.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = leg.name;
                e.FindPropertyRelative("from").floatValue = leg.from;
                e.FindPropertyRelative("speed").floatValue = leg.speed;
                e.FindPropertyRelative("rough").floatValue = leg.rough;
                BuildDriveSky(e.FindPropertyRelative("sky"), leg.sky);
            }
            var far = root.Find("Backdrops");
            var backs = dso.FindProperty("backdrops");
            backs.arraySize = BuildEndingLand.Route.Length;
            for (var i = 0; i < BuildEndingLand.Route.Length; i++)
            {
                var t = far != null ? far.Find("Band" + i) : null;
                backs.GetArrayElementAtIndex(i).objectReferenceValue = t != null ? t.gameObject : null;
            }
            var beam = root.Find("Car/Beam");
            dso.FindProperty("beams").objectReferenceValue = beam != null ? beam.GetComponent<Renderer>() : null;
            dso.FindProperty("player").objectReferenceValue = player;
            dso.FindProperty("eye").objectReferenceValue = cam;
            dso.FindProperty("seat").vector3Value = root.TransformPoint(SeatAt);
            View(dso.FindProperty("view"), EndingView.Default);
            dso.FindProperty("twinProbe").arraySize = 0;
            var probe = TwinProbe(root);
            var probes = dso.FindProperty("twinProbe");
            probes.arraySize = probe.Length;
            for (var i = 0; i < probe.Length; i++) probes.GetArrayElementAtIndex(i).vector3Value = probe[i];
            dso.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            var twin = TwinLook(root);
            dso.FindProperty("twin").vector2Value = twin;
            dso.FindProperty("glanceShift").vector3Value = GlanceShift;
            var sound = player.transform.Find("Sound");
            dso.FindProperty("ignition").objectReferenceValue = Source(sound, "Ignition");
            dso.FindProperty("road").objectReferenceValue = Source(sound, "Road");
            dso.FindProperty("songCar").objectReferenceValue = Source(sound, "SongCar");
            dso.FindProperty("songOpen").objectReferenceValue = Source(sound, "SongOpen");
            dso.FindProperty("lensCanvas").objectReferenceValue = screen.GetComponent<Canvas>();
            var cover = screen.transform.Find("Cover");
            dso.FindProperty("cover").objectReferenceValue = cover != null ? cover.GetComponent<Image>() : null;
            dso.FindProperty("credits").objectReferenceValue = screen.GetComponent<CreditRoll>();
            dso.ApplyModifiedPropertiesWithoutUndo();
            note.AppendFormat("目: 根 {0}、前 {1}、片割れ {2}（下へ {3:0.0} 度。限り {4}）、見回しの限り 右 {5}・左の止め {6}・上 {7}・下 {8}、片割れの点 {9}",
                SeatAt.ToString("F3"), Front, twin, twin.y, TwinPitchMost, EndingView.Default.right, EndingView.Default.left, EndingView.Default.up, EndingView.Default.down, probe.Length).AppendLine();
            // 組み上がった場面の見え方: 明けて走っている所（黒は掛けない）
            var beats = new EndingBeats();
            d.Apply(beats.openFade + 0.5f);
            var c = cover != null ? cover.GetComponent<Image>() : null;
            if (c != null) { c.color = Color.black; c.enabled = true; }
        }

        /// <summary>EndingView の値を、直列化した枠へ一つずつ書く</summary>
        static void View(SerializedProperty into, EndingView from)
        {
            foreach (var f in typeof(EndingView).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var sp = into.FindPropertyRelative(f.Name);
                if (sp == null) continue;
                var v = f.GetValue(from);
                if (v is float) sp.floatValue = (float)v;
                else if (v is Vector2) sp.vector2Value = (Vector2)v;
                else if (v is Vector3) sp.vector3Value = (Vector3)v;
            }
        }

        /// <summary>
        /// 片割れの体の点（場面の座標）。見回しの左の限りを決めるのに使う。座った体のメッシュの頂点を間引いて 1200 点まで
        /// （画面の縁から 3 度の余白を残すので、点のあいだの隙間はその内に収まる。EndingViewTests が全部の頂点で確かめる）
        /// </summary>
        public static Vector3[] TwinProbe(Transform root)
        {
            var list = new List<Vector3>();
            var twin = root.Find("Twin");
            if (twin == null) return list.ToArray();
            foreach (var mf in twin.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled || mf.sharedMesh == null) continue;
                var verts = mf.sharedMesh.vertices;
                var step = Mathf.Max(1, verts.Length / 1200);
                for (var i = 0; i < verts.Length; i += step) list.Add(mf.transform.TransformPoint(verts[i]));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 片割れを見る間に目を寄せる量（Player の根から見た m）。少し身を低くして覗き込む（EndingDirector の glanceShift）。
        /// 片割れの目は運転席の目と同じ高さにあり、下を向ける限り（40 度）では画面の上の縁（水平から 5 度下）が顎に掛かる。
        /// 目を 6 cm 下げると、上の縁は片割れの顎の下（首）まで下がる。顔は自分の窓へ向けてあるので、首の後ろと髪の先だけが縁に掛かる
        /// </summary>
        public static readonly Vector3 GlanceShift = new Vector3(-0.03f, -0.06f, 0.02f);

        /// <summary>
        /// 片割れを見る向き。左右は寄せた目から <see cref="TwinFocus"/> へ。下は限りいっぱいの <see cref="TwinPitchMost"/>
        /// （顔を画面から出すのに、限りまで下を向く）
        /// </summary>
        public static Vector2 TwinLook(Transform root)
        {
            var eye = root.TransformPoint(EyeAt + GlanceShift);
            var focus = root.TransformPoint(TwinFocus);
            var a = Gaze.Angles(eye, focus);
            a.y = TwinPitchMost;
            return a;
        }

        static AudioSource Source(Transform parent, string name)
        {
            var t = parent != null ? parent.Find(name) : null;
            return t != null ? t.GetComponent<AudioSource>() : null;
        }

        /// <summary>DriveSky を書き込む（場面 8 の BuildDrive.FillSky と同じ並べ方。struct なので中身を並べ直す）</summary>
        static void BuildDriveSky(SerializedProperty at, DriveSky from)
        {
            at.FindPropertyRelative("sky").colorValue = from.sky;
            at.FindPropertyRelative("haze").colorValue = from.haze;
            at.FindPropertyRelative("density").floatValue = from.density;
            at.FindPropertyRelative("mist").boolValue = from.mist;
            at.FindPropertyRelative("sun").colorValue = from.sun;
            at.FindPropertyRelative("power").floatValue = from.power;
            at.FindPropertyRelative("aim").vector3Value = from.aim;
            at.FindPropertyRelative("lift").colorValue = from.lift;
            at.FindPropertyRelative("ground").colorValue = from.ground;
            at.FindPropertyRelative("beam").floatValue = from.beam;
        }

        /// <summary>組み立ての一覧（EditorBuildSettings）に足す。村（Village）の後</summary>
        static void AddToBuild(StringBuilder note)
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in all)
                if (s.path == ScenePath) return;
            var at = all.Count;
            for (var i = 0; i < all.Count; i++)
                if (all[i].path == "Assets/Scenes/Village.unity") at = i + 1;
            all.Insert(at, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = all.ToArray();
            note.AppendLine("組み立ての一覧に足した（村の後）");
        }

        // ---- 道具 ------------------------------------------------------------------------

        static Transform Root(string name)
        {
            GameObject go = null;
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == name) go = r;
            if (go == null) go = new GameObject(name);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static GameObject Loose(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) return go;
            return new GameObject(name);
        }

        static void Drop(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) Object.DestroyImmediate(go);
        }

        static void Prune(Transform parent, string[] keep)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (System.Array.IndexOf(keep, c.name) < 0) Object.DestroyImmediate(c.gameObject);
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

        static Transform[] Kids(Transform parent)
        {
            if (parent == null) return new Transform[0];
            var all = new Transform[parent.childCount];
            for (var i = 0; i < all.Length; i++) all[i] = parent.GetChild(i);
            return all;
        }

        static void Fill(SerializedProperty row, Transform[] all)
        {
            row.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }
    }
}
