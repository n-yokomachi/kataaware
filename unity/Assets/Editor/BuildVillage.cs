using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 村と庭の場所（<c>Village.unity</c>）を組む。場面 6（庭の記憶）・9（田舎）・10（対面）の舞台
    /// （設計: docs/superpowers/specs/2026-09-26-village-garden-design.md）。
    ///
    /// **二段に分けて組む。いまは一段目。** シーンの土台・片割れの家・その前庭と脇の格子戸・裏庭まで。
    /// 路地の家並み（家 A〜D）・村の小物・車の着く所の物・遠くの書き割りは次の段で、
    /// 並びの場所だけを空けてある（<see cref="LaneWest"/> から片割れの敷地の西の端まで）。
    /// 路地の床と芝の路肩だけは敷いてあり、車の着く所に立った Player が片割れの家の前まで歩ける。
    ///
    /// 大きいので partial に割ってある。ここには入口・並びの寸法・地面・空と時刻・Player・道具を置く。
    /// 家と前庭と格子戸は <c>BuildVillageHouse.cs</c>、裏庭の物は <c>BuildVillageGarden.cs</c>、
    /// 花と葉の札は <c>BuildVillagePlants.cs</c>。
    ///
    /// **組み方は場面 4 と同じ。** 面は <see cref="Bank"/> の箱で組み、素材ごとに一枚へ焼く。
    /// 空・日・霞・環境光は場面 4 の仕組み（<c>BuildDiveSky.cs</c> の <c>PaintedSky</c>）で、時刻ごとに一揃い持つ。
    ///
    /// 寸法は世界の値。+x が東、+z が北。路地は z 0 を東西に走り、片割れの家は路地の北側にある
    /// </summary>
    public static partial class BuildVillage
    {
        public const string ScenePath = "Assets/Scenes/Village.unity";
        public const string Materials = "Assets/Materials/Village/";
        public const string Generated = "Assets/Models/generated/village/";
        public const string Textures = "Assets/Textures/Village/";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        /// <summary>目は顔にある。体の前へこれだけ出す。ほかの場面と同じ</summary>
        const float EyeLead = 0.22f;

        // ---- 並び（設計書 2 節） ---------------------------------------------------------

        /// <summary>路地の片側の幅の半分。一車線で幅 4 m</summary>
        public const float LaneHalf = 2f;
        /// <summary>芝の路肩の幅。歩道は無い</summary>
        public const float Verge = 1.4f;
        /// <summary>路地の北の縁（路肩の外）。片割れの前庭の低い石垣の外の面</summary>
        public const float NorthEdge = LaneHalf + Verge;
        /// <summary>路地の西の端。車の着く所（次の段で農場の門・道標・石垣・麦畑の未舗装路を置く）</summary>
        public const float LaneWest = -80f;
        /// <summary>路地の東の端。片割れの家の先で切る</summary>
        public const float LaneEast = 18f;

        /// <summary>
        /// 車の着く所の立ち位置と向き。路地の西の端から少し入った北の路肩。
        /// 次の段で路肩に車を停めるので、Player はその脇に立つ形にしておく
        /// </summary>
        public static readonly Vector3 ArriveAt = new Vector3(-72f, 0f, 1.2f);
        public const float ArriveYaw = 90f;

        /// <summary>片割れの敷地。前庭の石垣（z <see cref="NorthEdge"/>）から奥の生け垣まで</summary>
        public const float PlotWest = -7f;
        public const float PlotEast = 7f;
        /// <summary>奥の生け垣の手前の面。裏庭は家の裏（z <see cref="HouseRear"/>）からここまでの 20 m</summary>
        public const float PlotNorth = 35.2f;

        // ---- 組み立て --------------------------------------------------------------------

        [MenuItem("HalfAware/Build the village", false, 270)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Open()) return;
            Folders();

            Drop("Main Camera");
            Drop("Directional Light");
            Rig();

            var root = Root("Village");
            Clear(root);
            pathCache = null;
            var hours = Hours(root);
            Ground(Child(root, "Ground"));
            // 家と庭は同じ入れ物に溜めて、素材ごとに一枚へ焼く
            var banks = new Banks();
            House(Child(root, "House"), banks);
            Garden(Child(root, "Garden"), banks);
            banks.Emit(Child(root, "Built"), "Village");
            Plants(Child(root, "Plants"));
            Fences(Child(root, "Bounds"));

            Stage(hours);
            Register();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log(string.Format("村と庭を組んだ。mesh {0} 枚、三角 {1}、当たり {2}",
                root.GetComponentsInChildren<MeshFilter>(true).Length, Triangles(root),
                root.GetComponentsInChildren<Collider>(true).Length));
        }

        /// <summary>
        /// 村のシーンを開く。無ければ作る。
        /// 別のシーンに手を入れたまま呼ばれたら何もしない。ここで開き直すと黙って消える（場面 4 と同じ）
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

        static void Folders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/village"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "village");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Village"))
                AssetDatabase.CreateFolder("Assets/Materials", "Village");
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Village"))
                AssetDatabase.CreateFolder("Assets/Textures", "Village");
        }

        /// <summary>組み立ての一覧に足す。まだ場面の順（SceneMenu）には入れない</summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in all) if (s.path == ScenePath) return;
            all.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = all.ToArray();
        }

        // ---- 地面 -------------------------------------------------------------------------

        /// <summary>
        /// 路地と芝の路肩、片割れの敷地の地面、仮の地面と仮の麦畑。
        ///
        /// **仮の地面は次の段で差し替える。** 家 A〜D の敷地と、路地の南の麦畑、奥の麦畑の丘は
        /// まだ組まないので、平らな面を一枚ずつ敷いて虚空が見えないようにしてある（名前に Placeholder）
        /// </summary>
        static void Ground(Transform parent)
        {
            var lane = new Bank { Texel = 0.3f };
            lane.FaceY(0.02f, LaneWest, LaneEast, -LaneHalf + 0.3f, LaneHalf - 0.3f, 1);
            NoShadow(Emit(parent, "VillageLane", lane, Paint("VillageLane", new Color(0.20f, 0.20f, 0.21f), 0.12f), true));

            // 路地の縁の砂利。舗装の端が崩れて路肩の芝へ混じる帯
            var gravel = new Bank { Texel = 0.3f };
            gravel.FaceY(0.018f, LaneWest, LaneEast, LaneHalf - 0.3f, LaneHalf, 1);
            gravel.FaceY(0.018f, LaneWest, LaneEast, -LaneHalf, -LaneHalf + 0.3f, 1);
            NoShadow(Emit(parent, "VillageGravel", gravel, Paint("VillageGravel", new Color(0.48f, 0.44f, 0.36f), 0.05f), true));

            var verge = new Bank { Texel = 0.3f };
            verge.FaceY(0.01f, LaneWest, LaneEast, LaneHalf, NorthEdge, 1);
            verge.FaceY(0.01f, LaneWest, LaneEast, -NorthEdge, -LaneHalf, 1);
            NoShadow(Emit(parent, "VillageVerge", verge, VergeMat(), true));

            // 片割れの敷地。芝と花の縁の土と菜園を一枚の絵で塗り分ける（GroundPicture）
            var plot = new Bank { Texel = 1f };
            plot.Patch(new Vector3(PicWest, 0f, PicNorth), new Vector3(PicEast, 0f, PicNorth),
                new Vector3(PicEast, 0f, PicSouth), new Vector3(PicWest, 0f, PicSouth),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            NoShadow(Emit(parent, "VillagePlot", plot, GroundMat(), true));

            // 仮の地面。少し下げて、路地と敷地の下まで一枚で敷く。
            // **縁を突き合わせない。** 敷地の地面と高さの違う面を縁で突き合わせると、その 2 cm の段の隙間から
            // 地面の裏の空が覗き、塀の根元に白い点線が出た
            var rest = new Bank { Texel = 0.2f };
            rest.FaceY(-0.02f, LaneWest - 20f, LaneEast + 20f, -NorthEdge - 8f, PlotNorth + 1f, 1);
            NoShadow(Emit(parent, "PlaceholderGround", rest, VergeMat(), false));
            // 仮の麦畑。奥の生け垣の向こうと、路地の南の畑。遠くは次の段で書き割りにする
            var field = new Bank { Texel = 0.1f };
            field.FaceY(-0.02f, LaneWest - 20f, LaneEast + 20f, PlotNorth + 1f, PlotNorth + 140f, 1);
            field.FaceY(-0.02f, LaneWest - 20f, LaneEast + 20f, -NorthEdge - 140f, -NorthEdge - 8f, 1);
            NoShadow(Emit(parent, "PlaceholderField", field, Paint("PlaceholderField", new Color(0.62f, 0.50f, 0.24f), 0.04f), false));
        }

        /// <summary>芝の路肩と仮の地面。地面の絵の芝と揃えた色</summary>
        static Material VergeMat()
        {
            return Paint("VillageVerge", new Color(0.27f, 0.34f, 0.15f), 0.03f);
        }

        // ---- 空と時刻 -----------------------------------------------------------------------

        /// <summary>
        /// 時刻の入れ物。朝と夕方の灯りを子に分けて置き、<see cref="VillageHour"/> が片方だけを点ける。
        /// どちらの時刻もここで空の絵を焼き、空・霞・環境光・日を一揃いで持たせる
        /// </summary>
        static Transform Hours(Transform root)
        {
            var hours = Child(root, "Hours");
            var morning = Child(hours, "Morning");
            var evening = Child(hours, "Evening");
            MorningLights(morning);
            EveningLights(evening);
            return hours;
        }

        /// <summary>
        /// 時刻を繋ぎ、朝で置く。カメラの空の塗り方もここで合わせる
        /// </summary>
        static void Stage(Transform hours)
        {
            var hour = hours.GetComponent<VillageHour>();
            if (hour == null) hour = hours.gameObject.AddComponent<VillageHour>();
            var morning = hours.Find("Morning");
            var evening = hours.Find("Evening");
            // 空の絵を焼くあいだは両方の灯りを点けておく。Sunward が灯りの向きを読むため
            morning.gameObject.SetActive(true);
            evening.gameObject.SetActive(true);
            var so = new SerializedObject(hour);
            WriteSky(so.FindProperty("morning"), MorningSky(morning));
            WriteSky(so.FindProperty("evening"), EveningSky(evening));
            so.FindProperty("morningLights").objectReferenceValue = morning.gameObject;
            so.FindProperty("eveningLights").objectReferenceValue = evening.gameObject;
            var eye = GameObject.Find("Player/Main Camera");
            so.FindProperty("eye").objectReferenceValue = eye != null ? eye.GetComponent<Camera>() : null;
            so.FindProperty("hour").enumValueIndex = (int)VillageHour.Hour.Morning;
            so.ApplyModifiedPropertiesWithoutUndo();
            hour.Apply();
        }

        /// <summary>PlaceSky を直列化した行へ書く（BuildDive.WriteSky と同じ並び）</summary>
        static void WriteSky(SerializedProperty at, PlaceSky sky)
        {
            at.FindPropertyRelative("skybox").objectReferenceValue = sky.skybox;
            at.FindPropertyRelative("flat").colorValue = sky.flat;
            at.FindPropertyRelative("haze").boolValue = sky.haze;
            at.FindPropertyRelative("hazeColor").colorValue = sky.hazeColor;
            at.FindPropertyRelative("hazeDensity").floatValue = sky.hazeDensity;
            at.FindPropertyRelative("ambientSky").colorValue = sky.ambientSky;
            at.FindPropertyRelative("ambientEquator").colorValue = sky.ambientEquator;
            at.FindPropertyRelative("ambientGround").colorValue = sky.ambientGround;
            at.FindPropertyRelative("sun").objectReferenceValue = sky.sun;
        }

        [MenuItem("HalfAware/Village: morning", false, 271)]
        public static void MorningMenu() { SetHour(VillageHour.Hour.Morning); }

        [MenuItem("HalfAware/Village: evening", false, 272)]
        public static void EveningMenu() { SetHour(VillageHour.Hour.Evening); }

        /// <summary>エディタで時刻を切り替える。保存はしない（見比べるための切り替え）</summary>
        public static void SetHour(VillageHour.Hour h)
        {
            var hour = Object.FindFirstObjectByType<VillageHour>(FindObjectsInactive.Include);
            if (hour == null)
            {
                Debug.LogWarning("VillageHour が無い。先に HalfAware/Build the village");
                return;
            }
            var so = new SerializedObject(hour);
            so.FindProperty("hour").enumValueIndex = (int)h;
            so.ApplyModifiedPropertiesWithoutUndo();
            hour.Apply();
            SceneView.RepaintAll();
        }

        // ---- Player -----------------------------------------------------------------------

        /// <summary>
        /// 歩いて見るための Player。場面 8 の rig（<c>BuildDrive.Rig</c>）に倣い、体・目・耳・足音だけを持つ。
        /// SceneFlow の必須や出来事はまだ入れない（設計書 4 節）
        /// </summary>
        static void Rig()
        {
            Drop("Player");
            var player = new GameObject("Player");
            var body = player.AddComponent<CharacterController>();
            body.height = 1.7f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.85f, 0f);
            body.slopeLimit = 45f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.08f;
            body.minMoveDistance = 0.001f;

            var eye = new GameObject("Main Camera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, PlayerController.StandingEyeHeight, EyeLead);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 70f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            eye.AddComponent<AudioListener>();

            var walker = player.AddComponent<PlayerController>();
            var pso = new SerializedObject(walker);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (actions == null) Debug.LogWarning("入力の割り当てが無い: " + ActionsPath);
            pso.FindProperty("actions").objectReferenceValue = actions;
            pso.FindProperty("eye").objectReferenceValue = eye.transform;
            pso.FindProperty("eyeLead").floatValue = EyeLead;
            pso.ApplyModifiedPropertiesWithoutUndo();

            Feet(player.transform, body);

            // 当たりを入れたまま動かすと床や壁に押し出されて狙った場所に立たない（BuildAlley.Place と同じ）
            body.enabled = false;
            player.transform.position = ArriveAt + Vector3.up * 0.06f;
            player.transform.rotation = Quaternion.Euler(0f, ArriveYaw, 0f);
            body.enabled = true;
        }

        /// <summary>場面 1・2 の柔らかい足音。路地の舗装も芝も煉瓦も、これで通す</summary>
        static readonly string[] SoftSteps =
        {
            "Assets/Audio/Step1.wav", "Assets/Audio/Step2.wav", "Assets/Audio/Step3.wav",
            "Assets/Audio/Step4.wav", "Assets/Audio/Step5.wav",
        };

        static void Feet(Transform player, CharacterController body)
        {
            var feet = new GameObject("Feet");
            feet.transform.SetParent(player, false);
            feet.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var src = feet.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 0.55f;
            var steps = feet.AddComponent<Footsteps>();
            var so = new SerializedObject(steps);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("source").objectReferenceValue = src;
            var clips = so.FindProperty("clips");
            clips.arraySize = SoftSteps.Length;
            for (var i = 0; i < SoftSteps.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoftSteps[i]);
                if (clip == null) Debug.LogWarning("足音の素材が無い: " + SoftSteps[i]);
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 当たり -----------------------------------------------------------------------

        /// <summary>
        /// 場所の外へ出さない見えない囲い。路地の両端と南の縁、敷地の外の北の縁。
        /// 敷地の中の塀・生け垣・家・花の縁の当たりは、それぞれを組むところで置く
        /// </summary>
        static void Fences(Transform parent)
        {
            const float high = 2.4f;
            Wall(parent, "LaneSouth", new Vector3(LaneWest, 0f, -NorthEdge), new Vector3(LaneEast, 0f, -NorthEdge), high, 0.4f);
            Wall(parent, "LaneWestEnd", new Vector3(LaneWest, 0f, -NorthEdge), new Vector3(LaneWest, 0f, NorthEdge), high, 0.4f);
            Wall(parent, "LaneEastEnd", new Vector3(LaneEast, 0f, -NorthEdge), new Vector3(LaneEast, 0f, NorthEdge), high, 0.4f);
            // 路地の北の縁。片割れの前庭の石垣のところは石垣が止める
            Wall(parent, "LaneNorthWest", new Vector3(LaneWest, 0f, NorthEdge + 0.2f), new Vector3(PlotWest, 0f, NorthEdge + 0.2f), high, 0.4f);
            Wall(parent, "LaneNorthEast", new Vector3(PlotEast, 0f, NorthEdge + 0.2f), new Vector3(LaneEast, 0f, NorthEdge + 0.2f), high, 0.4f);
        }

        /// <summary>a から b へ伸びる見えない壁。高さと厚み（BuildDive.ParkWall と同じ作り）</summary>
        static void Wall(Transform parent, string name, Vector3 a, Vector3 b, float high, float thick)
        {
            var d = b - a;
            d.y = 0f;
            var len = d.magnitude;
            if (len < 0.01f) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f + Vector3.up * (a.y + high * 0.5f - (a.y + b.y) * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(thick, high, len + thick);
        }

        /// <summary>見えない箱。中心と大きさ（軸に揃える）</summary>
        static void Block(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>折れ線に沿って見えない壁を並べる。花の縁の前の線に使う</summary>
        static void Edge(Transform parent, string name, IList<Vector2> line, float high)
        {
            for (var i = 0; i + 1 < line.Count; i++)
                Wall(parent, name + i, new Vector3(line[i].x, 0f, line[i].y), new Vector3(line[i + 1].x, 0f, line[i + 1].y), high, 0.2f);
        }

        // ---- 道具 -------------------------------------------------------------------------

        /// <summary>
        /// 素材ごとの入れ物。家も庭も同じ入れ物に溜めて、同じ素材の物を一枚の mesh にまとめる。
        /// 描く回数を素材の数に抑えるため（設計書 5 節の 80 回ほどまで）
        /// </summary>
        sealed class Banks
        {
            public readonly Bank Stone = new Bank { Texel = 0.5f };
            public readonly Bank Dressed = new Bank { Texel = 0.5f };
            public readonly Bank Slate = new Bank { Texel = 0.5f };
            public readonly Bank Brick = new Bank { Texel = 1f };
            public readonly Bank Flag = new Bank { Texel = 0.5f };
            public readonly Bank Hedge = new Bank { Texel = 1f };
            public readonly Bank Boards = new Bank { Texel = 1f };
            public readonly Bank Paint = new Bank { Texel = 0.5f };
            public readonly Bank Door = new Bank { Texel = 0.5f };
            public readonly Bank Iron = new Bank { Texel = 0.5f };
            public readonly Bank Dark = new Bank { Texel = 0.5f };
            public readonly Bank Cloth = new Bank { Texel = 0.5f };
            public readonly Bank Glass = new Bank { Texel = 0.5f };
            public readonly Bank Water = new Bank { Texel = 0.5f };
            public readonly Bank Clay = new Bank { Texel = 0.5f };
            public readonly Bank Bark = new Bank { Texel = 0.5f };
            public readonly Bank Soil = new Bank { Texel = 0.5f };

            /// <summary>
            /// 焼いて置く。**平らな物と窓の奥は影を落とさない。** 影は日の側からもう一度描くので、
            /// 落としても絵の変わらない物（敷石・小路・土・窓の奥・ガラス・水・カーテン）まで落とすと、
            /// 描く回数が設計書 5 節の 80 回を超える
            /// </summary>
            public void Emit(Transform parent, string prefix)
            {
                BuildVillage.Emit(parent, prefix + "Stone", Stone, StoneMat(), false);
                BuildVillage.Emit(parent, prefix + "Dressed", Dressed, Paint("VillageDressed", new Color(0.80f, 0.70f, 0.52f), 0.06f), false);
                BuildVillage.Emit(parent, prefix + "Slate", Slate, Pictured("VillageSlate", "VillageSlate.png", Color.white, 0.10f), false);
                NoShadow(BuildVillage.Emit(parent, prefix + "Brick", Brick, Pictured("VillageBrick", "VillageBrick.png", Color.white, 0.06f), false));
                NoShadow(BuildVillage.Emit(parent, prefix + "Flag", Flag, Pictured("VillageFlag", "VillageFlag.png", new Color(1.0f, 0.94f, 0.84f), 0.08f), false));
                BuildVillage.Emit(parent, prefix + "Hedge", Hedge, Pictured("VillageHedge", "VillageHedge.png", Color.white, 0.04f), false);
                BuildVillage.Emit(parent, prefix + "Boards", Boards, Pictured("VillageBoards", "VillageBoards.png", Color.white, 0.05f), false);
                BuildVillage.Emit(parent, prefix + "Paint", Paint, PaintMat(), false);
                BuildVillage.Emit(parent, prefix + "Door", Door, DoorMat(), false);
                BuildVillage.Emit(parent, prefix + "Iron", Iron, IronMat(), false);
                NoShadow(BuildVillage.Emit(parent, prefix + "Dark", Dark, DarkMat(), false));
                NoShadow(BuildVillage.Emit(parent, prefix + "Cloth", Cloth, ClothMat(), false));
                NoShadow(BuildVillage.Emit(parent, prefix + "Glass", Glass, GlassMat(), false));
                NoShadow(BuildVillage.Emit(parent, prefix + "Water", Water, Paint("VillageWater", new Color(0.26f, 0.32f, 0.34f), 0.88f), false));
                BuildVillage.Emit(parent, prefix + "Clay", Clay, Paint("VillageClay", new Color(0.58f, 0.30f, 0.18f), 0.08f), false);
                BuildVillage.Emit(parent, prefix + "Bark", Bark, Paint("VillageBark", new Color(0.38f, 0.33f, 0.26f), 0.04f), false);
                NoShadow(BuildVillage.Emit(parent, prefix + "Soil", Soil, Paint("VillageSoil", new Color(0.20f, 0.15f, 0.10f), 0.02f), false));
            }
        }

        static Material StoneMat() { return Pictured("VillageStone", "VillageStone.png", Color.white, 0.05f); }
        /// <summary>白く塗った木と鉄。格子戸・アーチ・東屋・温室の枠・窓枠・パラソル</summary>
        static Material PaintMat() { return Paint("VillagePaint", new Color(0.86f, 0.85f, 0.81f), 0.18f); }
        /// <summary>戸の色。コッツウォルズの家に多い、くすんだ緑</summary>
        static Material DoorMat() { return Paint("VillageDoor", new Color(0.30f, 0.39f, 0.33f), 0.22f); }
        static Material IronMat() { return Paint("VillageIron", new Color(0.05f, 0.05f, 0.055f), 0.35f); }
        /// <summary>窓の奥と戸口の奥。室内の暗がり</summary>
        static Material DarkMat() { return Paint("VillageDark", new Color(0.035f, 0.032f, 0.030f), 0.30f); }
        /// <summary>カーテンと物干しのタオル。淡い生成り</summary>
        static Material ClothMat() { return Paint("VillageCloth", new Color(0.78f, 0.72f, 0.62f), 0.04f); }

        /// <summary>
        /// 温室のガラス。透かして中の棚と鉢を見せる。
        /// 色の付いた半透明にして、空の明るさを少し拾わせる
        /// </summary>
        static Material GlassMat()
        {
            var m = Paint("VillageGlass", new Color(0.72f, 0.80f, 0.80f, 0.22f), 0.92f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>一色の素材（URP の Lit）。組み直すたびに色と艶を結び直す</summary>
        static Material Paint(string name, Color col, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>絵を貼る素材。絵は tools/make-garden.py が描く</summary>
        static Material Pictured(string name, string picture, Color tint, float smooth)
        {
            var m = Paint(name, tint, smooth);
            m.SetTexture("_BaseMap", Picture(picture, false));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 絵を取り込みの値に揃えて返す。<paramref name="cutout"/> なら α を持つ札の絵で、
        /// mip で α が痩せないよう覆いを保つ（遠い花の縁が消えないように）
        /// </summary>
        static Texture2D Picture(string file, bool cutout)
        {
            var path = Textures + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("絵が無い。python tools/make-garden.py を走らせる: " + path);
                return null;
            }
            var sign = cutout ? "flora1" : "tile1";
            if (importer.userData != sign)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = cutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = cutout;
                importer.mipMapsPreserveCoverage = cutout;
                importer.alphaTestReferenceValue = 0.5f;
                importer.maxTextureSize = 1024;
                importer.userData = sign;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>溜めた面を一枚の mesh にして置く。当たりを入れるのは歩く床だけ</summary>
        static Transform Emit(Transform parent, string name, Bank bank, Material mat, bool collide)
        {
            var made = bank.Emit(parent, name, mat, collide, Generated);
            return made != null ? made.transform : null;
        }

        /// <summary>影を落とさせない。書き割りと仮の地面</summary>
        static void NoShadow(Transform what)
        {
            if (what == null) return;
            var r = what.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Drop(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        static Transform Root(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.SetParent(null, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
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

        static int Triangles(Transform root)
        {
            var n = 0;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) n += (int)(f.sharedMesh.GetIndexCount(0) / 3);
            return n;
        }

        /// <summary>面を一枚。outward の側を表にする（BuildDive.MidQuad）</summary>
        static void Face(Bank b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            BuildDive.MidQuad(b, p0, p1, p2, p3, outward);
        }

        /// <summary>
        /// 多角柱。底の中心・半径・高さ・面の数。側面と天面を張る（底は見えないので張らない）。
        /// 鉢・樽・柱の台・鳥の水盤に使う
        /// </summary>
        static void Prism(Bank b, Vector3 foot, float r, float high, int sides, float top = -1f)
        {
            var rt = top < 0f ? r : top;
            var rim = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var a0 = Mathf.PI * 2f * i / sides;
                var a1 = Mathf.PI * 2f * (i + 1) / sides;
                var p0 = foot + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
                var p1 = foot + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                var q0 = foot + new Vector3(Mathf.Cos(a0) * rt, high, Mathf.Sin(a0) * rt);
                var q1 = foot + new Vector3(Mathf.Cos(a1) * rt, high, Mathf.Sin(a1) * rt);
                var mid = (a0 + a1) * 0.5f;
                Face(b, p0, p1, q1, q0, new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid)));
                // FanY は上から見て左回りに縁を渡す。角を増やす向きがそれに当たる（BuildDive.ParkRim と同じ）
                rim[i] = new Vector2(foot.x + Mathf.Cos(a0) * rt, foot.z + Mathf.Sin(a0) * rt);
            }
            b.FanY(foot + Vector3.up * high, rim);
        }

        /// <summary>a から b へ伸びる角材。太さ（幅・厚み）と、長さの向きに沿った箱</summary>
        static void Beam(Bank b, Vector3 a, Vector3 c, float wide, float thick)
        {
            var d = c - a;
            var len = d.magnitude;
            if (len < 1e-4f) return;
            var dir = d / len;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            b.Box((a + c) * 0.5f, new Vector3(wide, thick, len), Quaternion.LookRotation(dir, up));
        }

        /// <summary>決まった種からの乱数。組み直しても同じ並びになるように、場所ごとに種を渡す</summary>
        static float Hash(int seed, int i)
        {
            var x = Mathf.Sin(seed * 127.1f + i * 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }
    }
}
