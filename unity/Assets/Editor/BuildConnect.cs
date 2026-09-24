using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 3（自室・接続）を、場面 1 の <c>Room.unity</c> から組み直す。
    ///
    /// **部屋の地形と小物の出処は Room.unity ひとつだけ。** 同じ部屋を二度組むと、
    /// 片方だけ直したときに朝の部屋と夕方の部屋で寸法が食い違う。
    ///
    /// **開いたらその場で Connect.unity へ名前を移す。** 以降の書き換えはすべて
    /// 複製の側に載る。Room.unity を保存すると場面 1 が丸ごと壊れる。
    ///
    /// 詰め直しは「場面 1 のものを落とす」「夕方の状態に置き直す」「場面 3 のものを足す」の順。
    /// 落とすのが先なのは、JackPull と JackPlug が同じ id で動くため
    /// </summary>
    public static class BuildConnect
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string ScenePath = "Assets/Scenes/Connect.unity";
        public const string ScriptPath = "Assets/Data/ConnectScript.asset";
        public const string Materials = "Assets/Materials/Connect";
        public const string FacePath = Materials + "/TerminalFace.mat";
        public const string PanePath = Materials + "/TerminalPane.mat";
        public const string RowsPath = "Assets/Textures/TerminalRows.png";
        public const string PlugPath = "Assets/Audio/JackPlug.wav";

        /// <summary>手首に残す受け口の名前</summary>
        public const string SocketName = "JackSocket";

        /// <summary>戸口の内側。場面 2 の暗転から、ここで部屋の奥を向いて明ける</summary>
        static readonly Vector3 StartAt = new Vector3(0.80f, 0.05f, -2.45f);
        /// <summary>腰を下ろす場所。場面 1 が座って始まるのと同じ点</summary>
        public static readonly Vector3 SeatAt = new Vector3(1.50f, 0.05f, 1.20f);
        /// <summary>ソファの上の売上メモ</summary>
        static readonly Vector3 NoteAt = new Vector3(-2.40f, 0.67f, 0.19f);
        /// <summary>椅子。座面ではなく、立って見下ろせる高さに置く</summary>
        static readonly Vector3 ChairAt = new Vector3(1.50f, 0.75f, 1.20f);
        /// <summary>モニター。monitor / list / dive は順に開くので同じ点でよい</summary>
        public static readonly Vector3 ScreenAt = new Vector3(1.50f, 1.10f, 2.42f);

        /// <summary>座ったときの目線の高さ。ConnectDirector へ渡す</summary>
        public const float SeatEyeHeight = 1.1f;
        public const float ItemRadius = 2f;
        /// <summary>ジャックだけ近い。腕の上のものを部屋の向こうから拾わせない</summary>
        public const float JackRadius = 1.2f;

        /// <summary>消えている画面の色。TerminalScreen の off と揃える。点いた色はあちらが持つ</summary>
        static readonly Color ScreenOff = new Color(0.035f, 0.040f, 0.045f);

        // ---- 組み立て ------------------------------------------------------

        [MenuItem("HalfAware/Build the connect scene", false, 240)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Rename()) return;

            Strip();
            Flow();
            Stand();
            var socket = Park();
            var items = Items();
            var sheet = Screens();
            Wire(socket, sheet, items);
            Register();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Check(sheet.backs, items);
            Debug.Log(string.Format("夕方の部屋を組んだ。調べる対象 {0} 個（頭から開くのは {1} 個）、"
                + "画面 {2} 枚に窓 {6} 個。立ち位置 {3} から椅子 {4} まで約 {5:F1} m",
                items.Count, Open(items), sheet.backs.Length, StartAt.ToString("F2"), SeatAt.ToString("F2"),
                Vector3.Distance(new Vector3(StartAt.x, 0f, StartAt.z), new Vector3(SeatAt.x, 0f, SeatAt.z)),
                sheet.panes.Length));
        }

        /// <summary>
        /// 組み立ての一覧へ入れる。<c>SceneManager.LoadScene</c> は入っていないシーンを読めないので、
        /// Tab の一覧からここへ飛べない
        /// </summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (all.Exists(s => s.path == ScenePath)) return;
            all.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = all.ToArray();
            Debug.Log("組み立ての一覧へ Connect.unity を入れた");
        }

        /// <summary>
        /// 場面 1 を開いて、その場で Connect.unity として名前を移す。
        ///
        /// **開いてから名前を移すまでのあいだは何も触らない。** ここで手を入れると、
        /// 保存が途中で失敗したときに場面 1 が書き換わったまま残る。
        /// 未保存のシーンが開いているときは何もしない。開き直すと黙って消える
        /// </summary>
        static bool Rename()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (!other.isDirty) continue;
                Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + other.path);
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RoomPath) == null)
            {
                Debug.LogError("場面 1 が無い: " + RoomPath);
                return false;
            }
            var scene = EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Connect.unity として保存できなかった。場面 1 を守るためここで止める");
                return false;
            }
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            Debug.LogError("名前が移っていない。まだ Room.unity を開いたままなので止める");
            return false;
        }

        // ---- 場面 1 のものを落とす -------------------------------------------

        /// <summary>
        /// 場面 1 の頭の演出と、場面 1 の調べる対象を落とす。
        /// 対象は入れ物の下だけでなく手首のジャックにも付いているので、
        /// 名前ではなく付いているコンポーネントで拾う
        /// </summary>
        static void Strip()
        {
            var intro = Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro != null) Object.DestroyImmediate(intro);
            foreach (var item in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(item.gameObject);
            Sold();
        }

        /// <summary>
        /// 抜き差し台のメモリは場面 2 で売り切れている。チップと札を落とし、
        /// 場面 1 で持ち出したときと同じところまで灯りを落とす。
        /// 持ち出しの仕掛け（<see cref="Taken"/>）は id が場面 1 のものなので、
        /// 置いておいても二度と動かない。落とす前に灯りの替えだけ写し取る
        /// </summary>
        static void Sold()
        {
            var hub = Look("Room/MemoryHub");
            if (hub == null) return;
            var taken = hub.GetComponent<Taken>();
            if (taken != null)
            {
                var so = new SerializedObject(taken);
                var lamp = so.FindProperty("lamp").objectReferenceValue as Material;
                var relit = so.FindProperty("relit");
                for (var i = 0; i < relit.arraySize; i++)
                {
                    var r = relit.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    if (r == null || lamp == null) continue;
                    r.sharedMaterial = lamp;
                    EditorUtility.SetDirty(r);
                }
                Object.DestroyImmediate(taken);
            }
            for (var i = hub.childCount - 1; i >= 0; i--)
            {
                var c = hub.GetChild(i);
                if (!c.name.StartsWith("Chip") && !c.name.StartsWith("Label")) continue;
                Object.DestroyImmediate(c.gameObject);
            }
        }

        // ---- 夕方の状態に置き直す ---------------------------------------------

        /// <summary>
        /// 場面 3 の SceneFlow。座って始まる仕度と、場面 1 の頭と終わりの段取りを外す。
        /// SceneFlow そのものには手を入れない（場面 1・2・8 が同じものに乗っている）
        /// </summary>
        static void Flow()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { Debug.LogError("SceneFlow が無い"); return; }
            var so = new SerializedObject(flow);
            // 空だと Awake が座位の仕度を丸ごと飛ばす。場面 3 は立って始まる
            so.FindProperty("standAfter").stringValue = "";
            // 「潜る」で二択に「はい」と答えたら、そのまま場面 4 へ渡す
            so.FindProperty("nextScene").stringValue = "Dive";
            // 場面 2 の暗転から続くので見出しを挟まない
            so.FindProperty("openingCard").stringValue = "";
            so.FindProperty("cutToBlack").boolValue = false;
            // 部屋を出ないので扉の音は鳴らない
            so.FindProperty("exitSound").objectReferenceValue = null;
            so.FindProperty("dazeUntil").stringValue = "";
            // **眩暈は繋がない。** dazeUntil が空でも ReleaseDaze は最初の Update で
            // Decay を呼ぶので、繋いだままだと場面の頭に 5 秒ぶんの眩暈が乗る。
            // 眩暈は場面 1 の目覚めのもの
            so.FindProperty("daze").objectReferenceValue = null;
            // **空にする。** Awake は standAfter が空でも chairBlocker.SetActive(false) を
            // 無条件に呼ぶ。歩き回る場面で椅子の当たりを切られると、椅子をすり抜ける。
            // 座ったところで切るのは ConnectDirector の仕事
            so.FindProperty("chairBlocker").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        /// <summary>
        /// 戸口の内側に立たせる。椅子の当たりは入れておく。
        /// 座位の姿勢（<see cref="SeatedPose.Seated"/>）は直列化されないので、
        /// 解くのは ConnectDirector が再生のたびにやる
        /// </summary>
        static void Stand()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogError("PlayerController が無い"); return; }
            player.transform.position = StartAt;
            player.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(player);
            var pose = Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
            if (pose != null) { pose.Seated = false; EditorUtility.SetDirty(pose); }
            var blocker = Look("Room/Chair/Blocker");
            if (blocker != null) blocker.gameObject.SetActive(true);
        }

        /// <summary>主人公の体の骨（Humanoid）。右の手首はジャックの受け口、左の手は掴む置き所を持つ</summary>
        public static Transform Bone(HumanBodyBones bone)
        {
            var pro = Look("Player/Protagonist");
            var an = pro != null ? pro.GetComponent<Animator>() : null;
            if (an == null) { Debug.LogWarning("主人公の Animator が無い"); return null; }
            return an.GetBoneTransform(bone);
        }

        /// <summary>
        /// ジャックは肘掛けに置いてある。場面 1 の終わりに置いたままの形。
        /// 手首には受け口だけを残す。
        ///
        /// **受け口を空の GameObject で残すのは、ジャックの置き方と大きさを覚えさせるため。**
        /// JackPlug は挿さったところでジャックを受け口の原点へ等倍で置く。
        /// 受け口が元の置き方と大きさを覚えていれば、挿した後の見え方は場面 1 の頭とそのまま同じになる
        /// </summary>
        static Transform Park()
        {
            var wrist = Bone(HumanBodyBones.RightHand);
            var rest = Look("Room/Chair/JackRest");
            if (wrist == null || rest == null) return null;
            var jack = wrist.Find("Jack");
            if (jack == null) { Debug.LogWarning("手首にジャックが無い"); return null; }
            var socket = wrist.Find(SocketName);
            if (socket == null)
            {
                var go = new GameObject(SocketName);
                go.transform.SetParent(wrist, false);
                socket = go.transform;
            }
            socket.localPosition = jack.localPosition;
            socket.localRotation = jack.localRotation;
            socket.localScale = jack.localScale;
            jack.SetParent(rest, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
            return socket;
        }

        // ---- 調べる対象 ------------------------------------------------------

        /// <summary>
        /// 6 つとも必須。<c>jack</c> と <c>monitor</c> だけは伏せて始める。
        /// after は「その id が済んだか」しか見ないので、座る演出と挿す演出の途中で
        /// 次が拾えてしまう。演出の終わりで ConnectDirector が開けば、
        /// 開く時刻が演出の終わりと一致する
        /// </summary>
        static Dictionary<string, GameObject> Items()
        {
            var made = new Dictionary<string, GameObject>();
            var parent = Look("Interactables");
            if (parent == null) return made;
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
                Debug.LogWarning("場面 3 の文面が無い。先に HalfAware/Write the connect script を走らせる: " + ScriptPath);

            made[ConnectIds.Note] = Put(parent, "Note", NoteAt, script, ConnectIds.Note, ItemRadius, null, true);
            made[ConnectIds.Chair] = Put(parent, "Chair", ChairAt, script, ConnectIds.Chair, ItemRadius, ConnectIds.Note, true);
            made[ConnectIds.Monitor] = Put(parent, "Monitor", ScreenAt, script, ConnectIds.Monitor, ItemRadius, null, false);
            made[ConnectIds.List] = Put(parent, "List", ScreenAt, script, ConnectIds.List, ItemRadius, ConnectIds.Monitor, true);
            made[ConnectIds.Dive] = Put(parent, "Dive", ScreenAt, script, ConnectIds.Dive, ItemRadius, ConnectIds.List, true);

            // ジャックの対象はジャックに付いて回る。肘掛けに置いてある間はそこで拾い、
            // 挿した後は手首に付いていく
            var jack = Look("Room/Chair/JackRest/Jack");
            if (jack != null)
            {
                var item = Put(jack, "Jack", jack.position, script, ConnectIds.Jack, JackRadius, null, false);
                item.transform.localPosition = Vector3.zero;
                made[ConnectIds.Jack] = item;
            }
            return made;
        }

        /// <summary>調べる対象をひとつ立てる。どれも一度調べたら終わり</summary>
        static GameObject Put(Transform parent, string name, Vector3 at, RoomScript script,
            string id, float radius, string after, bool live)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            // Interactable の OnValidate は AddComponent の中で走るので、
            // ここで「id がない」と一度警告が出る。id はこの直後に入れている
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("required").boolValue = true;
            so.FindProperty("once").boolValue = true;
            var chain = so.FindProperty("after");
            chain.arraySize = after == null ? 0 : 1;
            if (after != null) chain.GetArrayElementAtIndex(0).stringValue = after;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(live);
            return go;
        }

        // ---- モニター --------------------------------------------------------

        /// <summary>組み上がった画面。地の面と、その中に開いた窓</summary>
        public struct Sheet
        {
            public Renderer[] backs;
            public TerminalScreen.Pane[] panes;
        }

        /// <summary>
        /// 画面の中に開く窓の割り付け。面を 1 とした割合で、左下を原点に置く。
        /// モニターごとに違う並びを当てて、5 枚が同じ絵にならないようにする
        /// </summary>
        static readonly Rect[][] Layouts =
        {
            new[] { new Rect(0f, 0f, 0.46f, 1f), new Rect(0.52f, 0.54f, 0.48f, 0.46f), new Rect(0.52f, 0f, 0.48f, 0.48f) },
            new[] { new Rect(0f, 0.60f, 1f, 0.40f), new Rect(0f, 0f, 0.56f, 0.54f), new Rect(0.62f, 0f, 0.38f, 0.54f) },
            new[] { new Rect(0f, 0.54f, 1f, 0.46f), new Rect(0f, 0f, 1f, 0.48f) },
            new[] { new Rect(0f, 0f, 0.52f, 1f), new Rect(0.58f, 0f, 0.42f, 1f) },
            new[] { new Rect(0f, 0f, 0.36f, 1f), new Rect(0.42f, 0.56f, 0.58f, 0.44f), new Rect(0.42f, 0f, 0.58f, 0.50f) },
        };

        /// <summary>窓ごとの字の大きさ。小さいほど大きく映る</summary>
        static readonly float[] PaneScale = { 0.70f, 1.15f, 0.90f };
        /// <summary>窓ごとの流れる速さの倍率</summary>
        static readonly float[] PaneSpeed = { 1.00f, 0.65f, 1.35f };

        /// <summary>画面の面の大きさ。メートル</summary>
        const float FaceWide = 0.810f;
        const float FaceHigh = 0.480f;
        /// <summary>面の前へ窓を浮かせる量。メートル。**0 にすると面と喧嘩して縞が出る**</summary>
        const float PaneLift = 0.002f;
        /// <summary>画面の縁に残す余白。面を 1 とした割合</summary>
        const float PaneEdge = 0.035f;
        /// <summary>
        /// 面を丸ごと映したときの割り当て。窓はこれに窓の大きさと
        /// <see cref="PaneScale"/> を掛けたものになる
        /// </summary>
        static readonly Vector2 PaneFit = new Vector2(1.0f, 0.5f);

        /// <summary>
        /// 画面の面を場面 3 のマテリアルへ替え、その中に窓を開ける。
        ///
        /// **5 枚とも替える。** 机の 5 枚は場面 1 から同じマテリアルを共有していて、
        /// 1 枚だけ替えて灯すと隣の 4 枚が消えたままになる。ひとつの作業机なので、
        /// 点くときは 5 枚とも点く。
        ///
        /// **帯は面ではなく窓に貼る。** 面に貼ると、消えている間も地の色に帯が掛かり、
        /// 灰色の文字が読めてしまう
        /// </summary>
        static Sheet Screens()
        {
            var backs = new List<Renderer>();
            var panes = new List<TerminalScreen.Pane>();
            var monitors = Look("Room/Monitors");
            if (monitors == null) return new Sheet { backs = backs.ToArray(), panes = panes.ToArray() };
            var ground = Face();
            var ink = PaneFace();
            var which = 0;
            foreach (Transform m in monitors)
            {
                var face = m.Find("Face");
                var r = face != null ? face.GetComponent<Renderer>() : null;
                if (r == null) continue;
                if (ground != null) r.sharedMaterial = ground;
                EditorUtility.SetDirty(r);
                backs.Add(r);
                panes.AddRange(Panes(m, face, ink, which));
                which++;
            }
            if (backs.Count == 0) Debug.LogWarning("モニターの面が見つからない");
            return new Sheet { backs = backs.ToArray(), panes = panes.ToArray() };
        }

        /// <summary>
        /// 1 枚ぶんの窓を開ける。
        ///
        /// **面の子にはしない。** 面は 0.810 × 0.480 × 0.012 に伸ばした立方体なので、
        /// その子に置くと窓まで縦横で違う倍率に引き伸ばされる。モニターの子に置き、
        /// 面の前の位置だけ自分で出す
        /// </summary>
        static TerminalScreen.Pane[] Panes(Transform monitor, Transform face, Material ink, int which)
        {
            var group = monitor.Find("Windows");
            if (group != null) Object.DestroyImmediate(group.gameObject);
            group = new GameObject("Windows").transform;
            group.SetParent(monitor, false);
            group.localPosition = Vector3.zero;
            group.localRotation = Quaternion.identity;

            // 面の手前の側。面の中心から厚みの半分だけ、モニターの -z へ出たところ
            var front = face.localPosition.z - face.localScale.z * 0.5f - PaneLift;
            var plan = Layouts[which % Layouts.Length];
            var made = new TerminalScreen.Pane[plan.Length];
            for (var i = 0; i < plan.Length; i++)
            {
                var cut = plan[i];
                // 縁の余白は面の外周にだけ残す。窓と窓のあいだは割り付けが持っている
                var x0 = Mathf.Lerp(PaneEdge, 1f - PaneEdge, cut.xMin);
                var x1 = Mathf.Lerp(PaneEdge, 1f - PaneEdge, cut.xMax);
                var y0 = Mathf.Lerp(PaneEdge, 1f - PaneEdge, cut.yMin);
                var y1 = Mathf.Lerp(PaneEdge, 1f - PaneEdge, cut.yMax);
                var wide = (x1 - x0) * FaceWide;
                var high = (y1 - y0) * FaceHigh;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Window" + i;
                quad.transform.SetParent(group, false);
                quad.transform.localPosition = new Vector3(
                    ((x0 + x1) * 0.5f - 0.5f) * FaceWide, ((y0 + y1) * 0.5f - 0.5f) * FaceHigh, front);
                // **回さない。** Unity の Quad は法線が -z を向いていて、
                // そのままでモニターの表（運転席ではなく椅子の側）を向く。
                // 180 度回すと裏返って、背面の削りで丸ごと消える
                quad.transform.localRotation = Quaternion.identity;
                quad.transform.localScale = new Vector3(wide, high, 1f);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = ink;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                // 灯るまで伏せておく。TerminalScreen が点けたところで起こす
                r.enabled = false;

                var scale = PaneScale[i % PaneScale.Length];
                made[i] = new TerminalScreen.Pane
                {
                    face = r,
                    tiling = new Vector2((x1 - x0) * PaneFit.x * scale, (y1 - y0) * PaneFit.y * scale),
                    speed = PaneSpeed[i % PaneSpeed.Length],
                    // 窓ごとに頭をずらす。揃えると 5 枚とも同じ行が並んで模様に見える
                    phase = which * 0.41f + i * 0.137f,
                };
            }
            return made;
        }

        /// <summary>
        /// 画面の地のマテリアル。場面 1 と共有したままだと、
        /// こちらで灯した画面が向こうでも灯る。
        ///
        /// **帯は貼らない。** 貼ると、消えている間も地の色に帯が掛かって
        /// 灰色の文字が読めてしまう。文字は窓（<see cref="PaneFace"/>）が持つ
        /// </summary>
        static Material Face()
        {
            var m = Clone(FacePath, "TerminalFace");
            if (m == null) return null;
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_EmissionMap", null);
            m.SetColor("_BaseColor", ScreenOff);
            // **emission を切らない。** 切ってあると TerminalScreen が
            // MaterialPropertyBlock から色を渡しても、シェーダーの側で捨てられる。
            // 色そのものは消えているぶんを入れておく。エディタで開いたときの見え方も
            // 組み立ての責任で、場面の頭では画面は消えている
            m.SetColor("_EmissionColor", ScreenOff);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>窓のマテリアル。帯を地と光る側の両方に貼る</summary>
        static Material PaneFace()
        {
            var m = Clone(PanePath, "TerminalPane");
            if (m == null) return null;
            var rows = AssetDatabase.LoadAssetAtPath<Texture2D>(RowsPath);
            if (rows == null) Debug.LogWarning("帯の絵が無い: " + RowsPath);
            // **帯は地と光る側の両方に貼る。** 地だけだと、灯ったときに一様な色の
            // emission が上から塗り潰して帯が消え、窓が緑の板になる。
            // URP の Lit は emission も _BaseMap_ST で畳んだ uv で引くので、
            // 流すのは TerminalScreen に任せられる
            m.SetTexture("_BaseMap", rows);
            m.SetTexture("_EmissionMap", rows);
            m.SetColor("_BaseColor", Color.black);
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 置き場のマテリアルを、無ければ元の画面から複製して返す。
        /// 艶は落とす。元の Screen.mat は smoothness 0.85 で、机の並びが画面に映り込み、
        /// 灯る前は縁がぎざぎざに光り、灯った後は帯の上に部屋が重なって行が読めなくなる。
        /// 画面は自分で光るものなので反射は要らない
        /// </summary>
        static Material Clone(string path, string name)
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Materials", "Connect");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Placeholder/Screen.mat");
                if (source == null) { Debug.LogWarning("元の画面のマテリアルが無い"); return null; }
                m = new Material(source);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Smoothness", 0.02f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return m;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>
        /// 場面 3 のものを足して繋ぐ。private な [SerializeField] なので
        /// SerializedObject 越しに書く
        /// </summary>
        static void Wire(Transform socket, Sheet sheet, Dictionary<string, GameObject> items)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            TerminalScreen screen = null;
            // 画面は机に並んだ 5 枚をひとつとして扱うので、入れ物の側に付ける
            var monitors = Look("Room/Monitors");
            if (monitors != null)
            {
                screen = monitors.GetComponent<TerminalScreen>();
                if (screen == null) screen = monitors.gameObject.AddComponent<TerminalScreen>();
                var so = new SerializedObject(screen);
                var row = so.FindProperty("backs");
                row.arraySize = sheet.backs.Length;
                for (var i = 0; i < sheet.backs.Length; i++)
                    row.GetArrayElementAtIndex(i).objectReferenceValue = sheet.backs[i];
                var win = so.FindProperty("panes");
                win.arraySize = sheet.panes.Length;
                for (var i = 0; i < sheet.panes.Length; i++)
                {
                    var e = win.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("face").objectReferenceValue = sheet.panes[i].face;
                    e.FindPropertyRelative("tiling").vector2Value = sheet.panes[i].tiling;
                    e.FindPropertyRelative("speed").floatValue = sheet.panes[i].speed;
                    e.FindPropertyRelative("phase").floatValue = sheet.panes[i].phase;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screen);
            }
            var plug = Plug(flow, socket);
            Director(flow, screen, plug, items);
        }

        /// <summary>
        /// 段の進行。SceneFlow と同じ GameObject に置く。
        /// 秒数と間合いは仮置きで、オーナーが再生しながら Inspector で決める
        /// </summary>
        static void Director(SceneFlow flow, TerminalScreen screen, JackPlug plug,
            Dictionary<string, GameObject> items)
        {
            if (flow == null) { Debug.LogWarning("SceneFlow が無い。ConnectDirector を繋げない"); return; }
            var director = flow.GetComponent<ConnectDirector>();
            if (director == null) director = flow.gameObject.AddComponent<ConnectDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pose").objectReferenceValue = Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
            so.FindProperty("screen").objectReferenceValue = screen;
            so.FindProperty("plug").objectReferenceValue = plug;
            var blocker = Look("Room/Chair/Blocker");
            so.FindProperty("chairBlocker").objectReferenceValue = blocker != null ? blocker.gameObject : null;
            GameObject item;
            so.FindProperty("jackItem").objectReferenceValue = items.TryGetValue(ConnectIds.Jack, out item) ? item : null;
            so.FindProperty("monitorItem").objectReferenceValue = items.TryGetValue(ConnectIds.Monitor, out item) ? item : null;
            so.FindProperty("script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            so.FindProperty("seatSpot").vector3Value = SeatAt;
            so.FindProperty("seatEyeHeight").floatValue = SeatEyeHeight;
            // モニターの方。部屋は z の正の向きに机が並んでいる
            so.FindProperty("seatYaw").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        /// <summary>
        /// 挿すしぐさ。曲げは手で写さない。
        ///
        /// **複製した時点で抜く側（<see cref="JackPull"/>）がまだ乗っているので、
        /// 段を読み替えてそのまま複写する。** 手で数字を打ち直すより確実で、
        /// 抜く側を詰め直したら組み直すだけで挿す側も付いてくる。
        /// 写し終えたら抜く側は落とす。id が同じなので、残すと両方が動く
        /// </summary>
        static JackPlug Plug(SceneFlow flow, Transform socket)
        {
            var pro = Look("Player/Protagonist");
            if (pro == null) return null;
            var pull = pro.GetComponent<JackPull>();
            var plug = pro.GetComponent<JackPlug>();
            if (plug == null) plug = pro.gameObject.AddComponent<JackPlug>();
            var so = new SerializedObject(plug);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pose").objectReferenceValue = pro.GetComponent<SeatedPose>();
            so.FindProperty("id").stringValue = ConnectIds.Jack;
            so.FindProperty("jack").objectReferenceValue = Look("Room/Chair/JackRest/Jack");
            var left = Bone(HumanBodyBones.LeftHand);
            so.FindProperty("grip").objectReferenceValue = left != null ? left.Find("JackHold") : null;
            so.FindProperty("socket").objectReferenceValue = socket;
            var voice = Look("Player/Main Camera/Voice");
            so.FindProperty("source").objectReferenceValue = voice != null ? voice.GetComponent<AudioSource>() : null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PlugPath);
            if (clip == null) Debug.LogWarning("挿さる音が無い: " + PlugPath);
            so.FindProperty("plug").objectReferenceValue = clip;
            if (pull == null) Debug.LogWarning("JackPull が無い。狙いの値を写せないので挿すしぐさは既定の値になる");
            else
            {
                var from = new SerializedObject(pull);
                // 抜く側の「右の手首を目の前へ出す」が、挿す側では「差込口を出す」にあたる
                so.FindProperty("liftWrist").vector3Value = from.FindProperty("lookWrist").vector3Value;
                so.FindProperty("liftHand").quaternionValue = from.FindProperty("lookHand").quaternionValue;
                so.FindProperty("rightElbowPole").vector3Value = from.FindProperty("rightElbowPole").vector3Value;
                so.FindProperty("leftElbowPole").vector3Value = from.FindProperty("leftElbowPole").vector3Value;
                // 抜く長さが、挿す前に差込口の手前で止める距離にあたる
                so.FindProperty("pushDistance").floatValue = from.FindProperty("pullDistance").floatValue;
                Copy(from, "pinch", so, "pinch");
                Copy(from, "open", so, "open");
                so.FindProperty("approach").floatValue = from.FindProperty("approach").floatValue;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(plug);
            if (pull != null) Object.DestroyImmediate(pull);
            return plug;
        }

        /// <summary>骨の向きの配列を写す。SeatedPose.Bone は struct なので、要素ごとに並べ直す</summary>
        static void Copy(SerializedObject from, string source, SerializedObject to, string target)
        {
            var a = from.FindProperty(source);
            var b = to.FindProperty(target);
            b.arraySize = a.arraySize;
            for (var i = 0; i < a.arraySize; i++)
            {
                var x = a.GetArrayElementAtIndex(i);
                var y = b.GetArrayElementAtIndex(i);
                y.FindPropertyRelative("bone").intValue = x.FindPropertyRelative("bone").intValue;
                y.FindPropertyRelative("position").vector3Value = x.FindPropertyRelative("position").vector3Value;
                y.FindPropertyRelative("rotation").quaternionValue = x.FindPropertyRelative("rotation").quaternionValue;
            }
        }

        // ---- 見直し ----------------------------------------------------------

        /// <summary>組み終えたら必ず見直す。目で気づくまで放っておかない</summary>
        static void Check(Renderer[] faces, Dictionary<string, GameObject> items)
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            var placed = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var id in ConnectIds.Order)
            {
                if (!items.ContainsKey(id)) Debug.LogWarning("シーンに対象が無い: " + id);
                if (script != null && script.Find(id).id == null) Debug.LogWarning("文面に id が無い: " + id);
            }
            if (placed.Length != ConnectIds.Order.Length)
                Debug.LogWarning(string.Format("対象の数が合わない。{0} 個あるが、場面 3 は {1} 個",
                    placed.Length, ConnectIds.Order.Length));

            Reach(ConnectIds.Note, NoteAt);
            Reach(ConnectIds.Chair, ChairAt);

            // 挿す前のジャックは肘掛けの上。判定点がそこから離れていると、
            // 座っても拾えないか、座る前に部屋の向こうから拾える
            var rest = Look("Room/Chair/JackRest");
            GameObject jack;
            if (rest != null && items.TryGetValue(ConnectIds.Jack, out jack))
            {
                var gap = Vector3.Distance(jack.transform.position, rest.position);
                if (gap > 0.05f) Debug.LogWarning(string.Format("jack の判定点が肘掛けから {0:F3} m 離れている", gap));
            }

            // 画面のマテリアルが場面 1 と共有のままだと、こちらで灯した画面が向こうでも灯る
            foreach (var face in faces)
            {
                if (face == null) continue;
                var path = AssetDatabase.GetAssetPath(face.sharedMaterial);
                if (path == FacePath) continue;
                Debug.LogWarning("画面のマテリアルが場面 3 のものではない: " + face.name + " → " + path);
            }
        }

        /// <summary>
        /// 立って届く対象か。床に立った目の高さから、半径の中に入れる点があるかを測る。
        /// 高さの差が半径より大きいと、どこに立っても届かない
        /// </summary>
        static void Reach(string id, Vector3 at)
        {
            var drop = at.y - (StartAt.y + PlayerController.StandingEyeHeight);
            if (Mathf.Abs(drop) >= ItemRadius)
            {
                Debug.LogWarning(string.Format("{0} は立って届かない。高さの差 {1:F2} m が半径 {2:F2} m を超える",
                    id, drop, ItemRadius));
                return;
            }
            var room = Mathf.Sqrt(ItemRadius * ItemRadius - drop * drop);
            Debug.Log(string.Format("{0} は床の上 {1:F2} m まで下がっても届く。手前 {2:F2} m から拾える", id, -drop, room));
        }

        /// <summary>頭から開いている対象の数</summary>
        static int Open(Dictionary<string, GameObject> items)
        {
            var n = 0;
            foreach (var pair in items) if (pair.Value != null && pair.Value.activeSelf) n++;
            return n;
        }

        // ---- 道具 ------------------------------------------------------------

        static Transform Look(string path)
        {
            var t = Find(path);
            if (t == null) Debug.LogWarning("繋ぎ先が見つからない: " + path);
            return t;
        }

        /// <summary>根から名前で辿る。切ってある物も辿れる（GameObject.Find は拾わない）</summary>
        static Transform Find(string path)
        {
            var cut = path.IndexOf('/');
            var head = cut < 0 ? path : path.Substring(0, cut);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != head) continue;
                return cut < 0 ? go.transform : go.transform.Find(path.Substring(cut + 1));
            }
            return null;
        }
    }
}
