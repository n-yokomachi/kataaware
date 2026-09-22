using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4（潜る）の <c>Dive.unity</c> を組む。
    ///
    /// 五つの場所と十六の記憶を一つのシーンへ無効のまま置き、潜るときに一組だけ
    /// 有効にする。読み込みを挟むと記憶から記憶へ直に切り替えられない。
    ///
    /// **場所は原点に重ねない。** 一度に一つしか有効にならないので重ねても成り立つが、
    /// 重ねるとシーンビューで五つの箱が絡まって、どれがどの場所か見分けが付かない。
    /// x へ <see cref="PlaceStride"/> ずつ離し、記憶（Take）はその場所と同じ点へ置く。
    /// 鍵打ちも人も場所のローカルで持っているので、離しても値は変わらない。
    ///
    /// 大きいので partial に割ってある。ここには入口・rig・画面・Volume・板・
    /// 繋ぎ込み・見直し・道具を置く。形を作るところは <c>BuildDivePlaces.cs</c>（五つの場所）と
    /// <c>BuildDiveTakes.cs</c>（十六の記憶）にある
    /// </summary>
    public static partial class BuildDive
    {
        public const string ScenePath = "Assets/Scenes/Dive.unity";
        public const string RestPath = "Assets/Scenes/Rest.unity";
        public const string RosterPath = "Assets/Data/DiveRoster.asset";
        public const string Materials = "Assets/Materials/Dive/";
        public const string Generated = "Assets/Models/generated/dive/";
        public const string HoloPath = Materials + "Holo.mat";
        public const string ProfilePath = Materials + "DiveVolume.asset";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        public const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

        /// <summary>右上の行の色。場面 3 の端末と同じ緑</summary>
        static readonly Color Terminal = new Color(0.32f, 0.80f, 0.46f);

        /// <summary>場所どうしの間隔。m。どの場所より広く取って、隣の灯りが届かないようにする</summary>
        public const float PlaceStride = 100f;

        /// <summary>目は顔にある。体の前へこれだけ出す。ほかの場面と同じ</summary>
        const float EyeLead = 0.22f;

        /// <summary>同じ形を何度も並べるので、mesh は名前で引いて使い回す</summary>
        static readonly Dictionary<string, Mesh> shapes = new Dictionary<string, Mesh>();

        // ---- 組み立て ------------------------------------------------------

        [MenuItem("HalfAware/Build the dive", false, 250)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Open()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/dive"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "dive");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Dive"))
                AssetDatabase.CreateFolder("Assets/Materials", "Dive");
            shapes.Clear();

            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(RosterPath);
            if (roster == null)
            {
                Debug.LogError("記憶の一覧が無い。先に HalfAware/Write the dive roster を走らせる: " + RosterPath);
                return;
            }

            // Stage より先に rig を作る。Stage は Player/Main Camera があればそちらへ譲るので、
            // 後から rig を作ると札と AudioListener が二つずつになる（場面 8 と同じ）
            var volume = Lens();
            Rig(volume);
            Stage();

            var root = Root("Dive");
            Prune(root, new[] { "Places", "Takes", "HoloPanel" });
            var places = Places(root);
            var takes = Takes(root, roster);
            Panel(root);
            Wire(root, roster, places, takes);
            Register();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Check(roster, places, takes);
            Selection.activeGameObject = root.gameObject;
            Debug.Log(string.Format("潜る場面を組んだ。場所 {0} 箇所、記憶 {1} 本、人 {2} 人、鍵打ち {3} 打、"
                + "動く物 {4} 個、合わせて {5} 秒",
                places.childCount, takes.childCount, People(takes), Keys(takes),
                takes.GetComponentsInChildren<Mover>(true).Length, Seconds(roster)));
        }

        /// <summary>
        /// 場面 4 のシーンを開く。無ければ作る。
        /// 別のシーンに手を入れたまま呼ばれたら何もしない。ここで開き直すと黙って消える。
        /// **Connect.unity も Room.unity も、この道を通る限り保存されない**
        /// </summary>
        static bool Open()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (!other.isDirty) continue;
                Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + other.path);
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }
            var made = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(made, ScenePath);
            return true;
        }

        /// <summary>
        /// 場所を離して置く量。場所の id の並び（<see cref="DiveIds.Places"/>）がそのまま順になる。
        /// 知らない id は原点へ置く。見直しがそこで気づく
        /// </summary>
        public static Vector3 PlaceOrigin(string id)
        {
            var which = System.Array.IndexOf(DiveIds.Places, id);
            return new Vector3(Mathf.Max(0, which) * PlaceStride, 0f, 0f);
        }

        // ---- シーンの地 ----------------------------------------------------

        /// <summary>
        /// カメラの設定と、場所の外側の暗さ。
        ///
        /// **空は張らない。** 記憶はどれも屋内か暗がりで、空が映るのは団地の階段と公園だけ。
        /// そこも霞んだ一色でよいので、カメラの塗り潰しで済ませる。
        /// 環境光を切ると屋内が真っ黒になるので、灯りの届かないところの下限としてだけ置く
        /// </summary>
        static void Stage()
        {
            var cam = GameObject.Find("Player/Main Camera");
            var c = cam != null ? cam.GetComponent<Camera>() : null;
            if (c == null) { Debug.LogWarning("Player/Main Camera が無い。Stage は Rig の後に呼ぶ"); return; }
            c.nearClipPlane = 0.08f;
            c.farClipPlane = 260f;
            c.fieldOfView = 70f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.055f, 0.060f, 0.072f);
            EditorUtility.SetDirty(c);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.135f, 0.145f, 0.170f);
            RenderSettings.ambientEquatorColor = new Color(0.085f, 0.088f, 0.100f);
            RenderSettings.ambientGroundColor = new Color(0.040f, 0.040f, 0.046f);
            RenderSettings.fog = false;
        }

        // ---- プレイヤーと画面 -------------------------------------------------

        /// <summary>
        /// プレイヤーの rig と画面。空のシーンから組み上げるのでここで作る。
        ///
        /// **CharacterController は有効にする。** 記憶の中でもプレイヤーが歩くので、
        /// 床を踏み、壁で止まる体が要る。記憶の頭で立ち位置へ据えるときだけ
        /// <see cref="DiveDirector"/> が一度切る
        /// </summary>
        static void Rig(Volume volume)
        {
            Drop("Main Camera");
            Drop("Player");
            Drop("Hud");
            Drop("Daze");

            var player = new GameObject("Player");

            var eye = new GameObject("Main Camera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, PlayerController.StandingEyeHeight, EyeLead);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            // 記憶ごとのぼやけと色味は Volume が持つ。カメラが後処理を回さないと何も掛からない
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            // 耳は場面にひとつ。詰まりもここへ掛ける
            eye.AddComponent<AudioListener>();
            var ear = eye.AddComponent<AudioLowPassFilter>();
            ear.cutoffFrequency = HostBody.Open;

            var walker = player.AddComponent<PlayerController>();
            var pso = new SerializedObject(walker);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (actions == null) Debug.LogWarning("入力の割り当てが無い: " + ActionsPath);
            pso.FindProperty("actions").objectReferenceValue = actions;
            pso.FindProperty("eye").objectReferenceValue = eye.transform;
            pso.FindProperty("eyeLead").floatValue = EyeLead;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var body = player.GetComponent<CharacterController>();
            if (body != null)
            {
                body.height = 1.7f;
                // 団地の階段は手すりの内側が 0.9 m しかない。
                // 0.3 の半径だと登りながら両側に触れて、段の途中で止まることがある
                body.radius = 0.26f;
                body.center = new Vector3(0f, 0.85f, 0f);
                body.enabled = true;
            }
            Feet(player.transform, body);

            // 心音は借りた体の内側で鳴るので、距離で薄れないように平らに鳴らす
            var heart = new GameObject("Heart");
            heart.transform.SetParent(player.transform, false);
            var beat = heart.AddComponent<AudioSource>();
            beat.playOnAwake = false;
            beat.loop = true;
            beat.spatialBlend = 0f;
            beat.volume = 0.5f;

            var host = player.AddComponent<HostBody>();
            var hso = new SerializedObject(host);
            hso.FindProperty("player").objectReferenceValue = walker;
            hso.FindProperty("volume").objectReferenceValue = volume;
            hso.FindProperty("ear").objectReferenceValue = ear;
            hso.FindProperty("heart").objectReferenceValue = beat;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // 最初の記憶の頭に据える。DiveDirector が無いあいだ、開いた絵が真っ暗にならないように。
            // 当たりを入れたまま動かすと床や壁に押し出される（BuildAlley.Place と同じ手）
            if (body != null) body.enabled = false;
            player.transform.position = PlaceOrigin(DiveIds.Estate) + FirstStand;
            player.transform.rotation = Quaternion.Euler(0f, FirstYaw, 0f);
            if (body != null) body.enabled = true;

            Screen();
            var daze = new GameObject("Daze");
            daze.AddComponent<DazeVolume>();
        }

        /// <summary>
        /// 団地・教室・台所の床。場面 8 と同じコンクリートの打音（`tools/make-steps.py`）
        /// </summary>
        static readonly string[] HardSteps =
        {
            "Assets/Audio/Concrete1.wav", "Assets/Audio/Concrete2.wav",
            "Assets/Audio/Concrete3.wav", "Assets/Audio/Concrete4.wav",
        };

        /// <summary>公園の土と電車の板。場面 1・2 の柔らかい足音</summary>
        static readonly string[] SoftSteps =
        {
            "Assets/Audio/Step1.wav", "Assets/Audio/Step2.wav", "Assets/Audio/Step3.wav",
            "Assets/Audio/Step4.wav", "Assets/Audio/Step5.wav",
        };

        static AudioClip[] Steps(string[] paths)
        {
            var all = new AudioClip[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                all[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                if (all[i] == null) Debug.LogWarning("足音の素材が無い: " + paths[i]);
            }
            return all;
        }

        /// <summary>
        /// 足音。<see cref="DiveDirector"/> が記憶の頭で床の音を取り替えるので、
        /// ここでは入れ物だけを組んで、コンクリートを初めの一組として入れておく。
        ///
        /// 響きは付けない。五つの場所は屋外の階段から電車の中まで広さも素材も違い、
        /// 一つの <c>AudioReverbFilter</c> で通すと、どこかの場所で必ず嘘になる
        /// </summary>
        static void Feet(Transform player, CharacterController body)
        {
            var feet = new GameObject("Feet");
            feet.transform.SetParent(player, false);
            feet.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var src = feet.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            // 耳と同じ体に付いているので、距離で減らさない
            src.spatialBlend = 0f;
            src.volume = 0.55f;

            var steps = feet.AddComponent<Footsteps>();
            var so = new SerializedObject(steps);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("source").objectReferenceValue = src;
            Fill(so.FindProperty("clips"), Steps(HardSteps));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 記憶ごとのぼやけと色味を持つ Volume。<see cref="HostBody"/> が中身を書き換える。
        ///
        /// profile はアセットとして置く。シーンに埋め込むと、組み直すたびに前の profile が
        /// シーンの中に取り残されて、どれが効いているのか読めなくなる
        /// </summary>
        static Volume Lens()
        {
            Drop("Global Volume");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var blur = Override<DepthOfField>(profile);
            blur.active = false;
            blur.mode.Override(DepthOfFieldMode.Gaussian);
            blur.gaussianStart.Override(HostBody.NearStart);
            blur.gaussianEnd.Override(HostBody.NearEnd);
            blur.gaussianMaxRadius.Override(1f);

            var tone = Override<ColorAdjustments>(profile);
            tone.active = true;
            tone.colorFilter.Override(Color.white);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            // **profile ではなく sharedProfile へ入れる。** profile の set は直列化されない方を
            // 差し替えるので、保存して開き直すと中身の無い Volume になり、
            // ぼやけも色味も掛からないまま記憶だけが流れる。
            // 再生中はこの profile の写しが使われるので、HostBody が書き換えてもアセットは汚れない
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>profile に override を一つ据える。既にあるものは作り直さない</summary>
        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T had;
            if (profile.TryGet(out had)) return had;
            var made = profile.Add<T>(false);
            made.name = typeof(T).Name;
            made.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(made, profile);
            return made;
        }

        /// <summary>同じ名前の根を落とす</summary>
        static void Drop(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) Object.DestroyImmediate(go);
        }

        /// <summary>
        /// 暗転と幕と、字幕帯と、右上の行。
        ///
        /// 右上の行はいま潜っている人が誰かを、字幕帯は記憶の中のやりとりを持つ。
        /// 呼ばれた言葉だけは右上の行と重なるが、そちらは名前と歳と日付の一行で、
        /// 帯に出るのは鉤括弧の台詞なので、同じことを二度言うことにはならない
        /// </summary>
        static HudView Screen()
        {
            var go = new GameObject("Hud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudView));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning("字の形が無い: " + FontPath);

            // 記憶の中にいるあいだ、画面の角へ向かって白く溶ける膜。
            // **いちばん先に置く。** キャンバスは並びの順に重ねるので、
            // 後から置く字幕・案内・右上の行は、どれも膜の上に出て読める
            var haze = new GameObject("Haze", typeof(RectTransform), typeof(CanvasRenderer), typeof(ScreenHaze));
            haze.transform.SetParent(go.transform, false);
            var hazeRect = haze.GetComponent<RectTransform>();
            Frame(hazeRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var hazeView = haze.GetComponent<ScreenHaze>();
            var hso = new SerializedObject(hazeView);
            hso.FindProperty("from").floatValue = HazeFrom;
            hso.FindProperty("upto").floatValue = HazeUpto;
            hso.FindProperty("depth").floatValue = HazeDepth;
            hso.FindProperty("amount").floatValue = 0f;
            hso.ApplyModifiedPropertiesWithoutUndo();
            haze.SetActive(false);

            // **場面 4 の帯だけ薄く、細くする。** 場面 1・2・3・8 では、字幕が出ている
            // あいだ画面の下の案内が消えて、E で送る。その画と見分けが付かないと、
            // 記憶の時計で勝手に流れる会話まで送り待ちに見える。
            // 案内を出さないだけでは合図にならない（絵が同じなので）。
            // 場面 4 の字幕は全部が自動で流れる会話で、`Choice` は一度も出ないから、
            // 帯の見た目を変えても二択と取り違えることは起きない
            var band = Layer(go.transform, "SubtitleBand", new Color(0f, 0f, 0f, 0.35f), true);
            Frame(band, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(0f, 120f));
            var subtitle = Line(band, "Subtitle", font, 28f, Color.white, TextAlignmentOptions.Left);
            // 帯が狭くなったぶん、左右の余白も詰める。160 のままだと
            // 一行に入る幅が七割の帯の中でさらに七割になって、二行の文が四行に割れる
            Frame(subtitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-96f, -24f));
            band.gameObject.SetActive(false);

            var prompt = Line(go.transform, "Prompt", font, 22f, Color.white, TextAlignmentOptions.Center);
            Frame(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(800f, 40f));

            var fade = Layer(go.transform, "Fade", new Color(0f, 0f, 0f, 0f), false);
            Frame(fade, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var curtain = Layer(go.transform, "Curtain", new Color(0f, 0f, 0f, 1f), false);
            Frame(curtain, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            curtain.gameObject.SetActive(false);

            // いま潜っている人の行。端末から抜いてきた字なので、端末と同じ緑で右上に置く
            var caption = Line(go.transform, "Caption", font, 22f, Terminal, TextAlignmentOptions.TopRight);
            Frame(caption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(640f, 40f));

            var centre = Line(go.transform, "Center", font, 40f, Color.white, TextAlignmentOptions.Center);
            Frame(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 80f));

            var hud = go.GetComponent<HudView>();
            var so = new SerializedObject(hud);
            so.FindProperty("subtitleBand").objectReferenceValue = band.gameObject;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("subtitleRowHeight").floatValue = 44f;
            so.FindProperty("subtitlePadding").floatValue = 34f;
            so.FindProperty("promptText").objectReferenceValue = prompt;
            so.FindProperty("centerText").objectReferenceValue = centre;
            so.FindProperty("fadeLayer").objectReferenceValue = fade.GetComponent<Image>();
            so.FindProperty("curtainLayer").objectReferenceValue = curtain.GetComponent<Image>();
            so.FindProperty("hazeLayer").objectReferenceValue = hazeView;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        /// <summary>
        /// 角の白い膜の形。真ん中からの隔たりで測り、辺の真ん中が 1、角が 1.41。
        ///
        /// 立ち上がりを 0.52 に置くのは、画面の真ん中の半分を素のまま残すため。
        /// ここを下げると、見ている物の上にまで白が乗って、記憶の絵ではなく曇りに見える
        /// </summary>
        const float HazeFrom = 0.52f;
        const float HazeUpto = 1.38f;
        /// <summary>角での濃さ。字幕と右上の行が読めることを条件に決めた</summary>
        const float HazeDepth = 0.34f;

        static RectTransform Layer(Transform parent, string name, Color col, bool blocks)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = blocks;
            return go.GetComponent<RectTransform>();
        }

        static TMP_Text Line(Transform parent, string name, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = col;
            text.alignment = align;
            text.text = "";
            return text;
        }

        static void Frame(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 at, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        // ---- 人の脇の板 -------------------------------------------------------

        /// <summary>
        /// 板の幅と高さ。m。
        ///
        /// **設計書は「掌ほど」だが、そこまで小さくすると行が読めない。** 一行目は
        /// 「女　34　『ハンナ』　2156/03/02 09:02」の二十数文字で、掌ほどの板に収めると
        /// 一文字が一度に満たない。読める大きさから逆に決めてある。
        /// 行の書式を短くできるなら、板もそのぶん小さく戻せる。
        ///
        /// 高さだけ 0.28 から広げてある。二行を <see cref="Text3D"/> の大きさで並べると
        /// 上下の余りが 3 px ほどしか残らず、縁に触れて見えた
        /// </summary>
        const float PanelWide = 0.92f;
        const float PanelHigh = 0.30f;

        /// <summary>
        /// 人の脇に浮く板。
        ///
        /// **Quad も 3D の TextMeshPro も -z から見て表になる。** <see cref="HoloPanel"/> は
        /// 目から離れる向きへ forward を置くので、板も字もそのまま読める側を向く。
        /// 字だけ 180 度回して揃えようとすると鏡文字になる（一度やって撮って気づいた）
        /// </summary>
        static void Panel(Transform root)
        {
            var had = root.Find("HoloPanel");
            if (had != null) Object.DestroyImmediate(had.gameObject);

            var go = new GameObject("HoloPanel");
            go.transform.SetParent(root, false);
            var panel = go.AddComponent<HoloPanel>();

            var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pane.name = "Pane";
            Object.DestroyImmediate(pane.GetComponent<MeshCollider>());
            pane.transform.SetParent(go.transform, false);
            pane.transform.localScale = new Vector3(PanelWide, PanelHigh, 1f);
            var holo = AssetDatabase.LoadAssetAtPath<Material>(HoloPath);
            if (holo == null) Debug.LogWarning("板のマテリアルが無い: " + HoloPath);
            pane.GetComponent<MeshRenderer>().sharedMaterial = holo;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var row = Text3D(go.transform, "Row", font, new Vector3(0f, 0.066f, -0.004f), Terminal);
            var action = Text3D(go.transform, "Action", font, new Vector3(0f, -0.070f, -0.004f), Terminal);

            var so = new SerializedObject(panel);
            so.FindProperty("eye").objectReferenceValue = Look("Player/Main Camera");
            so.FindProperty("rowText").objectReferenceValue = row;
            so.FindProperty("actionText").objectReferenceValue = action;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 目を留めた相手が決まるまで出さない。DiveDirector が Show で起こす
            go.SetActive(false);
        }

        /// <summary>
        /// 世界に浮く一行。
        ///
        /// **字の大きさは自動に任せる。** 行は「女　34　『ハンナ』」までで、
        /// 名前の長さが人によって 8〜11 文字ぶん変わる。固定の大きさで流し込むと
        /// 長い名前が板の左右へ突き抜ける。
        ///
        /// **上限は板の幅から逆に出す。** 0.14 だった頃は、一番長い
        /// 「男　78　『アルベルト』」でも板の幅の 15 % しか使わず、427×240 で撮ると
        /// 一文字が 2 px（漢字の墨が 1.96×2.38 px）にしかならなかった。
        /// 0.74 なら同じ行が板の幅の 94 % に届き、一文字が 10 px になる。
        /// 下限は、行の書式が伸びたときに潰れきらない所で止める
        /// </summary>
        static TMP_Text Text3D(Transform parent, string name, TMP_FontAsset font, Vector3 at, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.color = col;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.50f;
            text.fontSizeMax = 0.74f;
            text.text = "";
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(PanelWide * 0.94f, PanelHigh * 0.46f);
            return text;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>
        /// 板・体・段を繋ぐ。
        ///
        /// <see cref="DiveDirector"/> は <c>Dive</c> の根に付ける。場所も記憶も板も
        /// その下にあるので、根に置けば繋ぎ先が全部ひと続きの枝の中に収まる。
        /// <c>CanMove</c> と <c>HeadYawLimit</c> は直列化されない性質なので、ここでは渡さない。
        /// 掛けるのは <see cref="DiveDirector"/> が再生のたびに行う
        /// </summary>
        static void Wire(Transform root, DiveRoster roster, Transform places, Transform takes)
        {
            var panel = root.Find("HoloPanel");
            if (panel == null) { Debug.LogWarning("板が無い。目を繋げない"); return; }
            var holo = panel.GetComponent<HoloPanel>();
            var so = new SerializedObject(holo);
            so.FindProperty("eye").objectReferenceValue = Look("Player/Main Camera");
            so.ApplyModifiedPropertiesWithoutUndo();

            var body = Object.FindFirstObjectByType<HostBody>(FindObjectsInactive.Include);
            if (body == null) { Debug.LogWarning("HostBody が無い。体の差を当てられない"); return; }
            var bso = new SerializedObject(body);
            bso.FindProperty("volume").objectReferenceValue = Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include);
            bso.FindProperty("ear").objectReferenceValue = Object.FindFirstObjectByType<AudioLowPassFilter>(FindObjectsInactive.Include);
            bso.ApplyModifiedPropertiesWithoutUndo();

            var director = root.GetComponent<DiveDirector>();
            if (director == null) director = root.gameObject.AddComponent<DiveDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("player").objectReferenceValue =
                Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            dso.FindProperty("hud").objectReferenceValue =
                Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            dso.FindProperty("caption").objectReferenceValue = Caption();
            dso.FindProperty("daze").objectReferenceValue =
                Object.FindFirstObjectByType<DazeVolume>(FindObjectsInactive.Include);
            dso.FindProperty("volume").objectReferenceValue =
                Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include);
            dso.FindProperty("body").objectReferenceValue = body;
            dso.FindProperty("panel").objectReferenceValue = holo;
            dso.FindProperty("feet").objectReferenceValue =
                Object.FindFirstObjectByType<Footsteps>(FindObjectsInactive.Include);
            Fill(dso.FindProperty("hardSteps"), Steps(HardSteps));
            Fill(dso.FindProperty("softSteps"), Steps(SoftSteps));
            dso.FindProperty("roster").objectReferenceValue = roster;
            Fill(dso.FindProperty("places"), Named(places, DiveIds.Places));
            var sky = dso.FindProperty("skies");
            sky.arraySize = Skies.Length;
            for (var i = 0; i < Skies.Length; i++) sky.GetArrayElementAtIndex(i).colorValue = Skies[i];
            Fill(dso.FindProperty("takes"), Numbered(takes, roster.Count));
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 場所ごとの空の色。<see cref="DiveIds.Places"/> と同じ並び。
        ///
        /// 開口の向こうと、見上げた先に出る。時刻は記憶の行のとおりで、
        /// 団地が朝の七時と九時、公園が午後の三時台、電車が夕方の六時台、
        /// 台所が朝の七時前、教室が昼前。**真っ黒のままにしない。**
        /// 公園で見上げる記憶と、団地の廊下から外を向いたときに画面の上が抜ける
        /// </summary>
        static readonly Color[] Skies =
        {
            new Color(0.42f, 0.47f, 0.55f),   // 団地。朝の薄い青
            new Color(0.60f, 0.57f, 0.48f),   // 公園。午後の黄ばんだ白
            new Color(0.07f, 0.08f, 0.11f),   // 電車。夜の街。窓は自分で光る
            new Color(0.50f, 0.56f, 0.62f),   // 台所。朝の白
            new Color(0.70f, 0.72f, 0.74f),   // 教室。昼の白
        };

        /// <summary>右上の行。HUD の下に一枚だけある</summary>
        static TMP_Text Caption()
        {
            var at = Look("Hud/Caption");
            if (at == null) return null;
            var text = at.GetComponent<TMP_Text>();
            if (text == null) Debug.LogWarning("右上の行に TMP_Text が付いていない");
            return text;
        }

        /// <summary>名前の並びで子を探す。並びがそのまま繋ぎ先の並びになる</summary>
        static Transform[] Named(Transform parent, string[] names)
        {
            var all = new Transform[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                all[i] = parent != null ? parent.Find(names[i]) : null;
                if (all[i] == null) Debug.LogWarning("繋ぎ先が見つからない: " + names[i]);
            }
            return all;
        }

        /// <summary>番号の名前で子を探す。一覧の番号がそのまま繋ぎ先の並びになる</summary>
        static Transform[] Numbered(Transform parent, int count)
        {
            var all = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                all[i] = parent != null ? parent.Find(i.ToString()) : null;
                if (all[i] == null) Debug.LogWarning("繋ぎ先が見つからない: 記憶 " + i);
            }
            return all;
        }

        /// <summary>
        /// 組み立ての一覧へ入れる。<c>SceneManager.LoadScene</c> は入っていないシーンを読めない。
        /// <c>Rest.unity</c> はまだ無いので、出来てから入る
        /// </summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var added = 0;
            foreach (var path in new[] { ScenePath, RestPath })
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                if (all.Exists(s => s.path == path)) continue;
                all.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }
            if (added == 0) return;
            EditorBuildSettings.scenes = all.ToArray();
            Debug.Log("組み立ての一覧へ " + added + " 本のシーンを入れた");
        }

        // ---- 見直し ----------------------------------------------------------

        /// <summary>
        /// 組み終えたら必ず見直す。目で気づくまで放っておかない。
        ///
        /// 見るのは五つ。場所が揃っているか、記憶の番号と一覧が並んでいるか、
        /// 人の名前が <see cref="Seen.name"/> と一字も違わないか、鍵打ちの最後が
        /// <see cref="DiveEntry.length"/> と揃っているか、鍵打ちと人が場所の中に収まっているか
        /// </summary>
        static void Check(DiveRoster roster, Transform places, Transform takes)
        {
            foreach (var id in DiveIds.Places)
                if (places.Find(id) == null) Debug.LogWarning("見直し: 場所が無い " + id);

            for (var i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                var take = takes.Find(i.ToString());
                if (take == null) { Debug.LogWarning("見直し: 記憶が無い " + i); continue; }
                var t = take.GetComponent<Take>();
                if (t == null) { Debug.LogWarning("見直し: Take が付いていない " + i); continue; }

                if (t.Entry != i) Debug.LogWarning(string.Format("見直し: 記憶 {0} の番号が {1} になっている", i, t.Entry));
                if (places.Find(entry.place) == null)
                    Debug.LogWarning(string.Format("見直し: 記憶 {0} の場所 {1} が Places に無い", i, entry.place));
                if (Mathf.Abs(t.Length - entry.length) > 0.01f)
                    Debug.LogWarning(string.Format("見直し: 記憶 {0} の鍵打ちは {1} 秒で終わるが、一覧は {2} 秒",
                        i, t.Length, entry.length));
                if (t.Keys.Length < 2)
                    Debug.LogWarning(string.Format("見直し: 記憶 {0} の鍵打ちが {1} 打しかない", i, t.Keys.Length));

                var seen = entry.seen ?? new Seen[0];
                for (var k = 0; k < seen.Length; k++)
                    if (t.Person(seen[k].name) == null)
                        Debug.LogWarning(string.Format("見直し: 記憶 {0} に {1} がいない", i, seen[k].name));
                if (t.People.Length != seen.Length)
                    Debug.LogWarning(string.Format("見直し: 記憶 {0} の板の相手が {1} 人、一覧は {2} 人",
                        i, t.People.Length, seen.Length));

                Inside(i, entry, t, places);
            }
        }

        /// <summary>
        /// 鍵打ちと人が場所の外へ出ていないか。床を突き抜けても壁の外へ出ても、
        /// 再生して初めて真っ暗な虚空に立っていることに気づくことになる
        /// </summary>
        static void Inside(int which, DiveEntry entry, Take take, Transform places)
        {
            var place = places.Find(entry.place);
            if (place == null) return;
            var box = Reach(place);
            if (box.size == Vector3.zero) return;
            // 場所の当たりは箱の見立てなので、少しはみ出すぶんは通す
            box.Expand(1.2f);

            var origin = PlaceOrigin(entry.place);
            for (var i = 0; i < take.Keys.Length; i++)
            {
                var at = origin + take.Keys[i].position;
                if (box.Contains(at)) continue;
                Debug.LogWarning(string.Format("見直し: 記憶 {0} の {1} 打目 {2} 秒が場所 {3} の外に出ている {4}",
                    which, i, take.Keys[i].at, entry.place, take.Keys[i].position.ToString("F2")));
            }
            for (var i = 0; i < take.People.Length; i++)
            {
                if (take.People[i] == null) continue;
                if (box.Contains(take.People[i].position)) continue;
                Debug.LogWarning(string.Format("見直し: 記憶 {0} の {1} が場所 {2} の外に立っている {3}",
                    which, take.People[i].name, entry.place, take.People[i].localPosition.ToString("F2")));
            }
        }

        /// <summary>
        /// 場所の広がり。伏せてある物も測るので、レンダラーではなく mesh と transform から出す。
        /// 無効な GameObject のレンダラーは範囲を返さないことがある
        /// </summary>
        static Bounds Reach(Transform place)
        {
            var box = new Bounds();
            var first = true;
            foreach (var f in place.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var local = f.sharedMesh.bounds;
                var at = f.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var world = at.MultiplyPoint3x4(corner);
                    if (first) { box = new Bounds(world, Vector3.zero); first = false; }
                    else box.Encapsulate(world);
                }
            }
            return box;
        }

        static int People(Transform takes)
        {
            var n = 0;
            foreach (var t in takes.GetComponentsInChildren<Take>(true)) n += t.People.Length;
            return n;
        }

        static int Keys(Transform takes)
        {
            var n = 0;
            foreach (var t in takes.GetComponentsInChildren<Take>(true)) n += t.Keys.Length;
            return n;
        }

        static int Seconds(DiveRoster roster)
        {
            var n = 0f;
            for (var i = 0; i < roster.Count; i++) n += roster[i].length;
            return Mathf.RoundToInt(n);
        }

        // ---- 道具 ----------------------------------------------------------

        /// <summary>繋ぎ先を探す。黙って null を渡すと、再生して初めて気づくことになる</summary>
        static Transform Look(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) Debug.LogWarning("繋ぎ先が見つからない: " + path);
            return go != null ? go.transform : null;
        }

        /// <summary>溜めた面を mesh のアセットにして返す</summary>
        static Mesh Bake(Bank bank, string name)
        {
            var scratch = new GameObject("BakeScratch");
            var made = bank.Emit(scratch.transform, name, null, false, Generated);
            var mesh = made != null ? made.GetComponent<MeshFilter>().sharedMesh : null;
            Object.DestroyImmediate(scratch);
            return mesh;
        }

        /// <summary>名前で引ける形。同じ名前なら二度焼かない</summary>
        static Mesh Shape(string name, float texel, System.Action<Bank> draw)
        {
            Mesh mesh;
            if (shapes.TryGetValue(name, out mesh) && mesh != null) return mesh;
            var bank = new Bank { Texel = texel };
            draw(bank);
            mesh = Bake(bank, name);
            shapes[name] = mesh;
            return mesh;
        }

        /// <summary>形を一つ置く</summary>
        static Transform Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go.transform;
        }

        /// <summary>素材ごとに溜めた面を、その素材のマテリアルで一枚に焼いて置く</summary>
        static Transform Emit(Transform parent, string name, Bank bank, string material)
        {
            return Emit(parent, name, bank, material, false);
        }

        /// <summary>
        /// 当たりを入れて置く。
        ///
        /// **入れるのは床と壁だけ。** 記憶の中をプレイヤーが歩くようになったので、
        /// 踏む面と外へ出さない面が要る。天井・遠景の書き割り・家具の細かいところは、
        /// 入れても歩く先が変わらないわりに、引っ掛かって抜け出せない隅が増える
        /// </summary>
        static Transform Emit(Transform parent, string name, Bank bank, string material, bool collide)
        {
            var made = bank.Emit(parent, name, Mat(material), collide, Generated);
            return made != null ? made.transform : null;
        }

        /// <summary>
        /// 見えない仕切り。
        ///
        /// 団地の地面も公園の地面も、端から先は何も無い虚空で、歩いて行けば落ちる。
        /// 絵に出さずに止めたいので、レンダラーを持たない当たりだけを置く
        /// </summary>
        static void Fence(Transform place, string name, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(place, false);
            go.transform.localPosition = at;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>場所の地面をぐるりと囲う。x と z の幅、囲いの高さ、厚み</summary>
        static void Ring(Transform place, string name, Vector2 x, Vector2 z, float high)
        {
            const float thick = 0.4f;
            var mid = new Vector3((x.x + x.y) * 0.5f, high * 0.5f, (z.x + z.y) * 0.5f);
            var span = new Vector3(x.y - x.x, high, z.y - z.x);
            Fence(place, name + "X0", new Vector3(x.x - thick * 0.5f, mid.y, mid.z), new Vector3(thick, high, span.z));
            Fence(place, name + "X1", new Vector3(x.y + thick * 0.5f, mid.y, mid.z), new Vector3(thick, high, span.z));
            Fence(place, name + "Z0", new Vector3(mid.x, mid.y, z.x - thick * 0.5f), new Vector3(span.x, high, thick));
            Fence(place, name + "Z1", new Vector3(mid.x, mid.y, z.y + thick * 0.5f), new Vector3(span.x, high, thick));
        }

        /// <summary>
        /// 影を落とさせない。遠景の板は実体ではなく書き割りなので、
        /// 落とすと十数メートルの帯が場所の真ん中まで伸びて、そこだけ夜になる
        /// </summary>
        static void NoShadow(Transform what)
        {
            if (what == null) return;
            var r = what.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
        }

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

        static void Fill(SerializedProperty row, Object[] all)
        {
            row.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }

        /// <summary>
        /// 素材ごとのマテリアル。組み直すたびに色と艶を結び直すので、
        /// Inspector で触った値は残らない。詰めるならこの表を直す
        /// </summary>
        static Material Mat(string name)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            Color col;
            float smooth;
            Tone(name, out col, out smooth);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", name == "Rail" ? 0.6f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 素材ごとの色と艶。**どれも暗い。** 記憶は他人の頭の中から抜いてきた絵で、
        /// 明るさと色味は記憶ごとに Volume（Color Adjustments）が寄せる前提にしてある。
        /// 地の色を明るく置くと、色味を掛けたときに白く飛ぶ
        /// </summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                case "Floor": col = new Color(0.255f, 0.250f, 0.238f); smooth = 0.08f; break;
                case "Wall": col = new Color(0.118f, 0.120f, 0.130f); smooth = 0.05f; break;
                case "Ceiling": col = new Color(0.068f, 0.070f, 0.078f); smooth = 0.03f; break;
                case "Rail": col = new Color(0.205f, 0.215f, 0.230f); smooth = 0.45f; break;
                case "Timber": col = new Color(0.185f, 0.142f, 0.098f); smooth = 0.12f; break;
                case "Cloth": col = new Color(0.142f, 0.132f, 0.155f); smooth = 0.06f; break;
                case "Ground": col = new Color(0.150f, 0.163f, 0.122f); smooth = 0.04f; break;
                case "Leaf": col = new Color(0.062f, 0.078f, 0.056f); smooth = 0.02f; break;
                case "Board": col = new Color(0.082f, 0.112f, 0.092f); smooth = 0.10f; break;
                case "Bird": col = new Color(0.235f, 0.238f, 0.248f); smooth = 0.06f; break;
                case "Door": col = new Color(0.108f, 0.092f, 0.082f); smooth = 0.10f; break;
                case "Water": col = new Color(0.092f, 0.108f, 0.124f); smooth = 0.30f; break;
                default: col = new Color(0.150f, 0.150f, 0.155f); smooth = 0.06f; break;
            }
        }

        /// <summary>
        /// 自分で光って見える面。窓の外もテレビも黒板の上の蛍光灯も、
        /// 灯りを一つずつ置くと WebGL では持たないので明るい Unlit で済ませる
        /// </summary>
        static Material Glow(string name, Color col, float gain)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(col.r * gain, col.g * gain, col.b * gain, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>灯りを一つ据える</summary>
        static Light Lamp(Transform parent, string name, LightType kind, Vector3 at, Vector3 aim,
            Color col, float power, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(aim);
            var l = go.AddComponent<Light>();
            l.type = kind;
            l.color = col;
            l.intensity = power;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>鍵打ち一つ。位置は場所のローカル、yaw は +z が 0、pitch は下が正</summary>
        static HostKey K(float at, float x, float y, float z, float yaw, float pitch, float eye)
        {
            return new HostKey
            {
                at = at,
                position = new Vector3(x, y, z),
                yaw = yaw,
                pitch = pitch,
                eyeHeight = eye,
            };
        }
    }
}
