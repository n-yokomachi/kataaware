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
        // 画面を一枚ずつ鏡にして、映り込みのカメラで部屋と主人公の写し（頭を含む）を撮り、画面に薄く重ねる（TerminalReflection）。
        // 映っている主人公は煙草を吸っている最中（MirrorSmoking。オーナー、2026-10-05）。写しの右腕だけを口元へ運び、
        // 場面の主人公の右腕は肘掛けに置いたまま（一人称の視界に腕を入れない）。煙が顔と髪の前を流れる。
        // 主人公の性別は対面まで見せないので、目のあたりは影と煙に沈め、胸元から下は映さない（HalfAware/ScreenReflection）。
        // 端末を調べたら、座った正面の視点へ移して体も座らせ、読み終えたら戻す（TerminalSeat）

        /// <summary>映り込みの板のマテリアル</summary>
        public const string ReflectionMaterial = "Assets/Materials/Room/TerminalReflection.mat";
        /// <summary>映り込みの一式の親の名前。端末（調べる対象）の子に置く</summary>
        public const string ReflectionName = "TerminalReflection";
        /// <summary>
        /// 映り込みのいちばん濃いときの明るさ（画面の色に重ねる明るさの倍率）。ガラスの反射らしく薄く
        /// </summary>
        const float ReflectionStrength = 0.35f;
        /// <summary>映り込みの色の残し方（0 で灰色、1 で元の色）。肌の色、髪の黒、タンクトップの灰が分かる程度</summary>
        const float ReflectionSaturation = 0.5f;
        /// <summary>
        /// 写す範囲。鼻から胸の上まで（Mouth）か、髪から胸の上まで（Face）。二案を撮り比べて Face にした
        /// （オーナー「煙草の煙で顔や髪が良く見えない」。髪と目も枠に入れ、手と煙越しにぼんやり見せる）
        /// </summary>
        public static TerminalReflection.Extent ReflectionExtent = TerminalReflection.Extent.Face;
        /// <summary>
        /// 映り込みに人と煙草と煙だけを映すか（映り込みのカメラは TerminalReflection.MirrorLayer だけを撮る。人の周りは画面の黒のまま）。
        /// false なら椅子と部屋も映す（2026-10-05 より前の作り）
        /// </summary>
        public static bool ReflectionIsolated = true;

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
        /// 端末の子に、モニターの映り込みの一式（画面ごとの板とカメラ、主人公の写し、写しの右腕と煙草、顔の下半分を照らす灯り）と、
        /// 座った正面へ移す仕掛けを置いて繋ぐ。主人公を置き直したら、写しのボーンが切れるので組み直す。
        /// 髪をなでつけた mesh も、組み直すたびに場面の主人公の今の mesh から作り直す（顔の形を作り替えたら、組み直せば映り込みにも入る）
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

            // 顔を映すのは、下の段の真ん中（座った正面）の画面だけ。ほかの画面は黒のまま
            var lowest = float.MaxValue;
            foreach (var f in faces) lowest = Mathf.Min(lowest, f.position.y);
            Transform front = null;
            foreach (var f in faces)
            {
                if (f.position.y > lowest + 0.1f) continue;
                if (front == null || Mathf.Abs(Vector3.Dot(f.position - player.position, player.right)) < Mathf.Abs(Vector3.Dot(front.position - player.position, player.right))) front = f;
            }
            if (front == null) { note.AppendLine("正面の画面が無い"); return false; }
            var panes = new List<TerminalReflection.Pane>();
            foreach (var face in new[] { front })
            {
                var pane = new GameObject("Pane").transform;
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
                cam.cullingMask = ReflectionIsolated ? 1 << TerminalReflection.MirrorLayer : ~0;
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
            // 主人公の写し。体の全部（頭を含む）を、場面の主人公のボーンで描く（影は落とさない）。撮る間は場面の主人公の体を伏せる。
            // 髪はこの写しに限り、耳の上から後ろへなでつけて結んだ形にする（ボブの長さを映り込みで見せない）。
            // 右腕（上腕から指先）だけは写しのボーン（ArmMirror の下）で描き、煙草を口元へ運ぶ（MirrorSmoking）
            var shadow = her.transform.Find("HeadShadow");
            var bodySkin = SkinPoint.BodyOf(her.transform);
            var an = her.GetComponent<Animator>();
            if (shadow == null || bodySkin == null || an == null) { note.AppendLine("頭の影（HeadShadow）か体の肌か Animator が無い"); return false; }
            var shadowSkin = shadow.GetComponent<SkinnedMeshRenderer>();
            var slicked = SlickedHair(her, shadowSkin.sharedMaterials, note);
            if (slicked == null) return false;
            var arm = SolveSmokingArm(her, note);
            if (arm == null) return false;
            var copies = MirrorArm(root, an, bodySkin);
            var mirrorGo = new GameObject("BodyMirror") { layer = TerminalReflection.MirrorLayer };
            mirrorGo.transform.SetParent(root, false);
            var mirror = mirrorGo.AddComponent<SkinnedMeshRenderer>();
            mirror.sharedMesh = slicked;
            var mirrorBones = bodySkin.bones;
            for (var i = 0; i < mirrorBones.Length; i++)
            {
                Transform c;
                if (mirrorBones[i] != null && copies.TryGetValue(mirrorBones[i], out c)) mirrorBones[i] = c;
            }
            mirror.bones = mirrorBones;
            mirror.rootBone = bodySkin.rootBone;
            // 囲いは決めておく（座った体と、口元へ上げた右腕を囲う広さ）。撮る間だけ点けるので、こまごとに測り直させない
            var bounds = bodySkin.localBounds;
            bounds.Expand(0.4f);
            mirror.localBounds = bounds;
            mirror.updateWhenOffscreen = false;
            // 頭の面の組は頭の影の素材、ほかは体の素材（どちらかが何も描かない素材になっている）
            var bodyMats = bodySkin.sharedMaterials;
            var headMats = shadowSkin.sharedMaterials;
            var mats = new Material[bodyMats.Length];
            for (var i = 0; i < mats.Length; i++)
                mats[i] = i < headMats.Length && headMats[i] != null && headMats[i].shader != null && headMats[i].shader.name != HiddenShader ? headMats[i] : bodyMats[i];
            mirror.sharedMaterials = mats;
            mirror.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mirror.receiveShadows = false;
            mirror.enabled = false;

            // 写しの右腕のボーンと、二つの形
            var armBones = new List<Transform>();
            var lips = new List<Quaternion>();
            var aside = new List<Quaternion>();
            foreach (var kv in copies)
            {
                var k = arm.names.IndexOf(kv.Key.name);
                if (k < 0) continue;
                armBones.Add(kv.Value);
                lips.Add(arm.lips[k]);
                aside.Add(arm.aside[k]);
                kv.Value.localRotation = arm.lips[k];
            }
            Transform copyHand;
            copies.TryGetValue(an.GetBoneTransform(HumanBodyBones.RightHand), out copyHand);
            if (copyHand == null) { note.AppendLine("写しの右手が無い"); return false; }
            Renderer ember, edge;
            Transform tip;
            var props = MirrorCigarette(copyHand, arm.holdLocal, arm.holdTurn, out ember, out edge, out tip);
            var headBone = an.GetBoneTransform(HumanBodyBones.Head);
            var smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Room/Smoke.mat");
            if (smokeMat == null) { note.AppendLine("煙のマテリアルが無い（Assets/Materials/Room/Smoke.mat）"); return false; }
            var smoking = root.gameObject.AddComponent<MirrorSmoking>();
            var ms = new SerializedObject(smoking);
            ms.FindProperty("source").objectReferenceValue = an.GetBoneTransform(HumanBodyBones.RightShoulder);
            ms.FindProperty("body").objectReferenceValue = her.transform;
            ms.FindProperty("root").objectReferenceValue = root.Find("ArmMirror");
            var pb = ms.FindProperty("bones");
            var pl = ms.FindProperty("lips");
            var pa = ms.FindProperty("aside");
            pb.arraySize = pl.arraySize = pa.arraySize = armBones.Count;
            for (var i = 0; i < armBones.Count; i++)
            {
                pb.GetArrayElementAtIndex(i).objectReferenceValue = armBones[i];
                pl.GetArrayElementAtIndex(i).quaternionValue = lips[i];
                pa.GetArrayElementAtIndex(i).quaternionValue = aside[i];
            }
            var pp = ms.FindProperty("props");
            pp.arraySize = props.Length;
            for (var i = 0; i < props.Length; i++) pp.GetArrayElementAtIndex(i).objectReferenceValue = props[i];
            ms.FindProperty("ember").objectReferenceValue = ember;
            ms.FindProperty("emberEdge").objectReferenceValue = edge;
            ms.FindProperty("tip").objectReferenceValue = tip;
            ms.FindProperty("head").objectReferenceValue = headBone;
            ms.FindProperty("mouth").vector3Value = headBone.InverseTransformPoint(arm.mouth);
            ms.FindProperty("smoke").objectReferenceValue = smokeMat;
            ms.FindProperty("glow").objectReferenceValue = GlowMat(smokeMat);
            ms.ApplyModifiedPropertiesWithoutUndo();

            // 座った目と、正面のモニターの真ん中
            var seatEye = new SerializedObject(flow).FindProperty("seatEyeHeight").floatValue;
            var eye = player.position + Vector3.up * seatEye + player.forward * EyeLead(player);
            var middle = Vector3.zero;
            foreach (var f in faces) middle += f.position;
            if (faces.Count > 0) middle /= faces.Count;

            // 映り込みのカメラが撮る間だけ点く灯り。
            // 口元の灯り: 画面の光のように前のやや上から、口元へ向けて狭く。唇の上と顎の先が明るく、頬へ向かって沈み、
            // 鼻の下は下を向くので暗い。狙いは口の真ん中から体の左へ 2.5 cm 寄せる（口の右に添えた手の甲を、灯りの芯から外す。
            // 口の真ん中を狙うと、手の甲がいちばん明るく光って顔より目立った）
            var mouth = eye + Vector3.down * 0.075f - player.right * 0.025f;
            var lamp = Lamp(root, "MirrorLamp", eye + (front.position - eye).normalized * 0.5f + Vector3.up * 0.05f, mouth, 13f, 2f, 1.2f, 0.9f);
            // 頭の後ろの壁の灯り: 頭と肩の影の形を、後ろの部屋から少し浮かせる。椅子の後ろの高い所から、後ろの壁へ広く
            var back = player.position - player.forward * 0.9f + Vector3.up * (seatEye + 0.35f);
            // 首から下の灯り: 画面の光のように前から、首と肩と胸の上へ広く弱く。服の形（肩の線、襟）が読める明るさに
            var fill = Lamp(root, "MirrorFillLamp", eye + (front.position - eye).normalized * 0.6f, eye + Vector3.down * 0.22f, 50f, 20f, 1.5f, 0.7f);
            var wall = Lamp(root, "MirrorBackLamp", back, back - player.forward * 1f + Vector3.down * 0.4f, 110f, 60f, 3f, 0.6f);

            var reflection = root.gameObject.AddComponent<TerminalReflection>();
            var so = new SerializedObject(reflection);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("source").objectReferenceValue = terminal;
            so.FindProperty("fromLine").intValue = TerminalReflection.FromPage;
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
                e.FindPropertyRelative("bottom").floatValue = panes[i].bottom;
            }
            so.FindProperty("mirror").objectReferenceValue = mirror;
            so.FindProperty("original").objectReferenceValue = bodySkin;
            so.FindProperty("smoking").objectReferenceValue = smoking;
            so.FindProperty("extent").enumValueIndex = (int)ReflectionExtent;
            so.FindProperty("body").objectReferenceValue = her.transform;
            var lamps = so.FindProperty("lamps");
            lamps.arraySize = 3;
            lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            lamps.GetArrayElementAtIndex(1).objectReferenceValue = wall;
            lamps.GetArrayElementAtIndex(2).objectReferenceValue = fill;
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

            note.AppendFormat("モニターの映り込み: 正面の画面（{0}）だけ、座った正面の見下ろし {1:0.0} 度、写す範囲 {2}、写しの右腕のボーン {3} 本",
                front.parent != null ? front.parent.name : front.name, ss.FindProperty("seatPitch").floatValue, ReflectionExtent, armBones.Count).AppendLine();
            return true;
        }

        /// <summary>映り込みの写しの、髪をなでつけた形の mesh（体の mesh の写しで、髪の点だけを動かした物）</summary>
        public const string MirrorHeadMesh = "Assets/Models/generated/HeadMirror_Protagonist.asset";
        /// <summary>目の高さからこの下がり（m）より下の髪を、後ろで結んだ所へ寄せる。上ほど元の形に残す</summary>
        static readonly Vector2 SlickBand = new Vector2(0.0f, 0.03f);
        /// <summary>頭皮から髪の外側までの厚み（m）。耳より上の髪は、この厚みまで頭に沿わせる</summary>
        const float SlickThick = 0.006f;
        /// <summary>頭の後ろで髪を結んだ所の、頭の真ん中からの離れ（後ろへ、下へ。m）と、そこへ寄せる髪の縮め方</summary>
        static readonly Vector2 TieOffset = new Vector2(0.085f, 0.03f);
        const float TieGather = 0.12f;
        /// <summary>頭の絵のこの明るさより暗い所を髪とみなす。目尻からこの横の離れ（m）より外か、目からこの奥（m）より後ろの点だけ</summary>
        const float SlickDark = 0.2f;
        const float SlickSide = 0.045f;
        const float SlickBack = 0.04f;

        /// <summary>
        /// 主人公の体の mesh の写しで、髪だけを耳の上から後ろへなでつけて結んだ形にした物（映り込みの頭の写しに使う）。
        /// 場面の主人公 her を写した体（撮る間だけ作って捨てる）を、ボーンの基の姿勢（取り込みで決めた T の字）に戻して測る。
        /// 目の高さより下の髪（耳にかかる所と顎の横に下がる房）は頭の後ろの結び目へ寄せ、
        /// 耳より上の髪は頭の丸みに沿わせて薄くする。骨と束ねた姿勢は元の mesh のまま（場面の主人公の骨でそのまま動く）。
        /// headMaterials は頭の影と同じ素材の並び（髪の面の組は名前に Hair を含む素材）。
        /// 前は主人公を一人組み直して（BuildRocketboxProtagonist.Build）測っていたが、あちらは差込口の mesh も書き直すので通らない
        /// </summary>
        static Mesh SlickedHair(GameObject her, Material[] headMaterials, StringBuilder note)
        {
            var hairSub = -1;
            for (var i = 0; i < headMaterials.Length; i++)
                if (headMaterials[i] != null && headMaterials[i].name.Contains("Hair")) hairSub = i;
            var headSub = -1;
            for (var i = 0; i < headMaterials.Length; i++)
                if (headMaterials[i] != null && i != hairSub && headMaterials[i].shader != null && headMaterials[i].shader.name != "HalfAware/Hidden") headSub = i;
            if (hairSub < 0 || headSub < 0) { note.AppendLine("頭の写しの髪か頭の面の組が見つからない"); return null; }
            var probe = Object.Instantiate(her);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var an = probe.GetComponent<Animator>();
                BasePose(an);
                var skin = SkinPoint.BodyOf(probe.transform);
                var source = skin.sharedMesh;
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                var world = baked.vertices;
                Object.DestroyImmediate(baked);
                for (var i = 0; i < world.Length; i++) world[i] = skin.transform.TransformPoint(world[i]);
                var eyes = BodyPoser.Eyes(an);
                var up = Vector3.up;
                var forward = Vector3.ProjectOnPlane(probe.transform.forward, up).normalized;
                // 頭の真ん中は目の 7 cm 奥、2 cm 上。頭皮の半径は、目より上の頭の面の点までの離れの中ほど
                var centre = eyes - forward * 0.07f + up * 0.02f;
                var radii = new List<float>();
                foreach (var i in source.GetTriangles(headSub))
                    if (Vector3.Dot(world[i] - eyes, up) > 0.01f) radii.Add((world[i] - centre).magnitude);
                radii.Sort();
                var skull = radii.Count > 0 ? radii[radii.Count / 2] : 0.085f;
                var tie = centre - forward * TieOffset.x - up * TieOffset.y;

                var hair = new HashSet<int>(source.GetTriangles(hairSub));
                // 頭の面にも髪の殻（ボブのかたまり）がある。頭の絵で暗い所（黒い髪）の点を髪とみなす。
                // 顔の真ん中（鼻の穴、唇、ほくろ）は拾わないよう、目尻より外か、目より奥の点だけ
                // 頭の絵は、URP の Lit なら mainTexture、主人公の顔の AnimeSkin なら _BaseMap
                var texture = headMaterials[headSub].mainTexture;
                if (texture == null && headMaterials[headSub].HasProperty("_BaseMap")) texture = headMaterials[headSub].GetTexture("_BaseMap");
                if (texture == null) note.AppendLine("頭の絵が無いので、頭の面の髪の殻はなでつけない");
                var right = Vector3.Cross(up, forward);
                if (texture != null)
                {
                    var png = new Texture2D(2, 2);
                    png.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(texture)));
                    var uv = source.uv;
                    foreach (var i in source.GetTriangles(headSub))
                    {
                        var d = world[i] - eyes;
                        if (Mathf.Abs(Vector3.Dot(d, right)) < SlickSide && Vector3.Dot(d, forward) > -SlickBack) continue;
                        var c = png.GetPixelBilinear(uv[i].x, uv[i].y);
                        if (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f < SlickDark) hair.Add(i);
                    }
                    Object.DestroyImmediate(png);
                }
                var moved = (Vector3[])world.Clone();
                foreach (var i in hair)
                {
                    var p = world[i];
                    var rel = p - centre;
                    var r = rel.magnitude;
                    // 頭に沿わせる（外へ膨らんだ所を、頭皮から SlickThick の所まで寄せる）
                    var hug = r > skull + SlickThick ? centre + rel / r * (skull + SlickThick + (r - skull - SlickThick) * 0.3f) : p;
                    // 目の高さより下は、後ろの結び目へ寄せる
                    var below = -Vector3.Dot(p - eyes, up);
                    var k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SlickBand.x, SlickBand.y, below));
                    moved[i] = Vector3.Lerp(hug, tie + (p - tie) * TieGather, k);
                }

                // 束ねた姿勢へ戻す（頂点ごとの、今の骨の混ぜ方の逆）
                var verts = source.vertices;
                var weights = source.boneWeights;
                var binds = source.bindposes;
                var bones = skin.bones;
                foreach (var i in hair)
                {
                    var w = weights[i];
                    var m = new Matrix4x4();
                    System.Action<int, float> add = (b, k) =>
                    {
                        if (k <= 0f) return;
                        var bm = bones[b].localToWorldMatrix * binds[b];
                        for (var e = 0; e < 16; e++) m[e] += bm[e] * k;
                    };
                    add(w.boneIndex0, w.weight0);
                    add(w.boneIndex1, w.weight1);
                    add(w.boneIndex2, w.weight2);
                    add(w.boneIndex3, w.weight3);
                    verts[i] = m.inverse.MultiplyPoint3x4(moved[i]);
                }
                var mesh = Object.Instantiate(source);
                mesh.name = "HeadMirror_Protagonist";
                mesh.vertices = verts;
                // 法線は、動かした髪の点だけ測り直す。ほかの点（顔）は元の mesh の法線のまま（顔の陰の付き方を変えない）
                var keepNormals = source.normals;
                mesh.RecalculateNormals();
                var normals = mesh.normals;
                if (keepNormals.Length == normals.Length)
                    for (var i = 0; i < normals.Length; i++)
                        if (!hair.Contains(i)) normals[i] = keepNormals[i];
                mesh.normals = normals;
                mesh.RecalculateBounds();
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MirrorHeadMesh);
                if (existing != null)
                {
                    // 在れば中身を丸ごと入れ替える（場面からの参照を切らず、元の mesh の面や uv を作り替えていても揃う）
                    EditorUtility.CopySerialized(mesh, existing);
                    existing.name = "HeadMirror_Protagonist";
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                    EditorUtility.SetDirty(mesh);
                }
                else AssetDatabase.CreateAsset(mesh, MirrorHeadMesh);
                AssetDatabase.SaveAssets();
                note.AppendFormat("映り込みの頭の写しの髪: {0} 点を、耳の上から後ろへなでつけた（頭皮の半径 {1:0.000} m）", hair.Count, skull).AppendLine();
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>体のボーンを、基の姿勢（Avatar の作りに残っている、取り込みで決めた T の字）へ戻す。根は動かさない</summary>
        static void BasePose(Animator an)
        {
            if (an == null || an.avatar == null) return;
            var base_ = new Dictionary<string, SkeletonBone>();
            foreach (var sk in an.avatar.humanDescription.skeleton) base_[sk.name] = sk;
            foreach (var t in an.GetComponentsInChildren<Transform>(true))
            {
                SkeletonBone sk;
                if (t == an.transform || !base_.TryGetValue(t.name, out sk)) continue;
                t.localPosition = sk.position;
                t.localRotation = sk.rotation;
                t.localScale = sk.scale;
            }
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
            m.SetFloat("_Saturation", ReflectionSaturation);
            m.SetVector("_Edge", new Vector4(0.06f, 0.05f, 0f, 0f));
            m.SetVector("_Focus", new Vector4(0.5f, 0.65f, 0.7f, 0.8f));
            m.SetFloat("_FocusFloor", 0.65f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
