using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    public static partial class PlaceProtagonist
    {
        // ---- 机のモニターに映る主人公 ------------------------------------------
        //
        // 場面 1 の独白「こうして反射で自分の顔が見られるからだ」で、机のモニターの黒い画面に主人公が映る。
        // 画面を一枚ずつ鏡にして、映り込みのカメラで部屋と主人公の体（頭を含む）を撮り、画面に薄く重ねる（TerminalReflection）。
        // 主人公の性別は対面まで見せないので、目のあたりは影に沈め、胸元から下は映さない（HalfAware/ScreenReflection）。
        // 端末を調べたら、座った正面の視点へ移して体も座らせ、読み終えたら戻す（TerminalSeat）

        /// <summary>映り込みの板のマテリアル</summary>
        public const string ReflectionMaterial = "Assets/Materials/Room/TerminalReflection.mat";
        /// <summary>映り込みの一式の親の名前。端末（調べる対象）の子に置く</summary>
        public const string ReflectionName = "TerminalReflection";
        /// <summary>
        /// 映り込みのいちばん濃いときの明るさ（画面の色に重ねる明るさの倍率）。ガラスの反射らしく薄く
        /// </summary>
        const float ReflectionStrength = 0.35f;

        [MenuItem("HalfAware/Put the terminal reflection in the room", false, 206)]
        public static void ReflectionMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RoomPath) { Debug.LogError("端末の映り込みは場面 1（Room.unity）だけ。今開いているのは " + scene.path); return; }
            if (scene.isDirty) { Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + scene.path); return; }
            var before = Snapshot();
            var note = new StringBuilder();
            var ok = Reflection(note);
            var after = Snapshot();
            var diff = Diff(before, after, out var unexpected);
            note.AppendLine("場面の中の物の差（Player/Protagonist の下を除く）:").Append(diff);
            if (!ok || unexpected > 0)
            {
                Debug.LogError("端末の映り込みを置いたが、組み立ての外の物が " + unexpected + " 個変わった（または組み立てに失敗した）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("端末の映り込みを置いて保存した: " + scene.path + "\n" + note);
        }

        /// <summary>
        /// 端末の子に、モニターの映り込みの一式（画面ごとの板とカメラ、頭の写し、顔の下半分を照らす灯り）と、
        /// 座った正面へ移す仕掛けを置いて繋ぐ。主人公を置き直したら、頭の写しの骨が切れるので組み直す
        /// </summary>
        public static bool Reflection(StringBuilder note)
        {
            Interactable terminal = null;
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (it.Id == "terminal") terminal = it;
            if (terminal == null) { note.AppendLine("端末（terminal）の調べる対象が無い"); return false; }
            var monitors = GameObject.Find("Room/Monitors");
            if (monitors == null) { note.AppendLine("Room/Monitors が無い"); return false; }
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            if (flow == null) { note.AppendLine("SceneFlow が無い"); return false; }
            var her = GameObject.Find("Player/Protagonist");
            if (her == null) { note.AppendLine("Player/Protagonist が無い"); return false; }
            var player = flow.Player != null ? flow.Player.transform : null;
            if (player == null) { note.AppendLine("Player が無い"); return false; }

            // 作り直す。前の一式（前の口元の板など）は捨てる
            var old = terminal.transform.Find(ReflectionName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(ReflectionName).transform;
            root.SetParent(terminal.transform, false);

            var material = ReflectionMat();
            var faces = new List<Transform>();
            foreach (var t in monitors.GetComponentsInChildren<Transform>(true))
                if (t.name == "Face") faces.Add(t);
            // 上の段から、左から
            faces.Sort((a, b) =>
            {
                var dy = b.position.y - a.position.y;
                if (Mathf.Abs(dy) > 0.1f) return dy > 0f ? 1 : -1;
                return Vector3.Dot(a.position - b.position, player.right) < 0f ? -1 : 1;
            });

            var panes = new List<TerminalReflection.Pane>();
            for (var i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                var pane = new GameObject("Pane" + i).transform;
                pane.SetParent(root, false);
                pane.gameObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var mr = pane.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.enabled = false;
                // 置き場は再生中に TerminalReflection が毎こま決める。ここでは画面の表に合わせておく
                pane.SetPositionAndRotation(face.position - face.forward * (face.lossyScale.z * 0.5f + 0.002f), face.rotation);
                pane.localScale = new Vector3(face.lossyScale.x / root.lossyScale.x, face.lossyScale.y / root.lossyScale.y, 1f);

                var camT = new GameObject("Camera").transform;
                camT.SetParent(pane, false);
                var cam = camT.gameObject.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 8f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.depth = -10f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;

                panes.Add(new TerminalReflection.Pane
                {
                    screen = face,
                    size = new Vector2(face.lossyScale.x, face.lossyScale.y),
                    thick = face.lossyScale.z,
                    face = mr,
                    camera = cam,
                });
            }
            // 画面ごとの映す向き（仮置き。オーナーが実画面で決める）。上の段は下から見上げた顎と首の線、
            // 下の段は正面と、左右は画面のある側からの横顔寄り
            for (var i = 0; i < panes.Count; i++)
            {
                var top = faces[i].position.y > faces[faces.Count - 1].position.y + 0.1f;
                var side = Vector3.Dot(faces[i].position - player.position, player.right);
                if (top)
                {
                    panes[i].yaw = side < 0f ? -15f : 15f;
                    panes[i].pitch = -22f;
                }
                else
                {
                    panes[i].yaw = Mathf.Abs(side) < 0.2f ? 0f : side < 0f ? -38f : 38f;
                    panes[i].pitch = 0f;
                }
            }

            // 頭の写し。体の頭の面だけを描く（HeadShadow と同じ素材の並びで、影は落とさない）
            var shadow = her.transform.Find("HeadShadow");
            var bodySkin = SkinPoint.BodyOf(her.transform);
            if (shadow == null || bodySkin == null) { note.AppendLine("頭の影（HeadShadow）か体の肌が無い"); return false; }
            var shadowSkin = shadow.GetComponent<SkinnedMeshRenderer>();
            var headGo = new GameObject("HeadMirror");
            headGo.transform.SetParent(root, false);
            var head = headGo.AddComponent<SkinnedMeshRenderer>();
            head.sharedMesh = bodySkin.sharedMesh;
            head.bones = bodySkin.bones;
            head.rootBone = bodySkin.rootBone;
            head.localBounds = bodySkin.localBounds;
            head.updateWhenOffscreen = true;
            head.sharedMaterials = shadowSkin.sharedMaterials;
            head.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            head.receiveShadows = false;
            head.enabled = false;

            // 座った目と、正面のモニターの真ん中
            var seatEye = new SerializedObject(flow).FindProperty("seatEyeHeight").floatValue;
            var eye = player.position + Vector3.up * seatEye + player.forward * EyeLead(player);
            var middle = Vector3.zero;
            foreach (var f in faces) middle += f.position;
            if (faces.Count > 0) middle /= faces.Count;

            // 映り込みのカメラが撮る間だけ点く灯り。
            // 口元の灯り: 画面の光のように前のやや上から、口元へ向けて狭く。唇の上と顎の先が明るく、頬へ向かって沈み、
            // 鼻の下は下を向くので暗い
            var mouth = eye + Vector3.down * 0.075f;
            var lamp = Lamp(root, "MirrorLamp", eye + (middle - eye).normalized * 0.5f + Vector3.up * 0.05f, mouth, 13f, 2f, 1.2f, 0.9f);
            // 頭の後ろの壁の灯り: 頭と肩の影の形を、後ろの部屋から少し浮かせる。椅子の後ろの高い所から、後ろの壁へ広く
            var back = player.position - player.forward * 0.9f + Vector3.up * (seatEye + 0.35f);
            var wall = Lamp(root, "MirrorBackLamp", back, back - player.forward * 1f + Vector3.down * 0.4f, 110f, 60f, 3f, 0.6f);

            var reflection = root.gameObject.AddComponent<TerminalReflection>();
            var so = new SerializedObject(reflection);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("source").objectReferenceValue = terminal;
            so.FindProperty("fromLine").intValue = 2;
            var list = so.FindProperty("panes");
            list.arraySize = panes.Count;
            for (var i = 0; i < panes.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("screen").objectReferenceValue = panes[i].screen;
                e.FindPropertyRelative("size").vector2Value = panes[i].size;
                e.FindPropertyRelative("thick").floatValue = panes[i].thick;
                e.FindPropertyRelative("face").objectReferenceValue = panes[i].face;
                e.FindPropertyRelative("camera").objectReferenceValue = panes[i].camera;
                e.FindPropertyRelative("yaw").floatValue = panes[i].yaw;
                e.FindPropertyRelative("pitch").floatValue = panes[i].pitch;
            }
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("body").objectReferenceValue = her.transform;
            var lamps = so.FindProperty("lamps");
            lamps.arraySize = 2;
            lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            lamps.GetArrayElementAtIndex(1).objectReferenceValue = wall;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 座った正面へ移す仕掛け。座った所は、場面の頭の座った所（Player の今の置き場）
            var seat = root.gameObject.AddComponent<TerminalSeat>();
            var ss = new SerializedObject(seat);
            var flowSo = new SerializedObject(flow);
            var chair = (Transform)flowSo.FindProperty("chair").objectReferenceValue;
            var blocker = (GameObject)flowSo.FindProperty("chairBlocker").objectReferenceValue;
            var to = (middle - eye).normalized;
            ss.FindProperty("flow").objectReferenceValue = flow;
            ss.FindProperty("pose").objectReferenceValue = her.GetComponent<SeatedPose>();
            ss.FindProperty("reflection").objectReferenceValue = reflection;
            ss.FindProperty("seatSpot").vector3Value = player.position;
            ss.FindProperty("seatYaw").floatValue = player.eulerAngles.y;
            ss.FindProperty("seatPitch").floatValue = PlayerController.ClampPitch(-Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg);
            ss.FindProperty("seatEyeHeight").floatValue = seatEye;
            ss.FindProperty("chair").objectReferenceValue = chair;
            ss.FindProperty("chairSeated").vector3Value = chair != null ? chair.position : Vector3.zero;
            ss.FindProperty("chairBlocker").objectReferenceValue = blocker;
            ss.ApplyModifiedPropertiesWithoutUndo();

            note.AppendFormat("モニターの映り込み: 画面 {0} 枚（{1}）、座った正面の見下ろし {2:0.0} 度",
                faces.Count, string.Join("・", faces.ConvertAll(f => f.parent != null ? f.parent.name : f.name)),
                ss.FindProperty("seatPitch").floatValue).AppendLine();
            return true;
        }

        /// <summary>映り込みのカメラが撮る間だけ点く灯り（影は落とさない）。at から look へ向けたスポット</summary>
        static Light Lamp(Transform parent, string name, Vector3 at, Vector3 look, float angle, float inner, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at, Vector3.up));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = inner;
            l.range = range;
            l.intensity = intensity;
            l.color = new Color(0.78f, 0.82f, 0.95f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        /// <summary>映り込みの板のマテリアル。明るい所だけを画面に重ねる（HalfAware/ScreenReflection）</summary>
        static Material ReflectionMat()
        {
            var shader = Shader.Find("HalfAware/ScreenReflection");
            var m = AssetDatabase.LoadAssetAtPath<Material>(ReflectionMaterial);
            if (m == null)
            {
                m = new Material(shader) { name = "TerminalReflection" };
                AssetDatabase.CreateAsset(m, ReflectionMaterial);
            }
            m.shader = shader;
            m.SetFloat("_Strength", ReflectionStrength);
            m.SetFloat("_Compress", 1.5f);
            m.SetVector("_Edge", new Vector4(0.06f, 0.10f, 0f, 0f));
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
