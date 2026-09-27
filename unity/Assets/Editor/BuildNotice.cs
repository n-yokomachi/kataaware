using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 7（自室・気づき）の <c>Notice.unity</c> を、場面 3 の <c>Connect.unity</c> から組む（場面 5 の BuildRest と同じ作り）。
    ///
    /// 同じ部屋の、庭の記憶が途切れた直後の姿。座ったまま、ジャックは挿さったまま、モニターは灯ったまま、
    /// ジャケットは玄関先のコートハンガーに掛けたまま（場面 5 と同じ形）。場面 3 の複製なので、灯した画面の窓も、
    /// 売り切った抜き差し台も、ハンガーのジャケットも場面 3 のものがそのまま来る。
    ///
    /// **ジャックを抜くしぐさだけは場面 1 から写す。** 場面 3 の組み立ては、抜く側（<see cref="JackPull"/>）を挿す側へ写したあと落としている。
    /// 場面 1（<c>Room.unity</c>）を開いて抜く側の値を読み取り（開くだけで触らない）、複製に抜く側を付け直して値を当て、繋ぎ先だけ繋ぎ直す。
    /// 場面 1 で抜く側を詰め直したら、組み直すだけでこちらにも付いてくる。
    ///
    /// **開いてから名前を移すまでのあいだは何も触らない。** ここで手を入れると、保存が途中で失敗したときに場面 3 が書き換わったまま残る。
    /// <c>Room.unity</c> も <c>Connect.unity</c> も、この道を通る限り保存されない
    /// </summary>
    public static class BuildNotice
    {
        public const string ScenePath = "Assets/Scenes/Notice.unity";
        public const string ScriptPath = "Assets/Data/NoticeScript.asset";
        /// <summary>場面 7 の終わりに読むシーン。ガレージ（場面 8 の頭）</summary>
        public const string NextScene = "Drive";
        /// <summary>抜ける音。場面 1 の抜く側と同じもの</summary>
        public const string UnplugPath = "Assets/Audio/JackPull.wav";

        /// <summary>ドア。場面 1 の対象と同じ点</summary>
        static readonly Vector3 DoorAt = new Vector3(0.80f, 1.20f, -2.80f);
        /// <summary>コートハンガーの対象を、掛けたジャケットの襟（フック）からどれだけ下に置くか。場面 3 と同じ</summary>
        const float CoatBelow = 0.15f;

        [MenuItem("HalfAware/Build the notice", false, 261)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Clean()) return;
            List<Value> pull;
            if (!ReadPull(out pull)) return;
            if (!Rename()) return;

            Strip();
            Flow();
            Seat();
            Undressed();
            var jack = Plugged();
            Pull(pull, jack);
            Lit();
            var items = Items(jack);
            Director(items);
            Tone();
            Register();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Check(items);
            Debug.Log(string.Format("気づきの部屋を組んだ。調べる対象 {0} 個（頭から開くのは {1} 個）。ジャックは挿さったまま、画面は灯ったまま、"
                + "ジャケットはコートハンガーに掛けたまま。抜く側の値を場面 1 から {2} 個写した",
                items.Count, Open(items), pull.Count));
        }

        /// <summary>未保存のシーンが開いているときは何もしない。開き直すと黙って消える</summary>
        static bool Clean()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (!other.isDirty) continue;
                Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + other.path);
                return false;
            }
            return true;
        }

        // ---- 場面 1 の抜く側を読む ---------------------------------------------

        /// <summary>写す値一つ。繋ぎ先（物への参照）は写さない。場面が替わると指す先が無くなるので、繋ぎ直す</summary>
        struct Value
        {
            public string path;
            public SerializedPropertyType type;
            public int i;
            public float f;
            public bool b;
            public string s;
        }

        /// <summary>
        /// 場面 1 を開いて、抜く側（<see cref="JackPull"/>）の値を読み取る。開くだけで何も書き換えない（保存もしない）。
        /// 骨の向きの配列（掴む手の形など）も、長さから先に写せるように並びのまま取る
        /// </summary>
        static bool ReadPull(out List<Value> values)
        {
            values = new List<Value>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildConnect.RoomPath) == null)
            {
                Debug.LogError("場面 1 が無い: " + BuildConnect.RoomPath);
                return false;
            }
            EditorSceneManager.OpenScene(BuildConnect.RoomPath, OpenSceneMode.Single);
            var pull = Object.FindFirstObjectByType<JackPull>(FindObjectsInactive.Include);
            if (pull == null)
            {
                Debug.LogError("場面 1 に JackPull が無い。抜くしぐさを写せない");
                return false;
            }
            var so = new SerializedObject(pull);
            var p = so.GetIterator();
            var enter = true;
            while (p.NextVisible(enter))
            {
                enter = true;
                if (p.propertyPath.StartsWith("m_")) { enter = false; continue; }
                var v = new Value { path = p.propertyPath, type = p.propertyType };
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Enum:
                        v.i = p.intValue;
                        break;
                    case SerializedPropertyType.Float:
                        v.f = p.floatValue;
                        break;
                    case SerializedPropertyType.Boolean:
                        v.b = p.boolValue;
                        break;
                    case SerializedPropertyType.String:
                        v.s = p.stringValue;
                        break;
                    default:
                        continue;
                }
                values.Add(v);
            }
            if (values.Count == 0)
            {
                Debug.LogError("場面 1 の JackPull から値が読めなかった");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 場面 3 を開いて、その場で <c>Notice.unity</c> として名前を移す
        /// </summary>
        static bool Rename()
        {
            if (!Clean()) return false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildConnect.ScenePath) == null)
            {
                Debug.LogError("場面 3 が無い: " + BuildConnect.ScenePath);
                return false;
            }
            var scene = EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Notice.unity として保存できなかった。場面 3 を守るためここで止める");
                return false;
            }
            if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
            Debug.LogError("名前が移っていない。まだ Connect.unity を開いたままなので止める");
            return false;
        }

        // ---- 場面 3 のものを落とす -------------------------------------------

        /// <summary>
        /// 場面 3 の段と、調べる対象を全部落とす。対象は入れ物の下だけでなく手首の受け口にも付いているので、
        /// 名前ではなく付いているコンポーネントで拾う。挿す側（<see cref="JackPlug"/>）も落とす。id が抜く側と同じなので、残すと両方が動く
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
        /// 場面 7 の SceneFlow。座って始まり、ジャックを抜くと立つ（場面 1 のジャケットの代わりにジャック）。
        /// ドアの二択で「はい」を選ぶと、場面 1 と同じく出がけの音（玄関のドア）を鳴らし、黒く落としてからガレージへ。
        ///
        /// **眩暈は繋がない。** 繋ぐと <c>ReleaseDaze</c> が最初の Update で 5 秒ぶんの眩暈を乗せる。
        /// **黒からの明けも挟まない。** 庭が途切れた次のフレームに部屋が映る（<c>fadeInSeconds</c> 0）。
        /// 目覚めの起き上がりも入れない（<c>wakeSeconds</c> 0）
        /// </summary>
        static void Flow()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { Debug.LogError("SceneFlow が無い"); return; }
            var so = new SerializedObject(flow);
            so.FindProperty("standAfter").stringValue = NoticeIds.StandAfter;
            so.FindProperty("nextScene").stringValue = NextScene;
            so.FindProperty("openingCard").stringValue = "";
            so.FindProperty("fadeInSeconds").floatValue = 0f;
            so.FindProperty("wakeSeconds").floatValue = 0f;
            // 場面 1 のドアと同じ出方。場面 8 は暗転明けにガレージの隅に立つ
            so.FindProperty("cutToBlack").boolValue = true;
            // 玄関のドアの音。場面 3 では帰ってきた頭で鳴らしていた（ConnectDirector）が、ここでは場面 1 と同じく出がけに鳴らす
            var door = flow.transform.Find("Door");
            var sound = door != null ? door.GetComponent<AudioSource>() : null;
            if (sound == null) Debug.LogWarning("SceneFlow/Door の音が無い。出がけのドアの音を鳴らせない");
            so.FindProperty("exitSound").objectReferenceValue = sound;
            so.FindProperty("dazeUntil").stringValue = "";
            so.FindProperty("daze").objectReferenceValue = null;
            // 立ってから効かせる椅子の当たり。Awake が座っている間は切る
            var blocker = Look("Room/Chair/Blocker");
            so.FindProperty("chairBlocker").objectReferenceValue = blocker != null ? blocker.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        /// <summary>
        /// 椅子に据える。目の高さと首の制限と座位の姿勢は直列化されないので、ここでは位置と向きだけ置き、
        /// 残りは SceneFlow の Awake（standAfter があるときの座位の仕度）が掛ける
        /// </summary>
        static void Seat()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogError("PlayerController が無い"); return; }
            player.transform.position = BuildConnect.SeatAt;
            // モニターの方。部屋は z の正の向きに机が並んでいる
            player.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(player);
            var blocker = Look("Room/Chair/Blocker");
            if (blocker != null) blocker.gameObject.SetActive(false);
        }

        /// <summary>
        /// 場面 3 で玄関先のコートハンガーに掛けたまま。体のジャケットは脱いだ形、ハンガーにジャケットが掛かっている。
        /// 場面 3 の組み立てはハンガーのジャケットを伏せて保存するので、ここで出す（切ってある物は GameObject.Find で拾えないので Room から辿る）
        /// </summary>
        static void Undressed()
        {
            var pro = Look("Player/Protagonist");
            var garment = pro != null ? pro.GetComponentInChildren<Garment>(true) : null;
            if (garment == null) Debug.LogWarning("主人公にジャケットが無い");
            else { garment.Worn = false; EditorUtility.SetDirty(garment); }
            var hung = Hung();
            if (hung == null) Debug.LogWarning("コートハンガーのジャケットが無い");
            else hung.gameObject.SetActive(true);
        }

        /// <summary>
        /// ジャックを手首の受け口へ戻す。場面 5 の組み立てと同じ置き方（受け口が元の置き方と大きさを覚えている）。
        /// 抜く側はここを「刺さっている所」と覚える
        /// </summary>
        static Transform Plugged()
        {
            var socket = Socket();
            if (socket == null) { Debug.LogWarning("手首に受け口が無い。ジャックを戻せない"); return null; }
            var jack = Look("Room/Chair/JackRest/Jack");
            if (jack == null)
            {
                jack = socket.Find("Jack");
                if (jack == null) Debug.LogWarning("ジャックが見つからない");
                return jack;
            }
            jack.SetParent(socket, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
            EditorUtility.SetDirty(jack);
            return jack;
        }

        /// <summary>
        /// 抜く側（<see cref="JackPull"/>）を付け直し、場面 1 から読んだ値を当ててから、繋ぎ先をこの場面の物へ繋ぐ。
        /// 抜いた後は右の肘掛けの置き場へ置く（場面 1 と同じ）
        /// </summary>
        static void Pull(List<Value> values, Transform jack)
        {
            var pro = Look("Player/Protagonist");
            if (pro == null) return;
            var pull = pro.GetComponent<JackPull>();
            if (pull == null) pull = pro.gameObject.AddComponent<JackPull>();
            var so = new SerializedObject(pull);
            var missed = 0;
            foreach (var v in values)
            {
                var p = so.FindProperty(v.path);
                if (p == null) { missed++; continue; }
                switch (v.type)
                {
                    case SerializedPropertyType.ArraySize:
                        // 長さは配列の側から変える（要素がこの後に続く）
                        var array = so.FindProperty(v.path.Substring(0, v.path.Length - ".Array.size".Length));
                        if (array != null && array.isArray) array.arraySize = v.i;
                        else p.intValue = v.i;
                        break;
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum:
                        p.intValue = v.i;
                        break;
                    case SerializedPropertyType.Float:
                        p.floatValue = v.f;
                        break;
                    case SerializedPropertyType.Boolean:
                        p.boolValue = v.b;
                        break;
                    case SerializedPropertyType.String:
                        p.stringValue = v.s;
                        break;
                }
            }
            if (missed > 0) Debug.LogWarning("抜く側の値のうち " + missed + " 個は当てる所が無かった");
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("pose").objectReferenceValue = pro.GetComponent<SeatedPose>();
            so.FindProperty("id").stringValue = NoticeIds.Jack;
            so.FindProperty("jack").objectReferenceValue = jack;
            var left = BuildConnect.Bone(HumanBodyBones.LeftHand);
            var grip = left != null ? left.Find("JackHold") : null;
            if (grip == null) Debug.LogWarning("左手に掴む置き所（JackHold）が無い");
            so.FindProperty("grip").objectReferenceValue = grip;
            var rest = Look("Room/Chair/JackRest");
            so.FindProperty("parked").objectReferenceValue = rest;
            var voice = Look("Player/Main Camera/Voice");
            so.FindProperty("source").objectReferenceValue = voice != null ? voice.GetComponent<AudioSource>() : null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UnplugPath);
            if (clip == null) Debug.LogWarning("抜ける音が無い: " + UnplugPath);
            so.FindProperty("unplug").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pull);
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

        // ---- 調べる対象 ------------------------------------------------------

        /// <summary>
        /// 四つとも必須で、一度調べたら終わり。前提は <see cref="NoticeIds.After"/>（一つ前が済むまで選べない）。
        /// ログだけは伏せて始め、気づく独白を出したところで <see cref="NoticeDirector"/> が開く。
        /// ジャックの対象はジャックに付けて手首で拾う（場面 1 と同じ）
        /// </summary>
        static Dictionary<string, GameObject> Items(Transform jack)
        {
            var made = new Dictionary<string, GameObject>();
            var parent = Look("Interactables");
            if (parent == null) return made;
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
                Debug.LogWarning("場面 7 の文面が無い。先に HalfAware/Write the notice script を走らせる: " + ScriptPath);

            made[NoticeIds.Log] = Put(parent, "Log", BuildConnect.ScreenAt, script, NoticeIds.Log, BuildConnect.ItemRadius, false);
            if (jack != null)
            {
                var item = Put(jack, "Jack", jack.position, script, NoticeIds.Jack, BuildConnect.JackRadius, true);
                item.transform.localPosition = Vector3.zero;
                made[NoticeIds.Jack] = item;
            }
            var hung = Hung();
            var coatAt = hung != null ? hung.position + Vector3.down * CoatBelow : new Vector3(1.69f, 1.04f, -2.55f);
            made[NoticeIds.Coat] = Put(parent, "Coat", coatAt, script, NoticeIds.Coat, BuildConnect.ItemRadius, true);
            made[NoticeIds.Door] = Put(parent, "Door", DoorAt, script, NoticeIds.Door, BuildConnect.ItemRadius, true);
            return made;
        }

        /// <summary>調べる対象をひとつ立てる。前提は <see cref="NoticeIds.After"/></summary>
        static GameObject Put(Transform parent, string name, Vector3 at, RoomScript script, string id, float radius, bool live)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            // Interactable の OnValidate は一拍置いてから id を見るので、ここで入れれば警告は出ない
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("required").boolValue = true;
            so.FindProperty("once").boolValue = true;
            var after = NoticeIds.After(id);
            var chain = so.FindProperty("after");
            chain.arraySize = after.Length;
            for (var i = 0; i < after.Length; i++) chain.GetArrayElementAtIndex(i).stringValue = after[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(live);
            return go;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>段の進行。SceneFlow と同じ GameObject に置く。頭の独白は WriteNoticeScript から書き込む</summary>
        static void Director(Dictionary<string, GameObject> items)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { Debug.LogWarning("SceneFlow が無い。NoticeDirector を繋げない"); return; }
            var director = flow.GetComponent<NoticeDirector>();
            if (director == null) director = flow.gameObject.AddComponent<NoticeDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("flow").objectReferenceValue = flow;
            GameObject log;
            so.FindProperty("logItem").objectReferenceValue = items.TryGetValue(NoticeIds.Log, out log) ? log : null;
            var lines = so.FindProperty("noticeLines");
            lines.arraySize = WriteNoticeScript.NoticeLines.Length;
            for (var i = 0; i < WriteNoticeScript.NoticeLines.Length; i++)
                lines.GetArrayElementAtIndex(i).stringValue = WriteNoticeScript.NoticeLines[i];
            so.FindProperty("coatId").stringValue = NoticeIds.Coat;
            var pro = Look("Player/Protagonist");
            so.FindProperty("garment").objectReferenceValue = pro != null ? pro.GetComponentInChildren<Garment>(true) : null;
            var hung = Hung();
            so.FindProperty("hung").objectReferenceValue = hung != null ? hung.gameObject : null;
            var voice = Look("Player/Main Camera/Voice");
            so.FindProperty("voice").objectReferenceValue = voice != null ? voice.GetComponent<AudioSource>() : null;
            var on = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceProtagonist.JacketOnPath);
            if (on == null) Debug.LogWarning("着る音が無い: " + PlaceProtagonist.JacketOnPath);
            so.FindProperty("jacketOn").objectReferenceValue = on;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        /// <summary>自室の空気の音。場面 3 から写してくるが、念のため繋ぎ直す（大きさは書き戻さないので、写した値が残る）</summary>
        static void Tone()
        {
            var note = new System.Text.StringBuilder();
            if (!PlaceRoomTone.Put(note)) Debug.LogWarning("自室の空気の音を繋げない: " + note);
        }

        /// <summary>
        /// 組み立ての一覧へ入れる。<c>SceneManager.LoadScene</c> は入っていないシーンを読めないので、Tab の一覧からここへ飛べない。
        /// 物語の順に、車内（Drive）の前へ差し込む。車内が無ければ末尾
        /// </summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (all.Exists(s => s.path == ScenePath)) return;
            var at = all.FindIndex(s => System.IO.Path.GetFileNameWithoutExtension(s.path) == NextScene);
            var entry = new EditorBuildSettingsScene(ScenePath, true);
            if (at < 0) all.Add(entry);
            else all.Insert(at, entry);
            EditorBuildSettings.scenes = all.ToArray();
            Debug.Log("組み立ての一覧へ Notice.unity を入れた");
        }

        // ---- 見直し ----------------------------------------------------------

        /// <summary>組み終えたら必ず見直す。目で気づくまで放っておかない</summary>
        static void Check(Dictionary<string, GameObject> items)
        {
            var placed = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (placed.Length != NoticeIds.Order.Length)
                Debug.LogWarning(string.Format("見直し: 調べる対象が {0} 個ある。場面 7 は {1} 個だけ", placed.Length, NoticeIds.Order.Length));
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            foreach (var id in NoticeIds.Order)
            {
                if (!items.ContainsKey(id)) Debug.LogWarning("見直し: シーンに対象が無い: " + id);
                if (script != null && script.Find(id).id == null) Debug.LogWarning("見直し: 文面に id が無い: " + id);
            }
            GameObject log;
            if (items.TryGetValue(NoticeIds.Log, out log) && log.activeSelf)
                Debug.LogWarning("見直し: モニターが頭から開いている。気づく前に調べられる");

            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow != null)
            {
                var so = new SerializedObject(flow);
                if (so.FindProperty("daze").objectReferenceValue != null)
                    Debug.LogWarning("見直し: SceneFlow に眩暈が繋がっている。場面 7 は眩暈を出さない");
                if (so.FindProperty("fadeInSeconds").floatValue > 0f)
                    Debug.LogWarning("見直し: 黒からの明けが入っている。庭が途切れた次のフレームに部屋が映らない");
                if (so.FindProperty("nextScene").stringValue != NextScene)
                    Debug.LogWarning("見直し: 行き先が " + NextScene + " ではない");
                if (so.FindProperty("standAfter").stringValue != NoticeIds.StandAfter)
                    Debug.LogWarning("見直し: ジャックを抜いても立てない");
                if (so.FindProperty("exitSound").objectReferenceValue == null)
                    Debug.LogWarning("見直し: 出がけのドアの音が無い");
                if (so.FindProperty("chairBlocker").objectReferenceValue == null)
                    Debug.LogWarning("見直し: 椅子の当たりが無い。立った後に椅子をすり抜ける");
            }
            if (Object.FindFirstObjectByType<JackPlug>(FindObjectsInactive.Include) != null)
                Debug.LogWarning("見直し: JackPlug が残っている。抜く側と同じ id で動く");
            if (Object.FindFirstObjectByType<ConnectDirector>(FindObjectsInactive.Include) != null)
                Debug.LogWarning("見直し: ConnectDirector が残っている");
            var pull = Object.FindFirstObjectByType<JackPull>(FindObjectsInactive.Include);
            if (pull == null) Debug.LogWarning("見直し: JackPull が無い。ジャックを抜けない");
            else
            {
                var so = new SerializedObject(pull);
                var jack = so.FindProperty("jack").objectReferenceValue as Transform;
                if (jack == null || jack.parent == null || jack.parent.name != BuildConnect.SocketName)
                    Debug.LogWarning("見直し: ジャックが手首に挿さっていない");
                if (so.FindProperty("parked").objectReferenceValue == null)
                    Debug.LogWarning("見直し: 抜いたジャックの置き場が無い");
                if (so.FindProperty("grip").objectReferenceValue == null)
                    Debug.LogWarning("見直し: 掴む置き所が無い");
            }
            var hung = Hung();
            if (hung == null || !hung.gameObject.activeSelf) Debug.LogWarning("見直し: コートハンガーにジャケットが掛かっていない");
            var pro = Look("Player/Protagonist");
            var garment = pro != null ? pro.GetComponentInChildren<Garment>(true) : null;
            if (garment != null && garment.Worn) Debug.LogWarning("見直し: 頭からジャケットを着ている");
        }

        /// <summary>頭から開いている対象の数</summary>
        static int Open(Dictionary<string, GameObject> items)
        {
            var n = 0;
            foreach (var pair in items) if (pair.Value != null && pair.Value.activeSelf) n++;
            return n;
        }

        // ---- 道具 ------------------------------------------------------------

        /// <summary>コートハンガーに掛けたジャケット（Room の子）。切ってあっても拾う</summary>
        static Transform Hung()
        {
            var room = Find("Room");
            return room != null ? room.Find(BuildConnect.HungName) : null;
        }

        /// <summary>手首の受け口。右の前腕の、手首の差込口の子</summary>
        static Transform Socket()
        {
            var arm = BuildConnect.Bone(HumanBodyBones.RightLowerArm);
            var port = arm != null ? arm.Find(BuildProps.PortName) : null;
            var holder = port != null ? port : arm;
            return holder != null ? holder.Find(BuildConnect.SocketName) : null;
        }

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
