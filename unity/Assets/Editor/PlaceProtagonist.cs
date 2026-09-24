using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 開いている場面（場面 1 の Room.unity、場面 2 の Alley.unity）の主人公を、Rocketbox で作った主人公
    /// （<see cref="BuildRocketboxProtagonist.Chosen"/>）に替える。
    ///
    /// **一つの繋がった体。** 一人称で見える腕は、プレイヤーに付けたこの体の腕そのもの。カメラは体の目の位置にある。
    /// 頭（顔・髪・首より上）だけは一人称のカメラに映さない（<see cref="SplitHead"/>）。
    ///
    /// **組み立ての出力以外は変えない。** 手で置いて保存された物（マテリアルや配置）を上書きしないよう、
    /// 保存の前に、場面の中の物の一覧（名前・位置・有効かどうか）を組み立ての前と比べ、
    /// Player/Protagonist の下と、組み立てが繋ぎ直す物のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceProtagonist
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";
        public const string AlleyPath = "Assets/Scenes/Alley.unity";
        public const string HiddenPath = "Assets/Materials/Hidden.mat";
        public const string HiddenShader = "HalfAware/Hidden";

        // ---- 組み立て ------------------------------------------------------------

        [MenuItem("HalfAware/Put the protagonist in the scene", false, 205)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RoomPath && scene.path != AlleyPath)
            {
                Debug.LogError("主人公を替えるのは場面 1（Room.unity）と場面 2（Alley.unity）だけ。今開いているのは " + scene.path);
                return;
            }
            if (scene.isDirty) { Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + scene.path); return; }
            var before = Snapshot();
            var note = new StringBuilder();
            var ok = Place(scene.path == RoomPath, note);
            var after = Snapshot();
            var diff = Diff(before, after, out var unexpected);
            note.AppendLine("場面の中の物の差（Player/Protagonist の下を除く）:").Append(diff);
            if (!ok || unexpected > 0)
            {
                Debug.LogError("主人公を替えたが、組み立ての外の物が " + unexpected + " 個変わった（または組み立てに失敗した）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("主人公を替えて保存した: " + scene.path + "\n" + note);
        }

        /// <summary>
        /// 主人公を替える。古い体に付いていたジャックと左手の置き所は新しい体の手へ移し、
        /// 古い体とその部品（顔の面、ライダースの部品）は捨てる。場面 1 では座った形と抜くしぐさも作る
        /// </summary>
        public static bool Place(bool room, StringBuilder note)
        {
            var player = GameObject.Find("Player");
            if (player == null) { note.AppendLine("Player が無い"); return false; }
            var old = player.transform.Find("Protagonist");
            var pc = player.GetComponent<PlayerController>();
            var eyeLead = pc != null ? new SerializedObject(pc).FindProperty("eyeLead").floatValue : 0.22f;

            var her = BuildRocketboxProtagonist.Build(player.transform, false);
            her.name = "Protagonist";
            if (old != null) her.transform.SetSiblingIndex(old.GetSiblingIndex());
            SplitHead(her);
            var an = her.GetComponent<Animator>();

            // 立った形で目を測り、カメラの目（Player から見た (0, 目の高さ, eyeLead)）に重なる所へ体を置く
            BodyPoser.Stand(an);
            her.transform.localPosition = Vector3.zero;
            var eye = player.transform.InverseTransformPoint(BodyPoser.Eyes(an));
            her.transform.localPosition = new Vector3(-eye.x, 0f, eyeLead - eye.z);
            note.AppendFormat("立った目: 足元から {0:0.000} m（カメラは {1:0.000} m）。体を前へ {2:0.000} m 置いた", eye.y, PlayerController.StandingEyeHeight, eyeLead - eye.z).AppendLine();

            var motion = her.AddComponent<BodyMotion>();
            var mso = new SerializedObject(motion);
            mso.FindProperty("body").objectReferenceValue = player.GetComponent<CharacterController>();
            mso.FindProperty("animator").objectReferenceValue = an;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (room && !Room(player, old, her, an, eyeLead, flow, note)) return false;

            // カメラの子の前腕は止めた。見える腕は体の腕だけ
            var forearm = player.transform.Find("Main Camera/Forearm");
            if (forearm != null) { Object.DestroyImmediate(forearm.gameObject); note.AppendLine("カメラの子の前腕（Forearm）を消した"); }

            if (flow != null)
            {
                var so = new SerializedObject(flow);
                so.FindProperty("body").objectReferenceValue = her;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Remap(old != null ? old.gameObject : null, her, note);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return true;
        }

        /// <summary>場面 1: 座った形、ジャック、左手の置き所、抜くしぐさ、ケーブル、座った目の高さ</summary>
        static bool Room(GameObject player, Transform old, GameObject her, Animator an, float eyeLead, SceneFlow flow, StringBuilder note)
        {
            var chair = GameObject.Find("Room/Chair");
            if (chair == null) { note.AppendLine("Room/Chair が無い"); return false; }
            var body = her.transform;

            // 腕のしぐさの狙いは、座った形のまま腕だけを動かして決める
            BodyPoser.Pose(an, RoomSit(chair.transform));
            var seated = BodyPoser.Capture(an);
            var eye = player.transform.InverseTransformPoint(BodyPoser.Eyes(an));
            note.AppendFormat("座った目: 足元から {0:0.000} m、前へ {1:0.000} m（カメラの前への出は {2:0.000}）", eye.y, eye.z, eyeLead).AppendLine();

            var pose = her.AddComponent<SeatedPose>();
            var pso = new SerializedObject(pose);
            pso.FindProperty("animator").objectReferenceValue = an;
            pso.ApplyModifiedPropertiesWithoutUndo();
            BodyPoser.Write(pose, "seated", seated);

            if (flow != null)
            {
                var so = new SerializedObject(flow);
                so.FindProperty("seatEyeHeight").floatValue = eye.y;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ジャック。古い手首から新しい右手へ移し、手のひらの側の皮膚へ刺し直す（子の調べる対象とケーブルの端はそのまま）
            var handR = an.GetBoneTransform(HumanBodyBones.RightHand);
            var handL = an.GetBoneTransform(HumanBodyBones.LeftHand);
            var jack = old != null ? FindDeep(old, "Jack") : null;
            if (jack == null) { note.AppendLine("古い体にジャックが無い"); return false; }
            jack.SetParent(handR, true);
            BuildProps.BuildJack(handR);
            var item = jack.Find("Interactable_jack");
            if (item != null) item.gameObject.SetActive(true);

            // 左手の置き所。つまむ形の親指と人差し指の間
            var hold = old != null ? FindDeep(old, "JackHold") : null;
            if (hold == null) hold = new GameObject("JackHold").transform;
            hold.SetParent(handL, false);
            var keepFingers = BodyPoser.Fingers(an, true);
            Vector3 holdLocal;
            Quaternion holdRotation;
            // 掴む形と、掴む前に開いておく形（掴む形から親指を 40 度遠ざけ、人差し指の曲げを 35 度戻す）
            BodyPoser.Pinch(an, true, out holdLocal, out holdRotation);
            var pinch = BodyPoser.Fingers(an, true);
            BodyPoser.Open(an, true, 40f, 35f);
            var open = BodyPoser.Fingers(an, true);
            hold.position = handL.position + handL.rotation * holdLocal;
            hold.rotation = handL.rotation * holdRotation;
            hold.localScale = Vector3.one;
            Restore(an, keepFingers);

            // ケーブルと、抜いたジャックの置き場
            var cable = chair.transform.Find("Cable");
            if (cable != null) BuildProps.Tune(cable.GetComponent<Cable>(), an);
            BuildProps.JackRest(chair.transform, an.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);

            // 抜くしぐさ。古い体の JackPull から、流れと音と置き場を受け継ぐ
            var oldPull = old != null ? old.GetComponent<JackPull>() : null;
            var pull = her.AddComponent<JackPull>();
            var jso = new SerializedObject(pull);
            if (oldPull != null)
            {
                var from = new SerializedObject(oldPull);
                foreach (var name in new[] { "flow", "parked", "source", "unplug" })
                    jso.FindProperty(name).objectReferenceValue = from.FindProperty(name).objectReferenceValue;
                jso.FindProperty("id").stringValue = from.FindProperty("id").stringValue;
            }
            jso.FindProperty("pose").objectReferenceValue = pose;
            jso.FindProperty("jack").objectReferenceValue = jack;
            jso.FindProperty("grip").objectReferenceValue = hold;
            var aims = PullAims(an, body, chair.transform, player.transform.TransformPoint(new Vector3(0f, eye.y, eyeLead)));
            jso.FindProperty("lookWrist").vector3Value = aims.lookWrist;
            jso.FindProperty("lookHand").quaternionValue = aims.lookHand;
            jso.FindProperty("rightElbowPole").vector3Value = aims.rightPole;
            jso.FindProperty("leftElbowPole").vector3Value = aims.leftPole;
            jso.FindProperty("showAt").vector3Value = aims.showAt;
            jso.FindProperty("showRotation").quaternionValue = aims.showRotation;
            jso.ApplyModifiedPropertiesWithoutUndo();
            var pinchProp = new SerializedObject(pull);
            WriteBones(pinchProp.FindProperty("pinch"), pinch);
            WriteBones(pinchProp.FindProperty("open"), open);
            pinchProp.ApplyModifiedPropertiesWithoutUndo();

            // 掴む手の角。左腕が人の腕の範囲に収まる角を、流れを通して試して選ぶ
            pose.Bind();
            JackHoldRoll.ForPull(pull, note);

            // 保存する姿は座った形（場面 1 は座って始まる）
            pose.Bind();
            pose.Apply();
            return true;
        }

        /// <summary>右の手首を目の前へ出したとき、手のひらを目へ向けきらずに戻す角（度、前腕の軸まわり）</summary>
        public const float LookTurnBack = 20f;

        /// <summary>見せるときのジャックの軸（尻の向き）。体の根から見た向き</summary>
        public static readonly Vector3 ShowAxis = new Vector3(-0.87f, 0.5f, 0f).normalized;

        /// <summary>抜くしぐさの狙い（体の根から見た値）</summary>
        public struct Aims
        {
            public Vector3 lookWrist, rightPole, leftPole, showAt;
            public Quaternion lookHand, showRotation;
        }

        /// <summary>
        /// 抜くしぐさの狙いを、座った形の体と椅子から決める。
        /// - 右の手首: 胸の前、目から 40 cm ほど下の前へ持ち上げ、手のひら（ジャック）を目へ向ける
        /// - 見せる所: 目の前 35 cm ほど、少し左下。ジャックの尻（ケーブルの出る側）は体の左上へ向け、先で差込口の側を指す（<see cref="ShowAxis"/>）
        /// - 肘: 右は外へ、左は下へ逃がす
        /// </summary>
        public static Aims PullAims(Animator an, Transform body, Transform chair, Vector3 eye)
        {
            System.Func<float, float, float, Vector3> P = (x, y, z) => chair.TransformPoint(new Vector3(x, y, z));
            var a = new Aims();
            var lookWrist = P(0.08f, 0.98f, 0.38f);
            a.lookWrist = body.InverseTransformPoint(lookWrist);
            // 手の向きは、右腕をそこへ届かせて寄せてから読み、腕は元へ戻す
            var keep = BodyPoser.Capture(an);
            var rightPole = P(0.45f, 0.75f, -0.13f);
            BodyPoser.Arm(an, false, lookWrist, rightPole, chair.TransformDirection(new Vector3(-0.45f, 0.25f, 1f)), eye - lookWrist);
            // 手のひらを目へ向けきらず、前腕の軸まわりに 20 度戻す。向けきると、ジャックを掴みに来た左の人差し指が
            // 右の手のひらの付け根に触れた（左手を楽な角で掴ませたとき）
            var handR = an.GetBoneTransform(HumanBodyBones.RightHand);
            var lowerR = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            handR.rotation = Quaternion.AngleAxis(LookTurnBack, (handR.position - lowerR.position).normalized) * handR.rotation;
            a.lookHand = Quaternion.Inverse(body.rotation) * handR.rotation;
            Restore(an, keep);
            a.rightPole = body.InverseTransformPoint(rightPole);
            a.leftPole = body.InverseTransformPoint(P(-0.40f, 0.70f, -0.08f));
            var show = P(-0.14f, 1.06f, 0.48f);
            a.showAt = body.InverseTransformPoint(show);
            // 見せるときのジャックの尻（ケーブルの出る側）は、体の左上へ向ける。先は右下（差込口の側）を指す。
            // 前は尻を差込口へ向けていたが、つまむ手のひらはジャックの先の側を向くので、左手が手首を 120 度も
            // 反らして手のひらを差込口の逆へ向けることになり、手首の肌が潰れて腕が極端に細く見えた。
            // 左上へ向けると、左手は手のひらを右下へ向けた楽な形（手のひらのひねり 50 度、手首の曲げ 50 度まで）で持てる
            a.showRotation = Quaternion.LookRotation(ShowAxis, Vector3.up);
            return a;
        }

        /// <summary>骨の向きを書き戻す（腰は位置も）</summary>
        static void Restore(Animator an, SeatedPose.Bone[] bones)
        {
            foreach (var b in bones)
            {
                var t = an.GetBoneTransform(b.bone);
                if (t == null) continue;
                if (b.bone == HumanBodyBones.Hips) t.localPosition = b.position;
                t.localRotation = b.rotation;
            }
        }

        static void WriteBones(SerializedProperty p, SeatedPose.Bone[] bones)
        {
            p.arraySize = bones.Length;
            for (var i = 0; i < bones.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("bone").intValue = (int)bones[i].bone;
                e.FindPropertyRelative("position").vector3Value = bones[i].position;
                e.FindPropertyRelative("rotation").quaternionValue = bones[i].rotation;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// 場面の中で古い体（とその部品）を指している参照を、新しい体の同じ種類の部品へ付け替える。
        /// 移したジャックと左手の置き所は、古い体の外へ出ているので触らない。付け替えられない参照は書き出す
        /// </summary>
        static void Remap(GameObject old, GameObject her, StringBuilder note)
        {
            if (old == null) return;
            var inside = new HashSet<Object>();
            foreach (var t in old.GetComponentsInChildren<Transform>(true))
            {
                inside.Add(t.gameObject);
                foreach (var c in t.GetComponents<Component>()) if (c != null) inside.Add(c);
            }
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || inside.Contains(mb)) continue;
                var so = new SerializedObject(mb);
                var p = so.GetIterator();
                var changed = false;
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = p.objectReferenceValue;
                    if (target == null || !inside.Contains(target)) continue;
                    Object to = null;
                    if (target == old) to = her;
                    else if (target is Component c && c.gameObject == old) to = her.GetComponent(c.GetType());
                    if (to != null) { p.objectReferenceValue = to; changed = true; note.AppendFormat("付け替えた参照: {0}.{1} → {2}", mb.GetType().Name, p.propertyPath, to.GetType().Name).AppendLine(); }
                    else note.AppendFormat("付け替えられない参照: {0}.{1} → {2}", mb.GetType().Name, p.propertyPath, target.name).AppendLine();
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- 保存の前の見比べ ------------------------------------------------------

        /// <summary>場面の中の物の一覧。道筋 → 有効かどうか・置き方・部品の種類とマテリアルの名前</summary>
        public static Dictionary<string, string> Snapshot()
        {
            var map = new Dictionary<string, string>();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                Walk(root.transform, root.name, map);
            return map;
        }

        static void Walk(Transform t, string path, Dictionary<string, string> map)
        {
            var key = path;
            for (var k = 2; map.ContainsKey(key); k++) key = path + "#" + k;
            var sb = new StringBuilder();
            sb.Append(t.gameObject.activeSelf ? "on " : "off ");
            sb.Append(t.localPosition.ToString("F4")).Append(' ').Append(t.localEulerAngles.ToString("F2")).Append(' ').Append(t.localScale.ToString("F3"));
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { sb.Append(" [missing]"); continue; }
                sb.Append(' ').Append(c.GetType().Name);
                if (c is Renderer r)
                    foreach (var m in r.sharedMaterials) sb.Append(':').Append(m != null ? m.name : "null");
            }
            map[key] = sb.ToString();
            foreach (Transform c in t) Walk(c, path + "/" + c.name, map);
        }

        /// <summary>
        /// 二つの一覧の差。Player/Protagonist の下は数えない。組み立てが変えてよい物（カメラの子の前腕、
        /// 椅子のケーブル、SceneFlow）の差は書くが、思いがけない差には数えない
        /// </summary>
        public static string Diff(Dictionary<string, string> a, Dictionary<string, string> b, out int unexpected)
        {
            unexpected = 0;
            var sb = new StringBuilder();
            var keys = new SortedSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            foreach (var k in keys)
            {
                if (k.StartsWith("Player/Protagonist")) continue;
                string x, y;
                a.TryGetValue(k, out x);
                b.TryGetValue(k, out y);
                if (x == y) continue;
                // 組み立てが変えてよい物: カメラの子の前腕（消す）、抜いたジャックの置き場（座面の縁へ移す）
                var allowed = k.StartsWith("Player/Main Camera/Forearm") || k == "Room/Chair/JackRest";
                if (!allowed) unexpected++;
                sb.AppendFormat("  {0}{1}: {2} → {3}", allowed ? "" : "（思いがけない）", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            if (sb.Length == 0) sb.AppendLine("  無し");
            return sb.ToString();
        }

        // ---- 頭を映さない ------------------------------------------------------

        /// <summary>頭の面の組か（顔・髪。髪を載せ替えた人は髪の殻とまつ毛も）</summary>
        public static bool IsHead(Material m)
        {
            if (m == null) return false;
            var n = m.name;
            return n.StartsWith("Head_") || n == "Hair" || n == "Shell" || n == "Lash";
        }

        /// <summary>何も描かない素材（<c>HalfAware/Hidden</c>）。無ければ作る</summary>
        public static Material Hidden()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(HiddenPath);
            if (m != null) return m;
            var shader = Shader.Find(HiddenShader);
            if (shader == null) { Debug.LogError("何も描かない素材の shader が無い: " + HiddenShader); return null; }
            m = new Material(shader) { name = "Hidden" };
            AssetDatabase.CreateAsset(m, HiddenPath);
            return m;
        }

        /// <summary>
        /// 体のレンダラーを二つに分ける。一つ目（元のレンダラー）は頭の面の組を何も描かない素材にして、胴・腕・脚だけを映す。
        /// 二つ目（HeadShadow）は同じメッシュと骨で、頭の面の組だけを影だけ落とす形にする。
        /// 頭はカメラの中にあるので映さないが、影は床や壁に落ちる
        /// </summary>
        public static void SplitHead(GameObject her)
        {
            var hidden = Hidden();
            var smr = her.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || hidden == null) return;
            var old = her.transform.Find("HeadShadow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var mats = smr.sharedMaterials;
            var shown = new Material[mats.Length];
            var shade = new Material[mats.Length];
            for (var i = 0; i < mats.Length; i++)
            {
                var head = IsHead(mats[i]);
                shown[i] = head ? hidden : mats[i];
                shade[i] = head ? mats[i] : hidden;
            }
            var go = new GameObject("HeadShadow");
            go.transform.SetParent(smr.transform.parent, false);
            go.transform.localPosition = smr.transform.localPosition;
            go.transform.localRotation = smr.transform.localRotation;
            go.transform.localScale = smr.transform.localScale;
            var copy = go.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = smr.sharedMesh;
            copy.bones = smr.bones;
            copy.rootBone = smr.rootBone;
            copy.localBounds = smr.localBounds;
            copy.updateWhenOffscreen = true;
            copy.sharedMaterials = shade;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            copy.receiveShadows = false;
            smr.sharedMaterials = shown;
        }

        // ---- 座った形（場面 1 の椅子） -------------------------------------------

        /// <summary>
        /// 場面 1 の椅子に座った形の狙い。値は椅子（Room/Chair）から見た位置。
        ///
        /// - 腰: 座面（上面 0.549）の上 7.6 cm、背もたれの前。尻の後ろ（腰の骨の 14.6 cm 後ろ）が背もたれの下の縁に触れる手前
        /// - 背: 前へ倒す。目が、カメラの目の位置（座った所から前へ eyeLead）に来るように決めた角
        /// - 脚: 腿はほぼ水平に座面へ乗せ、足は椅子の台（上面 0.15）へ寄せて乗せる。
        ///   座面が高く（0.549）、足を床へ下ろすと腿が座面の前の縁に 8 cm 食い込むため
        /// - 腕: 肩から肘掛けまでが上腕より長いので、前腕は肘掛けへ斜めに下ろし、手首から先を肘掛けに預ける。
        ///   右は手のひらを上へ返し（手首のジャックが目に入る）、差込口（肘掛けの前寄り）を前腕で塞がないよう内へ寄せる。
        ///   左は手のひらを下へ
        /// </summary>
        public static BodyPoser.Sit RoomSit(Transform chair)
        {
            System.Func<float, float, float, Vector3> P = (x, y, z) => chair.TransformPoint(new Vector3(x, y, z));
            System.Func<float, float, float, Vector3> D = (x, y, z) => chair.TransformDirection(new Vector3(x, y, z));
            return new BodyPoser.Sit
            {
                hips = P(0f, 0.625f, 0.02f),
                pelvis = 0f,
                lean = 13f,
                headKeep = 1f,
                ankleL = P(-0.10f, 0.25f, 0.26f),
                ankleR = P(0.10f, 0.25f, 0.26f),
                kneePoleL = P(-0.10f, 0.9f, 1.2f),
                kneePoleR = P(0.10f, 0.9f, 1.2f),
                footPoint = 15f,
                wristL = P(-0.265f, 0.70f, 0.16f),
                wristR = P(0.215f, 0.70f, 0.25f),
                elbowPoleL = P(-0.45f, 0.6f, -0.28f),
                elbowPoleR = P(0.45f, 0.6f, -0.28f),
                fingersL = D(0f, -0.3f, 1f),
                palmL = D(0f, -1f, 0f),
                fingersR = D(0f, -0.1f, 1f),
                palmR = D(0f, 1f, 0f),
            };
        }
    }
}
