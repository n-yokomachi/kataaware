using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 5（小休止）の <c>Rest.unity</c> を、場面 3 の <c>Connect.unity</c> から組む。
    ///
    /// 同じ部屋の、潜って戻ってきた直後の姿。座ったまま、ジャックは挿さったまま、
    /// モニターは灯ったまま。調べられるのはそのモニターひとつだけ。
    ///
    /// **開いてから名前を移すまでのあいだは何も触らない。** ここで手を入れると、
    /// 保存が途中で失敗したときに場面 3 が書き換わったまま残る。
    /// <c>Connect.unity</c> も <c>Room.unity</c> も、この道を通る限り保存されない
    /// </summary>
    public static class BuildRest
    {
        public const string ConnectPath = "Assets/Scenes/Connect.unity";
        public const string ScenePath = "Assets/Scenes/Rest.unity";
        public const string ScriptPath = "Assets/Data/RestScript.asset";

        [MenuItem("HalfAware/Build the rest", false, 251)]
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
            Seat();
            Undressed();
            Plugged();
            Lit();
            var cigarette = Smoke();
            var item = Item();
            Director(cigarette, item);
            Register();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Check(item);
            Debug.Log(string.Format("小休止の部屋を組んだ。調べる対象 {0} 個、煙草 {1}、"
                + "ジャックは挿さったまま、画面は灯ったまま",
                Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                cigarette == null ? "無し" : "有り"));
        }

        /// <summary>
        /// 場面 3 を開いて、その場で <c>Rest.unity</c> として名前を移す。
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ConnectPath) == null)
            {
                Debug.LogError("場面 3 が無い: " + ConnectPath);
                return false;
            }
            var scene = EditorSceneManager.OpenScene(ConnectPath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Rest.unity として保存できなかった。場面 3 を守るためここで止める");
                return false;
            }
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            Debug.LogError("名前が移っていない。まだ Connect.unity を開いたままなので止める");
            return false;
        }

        // ---- 場面 3 のものを落とす -------------------------------------------

        /// <summary>
        /// 場面 3 の段と、調べる対象を全部落とす。
        ///
        /// 対象は入れ物の下だけでなくジャックにも付いているので、名前ではなく
        /// 付いているコンポーネントで拾う。<see cref="JackPlug"/> も落とす。
        /// 挿すしぐさはもう済んでいるし、残すと <c>jack</c> の対象が無いまま
        /// SceneFlow の便りを聞き続ける
        /// </summary>
        static void Strip()
        {
            var director = Object.FindFirstObjectByType<ConnectDirector>(FindObjectsInactive.Include);
            if (director != null) Object.DestroyImmediate(director);
            foreach (var item in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(item.gameObject);
            var plug = Object.FindFirstObjectByType<JackPlug>(FindObjectsInactive.Include);
            if (plug != null) Object.DestroyImmediate(plug);
        }

        // ---- 戻ってきた状態に置き直す -----------------------------------------

        /// <summary>
        /// 場面 5 の SceneFlow。庭（場面 6）はまだ無いので「（仮）続く」で止める。
        ///
        /// **眩暈は繋がない。** <c>dazeUntil</c> が空でも <c>ReleaseDaze</c> は最初の Update で
        /// 自分の秒数の <c>Decay</c> を呼ぶので、繋いだままだと
        /// <see cref="RestDirector"/> が掛けた濃さがその場で上書きされる
        /// </summary>
        static void Flow()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { Debug.LogError("SceneFlow が無い"); return; }
            var so = new SerializedObject(flow);
            // 立ち上がらない。空にしておくと Awake が座位の仕度を飛ばすので、
            // 座らせるのは RestDirector の仕事になる
            so.FindProperty("standAfter").stringValue = "";
            so.FindProperty("nextScene").stringValue = "";
            so.FindProperty("openingCard").stringValue = "";
            so.FindProperty("cutToBlack").boolValue = false;
            so.FindProperty("exitSound").objectReferenceValue = null;
            so.FindProperty("dazeUntil").stringValue = "";
            so.FindProperty("daze").objectReferenceValue = null;
            // Awake が無条件に伏せるので、歩かない場面でも椅子の当たりは渡さない
            so.FindProperty("chairBlocker").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        /// <summary>
        /// 椅子に据える。目の高さと首の制限と座位の姿勢は直列化されないので、
        /// ここでは位置と向きだけ置いて、残りは <see cref="RestDirector"/> が掛ける
        /// </summary>
        static void Seat()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogError("PlayerController が無い"); return; }
            player.transform.position = BuildConnect.SeatAt;
            // モニターの方。部屋は z の正の向きに机が並んでいる
            player.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(player);
            // 座ったままなので椅子には当たらない。入れると座面から押し出される
            var blocker = Look("Room/Chair/Blocker");
            if (blocker != null) blocker.gameObject.SetActive(false);
        }

        /// <summary>
        /// 場面 3 で座る前に脱いだまま。体のジャケットは脱いだ形、左の肘掛けにジャケットが掛かっている。
        /// 場面 3 の組み立ては肘掛けのジャケットを伏せて保存するので、ここで出す（切ってある物は GameObject.Find で拾えないので椅子から辿る）
        /// </summary>
        static void Undressed()
        {
            var pro = Look("Player/Protagonist");
            var garment = pro != null ? pro.GetComponentInChildren<Garment>(true) : null;
            if (garment == null) Debug.LogWarning("主人公にジャケットが無い");
            else { garment.Worn = false; EditorUtility.SetDirty(garment); }
            var chair = Look("Room/Chair");
            var draped = chair != null ? chair.Find(PlaceProtagonist.DrapedName) : null;
            if (draped == null) Debug.LogWarning("左の肘掛けのジャケットが無い");
            else draped.gameObject.SetActive(true);
        }

        /// <summary>
        /// ジャックを手首の受け口へ戻す。<see cref="JackPlug"/> が挿し終えたときと同じ置き方。
        /// 受け口が元の大きさを覚えているので、等倍で入れれば場面 3 の終わりとそのまま同じに見える
        /// </summary>
        static void Plugged()
        {
            var arm = BuildConnect.Bone(HumanBodyBones.RightLowerArm);
            var port = arm != null ? arm.Find(BuildProps.PortName) : null;
            var holder = port != null ? port : arm;
            var socket = holder != null ? holder.Find(BuildConnect.SocketName) : null;
            var jack = Look("Room/Chair/JackRest/Jack");
            if (socket == null) { Debug.LogWarning("手首に受け口が無い。ジャックを戻せない"); return; }
            if (jack == null)
            {
                // 既に受け口の下にいるなら、それでよい
                if (socket.Find("Jack") == null) Debug.LogWarning("ジャックが見つからない");
                return;
            }
            jack.SetParent(socket, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
            EditorUtility.SetDirty(jack);
        }

        /// <summary>画面は灯ったまま。二度瞬く起動は場面 3 で済んでいる</summary>
        static void Lit()
        {
            var screen = Object.FindFirstObjectByType<TerminalScreen>(FindObjectsInactive.Include);
            if (screen == null) { Debug.LogWarning("TerminalScreen が無い。画面を灯せない"); return; }
            var so = new SerializedObject(screen);
            so.FindProperty("startLit").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(screen);
        }

        /// <summary>
        /// 煙草と煙。場面 3 から残っていればそれを使い、落ちていれば
        /// <c>HalfAware/Build the cigarette smoke</c> と同じ手順で作り直して繋ぐ
        /// </summary>
        static Cigarette Smoke()
        {
            var cam = Look("Player/Main Camera");
            if (cam == null) { Debug.LogWarning("カメラが無い。煙草を置けない"); return null; }
            var puffs = Object.FindFirstObjectByType<SmokePuffs>(FindObjectsInactive.Include);
            if (puffs == null)
            {
                var made = BuildProps.BuildSmoke(cam);
                puffs = made != null ? made.GetComponent<SmokePuffs>() : null;
                if (puffs == null) { Debug.LogWarning("煙を作れなかった"); return null; }
            }
            var cigarette = puffs.GetComponent<Cigarette>();
            if (cigarette == null) cigarette = puffs.gameObject.AddComponent<Cigarette>();
            var so = new SerializedObject(cigarette);
            so.FindProperty("puffs").objectReferenceValue = puffs;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cigarette);
            return cigarette;
        }

        // ---- 調べる対象 ------------------------------------------------------

        /// <summary>
        /// モニターひとつだけ。必須で、二択は無い。伏せて始め、
        /// 吸い終えて独白を出したところで <see cref="RestDirector"/> が開く
        /// </summary>
        static GameObject Item()
        {
            var parent = Look("Interactables");
            if (parent == null) { Debug.LogWarning("Interactables が無い"); return null; }
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
                Debug.LogWarning("場面 5 の文面が無い。先に HalfAware/Write the rest script を走らせる: " + ScriptPath);

            var go = new GameObject("Interactable_Dive");
            go.transform.SetParent(parent, false);
            go.transform.position = BuildConnect.ScreenAt;
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = ConnectIds.Dive;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = BuildConnect.ItemRadius;
            so.FindProperty("required").boolValue = true;
            so.FindProperty("once").boolValue = true;
            so.FindProperty("after").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return go;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>段の進行。SceneFlow と同じ GameObject に置く</summary>
        static void Director(Cigarette cigarette, GameObject item)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { Debug.LogWarning("SceneFlow が無い。RestDirector を繋げない"); return; }
            var director = flow.GetComponent<RestDirector>();
            if (director == null) director = flow.gameObject.AddComponent<RestDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("daze").objectReferenceValue =
                Object.FindFirstObjectByType<DazeVolume>(FindObjectsInactive.Include);
            so.FindProperty("cigarette").objectReferenceValue = cigarette;
            so.FindProperty("pose").objectReferenceValue =
                Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
            so.FindProperty("diveItem").objectReferenceValue = item;
            so.FindProperty("seatEyeHeight").floatValue = BuildConnect.SeatEyeHeight();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        /// <summary>
        /// 組み立ての一覧へ入れる。<c>SceneManager.LoadScene</c> は入っていないシーンを読めないので、
        /// 場面 4 の切断がここへ来られない
        /// </summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (all.Exists(s => s.path == ScenePath)) return;
            all.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = all.ToArray();
            Debug.Log("組み立ての一覧へ Rest.unity を入れた");
        }

        // ---- 見直し ----------------------------------------------------------

        /// <summary>組み終えたら必ず見直す。目で気づくまで放っておかない</summary>
        static void Check(GameObject item)
        {
            var items = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (items.Length != 1)
                Debug.LogWarning("見直し: 調べる対象が " + items.Length + " 個ある。場面 5 はモニターひとつだけ");
            if (item == null) Debug.LogWarning("見直し: モニターの対象が置けていない");
            else if (item.activeSelf) Debug.LogWarning("見直し: モニターが頭から開いている。吸う前に調べられる");

            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script != null && script.Find(ConnectIds.Dive).id == null)
                Debug.LogWarning("見直し: 文面に " + ConnectIds.Dive + " が無い");

            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow != null)
            {
                var so = new SerializedObject(flow);
                if (so.FindProperty("daze").objectReferenceValue != null)
                    Debug.LogWarning("見直し: SceneFlow に眩暈が繋がっている。RestDirector の濃さが上書きされる");
                if (so.FindProperty("nextScene").stringValue.Length > 0)
                    Debug.LogWarning("見直し: 庭はまだ無いのに nextScene が入っている");
            }
            if (Object.FindFirstObjectByType<JackPlug>(FindObjectsInactive.Include) != null)
                Debug.LogWarning("見直し: JackPlug が残っている");
            if (Object.FindFirstObjectByType<Cigarette>(FindObjectsInactive.Include) == null)
                Debug.LogWarning("見直し: 煙草が無い");
        }

        // ---- 道具 ------------------------------------------------------------

        /// <summary>繋ぎ先を探す。黙って null を渡すと、再生して初めて気づくことになる</summary>
        static Transform Look(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) Debug.LogWarning("繋ぎ先が見つからない: " + path);
            return go != null ? go.transform : null;
        }
    }
}
